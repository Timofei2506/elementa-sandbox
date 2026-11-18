using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Левая вертикальная панель Melon-стиля: колонка инструментов + сменные панели
    /// (предметы, оружие, персонажи, жидкости, сцены). Всё строится кодом,
    /// поэтому панель одинаково работает на ПК и на телефоне.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        public enum Page { Materials, Weapons, Npc, Fluids, Scenes, Shapes }

        const float ToolColWidth = 118f;
        const float PageWidth = 470f;

        UIRoot _ui;
        RectTransform _toolCol, _page;
        readonly List<GameObject> _toolButtons = new List<GameObject>();
        readonly List<GameObject> _pageButtons = new List<GameObject>();
        Text _pageTitle, _footer;
        Page _current = Page.Materials;
        PlayerController Pc => Game.I?.World != null ? Game.I.World.GetComponent<PlayerController>() : null;

        public static InventoryPanel Create(UIRoot ui, RectTransform parent)
        {
            var rt = UIFactory.Box(parent, "Inventory", new Vector2(0f, 0f), new Vector2(0f, 1f),
                                   new Vector2(10f, 120f), new Vector2(10f + ToolColWidth + PageWidth + 22f, -66f));
            var panel = rt.gameObject.AddComponent<InventoryPanel>();
            panel._ui = ui;
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform root)
        {
            _toolCol = UIFactory.Panel(root, "ToolCol", Theme.Panel, 14f);
            UIFactory.SetAnchors(_toolCol, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ToolColWidth, 0f));

            _page = UIFactory.Panel(root, "Page", new Color(0.075f, 0.086f, 0.11f, 0.985f), 14f);
            UIFactory.SetAnchors(_page, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(ToolColWidth + 10f, 0f), Vector2.zero);

            BuildToolColumn();
            BuildPageHeader();
            ShowPage(Page.Materials, true);
        }

        // ---------------- колонка инструментов ----------------
        void BuildToolColumn()
        {
            var v = UIFactory.VLayout(_toolCol, 4f, new RectOffset(6, 6, 8, 8));
            v.childAlignment = TextAnchor.UpperCenter;
            var caption = UIFactory.Label(_toolCol, "ИНСТРУМЕНТ", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleCenter);
            caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            var icons = new Dictionary<ToolId, string>
            {
                { ToolId.Select, "✥" }, { ToolId.Spawn, "▣" }, { ToolId.Fluid, "◍" }, { ToolId.Weapon, "➤" },
                { ToolId.Fire, "✷" }, { ToolId.Heat, "♨" }, { ToolId.Hammer, "⚒" }, { ToolId.Npc, "☺" },
                { ToolId.Grab, "◎" }, { ToolId.Delete, "✖" },
            };
            int index = 1;
            foreach (var tool in PlayerController.AllTools)
            {
                var t = tool;
                var go = new GameObject("Tool_" + tool, typeof(RectTransform));
                go.transform.SetParent(_toolCol, false);
                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = 54f;

                var img = go.AddComponent<Image>();
                img.color = Theme.Card;
                img.sprite = RuntimeVisuals.Rounded(64, 12);
                img.type = Image.Type.Sliced;
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => { Pc?.SelectTool(t); RefreshToolHighlight(); });
                var marker = go.AddComponent<ToolButtonMarker>();
                marker.Tool = t;

                var icon = UIFactory.Label(go.transform, icons.TryGetValue(tool, out var ic) ? ic : "•", 22, Theme.Text, TextAnchor.UpperCenter);
                UIFactory.SetAnchors((RectTransform)icon.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, -4f));

                var name = UIFactory.Label(go.transform, PlayerController.ShortToolName(tool), Theme.FontTiny, Theme.TextDim, TextAnchor.LowerCenter);
                UIFactory.SetAnchors((RectTransform)name.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 3f), new Vector2(-2f, 20f));

                var key = UIFactory.Label(go.transform, index.ToString(), 9, Theme.AccentDim, TextAnchor.UpperLeft);
                UIFactory.SetAnchors((RectTransform)key.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -15f), new Vector2(20f, -3f));

                _toolButtons.Add(go);
                index = (index + 1) % 10;
            }
            RefreshToolHighlight();
        }

        void RefreshToolHighlight()
        {
            var pc = Pc;
            foreach (var go in _toolButtons)
            {
                var marker = go.GetComponent<ToolButtonMarker>();
                var img = go.GetComponent<Image>();
                if (marker == null || img == null) continue;
                bool active = pc != null && pc.CurrentTool == marker.Tool;
                img.color = active ? Theme.Accent : Theme.Card;
                var icon = go.transform.Find("Label") != null ? null : go.GetComponentInChildren<Text>();
                if (icon != null) icon.color = active ? Theme.Bg : Theme.Text;
            }
        }

        // ---------------- шапка страницы ----------------
        void BuildPageHeader()
        {
            var tabs = UIFactory.Box(_page, "PageTabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -76f), new Vector2(-8f, -6f));
            var h = UIFactory.HLayout(tabs, 4f, new RectOffset(0, 0, 0, 0));
            h.childAlignment = TextAnchor.MiddleLeft;
            foreach (var (page, caption) in new (Page, string)[]
            {
                (Page.Materials, "ПРЕДМЕТЫ"), (Page.Weapons, "ОРУЖИЕ"), (Page.Npc, "ЛЮДИ"),
                (Page.Fluids, "ЖИДКОСТИ"), (Page.Scenes, "СЦЕНЫ"),
            })
            {
                var p = page;
                var btn = UIFactory.Button(tabs, caption, new Vector2(84f, 30f), () => ShowPage(p), Theme.Card, Theme.FontTiny);
                btn.gameObject.AddComponent<LayoutElement>().preferredWidth = 84f;
                btn.gameObject.AddComponent<PageTabMarker>().Page = page;
            }

            _pageTitle = UIFactory.Label(_page, "ПРЕДМЕТЫ", Theme.FontHeader, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchors((RectTransform)_pageTitle.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -112f), new Vector2(-14f, -80f));
            _pageTitle.gameObject.SetActive(false);

            _footer = UIFactory.Label(_page, "", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.SetAnchors((RectTransform)_footer.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 2f), new Vector2(-12f, 24f));
        }

        public void ShowPage(Page page, bool silent = false)
        {
            _current = page;
            foreach (var go in _pageButtons) Destroy(go);
            _pageButtons.Clear();
            foreach (var tab in _page.GetComponentsInChildren<PageTabMarker>())
            {
                var img = tab.GetComponent<Image>();
                if (img != null) img.color = tab.Page == page ? Theme.AccentDim : Theme.Card;
            }

            // область контента
            var old = _page.Find("Content");
            if (old != null) Destroy(old.gameObject);

            var content = UIFactory.Box(_page, "Content", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 28f), new Vector2(-8f, -80f));
            switch (page)
            {
                case Page.Materials: BuildMaterials(content); break;
                case Page.Weapons: BuildWeapons(content); break;
                case Page.Npc: BuildNpcs(content); break;
                case Page.Fluids: BuildFluids(content); break;
                case Page.Scenes: BuildScenes(content); break;
            }
            if (!silent) Game.I?.UI?.Log("Панель: " + page);
        }

        // ---------------- сетка предметов ----------------
        RectTransform Grid(Transform parent, int columns, float cellW, float cellH, out ScrollRect scroll)
        {
            var viewport = UIFactory.Box(parent, "Grid", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = viewport.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = UIFactory.Box(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, cellH);
            g.spacing = new Vector2(6f, 6f);
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

        /// <summary>Карточка предмета: цветной образец, название, ключевые характеристики.</summary>
        RectTransform Card(Transform parent, string title, string subtitle, Color swatch, Action onClick, bool selected = false)
        {
            var go = new GameObject("Card_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = selected ? new Color(0.16f, 0.36f, 0.34f, 1f) : Theme.Card;
            img.sprite = RuntimeVisuals.Rounded(64, 10);
            img.type = Image.Type.Sliced;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var chip = UIFactory.Box(go.transform, "Swatch", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -14f), new Vector2(36f, 14f), swatch);
            chip.GetComponent<Image>().sprite = RuntimeVisuals.Rounded(32, 8);
            chip.GetComponent<Image>().type = Image.Type.Sliced;

            var t = UIFactory.Label(go.transform, title, Theme.FontSmall, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)t.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(42f, 2f), new Vector2(-6f, -4f));

            if (!string.IsNullOrEmpty(subtitle))
            {
                var st = UIFactory.Label(go.transform, subtitle, Theme.FontTiny, Theme.TextDim, TextAnchor.LowerLeft);
                UIFactory.SetAnchors((RectTransform)st.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(42f, 3f), new Vector2(-6f, 20f));
            }
            return (RectTransform)go.transform;
        }

        void BuildMaterials(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 2, 210f, 58f, out _);

            // формы предметов — первая строка
            foreach (var (shape, caption) in new (BodyShape, string)[]
            {
                (BodyShape.Box, "Куб"), (BodyShape.Plate, "Плита"), (BodyShape.Sphere, "Шар"),
                (BodyShape.Cylinder, "Цилиндр"), (BodyShape.Capsule, "Капсула"),
            })
            {
                var s = shape;
                var selected = pc != null && pc.SelectedShape == shape;
                Card(content, caption, "форма", selected ? Theme.Accent : Theme.Stroke,
                     () => { pc.SetShape(s); pc.SelectTool(ToolId.Spawn); ShowPage(Page.Materials); }, selected);
            }

            var db = Game.I.Materials;
            int count = 0;
            foreach (var group in db.All.GroupBy(m => m.category).OrderBy(g => g.Key))
            {
                foreach (var m in group.OrderByDescending(m => m.hardness))
                {
                    var mat = m;
                    bool selected = pc != null && pc.SelectedMaterial != null && pc.SelectedMaterial.id == mat.id;
                    Card(content, mat.nameRu,
                         $"{mat.density:F0} кг/м³ · HB {mat.hardness:F0}",
                         mat.color,
                         () => { pc.SetMaterial(mat); pc.SelectTool(ToolId.Spawn); ShowPage(Page.Materials); },
                         selected);
                    count++;
                }
            }
            _footer.text = $"{count} материалов · {CategoryName(pc?.SelectedShape)} · колесо мыши — зум";
        }

        void BuildWeapons(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 2, 210f, 58f, out _);
            int count = 0;
            foreach (var group in Game.I.Weapons.All.GroupBy(w => w.category).OrderBy(g => CategoryOrder(g.Key)))
            {
                foreach (var w in group.OrderBy(w => w.massGrams))
                {
                    var weapon = w;
                    bool selected = pc != null && pc.Weapons.Current != null && pc.Weapons.Current.id == weapon.id;
                    var cat = weapon.category switch
                    {
                        "melee" => "ближний бой",
                        "thrown" => $"{weapon.massGrams:F0} г · бросок",
                        "bow" => "стрелы",
                        "heavy" => "тяжёлое",
                        "energy" => "энергия",
                        _ => $"{weapon.caliberMM:F1} мм · {weapon.rpm:F0} в/мин",
                    };
                    Card(content, weapon.nameRu, cat,
                         WeaponColor(weapon.category),
                         () => { pc.SetWeapon(weapon); pc.SelectTool(ToolId.Weapon); ShowPage(Page.Weapons); },
                         selected);
                    count++;
                }
            }
            _footer.text = $"{count} единиц оружия · Z/X — переключение · ПКМ — панорама";
        }

        static Color WeaponColor(string cat) => cat switch
        {
            "melee" => new Color(0.75f, 0.35f, 0.35f),
            "thrown" => new Color(0.85f, 0.6f, 0.25f),
            "bow" => new Color(0.55f, 0.7f, 0.35f),
            "heavy" => new Color(0.9f, 0.45f, 0.2f),
            "energy" => new Color(0.35f, 0.8f, 0.95f),
            _ => new Color(0.6f, 0.65f, 0.75f),
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
            var content = Grid(parent, 2, 210f, 58f, out _);
            foreach (var kind in new[] { NpcActor.NpcKind.Human, NpcActor.NpcKind.Heavy, NpcActor.NpcKind.Scientist, NpcActor.NpcKind.Zombie, NpcActor.NpcKind.Robot })
            {
                var k = kind;
                bool selected = pc != null && pc.SelectedNpcKind == kind;
                Card(content, NpcName(kind), NpcDesc(kind), NpcColor(kind),
                     () => { pc.SetNpcKind(k); pc.SelectTool(ToolId.Npc); ShowPage(Page.Npc); }, selected);
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
            NpcActor.NpcKind.Human => "100 HP · обычный",
            NpcActor.NpcKind.Heavy => "320 HP · тяжёлый",
            NpcActor.NpcKind.Scientist => "65 HP · хрупкий",
            NpcActor.NpcKind.Zombie => "160 HP · горит хуже",
            NpcActor.NpcKind.Robot => "420 HP · металл",
            _ => ""
        };

        static Color NpcColor(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => new Color(0.82f, 0.64f, 0.50f),
            NpcActor.NpcKind.Heavy => new Color(0.72f, 0.55f, 0.42f),
            NpcActor.NpcKind.Scientist => new Color(0.92f, 0.93f, 0.95f),
            NpcActor.NpcKind.Zombie => new Color(0.45f, 0.60f, 0.38f),
            NpcActor.NpcKind.Robot => new Color(0.55f, 0.60f, 0.68f),
            _ => Color.gray
        };

        void BuildFluids(RectTransform parent)
        {
            var pc = Pc;
            var content = Grid(parent, 2, 210f, 58f, out _);
            int count = 0;
            foreach (var s in Game.I.Substances.All.OrderBy(s => s.phase).ThenBy(s => s.nameRu))
            {
                var sub = s;
                bool selected = pc != null && pc.SelectedSubstance != null && pc.SelectedSubstance.id == sub.id;
                Card(content, sub.nameRu,
                     $"{sub.formula} · pH {sub.ph:F0} · {PhaseName(sub.phase)}",
                     sub.color,
                     () => { pc.SetSubstance(sub); pc.SelectTool(ToolId.Fluid); ShowPage(Page.Fluids); },
                     selected);
                count++;
            }
            _footer.text = $"{count} веществ · клик по ёмкости — налить, по пустому месту — колба";
        }

        static string PhaseName(Sim.Phase p) => p switch
        {
            Sim.Phase.Gas => "газ",
            Sim.Phase.Solid => "порошок",
            _ => "жидкость"
        };

        void BuildScenes(RectTransform parent)
        {
            var content = Grid(parent, 1, 430f, 62f, out _);
            foreach (var scenario in ScenarioLibrary.All())
            {
                var sc = scenario;
                Card(content, sc.nameRu, sc.description, Theme.AccentDim, () =>
                {
                    Game.I.World.ClearAll();
                    sc.Build(Game.I.World);
                    Game.I.CameraRig.ResetView();
                    Game.I.UI?.Log("Сцена: " + sc.nameRu);
                });
            }
            _footer.text = "Сцена заменяет всё содержимое мира";
        }

        static string CategoryName(BodyShape? shape) => shape switch
        {
            BodyShape.Box => "куб",
            BodyShape.Plate => "плита",
            BodyShape.Sphere => "шар",
            BodyShape.Cylinder => "цилиндр",
            BodyShape.Capsule => "капсула",
            _ => "куб"
        };

        public void Update()
        {
            var pc = Pc;
            if (pc == null) return;
            RefreshToolHighlight();
            if (_footer != null && _current == Page.Materials && pc.SelectedMaterial != null)
            {
                // показываем выбранный материал и размер штампа
                if (Time.frameCount % 15 == 0)
                    _footer.text = $"Выбрано: {pc.SelectedMaterial.nameRu} · масштаб x{pc.SpawnScale:0.00} · [ ] меняет размер";
            }
        }
    }

    public class PageTabMarker : MonoBehaviour
    {
        public InventoryPanel.Page Page;
    }
}
