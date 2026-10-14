using Gridlock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>A settings slider bound to one <see cref="Setting"/>, with its value shown on the right.</summary>
    public class SettingSlider : MonoBehaviour
    {
        public enum Format { Percent, Multiplier }

        [SerializeField] Setting setting;
        [SerializeField] Slider slider;
        [SerializeField] TMP_Text value;
        [SerializeField] Format format;

        public void Init(Setting bound, Slider control, TMP_Text valueText, Format display)
        {
            setting = bound;
            slider = control;
            value = valueText;
            format = display;
        }

        void Awake() => slider.onValueChanged.AddListener(v =>
        {
            GameSettings.Set(setting, v);
            Show(v);
        });

        void OnEnable() => Refresh();

        public void Refresh()
        {
            float v = GameSettings.Get(setting);
            slider.SetValueWithoutNotify(v);
            Show(v);
        }

        void Show(float v) => value.text = format == Format.Percent
            ? $"{Mathf.RoundToInt(v * 100f)}%"
            : $"{GameSettings.LookMultiplier:0.0}×";
    }
}
