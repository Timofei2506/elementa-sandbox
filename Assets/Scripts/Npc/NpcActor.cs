using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Персонаж-рагдолл в духе Melon Sandbox: набор связанных тел в плоскости экрана.
    /// Части висят на суставах-шарнирах, отрываются от перегрузки, горят, истекают кровью
    /// и складываются в кучу. Это не «ходячий актёр», а физический объект, которым играют.
    /// </summary>
    public class NpcActor : MonoBehaviour
    {
        public enum NpcKind { Human, Heavy, Scientist, Zombie, Robot }
        public enum Behaviour { Still, Wander, Flee, Panic }

        public NpcKind Kind { get; private set; } = NpcKind.Human;
        public Behaviour CurrentBehaviour { get; private set; } = Behaviour.Still;
        public float Health { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;
        public float Temperature = 310f;
        public bool IsDead => Health <= 0f;
        public bool IsBurning { get; private set; }

        public class Limb
        {
            public string name;
            public BodyPart part;
            public float health = 1f;          // 0..1, 0 = оторван
            public float maxImpulse = 1f;      // порог отрыва
        }

        readonly List<Limb> _limbs = new List<Limb>();
        SandboxWorld _world;
        Rigidbody _root;
        Color _skin, _cloth, _pants;
        float _burnTimer, _bleed, _thinkTimer;
        Vector3 _wanderTo;
        float _wanderTimer;
        Transform _marker;

        public event Action<NpcActor> OnDied;

        public static NpcActor Create(Transform parent, Vector3 position, NpcKind kind, SandboxWorld world)
        {
            var go = new GameObject("Ragdoll_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var npc = go.AddComponent<NpcActor>();
            npc.Kind = kind;
            npc._world = world;
            npc.Build();
            return npc;
        }

        void Build()
        {
            float scale = Kind == NpcKind.Heavy ? 1.35f : Kind == NpcKind.Robot ? 1.4f : 1f;
            MaxHealth = Kind switch
            {
                NpcKind.Heavy => 320f,
                NpcKind.Robot => 420f,
                NpcKind.Zombie => 160f,
                NpcKind.Scientist => 65f,
                _ => 100f
            };
            Health = MaxHealth;

            switch (Kind)
            {
                case NpcKind.Heavy: _skin = new Color(.72f, .55f, .42f); _cloth = new Color(.24f, .28f, .24f); _pants = new Color(.16f, .17f, .18f); break;
                case NpcKind.Scientist: _skin = new Color(.86f, .68f, .55f); _cloth = new Color(.92f, .93f, .95f); _pants = new Color(.35f, .38f, .45f); break;
                case NpcKind.Zombie: _skin = new Color(.45f, .60f, .38f); _cloth = new Color(.35f, .30f, .28f); _pants = new Color(.20f, .22f, .25f); break;
                case NpcKind.Robot: _skin = new Color(.55f, .60f, .68f); _cloth = new Color(.30f, .42f, .52f); _pants = new Color(.22f, .25f, .30f); break;
                default: _skin = new Color(.82f, .64f, .50f); _cloth = new Color(.28f, .42f, .62f); _pants = new Color(.20f, .22f, .28f); break;
            }

            // ---- скелет в плоскости XY ----
            var torso = AddLimb("Torso", new Vector3(0f, 1.05f, 0f) * scale, new Vector3(0.46f, 0.72f, 0.30f) * scale, _cloth, 4f, -0.12f);
            _root = torso.part.Body;
            AddLimb("Hips", new Vector3(0f, 0.62f, 0f) * scale, new Vector3(0.42f, 0.26f, 0.28f) * scale, _pants, 3.5f, 0.10f);
            AddLimb("Head", new Vector3(0f, 1.55f, 0f) * scale, new Vector3(0.34f, 0.34f, 0.30f) * scale, _skin, 1.5f, -0.02f);
            AddLimb("ArmL", new Vector3(-0.30f, 1.16f, 0f) * scale, new Vector3(0.15f, 0.60f, 0.22f) * scale, _cloth, 0.9f, 0.14f);
            AddLimb("ArmR", new Vector3(0.30f, 1.16f, 0f) * scale, new Vector3(0.15f, 0.60f, 0.22f) * scale, _cloth, 0.9f, 0.14f);
            AddLimb("LegL", new Vector3(-0.12f, 0.30f, 0f) * scale, new Vector3(0.20f, 0.62f, 0.24f) * scale, _pants, 1.4f, 0.18f);
            AddLimb("LegR", new Vector3(0.12f, 0.30f, 0f) * scale, new Vector3(0.20f, 0.62f, 0.24f) * scale, _pants, 1.4f, 0.18f);

            // ---- суставы: шарниры, чтобы рагдолл складывался, но не разваливался сам ----
            foreach (var limb in _limbs)
            {
                if (limb.part == _limbs[0].part) continue;
                Join(torso.part, limb.part);
            }

            _wanderTo = transform.position;
        }

        Limb AddLimb(string name, Vector3 localPos, Vector3 scale, Color color, float mass, float maxImpulse)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            // 2D: это плоская «деталь» персонажа
            go.transform.localScale = new Vector3(scale.x, scale.y, Mathf.Max(0.2f, scale.z));
            go.AddComponent<MeshFilter>().sharedMesh = RuntimeVisuals.Box;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RuntimeVisuals.For(Kind == NpcKind.Robot ? Look.Metal : Look.Flesh, color, 0.72f, Kind == NpcKind.Robot ? 0.6f : 0f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            PhysicsCompat.SetLinearDamping(rb, 0.15f);
            PhysicsCompat.SetAngularDamping(rb, 0.35f);
            rb.constraints = RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;

            var part = go.AddComponent<BodyPart>();
            part.Init(this, name);
            var limb = new Limb { name = name, part = part, maxImpulse = maxImpulse };
            _limbs.Add(limb);
            return limb;
        }

        static void Join(BodyPart a, BodyPart b)
        {
            var joint = a.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = b.Body;
            joint.autoConfigureConnectedAnchor = true;
            joint.useLimits = true;
            joint.limits = new JointLimits { min = -120f, max = 120f };
        }

        public Rigidbody Body => _root;

        public void SetBehaviour(Behaviour b)
        {
            CurrentBehaviour = b;
            if (b == Behaviour.Wander) PickWander();
        }

        void PickWander()
        {
            _wanderTo = new Vector3(
                UnityEngine.Random.Range(-WorldEnvironment.ArenaHalfSize * 0.7f, WorldEnvironment.ArenaHalfSize * 0.7f),
                0.4f, 0f);
            _wanderTimer = UnityEngine.Random.Range(2.5f, 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsDead)
            {
                _bleed -= dt * 0.4f;
                if (_bleed <= 0f) { Die(); }
                return;
            }

            if (IsBurning)
            {
                _burnTimer += dt;
                Health -= 11f * dt;
                Temperature = Mathf.Max(Temperature, 900f);
                if (UnityEngine.Random.value < dt * 6f)
                    EffectsSystem.SpawnSmoke(transform.position + Vector3.up * 0.8f, 0.5f, 0.8f, 0.5f);
                if (Health <= 0f) Die();
            }
            else
            {
                Temperature = Mathf.MoveTowards(Temperature, 310f, 14f * dt);
            }

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = 0.22f;
                Think();
            }

            // потеря конечностей превращается в кровотечение
            if (_bleed > 0f)
            {
                Health -= _bleed * dt;
                if (UnityEngine.Random.value < dt * 3f)
                    EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up * 0.3f, new Color(0.5f, 0.03f, 0.03f), 4, 1.6f);
                if (Health <= 0f) Die();
            }

            if (transform.position.y < -25f) Die();
        }

        void Think()
        {
            Vector3 desired = Vector3.zero;
            switch (CurrentBehaviour)
            {
                case Behaviour.Wander:
                    _wanderTimer -= 0.22f;
                    if (_wanderTimer <= 0f || Vector2.Distance(_wanderTo, transform.position) < 0.8f) PickWander();
                    desired = _wanderTo - transform.position;
                    break;
                case Behaviour.Flee:
                case Behaviour.Panic:
                    desired = transform.position - NearestHazard();
                    break;
            }
            if (desired.sqrMagnitude < 0.04f) return;

            // «ходьба» — это импульсы в таз и корпус, а не телепорт: рагдолл остаётся физикой
            var dir = new Vector3(desired.x, 0f, 0f).normalized;
            foreach (var limb in _limbs)
            {
                if (limb.part == null || limb.part.Body == null) continue;
                if (limb.name is "LegL" or "LegR" or "Hips")
                    limb.part.Body.AddForce(dir * 22f, ForceMode.Force);
            }
        }

        Vector3 NearestHazard()
        {
            Vector3 best = transform.position + Vector3.right * 5f;
            float bestDist = 30f;
            if (_world == null) return best;
            foreach (var o in _world.Objects)
            {
                if (o == null || !o.IsBurning) continue;
                float d = Vector2.Distance(o.transform.position, transform.position);
                if (d < bestDist) { bestDist = d; best = o.transform.position; }
            }
            return best;
        }

        public void ApplyDamage(float amount, Vector3? source = null)
        {
            if (IsDead || amount <= 0f) return;
            Health -= amount;

            // попадание отрывает ближайшую конечность, если перегрузка выше её порога
            if (amount > 18f && _limbs.Count > 2)
            {
                Limb pick = null;
                float bestDist = float.MaxValue;
                var from = source ?? transform.position + Vector3.right;
                foreach (var limb in _limbs)
                {
                    if (limb.part == null || limb.part.name == "Torso" || limb.part.name == "Head") continue;
                    float d = Vector2.Distance(limb.part.transform.position, from);
                    if (d < bestDist && amount > limb.maxImpulse * 26f) { bestDist = d; pick = limb; }
                }
                if (pick != null) Detach(pick);
            }

            if (source.HasValue)
            {
                foreach (var limb in _limbs)
                    if (limb.part != null && limb.part.Body != null)
                        limb.part.Body.AddForce((transform.position - source.Value).normalized * Mathf.Clamp(amount, 2f, 140f), ForceMode.Impulse);
            }

            EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up * 0.6f, new Color(0.6f, 0.08f, 0.08f), 8, 2.2f);
            if (CurrentBehaviour == Behaviour.Still) SetBehaviour(Behaviour.Panic);
            if (Health <= 0f) Die();
        }

        /// <summary>Отрывает конечность: сустав разрывается, часть остаётся физическим объектом.</summary>
        public void Detach(Limb limb)
        {
            if (limb?.part == null) return;
            var joint = limb.part.GetComponent<Joint>();
            if (joint != null) Destroy(joint);
            limb.health = 0f;
            _bleed += 2.2f;
            EffectsSystem.SpawnFluidSplash(limb.part.transform.position, new Color(0.55f, 0.05f, 0.05f), 14, 3f);
            Game.I?.UI?.Log("Конечность оторвана: " + limb.name);
        }

        public void Ignite()
        {
            if (IsBurning || IsDead) return;
            IsBurning = true;
            _burnTimer = 0f;
            EffectsSystem.SpawnFire(transform.position + Vector3.up * 0.7f, Vector3.one * 1.1f, 1f, transform);
            SetBehaviour(Behaviour.Panic);
        }

        public void Extinguish()
        {
            IsBurning = false;
            EffectsSystem.DespawnFireFor(transform);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        void Die()
        {
            if (Health <= 0f && _bleed < -100f) return;
            Health = 0f;
            _bleed = -1000f;
            EffectsSystem.DespawnFireFor(transform);
            foreach (var limb in _limbs)
            {
                var joint = limb.part != null ? limb.part.GetComponent<Joint>() : null;
                if (joint != null) Destroy(joint);
            }
            EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up * 0.3f, new Color(0.45f, 0.03f, 0.03f), 40, 3.2f);
            AudioSystem.I?.Play("impact", 0.5f, 0.9f);
            OnDied?.Invoke(this);
        }

        public void Remove() => Destroy(gameObject);

        public string StatusText()
        {
            int attached = 0;
            foreach (var l in _limbs) if (l.health > 0f) attached++;
            string state = IsDead ? "мёртв" : IsBurning ? "горит" : CurrentBehaviour switch
            {
                Behaviour.Wander => "идёт",
                Behaviour.Flee => "убегает",
                Behaviour.Panic => "паника",
                _ => "стоит"
            };
            return $"{Kind} · {state} · {Health:F0}/{MaxHealth:F0} HP · частей {attached}/{_limbs.Count} · {Temperature:F0} K";
        }
    }

    /// <summary>Часть тела рагдолла: сама сообщает хозяину о попаданиях.</summary>
    public class BodyPart : MonoBehaviour
    {
        public NpcActor Owner { get; private set; }
        public Rigidbody Body { get; private set; }
        string _name;

        public void Init(NpcActor owner, string partName)
        {
            Owner = owner;
            Body = GetComponent<Rigidbody>();
            _name = partName;
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 8f || Owner == null) return;
            Owner.ApplyDamage((speed - 7f) * Body.mass * 0.9f, collision.GetContact(0).point);
            Sound();
        }

        void Sound()
        {
            if (UnityEngine.Random.value < 0.35f) AudioSystem.I?.Play("impact", 0.35f, UnityEngine.Random.Range(0.85f, 1.15f));
        }
    }
}
