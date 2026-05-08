using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed class CheckpointTrack : MonoBehaviour
{
    public const float RequiredRadiusMeters = 30f * 0.3048f;
    public const int MaxCheckpointCount = 100;

    [Header("Loading")]
    [SerializeField] private TextAsset fallbackTrack;
    [SerializeField] private string competitionFileName = "competition.xyz";
    [SerializeField] private string trackManifestFileName = "track_manifest.txt";
    [SerializeField] private string optionalAbsoluteTrackPath = "";
    [SerializeField] private bool preferSceneCheckpointsWhenNoCompetitionFile = true;

    [Header("Visualization")]
    [SerializeField] private Transform checkpointRoot;
    [SerializeField] private GameObject checkpointPrefab;
    [SerializeField] private float visualRadiusMeters = RequiredRadiusMeters;
    [SerializeField] private Color pendingColor = new Color(0.2f, 0.75f, 1f, 0.18f);
    [SerializeField] private Color currentColor = new Color(1f, 0.9f, 0.18f, 0.35f);
    [SerializeField] private Color clearedColor = new Color(0.2f, 1f, 0.45f, 0.12f);

    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly List<CheckpointVisual> visuals = new List<CheckpointVisual>();

    public IReadOnlyList<Vector3> Positions => positions;
    public float ReachRadiusMeters => RequiredRadiusMeters;
    public int Count => positions.Count;
    public string LastLoadedSource { get; private set; } = "";

    public bool LoadTrack()
    {
        positions.Clear();

        var content = TryReadCompetitionTrack(out var source);
        if (!string.IsNullOrWhiteSpace(content))
        {
            AddParsedPositions(content, source);
        }

        if (positions.Count == 0 && TryUseSceneCheckpoints(out source))
        {
            LastLoadedSource = source;
            Debug.Log($"Loaded {positions.Count} checkpoints from {source}.");
            RebuildVisuals();
            return true;
        }

        if (positions.Count == 0 && fallbackTrack != null)
        {
            source = fallbackTrack.name;
            AddParsedPositions(fallbackTrack.text, source);
        }

        if (positions.Count == 0)
        {
            Debug.LogError("No checkpoints loaded. Add a .xyz file to StreamingAssets or assign a fallback TextAsset.");
            return false;
        }

        LastLoadedSource = source;
        Debug.Log($"Loaded {positions.Count} checkpoints from {source}.");
        RebuildVisuals();
        return true;
    }

    public System.Collections.IEnumerator LoadTrackAsync(Action<bool> completed)
    {
        positions.Clear();

        var content = TryReadCompetitionTrack(out var source);

        if (string.IsNullOrWhiteSpace(content))
        {
            if (LooksLikeUrl(Application.streamingAssetsPath))
            {
                yield return TryReadStreamingAssetsUrlTrack(result =>
                {
                    content = result.Content;
                    source = result.Source;
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            AddParsedPositions(content, source);
        }

        if (positions.Count == 0 && TryUseSceneCheckpoints(out source))
        {
            LastLoadedSource = source;
            Debug.Log($"Loaded {positions.Count} checkpoints from {source}.");
            RebuildVisuals();
            completed?.Invoke(true);
            yield break;
        }

        if (positions.Count == 0 && fallbackTrack != null)
        {
            source = fallbackTrack.name;
            AddParsedPositions(fallbackTrack.text, source);
        }

        if (positions.Count == 0)
        {
            Debug.LogError("No checkpoints loaded. Add competition.xyz to persistent data, StreamingAssets, or assign a fallback TextAsset.");
            completed?.Invoke(false);
            yield break;
        }

        LastLoadedSource = source;
        Debug.Log($"Loaded {positions.Count} checkpoints from {source}.");
        RebuildVisuals();
        completed?.Invoke(true);
    }

    public void SetFallbackTrack(TextAsset track)
    {
        fallbackTrack = track;
    }

    public void SetCheckpointPrefab(GameObject prefab)
    {
        checkpointPrefab = prefab;
    }

    public void SetCheckpointRoot(Transform root)
    {
        checkpointRoot = root;
    }

    public Vector3 GetCheckpoint(int index)
    {
        return positions[Mathf.Clamp(index, 0, positions.Count - 1)];
    }

    public void SetCurrentIndex(int currentIndex)
    {
        for (var i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] == null)
            {
                continue;
            }

            if (i < currentIndex)
            {
                visuals[i].SetState(clearedColor, false, visualRadiusMeters * 0.5f);
            }
            else if (i == currentIndex)
            {
                visuals[i].SetState(currentColor, true, visualRadiusMeters * 2f);
            }
            else
            {
                visuals[i].SetState(pendingColor, false, visualRadiusMeters * 0.9f);
            }
        }
    }

    private void SetRuntimePositions(IEnumerable<Vector3> newPositions, string source)
    {
        positions.Clear();
        positions.AddRange(newPositions.Take(MaxCheckpointCount));
        LastLoadedSource = source;
        Debug.Log($"Loaded {positions.Count} checkpoints from {source}.");
        RebuildVisuals();
    }

    public bool TryLoadXyzFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var parsed = XyzTrackParser.Parse(File.ReadAllText(path));
        if (parsed.Count > MaxCheckpointCount)
        {
            Debug.LogWarning($"Track {path} has {parsed.Count} checkpoints. Using the first {MaxCheckpointCount}.");
        }

        var loaded = parsed.Take(MaxCheckpointCount).ToList();
        if (loaded.Count < 2)
        {
            Debug.LogWarning($"Track file needs at least two checkpoints: {path}");
            return false;
        }

        SetRuntimePositions(loaded, path);
        return true;
    }


    private string TryReadCompetitionTrack(out string source)
    {
        source = "";
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(optionalAbsoluteTrackPath))
        {
            if (File.Exists(optionalAbsoluteTrackPath))
            {
                source = optionalAbsoluteTrackPath;
                return File.ReadAllText(optionalAbsoluteTrackPath);
            }
        }

        AddTrackCandidates(candidates, Application.persistentDataPath);

        if (!LooksLikeUrl(Application.streamingAssetsPath))
        {
            AddTrackCandidates(candidates, Application.streamingAssetsPath);
        }

        foreach (var candidate in PrioritizeTrackCandidates(candidates))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            source = candidate;
            return File.ReadAllText(candidate);
        }

        return "";
    }

    private void AddTrackCandidates(List<string> candidates, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        candidates.Add(Path.Combine(folder, competitionFileName));

        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var path in Directory.GetFiles(folder, "*.xyz", SearchOption.TopDirectoryOnly))
        {
            candidates.Add(path);
        }
    }

    private IEnumerable<string> PrioritizeTrackCandidates(IEnumerable<string> candidates)
    {
        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .OrderByDescending(path => string.Equals(Path.GetFileName(path), competitionFileName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => string.Equals(Path.GetFileName(path), "sample_track.xyz", StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private System.Collections.IEnumerator TryReadStreamingAssetsUrlTrack(Action<TrackReadResult> completed)
    {
        var triedNames = new List<string> { competitionFileName };
        var manifestUrl = CombineStreamingAssetUrl(Application.streamingAssetsPath, trackManifestFileName);
        using (var request = UnityWebRequest.Get(manifestUrl))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                foreach (var line in request.downloadHandler.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = line.Trim();
                    if (name.EndsWith(".xyz", StringComparison.OrdinalIgnoreCase))
                    {
                        triedNames.Add(name);
                    }
                }
            }
        }

        foreach (var name in triedNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var trackUrl = CombineStreamingAssetUrl(Application.streamingAssetsPath, name);
            using (var request = UnityWebRequest.Get(trackUrl))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    completed?.Invoke(new TrackReadResult(request.downloadHandler.text, trackUrl));
                    yield break;
                }
            }
        }

        completed?.Invoke(default);
    }

    private static string CombineStreamingAssetUrl(string root, string fileName)
    {
        return root.TrimEnd('/') + "/" + fileName.TrimStart('/');
    }

    private void AddParsedPositions(string content, string source)
    {
        var parsed = XyzTrackParser.Parse(content);
        if (parsed.Count > MaxCheckpointCount)
        {
            Debug.LogWarning($"Track {source} has {parsed.Count} checkpoints. Using the first {MaxCheckpointCount}.");
        }

        positions.AddRange(parsed.Take(MaxCheckpointCount));
    }

    private bool TryUseSceneCheckpoints(out string source)
    {
        source = "scene checkpoint objects";
        if (!preferSceneCheckpointsWhenNoCompetitionFile || checkpointRoot == null || checkpointRoot.childCount < 2)
        {
            return false;
        }

        for (var i = 0; i < checkpointRoot.childCount; i++)
        {
            positions.Add(checkpointRoot.GetChild(i).position);
        }

        return positions.Count >= 2;
    }

    private static bool LooksLikeUrl(string path)
    {
        return path.Contains("://") || path.Contains("jar:");
    }

    private struct TrackReadResult
    {
        public readonly string Content;
        public readonly string Source;

        public TrackReadResult(string content, string source)
        {
            Content = content;
            Source = source;
        }
    }

    private void RebuildVisuals()
    {
        if (checkpointRoot == null)
        {
            checkpointRoot = transform;
        }

        for (var i = checkpointRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(checkpointRoot.GetChild(i).gameObject);
        }

        visuals.Clear();
        for (var i = 0; i < positions.Count; i++)
        {
            var checkpoint = checkpointPrefab != null
                ? Instantiate(checkpointPrefab, positions[i], Quaternion.identity, checkpointRoot)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            checkpoint.name = $"Checkpoint_{i + 1:00}";
            checkpoint.transform.SetParent(checkpointRoot, true);
            checkpoint.transform.position = positions[i];

            foreach (var colliderComponent in checkpoint.GetComponentsInChildren<Collider>())
            {
                Destroy(colliderComponent);
            }

            var renderer = checkpoint.GetComponentInChildren<Renderer>();
            var spinner = checkpoint.transform;
            var visual = checkpoint.GetComponent<CheckpointVisual>() ?? checkpoint.AddComponent<CheckpointVisual>();
            visual.Initialize(renderer, spinner);
            visuals.Add(visual);
        }
    }
}
