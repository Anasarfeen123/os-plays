using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gridlock.Editor
{
    /// <summary>
    /// Android phone build: the same scenes in AR instead of VR. The build uses the ARCore loader instead of OpenXR, so
    /// every scene is placed as a tabletop model (see XRModeController and ARStage; phones without ARCore get the touch
    /// view). It installs next to the Quest app under its own id and auto-rotates between the two landscape orientations.
    /// The Quest configuration is restored afterwards, so the project stays set up for the headset.
    /// Menu: GRIDLOCK ▸ Build Phone APK.
    /// Batchmode: -executeMethod Gridlock.Editor.GridlockPhoneSetup.ConfigureAndBuild (-buildTarget Android)
    /// </summary>
    public static class GridlockPhoneSetup
    {
        const string PackageId = "com.anasarfeen.gridlock.mobile";
        const string ApkPath = "Builds/OSPlays-Phone.apk";

        [MenuItem("GRIDLOCK/Build Phone APK")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.RevealInFinder(ApkPath);
        }

        /// <summary>Batchmode entry: rebuild the scenes, then build the phone APK; exits non-zero on failure.</summary>
        public static void ConfigureAndBuild()
        {
            GridlockSceneBuilder.Build();
            Build();
        }

        static void Build()
        {
            try
            {
                Configure();
                GridlockQuestSetup.BuildApk(ApkPath, "Phone");
            }
            finally
            {
                GridlockQuestSetup.Configure(); // back to the headset setup
            }
        }

        static void Configure()
        {
            GridlockQuestSetup.ConfigurePlayer();
            GridlockQuestSetup.ConfigureRendering();
            GridlockQuestSetup.SetAndroidARLoader();
            // ARCore "optional": phones without it still run, in the touch view (XRModeController falls back).
            var arcore = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            UnityEditor.XR.ARCore.ARCoreSettings.currentSettings = arcore;
            EditorUtility.SetDirty(arcore);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            // ARCore's camera feed is most widely supported on GLES3; Vulkan stays as a fallback.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Android 10+ (ARCore needs 24); the Quest build raises it to 32
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.renderOutsideSafeArea = false; // keep the HUD clear of camera cut-outs
            PlayerSettings.Android.startInFullscreen = true; // no status bar over the camera view
            AssetDatabase.SaveAssets();
            Debug.Log("[GRIDLOCK] Configured for Android phones (ARCore AR in every scene).");
        }
    }
}
