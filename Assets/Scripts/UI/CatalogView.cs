using System;
using System.Collections.Generic;
using System.Linq;
using Elementa.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Elementa
{
    /// <summary>
    /// Scrollable catalog of materials, chemicals, weapons and NPCs.
    /// Built at runtime from the JSON databases — adding a row to the JSON adds it to the UI.
    /// </summary>
    public class CatalogView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Tab { Materials, Chemicals, Weapons, Elements, Scenarios, Npc, Settings }
        public Tab CurrentTab { get; private set; } = Tab.Materials;

        UIRoot _ui;
        RectTransform _root, _tabBar, _list;
        Text _header, _footer;
        ScrollRect _scroll;
        readonly List<GameObject> _tabButtons = new List<GameObject>();
        string _filter = "";

        public void Build(UIRoot ui, RectTransform root)
        {
            _ui = ui;
            _root = root;

            _header = UIFactory.Label(root, "КАТАЛОГ", Theme.FontHeader, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchors((RectTransform)_header.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -38f), new Vector2(-14f, -6f));

            _tabBar = UIFactory.Box(root, "Tabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -82f), new Vector2(-10f, -42f));
            var h = UIFactory.HLayout(_tabBar, 4f, new RectOffset(0, 0, 0, 0));
            h.childAlignment = TextAnchor.MiddleLeft;

            foreach (Tab tab in Enum.GetValues(typeof(Tab)))
            {
                var t = tab;
                var btn = UIFactory.Button(_tabBar, TabName(tab), new Vector2(60f, 34f), () => SetTab(t), Theme.Card, Theme.FontTiny);
                btn.gameObject.AddComponent<LayoutElement>().preferredWidth = 62f;
                _tabButtons.Add(btn.gameObject);
            }

            var scrollRt = UIFactory.Box(root, "ScrollArea", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 34f), new Vector2(-8f, -88f));
            var scroll = UIFactory.Scroll(scrollRt, out var content, "CatalogScroll");
            _scroll = scroll;
            _list = content;
            var v = UIFactory.VLayout(content, 5f, new RectOffset(2, 2, 2, 2));
            v.childControlHeight = false;

            _footer = UIFactory.Label(root, "", Theme.FontTiny, Theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.SetAnchors((RectTransform)_footer.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 6f), new Vector2(-12f, 30f));

            SetTab(Tab.Materials);
        }

        static string TabName(Tab t) => t switch
        {
            Tab.Materials => "Материалы",
            Tab.Chemicals => "Химия",
            Tab.Weapons => "Оружие",
            Tab.Elements => "Элементы",
            Tab.Scenarios => "Сцены",
            Tab.Npc => "NPC",
            _ => "Настройки"
        };

        public void SetTab(Tab tab)
        {
            CurrentTab = tab;
            Rebuild();
        }

        void ClearList()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
            var table = _list.GetComponent<PeriodicTableView>();
            if (table != null) Destroy(table);
            var vl = _list.GetComponent<VerticalLayoutGroup>();
            if (vl != null) vl.enabled = true;
            var fitter = _list.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = true;
        }

        void Rebuild()
        {
            ClearList();
            switch (CurrentTab)
            {
                case Tab.Materials: BuildMaterials(); break;
                case Tab.Chemicals: BuildChemicals(); break;
                case Tab.Weapons: BuildWeapons(); break;
                case Tab.Npc: BuildNpcs(); break;
                case Tab.Elements: BuildPeriodicTable(); break;
                case Tab.Scenarios: BuildScenarios(); break;
                case Tab.Settings: BuildSettings(); break;
            }
        }

        void BuildMaterials()
        {
            var db = Game.I.Materials;
            var groups = db.All.GroupBy(m => m.category).OrderBy(g => g.Key);
            int count = 0;
            foreach (var group in groups)
            {
                AddSectionHeader(CategoryName(group.Key));
                foreach (var m in group.OrderByDescending(m => m.hardness))
                {
                    AddMaterialRow(m);
                    count++;
                }
            }
            _footer.text = $"{count} материалов · нажмите, чтобы выбрать";
        }

        void AddMaterialRow(MaterialDef m)
        {
            var row = UIFactory.Box(_list, "Mat_" + m.id, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 44f), Theme.Card);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var swatch = UIFactory.Box(row, "Swatch", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -14f), new Vector2(36f, 14f), m.color);
            var name = UIFactory.Label(row, m.nameRu, Theme.FontBody, Theme.Text);
            UIFactory.SetAnchors((RectTransform)name.transform, Vector2.zero, Vector2.one, new Vector2(44f, 18f), new Vector2(-8f, -2f));
            var stats = UIFactory.Label(row, $"{m.density:F0} кг/м³ · HB {m.hardness:F0} МПа · {m.strength:F0} МПа", Theme.FontTiny, Theme.TextDim);
            UIFactory.SetAnchors((RectTransform)stats.transform, Vector2.zero, Vector2.one, new Vector2(44f, 2f), new Vector2(-8f, -24f));

            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>();
            btn.onClick.AddListener(() =>
            {
                var pc = Game.I.World.GetComponent<PlayerController>();
                pc.SetMaterial(m);
                Game.I.UI.Log($"Материал выбран: {m.nameRu}");
                Highlight(row);
            });
            row.gameObject.AddComponent<CatalogHover>().Setup(this, () => DescribeMaterial(m));
        }

        void BuildChemicals()
        {
            var db = Game.I.Substances;
            foreach (var phase in new[] { Phase.Liquid, Phase.Solid, Phase.Gas })
            {
                var group = db.All.Where(s => s.phase == phase).ToList();
                if (group.Count == 0) continue;
                AddSectionHeader(phase switch { Phase.Liquid => "Жидкости", Phase.Solid => "Твёрдые реагенты", _ => "Газы" });
                foreach (var s in group.OrderBy(s => s.nameRu, StringComparer.OrdinalIgnoreCase))
                    AddSubstanceRow(s);
            }
            _footer.text = $"{db.Count} веществ · нажмите, чтобы наливать";
        }

        void AddSubstanceRow(SubstanceDef s)
        {
            var row = UIFactory.Box(_list, "Sub_" + s.id, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 44f), Theme.Card);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            UIFactory.Box(row, "Swatch", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -14f), new Vector2(36f, 14f), s.color);
            var name = UIFactory.Label(row, $"{s.nameRu}  <color=#7a8a9a>{s.formula}</color>", Theme.FontBody, Theme.Text);
            UIFactory.SetAnchors((RectTransform)name.transform, Vector2.zero, Vector2.one, new Vector2(44f, 18f), new Vector2(-8f, -2f));
            string tags = $"pH {s.ph:0.#}";
            if (s.flammable) tags += " · горючее";
            if (s.oxidizer) tags += " · окислитель";
            if (s.toxicity > 0.5f) tags += " · ядовито";
            if (s.corrosivity > 0.5f) tags += " · едкое";
            if (s.energyMJ > 5) tags += $" · {s.energyMJ:F0} МДж/кг";
            var stats = UIFactory.Label(row, tags, Theme.FontTiny, Theme.TextDim);
            UIFactory.SetAnchors((RectTransform)stats.transform, Vector2.zero, Vector2.one, new Vector2(44f, 2f), new Vector2(-8f, -24f));

            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>();
            btn.onClick.AddListener(() =>
            {
                var pc = Game.I.World.GetComponent<PlayerController>();
                pc.SetSubstance(s);
                Game.I.UI.Log($"Вещество выбрано: {s.nameRu} ({s.formula})");
                Highlight(row);
            });
            row.gameObject.AddComponent<CatalogHover>().Setup(this, () => DescribeSubstance(s));
        }

        void BuildWeapons()
        {
            var db = Game.I.Weapons;
            foreach (var group in db.All.GroupBy(w => w.category).OrderBy(g => g.Key))
            {
                AddSectionHeader(CategoryName(group.Key));
                foreach (var w in group)
                {
                    var row = UIFactory.Box(_list, "W_" + w.id, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 44f), Theme.Card);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                    var st = w.Stats;
                    var name = UIFactory.Label(row, w.nameRu, Theme.FontBody, Theme.Text);
                    UIFactory.SetAnchors((RectTransform)name.transform, Vector2.zero, Vector2.one, new Vector2(10f, 18f), new Vector2(-8f, -2f));
                    var stats = UIFactory.Label(row, $"{st.energyJ:F0} Дж · {w.velocity:F0} м/с · пробитие ~{st.penetrationMM:F0} мм", Theme.FontTiny, Theme.TextDim);
                    UIFactory.SetAnchors((RectTransform)stats.transform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-8f, -24f));
                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = row.GetComponent<Image>();
                    btn.onClick.AddListener(() =>
                    {
                        var pc = Game.I.World.GetComponent<PlayerController>();
                        pc.SetWeapon(w);
                        Game.I.UI.Log($"Оружие выбрано: {w.nameRu}");
                        Highlight(row);
                    });
                    row.gameObject.AddComponent<CatalogHover>().Setup(this, () => DescribeWeapon(w));
                }
            }
            _footer.text = $"{db.Count} оружий · ЛКМ стрелять, Enter перезарядить";
        }

        void BuildNpcs()
        {
            AddSectionHeader("Персонажи");
            foreach (NpcActor.NpcKind kind in Enum.GetValues(typeof(NpcActor.NpcKind)))
            {
                var k = kind;
                var row = UIFactory.Box(_list, "NPC_" + k, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 44f), Theme.Card);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                var name = UIFactory.Label(row, NpcName(k), Theme.FontBody, Theme.Text);
                UIFactory.SetAnchors((RectTransform)name.transform, Vector2.zero, Vector2.one, new Vector2(10f, 18f), new Vector2(-8f, -2f));
                var stats = UIFactory.Label(row, NpcDesc(k), Theme.FontTiny, Theme.TextDim);
                UIFactory.SetAnchors((RectTransform)stats.transform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-8f, -24f));
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.onClick.AddListener(() =>
                {
                    var cam = Game.I.CameraRig;
                    var pos = new Vector3(cam.Focus.x + UnityEngine.Random.Range(-3f, 3f), 1.4f, 0f);
                    var npc = Game.I.World.SpawnNpc(pos, k);
                    npc.SetBehaviour(NpcActor.Behaviour.Wander);
                    Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Npc);
                    Game.I.UI.Log("Создан NPC: " + NpcName(k));
                });
            }

            AddSectionHeader("Поведение");
            foreach (NpcActor.Behaviour b in Enum.GetValues(typeof(NpcActor.Behaviour)))
            {
                var beh = b;
                var btn = UIFactory.Button(_list, BehaviourName(beh), new Vector2(0f, 34f), () =>
                {
                    foreach (var npc in Game.I.World.Npcs) npc.SetBehaviour(beh);
                    Game.I.UI.Log("Поведение всех NPC: " + BehaviourName(beh));
                }, Theme.PanelLight, Theme.FontSmall);
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            }
            _footer.text = "Создавайте персонажей и меняйте их поведение";
        }

        void BuildPeriodicTable()
        {
            var view = _list.gameObject.GetComponent<PeriodicTableView>();
            if (view == null) view = _list.gameObject.AddComponent<PeriodicTableView>();
            // the periodic table wants free layout, not the vertical list
            var vl = _list.GetComponent<VerticalLayoutGroup>();
            if (vl != null) vl.enabled = false;
            var fitter = _list.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            ((RectTransform)_list).anchoredPosition = Vector2.zero;
            view.Build(_list);
            _footer.text = $"{Game.I.Elements.Count} элементов · двойной клик — создать блок";
        }

        void BuildScenarios()
        {
            AddSectionHeader("Готовые сцены");
            foreach (var scenario in ScenarioLibrary.All())
            {
                var sc = scenario;
                var row = UIFactory.Box(_list, "Sc_" + sc.id, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 46f), Theme.Card);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
                var name = UIFactory.Label(row, sc.nameRu, Theme.FontBody, Theme.Text);
                UIFactory.SetAnchors((RectTransform)name.transform, Vector2.zero, Vector2.one, new Vector2(10f, 18f), new Vector2(-8f, -2f));
                var desc = UIFactory.Label(row, sc.description, Theme.FontTiny, Theme.TextDim);
                UIFactory.SetAnchors((RectTransform)desc.transform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-8f, -24f));
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.onClick.AddListener(() => ScenarioLibrary.Load(sc.id));
            }

            AddSectionHeader("Сохранение");
            var saveRow = UIFactory.Box(_list, "SaveRow", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 40f));
            saveRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var h = UIFactory.HLayout(saveRow, 6f, new RectOffset(2, 2, 4, 4));
            var saveBtn = UIFactory.Button(saveRow, "Сохранить сцену", new Vector2(120f, 32f), () =>
            {
                string name = "scene_" + System.DateTime.Now.ToString("MMdd_HHmm");
                SaveSystem.Save(name);
            }, Theme.AccentDim, Theme.FontSmall);
            saveBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;

            var saves = SaveSystem.ListSaves();
            AddSectionHeader(saves.Count > 0 ? "Загрузить" : "Сохранений пока нет");
            foreach (var name in saves)
            {
                var n = name;
                var row = UIFactory.Box(_list, "Save_" + n, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 34f), Theme.Card);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
                var label = UIFactory.Label(row, n, Theme.FontSmall, Theme.Text);
                UIFactory.SetAnchors((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.onClick.AddListener(() => SaveSystem.Load(n));
            }
            _footer.text = "Сцены и сохранения";
        }

        void BuildSettings()
        {
            AddSectionHeader("Симуляция");
            AddToggle("Гравитация", Game.I.World.GravityEnabled, v => Game.I.World.SetGravity(v));
            AddToggle("Показывать FPS", true, v => { if (Game.I.UI.FpsText != null) Game.I.UI.FpsText.enabled = v; });
            AddSlider("Скорость времени", 0f, 2f, Game.I.World.TimeScale, v => Game.I.World.TimeScale = v);
            AddSlider("Масштаб спавна", 0.4f, 2.5f, Game.I.World.GetComponent<PlayerController>().SpawnScale, v => Game.I.World.GetComponent<PlayerController>().SpawnScale = v);
            AddSlider("Сила жара", 100f, 3000f, Game.I.World.GetComponent<PlayerController>().HeatPower, v => Game.I.World.GetComponent<PlayerController>().HeatPower = v);

            AddSectionHeader("Форма объектов");
            foreach (BodyShape shape in Enum.GetValues(typeof(BodyShape)))
            {
                var s = shape;
                var btn = UIFactory.Button(_list, ShapeName(s), new Vector2(0f, 34f), () =>
                {
                    Game.I.World.GetComponent<PlayerController>().SetShape(s);
                    Game.I.World.GetComponent<PlayerController>().SelectTool(ToolId.Spawn);
                    Game.I.UI.Log("Форма: " + ShapeName(s));
                }, Theme.PanelLight, Theme.FontSmall);
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            }
            _footer.text = "Настройки применяются сразу";
        }

        void AddSectionHeader(string text)
        {
            var label = UIFactory.Label(_list, text.ToUpperInvariant(), Theme.FontTiny, Theme.Accent);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
        }

        void AddToggle(string caption, bool value, Action<bool> onChange)
        {
            var holder = UIFactory.Box(_list, "T_" + caption, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 34f));
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var toggle = UIFactory.Toggle(holder, caption, value, onChange, new Vector2(300f, 30f));
            UIFactory.SetAnchors((RectTransform)toggle.transform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
        }

        void AddSlider(string caption, float min, float max, float value, Action<float> onChange)
        {
            var holder = UIFactory.Box(_list, "S_" + caption, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 46f));
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
            var slider = UIFactory.Slider(holder, caption, min, max, value, onChange, new Vector2(300f, 22f));
            UIFactory.SetAnchors((RectTransform)slider.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -18f));
        }

        void Highlight(RectTransform row)
        {
            foreach (var t in _list.GetComponentsInChildren<Image>())
            {
                if (t.color == Theme.PanelLight) t.color = Theme.Card;
            }
            var img = row.GetComponent<Image>();
            if (img != null) img.color = Theme.PanelLight;
        }

        public void ShowDescription(string text)
        {
            if (Game.I.UI != null && Game.I.UI.InfoPanel != null)
                _ui.Log(text.Replace("\n", " · "));
        }

        public void OnPointerEnter(PointerEventData eventData) => _ui.SetPointerOverUI(true);
        public void OnPointerExit(PointerEventData eventData) => _ui.SetPointerOverUI(false);

        // ---------- description builders ----------
        string DescribeMaterial(MaterialDef m) =>
            $"{m.nameRu} ({m.nameEn}) — {CategoryName(m.category)}\n" +
            $"Плотность {m.density:F0} кг/м³, твёрдость {m.hardness:F0} МПа, прочность {m.strength:F0} МПа\n" +
            $"Вязкость {m.toughness:F0} Дж, теплоёмкость {m.heat:F0} Дж/кг·К\n" +
            $"Теплопроводность {m.cond:F1} Вт/м·К, плавление {m.meltK:F0} K\n" +
            $"Воспламеняемость {m.flammability:P0}, коррозионная стойкость {m.corrosion:P0}";

        string DescribeSubstance(SubstanceDef s) =>
            $"{s.nameRu} — {s.formula} ({s.phase})\n" +
            $"pH {s.ph:0.#}, плотность {s.density:F0} кг/м³, молярная масса {s.molarMass:F1} г/моль\n" +
            (s.energyMJ > 0 ? $"Энергия {s.energyMJ:F1} МДж/кг\n" : "") +
            $"Токсичность {s.toxicity:P0}, едкость {s.corrosivity:P0}";

        string DescribeWeapon(WeaponDef w)
        {
            var s = w.Stats;
            return $"{w.nameRu} ({w.category})\n" +
                   $"Кинетическая энергия {s.energyJ:F0} Дж, скорость {w.velocity:F0} м/с\n" +
                   $"Пробитие {s.penetrationMM:F0} мм RHA, скорострельность {w.rpm:F0} в/мин\n" +
                   $"Точность {w.accuracy:P0}, отдача {w.recoil:F1}";
        }

        static string CategoryName(string cat) => cat switch
        {
            "metal" => "Металлы",
            "polymer" => "Полимеры",
            "ceramic" => "Керамика и стекло",
            "composite" => "Композиты",
            "mineral" => "Минералы",
            "wood" => "Дерево",
            "organic" => "Органика",
            "pistol" => "Пистолеты",
            "smg" => "Пистолеты-пулемёты",
            "rifle" => "Автоматы и винтовки",
            "sniper" => "Снайперские",
            "shotgun" => "Дробовики",
            "explosive" => "Взрывчатка",
            "launcher" => "Гранатомёты",
            "bow" => "Луки и арбалеты",
            "energy" => "Энергетическое",
            "flame" => "Огнемёты",
            _ => cat
        };

        static string ShapeName(BodyShape s) => s switch
        {
            BodyShape.Box => "Куб",
            BodyShape.Sphere => "Сфера",
            BodyShape.Cylinder => "Цилиндр",
            BodyShape.Capsule => "Капсула",
            BodyShape.Plate => "Плита",
            _ => s.ToString()
        };

        static string NpcName(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => "Человек",
            NpcActor.NpcKind.Heavy => "Тяжёлый",
            NpcActor.NpcKind.Scientist => "Учёный",
            NpcActor.NpcKind.Zombie => "Зомби",
            NpcActor.NpcKind.Robot => "Робот",
            _ => k.ToString()
        };

        static string NpcDesc(NpcActor.NpcKind k) => k switch
        {
            NpcActor.NpcKind.Human => "100 HP · скорость 2.2 м/с",
            NpcActor.NpcKind.Heavy => "320 HP · масса 190 кг",
            NpcActor.NpcKind.Scientist => "100 HP · лёгкий, 65 кг",
            NpcActor.NpcKind.Zombie => "160 HP · не чувствует боли",
            NpcActor.NpcKind.Robot => "420 HP · бронированный",
            _ => ""
        };

        static string BehaviourName(NpcActor.Behaviour b) => b switch
        {
            NpcActor.Behaviour.Still => "Стоит",
            NpcActor.Behaviour.Wander => "Идёт",
            NpcActor.Behaviour.Flee => "Убегает",
            NpcActor.Behaviour.Panic => "Паника",
            _ => b.ToString()
        };
    }

    /// <summary>Hover handler for catalog rows: shows the fact sheet in the info panel.</summary>
    public class CatalogHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        CatalogView _view;
        Func<string> _describe;
        Text _info;

        public void Setup(CatalogView view, Func<string> describe)
        {
            _view = view;
            _describe = describe;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Game.I?.UI == null || Game.I.UI.SelectionText == null) return;
            Game.I.UI.SelectionText.text = _describe();
            Game.I.UI.SetPointerOverUI(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Game.I?.UI?.SetPointerOverUI(false);
        }
    }
}
