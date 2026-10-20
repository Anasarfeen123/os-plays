using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Gridlock.XR
{
    /// <summary>
    /// Keeps world-space UI usable with controller rays and hand pokes. Imported scenes often put decorative TMP text,
    /// panels, or a nested canvas in front of the actual control; those graphics would receive the ray first and make a
    /// button look broken. Interactive controls retain their targets, while display-only graphics become transparent to
    /// UI raycasts (except inside a scroll view, which needs them to be dragged). The rescan also covers UI created after
    /// scene load: often while a scene assembles itself, rarely after that (each pass walks every canvas).
    /// </summary>
    public sealed class UIInteractionReliability : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float rescanSeconds = 0.75f;
        [SerializeField, Min(0.1f)] float settledRescanSeconds = 3f;
        [Tooltip("Seconds after a scene load during which the fast rescan runs.")]
        [SerializeField, Min(0f)] float settleSeconds = 8f;

        readonly List<Selectable> selectables = new List<Selectable>();
        readonly List<Graphic> graphics = new List<Graphic>();
        float sceneLoadedAt;

        /// <summary>
        /// Existing generated scenes and imported scenes get the same protection immediately after scripts recompile;
        /// rebuilding a scene is therefore an optimisation, not a requirement for a usable UI.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<UIInteractionReliability>() != null) return;
            var host = new GameObject("UI Interaction Reliability");
            host.AddComponent<UIInteractionReliability>();
            DontDestroyOnLoad(host);
        }

        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) =>
            sceneLoadedAt = Time.realtimeSinceStartup;

        IEnumerator Start()
        {
            sceneLoadedAt = Time.realtimeSinceStartup;
            var fast = new WaitForSecondsRealtime(rescanSeconds);
            var slow = new WaitForSecondsRealtime(settledRescanSeconds);
            while (true)
            {
                Repair();
                yield return Time.realtimeSinceStartup - sceneLoadedAt < settleSeconds ? fast : slow;
            }
        }

        void Repair()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsWorldCanvas(canvas)) continue;

                bool hasControl = false;
                canvas.GetComponentsInChildren(true, selectables);
                foreach (var selectable in selectables)
                {
                    if (selectable.GetComponentInParent<Canvas>(true) != canvas) continue;
                    hasControl = true;
                    break;
                }

                if (hasControl && canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                canvas.GetComponentsInChildren(true, graphics);
                foreach (var graphic in graphics)
                {
                    if (!graphic.raycastTarget || graphic.GetComponentInParent<Canvas>(true) != canvas) continue;
                    if (graphic.GetComponentInParent<ScrollRect>(true) != null) continue; // the drag area of a scroll view
                    // Labels, frames and translucent text boxes should never be the first UI ray hit. A graphic inside a
                    // Selectable stays interactive so buttons, sliders, toggles and input fields retain their normal hit area.
                    if (graphic.GetComponentInParent<Selectable>(true) == null)
                        graphic.raycastTarget = false;
                }
            }
        }

        static bool IsWorldCanvas(Canvas canvas)
        {
            var root = canvas.rootCanvas;
            return root != null && root.renderMode == RenderMode.WorldSpace;
        }
    }
}
