using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Sandbox NPC: a simple articulated figure with health, ragdoll death,
    /// and pluggable behaviours (idle, wander, chase, flee, follow player cursor).
    /// </summary>
    public class NpcActor : MonoBehaviour
    {
        public enum NpcKind { Human, Heavy, Scientist, Zombie, Robot }
        public enum Behaviour { Idle, Wander, Follow, Chase, Flee, Panic }

        public NpcKind Kind { get; private set; } = NpcKind.Human;
        public Behaviour CurrentBehaviour { get; private set; } = Behaviour.Idle;
        public float Health { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;
        public float Temperature = 310f;
        public bool IsDead => Health <= 0f;
        public bool IsBurning { get; private set; }
        public float Speed = 2.2f;

        Rigidbody _rb;
        Transform _head, _torso, _armL, _armR, _legL, _legR;
        SandboxWorld _world;
        Vector3 _wanderTarget;
        float _wanderTimer, _thinkTimer, _burnTimer;
        CapsuleCollider _collider;
        MaterialPropertyBlock _mpb;
        readonly List<Renderer> _renderers = new List<Renderer>();
        Color _skin, _shirt, _pants;
        Vector3 _lastSafePos;

        public event Action<NpcActor> OnDied;

        public static NpcActor Create(Transform parent, Vector3 position, NpcKind kind, SandboxWorld world)
        {
            var go = new GameObject("NPC_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Npc");

            var npc = go.AddComponent<NpcActor>();
            npc.Kind = kind;
            npc._world = world;
            npc.Build(kind);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = kind switch { NpcKind.Heavy => 190f, NpcKind.Robot => 260f, NpcKind.Scientist => 65f, _ => 78f };
            rb.drag = 0.4f;
            rb.angularDrag = 0.6f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            npc._rb = rb;

            var col = go.AddComponent<CapsuleCollider>();
            col.height = kind == NpcKind.Heavy ? 2.2f : 1.85f;
            col.radius = kind == NpcKind.Heavy ? 0.55f : 0.35f;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
            npc._collider = col;

            npc.MaxHealth = kind switch { NpcKind.Heavy => 320f, NpcKind.Robot => 420f, NpcKind.Zombie => 160f, _ => 100f };
            npc.Health = npc.MaxHealth;
            npc._wanderTarget = position;
            npc._lastSafePos = position;
            return npc;
        }

        void Build(NpcKind kind)
        {
            _mpb = new MaterialPropertyBlock();
            switch (kind)
            {
                case NpcKind.Heavy: _skin = new Color(.72f, .55f, .42f); _shirt = new Color(.22f, .26f, .22f); _pants = new Color(.16f, .17f, .18f); break;
                case NpcKind.Scientist: _skin = new Color(.86f, .68f, .55f); _shirt = new Color(.92f, .93f, .95f); _pants = new Color(.35f, .38f, .45f); break;
                case NpcKind.Zombie: _skin = new Color(.45f, .6f, .38f); _shirt = new Color(.35f, .3f, .28f); _pants = new Color(.2f, .22f, .25f); break;
                case NpcKind.Robot: _skin = new Color(.55f, .6f, .68f); _shirt = new Color(.3f, .42f, .52f); _pants = new Color(.22f, .25f, .3f); break;
                default: _skin = new Color(.82f, .64f, .5f); _shirt = new Color(.28f, .42f, .62f); _pants = new Color(.2f, .22f, .28f); break;
            }

            float scale = kind == NpcKind.Heavy ? 1.3f : kind == NpcKind.Robot ? 1.35f : 1f;
            _torso = Part("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f) * scale, new Vector3(0.5f, 0.42f, 0.36f) * scale, _shirt, Look.Polymer);
            _head = Part("Head", PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0f) * scale, Vector3.one * 0.26f * scale, _skin, Look.Flesh);
            _armL = Part("ArmL", PrimitiveType.Capsule, new Vector3(-0.33f, 1.1f, 0f) * scale, new Vector3(0.14f, 0.36f, 0.14f) * scale, _shirt, Look.Polymer);
            _armR = Part("ArmR", PrimitiveType.Capsule, new Vector3(0.33f, 1.1f, 0f) * scale, new Vector3(0.14f, 0.36f, 0.14f) * scale, _shirt, Look.Polymer);
            _legL = Part("LegL", PrimitiveType.Capsule, new Vector3(-0.15f, 0.42f, 0f) * scale, new Vector3(0.16f, 0.42f, 0.16f) * scale, _pants, Look.Polymer);
            _legR = Part("LegR", PrimitiveType.Capsule, new Vector3(0.15f, 0.42f, 0f) * scale, new Vector3(0.16f, 0.42f, 0.16f) * scale, _pants, Look.Polymer);
            _armL.localRotation = Quaternion.Euler(0f, 0f, 12f);
            _armR.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }

        Transform Part(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color, Look look)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.layer = LayerMask.NameToLayer("Npc");
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = RuntimeVisuals.For(look, color, 0.65f, 0f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _renderers.Add(mr);
            return go.transform;
        }

        public void SetBehaviour(Behaviour b)
        {
            CurrentBehaviour = b;
            if (b == Behaviour.Wander) PickWander();
        }

        void PickWander()
        {
            _wanderTarget = new Vector3(
                UnityEngine.Random.Range(-WorldEnvironment.ArenaHalfSize * 0.78f, WorldEnvironment.ArenaHalfSize * 0.78f),
                transform.position.y,
                UnityEngine.Random.Range(-WorldEnvironment.ArenaHalfSize * 0.78f, WorldEnvironment.ArenaHalfSize * 0.78f));
            _wanderTimer = UnityEngine.Random.Range(3f, 7f);
        }

        Transform _barBg, _barFill;
        void EnsureHealthBar()
        {
            if (_barBg != null) return;
            var canvasGo = new GameObject("HealthBar", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(120f, 18f);
            rt.localPosition = new Vector3(0f, 2.35f, 0f);
            rt.localScale = Vector3.one * 0.012f;

            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(canvasGo.transform, false);
            UIFactory.SetAnchors((RectTransform)bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(bg.transform, false);
            UIFactory.SetAnchors((RectTransform)fill.transform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var fillImg = fill.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = Theme.Accent;
            fillImg.sprite = RuntimeVisuals.Rounded(32, 8);
            fillImg.type = UnityEngine.UI.Image.Type.Filled;
            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _barBg = bg.transform;
            _barFill = fill.transform;
        }

        void UpdateHealthBar()
        {
            EnsureHealthBar();
            if (_barFill == null) return;
            float ratio = Mathf.Clamp01(Health / Mathf.Max(1f, MaxHealth));
            var img = _barFill.GetComponent<UnityEngine.UI.Image>();
            img.fillAmount = ratio;
            img.color = ratio > 0.6f ? Theme.Accent : ratio > 0.3f ? Theme.Warn : Theme.Danger;
            _barBg.parent.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        }

        void Update()
        {
            UpdateHealthBar();
            if (IsDead) return;
            float dt = Time.deltaTime;

            // animation: limb swing based on horizontal speed
            float speed = _rb != null ? new Vector3(_rb.velocity.x, 0f, _rb.velocity.z).magnitude : 0f;
            float swing = Mathf.Sin(Time.time * 9f) * Mathf.Clamp01(speed * 0.4f) * 42f;
            if (_legL != null) _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-swing * 0.7f, 0f, 12f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(swing * 0.7f, 0f, -12f);
            if (_head != null) _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.7f) * 6f, 0f);

            if (IsBurning)
            {
                _burnTimer += dt;
                Health -= 9f * dt;
                Temperature = Mathf.Max(Temperature, 900f);
                if (Health <= 0f) Die();
                if (_burnTimer > 7f) Extinguish();
            }
            else
            {
                Temperature = Mathf.MoveTowards(Temperature, 310f, 12f * dt);
            }

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = 0.25f;
                Think();
            }

            if (transform.position.y < -20f) Die();
        }

        void Think()
        {
            if (_rb == null) return;
            Vector3 desired = Vector3.zero;
            switch (CurrentBehaviour)
            {
                case Behaviour.Wander:
                    _wanderTimer -= 0.25f;
                    if (_wanderTimer <= 0f || Vector3.Distance(transform.position, _wanderTarget) < 1.2f) PickWander();
                    desired = (new Vector3(_wanderTarget.x, transform.position.y, _wanderTarget.z) - transform.position);
                    break;
                case Behaviour.Follow:
                case Behaviour.Chase:
                    {
                        var target = NearestTarget();
                        if (target.HasValue) desired = target.Value - transform.position;
                        break;
                    }
                case Behaviour.Flee:
                case Behaviour.Panic:
                    {
                        var threat = NearestTarget();
                        if (threat.HasValue) desired = transform.position - threat.Value;
                        break;
                    }
            }

            if (desired.sqrMagnitude > 0.05f)
            {
                var dir = new Vector3(desired.x, 0f, desired.z).normalized;
                float moveSpeed = CurrentBehaviour is Behaviour.Flee or Behaviour.Panic ? Speed * 1.8f : Speed;
                if (IsBurning) moveSpeed *= 1.5f;
                var v = _rb.velocity;
                _rb.velocity = new Vector3(dir.x * moveSpeed, v.y, dir.z * moveSpeed);
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.25f);
            }
            else
            {
                var v = _rb.velocity;
                _rb.velocity = new Vector3(v.x * 0.6f, v.y, v.z * 0.6f);
            }
        }

        Vector3? NearestTarget()
        {
            Vector3? best = null;
            float bestDist = 28f;
            if (_world == null) return null;
            foreach (var o in _world.Objects)
            {
                if (o == null) continue;
                float d = Vector3.Distance(transform.position, o.transform.position);
                if (d < bestDist) { bestDist = d; best = o.transform.position; }
            }
            return best;
        }

        public void ApplyDamage(float amount, Vector3? source = null)
        {
            if (IsDead || amount <= 0f) return;
            Health -= amount;
            EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up, new Color(0.6f, 0.08f, 0.08f), 10, 2.4f);
            if (_rb != null && source.HasValue)
            {
                var dir = (transform.position - source.Value).normalized + Vector3.up * 0.3f;
                _rb.AddForce(dir * Mathf.Clamp(amount, 1f, 120f), ForceMode.Impulse);
            }
            if (CurrentBehaviour is Behaviour.Idle or Behaviour.Wander) SetBehaviour(Behaviour.Flee);
            if (Health <= 0f) Die();
        }

        public void Ignite()
        {
            if (IsBurning || IsDead) return;
            IsBurning = true;
            _burnTimer = 0f;
            EffectsSystem.SpawnFire(transform.position, Vector3.one, 1f, transform);
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
            if (IsDead && _collider == null) return;
            Health = 0f;
            EffectsSystem.DespawnFireFor(transform);
            // ragdoll: release rotation, push over
            if (_collider != null) { Destroy(_collider); _collider = null; }
            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.None;
                _rb.AddTorque(new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(1f, 3f)), ForceMode.Impulse);
                _rb.drag = 1.2f;
            }
            foreach (var id in new[] { "flesh" })
            {
                EffectsSystem.SpawnFluidSplash(transform.position + Vector3.up * 0.4f, new Color(0.45f, 0.03f, 0.03f), 45, 3.4f);
            }
            OnDied?.Invoke(this);
        }

        public void Remove() { Destroy(gameObject); }

        public string StatusText()
        {
            string state = IsDead ? "мёртв" : IsBurning ? "горит" : CurrentBehaviour switch
            {
                Behaviour.Wander => "патруль",
                Behaviour.Follow => "следует",
                Behaviour.Chase => "преследует",
                Behaviour.Flee => "убегает",
                Behaviour.Panic => "паника",
                _ => "ожидает"
            };
            return $"{Kind} | {state} | {Health:F0}/{MaxHealth:F0} HP | {Temperature:F0} K";
        }
    }
}
