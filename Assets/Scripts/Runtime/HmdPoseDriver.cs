using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

public sealed class HmdPoseDriver : MonoBehaviour
{
    [SerializeField] private bool useCenterEyeWhenAvailable = true;

    private InputDevice hmd;

    private void Update()
    {
        ApplyPose();
    }

    private void OnBeforeRender()
    {
        ApplyPose();
    }

    private void ApplyPose()
    {
        if (!EnsureHmd())
        {
            TryApplyInputSystemPose();
            return;
        }

        var appliedAnyPose = false;
        if (hmd.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
        {
            transform.localPosition = position;
            appliedAnyPose = true;
        }

        if (hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
        {
            transform.localRotation = rotation;
            appliedAnyPose = true;
        }

        if (!appliedAnyPose)
        {
            TryApplyInputSystemPose();
        }
    }

    private bool TryApplyInputSystemPose()
    {
        var hmdDevice = UnityEngine.InputSystem.InputSystem.GetDevice<XRHMD>();
        if (hmdDevice == null)
        {
            return false;
        }

        if (hmdDevice.centerEyePosition != null)
        {
            transform.localPosition = hmdDevice.centerEyePosition.ReadValue();
        }

        if (hmdDevice.centerEyeRotation != null)
        {
            transform.localRotation = hmdDevice.centerEyeRotation.ReadValue();
        }

        return true;
    }

    private bool EnsureHmd()
    {
        if (hmd.isValid)
        {
            return true;
        }

        hmd = InputDevices.GetDeviceAtXRNode(useCenterEyeWhenAvailable ? XRNode.CenterEye : XRNode.Head);
        if (!hmd.isValid)
        {
            hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }

        return hmd.isValid;
    }
}
