/// <summary>
/// The script gives you choice to whether to build addressable bundles when clicking the build button.
/// For custom build script, call PreExport method yourself.
/// For cloud build, put BuildAddressablesProcessor.PreExport as PreExport command.
/// Discussion: https://forum.unity.com/threads/how-to-trigger-build-player-content-when-build-unity-project.689602/
///
/// License: The MIT License https://opensource.org/licenses/MIT
/// </summary>
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

class BuildAddressablesProcessor
{
    /// <summary>
    /// Run a clean build before export.
    /// </summary>
    static public void PreExport()
    {
        AddressableAssetSettings.CleanPlayerContent(
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent();
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
    }

    private static void BuildPlayerHandler(BuildPlayerOptions options)
    {
        if (EditorUtility.DisplayDialog("Build with Addressables",
                "Do you want to build a clean addressables before export?",
                "Build with Addressables", "Skip"))
        {
            PreExport();
        }
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

}