using Gridlock.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Gridlock.XR
{
    /// <summary>
    /// Teleport for tracked hands, which have no thumbstick: point the hand ray at open ground, pinch, then release
    /// to jump there. A landing ring follows the ray while the pinch is held; pointing at a car, a bay or a panel
    /// pinches that instead, so selecting and teleporting never fight.
    /// Turning: pinch anywhere free and swipe the hand sideways (like flicking a page) to snap-turn that way; the swipe
    /// cancels any teleport aim. Release the pinch to swipe again.
    /// </summary>
    public class HandTeleport : MonoBehaviour
    {
        [SerializeField] NearFarInteractor[] handInteractors;
        [SerializeField] TeleportationProvider provider;
        [SerializeField] Transform reticle;
        [SerializeField] float maxDistance = 30f;
        [Tooltip("Minimum surface normal Y for a valid landing spot.")]
        [SerializeField] float minUpDot = 0.7f;
        [SerializeField] XROrigin origin;
        [Tooltip("Sideways hand travel during a pinch that counts as a turn swipe (metres).")]
        [SerializeField] float swipeDistance = 0.1f;
        [Tooltip("Hand speed (m/s) below which movement is aiming, not swiping.")]
        [SerializeField] float minSwipeSpeed = 0.35f;

        NearFarInteractor aiming;
        Vector3 target;
        bool targetValid;
        NearFarInteractor swiping;
        float swipeStartX, lastX;
        bool swipeUsed;

        public void Init(NearFarInteractor[] hands, TeleportationProvider teleportation, Transform landingReticle, XROrigin xrOrigin)
        {
            handInteractors = hands;
            provider = teleportation;
            reticle = landingReticle;
            origin = xrOrigin;
        }

        void Awake() => reticle.gameObject.SetActive(false);

        void OnDisable()
        {
            aiming = null;
            if (reticle != null) reticle.gameObject.SetActive(false);
        }

        void Update()
        {
            UpdateSwipe();
            if (aiming != null)
            {
                UpdateAim();
                return;
            }

            foreach (var hand in handInteractors)
            {
                if (hand == null || !hand.isActiveAndEnabled) continue;
                if (!hand.selectInput.ReadWasPerformedThisFrame() || Busy(hand)) continue;
                if (swiping == null) StartSwipe(hand);
                if (!TryGround(hand, out _)) continue;
                aiming = hand;
                UpdateAim();
                return;
            }
        }

        void StartSwipe(NearFarInteractor hand)
        {
            swiping = hand;
            swipeStartX = lastX = SideOf(hand);
            swipeUsed = false;
        }

        /// <summary>Hand position across the view, in metres (head-relative, so turning the head doesn't count).</summary>
        float SideOf(NearFarInteractor hand)
        {
            var head = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            return head == null ? 0f : head.InverseTransformPoint(hand.transform.position).x;
        }

        void UpdateSwipe()
        {
            if (swiping == null) return;
            if (!swiping.isActiveAndEnabled || !swiping.selectInput.ReadIsPerformed())
            {
                swiping = null;
                return;
            }
            if (swipeUsed || origin == null) return;

            // Only a quick flick builds up a swipe: slow drift (aiming a teleport) keeps resetting its start.
            float x = SideOf(swiping);
            float speed = Mathf.Abs(x - lastX) / Mathf.Max(Time.deltaTime, 0.001f);
            lastX = x;
            if (speed < minSwipeSpeed) swipeStartX = x;
            float moved = x - swipeStartX;
            if (Mathf.Abs(moved) < swipeDistance) return;
            swipeUsed = true;
            if (aiming == swiping) Cancel();
            origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(moved) * GameSettings.SnapDegrees);
        }

        void UpdateAim()
        {
            if (aiming == null) return;
            if (!aiming.isActiveAndEnabled)
            {
                Cancel();
                return;
            }

            if (aiming.selectInput.ReadIsPerformed())
            {
                targetValid = !Busy(aiming) && TryGround(aiming, out target);
                reticle.gameObject.SetActive(targetValid);
                if (targetValid) reticle.SetPositionAndRotation(target + Vector3.up * 0.02f, Quaternion.identity);
                return;
            }

            // Pinch released: go.
            if (targetValid)
                provider.QueueTeleportRequest(new TeleportRequest
                {
                    destinationPosition = target,
                    destinationRotation = Quaternion.identity,
                    matchOrientation = MatchOrientation.None,
                    requestTime = Time.time,
                });
            Cancel();
        }

        void Cancel()
        {
            aiming = null;
            targetValid = false;
            reticle.gameObject.SetActive(false);
        }

        /// <summary>The ray is on a car, a bay or a UI panel, so the pinch belongs to that.</summary>
        static bool Busy(NearFarInteractor hand) =>
            hand.hasHover || hand.hasSelection || hand.TryGetCurrentUIRaycastResult(out _);

        bool TryGround(NearFarInteractor hand, out Vector3 point)
        {
            point = default;
            var origin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out var hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < minUpDot || hit.collider.GetComponentInParent<BaseTeleportationInteractable>() == null)
                return false;
            point = hit.point;
            return true;
        }
    }
}
