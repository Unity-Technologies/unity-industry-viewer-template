using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.Assets;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamingModel : MonoBehaviour
    {
        public static Action<StreamingModel> OnActivityStateChanged;
        
        private IModelStream m_ModelStream;
        private IAsset m_Asset;
        private IDataset m_Dataset;
        private AssetProperties? m_AssetProperties;
        private int m_InstanceNumber;
        private string m_AnchorId;

        public string AssetId => m_Asset.Descriptor.AssetId.ToString();

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
        
        public int Version
        {
            get
            {
                if (m_Asset != null && m_Asset is not OfflineAsset && m_AssetProperties.HasValue)
                {
                    return m_AssetProperties.Value.FrozenSequenceNumber;
                }
                if (m_Asset is OfflineAsset offlineAsset)
                {
                    return offlineAsset.OfflineAssetInfo.assetVersion;
                }
                return 0;
            }
        }

        public string VersionID => m_Asset != null ? m_Asset.Descriptor.AssetVersion.ToString() : string.Empty;
        
        public ModelStreamId ModelStreamId => m_ModelStream.Id;
        public IModelStream ModelStream => m_ModelStream;
        public IAsset Asset => m_Asset;
        public IDataset Dataset => m_Dataset;
        public int InstanceNumber => m_InstanceNumber;
        public bool IsStreaming { get; private set; }

        // Immutable, per-instance id (a GUID minted once at creation, persisted in the layout and synced in
        // multiplayer). Used to anchor collaboration annotations to a specific model instance, decoupled from
        // the mutable gameObject.name so a load-time rename can't orphan an annotation.
        public string AnchorId => m_AnchorId;

        // Resolves a loaded model child by its AnchorId, used to re-anchor collaboration annotations to the
        // specific model they were placed on. Falls back to a gameObject.name match so annotations authored
        // before AnchorId existed still resolve. Returns null when the id is empty, there is no
        // TransformController, or no model matches.
        public static Transform FindModelTransformByAnchorId(string id)
        {
            if (string.IsNullOrEmpty(id) || TransformController.Instance == null) return null;
            var models = TransformController.Instance.GetComponentsInChildren<StreamingModel>(true);
            return (models.FirstOrDefault(model => model.AnchorId == id)
                    ?? models.FirstOrDefault(model => model.gameObject.name == id))?.transform;
        }

        // Resolves the loaded model whose stream matches the given id (e.g. from a raycast hit).
        public static StreamingModel FindByModelStreamId(ModelStreamId id)
        {
            if (TransformController.Instance == null) return null;
            return TransformController.Instance
                .GetComponentsInChildren<StreamingModel>(true)
                .FirstOrDefault(model => model.ModelStream != null && model.ModelStream.Id == id);
        }

        public void Initialize(
            IModelStream modelStream,
            AssetInfo asset,
            IDataset dataset,
            bool isStreaming,
            int? instanceNumber = null,
            string anchorId = null)
        {
            m_ModelStream = modelStream;
            m_AssetProperties = asset.Properties;
            m_Asset = asset.Asset;
            m_Dataset = dataset;
            IsStreaming = isStreaming;
            m_InstanceNumber = instanceNumber is null or 0 ? GetInstanceNumber() : instanceNumber.Value;
            // Seed from the model's initial name (deterministic "{assetId}@1" for the primary/first model,
            // "{assetId}@{guid}" for others) so a self-loaded model has a stable id across sessions and
            // multiplayer clients; a persisted/synced anchorId (from a layout or another client) overrides
            // it. This value is frozen here and never reassigned, so a later gameObject.name rename can't
            // orphan an annotation.
            m_AnchorId = string.IsNullOrEmpty(anchorId) ? gameObject.name : anchorId;
        }

        public void Initialize(
            IModelStream modelStream,
            AssetInfo offlineAsset,
            bool isStreaming,
            int? instanceNumber = null,
            string anchorId = null)
        {
            m_Asset = offlineAsset.Asset;
            m_ModelStream = modelStream;
            IsStreaming = isStreaming;
            m_InstanceNumber = instanceNumber is null or 0 ? GetInstanceNumber() : instanceNumber.Value;
            // Seed from the model's initial name (deterministic "{assetId}@1" for the primary/first model,
            // "{assetId}@{guid}" for others) so a self-loaded model has a stable id across sessions and
            // multiplayer clients; a persisted/synced anchorId (from a layout or another client) overrides
            // it. This value is frozen here and never reassigned, so a later gameObject.name rename can't
            // orphan an annotation.
            m_AnchorId = string.IsNullOrEmpty(anchorId) ? gameObject.name : anchorId;
        }

        private int GetInstanceNumber()
        {
            var streamingModels = new List<StreamingModel>();
            TransformController.Instance.GetComponentsInChildren(true, streamingModels);
            var assetId = AssetId;

            return streamingModels
                .Where(streamingModel => streamingModel.AssetId == assetId)
                .Select(streamingModel => streamingModel.InstanceNumber)
                .DefaultIfEmpty()
                .Max() + 1;
        }

        private void OnEnable()
        {
            if(m_ModelStream == null) return;
            m_ModelStream.Visibility?.Set(true);
            OnActivityStateChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            if(m_ModelStream == null) return;
            m_ModelStream.Visibility?.Set(false);
            OnActivityStateChanged?.Invoke(this);
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
