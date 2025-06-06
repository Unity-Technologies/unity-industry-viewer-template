using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using Unity.XR.Oculus;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.OpenXR;

namespace Unity.Industry.Viewer.Navigation.VR.Editor
{
    public static class LoaderControl
    {
        const string k_ARCoreLoader = "ARCoreLoader";
        const string k_OculusLoader = "OculusLoader";
        const string k_OpenXRLoader = "OpenXRLoader";

        static void GetXRManagerSettings(BuildTarget buildTarget, out BuildTargetGroup buildTargetGroup, out XRManagerSettings manager)
        {
            buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
            if (xrSettings == null)
            {
                manager = null;
                return;
            }

            manager = xrSettings.Manager;
        }
        
        public static bool IsLoaderEnabled(BuildTarget buildTarget, Type loaderType)
        {
            GetXRManagerSettings(buildTarget, out _, out var manager);
            if (manager == null)
                return false;

            var activeLoaders = manager.activeLoaders;
            return activeLoaders != null && activeLoaders.Any(x => x.GetType() == loaderType);
        }

        public static void EnableLoader(BuildTarget buildTarget, Type loaderType)
        {
            GetXRManagerSettings(buildTarget, out var buildTargetGroup, out var manager);
            if(manager == null) return;

            string loaderName = string.Empty;
            
            switch (loaderType)
            {
                case not null when loaderType == typeof(ARCoreLoader):
                    // Add any specific logic for ARCoreLoader here
                    loaderName = k_ARCoreLoader;
                    break;
                
                case not null when loaderType == typeof(OculusLoader):
                    loaderName = k_OculusLoader;
                    break;
                
                case not null when loaderType == typeof(OpenXRLoader):
                    loaderName = k_OpenXRLoader;
                    break;
            }
            
            if (string.IsNullOrEmpty(loaderName))
                return;
            
            if(!XRPackageMetadataStore.AssignLoader(manager, loaderName, buildTargetGroup))
                Debug.LogError("Failed to assign loader to XR Manager Settings");
        }
        
        public static void DisableLoader(BuildTarget buildTarget, Type loaderType)
        {
            GetXRManagerSettings(buildTarget, out var buildTargetGroup, out var manager);
            if(manager == null) return;

            string loaderName = string.Empty;
            
            switch (loaderType)
            {
                case not null when loaderType == typeof(ARCoreLoader):
                    // Add any specific logic for ARCoreLoader here
                    loaderName = k_ARCoreLoader;
                    break;
                
                case not null when loaderType == typeof(OculusLoader):
                    loaderName = k_OculusLoader;
                    break;
                
                case not null when loaderType == typeof(OpenXRLoader):
                    loaderName = k_OpenXRLoader;
                    break;
            }
            
            if (string.IsNullOrEmpty(loaderName))
                return;
            
            if(!XRPackageMetadataStore.RemoveLoader(manager, loaderName, buildTargetGroup))
                Debug.LogError("Failed to remove loader from XR Manager Settings");
        }
    }
}
