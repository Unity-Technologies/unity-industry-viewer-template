using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Collections;
using UnityEngine.SceneManagement;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Multiplay
{
    [Serializable]
    public struct ModelSyncData : INetworkSerializable//, IEquatable<ModelSyncData>
    {
        public string OrgId;
        public string ProjectId;
        public string AssetId;
        public string AssetVersionId;
        public string GameObjectName;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrgId);
            serializer.SerializeValue(ref ProjectId);
            serializer.SerializeValue(ref AssetId);
            serializer.SerializeValue(ref GameObjectName);
            serializer.SerializeValue(ref AssetVersionId);
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
            m_StreamingModelController = FindFirstObjectByType<StreamingModelController>();
            m_ModelSyncData.OnValueChanged += OnModelDataChanged;
            NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
            StartCoroutine(Initializing());
        }

        private IEnumerator Initializing()
        {
            while(TransformController.Instance == null ||
                  TransformController.Instance.GetComponent<NetworkTransformController>() == null
                  || TransformController.Instance.transform.childCount == 0)
            {
                yield return null;
            }
            
            yield return null;

            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                foreach (Transform child in TransformController.Instance.transform)
                {
                    if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                    if (!child.TryGetComponent(out StreamingModel model)) continue;
                    m_ModelSyncData.Value.Add(
                        new ModelSyncData
                        {
                            OrgId = model.Asset.Descriptor.OrganizationId.ToString(),
                            ProjectId = model.Asset.Descriptor.ProjectId.ToString(),
                            AssetId = model.Asset.Descriptor.AssetId.ToString(),
                            AssetVersionId = model.Asset.Descriptor.AssetVersion.ToString(),
                            GameObjectName = child.gameObject.name
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
                    FindAndAddMissingModels();
                }
            }

            IEnumerator WaitForLayoutAsset()
            {
                while (m_StreamingModelController.LayoutJson == null || 
                       m_StreamingModelController.LayoutJson.LayoutModels == null ||
                       m_StreamingModelController.LayoutJson.LayoutModels.Count == 0)
                {
                    yield return null;
                }
                FindAndAddMissingModels();
            }
        }

        private void Start()
        {
            TransformController.ModelAdded += OnModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            TransformController.ModelAdded -= OnModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
        }
        
        public override void OnNetworkDespawn()
        {
            NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
            m_ModelSyncData.OnValueChanged -= OnModelDataChanged;
        }

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
            if(m_ModelSyncData.Value.Any(x => string.Equals(x.GameObjectName, newModelAssetObject.name))) return; 
            
            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                m_ModelSyncData.Value.Add(new ModelSyncData()
                {
                    OrgId = model.Asset.Descriptor.OrganizationId.ToString(),
                    ProjectId = model.Asset.Descriptor.ProjectId.ToString(),
                    AssetId = model.Asset.Descriptor.AssetId.ToString(),
                    AssetVersionId = model.Asset.Descriptor.AssetVersion.ToString(),
                    GameObjectName = newModelAssetObject.name
                });
                m_ModelSyncData.CheckDirtyState();
            }
            else
            {
                PongNewModelDataRpc(model.Asset.Descriptor.OrganizationId.ToString(),
                    model.Asset.Descriptor.ProjectId.ToString(),
                    model.Asset.Descriptor.AssetId.ToString(), model.Asset.Descriptor.AssetVersion.ToString(),
                    newModelAssetObject.name);
            }
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongNewModelDataRpc(FixedString64Bytes orgId, FixedString64Bytes projectId,
            FixedString64Bytes assetId, FixedString64Bytes assetVersionId, FixedString64Bytes gameObjectName)
        {
            if(!IsOwner) return;
            m_ModelSyncData.Value.Add(new ModelSyncData()
            {
                OrgId = orgId.ToString(),
                ProjectId = projectId.ToString(),
                AssetId = assetId.ToString(),
                AssetVersionId = assetVersionId.ToString(),
                GameObjectName = gameObjectName.ToString()
            });
            m_ModelSyncData.CheckDirtyState();
        }

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

        private void FoundRemovedModels()
        {
            foreach (Transform child in TransformController.Instance.transform)
            {
                if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                if (!child.TryGetComponent(out StreamingModel model)) continue;
                if (m_ModelSyncData.Value.Any(x => string.Equals(x.GameObjectName, model.gameObject.name))) continue;
                StreamingModelController.RemoveStreamModel?.Invoke(model);
            }
        }
        
        // Find if there are any missing models and add them
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

                if (string.Equals(modelData.GameObjectName,
                        StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId + "@1"))
                {
                    continue;
                }

                if (currentAllModels.Any(x => string.Equals(x.gameObject.name, modelData.GameObjectName)))
                {
                    continue;
                }
                newLayoutJson ??= new LayoutJson();
                newLayoutJson.LayoutModels ??= new List<LayoutModelEntity>();
                newLayoutJson.LayoutModels.Add(new LayoutModelEntity()
                {
                    orgID = modelData.OrgId,
                    projectID = modelData.ProjectId,
                    assetID = modelData.AssetId,
                    versionID = modelData.AssetVersionId,
                    gameObjectName = modelData.GameObjectName
                });

                newLayoutJson.LayoutModels = newLayoutJson.LayoutModels.Distinct().ToList();
            }
            
            if(newLayoutJson == null || newLayoutJson.LayoutModels == null || newLayoutJson.LayoutModels.Count == 0) return;
            _ = m_StreamingModelController.ProcessLayoutJson(newLayoutJson);
        }

        private void OnModelDataChanged(List<ModelSyncData> previousValue, List<ModelSyncData> newValue)
        {
            if (StreamingModelController.IsLayoutAsset)
            {
                //Make sure the layout variable is populated first
                StartCoroutine(WaitForLayout());
            }
            else
            {
                FoundRemovedModels();
                FindAndAddMissingModels();
            }
            return;

            IEnumerator WaitForLayout()
            {
                while (m_StreamingModelController.LayoutJson == null || 
                       m_StreamingModelController.LayoutJson.LayoutModels == null ||
                       m_StreamingModelController.LayoutJson.LayoutModels.Count == 0)
                {
                    yield return null;
                }
                FoundRemovedModels();
                FindAndAddMissingModels();
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
