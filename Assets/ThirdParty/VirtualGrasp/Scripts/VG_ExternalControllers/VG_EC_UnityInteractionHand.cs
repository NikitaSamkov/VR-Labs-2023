// Copyright (C) 2014-2023 Gleechi AB. All rights reserved.

//#define VG_USE_UNITYINTERACTION_HAND

using System;
using System.Collections.Generic;
using UnityEngine;
#if VG_USE_UNITYINTERACTION_HAND
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
#endif

namespace VirtualGrasp.Controllers
{
    /**
     * This is an external controller class that supports the action-based Unity XR Interaction toolkit controller as an external controller.
     * Please refer to https://docs.virtualgrasp.com/controllers.html for the definition of an external controller for VG, and to
     * https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.0/manual/index.html for the plugin itself.
     * 
     * The following requirements have to be met to be able to enable the #define VG_USE_UNITYINTERACTION_HAND above and use the controller:
     * - You have the "XR Plugin Management" package installed into your Unity project.
     * - You have the "XR Interaction Toolkit" package installed into your Unity project.
     * - You have selected "OpenXR" as the Plugin-Provider in Project Settings -> XR Plugin Management
     * - if you use Oculus, you use it through "OpenXR" (Oculus -> Tools -> OVR Utilitites Plugin -> Set OVR to OpenXR)
     */

    [LIBVIRTUALGRASP_UNITY_SCRIPT]
    [HelpURL("https://docs.virtualgrasp.com/unity_vg_ec_unityinputhand." + VG_Version.__VG_VERSION__ + ".html")]
    public class VG_EC_UnityInteractionHand : VG_ExternalController
    {
#if VG_USE_UNITYINTERACTION_HAND
    static InputActionManager m_provider = null;
    private ActionBasedController m_controller = null;
#endif

        [Serializable]
        public class HandMapping : VG_ExternalControllerMapping
        {
            public override void Initialize(int avatarID, VG_HandSide side)
            {
                base.Initialize(avatarID, side);
                m_BoneToTransform = new Dictionary<int, Transform>()
            {
                { 0, Hand_WristRoot }
            };
            }
        }

        public VG_EC_UnityInteractionHand(int avatarID, VG_HandSide side)
        {
            m_avatarID = avatarID;
            m_handType = side;
            m_enabled = true;

#if VG_USE_UNITYINTERACTION_HAND
        m_enabled = true;
#else
            VG_Debug.LogError("You want a Action-based Interaction controller, but have not defined VG_USE_UNITYINPUT_HAND at the top of this file.");
            m_enabled = false;
#endif
        }

        public new void Initialize()
        {
#if VG_USE_UNITYINTERACTION_HAND
        if (m_provider == null) m_provider = GameObject.FindObjectOfType<InputActionManager>();
        InputActionAsset inputActionAsset = null;
        if (m_provider == null)
        {
            m_provider = GameObject.FindObjectOfType<VG_MainScript>().gameObject.AddComponent<InputActionManager>();
            inputActionAsset = Resources.Load<InputActionAsset>("XRI Default Input Actions");
            if (inputActionAsset != null)
            {
                m_provider.actionAssets = new List<InputActionAsset>{ inputActionAsset };
                m_provider.EnableInput();
            }
            else Debug.Log("Could not load inputactions.");                
        }

        if (m_provider != null && m_provider.actionAssets.Count > 0 && m_provider.actionAssets[0] != null)
        {
            m_mapping = new HandMapping();
            base.Initialize();

            string handSide = (m_handType == VG_HandSide.LEFT) ? "XRI LeftHand" : "XRI RightHand";
            //m_controller = m_mapping.Hand_WristRoot.gameObject.GetComponent<ActionBasedController>();

            // We put the ActionBasedController components on dummy GameObjects so they do not
            // affect the wrist transforms per se (this can't be disabled in the XRController it seems).
            GameObject controller = new GameObject(handSide);
            controller.transform.SetParent(m_provider.transform);
            m_controller = controller.AddComponent<ActionBasedController>();
            //m_controller = m_mapping.Hand_WristRoot.gameObject.AddComponent<ActionBasedController>();

            // Bind Position and Rotation Signals
            InputActionMap inputMap = m_provider.actionAssets[0].FindActionMap(handSide);
            if (inputMap == null) Debug.LogError("Could not find map " + handSide);
            else
            {
                m_controller.enableInputTracking = true;
                m_controller.updateTrackingType = XRBaseController.UpdateType.Update;
                InputAction inputAction = inputMap.FindAction("Position");
                if (inputAction != null) m_controller.positionAction = new InputActionProperty(inputAction);                        
                else Debug.LogError("Could not find action Position in " + handSide);
                inputAction = inputMap.FindAction("Rotation");
                if (inputAction != null) m_controller.rotationAction = new InputActionProperty(inputAction);
                else Debug.LogError("Could not find action Rotation in " + handSide);
                inputAction = inputMap.FindAction("Haptic Device");
                if (inputAction != null) m_controller.hapticDeviceAction = new InputActionProperty(inputAction);
                else Debug.LogError("Could not find action Haptic Device in " + handSide);
            }

            // Bind Trigger Signals
            inputMap = m_provider.actionAssets[0].FindActionMap(handSide + " Interaction");
            if (inputMap == null) Debug.LogError("Could not find map " + handSide);
            else
            {
                InputAction inputAction = inputMap.FindAction("Activate Value");
                if (inputAction != null) m_controller.activateAction = new InputActionProperty(inputAction);
                else Debug.LogError("Could not find action Activate Value in " + handSide);
                inputAction = inputMap.FindAction("Select Value");
                if (inputAction != null) m_controller.selectAction = new InputActionProperty(inputAction);
                else Debug.LogError("Could not find action Select Value in " + handSide);
            }
        }
        
        m_initialized = (m_provider != null && m_controller != null);
#endif
        }

        public override bool Compute()
        {
#if VG_USE_UNITYINTERACTION_HAND
        if (!m_enabled) return false;
        if (!m_initialized) { Initialize(); return false; }
        SetPose(0, Matrix4x4.TRS(m_controller.currentControllerState.position, m_controller.currentControllerState.rotation, Vector3.one));
#endif
            return true;
        }

        public override float GetGrabStrength()
        {
            float trigger = 0.0f;
#if VG_USE_UNITYINTERACTION_HAND
        if (!m_initialized) return 0.0f;
        switch (VG_Controller.GetGraspButton())
        {
            case VG_VrButton.TRIGGER:
                trigger = m_controller.currentControllerState.activateInteractionState.value; break;
            case VG_VrButton.GRIP:
                trigger = m_controller.currentControllerState.selectInteractionState.value; break;
            case VG_VrButton.GRIP_OR_TRIGGER:
                trigger = Mathf.Max(m_controller.currentControllerState.activateInteractionState.value,
                                    m_controller.currentControllerState.selectInteractionState.value);
                break;
        }
#endif
            return trigger;
        }

        public override Color GetConfidence()
        {
            //m_controller.hapticDeviceAction.
            return Color.yellow;
        }

        public override void HapticPulse(VG_HandStatus hand, float amplitude = 0.5F, float duration = 0.015F, int finger = 5)
        {
#if VG_USE_UNITYINTERACTION_HAND
        m_controller.SendHapticImpulse(amplitude, duration);
#endif
        }
    }
}