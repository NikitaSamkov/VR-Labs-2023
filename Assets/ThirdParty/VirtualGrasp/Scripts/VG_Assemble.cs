// Copyright (C) 2014-2023 Gleechi AB. All rights reserved.

using UnityEngine;
using System.Collections.Generic;
using VirtualGrasp;
using UnityEngine.Events;

namespace VirtualGrasp.Scripts
{
    /** 
     * VG_Assemble provides a tool to assemble / dissemble an object through VG_Articulation.
     * The MonoBehavior provides a tutorial on the VG API functions VG_Controller.ChangeObjectJoint and RecoverObjectJoint.
     */
    [LIBVIRTUALGRASP_UNITY_SCRIPT]
    [HelpURL("https://docs.virtualgrasp.com/unity_component_vgassemble." + VG_Version.__VG_VERSION__ + ".html")]
    public class VG_Assemble : MonoBehaviour
    {
        [Tooltip("If this object will be reparented to the parent of the desired pose transform when it is assembled.")]
        public bool m_assembleToParent = true;
        [Tooltip("The target pose(s) of the assembled object (or assemble anchor if provided).")]
        public List<Transform> m_desiredPoses = new List<Transform>();
        [Tooltip("Threshold to assemble when object (or assemble anchor) to desired pose is smaller than this value (m).")]
        public float m_assembleDistance = 0.05f;
        [Tooltip("Threshold to assemble when object (or assemble anchor) to desired rotation is smaller than this value (deg).")]
        public float m_assembleAngle = 45;
        [Tooltip("The axis to be aligned for assembling to desired pose. If zero will match the whole rotation.")]
        public Vector3 m_assembleAxis = Vector3.forward;
        [Tooltip("If provided will match this anchor to the desired pose, otherwise match this object.")]
        public Transform m_assembleAnchor = null;
        
        [Tooltip("Threshold to disassemble when object (or assemble anchor) distance to desired pose is bigger than this value (m).")]
        public float m_disassembleDistance = 0.25f;
        [Tooltip("If only allow dissasemble when object is at the zero joint state (most relevant for screw joint).")]
        public bool m_disassembleOnZeroState = false;

        [Tooltip("The VG Articulation of constrained (non-FLOATING) joint type to switch to when assembling an object. If null will use Fixed joint.")]
        public VG_Articulation m_assembleArticulation = null;

        [Tooltip("The VG Articulation of floating joint type to switch to when disassembling an object. Must be provided if original joint is non-Floating.")]
        public VG_Articulation m_disassembleArticulation = null;

        [Tooltip("If force the disassembled object to become physical. Only relevant if original joint is non-Floating.")]
        public bool m_forceDisassembledPhysical = false;

        public UnityEvent<Transform> OnAssembled = new UnityEvent<Transform>();
        public UnityEvent<Transform> OnDisassembled = new UnityEvent<Transform>();

        private float m_timeAtDisassemble = 0.0F;
        private float m_assembleDelay = 2.0F;

        private Transform m_disassembleParent = null;
        private Transform m_desiredPose = null;

        void Start()
        {
            if (m_assembleArticulation == null)
                VG_Debug.LogWarning("Assemble Articulation is not assigned, so assemble will use Fixed joint for " + transform.name);
            else if (m_assembleArticulation.m_type == VG_JointType.FLOATING)
                VG_Debug.LogError("Assemble Articulation can not be FLOATING joint type on " + transform.name);

            m_disassembleParent = transform.parent;

            if (VG_Controller.GetObjectJointType(transform, true, out VG_JointType originalJointType) == VG_ReturnCode.SUCCESS
                && originalJointType != VG_JointType.FLOATING)
            {
                if (m_disassembleArticulation == null)
                    VG_Debug.LogError("Disassemble Articulation with FLOATING type has to be assigned on " + transform.name);
                else if (m_disassembleArticulation.m_type != VG_JointType.FLOATING)
                    VG_Debug.LogError("Disassemble Articulation should be FLOATING type on " + transform.name);

                // If originally is non-floating means disassemble parent should be original parent's parent
                m_disassembleParent = transform.parent.parent;
            }

            if (m_assembleAnchor == null)
                m_assembleAnchor = transform;
        }

        void LateUpdate()
        {
            assembleByJointChange();
            disassembleByJointChange();
        }

        void assembleByJointChange()
        {
            Quaternion relRot = Quaternion.identity;
            if (!findTarget(ref relRot))
                return;

            VG_JointType jointType;
            if ((Time.timeSinceLevelLoad - m_timeAtDisassemble) > m_assembleDelay
               && VG_Controller.GetObjectJointType(transform, false, out jointType) == VG_ReturnCode.SUCCESS &&
               jointType == VG_JointType.FLOATING)
            {
                m_desiredPose.gameObject.SetActive(false);

                // Project object rotation axis to align to desired rotation axis.
                transform.SetPositionAndRotation(transform.position, relRot * transform.rotation);
                Vector3 offset = m_desiredPose.position - m_assembleAnchor.position;
                transform.SetPositionAndRotation(transform.position + offset, transform.rotation);

                if (m_assembleToParent)
                    transform.SetParent(m_desiredPose.parent);

                VG_ReturnCode ret = m_assembleArticulation ? VG_Controller.ChangeObjectJoint(transform, m_assembleArticulation) : VG_Controller.ChangeObjectJoint(transform, VG_JointType.FIXED);

                if (ret != VG_ReturnCode.SUCCESS)
                    VG_Debug.LogError("Failed to ChangeObjectJoint() on " + transform.name + " with return code " + ret, transform);

                OnAssembled.Invoke(transform);

            }
        }

