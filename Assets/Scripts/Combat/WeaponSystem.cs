using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Fires weapon definitions from the sandbox: aiming, spread, recoil, automatic fire.
    /// Player input and NPC AI both go through this API.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        public WeaponDef Current { get; private set; }
        public int Ammo { get; private set; }
        float _cooldown;
        public float LastShotTime { get; private set; }
        public event Action<Projectile> OnShotFired;

        public static WeaponSystem Create(Transform parent)
        {
            var go = new GameObject("WeaponSystem");
            go.transform.SetParent(parent, false);
            return go.AddComponent<WeaponSystem>();
        }

        public void Select(string weaponId)
        {
            var w = Game.I?.Weapons?.TryGet(weaponId);
            if (w == null) return;
            Current = w;
            Ammo = Mathf.RoundToInt(w.magSize);
            _cooldown = 0f;
        }

        public void Refill()
        {
            if (Current != null) Ammo = Mathf.RoundToInt(Current.magSize);
        }

        public bool CanFire => Current != null && _cooldown <= 0f && Ammo > 0;

        /// <summary>Fires one round. Returns the projectile or null when the shot is not possible.</summary>
        public Projectile Fire(Vector3 origin, Vector3 direction, float spreadBonusDeg = 0f)
        {
            if (!CanFire) return null;
            float spread = (1f - Mathf.Clamp01(Current.accuracy)) * 9f + spreadBonusDeg;
            var p = Projectile.Launch(origin, direction, Current.Stats, spread, null);
            Ammo--;
            _cooldown = 60f / Mathf.Max(20f, Current.rpm);
            LastShotTime = Time.time;
            OnShotFired?.Invoke(p);

            if (Current.kind == ProjectileKind.Incendiary)
                EffectsSystem.SpawnSparks(origin, direction, 4, 6f);
            if (Game.I?.CameraRig != null)
                ScreenShake.Impact(Current.recoil * 0.35f);
            return p;
        }

        void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        }

        /// <summary>Weapon stats summary for the HUD.</summary>
        public string Describe()
        {
            if (Current == null) return "";
            var s = Current.Stats;
            return $"{Current.nameRu}\n• {s.energyJ:F0} Дж | {Current.velocity:F0} м/с\n• Пробитие ~{s.penetrationMM:F0} мм RHA\n• {Ammo}/{Current.magSize:F0} патронов";
        }
    }
}
