using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Unity.Industry.Viewer.Multiplay.Editor
{
    public static class MultiplayDefineSymbol
    {
        private const string k_EnableMultiplay = "ENABLE_MULTIPLAY";
        
        [MenuItem("Tools/Multiplay/Disable Multiplay for all platforms")]
        public static void DisableMultiplay()
        {
            RemoveMultiplay(NamedBuildTarget.Android);
            RemoveMultiplay(NamedBuildTarget.iOS);
            RemoveMultiplay(NamedBuildTarget.Standalone);
            RemoveMultiplay(NamedBuildTarget.WebGL);
        }
        
        [MenuItem("Tools/Multiplay/Enable Multiplay for all platforms")]
        public static void EnableMultiplay()
        {
            AddMultiplay(NamedBuildTarget.Android);
            AddMultiplay(NamedBuildTarget.iOS);
            AddMultiplay(NamedBuildTarget.Standalone);
            AddMultiplay(NamedBuildTarget.WebGL);
        }
        
        [MenuItem("Tools/Multiplay/Disable Multiplay for the current platform")]
        public static void DisableMultiplayForCurrentPlatform()
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            RemoveMultiplay(namedBuildTarget);
        }
        
        [MenuItem("Tools/Multiplay/Enable Multiplay for the current platform")]
        public static void EnableMultiplayForCurrentPlatform()
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            AddMultiplay(namedBuildTarget);
        }

        private static void AddMultiplay(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            if(symbols.Any(x => string.Equals(x, k_EnableMultiplay))) return;
            symbols = symbols.Append(k_EnableMultiplay).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(target, symbols);
        }

        private static void RemoveMultiplay(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            if(symbols.Any(x => string.Equals(x, k_EnableMultiplay)))
            {
                symbols = symbols.Where(x => !string.Equals(x, k_EnableMultiplay)).ToArray();
                PlayerSettings.SetScriptingDefineSymbols(target, symbols);
            }
        }
    }
}

