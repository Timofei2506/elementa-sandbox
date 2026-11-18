using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Панель предметов в духе Melon Sandbox: слева узкая колонка крупных квадратных
    /// кнопок-инструментов, справа сетка плиток с превью. Чёрные панели, жёсткие белые
    /// рамки, жёлтая подсветка выбранного. Всё строится кодом, поэтому панель одинаково
    /// выглядит на ПК и на телефоне.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        public enum Page { Materials, Weapons, Npc, Fluids, Scenes, Shapes }

        const float ToolColWidth = 168f;
        const float PageWidth = 470f;

        UIRoot _ui;
        RectTransform _toolCol, _page, _strip;
        readonly List<Button> _toolButtons = new List<Button>();
        readonly List<(Button btn, Page page)> _tabButtons = new List<(Button, Page)>();
        Text _pageTitle, _footer;
        Page _current = Page.Materials;
        PlayerController Pc => Game.I?.World != null ? Game.I.World.GetComponent<PlayerController>() : null;

        public static InventoryPanel Create(UIRoot ui, RectTransform parent)
        {
            var rt = UIFactory.Box(parent, "Inventory", new Vector2(0f, 0f), new Vector2(0f, 1f),
                                   new Vector2(10f, 110f), new Vector2(10f + ToolColWidth + PageWidth + 22f, -62f));
            var panel = rt.gameObject.AddComponent<InventoryPanel>();
            panel._ui = ui;
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform root)
        {
            // ---- колонка инструментов: чёрная плита с белой рамкой ----
            _toolCol = UIFactory.SteelPanel(root, "ToolCol", 2f);
            UIFactory.SetAnchors(_toolCol, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ToolColWidth, 0f));

            // ---- страница предметов ----
            _page = UIFactory.SteelPanel(root, "Page", 2f, new Color(0.035f, 0.035f, 0.04f, 0.985f));
            UIFactory.SetAnchors(_page, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(ToolColWidth + 8f, 0f), Vector2.zero);

            BuildToolColumn();
            BuildTabs();
            ShowPage(Page.Materials, true);
        }

        // ---------------- колонка инструментов ----------------
        static readonly (ToolId tool, string icon, string caption)[] Tools =
        {
            (ToolId.Select, "✥", "Рука"),
            (ToolId.Spawn,  "▣", "Предмет"),
            (ToolId.Fluid,  "◍", "Жидкость"),
            (ToolId.Weapon, "➤", "Оружие"),
            (ToolId.Fire,   "✷", "Огонь"),
            (ToolId.Heat,   "♨", "Жар"),
            (ToolId.Hammer, "⚒", "Молот"),
            (ToolId.Npc,    "☺", "Люди"),
            (ToolId.Grab,   "◷", "Время"),
            (ToolId.Delete, "✖", "Ластик"),
        };

        void BuildToolColumn()
        {
            var header = UIFactory.Label(_toolCol, "ИНСТРУМЕНТ", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleCenter);
            UIFactory.SetAnchors((RectTransform)header.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(4f, -26f), new Vector2(-4f, -4f));

            var gridRt = UIFactory.Box(_toolCol, "ToolGrid", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.SetAnchors(gridRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 40f), new Vector2(-8f, -30f));
            var g = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(70f, 74f);
            g.spacing = new Vector2(6f, 6f);
            g.padding = new RectOffset(0, 0, 0, 0);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;

            var keys = new Dictionary<ToolId, string>
            {
                { ToolId.Select, "1" }, { ToolId.Spawn, "2" }, { ToolId.Fluid, "3" }, { ToolId.Weapon, "4" },
                { ToolId.Fire, "5" }, { ToolId.Heat, "6" }, { ToolId.Hammer, "7" }, { ToolId.Npc, "8" },
                { ToolId.Grab, "9" }, { ToolId.Delete, "0" },
            };

            foreach (var (tool, icon, caption) in Tools)
            {
                var t = tool;
                var btn = UIFactory.IconButton(gridRt, icon, caption, new Vector2(70f, 74f),
                    () => { Pc?.SelectTool(t); RefreshToolHighlight(); });
                var key = UIFactory.Label(btn.transform, keys.TryGetValue(tool, out var k) ? k : "", 10,
                    new Color(0.55f, 0.56f, 0.6f, 1f), TextAnchor.UpperLeft);
                UIFactory.SetAnchors((RectTransform)key.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(4f, -15f), new Vector2(18f, -3f));
                _toolButtons.Add(btn);
                btn.gameObject.AddComponent<ToolButtonMarker>().Tool = t;
            }

            // нижние кнопки-действия: пауза и сброс — как в Melon «CLEAR EVERYTHING»
            var actions = UIFactory.Box(_toolCol, "ToolActions", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(8f, 8f), new Vector2(-8f, 38f));
            UIFactory.Frame(actions, 2f, Theme.StrokeDim);
            var h = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.padding = new RectOffset(4, 4, 4, 4);
            h.childControlWidth = true;
            h.childForceExpandWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;

            UIFactory.Button(actions, "ПАУЗА", new Vector2(0f, 24f), () => _ui?.TogglePause(), Theme.Slot, Theme.FontTiny);
            UIFactory.Button(actions, "СБРОС", new Vector2(0f, 24f), () =>
            {
                Game.I?.World?.ResetWorld();
                _ui?.Log("Мир сброшен.");
            }, Theme.Slot, Theme.FontTiny);
        }

        void RefreshToolHighlight()
        {
            var pc = Pc;
            for (int i = 0; i < _toolButtons.Count; i++)
            {
                var marker = _toolButtons[i].GetComponent<ToolButtonMarker>();
                bool active = pc != null && marker != null && pc.CurrentTool == marker.Tool;
                UIFactory.SetSelected(_toolButtons[i], active);
            }
        }

        // ---------------- вкладки ----------------
        static readonly (Page page, string caption)[] Tabs =
        {
            (Page.Materials, "ПРЕДМЕТЫ"), (Page.Weapons, "ОРУЖИЕ"), (Page.Npc, "ЛЮДИ"),
            (Page.Fluids, "ЖИДКОСТИ"), (Page.Scenes, "СЦЕНЫ"),
        };

        void BuildTabs()
        {
            _strip = UIFactory.Box(_page, "PageTabs", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -52f), Vector2.zero);
            _strip.gameObject.AddComponent<Image>().color = Theme.Bg;
            UIFactory.Frame(_strip, 2f, Theme.StrokeDim);
            var h = _strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 0f;
            h.padding = new RectOffset(3, 3, 3, 3);
            h.childControlWidth = true;
            h.childForceExpandWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;

            foreach (var (page, caption) in Tabs)
            {
                var p = page;
                var btn = UIFactory.Button(_strip, caption, new Vector2(0f, 42f), () => ShowPage(p), Theme.Slot, Theme.FontTiny);
                _tabButtons.Add((btn, page));
            }

            // полоса-шапка: заголовок выбранной страницы + счётчик
            _pageTitle = UIFactory.Label(_page, "", Theme.FontSmall, Theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.SetAnchors((RectTransform)_pageTitle.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(8f, -74f), new Vector2(-8f, -54f));
        }

        void ShowPage(Page page, bool silent = false)
        {
            _current = page;
            foreach (var (btn, p) in _tabButtons) UIFactory.SetSelected(btn, p == page);
            RefreshToolHighlight();

            for (int i = _page.childCount - 1; i >= 0; i--)
            {
                var child = _page.GetChild(i);
                if (child == _strip || child.name == "PageTabs" || child.name == "Label") continue;
                if (child.GetComponent<PageTabMarker>() != null) continue;
                if (child.name == "Body" || child.name == "Footer" || child.name == "PageTabs") Destroy(child.gameObject);
            }

            var body = UIFactory.Box(_page, "Body", Vector2.zero, Vector2.one,
                new Vector2(4f, 28f), new Vector2(-4f, -78f));
            body.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            body.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _footer = UIFactory.Label(_page, "", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.SetAnchors((RectTransform)_footer.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(8f, 4f), new Vector2(-8f, 26f));

            switch (page)
            {
                case Page.Materials: BuildMaterials(body); break;
                case Page.Weapons: BuildWeapons(body); break;
                case Page.Npc: BuildNpcs(body); break;
                case Page.Fluids: BuildFluids(body); break;
                case Page.Scenes: BuildScenes(body); break;
            }
            if (!silent) _ui?.Log("Панель: " + PageName(page));
        }

        static string PageName(Page p) => p switch
        {
            Page.Materials => "Предметы",
            Page.Weapons => "Оружие",
            Page.Npc => "Люди",
            Page.Fluids => "Жидкости",
            Page.Scenes => "Сцены",
            _ => p.ToString(),
        };

        // ---------------- сетка ---------------- （плитки 3 в ряд, как в Melon）
        RectTransform Grid(Transform parent, int columns, float cellW, float cellH, out ScrollRect scroll)
        {
            var viewport = UIFactory.Box(parent, "Grid", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = UIFactory.Box(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, cellH);
            g.spacing = new Vector2(5f, 5f);
            g.padding = new RectOffset(2, 2, 2, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.06f;
            scroll.scrollSensitivity = 34f;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        void BuildMaterials(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 3, 148f, 104f, out _);
            _pageTitle.text = "ФОРМА И МАТЕРИАЛ";

            // ---- формы предметов — первая строка, как «USERS MODS» в Melon
            var shapes = new (BodyShape shape, string caption, string glyph)[]
            {
                (BodyShape.Box, "Куб", "■"), (BodyShape.Plate, "Плита", "▬"), (BodyShape.Sphere, "Шар", "●"),
                (BodyShape.Cylinder, "Цилиндр", "▮"), (BodyShape.Capsule, "Капсула", "⬮"),
            };
            foreach (var (shape, caption, glyph) in shapes)
            {
                var s = shape;
                bool selected = pc != null && pc.SelectedShape == shape;
                UIFactory.Tile(content, caption, new Color(0.86f, 0.87f, 0.89f), new Vector2(148f, 104f),
                    () => { pc?.SetShape(s); pc?.SelectTool(ToolId.Spawn); ShowPage(Page.Materials); },
                    glyph: glyph, selected: selected);
            }

            int count = 0;
            foreach (var group in Game.I.Materials.All.GroupBy(m => m.category).OrderBy(g => g.Key))
            {
                // заголовок категории занимает всю строку
                var header = UIFactory.Label(content, CategoryName(group.Key).ToUpperInvariant(), Theme.FontTiny,
                    Theme.Accent, TextAnchor.MiddleLeft);
                var le = header.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 148f * 3 + 10f;
                le.preferredHeight = 18f;
                header.gameObject.AddComponent<CategoryRow>();

                foreach (var m in group.OrderByDescending(m => m.hardness))
                {
                    var mat = m;
                    bool selected = pc != null && pc.SelectedMaterial != null && pc.SelectedMaterial.id == mat.id;
                    UIFactory.Tile(content, mat.nameRu, mat.color, new Vector2(148f, 104f),
                        () => { pc?.SetMaterial(mat); pc?.SelectTool(ToolId.Spawn); ShowPage(Page.Materials); },
                        badge: $"{mat.density:F0}", selected: selected);
                    count++;
                }
            }
            _footer.text = $"{count} материалов · колесо — зум · [ ] — размер штампа";
        }

        void BuildWeapons(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 3, 148f, 104f, out _);
            _pageTitle.text = "ОРУЖИЕ";
            int count = 0;
            foreach (var group in Game.I.Weapons.All.GroupBy(w => w.category).OrderBy(g => CategoryOrder(g.Key)))
            {
                var header = UIFactory.Label(content, WeaponCategoryName(group.Key).ToUpperInvariant(), Theme.FontTiny,
                    Theme.Accent, TextAnchor.MiddleLeft);
                header.gameObject.AddComponent<LayoutElement>().preferredWidth = 148f * 3 + 10f;
                header.gameObject.AddComponent<CategoryRow>();

                foreach (var w in group.OrderBy(w => w.massGrams))
                {
                    var weapon = w;
                    bool selected = pc != null && pc.Weapons.Current != null && pc.Weapons.Current.id == weapon.id;
                    UIFactory.Tile(content, weapon.nameRu, WeaponColor(weapon.category), new Vector2(148f, 104f),
                        () => { pc?.SetWeapon(weapon); pc?.SelectTool(ToolId.Weapon); ShowPage(Page.Weapons); },
                        badge: weapon.caliberMM > 0.1f ? $"{weapon.caliberMM:F0}" : null, selected: selected);
                    count++;
                }
            }
            _footer.text = $"{count} единиц · Z/X — переключить · ЛКМ — выстрел";
        }

        static string WeaponCategoryName(string cat) => cat switch
        {
            "melee" => "Ближний бой",
            "thrown" => "Метательное",
            "bow" => "Луки",
            "pistol" => "Пистолеты",
            "smg" => "ПП",
            "rifle" => "Винтовки",
            "shotgun" => "Дробовики",
            "sniper" => "Снайперские",
            "heavy" => "Тяжёлое",
            "explosive" => "Взрывчатка",
            "launcher" => "Гранатомёты",
            "flame" => "Огнемёты",
            _ => cat,
        };

        static Color WeaponColor(string cat) => cat switch
        {
            "melee" => new Color(0.86f, 0.80f, 0.72f),
            "thrown" => new Color(0.92f, 0.84f, 0.55f),
            "bow" => new Color(0.80f, 0.86f, 0.62f),
            "heavy" => new Color(0.90f, 0.66f, 0.42f),
            "energy" => new Color(0.55f, 0.88f, 0.96f),
            "explosive" => new Color(0.95f, 0.55f, 0.42f),
            _ => new Color(0.78f, 0.80f, 0.84f),
        };

        static int CategoryOrder(string cat) => cat switch
        {
            "melee" => 0, "thrown" => 1, "bow" => 2, "pistol" => 3, "smg" => 4,
            "rifle" => 5, "shotgun" => 6, "sniper" => 7, "heavy" => 8, "explosive" => 9,
            "launcher" => 10, "flame" => 11, _ => 12,
        };

        void BuildNpcs(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 2, 226f, 118f, out _);
            _pageTitle.text = "ЖИВЫЕ";
            foreach (var kind in new[] { NpcActor.NpcKind.Human, NpcActor.NpcKind.Heavy, NpcActor.NpcKind.Scientist, NpcActor.NpcKind.Zombie, NpcActor.NpcKind.Robot })
            {
                var k = kind;
                bool selected = pc != null && pc.SelectedNpcKind == kind;
                UIFactory.Tile(content, NpcName(kind), NpcColor(kind), new Vector2(226f, 118f),
                    () => { pc?.SetNpcKind(k); pc?.SelectTool(ToolId.Npc); ShowPage(Page.Npc); },
                    badge: $"{NpcDesc(kind)}", selected: selected, glyph: "☺");
            }
            _footer.text = "Клик по сцене ставит персонажа · рагдолл физический";
        }

        static string NpcName(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => "Человек",
            NpcActor.NpcKind.Heavy => "Здоровяк",
            NpcActor.NpcKind.Scientist => "Учёный",
            NpcActor.NpcKind.Zombie => "Зомби",
            NpcActor.NpcKind.Robot => "Робот",
            _ => k.ToString()
        };

        static string NpcDesc(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => "100 HP",
            NpcActor.NpcKind.Heavy => "320 HP",
            NpcActor.NpcKind.Scientist => "65 HP",
            NpcActor.NpcKind.Zombie => "160 HP",
            NpcActor.NpcKind.Robot => "420 HP",
            _ => ""
        };

        static Color NpcColor(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => new Color(0.86f, 0.72f, 0.58f),
            NpcActor.NpcKind.Heavy => new Color(0.78f, 0.64f, 0.50f),
            NpcActor.NpcKind.Scientist => new Color(0.93f, 0.94f, 0.96f),
            NpcActor.NpcKind.Zombie => new Color(0.58f, 0.72f, 0.48f),
            NpcActor.NpcKind.Robot => new Color(0.66f, 0.70f, 0.78f),
            _ => Color.gray
        };

        void BuildFluids(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 3, 148f, 104f, out _);
            _pageTitle.text = "ЖИДКОСТИ И ВЕЩЕСТВА";
            int count = 0;
            foreach (var s in Game.I.Substances.All.OrderBy(s => s.phase).ThenBy(s => s.nameRu))
            {
                var sub = s;
                bool selected = pc != null && pc.SelectedSubstance != null && pc.SelectedSubstance.id == sub.id;
                bool solid = sub.phase == Sim.Phase.Solid;
                UIFactory.Tile(content, sub.nameRu, sub.color, new Vector2(148f, 104f),
                    () => { pc?.SetSubstance(sub); pc?.SelectTool(ToolId.Fluid); ShowPage(Page.Fluids); },
                    badge: solid ? "порошок" : sub.formula, selected: selected, glyph: solid ? "▨" : "◍");
                count++;
            }
            _footer.text = $"{count} веществ · клик по ёмкости — налить, по пустому месту — колба";
        }

        void BuildScenes(RectTransform parent)
        {
            var content = Grid(parent, 1, 452f, 86f, out _);
            _pageTitle.text = "СЦЕНЫ";
            foreach (var scenario in ScenarioLibrary.All())
            {
                var sc = scenario;
                UIFactory.Tile(content, sc.nameRu, Theme.AccentDim, new Vector2(452f, 86f), () =>
                {
                    Game.I.World.ClearAll();
                    sc.Build(Game.I.World);
                    Game.I.CameraRig.ResetView();
                    _ui?.Log("Сцена: " + sc.nameRu);
                }, badge: sc.description);
            }
            _footer.text = "Сцена заменяет всё содержимое мира";
        }

        static string CategoryName(string cat) => cat switch
        {
            "metal" => "Металлы",
            "ceramic" => "Керамика",
            "mineral" => "Минералы",
            "polymer" => "Полимеры",
            "wood" => "Дерево",
            "organic" => "Органика",
            _ => cat,
        };

        public void Update()
        {
            var pc = Pc;
            if (pc == null) return;
            RefreshToolHighlight();
        }
    }

    public class PageTabMarker : MonoBehaviour
    {
        public InventoryPanel.Page Page;
    }

    /// <summary>Маркер строки-заголовка категории внутри сетки (чтобы не удалить её при смене страницы).</summary>
    public class CategoryRow : MonoBehaviour { }
}
