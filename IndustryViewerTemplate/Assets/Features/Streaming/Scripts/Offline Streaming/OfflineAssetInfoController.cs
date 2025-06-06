using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Unity.Industry.Viewer.Streaming
{
    public class OfflineAssetInfoController : AssetInfoUIBaseController
    {
        public OfflineAssetInfoController() : base()
        {
            
        }
        
        private void OnAssetProjectChanged(AssetProjectInfo? arg1)
        {
            CloseInfoPanel();
        }

        private void OnOrganizationChanged(IOrganization newOrg)
        {
            CloseInfoPanel();
        }

        protected override void StatusBindItem(DropdownItem item, int index)
        {
            if(!SharedUIManager.SelectedAsset.HasValue || SharedUIManager.SelectedAsset.Value.Asset is not OfflineAsset) return;
            var currentAssetStatus = ((OfflineAsset)SharedUIManager.SelectedAsset.Value.Asset).OfflineAssetInfo.projectStatus
                .GetAssetStatusFromString();
            UpdateStatusBinding(currentAssetStatus, item, index);
        }

        protected override void CloseInfoPanel()
        {
            base.CloseInfoPanel();
            OfflineModeAssetsController.AssetDeselected?.Invoke();
        }

        protected override void AssetVersionDropdownBindItem(DropdownItem arg1, int arg2)
        {
            var versions = m_AssetVersionDropdown.sourceItems as List<int>;
            if (versions == null)
            {
                return;
            }
            int version = versions[arg2];
            var localVersionLocalizedString = new LocalizedString(k_AssetLocalisedTable, k_VersionKey);
            localVersionLocalizedString.Add("num", new IntVariable{ Value = version});
            
            var text = arg1.Q<LocalizedTextElement>();
            text.SetBinding("text", localVersionLocalizedString);
        }

        public override void DisposeUI()
        {
            UnregisterCallbacks();
        }

        public override void RegisterCallbacks()
        {
            OfflineModeAssetsController.AssetSelected -= AssetSelected;
            OfflineModeAssetsController.AssetSelected += AssetSelected;
            
            OfflineModeAssetsController.AssetDeselected -= OnDeselectAsset;
            OfflineModeAssetsController.AssetDeselected += OnDeselectAsset;
            
            SharedUIManager.OrganizationSelected -= OnOrganizationChanged;
            SharedUIManager.OrganizationSelected += OnOrganizationChanged;
            
            SharedUIManager.AssetProjectSelected -= OnAssetProjectChanged;
            SharedUIManager.AssetProjectSelected += OnAssetProjectChanged;
            
            m_AssetVersionDropdown.bindItem -= AssetVersionDropdownBindItem;
            m_AssetVersionDropdown.bindItem += AssetVersionDropdownBindItem;
            
            m_AssetStatusDropdown.bindItem -= StatusBindItem;
            m_AssetStatusDropdown.bindTitle -= StatusBindItem;
            m_AssetStatusDropdown.makeItem -= StatusMakeItem;
            m_AssetStatusDropdown.makeTitle -= StatusMakeItem;
            
            m_AssetStatusDropdown.bindItem += StatusBindItem;
            m_AssetStatusDropdown.bindTitle += StatusBindItem;
            m_AssetStatusDropdown.makeItem += StatusMakeItem;
            m_AssetStatusDropdown.makeTitle += StatusMakeItem;
        }

        public override void UnregisterCallbacks()
        {
            m_AssetVersionDropdown.bindItem -= AssetVersionDropdownBindItem;
            
            m_AssetStatusDropdown.bindItem -= StatusBindItem;
            m_AssetStatusDropdown.bindTitle -= StatusBindItem;
            m_AssetStatusDropdown.makeItem -= StatusMakeItem;
            m_AssetStatusDropdown.makeTitle -= StatusMakeItem;
            
            OfflineModeAssetsController.AssetSelected -= AssetSelected;
            
            OfflineModeAssetsController.AssetDeselected -= OnDeselectAsset;
            
            SharedUIManager.OrganizationSelected -= OnOrganizationChanged;
            
            SharedUIManager.AssetProjectSelected -= OnAssetProjectChanged;
        }

        protected override void UpdateAssetUI(AssetInfo asset)
        {
            base.UpdateAssetUI(asset);
            
            var fileTabs = m_Tabs.items[2];
            (fileTabs as TabItem)?.SetEnabled(false);
            
            OfflineAsset offlineAsset = (OfflineAsset)asset.Asset;
            OfflineAssetInfo offlineAssetInfo = offlineAsset.OfflineAssetInfo;
            m_AssetNameLabel.text = offlineAsset.OfflineAssetInfo.assetName;
            
            m_AssetTypeLabel.SetBinding("text", offlineAsset.OfflineAssetInfo.assetType.GetAssetTypeAsString());
            
            AssignTags(offlineAsset.OfflineAssetInfo.tags);
            
            m_AssetVersionDropdown.SetEnabled(false);
            
            m_AssetVersionDropdown.SetValueWithoutNotify(null);
            m_AssetVersionDropdown.userData = null;
            m_AssetVersionDropdown.sourceItems = null;
            
            m_AssetVersionDropdown.sourceItems = new List<int>() {offlineAsset.OfflineAssetInfo.assetVersion};
            m_AssetVersionDropdown.SetValueWithoutNotify(new int[] {0});
            
            m_AssetStatusDropdown.sourceItems = new List<AssetStatus>()
            {
                offlineAssetInfo.projectStatus.GetAssetStatusFromString()
            };
            m_AssetStatusDropdown.SetValueWithoutNotify(new int[]{ 0 });
            m_AssetStatusDropdown.SetEnabled(false);
            
            OnRetrieveLinkedProjects(new List<(string name, string id, bool source)>()
            {
                (offlineAssetInfo.projectName, offlineAsset.Descriptor.ProjectId.ToString(), true)
            });
            
            UpdateAssetUpdateLabel(offlineAssetInfo.lastModified);
            
            UpdateAssetCreationLabel(offlineAssetInfo.created);
            
            m_AssetFilesSizeLabel.text = "N/A";
            m_AssetFilesLabel.text = "N/A";

            m_AssetUpdateByLabel.text = offlineAssetInfo.lastModifiedBy;
            m_AssetCreateByLabel.text = offlineAssetInfo.createdBy;
            
            _ = TextureDownload.DownloadThumbnail(offlineAsset.Descriptor.AssetId.GetHashCode(), offlineAssetInfo.previewPic, OnTextureDownloaded);
        }
    }
}
