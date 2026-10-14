using System;
using UnityEngine;

namespace Gridlock.XR
{
    /// <summary>
    /// Face-button relay for the imported scenarios, whose scripts live outside GRIDLOCK's assembly: the XR shortcuts
    /// call <see cref="PressA"/>/<see cref="PressB"/>/<see cref="PressX"/> and the scenario's own adapter listens. The labels
    /// feed the controller hint cards.
    /// </summary>
    public class ScenarioButtons : MonoBehaviour
    {
        [SerializeField] string labelA, labelB, labelX;

        public event Action A, B, X;

        public void Init(string a, string b, string x)
        {
            labelA = a;
            labelB = b;
            labelX = x;
        }

        public string Label(string method) => method switch
        {
            nameof(PressA) => labelA,
            nameof(PressB) => labelB,
            nameof(PressX) => labelX,
            _ => null,
        };

        public void PressA() => A?.Invoke();
        public void PressB() => B?.Invoke();
        public void PressX() => X?.Invoke();
    }
}
