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

    private AudioClip countdownClip;
    private AudioClip checkpointClip;
    private AudioClip crashClip;
    private AudioClip finishClip;

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

        if (motorSource != null && motorSource.clip == null)
        {
            motorSource.clip = MakeMotorLoop();
            motorSource.loop = true;
            motorSource.spatialBlend = 0f;
            motorSource.Play();
        }
    }

    private void Update()
    {
        if (motorSource == null || droneController == null)
        {
            return;
        }

        var speed01 = Mathf.Clamp01(droneController.CurrentSpeedMetersPerSecond / maxSpeedForPitch);
        motorSource.volume = Mathf.Lerp(motorMinVolume, motorMaxVolume, speed01);
        motorSource.pitch = Mathf.Lerp(motorMinPitch, motorMaxPitch, speed01);
    }

    public void SetWaypoint(Vector3 position)
    {
        if (waypointSource == null)
        {
            return;
        }

        waypointSource.transform.position = position;
        if (!waypointSource.isPlaying)
        {
            waypointSource.clip = MakeTone("WaypointPulse", 660f, 0.18f, 0.18f);
            waypointSource.loop = true;
            waypointSource.spatialBlend = 1f;
            waypointSource.minDistance = 2f;
            waypointSource.maxDistance = 90f;
            waypointSource.Play();
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
