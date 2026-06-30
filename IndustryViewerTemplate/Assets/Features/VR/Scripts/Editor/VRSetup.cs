using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.OpenXR;
using UnityEngine.Rendering;

//Enable this if want to use Oculus Loader
//using Unity.XR.Oculus;

namespace Unity.Industry.Viewer.VR.Editor
{
    public static class VRSetup
    {
        private const string k_VRMode = "VR_MODE";

        private const string k_MetaFeatureSetID = "com.unity.openxr.featureset.meta";
        private const string k_AndroidXRFeatureSetID = "com.unity.openxr.featureset.android";

        // URP render pipeline assets per VR platform, and the non-VR default used as the restore fallback.
        private const string k_AndroidXRUrpPath = "Assets/Settings/URP-Android XR.asset";
        private const string k_StandaloneVRUrpPath = "Assets/Settings/URP-Standalone VR.asset";
        private const string k_DefaultUrpPath = "Assets/Settings/URP-HighFidelity.asset";
        private const string k_PreviousDefaultRpKey = "PreviousDefaultRenderPipelinePath";
        
        static string[] vrScenes = new string[]
        {
            "Assets/Scenes/Main VR.unity",
            "Assets/Scenes/Streaming VR.unity"
        };
        
        static string[] noneVRScenes = new string[]
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Streaming.unity"
        };

        [MenuItem("Tools/XR/Setup Android XR")]
        public static void SetupAndroidXR()
        {
            XRSceneSetup();
            #region Quality Settings

            var currentDefaultQualityName = ReturnCurrentQualityName();
            string[] qualityNames = QualitySettings.names;
            int vrQualityIndex = Array.FindIndex(qualityNames, name => name.Equals("Android XR", StringComparison.OrdinalIgnoreCase));
            if (currentDefaultQualityName == "Android XR")
            {
                // If already on "Android XR", save the next quality level as previous
                if (vrQualityIndex >= 0 && vrQualityIndex + 1 < qualityNames.Length)
                {
                    var targetQualityName = qualityNames[vrQualityIndex + 1];
                    EditorPrefs.SetString("PreviousQualityLevelName", targetQualityName);
                    Debug.Log($"Saved current default quality level: {targetQualityName}");
                }
                else
                {
                    EditorPrefs.SetString("PreviousQualityLevelName", currentDefaultQualityName);
                    Debug.Log($"Saved current default quality level: {currentDefaultQualityName}");
                }
            }
            else
            {
                EditorPrefs.SetString("PreviousQualityLevelName", currentDefaultQualityName);
                Debug.Log($"Saved current default quality level: {currentDefaultQualityName}");
            }

            // Set quality level to "Standalone VR"
            SetDefaultQualityLevel(vrQualityIndex);
            #endregion
            
            #region Setup XR Plugin Management
            EnableOpenXRLoader();
            LoaderControl.DisableFeatureGroup(BuildTargetGroup.Android, k_MetaFeatureSetID);
            LoaderControl.EnableFeatureGroup(BuildTargetGroup.Android, k_AndroidXRFeatureSetID);
            #endregion

            // Pin the Android XR URP asset (active quality level + global default).
            ApplyRenderPipeline(k_AndroidXRUrpPath);
            
            #region Add Android XR to Scripting Define Symbols

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var symbols);
            if(!symbols.Any(x => string.Equals(x, k_VRMode)))
            {
                symbols = symbols.Append(k_VRMode).ToArray();
            }
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, symbols);
            
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode))) return;
            symbols = symbols.Append(k_VRMode).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, symbols);

            #endregion
        }
        
        [MenuItem("Tools/XR/Setup Standalone VR")]
        public static void SetupForStandaloneVR()
        {
            XRSceneSetup();

            #region Quality Settings

            var currentDefaultQualityName = ReturnCurrentQualityName();
            string[] qualityNames = QualitySettings.names;
            int vrQualityIndex = Array.FindIndex(qualityNames, name => name.Equals("Standalone VR", StringComparison.OrdinalIgnoreCase));
            if (currentDefaultQualityName == "Standalone VR")
            {
                // If already on "Standalone VR", save the next quality level as previous
                if (vrQualityIndex >= 0 && vrQualityIndex + 1 < qualityNames.Length)
                {
                    var targetQualityName = qualityNames[vrQualityIndex + 1];
                    EditorPrefs.SetString("PreviousQualityLevelName", targetQualityName);
                    Debug.Log($"Saved current default quality level: {targetQualityName}");
                }
                else
                {
                    EditorPrefs.SetString("PreviousQualityLevelName", currentDefaultQualityName);
                    Debug.Log($"Saved current default quality level: {currentDefaultQualityName}");
                }
            }
            else
            {
                EditorPrefs.SetString("PreviousQualityLevelName", currentDefaultQualityName);
                Debug.Log($"Saved current default quality level: {currentDefaultQualityName}");
            }

            // Set quality level to "Standalone VR"
            SetDefaultQualityLevel(vrQualityIndex);
            #endregion
            
            #region Setup XR Plugin Management
            EnableOpenXRLoader();
            LoaderControl.EnableFeatureGroup(BuildTargetGroup.Android, k_MetaFeatureSetID);
            LoaderControl.DisableFeatureGroup(BuildTargetGroup.Android, k_AndroidXRFeatureSetID);
            #endregion

            // Pin the Standalone VR URP asset (active quality level + global default).
            ApplyRenderPipeline(k_StandaloneVRUrpPath);

#region Add VR Mode to Scripting Define Symbols

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var symbols);
            if (!symbols.Any(x => string.Equals(x, k_VRMode)))
            {
                symbols = symbols.Append(k_VRMode).ToArray();
            }
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, symbols);
            
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode))) return;
            symbols = symbols.Append(k_VRMode).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, symbols);

