using System;
using System.Collections.Generic;
using System.IO;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    [Serializable]
    public class SaveHeader
    {
        public string sceneName;
        public string savedAt;
        public int version = 1;
    }

    /// <summary>Saves and restores the whole sandbox as JSON in persistentDataPath.</summary>
    public static class SaveSystem
    {
        public const int Version = 1;

        public static string Directory => Path.Combine(Application.persistentDataPath, "scenes");

        public static string Save(string name)
        {
            var world = Game.I.World;
            if (world == null) return null;
            if (!System.IO.Directory.Exists(Directory)) System.IO.Directory.CreateDirectory(Directory);

            var w = new JsonWriter();
            w.BeginObject();
            w.Prop("version", Version);
            w.Prop("name", name);
            w.Prop("savedAt", DateTime.UtcNow.ToString("o"));
            w.Prop("gravity", world.GravityEnabled);

            w.Key("objects").BeginArray();
            foreach (var o in world.Objects)
            {
                if (o == null) continue;
                w.BeginObject();
                w.Prop("material", o.Material.id);
                w.Prop("shape", o.Shape.ToString());
                var p = o.transform.position;
                var s = o.transform.localScale;
                w.Prop("x", p.x); w.Prop("y", p.y); w.Prop("z", p.z);
                w.Prop("sx", s.x); w.Prop("sy", s.y); w.Prop("sz", s.z);
                w.Prop("rotY", o.transform.eulerAngles.y);
                w.Prop("health", o.Health);
                w.Prop("temperature", o.Temperature);
                w.Prop("burning", o.IsBurning);
                w.EndObject();
            }
            w.EndArray();

            w.Key("containers").BeginArray();
            foreach (var c in world.Containers)
            {
                if (c == null) continue;
                w.BeginObject();
                var p = c.transform.position;
                w.Prop("x", p.x); w.Prop("y", p.y); w.Prop("z", p.z);
                w.Prop("capacity", c.CapacityLiters);
                w.Prop("temperature", c.Mix.Temperature);
                w.Key("contents").BeginObject();
                foreach (var kv in c.Mix.Contents) w.Prop(kv.Key, kv.Value);
                w.EndObject();
                w.EndObject();
            }
            w.EndArray();

            w.Key("npcs").BeginArray();
            foreach (var n in world.Npcs)
            {
                if (n == null) continue;
                w.BeginObject();
                var p = n.transform.position;
                w.Prop("kind", n.Kind.ToString());
                w.Prop("behaviour", n.CurrentBehaviour.ToString());
                w.Prop("x", p.x); w.Prop("y", p.y); w.Prop("z", p.z);
                w.Prop("health", n.Health);
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();

            string path = Path.Combine(Directory, Sanitize(name) + ".json");
            File.WriteAllText(path, w.ToString());
            Game.I.UI?.Log("Сцена сохранена: " + path);
            return path;
        }

        public static bool Load(string name)
        {
            string path = Path.Combine(Directory, Sanitize(name) + ".json");
            if (!File.Exists(path)) { Game.I.UI?.Log("Сцена не найдена: " + name); return false; }
            try
            {
                var root = Json.Obj(Json.Parse(File.ReadAllText(path)));
                var world = Game.I.World;
                world.ClearAll();
                world.SetGravity(Json.Bool(root, "gravity", true));

                foreach (var raw in Json.ArrOf(root, "objects"))
                {
                    var o = Json.Obj(raw);
                    var mat = Game.I.Materials.TryGet(Json.Str(o, "material"));
                    if (mat == null) continue;
                    Enum.TryParse(Json.Str(o, "shape"), out BodyShape shape);
                    var pos = new Vector3((float)Json.Num(o, "x"), (float)Json.Num(o, "y"), (float)Json.Num(o, "z"));
                    var size = new Vector3((float)Json.Num(o, "sx", 1), (float)Json.Num(o, "sy", 1), (float)Json.Num(o, "sz", 1));
                    var obj = world.Spawn(mat, shape, size, pos, (float)Json.Num(o, "rotY"));
                    if (obj == null) continue;
                    obj.SetState((float)Json.Num(o, "health", obj.MaxHealth), (float)Json.Num(o, "temperature", 293f));
                    if (Json.Bool(o, "burning")) obj.Ignite();
                }

                foreach (var raw in Json.ArrOf(root, "containers"))
                {
                    var c = Json.Obj(raw);
                    var pos = new Vector3((float)Json.Num(c, "x"), (float)Json.Num(c, "y"), (float)Json.Num(c, "z"));
                    var container = world.SpawnContainer(pos, (float)Json.Num(c, "capacity", 8));
                    container.Mix.Temperature = (float)Json.Num(c, "temperature", 293f);
                    if (c.TryGetValue("contents", out var contentsObj) && contentsObj is Dictionary<string, object> contents)
                        foreach (var kv in contents)
                            container.Mix.AddMoles(kv.Key, (float)Json.Num(contents, kv.Key));
                    container.RefreshVisual();
                }

                foreach (var raw in Json.ArrOf(root, "npcs"))
                {
                    var o = Json.Obj(raw);
                    Enum.TryParse(Json.Str(o, "kind"), out NpcActor.NpcKind kind);
                    Enum.TryParse(Json.Str(o, "behaviour"), out NpcActor.Behaviour behaviour);
                    var pos = new Vector3((float)Json.Num(o, "x"), (float)Json.Num(o, "y"), (float)Json.Num(o, "z"));
                    var npc = world.SpawnNpc(pos, kind);
                    npc.SetBehaviour(behaviour);
                }

                Game.I.UI?.Log($"Сцена загружена: {name} ({world.Objects.Count} объектов, {world.Npcs.Count} NPC)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[ELEMENTA] Ошибка загрузки: " + e);
                Game.I.UI?.Log("Ошибка загрузки сцены: " + e.Message);
                return false;
            }
        }

        public static List<string> ListSaves()
        {
            var result = new List<string>();
            if (!System.IO.Directory.Exists(Directory)) return result;
            foreach (var f in System.IO.Directory.GetFiles(Directory, "*.json"))
                result.Add(Path.GetFileNameWithoutExtension(f));
            result.Sort();
            return result;
        }

        public static void Delete(string name)
        {
            string path = Path.Combine(Directory, Sanitize(name) + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "scene" : name.Trim();
        }
    }
}
