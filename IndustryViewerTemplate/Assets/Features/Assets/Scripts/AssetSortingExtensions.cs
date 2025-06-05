using System;
using Unity.Cloud.Assets;
using UnityEngine.Localization;
using System.Threading.Tasks;

namespace Unity.Industry.Viewer.Assets
{
    public enum SortingType
    {
        Name,
        Upload_date,
        Last_modified,
        //Description,
        Asset_type,
        Status
    }
    
    public static class AssetSortingExtensions
    {
        private const string k_AssetsTableKey = "Assets";
        private const string k_SortingName = "Sorting Name";
        private const string k_SortingUploadDate = "Sorting Upload Date";
        private const string k_SortingLastModified = "Sorting Last Modified";
        //private const string k_SortingDescription = "Sorting Description";
        private const string k_SortingAssetType = "Asset Type";
        private const string k_SortingStatus = "Sorting Status";
        
        private static LocalizedString m_SortingNameLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingName);
        private static LocalizedString m_SortingUploadDateLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingUploadDate);
        private static LocalizedString m_SortingLastModifiedLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingLastModified);
        //private static LocalizedString m_SortingDescriptionLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingDescription);
        private static LocalizedString m_SortingAssetTypeLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingAssetType);
        private static LocalizedString m_SortingStatusLocalizedString = new LocalizedString(k_AssetsTableKey, k_SortingStatus);
        
        public static LocalizedString GetValueAsString(this SortingType sortingType)
        {
            return sortingType switch
            {
                SortingType.Name => m_SortingNameLocalizedString,
                SortingType.Upload_date => m_SortingUploadDateLocalizedString,
                SortingType.Last_modified => m_SortingLastModifiedLocalizedString,
                //SortingType.Description => m_SortingDescriptionLocalizedString,
                SortingType.Asset_type => m_SortingAssetTypeLocalizedString,
                SortingType.Status => m_SortingStatusLocalizedString,
                _ => null
            };
        }

        public static string GetPropertyName(this SortingType sortingType)
        {
            switch (sortingType)
            {
                case SortingType.Name:
                    return "name";
                case SortingType.Upload_date:
                    return "created";
                case SortingType.Last_modified:
                    return "updated";
                case SortingType.Status:
                    return "status";
                /*case SortingType.Description:
                    return "description";*/
                case SortingType.Asset_type:
                    return "primaryType";
                
            }

            return new AssetSearchFilter().Include().Name.PropertyName;
        }
        
        public static SortingType[] AssetTypeList()
        {
            return (SortingType[])Enum.GetValues(typeof(SortingType));
        }
    }
}
