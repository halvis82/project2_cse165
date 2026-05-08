using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class RaceManager : MonoBehaviour
{
    [SerializeField] private CheckpointTrack checkpointTrack;
    [SerializeField] private DroneController droneController;
    [SerializeField] private DroneCameraRig cameraRig;
    [SerializeField] private HandGestureFlightInput handInput;
    [SerializeField] private RaceHud hud;
    [SerializeField] private WayfindingSystem wayfindingSystem;
    [SerializeField] private RaceAudio raceAudio;
    [SerializeField] private float startCountdownSeconds = 3f;
    [SerializeField] private float crashCountdownSeconds = 3f;
    [SerializeField] private bool enableTrackEditor = true;
    [SerializeField] private string savedTrackFolderName = "TrainingTracks";
    [SerializeField] private bool enableGhostChampion = true;
    [SerializeField] private float ghostSampleRateHz = 90f;

    private int currentTargetIndex = 1;
    private int lastClearedIndex;
    private float elapsedSeconds;
    private float countdownRemaining;
    private float statusClearAt;
    private bool timerRunning;
    private bool finished;
    private bool crashResetInProgress;
    private bool trackEditorMode;
    private bool audioOnlyWayfindingMode;
    private bool ghostChampionVisible = true;
    private int savedTrackLoadIndex = -1;
    private float nextGhostSampleTime;
    private float lastGhostPoseTime;
    private Vector3 lastGhostPosePosition;
    private Quaternion lastGhostPoseRotation = Quaternion.identity;
    private bool hasGhostPreviousPose;
    private float bestGhostDurationSeconds = float.PositiveInfinity;
    private GameObject ghostChampionObject;
    private readonly List<GhostSample> currentRunSamples = new List<GhostSample>();
    private readonly List<GhostSample> bestRunSamples = new List<GhostSample>();

    public bool HasCurrentTarget => checkpointTrack != null && currentTargetIndex >= 0 && currentTargetIndex < checkpointTrack.Count;
    public Vector3 CurrentTargetPosition => checkpointTrack.GetCheckpoint(currentTargetIndex);
    public int CurrentTargetIndex => currentTargetIndex;
    public int CheckpointCount => checkpointTrack != null ? checkpointTrack.Count : 0;

    private struct GhostSample
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;

        public GhostSample(float time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }
    }

    public void SetReferences(
        CheckpointTrack track,
        DroneController controller,
        DroneCameraRig rig,
        HandGestureFlightInput input,
        RaceHud raceHud,
        WayfindingSystem wayfinding,
        RaceAudio audio)
    {
        checkpointTrack = track;
        droneController = controller;
        cameraRig = rig;
        handInput = input;
        hud = raceHud;
        wayfindingSystem = wayfinding;
        raceAudio = audio;
    }

    private void Start()
    {
        LoadBestGhostRun();
        EnsureGhostChampionVisual();
        BeginRaceSetup();
    }

    private void Update()
    {
        UpdateCountdown();
        UpdateTimer();
        UpdateGestures();
        UpdateCheckpointProgress();
        UpdateGhostRecording();
        UpdateGhostPlayback();
        UpdateHud();
    }

    public void BeginRaceSetup()
    {
        StopAllCoroutines();
        StartCoroutine(BeginRaceSetupRoutine());
    }

    private System.Collections.IEnumerator BeginRaceSetupRoutine()
    {
        if (checkpointTrack == null)
        {
            SetupFailed();
            yield break;
        }

        var loaded = false;
        yield return checkpointTrack.LoadTrackAsync(success => loaded = success);

        if (!loaded || checkpointTrack.Count < 2)
        {
            SetupFailed();
            yield break;
        }

        StartCurrentTrackRace("Ready", true);
    }

    private void SetupFailed()
    {
        SetStatus("Track needs at least 2 checkpoints.");
        if (droneController != null)
        {
            droneController.ControlsEnabled = false;
        }
    }

    public void HandleCrash()
    {
        if (trackEditorMode || finished || crashResetInProgress)
        {
            return;
        }

        crashResetInProgress = true;
        raceAudio?.PlayCrash();
        SetStatus("Crash reset");
        PlaceDroneAtCheckpoint(lastClearedIndex);
        StartCountdown(crashCountdownSeconds, timerRunning, "Recover");
    }

    private void UpdateCountdown()
    {
        if (countdownRemaining <= 0f)
        {
            return;
        }

        var previousCeil = Mathf.CeilToInt(countdownRemaining);
        countdownRemaining -= Time.deltaTime;
        var nextCeil = Mathf.CeilToInt(countdownRemaining);
        if (nextCeil > 0 && nextCeil != previousCeil)
        {
            raceAudio?.PlayCountdown();
        }

        if (countdownRemaining <= 0f)
        {
            countdownRemaining = 0f;
            crashResetInProgress = false;

            if (droneController != null && !finished)
            {
                droneController.ControlsEnabled = !trackEditorMode;
            }

            if (!timerRunning && !finished && !trackEditorMode)
            {
                timerRunning = true;
            }

            SetStatus("Go");
        }
    }

    private void UpdateTimer()
    {
        if (timerRunning && !finished)
        {
            elapsedSeconds += Time.deltaTime;
        }
    }

    private void UpdateGestures()
    {
        if (handInput == null)
        {
            return;
        }

        if (handInput.ViewModeCycleRequested)
        {
            handInput.ConsumeViewModeCycleRequest();
            cameraRig?.CycleMode();
            hud?.SetViewMode(cameraRig != null ? cameraRig.CurrentMode : DroneViewMode.FirstPerson);
        }

        if (handInput.TrackEditorToggleRequested)
        {
            handInput.ConsumeTrackEditorToggleRequest();
            ToggleTrackEditorMode();
        }

        if (handInput.TrackEditorAddCheckpointRequested)
        {
            handInput.ConsumeTrackEditorAddCheckpointRequest();
            AddTrackEditorCheckpoint();
        }

        if (handInput.TrackEditorSaveRequested)
        {
            handInput.ConsumeTrackEditorSaveRequest();
            SaveEditedTrack();
        }

        if (handInput.TrackEditorLoadNextRequested)
        {
            handInput.ConsumeTrackEditorLoadNextRequest();
            LoadNextSavedTrack();
        }

        if (handInput.AudioWayfindingToggleRequested)
        {
            handInput.ConsumeAudioWayfindingToggleRequest();
            ToggleAudioOnlyWayfinding();
        }

        if (handInput.GhostChampionToggleRequested)
        {
            handInput.ConsumeGhostChampionToggleRequest();
            ghostChampionVisible = !ghostChampionVisible;
            SetStatus(ghostChampionVisible ? "Ghost champion on" : "Ghost champion off");
        }
    }

    private void UpdateCheckpointProgress()
    {
        if (trackEditorMode ||
            finished ||
            countdownRemaining > 0f ||
            checkpointTrack == null ||
            checkpointTrack.Count < 2 ||
            droneController == null ||
            !HasCurrentTarget)
        {
            return;
        }

        var distance = Vector3.Distance(droneController.transform.position, CurrentTargetPosition);
        if (distance > checkpointTrack.ReachRadiusMeters)
        {
            return;
        }

        lastClearedIndex = currentTargetIndex;
        raceAudio?.PlayCheckpoint();

        if (currentTargetIndex >= checkpointTrack.Count - 1)
        {
            finished = true;
            timerRunning = false;
            droneController.ControlsEnabled = false;
            checkpointTrack.SetCurrentIndex(checkpointTrack.Count);
            raceAudio?.PlayFinish();
            if (!FinishGhostRecording())
            {
                SetStatus("Finished");
            }
            return;
        }

        currentTargetIndex++;
        checkpointTrack.SetCurrentIndex(currentTargetIndex);
        raceAudio?.SetWaypoint(CurrentTargetPosition);
        SetStatus($"Checkpoint {lastClearedIndex + 1}");
    }

    private void UpdateHud()
    {
        hud?.SetTimer(elapsedSeconds, finished);
        hud?.SetCountdown(countdownRemaining);

        if (Time.time > statusClearAt && statusClearAt > 0f && !finished && countdownRemaining <= 0f)
        {
            if (handInput != null && handInput.UsingEditorDebugInput)
            {
                hud?.SetStatus("Editor test: WASD, Q/E, Shift boost, C");
            }
            else
            {
                hud?.SetStatus(handInput != null && !handInput.HandsTracked
                    ? "Left pinch/trigger to fly"
                    : "Look steers. Right height climbs.");
            }
            statusClearAt = 0f;
        }

        if (cameraRig != null)
        {
            hud?.SetViewMode(cameraRig.CurrentMode);
        }

        if (droneController != null)
        {
            hud?.SetSpeed(droneController.CurrentSpeedMetersPerSecond);
        }
    }

    private void StartCurrentTrackRace(string status, bool applyPreferredStartingView)
    {
        if (checkpointTrack == null || checkpointTrack.Count < 2 || droneController == null)
        {
            SetupFailed();
            return;
        }

        trackEditorMode = false;
        currentTargetIndex = 1;
        lastClearedIndex = 0;
        elapsedSeconds = 0f;
        timerRunning = false;
        finished = false;
        crashResetInProgress = false;
        audioOnlyWayfindingMode = false;

        PlaceDroneAtCheckpoint(lastClearedIndex);
        checkpointTrack.SetCurrentIndex(currentTargetIndex);
        SetVisualWayfinding(true);
        if (applyPreferredStartingView)
        {
            ApplyPreferredStartingView();
        }

        raceAudio?.SetWaypoint(CurrentTargetPosition);
        ResetGhostRecordingState(true);
        StartCountdown(startCountdownSeconds, false, status);
    }

    private void StartCountdown(float seconds, bool keepTimerRunning, string status)
    {
        countdownRemaining = Mathf.Max(3f, seconds);
        if (droneController != null)
        {
            droneController.ControlsEnabled = false;
        }

        timerRunning = keepTimerRunning;
        raceAudio?.PlayCountdown();
        SetStatus(status);
    }

    private void PlaceDroneAtCheckpoint(int checkpointIndex)
    {
        if (checkpointTrack == null || droneController == null)
        {
            return;
        }

        var position = checkpointTrack.GetCheckpoint(checkpointIndex);
        var lookIndex = Mathf.Clamp(checkpointIndex + 1, 0, checkpointTrack.Count - 1);
        var horizontalForward = checkpointTrack.GetCheckpoint(lookIndex) - position;
        horizontalForward.y = 0f;
        if (horizontalForward.sqrMagnitude < 0.01f)
        {
            horizontalForward = Vector3.forward;
        }

#if UNITY_EDITOR
        var spawnRotation = Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
#else
        var spawnRotation = Quaternion.identity;
#endif
        droneController.Teleport(position, spawnRotation);
    }

    private void ToggleTrackEditorMode()
    {
        if (!enableTrackEditor)
        {
            SetStatus("Track editor disabled");
            return;
        }

        trackEditorMode = !trackEditorMode;
        if (trackEditorMode && checkpointTrack != null && droneController != null)
        {
            checkpointTrack.SetRuntimePositions(new[] { droneController.transform.position }, "new hand-edited track");
            currentTargetIndex = 0;
            lastClearedIndex = 0;
            countdownRemaining = 0f;
            timerRunning = false;
            finished = false;
            crashResetInProgress = false;
            ResetGhostRecordingState(false);
            droneController.ControlsEnabled = true;
        }
        else if (!trackEditorMode)
        {
            if (checkpointTrack != null && checkpointTrack.Count >= 2)
            {
                StartCurrentTrackRace("Edited track ready", false);
            }
            else if (droneController != null)
            {
                droneController.ControlsEnabled = false;
                SetStatus("Track edit needs 2 checkpoints.");
            }

            return;
        }

        SetStatus(trackEditorMode
            ? "Track edit: CP1 placed. R middle adds."
            : "Track edit off");
    }

    private void AddTrackEditorCheckpoint()
    {
        if (!trackEditorMode || checkpointTrack == null || droneController == null)
        {
            SetStatus("Enable track edit first");
            return;
        }

        if (!checkpointTrack.AppendRuntimeCheckpoint(droneController.transform.position))
        {
            SetStatus($"Track limit: {CheckpointTrack.MaxCheckpointCount}");
            return;
        }

        currentTargetIndex = checkpointTrack.Count > 1
            ? Mathf.Clamp(currentTargetIndex, 1, checkpointTrack.Count - 1)
            : 0;
        checkpointTrack.SetCurrentIndex(currentTargetIndex);
        SetStatus($"Added checkpoint {checkpointTrack.Count}");
    }

    private void SaveEditedTrack()
    {
        if (!trackEditorMode || checkpointTrack == null || checkpointTrack.Count < 2)
        {
            SetStatus("No edited track to save");
            return;
        }

        var folder = GetSavedTrackFolder();
        var path = Path.Combine(folder, $"track_{DateTime.Now:yyyyMMdd_HHmmss}.xyz");
        checkpointTrack.SaveCurrentTrackToXyz(path);
        SetStatus($"Saved track {Path.GetFileNameWithoutExtension(path)}");
    }

    private void LoadNextSavedTrack()
    {
        if (!enableTrackEditor || checkpointTrack == null)
        {
            return;
        }

        var files = GetSavedTrackFiles();
        if (files.Length == 0)
        {
            SetStatus("No saved tracks");
            return;
        }

        savedTrackLoadIndex = (savedTrackLoadIndex + 1) % files.Length;
        if (!checkpointTrack.TryLoadXyzFile(files[savedTrackLoadIndex]))
        {
            SetStatus("Saved track failed");
            return;
        }

        StartCurrentTrackRace($"Loaded {Path.GetFileNameWithoutExtension(files[savedTrackLoadIndex])}", false);
    }

    private void ToggleAudioOnlyWayfinding()
    {
        audioOnlyWayfindingMode = !audioOnlyWayfindingMode;
        SetVisualWayfinding(!audioOnlyWayfindingMode);
        SetStatus(audioOnlyWayfindingMode ? "Audio-only wayfinding" : "Visual wayfinding on");
    }

    private void SetVisualWayfinding(bool visible)
    {
        wayfindingSystem?.SetVisualWayfindingEnabled(visible);
        hud?.SetWayfindingVisible(visible);
    }

    private string GetSavedTrackFolder()
    {
        return Path.Combine(Application.persistentDataPath, savedTrackFolderName);
    }

    private string[] GetSavedTrackFiles()
    {
        var folder = GetSavedTrackFolder();
        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.xyz").OrderBy(path => path).ToArray()
            : Array.Empty<string>();
    }

    private void UpdateGhostRecording()
    {
        if (!enableGhostChampion || droneController == null || !timerRunning || finished)
        {
            return;
        }

        var currentSample = CreateGhostSample(elapsedSeconds);
        if (!hasGhostPreviousPose)
        {
            currentRunSamples.Add(currentSample);
            lastGhostPoseTime = currentSample.Time;
            lastGhostPosePosition = currentSample.Position;
            lastGhostPoseRotation = currentSample.Rotation;
            nextGhostSampleTime = currentSample.Time + GetGhostSampleInterval();
            hasGhostPreviousPose = true;
            return;
        }

        var interval = GetGhostSampleInterval();
        while (nextGhostSampleTime <= elapsedSeconds + 0.0001f)
        {
            var t = Mathf.InverseLerp(lastGhostPoseTime, elapsedSeconds, nextGhostSampleTime);
            currentRunSamples.Add(new GhostSample(
                nextGhostSampleTime,
                Vector3.Lerp(lastGhostPosePosition, currentSample.Position, t),
                Quaternion.Slerp(lastGhostPoseRotation, currentSample.Rotation, t)));
            nextGhostSampleTime += interval;
        }

        lastGhostPoseTime = currentSample.Time;
        lastGhostPosePosition = currentSample.Position;
        lastGhostPoseRotation = currentSample.Rotation;
    }

    private void ResetGhostRecordingState(bool seedStartPose)
    {
        currentRunSamples.Clear();
        nextGhostSampleTime = 0f;
        lastGhostPoseTime = 0f;
        lastGhostPosePosition = Vector3.zero;
        lastGhostPoseRotation = Quaternion.identity;
        hasGhostPreviousPose = false;

        if (!seedStartPose || droneController == null)
        {
            return;
        }

        var startSample = CreateGhostSample(0f);
        currentRunSamples.Add(startSample);
        lastGhostPoseTime = startSample.Time;
        lastGhostPosePosition = startSample.Position;
        lastGhostPoseRotation = startSample.Rotation;
        nextGhostSampleTime = GetGhostSampleInterval();
        hasGhostPreviousPose = true;
    }

    private GhostSample CreateGhostSample(float time)
    {
        return new GhostSample(time, droneController.transform.position, GetGhostRotation());
    }

    private float GetGhostSampleInterval()
    {
        return 1f / Mathf.Max(1f, ghostSampleRateHz);
    }

    private Quaternion GetGhostRotation()
    {
        if (droneController == null)
        {
            return Quaternion.identity;
        }

        var direction = droneController.CurrentMoveDirection;
        if (direction.sqrMagnitude < 0.001f)
        {
            return droneController.transform.rotation;
        }

        direction.Normalize();
        var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
            ? Vector3.forward
            : Vector3.up;
        return Quaternion.LookRotation(direction, up);
    }

    private bool FinishGhostRecording()
    {
        if (!enableGhostChampion || droneController == null)
        {
            return false;
        }

        if (currentRunSamples.Count == 0 ||
            currentRunSamples[currentRunSamples.Count - 1].Time < elapsedSeconds - 0.0001f)
        {
            currentRunSamples.Add(CreateGhostSample(elapsedSeconds));
        }

        if (currentRunSamples.Count < 2 || elapsedSeconds >= bestGhostDurationSeconds)
        {
            return false;
        }

        bestGhostDurationSeconds = elapsedSeconds;
        bestRunSamples.Clear();
        bestRunSamples.AddRange(currentRunSamples);
        SaveBestGhostRun();
        SetStatus("Finished: new ghost champion");
        return true;
    }

    private void UpdateGhostPlayback()
    {
        if (ghostChampionObject == null)
        {
            return;
        }

        var visible = enableGhostChampion &&
                      ghostChampionVisible &&
                      timerRunning &&
                      bestRunSamples.Count >= 2 &&
                      !finished;
        ghostChampionObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        var sample = SampleGhost(elapsedSeconds);
        ghostChampionObject.transform.SetPositionAndRotation(sample.Position, sample.Rotation);
    }

    private GhostSample SampleGhost(float time)
    {
        if (bestRunSamples.Count == 0)
        {
            return new GhostSample(0f, Vector3.zero, Quaternion.identity);
        }

        if (time <= bestRunSamples[0].Time)
        {
            return bestRunSamples[0];
        }

        for (var i = 1; i < bestRunSamples.Count; i++)
        {
            var next = bestRunSamples[i];
            if (time > next.Time)
            {
                continue;
            }

            var previous = bestRunSamples[i - 1];
            var t = Mathf.InverseLerp(previous.Time, next.Time, time);
            return new GhostSample(
                time,
                Vector3.Lerp(previous.Position, next.Position, t),
                Quaternion.Slerp(previous.Rotation, next.Rotation, t));
        }

        return bestRunSamples[bestRunSamples.Count - 1];
    }

    private void EnsureGhostChampionVisual()
    {
        if (ghostChampionObject != null)
        {
            return;
        }

        ghostChampionObject = new GameObject("Ghost Champion");
        ghostChampionObject.SetActive(false);
        var material = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.2f, 1f, 1f, 0.28f)
        };
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        CreateGhostPart("Body", PrimitiveType.Cube, Vector3.zero, new Vector3(0.9f, 0.24f, 1.2f), material);
        CreateGhostPart("Nose", PrimitiveType.Cube, new Vector3(0f, 0f, 0.75f), new Vector3(0.35f, 0.18f, 0.35f), material);
        CreateGhostPart("Left Wing", PrimitiveType.Cube, new Vector3(-0.75f, 0f, 0f), new Vector3(0.8f, 0.08f, 0.25f), material);
        CreateGhostPart("Right Wing", PrimitiveType.Cube, new Vector3(0.75f, 0f, 0f), new Vector3(0.8f, 0.08f, 0.25f), material);
    }

    private void CreateGhostPart(string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(ghostChampionObject.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        var colliderComponent = part.GetComponent<Collider>();
        if (colliderComponent != null)
        {
            Destroy(colliderComponent);
        }

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }
    }

    private string GetGhostPath()
    {
        return Path.Combine(Application.persistentDataPath, "ghost_champion.tsv");
    }

    private void SaveBestGhostRun()
    {
        var path = GetGhostPath();
        using (var writer = new StreamWriter(path, false))
        {
            writer.WriteLine(bestGhostDurationSeconds.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < bestRunSamples.Count; i++)
            {
                var sample = bestRunSamples[i];
                writer.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.000000}\t{1:0.000000}\t{2:0.000000}\t{3:0.000000}\t{4:0.000000}\t{5:0.000000}\t{6:0.000000}\t{7:0.000000}",
                    sample.Time,
                    sample.Position.x,
                    sample.Position.y,
                    sample.Position.z,
                    sample.Rotation.x,
                    sample.Rotation.y,
                    sample.Rotation.z,
                    sample.Rotation.w));
            }
        }
    }

    private void LoadBestGhostRun()
    {
        var path = GetGhostPath();
        if (!File.Exists(path))
        {
            return;
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length < 3 || !float.TryParse(lines[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var loadedDurationSeconds))
        {
            return;
        }

        bestRunSamples.Clear();
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split('\t');
            if (parts.Length < 8)
            {
                continue;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ||
                !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var qx) ||
                !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var qy) ||
                !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var qz) ||
                !float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var qw))
            {
                continue;
            }

            bestRunSamples.Add(new GhostSample(time, new Vector3(x, y, z), new Quaternion(qx, qy, qz, qw)));
        }

        if (bestRunSamples.Count < 2)
        {
            bestRunSamples.Clear();
            bestGhostDurationSeconds = float.PositiveInfinity;
            return;
        }

        bestGhostDurationSeconds = loadedDurationSeconds;
    }

    private void SetStatus(string message)
    {
        hud?.SetStatus(message);
        statusClearAt = Time.time + 1.6f;
    }

    private void ApplyPreferredStartingView()
    {
        if (cameraRig == null)
        {
            return;
        }

#if UNITY_EDITOR
        cameraRig.ApplyMode(DroneViewMode.ThirdPerson);
#else
        cameraRig.ApplyMode(DroneViewMode.FirstPerson);
#endif
        hud?.SetViewMode(cameraRig.CurrentMode);
    }
}
