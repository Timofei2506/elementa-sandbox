using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Ballistic projectile with real travel time, gravity drop and penetration resolution.
    /// Uses a raycast-per-step sweep so fast rounds never tunnel through thin plates.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public ProjectileStats Stats;
        public Vector3 Velocity;
        public float Lifetime = 4f;
        public float GravityScale = 0.35f;
        Vector3 _lastPos;
        float _age;
        int _bounces;
        TrailRenderer _trail;
        public event Action<Projectile, HitOutcome> OnHit;

        public static Projectile Launch(Vector3 origin, Vector3 direction, ProjectileStats stats, float spreadDeg = 0f, Transform parent = null)
        {
            var go = new GameObject("Projectile_" + stats.kind);
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.layer = LayerMask.NameToLayer("Projectile");
            var p = go.AddComponent<Projectile>();
            p.Stats = stats;

            if (spreadDeg > 0.01f)
            {
                var rot = Quaternion.Euler(
                    UnityEngine.Random.Range(-spreadDeg, spreadDeg),
                    UnityEngine.Random.Range(-spreadDeg, spreadDeg),
                    0f);
                direction = rot * direction;
            }
            p.Velocity = direction.normalized * stats.velocity;
            p._lastPos = origin;

            // visual: small glowing tracer
            var mesh = stats.caliberMM > 20f ? RuntimeVisuals.Sphere : RuntimeVisuals.Capsule;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            Color tracer = stats.kind switch
            {
                ProjectileKind.Laser => new Color(1f, 0.25f, 0.25f),
                ProjectileKind.Incendiary => new Color(1f, 0.6f, 0.15f),
                ProjectileKind.Explosive or ProjectileKind.ShapedCharge => new Color(0.9f, 0.85f, 0.6f),
                _ => new Color(1f, 0.9f, 0.55f),
            };
            mr.sharedMaterial = RuntimeVisuals.For(Look.Emissive, tracer, 0.2f, 0f, 2.5f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float scale = Mathf.Clamp(stats.caliberMM * 0.004f, 0.03f, 0.35f);
            go.transform.localScale = mesh == RuntimeVisuals.Capsule
                ? new Vector3(scale, scale * 2.4f, scale)
                : Vector3.one * scale * 2f;

            p._trail = go.AddComponent<TrailRenderer>();
            p._trail.time = stats.kind == ProjectileKind.Laser ? 0.12f : 0.055f;
            p._trail.startWidth = scale * 1.6f;
            p._trail.endWidth = 0f;
            p._trail.material = RuntimeVisuals.For(Look.Emissive, tracer, 0.3f, 0f, 3f);
            p._trail.numCapVertices = 2;
            p._trail.minVertexDistance = 0.05f;

            Destroy(go, p.Lifetime);
            return p;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _age += dt;
            if (_age > Lifetime) { Destroy(gameObject); return; }

            Velocity += Physics.gravity * GravityScale * dt;
            Vector3 next = _lastPos + Velocity * dt;
            Vector3 dir = next - _lastPos;
            float dist = dir.magnitude;
            if (dist > 0.0001f)
            {
                // sweep so we cannot tunnel through thin armour
                if (Physics.Raycast(_lastPos, dir / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Projectile>() == null)
                    {
                        HandleHit(hit);
                        return;
                    }
                }
            }
            transform.position = next;
            if (dist > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir / dist);
            _lastPos = next;

            if (_age > 2.5f && Stats.kind == ProjectileKind.Laser) { Destroy(gameObject); return; }
        }

        void HandleHit(RaycastHit hit)
        {
            float impactAngle = Vector3.Angle(-Velocity.normalized, hit.normal);
            impactAngle = Mathf.Abs(90f - impactAngle);
            var outcome = Game.I != null && Game.I.World != null
                ? Game.I.World.ResolveHit(Stats, hit, impactAngle)
                : new HitOutcome { result = HitResult.Penetrated };

            OnHit?.Invoke(this, outcome);
            EffectsSystem.SpawnSparks(hit.point, hit.normal, 6, 4f);

            if (Stats.kind == ProjectileKind.Laser)
            {
                var t = gameObject.GetComponent<TrailRenderer>();
                if (t != null) t.time = 0.2f;
                Destroy(gameObject, 0.05f);
                return;
            }

            if (outcome.result == HitResult.Penetrated)
            {
                // continue through with reduced velocity
                float ratio = Mathf.Clamp01(outcome.residualVelocity / Mathf.Max(1f, Stats.velocity));
                Velocity *= Mathf.Clamp(ratio, 0.15f, 0.95f);
                transform.position = hit.point + Velocity.normalized * 0.05f;
                _lastPos = transform.position;
                if (Velocity.magnitude < 40f) Destroy(gameObject);
                return;
            }

            if (outcome.result == HitResult.Ricochet && _bounces < 2)
            {
                _bounces++;
                Velocity = Vector3.Reflect(Velocity, hit.normal) * 0.55f;
                transform.position = hit.point + Velocity.normalized * 0.05f;
                _lastPos = transform.position;
                return;
            }

            Destroy(gameObject);
        }
    }
}
