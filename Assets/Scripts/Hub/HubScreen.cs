using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Hub
{
    /// <summary>
    /// The hub monitor's show. It boots with a CRT power-on, types a title and a boot log while the keyboard types along,
    /// then slides the scenario tiles in. While idle it runs a clock, telemetry lines and a blinking prompt. The desk
    /// keyboard drives it (<see cref="KeyPressed"/>): ◄ ► or 1 2 3 move the focus, ENTER activates. Launching plays a
    /// confirm, glitches the screen and hands over to <see cref="HubLauncher"/>.
    /// </summary>
    public class HubScreen : MonoBehaviour
    {
        [SerializeField] RectTransform content;
        [SerializeField] Image flash;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text console;
        [SerializeField] TMP_Text clock;
        [SerializeField] TMP_Text prompt;
        [SerializeField] HubTile[] tiles;
        [SerializeField] HubLauncher launcher;
        [SerializeField] HubKeyboard keyboard;
        [SerializeField] AudioSource ui;
        [SerializeField] AudioClip bootClip, tickClip, hoverClip, selectClip, deniedClip, glitchClip;

        const string TitleText = "OS PLAYS  //  SIMULATION SELECT";
        static readonly string[] BootLog =
        {
            "> neural link ................ <color=#22C55E>OK</color>",
            "> haptic channels ............ <color=#22C55E>OK</color>",
            "> scenario index ............. <color=#FBBF24>3 FOUND</color>",
            "> deadlock kernel ............ <color=#22C55E>READY</color>",
        };
        static readonly string[] Telemetry =
        {
            "> telemetry sync ............. OK",
            "> wait-for graph cache ....... warm",
            "> banker safety core ......... nominal",
            "> scenario 02 build .......... <color=#94A3B8>pending</color>",
            "> scenario 03 build .......... <color=#94A3B8>pending</color>",
            "> reactor output ............. 98.2%",
        };

        readonly List<string> lines = new List<string>();
        Vector2 contentHome;
        bool booted;
        bool chosen;
        int focus;

        public void Init(RectTransform screenContent, Image flashOverlay, TMP_Text titleText, TMP_Text consoleText, TMP_Text clockText,
                         TMP_Text promptText, HubTile[] scenarioTiles, HubLauncher hubLauncher, HubKeyboard hubKeyboard, AudioSource uiSource,
                         AudioClip boot, AudioClip tick, AudioClip hover, AudioClip select, AudioClip denied, AudioClip glitch)
        {
            content = screenContent;
            flash = flashOverlay;
            title = titleText;
            console = consoleText;
            clock = clockText;
            prompt = promptText;
            tiles = scenarioTiles;
            launcher = hubLauncher;
            keyboard = hubKeyboard;
            ui = uiSource;
            bootClip = boot;
            tickClip = tick;
            hoverClip = hover;
            selectClip = select;
            deniedClip = denied;
            glitchClip = glitch;
        }

        void Awake()
        {
            contentHome = content.anchoredPosition;
            content.localScale = new Vector3(0.02f, 0.004f, 1f);
            title.text = console.text = prompt.text = "";
            SetFlash(0f);
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.6f); // let the scene fade in first
            ui.PlayOneShot(bootClip);

            // CRT power-on: a bright dot stretches into a line, then opens into the picture.
            SetFlash(1f);
            yield return Animate(0.18f, k => content.localScale = new Vector3(Mathf.Lerp(0.02f, 1f, Ease(k)), 0.004f, 1f));
            yield return Animate(0.32f, k =>
            {
                content.localScale = new Vector3(1f, Mathf.Lerp(0.004f, 1f, Ease(k)), 1f);
                SetFlash(1f - k);
            });
            content.localScale = Vector3.one;

            yield return Type(title, TitleText, 0.035f);
            foreach (var line in BootLog)
            {
                yield return new WaitForSeconds(0.12f);
                AddLine(line);
                keyboard?.Burst(4);
            }

            for (int i = 0; i < tiles.Length; i++) StartCoroutine(tiles[i].Reveal(0.15f + i * 0.18f));
            yield return new WaitForSeconds(0.25f + tiles.Length * 0.18f + 0.45f);
            booted = true;
            Focus(0, sound: false);
            StartCoroutine(Telemetries());
        }

        void Update()
        {
            if (clock != null) clock.text = DateTime.Now.ToString("HH:mm:ss");
            if (booted && !chosen && prompt != null)
            {
                prompt.text = "Use the desk keyboard:   ◄ ►  or  1 2 3  to choose,   ENTER  to start";
                prompt.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
            }
        }

        IEnumerator Telemetries()
        {
            var rng = new System.Random();
            while (!chosen)
            {
                yield return new WaitForSeconds(4f + (float)rng.NextDouble() * 5f);
                if (chosen) yield break;
                AddLine(Telemetry[rng.Next(Telemetry.Length)]);
                keyboard?.Burst(3);
            }
        }

        public void PlayTick() => ui.PlayOneShot(tickClip, 0.6f);

        public bool Booted => booted;
        public int FocusedIndex => focus;

        /// <summary>A desk key was pressed.</summary>
        public void KeyPressed(HubKey.KeyAction action, int slot)
        {
            if (!booted || chosen) return;
            switch (action)
            {
                case HubKey.KeyAction.Left: Focus((focus + tiles.Length - 1) % tiles.Length); break;
                case HubKey.KeyAction.Right: Focus((focus + 1) % tiles.Length); break;
                case HubKey.KeyAction.Slot: if (slot >= 0 && slot < tiles.Length) Focus(slot); break;
                case HubKey.KeyAction.Enter: tiles[focus].Activate(); break;
            }
        }

        void Focus(int index, bool sound = true)
        {
            tiles[focus].SetFocused(false);
            focus = index;
            tiles[focus].SetFocused(true);
            if (sound) ui.PlayOneShot(hoverClip, 0.8f);
        }

        public void TileDenied(HubTile tile)
        {
            ui.PlayOneShot(deniedClip);
            AddLine("> <color=#EF4444>ACCESS DENIED</color>: scenario still in development");
        }

        public void TileChosen(HubTile tile)
        {
            if (chosen) return;
            chosen = true;
            StartCoroutine(Choose(tile));
        }

        IEnumerator Choose(HubTile tile)
        {
            ui.PlayOneShot(selectClip);
            AddLine($"> loading <color=#FBBF24>{tile.name.Replace("Tile_", "").ToUpperInvariant()}</color> ...");
            prompt.text = "ENTERING SIMULATION";
            prompt.alpha = 1f;
            yield return new WaitForSeconds(0.25f);

            // Digital glitch: jitter, horizontal tearing and flicker, then the pull begins.
            ui.PlayOneShot(glitchClip);
            var rng = new System.Random();
            yield return Animate(0.45f, k =>
            {
                float j = (1f - k) * 18f;
                content.anchoredPosition = contentHome + new Vector2((float)(rng.NextDouble() * 2 - 1) * j, (float)(rng.NextDouble() * 2 - 1) * j * 0.4f);
                content.localScale = new Vector3(1f + (rng.Next(6) == 0 ? 0.03f : 0f), 1f, 1f);
                SetFlash(rng.Next(4) == 0 ? 0.35f : 0f);
            });
            content.anchoredPosition = contentHome;
            content.localScale = Vector3.one;
            SetFlash(0f);
            launcher.Launch(tile.SceneName);

            // The picture blooms to white as the player reaches the glass.
            yield return new WaitForSeconds(0.5f);
            yield return Animate(1.1f, k => SetFlash(k * k));
        }

        void AddLine(string line)
        {
            lines.Add(line);
            while (lines.Count > 5) lines.RemoveAt(0);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                float a = 0.45f + 0.55f * (i + 1) / lines.Count; // older lines fade
                sb.Append($"<alpha=#{(int)(a * 255):X2}>").AppendLine(lines[i]);
            }
            console.text = sb.ToString();
            ui.PlayOneShot(tickClip, 0.35f);
        }

        IEnumerator Type(TMP_Text text, string full, float perChar)
        {
            for (int i = 1; i <= full.Length; i++)
            {
                text.text = full.Substring(0, i) + (i < full.Length ? "_" : "");
                if (full[i - 1] != ' ') keyboard?.Press();
                yield return new WaitForSeconds(perChar);
            }
        }

        void SetFlash(float a)
        {
            if (flash == null) return;
            var c = flash.color;
            c.a = a;
            flash.color = c;
            flash.enabled = a > 0.001f;
        }

        static IEnumerator Animate(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                step(t / seconds);
                yield return null;
            }
            step(1f);
        }

        static float Ease(float x) => 1f - Mathf.Pow(1f - x, 3f);
    }
}
