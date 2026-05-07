using UnityEngine;

public sealed class WayfindingSystem : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private Transform drone;
    [SerializeField] private Camera viewerCamera;
    [SerializeField] private LineRenderer worldLine;
    [SerializeField] private Transform worldArrow;
    [SerializeField] private RaceHud hud;
    [SerializeField] private bool visualWayfindingEnabled = true;

    public void SetReferences(
        RaceManager manager,
        Transform droneTransform,
        Camera cameraTransform,
        LineRenderer line,
        Transform arrow,
        RaceHud raceHud)
    {
        raceManager = manager;
        drone = droneTransform;
        viewerCamera = cameraTransform;
        worldLine = line;
        worldArrow = arrow;
        hud = raceHud;
    }

    public void SetVisualWayfindingEnabled(bool enabled)
    {
        visualWayfindingEnabled = enabled;
        if (worldLine != null)
        {
            worldLine.enabled = enabled;
        }

        if (worldArrow != null)
        {
            worldArrow.gameObject.SetActive(enabled);
        }
    }

    private void LateUpdate()
    {
        if (raceManager == null || drone == null || !raceManager.HasCurrentTarget)
        {
            return;
        }

        var target = raceManager.CurrentTargetPosition;
        var toTarget = target - drone.position;
        var distance = toTarget.magnitude;
        var direction = distance > 0.001f ? toTarget / distance : drone.forward;

        UpdateWorldAid(target, direction);
        UpdateHeadAid(direction, distance);
    }

    private void UpdateWorldAid(Vector3 target, Vector3 direction)
    {
        if (!visualWayfindingEnabled)
        {
            return;
        }

        if (worldLine != null)
        {
            worldLine.enabled = true;
            worldLine.SetPosition(0, drone.position);
            worldLine.SetPosition(1, target);
        }

        if (worldArrow != null)
        {
            worldArrow.gameObject.SetActive(true);
            worldArrow.position = drone.position + Vector3.up * 1.2f + direction * 5f;
            worldArrow.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private void UpdateHeadAid(Vector3 direction, float distance)
    {
        if (hud == null)
        {
            return;
        }

        if (viewerCamera != null)
        {
            var headLocalDirection = viewerCamera.transform.InverseTransformDirection(direction);
            var signedAngle = Mathf.Atan2(headLocalDirection.x, headLocalDirection.z) * Mathf.Rad2Deg;
            hud.SetHeading(signedAngle);
        }

        hud.SetTarget(raceManager.CurrentTargetIndex, raceManager.CheckpointCount, distance);
    }
}
