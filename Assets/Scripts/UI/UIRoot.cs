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
            var bar = UIFactory.Box(Root, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -58f), Vector2.zero, Theme.Bg);
            var title = UIFactory.Label(bar, "ELEMENTA", Theme.FontTitle, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(22f, 0f), new Vector2(240f, 0f));
            TopTitle = title;

            var sub = UIFactory.Label(bar, "физика · химия · оружие", Theme.FontSmall, Theme.TextDim);
            UIFactory.SetAnchors((RectTransform)sub.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(150f, 0f), new Vector2(420f, 0f));
            _desktopOnly.Add(sub.gameObject);

            FpsText = UIFactory.Label(bar, "60 FPS", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleRight);
            UIFactory.SetAnchors((RectTransform)FpsText.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-330f, 0f), new Vector2(-250f, 0f));

            StatusText = UIFactory.Label(bar, "", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleRight);
            UIFactory.SetAnchors((RectTransform)StatusText.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-250f, 0f), new Vector2(-10f, 0f));

            float x = 260f;
            AddTopButton(bar, "Пауза", ref x, () => TogglePause());
            AddTopButton(bar, "Сброс", ref x, () => { Game.I.World.ResetWorld(); Log("Мир сброшен."); });
            AddTopButton(bar, "Очистить", ref x, () => { Game.I.World.ClearAll(); Log("Сцена очищена."); });
            AddTopButton(bar, "Гравитация", ref x, () => { var w = Game.I.World; w.SetGravity(!w.GravityEnabled); Log("Гравитация: " + (w.GravityEnabled ? "вкл" : "выкл")); });
            AddTopButton(bar, "Инвентарь", ref x, () => ToggleInventory());
            AddTopButton(bar, "Справочник", ref x, () => ToggleCatalog());
            AddTopButton(bar, "Сохранить", ref x, () =>
            {
                string path = SaveSystem.Save("quick_" + System.DateTime.Now.ToString("HHmmss"));
                Log(path != null ? "Быстрое сохранение готово" : "Не удалось сохранить");
            });
            AddTopButton(bar, "Загрузить", ref x, () =>
            {
                var latest = SaveSystem.LatestSave();
                if (latest == null) Log("Сохранений пока нет");
                else SaveSystem.Load(latest);
            });
            AddTopButton(bar, "Звук", ref x, () => { var a = AudioSystem.I; if (a != null) { a.SetMuted(!a.Muted); Log(a.Muted ? "Звук выключен" : "Звук включён"); } });
            AddTopButton(bar, "Помощь", ref x, () => ShowHelp());
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

        // ---------------- toolbar (легаси, скрыт: инструменты теперь в инвентаре) ----------------
        void BuildToolbar()
        {
            var panel = UIFactory.Panel(Root, "Toolbar", Theme.Panel, 14f);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, -250f), new Vector2(96f, 250f));
            ToolbarPanel = panel;

            var v = UIFactory.VLayout(panel, 6f, new RectOffset(6, 6, 10, 10));
            v.childAlignment = TextAnchor.UpperCenter;

            var header = UIFactory.Label(panel, "ИНСТРУМЕНТ", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleCenter);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            var tools = new (string, ToolId, string)[]
            {
                ("Курсор", ToolId.Select, "Выбор и перетаскивание объектов"),
                ("Материал", ToolId.Spawn, "Создать объект из материала"),
                ("Жидкость", ToolId.Fluid, "Налить вещество в контейнер"),
                ("Оружие", ToolId.Weapon, "Стрельба выбранным оружием"),
                ("Огонь", ToolId.Fire, "Поджечь объект"),
                ("Жар", ToolId.Heat, "Нагреть/охладить объект"),
                ("Молот", ToolId.Hammer, "Нанести физический удар"),
                ("НПС", ToolId.Npc, "Создать персонажа"),
                ("Разрушить", ToolId.Delete, "Удалить объект"),
                ("Время", ToolId.Grab, "Замедление времени"),
            };

            foreach (var (caption, id, tip) in tools)
            {
                var btn = UIFactory.Button(panel, caption, new Vector2(74f, 40f), () => Game.I.World.GetComponent<PlayerController>().SelectTool(id), Theme.Card, Theme.FontSmall);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 40f;
                btn.gameObject.AddComponent<ToolButtonMarker>().Tool = id;
                var tipGo = btn.gameObject.AddComponent<SimpleTooltip>();
                tipGo.Text = tip;
            }
            // панель инвентаря полностью заменяет этот список
            panel.gameObject.SetActive(false);
        }

        // ---------------- catalog ----------------
        void BuildCatalog()
        {
            var panel = UIFactory.Panel(Root, "Catalog", Theme.Panel, 14f);
            UIFactory.SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-360f, 62f), new Vector2(-14f, -70f));
            CatalogPanel = panel;
            var catalog = panel.gameObject.AddComponent<CatalogView>();
            catalog.Build(this, panel);
            panel.gameObject.SetActive(_catalogOpen);
        }

        // ---------------- info panel ----------------
        void BuildInfoPanel()
        {
            var panel = UIFactory.Panel(Root, "Info", Theme.Panel, 12f);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 62f), new Vector2(420f, 190f));
            InfoPanel = panel;
            var label = UIFactory.Label(panel, "Ничего не выбрано.\nНаведите курсор и нажмите ЛКМ", Theme.FontSmall, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-10f, -8f));
            SelectionText = label;
        }

        void BuildConsole()
        {
            var panel = UIFactory.Panel(Root, "Console", Theme.Bg, 12f);
            UIFactory.SetAnchors(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(432f, 16f), new Vector2(-378f, 130f));
            ConsolePanel = panel;
            ConsoleText = UIFactory.Label(panel, "", Theme.FontSmall, Theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetAnchors((RectTransform)ConsoleText.transform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -6f));
        }

        Text _weaponText;
        void BuildWeaponHud()
        {
            var panel = UIFactory.Panel(Root, "WeaponHud", Theme.Panel, 12f);
            UIFactory.SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-372f, 16f), new Vector2(-14f, 118f));
            _weaponText = UIFactory.Label(panel, "", Theme.FontSmall, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)_weaponText.transform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-10f, -8f));
        }

        void BuildMobileControls()
        {
            var panel = UIFactory.Box(Root, "MobileBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 58f), Theme.Bg);
            var h = UIFactory.HLayout(panel, 8f, new RectOffset(12, 12, 9, 9));
            h.childAlignment = TextAnchor.MiddleCenter;
            foreach (var (caption, action) in new (string, Action)[]
            {
                ("Инвентарь", () => ToggleInventory()),
                ("Предмет", () => Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Spawn)),
                ("Оружие", () => Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Weapon)),
                ("Огонь", () => Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Fire)),
                ("НПС", () => Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Npc)),
                ("Пауза", () => TogglePause()),
            })
            {
                var b = UIFactory.Button(panel, caption, new Vector2(0f, 40f), action, Theme.PanelLight, Theme.FontSmall);
                b.gameObject.AddComponent<LayoutElement>().preferredWidth = 118f;
            }
            panel.gameObject.SetActive(IsMobileLayout);
        }

        public void ToggleInventory()
        {
            if (InventoryPanel == null) return;
            InventoryPanel.gameObject.SetActive(!InventoryPanel.gameObject.activeSelf);
        }

        public void ToggleCatalog()
        {
            _catalogOpen = !_catalogOpen;
            CatalogPanel.gameObject.SetActive(_catalogOpen);
        }

        public void ToggleToolbar()
        {
            ToolbarPanel.gameObject.SetActive(!ToolbarPanel.gameObject.activeSelf);
        }

        void TogglePause()
        {
            var w = Game.I.World;
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

        /// <summary>Reflows the interface once for small/touch screens.</summary>
        public void ApplyMobileLayout()
        {
            if (_mobileLayoutApplied) return;
            _mobileLayoutApplied = true;
            IsMobileLayout = true;

            // compact captions so the horizontal sheet fits a phone in portrait
            var shortNames = new System.Collections.Generic.Dictionary<ToolId, string>
            {
                { ToolId.Select, "Кур" }, { ToolId.Spawn, "Мат" }, { ToolId.Fluid, "Жид" },
                { ToolId.Weapon, "Стрел" }, { ToolId.Fire, "Подж" }, { ToolId.Heat, "Жар" },
                { ToolId.Hammer, "Мол" }, { ToolId.Npc, "НПС" }, { ToolId.Delete, "Дел" },
                { ToolId.Grab, "Вр" },
            };
            for (int i = 0; i < ToolbarPanel.childCount; i++)
            {
                var child = ToolbarPanel.GetChild(i) as RectTransform;
                if (child == null) continue;
                var marker = child.GetComponent<ToolButtonMarker>();
                var le = child.GetComponent<LayoutElement>();
                if (le != null) Destroy(le);
                if (marker == null)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }
                child.sizeDelta = new Vector2(54f, 44f);
                child.anchoredPosition = Vector2.zero;
                var text = child.GetComponentInChildren<Text>();
                if (text != null && shortNames.TryGetValue(marker.Tool, out var shortName)) text.text = shortName;
            }

            // на телефоне инвентарь занимал бы весь экран — вместо него нижняя полоса
            if (InventoryPanel != null) InventoryPanel.gameObject.SetActive(false);

            // left toolbar is replaced by the icon strip; keep it hidden by default
            UIFactory.SetAnchors(ToolbarPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 62f), new Vector2(-12f, 118f));
            ToolbarPanel.gameObject.SetActive(false);
            var vl = ToolbarPanel.GetComponent<VerticalLayoutGroup>();
            if (vl != null) Destroy(vl);
            var h = ToolbarPanel.gameObject.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = ToolbarPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.padding = new RectOffset(6, 6, 6, 6);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            // catalog becomes a slide-over sheet occupying most of the screen
            UIFactory.SetAnchors(CatalogPanel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-330f, 122f), new Vector2(-10f, -64f));

            // info + console collapse to small strips
            UIFactory.SetAnchors(InfoPanel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 122f), new Vector2(330f, 210f));
            if (ConsolePanel != null) ConsolePanel.gameObject.SetActive(false);
            var weaponHud = Root.Find("WeaponHud") as RectTransform;
            if (weaponHud != null)
                UIFactory.SetAnchors(weaponHud, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 66f), new Vector2(-10f, 160f));
            CatalogPanel.gameObject.SetActive(false);
        }

        /// <summary>Vertical icon strip for phones: one icon per tool, no text.</summary>
        void BuildMobileToolStrip()
        {
            var strip = UIFactory.Box(Root, "MobileToolStrip", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 190f), new Vector2(64f, -70f));
            var v = UIFactory.VLayout((RectTransform)strip, 4f, new RectOffset(4, 4, 6, 6));
            v.childAlignment = TextAnchor.UpperCenter;
            var icons = new (string, ToolId)[]
            {
                ("◈", ToolId.Select), ("▣", ToolId.Spawn), ("◍", ToolId.Fluid),
                ("➤", ToolId.Weapon), ("✷", ToolId.Fire), ("♨", ToolId.Heat),
                ("⚒", ToolId.Hammer), ("☺", ToolId.Npc), ("✖", ToolId.Delete), ("◷", ToolId.Grab),
            };
            foreach (var (icon, tool) in icons)
            {
                var t = tool;
                var btn = UIFactory.Button(strip, icon, new Vector2(48f, 48f), () => Game.I.World.GetComponent<PlayerController>().SelectTool(t), Theme.Card, 22);
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
                var marker = btn.gameObject.AddComponent<ToolButtonMarker>();
                marker.Tool = t;
            }
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
