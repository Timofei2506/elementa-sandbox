using System;
using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elementa
{
    public enum Look
    {
        Metal, Ceramic, Polymer, Wood, Liquid, Glass, Emissive, Foliage, Concrete, Stone, Ice, Flesh, Gas
    }

    /// <summary>
    /// Builds all runtime materials from Unity's built-in Standard shader and creates
    /// primitive meshes. No external assets required, so the project builds anywhere.
    /// </summary>
    public static class RuntimeVisuals
    {
        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        static readonly Dictionary<Look, Material> _lookCache = new Dictionary<Look, Material>();
        static Shader _std, _unlit;
        static Texture2D _white;

        public static Shader Standard => _std ??= Shader.Find("Standard");
        public static Shader Unlit => _unlit ??= Shader.Find("Unlit/Color");

        public static Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                _white = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "ELEMENTA_White" };
                _white.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                _white.Apply();
                return _white;
            }
        }

        public static Material For(Look look, Color color, float roughness = 0.5f, float metallic = 0f, float emission = 0f)
        {
            string key = $"{look}|{(int)(color.r * 40)}|{(int)(color.g * 40)}|{(int)(color.b * 40)}|{(int)(roughness * 10)}|{(int)(metallic * 10)}|{(int)(emission * 10)}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = new Material(Standard) { name = "ELEMENTA_" + look };
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness));
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            switch (look)
            {
                case Look.Glass:
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                    var gc = color; gc.a = 0.35f;
                    mat.color = gc;
                    break;
                case Look.Emissive:
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", color * Mathf.Max(1f, emission * 3f));
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    break;
                case Look.Liquid:
                    mat.SetFloat("_Glossiness", 0.95f);
                    break;
            }
            _cache[key] = mat;
            return mat;
        }

        public static Material ForMaterial(MaterialDef def, float damageTint = 0f, float temperature = 293f)
        {
            Color c = def.color;
            if (damageTint > 0f) c = Color.Lerp(c, new Color(0.12f, 0.1f, 0.1f), Mathf.Clamp01(damageTint) * 0.8f);
            c = ColorUtil.TemperatureTint(temperature, c);
            Look look = def.category switch
            {
                "metal" => Look.Metal,
                "ceramic" or "mineral" => Look.Concrete,
                "polymer" => Look.Polymer,
                "wood" => Look.Wood,
                "organic" => Look.Flesh,
                _ => Look.Polymer,
            };
            float emission = temperature > 900f ? Mathf.Clamp01((temperature - 900f) / 1400f) : 0f;
            if (emission > 0.01f) look = Look.Emissive;
            return For(look, c, def.roughness, def.metallic, emission);
        }

        public static Material ForSubstance(SubstanceDef s, bool emissive = false)
        {
            Look look = s.phase switch
            {
                Phase.Gas => Look.Gas,
                Phase.Solid => Look.Polymer,
                _ => Look.Liquid,
            };
            return For(look, s.color, look == Look.Liquid ? 0.05f : 0.5f, 0f, emissive ? 1.5f : 0f);
        }

        // ---------------- procedural primitives ----------------
        static Mesh _sphere, _cylinder, _capsule, _quad;
        public static Mesh Sphere => _sphere != null ? _sphere : _sphere = Grab(PrimitiveType.Sphere);
        public static Mesh Cylinder => _cylinder != null ? _cylinder : _cylinder = Grab(PrimitiveType.Cylinder);
        public static Mesh Capsule => _capsule != null ? _capsule : _capsule = Grab(PrimitiveType.Capsule);
        public static Mesh Quad => _quad != null ? _quad : _quad = Grab(PrimitiveType.Quad);

        static readonly Dictionary<string, Mesh> _boxes = new Dictionary<string, Mesh>();
        public static Mesh Box => _boxes.TryGetValue("unit", out var b) ? b : (_boxes["unit"] = Grab(PrimitiveType.Cube));

        static Mesh Grab(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(go);
            return mesh;
        }

        /// <summary>Flat ground quad with a subtle grid so movement is readable.</summary>
        public static Texture2D GridTexture(int size = 512, int cells = 32, float lineWidth = 1.4f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "ELEMENTA_Grid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            float cell = (float)size / cells;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool lineX = (x % cell) < lineWidth;
                    bool lineY = (y % cell) < lineWidth;
                    bool majorX = x % (cell * 8) < lineWidth * 1.6f;
                    bool majorY = y % (cell * 8) < lineWidth * 1.6f;
                    Color c = lineX || lineY ? new Color(0.19f, 0.22f, 0.27f) : new Color(0.12f, 0.14f, 0.18f);
                    if (majorX || majorY) c = new Color(0.28f, 0.33f, 0.4f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Soft radial alpha texture for glow sprites and ground shadows.</summary>
        public static Texture2D RadialTexture(int size = 128, float power = 2.2f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ELEMENTA_Radial", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, power);
                    px[y * size + x] = new Color(a, a, a, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Material Sprite()
        {
            var sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "ELEMENTA_Sprite", mainTexture = White };
            return m;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _lookCache.Clear();
        }
    }
}
