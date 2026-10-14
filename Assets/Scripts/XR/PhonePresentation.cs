using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Gridlock.XR
{
    /// <summary>
    /// Phone setup for the desktop presentation. XRUIInputModule only tracks a touch's position (it never presses
    /// buttons without a mouse), so the HUD gets the Input System's touch-aware UI module instead. The camera drops
    /// SMAA (4× MSAA comes from the Mobile URP asset), runs at 60 fps and the screen stays awake.
    /// </summary>
    public static class PhonePresentation
    {
        public static void Apply(GameObject desktopRig)
        {
            var events = EventSystem.current != null ? EventSystem.current : Object.FindAnyObjectByType<EventSystem>();
            if (events != null && events.GetComponent<InputSystemUIInputModule>() == null)
            {
                var xrModule = events.GetComponent<XRUIInputModule>();
                if (xrModule != null) Object.DestroyImmediate(xrModule);
                events.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            foreach (var cam in desktopRig.GetComponentsInChildren<Camera>(true))
            {
                cam.allowHDR = false;
                cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
            }

            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
            if (desktopRig.GetComponent<PhoneBackButton>() == null) desktopRig.AddComponent<PhoneBackButton>();
        }
    }
}
