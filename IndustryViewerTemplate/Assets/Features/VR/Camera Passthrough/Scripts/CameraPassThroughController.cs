using System;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using Unity.Cloud.HighPrecision.Runtime;
using System.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID
using UnityEngine.XR.OpenXR.Features.Meta;
using UnityEngine.Android;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
#endif
using System.Collections.Generic;
using Unity.Industry.Viewer.VR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Unity.Cloud.DataStreaming.Runtime;

namespace Unity.Industry.Viewer.VR.CameraPassThrough
{
    public enum ARState
    {
        Initializing,
        Placing,
        Positioning,
        ConfirmPosition
    }
    
    public class CameraPassThroughController : ARPlacementNavigationOption
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string k_DefaultMetaPermissionId = "com.oculus.permission.USE_SCENE";
        const string k_DefaultAndroidXRPermissionId = "android.permission.SCENE_UNDERSTANDING_COARSE";
#endif
        
        public static Action<ARState> OnStateChange;
        public static Action<bool> RequestOcclusionOnOff;

        [SerializeField]
        MeshFilter m_MeshFilterForARMeshManager;

        [SerializeField] private GameObject m_ARPlanePrefab;
        
        ARMeshManager m_ARMeshManager;
        ARRaycastManager m_ARRaycastManager;
        AROcclusionManager m_AROcclusionManager;
        
        XROrigin m_XROrigin;
        ARPlaneManager m_ARPlaneManager;
        
        [SerializeField] private GameObject m_placingMakerPrefab;
        private GameObject m_placingMakerGO;
        
        [SerializeField]
        private LayerMask m_ARPlaneLayerMask;
        
        List<ARRaycastHit> m_RaycastHit;
        public ARState State => m_ARState;
        private ARState m_ARState;
        private ARPlane m_SelectedPlane;
        
        private StreamingModelController m_StreamingModelController;
        
        private Vector3 m_OriginalXROriginPosition;
        private Vector3 m_OriginalXROriginLookDirection;

        public bool MeshManagerSupported => false;// m_ARSession.subsystem is MetaOpenXRSessionSubsystem; //Currently we know only Meta supports mesh reconstruction

        private XRPanel.AlertXRPanel m_ScanSpacePanel;

        [SerializeField]
        private LayerMask m_ARMeshLayerMask;
        
        private bool m_HasShownRequestSceneCapture = false;

        // Guard against the trigger press that opened AR mode firing placement immediately.
        // SingleActivate fires after a 0.25s WaitForDoubleClick delay; 0.5s safely clears that window.
        private float m_placingStartTime;
        private const float k_MinPlacingDelay = 0.5f;

        public bool IsWorldMapSupported => false; //Make it false for now as we don't have a way to test it.
        
        public bool isWorldMapFound { get; private set; } = false;
        
        private LocomotionMediator m_LocomotionMediator;

        private CameraUtility Utility;
        
        DoubleBounds? m_Bounds;
        
        private void Awake()
        {
            m_LocomotionMediator = FindFirstObjectByType<LocomotionMediator>(FindObjectsInactive.Include);
            Utility ??= new CameraUtility(Camera.main);
            OnStateChange += OnARStateChanged;
        }

        private void OnEnable()
        {
            m_ARState = ARState.Initializing;
            StreamToolsController.DisableAllTools?.Invoke(true);
            ToolPanelUIController.CloseToolPanel?.Invoke();
        }

        private void Start()
        {
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>();
        }

