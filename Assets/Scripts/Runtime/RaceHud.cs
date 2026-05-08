using UnityEngine;
using UnityEngine.UI;

public sealed class RaceHud : MonoBehaviour
{
    [SerializeField] private Text timerText;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text restartWarningText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text targetText;
    [SerializeField] private Text arrowText;
    [SerializeField] private Text modeText;
    [SerializeField] private Text speedText;
    [SerializeField] private Text selectedStateText;
    [SerializeField] private Text infoPanelText;
    [SerializeField] private Text controlsText;

    private void Awake()
    {
        EnsureInfoPanel();
        EnsureRestartWarningText();
    }

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

    public void SetRestartWarning(string message)
    {
        EnsureRestartWarningText();
        if (restartWarningText == null)
        {
            return;
        }

        var visible = !string.IsNullOrWhiteSpace(message);
        restartWarningText.gameObject.SetActive(visible);
        restartWarningText.text = visible ? message : string.Empty;
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

    public void SetInfoPanel(string selectedState, string details)
    {
        EnsureInfoPanel();

        if (selectedStateText != null)
        {
            selectedStateText.text = selectedState;
        }

        if (infoPanelText != null)
        {
            infoPanelText.text = details;
        }
    }

    public void SetControlsPanel(string controls)
    {
        EnsureInfoPanel();

        if (controlsText != null)
        {
            controlsText.text = controls;
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

    private void EnsureInfoPanel()
    {
        if (selectedStateText != null && infoPanelText != null && controlsText != null)
        {
            return;
        }

        if (selectedStateText == null)
        {
            selectedStateText = CreateInfoText(
                "Live Selected State",
                new Vector2(-18f, -18f),
                new Vector2(300f, 34f),
                18,
                TextAnchor.UpperRight,
                true);

            if (selectedStateText != null)
            {
                selectedStateText.color = Color.white;
                var outline = selectedStateText.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.88f, 0.05f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        if (infoPanelText == null)
        {
            infoPanelText = CreateInfoText(
                "Live Race Info",
                new Vector2(-18f, -52f),
                new Vector2(300f, 150f),
                12,
                TextAnchor.UpperRight,
                true);

            if (infoPanelText != null)
            {
                infoPanelText.color = Color.white;
                var outline = infoPanelText.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        if (controlsText == null)
        {
            controlsText = CreateInfoText(
                "Controls Help",
                new Vector2(18f, -130f),
                new Vector2(330f, 205f),
                13,
                TextAnchor.UpperLeft,
                false);

            if (controlsText != null)
            {
                controlsText.color = Color.white;
                var outline = controlsText.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }
    }

    private void EnsureRestartWarningText()
    {
        if (restartWarningText != null)
        {
            return;
        }

        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        var textObject = new GameObject("Restart Warning", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        var childRect = textObject.GetComponent<RectTransform>();
        childRect.anchorMin = new Vector2(0.5f, 0.5f);
        childRect.anchorMax = new Vector2(0.5f, 0.5f);
        childRect.pivot = new Vector2(0.5f, 0.5f);
        childRect.anchoredPosition = new Vector2(0f, -6f);
        childRect.sizeDelta = new Vector2(620f, 94f);

        restartWarningText = textObject.AddComponent<Text>();
        restartWarningText.font = ResolveFont();
        restartWarningText.fontSize = 42;
        restartWarningText.alignment = TextAnchor.MiddleCenter;
        restartWarningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        restartWarningText.verticalOverflow = VerticalWrapMode.Overflow;
        restartWarningText.raycastTarget = false;
        restartWarningText.color = new Color(1f, 0.9f, 0.25f, 1f);

        var outline = restartWarningText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        restartWarningText.gameObject.SetActive(false);
    }

    private Text CreateInfoText(string objectName, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, bool anchorRight)
    {
        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            return null;
        }

        var textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        var childRect = textObject.GetComponent<RectTransform>();
        childRect.anchorMin = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        childRect.anchorMax = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        childRect.pivot = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        childRect.anchoredPosition = anchoredPosition;
        childRect.sizeDelta = size;

        var text = textObject.AddComponent<Text>();
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Font ResolveFont()
    {
        if (timerText != null && timerText.font != null)
        {
            return timerText.font;
        }

        if (statusText != null && statusText.font != null)
        {
            return statusText.font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
