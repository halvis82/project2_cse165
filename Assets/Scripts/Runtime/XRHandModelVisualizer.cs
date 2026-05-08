using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public sealed class XRHandModelVisualizer : MonoBehaviour
{
    [SerializeField] private bool showHands = true;
    [SerializeField] private float jointRadiusMeters = 0.018f;
    [SerializeField] private float boneWidthMeters = 0.012f;
    [SerializeField] private Color leftHandColor = new Color(0.15f, 0.65f, 1f, 0.95f);
    [SerializeField] private Color rightHandColor = new Color(1f, 0.74f, 0.16f, 0.95f);

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

    private readonly XRHandJointID[] trackedJoints =
    {
        XRHandJointID.Wrist,
        XRHandJointID.Palm,
        XRHandJointID.ThumbMetacarpal,
        XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal,
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexMetacarpal,
        XRHandJointID.IndexProximal,
        XRHandJointID.IndexIntermediate,
        XRHandJointID.IndexDistal,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleMetacarpal,
        XRHandJointID.MiddleProximal,
        XRHandJointID.MiddleIntermediate,
        XRHandJointID.MiddleDistal,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingMetacarpal,
        XRHandJointID.RingProximal,
        XRHandJointID.RingIntermediate,
        XRHandJointID.RingDistal,
        XRHandJointID.RingTip,
        XRHandJointID.LittleMetacarpal,
        XRHandJointID.LittleProximal,
        XRHandJointID.LittleIntermediate,
        XRHandJointID.LittleDistal,
        XRHandJointID.LittleTip
    };

    private readonly XRHandJointID[][] fingers =
    {
        new[] { XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
        new[] { XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
        new[] { XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
        new[] { XRHandJointID.Wrist, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
        new[] { XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip }
    };

    private XRHandSubsystem handSubsystem;
    private HandModel leftHand;
    private HandModel rightHand;

    private void Awake()
    {
        leftHand = new HandModel("Left Hand Model", transform, trackedJoints, fingers, leftHandColor, jointRadiusMeters, boneWidthMeters);
        rightHand = new HandModel("Right Hand Model", transform, trackedJoints, fingers, rightHandColor, jointRadiusMeters, boneWidthMeters);
    }

    private void LateUpdate()
    {
        if (!showHands)
        {
            leftHand.SetVisible(false);
            rightHand.SetVisible(false);
            return;
        }

        var hasHandSubsystem = EnsureSubsystem();
        var leftRendered = hasHandSubsystem && leftHand.TryUpdateFromHand(handSubsystem.leftHand);
        var rightRendered = hasHandSubsystem && rightHand.TryUpdateFromHand(handSubsystem.rightHand);

        leftHand.SetVisible(leftRendered);
        rightHand.SetVisible(rightRendered);
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

    private sealed class HandModel
    {
        private readonly Transform root;
        private readonly XRHandJointID[] joints;
        private readonly XRHandJointID[][] fingerChains;
        private readonly Dictionary<XRHandJointID, Transform> jointObjects = new Dictionary<XRHandJointID, Transform>();
        private readonly LineRenderer[] bones;
        private readonly Transform aimRay;
        private readonly Material material;

        public HandModel(
            string name,
            Transform parent,
            XRHandJointID[] trackedJoints,
            XRHandJointID[][] fingers,
            Color color,
            float jointRadius,
            float boneWidth)
        {
            joints = trackedJoints;
            fingerChains = fingers;
            material = new Material(Shader.Find("Standard"))
            {
                color = color
            };

            root = new GameObject(name).transform;
            root.SetParent(parent, false);

            for (var i = 0; i < joints.Length; i++)
            {
                var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = joints[i].ToString();
                joint.transform.SetParent(root, false);
                joint.transform.localScale = Vector3.one * (jointRadius * 2f);
                var collider = joint.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                var renderer = joint.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = material;
                }

                jointObjects[joints[i]] = joint.transform;
            }

            bones = new LineRenderer[fingerChains.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                var boneObject = new GameObject($"Finger Bone {i + 1}");
                boneObject.transform.SetParent(root, false);
                var line = boneObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.material = material;
                line.startWidth = boneWidth;
                line.endWidth = boneWidth;
                line.numCapVertices = 4;
                bones[i] = line;
            }

            aimRay = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            aimRay.name = "Aim Direction";
            aimRay.SetParent(root, false);
            aimRay.localScale = new Vector3(boneWidth * 1.4f, 0.35f, boneWidth * 1.4f);
            var aimCollider = aimRay.GetComponent<Collider>();
            if (aimCollider != null)
            {
                Object.Destroy(aimCollider);
            }

            var aimRenderer = aimRay.GetComponent<Renderer>();
            if (aimRenderer != null)
            {
                aimRenderer.material = material;
            }

            SetVisible(false);
        }

        public bool TryUpdateFromHand(XRHand hand)
        {
            var anyPose = false;
            for (var i = 0; i < joints.Length; i++)
            {
                var joint = hand.GetJoint(joints[i]);
                if (!joint.TryGetPose(out var pose))
                {
                    jointObjects[joints[i]].gameObject.SetActive(false);
                    continue;
                }

                anyPose = true;
                var jointObject = jointObjects[joints[i]];
                jointObject.gameObject.SetActive(true);
                jointObject.localPosition = pose.position;
                jointObject.localRotation = pose.rotation;
            }

            if (!anyPose)
            {
                return false;
            }

            for (var i = 0; i < fingerChains.Length; i++)
            {
                UpdateFingerLine(bones[i], fingerChains[i]);
            }

            aimRay.gameObject.SetActive(false);
            return true;
        }

        public void SetVisible(bool visible)
        {
            root.gameObject.SetActive(visible);
        }

        private void UpdateFingerLine(LineRenderer line, XRHandJointID[] chain)
        {
            var pointCount = 0;
            for (var i = 0; i < chain.Length; i++)
            {
                if (jointObjects.TryGetValue(chain[i], out var joint) && joint.gameObject.activeSelf)
                {
                    pointCount++;
                }
            }

            line.positionCount = pointCount;
            var lineIndex = 0;
            for (var i = 0; i < chain.Length; i++)
            {
                if (jointObjects.TryGetValue(chain[i], out var joint) && joint.gameObject.activeSelf)
                {
                    line.SetPosition(lineIndex, root.InverseTransformPoint(joint.position));
                    lineIndex++;
                }
            }
        }
    }
}
