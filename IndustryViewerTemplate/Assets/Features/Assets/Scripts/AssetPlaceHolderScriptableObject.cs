using System;
using System.Linq;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.Industry.Viewer.Assets
{
    [CreateAssetMenu(menuName = "UC/Assets/AssetPlaceHolderScriptableObject")]
    public class AssetPlaceHolderScriptableObject : ScriptableObject
    {
        [Serializable]
        public class AssetPlaceHolder
        {
            public AssetType assetType;
            public Texture2D assetTypeIcon;
        }
        
        [SerializeField]
        private AssetPlaceHolder[] m_AssetPlaceHolders;
        
        public Texture2D GetAssetTypeIcon(AssetType assetType)
        {
            var firstOrDefault = m_AssetPlaceHolders.FirstOrDefault(x => x.assetType == assetType);
            return firstOrDefault == null ? m_AssetPlaceHolders.FirstOrDefault(x => x.assetType == AssetType.Other)?.assetTypeIcon : firstOrDefault.assetTypeIcon;
        }
    }
}
