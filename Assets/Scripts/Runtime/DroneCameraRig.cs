using UnityEngine;

public enum DroneViewMode
{
    FirstPerson,
    Cockpit,
    ThirdPerson
}

public sealed class DroneCameraRig : MonoBehaviour
{
    [SerializeField] private Transform viewAnchor;
    [SerializeField] private DroneController followController;
    [SerializeField] private GameObject cockpitVisual;
    [SerializeField] private GameObject droneVisual;
    [SerializeField] private Vector3 firstPersonAnchorLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 cockpitAnchorLocalPosition = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private Vector3 thirdPersonAnchorLocalPosition = new Vector3(0f, 2.2f, -7f);
    [SerializeField] private float thirdPersonDistanceMeters = 7f;
    [SerializeField] private float thirdPersonHeightMeters = 2.2f;

    public DroneViewMode CurrentMode { get; private set; } = DroneViewMode.FirstPerson;

    private void Awake()
    {
        if (followController == null)
        {
            followController = GetComponent<DroneController>() ?? GetComponentInParent<DroneController>();
        }
    }

    public void SetReferences(Transform anchor, GameObject cockpit, GameObject droneBody)
    {
        viewAnchor = anchor;
        cockpitVisual = cockpit;
        droneVisual = droneBody;
        ApplyMode(CurrentMode);
    }

    public void SetFollowController(DroneController controller)
    {
        followController = controller;
    }

    private void LateUpdate()
    {
        if (CurrentMode == DroneViewMode.ThirdPerson)
        {
            UpdateThirdPersonAnchor();
        }
    }

    public void CycleMode()
    {
        var next = (DroneViewMode)(((int)CurrentMode + 1) % 3);
        ApplyMode(next);
    }

    public void ApplyMode(DroneViewMode mode)
    {
        CurrentMode = mode;

        if (viewAnchor != null)
        {
            switch (mode)
            {
                case DroneViewMode.FirstPerson:
                    viewAnchor.localPosition = firstPersonAnchorLocalPosition;
                    viewAnchor.localRotation = Quaternion.identity;
                    break;
                case DroneViewMode.Cockpit:
                    viewAnchor.localPosition = cockpitAnchorLocalPosition;
                    viewAnchor.localRotation = Quaternion.identity;
                    break;
                case DroneViewMode.ThirdPerson:
                    UpdateThirdPersonAnchor();
                    if (followController == null)
                    {
                        viewAnchor.localPosition = thirdPersonAnchorLocalPosition;
                        viewAnchor.localRotation = Quaternion.identity;
                    }
                    break;
            }
        }

        if (cockpitVisual != null)
        {
            cockpitVisual.SetActive(mode == DroneViewMode.Cockpit);
        }

        if (droneVisual != null)
        {
            droneVisual.SetActive(mode == DroneViewMode.ThirdPerson);
        }
    }

    private void UpdateThirdPersonAnchor()
    {
        if (viewAnchor == null || followController == null)
        {
            return;
        }

        var movementDirection = followController.CurrentMoveDirection;
        movementDirection.y = 0f;
        if (movementDirection.sqrMagnitude < 0.001f)
        {
            movementDirection = followController.transform.forward;
            movementDirection.y = 0f;
        }

        if (movementDirection.sqrMagnitude < 0.001f)
        {
            movementDirection = Vector3.forward;
        }

        movementDirection.Normalize();

        var worldOffset = -movementDirection * thirdPersonDistanceMeters + Vector3.up * thirdPersonHeightMeters;
        var parent = viewAnchor.parent;
        viewAnchor.localPosition = parent != null
            ? parent.InverseTransformVector(worldOffset)
            : worldOffset;

        var worldRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
        viewAnchor.localRotation = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;
    }
}
