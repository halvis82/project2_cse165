using UnityEngine;

public sealed class DroneCollisionReporter : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private float fallbackRadiusMeters = 0.7f;
    [SerializeField] private float segmentCastPaddingMeters = 0.08f;
    [SerializeField] private float verticalProbeDistanceMeters = 3f;
    [SerializeField] private float teleportSegmentThresholdMeters = 8f;

    private SphereCollider triggerCollider;
    private Vector3 previousPosition;

    public void SetRaceManager(RaceManager manager)
    {
        raceManager = manager;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        previousPosition = transform.position;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
    }

    private void FixedUpdate()
    {
        var currentPosition = transform.position;
        if (Vector3.Distance(previousPosition, currentPosition) > teleportSegmentThresholdMeters)
        {
            previousPosition = currentPosition;
            return;
        }

        if (TouchesTerrain(currentPosition) || CrossedTerrain(previousPosition, currentPosition))
        {
            ReportCrash();
        }

        previousPosition = currentPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsTerrainCollider(other))
        {
            ReportCrash();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsTerrainCollider(other))
        {
            ReportCrash();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsTerrainCollider(collision.collider))
        {
            ReportCrash();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (IsTerrainCollider(collision.collider))
        {
            ReportCrash();
        }
    }

    private bool TouchesTerrain(Vector3 position)
    {
        var radius = GetCrashRadius();
        var hits = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore);
        for (var i = 0; i < hits.Length; i++)
        {
            if (IsTerrainCollider(hits[i]))
            {
                return true;
            }
        }

        return (Physics.Raycast(position, Vector3.down, out var downHit, verticalProbeDistanceMeters, ~0, QueryTriggerInteraction.Ignore) &&
                IsTerrainCollider(downHit.collider) &&
                downHit.distance <= radius) ||
               (Physics.Raycast(position, Vector3.up, out var upHit, verticalProbeDistanceMeters, ~0, QueryTriggerInteraction.Ignore) &&
                IsTerrainCollider(upHit.collider) &&
                upHit.distance <= radius);
    }

    private bool CrossedTerrain(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var distance = delta.magnitude;
        if (distance < 0.001f)
        {
            return false;
        }

        var radius = Mathf.Max(0.05f, GetCrashRadius() - segmentCastPaddingMeters);
        var direction = delta / distance;
        return Physics.SphereCast(from, radius, direction, out var hit, distance + segmentCastPaddingMeters, ~0, QueryTriggerInteraction.Ignore) &&
               IsTerrainCollider(hit.collider);
    }

    private float GetCrashRadius()
    {
        return triggerCollider != null ? Mathf.Max(0.05f, triggerCollider.radius * MaxComponent(transform.lossyScale)) : fallbackRadiusMeters;
    }

    private static float MaxComponent(Vector3 vector)
    {
        return Mathf.Max(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
    }

    private static bool IsTerrainCollider(Collider colliderComponent)
    {
        return colliderComponent != null && colliderComponent.GetComponentInParent<MachuPicchuTerrain>() != null;
    }

    private void ReportCrash()
    {
        raceManager?.HandleCrash();
    }
}
