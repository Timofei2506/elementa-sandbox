using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// A beaker / tank / barrel holding a live Mixture. Ticks reactions, shows the liquid,
    /// and reacts to world events (heat, impacts, being shot).
    /// </summary>
    public class FluidContainer : MonoBehaviour
    {
        public Mixture Mix { get; private set; }
        public float CapacityLiters { get; private set; } = 8f;
        public float GlassThicknessMM = 4f;
        public bool IsOpen = true;
        public bool HasSpark;
        public bool IsBroken { get; private set; }

        Renderer _glassRenderer, _liquidRenderer;
        Transform _liquid;
        MaterialPropertyBlock _mpb;
        float _reactionTimer;
        public string LastReactionRu { get; private set; } = "";
        public float LastHeatJ { get; private set; }

        public static FluidContainer Create(Transform parent, Vector3 position, float capacityLiters = 8f)
        {
            var go = new GameObject("Container");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Fluid");

            // glass shell
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glass.name = "Glass";
            glass.transform.SetParent(go.transform, false);
            glass.transform.localScale = new Vector3(0.85f, 0.6f, 0.85f);
            var glassMat = RuntimeVisuals.For(Look.Glass, new Color(0.75f, 0.88f, 0.95f), 0.05f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = glassMat;
            glass.layer = LayerMask.NameToLayer("Fluid");
            Destroy(glass.GetComponent<Collider>());
            // physics collider on the root
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.42f; cc.height = 1.2f;

            // liquid body
            var liq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liq.name = "Liquid";
            liq.transform.SetParent(go.transform, false);
            liq.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            liq.transform.localScale = new Vector3(0.78f, 0.01f, 0.78f);
            Destroy(liq.GetComponent<Collider>());
            liq.GetComponent<MeshRenderer>().sharedMaterial = RuntimeVisuals.For(Look.Liquid, new Color(0.24f, 0.61f, 0.88f), 0.05f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2.5f;
            rb.drag = 0.2f;
            rb.angularDrag = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var c = go.AddComponent<FluidContainer>();
            c.CapacityLiters = capacityLiters;
            c.Mix = new Mixture(Game.I.Substances, capacityLiters / 1000f)
            {
                ContainerId = go.GetInstanceID().ToString(),
                MaxVolume = capacityLiters / 1000f,
            };
            c._liquid = liq.transform;
            c._liquidRenderer = liq.GetComponent<MeshRenderer>();
            c._glassRenderer = glass.GetComponent<MeshRenderer>();
            c._mpb = new MaterialPropertyBlock();
            return c;
        }

        public void Fill(string substanceId, float liters)
        {
            Mix.AddVolumeML(substanceId, liters * 1000f);
            RefreshVisual();
        }

        public void Add(string substanceId, float ml) { Mix.AddVolumeML(substanceId, ml); RefreshVisual(); }

        public void Heat(float kelvin) { Mix.Temperature += kelvin; }

        public void Spark()
        {
            HasSpark = true;
            _reactionTimer = 0f;
        }

        public void BreakOpen()
        {
            if (IsBroken) return;
            IsBroken = true;
            var dom = Mix.Dominant();
            Color color = dom != null ? dom.color : new Color(0.3f, 0.6f, 0.9f);
            EffectsSystem.SpawnFluidSplash(transform.position, color, 40, 5f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Puddle";
            go.transform.position = new Vector3(transform.position.x, 0.02f, transform.position.z);
            float amount = Mathf.Clamp(Mix.Mass() * 8f, 0.6f, 2.6f);
            go.transform.localScale = new Vector3(amount, 0.01f, amount);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = RuntimeVisuals.For(Look.Liquid, color, 0.05f);
            Destroy(go, 30f);

            // leaking chemicals can react with the world
            if (Mix.Corrosivity > 0.4f)
                foreach (var obj in Game.I.World.Objects)
                    if (obj != null && Vector3.Distance(obj.transform.position, transform.position) < 2.5f)
                        obj.ApplyDamage(35f * Mix.Corrosivity);

            Destroy(gameObject);
        }

        void FixedUpdate()
        {
            if (Mix == null || IsBroken) return;
            float dt = Time.fixedDeltaTime;
            Mix.Tick(dt);

            // heat interaction with nearby hot objects
            foreach (var obj in Game.I.World.Objects)
            {
                if (obj == null) continue;
                if (Vector3.Distance(obj.transform.position, transform.position) > 1.6f) continue;
                if (obj.Temperature > Mix.Temperature + 40f)
                {
                    Mix.Temperature += (obj.Temperature - Mix.Temperature) * 0.25f * dt;
                    obj.Temperature -= 3f * dt;
                }
                else if (Mix.Temperature > obj.Temperature + 40f)
                {
                    obj.AddHeat((Mix.Temperature - obj.Temperature) * 0.3f * dt);
                }
            }

            if (Mix.Temperature > 320f || HasSpark)
            {
                _reactionTimer -= dt;
                if (_reactionTimer <= 0f)
                {
                    _reactionTimer = 0.05f;
                    StepReactions(dt * 8f);
                }
            }

            if (Mix.Temperature > 373f && IsOpen && !IsBurningReaction())
                EffectsSystem.SpawnSmoke(transform.position + Vector3.up * 0.6f, 0.6f, 1.2f, 0.4f);
        }

        bool IsBurningReaction() => LastReactionRu.Contains("орение") || LastReactionRu.Contains("взрыв");

        void StepReactions(float dt)
        {
            var found = Game.I.Reactions.FindReactions(Mix);
            if (found.Count == 0) { HasSpark = false; return; }

            foreach (var r in found)
            {
                float heat = Game.I.Reactions.Apply(r, Mix, dt, out float extent);
                if (extent <= 1e-5f) continue;

                LastReactionRu = r.nameRu;
                LastHeatJ = heat;
                Mix.Temperature += heat / Mathf.Max(1f, Mix.Mass() * 3800f);

                var eff = r.effects;

                if (eff.explosion > 0.01f)
                {
                    float tnt = Mathf.Max(1f, extent * eff.explosion * 220f);
                    Game.I.World.Explode(transform.position, tnt);
                    BreakOpen();
                    return;
                }
                if (eff.fire > 0.01f)
                    EffectsSystem.SpawnFire(transform.position + Vector3.up * 0.8f, Vector3.one * 0.6f, eff.fire, transform);
                if (eff.smoke > 0.01f)
                    EffectsSystem.SpawnSmoke(transform.position + Vector3.up * 0.7f, 1.1f, 1.8f, eff.smoke);
                if (eff.sparks || eff.light > 0.1f)
                    EffectsSystem.SpawnSparks(transform.position + Vector3.up * 0.5f, Vector3.up, (int)(8 * Mathf.Max(1f, eff.light * 2f)), 4f * Mathf.Max(1f, eff.light * 2f));
                if (eff.flash > 0.05f)
                    EffectsSystem.SpawnExplosion(transform.position, eff.flash * 0.6f);
                if (eff.fizz > 0.1f)
                    EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up * 0.5f, Mix.Dominant()?.color ?? Color.cyan, 8, 2.5f);
                if (eff.corrode > 0.1f)
                {
                    var dom = Mix.Dominant();
                    if (dom != null) EffectsSystem.SpawnSmoke(transform.position, 0.5f, 0.8f, 0.4f);
                }
                if (!string.IsNullOrEmpty(eff.gas))
                {
                    var gas = Game.I.Substances.TryGet(eff.gas);
                    if (gas != null) EffectsSystem.SpawnSmoke(transform.position + Vector3.up * 0.6f, 0.8f, 1.4f, 0.6f);
                }
                RefreshVisual();
            }
            HasSpark = found.Exists(r => r.effects.unstable);
        }

        public void RefreshVisual()
        {
            if (_liquidRenderer == null || Mix == null) return;
            var dom = Mix.Dominant();
            Color color = dom != null ? dom.color : new Color(0.3f, 0.6f, 0.9f);
            float fill = Mathf.Clamp01(Mix.TotalVolumeL / Mathf.Max(0.001f, CapacityLiters));
            float height = Mathf.Max(0.01f, fill * 1.05f);
            _liquid.localScale = new Vector3(0.78f, height, 0.78f);
            _liquid.localPosition = new Vector3(0f, -0.55f + height, 0f);
            _mpb.SetColor("_Color", color);
            _mpb.SetColor("_BaseColor", color);
            _liquidRenderer.SetPropertyBlock(_mpb);
        }

        public string StatusText()
        {
            if (Mix == null) return "";
            string s = $"pH {Mix.PH:F1}  |  {Mix.Temperature:F0} K\n{Mix.Describe()}";
            if (!string.IsNullOrEmpty(LastReactionRu)) s += "\n> " + LastReactionRu;
            return s;
        }

        void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            float strength = impulse / Mathf.Max(0.1f, Time.fixedDeltaTime);
            if (collision.relativeVelocity.magnitude > 4.5f)
            {
                var dom = Mix.Dominant();
                EffectsSystem.SpawnFluidSplash(transform.position, dom?.color ?? Color.cyan, 12, 3f);
                if (collision.relativeVelocity.magnitude > 9f && !IsBroken) BreakOpen();
            }
            if (Mix.Temperature > 400f && collision.relativeVelocity.magnitude > 2f) Spark();
        }
    }
}
