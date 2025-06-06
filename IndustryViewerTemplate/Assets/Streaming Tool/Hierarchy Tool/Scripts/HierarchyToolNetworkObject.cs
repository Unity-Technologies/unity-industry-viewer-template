using System;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Cloud.Common;
using Unity.Collections;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming.Hierarchy
{
    [Serializable]
    public struct HierarchySyncData : INetworkSerializable, IEquatable<HierarchySyncData>
    {
        public string AssetId;
        public string ProjectID;
        public string OrgID;
        public string GameObjectName;
        public string InstanceId;
        public bool root;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AssetId);
            serializer.SerializeValue(ref ProjectID);
            serializer.SerializeValue(ref OrgID);
            serializer.SerializeValue(ref GameObjectName);
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref root);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(OrgID.GetHashCode(), ProjectID.GetHashCode(), AssetId.GetHashCode(), GameObjectName.GetHashCode());
        }

        public bool Equals(HierarchySyncData other)
        {
            return OrgID == other.OrgID && ProjectID == other.ProjectID && AssetId == other.AssetId 
                   && GameObjectName == other.GameObjectName;
        }
        
        public static bool operator ==(HierarchySyncData left, HierarchySyncData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HierarchySyncData left, HierarchySyncData right)
        {
            return !left.Equals(right);
        }
    }
    
    // This script manages the network synchronization of hierarchy data in a Unity project.
    // It uses Unity's Netcode for GameObjects to handle network variables and RPCs for data synchronization.
    // The script listens for network events such as session ownership changes and updates the visibility of instances accordingly.
    // It integrates with Unity's MonoBehaviour for lifecycle management and interacts with the HierarchyToolSceneListener for data updates.
    public class HierarchyToolNetworkObject : NetworkBehaviour
    {
        public List<FixedString64Bytes> LockList => m_LockList.Value;
        
        private NetworkVariable<List<FixedString64Bytes>> m_LockList = new NetworkVariable<List<FixedString64Bytes>>(new List<FixedString64Bytes>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        private NetworkVariable<List<HierarchySyncData>> m_HierarchySyncData = new NetworkVariable<List<HierarchySyncData>>(new List<HierarchySyncData>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        private HierarchyToolSceneListener m_HierarchyToolSceneListener;
        
        public override void OnNetworkSpawn()
        {
#if ENABLE_MULTIPLAY
            HierarchyToolController.LockModel += LockModel;
#endif
            m_HierarchySyncData.OnValueChanged += OnHierarchyDataChanged;
            NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
            HierarchyToolController.InstanceVisibilityChanged += OnInstanceVisibilityChanged;
            m_HierarchyToolSceneListener = FindAnyObjectByType<HierarchyToolSceneListener>();
            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                if (m_HierarchyToolSceneListener.VisibilityModifier.HiddenInstances != null)
                {
                    foreach (var hidingItem in m_HierarchyToolSceneListener.VisibilityModifier.HiddenInstances)
                    {
                        var data = new HierarchySyncData()
                        {
                            AssetId = hidingItem.StreamingModel.AssetId,
                            ProjectID = hidingItem.StreamingModel.ProjectID,
                            OrgID = hidingItem.StreamingModel.OrgID,
                            GameObjectName = hidingItem.StreamingModel.gameObject.name,
                        };

                        if (hidingItem.Instance != null)
                        {
                            data.InstanceId = hidingItem.Instance.Id.ToString();
                            data.root = hidingItem.Instance == null || hidingItem.Instance.AncestorIds.Count == 0;
                        }
                        else
                        {
                            data.InstanceId = string.Empty;
                            data.root = true;
                        }
                        
                        m_HierarchySyncData.Value.Add(data);
                    }
                }
                
                m_HierarchySyncData.CheckDirtyState();
            }
            else
            {
                m_HierarchyToolSceneListener?.VisibilityModifier?.Reset();
                for (var i = 0; i < TransformController.Instance.transform.childCount; i++)
                {
                    if (!TransformController.Instance.transform.GetChild(i)
                            .TryGetComponent(out StreamingModel model)) continue;
                    model.gameObject.SetActive(true);
                }
                foreach (var syncData in m_HierarchySyncData.Value)
                {
                    CheckHierarchyDataAgainstModel(syncData, false);
                }
            }
        }

        private void CheckHierarchyDataAgainstModel(HierarchySyncData data, bool visibility)
        {
            for (var i = 0; i < TransformController.Instance.transform.childCount; i++)
            {
                if (!TransformController.Instance.transform.GetChild(i)
                        .TryGetComponent(out StreamingModel model)) continue;
                if(data.ProjectID == model.ProjectID && data.OrgID == model.OrgID && data.AssetId == model.AssetId && data.GameObjectName == model.gameObject.name)
                {
                    m_HierarchyToolSceneListener.UpdateVisibility(model, data.root,
                        data.root ? InstanceId.None : new InstanceId(data.InstanceId), visibility);
                }
            }
        }
        
        public override void OnNetworkDespawn()
        {
#if ENABLE_MULTIPLAY
            HierarchyToolController.LockModel -= LockModel;
#endif
            HierarchyToolController.InstanceVisibilityChanged -= OnInstanceVisibilityChanged;
            m_HierarchySyncData.OnValueChanged -= OnHierarchyDataChanged;
            NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
        }
        
#if ENABLE_MULTIPLAY
        private void LockModel(string modelName, bool toLock)
        {
            PongLockListUpdateRpc(new FixedString64Bytes(modelName), toLock);
        }
#endif
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongLockListUpdateRpc(FixedString64Bytes modelName, bool toLock)
        {
            if(!IsOwner) return;
            if (toLock)
            {
                if(m_LockList.Value.Contains(modelName)) return;
                m_LockList.Value.Add(modelName);
                m_LockList.CheckDirtyState();
            }
            else
            {
                if(!m_LockList.Value.Contains(modelName)) return;
                m_LockList.Value.Remove(modelName);
                m_LockList.CheckDirtyState();
            }
        }

        private void OnInstanceVisibilityChanged(InstanceData arg1, bool arg2)
        {
            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                var data = new HierarchySyncData
                {
                    AssetId = arg1.StreamingModel.AssetId,
                    ProjectID = arg1.StreamingModel.ProjectID,
                    OrgID = arg1.StreamingModel.OrgID,
                    GameObjectName = arg1.StreamingModel.gameObject.name,
                    InstanceId = arg1.Instance == null ? string.Empty : arg1.Instance.Id.ToString(),
                    root = arg1.Instance == null || arg1.Instance.AncestorIds.Count == 0
                };

                if (!arg2)
                {
                    if (!m_HierarchySyncData.Value.Contains(data))
                    {
                        m_HierarchySyncData.Value.Add(data);
                    }
                }
                else
                {
                    if(m_HierarchySyncData.Value.Contains(data))
                    {
                        m_HierarchySyncData.Value.Remove(data);
                    }
                }
                m_HierarchySyncData.CheckDirtyState();
            }
            else
            {
                var instanceId = arg1.Instance == null ? string.Empty : arg1.Instance.Id.ToString();
                
                PongDataRpc(arg1.StreamingModel.AssetId,
                    arg1.StreamingModel.ProjectID,
                    arg1.StreamingModel.OrgID,
                    arg1.StreamingModel.gameObject.name,
                    instanceId,
                    arg1.Instance == null || arg1.Instance.AncestorIds.Count == 0,
                    arg2);
            }
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongDataRpc(FixedString64Bytes assetId, FixedString64Bytes projectId,
            FixedString64Bytes orgId, FixedString64Bytes gameObjectName, FixedString64Bytes instanceId, bool root, bool visibility)
        {
            if(!IsOwner) return;
            var data = new HierarchySyncData()
            {
                AssetId = assetId.ToString(),
                ProjectID = projectId.ToString(),
                OrgID = orgId.ToString(),
                GameObjectName = gameObjectName.ToString(),
                InstanceId = instanceId.ToString(),
                root = root
            };
            
            if (!visibility)
            {
                if (!m_HierarchySyncData.Value.Contains(data))
                {
                    m_HierarchySyncData.Value.Add(data);
                }
            }
            else
            {
                if(m_HierarchySyncData.Value.Contains(data))
                {
                    m_HierarchySyncData.Value.Remove(data);
                }
            }
            m_HierarchySyncData.CheckDirtyState();
        }

        private void OnHierarchyDataChanged(List<HierarchySyncData> previousvalue, List<HierarchySyncData> newvalue)
        {
            FindRemovedItem(previousvalue, newvalue);
            FindNewItem(previousvalue, newvalue);
        }

        private void FindRemovedItem(List<HierarchySyncData> previousvalue, List<HierarchySyncData> newvalue)
        {
            Queue<HierarchySyncData> removedItems = new Queue<HierarchySyncData>();
            foreach (var hierarchySyncData in previousvalue)
            {
                if(newvalue.Contains(hierarchySyncData)) continue;
                removedItems.Enqueue(hierarchySyncData);
            }
            while (removedItems.Count > 0)
            {
                var data = removedItems.Dequeue();
                CheckHierarchyDataAgainstModel(data, true);
            }
        }

        private void FindNewItem(List<HierarchySyncData> previousvalue, List<HierarchySyncData> newvalue)
        {
            Queue<HierarchySyncData> newItems = new Queue<HierarchySyncData>();
            foreach (var hierarchySyncData in newvalue)
            {
                if(previousvalue.Contains(hierarchySyncData)) continue;
                newItems.Enqueue(hierarchySyncData);
            }

            while (newItems.Count > 0)
            {
                var data = newItems.Dequeue();
                CheckHierarchyDataAgainstModel(data, false);
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
