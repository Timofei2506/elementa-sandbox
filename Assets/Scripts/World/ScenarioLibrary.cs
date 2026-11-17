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
            w.Spawn("steel_1045", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.3f), new Vector3(-6f, 1.4f, 0f));
            w.Spawn("steel_hardened", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.3f), new Vector3(-3.5f, 1.4f, 0f));
            w.Spawn("ceramic_alumina", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.4f), new Vector3(-1f, 1.4f, 0f));
            w.Spawn("kevlar", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.25f), new Vector3(1.5f, 1.4f, 0f));
            w.Spawn("aluminium", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.3f), new Vector3(4f, 1.4f, 0f));
            w.Spawn("tungsten", BodyShape.Plate, new Vector3(1.4f, 2.4f, 0.3f), new Vector3(6.5f, 1.4f, 0f));
            for (int i = 0; i < 4; i++)
                w.Spawn("ballistic_gel", BodyShape.Box, new Vector3(0.8f, 1.2f, 0.8f), new Vector3(-4.5f + i * 3f, 0.7f, -4f));
        }

        static void BuildLab(SandboxWorld w)
        {
            var a = w.SpawnContainer(new Vector3(-4f, 0.9f, 2f)); a.Fill("hcl", 2f);
            var b = w.SpawnContainer(new Vector3(-1.5f, 0.9f, 2f)); b.Fill("naoh", 2f);
            var c = w.SpawnContainer(new Vector3(1f, 0.9f, 2f)); c.Fill("sulfuric", 2f);
            var d = w.SpawnContainer(new Vector3(3.5f, 0.9f, 2f)); d.Fill("hydrogen_peroxide", 2f);
            var e = w.SpawnContainer(new Vector3(6f, 0.9f, 2f)); e.Fill("copper_sulfate", 2f);
            w.Spawn("glass", BodyShape.Plate, new Vector3(3f, 2.5f, 0.15f), new Vector3(0f, 1.4f, -3f));
            w.Spawn("borosilicate", BodyShape.Cylinder, new Vector3(0.8f, 1.4f, 0.8f), new Vector3(-6f, 1.2f, -1f));
        }

        static void BuildDemolition(SandboxWorld w)
        {
            int rows = 7;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < 3 - y / 3; x++)
                    w.Spawn(y > 3 ? "concrete" : "brick", BodyShape.Box, new Vector3(1.1f, 0.9f, 1.1f),
                        new Vector3((x - (2 - y / 3) * 0.5f) * 1.2f, 0.55f + y * 0.95f, 0f));
            w.Spawn("concrete", BodyShape.Plate, new Vector3(5f, 0.4f, 5f), new Vector3(0f, 0.2f, 0f));
            var npc = w.SpawnNpc(new Vector3(4f, 1.2f, 3f)); npc.SetBehaviour(NpcActor.Behaviour.Wander);
        }

        static void BuildArmor(SandboxWorld w)
        {
            w.Spawn("steel_1045", BodyShape.Plate, new Vector3(0.35f, 2.6f, 6f), new Vector3(-7f, 1.5f, 0f));
            w.Spawn("ceramic_alumina", BodyShape.Plate, new Vector3(0.5f, 2.6f, 6f), new Vector3(-4f, 1.5f, 0f));
            w.Spawn("kevlar", BodyShape.Plate, new Vector3(0.3f, 2.6f, 6f), new Vector3(-1f, 1.5f, 0f));
            w.Spawn("titanium", BodyShape.Plate, new Vector3(0.4f, 2.6f, 6f), new Vector3(2f, 1.5f, 0f));
            w.Spawn("carbon_fiber", BodyShape.Plate, new Vector3(0.4f, 2.6f, 6f), new Vector3(5f, 1.5f, 0f));
            for (int i = 0; i < 5; i++)
            {
                var p = w.Spawn("steel_1045", BodyShape.Plate, new Vector3(0.35f, 2.6f, 1.2f), new Vector3(-4f + i * 3f, 1.5f, 4.5f), 55f);
                if (p != null) ArmorPlate.Attach(p, 35f);
            }
        }

        static void BuildFire(SandboxWorld w)
        {
            w.Spawn("oak", BodyShape.Plate, new Vector3(12f, 0.3f, 8f), new Vector3(0f, 0.3f, 0f));
            for (int i = 0; i < 5; i++)
                w.Spawn(i % 2 == 0 ? "pine" : "bamboo", BodyShape.Box, Vector3.one, new Vector3(-4f + i * 2f, 1f, -1f + (i % 2)));
            var barrel = w.SpawnContainer(new Vector3(3f, 0.9f, 2f)); barrel.Fill("gasoline", 4f);
            var barrel2 = w.SpawnContainer(new Vector3(4.5f, 0.9f, 2f)); barrel2.Fill("kerosene", 4f);
            var npc = w.SpawnNpc(new Vector3(-2f, 1.2f, 3f)); npc.SetBehaviour(NpcActor.Behaviour.Wander);
        }

        static void BuildChemWar(SandboxWorld w)
        {
            var thermite = w.SpawnContainer(new Vector3(-3f, 0.9f, 0f)); thermite.Fill("thermite", 1.5f);
            var bleach = w.SpawnContainer(new Vector3(0f, 0.9f, 0f)); bleach.Fill("bleach", 3f);
            var acid = w.SpawnContainer(new Vector3(3f, 0.9f, 0f)); acid.Fill("hcl", 3f);
            w.Spawn("steel_1045", BodyShape.Plate, new Vector3(4f, 0.25f, 4f), new Vector3(0f, 0.2f, -3f));
            w.Spawn("steel_1045", BodyShape.Plate, new Vector3(4f, 0.25f, 4f), new Vector3(-3f, 2.2f, -3f));
            var npc = w.SpawnNpc(new Vector3(4f, 1.2f, 3f)); npc.SetBehaviour(NpcActor.Behaviour.Wander);
        }

        public static void Load(string id)
        {
            var world = Game.I.World;
            var scenario = All().Find(s => s.id == id);
            if (scenario == null) return;
            world.ClearAll();
            world.Spawn("concrete", BodyShape.Plate, new Vector3(20f, 0.3f, 14f), new Vector3(0f, 0.15f, 0f));
            scenario.Build(world);
            Game.I.CameraRig.ResetView();
            Game.I.UI?.Log("Сценарий: " + scenario.nameRu + " — " + scenario.description);
        }
    }
}
