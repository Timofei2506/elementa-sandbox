using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Elementa.Sim
{
    public class ReactionEffects
    {
        public float explosion;   // relative blast power (0 = none)
        public float fire;        // fire intensity 0..1
        public float heat;        // extra heat visual scale
        public float light;       // emissive light intensity
        public float smoke;       // smoke amount
        public float shockwave;
        public float flash;
        public float fizz;        // bubbling
        public float corrode;
        public float colorShift;
        public bool unstable;
        public bool toxic;
        public bool molten;
        public bool sparks;
        public string gas;        // id of gas product to spawn as volume
        public string precipitate;

        public static ReactionEffects FromJson(Dictionary<string, object> o)
        {
            if (o == null) return new ReactionEffects();
            return new ReactionEffects
            {
                explosion = (float)Json.Num(o, "explosion"),
                fire = (float)Json.Num(o, "fire"),
                heat = (float)Json.Num(o, "heat"),
                light = (float)Json.Num(o, "light"),
                smoke = (float)Json.Num(o, "smoke"),
                shockwave = (float)Json.Num(o, "shockwave"),
                flash = (float)Json.Num(o, "flash"),
                fizz = (float)Json.Num(o, "fizz"),
                corrode = (float)Json.Num(o, "corrode"),
                colorShift = (float)Json.Num(o, "color_shift"),
                unstable = Json.Bool(o, "unstable"),
                toxic = Json.Bool(o, "toxic"),
                molten = Json.Bool(o, "molten"),
                sparks = Json.Bool(o, "sparks"),
                gas = Json.Str(o, "gas"),
                precipitate = Json.Str(o, "precipitate"),
            };
        }
    }

    [Serializable]
    public class ReactionDef
    {
        public string id, nameRu;
        public Dictionary<string, float> reactants = new Dictionary<string, float>();
        public Dictionary<string, float> products = new Dictionary<string, float>();
        public float deltaH;         // kJ per mol of reaction
        public float minTempK;       // activation temperature
        public string catalyst;
        public float rate;           // reaction speed multiplier
        public ReactionEffects effects = new ReactionEffects();

        /// <summary>Energy released (J) when <paramref name="moles"/> mol react. Negative = endothermic.</summary>
        public float Energy(float moles) => -deltaH * 1000f * moles;

        public bool NeedsCatalyst => !string.IsNullOrEmpty(catalyst);

        public static ReactionDef FromJson(Dictionary<string, object> o)
        {
            var r = new ReactionDef
            {
                id = Json.Str(o, "id"),
                nameRu = Json.Str(o, "ru"),
                deltaH = (float)Json.Num(o, "dh"),
                minTempK = (float)Json.Num(o, "temp", 273),
                catalyst = Json.Str(o, "catalyst"),
                rate = (float)Json.Num(o, "rate", 0.5),
                effects = ReactionEffects.FromJson(o.TryGetValue("effects", out var e) ? Json.Obj(e) : null),
            };
            foreach (var kv in Json.Obj(o["reactants"]))
                if (kv.Value is double d && d > 0) r.reactants[kv.Key] = (float)d;
            if (o.TryGetValue("products", out var p))
                foreach (var kv in Json.Obj(p))
                    r.products[kv.Key] = (float)Json.Num(Json.Obj(p), kv.Key);
            return r;
        }
    }

    /// <summary>
    /// Matches mixtures against the reaction database and computes the outcome.
    /// Pure logic: no Unity objects, fully unit-testable.
    /// </summary>
    public class ReactionEngine
    {
        readonly List<ReactionDef> _reactions = new List<ReactionDef>();
        public IReadOnlyList<ReactionDef> All => _reactions;
        public int Count => _reactions.Count;

        public void Add(ReactionDef r)
        {
            if (r != null && !string.IsNullOrEmpty(r.id)) _reactions.Add(r);
        }

        public void LoadFromJson(string json)
        {
            var root = Json.Obj(Json.Parse(json));
            foreach (var raw in Json.ArrOf(root, "reactions")) Add(ReactionDef.FromJson(Json.Obj(raw)));
        }

        public ReactionDef Find(string id) => _reactions.FirstOrDefault(r => r.id == id);

        /// <summary>
        /// Finds reactions whose every reactant is present in the container
        /// at a temperature above the activation threshold.
        /// </summary>
        public List<ReactionDef> FindReactions(Mixture mix)
        {
            var result = new List<ReactionDef>();
            if (mix == null || mix.IsEmpty) return result;
            foreach (var r in _reactions)
            {
                if (mix.Temperature < r.minTempK) continue;
                bool ok = true;
                foreach (var req in r.reactants)
                {
                    if (req.Value <= 0f) continue;
                    if (!mix.Has(req.Key)) { ok = false; break; }
                }
                if (!ok) continue;
                if (r.NeedsCatalyst && !mix.Has(r.catalyst)) continue;
                result.Add(r);
            }
            // most exothermic first: explosions happen before slow reactions
            result.Sort((a, b) => a.deltaH.CompareTo(b.deltaH));
            return result;
        }

        /// <summary>
        /// Runs one reaction as far as the available moles allow.
        /// Returns the amount of heat released (J) so the caller can warm the mixture.
        /// </summary>
        public float Apply(ReactionDef r, Mixture mix, float dt, out float extent)
        {
            extent = 0f;
            if (r == null || mix == null) return 0f;

            // limiting reactant -> reaction moles available
            float limiter = float.MaxValue;
            string limiterId = null;
            foreach (var req in r.reactants)
            {
                if (req.Value <= 0f) continue;
                float moles = mix.Moles(req.Key);
                float possible = moles / req.Value;
                if (possible < limiter) { limiter = possible; limiterId = req.Key; }
            }
            if (limiterId == null || limiter <= 1e-6f) return 0f;

            // reaction rate scales with surface contact: sqrt of amount, capped
            float drive = Mathf.Clamp(r.rate * dt * (1f + Mathf.Sqrt(limiter)), 0f, limiter * 10f);
            if (limiterId == "water" || limiterId == "gasoline" || limiterId == "kerosene") drive = Mathf.Min(drive, limiter * dt * 2f);
            extent = Mathf.Min(drive, limiter);
            if (extent <= 1e-6f) return 0f;

            foreach (var req in r.reactants)
                if (req.Value > 0f) mix.RemoveMoles(req.Key, req.Value * extent);

            foreach (var prod in r.products)
            {
                if (prod.Value <= 0f) continue;
                if (!mix.Substances.Has(prod.Key)) continue;  // product not a registered substance
                mix.AddMoles(prod.Key, prod.Value * extent);
            }

            return r.Energy(extent);
        }
    }
}
