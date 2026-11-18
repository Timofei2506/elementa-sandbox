// Extra editor API stubs for compile-checking Editor scripts in CI.
using UnityEngine;

namespace UnityEditor
{
    public class InitializeOnLoadAttribute : System.Attribute { }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static void SetApplicationIdentifier(BuildTargetGroup group, string identifier) { }
        public static ScriptingImplementation GetScriptingBackend(BuildTargetGroup group) => ScriptingImplementation.Mono2x;
        public static void SetScriptingBackend(BuildTargetGroup group, ScriptingImplementation impl) { }
        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
        }
    }

    public enum ScriptingImplementation { Mono2x, IL2CPP, WinRTDotNET }
    [System.Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86 = 4, X86_64 = 8, All = 15 }
    public enum AndroidSdkVersions { AndroidApiLevel22 = 22, AndroidApiLevel23 = 23, AndroidApiLevel24 = 24, AndroidApiLevel26 = 26 }
}
