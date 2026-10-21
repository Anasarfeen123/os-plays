using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Gridlock.AR
{
    /// <summary>
    /// Real things in front of the tabletop model hide it, but the surface it stands on never does. AR Foundation's own
    /// background occlusion is switched off (the camera background is told that shader occlusion handles it, which is
    /// what an ARShaderOcclusion component would do; that component throws when the occlusion manager shuts down), and
    /// the depth texture is drawn into the depth buffer with a margin instead (see OSPlays/ARDepthOcclusion), on a quad
    /// just in front of the camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ARDepthOcclusion : MonoBehaviour
    {
        static readonly int DisplayTransform = Shader.PropertyToID("_OcclusionDisplayTransform");
        static readonly int Scale = Shader.PropertyToID("_OcclusionScale");
        static readonly int EnvironmentDepth = Shader.PropertyToID("_EnvironmentDepth");

        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] AROcclusionManager occlusionManager;
        [SerializeField] Material material;

        Camera view;
        Transform quad;
        Renderer quadRenderer;
        bool haveFrame;

        public void Init(ARCameraManager manager, AROcclusionManager occlusion, Material occlusionMaterial)
        {
            cameraManager = manager;
            occlusionManager = occlusion;
            material = occlusionMaterial;
        }

        /// <summary>
        /// Tell this camera's ARCameraBackground that occlusion is done by shaders, so it stops writing the unbiased depth.
        /// (ARShaderOcclusion raises the same internal event when it is enabled.)
        /// </summary>
        void Start()
        {
            var field = typeof(ARShaderOcclusion).GetField("shaderOcclusionComponentEnabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (field?.GetValue(null) is Action<GameObject> raise) raise(gameObject);
            else if (field == null) Debug.LogWarning("[GRIDLOCK] AR occlusion: could not switch off the background's own occlusion");
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
            Texture depth = null;
            bool hasDepth = occlusionManager != null && occlusionManager.TryGetEnvironmentDepthTexture(out depth) && depth != null;
            quadRenderer.enabled = haveFrame && hasDepth;
            if (!quadRenderer.enabled) return;
            material.SetTexture(EnvironmentDepth, depth);

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
