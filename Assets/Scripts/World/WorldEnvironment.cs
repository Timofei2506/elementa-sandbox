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
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.29f, 0.4f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.19f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.09f);
            RenderSettings.fog = true;
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
            go.transform.rotation = Quaternion.Euler(48f, 34f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.85f);
            light.intensity = 1.25f;
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
            fl.intensity = 0.35f;
            fl.shadows = LightShadows.None;
        }

        static void BuildArena(Transform parent)
        {
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(parent, false);
            groundGo.transform.position = new Vector3(0f, -0.05f, 0f);
            groundGo.transform.localScale = new Vector3(ArenaHalfSize * 2f, 0.1f, ArenaHalfSize * 2f);
            var mf = groundGo.AddComponent<MeshFilter>();
            mf.sharedMesh = RuntimeVisuals.Box;
            var mr = groundGo.AddComponent<MeshRenderer>();
            var tex = RuntimeVisuals.GridTexture(1024, 32, 1.6f);
            tex.anisoLevel = 8;
            var mat = RuntimeVisuals.For(Look.Concrete, Color.white, 0.85f, 0f);
            var groundMat = new Material(mat) { name = "ELEMENTA_Ground", mainTexture = tex, mainTextureScale = new Vector2(ArenaHalfSize / 4f, ArenaHalfSize / 4f) };
            mr.sharedMaterial = groundMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var bc = groundGo.AddComponent<BoxCollider>();
            groundGo.layer = LayerMask.NameToLayer("Default");

            // invisible arena walls keep props inside
            CreateWall(parent, new Vector3(0, 5, ArenaHalfSize), new Vector3(ArenaHalfSize * 2, 10, 1));
            CreateWall(parent, new Vector3(0, 5, -ArenaHalfSize), new Vector3(ArenaHalfSize * 2, 10, 1));
            CreateWall(parent, new Vector3(ArenaHalfSize, 5, 0), new Vector3(1, 10, ArenaHalfSize * 2));
            CreateWall(parent, new Vector3(-ArenaHalfSize, 5, 0), new Vector3(1, 10, ArenaHalfSize * 2));

            // decorative floor medallion
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Medallion";
            ring.transform.SetParent(parent, false);
            ring.transform.position = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(9f, 0.01f, 9f);
            Object.Destroy(ring.GetComponent<Collider>());
            var ringMat = RuntimeVisuals.For(Look.Metal, new Color(0.16f, 0.2f, 0.26f), 0.35f, 0.8f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;
            Ground = groundGo.transform;
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
