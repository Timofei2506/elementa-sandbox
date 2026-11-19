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

        // Свои шейдеры из Resources: встроенный "Standard" Unity вырезает из билда,
        // если на него нет ссылок из ассетов (материалы-то создаются кодом) — и всё
        // становится розовым. Resources гарантированно попадает в сборку.
        static Shader _surface, _transparent, _sprite, _fallback;

        public static Shader Surface =>
            _surface ??= Resources.Load<Shader>("Shaders/ElementaSurface") ?? Sprite;

        public static Shader Transparent =>
            _transparent ??= Resources.Load<Shader>("Shaders/ElementaAdditive") ?? Shader.Find("Sprites/Default");

        /// <summary>Шейдер для спрайтов/UI-подобных квадов.</summary>
        public static Shader Sprite =>
            _sprite ??= Shader.Find("Sprites/Default") ?? Transparent;

        /// <summary>Обратная совместимость: раньше это был Standard.</summary>
        public static Shader Standard => Surface;
        public static Shader Unlit => Transparent;

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

            bool transparent = look is Look.Glass or Look.Gas or Look.Liquid;
            var mat = new Material(transparent ? Transparent : Surface) { name = "ELEMENTA_" + look };
            if (transparent)
            {
                // прозрачные материалы: цвет + альфа, без PBR-свойств
                Color tint = color;
                if (look == Look.Glass) tint.a = 0.34f;
                else if (look == Look.Gas) tint.a = 0.22f;
                else tint.a = Mathf.Clamp(color.a <= 0f ? 0.9f : color.a, 0.55f, 1f);
                if (mat.HasProperty("_Color")) mat.color = tint;
                if (mat.HasProperty("_Fade")) mat.SetFloat("_Fade", tint.a);
                mat.renderQueue = 3000;
                _cache[key] = mat;
                return mat;
            }
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness));
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emission > 0.01f && mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * Mathf.Max(1f, emission * 3f));
            _cache[key] = mat;
            return mat;
        }

        static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
        public static Material ForDef(MaterialDef def)
        {
            if (def == null) return For(Look.Polymer, Color.gray);
            if (_matCache.TryGetValue(def.id, out var m) && m != null) return m;
            m = ForMaterial(def);
            m.name = "ELEMENTA_" + def.id;
            _matCache[def.id] = m;
            return m;
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

        /// <summary>9-sliced rounded rectangle sprite so every panel has soft corners.</summary>
        static readonly Dictionary<string, Sprite> _ringCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Скруглённое КОЛЬЦО (контур толщиной border px, прозрачная середина) — «грань стекла».
        /// Тянится 9-slice, край остаётся ровным при любом размере панели.
        /// </summary>
        public static Sprite Ring(int size = 96, int radius = 18, int border = 2)
        {
            string key = $"ring_{size}_{radius}_{border}";
            if (_ringCache.TryGetValue(key, out var cached) && cached != null) return cached;

            radius = Mathf.Clamp(radius, 2, size / 2 - 1);
            border = Mathf.Clamp(border, 1, Mathf.Min(8, radius));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ELEMENTA_Ring",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // расстояние до скруглённого прямоугольника
                    float dx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float dy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float dist = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;   // <0 внутри
                    float a = Mathf.Clamp01((0f - dist) / 1.5f) * Mathf.Clamp01((dist + border) / 1.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            int b = radius + 2;
            var sp = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sp.name = "ELEMENTA_RingSprite";
            _ringCache[key] = sp;
            return sp;
        }

        static Sprite _radialSprite;

        /// <summary>Мягкий радиальный градиент как спрайт — для свечений и «ореолов» за стеклом.</summary>
        public static Sprite RadialSprite()
        {
            if (_radialSprite != null) return _radialSprite;
            var tex = RadialTexture(128, 2.0f);
            _radialSprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _radialSprite.name = "ELEMENTA_RadialSprite";
            return _radialSprite;
        }

        static Shader _glass;
        /// <summary>Стеклянный UI-шейдер: размытие фона + скругления из спрайта панели.</summary>
        public static Shader Glass =>
            _glass ??= Resources.Load<Shader>("Shaders/ElementaGlass") ?? Shader.Find("Elementa/Glass") ?? Sprite;

        static Sprite _boxSprite, _frameSprite;


        /// <summary>Пустой (белый) спрайт: прямоугольники без скруглений — стиль Melon.</summary>
        public static Sprite BoxSprite
        {
            get
            {
                if (_boxSprite != null) return _boxSprite;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "ELEMENTA_Box",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false);
                _boxSprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
                _boxSprite.name = "ELEMENTA_BoxSprite";
                return _boxSprite;
            }
        }

        /// <summary>
        /// Рамка толщиной border px: белый контур, прозрачная середина. Тянится 9-slice,
        /// поэтому из неё получаются жёсткие рамки любого размера — как в Melon Sandbox.
        /// </summary>
        public static Sprite Frame(int size = 32, int border = 3)
        {
            string key = "frame_" + size + "_" + border;
            if (_frameCache.TryGetValue(key, out var cached) && cached != null) return cached;
            border = Mathf.Clamp(border, 1, size / 2 - 1);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ELEMENTA_Frame",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    px[y * size + x] = edge ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            var sp = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sp.name = "ELEMENTA_FrameSprite";
            _frameCache[key] = sp;
            return sp;
        }

        static readonly Dictionary<string, Sprite> _frameCache = new Dictionary<string, Sprite>();

        public static Sprite Rounded(int size = 64, int radius = 18)
        {
            string key = $"r{size}_{radius}";
            if (_rounded.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ELEMENTA_Rounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            sprite.name = "ELEMENTA_RoundedSprite";
            _rounded[key] = sprite;
            return sprite;
        }
        static readonly Dictionary<string, Sprite> _rounded = new Dictionary<string, Sprite>();

        public static Material TrailMaterial(Color color)
        {
            string key = $"trail{(int)(color.r * 20)}{(int)(color.g * 20)}{(int)(color.b * 20)}";
            if (_trails.TryGetValue(key, out var m) && m != null) return m;
            var sh = Sprite;
            m = new Material(sh) { name = "ELEMENTA_Trail", mainTexture = White, color = color };
            _trails[key] = m;
            return m;
        }
        static readonly Dictionary<string, Material> _trails = new Dictionary<string, Material>();

        public static Material SpriteMaterial()
        {
            var sh = Sprite;
            var m = new Material(sh) { name = "ELEMENTA_Sprite", mainTexture = White };
            return m;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _lookCache.Clear();
            _rounded.Clear();
            _trails.Clear();
            _matCache.Clear();
        }
    }
}