        private void Update()
        {
            if (m_Bounds.HasValue)
            {
                var newBounds = StreamingUtils.ReturnBounds(m_Bounds.Value);
                Utility?.SetClipPlane(newBounds);
                Camera.main.nearClipPlane = 0.01f;
            }
            
            if (m_ARState == ARState.Placing)
            {
                if (m_ARRaycastManager == null || m_ScanSpacePanel != null || Camera.main == null)
                {
                    m_placingMakerGO?.SetActive(false);
                    return;
                }
                Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                m_RaycastHit ??= new List<ARRaycastHit>();
                if (m_ARRaycastManager.Raycast(ray, m_RaycastHit, TrackableType.PlaneWithinBounds))
                {
                    if (m_RaycastHit.Count == 0)
                    {
                        m_placingMakerGO?.SetActive(false);
                        return;
                    }
                    
                    ARPlane plane = m_RaycastHit[0].trackable as ARPlane;
#if UNITY_ANDROID
                    if (XRPlatformUnderstanding.CurrentPlatform == XRPlatformType.OpenXRMeta)
                    {
                        // Accept any upward-facing horizontal plane via its surface normal rather than the
                        // exact PlaneAlignment enum — different Meta package versions classify a floor's
                        // alignment differently, so relying on the normal is more robust than `== HorizontalUp`
                        // (which is kept as a fallback).
                        float upDot = plane != null ? Vector3.Dot(plane.transform.up, Vector3.up) : 1f;
                        bool upwardHorizontal = plane == null || upDot >= 0.9f || plane.alignment == PlaneAlignment.HorizontalUp;
                        if (!upwardHorizontal)
                        {
                            m_placingMakerGO?.SetActive(false);
                            return;
                        }
                    }
#endif
                    
                    var hitPose = m_RaycastHit[0].pose;
                    m_SelectedPlane = plane;
                    if (m_placingMakerGO == null)
                    {
                        m_placingMakerGO = Instantiate(m_placingMakerPrefab);
                    }
                    m_placingMakerGO.SetActive(true);
                    m_placingMakerGO.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                }
                else
                {
                    m_placingMakerGO?.SetActive(false);
                }
            }
        }
        
#if UNITY_ANDROID
        private void OnScanAlertPanelDismissed(XRPanel.CustomXRPanel obj)
        {
            m_ScanSpacePanel.Dismissed -= OnScanAlertPanelDismissed;
            m_ScanSpacePanel = null;
        }
#endif
        
