#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elementa.EditorTools
{
    /// <summary>
    /// Editor automation: creates the main scene, builds Windows/Android players and
    /// validates project content. Menu: ELEMENTA.
    /// </summary>
    public static class ElementaMenu
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("ELEMENTA/1. Создать главную сцену", priority = 1)]
        public static void CreateMainScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // the boot script builds everything at runtime; a camera keeps the scene valid
            if (Camera.main == null)
            {
                var camGo = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ELEMENTA] Главная сцена создана: " + ScenePath);
            EditorUtility.DisplayDialog("ELEMENTA", "Сцена Main.unity создана и добавлена в Build Settings.\nНажмите Play.", "OK");
        }

        public static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        [MenuItem("ELEMENTA/2. Проверить проект", priority = 2)]
        public static void ValidateProject()
        {
            var problems = new System.Collections.Generic.List<string>();
            foreach (var f in new[] { "materials", "substances", "reactions", "weapons", "elements" })
            {
                var path = $"Assets/Resources/ElementaData/{f}.json";
                if (!File.Exists(path)) problems.Add("Отсутствует " + path);
            }
            if (AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Roboto-Regular.ttf") == null)
                problems.Add("Отсутствует шрифт Roboto-Regular.ttf с кириллицей");

            if (problems.Count == 0)
                EditorUtility.DisplayDialog("ELEMENTA", "Проверка пройдена: данные и шрифты на месте.\nВерсия Unity: " + Application.unityVersion, "OK");
            else
                EditorUtility.DisplayDialog("ELEMENTA", "Найдены проблемы:\n\n" + string.Join("\n", problems), "OK");
        }

        [MenuItem("ELEMENTA/3. Собрать Windows (.exe)", priority = 3)]
        public static void BuildWindows()
        {
            string path = EditorUtility.SaveFolderPanel("Куда собрать Windows-билд", "", "Builds");
            if (string.IsNullOrEmpty(path)) return;
            var result = Build(BuildTarget.StandaloneWindows64, Path.Combine(path, "ELEMENTA.exe"));
            Report(result, path);
        }

        [MenuItem("ELEMENTA/4. Собрать Android (.apk)", priority = 4)]
        public static void BuildAndroid()
        {
            string path = EditorUtility.SaveFolderPanel("Куда собрать APK", "", "Builds");
            if (string.IsNullOrEmpty(path)) return;
            EditorUserBuildSettings.buildAppBundle = false;
            var result = Build(BuildTarget.Android, Path.Combine(path, "ELEMENTA.apk"));
            Report(result, path);
        }

        /// <summary>
        /// Shaders resolved through Shader.Find are stripped from player builds unless they are
        /// always-included. This runs before every build and once on editor load.
        /// </summary>
        public static void EnsureAlwaysIncludedShaders()
        {
            string[] names = { "Standard", "Sprites/Default", "Skybox/Panoramic", "Unlit/Color", "UI/Default", "Particles/Standard Unlit", "Skybox/Procedural" };
            var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null) return;
            var so = new SerializedObject(graphicsSettings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                }
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log("[ELEMENTA] Шейдер добавлен в Always Included: " + name);
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists("Assets/Resources/ElementaData/materials.json")) EnsureAlwaysIncludedShaders();
            };
        }

        static BuildReport Build(BuildTarget target, string output)
        {
            EnsureScene();
            EnsureAlwaysIncludedShaders();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            return report;
        }

        static void EnsureScene()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Count == 0 || !File.Exists(ScenePath)) CreateMainScene();
            else
            {
                scenes[0].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        static void Report(BuildReport report, string folder)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ELEMENTA] Сборка успешна: {folder} ({summary.totalSize / 1024f / 1024f:F1} МБ)");
                EditorUtility.DisplayDialog("ELEMENTA", $"Готово!\n{folder}\n{summary.totalSize / 1024f / 1024f:F1} МБ", "OK");
            }
            else
            {
                Debug.LogError("[ELEMENTA] Сборка не удалась: " + summary.result);
                EditorUtility.DisplayDialog("ELEMENTA", "Сборка не удалась: " + summary.result + "\nПодробности в Console.", "OK");
            }
        }

        [MenuItem("ELEMENTA/5. Проверить сборку сцены (Play Mode)", priority = 5)]
        public static void VerifyRuntimeScene()
        {
            // enters play mode and reports whether the boot sequence created every system
            EditorApplication.isPlaying = true;
            EditorApplication.delayCall += () =>
            {
                bool ok = UnityEngine.Object.FindAnyObjectByType<Game>() != null;
                Debug.Log(ok
                    ? "[ELEMENTA] Boot выполнен: система Game создана. Смотрите сцену в Game View."
                    : "[ELEMENTA] Boot НЕ выполнен — проверьте Console на ошибки.");
            };
        }

        [MenuItem("ELEMENTA/Документация", priority = 20)]
        public static void OpenDocs()
        {
            Application.OpenURL("https://github.com/Timofei2506/elementa-sandbox#readme");
        }
    }
}
#endif
