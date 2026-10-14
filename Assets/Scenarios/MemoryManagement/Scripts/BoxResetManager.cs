
using UnityEngine;
using System.Collections;

public class BoxResetManager : MonoBehaviour
{
    [System.Serializable]
    public class BoxData
    {
        public Transform box;

        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public Quaternion startRotation;
        [HideInInspector] public Transform startParent;
        [HideInInspector] public Rigidbody body;
        [HideInInspector] public bool wasKinematic;
    }

    [SerializeField] private BoxData[] boxes;
    [SerializeField] private float resetDelay = 0.5f;

    private bool isResetting;

    private void Awake()
    {
        foreach (BoxData data in boxes)
        {
            if (data.box == null)
                continue;

            data.startPosition = data.box.position;
            data.startRotation = data.box.rotation;
            data.startParent = data.box.parent;
            data.body = data.box.GetComponent<Rigidbody>();

            if (data.body != null)
                data.wasKinematic = data.body.isKinematic;
        }
    }

    public void ResetBoxes()
    {
        if (!isResetting)
            StartCoroutine(ResetBoxesRoutine());
    }

    private IEnumerator ResetBoxesRoutine()
    {
        isResetting = true;

        yield return new WaitForSeconds(resetDelay);

        foreach (BoxData data in boxes)
        {
            if (data.box == null)
                continue;

            if (data.body != null)
            {
                data.body.isKinematic = true;
                data.body.linearVelocity = Vector3.zero;
                data.body.angularVelocity = Vector3.zero;
            }

            data.box.SetParent(data.startParent, true);
            data.box.SetPositionAndRotation(
                data.startPosition,
                data.startRotation
            );
        }

        Physics.SyncTransforms();

        foreach (BoxData data in boxes)
        {
            if (data.body != null)
            {
                data.body.isKinematic = data.wasKinematic;
                data.body.linearVelocity = Vector3.zero;
                data.body.angularVelocity = Vector3.zero;
            }
        }

        isResetting = false;
    }
}
