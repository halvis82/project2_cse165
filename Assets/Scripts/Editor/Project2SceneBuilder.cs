using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.Android;

public static class Project2SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Project2Race.unity";
    private const string ModelPath = "Assets/MachuPicchu/machu_picchu_2.obj";
    private const string SampleTrackPath = "Assets/Tracks/sample_track.xyz";
    private const string CheckpointPrefabPath = "Assets/Checkpoint.prefab";
    private const string MaterialFolder = "Assets/Materials";
    private const float InchesToMeters = 1f / 39.37f;
    private const int MaxCheckpointCount = 100;

    [MenuItem("CSE165 Project 2/Rebuild Race Scene")]
    public static void RebuildRaceScene()
    {
        BuildRaceScene(overwriteExisting: true);
    }

    [MenuItem("CSE165 Project 2/Frame Scene View At Race Start")]
    public static void FrameSceneViewAtRaceStart()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("Open a Scene view first.");
            return;
        }

        if (!TryGetSampleTrackPoints(out var points) || points.Count < 2)
        {
            Debug.LogWarning("Could not read sample track for scene framing.");
            return;
        }

        var start = points[0];
        var forward = points[1] - points[0];
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        sceneView.LookAt(start + new Vector3(0f, 8f, -12f), Quaternion.LookRotation(forward.normalized, Vector3.up), 35f, false);
        sceneView.Repaint();
    }

    [MenuItem("CSE165 Project 2/Save Visible Checkpoints To XYZ")]
    public static void SaveVisibleCheckpointsToXyz()
    {
        var checkpointRoot = GameObject.Find("Checkpoints");
        if (checkpointRoot == null || checkpointRoot.transform.childCount < 2)
        {
            Debug.LogError("Could not find at least 2 checkpoint objects under a Checkpoints GameObject.");
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < checkpointRoot.transform.childCount; i++)
        {
            var positionMeters = checkpointRoot.transform.GetChild(i).position;
            var xInches = positionMeters.x / InchesToMeters;
            var yInches = positionMeters.y / InchesToMeters;
            var zInches = positionMeters.z / InchesToMeters;

            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###}\n",
                xInches,
                yInches,
                zInches);
        }

        var trackText = builder.ToString();
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), SampleTrackPath), trackText);

        var streamingPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/StreamingAssets/sample_track.xyz");
        Directory.CreateDirectory(Path.GetDirectoryName(streamingPath));
        File.WriteAllText(streamingPath, trackText);

        SnapDroneToFirstVisibleCheckpoint();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        Debug.Log($"Saved {checkpointRoot.transform.childCount} visible checkpoints to sample_track.xyz.");
    }

    [MenuItem("CSE165 Project 2/Import Competition XYZ...")]
    public static void ImportCompetitionXyz()
    {
        var sourcePath = EditorUtility.OpenFilePanel("Import Competition XYZ", "", "xyz");
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        var content = File.ReadAllText(sourcePath);
        var checkpoints = XyzTrackParser.Parse(content);
        if (checkpoints.Count < 2)
        {
            Debug.LogError($"Competition track needs at least 2 valid checkpoints: {sourcePath}");
            return;
        }

        if (checkpoints.Count > MaxCheckpointCount)
        {
            Debug.LogWarning($"Competition track has {checkpoints.Count} checkpoints. Runtime will use the first {MaxCheckpointCount}.");
        }

        var streamingFolder = Path.Combine(Directory.GetCurrentDirectory(), "Assets/StreamingAssets");
        Directory.CreateDirectory(streamingFolder);
        var destinationPath = Path.Combine(streamingFolder, "competition.xyz");
        File.Copy(sourcePath, destinationPath, true);
        var originalFileName = Path.GetFileName(sourcePath);
        if (!string.Equals(originalFileName, "competition.xyz", System.StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, Path.Combine(streamingFolder, originalFileName), true);
        }

        WriteTrackManifest(streamingFolder, originalFileName);
        AssetDatabase.Refresh();
        Debug.Log($"Imported competition track: {sourcePath} -> Assets/StreamingAssets/competition.xyz");
    }

    [MenuItem("CSE165 Project 2/Clear Competition XYZ")]
    public static void ClearCompetitionXyz()
    {
        var competitionPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/StreamingAssets/competition.xyz");
        if (!File.Exists(competitionPath))
        {
            Debug.Log("No Assets/StreamingAssets/competition.xyz file exists.");
            return;
        }

        File.Delete(competitionPath);
        var streamingFolder = Path.GetDirectoryName(competitionPath);
        var manifestPath = Path.Combine(streamingFolder, "track_manifest.txt");
        if (File.Exists(manifestPath))
        {
            foreach (var line in File.ReadAllLines(manifestPath))
            {
                var fileName = line.Trim();
                if (string.IsNullOrWhiteSpace(fileName) ||
                    string.Equals(fileName, "competition.xyz", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "sample_track.xyz", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var importedPath = Path.Combine(streamingFolder, fileName);
                if (File.Exists(importedPath))
                {
                    File.Delete(importedPath);
                }

                var importedMetaPath = importedPath + ".meta";
                if (File.Exists(importedMetaPath))
                {
                    File.Delete(importedMetaPath);
                }
            }

            File.Delete(manifestPath);
        }

        var metaPath = competitionPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        AssetDatabase.Refresh();
        Debug.Log("Removed Assets/StreamingAssets/competition.xyz. The scene/fallback track will load again.");
    }

    private static void WriteTrackManifest(string streamingFolder, string importedFileName)
    {
        var manifestPath = Path.Combine(streamingFolder, "track_manifest.txt");
        var builder = new StringBuilder();
        builder.AppendLine("competition.xyz");
        if (!string.IsNullOrWhiteSpace(importedFileName) &&
            !string.Equals(importedFileName, "competition.xyz", System.StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(importedFileName);
        }

        File.WriteAllText(manifestPath, builder.ToString());
    }

    [MenuItem("CSE165 Project 2/Snap Drone To Checkpoint 01")]
    public static void SnapDroneToFirstVisibleCheckpoint()
    {
        var checkpointRoot = GameObject.Find("Checkpoints");
        var drone = GameObject.Find("Drone Rig");
        if (checkpointRoot == null || checkpointRoot.transform.childCount < 2 || drone == null)
        {
            Debug.LogWarning("Need Checkpoints with at least 2 children and a Drone Rig in the open scene.");
            return;
        }

        var start = checkpointRoot.transform.GetChild(0).position;
        var next = checkpointRoot.transform.GetChild(1).position;
        var forward = next - start;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Undo.RecordObject(drone.transform, "Snap Drone To Checkpoint 01");
        drone.transform.position = start;
        drone.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = drone;
        Debug.Log("Snapped Drone Rig to Checkpoint_01.");
    }

    private static void BuildRaceScene(bool overwriteExisting)
    {
        if (File.Exists(ScenePath) && !overwriteExisting)
        {
            return;
        }

        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.2f;

        var terrainMaterial = CreateMaterial("MachuPicchuRuntime", new Color(0.82f, 0.78f, 0.68f, 1f), false);
        var checkpointMaterial = CreateMaterial("CheckpointTransparent", new Color(0.2f, 0.75f, 1f, 0.22f), true);
        var lineMaterial = CreateMaterial("WaypointLine", new Color(1f, 0.88f, 0.15f, 1f), false);
        var droneMaterial = CreateMaterial("DroneBody", new Color(0.16f, 0.19f, 0.22f, 1f), false);
        var cockpitMaterial = CreateMaterial("CockpitGlass", new Color(0.08f, 0.35f, 0.5f, 0.32f), true);
        var accentMaterial = CreateMaterial("RaceAccent", new Color(1f, 0.82f, 0.16f, 1f), false);

        CreateLighting();

        var machuPicchu = CreateMachuPicchu(terrainMaterial);
        var checkpointPrefab = CreateCheckpointPrefab(checkpointMaterial);
        var checkpointRoot = new GameObject("Checkpoints").transform;

        var droneRoot = new GameObject("Drone Rig");
        droneRoot.transform.position = Vector3.zero;
        droneRoot.transform.rotation = Quaternion.identity;

        var body = droneRoot.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var trigger = droneRoot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.7f;

        var input = droneRoot.AddComponent<HandGestureFlightInput>();
        input.SetTrackingRoot(droneRoot.transform);

        var controller = droneRoot.AddComponent<DroneController>();
        controller.SetInput(input);

        var cameraRig = droneRoot.AddComponent<DroneCameraRig>();
        var collisionReporter = droneRoot.AddComponent<DroneCollisionReporter>();

        var viewAnchor = new GameObject("View Anchor").transform;
        viewAnchor.SetParent(droneRoot.transform, false);

        var mainCameraObject = new GameObject("Main Camera");
        mainCameraObject.tag = "MainCamera";
        mainCameraObject.transform.SetParent(viewAnchor, false);
        var mainCamera = mainCameraObject.AddComponent<Camera>();
        mainCamera.nearClipPlane = 0.03f;
        mainCamera.farClipPlane = 2000f;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCameraObject.AddComponent<AudioListener>();
        mainCameraObject.AddComponent<HmdPoseDriver>();

        var droneVisual = CreateDroneVisual(droneRoot.transform, droneMaterial, accentMaterial);
        var cockpitVisual = CreateCockpitVisual(viewAnchor, cockpitMaterial, droneMaterial, accentMaterial);
        cameraRig.SetReferences(viewAnchor, cockpitVisual, droneVisual);
        cameraRig.ApplyMode(DroneViewMode.FirstPerson);

        var hud = CreateHud(mainCameraObject.transform);

        var raceManagerObject = new GameObject("Race Manager");
        var track = raceManagerObject.AddComponent<CheckpointTrack>();
        track.SetFallbackTrack(AssetDatabase.LoadAssetAtPath<TextAsset>(SampleTrackPath));
        track.SetCheckpointPrefab(checkpointPrefab);
        track.SetCheckpointRoot(checkpointRoot);
        BuildEditorCheckpointPreview(checkpointRoot, checkpointPrefab);

        var lineObject = new GameObject("World Waypoint Line");
        var line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.45f;
        line.endWidth = 0.12f;
        line.material = lineMaterial;
        line.textureMode = LineTextureMode.Stretch;

        var arrow = CreateWorldArrow(accentMaterial);

        var audioObject = new GameObject("Race Audio");
        var motorSource = audioObject.AddComponent<AudioSource>();
        motorSource.playOnAwake = false;
        var effectsSource = audioObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;
        var waypointSourceObject = new GameObject("Spatial Waypoint Audio");
        var waypointSource = waypointSourceObject.AddComponent<AudioSource>();
        waypointSource.playOnAwake = false;

        var raceAudio = audioObject.AddComponent<RaceAudio>();
        raceAudio.SetReferences(controller, motorSource, effectsSource, waypointSource);

        var manager = raceManagerObject.AddComponent<RaceManager>();
        var wayfinding = raceManagerObject.AddComponent<WayfindingSystem>();
        manager.SetReferences(track, controller, cameraRig, input, hud, wayfinding, raceAudio);
        wayfinding.SetReferences(manager, droneRoot.transform, mainCamera, line, arrow, hud);
        collisionReporter.SetRaceManager(manager);

        PlaceEditorRigAtSampleStart(droneRoot.transform);

        Selection.activeGameObject = raceManagerObject;
        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScene(ScenePath);
        ConfigureQuestOpenXR();
        AssetDatabase.SaveAssets();
        Debug.Log($"Built CSE 165 Project 2 scene at {ScenePath}. Machu Picchu root: {machuPicchu.name}");
    }

    [MenuItem("CSE165 Project 2/Configure Quest OpenXR")]
    public static void ConfigureQuestOpenXR()
    {
        Directory.CreateDirectory("Assets/XR");
        Directory.CreateDirectory("Assets/XR/Settings");

        PlayerSettings.productName = "CSE165 Drone Wayfinding";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "edu.ucsd.cse165.project2.dronewayfinding");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        ConfigureAndroidExternalTools();

        var buildTargetSettings = GetOrCreateXRBuildTargetSettings();
        if (!buildTargetSettings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            buildTargetSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        if (!buildTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        var androidSettings = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
        androidSettings.InitManagerOnStart = true;

        var managerSettings = buildTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        managerSettings.automaticLoading = true;
        managerSettings.automaticRunning = true;
        XRPackageMetadataStore.AssignLoader(managerSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);

        var openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (openXrSettings != null)
        {
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            EnableFeature<MetaQuestFeature>(openXrSettings);
            EnableFeature<HandTracking>(openXrSettings);
            EnableFeature<MetaHandTrackingAim>(openXrSettings);
            EditorUtility.SetDirty(openXrSettings);
        }

        EditorUtility.SetDirty(buildTargetSettings);
        EditorUtility.SetDirty(androidSettings);
        EditorUtility.SetDirty(managerSettings);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureAndroidExternalTools()
    {
        var androidPlayerRoot = FindAndroidPlayerRoot();
        if (string.IsNullOrWhiteSpace(androidPlayerRoot))
        {
            Debug.LogWarning("Could not locate Unity AndroidPlayer external tools folder.");
            return;
        }

        SetAndroidToolPath(Path.Combine(androidPlayerRoot, "OpenJDK"), path => AndroidExternalToolsSettings.jdkRootPath = path, "JDK");
        SetAndroidToolPath(Path.Combine(androidPlayerRoot, "SDK"), path => AndroidExternalToolsSettings.sdkRootPath = path, "SDK");
        SetAndroidToolPath(Path.Combine(androidPlayerRoot, "NDK"), path => AndroidExternalToolsSettings.ndkRootPath = path, "NDK");
        SetAndroidToolPath(Path.Combine(androidPlayerRoot, "Tools/gradle"), path => AndroidExternalToolsSettings.gradlePath = path, "Gradle");
    }

    private static string FindAndroidPlayerRoot()
    {
        var candidates = new[]
        {
            Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer"),
            Path.Combine(Directory.GetParent(EditorApplication.applicationContentsPath)?.Parent?.FullName ?? "", "PlaybackEngines/AndroidPlayer")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return "";
    }

    private static void SetAndroidToolPath(string path, System.Action<string> setter, string toolName)
    {
        if (!Directory.Exists(path))
        {
            Debug.LogWarning($"Unity Android {toolName} path does not exist: {path}");
            return;
        }

        setter(path);
    }

    [MenuItem("CSE165 Project 2/Clean Android Build Cache")]
    public static void CleanAndroidBuildCache()
    {
        CleanAndroidBuildArtifacts();
        Debug.Log("Cleaned generated Android build cache.");
    }

    [MenuItem("CSE165 Project 2/Build And Run Quest")]
    public static void BuildAndRunQuest()
    {
        BuildQuestApk(autoRun: true);
    }

    [MenuItem("CSE165 Project 2/Build Quest APK")]
    public static void BuildQuestApk()
    {
        BuildQuestApk(autoRun: false);
    }

    private static void BuildQuestApk(bool autoRun)
    {
        ConfigureQuestOpenXR();
        SetBuildScene(ScenePath);
        if (Application.isBatchMode)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.SaveOpenScenes();
        }

        AssetDatabase.SaveAssets();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        var buildDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        Directory.CreateDirectory(buildDirectory);
        var apkPath = Path.Combine(buildDirectory, "Project2Race.apk");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apkPath,
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            options = autoRun
                ? BuildOptions.Development | BuildOptions.AutoRunPlayer
                : BuildOptions.Development
        });

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log(autoRun
                ? $"Built and launched Quest APK: {apkPath}"
                : $"Built Quest APK: {apkPath}");
        }
        else
        {
            Debug.LogError($"Quest build failed: {report.summary.result}");
        }
    }

    private static void CleanAndroidBuildArtifacts()
    {
        DeleteGeneratedPath("Library/Bee/artifacts/Android");
        DeleteGeneratedPath("Library/Bee/Android");
        DeleteGeneratedPath("Library/Il2cppBuildCache");
        DeleteGeneratedPath("Temp/StagingArea");
    }

    private static void DeleteGeneratedPath(string relativePath)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (Directory.Exists(path) || File.Exists(path))
        {
            FileUtil.DeleteFileOrDirectory(path);
        }
    }

    private static XRGeneralSettingsPerBuildTarget GetOrCreateXRBuildTargetSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var settings) && settings != null)
        {
            return settings;
        }

        var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
        if (guids.Length > 0)
        {
            settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (settings != null)
            {
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
                return settings;
            }
        }

        settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        AssetDatabase.CreateAsset(settings, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static void EnableFeature<TFeature>(OpenXRSettings settings) where TFeature : OpenXRFeature
    {
        var feature = settings.GetFeature<TFeature>();
        if (feature != null)
        {
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }
    }

    private static Material CreateMaterial(string name, Color color, bool transparent)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (transparent)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }
        else
        {
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateLighting()
    {
        var lightObject = new GameObject("Sun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

        var fillObject = new GameObject("Fill Light");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;
        fillObject.transform.rotation = Quaternion.Euler(15f, 130f, 0f);
    }

    private static GameObject CreateMachuPicchu(Material fallbackMaterial)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            var missing = new GameObject("Machu Picchu Missing Model");
            Debug.LogError($"Could not find {ModelPath}. Copy the downloaded OBJ and textures into Assets/MachuPicchu.");
            return missing;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        instance.name = "Machu Picchu Terrain";
        instance.transform.localScale = Vector3.one * InchesToMeters;
        instance.AddComponent<MachuPicchuTerrain>();

        foreach (var meshFilter in instance.GetComponentsInChildren<MeshFilter>())
        {
            var collider = meshFilter.gameObject.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;
        }

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (renderer.sharedMaterial == null)
            {
                renderer.sharedMaterial = fallbackMaterial;
            }
        }

        return instance;
    }

    private static GameObject CreateCheckpointPrefab(Material material)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Checkpoint";
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        var renderer = sphere.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        sphere.AddComponent<CheckpointVisual>().Initialize(renderer, sphere.transform);

        var prefab = PrefabUtility.SaveAsPrefabAsset(sphere, CheckpointPrefabPath);
        Object.DestroyImmediate(sphere);
        return prefab;
    }

    private static void PlaceEditorRigAtSampleStart(Transform droneRoot)
    {
        if (!TryGetSampleTrackPoints(out var points) || points.Count < 2)
        {
            return;
        }

        var start = points[0];
        var forward = points[1] - points[0];
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        droneRoot.position = start;
        droneRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static void BuildEditorCheckpointPreview(Transform checkpointRoot, GameObject checkpointPrefab)
    {
        if (!TryGetSampleTrackPoints(out var points))
        {
            return;
        }
        for (var i = checkpointRoot.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(checkpointRoot.GetChild(i).gameObject);
        }

        for (var i = 0; i < points.Count; i++)
        {
            var checkpoint = checkpointPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(checkpointPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            checkpoint.name = $"Checkpoint_{i + 1:00}";
            checkpoint.transform.SetParent(checkpointRoot, false);
            checkpoint.transform.position = points[i];

            var visual = checkpoint.GetComponent<CheckpointVisual>();
            if (visual != null)
            {
                if (i == 0)
                {
                    visual.SetState(new Color(0.2f, 1f, 0.45f, 0.16f), false, 2f);
                }
                else if (i == 1)
                {
                    visual.SetState(new Color(1f, 0.9f, 0.18f, 0.35f), true, 5f);
                }
                else
                {
                    visual.SetState(new Color(0.2f, 0.75f, 1f, 0.18f), false, 3f);
                }
            }
        }
    }

    private static bool TryGetSampleTrackPoints(out System.Collections.Generic.List<Vector3> points)
    {
        points = null;
        var absoluteTrackPath = Path.Combine(Directory.GetCurrentDirectory(), SampleTrackPath);
        if (!File.Exists(absoluteTrackPath))
        {
            return false;
        }

        points = XyzTrackParser.Parse(File.ReadAllText(absoluteTrackPath));
        return points != null && points.Count > 0;
    }

    private static GameObject CreateDroneVisual(Transform parent, Material bodyMaterial, Material accentMaterial)
    {
        var root = new GameObject("Visible Drone");
        root.transform.SetParent(parent, false);

        CreatePrimitive(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.85f, 0.25f, 1.1f), bodyMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Front Stripe", new Vector3(0f, 0.03f, 0.62f), new Vector3(0.5f, 0.08f, 0.08f), accentMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Arm X", Vector3.zero, new Vector3(2.6f, 0.08f, 0.08f), bodyMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Arm Z", Vector3.zero, new Vector3(0.08f, 0.08f, 2.6f), bodyMaterial);

        var rotorPositions = new[]
        {
            new Vector3(-1.25f, 0f, -1.25f),
            new Vector3(1.25f, 0f, -1.25f),
            new Vector3(-1.25f, 0f, 1.25f),
            new Vector3(1.25f, 0f, 1.25f)
        };

        for (var i = 0; i < rotorPositions.Length; i++)
        {
            var rotor = CreatePrimitive(root.transform, PrimitiveType.Cylinder, $"Rotor {i + 1}", rotorPositions[i], new Vector3(0.55f, 0.04f, 0.55f), accentMaterial);
            rotor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        root.SetActive(false);
        return root;
    }

    private static GameObject CreateCockpitVisual(Transform parent, Material glassMaterial, Material bodyMaterial, Material accentMaterial)
    {
        var root = new GameObject("Virtual Cockpit");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, -0.35f, 0.65f);

        CreatePrimitive(root.transform, PrimitiveType.Cube, "Dashboard", new Vector3(0f, -0.25f, 0.75f), new Vector3(1.6f, 0.25f, 0.35f), bodyMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Left Rail", new Vector3(-0.9f, -0.2f, 0.2f), new Vector3(0.08f, 0.12f, 1.6f), accentMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Right Rail", new Vector3(0.9f, -0.2f, 0.2f), new Vector3(0.08f, 0.12f, 1.6f), accentMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Canopy", new Vector3(0f, 0.2f, 0.75f), new Vector3(1.8f, 0.08f, 1.5f), glassMaterial);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Nose Frame", new Vector3(0f, -0.05f, 1.5f), new Vector3(0.1f, 0.75f, 0.08f), accentMaterial);

        root.SetActive(false);
        return root;
    }

    private static RaceHud CreateHud(Transform cameraTransform)
    {
        var canvasObject = new GameObject("Race HUD");
        canvasObject.transform.SetParent(cameraTransform, false);
        canvasObject.transform.localPosition = new Vector3(0f, -0.05f, 1.5f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.0025f;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cameraTransform.GetComponent<Camera>();
        canvas.pixelPerfect = false;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(760f, 360f);
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        var timer = CreateText(canvasObject.transform, "Timer", font, 34, TextAnchor.UpperLeft, new Vector2(-350f, 135f), new Vector2(220f, 55f));
        var target = CreateText(canvasObject.transform, "Target", font, 24, TextAnchor.UpperLeft, new Vector2(-350f, 88f), new Vector2(310f, 44f));
        var speed = CreateText(canvasObject.transform, "Speed", font, 22, TextAnchor.UpperLeft, new Vector2(-350f, 48f), new Vector2(180f, 38f));
        var mode = CreateText(canvasObject.transform, "View Mode", font, 20, TextAnchor.UpperRight, new Vector2(245f, 132f), new Vector2(220f, 44f));
        var status = CreateText(canvasObject.transform, "Status", font, 30, TextAnchor.MiddleCenter, new Vector2(0f, -118f), new Vector2(560f, 54f));
        var countdown = CreateText(canvasObject.transform, "Countdown", font, 110, TextAnchor.MiddleCenter, new Vector2(0f, 8f), new Vector2(160f, 150f));
        var arrow = CreateText(canvasObject.transform, "Head Direction Arrow", font, 78, TextAnchor.MiddleCenter, new Vector2(0f, 112f), new Vector2(120f, 120f));
        arrow.text = ">";

        var hud = canvasObject.AddComponent<RaceHud>();
        hud.SetReferences(timer, countdown, status, target, arrow, mode, speed);
        return hud;
    }

    private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor, Vector2 position, Vector2 sizeDelta)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }

    private static Transform CreateWorldArrow(Material material)
    {
        var root = new GameObject("World Direction Arrow").transform;
        CreatePrimitive(root, PrimitiveType.Cylinder, "Arrow Shaft", new Vector3(0f, 0f, 0.9f), new Vector3(0.12f, 0.7f, 0.12f), material).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        CreatePrimitive(root, PrimitiveType.Cube, "Arrow Head", new Vector3(0f, 0f, 1.55f), new Vector3(0.45f, 0.45f, 0.45f), material)
            .transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        CreatePrimitive(root, PrimitiveType.Cube, "Arrow Tip", new Vector3(0f, 0f, 1.95f), new Vector3(0.22f, 0.22f, 0.22f), material)
            .transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

        return root;
    }

    private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;

        var collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        var renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return primitive;
    }

    private static void SetBuildScene(string scenePath)
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
    }
}