        private void OnDisable()
        {
            OnNavigationOptionDisable();
            CameraPassThroughGlobal.InMRMode = false;
            m_LocomotionMediator?.gameObject.SetActive(true);
            m_XROrigin?.MoveCameraToWorldLocation(m_OriginalXROriginPosition);
            m_XROrigin?.MatchOriginUpCameraForward(Vector3.up, m_OriginalXROriginLookDirection);
            
            m_HasShownRequestSceneCapture = false;
            
            var transformControllerInstance = TransformController.Instance;
            if (transformControllerInstance != null)
            {
                transformControllerInstance.gameObject.SetActive(true);
                transformControllerInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                transformControllerInstance.transform.localScale = Vector3.one;
            }

            if (m_ARMeshManager != null)
            {
                m_ARMeshManager.enabled = false;
                foreach (var meshFilter in m_ARMeshManager.meshes)
                {
                    meshFilter.gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            OnNavigationOptionDisable();
            if (m_ARMeshManager != null)
            {
                foreach (var meshFilter in m_ARMeshManager.meshes)
                {
                    if (meshFilter != null)
                    {
                        if (meshFilter.mesh != null)
                            Destroy(meshFilter.mesh);
                        Destroy(meshFilter.gameObject);
                    }
                }
                Destroy(m_ARMeshManager.gameObject);
            }

            if (m_ARPlaneManager != null)
            {
                Destroy(m_ARPlaneManager);
            }

            if (m_ARRaycastManager != null)
            {
                Destroy(m_ARRaycastManager);
            }

            if (m_placingMakerGO != null)
            {
                Destroy(m_placingMakerGO);
            }

            if (m_AROcclusionManager != null)
            {
                Destroy(m_AROcclusionManager);
            }
            
            OnStateChange -= OnARStateChanged;
            RequestOcclusionOnOff -= OnRequestOcclusionOnOff;
        }

        private void OnBoundsUpdated(DoubleBounds newBound, bool arg2)
        {
            m_Bounds = new DoubleBounds(newBound.Center, newBound.Size * 1.5f);
        }

        private void OnARStateChanged(ARState newState)
        {
            m_ARState = newState;
            switch (newState)
            {
                case ARState.Placing:
                    m_ARPlaneManager.enabled = true;
                    foreach (var trackable in m_ARPlaneManager.trackables)
                    {
                        trackable.gameObject.SetActive(true);
                    }
                    TransformController.Instance.transform.localScale = Vector3.one;
                    TransformController.Instance.gameObject.SetActive(false);
                    m_placingStartTime = Time.unscaledTime;
                    VRInteractionController.SubscribeSingleActivate(this, SingleActivateAction);
                    break;
                
                case ARState.Positioning:
                    TransformController.Instance.gameObject.SetActive(true);
                    VRInteractionController.UnsubscribeSingleActivate(this);

                    if (m_ARPlaneManager != null)
                    {
                        m_ARPlaneManager.enabled = true;
                    
                        foreach (var trackable in m_ARPlaneManager.trackables)
                        {
                            trackable.gameObject.SetActive(true);
                        }
                    }
                    break;
                
                case ARState.ConfirmPosition:
                    foreach (var trackable in m_ARPlaneManager.trackables)
                    {
                        trackable.gameObject.SetActive(false);
                    }
                    m_ARPlaneManager.enabled = false;
                    break;
            }
        }

        private void SingleActivateAction(Ray ray, int controllerInstanceId)
        {
            if (Time.unscaledTime - m_placingStartTime < k_MinPlacingDelay) return;
            if (m_StreamingModelController == null || m_placingMakerGO == null)
            {
                return;
            }
            if (Physics.Raycast(ray, out var hit, m_StreamingModelController.ActiveCamera.farClipPlane,
                    LayerMask.GetMask("UI")))
            {
                var uiRaycastPoint = hit.point;
                var markerPosition = m_placingMakerGO.transform.position;
                float uiDistance = Vector3.Dot(uiRaycastPoint - ray.origin, ray.direction);
                float markerDistance = Vector3.Dot(markerPosition - ray.origin, ray.direction);
                bool isUICloser = uiDistance < markerDistance;
                if (isUICloser)
                {
                    return;
                }
            }
            if (m_placingMakerGO != null && m_placingMakerGO.activeInHierarchy)
            {
                m_OriginalPosition = m_placingMakerGO.transform.position;
                TransformController.Instance.transform.position = m_OriginalPosition;
                Vector3 directionToCamera = (navigationCamera.transform.position - m_OriginalPosition).normalized;
                directionToCamera.y = 0; // Keep the rotation on the horizontal plane
                m_OriginalRotation = Quaternion.LookRotation(-directionToCamera);
                TransformController.Instance.transform.rotation = m_OriginalRotation;
                TransformController.Instance.gameObject.SetActive(true);
                m_placingMakerGO.SetActive(false);
                DoubleBounds tmpBounds = m_StreamingModelController.GetWorldBounds();
                double width = tmpBounds.Max.x - tmpBounds.Min.x;
                double depth = tmpBounds.Max.z - tmpBounds.Min.z;
                // Get the plane's dimensions from its size property
                float planeWidth = m_SelectedPlane.size.x;
                float planeDepth = m_SelectedPlane.size.y;

                // Calculate the scale ratio for both width and depth
                float widthRatio = planeWidth / (float)width;
                float depthRatio = planeDepth / (float)depth;

                // Choose the smaller of the two ratios to ensure the model fits entirely
                float scaleFactor = Mathf.Min(widthRatio, depthRatio);
                scaleFactor = Mathf.Min(scaleFactor, 1f); // Limit the maximum scale factor to 1x
                    
                Scale(scaleFactor);
                OnStateChange?.Invoke(ARState.Positioning);
            }
        }

        IEnumerator WaitForOneFrame()
        {
            //Wait one frame to ensure all systems are initialized
            yield return null;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            var currentPlatform = XRPlatformUnderstanding.CurrentPlatform;
            if (currentPlatform == XRPlatformType.OpenXRMeta)
            {
                CheckPermission(k_DefaultMetaPermissionId);
            } else if (currentPlatform == XRPlatformType.OpenXRAndroidXR)
            {
                CheckPermission(k_DefaultAndroidXRPermissionId);
            }
            else
            {
                StartPassthrough();
            }
#else
            StartPassthrough();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void CheckPermission(string permission)
        {
            if (Permission.HasUserAuthorizedPermission(permission))
            {
                StartPassthrough();
            }
            else
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += OnPermissionGranted;
                Permission.RequestUserPermission(permission, callbacks);
            }
        }
#endif

        public override void Initialize()
        {
            navigationOptionUIComponent ??= GetComponent<NavigationOptionUI>();
            navigationCamera = Camera.main;
            RequestOcclusionOnOff += OnRequestOcclusionOnOff;
            StreamingModelController.BoundsUpdated += OnBoundsUpdated;
        }

        private void OnRequestOcclusionOnOff(bool newState)
        {
            m_AROcclusionManager.requestedEnvironmentDepthMode = newState ? EnvironmentDepthMode.Medium: EnvironmentDepthMode.Disabled;
            m_ARMeshManager.enabled = newState;
            foreach (var mesh in m_ARMeshManager.meshes)
            {
                mesh.gameObject.SetActive(newState);
            }
        }

        public override void Uninitialize()
        {
            RequestOcclusionOnOff -= OnRequestOcclusionOnOff;
            StreamingModelController.BoundsUpdated -= OnBoundsUpdated;
        }

        public override void OnNavigationOptionEnable()
        {
            StartCoroutine(WaitForOneFrame());
        }
        


        private void StartPassthrough()
        {
            CameraPassThroughGlobal.InMRMode = true;
            CameraPassThroughGlobal.ToggleCameraPassThrough(true);
            m_LocomotionMediator?.gameObject.SetActive(false);
            m_XROrigin ??= FindFirstObjectByType<XROrigin>();
            ARSession ARSession = FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
            if (m_XROrigin != null)
            {
                if (ARSession != null)
                {
                    SceneManager.MoveGameObjectToScene(ARSession.gameObject, m_XROrigin.gameObject.scene);
                }
                m_OriginalXROriginPosition = Camera.main.transform.position;
                m_OriginalXROriginLookDirection = Camera.main.transform.forward;
                if (m_ARMeshManager == null)
                {
                    var arMeshObject = new GameObject("ARMeshManager");
                    arMeshObject.SetActive(false);
                    arMeshObject.transform.SetParent(m_XROrigin.transform);
                    m_ARMeshManager = arMeshObject.AddComponent<ARMeshManager>();
                    m_ARMeshManager.meshPrefab = m_MeshFilterForARMeshManager;
                    m_ARMeshManager.enabled = false;
                    m_ARMeshManager.meshesChanged += ARMeshManagerOnMeshesChanged;
                }

                if (m_ARRaycastManager == null)
                {
                    m_ARRaycastManager = m_XROrigin.gameObject.AddComponent<ARRaycastManager>();
                }
                
                if (m_ARPlaneManager == null)
                {
                    m_ARPlaneManager = m_XROrigin.gameObject.AddComponent<ARPlaneManager>();
                    m_ARPlaneManager.planePrefab = m_ARPlanePrefab;
                    m_ARPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                }
            }

            if (m_ARPlaneManager != null)
            {
                m_ARPlaneManager.enabled = true;
            }

            if (m_ARRaycastManager != null)
            {
                m_ARRaycastManager.enabled = true;
            }

            if (m_AROcclusionManager == null)
            {
                m_AROcclusionManager = Camera.main.gameObject.AddComponent<AROcclusionManager>();
            }

            if (m_AROcclusionManager != null)
            {
                m_AROcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                m_AROcclusionManager.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;
                m_AROcclusionManager.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
                m_AROcclusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                m_AROcclusionManager.environmentDepthTemporalSmoothingRequested = true;
                m_AROcclusionManager.enabled = true;
            }
            
            m_ARMeshManager?.gameObject.SetActive(true);
            if (m_ARMeshManager != null)
            {
                m_ARMeshManager.enabled = false;
                foreach (var meshFilter in m_ARMeshManager.meshes)
                {
                    meshFilter.gameObject.SetActive(false);
                }
            }
            
            
            navigationCamera.clearFlags = CameraClearFlags.SolidColor;
            navigationCamera.backgroundColor = Color.clear;
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            TransformController.Instance.transform.localScale = Vector3.one;
            TransformController.Instance.gameObject.SetActive(false);
            
#if UNITY_ANDROID
            var currentPlatform = XRPlatformUnderstanding.CurrentPlatform;
            if (currentPlatform == XRPlatformType.OpenXRMeta && !m_HasShownRequestSceneCapture)
            {
                m_HasShownRequestSceneCapture = true;
                
                if (m_ScanSpacePanel == null && ARSession.subsystem is MetaOpenXRSessionSubsystem metaOpenXRSessionSubsystem)
                {
                    m_ScanSpacePanel = new XRPanel.AlertXRPanel("Space Setup", "Please make sure you have set up your space on the device");
                    m_ScanSpacePanel.SetPrimaryButton("Setup Space/Update", () =>
                    {
                        var done = metaOpenXRSessionSubsystem.TryRequestSceneCapture();
                        if(!done)
                        {
                            Debug.LogError("Failed to request scene capture.");
                            return;
                        }
                        OnStateChange.Invoke(ARState.Placing);
                    });
                    
                    m_ScanSpacePanel.Dismissed += OnScanAlertPanelDismissed;
                    m_ScanSpacePanel.Show();
                }
            }
            else
            {
                OnStateChange.Invoke(ARState.Placing);
            }
            
#else
            OnStateChange.Invoke(ARState.Placing);
#endif
            
        }

        private void ARMeshManagerOnMeshesChanged(ARMeshesChangedEventArgs eventArgs)
        {
            foreach (var meshFilter in eventArgs.added)
            {
                meshFilter.gameObject.layer = m_ARMeshLayerMask;
            }
            foreach (var meshFilter in eventArgs.updated)
            {
                meshFilter.gameObject.layer = m_ARMeshLayerMask;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnPermissionGranted(string obj)
        {
            StartPassthrough();
        }
#endif
        


        public override void OnNavigationOptionDisable()
        {
            if(!CameraPassThroughGlobal.InMRMode) return;
            CameraPassThroughGlobal.InMRMode = false;
            if (!PlayerPrefs.HasKey(CameraPassThroughGlobal.k_CameraPassThroughEnabledKey))
            {
                CameraPassThroughGlobal.ToggleCameraPassThrough(false);
            }
            
            if (m_ARPlaneManager != null)
            {
                m_ARPlaneManager.enabled = false;
            }

            if (m_ARRaycastManager != null)
            {
                m_ARRaycastManager.enabled = false;
            }

            if (m_AROcclusionManager != null)
            {
                m_AROcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                m_AROcclusionManager.enabled = false;
            }

            if (m_ARMeshManager != null)
            {
                m_ARMeshManager.enabled = false;
                foreach (var meshFilter in m_ARMeshManager.meshes)
                {
                    meshFilter.gameObject.SetActive(false);
                }
            }
            
            VRInteractionController.UnsubscribeSingleActivate(this);
            m_placingMakerGO?.SetActive(false);

            if (TransformController.Instance == null) return;
            TransformController.Instance.gameObject.SetActive(true);
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            TransformController.Instance.transform.localScale = Vector3.one;
        }

        public override bool IsSupported()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            return XRPlatformUnderstanding.CurrentPlatform == XRPlatformType.OpenXRMeta 
                   || XRPlatformUnderstanding.CurrentPlatform == XRPlatformType.OpenXRAndroidXR;
            #else
            return true; //Assume it's supported on other platforms for now since we don't have a
            #endif
        }










        

        

        

        



        





        











    }
}
