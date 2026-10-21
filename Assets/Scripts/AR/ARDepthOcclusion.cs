using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Gridlock.AR
{
    /// <summary>
    /// Real things in front of the tabletop model hide it, but the surface it stands on never does. ARShaderOcclusion on
    /// the same camera stops AR Foundation's own background occlusion and publishes the depth texture; this draws it
    /// into the depth buffer with a margin (see OSPlays/ARDepthOcclusion), on a quad just in front of the camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ARDepthOcclusion : MonoBehaviour
    {
        static readonly int DisplayTransform = Shader.PropertyToID("_OcclusionDisplayTransform");
        static readonly int Scale = Shader.PropertyToID("_OcclusionScale");
        static readonly int EnvironmentDepth = Shader.PropertyToID("_EnvironmentDepth");

        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] Material material;

        Camera view;
        Transform quad;
        Renderer quadRenderer;
        bool haveFrame;

        public void Init(ARCameraManager manager, Material occlusionMaterial)
        {
            cameraManager = manager;
            material = occlusionMaterial;
        }

        void Awake()
        {
            view = GetComponent<Camera>();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Depth Occlusion";
            Destroy(go.GetComponent<Collider>());
            quad = go.transform;
            quad.SetParent(transform, false);
            quadRenderer = go.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = material = new Material(material);
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.enabled = false;
        }

        void OnEnable()
        {
            if (cameraManager != null) cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            if (quadRenderer != null) quadRenderer.enabled = false;
        }

        void OnFrame(ARCameraFrameEventArgs frame)
        {
            if (!frame.displayMatrix.HasValue) return;
            material.SetMatrix(DisplayTransform, frame.displayMatrix.Value);
            haveFrame = true;
        }

        void LateUpdate()
        {
            // Only once the phone actually has depth (not every ARCore phone does).
            quadRenderer.enabled = haveFrame && Shader.GetGlobalTexture(EnvironmentDepth) != null;
            if (!quadRenderer.enabled) return;

            // The rig is scaled to the model's size: real metres to world units.
            material.SetFloat(Scale, transform.localToWorldMatrix.GetColumn(2).magnitude);

            // Fill the view just beyond the near plane (the shader writes its own depth, so where it sits does not matter).
            float scale = transform.lossyScale.x;
            float distance = view.nearClipPlane * 2f / scale;
            float height = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
            quad.localPosition = new Vector3(0f, 0f, distance);
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(height * view.aspect * 1.2f, height, 1f);
        }
    }
}
