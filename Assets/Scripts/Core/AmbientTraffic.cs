using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Background city traffic on the east street: decorative cars that drive their lane end to end and wrap around,
    /// slowing near the lot exit as if giving way. Not part of the simulation; purely life on the streets.
    /// </summary>
    public class AmbientTraffic : MonoBehaviour
    {
        [SerializeField] Transform[] cars;
        [Tooltip("Lane x position per car.")]
        [SerializeField] float[] laneX;
        [Tooltip("+1 drives north (+Z), -1 south.")]
        [SerializeField] float[] direction;
        [SerializeField] float minZ = -60f;
        [SerializeField] float maxZ = 60f;
        [SerializeField] float speed = 7f;
        [SerializeField] float slowZ = 0.75f;
        [SerializeField] float slowRadius = 12f;

        float[] speeds;
        Transform[][] wheels;

        public void Init(Transform[] carTransforms, float[] lanes, float[] directions, float min, float max)
        {
            cars = carTransforms;
            laneX = lanes;
            direction = directions;
            minZ = min;
            maxZ = max;
        }

        void Start()
        {
            speeds = new float[cars.Length];
            wheels = new Transform[cars.Length][];
            var rng = new System.Random(3);
            for (int i = 0; i < cars.Length; i++)
            {
                speeds[i] = speed * (0.75f + 0.5f * (float)rng.NextDouble());
                wheels[i] = System.Array.FindAll(cars[i].GetComponentsInChildren<Transform>(), t => t.name.StartsWith("wheel"));
            }
        }

        void Update()
        {
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                var p = car.position;
                float near = Mathf.Clamp01(Mathf.Abs(p.z - slowZ) / slowRadius);
                float v = speeds[i] * Mathf.Lerp(0.45f, 1f, near);
                p.z += direction[i] * v * Time.deltaTime;
                if (direction[i] > 0f && p.z > maxZ) p.z = minZ;
                if (direction[i] < 0f && p.z < minZ) p.z = maxZ;
                p.x = laneX[i];
                car.SetPositionAndRotation(p, Quaternion.Euler(0f, direction[i] > 0f ? 0f : 180f, 0f));
                foreach (var w in wheels[i]) w.Rotate(v * Time.deltaTime / 0.35f * Mathf.Rad2Deg, 0f, 0f, Space.Self);
            }
        }
    }
}
