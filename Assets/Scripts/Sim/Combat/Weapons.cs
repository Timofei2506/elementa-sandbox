using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elementa.Sim
{
    [Serializable]
    public class WeaponDef
    {
        public string id, nameRu, category;
        public ProjectileKind kind = ProjectileKind.Bullet;
        public float massGrams = 8f;      // projectile mass
        public float velocity = 700f;     // muzzle velocity m/s
        public float caliberMM = 7.62f;
        public float rpm = 600f;
        public float accuracy = 0.95f;    // 0..1
        public float recoil = 1f;
        public bool automatic;
        public float magSize = 30f;

        public ProjectileStats Stats => ProjectileStats.Make(kind, massGrams, velocity, caliberMM);
        public float DistancePerSecond => velocity;

        public static WeaponDef FromJson(Dictionary<string, object> o) => new WeaponDef
        {
            id = Json.Str(o, "id"),
            nameRu = Json.Str(o, "ru"),
            category = Json.Str(o, "cat"),
            kind = Json.Str(o, "kind") switch
            {
                "ap" => ProjectileKind.AP,
                "incendiary" => ProjectileKind.Incendiary,
                "explosive" => ProjectileKind.Explosive,
                "shaped" => ProjectileKind.ShapedCharge,
                "frag" => ProjectileKind.Frag,
                "arrow" => ProjectileKind.Arrow,
                "laser" => ProjectileKind.Laser,
                _ => ProjectileKind.Bullet,
            },
            massGrams = (float)Json.Num(o, "mass", 8),
            velocity = (float)Json.Num(o, "velocity", 700),
            caliberMM = (float)Json.Num(o, "caliber", 7.62f),
            rpm = (float)Json.Num(o, "rpm", 600),
            accuracy = (float)Json.Num(o, "accuracy", 0.95f),
            recoil = (float)Json.Num(o, "recoil", 1),
            automatic = Json.Bool(o, "auto"),
            magSize = (float)Json.Num(o, "mag", 30),
        };
    }

    public class WeaponDatabase
    {
        readonly Dictionary<string, WeaponDef> _byId = new Dictionary<string, WeaponDef>(StringComparer.OrdinalIgnoreCase);
        readonly List<WeaponDef> _all = new List<WeaponDef>();
        public IReadOnlyList<WeaponDef> All => _all;
        public int Count => _all.Count;
        public void Add(WeaponDef w) { if (w == null || string.IsNullOrEmpty(w.id)) return; _byId[w.id] = w; _all.Add(w); }
        public WeaponDef Get(string id) => TryGet(id) ?? throw new KeyNotFoundException("Unknown weapon: " + id);
        public WeaponDef TryGet(string id) => id != null && _byId.TryGetValue(id, out var w) ? w : null;
        public void LoadFromJson(string json)
        {
            var root = Json.Obj(Json.Parse(json));
            foreach (var raw in Json.ArrOf(root, "weapons")) Add(WeaponDef.FromJson(Json.Obj(raw)));
        }
    }
}