        void disassembleByJointChange()
        {
            foreach (VG_HandStatus hand in VG_Controller.GetHands())
            {
                VG_JointType jointType;
                if (hand.m_selectedObject == transform && hand.IsHolding()
                    && VG_Controller.GetObjectJointType(transform, false, out jointType) == VG_ReturnCode.SUCCESS
                    && jointType != VG_JointType.FLOATING)
                {
                    getSensorControlledAnchorPose(hand, out Vector3 sensor_anchor_pos, out Quaternion sensor_anchor_rot);

                    if (isZeroState(jointType)
                        && (sensor_anchor_pos - m_assembleAnchor.position).magnitude > m_disassembleDistance
                        )
                    {
                        if (m_desiredPose != null)
                            m_desiredPose.gameObject.SetActive(true);
                        if (m_assembleToParent)
                            transform.SetParent(transform.parent.parent);
                        else
                            transform.SetParent(m_disassembleParent);

                        VG_Controller.GetObjectJointType(transform, true, out VG_JointType originalJointType);
                        if (originalJointType == VG_JointType.FLOATING)
                        {
                            if (VG_Controller.RecoverObjectJoint(transform) != VG_ReturnCode.SUCCESS)
                            {
                                VG_Debug.LogError("Failed to disassemble with RecoverObjectJoint() on " + transform.name);
                                return;
                            }

                        }
                        else if (m_disassembleArticulation != null)
                        {
                            if (VG_Controller.ChangeObjectJoint(transform, m_disassembleArticulation) != VG_ReturnCode.SUCCESS)
                            {
                                VG_Debug.LogError("Failed to disassemble with ChangeObjectJoint() on " + transform.name);
                                return;
                            }
                            // When object originally has VG constrained joint type (non-Floating) it has to be a non-physical object,
                            // therefore disassemble will not recover its original physical property, so here we add rigid body
                            // if user choose to m_makeDisassembledPhysical.
                            if (m_forceDisassembledPhysical && !transform.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
                            {
                                rb = transform.gameObject.AddComponent<Rigidbody>();
                                rb.useGravity = true;
                                if (!transform.TryGetComponent<Collider>(out _))
                                {
                                    MeshCollider collider = transform.gameObject.AddComponent<MeshCollider>();
                                    collider.convex = true;
                                }
                            }
                        }
                        else
                        {
                            VG_Debug.LogError("Failed to disassemble with ChangeObjectJoint() since Disassemble Articulation on " + transform.name + " is not assigned.");
                            return;
                        }

                        m_timeAtDisassemble = Time.timeSinceLevelLoad;

                        OnDisassembled.Invoke(transform);
                    }
                }
            }
        }

        bool isZeroState(VG_JointType jointType)
        {
            if (!m_disassembleOnZeroState)
                return true;

            // If object is of a joint type that has no relevant joint states, then no zero state control for disassemble so return true
            VG_Controller.GetObjectJointState(transform, out float jointState);
            if (jointType == VG_JointType.REVOLUTE || jointType == VG_JointType.PRISMATIC || jointType == VG_JointType.CONE)
                return jointState == 0.0F;
            else if (jointType == VG_JointType.PLANAR)
            {
                VG_Controller.GetObjectSecondaryJointState(transform, out float jointState2);
                return (jointState == 0 && jointState2 == 0);
            }
            else
                return true;
        }

        void getSensorControlledAnchorPose(VG_HandStatus hand, out Vector3 anchorPos, out Quaternion anchorRot)
        {
            // Compute relative pose of anchor to grasping hand pose
            Vector3 lp = Quaternion.Inverse(hand.m_hand.rotation) * (m_assembleAnchor.position - hand.m_hand.position);
            Quaternion lq = Quaternion.Inverse(hand.m_hand.rotation) * m_assembleAnchor.rotation;

            // Then evaluate anchor rotation corresponding to hand pose determined by sensor
            VG_Controller.GetSensorPose(hand.m_avatarID, hand.m_side, out Vector3 sensor_pos, out Quaternion sensor_rot);
            anchorPos = sensor_rot * lp + sensor_pos;
            anchorRot = sensor_rot * lq;
        }

        bool findTarget(ref Quaternion relRot)
        {
            m_desiredPose = null;
            foreach (Transform pose in m_desiredPoses)
            {
                if (closeToTargetPose(m_assembleAnchor, pose, m_assembleAxis, ref relRot))
                {
                    m_desiredPose = pose;
                    return true;
                }
            }
            return false;
        }

        bool closeToTargetPose(Transform anchor, Transform target, Vector3 axisType, ref Quaternion relRot)
        {
            relRot = Quaternion.identity;
            float angle = 0.0F;
            if (axisType == Vector3.up)
            {
                relRot = Quaternion.FromToRotation(anchor.up, target.up);
            }
            else if (axisType == Vector3.forward)
            {
                relRot = Quaternion.FromToRotation(anchor.forward, target.forward);
                relRot.ToAngleAxis(out angle, out _);
            }
            else if (axisType == Vector3.right)
            {
                relRot = Quaternion.FromToRotation(anchor.right, target.right);
                relRot.ToAngleAxis(out angle, out _);
            }
            else
            {
                relRot = target.rotation * Quaternion.Inverse(anchor.rotation);
                angle = Quaternion.Angle(anchor.rotation, target.rotation);
            }

            return (target.position - anchor.position).magnitude < m_assembleDistance && (angle < m_assembleAngle);
        }
    }
}
