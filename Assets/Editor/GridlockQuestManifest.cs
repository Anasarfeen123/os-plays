#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Adds Meta's hand-tracking declarations to the generated Android manifest (com.unity.xr.hands does not):
    /// without them Quest never hands tracked hands to the app and only controllers work.
    /// Hands are optional, so the app still installs and runs with controllers alone. (Android Build Support only.)
    /// Skipped for the phone build, which has no XR loader.
    /// </summary>
    public class GridlockQuestManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!GridlockQuestSetup.HasAndroidXRLoader()) return;

            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[GRIDLOCK] No manifest at {manifestPath}; hand tracking not declared.");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            var application = manifest["application"];

            Ensure(doc, manifest, "uses-permission", "com.oculus.permission.HAND_TRACKING");
            SetAndroid(Ensure(doc, manifest, "uses-feature", "oculus.software.handtracking"), "required", "false");
            if (application != null)
                SetAndroid(Ensure(doc, application, "meta-data", "com.oculus.handtracking.version"), "value", "V2.0");

            doc.Save(manifestPath);
        }

        static XmlElement Ensure(XmlDocument doc, XmlElement parent, string tag, string name)
        {
            foreach (XmlNode node in parent.ChildNodes)
                if (node is XmlElement e && e.Name == tag && e.GetAttribute("name", AndroidNs) == name)
                    return e;
            var element = doc.CreateElement(tag);
            SetAndroid(element, "name", name);
            parent.AppendChild(element);
            return element;
        }

        static void SetAndroid(XmlElement element, string attribute, string value)
        {
            var attr = element.OwnerDocument.CreateAttribute("android", attribute, AndroidNs);
            attr.Value = value;
            element.SetAttributeNode(attr);
        }
    }
}
#endif
