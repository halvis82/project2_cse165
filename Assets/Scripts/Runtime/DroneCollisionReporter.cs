using UnityEngine;

public sealed class DroneCollisionReporter : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;

    public void SetRaceManager(RaceManager manager)
    {
        raceManager = manager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (raceManager == null)
        {
            return;
        }

        if (other.GetComponentInParent<MachuPicchuTerrain>() != null)
        {
            raceManager.HandleCrash();
        }
    }
}
