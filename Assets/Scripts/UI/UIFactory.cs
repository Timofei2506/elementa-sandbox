using System;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>Design tokens shared by the whole interface.</summary>
    public static class Theme
    {
        public static readonly Color Bg = new Color(0.055f, 0.063f, 0.082f, 0.96f);
        public static readonly Color Panel = new Color(0.086f, 0.098f, 0.125f, 0.98f);
        public static readonly Color PanelLight = new Color(0.135f, 0.153f, 0.19f, 1f);
        public static readonly Color Stroke = new Color(0.22f, 0.25f, 0.32f, 0.9f);
        public static readonly Color Accent = new Color(0.24f, 0.83f, 0.78f, 1f);
        public static readonly Color AccentDim = new Color(0.16f, 0.55f, 0.52f, 1f);
        public static readonly Color Warn = new Color(1f, 0.68f, 0.25f, 1f);
        public static readonly Color Danger = new Color(1f, 0.35f, 0.35f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.95f, 0.98f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.68f, 0.76f, 1f);
        public static readonly Color Card = new Color(0.115f, 0.13f, 0.16f, 1f);

        public const int FontTitle = 30;
        public const int FontHeader = 21;
        public const int FontBody = 16;
        public const int FontSmall = 13;
        public const int FontTiny = 11;

        public static Font Font { get; private set; }

        public static void EnsureFont()
        {
            if (Font != null) return;
            Font = Resources.Load<Font>("Fonts/Roboto-Regular");
            if (Font == null)
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f.name == "Arial" || f.name == "LegacyRuntime") { Font = f; break; }
            }
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>
    /// Builds uGUI elements from code. The whole interface is generated at runtime,
    /// which keeps the repository free of fragile serialized scene data.
    /// </summary>
    public static class UIFactory
    {
        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Box(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color? color = null, bool rounded = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            if (color.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = color.Value;
                if (rounded) Round(img);
            }
            return rt;
        }

        public static void Round(Graphic g, float radius = 10f)
        {
            var img = g as Image;
            if (img == null) return;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, float radius = 14f)
        {
            var rt = Box(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
            var img = rt.GetComponent<Image>();
            if (img != null) img.color = color;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Stroke;
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Theme.EnsureFont();
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Theme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string caption, Vector2 size, Action onClick, Color? baseColor = null, int fontSize = Theme.FontBody)
        {
            var go = new GameObject("Button_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            var bc = baseColor ?? Theme.PanelLight;
            img.color = bc;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.85f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var label = Label(go.transform, caption, fontSize, Theme.Text, TextAnchor.MiddleCenter);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 4f); lrt.offsetMax = new Vector2(-8f, -4f);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Slider Slider(Transform parent, string caption, float min, float max, float value, Action<float> onChange, Vector2 size)
        {
            var root = new GameObject("Slider_" + caption, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = size;

            var bg = Box(root.transform, "Bg", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f), Theme.PanelLight);
            var fillArea = Box(root.transform, "Fill", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            var fill = Box(fillArea, "FillBar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Theme.Accent);
            var handle = Box(root.transform, "Handle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-9f, -9f), new Vector2(9f, 9f), Theme.Text);

            var slider = root.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = (RectTransform)fill;
            slider.handleRect = (RectTransform)handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));

            var label = Label(root.transform, caption, Theme.FontSmall, Theme.TextDim);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.offsetMin = new Vector2(0f, 0f); lrt.offsetMax = new Vector2(0f, 18f);
            return slider;
        }

        public static Toggle Toggle(Transform parent, string caption, bool value, Action<bool> onChange, Vector2 size)
        {
            var root = new GameObject("Toggle_" + caption, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = size;

            var box = Box(root.transform, "Box", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -11f), new Vector2(22f, 11f), Theme.PanelLight);
            var check = Box(box, "Check", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f), Theme.Accent);

            var label = Label(root.transform, caption, Theme.FontBody, Theme.Text);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(30f, 0f); lrt.offsetMax = Vector2.zero;

            var toggle = root.AddComponent<Toggle>();
            var bgImg = box.GetComponent<Image>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = value;
            if (onChange != null) toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        /// <summary>Adds a vertical layout group that stretches children horizontally.</summary>
        public static VerticalLayoutGroup VLayout(RectTransform rt, float spacing = 8f, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(10, 10, 10, 10);
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperLeft;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(RectTransform rt, float spacing = 8f, RectOffset padding = null)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(10, 10, 10, 10);
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static GridLayoutGroup Grid(RectTransform rt, Vector2 cell, float spacing = 6f)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = new Vector2(spacing, spacing);
            g.padding = new RectOffset(8, 8, 8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Mathf.Max(2, (int)(rt.rect.width / (cell.x + spacing)));
            return g;
        }

        public static ScrollRect Scroll(Transform parent, out RectTransform content, string name = "Scroll")
        {
            var viewportRt = Box(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = viewportRt.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0.001f);
            viewportRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentRt = Box(viewportRt, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            contentRt.pivot = new Vector2(0.5f, 1f);

            var scroll = viewportRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            content = contentRt;
            return scroll;
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        /// <summary>Small pill showing key information (FPS, object count, mode).</summary>
        public static Text Pill(Transform parent, string text, Color color, Vector2 size)
        {
            var bg = Box(parent, "Pill", Vector2.zero, Vector2.zero, Vector2.zero, size, new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.85f));
            var label = Label(bg, text, Theme.FontSmall, color, TextAnchor.MiddleCenter);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6f, 0f); lrt.offsetMax = new Vector2(-6f, 0f);
            return label;
        }
    }
}
