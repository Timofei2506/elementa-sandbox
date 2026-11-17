using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Central thermal step: objects exchange heat with nearby hotter/colder objects,
    /// melt into puddles above their melting point and conduct through contact.
    /// </summary>
    public class ThermalSystem : MonoBehaviour
    {
        static readonly int HashPhase = Shader.PropertyToID("_Color");
        float _tick;
        readonly HashSet<int> _melted = new HashSet<int>();

        void Update()
        {
            _tick += Time.deltaTime;
            if (_tick < 0.12f) return;
            _tick = 0f;
            if (Game.I?.World == null) return;

            var objects = Game.I.World.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                var a = objects[i];
                if (a == null) continue;

                // melt into a puddle of the same material
                if (a.Temperature > a.Material.meltK + 60f && !_melted.Contains(a.GetInstanceID()))
                {
                    _melted.Add(a.GetInstanceID());
                    MeltObject(a);
                    continue;
                }

                for (int j = i + 1; j < objects.Count; j++)
                {
                    var b = objects[j];
                    if (b == null) continue;
                    float d = Vector3.Distance(a.transform.position, b.transform.position);
                    if (d > 1.6f) continue;
                    float diff = a.Temperature - b.Temperature;
                    if (Mathf.Abs(diff) < 8f) continue;
                    float transfer = diff * 0.06f * (1f - d / 1.6f);
                    a.Temperature -= transfer * 0.5f;
                    b.Temperature += transfer * 0.5f;
                    if (b.Temperature > b.Material.meltK && b.Temperature > 400f && b.Material.flammability > 0.3f) b.Ignite();
                }
            }
        }

        void MeltObject(SandboxObject obj)
        {
            var mat = obj.Material;
            var pos = obj.transform.position;
            EffectsSystem.SpawnFluidSplash(pos, mat.color, 30, 4f);
            EffectsSystem.SpawnSmoke(pos, 1.4f, 1.8f, 1f);

            // molten pool: visual disc that keeps emitting heat and light
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = "Molten_" + mat.id;
            puddle.transform.position = new Vector3(pos.x, 0.03f, pos.z);
            float size = Mathf.Clamp(obj.transform.localScale.magnitude * 0.8f, 0.8f, 4f);
            puddle.transform.localScale = new Vector3(size, 0.02f, size);
            Object.Destroy(puddle.GetComponent<Collider>());
            puddle.GetComponent<MeshRenderer>().sharedMaterial =
                RuntimeVisuals.For(Look.Emissive, ColorUtil.TemperatureTint(mat.meltK + 700f, mat.color), 0.2f, 0f, 2.4f);

            var molten = puddle.AddComponent<MoltenPool>();
            molten.Material = mat;
            molten.Lifetime = 14f;

            Destroy(obj.gameObject);
            Game.I.UI?.Log($"{mat.nameRu} расплавился");
        }
    }

    /// <summary>Hot puddle that cools, glows and damages anything standing in it.</summary>
    public class MoltenPool : MonoBehaviour
    {
        public MaterialDef Material;
        public float Lifetime = 14f;
        float _age, _temperature;
        Renderer _renderer;

        void Start()
        {
            _temperature = Material.meltK + 600f;
            _renderer = GetComponent<Renderer>();
        }

        void Update()
        {
            _age += Time.deltaTime;
            _temperature = Mathf.MoveTowards(_temperature, 400f, 45f * Time.deltaTime);
            float glow = Mathf.Clamp01((_temperature - 700f) / 900f);
            var block = new MaterialPropertyBlock();
            var color = Color.Lerp(new Color(0.15f, 0.13f, 0.12f), ColorUtil.TemperatureTint(_temperature, Material.color), glow);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(block);

            if (Game.I?.World != null)
            {
                foreach (var obj in Game.I.World.Objects)
                {
                    if (obj == null) continue;
                    if (Vector3.Distance(obj.transform.position, transform.position) < 1.4f)
                    {
                        obj.AddHeat(_temperature * 0.6f * Time.deltaTime);
                        if (obj.Material.flammability > 0.3f) obj.Ignite();
                    }
                }
                foreach (var npc in Game.I.World.Npcs)
                {
                    if (npc == null) continue;
                    if (Vector3.Distance(npc.transform.position, transform.position) < 1.4f)
                    {
                        npc.ApplyDamage(24f * Time.deltaTime, transform.position);
                        npc.Ignite();
                    }
                }
            }

            if (_age > Lifetime || _temperature < 430f) Destroy(gameObject);
        }
    }
}
