using Gridlock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>Segmented buttons for one <see cref="Setting"/>: each button stores a value; the chosen one lights up.</summary>
    public class SettingChoice : MonoBehaviour
    {
        static readonly Color On = new Color(0.35f, 0.85f, 1f), Off = new Color(0.12f, 0.16f, 0.26f);

        [SerializeField] Setting setting;
        [SerializeField] Button[] options;
        [SerializeField] float[] values;

        public void Init(Setting bound, Button[] buttons, float[] optionValues)
        {
            setting = bound;
            options = buttons;
            values = optionValues;
        }

        void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                float v = values[i];
                options[i].onClick.AddListener(() =>
                {
                    GameSettings.Set(setting, v);
                    Refresh();
                });
            }
        }

        void OnEnable() => Refresh();

        public void Refresh()
        {
            float current = GameSettings.Get(setting);
            for (int i = 0; i < options.Length; i++)
            {
                bool on = Mathf.Approximately(values[i], current);
                options[i].image.color = on ? On : Off;
                var label = options[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? new Color(0.04f, 0.07f, 0.12f) : new Color(0.89f, 0.91f, 0.94f);
            }
        }
    }
}
