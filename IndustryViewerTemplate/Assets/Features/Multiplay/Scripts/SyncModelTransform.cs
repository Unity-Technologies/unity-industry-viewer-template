using System;
using Unity.Collections;
using Unity.Industry.Viewer.Assets;
using Unity.Netcode;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Multiplay
{
    // This script synchronizes the transform (position and rotation) of a model across a network in a Unity project.
    // It uses Unity's Netcode for GameObjects to manage network variables and RPCs for transform updates.
    // The script handles the initialization and synchronization of model data, ensuring consistency across clients.
    // It integrates with Unity's MonoBehaviour for lifecycle management and listens for network events such as session ownership changes.
    public class SyncModelTransform : NetworkBehaviour
    {
        public static Action<bool> RuntimeTransformHandleCreated;
        
        private Transform TargetTransform
        {
            get {
                if (m_Transform != null) return m_Transform;
                foreach (Transform child in TransformController.Instance.transform)
                {
                    if (!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                    if (!string.Equals(child.name, m_ModelName.Value.ToString())) continue;
                    m_Transform = child;
                    break;
                }

                if (m_Transform == null) return m_Transform;
                m_Transform.localPosition = m_Position.Value;
                m_Transform.localRotation = m_Rotation.Value;

                return m_Transform;
            }
        }
        
        private Transform m_Transform;
        
        public NetworkVariable<FixedString64Bytes> m_ModelName = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<Vector3> m_Position = new NetworkVariable<Vector3>();
        public NetworkVariable<Quaternion> m_Rotation = new NetworkVariable<Quaternion>();
        
        public override void OnNetworkSpawn()
        {
            m_ModelName.OnValueChanged += OnNameValueChanged;
            m_Position.OnValueChanged += OnPositionValueChanged;
            m_Rotation.OnValueChanged += OnRotationValueChanged;
            NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
            RuntimeTransformHandleCreated += OnRunTimeHandleCreated;
            GetTransform();
        }

        public override void OnNetworkDespawn()
        {
            m_ModelName.OnValueChanged -= OnNameValueChanged;
            m_Position.OnValueChanged -= OnPositionValueChanged;
            m_Rotation.OnValueChanged -= OnRotationValueChanged;
            NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
            RuntimeTransformHandleCreated -= OnRunTimeHandleCreated;
        }

        private void Update()
        {
            if (m_Transform != null)
            {
                return;
            }
            GetTransform();
        }

        public void NotifySessionChanged(AssetInfo assetInfo)
        {
            PongNotifySessionChangedRpc(new FixedString64Bytes(assetInfo.Asset.Descriptor.OrganizationId.ToString()),
                new FixedString64Bytes(assetInfo.Asset.Descriptor.ProjectId.ToString()),
                new FixedString64Bytes(assetInfo.Asset.Descriptor.AssetId.ToString()), 
                new FixedString64Bytes(assetInfo.Asset.Descriptor.AssetVersion.ToString()));
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Reliable)]
        private void PongNotifySessionChangedRpc(FixedString64Bytes orgId, FixedString64Bytes projectId, FixedString64Bytes assetId, FixedString64Bytes version)
        {
            MultiplayController.SearchNewLayout?.Invoke(orgId.ToString(), projectId.ToString(), assetId.ToString(), version.ToString());
        }

        private void GetTransform()
        {
            if (string.IsNullOrEmpty(m_ModelName.Value.ToString()))
            {
                return;
            }

            if (TransformController.Instance == null)
            {
                return;
            }
            
            foreach (Transform child in TransformController.Instance.transform)
            {
                if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                if (!string.Equals(child.name, m_ModelName.Value.ToString()))
                {
                    continue;
                }
                m_Transform = child;
                if(m_Transform.localPosition != m_Position.Value)
                {
                    m_Transform.localPosition = m_Position.Value;
                }
            
                if(m_Transform.localRotation != m_Rotation.Value)
                {
                    m_Transform.localRotation = m_Rotation.Value;
                }
                return;
            }
        }

        private void OnRunTimeHandleCreated(bool created)
        {
            if(created) return;
            if(TargetTransform == null) return;
            if (IsSessionOwner)
            {
                if (m_Position.Value != m_Transform.localPosition)
                {
                    m_Position.Value = m_Transform.localPosition;
                    m_Position.CheckDirtyState();
                }
                
                if(m_Rotation.Value != m_Transform.localRotation)
                {
                    m_Rotation.Value = m_Transform.localRotation;
                    m_Rotation.CheckDirtyState();
                }
            }
            else
            {
                transform.position = m_Transform.position;
                if (m_Position.Value != m_Transform.localPosition)
                {
                    PongTransformUpdatePositionRpc(m_Transform.localPosition);
                }
                
                if(m_Rotation.Value != m_Transform.localRotation)
                {
                    PongTransformUpdateRotationRpc(m_Transform.localRotation);
                }
            }
        }

        private void OnRotationValueChanged(Quaternion previousValue, Quaternion newValue)
        {
            if (TargetTransform == null) return;
            if (m_Transform.localRotation == newValue) return;
            m_Transform.localRotation = newValue;
        }

        private void OnPositionValueChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (TargetTransform == null)
            {
                return;
            }

            if (m_Transform.localPosition == newValue)
            {
                return;
            }
            m_Transform.localPosition = newValue;
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Unreliable)]
        private void PongTransformUpdatePositionRpc(Vector3 position)
        {
            m_Position.Value = position;
            m_Position.CheckDirtyState();
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Unreliable)]
        private void PongTransformUpdateRotationRpc(Quaternion rotation)
        {
            m_Rotation.Value = rotation;
            m_Rotation.CheckDirtyState();
        }

        private void OnNameValueChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            if(m_Transform != null) return;
            foreach (Transform child in TransformController.Instance.transform)
            {
                if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                if (!string.Equals(child.name, newValue.ToString())) continue;
                m_Transform = child;
                if (m_Transform.localPosition != m_Position.Value)
                {
                    m_Transform.localPosition = m_Position.Value;
                }

                if (m_Transform.localRotation != m_Rotation.Value)
                {
                    m_Transform.localRotation = m_Rotation.Value;
                }

                return;
            }
        }
        
        public void SetValue(GameObject newModel)
        {
            m_Transform = newModel.transform;
            m_ModelName.Value = newModel.name;
            m_ModelName.CheckDirtyState();

            m_Position.Value = m_Transform.localPosition;
            m_Position.CheckDirtyState();
            
            m_Rotation.Value = m_Transform.localRotation;
            m_Rotation.CheckDirtyState();

            transform.position = m_Transform.position;
        }

        private void OnSessionOwnerPromoted(ulong sessionOwnerPromoted)
        {
            if (NetworkManager.LocalClientId == sessionOwnerPromoted)
            {
                NetworkObject.ChangeOwnership(sessionOwnerPromoted);
            }
        }
    }
}
