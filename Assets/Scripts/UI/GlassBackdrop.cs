using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Стеклянный интерфейс: размытие настоящей сцены под панелями.
    ///
    /// Как это работает без пост-обработки URP:
    ///   1. в конце кадра камера рендерит сцену в маленький RenderTexture (1/4 экрана);
    ///   2. текстура дважды размывается «пинг-понгом» через собственный шейдер;
    ///   3. размытый кадр выставляется глобальной текстурой _ElementaBlurTex,
    ///      а <see cref="ElementaGlass"/> читает её по экранным координатам.
    ///
    /// Плюс рисуются мягкие цветные пятна-«ореолы», чтобы у стекла было что подсвечивать.
    /// </summary>
    public class GlassBackdrop : MonoBehaviour
    {
        public static GlassBackdrop I { get; private set; }

        Camera _source;
        Camera _blurCam;
        Material _blurMat;
        RenderTexture[] _rt;
        MeshRenderer _blurRenderer;
        int _width, _height;
        float _timer;
        bool _ready;

        /// <summary>Размер буфера размытия относительно экрана. Меньше — мягче и дешевле.</summary>
        public const int Downscale = 4;

        public static GlassBackdrop Attach(Camera source, UIRoot ui)
        {
            if (I != null) { I._source = source; return I; }
            var go = new GameObject("GlassBackdrop");
            Object.DontDestroyOnLoad(go);
            var g = go.AddComponent<GlassBackdrop>();
            g._source = source;
            I = g;
            g.Build();
            return g;
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("Shaders/ElementaBlur");
            if (shader == null) shader = Shader.Find("Hidden/Elementa/Blur");
            if (shader == null)
            {
                Debug.LogWarning("[ELEMENTA] Шейдер размытия не найден — стекло будет без блюра.");
                return;
            }
            _blurMat = new Material(shader) { name = "ELEMENTA_Blur", hideFlags = HideFlags.HideAndDontSave };

            var camGo = new GameObject("GlassBlurCamera");
            camGo.transform.SetParent(transform, false);
            _blurCam = camGo.AddComponent<Camera>();
            _blurCam.enabled = false;
            _blurCam.orthographic = true;
            _blurCam.clearFlags = CameraClearFlags.SolidColor;
            _blurCam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            _blurCam.cullingMask = 0;
            _blurCam.depth = -50;

            // полноэкранный квад, который прогоняет размытие
            var quad = new GameObject("BlurQuad");
            quad.transform.SetParent(camGo.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 1f);
            quad.transform.localScale = new Vector3(2f, 2f, 1f);
            var mf = quad.AddComponent<MeshFilter>();
            mf.sharedMesh = RuntimeVisuals.Quad;
            _blurRenderer = quad.AddComponent<MeshRenderer>();
            _blurRenderer.sharedMaterial = _blurMat;
            _blurRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _blurRenderer.receiveShadows = false;
            _blurRenderer.enabled = false;
            _ready = true;
        }

        void Resize()
        {
            int w = Mathf.Max(64, Screen.width / Downscale);
            int h = Mathf.Max(64, Screen.height / Downscale);
            if (w == _width && h == _height && _rt != null && _rt[0] != null) return;

            Release();
            _width = w; _height = h;
            _rt = new RenderTexture[2];
            for (int i = 0; i < 2; i++)
            {
                _rt[i] = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
                {
                    name = "ELEMENTA_Blur" + i,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _rt[i].Create();
            }
            Shader.SetGlobalTexture("_ElementaBlurTex", _rt[0]);
            Shader.SetGlobalVector("_ElementaBlurTex_TexelSize", new Vector4(1f / w, 1f / h, w, h));
        }

        void Release()
        {
            if (_rt == null) return;
            foreach (var t in _rt) if (t != null) { t.Release(); DestroyImmediate(t); }
            _rt = null;
        }

        void OnDestroy()
        {
            Release();
            if (I == this) I = null;
        }

        void LateUpdate()
        {
            if (!_ready || _source == null) return;
            // четверть частоты кадров вполне хватает: размытие почти не заметно «дёргается»
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.016f;

            Resize();
            if (_rt == null) return;

            // 1. снимок сцены (без UI: UI-камера не трогается, рендерим только сцену)
            var prev = RenderTexture.active;
            _source.targetTexture = _rt[0];
            _source.Render();
            _source.targetTexture = null;

            // 2. два прохода размытия: горизонталь, затем вертикаль
            _blurRenderer.enabled = true;
            _blurCam.targetTexture = _rt[1];
            _blurMat.SetVector("_BlurDir", new Vector4(1f / _width, 0f, 0f, 0f));
            _blurCam.Render();

            _blurCam.targetTexture = _rt[0];
            _blurMat.SetVector("_BlurDir", new Vector4(0f, 1f / _height, 0f, 0f));
            _blurCam.Render();
            _blurCam.targetTexture = null;
            _blurRenderer.enabled = false;

            RenderTexture.active = prev;
            Shader.SetGlobalTexture("_ElementaBlurTex", _rt[0]);
        }

        /// <summary>Мягкое цветное пятно за панелью: даёт стеклу что подсвечивать.</summary>
        public static Image Glow(RectTransform parent, Vector2 center, Vector2 size, Color color)
        {
            var go = new GameObject("GlassGlow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = center;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = RuntimeVisuals.RadialSprite();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
