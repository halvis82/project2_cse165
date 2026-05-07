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
    [SerializeField] private GameObject cockpitVisual;
    [SerializeField] private GameObject droneVisual;
    [SerializeField] private Vector3 firstPersonAnchorLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 cockpitAnchorLocalPosition = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private Vector3 thirdPersonAnchorLocalPosition = new Vector3(0f, 2.2f, -7f);

    public DroneViewMode CurrentMode { get; private set; } = DroneViewMode.FirstPerson;

    public void SetReferences(Transform anchor, GameObject cockpit, GameObject droneBody)
    {
        viewAnchor = anchor;
        cockpitVisual = cockpit;
        droneVisual = droneBody;
        ApplyMode(CurrentMode);
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
                    break;
                case DroneViewMode.Cockpit:
                    viewAnchor.localPosition = cockpitAnchorLocalPosition;
                    break;
                case DroneViewMode.ThirdPerson:
                    viewAnchor.localPosition = thirdPersonAnchorLocalPosition;
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
}
