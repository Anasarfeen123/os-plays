using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gridlock.Core;
using Gridlock.XR;
using UnityEngine;

namespace Gridlock.Guide
{
    /// <summary>
    /// Shared machinery of the narrated guided tours (Deadlock and Banker): a voice line per step with live subtitles on
    /// the floating guide screen, a bouncing marker, desktop camera moves, contextual action buttons, NEXT / SKIP /
    /// REPLAY, music ducking, and "runs once per session unless turned off in Settings". Subclasses write
    /// <see cref="Script"/> and wire their scene in the hooks.
    /// </summary>
    public abstract class NarratedTour : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            public string id;
            public string title;
            [TextArea] public string text;
            public AudioClip clip;
        }

        static readonly HashSet<string> seen = new HashSet<string>();

        public static bool IsSeen(string key) => seen.Contains(key);

        public static void SetSeen(string key, bool value)
        {
            if (value) seen.Add(key);
            else seen.Remove(key);
        }

        /// <summary>Menu → Settings: show every tour again on the next visit.</summary>
        public static void ForgetAll() => seen.Clear();

        [SerializeField] protected Step[] steps;
        [SerializeField] protected GuideMarker marker;
        [SerializeField] GuideScreen desktopScreen;
        [SerializeField] GuideScreen xrScreen;
        [SerializeField] XRModeController xrMode;
        [SerializeField] protected DesktopCameraController desktopCamera;

        AudioSource voice;
        Coroutine routine;
        bool willRun;
        float lineStart;
        float lineLength;
        float tourStart;
        int spokenSteps;

        public bool Running { get; private set; }
        public string CurrentStep { get; private set; }
        public event Action Finished;

        /// <summary>Session key: a tour runs by itself only on the first visit.</summary>
        protected abstract string Key { get; }

        /// <summary>The tour itself, step by step.</summary>
        protected abstract IEnumerator Script();

        /// <summary>Steps that do not get their own number (asides, alternative branches).</summary>
        protected virtual bool Counted(string id) => true;

        /// <summary>First visit only, from Awake: stop the scene's own intro before its Start runs.</summary>
        protected virtual void PrepareFirstRun() { }

        protected virtual void OnBegin() { }
        protected virtual void OnEnd(bool finished) { }

        /// <summary>SKIP pressed. <paramref name="early"/> is true before the hands-on part.</summary>
        protected virtual void OnSkip(bool early) { }

        /// <summary>Steps before this one count as "early" for <see cref="OnSkip"/>.</summary>
        protected virtual string HandsOnStep => null;

        int StepCount => steps.Count(s => Counted(s.id));

        public void InitTour(Step[] tourSteps, GuideMarker guideMarker, GuideScreen desktop, GuideScreen xr, XRModeController mode,
                             DesktopCameraController camera)
        {
            steps = tourSteps;
            marker = guideMarker;
            desktopScreen = desktop;
            xrScreen = xr;
            xrMode = mode;
            desktopCamera = camera;
        }

        protected virtual void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
            voice.priority = 0;

            willRun = !IsSeen(Key) && GameSettings.AutoTours;
            if (willRun) PrepareFirstRun();
            marker.Hide();
        }

        protected virtual void Start()
        {
            // The floating screen is for the headset; the docked one lives in the desktop HUD.
            xrScreen.gameObject.SetActive(xrMode != null && xrMode.XRActive);
            if (willRun) Begin();
        }

        void Update()
        {
            if (!Running) return;
            float spoken = lineLength > 0f ? (Time.time - lineStart) / lineLength : 1f;
            ForScreens(s =>
            {
                s.SetSpoken(spoken);
                s.SetProgress((spokenSteps - 1 + Mathf.Clamp01(spoken)) / StepCount);
            });
        }

        // ---------------------------------------------------------------------------------------------------------
        // Controls (guide screen buttons and the system menu)
        // ---------------------------------------------------------------------------------------------------------

        public void Replay()
        {
            if (Running) Stop(finished: false);
            Begin();
        }

        /// <summary>Cut the current line short and move on (the step's actions still complete).</summary>
        public void Next()
        {
            if (!Running) return;
            voice.Stop();
            lineStart = Time.time - lineLength - 1f;
        }

        public void Skip()
        {
            if (!Running) return;
            bool early = HandsOnStep != null &&
                         Array.FindIndex(steps, s => s.id == CurrentStep) < Array.FindIndex(steps, s => s.id == HandsOnStep);
            Stop(finished: false);
            ForScreens(s => s.Collapse());
            OnSkip(early);
        }

        void Begin()
        {
            SetSeen(Key, true);
            Running = true;
            spokenSteps = 0;
            tourStart = Time.time;
            MusicPlayer.Duck(true);
            OnBegin();
            routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitUntil(() => ScenePlacement.Ready); // phone AR: start talking once the lot is placed
            yield return Script();
            Stop(finished: true);
        }

        protected void Stop(bool finished)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Running = false;
            CurrentStep = null;
            voice.Stop();
            marker.Hide();
            HideActions();
            MusicPlayer.Duck(false);
            OnEnd(finished);
            if (desktopCamera != null && desktopCamera.isActiveAndEnabled) desktopCamera.FocusHome(1.5f);
            if (finished)
            {
                Debug.Log($"[GRIDLOCK] {GetType().Name} finished in {Time.time - tourStart:F0} s");
                ForScreens(s => s.ShowEnd());
                Finished?.Invoke();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Script helpers
        // ---------------------------------------------------------------------------------------------------------

        protected float LineLength => lineLength;
        protected float LineElapsed => Time.time - lineStart;

        protected void Say(string id)
        {
            var step = steps.First(s => s.id == id);
            CurrentStep = id;
            if (Counted(id)) spokenSteps++;

            lineStart = Time.time;
            lineLength = step.clip != null ? step.clip.length : step.text.Split(' ').Length / 2.6f; // ~155 words a minute
            voice.Stop();
            if (AudioRouting.VoiceGroup != null) voice.outputAudioMixerGroup = AudioRouting.VoiceGroup;
            if (step.clip != null)
            {
                voice.clip = step.clip;
                voice.Play();
            }
            int number = Mathf.Max(1, spokenSteps);
            ForScreens(s => s.ShowStep(step, number, StepCount));
        }

        protected IEnumerator At(float fraction)
        {
            while (Time.time - lineStart < lineLength * fraction) yield return null;
        }

        protected IEnumerator EndOfLine(float pause = 0.35f)
        {
            while (Time.time - lineStart < lineLength + pause) yield return null;
        }

        protected void Focus(Vector3 target, float distance, float pitch)
        {
            if (desktopCamera != null && desktopCamera.isActiveAndEnabled) desktopCamera.FocusOn(target, distance, pitch, 1.6f);
        }

        /// <summary>Show up to two buttons on the guide screen for the hands-on step.</summary>
        protected void ShowActions(params GuideScreen.Action[] actions) => ForScreens(s => s.ShowActions(actions));

        protected void UpdateAction(int index, string label, bool interactable) => ForScreens(s => s.UpdateAction(index, label, interactable));

        protected void HideActions() => ForScreens(s => s.HideActions());

        void ForScreens(Action<GuideScreen> action)
        {
            if (desktopScreen != null) action(desktopScreen);
            if (xrScreen != null) action(xrScreen);
        }
    }
}
