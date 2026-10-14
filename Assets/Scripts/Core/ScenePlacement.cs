using System;

namespace Gridlock.Core
{
    /// <summary>
    /// Whether the scene's world is in front of the player yet. Always true, except in phone AR before the lot has been
    /// placed on a real surface: the scripted demo and the guided tours wait for it.
    /// </summary>
    public static class ScenePlacement
    {
        static bool ready = true;

        public static event Action Placed;

        public static bool Ready
        {
            get => ready;
            set
            {
                bool was = ready;
                ready = value;
                if (value && !was) Placed?.Invoke();
            }
        }
    }
}
