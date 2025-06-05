using System;
using Unity.Cloud.Assets;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamingModel : MonoBehaviour
    {
        public string AssetId => m_Asset.Descriptor.AssetId.ToString();
        
        public static Action<StreamingModel> OnActivityStateChanged;

        public string AssetName
        {
            get
            {
                if (m_Asset != null && m_Asset is not OfflineAsset && m_AssetProperties.HasValue)
                {
                    return m_AssetProperties.Value.Name;
                }
                if (m_Asset is OfflineAsset offlineAsset)
                {
                    return offlineAsset.OfflineAssetInfo.assetName;
                }

                return string.Empty;
            }
        }
        
        public string ProjectID => m_Asset != null ? m_Asset.Descriptor.ProjectId.ToString() : string.Empty;
        
        public string OrgID => m_Asset != null ? m_Asset.Descriptor.OrganizationId.ToString() : string.Empty;
        
        public int Version => m_AssetProperties?.FrozenSequenceNumber ?? 0;
        
        public string VersionID => m_Asset != null ? m_Asset.Descriptor.AssetVersion.ToString() : string.Empty;
        
        public IModelStream ModelStream => m_ModelStream;
        public IAsset Asset => m_Asset;
        public IDataset Dataset => m_Dataset;
        
        private IModelStream m_ModelStream;
        private IAsset m_Asset;
        private IDataset m_Dataset;
        
        private AssetProperties? m_AssetProperties;
        
        public bool IsStreaming { get; private set; }

        public void Initialize(IModelStream modelStream, AssetInfo asset, IDataset dataset, bool isStreaming)
        {
            m_ModelStream = modelStream;
            m_AssetProperties = asset.Properties;
            m_Asset = asset.Asset;
            m_Dataset = dataset;
            IsStreaming = isStreaming;
        }
        
        public void Initialize(IModelStream modelStream, AssetInfo offlineAsset, bool isStreaming)
        {
            m_Asset = offlineAsset.Asset;
            m_ModelStream = modelStream;
            IsStreaming = isStreaming;
        }

        private void OnEnable()
        {
            if(m_ModelStream == null) return;
            m_ModelStream.Visibility?.Set(true);
            OnActivityStateChanged.Invoke(this);
        }

        private void OnDisable()
        {
            if(m_ModelStream == null) return;
            m_ModelStream.Visibility?.Set(false);
            OnActivityStateChanged.Invoke(this);
        }

        public override bool Equals(object obj)
        {
            if (obj is StreamingModel other)
            {
                return AssetId == other.AssetId &&
                       AssetName == other.AssetName &&
                       gameObject.name == other.gameObject.name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (AssetId, AssetName, gameObject.name).GetHashCode();
        }
    }
}
