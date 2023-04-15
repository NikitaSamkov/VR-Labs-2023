// Copyright (C) 2014-2023 Gleechi AB. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using VirtualGrasp.Controllers;

namespace VirtualGrasp.Scripts
{
    /** 
     * VG_ExternalControllerManager exemplifies how you could provide custom controller scripts for your application.
     * The class, used in MyVirtualGrasp.cs, provides a tutorial on the VG API functions for external sensor control.
     */
    [LIBVIRTUALGRASP_UNITY_SCRIPT]
    [HelpURL("https://docs.virtualgrasp.com/unity_component_vgexternalcontrollermanager." + VG_Version.__VG_VERSION__ + ".html")]
    public class VG_ExternalControllerManager
    {
        static public void Initialize(VG_MainScript vg)
        {
            foreach (VG_Avatar avatar in vg.m_avatars)
            {
                if (avatar.m_primarySensorSetup.m_profile == null || avatar.m_primarySensorSetup.m_profile.m_sensor != VG_SensorType.EXTERNAL_CONTROLLER)
                    continue;

                RegisterExternalController(avatar.m_avatarID, avatar.m_primarySensorSetup.m_profile);

                if (avatar.m_secondarySensorSetup.m_profile == null || avatar.m_secondarySensorSetup.m_profile.m_sensor != VG_SensorType.EXTERNAL_CONTROLLER)
                    continue;

                RegisterExternalController(avatar.m_avatarID, avatar.m_secondarySensorSetup.m_profile);
            }
        }

        // Register an external controller type for an avatar.
        static public void RegisterExternalController(int avatarID, VG_ControllerProfile controllerType)
        {
            Dictionary<int, List<VG_ExternalController>> controllers = new Dictionary<int, List<VG_ExternalController>>();

            string[] controllerTypes = controllerType.m_externalType.Split(';');
            foreach (VG_HandSide side in new List<VG_HandSide>() { VG_HandSide.LEFT, VG_HandSide.RIGHT })
            {
                if (VG_Controller.GetBone(avatarID, side, VG_BoneType.WRIST, out int wristID, out _) == null)
                    continue;

                foreach (string controller in controllerTypes)
                {
                    if (!controllers.ContainsKey(wristID)) controllers[wristID] = new List<VG_ExternalController>();
                    switch (controller)
                    {
                        case "OculusHand": controllers[wristID].Add(new VG_EC_OculusHand(avatarID, side)); break;
                        case "UnityXR": controllers[wristID].Add(new VG_EC_UnityXRHand(avatarID, side)); break;
                        case "MouseHand": controllers[wristID].Add(new VG_EC_MouseHand(avatarID, side)); break;
                        case "LeapHand": controllers[wristID].Add(new VG_EC_LeapHand(avatarID, side)); break;
                        case "SteamHand": controllers[wristID].Add(new VG_EC_SteamHand(avatarID, side)); break;
                        //case "PicoHand": controllers[wristID].Add(new VG_EC_PicoHand(avatarID, side)); break;
                        case "UnityInteractionHand": controllers[wristID].Add(new VG_EC_UnityInteractionHand(avatarID, side)); break;
                        default:
                            //VG_Debug.LogWarning("No VG_ExternalController found for \"" + controller + "\". Program it and/or add it to this list. Replacing with VG_EC_GenericHand.");
                            controllers[wristID].Add(new VG_EC_GenericHand(avatarID, side));
                            break;
                    }
                }
            }

            VG_Controller.RegisterExternalControllers(controllers);
        }
    }
}