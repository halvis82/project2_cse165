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

    private int currentTargetIndex = 1;
    private int lastClearedIndex;
    private float elapsedSeconds;
    private float countdownRemaining;
    private float statusClearAt;
    private bool timerRunning;
    private bool finished;
    private bool crashResetInProgress;

    public bool HasCurrentTarget => checkpointTrack != null && currentTargetIndex >= 0 && currentTargetIndex < checkpointTrack.Count;
    public Vector3 CurrentTargetPosition => checkpointTrack.GetCheckpoint(currentTargetIndex);
    public int CurrentTargetIndex => currentTargetIndex;
    public int CheckpointCount => checkpointTrack != null ? checkpointTrack.Count : 0;

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
        BeginRaceSetup();
    }

    private void Update()
    {
        UpdateCountdown();
        UpdateTimer();
        UpdateGestures();
        UpdateCheckpointProgress();
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

        currentTargetIndex = 1;
        lastClearedIndex = 0;
        elapsedSeconds = 0f;
        timerRunning = false;
        finished = false;
        crashResetInProgress = false;

        PlaceDroneAtCheckpoint(lastClearedIndex);
        checkpointTrack.SetCurrentIndex(currentTargetIndex);
        ApplyPreferredStartingView();
        StartCountdown(startCountdownSeconds, false, "Ready");
        raceAudio?.SetWaypoint(CurrentTargetPosition);
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
        if (finished || crashResetInProgress)
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
                droneController.ControlsEnabled = true;
            }

            if (!timerRunning && !finished)
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
    }

    private void UpdateCheckpointProgress()
    {
        if (finished || countdownRemaining > 0f || checkpointTrack == null || droneController == null || !HasCurrentTarget)
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
            SetStatus("Finished");
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
                    ? "Show both hands"
                    : "Look + right hand steer. Left pinch moves.");
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
