using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Turns destroyed objects into physical debris chunks (glass shards, concrete rubble,
    /// metal fragments) and shatters fragile plates under gunfire.
    /// </summary>
    public static class DebrisSystem
    {
        public static void Shatter(SandboxObject obj, Vector3 impactPoint, ProjectileStats stats)
        {
            if (obj == null) return;
            var mat = obj.Material;
            var center = obj.transform.position;
            int pieces = Mathf.Clamp((int)(stats.energyJ / 90f) + 3, 3, mat.category == "ceramic" ? 24 : 12);
            for (int i = 0; i < pieces; i++)
            {
                var chunk = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                chunk.name = "Debris";
                // 2D: осколки разлетаются в плоскости экрана
                var off = Random.insideUnitCircle * 0.35f;
                chunk.transform.position = center + new Vector3(off.x, off.y, 0f);
                float s = UnityEngine.Random.Range(0.06f, 0.22f) * Mathf.Clamp(obj.transform.localScale.magnitude * 0.4f, 0.5f, 2f);
                chunk.transform.localScale = new Vector3(s, s * UnityEngine.Random.Range(0.5f, 1.4f), s);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = RuntimeVisuals.ForDef(mat);
                var rb = chunk.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezePositionZ
                               | RigidbodyConstraints.FreezeRotationX
                               | RigidbodyConstraints.FreezeRotationY;
                rb.mass = Mathf.Max(0.02f, mat.density * s * s * s * 0.5f);
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                var flat = chunk.transform.position - impactPoint;
                flat.z = 0f;
                var dir = flat.normalized + Vector3.up * 0.5f;
                rb.AddForce(dir * UnityEngine.Random.Range(1.5f, 5.5f), ForceMode.Impulse);
                rb.AddTorque(new Vector3(0f, 0f, Random.Range(-0.6f, 0.6f)), ForceMode.Impulse);
                Object.Destroy(chunk, UnityEngine.Random.Range(6f, 12f));
            }
            EffectsSystem.SpawnSmoke(center, 0.9f, 1.2f, 0.7f);
        }

        /// <summary>Should this material break apart rather than just take damage?</summary>
        public static bool IsFragile(MaterialDef m)
            => m != null && (m.category is "ceramic" || m.toughness < 6f && m.hardness > 300f);
    }
}
