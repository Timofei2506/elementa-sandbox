using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    public enum BodyShape { Box, Sphere, Cylinder, Capsule, Plate, Ragdoll }

    /// <summary>
    /// A physical object in the sandbox: crate, wall, barrel, ragdoll body part.
    /// Tracks material, health, temperature and burning state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SandboxObject : MonoBehaviour
    {
        public MaterialDef Material { get; private set; }
        public BodyShape Shape { get; private set; }
        public float Mass => _rb != null ? _rb.mass : 1f;
        public float Health { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;
        public float Temperature { get; set; } = 293f;
        public float Damage => 1f - Mathf.Clamp01(Health / Mathf.Max(1f, MaxHealth));
        public bool IsBurning { get; private set; }
        public bool IsDestroyed => Health <= 0f;
        public float SpawnTime { get; private set; }

        Rigidbody _rb;
        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        Vector3 _baseScale = Vector3.one;
        MeshRenderer _bodyRenderer;
        float _burnTimer, _burnIntensity;
        bool _frozen;

        public event Action<SandboxObject> OnDestroyed;

        public Rigidbody Body => _rb;

        public static SandboxObject Create(Transform parent, MaterialDef mat, BodyShape shape, Vector3 size, Vector3 position, float rotationDeg = 0f)
        {
            var go = new GameObject($"Obj_{mat.id}_{shape}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            // 2D: вращение только вокруг Z — объект остаётся в плоскости экрана
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
            go.layer = LayerMask.NameToLayer("Sandbox");

            Mesh mesh = shape switch
            {
                BodyShape.Sphere => RuntimeVisuals.Sphere,
                BodyShape.Cylinder => RuntimeVisuals.Cylinder,
                BodyShape.Capsule => RuntimeVisuals.Capsule,
                BodyShape.Plate => RuntimeVisuals.Box,
                _ => RuntimeVisuals.Box,
            };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RuntimeVisuals.ForDef(mat);

            // Collider абстрактный: AddComponent<Collider>() вернул бы null и следующий
            // col.material уронил бы всю стартовую сцену. Создаём конкретный тип.
            Collider col;
            switch (shape)
            {
                case BodyShape.Sphere:
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = 0.5f;
                    col = sc;
                    break;
                case BodyShape.Capsule:
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.radius = 0.5f; cc.height = 2f; cc.direction = 1;
                    col = cc;
                    break;
                case BodyShape.Cylinder:
                    var cy = go.AddComponent<CapsuleCollider>(); // вид сбоку: цилиндр читается как капсула
                    cy.radius = 0.5f; cy.height = 2f; cy.direction = 1;
                    col = cy;
                    break;
                default:
                    col = go.AddComponent<BoxCollider>();
                    break;
            }
            // Cylinder/capsule primitives are 2 units tall: scale accordingly.
            Vector3 scale = shape is BodyShape.Cylinder or BodyShape.Capsule
                ? new Vector3(size.x, size.y * 0.5f, size.z)
                : size;
            go.transform.localScale = scale;

            var rb = go.AddComponent<Rigidbody>();
            // ---- ключевое для 2D-вида: движение только в плоскости X/Y ----
            rb.constraints = RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY;
            float volume = shape switch
            {
                BodyShape.Sphere => 0.5236f * size.x * size.y * size.z,
                BodyShape.Cylinder => 0.7854f * size.x * size.y * size.z,
                BodyShape.Capsule => 0.6f * size.x * size.y * size.z,
                _ => size.x * size.y * size.z,
            };
            rb.mass = Mathf.Max(0.05f, mat.density * volume * 0.001f * 1000f * 0.001f);
            PhysicsCompat.SetLinearDamping(rb, 0.05f);
            PhysicsCompat.SetAngularDamping(rb, 0.1f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 3f;
            // soft materials bounce, rigid ones thud
            PhysicsCompat.SetLinearDamping(rb, Mathf.Lerp(0.02f, 0.3f, mat.elasticity));
            // Unity 6 переименовала PhysicMaterial в PhysicsMaterial; старый тип
            // помечен obsolete-ошибкой (CS0619), поэтому выбор по версии движка.
#if UNITY_6000_0_OR_NEWER
            var pm = new PhysicsMaterial
#else
            var pm = new PhysicMaterial
#endif
            {
                bounciness = Mathf.Clamp01(mat.elasticity * 0.8f),
                dynamicFriction = Mathf.Lerp(0.85f, 0.4f, mat.elasticity),
                staticFriction = Mathf.Lerp(0.9f, 0.5f, mat.elasticity),
            };
            if (col != null) col.material = pm;

            var obj = go.AddComponent<SandboxObject>();
            obj.Material = mat;
            obj.Shape = shape;
            obj._rb = rb;
            obj._renderer = mr;
            obj._baseScale = scale;
            obj._spawnRotation = rotationDeg;
            obj.MaxHealth = Mathf.Clamp(mat.strength * 0.6f + mat.toughness * 2f, 40f, 900f);
            obj.Health = obj.MaxHealth;
            obj.SpawnTime = Time.time;
            return obj;
        }

        float _spawnRotation;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            RefreshVisual();
        }

        public void ApplyDamage(float amount, float heatJoules = 0f)
        {
            if (amount <= 0f && heatJoules <= 0f) return;
            Health -= amount;
            if (heatJoules > 0f)
            {
                float deltaK = heatJoules / Mathf.Max(1f, Mass * Material.heat);
                Temperature += deltaK;
            }
            if (Temperature > Material.meltK + 120f)
                Health -= (Temperature - Material.meltK) * 0.05f;

            if (Material.flammability > 0.2f && Temperature > 420f && !IsBurning && Material.flammability > 0.3f)
                Ignite();

            RefreshVisual();
            if (Health <= 0f)
            {
                Health = 0f;
                OnDestroyed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        public void AddHeat(float joules)
        {
            Temperature += joules / Mathf.Max(1f, Mass * Material.heat);
            if (Material.flammability > 0.25f && Temperature > 500f && !IsBurning) Ignite();
            RefreshVisual();
        }

        public void Ignite()
        {
            if (IsBurning) return;
            IsBurning = true;
            _burnTimer = 0f;
            _burnIntensity = Mathf.Clamp01(Material.flammability);
            EffectsSystem.SpawnFire(transform.position, transform.localScale, _burnIntensity, transform);
        }

        public void Extinguish()
        {
            if (!IsBurning) return;
            IsBurning = false;
            EffectsSystem.DespawnFire(this);
        }

        public void Freeze(bool state)
        {
            _frozen = state;
            if (_rb != null) _rb.isKinematic = state;
        }

        void FixedUpdate()
        {
            if (IsBurning)
            {
                _burnTimer += Time.fixedDeltaTime;
                float burnRate = Material.flammability * 4.5f;
                Health -= burnRate * Time.fixedDeltaTime;
                Temperature = Mathf.Max(Temperature, 780f);
                if (Health <= 0f)
                {
                    Health = 0f;
                    OnDestroyed?.Invoke(this);
                    Destroy(gameObject);
                    return;
                }
                if (Temperature < 480f || Material.flammability < 0.2f) Extinguish();
            }
            else if (Temperature > 293f)
            {
                Temperature = Mathf.MoveTowards(Temperature, 293f, 18f * Time.fixedDeltaTime);
                if (Time.frameCount % 12 == 0) RefreshVisual();
            }

            // stay inside the arena
            if (transform.position.y < -30f) Destroy(gameObject);
        }

        public void RefreshVisual()
        {
            if (_renderer == null || Material == null) return;
            _renderer.GetPropertyBlock(_mpb);
            Color tint = Material.color;
            if (Damage > 0.05f) tint = Color.Lerp(tint, new Color(0.1f, 0.09f, 0.09f), Damage * 0.75f);
            tint = ColorUtil.TemperatureTint(Temperature, tint);
            if (IsBurning) tint = Color.Lerp(tint, new Color(1f, 0.45f, 0.12f), 0.55f + 0.2f * Mathf.Sin(Time.time * 12f));
            _mpb.SetColor("_Color", tint);
            _mpb.SetColor("_BaseColor", tint);
            if (Temperature > 850f)
            {
                _mpb.SetColor("_EmissionColor", ColorUtil.TemperatureTint(Temperature, Color.black) * 2f);
            }
            _renderer.SetPropertyBlock(_mpb);
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 2.5f) return;
            var other = collision.collider.GetComponentInParent<SandboxObject>();
            AudioSystem.I?.ImpactFor(Material, Mathf.Clamp01(speed / 12f));
            if (speed > 7f)
            {
                float damage = (speed - 6f) * Mass * 0.35f;
                if (other != null) other.ApplyDamage(damage);
                ApplyDamage(damage * 0.5f);
                EffectsSystem.SpawnSparks(collision.GetContact(0).point, Vector3.up, 4, 2.5f);
                ScreenShake.Impact(Mathf.Clamp01(speed / 30f));
            }
        }

        public Vector3 WorldSize => Vector3.Scale(_baseScale, new Vector3(1f, 1f, 1f));

        /// <summary>Restores health/temperature from a save file.</summary>
        public void SetState(float health, float temperature)
        {
            MaxHealth = Mathf.Max(MaxHealth, health);
            Health = Mathf.Clamp(health, 1f, MaxHealth);
            Temperature = Mathf.Clamp(temperature, 1f, 4000f);
            RefreshVisual();
        }
    }
}
