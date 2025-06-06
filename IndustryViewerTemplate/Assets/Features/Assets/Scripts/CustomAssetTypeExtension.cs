using UnityEngine.Localization;
using Unity.Cloud.Assets;
using System;
using System.Threading.Tasks;

namespace Unity.Industry.Viewer.Assets
{
    public static class CustomAssetTypeExtension
    {
        private const string k_AssetsTableKey = "Assets";
        private const string k_AssetType_2D = "Type 2D Asset";
        private const string k_AssetType_3D = "Type 3D Model";
        private const string k_AssetType_Audio = "Type Audio";
        private const string k_AssetType_Material = "Type Material";
        private const string k_AssetType_Other = "Type Other";
        private const string k_AssetType_Script = "Type Script";
        private const string k_AssetType_Video = "Type Video";
        private const string k_AssetType_UnityEditor = "Type Unity Editor";
        
        private static LocalizedString m_AssetType_2DLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_2D);
        private static LocalizedString m_AssetType_3DLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_3D);
        private static LocalizedString m_AssetType_AudioLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_Audio);
        private static LocalizedString m_AssetType_MaterialLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_Material);
        private static LocalizedString m_AssetType_OtherLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_Other);
        private static LocalizedString m_AssetType_ScriptLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_Script);
        private static LocalizedString m_AssetType_VideoLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_Video);
        private static LocalizedString m_AssetType_UnityEditorLocalizedString = new LocalizedString(k_AssetsTableKey, k_AssetType_UnityEditor);
        
        public static LocalizedString GetAssetTypeAsString(this AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset_2D => m_AssetType_2DLocalizedString,
                AssetType.Model_3D => m_AssetType_3DLocalizedString,
                AssetType.Audio => m_AssetType_AudioLocalizedString,
                AssetType.Material => m_AssetType_MaterialLocalizedString,
                AssetType.Other => m_AssetType_OtherLocalizedString,
                AssetType.Script => m_AssetType_ScriptLocalizedString,
                AssetType.Video => m_AssetType_VideoLocalizedString,
                AssetType.Unity_Editor => m_AssetType_UnityEditorLocalizedString,
                _ => null
            };
        }

        public static AssetType[] AssetTypeList()
        {
            return (AssetType[])Enum.GetValues(typeof(AssetType));
        }
    }
}