#endregion

        }

        private static void EnableOpenXRLoader()
        {
            if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
            {
                LoaderControl.DisableLoader(BuildTarget.Android, typeof(ARCoreLoader));
            }
               
            //Enable this if want to use Oculus Loader
            /*if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
            {
                LoaderControl.EnableLoader(BuildTarget.Android, typeof(OculusLoader));
            }*/
            
            if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OpenXRLoader)))
            {
                LoaderControl.EnableLoader(BuildTarget.Android, typeof(OpenXRLoader));
            }
        }

        private static string ReturnCurrentQualityName()
        {
            var qualitySettingsAsset = AssetDatabase.LoadAssetAtPath<QualitySettings>("ProjectSettings/QualitySettings.asset");
            string currentDefaultQualityName = QualitySettings.names[QualitySettings.GetQualityLevel()]; // fallback

            if (qualitySettingsAsset != null)
            {
                var serializedObject = new SerializedObject(qualitySettingsAsset);
                var perPlatformDefaultQuality = serializedObject.FindProperty("m_PerPlatformDefaultQuality");

                // Find current default for Android platform
                for (int i = 0; i < perPlatformDefaultQuality.arraySize; i++)
                {
                    var element = perPlatformDefaultQuality.GetArrayElementAtIndex(i);
                    var first = element.FindPropertyRelative("first");
                    if (first.stringValue == "Android")
                    {
                        var second = element.FindPropertyRelative("second");
                        int currentIndex = second.intValue;
                        if (currentIndex >= 0 && currentIndex < QualitySettings.names.Length)
                        {
                            currentDefaultQualityName = QualitySettings.names[currentIndex];
                        }
                        break;
                    }
                }
            }
            return currentDefaultQualityName;
        }

        private static void XRSceneSetup()
        {
            // Get the current build settings scenes
            var buildScenes = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> newScenes = new List<EditorBuildSettingsScene>();

            if (buildScenes.Length == 0)
            {
                foreach (var vrScene in vrScenes)
                {
                    EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(vrScene, true);
                    newScenes.Add(newScene);
                }
                
                EditorBuildSettings.scenes = newScenes.ToArray();
            }
            else
            {
                foreach (var vrScene in vrScenes)
                {
                    if(Array.Exists(buildScenes, x => x.path == vrScene))
                    {
                        buildScenes.First(x => x.path == vrScene).enabled = true;
                    }
                    else
                    {
                        EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(vrScene, true);
                        newScenes.Add(newScene);
                    }
                }
            }

            // Optionally, disable other existing scenes
            foreach (var scene in buildScenes)
            {
                if (noneVRScenes.Contains(scene.path))
                {
                    scene.enabled = false;
                }
            }

            if (newScenes.Count > 0)
            {
                var buildScenesInList = buildScenes.ToList();
                buildScenesInList.AddRange(newScenes);
                buildScenes = buildScenesInList.ToArray();
            }

            // Set the new build settings scenes
            EditorBuildSettings.scenes = buildScenes;
            
            EditorSceneManager.SaveOpenScenes();
            
            EditorSceneManager.OpenScene(vrScenes[0], OpenSceneMode.Single);
        }

        [MenuItem("Tools/XR/Disable XR Setup")]
        public static void DisableSetupForStandaloneVR()
        {
#region Scene Setup
            // Get the current build settings scenes
            var buildScenes = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> newScenes = new List<EditorBuildSettingsScene>();

            if (buildScenes.Length == 0)
            {
                foreach (var normalScene in noneVRScenes)
                {
                    EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(normalScene, true);
                    newScenes.Add(newScene);
                }
                
                EditorBuildSettings.scenes = newScenes.ToArray();
            }
            else
            {
                foreach (var normalScene in noneVRScenes)
                {
                    if(Array.Exists(buildScenes, x => x.path == normalScene))
                    {
                        buildScenes.First(x => x.path == normalScene).enabled = true;
                    }
                    else
                    {
                        EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(normalScene, true);
                        newScenes.Add(newScene);
                    }
                }
            }

            // Optionally, disable other existing scenes
            foreach (var scene in buildScenes)
            {
                if (vrScenes.Contains(scene.path))
                {
                    scene.enabled = false;
                }
            }

            if (newScenes.Count > 0)
            {
                var buildScenesInList = buildScenes.ToList();
                buildScenesInList.AddRange(newScenes);
                buildScenes = buildScenesInList.ToArray();
            }

            // Set the new build settings scenes
            EditorBuildSettings.scenes = buildScenes;

            EditorSceneManager.SaveOpenScenes();
            
            EditorSceneManager.OpenScene(noneVRScenes[0], OpenSceneMode.Single);
#endregion

#region Quality Settings
            // Restore previous quality level by name, or use fallback
            string targetQualityName = null;

            if (EditorPrefs.HasKey("PreviousQualityLevelName"))
            {
                targetQualityName = EditorPrefs.GetString("PreviousQualityLevelName");
                EditorPrefs.DeleteKey("PreviousQualityLevelName");
            }
            else
            {
                // Fallback: find the next quality level after "Standalone VR"
                string[] qualityNames = QualitySettings.names;
                int vrQualityIndex = Array.FindIndex(qualityNames, name => name.Equals("Standalone VR", StringComparison.OrdinalIgnoreCase));
                
                if (vrQualityIndex >= 0 && vrQualityIndex + 1 < qualityNames.Length)
                {
                    targetQualityName = qualityNames[vrQualityIndex + 1];
                }
                else if (qualityNames.Length > 0)
                {
                    // If "Standalone VR" is last or not found, use first quality level
                    targetQualityName = qualityNames[0];
                }
            }

            if (!string.IsNullOrEmpty(targetQualityName))
            {
                string[] qualityNames = QualitySettings.names;
                int targetQualityIndex = Array.FindIndex(qualityNames, name => name.Equals(targetQualityName, StringComparison.OrdinalIgnoreCase));

                SetDefaultQualityLevel(targetQualityIndex);
            }
#endregion

            // Restore the global default render pipeline saved before VR setup.
            RestoreRenderPipeline();
            
#region Setup XR Plugin Management

        if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
        {
            LoaderControl.EnableLoader(BuildTarget.Android, typeof(ARCoreLoader));
        }
        
        //Enable this if want to use Oculus Loader
        /*if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
        {
            LoaderControl.DisableLoader(BuildTarget.Android, typeof(OculusLoader));
        }*/
        
        if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OpenXRLoader)))
        {
            LoaderControl.DisableLoader(BuildTarget.Android, typeof(OpenXRLoader));
        }
            
