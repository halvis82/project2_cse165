using UnityEngine;

public sealed class RaceAudio : MonoBehaviour
{
    [SerializeField] private DroneController droneController;
    [SerializeField] private AudioSource motorSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource waypointSource;
    [SerializeField] private float maxSpeedForPitch = 70f;
    [SerializeField] private float motorMinVolume = 0.07f;
    [SerializeField] private float motorMaxVolume = 0.42f;
    [SerializeField] private float motorMinPitch = 0.55f;
    [SerializeField] private float motorMaxPitch = 2.15f;
    [SerializeField] private float waypointAudibleRangeMeters = 120f;
    [SerializeField] private float waypointMinVolume = 0.02f;
    [SerializeField] private float waypointMaxVolume = 0.65f;
    [SerializeField] private float waypointMinPitch = 0.75f;
    [SerializeField] private float waypointMaxPitch = 1.75f;

    private AudioClip countdownClip;
    private AudioClip checkpointClip;
    private AudioClip crashClip;
    private AudioClip finishClip;
    private AudioClip waypointPulseClip;
    private Vector3 activeWaypointPosition;
    private bool hasActiveWaypoint;

    public void SetReferences(DroneController controller, AudioSource motor, AudioSource effects, AudioSource waypoint)
    {
        droneController = controller;
        motorSource = motor;
        effectsSource = effects;
        waypointSource = waypoint;
    }

    private void Awake()
    {
        countdownClip = MakeTone("Countdown", 880f, 0.12f, 0.25f);
        checkpointClip = MakeTone("Checkpoint", 1320f, 0.16f, 0.35f);
        crashClip = MakeTone("Crash", 120f, 0.35f, 0.55f);
        finishClip = MakeTone("Finish", 1760f, 0.35f, 0.45f);
        waypointPulseClip = MakePulseLoop("WaypointBeacon", 720f, 0.1f, 0.72f, 0.45f);

        if (motorSource != null && motorSource.clip == null)
        {
            motorSource.clip = MakeMotorLoop();
            motorSource.loop = true;
            motorSource.spatialBlend = 0f;
            motorSource.dopplerLevel = 0f;
            motorSource.Play();
        }

        if (effectsSource != null)
        {
            effectsSource.spatialBlend = 0f;
            effectsSource.dopplerLevel = 0f;
        }
    }

    private void Update()
    {
        if (droneController == null)
        {
            return;
        }

        UpdateMotorAudio();
        UpdateWaypointAudio();
    }

    public void SetWaypoint(Vector3 position)
    {
        activeWaypointPosition = position;
        hasActiveWaypoint = true;
        if (waypointSource == null)
        {
            return;
        }

        waypointSource.transform.position = position;
        waypointSource.clip = waypointPulseClip;
        waypointSource.loop = true;
        waypointSource.spatialBlend = 1f;
        waypointSource.dopplerLevel = 0f;
        waypointSource.rolloffMode = AudioRolloffMode.Logarithmic;
        waypointSource.minDistance = 2f;
        waypointSource.maxDistance = waypointAudibleRangeMeters;
        if (!waypointSource.isPlaying)
        {
            waypointSource.Play();
        }
    }

    public void StopWaypoint()
    {
        hasActiveWaypoint = false;
        if (waypointSource != null)
        {
            waypointSource.Stop();
        }
    }

    public void PlayCountdown()
    {
        Play(countdownClip);
    }

    public void PlayCheckpoint()
    {
        Play(checkpointClip);
    }

    public void PlayCrash()
    {
        Play(crashClip);
    }

    public void PlayFinish()
    {
        Play(finishClip);
    }

    private void Play(AudioClip clip)
    {
        if (effectsSource != null && clip != null)
        {
            effectsSource.PlayOneShot(clip);
        }
    }

    private void UpdateMotorAudio()
    {
        if (motorSource == null)
        {
            return;
        }

        motorSource.transform.position = droneController.transform.position;
        var speed01 = Mathf.Clamp01(droneController.CurrentSpeedMetersPerSecond / maxSpeedForPitch);
        motorSource.volume = Mathf.Lerp(motorMinVolume, motorMaxVolume, speed01);
        motorSource.pitch = Mathf.Lerp(motorMinPitch, motorMaxPitch, speed01);
    }

    private void UpdateWaypointAudio()
    {
        if (waypointSource == null || !hasActiveWaypoint)
        {
            return;
        }

        waypointSource.transform.position = activeWaypointPosition;
        if (!waypointSource.isPlaying)
        {
            waypointSource.Play();
        }

        var distanceMeters = Vector3.Distance(droneController.transform.position, activeWaypointPosition);
        var closeness01 = 1f - Mathf.Clamp01(distanceMeters / Mathf.Max(1f, waypointAudibleRangeMeters));
        closeness01 = Mathf.Pow(closeness01, 0.7f);
        waypointSource.volume = Mathf.Lerp(waypointMinVolume, waypointMaxVolume, closeness01);
        waypointSource.pitch = Mathf.Lerp(waypointMinPitch, waypointMaxPitch, closeness01);
    }

    private static AudioClip MakeTone(string name, float frequency, float durationSeconds, float volume)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleRate;
            var envelope = Mathf.Clamp01(1f - t / durationSeconds);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
        }

        var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakePulseLoop(string name, float frequency, float pulseSeconds, float intervalSeconds, float volume)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.CeilToInt(sampleRate * intervalSeconds);
        var pulseSamples = Mathf.CeilToInt(sampleRate * pulseSeconds);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            if (i >= pulseSamples)
            {
                samples[i] = 0f;
                continue;
            }

            var t = i / (float)sampleRate;
            var pulseT = i / Mathf.Max(1f, pulseSamples - 1f);
            var attack = Mathf.Clamp01(pulseT / 0.12f);
            var decay = Mathf.Clamp01((1f - pulseT) / 0.88f);
            var envelope = attack * decay;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
        }

        var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeMotorLoop()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 1f;
        var sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleRate;
            var baseWave =
                Mathf.Sin(2f * Mathf.PI * 72f * t) * 0.48f +
                Mathf.Sin(2f * Mathf.PI * 144f * t) * 0.26f +
                Mathf.Sin(2f * Mathf.PI * 216f * t) * 0.14f +
                Mathf.Sin(2f * Mathf.PI * 31f * t) * 0.12f;
            var rotorChop = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 18f * t);
            samples[i] = baseWave * rotorChop * 0.38f;
        }

        var clip = AudioClip.Create("MotorLoop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
