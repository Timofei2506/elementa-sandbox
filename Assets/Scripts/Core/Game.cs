using System;
using System.Collections.Generic;
using System.IO;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Central service locator + game state. Created automatically by <see cref="Boot"/>.
    /// Holds the loaded databases so every system shares one source of truth.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public MaterialDatabase Materials { get; private set; }
        public SubstanceDatabase Substances { get; private set; }
        public ReactionEngine Reactions { get; private set; }
        public WeaponDatabase Weapons { get; private set; }
        public List<Dictionary<string, object>> Elements { get; } = new List<Dictionary<string, object>>();

        public SandboxWorld World { get; set; }
        public CameraRig CameraRig { get; set; }
        public UIRoot UI { get; set; }
        public bool IsMobile { get; private set; }
        public float UiScale { get; private set; } = 1f;

        public event Action OnWorldReset;

        public int LoadedMaterials => Materials?.Count ?? 0;
        public int LoadedSubstances => Substances?.Count ?? 0;
        public int LoadedReactions => Reactions?.Count ?? 0;
        public int LoadedWeapons => Weapons?.Count ?? 0;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            LoadDatabases();
            DetectPlatform();
        }

        void DetectPlatform()
        {
            IsMobile = Application.isMobilePlatform;
            // tablet/phone also get touch UI when the screen is portrait-ish or small
            if (!IsMobile && Screen.width < 1100 && Input.touchSupported) IsMobile = true;
            UiScale = IsMobile ? Mathf.Clamp(Screen.width / 900f, 0.75f, 1.35f) : 1f;
            Application.targetFrameRate = IsMobile ? 60 : 144;
        }

        void LoadDatabases()
        {
            Materials = new MaterialDatabase();
            Substances = new SubstanceDatabase();
            Reactions = new ReactionEngine();
            Weapons = new WeaponDatabase();

            string mats = LoadText("ElementaData/materials");
            string subs = LoadText("ElementaData/substances");
            string reac = LoadText("ElementaData/reactions");
            string weap = LoadText("ElementaData/weapons");
            string elems = LoadText("ElementaData/elements");

            try { if (!string.IsNullOrEmpty(mats)) Materials.LoadFromJson(mats); }
            catch (Exception e) { Debug.LogError("[ELEMENTA] materials.json: " + e.Message); }
            try { if (!string.IsNullOrEmpty(subs)) Substances.LoadFromJson(subs); }
            catch (Exception e) { Debug.LogError("[ELEMENTA] substances.json: " + e.Message); }
            try { if (!string.IsNullOrEmpty(reac)) Reactions.LoadFromJson(reac); }
            catch (Exception e) { Debug.LogError("[ELEMENTA] reactions.json: " + e.Message); }
            try { if (!string.IsNullOrEmpty(weap)) Weapons.LoadFromJson(weap); }
            catch (Exception e) { Debug.LogError("[ELEMENTA] weapons.json: " + e.Message); }
            try
            {
                if (!string.IsNullOrEmpty(elems))
                {
                    var root = Json.Obj(Json.Parse(elems));
                    foreach (var e in Json.ArrOf(root, "elements")) Elements.Add(Json.Obj(e));
                }
            }
            catch (Exception e) { Debug.LogError("[ELEMENTA] elements.json: " + e.Message); }

            if (Materials.Count == 0) Materials.Add(CreateFallback());
            if (Substances.Count == 0) Substances.Add(new SubstanceDef { id = "water", nameRu = "Вода", nameEn = "Water", formula = "H2O", phase = Phase.Liquid, density = 1000, ph = 7, molarMass = 18.015f, color = new Color(.24f, .61f, .88f) });
            Debug.Log($"[ELEMENTA] Загружено: {Materials.Count} материалов, {Substances.Count} веществ, {Reactions.Count} реакций, {Weapons.Count} оружий, {Elements.Count} элементов.");
        }

        static MaterialDef CreateFallback() => new MaterialDef
        {
            id = "steel_1045", nameRu = "Сталь", nameEn = "Steel", category = "metal",
            density = 7850, hardness = 1500, strength = 585, toughness = 40, meltK = 1793,
            heat = 486, cond = 50, corrosion = .35f, elasticity = .3f,
            color = new Color(.54f, .54f, .57f), roughness = .45f, metallic = 1f,
        };

        static string LoadText(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) return asset.text;
            string fallback = Path.Combine(Application.dataPath, "Resources/" + resourcePath + ".json");
            if (File.Exists(fallback)) return File.ReadAllText(fallback);
            Debug.LogWarning("[ELEMENTA] Не найден ресурс: " + resourcePath);
            return null;
        }

        public void NotifyWorldReset() => OnWorldReset?.Invoke();
    }
}
