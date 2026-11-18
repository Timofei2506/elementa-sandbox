#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elementa.EditorTools
{
    /// <summary>
    /// Makes the project build-ready without any manual clicking — locally and in CI.
    /// Runs once per editor session: creates the main scene if missing, registers it in
    /// Build Settings, sets the player identity and the Android toolchain requirements.
    /// Every step is guarded, so a failure can never break a build.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectAutoSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string Company = "Timofei2506";
        const string Product = "ELEMENTA";
        const string BundleId = "com.timofei2506.elementa";
        const string Version = "0.1.0";

        static ProjectAutoSetup()
        {
            // deferred: the asset database is still importing when static ctors run
            EditorApplication.delayCall += Run;
        }

        public static void Run()
        {
            try
            {
                bool changed = false;
                changed |= EnsureScene();
                changed |= EnsureBuildSettings();
                changed |= EnsurePlayerSettings();
                changed |= EnsureShaders();
                if (changed) AssetDatabase.SaveAssets();
                Debug.Log("[ELEMENTA] Автонастройка проекта завершена. Готово к сборке.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ELEMENTA] Автонастройка пропущена: " + e.Message);
            }
        }

        static bool EnsureScene()
        {
            if (File.Exists(ScenePath)) return false;
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ELEMENTA] Создана сцена " + ScenePath);
            return true;
        }

        static bool EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath && s.enabled) return false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[ELEMENTA] Сцена добавлена в Build Settings");
            return true;
        }

        static bool EnsurePlayerSettings()
        {
#pragma warning disable 618
            bool changed = false;
            if (PlayerSettings.companyName != Company) { PlayerSettings.companyName = Company; changed = true; }
            if (PlayerSettings.productName != Product) { PlayerSettings.productName = Product; changed = true; }
            if (PlayerSettings.bundleVersion != Version) { PlayerSettings.bundleVersion = Version; changed = true; }

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);

            // Android ships only as IL2CPP + ARM64, otherwise modern Unity refuses to build
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion != AndroidSdkVersions.AndroidApiLevel24)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            // linear colour + HDR look better and are required by the emissive materials
            if (!PlayerSettings.colorSpace.Equals(ColorSpace.Linear))
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                changed = true;
            }
#pragma warning restore 618
            return changed;
        }

        static bool EnsureShaders()
        {
            ElementaMenu.EnsureAlwaysIncludedShaders();
            return false;
        }
    }
}
#endif
