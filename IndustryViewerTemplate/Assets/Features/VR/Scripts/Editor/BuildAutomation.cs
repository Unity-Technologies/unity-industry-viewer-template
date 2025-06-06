using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.XR.ARCore;
using Unity.XR.Oculus;

namespace Unity.Industry.Viewer.Navigation.VR.Editor
{
    public class BuildAutomation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform is not BuildTarget.Android) return;
            
#if VR_MODE
            if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
            {
                LoaderControl.DisableLoader(BuildTarget.Android, typeof(ARCoreLoader));
            }
                    
            if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
            {
                LoaderControl.EnableLoader(BuildTarget.Android, typeof(OculusLoader));
            }
#else
            if (!LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(ARCoreLoader)))
            {
                LoaderControl.EnableLoader(BuildTarget.Android, typeof(ARCoreLoader));
            }
        
            if (LoaderControl.IsLoaderEnabled(BuildTarget.Android, typeof(OculusLoader)))
            {
                LoaderControl.DisableLoader(BuildTarget.Android, typeof(OculusLoader));
            }
#endif
        }
    }
}
