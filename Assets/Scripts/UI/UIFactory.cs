using System;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>Design tokens shared by the whole interface.</summary>
    public static class Theme
    {
        // ---- палитра: тёмное стекло, холодный свет, яркий акцент ----
        public static readonly Color Bg = new Color(0.045f, 0.05f, 0.07f, 0.72f);
        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.095f, 0.66f);
        public static readonly Color PanelLight = new Color(0.16f, 0.18f, 0.23f, 0.62f);
        public static readonly Color Stroke = new Color(0.72f, 0.85f, 1f, 0.30f);      // тонкая светлая кромка
        public static readonly Color StrokeDim = new Color(0.6f, 0.7f, 0.85f, 0.16f);
        public static readonly Color Accent = new Color(0.42f, 0.86f, 1f, 1f);          // голубой неон
        public static readonly Color AccentDim = new Color(0.26f, 0.55f, 0.72f, 1f);
        public static readonly Color Warn = new Color(1f, 0.72f, 0.32f, 1f);
        public static readonly Color Danger = new Color(1f, 0.42f, 0.44f, 1f);
        public static readonly Color Text = new Color(0.95f, 0.97f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.70f, 0.78f, 0.88f, 0.86f);
        public static readonly Color Card = new Color(0.13f, 0.16f, 0.21f, 0.55f);      // стеклянная плитка
        public static readonly Color CardDark = new Color(0.10f, 0.12f, 0.16f, 0.62f);
        public static readonly Color Slot = new Color(0.11f, 0.13f, 0.18f, 0.60f);
        public static readonly Color Ink = new Color(0.05f, 0.07f, 0.10f, 1f);
        public static readonly Color GlowCyan = new Color(0.22f, 0.72f, 1f, 0.30f);
        public static readonly Color GlowViolet = new Color(0.55f, 0.35f, 1f, 0.26f);
        public static readonly Color GlowPink = new Color(1f, 0.35f, 0.65f, 0.20f);

        public const int FontTitle = 34;
        public const int FontHeader = 24;
        public const int FontBody = 17;
        public const int FontSmall = 14;
        public const int FontTiny = 12;
        public const int FontIcon = 26;

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

        /// <summary>Applies the procedural rounded sprite as a 9-slice so the image scales cleanly.</summary>
        public static void Round(Graphic g, float radius = 12f)
        {
            var img = g as Image;
            if (img == null) return;
            img.sprite = RuntimeVisuals.Rounded(64, Mathf.Clamp((int)radius, 4, 28));
            img.type = Image.Type.Sliced;
        }

        // ================= стеклянный интерфейс =================
        // Скруглённая панель с размытием фона, тонкой светлой кромкой и бликом сверху.

        /// <summary>Материал стекла: размытие фона + скругления из спрайта.</summary>
        public static Material GlassMaterial(float opacity = 1f)
        {
            var mat = new Material(RuntimeVisuals.Glass) { name = "ELEMENTA_GlassUI" };
            if (mat.HasProperty("_BlurStrength")) mat.SetFloat("_BlurStrength", 0.86f);
            if (mat.HasProperty("_BlurBoost")) mat.SetFloat("_BlurBoost", 1.18f);
            if (mat.HasProperty("_Highlight")) mat.SetFloat("_Highlight", 0.10f);
            if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", new Color(0.62f, 0.88f, 1f, 1f));
            mat.color = new Color(1f, 1f, 1f, opacity);
            return mat;
        }

        /// <summary>Скруглённый графический элемент со стеклянным материалом.</summary>
        public static Image Glass(RectTransform host, Color tint, float radius = 18f, int stencil = 0)
        {
            var img = host.GetComponent<Image>();
            if (img == null) img = host.gameObject.AddComponent<Image>();
            img.sprite = RuntimeVisuals.Rounded(96, Mathf.Clamp(Mathf.RoundToInt(radius), 4, 40));
            img.type = Image.Type.Sliced;
            img.color = tint;
            var mat = GlassMaterial(tint.a);
            if (stencil > 0 && mat.HasProperty("_Stencil"))
            {
                mat.SetFloat("_Stencil", stencil);
                mat.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
            }
            img.material = mat;
            return img;
        }

        /// <summary>Тонкая светлая кромка по контуру — «грань стекла».</summary>
        public static Image Bevel(RectTransform host, float radius = 18f, float thickness = 1.5f, Color? color = null)
        {
            var go = new GameObject("Bevel", typeof(RectTransform));
            go.transform.SetParent(host, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = RuntimeVisuals.Ring(96, Mathf.Clamp(Mathf.RoundToInt(radius), 4, 40), Mathf.Max(1, Mathf.RoundToInt(thickness)));
            img.type = Image.Type.Sliced;
            img.color = color ?? Theme.Stroke;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Мягкий блик в верхней части панели — отражение света на стекле.</summary>
        public static void Sheen(RectTransform host, float height = 0.42f, float strength = 0.10f, float radius = 18f)
        {
            var go = new GameObject("Sheen", typeof(RectTransform));
            go.transform.SetParent(host, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f - height);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            var img = go.AddComponent<Image>();
            img.sprite = RuntimeVisuals.Rounded(64, Mathf.Clamp(Mathf.RoundToInt(radius * 0.8f), 4, 28));
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, strength);
            img.raycastTarget = false;
            img.material = GlassMaterial(1f);
        }

        /// <summary>Стеклянная панель: тёмное стекло + кромка + блик. Основной контейнер интерфейса.</summary>
        public static RectTransform GlassPanel(Transform parent, string name, float radius = 20f, Color? fill = null, bool sheen = true)
        {
            var rt = Box(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Glass(rt, fill ?? Theme.Panel, radius);
            Bevel(rt, radius, 1.5f);
            if (sheen) Sheen(rt, 0.45f, 0.09f, radius);
            return rt;
        }

        /// <summary>Совместимость: прежнее имя стеклянной панели.</summary>
        public static RectTransform SteelPanel(Transform parent, string name, float border = 2f, Color? fill = null)
            => GlassPanel(parent, name, 20f, fill);

        /// <summary>Рамка-контур (для вложенных блоков). Тонкая, скруглённая.</summary>
        public static Image Frame(RectTransform host, float thickness = 1.5f, Color? color = null)
            => Bevel(host, 16f, thickness, color);

        /// <summary>
        /// Скруглённая кнопка-иконка со значком и подписью. Выбранная подсвечивается
        /// неоновым акцентом и мягким свечением под ней.
        /// </summary>
        public static Button IconButton(Transform parent, string icon, string caption, Vector2 size,
                                        Action onClick, int iconSize = Theme.FontIcon, int captionSize = Theme.FontTiny)
        {
            var go = new GameObject("Icon_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;

            var glow = new GameObject("Glow", typeof(RectTransform));
            glow.transform.SetParent(rt, false);
            var grt = (RectTransform)glow.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-8f, -8f); grt.offsetMax = new Vector2(8f, 8f);
            var gimg = glow.AddComponent<Image>();
            gimg.sprite = RuntimeVisuals.RadialSprite();
            gimg.color = new Color(0.3f, 0.8f, 1f, 0f);
            gimg.raycastTarget = false;

            var bg = Glass(rt, new Color(0.13f, 0.16f, 0.22f, 0.5f), 14f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Bevel(rt, 14f, 1.5f);

            bool hasCaption = !string.IsNullOrEmpty(caption);
            if (!string.IsNullOrEmpty(icon))
            {
                var gl = Label(rt, icon, iconSize, Theme.Text, hasCaption ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter);
                SetAnchors((RectTransform)gl.transform,
                    hasCaption ? new Vector2(0f, 1f) : Vector2.zero, Vector2.one,
                    hasCaption ? new Vector2(0f, -size.y * 0.58f) : Vector2.zero,
                    hasCaption ? new Vector2(0f, -3f) : Vector2.zero);
            }
            if (hasCaption)
            {
                var cl = Label(rt, caption, captionSize, Theme.TextDim, TextAnchor.LowerCenter);
                SetAnchors((RectTransform)cl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(2f, 4f), new Vector2(-2f, size.y * 0.40f));
            }
            return btn;
        }

        /// <summary>Стеклянная плитка предмета: превью в скруглённом слоте + подпись.</summary>
        public static Button Tile(Transform parent, string caption, Color swatch, Vector2 cell,
                                  Action onClick, string badge = null, bool selected = false, string glyph = null)
        {
            var go = new GameObject("Tile_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tileRt = (RectTransform)go.transform;
            var bg = Glass(tileRt, selected ? new Color(0.32f, 0.62f, 0.78f, 0.55f) : Theme.Card, 14f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.3f, 1.4f, 1f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.9f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Bevel(tileRt, 14f, selected ? 2f : 1.5f,
                selected ? new Color(0.55f, 0.92f, 1f, 0.9f) : Theme.Stroke);

            // превью материала: скруглённый «образец» с блик по верхней кромке
            var head = Mathf.Round(cell.y * 0.46f);
            var chip = Box(go.transform, "Chip", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(6f, -head - 4f), new Vector2(-6f, -6f));
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.sprite = RuntimeVisuals.Rounded(48, 10);
            chipImg.type = Image.Type.Sliced;
            chipImg.color = swatch;
            chipImg.raycastTarget = false;
            var gloss = Box(chip, "Gloss", new Vector2(0.06f, 0.55f), new Vector2(0.94f, 0.95f), Vector2.zero, Vector2.zero);
            var glossImg = gloss.gameObject.AddComponent<Image>();
            glossImg.sprite = RuntimeVisuals.Rounded(48, 10);
            glossImg.type = Image.Type.Sliced;
            glossImg.color = new Color(1f, 1f, 1f, 0.16f);
            glossImg.raycastTarget = false;

            if (!string.IsNullOrEmpty(glyph))
            {
                var gl = Label(chip, glyph, Theme.FontIcon, new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleCenter);
                SetAnchors((RectTransform)gl.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            var cap = Label(go.transform, caption, Theme.FontTiny, Theme.Text, TextAnchor.UpperCenter);
            cap.fontStyle = FontStyle.Bold;
            SetAnchors((RectTransform)cap.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, 3f), new Vector2(-4f, -head - 6f));

            if (!string.IsNullOrEmpty(badge))
            {
                var b = Label(go.transform, badge, 10, Theme.TextDim, TextAnchor.UpperLeft);
                SetAnchors((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(8f, -18f), new Vector2(40f, -5f));
            }
            return btn;
        }

        /// <summary>Вид выбранной кнопки: акцентная заливка, кромка и свечение.</summary>
        public static void SetSelected(Button btn, bool selected)
        {
            if (btn == null) return;
            var bg = btn.GetComponent<Image>();
            if (bg != null) bg.color = selected ? new Color(0.32f, 0.68f, 0.85f, 0.62f) : new Color(0.13f, 0.16f, 0.22f, 0.5f);

            var bevel = btn.transform.Find("Bevel");
            if (bevel != null)
            {
                var bi = bevel.GetComponent<Image>();
                if (bi != null) bi.color = selected ? new Color(0.6f, 0.95f, 1f, 0.95f) : Theme.Stroke;
            }

            var glow = btn.transform.Find("Glow");
            if (glow != null)
            {
                var gi = glow.GetComponent<Image>();
                if (gi != null) gi.color = selected ? new Color(0.30f, 0.82f, 1f, 0.42f) : new Color(0.3f, 0.8f, 1f, 0f);
            }

            foreach (var t in btn.GetComponentsInChildren<Text>(true))
            {
                bool isIcon = t.fontSize >= Theme.FontIcon - 4;
                t.color = selected ? Theme.Text : (isIcon ? Theme.Text : Theme.TextDim);
            }
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

        public static Button Button(Transform parent, string caption, Vector2 size, Action onClick,
                                    Color? baseColor = null, int fontSize = Theme.FontBody, float radius = 14f)
        {
            var go = new GameObject("Button_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;

            // стеклянная скруглённая кнопка с бликом по верхней кромке
            var img = Glass(rt, baseColor ?? Theme.PanelLight, radius);
            Bevel(rt, radius, 1.4f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.4f, 1f);
            colors.pressedColor = new Color(0.75f, 0.78f, 0.85f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            colors.fadeDuration = 0.07f;
            btn.colors = colors;

            var label = Label(go.transform, caption, fontSize, Theme.Text, TextAnchor.MiddleCenter);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 3f); lrt.offsetMax = new Vector2(-8f, -3f);
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
            var fitter = contentRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

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
