using UnityEngine;
using UnityEngine.Rendering;

namespace Elementa
{
    /// <summary>Lighting, sky, fog and ground of the sandbox arena.</summary>
    public static class WorldEnvironment
    {
        public const float ArenaHalfSize = 26f;

        public static Light Sun { get; private set; }
        public static Transform Ground { get; private set; }

        public static void Build(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.28f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.16f);
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.09f, 0.11f, 0.15f);
            RenderSettings.reflectionIntensity = 0.65f;

            BuildSky(parent);
            BuildSun(parent);
            BuildArena(parent);
        }

        static void BuildSky(Transform parent)
        {
            var tex = SkyTexture(1024, 512);
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "ELEMENTA_Sky" };
                mat.SetTexture("_MainTex", tex);
                mat.SetFloat("_Exposure", 1.1f);
                mat.SetFloat("_Rotation", 0f);
                RenderSettings.skybox = mat;
            }
            DynamicGI.UpdateEnvironment();

            var cam = Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>Procedural panoramic gradient with soft nebula bands and stars.</summary>
        public static Texture2D SkyTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "ELEMENTA_SkyTex", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            var zenith = new Color(0.045f, 0.06f, 0.11f);
            var horizon = new Color(0.16f, 0.18f, 0.26f);
            var below = new Color(0.05f, 0.055f, 0.07f);
            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;                 // 0 = bottom
                Color baseCol = v < 0.5f
                    ? Color.Lerp(below, horizon, v * 2f)
                    : Color.Lerp(horizon, zenith, (v - 0.5f) * 2f);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    // nebula
                    float neb = Mathf.PerlinNoise(u * 6f, v * 4f) * 0.6f + Mathf.PerlinNoise(u * 14f + 3f, v * 9f) * 0.4f;
                    Color c = baseCol + new Color(0.09f, 0.05f, 0.14f) * Mathf.Pow(Mathf.Clamp01(neb), 3f) * (v > 0.35f ? 1f : 0.3f);
                    float star = 0f;
                    if (v > 0.42f && rng.NextDouble() < 0.0022)
                        star = 0.55f + (float)rng.NextDouble() * 0.45f;
                    c += new Color(star, star, star * 1.05f);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static void BuildSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.85f);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.35f;
            RenderSettings.sun = light;
            Sun = light;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(parent, false);
            fill.transform.rotation = Quaternion.Euler(-20f, -140f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.45f, 0.6f, 1f);
            fl.intensity = 0.55f;
            fl.shadows = LightShadows.None;
        }

        static void BuildArena(Transform parent)
        {
            // ---- пол: длинная плита, видимая сбоку (камера ортографическая, смотрит по Z) ----
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(parent, false);
            groundGo.transform.position = new Vector3(0f, -0.6f, 0f);
            groundGo.transform.localScale = new Vector3(ArenaHalfSize * 2f, 1.2f, 3f);
            var mf = groundGo.AddComponent<MeshFilter>();
            mf.sharedMesh = RuntimeVisuals.Box;
            var mr = groundGo.AddComponent<MeshRenderer>();
            var tex = RuntimeVisuals.GridTexture(1024, 32, 1.6f);
            tex.anisoLevel = 8;
            var mat = RuntimeVisuals.For(Look.Concrete, Color.white, 0.85f, 0f);
            var groundMat = new Material(mat)
            {
                name = "ELEMENTA_Ground",
                mainTexture = tex,
                mainTextureScale = new Vector2(ArenaHalfSize / 3f, 1f)
            };
            mr.sharedMaterial = groundMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            groundGo.AddComponent<BoxCollider>();
            groundGo.layer = LayerMask.NameToLayer("Default");

            // ---- задник: мягкий градиент, чтобы силуэты читались как в 2D-игре ----
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.position = new Vector3(0f, 0f, 6f);
            backdrop.transform.localScale = new Vector3(ArenaHalfSize * 3f, ArenaHalfSize * 3f, 0.1f);
            var bmf = backdrop.AddComponent<MeshFilter>();
            bmf.sharedMesh = RuntimeVisuals.Box;
            var bmr = backdrop.AddComponent<MeshRenderer>();
            var btex = BackdropTexture(256, 256);
            bmr.sharedMaterial = new Material(RuntimeVisuals.For(Look.Concrete, Color.white, 1f, 0f))
            {
                name = "ELEMENTA_Backdrop",
                mainTexture = btex
            };
            bmr.shadowCastingMode = ShadowCastingMode.Off;
            Object.Destroy(bmr.gameObject.GetComponent<Collider>());
            Object.Destroy(bmr.gameObject.GetComponent<BoxCollider>());

            // ---- стены арены: слева, справа, сверху. В 2D-виде это границы экрана ----
            CreateWall(parent, new Vector3(-ArenaHalfSize, 12f, 0f), new Vector3(1f, 40f, 3f));
            CreateWall(parent, new Vector3(ArenaHalfSize, 12f, 0f), new Vector3(1f, 40f, 3f));
            CreateWall(parent, new Vector3(0f, 24f, 0f), new Vector3(ArenaHalfSize * 2f, 1f, 3f));

            Ground = groundGo.transform;
        }

        /// <summary>Вертикальный градиент для задника: тёмный низ, чуть светлее к горизонту, с сеткой.</summary>
        public static Texture2D BackdropTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "ELEMENTA_BackdropTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            var bottom = new Color(0.10f, 0.11f, 0.14f);
            var mid = new Color(0.17f, 0.19f, 0.24f);
            var top = new Color(0.12f, 0.13f, 0.17f);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                Color c = v < 0.55f ? Color.Lerp(bottom, mid, v / 0.55f) : Color.Lerp(mid, top, (v - 0.55f) / 0.45f);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    // еле заметные вертикальные полосы — глубина без шума
                    float stripe = Mathf.PerlinNoise(u * 18f, 0.4f) * 0.02f;
                    // крупная сетка
                    bool grid = (x % 64 == 0) || (y % 64 == 0);
                    float g = grid ? 0.012f : 0f;
                    px[y * w + x] = c + new Color(stripe + g, stripe + g, stripe + g + 0.003f, 0f);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        static void CreateWall(Transform parent, Vector3 position, Vector3 size)
        {
            var go = new GameObject("ArenaWall");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.layer = LayerMask.NameToLayer("Default");
        }
    }
}
