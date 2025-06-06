using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
#if VR_MODE
using Unity.Industry.Viewer.Navigation.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.AddModel
{
    // This script manages the addition of models in a Unity project.
    // It integrates with Unity's MonoBehaviour for lifecycle management and supports both online and offline modes.
    // The script also includes VR-specific handling for interactions and model manipulation.
    public class AddModelToolController : MonoBehaviour
    {
        // Events for tool lifecycle management
        public HashSet<AssetInfo> SelectedAssets => m_SelectedAssets;
        
        private HashSet<AssetInfo> m_SelectedAssets;
        
        private StreamingModelController m_StreamingModelController;

        private void Start()
        {
            m_SelectedAssets ??= new HashSet<AssetInfo>();
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
        }

        public bool ManageSelectedAssets(AssetInfo assetInfo, bool add)
        {
            if (add)
            {
                if (m_SelectedAssets.Any(x => x.Asset.Descriptor == assetInfo.Asset.Descriptor))
                {
                    return true;
                }
                m_SelectedAssets.Add(assetInfo);
                return true;
            }

            if (m_SelectedAssets.All(x => x.Asset.Descriptor != assetInfo.Asset.Descriptor))
            {
                return false;
            }

            m_SelectedAssets.Remove(assetInfo);
            return false;
        }
        
        public void ClearSelectedAssets()
        {
            m_SelectedAssets.Clear();
        }

        public void AddToScene()
        {
            var newLayoutJson = new LayoutJson();
            foreach (var selectedAsset in m_SelectedAssets)
            {
                newLayoutJson.LayoutModels ??= new List<LayoutModelEntity>();
                newLayoutJson.LayoutModels.Add(new LayoutModelEntity()
                {
                    assetID = selectedAsset.Asset.Descriptor.AssetId.ToString(),
                    orgID = selectedAsset.Asset.Descriptor.OrganizationId.ToString(),
                    projectID = selectedAsset.Asset.Descriptor.ProjectDescriptor.ProjectId.ToString(),
                    versionID = selectedAsset.Asset.Descriptor.AssetVersion.ToString(),
                    version = selectedAsset.Properties?.FrozenSequenceNumber ?? 0,
                });
            }
            m_SelectedAssets.Clear();
            if (newLayoutJson.LayoutModels == null || newLayoutJson.LayoutModels.Count == 0) return;
            _ = m_StreamingModelController.ProcessLayoutJson(newLayoutJson);
        }
    }
}
