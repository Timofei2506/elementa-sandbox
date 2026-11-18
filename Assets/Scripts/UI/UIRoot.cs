using System;
using System.Collections.Generic;
using System.Text;
using Elementa.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Root of the in-game interface. Builds the HUD, toolbar, catalog and info panels,
    /// and reflows them for desktop (mouse + keyboard) or mobile (touch).
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }
        public bool BlocksCameraInput { get; private set; }
        public bool IsMobileLayout { get; private set; }

        public RectTransform CatalogPanel { get; private set; }
        public RectTransform ToolbarPanel { get; private set; }
        public RectTransform InfoPanel { get; private set; }
        public RectTransform ConsolePanel { get; private set; }
        public Text ConsoleText { get; private set; }
        public Text StatusText { get; private set; }
        public Text SelectionText { get; private set; }
        public Text FpsText { get; private set; }
        public Text TopTitle { get; private set; }

        readonly List<GameObject> _desktopOnly = new List<GameObject>();
        readonly StringBuilder _log = new StringBuilder();
        float _fpsTimer;
        int _frames;
        bool _catalogOpen = false;

        public static UIRoot Create()
        {
            // ВАЖНО: объект под Canvas обязан сразу иметь RectTransform, иначе
            // (RectTransform)go.transform ниже падает с InvalidCastException.
            var go = new GameObject("UI", typeof(RectTransform));
            var canvas = UIFactory.CreateCanvas("ELEMENTA_Canvas", 100);
            go.transform.SetParent(canvas.transform, false);
            var ui = go.AddComponent<UIRoot>();
            ui.Canvas = canvas;
            ui.Root = go.GetComponent<RectTransform>();
            if (ui.Root == null) ui.Root = go.AddComponent<RectTransform>();
            UIFactory.SetAnchors(ui.Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.Build();
            return ui;
        }

        void Build()
        {
            IsMobileLayout = Game.I != null && Game.I.IsMobile;
            // Каждый блок интерфейса собирается отдельно: если один упал, остальные
            // всё равно появятся, а причина будет видна в диагностике (ErrorOverlay).
            Safe("TopBar", BuildTopBar);
            Safe("Toolbar", BuildToolbar);
            Safe("Inventory", BuildInventory);
            Safe("Catalog", BuildCatalog);
            Safe("Info", BuildInfoPanel);
            Safe("Console", BuildConsole);
            Safe("WeaponHud", BuildWeaponHud);
            Safe("MobileControls", BuildMobileControls);
            Log("ELEMENTA · " + Game.I.LoadedMaterials + " материалов, " + Game.I.LoadedSubstances + " веществ, "
                + Game.I.LoadedReactions + " реакций, " + Game.I.LoadedWeapons + " единиц оружия.");
            Log("ЛКМ — применить инструмент · колесо — зум · ПКМ/СКМ — двигать камеру · WASD — панорама.");
            if (IsMobileLayout || Screen.width < 1100) Safe("MobileLayout", ApplyMobileLayout);
            if (IsMobileLayout) Safe("MobileToolStrip", BuildMobileToolStrip);
        }

        static void Safe(string name, System.Action action)
        {
            try { action(); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                ErrorOverlay.Report("UI/" + name + ": " + e.Message);
            }
        }

        // ---------------- top bar ----------------
        void BuildTopBar()
        {
            // Чёрная полоса с белой линией снизу: только заголовок, счётчики и кнопки-иконки.
            var bar = UIFactory.Box(Root, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), Vector2.zero);
            bar.gameObject.AddComponent<Image>().color = Theme.Bg;
            var line = UIFactory.Box(bar, "Line", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));
            line.gameObject.AddComponent<Image>().color = Theme.StrokeDim;

            TopTitle = UIFactory.Label(bar, "ELEMENTA", Theme.FontHeader, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchors((RectTransform)TopTitle.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(14f, 0f), new Vector2(190f, 0f));

            FpsText = UIFactory.Label(bar, "60", Theme.FontSmall, Theme.TextDim, TextAnchor.MiddleRight);
            UIFactory.SetAnchors((RectTransform)FpsText.transform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-150f, 0f), new Vector2(-86f, 0f));

            StatusText = UIFactory.Label(bar, "", Theme.FontSmall, Theme.TextDim, TextAnchor.MiddleRight);
            UIFactory.SetAnchors((RectTransform)StatusText.transform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-470f, 0f), new Vector2(-158f, 0f));

            // Кнопки справа: крупные квадратные иконки, как в Melon
            float x = -10f;
            void TopIcon(string icon, string tip, Action action)
            {
                var btn = UIFactory.IconButton(bar, icon, null, new Vector2(46f, 44f), action, 22);
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(x - 23f, 0f);
                x -= 50f;
                var label = UIFactory.Label(rt, tip, 9, new Color(0.6f, 0.62f, 0.66f, 1f), TextAnchor.LowerCenter);
                UIFactory.SetAnchors((RectTransform)label.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 2f), new Vector2(0f, 14f));
            }

            TopIcon("?", "ПОМОЩЬ", () => ShowHelp());
            TopIcon("♪", "ЗВУК", () =>
            {
                var a = AudioSystem.I;
                if (a == null) return;
                a.SetMuted(!a.Muted);
                Log(a.Muted ? "Звук выключен" : "Звук включён");
            });
            TopIcon("▤", "СПРАВОЧНИК", () => ToggleCatalog());
            TopIcon("◎", "ГРАВИТАЦИЯ", () =>
            {
                var w = Game.I?.World;
                if (w == null) return;
                w.SetGravity(!w.GravityEnabled);
                Log("Гравитация: " + (w.GravityEnabled ? "вкл" : "выкл"));
            });
            TopIcon("⌫", "ОЧИСТИТЬ", () => { Game.I?.World?.ClearAll(); Log("Сцена очищена."); });
            TopIcon("↺", "СБРОС", () => { Game.I?.World?.ResetWorld(); Log("Мир сброшен."); });
            TopIcon("⏸", "ПАУЗА", () => TogglePause());
        }

        /// <summary>Full-screen help overlay listing every control.</summary>
        public void ShowHelp()
        {
            if (_help != null) { Destroy(_help.gameObject); _help = null; return; }
            var overlay = UIFactory.Box(Root, "Help", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
            _help = overlay;
            var panel = UIFactory.Panel(overlay, "HelpPanel", Theme.Panel);
            UIFactory.SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -260f), new Vector2(330f, 260f));
            var title = UIFactory.Label(panel, "УПРАВЛЕНИЕ", Theme.FontHeader, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -46f), new Vector2(0f, -8f));
            string text =
                "<b>Мышь</b>\n" +
                "ЛКМ — применить инструмент · ПКМ/СКМ — двигать сцену · колесо — зум\n" +
                "WASD / стрелки — панорама · Q/E — зум · F — сбросить вид\n\n" +
                "<b>Горячие клавиши</b>\n" +
                "1 Рука · 2 Предмет · 3 Жидкость · 4 Оружие · 5 Огонь\n" +
                "6 Жар · 7 Молот · 8 Персонаж · 9 Гравипушка · 0 Ластик\n" +
                "Space — пауза · R — сброс · Z/X — оружие · [ ] — размер · Enter — перезарядка\n" +
                "ПКМ/СКМ — двигать сцену · колесо — зум · F — сбросить вид\n\n" +
                "<b>Телефон</b>\n" +
                "Один палец — двигать сцену · два пальца — зум · тап — инструмент\n" +
                "Нижняя полоса — инструменты и инвентарь\n\n" +
                "<i>Совет: термит + магний даёт расплав стали, белизна + кислота — хлор.</i>";
            var body = UIFactory.Label(panel, text, Theme.FontBody, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)body.transform, Vector2.zero, Vector2.one, new Vector2(22f, 60f), new Vector2(-22f, -54f));
            body.lineSpacing = 1.15f;
            var close = UIFactory.Button(panel, "Закрыть", new Vector2(140f, 38f), () => { Destroy(overlay.gameObject); _help = null; }, Theme.PanelLight);
            UIFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-70f, 12f), new Vector2(70f, 50f));
        }
        RectTransform _help;

        const float TopButtonWidth = 86f;
        void AddTopButton(RectTransform bar, string caption, ref float x, Action action)
        {
            var btn = UIFactory.Button(bar, caption, new Vector2(TopButtonWidth, 34f), action, Theme.PanelLight, Theme.FontTiny);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x + TopButtonWidth * 0.5f, 0f);
            x += TopButtonWidth + 5f;
        }

        // ---------------- inventory (Melon-style) ----------------
        void BuildInventory()
        {
            InventoryPanel = InventoryPanel.Create(this, Root);
        }

        public InventoryPanel InventoryPanel { get; private set; }

        /// <summary>Легаси-колонка инструментов: заменена панелью инвентаря, оставлена скрытой.</summary>
        void BuildToolbar()
        {
            var panel = UIFactory.SteelPanel(Root, "Toolbar", 2f);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, -250f), new Vector2(96f, 250f));
            ToolbarPanel = panel;
            panel.gameObject.SetActive(false);
        }

        // ---------------- catalog ----------------
        void BuildCatalog()
        {
            var panel = UIFactory.SteelPanel(Root, "Catalog", 2f);
            UIFactory.SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-378f, 120f), new Vector2(-10f, -66f));
            CatalogPanel = panel;
            var catalog = panel.gameObject.AddComponent<CatalogView>();
            catalog.Build(this, panel);
            panel.gameObject.SetActive(_catalogOpen);
        }

        // ---------------- info panel: карточка выбранного объекта ----------------
        void BuildInfoPanel()
        {
            var panel = UIFactory.SteelPanel(Root, "Info", 2f);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 108f), new Vector2(430f, 232f));
            InfoPanel = panel;

            var cap = UIFactory.Label(panel, "ВЫБРАННЫЙ ОБЪЕКТ", Theme.FontTiny, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)cap.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -24f), new Vector2(-10f, -4f));

            var label = UIFactory.Label(panel, "Ничего не выбрано.\nНаведите курсор на объект.", Theme.FontSmall, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)label.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(10f, 8f), new Vector2(-10f, -28f));
            SelectionText = label;
        }

        // ---------------- консоль событий: нижняя треть экрана ----------------
        void BuildConsole()
        {
            var panel = UIFactory.SteelPanel(Root, "Console", 2f, Theme.Bg);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(440f, 12f), new Vector2(-390f, 108f));
            ConsolePanel = panel;
            ConsoleText = UIFactory.Label(panel, "", Theme.FontSmall, Theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetAnchors((RectTransform)ConsoleText.transform, Vector2.zero, Vector2.one,
                new Vector2(12f, 8f), new Vector2(-12f, -6f));
        }

        Text _weaponText;
        void BuildWeaponHud()
        {
            var panel = UIFactory.SteelPanel(Root, "WeaponHud", 2f);
            UIFactory.SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-380f, 12f), new Vector2(-10f, 108f));
            _weaponText = UIFactory.Label(panel, "", Theme.FontSmall, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)_weaponText.transform, Vector2.zero, Vector2.one,
                new Vector2(12f, 8f), new Vector2(-10f, -8f));
        }

        // ---------------- мобильная нижняя полоса ----------------
        void BuildMobileControls()
        {
            var panel = UIFactory.Box(Root, "MobileBar", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 76f));
            panel.gameObject.AddComponent<Image>().color = Theme.Bg;
            UIFactory.Frame(panel, 2f);
            var h = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.padding = new RectOffset(6, 6, 6, 6);
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;

            var items = new (string icon, string caption, Action action)[]
            {
                ("▤", "МЕНЮ", () => ToggleInventory()),
                ("▣", "ПРЕДМЕТ", () => SelectTool(ToolId.Spawn)),
                ("➤", "ОРУЖИЕ", () => SelectTool(ToolId.Weapon)),
                ("✷", "ОГОНЬ", () => SelectTool(ToolId.Fire)),
                ("☺", "ЛЮДИ", () => SelectTool(ToolId.Npc)),
                ("✥", "РУКА", () => SelectTool(ToolId.Select)),
                ("⏸", "ПАУЗА", () => TogglePause()),
            };
            foreach (var (icon, caption, action) in items)
            {
                var a = action;
                var b = UIFactory.IconButton(panel, icon, caption, new Vector2(74f, 62f), () => a(), 22, 10);
                b.gameObject.AddComponent<LayoutElement>().preferredWidth = 74f;
            }
            panel.gameObject.SetActive(IsMobileLayout);
            _mobileBar = panel;
        }
        RectTransform _mobileBar;

        static void SelectTool(ToolId id)
        {
            var pc = Game.I?.World != null ? Game.I.World.GetComponent<PlayerController>() : null;
            pc?.SelectTool(id);
        }

        public void ToggleInventory()
        {
            if (InventoryPanel == null) return;
            InventoryPanel.gameObject.SetActive(!InventoryPanel.gameObject.activeSelf);
        }

        public void ToggleCatalog()
        {
            _catalogOpen = !_catalogOpen;
            if (CatalogPanel != null) CatalogPanel.gameObject.SetActive(_catalogOpen);
        }

        public void ToggleToolbar()
        {
            if (ToolbarPanel != null) ToolbarPanel.gameObject.SetActive(!ToolbarPanel.gameObject.activeSelf);
        }

        public void TogglePause()
        {
            var w = Game.I?.World;
            if (w == null) return;
            w.TimeScale = w.TimeScale > 0.01f ? 0f : 1f;
            Log(w.TimeScale > 0.01f ? "Симуляция продолжена." : "Симуляция на паузе.");
        }

        public void Log(string message)
        {
            string line = "› " + message + "\n";
            _log.Append(line);
            if (_log.Length > 1200) _log.Remove(0, _log.Length - 1200);
            if (ConsoleText != null) ConsoleText.text = _log.ToString();
        }

        public void ReportReaction(string text)
        {
            Log(text);
        }

        bool _mobileLayoutApplied;

        /// <summary>Раскладка для телефона: инвентарь на весь экран, панели по краям, нижняя полоса.</summary>
        public void ApplyMobileLayout()
        {
            if (_mobileLayoutApplied) return;
            _mobileLayoutApplied = true;
            IsMobileLayout = true;

            // инвентарь раскрывается на весь экран — на телефоне иначе он нечитаем
            if (InventoryPanel != null)
            {
                var rt = (RectTransform)InventoryPanel.transform;
                UIFactory.SetAnchors(rt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 82f), new Vector2(-6f, -50f));
                InventoryPanel.gameObject.SetActive(false);
            }

            // на телефоне постоянной колонки инструментов нет — её заменяет нижняя полоса
            if (ToolbarPanel != null) ToolbarPanel.gameObject.SetActive(false);

            // справочник — выезжающая панель на весь экран
            if (CatalogPanel != null)
            {
                UIFactory.SetAnchors(CatalogPanel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 82f), new Vector2(-6f, -50f));
                CatalogPanel.gameObject.SetActive(false);
            }

            // карточка объекта и консоль прячутся: на телефоне каждый пиксель на счету
            if (InfoPanel != null) InfoPanel.gameObject.SetActive(false);
            if (ConsolePanel != null) ConsolePanel.gameObject.SetActive(false);

            var weaponHud = Root.Find("WeaponHud") as RectTransform;
            if (weaponHud != null)
                UIFactory.SetAnchors(weaponHud, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -104f), new Vector2(-8f, -56f));

            if (_mobileBar != null) _mobileBar.gameObject.SetActive(true);
        }

        /// <summary>Горизонтальная лента инструментов (используется как резервная навигация).</summary>
        void BuildMobileToolStrip()
        {
            BuildMobileControls();
        }

        void Update()
        {
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                float fps = _frames / _fpsTimer;
                _frames = 0; _fpsTimer = 0f;
                if (FpsText != null) FpsText.text = $"{fps:F0} FPS";
            }

            var w = Game.I.World;
            if (w != null && StatusText != null)
            {
                StatusText.text = $"{w.Objects.Count} объектов · {w.Npcs.Count} NPC · x{w.TimeScale:0.##}";
            }

            var pc = w != null ? w.GetComponent<PlayerController>() : null;
            if (pc != null && SelectionText != null)
            {
                SelectionText.text = pc.SelectionDescription;
            }
            if (pc != null && _weaponText != null)
            {
                var weapon = pc.Weapons != null ? pc.Weapons.Current : null;
                if (weapon == null)
                {
                    _weaponText.text = "<b>Инструмент</b>\n" + PlayerController.ToolName(pc.CurrentTool);
                }
                else
                {
                    var st = weapon.Stats;
                    string ammo = weapon.automatic || weapon.magSize > 1 ? $"{pc.Weapons.Ammo}/{weapon.magSize:F0}" : "—";
                    _weaponText.text = $"<b>{weapon.nameRu}</b>\n" +
                                       $"КЭ {st.energyJ:F0} Дж · {weapon.velocity:F0} м/с\n" +
                                       $"Пробитие ~{st.penetrationMM:F0} мм RHA\n" +
                                       $"Патроны {ammo} · {PlayerController.ToolName(pc.CurrentTool)}";
                }
            }

            // hide the catalog when the screen is too narrow for both panels
            if (Screen.width < 900 && !_mobileLayoutApplied) ApplyMobileLayout();
        }

        /// <summary>Called by CatalogView when the pointer is over UI so tools do not fire.</summary>
        public void SetPointerOverUI(bool over)
        {
            BlocksCameraInput = over;
        }
    }

    public class ToolButtonMarker : MonoBehaviour
    {
        public ToolId Tool;
    }

    public class SimpleTooltip : MonoBehaviour
    {
        public string Text;
    }
}
