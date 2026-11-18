using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elementa.Sim
{
    public enum ProjectileKind
    {
        Bullet,      // light rifle round
        AP,          // armour-piercing, tungsten/DU core
        Incendiary,  // burns on impact
        Explosive,   // HE filler
        ShapedCharge,// HEAT jet
        Frag,        // fragmentation / shrapnel
        Arrow,       // low velocity
        Laser,       // pure energy, no mass
        Blunt,       // hammer, bat, crowbar: no penetration, huge impulse
        Blade,       // knife, sword: cuts and dismembers
        Pierce,      // spear, pickaxe: deep narrow wound
        Shock,       // electricity: stun + burns
        Freeze,      // cryo: chilling, embrittlement
        Poison,      // toxin delivery
        Flash,       // flashbang: blinding + concussion
        Smoke,       // smoke screen, no damage
    }

    public enum HitResult
    {
        Stopped,      // plate held
        Penetrated,   // round passed through
        Partial,      // cracked, spalling, no exit
        Ricochet,     // deflected
        Ignored,      // too weak to matter
    }

    public struct ProjectileStats
    {
        public ProjectileKind kind;
        public float massGrams;
        public float velocity;     // m/s
        public float caliberMM;
        public float energyJ;      // kinetic energy
        public float penetrationMM;// armour equivalent in RHA
        public float blastPower;   // grams TNT equivalent

        public static ProjectileStats Make(ProjectileKind kind, float massGrams, float velocity, float caliberMM)
        {
            float ke = 0.5f * (massGrams / 1000f) * velocity * velocity;
            // armour equivalent scales with KE^0.5 and caliber; calibrated so a 7.62x39 API ~ 10mm RHA
            // HEAT jets are not kinetic-energy driven: penetration grows with charge diameter.
            float pen = kind == ProjectileKind.ShapedCharge
                ? caliberMM * 5.5f
                : Mathf.Sqrt(ke) * 0.030f * Mathf.Sqrt(Mathf.Max(4f, caliberMM)) * KindMultiplier(kind);
            return new ProjectileStats
            {
                kind = kind, massGrams = massGrams, velocity = velocity, caliberMM = caliberMM,
                energyJ = ke, penetrationMM = pen,
                blastPower = kind is ProjectileKind.Explosive or ProjectileKind.ShapedCharge ? massGrams * 0.08f : 0f,
            };
        }

        static float KindMultiplier(ProjectileKind k) => k switch
        {
            ProjectileKind.AP => 1.45f,
            ProjectileKind.Incendiary => 0.85f,
            ProjectileKind.Explosive => 0.9f,
            ProjectileKind.ShapedCharge => 1.0f,
            ProjectileKind.Frag => 0.35f,
            ProjectileKind.Arrow => 0.5f,
            ProjectileKind.Laser => 1.2f,
            ProjectileKind.Blunt => 0.05f,
            ProjectileKind.Blade => 0.7f,
            ProjectileKind.Pierce => 1.1f,
            ProjectileKind.Shock => 0.05f,
            ProjectileKind.Freeze => 0.05f,
            ProjectileKind.Poison => 0.15f,
            ProjectileKind.Flash => 0f,
            ProjectileKind.Smoke => 0f,
            _ => 1f,
        };
    }

    public struct ArmorLayer
    {
        public MaterialDef material;
        public float thicknessMM;
        public float angleDeg;      // 0 = perpendicular, 60 = heavily sloped

        public ArmorLayer(MaterialDef m, float thicknessMM, float angleDeg = 0f)
        {
            material = m; this.thicknessMM = thicknessMM; this.angleDeg = angleDeg;
        }

        /// <summary>Line-of-sight thickness grows with slope (cosine rule).</summary>
        public float EffectiveThickness => thicknessMM / Mathf.Max(0.12f, Mathf.Cos(angleDeg * Mathf.Deg2Rad));

        /// <summary>RHA-equivalent using hardness: a harder plate resists more per millimetre.</summary>
        public float RHAEquivalent
        {
            get
            {
                if (material == null) return thicknessMM;
                float hardnessFactor = Mathf.Pow(Mathf.Max(0.05f, material.hardness / 1500f), 0.45f);
                float elasticPenalty = Mathf.Lerp(1.25f, 0.9f, material.elasticity);
                return EffectiveThickness * hardnessFactor * elasticPenalty;
            }
        }
    }

    public struct HitOutcome
    {
        public HitResult result;
        public float penetratedMM;
        public float residualVelocity;
        public float residualEnergyJ;
        public float spallMassGrams;
        public float trauma;        // 0..1 damage dealt to whatever is behind
        public float heatJ;         // incendiary/energy transfer
        public float blastGramsTNT;
        public string descriptionRu;
    }

    /// <summary>
    /// Layered armour penetration model. Combines:
    ///  - projectile penetration (RHA-equivalent) vs summed layer resistance
    ///  - slope effects and ricochet for oblique impacts
    ///  - spall / behind-armour debris for non-penetrating hits
    ///  - blast and thermal transfer
    /// Deterministic and engine independent.
    /// </summary>
    public static class ArmorModel
    {
        const float RicochetAngle = 68f;

        public static HitOutcome Resolve(in ProjectileStats p, IReadOnlyList<ArmorLayer> layers, float impactAngleDeg = 0f)
        {
            var outcome = new HitOutcome { result = HitResult.Stopped };

            if (layers == null || layers.Count == 0)
            {
                outcome.result = HitResult.Penetrated;
                outcome.penetratedMM = p.penetrationMM;
                outcome.residualVelocity = p.velocity;
                outcome.residualEnergyJ = p.energyJ;
                outcome.trauma = Mathf.Clamp01(p.energyJ / 800f);
                outcome.descriptionRu = "Попадание без преграды";
                return outcome;
            }

            if (p.kind == ProjectileKind.Laser)
            {
                float absorbed = 0f, resist = 0f;
                foreach (var l in layers)
                {
                    resist += l.EffectiveThickness * (l.material != null ? Mathf.Lerp(0.4f, 1f, l.material.corrosion) : 0.6f);
                }
                absorbed = p.energyJ / 1000f;
                outcome.heatJ = p.energyJ;
                outcome.result = absorbed > resist * 3f ? HitResult.Partial : HitResult.Stopped;
                outcome.descriptionRu = "Лазер: прожиг поверхности";
                return outcome;
            }

            // Ricochet on extreme angles for rigid projectiles
            float totalAngle = impactAngleDeg;
            if (p.kind is ProjectileKind.Bullet or ProjectileKind.AP && totalAngle > RicochetAngle && p.caliberMM > 5f)
            {
                outcome.result = HitResult.Ricochet;
                outcome.residualEnergyJ = p.energyJ * 0.45f;
                outcome.residualVelocity = p.velocity * 0.6f;
                outcome.trauma = 0.02f;
                outcome.descriptionRu = "Рикошет";
                return outcome;
            }

            float pen = p.penetrationMM;
            float remaining = pen;
            float lastLayerT = 0f;
            int layerIndex = 0;
            bool penetratedAll = true;

            // angled impacts on the first layer increase effective resistance
            float slopeLoss = 1f - Mathf.Clamp01(impactAngleDeg / 90f) * 0.35f;
            remaining *= slopeLoss;

            foreach (var l in layers)
            {
                float rha = l.RHAEquivalent;
                lastLayerT = rha;
                if (remaining <= 0f) { penetratedAll = false; break; }
                if (remaining >= rha)
                {
                    remaining -= rha;
                    layerIndex++;
                }
                else
                {
                    penetratedAll = false;
                    outcome.penetratedMM += remaining;
                    remaining = 0f;
                    break;
                }
            }

            float energyRatio = Mathf.Clamp01(remaining / Mathf.Max(1f, pen));

            if (penetratedAll && (remaining > 0f || pen >= TotalRHA(layers)))
            {
                outcome.result = HitResult.Penetrated;
                outcome.penetratedMM = pen;
                outcome.residualEnergyJ = p.energyJ * Mathf.Clamp(energyRatio, 0.05f, 1f);
                outcome.residualVelocity = Mathf.Sqrt(2f * outcome.residualEnergyJ / Mathf.Max(0.001f, p.massGrams / 1000f));
                outcome.trauma = Mathf.Clamp01(outcome.residualEnergyJ / 400f);
                outcome.spallMassGrams = p.massGrams * 0.35f;
                outcome.descriptionRu = "Пробитие";
            }
            else
            {
                float ratio = pen / Mathf.Max(1f, TotalRHA(layers));
                outcome.penetratedMM = Mathf.Max(0f, pen - remaining);
                outcome.residualEnergyJ = 0f;
                outcome.residualVelocity = 0f;
                outcome.spallMassGrams = p.massGrams * Mathf.Clamp01(0.8f - ratio) * 1.4f;
                outcome.trauma = Mathf.Clamp01(ratio * 0.45f) * (1f + outcome.spallMassGrams / 20f) * 0.25f;
                outcome.result = ratio > 0.5f ? HitResult.Partial : HitResult.Stopped;
                outcome.descriptionRu = ratio > 0.5f ? "Частичное пробитие, осколки заброневого" : "Броня выдержала";
            }

            // thermal / blast side effects regardless of penetration
            if (p.kind == ProjectileKind.Incendiary) outcome.heatJ = p.energyJ * 0.35f;
            if (p.kind == ProjectileKind.Explosive) outcome.blastGramsTNT = p.blastPower;
            if (p.kind == ProjectileKind.ShapedCharge) outcome.blastGramsTNT = p.blastPower * 0.6f;

            return outcome;
        }

        public static float TotalRHA(IReadOnlyList<ArmorLayer> layers)
        {
            float t = 0f;
            if (layers == null) return 0f;
            foreach (var l in layers) t += l.RHAEquivalent;
            return t;
        }

        /// <summary>
        /// How much damage a blunt/penetrating hit does to a soft body behind the armour,
        /// factoring in energy, spall and the projectile kind.
        /// </summary>
        /// <param name="targetToughness">Material toughness in joule; flesh ~15, bone ~3.</param>
        public static float BodyDamage(in ProjectileStats p, in HitOutcome hit, float targetToughness = 15f)
        {
            float baseDamage = p.energyJ * 0.05f;
            if (p.kind == ProjectileKind.Explosive) baseDamage += p.blastPower * 12f;
            if (p.kind == ProjectileKind.ShapedCharge) baseDamage += p.blastPower * 6f;
            if (p.kind == ProjectileKind.Frag) baseDamage *= 0.55f;
            float spall = hit.spallMassGrams * 12f;
            // softer bodies take more damage: 15 J toughness == 1.0x, bone (3 J) == 2.2x
            float resist = Mathf.Max(0.35f, Mathf.Sqrt(Mathf.Max(0.5f, targetToughness) / 15f));
            return Mathf.Max(0f, (baseDamage * hit.trauma + spall) / resist);
        }
    }
}
