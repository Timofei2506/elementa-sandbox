using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elementa.Sim
{
    public enum Phase { Solid, Liquid, Gas }

    [Serializable]
    public class SubstanceDef
    {
        public string id, nameRu, nameEn, formula;
        public Phase phase;
        public float density;        // kg/m^3
        public Color color;
        public float ph;             // 7 = neutral
        public bool soluble;
        public bool flammable;
        public bool oxidizer;
        public float ignitionK;      // 0 = not flammable
        public float toxicity;       // 0..1
        public float corrosivity;    // 0..1
        public float molarMass;
        public float energyMJ;       // energy density MJ/kg
        public float boilingK = 373f;
        public float freezingK = 273f;

        public bool IsAcid => ph < 6f;
        public bool IsBase => ph > 8f;
        public bool IsGas => phase == Phase.Gas;

        public static SubstanceDef FromJson(Dictionary<string, object> o)
        {
            var s = new SubstanceDef
            {
                id = Json.Str(o, "id"),
                nameRu = Json.Str(o, "ru"),
                nameEn = Json.Str(o, "en"),
                formula = Json.Str(o, "formula"),
                density = (float)Json.Num(o, "density", 1000),
                ph = (float)Json.Num(o, "ph", 7),
                soluble = Json.Bool(o, "soluble"),
                flammable = Json.Bool(o, "flammable"),
                oxidizer = Json.Bool(o, "oxidizer"),
                ignitionK = (float)Json.Num(o, "ignition", 0),
                toxicity = (float)Json.Num(o, "toxicity"),
                corrosivity = (float)Json.Num(o, "corrosivity"),
                molarMass = (float)Json.Num(o, "molar", 18),
                energyMJ = (float)Json.Num(o, "energy", 0),
                color = ColorUtil.Hex(Json.Str(o, "color", "#8AB4FF")),
            };
            s.phase = Json.Str(o, "state", "liquid") switch
            {
                "solid" => Phase.Solid,
                "gas" => Phase.Gas,
                _ => Phase.Liquid,
            };
            s.boilingK = s.phase == Phase.Gas ? 100f : 373f;
            s.freezingK = s.phase == Phase.Solid ? 400f : 273f;
            return s;
        }
    }

    /// <summary>Every chemical substance: water, acids, explosives, fuels, oxidizers.</summary>
    public class SubstanceDatabase
    {
        readonly Dictionary<string, SubstanceDef> _byId = new Dictionary<string, SubstanceDef>(StringComparer.OrdinalIgnoreCase);
        readonly List<SubstanceDef> _all = new List<SubstanceDef>();
        public IReadOnlyList<SubstanceDef> All => _all;
        public int Count => _all.Count;

        public void Add(SubstanceDef s)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) return;
            _byId[s.id] = s;
            _all.Add(s);
        }

        public SubstanceDef Get(string id) => TryGet(id) ?? throw new KeyNotFoundException("Unknown substance: " + id);
        public SubstanceDef TryGet(string id) => id != null && _byId.TryGetValue(id, out var s) ? s : null;
        public bool Has(string id) => id != null && _byId.ContainsKey(id);

        public void LoadFromJson(string json)
        {
            var root = Json.Obj(Json.Parse(json));
            foreach (var raw in Json.ArrOf(root, "substances")) Add(SubstanceDef.FromJson(Json.Obj(raw)));
        }
    }
}
