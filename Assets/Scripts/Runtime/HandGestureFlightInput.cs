using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;

public sealed class HandGestureFlightInput : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private Transform trackingToWorldRoot;
    [SerializeField] private Transform steeringReference;
    [SerializeField] private float pinchClosedDistanceMeters = 0.025f;
    [SerializeField] private float pinchOpenDistanceMeters = 0.12f;
    [SerializeField] private float rightHandNeutralBelowHeadMeters = 0.35f;
    [SerializeField] private float verticalDeadZoneMeters = 0.12f;
    [SerializeField] private float verticalFullRangeMeters = 0.75f;
    [SerializeField] private float verticalDirectionWeight = 1.15f;

    [Header("Gestures")]
    [SerializeField] private float viewSwitchHoldSeconds = 0.75f;
    [SerializeField] private float gestureCooldownSeconds = 0.8f;

    [Header("Editor Debug")]
    [SerializeField] private bool enableEditorDebugFlight = true;
    [SerializeField] private float editorDebugCruiseThrottle = 0.55f;
    [SerializeField] private float editorDebugBoostThrottle = 0.95f;

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

    private XRHandSubsystem handSubsystem;
    private float viewSwitchHold;
    private float gestureCooldown;
    private bool viewModeCycleRequested;
    private bool trackEditorToggleRequested;
    private bool trackEditorAddCheckpointRequested;
    private bool trackEditorSaveRequested;
    private bool trackEditorLoadNextRequested;
    private bool audioWayfindingToggleRequested;
    private bool ghostChampionToggleRequested;

    public bool HandsTracked { get; private set; }
    public Vector3 WorldMoveDirection { get; private set; } = Vector3.forward;
    public float Throttle01 { get; private set; }
    public bool HasUsableInput { get; private set; }
    public bool ViewModeCycleRequested => viewModeCycleRequested;
    public bool TrackEditorToggleRequested => trackEditorToggleRequested;
    public bool TrackEditorAddCheckpointRequested => trackEditorAddCheckpointRequested;
    public bool TrackEditorSaveRequested => trackEditorSaveRequested;
    public bool TrackEditorLoadNextRequested => trackEditorLoadNextRequested;
    public bool AudioWayfindingToggleRequested => audioWayfindingToggleRequested;
    public bool GhostChampionToggleRequested => ghostChampionToggleRequested;
    public bool UsingEditorDebugInput { get; private set; }
    public string ActiveInputSource { get; private set; } = "None";

    public void ConsumeViewModeCycleRequest()
    {
        viewModeCycleRequested = false;
    }

    public void ConsumeTrackEditorToggleRequest()
    {
        trackEditorToggleRequested = false;
    }

    public void ConsumeTrackEditorAddCheckpointRequest()
    {
        trackEditorAddCheckpointRequested = false;
    }

    public void ConsumeTrackEditorSaveRequest()
    {
        trackEditorSaveRequested = false;
    }

    public void ConsumeTrackEditorLoadNextRequest()
    {
        trackEditorLoadNextRequested = false;
    }

    public void ConsumeAudioWayfindingToggleRequest()
    {
        audioWayfindingToggleRequested = false;
    }

    public void ConsumeGhostChampionToggleRequest()
    {
        ghostChampionToggleRequested = false;
    }

    public void SetTrackingRoot(Transform root)
    {
        trackingToWorldRoot = root;
    }

    public void SetSteeringReference(Transform reference)
    {
        steeringReference = reference;
    }

    private void Awake()
    {
        if (steeringReference == null && Camera.main != null)
        {
            steeringReference = Camera.main.transform;
        }

        if (GetComponent<XRHandModelVisualizer>() == null)
        {
            gameObject.AddComponent<XRHandModelVisualizer>();
        }
    }

    private void Update()
    {
        viewModeCycleRequested = false;
        trackEditorToggleRequested = false;
        trackEditorAddCheckpointRequested = false;
        trackEditorSaveRequested = false;
        trackEditorLoadNextRequested = false;
        audioWayfindingToggleRequested = false;
        ghostChampionToggleRequested = false;

        if (gestureCooldown > 0f)
        {
            gestureCooldown -= Time.deltaTime;
        }

        var hasHandSubsystem = EnsureSubsystem();
        UsingEditorDebugInput = false;

        var leftTracked = TryGetMetaPinch(MetaAimHand.left, out var leftPinch);
        if (!leftTracked && hasHandSubsystem)
        {
            leftTracked = TryGetPinch(handSubsystem.leftHand, out leftPinch);
        }

        if (!leftTracked)
        {
            leftTracked = TryGetControllerThrottle(out leftPinch);
        }

        var rightPinch = 0f;
        if (!TryGetMetaPinch(MetaAimHand.right, out rightPinch))
        {
            rightPinch = 0f;
            if (hasHandSubsystem)
            {
                TryGetPinch(handSubsystem.rightHand, out rightPinch);
            }
        }

        var rightHeightTracked = TryGetRightHandVerticalInput(hasHandSubsystem, out var verticalInput, out var verticalSource);
        HandsTracked = leftTracked;
        Throttle01 = leftTracked ? Mathf.Clamp01(leftPinch) : 0f;
        ActiveInputSource = HandsTracked
            ? rightHeightTracked ? $"Look + {verticalSource}" : "Look"
            : "No input";

        if (TryBuildSupermanMoveDirection(verticalInput, out var moveDirection))
        {
            WorldMoveDirection = moveDirection;
        }

        HasUsableInput = HandsTracked && Throttle01 > 0.04f;
        UpdateModeGesture(leftPinch, rightPinch);
        if (hasHandSubsystem)
        {
            UpdateExtraGestures();
        }

        if (!HandsTracked && !TryUseEditorDebugInput())
        {
            HasUsableInput = false;
            Throttle01 = 0f;
            UsingEditorDebugInput = false;
        }
    }

    private bool TryBuildSupermanMoveDirection(float verticalInput, out Vector3 direction)
    {
        direction = Vector3.forward;

        var forward = steeringReference != null ? steeringReference.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            return false;
        }

        forward.Normalize();
        direction = forward + Vector3.up * (verticalInput * verticalDirectionWeight);
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = forward;
        }

        direction.Normalize();
        return true;
    }

    private bool TryGetRightHandVerticalInput(bool hasHandSubsystem, out float verticalInput, out string source)
    {
        verticalInput = 0f;
        source = "";

        if (!TryGetRightHandPosition(hasHandSubsystem, out var handPosition, out source))
        {
            return false;
        }

        var worldHandPosition = trackingToWorldRoot != null
            ? trackingToWorldRoot.TransformPoint(handPosition)
            : handPosition;
        var referenceY = steeringReference != null ? steeringReference.position.y : transform.position.y + 1.45f;
        var neutralY = referenceY - rightHandNeutralBelowHeadMeters;
        var offset = worldHandPosition.y - neutralY;
        var magnitude = Mathf.Abs(offset);
        if (magnitude <= verticalDeadZoneMeters)
        {
            verticalInput = 0f;
            return true;
        }

        var range = Mathf.Max(verticalDeadZoneMeters + 0.01f, verticalFullRangeMeters);
        verticalInput = Mathf.Sign(offset) * Mathf.InverseLerp(verticalDeadZoneMeters, range, magnitude);
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        return true;
    }

    private bool TryGetRightHandPosition(bool hasHandSubsystem, out Vector3 position, out string source)
    {
        source = "";

        if (TryGetMetaHandPosition(MetaAimHand.right, out position))
        {
            source = "right hand height";
            return true;
        }

        if (TryGetControllerPosition(XRController.rightHand, out position))
        {
            source = "right controller height";
            return true;
        }

        if (hasHandSubsystem && TryGetJointPosition(handSubsystem.rightHand, XRHandJointID.Palm, out position))
        {
            source = "right hand height";
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private static bool TryGetMetaHandPosition(MetaAimHand hand, out Vector3 position)
    {
        position = Vector3.zero;
        if (hand == null || hand.devicePosition == null)
        {
            return false;
        }

        position = hand.devicePosition.ReadValue();
        return true;
    }

    private static bool TryGetControllerPosition(XRController controller, out Vector3 position)
    {
        position = Vector3.zero;
        if (controller == null || controller.devicePosition == null)
        {
            return false;
        }

        if (controller.trackingState != null)
        {
            var trackingState = controller.trackingState.ReadValue();
            if ((trackingState & (int)UnityEngine.XR.InputTrackingState.Position) == 0)
            {
                return false;
            }
        }

        position = controller.devicePosition.ReadValue();
        return true;
    }

    private static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
    {
        position = Vector3.zero;
        var joint = hand.GetJoint(jointId);
        if (!joint.TryGetPose(out var pose))
        {
            return false;
        }

        position = pose.position;
        return true;
    }

    private static bool TryGetControllerThrottle(out float throttle01)
    {
        throttle01 = 0f;

        var leftController = XRController.leftHand;
        if (leftController == null)
        {
            return false;
        }

        var trigger = leftController.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
        var grip = leftController.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("grip");
        var triggerValue = trigger != null ? trigger.ReadValue() : 0f;
        var gripValue = grip != null ? grip.ReadValue() : 0f;
        throttle01 = Mathf.Clamp01(Mathf.Max(triggerValue, gripValue));
        return throttle01 > 0.01f;
    }

    private static bool TryGetMetaPinch(MetaAimHand hand, out float pinch01)
    {
        pinch01 = 0f;
        if (!HasValidMetaAimHand(hand) || hand.pinchStrengthIndex == null)
        {
            return false;
        }

        pinch01 = Mathf.Clamp01(hand.pinchStrengthIndex.ReadValue());
        return true;
    }

    private bool EnsureSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running)
        {
            return true;
        }

        SubsystemManager.GetSubsystems(HandSubsystems);
        for (var i = 0; i < HandSubsystems.Count; i++)
        {
            if (HandSubsystems[i] != null && HandSubsystems[i].running)
            {
                handSubsystem = HandSubsystems[i];
                return true;
            }
        }

        return false;
    }

    private bool TryGetPinch(XRHand hand, out float pinch01)
    {
        pinch01 = 0f;

        var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        if (!thumbTip.TryGetPose(out var thumbPose) || !indexTip.TryGetPose(out var indexPose))
        {
            return false;
        }

        var distance = Vector3.Distance(thumbPose.position, indexPose.position);
        pinch01 = 1f - Mathf.InverseLerp(pinchClosedDistanceMeters, pinchOpenDistanceMeters, distance);
        pinch01 = Mathf.Clamp01(pinch01);
        return true;
    }

    private void UpdateModeGesture(float leftPinch, float rightPinch)
    {
        if (gestureCooldown > 0f)
        {
            return;
        }

        if (leftPinch > 0.85f && rightPinch > 0.85f)
        {
            viewSwitchHold += Time.deltaTime;
            if (viewSwitchHold >= viewSwitchHoldSeconds)
            {
                viewModeCycleRequested = true;
                viewSwitchHold = 0f;
                gestureCooldown = gestureCooldownSeconds;
            }
        }
        else
        {
            viewSwitchHold = 0f;
        }
    }

    private void UpdateExtraGestures()
    {
        if (gestureCooldown > 0f)
        {
            return;
        }

        var left = MetaAimHand.left;
        var right = MetaAimHand.right;
        var rightMiddle = ReadGesturePinch(right, handSubsystem.rightHand, hand => hand.pinchStrengthMiddle, XRHandJointID.MiddleTip) > 0.85f;
        var rightRing = ReadGesturePinch(right, handSubsystem.rightHand, hand => hand.pinchStrengthRing, XRHandJointID.RingTip) > 0.85f;
        var rightLittle = ReadGesturePinch(right, handSubsystem.rightHand, hand => hand.pinchStrengthLittle, XRHandJointID.LittleTip) > 0.85f;
        var leftMiddle = ReadGesturePinch(left, handSubsystem.leftHand, hand => hand.pinchStrengthMiddle, XRHandJointID.MiddleTip) > 0.85f;
        var leftRing = ReadGesturePinch(left, handSubsystem.leftHand, hand => hand.pinchStrengthRing, XRHandJointID.RingTip) > 0.85f;
        var leftLittle = ReadGesturePinch(left, handSubsystem.leftHand, hand => hand.pinchStrengthLittle, XRHandJointID.LittleTip) > 0.85f;

        if (rightRing)
        {
            trackEditorToggleRequested = true;
        }
        else if (rightMiddle)
        {
            trackEditorAddCheckpointRequested = true;
        }
        else if (leftMiddle)
        {
            trackEditorSaveRequested = true;
        }
        else if (leftRing)
        {
            trackEditorLoadNextRequested = true;
        }
        else if (leftLittle)
        {
            audioWayfindingToggleRequested = true;
        }
        else if (rightLittle)
        {
            ghostChampionToggleRequested = true;
        }
        else
        {
            return;
        }

        gestureCooldown = gestureCooldownSeconds;
    }

    private delegate UnityEngine.InputSystem.Controls.AxisControl PinchSelector(MetaAimHand hand);

    private float ReadGesturePinch(MetaAimHand metaHand, XRHand xrHand, PinchSelector selector, XRHandJointID fingerTipId)
    {
        var metaPinch = ReadMetaPinch(metaHand, selector);
        if (metaPinch > 0f)
        {
            return metaPinch;
        }

        return TryGetFingerPinch(xrHand, fingerTipId, out var jointPinch) ? jointPinch : 0f;
    }

    private static float ReadMetaPinch(MetaAimHand hand, PinchSelector selector)
    {
        if (!HasValidMetaAimHand(hand))
        {
            return 0f;
        }

        var control = selector(hand);
        return control != null ? Mathf.Clamp01(control.ReadValue()) : 0f;
    }

    private static bool HasValidMetaAimHand(MetaAimHand hand)
    {
        if (hand == null || hand.aimFlags == null)
        {
            return false;
        }

        var flags = (MetaAimFlags)(ulong)hand.aimFlags.ReadValue();
        return (flags & MetaAimFlags.Computed) != MetaAimFlags.None &&
               (flags & MetaAimFlags.Valid) != MetaAimFlags.None;
    }

    private bool TryGetFingerPinch(XRHand hand, XRHandJointID fingerTipId, out float pinch01)
    {
        pinch01 = 0f;

        var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        var fingerTip = hand.GetJoint(fingerTipId);
        if (!thumbTip.TryGetPose(out var thumbPose) || !fingerTip.TryGetPose(out var fingerPose))
        {
            return false;
        }

        var distance = Vector3.Distance(thumbPose.position, fingerPose.position);
        pinch01 = 1f - Mathf.InverseLerp(pinchClosedDistanceMeters, pinchOpenDistanceMeters, distance);
        pinch01 = Mathf.Clamp01(pinch01);
        return true;
    }

    private bool TryUseEditorDebugInput()
    {
#if UNITY_EDITOR
        if (!Application.isEditor || !enableEditorDebugFlight)
        {
            return false;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        UsingEditorDebugInput = true;
        HandsTracked = true;

        var localDirection = Vector3.zero;
        if (keyboard.wKey.isPressed)
        {
            localDirection += Vector3.forward;
        }

        if (keyboard.sKey.isPressed)
        {
            localDirection += Vector3.back;
        }

        if (keyboard.aKey.isPressed)
        {
            localDirection += Vector3.left;
        }

        if (keyboard.dKey.isPressed)
        {
            localDirection += Vector3.right;
        }

        if (keyboard.eKey.isPressed)
        {
            localDirection += Vector3.up;
        }

        if (keyboard.qKey.isPressed)
        {
            localDirection += Vector3.down;
        }

        if (localDirection.sqrMagnitude > 0.0001f)
        {
            localDirection.Normalize();
            WorldMoveDirection = trackingToWorldRoot != null
                ? trackingToWorldRoot.TransformDirection(localDirection).normalized
                : localDirection;
        }

        var hasMovementIntent = localDirection.sqrMagnitude > 0.0001f;
        Throttle01 = hasMovementIntent
            ? (keyboard.leftShiftKey.isPressed ? editorDebugBoostThrottle : editorDebugCruiseThrottle)
            : 0f;
        HasUsableInput = Throttle01 > 0.04f && localDirection.sqrMagnitude > 0.0001f;

        if (gestureCooldown > 0f)
        {
            return true;
        }

        if (keyboard.cKey.wasPressedThisFrame)
        {
            viewModeCycleRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.tKey.wasPressedThisFrame)
        {
            trackEditorToggleRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.vKey.wasPressedThisFrame)
        {
            trackEditorAddCheckpointRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.bKey.wasPressedThisFrame)
        {
            trackEditorSaveRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.nKey.wasPressedThisFrame)
        {
            trackEditorLoadNextRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.xKey.wasPressedThisFrame)
        {
            audioWayfindingToggleRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }
        else if (keyboard.gKey.wasPressedThisFrame)
        {
            ghostChampionToggleRequested = true;
            gestureCooldown = gestureCooldownSeconds;
        }

        return true;
#else
        return false;
#endif
    }
}
