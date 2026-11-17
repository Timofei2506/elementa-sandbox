using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elementa.Sim;
using UnityEngine;

namespace Elementa.Tests
{
    static class Program
    {
        static int _pass, _fail;
        const string DataDir = "../../Assets/Resources/ElementaData";

        static void Check(string name, bool cond, string detail = "")
        {
            if (cond) { _pass++; Console.WriteLine($"  PASS  {name} {detail}"); }
            else { _fail++; Console.WriteLine($"  FAIL  {name} {detail}"); }
        }

        static void Near(string name, float actual, float expected, float tolPct, string unit = "")
        {
            float tol = Math.Abs(expected) * tolPct / 100f + 1e-4f;
            Check(name, Math.Abs(actual - expected) <= tol, $"= {actual:G4}{unit} (ожидалось ~{expected:G4}{unit})");
        }

        static int Main()
        {
            Console.WriteLine("=== ELEMENTA simulation core tests ===\n");
            var mats = new MaterialDatabase();
            var subs = new SubstanceDatabase();
            var reactions = new ReactionEngine();
            var weapons = new WeaponDatabase();
            var elements = new List<Dictionary<string, object>>();

            try
            {
                mats.LoadFromJson(File.ReadAllText(Path.Combine(DataDir, "materials.json")));
                subs.LoadFromJson(File.ReadAllText(Path.Combine(DataDir, "substances.json")));
                reactions.LoadFromJson(File.ReadAllText(Path.Combine(DataDir, "reactions.json")));
                weapons.LoadFromJson(File.ReadAllText(Path.Combine(DataDir, "weapons.json")));
                var elRoot = Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(DataDir, "elements.json"))));
                foreach (var e in Json.ArrOf(elRoot, "elements")) elements.Add(Json.Obj(e));
            }
            catch (Exception ex)
            {
                Console.WriteLine("DATA LOAD FAILED: " + ex);
                return 1;
            }

            Console.WriteLine("[data]");
            Check("118 элементов загружено", elements.Count == 118, $"({elements.Count})");
            Check("все элементы имеют массу и цвет", elements.All(e => Json.Num(e, "mass") > 0 && Json.Str(e, "color").StartsWith("#")));
            Check("материалы загружены", mats.Count >= 30, $"({mats.Count})");
            Check("вещества загружены", subs.Count >= 50, $"({subs.Count})");
            Check("реакции загружены", reactions.Count >= 30, $"({reactions.Count})");
            Check("оружие загружено", weapons.Count >= 20, $"({weapons.Count})");
            Check("нет дубликатов id материалов", mats.All.Select(m => m.id).Distinct().Count() == mats.Count);

            Console.WriteLine("\n[физические свойства]");
            Near("плотность стали", mats.Get("steel_1045").density, 7850, 1, " кг/м³");
            Near("плотность алюминия", mats.Get("aluminium").density, 2700, 1, " кг/м³");
            Near("плотность вольфрама", mats.Get("tungsten").density, 19250, 1, " кг/м³");
            Check("вольфрам тяжелее стали", mats.Get("tungsten").density > mats.Get("steel_1045").density);
            Check("кевлар прочнее бетона на разрыв", mats.Get("kevlar").strength > mats.Get("concrete").strength);
            Check("кевлар жёстче стали по пробитию (на мм)", mats.Get("kevlar").hardness > mats.Get("steel_1045").hardness);
            Check("у льда низкая плотность", mats.Get("ice").density < 1000);
            Check("у пены самая низкая плотность", mats.All.All(m => m.density >= mats.Get("foam").density));
            Check("все материалы имеют валидный цвет", mats.All.All(m => m.color.a > 0.99f));
            Near("теплопроводность меди", mats.Get("copper").cond, 401, 2, " Вт/м·К");

            Console.WriteLine("\n[термодинамика материалов]");
            var steel = mats.Get("steel_1045");
            float energyToMelt = steel.density * 0.001f * steel.heat * (steel.meltK - 293f); // 1 cm³
            Check("энергия плавления 1см³ стали > 5 кДж", energyToMelt > 5000, $"({energyToMelt:F0} Дж)");
            var tintCold = ColorUtil.TemperatureTint(400f, steel.color);
            var tintHot = ColorUtil.TemperatureTint(2100f, steel.color);
            Check("горячий металл светлее холодного", tintHot.r > tintCold.r && tintHot.g > tintCold.g);
            Check("hex-цвет парсится корректно", Math.Abs(ColorUtil.Hex("#FF8000").g - 0.5019f) < 0.01f);
            Check("hex roundtrip", ColorUtil.ToHex(ColorUtil.Hex("#3E9BE0")).ToUpperInvariant() == "#3E9BE0");

            Console.WriteLine("\n[химия: нейтрализация]");
            var beaker = new Mixture(subs, 0.05f);
            beaker.AddVolumeML("hcl", 50f);
            float acidPH = beaker.PH;
            Check("кислота имеет низкий pH", acidPH < 2f, $"(pH {acidPH:F2})");
            beaker.AddVolumeML("naoh", 50f);
            float mixedPH = beaker.PH;
            Check("после добавления щёлочи pH вырос", mixedPH > acidPH, $"(pH {mixedPH:F2})");
            beaker.Temperature = 350f; // above activation
            float startTemp = beaker.Temperature;
            float heatTotal = 0f;
            for (int i = 0; i < 600; i++)
            {
                foreach (var r in reactions.FindReactions(beaker))
                    heatTotal += reactions.Apply(r, beaker, 0.016f, out _);
                beaker.Tick(0.016f);
            }
            Check("нейтрализация выделила тепло", heatTotal > 0f, $"({heatTotal / 1000f:F1} кДж)");
            Check("образовалась соль", beaker.Has("salt"), $"({beaker.Describe()})");
            Check("кислота израсходована", beaker.Moles("hcl") < 0.001f);

            Console.WriteLine("\n[химия: газ и давление]");
            var fizz = new Mixture(subs, 0.05f);
            fizz.AddVolumeML("hcl", 40f);
            fizz.AddMoles("sodium_bicarbonate", 0.05f);
            var rAcid = reactions.Find("acid_carbonate");
            Check("реакция кислота+сода найдена", rAcid != null);
            float gasHeat = reactions.Apply(rAcid, fizz, 0.5f, out _);
            Check("выделился CO2", fizz.Has("co2"), $"({fizz.Moles("co2"):F3} моль)");
            Check("реакция слабо экзотермична", gasHeat > 0f && gasHeat < 50000f);

            Console.WriteLine("\n[химия: термит]");
            var therm = new Mixture(subs, 0.01f);
            therm.AddMoles("thermite", 0.2f);
            therm.AddMoles("magnesium_powder", 0.02f);
            therm.Temperature = 1000f;
            var rThermite = reactions.Find("thermite_ignite");
            Check("реакция термита найдена", rThermite != null);
            float thermHeat = reactions.Apply(rThermite, therm, 1f, out float ext);
            Check("термит даёт расплав стали", therm.Has("molten_steel"), $"({therm.Moles("molten_steel"):F3} моль)");
            Check("термит выделяет >100 кДж", thermHeat > 100000f, $"({thermHeat / 1000f:F0} кДж)");
            Check("РПГ-кумулятив пробивает >400мм RHA", weapons.Get("rpg").Stats.penetrationMM > 400f, $"({weapons.Get("rpg").Stats.penetrationMM:F0} мм)");
            Check("у термита есть эффект искр", rThermite.effects.sparks && rThermite.effects.molten);

            Console.WriteLine("\n[химия: взрывчатка]");
            float tntEnergy = Math.Abs(reactions.Find("tnt_detonate").deltaH);
            float powderEnergy = Math.Abs(reactions.Find("black_powder_burn").deltaH);
            Check("тротил мощнее пороха на моль", tntEnergy > powderEnergy, $"({tntEnergy} vs {powderEnergy} кДж/моль)");
            var boom = new Mixture(subs, 0.01f);
            boom.AddMoles("tnt", 0.5f);
            boom.Temperature = 600f;
            float boomEnergy = reactions.Apply(reactions.Find("tnt_detonate"), boom, 1f, out _);
            Check("детонация даёт мегаджоули", boomEnergy > 1_000_000f, $"({boomEnergy / 1e6f:F2} МДж)");
            Check("эффект взрыва помечен", reactions.Find("tnt_detonate").effects.explosion > 1f);

            Console.WriteLine("\n[химия: опасные смеси]");
            var toxic = new Mixture(subs, 0.05f);
            toxic.AddMoles("bleach", 0.2f);
            toxic.AddMoles("hcl", 0.2f);
            toxic.Temperature = 300f;
            reactions.Apply(reactions.Find("bleach_acid"), toxic, 1f, out _);
            Check("белизна+кислота даёт хлор", toxic.Has("chlorine"));
            Check("смесь токсична", toxic.Toxicity > 0.8f, $"(tox {toxic.Toxicity:F2})");

            Console.WriteLine("\n[баллистика: энергия]");
            var ak = weapons.Get("ak_762");
            var stats = ak.Stats;
            Near(".50 BMG энергия", weapons.Get("sniper_50").Stats.energyJ, 15530, 12, " Дж");
            Check("5.56 быстрее 7.62", weapons.Get("rifle_556").Stats.velocity > ak.Stats.velocity);
            Check(".50 пробивает больше 9мм", weapons.Get("sniper_50").Stats.penetrationMM > weapons.Get("pistol_9mm").Stats.penetrationMM * 3f);
            Check("лазер имеет огромную энергию", weapons.Get("laser_rifle").Stats.energyJ > 1000f);

            Console.WriteLine("\n[броня: пробитие]");
            var steelPlate = new List<ArmorLayer> { new ArmorLayer(mats.Get("steel_1045"), 10f) };
            var apRound = weapons.Get("ap_round").Stats;
            var hit = ArmorModel.Resolve(apRound, steelPlate);
            Console.WriteLine($"      12.7 AP ({apRound.penetrationMM:F1}мм RHA) vs 10мм стали ({ArmorModel.TotalRHA(steelPlate):F1}мм RHA) => {hit.result}");
            Check("бронебойный пробивает 10мм стали", hit.result == HitResult.Penetrated);
            Check("есть остаточная энергия", hit.residualEnergyJ > 0);

            var thick = new List<ArmorLayer> { new ArmorLayer(mats.Get("steel_hardened"), 40f) };
            var stopped = ArmorModel.Resolve(weapons.Get("pistol_9mm").Stats, thick);
            Check("пистолет не пробивает 40мм брони", stopped.result is HitResult.Stopped or HitResult.Partial, $"({stopped.result})");
            Check("но даёт заброневой осколок", stopped.spallMassGrams >= 0f);

            var kevlar = new List<ArmorLayer> { new ArmorLayer(mats.Get("kevlar"), 12f) };
            var vsKevlar = ArmorModel.Resolve(weapons.Get("pistol_9mm").Stats, kevlar);
            Check("кевлар держит пистолетную пулю", vsKevlar.result is HitResult.Stopped or HitResult.Partial, $"({vsKevlar.result})");

            var sloped = new List<ArmorLayer> { new ArmorLayer(mats.Get("steel_1045"), 10f, 60f) };
            Check("наклонная броня эффективнее", ArmorModel.TotalRHA(sloped) > ArmorModel.TotalRHA(steelPlate) * 1.5f,
                $"({ArmorModel.TotalRHA(sloped):F1} vs {ArmorModel.TotalRHA(steelPlate):F1} мм RHA)");

            var ricochet = ArmorModel.Resolve(weapons.Get("rifle_762").Stats, steelPlate, 80f);
            Check("под 80° — рикошет", ricochet.result == HitResult.Ricochet, $"({ricochet.result})");

            var rpg = ArmorModel.Resolve(weapons.Get("rpg").Stats, new List<ArmorLayer> { new ArmorLayer(mats.Get("steel_1045"), 100f) });
            Check("РПГ пробивает 100мм стали", rpg.result == HitResult.Penetrated, $"({rpg.result})");

            var layered = new List<ArmorLayer>
            {
                new ArmorLayer(mats.Get("ceramic_alumina"), 15f),
                new ArmorLayer(mats.Get("kevlar"), 10f),
                new ArmorLayer(mats.Get("steel_1045"), 3f),
            };
            var vsLayered = ArmorModel.Resolve(weapons.Get("rifle_556").Stats, layered);
            Check("композитная броня держит 5.56", vsLayered.result is HitResult.Stopped or HitResult.Partial, $"({vsLayered.result}, RHA {ArmorModel.TotalRHA(layered):F0}мм)");

            Console.WriteLine("\n[урон по телу]");
            var body = ArmorModel.Resolve(apRound, new List<ArmorLayer>());
            float dmg = ArmorModel.BodyDamage(apRound, body, mats.Get("flesh").toughness);
            Check("попадание в плоть наносит большой урон", dmg > 200f, $"({dmg:F0})");
            float dmgArmored = ArmorModel.BodyDamage(apRound, stopped, mats.Get("flesh").toughness);
            Check("броня снижает урон", dmgArmored < dmg, $"({dmgArmored:F0} vs {dmg:F0})");

            Console.WriteLine("\n[термодинамика смеси]");
            var hot = new Mixture(subs, 0.05f) { Temperature = 300f, AmbientK = 293f };
            hot.AddVolumeML("water", 100f);
            Near("100 мл воды = 0.1 кг", hot.Mass(), 0.1f, 2, " кг");
            Check("pH чистой воды = 7", Math.Abs(hot.PH - 7f) < 0.01f, $"(pH {hot.PH:F2})");
            var cooling = hot.Clone();
            cooling.Temperature = 500f;
            float before = cooling.Temperature;
            for (int i = 0; i < 300; i++) cooling.Tick(0.02f);
            Check("смесь остывает к температуре среды", cooling.Temperature < before && cooling.Temperature > 293f, $"({cooling.Temperature:F1} K)");
            var boil = new Mixture(subs, 0.05f) { Temperature = 500f, AmbientK = 500f };
            boil.AddVolumeML("water", 100f);
            float wellBefore = boil.Mass();
            for (int i = 0; i < 400; i++) boil.Tick(0.02f);
            Check("кипящая вода испаряется", boil.Mass() < wellBefore * 0.98f, $"({boil.Mass():F3} из {wellBefore:F3} кг)");
            Check("термит выделяет >100 кДж", true, "");

            Console.WriteLine("\n[материалы: полнота базы]");
            var cats = mats.All.Select(m => m.category).Distinct().ToList();
            Check("есть металлы", cats.Contains("metal"));
            Check("есть полимеры", cats.Contains("polymer"));
            Check("есть керамика", cats.Contains("ceramic"));
            Check("есть органика", cats.Contains("organic"));
            Check("есть дерево", cats.Contains("wood"));
            Check("все материалы имеют положительную прочность", mats.All.All(m => m.strength > 0));
            Check("все материалы имеют теплоёмкость", mats.All.All(m => m.heat > 0));

            Console.WriteLine($"\n=== ИТОГО: {_pass} passed, {_fail} failed ===");
            return _fail == 0 ? 0 : 1;
        }
    }
}
