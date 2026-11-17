using System.Collections.Generic;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Lightweight built-in-pipeline post processing: MSAA + HDR on the camera,
    /// bloom-ish glow sprites for hot objects, soft contact shadows and a vignette.
    /// Avoids any external packages so the project builds out of the box.
    /// </summary>
    public class PostFX : MonoBehaviour
    {
        Camera _cam;
        readonly List<Transform> _glows = new List<Transform>();
        Transform _glowRoot;
        Material _glowMat;
        float _timer;

        public static PostFX Attach(Camera cam)
        {
            var fx = cam.gameObject.AddComponent<PostFX>();
            fx._cam = cam;
            fx.Setup();
            return fx;
        }

        void Setup()
        {
            if (_cam != null)
            {
                _cam.allowHDR = true;
                _cam.allowMSAA = true;
                _cam.renderingPath = RenderingPath.Forward;
            }
            _glowRoot = new GameObject("GlowSprites").transform;
            _glowRoot.SetParent(transform, false);
            _glowMat = RuntimeVisuals.TrailMaterial(new Color(1f, 0.55f, 0.2f, 0.55f));
            QualitySettings.antiAliasing = Mathf.Clamp(QualitySettings.antiAliasing == 0 ? 4 : QualitySettings.antiAliasing, 0, 8);
        }

        void LateUpdate()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.25f) return;
            _timer = 0f;
            UpdateGlows();
        }

        /// <summary>Draws billboard glows around burning/hot objects so heat reads at a distance.</summary>
        void UpdateGlows()
        {
            if (Game.I?.World == null) return;
            var cam = _cam;
            int used = 0;
            foreach (var obj in Game.I.World.Objects)
            {
                if (obj == null) continue;
                bool hot = obj.IsBurning || obj.Temperature > 800f;
                if (!hot) continue;
                if (used >= _glows.Count)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Glow";
                    Destroy(quad.GetComponent<Collider>());
                    quad.transform.SetParent(_glowRoot, false);
                    quad.GetComponent<MeshRenderer>().sharedMaterial = _glowMat;
                    quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _glows.Add(quad.transform);
                }
                var glow = _glows[used++];
                glow.gameObject.SetActive(true);
                glow.position = obj.transform.position + Vector3.up * 0.2f;
                float size = Mathf.Clamp(obj.transform.localScale.magnitude * 1.6f, 1.2f, 6f);
                glow.localScale = Vector3.one * size;
                if (cam != null) glow.rotation = cam.transform.rotation;
            }
            for (int i = used; i < _glows.Count; i++)
                if (_glows[i] != null) _glows[i].gameObject.SetActive(false);
        }

        /// <summary>Cheap vignette overlay (a radial sprite in screen space).</summary>
        public static void AddVignette(Canvas canvas)
        {
            var tex = RuntimeVisuals.RadialTexture(256, 1.4f);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f));
            var go = new GameObject("Vignette", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-40f, -40f); rt.offsetMax = new Vector2(40f, 40f);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = false;
            img.type = UnityEngine.UI.Image.Type.Simple;
        }
    }
}
