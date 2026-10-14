using UnityEngine;

namespace Gridlock.XR
{
    /// <summary>
    /// Button cheat card above a controller: what A/B/X/Y, the stick, trigger and grip do in this scene. It fades in when
    /// the player glances at the controller (close to the centre of view and within reach) and turns to face them;
    /// it is a child of the controller, so it disappears with it when hands are tracked instead.
    /// </summary>
    public class ControllerHints : MonoBehaviour
    {
        [SerializeField] Transform controller;
        [SerializeField] CanvasGroup card;
        [SerializeField] float glanceAngle = 32f;
        [SerializeField] float maxDistance = 0.75f;
        [SerializeField] float fadeSpeed = 6f;

        Transform head;

        public void Init(Transform controllerTransform, CanvasGroup cardGroup)
        {
            controller = controllerTransform;
            card = cardGroup;
        }

        void OnEnable()
        {
            if (card != null) card.alpha = 0f;
        }

        void LateUpdate()
        {
            if (head == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                head = cam.transform;
            }

            var toController = controller.position - head.position;
            bool glancing = toController.magnitude < maxDistance && Vector3.Angle(head.forward, toController) < glanceAngle;
            card.alpha = Mathf.MoveTowards(card.alpha, glancing ? 1f : 0f, fadeSpeed * Time.deltaTime);

            // Float just above the controller, upright and turned towards the eyes.
            var t = card.transform;
            t.position = controller.position + Vector3.up * 0.075f;
            var away = t.position - head.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}
