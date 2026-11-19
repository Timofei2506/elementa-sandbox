using UnityEngine;

namespace Elementa
{
    /// <summary>Camera shake driven by explosions and heavy impacts.</summary>
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake I { get; private set; }
        float _trauma;
        public float Decay = 1.6f;
        public float MaxOffset = 0.6f;

        void Awake()
        {
            I = this;
        }

        public static void Blast(Vector3 worldPosition, float power)
        {
            if (I == null) return;
            var cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, worldPosition) : 10f;
            float falloff = Mathf.Clamp01(1f - dist / 40f);
            I._trauma = Mathf.Clamp01(I._trauma + power * 0.12f * falloff);
        }

        public static void Impact(float strength) { if (I != null) I._trauma = Mathf.Clamp01(I._trauma + strength * 0.05f); }

        public Vector3 Offset
        {
            get
            {
                if (_trauma <= 0.001f) return Vector3.zero;
                float shake = _trauma * _trauma;
                float t = Time.time * 34f;
                return new Vector3(
                    Mathf.PerlinNoise(t, 0.3f) * 2f - 1f,
                    Mathf.PerlinNoise(0.7f, t) * 2f - 1f,
                    0f) * (MaxOffset * shake);
            }
        }

        public float Roll => _trauma > 0.001f
            ? (Mathf.PerlinNoise(Time.time * 21f, 5.1f) * 2f - 1f) * _trauma * 1.8f
            : 0f;

        void Update()
        {
            _trauma = Mathf.Max(0f, _trauma - Decay * Time.deltaTime * (0.4f + _trauma));
        }
    }

}
