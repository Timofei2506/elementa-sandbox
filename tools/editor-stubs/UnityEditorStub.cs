// Editor-only API subset so the Editor scripts can be compile-checked in CI.
using System;
namespace UnityEditor
{
    public class MenuItem : Attribute { public MenuItem(string name) { } public MenuItem(string name, bool validate) { } public MenuItem(string name, bool validate, int priority) { } public int priority; }
    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
    }
    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static void SetDirty(UnityEngine.Object o) { }
        public static string SaveFolderPanel(string title, string folder, string defaultName) => folder;
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => directory;
    }
    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path; public bool enabled;
    }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } = new EditorBuildSettingsScene[0]; }
    public static class EditorApplication { public static bool isPlaying { get; set; } public static Action delayCall; }
    public class InitializeOnLoadMethodAttribute : Attribute { }
    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object o) { }
        public SerializedProperty FindProperty(string name) => null;
        public bool ApplyModifiedProperties() => true;
    }
    public class SerializedProperty
    {
        public int arraySize;
        public UnityEngine.Object objectReferenceValue;
        public SerializedProperty GetArrayElementAtIndex(int i) => null;
        public void InsertArrayElementAtIndex(int i) { }
    }
    public enum BuildTarget { StandaloneWindows64 = 19, Android = 13, StandaloneLinux64 = 24, StandaloneOSX = 2, WebGL = 20 }
    public enum BuildTargetGroup { Standalone, Android, WebGL, Unknown }
    public class BuildPlayerOptions
    {
        public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options;
    }
    [Flags] public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4 }
    public static class BuildPipeline { public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) => null; }
    public static class EditorUserBuildSettings { public static bool buildAppBundle { get; set; } }
}
namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }
    public struct BuildSummary { public BuildResult result; public ulong totalSize; public float totalTime; }
    public class BuildReport { public BuildSummary summary; }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string path) => default;
    }
}
