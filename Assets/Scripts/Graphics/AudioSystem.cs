using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Procedural sound: every effect is synthesised at startup (no audio assets),
    /// including impacts by material, gunshots, explosions and bubbling liquids.
    /// </summary>
    public class AudioSystem : MonoBehaviour
    {
        public static AudioSystem I { get; private set; }
        AudioSource _sfx, _ambient;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        public bool Muted { get; private set; }
        float _lastImpact;

        public static AudioSystem Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSystem>();
            return a;
        }

        void Awake()
        {
            I = this;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0.35f;
            _sfx.volume = 0.55f;
            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.loop = true;
            _ambient.volume = 0.12f;
            _ambient.playOnAwake = false;

            _clips["metal"] = MetalHit();
            _clips["soft"] = SoftHit();
            _clips["glass"] = GlassBreak();
            _clips["shot"] = Gunshot(0.16f, 900f);
            _clips["cannon"] = Gunshot(0.35f, 320f);
            _clips["boom"] = Explosion(1.4f);
            _clips["fire"] = NoiseLoop(2f, 0.35f, 2200f);
            // фоли для рагдоллов: тело, хруст костей, отрыв конечности
            _clips["impact"] = SoftHit();
            _clips["thud"] = SoftHit();
            _ambient.clip = _clips["fire"];

            AudioListener.volume = 0.9f;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> sample, int rate = 44100)
        {
            int count = Mathf.Max(1, (int)(seconds * rate));
            var data = new float[count];
            var rng = new System.Random(1234);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Clamp(sample(t, i) , -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static System.Random _rng = new System.Random(42);
        static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        static AudioClip MetalHit()
        {
            return Make("metal_hit", 0.35f, (t, i) =>
            {
                float env = Mathf.Exp(-t * 16f);
                float ring = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 410f * t) * 0.2f;
                return (ring * 0.55f + Noise() * 0.25f) * env;
            });
        }

        static AudioClip SoftHit()
        {
            return Make("soft_hit", 0.22f, (t, i) =>
            {
                float env = Mathf.Exp(-t * 26f);
                return (Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f + Noise() * 0.4f) * env;
            });
        }

        static AudioClip GlassBreak()
        {
            return Make("glass_break", 0.5f, (t, i) =>
            {
                float env = Mathf.Exp(-t * 9f);
                float hiss = Noise() * Mathf.Exp(-t * 22f);
                float shards = Mathf.Sin(2f * Mathf.PI * (2400f + 600f * Mathf.Sin(t * 60f)) * t) * 0.25f;
                return (hiss * 0.8f + shards) * env;
            });
        }

        static AudioClip Gunshot(float length, float toneHz)
        {
            return Make("shot", length, (t, i) =>
            {
                float env = Mathf.Exp(-t * (length < 0.2f ? 34f : 12f));
                float body = Mathf.Sin(2f * Mathf.PI * toneHz * t * 0.25f) * 0.4f;
                return (Noise() * 0.85f + body) * env;
            });
        }

        static AudioClip Explosion(float length)
        {
            return Make("boom", length, (t, i) =>
            {
                float env = Mathf.Exp(-t * 2.4f);
                float low = Mathf.Sin(2f * Mathf.PI * (70f - 25f * t) * t) * 0.7f;
                float crack = Noise() * Mathf.Exp(-t * 12f) * 0.9f;
                return (low + crack) * env;
            });
        }

        static AudioClip NoiseLoop(float length, float cutoff, float rate)
        {
            float lp = 0f;
            return Make("noise_loop", length, (t, i) =>
            {
                lp += (Noise() - lp) * 0.08f;
                return lp * 0.6f;
            });
        }

        public void Play(string clip, float volume = 1f, float pitch = 1f, Vector3? position = null)
        {
            if (Muted) return;
            if (!_clips.TryGetValue(clip, out var c) || c == null)
            {
                // неизвестный ключ не должен ломать логику: тихо подставляем ближайший фоли
                if (!_clips.TryGetValue("soft", out c) || c == null) return;
            }
            _sfx.pitch = pitch;
            _sfx.volume = 0.6f * volume;
            _sfx.PlayOneShot(c);
        }

        /// <summary>Plays the right impact sound for a material category.</summary>
        public void ImpactFor(MaterialDef mat, float strength = 1f)
        {
            if (mat == null) return;
            if (Time.unscaledTime - _lastImpact < 0.035f) return;
            _lastImpact = Time.unscaledTime;
            string clip = mat.category switch
            {
                "metal" => "metal",
                "ceramic" => "glass",
                _ => "soft",
            };
            Play(clip, Mathf.Clamp01(strength), UnityEngine.Random.Range(0.9f, 1.1f));
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 0.9f;
        }
    }
}
