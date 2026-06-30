using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Streaming;
using Unity.Netcode.Components;
using Unity.XR.CoreUtils;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace Unity.Industry.Viewer.Multiplay
{
    [Serializable]
    public struct AvatarColorData
    {
        public MeshRenderer meshRenderer;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public int index;
    }
    
    // This script manages the networked player in a multiplayer session using Unity Netcode.
    // It handles player initialization, color and name updates, and presentation mode interactions.
    // The script synchronizes player position, rotation, and state across the network.
    // It includes event handlers for network variable changes and UI updates.
    // The script integrates with the IdentityController for user information and the MultiplayController for session management.
    public class NetworkPlayerController : NetworkBehaviour
    {
        private const string k_ContainerName = "Container";
        private const string k_ColorName = "Color";
        private const string k_InitialName = "InitialLabel";
        private const string k_NameLabelName = "NameLabel";
        
        // Event handlers for player color and name changes
        public static Action<ulong, Color> OnColorChanged;
        public static Action<ulong, string> OnNameChanged;
        
        // Network variables for player position, rotation, color, name, and presentation mode
        public NetworkVariable<float> EyeLevelInVR;
        public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>();
        public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<bool> IsPresenter = new NetworkVariable<bool>();
        public NetworkVariable<bool> InPresentation = new NetworkVariable<bool>();
        public NetworkVariable<bool> IsInVR = new NetworkVariable<bool>();
        public NetworkVariable<ulong> LeftHandTrackerId = new NetworkVariable<ulong>();
        public NetworkVariable<ulong> RightHandTrackerId = new NetworkVariable<ulong>();
        
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        
        // UI elements for player color, name, and canvas
        [SerializeField]
        private AvatarColorData[] playerRenderer;

        [SerializeField]
        private UIDocument m_UiDocument;
        private VisualElement m_ContainerCanvas;
        private VisualElement m_ColorCanvas;
        private Text m_InitialLabel;
        private Text m_NameLabel;
        
        [SerializeField] private Material playerColorMat;

        [HideInInspector]
        public bool firstInitialization;

        private GameObject m_presenter;

        [SerializeField] private GameObject m_VRGlasses;
        
        private float m_HideDistance = 2f;

        [Header("VR")]
        [SerializeField] private GameObject m_rightHand;
        [SerializeField] private GameObject m_leftHand;

        [SerializeField] private GameObject m_trackerPrefab;
        
        private NetworkTransform m_rightHandNetworkTransform;
        private NetworkTransform m_leftHandNetworkTransform;

        private GameObject m_LeftHandTracker;
        private GameObject m_RightHandTracker;
        private XROrigin m_XROrigin;
        
        [SerializeField]
        float m_offset = 0.5f;
        
        #if UNITY_EDITOR
        private bool isUsingSimulator;
        #endif

        private void Start()
        {
            m_UiDocument.transform.parent.gameObject.SetActive(!IsOwner);
        }

        private void OnEnable()
        {
            //Initialize UI
            m_ContainerCanvas = m_UiDocument.rootVisualElement.Q<VisualElement>(k_ContainerName);
            m_ColorCanvas = m_UiDocument.rootVisualElement.Q<VisualElement>(k_ColorName);
            m_InitialLabel = m_UiDocument.rootVisualElement.Q<Text>(k_InitialName);
            m_NameLabel = m_UiDocument.rootVisualElement.Q<Text>(k_NameLabelName);
            
            if (!string.IsNullOrEmpty(PlayerName.Value.Value))
            {
                UpdatePlayerNameLabel(PlayerName.Value.Value);
            }
            if(PlayerColor.Value != default)
            {
                UpdatePlayerMeshColor(PlayerColor.Value);
            }
        }

        // Called when this networked player is spawned
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                m_UiDocument.rootVisualElement.style.display = DisplayStyle.None;
            }
            
            Debug.Log($"Client-{OwnerClientId}-{NetworkObjectId} spawned!");
            PlayerColor.OnValueChanged += OnColorValueChanged;
            PlayerName.OnValueChanged += OnNameValueChanged;
            IsPresenter.OnValueChanged += OnPresenterValueChanged;
            InPresentation.OnValueChanged += OnInPresentationValueChanged;
            IsInVR.OnValueChanged += OnVRValueChanged;
            MultiplayController.OnClientConnected?.Invoke(OwnerClientId, gameObject);
            
            if (!IsOwner)
            {
                m_VRGlasses.SetActive(!InPresentation.Value && IsInVR.Value);
                m_rightHand.SetActive(IsInVR.Value);
                m_leftHand.SetActive(IsInVR.Value);
                if (PlayerColor.Value != default)
                {
                    UpdatePlayerMeshColor(PlayerColor.Value);
                }
                return;
            }
            
            //Make sure those game meshes are not visible to the owner
            m_VRGlasses.SetActive(false);
            m_rightHand.SetActive(false);
            m_leftHand.SetActive(false);
            
#if UNITY_EDITOR
            var simulator = GameObject.Find("XR Interaction Simulator");
            IsInVR.Value = simulator != null && simulator.activeInHierarchy;
            isUsingSimulator = true;
#else
            IsInVR.Value = XRSettings.isDeviceActive;
#endif
            
            IsInVR.CheckDirtyState();

            if (IsInVR.Value)
            {
                EnsureVRHandTrackers();
            }
            
            AvatarMeshControl(false);

            if (IdentityController.UserInfo == null) return;
            PlayerName.Value = IdentityController.UserInfo.Name;
            PlayerName.CheckDirtyState();
        }

        // Lazily creates the left/right hand tracker NetworkObjects on the owner.
        // Idempotent and safe to call whenever VR is active — including when VR is
        // activated after the avatar has already spawned — so the owner LateUpdate
        // never dereferences a null tracker NetworkTransform.
        private void EnsureVRHandTrackers()
        {
            if (!IsOwner) return;
            m_XROrigin ??= FindFirstObjectByType<XROrigin>(FindObjectsInactive.Exclude);

            if (m_leftHandNetworkTransform == null)
            {
                var leftHandTracker = Instantiate(m_trackerPrefab, transform);
                leftHandTracker.name = "Left Hand Tracker";
                m_leftHandNetworkTransform = leftHandTracker.GetComponent<NetworkTransform>();
                var leftNetworkObject = leftHandTracker.GetComponent<NetworkObject>();
                leftNetworkObject.Spawn(true);
                leftNetworkObject.TrySetParent(NetworkObject, false);
                LeftHandTrackerId.Value = leftNetworkObject.NetworkObjectId;
                LeftHandTrackerId.CheckDirtyState();
            }

            if (m_rightHandNetworkTransform == null)
            {
                var rightHandTracker = Instantiate(m_trackerPrefab, transform);
                rightHandTracker.name = "Right Hand Tracker";
                m_rightHandNetworkTransform = rightHandTracker.GetComponent<NetworkTransform>();
                var rightNetworkObject = rightHandTracker.GetComponent<NetworkObject>();
                rightNetworkObject.Spawn(true);
                rightNetworkObject.TrySetParent(NetworkObject, false);
                RightHandTrackerId.Value = rightNetworkObject.NetworkObjectId;
                RightHandTrackerId.CheckDirtyState();
            }
        }

        public void AvatarMeshControl(bool active)
        {
            var allRenderers = GetComponentsInChildren<Renderer>();
            foreach (var localRenderer in allRenderers)
            {
                if (IsOwner)
                {
                    localRenderer.enabled = active;
                }
                else
                {
                    if(IsInVR.Value) return;
                    if(localRenderer.gameObject == m_VRGlasses) continue; 
                    localRenderer.enabled = active;
                }
            }
            var allSkinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var localRenderer in allSkinnedRenderers)
            {
                if (IsOwner)
                {
                    localRenderer.enabled = active;
                }
                else
                {
                    if(IsInVR.Value) return;
                    if(localRenderer.gameObject == m_VRGlasses) continue; 
                    localRenderer.enabled = active;
                }
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
            foreach (var data in playerRenderer)
            {
                if (data.meshRenderer != null)
                {
                    var material = data.meshRenderer.materials[data.index];
                    material.SetColor(BaseColorId, newColor);
                    data.meshRenderer.materials[data.index] = material;
                }
                if (data.skinnedMeshRenderer != null)
                {
                    var material = data.skinnedMeshRenderer.materials[data.index];
                    material.SetColor(BaseColorId, newColor);
                    data.skinnedMeshRenderer.materials[data.index] = material;
                }
            }
            if(IsOwner) return;
            if (m_ColorCanvas != null)
            {
                m_ColorCanvas.style.backgroundColor = newColor;
            }
        }

        public void UpdatePlayerNameLabel(string userName)
        {
            if(IsOwner) return;
            var firstName = userName.Split(" ")[0];
            var lastName = userName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
            if (m_InitialLabel != null)
            {
                m_InitialLabel.text = firstName.Substring(0, 1) + lastName.Substring(0,1);
            }
            if (m_NameLabel != null)
            {
                m_NameLabel.text = firstName;
            }
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorValueChanged;
            PlayerName.OnValueChanged -= OnNameValueChanged;
            IsPresenter.OnValueChanged -= OnPresenterValueChanged;
            InPresentation.OnValueChanged -= OnInPresentationValueChanged;
            IsInVR.OnValueChanged -= OnVRValueChanged;
            if (IsPresenter.Value)
            {
                MultiplayController.EndPresentation?.Invoke(OwnerClientId);
            }
        }

        private void OnVRValueChanged(bool previousValue, bool newValue)
        {
            if(IsOwner) return;
            if (InPresentation.Value)
            {
                m_VRGlasses.SetActive(false);
                m_rightHand.SetActive(false);
                m_leftHand.SetActive(false);
            }
            else
            {
                m_VRGlasses.SetActive(newValue);
                m_rightHand.SetActive(newValue);
                m_leftHand.SetActive(newValue);
            }
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
            if(IsOwner) return;
            
            //If there is a presenter
            if (newvalue)
            {
                MultiplayController.RequestToJoinPresentation?.Invoke();
            }
            else
            {
                if (IsOwner)
                {
                    InPresentation.Value = false;
                    InPresentation.CheckDirtyState();
                }
                MultiplayController.EndPresentation?.Invoke(OwnerClientId);
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
        
        [Rpc(SendTo.Authority)]
        public void SetPresenterRpc()
        {
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
            m_presenter = null;
            IsPresenter.Value = false;
            IsPresenter.CheckDirtyState();
            InPresentation.Value = false;
            InPresentation.CheckDirtyState();
        }
        
        public void LeavePresentation()
        {
            m_presenter = null;
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
            if (IsOwner && IsInVR.Value)
            {
                EyeLevelInVR ??= new NetworkVariable<float>();
#if UNITY_EDITOR
                if (isUsingSimulator && m_XROrigin != null)
                {
                    EyeLevelInVR.Value = m_XROrigin.CameraYOffset;
                    EyeLevelInVR.CheckDirtyState();
                }
#else
                InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition))
                {
                    EyeLevelInVR.Value = headPosition.y;
                    EyeLevelInVR.CheckDirtyState();
                }
                else
                {
                    EyeLevelInVR.Value = -1f;
                    EyeLevelInVR.CheckDirtyState();
                }
#endif
            }
            
            if (m_UiDocument == null || !m_UiDocument.gameObject.activeInHierarchy || m_ContainerCanvas == null) return;
            
            if (!IsOwner)
            {
                if(m_ContainerCanvas.style.display == DisplayStyle.None)
                {
                    m_ContainerCanvas.style.display = DisplayStyle.Flex;
                }
                Camera mainCam = Camera.main;
                if (mainCam == null) return;
                var distance = Vector3.Distance(mainCam.transform.position, transform.position);
                m_NameLabel.style.display = distance > m_HideDistance? DisplayStyle.Flex: DisplayStyle.None;
                return;
            }
            if(m_ContainerCanvas.style.display == DisplayStyle.Flex)
            {
                m_ContainerCanvas.style.display = DisplayStyle.None;
            }
        }

        private void LateUpdate()
        {
            if (transform.parent != null && transform.parent.localScale != Vector3.zero)
            {
                transform.localScale = new Vector3(
                    1f / transform.parent.localScale.x,
                    1f / transform.parent.localScale.y,
                    1f / transform.parent.localScale.z
                );
            }
            
            if (!IsOwner)
            {
                //user is not the owner, so we need to make the hands visible
                switch (IsInVR.Value)
                {
                    case true when !m_leftHand.activeSelf:
                        m_leftHand.SetActive(true);
                        break;
                    case false when m_leftHand.activeSelf:
                        m_leftHand.SetActive(false);
                        break;
                }

                switch (IsInVR.Value)
                {
                    case true when !m_rightHand.activeSelf:
                        m_rightHand.SetActive(true);
                        break;
                    case false when m_rightHand.activeSelf:
                        m_rightHand.SetActive(false);
                        break;
                }

                if (IsInVR.Value)
                {
                    //Make sure the hands are following the network object
                    if (m_leftHand.activeSelf)
                    {
                        if (m_leftHandNetworkTransform == null && LeftHandTrackerId.Value != 0 &&
                            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(LeftHandTrackerId.Value, out var leftHandTracker) &&
                            leftHandTracker != null)
                        {
                            leftHandTracker.TryGetComponent(out m_leftHandNetworkTransform);
                        }

                        if (m_leftHandNetworkTransform != null)
                        {
                            m_leftHand.transform.SetLocalPositionAndRotation(m_leftHandNetworkTransform.transform.localPosition, m_leftHandNetworkTransform.transform.localRotation);
                        }
                    }
                    if (m_rightHand.activeSelf)
                    {
                        if (m_rightHandNetworkTransform == null && RightHandTrackerId.Value != 0 &&
                            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(RightHandTrackerId.Value, out var rightHandTracker) &&
                            rightHandTracker != null)
                        {
                            rightHandTracker.TryGetComponent(out m_rightHandNetworkTransform);
                        }
                        if (m_rightHandNetworkTransform != null)
                        {
                            m_rightHand.transform.SetLocalPositionAndRotation(m_rightHandNetworkTransform.transform.localPosition, m_rightHandNetworkTransform.transform.localRotation);
                        }
                    }
                }
                
                return;
            }
            
            #if !UNITY_EDITOR
            if (IsInVR.Value != XRSettings.isDeviceActive)
            {
                IsInVR.Value = XRSettings.isDeviceActive;
                IsInVR.CheckDirtyState();
            }
            #else
            if (!isUsingSimulator)
            {
                if (IsInVR.Value != XRSettings.isDeviceActive)
                {
                    IsInVR.Value = XRSettings.isDeviceActive;
                    IsInVR.CheckDirtyState();
                }
            }
            #endif
            
            //if it is the owner, and in VR, then we need to hook up the network position and rotation based on left/right hand controllers/position
            if (IsInVR.Value)
            {
                m_XROrigin ??= FindFirstObjectByType<XROrigin>(FindObjectsInactive.Exclude);
                if(m_XROrigin == null) return;
                // VR may have been activated after spawn; make sure the trackers exist.
                EnsureVRHandTrackers();
                transform.position = m_XROrigin.Camera.transform.position;
                transform.rotation = m_XROrigin.Camera.transform.rotation;

                m_LeftHandTracker ??= GameObject.FindGameObjectWithTag("Left Hand Tracker");
                m_RightHandTracker ??= GameObject.FindGameObjectWithTag("Right Hand Tracker");
                if (m_LeftHandTracker != null && m_leftHandNetworkTransform != null)
                {
                    var worldPositionOfLeftHand = m_LeftHandTracker.transform.position;
                    var localPositionOfLeftHand = transform.InverseTransformPoint(worldPositionOfLeftHand);

                    var worldRotationOfLeftHand = m_LeftHandTracker.transform.rotation;
                    // Calculate the local rotation of the left hand relative to this player object
                    var localRotationOfLeftHand = Quaternion.Inverse(transform.rotation) * worldRotationOfLeftHand;
                    // Set the local position and rotation of the left hand tracker
                    m_leftHandNetworkTransform.transform.SetLocalPositionAndRotation(localPositionOfLeftHand, localRotationOfLeftHand);
                }

                if (m_RightHandTracker != null && m_rightHandNetworkTransform != null)
                {
                    var worldPositionOfRightHand = m_RightHandTracker.transform.position;
                    var localPositionOfRightHand = transform.InverseTransformPoint(worldPositionOfRightHand);

                    var worldRotationOfRightHand = m_RightHandTracker.transform.rotation;
                    // Calculate the local rotation of the right hand relative to this player object
                    var localRotationOfRightHand = Quaternion.Inverse(transform.rotation) * worldRotationOfRightHand;
                    // Set the local position and rotation of the right hand tracker
                    m_rightHandNetworkTransform.transform.SetLocalPositionAndRotation(localPositionOfRightHand, localRotationOfRightHand);
                }
            }
            else
            {
                if (Camera.main != null)
                {
                    var worldPosition = Camera.main.transform.position;
                    var localPosition = TransformController.Instance.transform.InverseTransformPoint(worldPosition);
                    //Give some offset to the player object
                    localPosition.y -= m_offset;

                    // Set the local position and rotation of the player object
                    if (transform.localPosition == Vector3.zero)
                    {
                        GetComponent<NetworkTransform>().Teleport(localPosition, Camera.main.transform.rotation, transform.localScale);
                    }
                    else
                    {
                        transform.localPosition = localPosition;
                        transform.rotation = Camera.main.transform.rotation;
                    }
                }
            }
            
            if(!InPresentation.Value || m_presenter == null) return;
            NavigationController.FollowPresenter?.Invoke(m_presenter);
        }
    }
}
