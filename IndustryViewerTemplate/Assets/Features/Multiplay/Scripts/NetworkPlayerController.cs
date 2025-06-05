using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.UI;
using Unity.Cloud.Identity;
using TMPro;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Multiplay
{
    // This script manages the networked player in a multiplayer session using Unity Netcode.
    // It handles player initialization, color and name updates, and presentation mode interactions.
    // The script synchronizes player position, rotation, and state across the network.
    // It includes event handlers for network variable changes and UI updates.
    // The script integrates with the IdentityController for user information and the MultiplayController for session management.
    public class NetworkPlayerController : NetworkBehaviour
    {
        // Event handlers for player color and name changes
        public static Action<ulong, Color> OnColorChanged;
        public static Action<ulong, string> OnNameChanged;
        
        // Network variables for player position, rotation, color, name, and presentation mode
        private NetworkVariable<Vector3> m_Position = new NetworkVariable<Vector3>();
        private NetworkVariable<Quaternion> m_Rotation = new NetworkVariable<Quaternion>();
        public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>();
        public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<bool> IsPresenter = new NetworkVariable<bool>();
        public NetworkVariable<bool> InPresentation = new NetworkVariable<bool>();
        bool m_StartingPresenter;
        
        private float m_ScaleFactor = 1f;
        
        // UI elements for player color, name, and canvas
        [SerializeField]
        private MeshRenderer[] playerRenderer;

        [SerializeField] private GameObject nameCanvas;
        [SerializeField]
        private Image playerColorImage;
        [SerializeField] private Material playerColorMat;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI playerInitialText;

        [HideInInspector]
        public bool firstInitialization;

        private GameObject m_presenter;
        
        [SerializeField]
        private GameObject[] m_GameObjectToHide;
        
        private float m_HideDistance = 2f;

        // Called when this networked player is spawned
        public override void OnNetworkSpawn()
        {
            Debug.Log($"Client-{OwnerClientId} spawned!");
            PlayerColor.OnValueChanged += OnColorValueChanged;
            PlayerName.OnValueChanged += OnNameValueChanged;
            IsPresenter.OnValueChanged += OnPresenterValueChanged;
            InPresentation.OnValueChanged += OnInPresentationValueChanged;
            
            MultiplayController.OnClientConnected?.Invoke(OwnerClientId, gameObject);
            
            if (!IsOwner)
            {
                transform.position = m_Position.Value;
                transform.rotation = m_Rotation.Value;
                
                if (PlayerColor.Value != default)
                {
                    UpdatePlayerMeshColor(PlayerColor.Value);
                }
                return;
            }
            
            AvatarMeshControl(false);

            if (IdentityController.UserInfo == null) return;
            PlayerName.Value = IdentityController.UserInfo.Name;
            PlayerName.CheckDirtyState();
        }

        public void AvatarMeshControl(bool active)
        {
            var allRenderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in allRenderers)
            {
                renderer.enabled = active;
            }
        }
        
        //Called when the name of the player is changed
        private void OnNameValueChanged(FixedString64Bytes previousvalue, FixedString64Bytes newvalue)
        {
            UpdatePlayerNameLabel(newvalue.ToString());
            OnNameChanged?.Invoke(OwnerClientId, newvalue.ToString());
        }
        
        // Called when the player color is changed
        private void OnColorValueChanged(Color previousvalue, Color newvalue)
        {
            UpdatePlayerMeshColor(newvalue);
            
            OnColorChanged?.Invoke(OwnerClientId, newvalue);
        }

        // Update the player mesh color
        public void UpdatePlayerMeshColor(Color newColor)
        {
            foreach (var meshRenderer in playerRenderer)
            {
                meshRenderer.material.color = newColor;
            }

            Material newMat = new Material(playerColorMat)
            {
                color = newColor
            };
            playerColorImage.color = newMat.color;
        }

        public void UpdatePlayerNameLabel(string userName)
        {
            var firstName = userName.Split(" ")[0];
            playerNameText.text = firstName;
            playerInitialText.text = firstName.Substring(0, 1);
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorValueChanged;
            PlayerName.OnValueChanged -= OnNameValueChanged;
            IsPresenter.OnValueChanged -= OnPresenterValueChanged;
            InPresentation.OnValueChanged -= OnInPresentationValueChanged;
        }

        private void OnInPresentationValueChanged(bool previousvalue, bool newvalue)
        {
            if (!IsOwner)
            {
                AvatarMeshControl(!newvalue);
                return;
            }
            NavigationController.PauseCameraControl?.Invoke(newvalue);
        }

        private void OnPresenterValueChanged(bool previousvalue, bool newvalue)
        {
            if (m_StartingPresenter)
            {
                m_StartingPresenter = false;
                return;
            }
            //If there is a presenter
            if (newvalue)
            {
                MultiplayController.RequestToJoinPresentation.Invoke();
            }
            else
            {
                if (IsOwner)
                {
                    InPresentation.Value = false;
                    InPresentation.CheckDirtyState();
                }
                MultiplayController.EndPresentation.Invoke(OwnerClientId);
            }
        }

        public void UpdatePlayerColor(Color color)
        {
            if (!IsSessionOwner) return;
            PongColorRpc(color);
        }
        
        [Rpc(SendTo.Authority, Delivery = RpcDelivery.Reliable)]
        private void PongColorRpc(Color color)
        {
            if(!IsOwner) return;
            PlayerColor.Value = color;
            PlayerColor.CheckDirtyState();
        }

        public void InitializePresentationMode()
        {
            m_StartingPresenter = true;
            IsPresenter.Value = true;
            IsPresenter.CheckDirtyState();
        }

        public void JoinPresentation(GameObject presenter)
        {
            m_presenter = presenter;
            InPresentation.Value = true;
            InPresentation.CheckDirtyState();
        }

        public void EndPresentation()
        {
            IsPresenter.Value = false;
            IsPresenter.CheckDirtyState();
            InPresentation.Value = false;
            InPresentation.CheckDirtyState();
        }
        
        public void LeavePresentation()
        {
            if(!IsOwner) return;
            IsPresenter.Value = false;
            IsPresenter.CheckDirtyState();
            InPresentation.Value = false;
            InPresentation.CheckDirtyState();
        }

        public void Reparent(Transform newParent)
        {
            if(!IsOwner) return;
            transform.SetParent(newParent, false);
        }

        private void Update()
        {
            if (nameCanvas == null) return;
            
            if (!IsOwner)
            {
                nameCanvas.SetActive(true);
                Camera mainCam = Camera.main;
                if (mainCam == null) return;
                var distance = Vector3.Distance(mainCam.transform.position, transform.position);
                foreach(var go in m_GameObjectToHide)
                {
                    go.SetActive(distance > m_HideDistance);
                }
                nameCanvas.transform.localScale = Vector3.one *
                                                  (Mathf.Sqrt(distance) * m_ScaleFactor);
                Vector3 directionToCamera = mainCam.transform.position - nameCanvas.transform.position;
                nameCanvas.transform.rotation = Quaternion.LookRotation(directionToCamera);
                // Ensure the text doesn't flip horizontally
                nameCanvas.transform.rotation = Quaternion.Euler(0, nameCanvas.transform.rotation.eulerAngles.y, 0);
                return;
            }
            nameCanvas.SetActive(false);
        }

        private void LateUpdate()
        {
            if(!IsOwner) return;

            if (Camera.main != null)
            {
                transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
                m_Position.Value = transform.position;
                m_Position.CheckDirtyState();
                m_Rotation.Value = transform.rotation;
                m_Rotation.CheckDirtyState();
            }
            
            if(!InPresentation.Value || m_presenter == null) return;
            NavigationController.FollowPresenter?.Invoke(m_presenter);
        }
    }
}
