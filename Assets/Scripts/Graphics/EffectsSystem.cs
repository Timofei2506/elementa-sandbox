using System.Collections.Generic;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Pooled particle effects: fire, smoke, sparks, explosions, fluid splashes, tracers.
    /// Everything is generated from small procedural particle systems at runtime.
    /// </summary>
    public class EffectsSystem : MonoBehaviour
    {
        public static EffectsSystem I { get; private set; }

        readonly Dictionary<int, ParticleSystem> _fires = new Dictionary<int, ParticleSystem>();
        readonly Stack<ParticleSystem> _smokePool = new Stack<ParticleSystem>();
        readonly Stack<ParticleSystem> _sparkPool = new Stack<ParticleSystem>();
        readonly Stack<ParticleSystem> _fluidPool = new Stack<ParticleSystem>();
        Material _smokeMat, _sparkMat, _fireMat, _shadowMat;
        Texture2D _radial, _shadowTex;
        Transform _root;

        void Awake()
        {
            I = this;
            _root = new GameObject("Effects").transform;
            _root.SetParent(transform, false);
            _radial = RuntimeVisuals.RadialTexture();
            _shadowTex = RuntimeVisuals.RadialTexture(64, 1.1f);
            _smokeMat = MakeParticleMaterial(_radial, new Color(0.25f, 0.25f, 0.27f, 0.5f), "Sprites/Default");
            _sparkMat = MakeParticleMaterial(_radial, new Color(1f, 0.75f, 0.3f, 1f), "Sprites/Default");
            _fireMat = MakeParticleMaterial(_radial, new Color(1f, 0.55f, 0.15f, 1f), "Sprites/Default");
        }

        static Material MakeParticleMaterial(Texture2D tex, Color color, string shader)
        {
            var sh = Shader.Find(shader) ?? RuntimeVisuals.Standard;
            var m = new Material(sh) { name = "ELEMENTA_Particle", mainTexture = tex, color = color };
            return m;
        }

        public static void SpawnFire(Vector3 position, Vector3 scale, float intensity, Transform parent)
        {
            if (I == null) return;
            int id = parent != null ? parent.GetInstanceID() : 0;
            if (I._fires.ContainsKey(id)) return;
            var go = new GameObject("Fire");
            go.transform.SetParent(I._root, false);
            go.transform.position = position + Vector3.up * Mathf.Max(0.1f, scale.y * 0.45f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * intensity + 0.15f, 0.65f * intensity + 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.35f, 0.05f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 45f * Mathf.Max(0.3f, intensity);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = Mathf.Max(0.15f, scale.x * 0.3f);
            shape.scale = Vector3.one;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = I._fireMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            I._fires[id] = ps;
            var follow = go.AddComponent<FollowTarget>();
            follow.Target = parent;
            follow.Offset = Vector3.up * Mathf.Max(0.1f, scale.y * 0.45f);
        }

        public static void DespawnFire(SandboxObject obj) => DespawnFireFor(obj != null ? obj.transform : null);

        public static void DespawnFireFor(Transform target)
        {
            if (I == null || target == null) return;
            int id = target.GetInstanceID();
            if (I._fires.TryGetValue(id, out var ps))
            {
                if (ps != null) Destroy(ps.gameObject);
                I._fires.Remove(id);
            }
        }

        public static void SpawnSmoke(Vector3 position, float size = 1f, float lifetime = 1.6f, float intensity = 1f)
        {
            if (I == null) return;
            var ps = I.Rent(I._smokePool, "Smoke");
            ps.transform.position = position;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f * size, 1.2f * size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.35f, 0.38f, 0.55f), new Color(0.15f, 0.15f, 0.16f, 0.35f));
            var em = ps.emission; em.rateOverTime = 25f * intensity; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(8 * intensity)) });
            ps.Play();
        }

        public static void SpawnSparks(Vector3 position, Vector3 direction, int count = 14, float speed = 8f)
        {
            if (I == null) return;
            var ps = I.Rent(I._sparkPool, "Sparks");
            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.up);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f));
            main.gravityModifier = 1.4f;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28f; shape.radius = 0.05f;
            ps.Play();
        }

        public static void SpawnFluidSplash(Vector3 position, Color color, int count = 26, float speed = 4.5f)
        {
            if (I == null) return;
            var ps = I.Rent(I._fluidPool, "Splash");
            ps.transform.position = position;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, color * 0.8f);
            main.gravityModifier = 1.2f;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.1f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = RuntimeVisuals.TrailMaterial(color);
            ps.Play();
        }

        public static void SpawnExplosion(Vector3 position, float power)
        {
            if (I == null) return;
            float radius = Mathf.Clamp(1.2f + power * 0.55f, 1.2f, 9f);
            bool mobile = Game.I != null && Game.I.IsMobile;
            // core flash
            var flash = I.Rent(I._sparkPool, "Flash");
            flash.transform.position = position;
            var fm = flash.main;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f * power * 0.5f);
            fm.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 1.4f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.5f, 0.12f, 0.8f));
            var em = flash.emission; em.rateOverTime = 0f;
            short burst = (short)Mathf.Clamp(power * (mobile ? 6 : 12), 8, mobile ? 40 : 90);
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
            var sh = flash.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = flash.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.4f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            flash.Play();
            SpawnSparks(position, Vector3.up, Mathf.Clamp((int)(power * 18), 10, 120), 6f + power * 3f);
            SpawnSmoke(position, 2.6f + power * 0.6f, 2.2f, 2f + power);
            ScreenShake.Blast(position, power);
        }

        ParticleSystem Rent(Stack<ParticleSystem> pool, string name)
        {
            if (pool.Count > 48) { /* hard cap on live systems */ }
            ParticleSystem ps;
            if (pool.Count > 0 && pool.Peek() != null) ps = pool.Pop();
            else
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root, false);
                ps = go.AddComponent<ParticleSystem>();
                var psr = go.GetComponent<ParticleSystemRenderer>();
                psr.material = name switch
                {
                    "Smoke" => _smokeMat,
                    "Sparks" or "Flash" => _sparkMat,
                    _ => _fluidMatFallback(),
                };
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.1f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var col = ps.colorOverLifetime; col.enabled = false;
                var sol = ps.sizeOverLifetime; sol.enabled = false;
            }
            ps.gameObject.SetActive(true);
            ps.Clear();
            return ps;
        }

        Material _fluidFallback;
        Material _fluidMatFallback() => _fluidFallback != null ? _fluidFallback : _fluidFallback = MakeParticleMaterial(_radial, new Color(0.3f, 0.6f, 0.9f, 1f), "Sprites/Default");

        void Update()
        {
            // recycle finished particle systems
            Recycle(_smokePool, "Smoke");
            Recycle(_sparkPool, "Sparks");
            Recycle(_fluidPool, "Splash");
        }

        void Recycle(Stack<ParticleSystem> pool, string tag)
        {
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var t = _root.GetChild(i);
                if (!t.name.StartsWith(tag)) continue;
                var ps = t.GetComponent<ParticleSystem>();
                if (ps == null) continue;
                if (!ps.IsAlive(true))
                {
                    t.gameObject.SetActive(false);
                    pool.Push(ps);
                }
            }
        }

        public static void ClearAll()
        {
            if (I == null) return;
            for (int i = I._root.childCount - 1; i >= 0; i--)
                Destroy(I._root.GetChild(i).gameObject);
            I._fires.Clear();
            I._smokePool.Clear();
            I._sparkPool.Clear();
            I._fluidPool.Clear();
        }
    }

    /// <summary>Keeps a fire effect glued to a burning object.</summary>
    public class FollowTarget : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset;
        void LateUpdate()
        {
            if (Target == null) { Destroy(gameObject); return; }
            transform.position = Target.position + Offset;
        }
    }
}
