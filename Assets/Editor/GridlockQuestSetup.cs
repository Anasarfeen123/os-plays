using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Gridlock.Editor
{
    /// <summary>
    /// One-click Meta Quest 2 setup and build (Quest 3/3S/Pro run it too).
    ///  • Android player: IL2CPP, ARM64, API 29 minimum, Vulkan, Linear, ASTC, GameActivity, landscape-left.
    ///  • XR: OpenXR loader on Android with Meta Quest Support (Quest 2 and newer), symmetric projection, Oculus Touch controllers,
    ///    hand tracking + Meta hand-tracking aim (pinch rays), foveated rendering, multiview.
    ///  • Mobile URP asset (Android quality level) tuned for the Quest 2 (XR2): 2× MSAA, no HDR, one hard-shadow cascade.
    /// Menus: GRIDLOCK ▸ Configure for Meta Quest 2 / Build Quest APK.
    /// Batchmode: -executeMethod Gridlock.Editor.GridlockQuestSetup.ConfigureAndBuild (-buildTarget Android)
    /// </summary>
    public static class GridlockQuestSetup
    {
        const string PackageId = "com.anasarfeen.gridlock";
        const string ApkPath = "Builds/OSPlays-Quest.apk";
        const string MobileRPAsset = "Assets/Settings/Mobile_RPAsset.asset";

        // OpenXR features to enable on Android, by type name (the hand features live in com.unity.xr.hands).
        static readonly string[] AndroidFeatures =
        {
            "MetaQuestFeature",
            "OculusTouchControllerProfile",
            "HandTracking",
            "MetaHandTrackingAim",
            "FoveatedRenderingFeature",
            "XrPerformanceSettingsFeature", // GPU clock hints (QuestFrameBudget)
            "ARSessionFeature",             // Meta OpenXR: the session behind passthrough (Settings → MIXED REALITY)
            "ARCameraFeature",              // Meta OpenXR: passthrough
            "OpenXRCompositionLayersFeature", // required once Meta OpenXR brings in the Composition Layers package
        };

        [MenuItem("GRIDLOCK/Configure for Meta Quest 2")]
        public static void Configure()
        {
            ConfigurePlayer();
            ConfigureQuestPlayer();
            ConfigureXRManagement();
            ConfigureOpenXR();
            ConfigureRendering();
            AssetDatabase.SaveAssets();
            Debug.Log("[GRIDLOCK] Configured for Meta Quest 2 (Android / OpenXR).");
        }

        [MenuItem("GRIDLOCK/Build Quest APK")]
        public static void BuildFromMenu()
        {
            Configure();
            BuildApk(ApkPath, "Quest");
            EditorUtility.RevealInFinder(ApkPath);
        }

        /// <summary>Batchmode entry: configure, rebuild the scenes, build the APK, and exit non-zero on failure.</summary>
        public static void ConfigureAndBuild()
        {
            Configure();
            GridlockSceneBuilder.Build();
            BuildApk(ApkPath, "Quest");
        }

        /// <summary>Builds the scenes in the build list to an APK with the current Android settings; throws on failure.</summary>
        internal static void BuildApk(string apkPath, string label)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not switch to Android. Install Android Build Support for this editor in Unity Hub.");

            Directory.CreateDirectory(Path.GetDirectoryName(apkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[GRIDLOCK] {label} build {summary.result}: {apkPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"{label} build {summary.result}");
        }

        /// <summary>
        /// Tools/release.sh passes the release version (e.g. 0.2.0) in OSPLAYS_VERSION: it becomes the app's version name,
        /// and the version code (0.2.0 → 200) grows with every release, so phones and headsets accept the update.
        /// </summary>
        static void ApplyReleaseVersion()
        {
            var version = Environment.GetEnvironmentVariable("OSPLAYS_VERSION");
            if (string.IsNullOrEmpty(version)) return;
            var parts = version.Split('.').Select(int.Parse).Concat(new[] { 0, 0, 0 }).Take(3).ToArray();
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = parts[0] * 10000 + parts[1] * 100 + parts[2];
            Debug.Log($"[GRIDLOCK] Version {version} (code {PlayerSettings.Android.bundleVersionCode})");
        }

        /// <summary>Android player settings shared by the Quest and phone builds.</summary>
        internal static void ConfigurePlayer()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Anas Arfeen";
            PlayerSettings.productName = "OS Plays";
            ApplyReleaseVersion();
            ConfigureBranding();
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32; // Meta's camera (passthrough) feature needs 12L; Quest 2 runs it. The phone build sets its own.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.gpuSkinning = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
        }

        const string IconPath = "Assets/UI/Brand/OSPlaysIcon.png", LogoPath = "Assets/UI/Brand/OSPlaysLogo.png";

        /// <summary>OS Plays app icon (launcher, Quest library) and the logo on the splash screen.</summary>
        static void ConfigureBranding()
        {
            var iconImporter = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (iconImporter != null && (iconImporter.maxTextureSize != 1024 || iconImporter.mipmapEnabled))
            {
                iconImporter.textureType = TextureImporterType.Default;
                iconImporter.maxTextureSize = 1024;
                iconImporter.mipmapEnabled = false;
                iconImporter.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android)) // legacy, round, adaptive
                {
                    var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                    foreach (var slot in icons)
                        for (int layer = 0; layer < slot.maxLayerCount; layer++) slot.SetTexture(icon, layer);
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
                }
            }
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.backgroundColor = Color.black;
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            }
        }

        /// <summary>The Quest-only side of the player settings (the phone build overrides these).</summary>
        static void ConfigureQuestPlayer()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.Android.optimizedFramePacing = false; // the OpenXR compositor paces frames
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        }

        static void ConfigureXRManagement() => SetAndroidXRLoader(true);

        /// <summary>Assigns (Quest) or removes (phone) the OpenXR loader in XR Plug-in Management's Android tab.</summary>
        internal static void SetAndroidXRLoader(bool enabled)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
                throw new InvalidOperationException("XR Plug-in Management settings are missing (Assets/XR).");

            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            settings.InitManagerOnStart = enabled;
            if (enabled) XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
            else XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
            // The headset never runs ARCore; the phone build assigns it itself (GridlockPhoneSetup).
            XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, ARCoreLoaderType, BuildTargetGroup.Android);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.AssignedSettings);
            EditorUtility.SetDirty(perTarget);
        }

        internal const string ARCoreLoaderType = "UnityEngine.XR.ARCore.ARCoreLoader";

        /// <summary>Phone: ARCore is the only Android loader, started with the app (OpenXR stays off).</summary>
        internal static void SetAndroidARLoader()
        {
            SetAndroidXRLoader(false);
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            settings.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, ARCoreLoaderType, BuildTargetGroup.Android);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.AssignedSettings);
        }

        /// <summary>True when the Android build will start an XR loader (the Quest configuration).</summary>
        internal static bool HasAndroidXRLoader()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
                return false;
            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            return settings != null && settings.AssignedSettings != null && settings.AssignedSettings.activeLoaders.Count > 0;
        }

        static void ConfigureOpenXR()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openxr == null) throw new InvalidOperationException("OpenXR settings for Android are missing.");

            foreach (var feature in openxr.GetFeatures())
            {
                if (Array.IndexOf(AndroidFeatures, feature.GetType().Name) < 0) continue;
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            var missing = AndroidFeatures.Where(n => openxr.GetFeatures().All(f => f.GetType().Name != n)).ToArray();
            if (missing.Length > 0) Debug.LogWarning($"[GRIDLOCK] OpenXR features not found: {string.Join(", ", missing)}");

            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced; // multiview: one pass for both eyes
            // The eye buffers get this depth format too: 16 bit left ~1.4 cm of precision at 3 m (15 cm at 10 m), so wall
            // screens a few centimetres off a wall flickered through it.
            openxr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth24Bit;
            openxr.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            openxr.optimizeBufferDiscards = true; // memoryless MSAA on Vulkan
            openxr.symmetricProjection = true;    // Quest 2 and later: cheaper multiview rendering
            // Fixed foveation through URP: only the SRP path honours XRDisplaySubsystem.foveatedRenderingLevel
            // (the Legacy default left it off: VrApi reported Fov=0). The Meta feature overrides these at runtime.
            openxr.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;
            EditorUtility.SetDirty(openxr);
            foreach (var feature in openxr.GetFeatures().Where(f => f.GetType().Name == "MetaQuestFeature"))
            {
                var so = new SerializedObject(feature);
                so.FindProperty("m_foveatedRenderingApi").enumValueIndex = (int)OpenXRSettings.BackendFovationApi.SRPFoveation;
                so.FindProperty("m_optimizeBufferDiscards").boolValue = true;
                so.FindProperty("m_symmetricProjection").boolValue = true;
                // Quest 2 and newer only: the original Quest is off the list (Meta no longer accepts builds for it).
                var devices = so.FindProperty("targetDevices");
                for (int i = 0; devices != null && i < devices.arraySize; i++)
                {
                    var device = devices.GetArrayElementAtIndex(i);
                    device.FindPropertyRelative("enabled").boolValue = device.FindPropertyRelative("manifestName").stringValue != "quest";
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
        }

        internal static void ConfigureRendering()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRPAsset);
            if (asset == null) throw new FileNotFoundException(MobileRPAsset);
            asset.msaaSampleCount = 2; // measured on Quest 2 in the hub: 4× held 47 fps (GPU-bound), 2× a steady 72
            asset.supportsHDR = false;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.shadowDistance = 35f;
            asset.shadowCascadeCount = 1;

            var so = new SerializedObject(asset);
            SetIfFound(so, "m_SoftShadowsSupported", p => p.boolValue = false);
            SetIfFound(so, "m_MainLightShadowmapResolution", p => p.intValue = 1024);
            // Per pixel: the CPU lab and the memory warehouse are lit by lamps (the hub and the lot have only their sun, so
            // they pay nothing). No shadows from lamps, at most 4 per object.
            SetIfFound(so, "m_AdditionalLightsRenderingMode", p => p.intValue = 1);
            SetIfFound(so, "m_AdditionalLightsPerObjectLimit", p => p.intValue = 4);
            SetIfFound(so, "m_AdditionalLightShadowsSupported", p => p.boolValue = false);
            SetIfFound(so, "m_UseSRPBatcher", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void SetIfFound(SerializedObject so, string name, Action<SerializedProperty> set)
        {
            var p = so.FindProperty(name);
            if (p != null) set(p);
            else Debug.LogWarning($"[GRIDLOCK] URP asset has no {name}");
        }
    }
}
