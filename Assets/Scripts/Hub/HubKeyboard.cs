using System.Collections.Generic;
using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>
    /// The desk keyboard: keys light up and click as if someone were typing. It types along with the screen and a
    /// little on its own while idle. Lit keys fade out; only the keys that are lit are touched each frame.
    /// </summary>
    public class HubKeyboard : MonoBehaviour
    {
        [SerializeField] Renderer[] keys;
        [SerializeField] Color idle = new Color(0.06f, 0.08f, 0.12f);
        [SerializeField] Color lit = new Color(0.4f, 1.6f, 2.2f);
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip click;
        [SerializeField] Vector2 idleGap = new Vector2(2.5f, 7f);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly Dictionary<int, float> glowing = new Dictionary<int, float>();
        readonly List<int> done = new List<int>();
        MaterialPropertyBlock block;
        int pending;
        float nextKey, nextIdle;

        public void Init(Renderer[] keyRenderers, AudioSource audioSource, AudioClip keyClick)
        {
            keys = keyRenderers;
            source = audioSource;
            click = keyClick;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            nextIdle = Time.time + Random.Range(idleGap.x, idleGap.y);
        }

        /// <summary>Press one random key now.</summary>
        public void Press()
        {
            int i = Random.Range(0, keys.Length);
            glowing[i] = 1f;
            source.pitch = Random.Range(0.9f, 1.15f);
            source.PlayOneShot(click, Random.Range(0.5f, 0.9f));
        }

        /// <summary>The player pressed a working key: hold off the idle "someone is typing" effect for a while.</summary>
        public void UserTyped()
        {
            pending = 0;
            nextIdle = Time.time + 10f;
        }

        /// <summary>Type a short run of keys.</summary>
        public void Burst(int count) => pending += count;

        void Update()
        {
            if (Time.time >= nextIdle)
            {
                Burst(Random.Range(3, 9));
                nextIdle = Time.time + Random.Range(idleGap.x, idleGap.y);
            }
            if (pending > 0 && Time.time >= nextKey)
            {
                pending--;
                Press();
                nextKey = Time.time + Random.Range(0.05f, 0.14f);
            }

            done.Clear();
            foreach (var kv in glowing)
            {
                float v = kv.Value - Time.deltaTime * 3f;
                block.SetColor(BaseColor, Color.Lerp(idle, lit, Mathf.Max(0f, v)));
                keys[kv.Key].SetPropertyBlock(block);
                done.Add(kv.Key);
            }
            foreach (var i in done)
            {
                float v = glowing[i] - Time.deltaTime * 3f;
                if (v <= 0f) glowing.Remove(i);
                else glowing[i] = v;
            }
        }
    }
}
