using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Collections;
using Unity.Industry.Viewer.Streaming;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Multiplay
{
    [Serializable]
    public struct ModelSyncData : INetworkSerializable, IEquatable<ModelSyncData>
    {
        public string OrgId;
        public string ProjectId;
        public string AssetId;
        public string AssetVersionId;
        public string GameObjectName;
        public string AnchorId;
        public int InstanceNumber;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrgId);
            serializer.SerializeValue(ref ProjectId);
            serializer.SerializeValue(ref AssetId);
            serializer.SerializeValue(ref GameObjectName);
            serializer.SerializeValue(ref AssetVersionId);
            serializer.SerializeValue(ref InstanceNumber);
            // Serialized last so the added field doesn't shift the layout of the pre-existing fields.
            serializer.SerializeValue(ref AnchorId);
        }

        public override bool Equals(object obj)
        {
            return obj is ModelSyncData other && Equals(other);
        }

        override public int GetHashCode()
        {
            return HashCode.Combine(OrgId, ProjectId, AssetId, AssetVersionId, GameObjectName, AnchorId, InstanceNumber);
        }

        public bool Equals(ModelSyncData other)
        {
            return OrgId == other.OrgId &&
                   ProjectId == other.ProjectId &&
                   AssetId == other.AssetId &&
                   AssetVersionId == other.AssetVersionId &&
                   GameObjectName == other.GameObjectName &&
                   AnchorId == other.AnchorId &&
                   InstanceNumber == other.InstanceNumber;
        }

        public static bool operator ==(ModelSyncData left, ModelSyncData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModelSyncData left, ModelSyncData right)
        {
            return !left.Equals(right);
        }
    }

    // This script synchronizes the addition and removal of models across a network in a Unity project.
    // It handles the initialization, spawning, and synchronization of model data using Unity's Netcode for GameObjects.
    // The script manages network events, such as session ownership changes and model data updates.
    // It supports asynchronous operations to fetch and load asset data from cloud and local sources.
    // The script integrates with Unity's MonoBehaviour for lifecycle management and uses Unity's UI Toolkit for user feedback.
    public class NetworkModelSync : NetworkBehaviour
    {
        private NetworkVariable<List<ModelSyncData>> m_ModelSyncData = new NetworkVariable<List<ModelSyncData>>(new List<ModelSyncData>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        [SerializeField]
        private GameObject m_SyncTransformPrefab;
        
        private StreamingModelController m_StreamingModelController;
        
        public override void OnNetworkSpawn()
        {
            TransformController.ModelAdded += OnModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
            m_StreamingModelController = FindFirstObjectByType<StreamingModelController>();
            NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
            StartCoroutine(Initializing());
        }

        private IEnumerator Initializing()
        {
            while(TransformController.Instance == null ||
                  TransformController.Instance.GetComponent<NetworkTransformController>() == null
                  || TransformController.Instance.transform.childCount == 0 || !m_StreamingModelController.FinishedInitialLoading)
            {
                yield return null;
            }
            
            m_ModelSyncData.OnValueChanged += OnModelDataChanged;
            
            yield return null;

            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                foreach (Transform child in TransformController.Instance.transform)
                {
                    if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                    if (!child.TryGetComponent(out StreamingModel model)) continue;

                    m_ModelSyncData.Value.Add(new ModelSyncData
                    {
                        OrgId = model.Asset.Descriptor.OrganizationId.ToString(),
                        ProjectId = model.Asset.Descriptor.ProjectId.ToString(),
                        AssetId = model.Asset.Descriptor.AssetId.ToString(),
                        AssetVersionId = model.Asset.Descriptor.AssetVersion.ToString(),
                        GameObjectName = child.gameObject.name,
                        AnchorId = model.AnchorId,
                        InstanceNumber = model.InstanceNumber
                    });
                    
                    var syncTransform = Instantiate(m_SyncTransformPrefab);
                    if (!syncTransform.TryGetComponent(out NetworkObject syncModelTransform)) continue;
                    syncModelTransform.Spawn(true);
                    syncModelTransform.GetComponent<SyncModelTransform>().SetValue(child.gameObject);
                }

                m_ModelSyncData.CheckDirtyState();
            }
            else
            {
                if (StreamingModelController.IsLayoutAsset)
                {
                    StartCoroutine(WaitForLayoutAsset());
                }
                else
                {
                    SyncSceneWithNetworkModels();
                }
            }

            IEnumerator WaitForLayoutAsset()
            {
                while (m_StreamingModelController.LayoutJson?.LayoutModels == null)
                {
                    yield return null;
                }

                SyncSceneWithNetworkModels();
            }
        }

        private void SyncSceneWithNetworkModels()
        {
            FoundRemovedModels();
            FindAndAddMissingModels();
        }

        public bool IsSceneSynckedWithNetworkModels()
        {
            var localSceneModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>(true);
            var networkModelList = m_ModelSyncData.Value;

            if (localSceneModels.Length != networkModelList.Count)
            {
                return false;
            }

            if (localSceneModels.Length == 0 && networkModelList.Count == 0)
            {
                return true;
            }

            // Use linked list to support duplicates (just in case)
            var networkModelLinkedList = new LinkedList<ModelSyncData>(networkModelList);
            foreach (var localSceneModel in localSceneModels)
            {
                var node = networkModelLinkedList.First;
                while (node != null)
                {
                    if (node.Value.GameObjectName == localSceneModel.gameObject.name)
                    {
                        networkModelLinkedList.Remove(node);
                        break;
                    }

                    node = node.Next;
                }
            }

            return networkModelLinkedList.Count == 0;
        }

        public override void OnNetworkDespawn()
        {
            NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
            m_ModelSyncData.OnValueChanged -= OnModelDataChanged;
            TransformController.ModelAdded -= OnModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
        }

        /// <summary>
        /// Handle the event from TransformController when a new model is added to scene.
        /// </summary>
        private void OnModelAdded(GameObject newModelAssetObject, ITransformValuesAccessor arg2)
        {
            if(!newModelAssetObject.gameObject.CompareTag(StreamingUtils.StreamModelTag)) return;
            if (!newModelAssetObject.TryGetComponent(out StreamingModel model)) return;

            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                var syncTransform = Instantiate(m_SyncTransformPrefab);
                SceneManager.MoveGameObjectToScene(syncTransform, TransformController.Instance.gameObject.scene);
                if (!syncTransform.TryGetComponent(out NetworkObject syncModelTransform)) return;
                syncModelTransform.Spawn(true);
                syncModelTransform.GetComponent<SyncModelTransform>().SetValue(newModelAssetObject);
            }

            if (m_ModelSyncData.Value.Any(x => string.Equals(x.GameObjectName, newModelAssetObject.name))) return;

            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                m_ModelSyncData.Value.Add(new ModelSyncData
                {
                    OrgId = model.Asset.Descriptor.OrganizationId.ToString(),
                    ProjectId = model.Asset.Descriptor.ProjectId.ToString(),
                    AssetId = model.Asset.Descriptor.AssetId.ToString(),
                    AssetVersionId = model.Asset.Descriptor.AssetVersion.ToString(),
                    GameObjectName = newModelAssetObject.name,
                    AnchorId = model.AnchorId,
                    InstanceNumber = model.InstanceNumber
                });

                m_ModelSyncData.CheckDirtyState();
            }
            else
            {
                PongNewModelDataRpc(model.Asset.Descriptor.OrganizationId.ToString(),
                    model.Asset.Descriptor.ProjectId.ToString(),
                    model.Asset.Descriptor.AssetId.ToString(),
                    model.Asset.Descriptor.AssetVersion.ToString(),
                    newModelAssetObject.name,
                    model.AnchorId,
                    model.InstanceNumber);
            }
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongNewModelDataRpc(
            FixedString64Bytes orgId,
            FixedString64Bytes projectId,
            FixedString64Bytes assetId,
            FixedString64Bytes assetVersionId,
            FixedString64Bytes gameObjectName,
            FixedString64Bytes anchorId,
            int instanceNumber)
        {
            if(!IsOwner) return;

            m_ModelSyncData.Value.Add(new()
            {
                OrgId = orgId.ToString(),
                ProjectId = projectId.ToString(),
                AssetId = assetId.ToString(),
                AssetVersionId = assetVersionId.ToString(),
                GameObjectName = gameObjectName.ToString(),
                AnchorId = anchorId.ToString(),
                InstanceNumber = instanceNumber
            });

            m_ModelSyncData.CheckDirtyState();
        }

        /// <summary>
        /// Handle the event from TransformController when a model is removed.
        /// </summary>
        private void OnModelRemoved(StreamingModel streamingModel)
        {
            if (IsSessionOwner)
            {
                //Remove model from the list
                m_ModelSyncData.Value.RemoveAll(x => string.Equals(x.GameObjectName, streamingModel.gameObject.name));
                m_ModelSyncData.CheckDirtyState();
            }
            else
            {
                PongRemoveModelDataRpc(streamingModel.gameObject.name);
            }
        }


        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongRemoveModelDataRpc(FixedString64Bytes gameObjectName)
        {
            if(!IsOwner) return;
            m_ModelSyncData.Value.RemoveAll(x => string.Equals(x.GameObjectName, gameObjectName.ToString()));
            m_ModelSyncData.CheckDirtyState();
        }

        /// <summary>
        /// Removes streaming models that are not present in the ModelSyncData list but are present on scene.
        /// </summary>
        private void FoundRemovedModels()
        {
            var allStreamingModels = TransformController.Instance.GetComponentsInChildren<StreamingModel>(true);

            foreach (var streamingModel in allStreamingModels)
            {
                if (m_ModelSyncData.Value.Any(x => string.Equals(x.GameObjectName, streamingModel.gameObject.name))) continue;
                StreamingModelController.RemoveStreamModel?.Invoke(streamingModel);
            }
        }

        /// <summary>
        /// Add streaming models that are present in the ModelSyncData list but are absent on scene.
        /// </summary>
        /// <remarks>This method scans the scene for models tagged as streaming models and compares them
        /// against the existing model synchronization data. If any models are missing from the layout configuration,
        /// they are added to a new layout JSON object. The method ensures that duplicate entries are avoided and
        /// updates the layout configuration accordingly.</remarks>
        private void FindAndAddMissingModels()
        {
            var currentAllModels = new List<StreamingModel>();
            
            foreach (Transform child in TransformController.Instance.transform)
            {
                if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                if (!child.TryGetComponent(out StreamingModel model)) continue;
                currentAllModels.Add(model);
            }

            LayoutJson newLayoutJson = null;

            foreach (var modelData in m_ModelSyncData.Value)
            {
                if (m_StreamingModelController.LayoutJson != null &&
                    m_StreamingModelController.LayoutJson.LayoutModels != null &&
                    m_StreamingModelController.LayoutJson.LayoutModels.Any(x =>
                        string.Equals(x.gameObjectName, modelData.GameObjectName)))
                {
                    continue;
                }
                
                if (currentAllModels.Any(x => string.Equals(x.gameObject.name, modelData.GameObjectName)))
                {
                    continue;
                }

                newLayoutJson ??= new LayoutJson();
                newLayoutJson.LayoutModels ??= new List<LayoutModelEntity>();
                newLayoutJson.LayoutModels.Add(new()
                {
                    orgID = modelData.OrgId,
                    projectID = modelData.ProjectId,
                    assetID = modelData.AssetId,
                    versionID = modelData.AssetVersionId,
                    gameObjectName = modelData.GameObjectName,
                    anchorId = modelData.AnchorId,
                    instanceNumber = modelData.InstanceNumber
                });

                newLayoutJson.LayoutModels = newLayoutJson.LayoutModels.Distinct().ToList();
            }

            if (newLayoutJson == null || newLayoutJson.LayoutModels == null || newLayoutJson.LayoutModels.Count == 0) return;
            _ = m_StreamingModelController.ProcessLayoutJson(newLayoutJson, this);
        }

        /// <summary>
        /// Handle the change in model data, which is triggered when the network model synchronization data is updated.
        /// </summary>
        private void OnModelDataChanged(List<ModelSyncData> previousValue, List<ModelSyncData> newValue)
        {
            if (StreamingModelController.IsLayoutAsset)
            {
                //Make sure the layout variable is populated first
                StartCoroutine(WaitForLayout());
            }
            else
            {
                SyncSceneWithNetworkModels();
            }

            return;

            IEnumerator WaitForLayout()
            {
                while (m_StreamingModelController.LayoutJson == null || 
                       m_StreamingModelController.LayoutJson.LayoutModels == null)
                {
                    yield return null;
                }

                SyncSceneWithNetworkModels();
            }
        }

        private void OnSessionOwnerPromoted(ulong sessionownerpromoted)
        {
            if (NetworkManager.LocalClientId == sessionownerpromoted)
            {
                NetworkObject.ChangeOwnership(sessionownerpromoted);
            }
        }
    }
}
