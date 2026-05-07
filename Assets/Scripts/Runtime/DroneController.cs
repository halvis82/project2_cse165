using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class DroneController : MonoBehaviour
{
    [SerializeField] private HandGestureFlightInput handInput;
    [SerializeField] private float maxSpeedMetersPerSecond = 14f;
    [SerializeField] private float accelerationMetersPerSecondSquared = 8f;
    [SerializeField] private float decelerationMetersPerSecondSquared = 18f;
    [SerializeField] private float rotationResponsiveness = 8f;
    [SerializeField] private bool rotateRigWithMovement = false;

    private Rigidbody body;
    private float currentSpeedMetersPerSecond;

    public bool ControlsEnabled { get; set; }
    public float CurrentSpeedMetersPerSecond { get; private set; }

    public void SetInput(HandGestureFlightInput input)
    {
        handInput = input;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (!ControlsEnabled || handInput == null || !handInput.HasUsableInput)
        {
            currentSpeedMetersPerSecond = Mathf.MoveTowards(
                currentSpeedMetersPerSecond,
                0f,
                decelerationMetersPerSecondSquared * Time.fixedDeltaTime);
            CurrentSpeedMetersPerSecond = currentSpeedMetersPerSecond;
            if (currentSpeedMetersPerSecond <= 0.001f)
            {
                return;
            }
        }
        else
        {
            var targetSpeed = handInput.Throttle01 * maxSpeedMetersPerSecond;
            currentSpeedMetersPerSecond = Mathf.MoveTowards(
                currentSpeedMetersPerSecond,
                targetSpeed,
                accelerationMetersPerSecondSquared * Time.fixedDeltaTime);
            CurrentSpeedMetersPerSecond = currentSpeedMetersPerSecond;
        }

        if (handInput == null)
        {
            return;
        }

        var direction = handInput.WorldMoveDirection.normalized;
        var nextPosition = body.position + direction * (currentSpeedMetersPerSecond * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);

        if (rotateRigWithMovement && direction.sqrMagnitude > 0.001f)
        {
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, rotationResponsiveness * Time.fixedDeltaTime));
        }
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        body.position = position;
        body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        currentSpeedMetersPerSecond = 0f;
        CurrentSpeedMetersPerSecond = 0f;
    }
}
