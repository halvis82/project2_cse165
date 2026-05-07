using UnityEngine;

public sealed class CheckpointVisual : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Transform spinner;
    [SerializeField] private float currentSpinDegreesPerSecond = 45f;

    private Material materialInstance;
    private bool isCurrent;

    public void Initialize(Renderer rendererToTint, Transform spinnerTransform)
    {
        targetRenderer = rendererToTint;
        spinner = spinnerTransform;
        if (targetRenderer != null)
        {
            materialInstance = targetRenderer.material;
        }
    }

    public void SetState(Color color, bool current, float scale)
    {
        isCurrent = current;
        transform.localScale = Vector3.one * scale;
        if (materialInstance != null)
        {
            materialInstance.color = color;
        }
    }

    private void Update()
    {
        if (isCurrent && spinner != null)
        {
            spinner.Rotate(Vector3.up, currentSpinDegreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
