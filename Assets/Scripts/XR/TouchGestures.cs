using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Gridlock.XR
{
    /// <summary>
    /// Phone gestures, read once per frame and shared by every touch-driven script: one-finger drag, two-finger pinch
    /// and pan, and taps. Touches that start on the screen-space HUD belong to the UI and are ignored here.
    /// </summary>
    public static class TouchGestures
    {
        const float TapSeconds = 0.35f;

        class Track
        {
            public bool OverUI, Moved, Multi;
            public int Seen;
        }

        static readonly Dictionary<int, Track> Tracks = new Dictionary<int, Track>();
        static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        static readonly List<Touch> Live = new List<Touch>();
        static readonly List<int> Stale = new List<int>();
        static int frame = -1;
        static Vector2 drag, pan, tapPosition;
        static float pinch = 1f;
        static bool tapped;

        /// <summary>Editor/test override: behave as a phone on desktop.</summary>
        public static bool ForceTouch { get; set; }

        public static bool IsTouchDevice => Application.isMobilePlatform || ForceTouch;

        /// <summary>One-finger drag this frame, in pixels (zero until the finger has moved past the tap slop).</summary>
        public static Vector2 Drag { get { Refresh(); return drag; } }

        /// <summary>Two-finger midpoint movement this frame, in pixels.</summary>
        public static Vector2 Pan { get { Refresh(); return pan; } }

        /// <summary>Two-finger spread ratio this frame: above 1 when the fingers move apart.</summary>
        public static float Pinch { get { Refresh(); return pinch; } }

        /// <summary>True on the frame a short single-finger tap outside the HUD is released.</summary>
        public static bool Tapped(out Vector2 position)
        {
            Refresh();
            position = tapPosition;
            return tapped;
        }

        static float Slop => Mathf.Max(14f, (Screen.dpi > 0f ? Screen.dpi : 320f) * 0.08f);

        static void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            drag = pan = Vector2.zero;
            pinch = 1f;
            tapped = false;
            if (!IsTouchDevice) return;
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();

            Live.Clear();
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.began || !Tracks.ContainsKey(touch.touchId))
                    Tracks[touch.touchId] = new Track { OverUI = OverUI(touch.startScreenPosition) };
                var track = Tracks[touch.touchId];
                track.Seen = frame;
                if ((touch.screenPosition - touch.startScreenPosition).sqrMagnitude > Slop * Slop) track.Moved = true;

                if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                {
                    if (touch.phase == TouchPhase.Ended && !track.OverUI && !track.Moved && !track.Multi &&
                        touch.time - touch.startTime < TapSeconds)
                    {
                        tapped = true;
                        tapPosition = touch.screenPosition;
                    }
                    Tracks.Remove(touch.touchId);
                }
                else if (!track.OverUI)
                {
                    Live.Add(touch);
                }
            }

            // Touches that vanished without an end (focus loss) are forgotten rather than left stuck.
            Stale.Clear();
            foreach (var pair in Tracks)
                if (pair.Value.Seen != frame) Stale.Add(pair.Key);
            foreach (var id in Stale) Tracks.Remove(id);

            if (Live.Count == 1 && Tracks[Live[0].touchId].Moved)
            {
                drag = Live[0].delta;
            }
            else if (Live.Count >= 2)
            {
                Touch a = Live[0], b = Live[1];
                Tracks[a.touchId].Multi = Tracks[b.touchId].Multi = true;
                float now = Vector2.Distance(a.screenPosition, b.screenPosition);
                float before = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
                if (before > 1f) pinch = now / before;
                pan = (a.delta + b.delta) * 0.5f;
            }
        }

        /// <summary>Only the screen-space HUD counts: world-space canvases (scoreboard, monitor) sit in the 3D scene.</summary>
        static bool OverUI(Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            Hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, Hits);
            foreach (var hit in Hits)
            {
                var canvas = hit.gameObject.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.WorldSpace) return true;
            }
            return false;
        }
    }
}
