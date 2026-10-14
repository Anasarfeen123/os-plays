using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>Palette and colour helpers shared by every GRIDLOCK view. Uses property blocks so shared materials stay untouched.</summary>
    public static class ViewColor
    {
        public static readonly Color Deadlock = new Color(0.94f, 0.27f, 0.27f);   // #EF4444
        public static readonly Color Waiting = new Color(0.98f, 0.75f, 0.14f);    // #FBBF24
        public static readonly Color Safe = new Color(0.13f, 0.77f, 0.37f);       // #22C55E
        public static readonly Color Text = new Color(0.89f, 0.91f, 0.94f);       // #E2E8F0
        public static readonly Color Panel = new Color(0.06f, 0.09f, 0.16f, 0.94f); // #0F172A

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>Identity colour of car / fleet A–D (matches the Kenney model paint: van, SUV, taxi, police).</summary>
        public static readonly Color[] Fleet =
        {
            new Color(0.23f, 0.51f, 0.96f), // A blue
            new Color(0.13f, 0.77f, 0.37f), // B green
            new Color(0.98f, 0.80f, 0.08f), // C yellow
            new Color(0.80f, 0.84f, 0.90f), // D white
        };

        /// <summary>0..1 pulse used by every deadlock highlight so they blink in sync.</summary>
        public static float Pulse => 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>The UI font draws "→" very small; enlarge it in rich text.</summary>
        public static string BigArrows(string s) => s.Replace("→", "<size=150%><voffset=-0.05em>→</voffset></size>");

        public static void Apply(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Tint plus emission, for lit materials that have _EMISSION enabled (the Kenney car material).</summary>
        public static void Apply(Renderer renderer, MaterialPropertyBlock block, Color tint, Color emission)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, tint);
            block.SetColor(LegacyColor, tint);
            block.SetColor(EmissionColor, emission);
            renderer.SetPropertyBlock(block);
        }
    }
}
