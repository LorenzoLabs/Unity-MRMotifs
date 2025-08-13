using UnityEngine;
using System;

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Emergency input hook: pressing the controller trigger (Meta OVR) or a keyboard key
    /// closes the menu panel and shows the alert panel. Uses fade if available.
    /// </summary>
    public class QuickPanelHotkey : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private GameObject menuPanelRoot;
        [SerializeField] private GameObject alertPanelRoot;

        [Header("Behavior")]
        [Tooltip("If true and PanelButtonActions is present, uses fade transitions. Otherwise toggles active state immediately.")]
        [SerializeField] private bool useFadeIfAvailable = true;
        [Tooltip("Cooldown seconds to prevent multiple rapid triggers.")]
        [SerializeField] private float cooldownSeconds = 0.5f;

        [Header("Editor Fallback")] 
        [SerializeField] private KeyCode editorKey = KeyCode.Space;

        private float lastTriggeredTime;
        private bool wasTriggerPressed;

        private void Update()
        {
            if (Time.time < lastTriggeredTime + cooldownSeconds) return;

            // Editor keyboard fallback
            if (Application.isEditor && Input.GetKeyDown(editorKey))
            {
                TriggerSequence();
                return;
            }

            // SIMPLE HACK: Unity Input Manager fallback for any joystick buttons/triggers
            if (Input.GetKeyDown(KeyCode.JoystickButton0) ||   // A on Xbox/Meta controller
                Input.GetKeyDown(KeyCode.JoystickButton1) ||   // B 
                Input.GetKeyDown(KeyCode.JoystickButton2) ||   // X
                Input.GetKeyDown(KeyCode.JoystickButton3) ||   // Y
                Input.GetKeyDown(KeyCode.JoystickButton14) ||  // Right index trigger
                Input.GetKeyDown(KeyCode.JoystickButton15) ||  // Left index trigger
                (Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.8f && !wasTriggerPressed) ||
                (Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.8f && !wasTriggerPressed) ||
                Input.GetButtonDown("Fire1") ||                // Left mouse/trigger
                Input.GetButtonDown("Jump"))                   // Space/A button
            {
                TriggerSequence();
                return;
            }

            // Track trigger state to prevent repeat firing
            bool triggerDown = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.8f ||
                               Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.8f;
            wasTriggerPressed = triggerDown;

            // Meta OVR controllers as backup
            if (IsOVRTriggerPressed())
            {
                TriggerSequence();
            }
        }

        private void TriggerSequence()
        {
            lastTriggeredTime = Time.time;

            if (menuPanelRoot && menuPanelRoot.activeInHierarchy)
            {
                if (useFadeIfAvailable)
                {
                    var actions = menuPanelRoot.GetComponentInChildren<PanelButtonActions>(true);
                    if (actions)
                    {
                        actions.CloseAndShow(alertPanelRoot);
                        return;
                    }
                }
                // Fallback: hard toggle
                menuPanelRoot.SetActive(false);
                if (alertPanelRoot) alertPanelRoot.SetActive(true);
                return;
            }

            // If menu isn't active, just show alert
            if (alertPanelRoot)
            {
                alertPanelRoot.SetActive(true);
            }
        }

        private bool IsOVRTriggerPressed()
        {
            // Try to use OVRInput via reflection to avoid compile-time dependency if package/layout changes
            var ovrType = Type.GetType("OVRInput", false);
            if (ovrType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    ovrType = asm.GetType("OVRInput", false);
                    if (ovrType != null) break;
                }
            }
            if (ovrType == null) return false;

            try
            {
                var buttonEnum = ovrType.GetNestedType("Button");
                var axis1dEnum = ovrType.GetNestedType("Axis1D");
                if (buttonEnum == null) return false;

                var getDown = ovrType.GetMethod("GetDown", new Type[] { buttonEnum });
                var getBtn = ovrType.GetMethod("Get", new Type[] { buttonEnum });
                var getAxis = axis1dEnum != null ? ovrType.GetMethod("Get", new Type[] { axis1dEnum }) : null;

                // 1) Try face buttons as a reliable click: A/B on right controller
                foreach (var name in new[] { "One", "Two", "Start", "PrimaryHandTrigger", "SecondaryHandTrigger", "PrimaryIndexTrigger", "SecondaryIndexTrigger" })
                {
                    object btn;
                    try { btn = Enum.Parse(buttonEnum, name); } catch { continue; }
                    if (getDown != null && (bool)getDown.Invoke(null, new object[] { btn })) return true;
                    if (getBtn != null && (bool)getBtn.Invoke(null, new object[] { btn })) return true;
                }

                // 2) If available, treat index triggers as analog and apply threshold
                if (axis1dEnum != null && getAxis != null)
                {
                    foreach (var name in new[] { "PrimaryIndexTrigger", "SecondaryIndexTrigger" })
                    {
                        object axis;
                        try { axis = Enum.Parse(axis1dEnum, name); } catch { continue; }
                        float val = (float)getAxis.Invoke(null, new object[] { axis });
                        if (val >= 0.75f) return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}


