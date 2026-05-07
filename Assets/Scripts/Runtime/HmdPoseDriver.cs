using UnityEngine;
using UnityEngine.XR;

public sealed class HmdPoseDriver : MonoBehaviour
{
    [SerializeField] private bool useCenterEyeWhenAvailable = true;

    private InputDevice hmd;

    private void Update()
    {
        if (!EnsureHmd())
        {
            return;
        }

        if (hmd.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
        {
            transform.localPosition = position;
        }

        if (hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
        {
            transform.localRotation = rotation;
        }
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
