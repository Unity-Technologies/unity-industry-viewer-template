using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine.XR.ARCore;
using Unity.XR.Oculus;

namespace Unity.Industry.Viewer.Navigation.VR.Editor
{
    public static class VRSetup
    {
        private const string k_VRMode = "VR_MODE";
        
        [MenuItem("Tools/VR/Setup VR")]
        public static void SetupVRForDesktop()
        {
#region Scene Setup

            // List of VR scenes to add
            string[] vrScenes = new string[]
            {
                "Assets/Scenes/Main VR.unity",
                "Assets/Scenes/Streaming VR.unity"
            };

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
                if (!vrScenes.Contains(scene.path))
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

#endregion
            
#region Setup XR Plugin Management
            
            if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
            {
                LoaderControl.DisableLoader(BuildTarget.Android, typeof(ARCoreLoader));
            }
                    
            if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
            {
                LoaderControl.EnableLoader(BuildTarget.Android, typeof(OculusLoader));
            }
            
#endregion

#region Add VR Mode to Scripting Define Symbols

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode))) return;
            symbols = symbols.Append(k_VRMode).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, symbols);
            
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out symbols);
            if(symbols.Any(x => string.Equals(x, k_VRMode))) return;
            symbols = symbols.Append(k_VRMode).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, symbols);

#endregion
        }

        [MenuItem("Tools/VR/Disable VR Setup")]
        public static void DisableVRSetupForDesktop()
        {
#region Scene Setup
            
            // List of VR scenes to remove
            string[] noneVRScenes = new string[]
            {
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Streaming.unity"
            };

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
                if (!noneVRScenes.Contains(scene.path))
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
            
#endregion
            
#region Setup XR Plugin Management

        if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
        {
            LoaderControl.EnableLoader(BuildTarget.Android, typeof(ARCoreLoader));
        }
        
        if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
        {
            LoaderControl.DisableLoader(BuildTarget.Android, typeof(OculusLoader));
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
    }
}