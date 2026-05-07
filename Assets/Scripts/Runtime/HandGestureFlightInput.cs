using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

public sealed class HandGestureFlightInput : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private Transform trackingToWorldRoot;
    [SerializeField] private Transform steeringReference;
    [SerializeField] private float pinchClosedDistanceMeters = 0.025f;
    [SerializeField] private float pinchOpenDistanceMeters = 0.12f;
    [SerializeField] private float steeringDeadZone = 0.08f;
    [SerializeField] private bool useHeadRelativeSteering = true;
    [SerializeField] private bool invertJointFallbackDirection = true;

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

    public bool HandsTracked { get; private set; }
    public Vector3 WorldMoveDirection { get; private set; } = Vector3.forward;
    public float Throttle01 { get; private set; }
    public bool HasUsableInput { get; private set; }
    public bool ViewModeCycleRequested => viewModeCycleRequested;
    public bool UsingEditorDebugInput { get; private set; }

    public void ConsumeViewModeCycleRequest()
    {
        viewModeCycleRequested = false;
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
    }

    private void Update()
    {
        viewModeCycleRequested = false;

        if (gestureCooldown > 0f)
        {
            gestureCooldown -= Time.deltaTime;
        }

        if (!EnsureSubsystem())
        {
            if (!TryUseEditorDebugInput())
            {
                HandsTracked = false;
                HasUsableInput = false;
                Throttle01 = 0f;
                UsingEditorDebugInput = false;
            }
            return;
        }

        UsingEditorDebugInput = false;

        var leftTracked = TryGetMetaPinch(MetaAimHand.left, out var leftPinch) ||
                          TryGetPinch(handSubsystem.leftHand, out leftPinch);
        var rightPinch = 0f;
        var rightTracked = TryGetMetaAimDirection(out var trackingDirection);
        if (!TryGetMetaPinch(MetaAimHand.right, out rightPinch))
        {
            rightTracked = rightTracked && TryGetPinch(handSubsystem.rightHand, out rightPinch);
        }

        if (!rightTracked)
        {
            rightTracked = TryGetFingerDirection(handSubsystem.rightHand, out trackingDirection) &&
                           TryGetPinch(handSubsystem.rightHand, out rightPinch);
            if (rightTracked && invertJointFallbackDirection)
            {
                trackingDirection = -trackingDirection;
            }
        }

        HandsTracked = leftTracked && rightTracked;
        Throttle01 = leftTracked ? Mathf.Clamp01(leftPinch) : 0f;

        if (rightTracked && trackingDirection.sqrMagnitude > steeringDeadZone * steeringDeadZone)
        {
            WorldMoveDirection = ResolveWorldMoveDirection(trackingDirection);
        }

        HasUsableInput = HandsTracked && Throttle01 > 0.04f;
        UpdateModeGesture(leftPinch, rightPinch);
    }

    private Vector3 ResolveWorldMoveDirection(Vector3 trackingDirection)
    {
        return trackingToWorldRoot != null
            ? trackingToWorldRoot.TransformDirection(trackingDirection).normalized
            : trackingDirection.normalized;
    }

    private bool TryGetMetaAimDirection(out Vector3 direction)
    {
        direction = Vector3.forward;

        var rightHand = MetaAimHand.right;
        if (rightHand == null || rightHand.aimFlags == null || rightHand.deviceRotation == null)
        {
            return false;
        }

        var flags = (MetaAimFlags)(ulong)rightHand.aimFlags.ReadValue();
        if ((flags & MetaAimFlags.Computed) == MetaAimFlags.None ||
            (flags & MetaAimFlags.Valid) == MetaAimFlags.None)
        {
            return false;
        }

        direction = rightHand.deviceRotation.ReadValue() * Vector3.forward;
        if (direction.sqrMagnitude < steeringDeadZone * steeringDeadZone)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    private static bool TryGetMetaPinch(MetaAimHand hand, out float pinch01)
    {
        pinch01 = 0f;
        if (hand == null || hand.pinchStrengthIndex == null)
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

    private bool TryGetFingerDirection(XRHand hand, out Vector3 direction)
    {
        direction = Vector3.forward;

        var palm = hand.GetJoint(XRHandJointID.Palm);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        if (!palm.TryGetPose(out var palmPose) || !indexTip.TryGetPose(out var indexPose))
        {
            return false;
        }

        direction = indexPose.position - palmPose.position;
        if (direction.sqrMagnitude < 0.0004f)
        {
            direction = palmPose.rotation * Vector3.forward;
        }

        direction.Normalize();
        return true;
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

        return true;
#else
        return false;
#endif
    }
}