#endregion
            
#region Remove VR Mode from Scripting Define Symbols
            
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode)))
            {
                symbols = symbols.Where(x => !string.Equals(x, k_VRMode)).ToArray();
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, symbols);
            }
            
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode)))
            {
                symbols = symbols.Where(x => !string.Equals(x, k_VRMode)).ToArray();
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, symbols);
            }
            
#endregion
        }

        private static void SetDefaultQualityLevel(int targetQualityIndex)
        {
            if (targetQualityIndex >= 0)
            {
                // Set quality level for Android platform
                QualitySettings.SetQualityLevel(targetQualityIndex, true);

                // Update the QualitySettings asset directly
                var qualitySettingsAsset = AssetDatabase.LoadAssetAtPath<QualitySettings>("ProjectSettings/QualitySettings.asset");
                if (qualitySettingsAsset != null)
                {
                    var serializedObject = new SerializedObject(qualitySettingsAsset);
                    var perPlatformDefaultQuality = serializedObject.FindProperty("m_PerPlatformDefaultQuality");

                    // Set for Android
                    for (int i = 0; i < perPlatformDefaultQuality.arraySize; i++)
                    {
                        var element = perPlatformDefaultQuality.GetArrayElementAtIndex(i);
                        var first = element.FindPropertyRelative("first");
                        if (first.stringValue == "Android")
                        {
                            var second = element.FindPropertyRelative("second");
                            second.intValue = targetQualityIndex;
                            break;
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(qualitySettingsAsset);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Quality settings restored to: {QualitySettings.names[targetQualityIndex]}");
            }
            else
            {
                Debug.LogWarning($"Target quality level '{{QualitySettings.names[targetQualityIndex]}}' not found. Available levels: " + string.Join(", ", QualitySettings.names));
            }
        }

        // Pins the given URP asset as the active render pipeline: assigns it to the now-active
        // VR quality level and to the global GraphicsSettings default. Call AFTER the quality
        // level has been switched (SetDefaultQualityLevel) so QualitySettings.renderPipeline
        // targets the VR level. The pre-existing global default is remembered once so
        // "Disable XR Setup" can restore it.
        private static void ApplyRenderPipeline(string urpAssetPath)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(urpAssetPath);
            if (pipeline == null)
            {
                Debug.LogWarning($"Render pipeline asset not found at '{urpAssetPath}'. Render pipeline not changed.");
                return;
            }

            // Remember the original default only once, so switching between VR setups (or
            // re-running a setup) never overwrites the true non-VR default.
            if (!EditorPrefs.HasKey(k_PreviousDefaultRpKey))
            {
                var currentDefault = GraphicsSettings.defaultRenderPipeline;
                var currentPath = currentDefault != null ? AssetDatabase.GetAssetPath(currentDefault) : string.Empty;
                EditorPrefs.SetString(k_PreviousDefaultRpKey, currentPath);
                Debug.Log($"Saved previous default render pipeline: {(string.IsNullOrEmpty(currentPath) ? "<none>" : currentPath)}");
            }

            // Active quality level was already switched to the VR level by SetDefaultQualityLevel.
            QualitySettings.renderPipeline = pipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            Debug.Log($"Render pipeline set to: {urpAssetPath}");
        }

        // Restores the global default render pipeline saved before the first VR setup
        // (falls back to URP-HighFidelity). Per-quality VR-level assignments are left intact.
        private static void RestoreRenderPipeline()
        {
            string targetPath = EditorPrefs.HasKey(k_PreviousDefaultRpKey)
                ? EditorPrefs.GetString(k_PreviousDefaultRpKey)
                : k_DefaultUrpPath;
            EditorPrefs.DeleteKey(k_PreviousDefaultRpKey);

            if (string.IsNullOrEmpty(targetPath)) targetPath = k_DefaultUrpPath;

            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(targetPath);
            if (pipeline == null)
            {
                Debug.LogWarning($"Could not restore default render pipeline from '{targetPath}'.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            Debug.Log($"Default render pipeline restored to: {targetPath}");
        }
    }
}