using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.AR
{
    /// <summary>
    /// The PHOTO button in phone AR: the camera view with the model, without the HUD (the AR camera rendered into an
    /// image), saved to the phone's gallery (Pictures/OS Plays, through MediaStore: no storage permission needed). A
    /// white flash and a haptic tick confirm it.
    /// </summary>
    public class ARPhoto : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image flash;

        /// <summary>Where the last photo went (tests read it; a content URI on a phone, a file elsewhere).</summary>
        public string LastSaved { get; private set; }

        public void Init(Button photoButton, Image flashImage)
        {
            button = photoButton;
            flash = flashImage;
        }

        void Awake()
        {
            button.onClick.AddListener(() => StartCoroutine(Take()));
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
        }

        public IEnumerator Take()
        {
            // The AR camera alone, rendered into an image: the camera feed and the model, without any HUD.
            var view = Camera.main;
            int w = Screen.width, h = Screen.height;
            var target = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var previous = view.targetTexture;
            view.targetTexture = target;
            view.Render();
            view.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);

            byte[] png = shot.EncodeToPNG();
            Destroy(shot);
            string name = $"OSPlays_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            LastSaved = Save(png, name);
            ARHaptics.Success();
            Debug.Log($"[GRIDLOCK] Photo saved: {LastSaved}");

            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, 0.8f * (1f - t / 0.35f));
                yield return null;
            }
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        static string Save(byte[] png, string name)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", name);
                values.Call("put", "mime_type", "image/png");
                values.Call("put", "relative_path", "Pictures/OS Plays");
                using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                using var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                using var uri = resolver.Call<AndroidJavaObject>("insert", collection, values);
                using var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
                stream.Call("write", png);
                stream.Call("close");
                return uri.Call<string>("toString");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GRIDLOCK] Gallery save failed ({e.Message}); keeping the photo in the app's folder");
            }
#endif
            string path = Path.Combine(Application.persistentDataPath, name);
            File.WriteAllBytes(path, png);
            return path;
        }
    }
}
