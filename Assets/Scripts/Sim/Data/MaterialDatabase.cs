using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elementa.Sim
{
    [Serializable]
    public class MaterialDef
    {
        public string id, nameRu, nameEn, category;
        public float density;      // kg/m^3
        public float hardness;     // MPa - resistance to penetration
        public float strength;     // MPa - tensile / structural strength
        public float toughness;    // J - energy absorbed before fracture
        public float meltK;        // melting point
        public float heat;         // specific heat J/(kg*K)
        public float cond;         // thermal conductivity W/(m*K)
        public float flammability; // 0..1
        public float corrosion;    // 0..1 resistance
        public float elasticity;   // 0..1 (1 = very bouncy/soft)
        public Color color;
        public float roughness, metallic;

        public bool IsMetal => category == "metal";
        public bool IsOrganic => category is "organic" or "wood" or "polymer";
        public bool CanMelt => meltK > 200f;

        public override string ToString() => $"{nameRu} ({density:F0} kg/m3, HB {hardness:F0} MPa)";

        public static MaterialDef FromJson(Dictionary<string, object> o)
        {
            var m = new MaterialDef
            {
                id = Json.Str(o, "id"),
                nameRu = Json.Str(o, "ru"),
                nameEn = Json.Str(o, "en"),
                category = Json.Str(o, "cat"),
                density = (float)Json.Num(o, "density", 1000),
                hardness = (float)Json.Num(o, "hardness", 50),
                strength = (float)Json.Num(o, "strength", 20),
                toughness = (float)Json.Num(o, "toughness", 10),
                meltK = (float)Json.Num(o, "melt", 300),
                heat = (float)Json.Num(o, "heat", 1000),
                cond = (float)Json.Num(o, "cond", 1),
                flammability = (float)Json.Num(o, "flamm"),
                corrosion = (float)Json.Num(o, "corr", 0.5f),
                elasticity = (float)Json.Num(o, "elast", 0.1f),
                roughness = (float)Json.Num(o, "rough", 0.5f),
                metallic = (float)Json.Num(o, "metal", 0f),
            };
            m.color = ColorUtil.Hex(Json.Str(o, "color", "#8A8A92"));
            return m;
        }
    }

    /// <summary>Registry of every simulated material. Populated from Resources/ElementaData/materials.json.</summary>
    public class MaterialDatabase
    {
        readonly Dictionary<string, MaterialDef> _byId = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
        readonly List<MaterialDef> _all = new List<MaterialDef>();

        public IReadOnlyList<MaterialDef> All => _all;
        public int Count => _all.Count;

        public void Add(MaterialDef m)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) return;
            if (_byId.ContainsKey(m.id)) throw new InvalidOperationException("Duplicate material id: " + m.id);
            _byId[m.id] = m;
            _all.Add(m);
        }

        public MaterialDef Get(string id)
            => id != null && _byId.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException("Unknown material: " + id);

        public MaterialDef TryGet(string id)
            => id != null && _byId.TryGetValue(id, out var m) ? m : null;

        public bool Has(string id) => id != null && _byId.ContainsKey(id);

        public IEnumerable<MaterialDef> ByCategory(string cat)
        {
            foreach (var m in _all) if (m.category == cat) yield return m;
        }

        public void LoadFromJson(string json)
        {
            var root = Json.Obj(Json.Parse(json));
            foreach (var raw in Json.ArrOf(root, "materials"))
                Add(MaterialDef.FromJson(Json.Obj(raw)));
        }

        public static MaterialDatabase CreateDefault()
        {
            var db = new MaterialDatabase();
            db.Add(new MaterialDef { id = "steel_1045", nameRu = "Сталь", nameEn = "Steel", category = "metal",
                density = 7850, hardness = 1500, strength = 585, toughness = 40, meltK = 1793, heat = 486,
                cond = 50, flammability = 0, corrosion = .35f, elasticity = .3f, color = ColorUtil.Hex("#8A8A92"), roughness = .45f, metallic = 1 });
            return db;
        }
    }

    public static class ColorUtil
    {
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length < 6) return Color.magenta;
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            byte a = hex.Length >= 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        public static string ToHex(Color c)
            => $"#{ToByte(c.r):X2}{ToByte(c.g):X2}{ToByte(c.b):X2}";
        static byte ToByte(float v) => (byte)(Mathf.Clamp01(v) * 255f);

        /// <summary>0 = blackbody cold, 1 = white hot. Used for heat/temperature tinting.</summary>
        public static Color TemperatureTint(float kelvin, Color baseColor)
        {
            if (kelvin < 640f) return baseColor;
            float t = Mathf.Clamp01((kelvin - 640f) / (2200f - 640f));
            Color glow = t < 0.5f
                ? Color.Lerp(ColorUtil.Hex("#D34A1E"), ColorUtil.Hex("#FF9A2E"), t * 2f)
                : Color.Lerp(ColorUtil.Hex("#FF9A2E"), ColorUtil.Hex("#FFF3C4"), (t - 0.5f) * 2f);
            return Color.Lerp(baseColor, glow, Mathf.Clamp01(t * 1.35f));
        }
    }
}
