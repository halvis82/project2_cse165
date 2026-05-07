using UnityEngine;
using UnityEngine.UI;

public sealed class RaceHud : MonoBehaviour
{
    [SerializeField] private Text timerText;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text targetText;
    [SerializeField] private Text arrowText;
    [SerializeField] private Text modeText;
    [SerializeField] private Text speedText;

    public void SetReferences(
        Text timer,
        Text countdown,
        Text status,
        Text target,
        Text arrow,
        Text mode,
        Text speed)
    {
        timerText = timer;
        countdownText = countdown;
        statusText = status;
        targetText = target;
        arrowText = arrow;
        modeText = mode;
        speedText = speed;
    }

    public void SetTimer(float seconds, bool finished)
    {
        if (timerText == null)
        {
            return;
        }

        var minutes = Mathf.FloorToInt(seconds / 60f);
        var wholeSeconds = Mathf.FloorToInt(seconds % 60f);
        var hundredths = Mathf.FloorToInt((seconds * 100f) % 100f);
        timerText.text = $"{minutes:00}:{wholeSeconds:00}.{hundredths:00}";
        timerText.color = finished ? new Color(0.45f, 1f, 0.45f) : Color.white;
    }

    public void SetCountdown(float remainingSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        if (remainingSeconds <= 0f)
        {
            countdownText.text = "";
            return;
        }

        countdownText.text = Mathf.CeilToInt(remainingSeconds).ToString();
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    public void SetTarget(int currentTargetIndex, int checkpointCount, float distanceMeters)
    {
        if (targetText != null)
        {
            targetText.text = checkpointCount > 0
                ? $"CP {currentTargetIndex + 1}/{checkpointCount}  {distanceMeters:0}m"
                : "No track";
        }
    }

    public void SetHeading(float signedAngleDegrees)
    {
        if (arrowText == null)
        {
            return;
        }

        arrowText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -signedAngleDegrees);
    }

    public void SetViewMode(DroneViewMode mode)
    {
        if (modeText != null)
        {
            modeText.text = mode.ToString();
        }
    }

    public void SetSpeed(float metersPerSecond)
    {
        if (speedText != null)
        {
            speedText.text = $"{metersPerSecond:0} m/s";
        }
    }

    public void SetWayfindingVisible(bool visible)
    {
        if (targetText != null)
        {
            targetText.gameObject.SetActive(visible);
        }

        if (arrowText != null)
        {
            arrowText.gameObject.SetActive(visible);
        }
    }
}
