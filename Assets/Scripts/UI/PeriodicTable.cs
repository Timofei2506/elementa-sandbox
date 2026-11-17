using System.Collections.Generic;
using Elementa.Sim;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Elementa
{
    /// <summary>
    /// Interactive periodic table built from elements.json.
    /// Click an element to see its properties and spawn a block made of it.
    /// </summary>
    public class PeriodicTableView : MonoBehaviour
    {
        RectTransform _grid;
        Text _info;
        readonly Dictionary<string, MaterialDef> _spawned = new Dictionary<string, MaterialDef>();

        static readonly string[,] Symbols = BuildLayout();

        static string[,] BuildLayout()
        {
            // 18 columns x 10 rows (7 periods + lanthanides/actinides), empty = no element
            var g = new string[10, 18];
            void Set(int period, int group, string sym) { if (period >= 0 && period < 10 && group >= 0 && group < 18) g[period, group] = sym; }
            string[] rows =
            {
                "H  He",
                "Li Be B  C  N  O  F  Ne",
                "Na Mg Al Si P  S  Cl Ar",
                "K  Ca Sc Ti V  Cr Mn Fe Co Ni Cu Zn Ga Ge As Se Br Kr",
            };
            var r0 = rows[0].Split(' ');
            Set(0, 0, r0[0]); Set(0, 17, r0[1]);
            var r1 = rows[1].Split(' ');
            for (int i = 0; i < r1.Length; i++) Set(1, i < 2 ? i : i + 10, r1[i]);
            var r2 = rows[2].Split(' ');
            for (int i = 0; i < r2.Length; i++) Set(2, i < 2 ? i : i + 10, r2[i]);
            var r3 = rows[3].Split(' ');
            for (int i = 0; i < r3.Length; i++) Set(3, i, r3[i]);
            var r4 = "Rb Sr Y  Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I  Xe".Split(' ');
            for (int i = 0; i < r4.Length; i++) Set(4, i, r4[i]);
            var r5 = "Cs Ba La Hf Ta W  Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn".Split(' ');
            for (int i = 0; i < r5.Length; i++) Set(5, i, r5[i]);
            var r6 = "Fr Ra Ac Rf Db Sg Bh Hs Mt Ds Rg Cn Nh Fl Mc Lv Ts Og".Split(' ');
            for (int i = 0; i < r6.Length; i++) Set(6, i, r6[i]);
            var lanth = "Ce Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb Lu".Split(' ');
            for (int i = 0; i < lanth.Length; i++) Set(8, i + 2, lanth[i]);
            var act = "Th Pa U  Np Pu Am Cm Bk Cf Es Fm Md No Lr".Split(' ');
            for (int i = 0; i < act.Length; i++) Set(9, i + 2, act[i]);
            return g;
        }

        public void Build(RectTransform parent)
        {
            var gridRoot = UIFactory.Box(parent, "Grid", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -300f), new Vector2(-4f, -4f));
            _grid = gridRoot;
            var glg = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(20f, 22f);
            glg.spacing = new Vector2(1.5f, 1.5f);
            glg.padding = new RectOffset(2, 2, 2, 2);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 18;

            var elements = Game.I.Elements;
            var bySym = new Dictionary<string, Dictionary<string, object>>();
            foreach (var e in elements) bySym[Json.Str(e, "sym")] = e;

            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 18; col++)
                {
                    string sym = Symbols[row, col];
                    if (string.IsNullOrEmpty(sym) || !bySym.TryGetValue(sym, out var data))
                    {
                        var spacer = UIFactory.Box(_grid, "empty", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                        spacer.gameObject.AddComponent<LayoutElement>();
                        continue;
                    }
                    AddCell(sym, data);
                }
            }

            _info = UIFactory.Label(parent, "Нажмите на элемент", Theme.FontSmall, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchors((RectTransform)_info.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 4f), new Vector2(-8f, 92f));
        }

        void AddCell(string sym, Dictionary<string, object> data)
        {
            var cell = UIFactory.Box(_grid, "El_" + sym, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20f, 22f), ColorUtil.Hex(Json.Str(data, "color", "#888888")));
            var image = cell.GetComponent<Image>();
            image.color = Color.Lerp(ColorUtil.Hex(Json.Str(data, "color", "#888888")), new Color(0.08f, 0.09f, 0.11f), 0.45f);

            var label = UIFactory.Label(cell, sym, 9, Theme.Text, TextAnchor.MiddleCenter);
            UIFactory.SetAnchors((RectTransform)label.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(() => OnElementClicked(sym, data));
        }

        void OnElementClicked(string sym, Dictionary<string, object> data)
        {
            var element = ElementMaterial(sym, data);
            _info.text = $"<b>{Json.Str(data, "ru")}</b> ({sym})  Z={Json.Int(data, "z")}  ·  {Json.Str(data, "cat")}\n" +
                         $"Масса {Json.Num(data, "mass"):F2} а.е.м. · плотность {Json.Num(data, "density"):F3} г/см³\n" +
                         $"Плавление {Json.Num(data, "melt"):F0} K · кипение {Json.Num(data, "boil"):F0} K · электроотрицательность {Json.Num(data, "eneg"):F2}\n" +
                         "Нажмите ещё раз, чтобы создать блок рядом с камерой";
            if (_lastClicked == sym && Time.unscaledTime - _lastClickTime < 1.2f)
            {
                var focus = Game.I.CameraRig.Focus;
                var obj = Game.I.World.Spawn(element, BodyShape.Box, Vector3.one * 1.1f,
                    focus + new Vector3(UnityEngine.Random.Range(-2f, 2f), 1.4f, UnityEngine.Random.Range(-2f, 2f)));
                if (obj != null) Game.I.UI.Log($"Создан блок: {element.nameRu}");
            }
            _lastClicked = sym;
            _lastClickTime = Time.unscaledTime;
        }

        string _lastClicked;
        float _lastClickTime;

        /// <summary>Builds a runtime MaterialDef from an element's real physical data.</summary>
        public static MaterialDef ElementMaterial(string sym, Dictionary<string, object> data)
        {
            float densityGcm3 = (float)Json.Num(data, "density", 1);
            float melt = (float)Json.Num(data, "melt", 1000);
            string cat = Json.Str(data, "cat");
            bool isMetal = cat is "metal" or "transition" or "alkali" or "alkaline" or "post-transition" or "lanthanide" or "actinide";
            string id = "element_" + sym;
            var db = Game.I?.Materials;
            if (db != null && db.Has(id)) return db.Get(id);

            var def = new MaterialDef
            {
                id = id,
                nameRu = Json.Str(data, "ru") + " (элемент)",
                nameEn = Json.Str(data, "en") + " element",
                category = isMetal ? "metal" : cat == "noble" ? "mineral" : "polymer",
                density = Mathf.Max(1f, densityGcm3 * 1000f),
                hardness = isMetal ? Mathf.Clamp(melt * 1.2f, 60f, 3600f) : Mathf.Clamp(melt * 0.5f, 30f, 900f),
                strength = isMetal ? Mathf.Clamp(melt * 0.7f, 20f, 1200f) : 40f,
                toughness = isMetal ? 30f : 8f,
                meltK = melt > 1f ? melt : 300f,
                heat = isMetal ? 300f : 1200f,
                cond = isMetal ? 80f : 1f,
                flammability = cat is "nonmetal" or "halogen" or "alkali" ? 0.4f : 0f,
                corrosion = isMetal ? 0.6f : 0.3f,
                elasticity = isMetal ? 0.25f : 0.4f,
                color = ColorUtil.Hex(Json.Str(data, "color", "#999999")),
                roughness = isMetal ? 0.35f : 0.7f,
                metallic = isMetal ? 1f : 0f,
            };
            db?.Add(def);
            return def;
        }
    }
}
