using System;
using System.Collections.Generic;
using System.Linq;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Owns every simulated entity and exposes the sandbox API used by tools, UI and NPCs:
    /// spawning, damage resolution, saving/loading, resetting the arena.
    /// </summary>
    public class SandboxWorld : MonoBehaviour
    {
        public Transform ObjectsRoot { get; private set; }
        public Transform ContainersRoot { get; private set; }
        public List<SandboxObject> Objects { get; } = new List<SandboxObject>();
        public List<FluidContainer> Containers { get; } = new List<FluidContainer>();
        public List<NpcActor> Npcs { get; } = new List<NpcActor>();

        public bool GravityEnabled = true;
        public float TimeScale = 1f;

        public event Action<SandboxObject> OnObjectSpawned;
        public event Action<SandboxObject> OnObjectRemoved;

        MaterialDatabase Mats => Game.I.Materials;

        public void Init()
        {
            ObjectsRoot = new GameObject("Objects").transform;
            ObjectsRoot.SetParent(transform, false);
            ContainersRoot = new GameObject("Containers").transform;
            ContainersRoot.SetParent(transform, false);

            var env = new GameObject("Environment").transform;
            env.SetParent(transform, false);
            WorldEnvironment.Build(env);

            var effects = new GameObject("EffectsSystem");
            effects.transform.SetParent(transform, false);
            effects.AddComponent<EffectsSystem>();
        }

        // ---------------- spawning ----------------
        public SandboxObject Spawn(string materialId, BodyShape shape, Vector3 size, Vector3 position, float rotation = 0f)
        {
            var mat = Mats.TryGet(materialId);
            if (mat == null)
            {
                Debug.LogWarning("[ELEMENTA] Неизвестный материал: " + materialId);
                mat = Mats.All.Count > 0 ? Mats.All[0] : null;
                if (mat == null) return null;
            }
            return Spawn(mat, shape, size, position, rotation);
        }

        public SandboxObject Spawn(MaterialDef mat, BodyShape shape, Vector3 size, Vector3 position, float rotation = 0f)
        {
            var obj = SandboxObject.Create(ObjectsRoot, mat, shape, size, position, rotation);
            Objects.Add(obj);
            obj.OnDestroyed += HandleDestroyed;
            OnObjectSpawned?.Invoke(obj);
            return obj;
        }

        void HandleDestroyed(SandboxObject obj)
        {
            Objects.Remove(obj);
            EffectsSystem.DespawnFire(obj);
            OnObjectRemoved?.Invoke(obj);
            if (obj != null)
            {
                var pos = obj.transform.position;
                EffectsSystem.SpawnSmoke(pos, 1.2f, 1.1f, 1.2f);
            }
        }

        public FluidContainer SpawnContainer(Vector3 position, float capacityLiters = 8f)
        {
            var c = FluidContainer.Create(ContainersRoot, position, capacityLiters);
            Containers.Add(c);
            return c;
        }

        public NpcActor SpawnNpc(Vector3 position, NpcActor.NpcKind kind = NpcActor.NpcKind.Human)
        {
            var npc = NpcActor.Create(ObjectsRoot, position, kind, this);
            Npcs.Add(npc);
            return npc;
        }

        // ---------------- 2D-хелперы поиска ----------------
        // Камера ортографическая и смотрит по +Z, поэтому «что под курсором» считается
        // простым расстоянием в плоскости X/Y — плоскость симуляции z = 0.
        public SandboxObject ObjectAt(Vector3 point, float radius = 0.5f)
        {
            SandboxObject best = null;
            float bestDist = float.MaxValue;
            foreach (var o in Objects)
            {
                if (o == null) continue;
                var p = o.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(point.x, point.y));
                float extent = Mathf.Max(o.WorldSize.x, o.WorldSize.y) * 0.5f;
                if (d > radius + extent) continue;
                if (d < bestDist) { bestDist = d; best = o; }
            }
            return best;
        }

        public NpcActor NpcAt(Vector3 point, float radius = 0.7f)
        {
            NpcActor best = null;
            float bestDist = float.MaxValue;
            foreach (var n in Npcs)
            {
                if (n == null) continue;
                var p = n.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(point.x, point.y));
                if (d > radius + 0.9f) continue;
                if (d < bestDist) { bestDist = d; best = n; }
            }
            return best;
        }

        public FluidContainer ContainerAt(Vector3 point, float radius = 0.8f)
        {
            FluidContainer best = null;
            float bestDist = float.MaxValue;
            foreach (var c in Containers)
            {
                if (c == null) continue;
                var p = c.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(point.x, point.y));
                if (d > radius) continue;
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        // ---------------- physics helpers ----------------
        /// <summary>Applies an explosion: radial impulse + damage falloff + fire ignition.</summary>
        public void Explode(Vector3 center, float powerGramsTNT, float igniteChance = 0.65f)
        {
            float radius = Mathf.Clamp(1.5f + Mathf.Sqrt(powerGramsTNT) * 0.55f, 1.8f, 14f);
            float joules = powerGramsTNT * 4184f;

            EffectsSystem.SpawnExplosion(center, Mathf.Clamp(powerGramsTNT / 200f, 0.4f, 6f));
            AudioSystem.I?.Play("boom", Mathf.Clamp01(powerGramsTNT / 300f), UnityEngine.Random.Range(0.85f, 1f));

            foreach (var obj in Objects.ToList())
            {
                if (obj == null) continue;
                var delta = obj.transform.position - center;
                float dist = delta.magnitude;
                if (dist > radius) continue;
                float falloff = 1f - dist / radius;
                float dmg = powerGramsTNT * 1.8f * falloff * falloff;
                obj.ApplyDamage(dmg, joules * falloff * 0.1f / Mathf.Max(1f, Objects.Count));
                if (obj.Body != null)
                {
                    var flat = new Vector3(delta.x, delta.y, 0f);
                    var dir = (flat.normalized + Vector3.up * 0.55f).normalized;
                    obj.Body.AddForce(dir * (powerGramsTNT * 0.9f * falloff) / Mathf.Max(0.3f, obj.Mass * 0.02f), ForceMode.Impulse);
                    obj.Body.AddTorque(new Vector3(0f, 0f, UnityEngine.Random.Range(-1f, 1f)) * powerGramsTNT * 0.02f * falloff, ForceMode.Impulse);
                }
                if (obj.Material.flammability > igniteChance * 0.5f && UnityEngine.Random.value < igniteChance * falloff)
                    obj.Ignite();
            }

            foreach (var npc in Npcs.ToList())
            {
                if (npc == null) continue;
                float dist = Vector3.Distance(npc.transform.position, center);
                if (dist > radius * 1.3f) continue;
                float falloff = 1f - dist / (radius * 1.3f);
                npc.ApplyDamage(powerGramsTNT * 1.2f * falloff * falloff, center);
            }

            foreach (var c in Containers.ToList())
            {
                if (c == null) continue;
                float dist = Vector3.Distance(c.transform.position, center);
                if (dist < radius * 0.9f) c.BreakOpen();
            }
        }

        /// <summary>Resolves a projectile hit against the first collider and applies armour + damage.</summary>
        public HitOutcome ResolveHit(ProjectileStats stats, RaycastHit raycastHit, float impactAngle)
        {
            var layers = new List<ArmorLayer>();
            var armor = raycastHit.collider.GetComponentInParent<ArmorPlate>();
            if (armor != null) layers.Add(new ArmorLayer(armor.Material, armor.ThicknessMM, impactAngle));

            var target = raycastHit.collider.GetComponentInParent<SandboxObject>();
            if (target != null && armor == null)
                layers.Add(new ArmorLayer(target.Material, Mathf.Max(2f, target.WorldSize.magnitude * 100f * 0.35f)));

            var outcome = ArmorModel.Resolve(stats, layers, impactAngle);

            if (target != null)
            {
                float dmg = ArmorModel.BodyDamage(stats, outcome, target.Material.toughness)
                            + stats.energyJ * 0.004f;
                target.ApplyDamage(dmg, outcome.heatJ);
                if (stats.kind == ProjectileKind.Incendiary && UnityEngine.Random.value < 0.6f) target.Ignite();
            }

            var npc = raycastHit.collider.GetComponentInParent<NpcActor>();
            if (npc != null) npc.ApplyDamage(ArmorModel.BodyDamage(stats, outcome, 15f) * 0.6f, raycastHit.point);

            var container = raycastHit.collider.GetComponentInParent<FluidContainer>();
            if (container != null && outcome.result == HitResult.Penetrated && UnityEngine.Random.value < 0.5f)
                container.BreakOpen();

            if (outcome.blastGramsTNT > 0.5f) Explode(raycastHit.point, outcome.blastGramsTNT);

            if (outcome.result is HitResult.Penetrated or HitResult.Partial)
                EffectsSystem.SpawnSparks(raycastHit.point, -raycastHit.normal, 10, 5.5f);
            else
                EffectsSystem.SpawnSparks(raycastHit.point, raycastHit.normal, 5, 3f);

            return outcome;
        }

        // ---------------- world controls ----------------
        public void SetGravity(bool on)
        {
            GravityEnabled = on;
            Physics.gravity = on ? new Vector3(0f, -9.81f, 0f) : Vector3.zero;
        }

        public void ClearAll()
        {
            foreach (var o in Objects.ToList()) if (o != null) { EffectsSystem.DespawnFire(o); Destroy(o.gameObject); }
            foreach (var c in Containers.ToList()) if (c != null) Destroy(c.gameObject);
            foreach (var n in Npcs.ToList()) if (n != null) Destroy(n.gameObject);
            Objects.Clear(); Containers.Clear(); Npcs.Clear();
            EffectsSystem.ClearAll();
            Game.I?.NotifyWorldReset();
        }

        public void ResetWorld()
        {
            ClearAll();
            BuildStarterScene();
        }

        /// <summary>Стартовая сцена: платформы, ящики, ёмкости и пара персонажей — всё в плоскости экрана.</summary>
        public void BuildStarterScene()
        {
            // пол и стены арены строит WorldEnvironment; здесь только объекты «на сцене»
            Spawn("steel_1045", BodyShape.Plate, new Vector3(4.5f, 0.35f, 1f), new Vector3(-6f, 2.2f, 0f));
            Spawn("steel_1045", BodyShape.Plate, new Vector3(4.5f, 0.35f, 1f), new Vector3(6f, 2.2f, 0f));
            Spawn("concrete", BodyShape.Box, new Vector3(1f, 1f, 1f), new Vector3(-8.5f, 0.6f, 0f));
            Spawn("concrete", BodyShape.Box, new Vector3(1f, 1f, 1f), new Vector3(-8.5f, 1.7f, 0f));
            Spawn("oak", BodyShape.Box, new Vector3(1f, 1f, 1f), new Vector3(-7.5f, 0.6f, 0f));
            Spawn("brick", BodyShape.Box, new Vector3(1f, 0.6f, 1f), new Vector3(-7.5f, 1.4f, 0f));
            Spawn("aluminium", BodyShape.Cylinder, new Vector3(0.9f, 1.3f, 1f), new Vector3(9.5f, 0.8f, 0f));
            Spawn("glass", BodyShape.Plate, new Vector3(1.8f, 2.4f, 0.4f), new Vector3(2.5f, 1.4f, 0f));
            Spawn("kevlar", BodyShape.Plate, new Vector3(1.8f, 2.4f, 0.4f), new Vector3(4.2f, 1.4f, 0f));
            Spawn("ceramic_alumina", BodyShape.Plate, new Vector3(1.8f, 2.4f, 0.4f), new Vector3(5.9f, 1.4f, 0f));

            var beaker = SpawnContainer(new Vector3(-3.4f, 0.7f, 0f), 6f);
            beaker.Fill("water", 2.0f);
            var acidFlask = SpawnContainer(new Vector3(-2.0f, 0.7f, 0f), 5f);
            acidFlask.Fill("hcl", 1.5f);
            var baseFlask = SpawnContainer(new Vector3(-0.6f, 0.7f, 0f), 5f);
            baseFlask.Fill("naoh", 1.5f);
            var fuelBarrel = SpawnContainer(new Vector3(0.8f, 0.7f, 0f), 8f);
            fuelBarrel.Fill("gasoline", 3.0f);

            SpawnNpc(new Vector3(-1.5f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Still);
            SpawnNpc(new Vector3(1.5f, 1.0f, 0f), NpcActor.NpcKind.Scientist).SetBehaviour(NpcActor.Behaviour.Wander);
        }

        void LateUpdate()
        {
            if (Mathf.Abs(Time.timeScale - TimeScale) > 0.001f) Time.timeScale = TimeScale;
        }
    }
}
