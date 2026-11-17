using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Elementa.Sim
{
    /// <summary>
    /// A volume of liquid/gas inside a container: the heart of the chemistry sandbox.
    /// Tracks per-substance amounts in moles, temperature and pressure.
    /// </summary>
    public class Mixture
    {
        readonly Dictionary<string, float> _moles = new Dictionary<string, float>();
        public SubstanceDatabase Substances { get; }
        public string ContainerId { get; internal set; }

        public float Temperature = 293.15f;  // K
        public float AmbientK = 293.15f;
        public float Volume = 0.02f;         // m^3
        public float MaxVolume = 0.05f;
        public bool Pressurized;
        public bool Sparked;

        public event Action<string> OnSubstanceAdded;

        public Mixture(SubstanceDatabase db, float volume = 0.02f)
        {
            Substances = db;
            Volume = volume;
            MaxVolume = volume;
        }

        public IReadOnlyDictionary<string, float> Contents => _moles;
        public int Count => _moles.Count;
        public bool IsEmpty => _moles.Count == 0;
        public float TotalMoles => _moles.Values.Sum();

        public bool Has(string id) => id != null && _moles.TryGetValue(id, out var m) && m > 1e-5f;
        public float Moles(string id) => id != null && _moles.TryGetValue(id, out var m) ? m : 0f;

        public void AddMoles(string id, float moles)
        {
            if (moles <= 0f || !Substances.Has(id)) return;
            _moles.TryGetValue(id, out var cur);
            _moles[id] = cur + moles;
            OnSubstanceAdded?.Invoke(id);
        }

        public bool RemoveMoles(string id, float moles)
        {
            if (!_moles.TryGetValue(id, out var cur)) return false;
            float left = cur - moles;
            if (left <= 1e-6f) _moles.Remove(id);
            else _moles[id] = left;
            return true;
        }

        public void Clear() => _moles.Clear();

        /// <summary>Adds a liquid volume in millilitres. Returns false if the container overflows.</summary>
        public bool AddVolumeML(string id, float ml)
        {
            var s = Substances.TryGet(id);
            if (s == null) return false;
            float moles = (ml / 1000f) * s.density / s.molarMass;
            if (Mass() * 0.001f / 1000f + ml > MaxVolume * 1e6f) return false;
            AddMoles(id, moles);
            return true;
        }

        public float Mass()
        {
            float kg = 0f;
            foreach (var kv in _moles)
            {
                var s = Substances.TryGet(kv.Key);
                if (s != null) kg += kv.Value * s.molarMass / 1000f;
            }
            return kg;
        }

        public float TotalVolumeL => Mass() / EffectiveDensity() * 1000f;
        float EffectiveDensity()
        {
            float mass = 0f, vol = 0f;
            foreach (var kv in _moles)
            {
                var s = Substances.TryGet(kv.Key);
                if (s == null) continue;
                float m = kv.Value * s.molarMass / 1000f;
                mass += m;
                vol += m / Mathf.Max(1f, s.density);
            }
            return vol > 1e-9f ? mass / vol : 1000f;
        }

        public float PH
        {
            get
            {
                if (IsEmpty) return 7f;
                float acid = 0f, baseStrength = 0f, neut = 0f;
                foreach (var kv in _moles)
                {
                    var s = Substances.TryGet(kv.Key);
                    if (s == null) continue;
                    if (s.ph < 6f) acid += kv.Value * (7f - s.ph);
                    else if (s.ph > 8f) baseStrength += kv.Value * (s.ph - 7f);
                    else neut += kv.Value;
                }
                float total = acid + baseStrength + neut;
                if (total <= 1e-6f) return 7f;
                if (acid >= baseStrength) return Mathf.Clamp(7f - 7f * acid / (acid + neut + 1e-6f), 0f, 7f);
                return Mathf.Clamp(7f + 7f * baseStrength / (baseStrength + neut + 1e-6f), 7f, 14f);
            }
        }

        public float Toxicity
        {
            get
            {
                float worst = 0f;
                foreach (var kv in _moles)
                {
                    var s = Substances.TryGet(kv.Key);
                    if (s != null) worst = Mathf.Max(worst, s.toxicity);
                }
                return worst;
            }
        }

        public float Corrosivity
        {
            get
            {
                float worst = 0f;
                foreach (var kv in _moles)
                {
                    var s = Substances.TryGet(kv.Key);
                    if (s != null) worst = Mathf.Max(worst, s.corrosivity);
                }
                return worst;
            }
        }

        public bool IsFlammable()
        {
            foreach (var kv in _moles)
            {
                var s = Substances.TryGet(kv.Key);
                if (s != null && s.flammable && kv.Value > 1e-5f) return true;
            }
            return false;
        }

        public bool HasOxidizer()
        {
            foreach (var kv in _moles)
            {
                var s = Substances.TryGet(kv.Key);
                if (s != null && s.oxidizer) return true;
            }
            return false;
        }

        /// <summary>Dominant substance by mass — drives the rendered liquid colour.</summary>
        public SubstanceDef Dominant()
        {
            SubstanceDef best = null;
            float bestMass = -1f;
            foreach (var kv in _moles)
            {
                var s = Substances.TryGet(kv.Key);
                if (s == null) continue;
                float m = kv.Value * s.molarMass;
                if (m > bestMass) { bestMass = m; best = s; }
            }
            return best;
        }

        /// <summary>Heat exchange with the environment (Newton cooling) plus evaporation.</summary>
        public void Tick(float dt)
        {
            float k = 0.06f;
            Temperature += (AmbientK - Temperature) * Mathf.Clamp01(k * dt);
            if (Temperature < 1f) Temperature = 1f;

            // boil off substances above their boiling point
            if (Temperature > 300f && !IsEmpty)
            {
                foreach (var id in _moles.Keys.ToList())
                {
                    var s = Substances.TryGet(id);
                    if (s == null || s.phase != Phase.Liquid) continue;
                    float bp = s.id switch
                    {
                        "water" => 373f,
                        "ethanol" => 351f,
                        "methanol" => 338f,
                        "acetone" => 329f,
                        "gasoline" => 400f,
                        "diesel" => 560f,
                        "kerosene" => 480f,
                        "glycerin" => 563f,
                        "sulfuric" => 610f,
                        "hcl" => 383f,
                        "nitric" => 356f,
                        "ammonia" => 240f,
                        "hydrogen_peroxide" => 423f,
                        "vinegar" => 391f,
                        "turpentine" => 434f,
                        "mercury" => 630f,
                        "liquid_oxygen" => 90f,
                        _ => 400f,
                    };
                    if (Temperature > bp + 20f)
                    {
                        float boil = (Temperature - bp) * 0.0006f * dt;
                        RemoveMoles(id, boil);
                    }
                }
            }

            // pressure build-up in a closed container
            if (Pressurized && TotalMoles > 1e-4f)
            {
                float ideal = TotalMoles * 8.314f * Temperature / Mathf.Max(0.001f, Volume);
                if (ideal > 800_000f)
                    Temperature += Mathf.Min(120f, (ideal - 800_000f) * 0.00002f * dt);
            }
        }

        public Mixture Clone()
        {
            var m = new Mixture(Substances, Volume) { Temperature = Temperature, AmbientK = AmbientK, Volume = Volume, MaxVolume = MaxVolume, Pressurized = Pressurized, ContainerId = ContainerId };
            foreach (var kv in _moles) m._moles[kv.Key] = kv.Value;
            return m;
        }

        public string Describe()
        {
            if (IsEmpty) return "пусто";
            var parts = new List<string>();
            foreach (var kv in _moles.OrderByDescending(k => k.Value).Take(4))
            {
                var s = Substances.TryGet(kv.Key);
                parts.Add(s != null ? $"{s.nameRu} {FractionOf(kv.Key) * 100f:F0}%" : kv.Key);
            }
            return string.Join(", ", parts);
        }

        public float FractionOf(string id)
        {
            float total = TotalMoles;
            return total <= 1e-9f ? 0f : Moles(id) / total;
        }
    }
}
