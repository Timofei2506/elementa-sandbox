using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>Ready-made sandbox setups, in the spirit of Melon Playground's built-in maps.</summary>
    public static class ScenarioLibrary
    {
        public class Scenario
        {
            public string id, nameRu, description;
            public System.Action<SandboxWorld> Build;
        }

        public static List<Scenario> All() => new List<Scenario>
        {
            new Scenario { id = "range", nameRu = "Стрельбище", description = "Плиты из разных материалов для проверки пробития", Build = BuildRange },
            new Scenario { id = "lab", nameRu = "Химлаборатория", description = "Ёмкости с кислотами, щёлочами и реагентами", Build = BuildLab },
            new Scenario { id = "demolition", nameRu = "Снос", description = "Башня из блоков и заряд C4 рядом", Build = BuildDemolition },
            new Scenario { id = "armor", nameRu = "Полигон брони", description = "Сталь, керамика, кевлар — сравнение в один клик", Build = BuildArmor },
            new Scenario { id = "fire", nameRu = "Пожар", description = "Дерево, бензин и открытый огонь", Build = BuildFire },
            new Scenario { id = "chemwar", nameRu = "ХиMPолигон", description = "Опасные смеси: хлор, термит, детонация", Build = BuildChemWar },
        };

        static void BuildRange(SandboxWorld w)
        {
            // мишени-плиты в ряд, слева направо: от мягкой к самой стойкой
            string[] plates = { "aluminium", "steel_1045", "steel_hardened", "ceramic_alumina", "kevlar", "tungsten" };
            for (int i = 0; i < plates.Length; i++)
            {
                w.Spawn(plates[i], BodyShape.Plate, new Vector3(0.9f, 2.2f, 0.4f), new Vector3(-9f + i * 3.6f, 1.2f, 0f));
            }
            for (int i = 0; i < 4; i++)
                w.Spawn("ballistic_gel", BodyShape.Box, new Vector3(0.9f, 1.3f, 0.9f), new Vector3(-7f + i * 4f, 0.7f, 0f));
            w.SpawnNpc(new Vector3(10f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Still);
            w.SpawnNpc(new Vector3(-11f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Wander);
        }

        static void BuildLab(SandboxWorld w)
        {
            // полки с колбами: каждая ёмкость — отдельная химия
            var a = w.SpawnContainer(new Vector3(-8f, 0.7f, 0f), 6f); a.Fill("hcl", 2f);
            var b = w.SpawnContainer(new Vector3(-6f, 0.7f, 0f), 6f); b.Fill("naoh", 2f);
            var c = w.SpawnContainer(new Vector3(-4f, 0.7f, 0f), 6f); c.Fill("sulfuric", 2f);
            var d = w.SpawnContainer(new Vector3(-2f, 0.7f, 0f), 6f); d.Fill("hydrogen_peroxide", 2f);
            var e = w.SpawnContainer(new Vector3(0f, 0.7f, 0f), 6f); e.Fill("copper_sulfate", 2f);
            var f = w.SpawnContainer(new Vector3(2f, 0.7f, 0f), 6f); f.Fill("ammonia", 2f);
            var g = w.SpawnContainer(new Vector3(4f, 0.7f, 0f), 6f); g.Fill("ethanol", 2f);
            var h = w.SpawnContainer(new Vector3(6f, 0.7f, 0f), 6f); h.Fill("thermite", 1.2f);
            w.Spawn("glass", BodyShape.Plate, new Vector3(3f, 2.4f, 0.3f), new Vector3(-6f, 1.3f, 0f));
            w.Spawn("borosilicate", BodyShape.Cylinder, new Vector3(1.2f, 1.6f, 0.6f), new Vector3(8f, 0.9f, 0f));
            w.SpawnNpc(new Vector3(-9.5f, 1.0f, 0f), NpcActor.NpcKind.Scientist).SetBehaviour(NpcActor.Behaviour.Wander);
        }

        static void BuildDemolition(SandboxWorld w)
        {
            // башня из блоков — валится целиком, как классическая карта Melon
            int rows = 9;
            for (int y = 0; y < rows; y++)
            {
                int width = Mathf.Max(1, 4 - y / 3);
                for (int x = 0; x < width; x++)
                    w.Spawn(y > 4 ? "brick" : "concrete", BodyShape.Box, new Vector3(1f, 0.92f, 0.9f),
                        new Vector3((x - (width - 1) * 0.5f) * 1.02f, 0.5f + y * 0.94f, 0f));
            }
            var charge = w.SpawnContainer(new Vector3(0f, 0.6f, 0f), 4f);
            charge.Fill("tnt", 2f);
            w.SpawnNpc(new Vector3(4f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Wander);
            w.SpawnNpc(new Vector3(-4f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Still);
        }

        static void BuildArmor(SandboxWorld w)
        {
            string[] mats = { "steel_1045", "ceramic_alumina", "kevlar", "titanium", "carbon_fiber", "armor_ceramic" };
            for (int i = 0; i < mats.Length; i++)
            {
                var p = w.Spawn(mats[i], BodyShape.Plate, new Vector3(0.4f, 2.4f, 0.5f), new Vector3(-9f + i * 3.6f, 1.3f, 0f), i % 2 == 0 ? 0f : 18f);
                if (p != null && i % 2 == 1) ArmorPlate.Attach(p, 30f);
            }
            for (int i = 0; i < 3; i++)
                w.SpawnNpc(new Vector3(-6f + i * 6f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Still);
        }

        static void BuildFire(SandboxWorld w)
        {
            for (int i = 0; i < 6; i++)
                w.Spawn(i % 2 == 0 ? "pine" : "bamboo", BodyShape.Box, new Vector3(0.8f, 1.6f, 0.8f), new Vector3(-7f + i * 2.6f, 0.8f, 0f));
            w.Spawn("oak", BodyShape.Plate, new Vector3(6f, 0.3f, 1f), new Vector3(-2f, 2.6f, 0f));
            var barrel = w.SpawnContainer(new Vector3(6f, 0.7f, 0f)); barrel.Fill("gasoline", 4f);
            var barrel2 = w.SpawnContainer(new Vector3(7.6f, 0.7f, 0f)); barrel2.Fill("kerosene", 4f);
            w.SpawnNpc(new Vector3(-4f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Wander);
            w.SpawnNpc(new Vector3(2f, 1.0f, 0f), NpcActor.NpcKind.Zombie).SetBehaviour(NpcActor.Behaviour.Wander);
        }

        static void BuildChemWar(SandboxWorld w)
        {
            var thermite = w.SpawnContainer(new Vector3(-4f, 0.7f, 0f)); thermite.Fill("thermite", 1.5f);
            var bleach = w.SpawnContainer(new Vector3(-1f, 0.7f, 0f)); bleach.Fill("bleach", 3f);
            var acid = w.SpawnContainer(new Vector3(2f, 0.7f, 0f)); acid.Fill("hcl", 3f);
            w.Spawn("steel_hardened", BodyShape.Plate, new Vector3(4f, 0.3f, 1f), new Vector3(-2f, 2.4f, 0f));
            w.Spawn("steel_hardened", BodyShape.Plate, new Vector3(4f, 0.3f, 1f), new Vector3(2f, 2.4f, 0f));
            w.SpawnNpc(new Vector3(5f, 1.0f, 0f)).SetBehaviour(NpcActor.Behaviour.Wander);
            w.SpawnNpc(new Vector3(-6f, 1.0f, 0f), NpcActor.NpcKind.Robot).SetBehaviour(NpcActor.Behaviour.Wander);
        }

        public static void Load(string id)
        {
            var world = Game.I.World;
            var scenario = All().Find(s => s.id == id);
            if (scenario == null) return;
            world.ClearAll();
            scenario.Build(world);
            Game.I.CameraRig.ResetView();
            Game.I.UI?.Log("Сценарий: " + scenario.nameRu + " — " + scenario.description);
        }
    }
}
