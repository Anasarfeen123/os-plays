using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime toggle for the legacy 2D SchedulingUI canvas.
/// Press [U] to open/close the full simulator UI on top of the 3D lab.
/// The 3D physical arena stays the primary runtime experience; this is an
/// optional overlay for the classic full-screen simulator view.
/// </summary>
public class SchedulingUIToggle : MonoBehaviour
{
    private SchedulingUIController ui;

    private void Update()
    {
        bool uPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            uPressed = Keyboard.current.uKey.wasPressedThisFrame;
        }
#else
        uPressed = Input.GetKeyDown(KeyCode.U);
#endif
        if (uPressed) ToggleUI();
    }

    public void ToggleUI()
    {
        ui = SchedulingUIController.Instance;
        if (ui == null)
        {
            // The SchedulingUI canvas starts disabled, so its Awake never ran.
            // Locate the component even on inactive objects.
            var all = Object.FindObjectsByType<SchedulingUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all != null && all.Length > 0) ui = all[0];
        }

        if (ui == null)
        {
            Debug.LogWarning("[SchedulingUIToggle] No SchedulingUIController found in scene.");
            return;
        }

        bool shown = ui.gameObject.activeInHierarchy && ui.IsUIPanelActive;
        if (shown)
        {
            ui.CloseUI();
            ui.gameObject.SetActive(false);
            Debug.Log("[SchedulingUIToggle] SchedulingUI closed. Press [U] to reopen.");
        }
        else
        {
            ui.gameObject.SetActive(true);
            ui.OpenUI();
            Debug.Log("[SchedulingUIToggle] SchedulingUI opened. Press [U] to close.");
        }

        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }
}

