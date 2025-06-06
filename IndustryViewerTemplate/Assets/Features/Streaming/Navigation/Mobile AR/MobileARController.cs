using System;
using Unity.Industry.Viewer.Shared;
using System.Collections;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.AR.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.XR.ARSubsystems;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Industry.Viewer.Assets;
using Unity.Cloud.Assets;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Runtime;

#if UNITY_IOS && !UNITY_EDITOR
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.XR.ARKit;
using Unity.Collections;
#endif

namespace Unity.Industry.Viewer.Navigation.MobileAR
{
    
    public enum ARState
    {
        Placing,
        Positioning,
        ConfirmPosition
    }
    
    //[DefaultExecutionOrder(-100)]
    public class MobileARController : NavigationOption
    {
        private class SimpleTransformValues
        {
            public float PositionX;
            public float PositionY;
            public float PositionZ;
            public float RotationX;
            public float RotationY;
            public float RotationZ;
            public float RotationW;
            public float Scale;
            public string TrackableID;
            
            public SimpleTransformValues()
            {
            }
            
            public SimpleTransformValues(ARAnchor arAnchor, Transform parent, string trackableID)
            {
                var localPosition = parent.InverseTransformPoint(arAnchor.transform.position);
                var localRotation = arAnchor.transform.localRotation;
                
                PositionX = localPosition.x;
                PositionY = localPosition.y;
                PositionZ = localPosition.z;
                RotationX = localRotation.x;
                RotationY = localRotation.y;
                RotationZ = localRotation.z;
                RotationW = localRotation.w;
                Scale = arAnchor.transform.localScale.x;
                TrackableID = trackableID;
            }
        }
        
        public static Action<ARState> ARStateChange;
        public static Action<bool> RequestOcclusionOnOff;
        public static Action TwoFingerDragging;
        public static Action PinchScaling;
        
#region AR Anchor
        public static Action<bool> WorldMapSaveReady;
        public static Action<bool, string> WorldAnchorFileSave;
        public static Action<bool> WorldAnchorFileLoad;
        public static Action WorldAnchorAligned;
        public static Action<string> LoadMapError;
#endregion
        
        [FormerlySerializedAs("ARSessionGO")]
        [Header("AR Components")]
        [SerializeField] private ARSession ARSession;

        [SerializeField] private GameObject XROriginGO;
        
        [SerializeField]
        private ARPlaneManager m_ARPlaneManager;
        
        [SerializeField]
        private LayerMask m_ARPlaneLayerMask;
        
        [SerializeField]
        AROcclusionManager m_AROcclusionManager;
        
        [SerializeField]
        ARMeshManager m_ARMeshManager;

        [SerializeField] private GameObject m_placingMakerPrefab;
        private GameObject m_placingMakerGO;
        
        RaycastHit[] m_RaycastHit;
        
        private bool m_Initialized = false;
        private bool m_IsSupported = false;
        public bool HasWriteAccess { get; private set; }
        
        [Header("Input")]
        [SerializeField]
        XRRayInteractor m_ARInteractor;
        
        private ScreenSpaceSelectInput m_ScreenSpaceSelectInput;
        
        public ARState CurrentARState => m_ARState;
        
        private ARState m_ARState = ARState.Positioning;

        [SerializeField] private XRInputValueReader<Vector2> m_TwoFingerDragDelta;
        
        [SerializeField] private XRInputValueReader<float> m_PinchDelta;
        
        private CameraUtility m_Utility;
        
        private WaitForEndOfFrame m_WaitForEndOfFrame = new WaitForEndOfFrame();
        
        private Coroutine m_DragCoroutine;
        
        private Quaternion m_OriginalRotation;
        private Vector3 m_OriginalPosition;
        
        private DoubleBounds? m_CurrentBounds;

        private float m_PlaceTimer = 0.0f;
        
        public bool MeshManagerSupported => m_ARMeshManager != null && m_ARMeshManager.subsystem != null;

#region AR Anchor

        private const string k_WorldMapFileFormat = ".worldmap";
        private const string k_DataSetName = "ARAnchorMap";
        private const string k_MetaDataKey = "Spatial_Anchors";
        CancellationTokenSource dataSetTokenSource;
        private IDataset m_ARAnchorMapDataSet;
        IFile m_WorldMapFile;
        SimpleTransformValues m_TargetTransformValues;
        private bool m_IsSavingMap;
        public bool isWorldMapFound { get; private set; } = false;
        
        IAssetRepository m_AssetRepository => PlatformServices.AssetRepository;
        
        string localWorldMapFilePath => Path.Combine(Application.persistentDataPath, StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId + k_WorldMapFileFormat);
        
        public bool IsWorldMapSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return ARSession.subsystem is ARKitSessionSubsystem && ARKitSessionSubsystem.worldMapSupported;
#elif UNITY_EDITOR
                return true;
#endif
                return false;
            }
        }

#endregion


        public override void Initialize()
        {
            m_Utility = new CameraUtility(navigationCamera);
            
            GameObject arChecker = new GameObject("AR Checker");
            CoroutineRunner coroutineRunner = arChecker.AddComponent<CoroutineRunner>();
            coroutineRunner.RunCoroutine(CheckARSupport(), () =>
            {
                m_Initialized = true;
            });
            m_ScreenSpaceSelectInput = m_ARInteractor.selectInput.GetObjectReference() as ScreenSpaceSelectInput;
            navigationOptionUIComponent ??= GetComponent<NavigationOptionUI>();
            ARStateChange += SwitchToState;
            RequestOcclusionOnOff += OnRequestOcclusionOnOff;
            StreamingModelController.BoundsUpdated += OnBoundsUpdated;
        }

        public override void Uninitialize()
        {
            StreamingModelController.BoundsUpdated -= OnBoundsUpdated;
        }

        private void OnEnable()
        {
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            TransformController.Instance.transform.localScale = Vector3.one;
            StreamToolsController.DisableAllTools?.Invoke();
            ToolPanelUIController.CloseToolPanel?.Invoke();
            //  instantiate the placeholder and hide it
            m_placingMakerGO = Instantiate(m_placingMakerPrefab, Vector3.zero, Quaternion.identity);
            m_placingMakerGO.transform.localScale = Vector3.one;
            m_placingMakerGO.SetActive(false);
        }

        private void Start()
        {
            m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.performed += OnTapInputPerformed;
            m_TwoFingerDragDelta.inputActionReference.action.performed += OnDragDeltaPerformed;
            m_TwoFingerDragDelta.inputActionReference.action.canceled += OnDragDeltaCancelled;
            m_PinchDelta.inputActionReference.action.performed += OnPinchDeltaPerformed;
            m_PinchDelta.inputActionReference.action.canceled += OnPinchDeltaCancelled;
        }

        private void Update()
        {
            if (m_CurrentBounds is not null)
            {
                var newBounds = StreamingUtils.ReturnBounds(m_CurrentBounds.Value);
                m_Utility.SetClipPlane(newBounds);
            }
            
            if (m_ARState == ARState.Placing)
            {
                if (TransformController.Instance == null) return;
                
                m_RaycastHit ??= new RaycastHit[1];
                var result = Physics.RaycastNonAlloc(navigationCamera.transform.position, navigationCamera.transform.forward, m_RaycastHit, 10f,
                    m_ARPlaneLayerMask);

                //  no hits
                if (result == 0)
                {
                    if (m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.enabled)
                    {
                        m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Disable();
                    }

                    if (m_PinchDelta.inputActionReference.action.enabled)
                    {
                        m_PinchDelta.inputActionReference.action.Disable();
                    }
                    
                    TransformController.Instance.gameObject.SetActive(false);
                    return;
                }

                //  display the placeholder duting ARState.Placing instead of the model itself
                m_placingMakerGO.SetActive(true);
                m_placingMakerGO.transform.position = m_RaycastHit[0].point;
                if (!m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.enabled)
                {
                    m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Enable();
                }

                if (!m_PinchDelta.inputActionReference.action.enabled)
                {
                    m_PinchDelta.inputActionReference.action.Enable();
                }
            }
            
#if UNITY_IOS && !UNITY_EDITOR
            var sessionSubsystem = (ARKitSessionSubsystem)ARSession.subsystem;
            if (sessionSubsystem == null)
            {
                Debug.LogError("No session subsystem available.");
                return;
            }
            if (!m_IsSavingMap)
            {
                bool isSaveMapReady = !m_IsSavingMap && sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Mapped && m_ARState == ARState.ConfirmPosition;
                WorldMapSaveReady?.Invoke(isSaveMapReady);
            }

            if (sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Mapped && m_TargetTransformValues != null)
            {
                Transform targetTrackable = null;
                foreach (var trackable in m_ARPlaneManager.trackables)
                {
                    if (!string.Equals(trackable.trackableId.ToString(), m_TargetTransformValues.TrackableID)) continue;
                    targetTrackable = trackable.transform;
                }
                if (targetTrackable == null) return;
                var targetPosition = targetTrackable.TransformPoint(new Vector3(m_TargetTransformValues.PositionX, m_TargetTransformValues.PositionY, m_TargetTransformValues.PositionZ));
                Quaternion resultRotation = new Quaternion(m_TargetTransformValues.RotationX, m_TargetTransformValues.RotationY, m_TargetTransformValues.RotationZ, m_TargetTransformValues.RotationW);
                TransformController.Instance.transform.transform.position = targetPosition;
                TransformController.Instance.transform.transform.localRotation = resultRotation;
                TransformController.Instance.transform.localScale = Vector3.one * m_TargetTransformValues.Scale;
                
                m_TargetTransformValues = null;
                WorldAnchorAligned?.Invoke();
                ARStateChange?.Invoke(ARState.ConfirmPosition);
            }
#endif
        }


        private void OnDisable()
        {
            dataSetTokenSource?.Cancel();
            dataSetTokenSource?.Dispose();
            dataSetTokenSource = null;
            TransformController.Instance.gameObject.SetActive(true);
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            TransformController.Instance.transform.localScale = Vector3.one;
            m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Disable();
            m_TwoFingerDragDelta.inputActionReference.action.Disable();
            m_PinchDelta.inputActionReference.action.Disable();
            m_placingMakerGO.SetActive(false);
            
            foreach (var plane in m_ARPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            ARStateChange -= SwitchToState;
            RequestOcclusionOnOff -= OnRequestOcclusionOnOff;
            m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.performed -= OnTapInputPerformed;
            m_TwoFingerDragDelta.inputActionReference.action.performed -= OnDragDeltaPerformed;
            m_TwoFingerDragDelta.inputActionReference.action.canceled -= OnDragDeltaCancelled;
            m_PinchDelta.inputActionReference.action.performed -= OnPinchDeltaPerformed;
            m_PinchDelta.inputActionReference.action.canceled -= OnPinchDeltaCancelled;
        }
        
        private void OnBoundsUpdated(DoubleBounds arg1, bool arg2)
        {
            m_CurrentBounds = arg1;
        }

        private void OnRequestOcclusionOnOff(bool newState)
        {
            m_AROcclusionManager.requestedEnvironmentDepthMode = newState ? EnvironmentDepthMode.Medium : EnvironmentDepthMode.Disabled;
            m_ARMeshManager.enabled = newState;
            foreach (var mesh in m_ARMeshManager.meshes)
            {
                mesh.gameObject.SetActive(newState);
            }
            //Debug.Log($"Occlusion {newState}, Mode {m_AROcclusionManager.requestedEnvironmentDepthMode}, Mesh {m_ARMeshManager.enabled}");
        }
        
        private void OnPinchDeltaCancelled(InputAction.CallbackContext action)
        {
            m_TwoFingerDragDelta.inputActionReference.action.Enable();
        }
        
        private void OnPinchDeltaPerformed(InputAction.CallbackContext action)
        {
            if(!action.performed) return;
            StartCoroutine(WaitForEndOfFrameTouch());
            return;
            
            IEnumerator WaitForEndOfFrameTouch()
            {
                yield return m_WaitForEndOfFrame;
                var isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
                if (!isPointerOverUI)
                {
                    if (!TransformController.Instance.gameObject.activeSelf) yield break;
                    var gesture = action.ReadValue<float>();
                    var scaleThreshold = 5f;
                    if (Mathf.Abs(gesture) > scaleThreshold)
                    {
                        var targetScale = Mathf.Clamp(TransformController.Instance.transform.localScale.x + (gesture * 0.01f), 0, 2f);
                        TransformController.Instance.transform.localScale = Vector3.one * targetScale;
                        PinchScaling?.Invoke();
                        
                        m_TwoFingerDragDelta.inputActionReference.action.Disable();
                    }
                    else
                    {
                        m_TwoFingerDragDelta.inputActionReference.action.Enable();
                    }
                }
            }
        }
        
        private void OnTapInputPerformed(InputAction.CallbackContext action)
        {
            if(!action.performed) return;
            StartCoroutine(WaitForEndOfFrameTouch());
            
            IEnumerator WaitForEndOfFrameTouch()
            {
                yield return m_WaitForEndOfFrame;
                var isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
                if (!isPointerOverUI)
                {
                    if (!m_placingMakerGO.activeSelf) yield break;

                    if (!TransformController.Instance.gameObject.activeSelf)
                    {
                        m_OriginalPosition = m_placingMakerGO.transform.position;
                        TransformController.Instance.transform.position = m_OriginalPosition;
                        
                        //Make sure object is facing the camera
                        Vector3 directionToCamera = (navigationCamera.transform.position - m_OriginalPosition).normalized;
                        directionToCamera.y = 0; // Keep the rotation on the horizontal plane
                        m_OriginalRotation = Quaternion.LookRotation(-directionToCamera);
                        
                        TransformController.Instance.transform.rotation = m_OriginalRotation;
                        
                        TransformController.Instance.gameObject.SetActive(true);
                        m_placingMakerGO.SetActive(false);

                        //  get bound and AR plane size to compute better scale factor
                        MobileARUIController tempController = GetComponent<MobileARUIController>();
                        if(tempController != null)
                        {
                            float tmpScaleFactor = 1.0f;

                            //  get bounds
                            StreamingModelController streamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
                            DoubleBounds tmpBounds = streamingModelController.GetWorldBounds();
                            double width = tmpBounds.Max.x - tmpBounds.Min.x;
                            double depth = tmpBounds.Max.z - tmpBounds.Min.z;
                            float tmpModelArea = (float)(width * depth);

                            //  get AR plane size
                            float distance = 0f;
                            float tmpPlaneSize = 0.0f;
                            foreach (var trackable in m_ARPlaneManager.trackables)
                            {
                                var planeDistance = Vector3.Distance(trackable.transform.position, TransformController.Instance.transform.position);
                                if (distance == 0 || planeDistance < distance)
                                {
                                    distance = planeDistance;
                                    tmpPlaneSize = trackable.size.x * trackable.size.y;

                                    tmpScaleFactor = Mathf.Sqrt(tmpPlaneSize / tmpModelArea);
                                    
                                    //Match to the UI lowest scale factor and highest scale factor
                                    tmpScaleFactor = Mathf.Clamp(tmpScaleFactor, 0.01f, 2f);
                                }
                            }

                            //  set the initial scale factor
                            Scale(tmpScaleFactor);
                        }
                    }
                    ARStateChange?.Invoke(ARState.Positioning);
                }
            }
        }
        
        private void OnDragDeltaCancelled(InputAction.CallbackContext action)
        {
            m_PinchDelta.inputActionReference.action.Enable();
        }

        private void OnDragDeltaPerformed(InputAction.CallbackContext action)
        {
            if(!action.performed) return;
            if (m_DragCoroutine != null)
            {
                StopCoroutine(m_DragCoroutine);
            }
            StartCoroutine(WaitForEndOfFrameTouch());

            IEnumerator WaitForEndOfFrameTouch()
            {
                yield return m_WaitForEndOfFrame;
                var isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
                if (!isPointerOverUI)
                {
                    const float moveThreshold = 10f;
                    var gesture = action.ReadValue<Vector2>();

                    //This is when the XZ or Y movement lock applied
                    if (Mathf.Abs(gesture.y) > moveThreshold || Mathf.Abs(gesture.x) > moveThreshold)
                    {
                        Vector3 moveDir = new Vector3(gesture.x > 0 && Mathf.Abs(gesture.x) > moveThreshold ? 1 : gesture.x < 0 && Mathf.Abs(gesture.x) > moveThreshold? -1 : 0, 0, gesture.y > 0 && Mathf.Abs(gesture.y) > moveThreshold ? 1 : gesture.y < 0 && Mathf.Abs(gesture.y) > moveThreshold? -1 : 0) * 0.01f;

                        Move(moveDir);
                        TwoFingerDragging?.Invoke();
                        
                        m_PinchDelta.inputActionReference.action.Disable();
                    }
                    else
                    {
                        m_PinchDelta.inputActionReference.action.Enable();
                    }
                    
                    /*
                    This is a function to move the object and make sure object to snap on the ARPlane
                    if(Mathf.Abs(gesture.y) > moveThreshold || Mathf.Abs(gesture.x) > moveThreshold)
                    {
                        Vector3 cameraForward =
                            Mathf.Abs(gesture.y) > moveThreshold ? navigationCamera.transform.forward : Vector3.zero;
                        Vector3 cameraRight = Mathf.Abs(gesture.x) > moveThreshold ? navigationCamera.transform.right : Vector3.zero;
                        
                        gesture.Normalize();
                    
                        Vector3 moveDir = (cameraRight * gesture.x + cameraForward * gesture.y) * 0.05f;
                    
                        var targetPos = TransformController.Instance.transform.position + moveDir;

                        var raycastPoint = new Vector3(targetPos.x, navigationCamera.transform.position.y + 1f,
                            targetPos.z);
                    
                        var result = Physics.RaycastAll(raycastPoint, Vector3.down, 10f, m_ARPlaneLayerMask);
                    
                        if(result.Length == 0) yield break;
                    
                        TransformController.Instance.transform.position = result[0].point;
                    
                        TwoFingerDragging?.Invoke();
                        
                        m_PinchDelta.inputActionReference.action.Disable();
                    }
                    else
                    {
                        m_PinchDelta.inputActionReference.action.Enable();
                    }
                    
                    */
                }
            }
        }

        public void SaveSpatialAnchor()
        {
#if UNITY_IOS && !UNITY_EDITOR
            StartCoroutine(SaveWorldMap());
            
            IEnumerator SaveWorldMap()
            {
                m_IsSavingMap = true;
                
                var sessionSubsystem = (ARKitSessionSubsystem)ARSession.subsystem;
                if (sessionSubsystem == null)
                {
                    WorldAnchorFileSave?.Invoke(false, "No subsystem available");
                    m_IsSavingMap = false;
                    Debug.LogError("No session subsystem available. Could not save.");
                    yield break;
                }

                ARAnchor arAnchor;
                
                if (!TransformController.Instance.gameObject.TryGetComponent(out arAnchor))
                {
                    arAnchor = TransformController.Instance.transform.gameObject.AddComponent<ARAnchor>();
                }

                if (arAnchor == null)
                {
                    WorldAnchorFileSave?.Invoke(false, "No AR Anchor available");
                    m_IsSavingMap = false;
                    Debug.LogError("No AR Anchor available");
                    yield break;
                }
                
                yield return new WaitForEndOfFrame();

                float distance = 0f;
                ARPlane closestPlane = null;
                foreach (var trackable in m_ARPlaneManager.trackables)
                {
                    var planeDistance = Vector3.Distance(trackable.transform.position, TransformController.Instance.transform.position);
                    if(distance == 0 || planeDistance < distance)
                    {
                        distance = planeDistance;
                        closestPlane = trackable;
                    }
                }
                
                var request = sessionSubsystem.GetARWorldMapAsync();
            
                while (!request.status.IsDone())
                    yield return null;
                
                if (request.status.IsError())
                {
                    WorldAnchorFileSave?.Invoke(false, "Session serialization failed");
                    m_IsSavingMap = false;
                    Debug.LogError($"Session serialization failed with status {request.status}");
                    yield break;
                }
                
                var worldMap = request.GetWorldMap();
                request.Dispose();
                
                if (File.Exists(localWorldMapFilePath))
                {
                    File.Delete(localWorldMapFilePath);
                }

                try
                {
                    //Debug.Log("Serializing ARWorldMap to byte array...");
                    var data = worldMap.Serialize(Allocator.Temp);
                    var file = File.Open(localWorldMapFilePath, FileMode.Create);
                    //Debug.Log($"ARWorldMap has {data.Length} bytes.");
                    var writer = new BinaryWriter(file);
                    writer.Write(data.ToArray());
                    writer.Close();
                    data.Dispose();
                    worldMap.Dispose();
                } catch (Exception e)
                {
                    WorldAnchorFileSave?.Invoke(false, "Failed to serialize ARWorldMap");
                    Debug.LogError(e);
                    m_IsSavingMap = false;
                    yield break;
                }
                
                
                //Debug.Log($"ARWorldMap written to {localWorldMapFilePath}");
                
                while (!File.Exists(localWorldMapFilePath))
                {
                    yield return null;
                }
                //Debug.Log("Finish Writing ARWorldMap to file");
                _ = UploadToDataSet(arAnchor, closestPlane);
            }

            async Task UploadToDataSet(ARAnchor arAnchor, ARPlane parent)
            {
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();

                IAsset unfrozenAsset = null;
                
                if (StreamingModelController.StreamingAsset.Value.Properties.Value.State == AssetState.Frozen)
                {
                    dataSetTokenSource?.Cancel();
                    dataSetTokenSource = new CancellationTokenSource();

                    try
                    {
                        unfrozenAsset = await StreamingModelController.StreamingAsset.Value.Asset.CreateUnfrozenVersionAsync(dataSetTokenSource.Token);
                    } catch (Exception e)
                    {
                        WorldAnchorFileSave?.Invoke(false, e.Message);
                        Debug.LogError(e);
                        m_IsSavingMap = false;
                        Destroy(arAnchor);
                        return;
                    }
                }
                else
                {
                    unfrozenAsset = StreamingModelController.StreamingAsset.Value.Asset;
                }
                if(unfrozenAsset == null) return;
                
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();

                try
                {
                    await foreach (var dataSet in unfrozenAsset.ListDatasetsAsync(Range.All, dataSetTokenSource.Token))
                    {
                        var datasetProperties = await dataSet.GetPropertiesAsync(CancellationToken.None);
                        if(!string.Equals(datasetProperties.Name, k_DataSetName)) continue;
                        m_ARAnchorMapDataSet = dataSet;
                        break;
                    }
                } catch (Exception e)
                {
                    WorldAnchorFileSave?.Invoke(false, "Failed to get datasets");
                    Debug.LogError(e);
                    m_IsSavingMap = false;
                    Destroy(arAnchor);
                    return;
                }
                    
                if (m_ARAnchorMapDataSet == null)
                {
                    //Debug.Log("No Dataset found, creating a new one");
                    DatasetCreation newDataSet = new DatasetCreation(k_DataSetName);
                    dataSetTokenSource?.Cancel();
                    dataSetTokenSource = new CancellationTokenSource();

                    try
                    {
                        m_ARAnchorMapDataSet = await unfrozenAsset.CreateDatasetAsync(newDataSet, dataSetTokenSource.Token);
                    } catch (Exception e)
                    {
                        WorldAnchorFileSave?.Invoke(false, "Failed to create dataset");
                        Debug.LogError(e);
                        m_IsSavingMap = false;
                        Destroy(arAnchor);
                        return;
                    }
                    //Debug.Log("Dataset created");
                }
                
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();

                try
                {
                    await foreach (var dataFile in m_ARAnchorMapDataSet.ListFilesAsync(Range.All, dataSetTokenSource.Token))
                    {
                        //Debug.Log("Removing " + dataFile.Descriptor.Path);
                        await m_ARAnchorMapDataSet.RemoveFileAsync(dataFile.Descriptor.Path, CancellationToken.None);
                        //Debug.Log("Removed file");
                    }
                } catch (Exception e)
                {
                    WorldAnchorFileSave?.Invoke(false, "Failed to remove files");
                    Debug.LogError(e);
                    m_IsSavingMap = false;
                    Destroy(arAnchor);
                    return;
                }
                
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();
                
                var fileName = Path.GetFileName(localWorldMapFilePath);
                FileCreation fileCreation = new FileCreation(fileName)
                {
                    DisableAutomaticTransformations = true
                };
                //Debug.Log("Uploading ARWorldMap...");
                Stream sourceStream = File.OpenRead(localWorldMapFilePath);
                
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();

                try
                {
                    m_WorldMapFile = await m_ARAnchorMapDataSet.UploadFileAsync(fileCreation, sourceStream, null, default);
                    //Debug.Log("Uploaded file");
                    if (File.Exists(localWorldMapFilePath))
                    {
                        File.Delete(localWorldMapFilePath);
                    }
                    
                    var simpleTransformValues = new SimpleTransformValues(arAnchor, parent.transform, parent.trackableId.ToString());
                    var valueInJson = JsonConvert.SerializeObject(simpleTransformValues);

                    if (!await HasMetaDataField(k_MetaDataKey))
                    {
                        var orgID = unfrozenAsset.Descriptor.OrganizationId;
                        var fieldCreation = new FieldDefinitionCreation()
                        {
                            Key = k_MetaDataKey,
                            Type = FieldDefinitionType.Text,
                            DisplayName = k_MetaDataKey
                        };
                        
                        dataSetTokenSource?.Cancel();
                        dataSetTokenSource = new CancellationTokenSource();
                        //Debug.Log("Creating Field Definition");
                        await m_AssetRepository.CreateFieldDefinitionAsync(orgID, fieldCreation, dataSetTokenSource.Token);
                        //Debug.Log("Created Field Definition");
                    }
                    
                    dataSetTokenSource?.Cancel();
                    dataSetTokenSource = new CancellationTokenSource();
                    StringMetadata metadataValue = new StringMetadata(valueInJson);
                    
                    //Debug.Log("Adding metadata to file");

                    try
                    {
                        await unfrozenAsset.Metadata.AddOrUpdateAsync(k_MetaDataKey, metadataValue, dataSetTokenSource.Token);
                    } catch (Exception e)
                    {
                        WorldAnchorFileSave?.Invoke(false, "Failed to add metadata");
                        Debug.LogError(e);
                        m_IsSavingMap = false;
                        Destroy(arAnchor);
                        return;
                    }
                    
                    //Debug.Log("Added metadata to file");
                    Destroy(arAnchor);
                    
                    WorldAnchorFileSave?.Invoke(true, string.Empty);
                    m_IsSavingMap = false;

                    try
                    {
                        var assetFreeze = new AssetFreeze("Update AR Anchor File")
                        {
                            Operation = AssetFreezeOperation.CancelTransformations
                        };
                        await unfrozenAsset.FreezeAsync(assetFreeze, CancellationToken.None);
                        //Debug.Log(sequenceNumber);
                        //Debug.Log("Saved ARWorldMap");
                        //Delay half a second to allow the asset to be refresh on server
                        bool updatedAsset = false;
                        while (!updatedAsset)
                        {
                            float elapsed = 0f;
                            while (elapsed < 0.5f)
                            {
                                await Task.Yield();
                                elapsed += Time.deltaTime;
                            }
                            dataSetTokenSource?.Cancel();
                            dataSetTokenSource = new CancellationTokenSource();
                            var latestVersion = await 
                                AssetsController.SelectedAsset.Value.Asset.WithLatestVersionAsync(dataSetTokenSource
                                    .Token);
                            dataSetTokenSource?.Cancel();
                            dataSetTokenSource = new CancellationTokenSource();
                            var property = await latestVersion.GetPropertiesAsync(dataSetTokenSource.Token);

                            if (property.FrozenSequenceNumber == AssetsController.SelectedAsset.Value.Properties.Value
                                    .FrozenSequenceNumber)
                            {
                                continue;
                            }
                            
                            var assetInfo = new AssetInfo()
                            {
                                Asset = latestVersion,
                                Properties = property
                            };
                            StreamingModelController.StreamingAsset = assetInfo;
                            AssetsController.AssetSelected?.Invoke(assetInfo);
                            updatedAsset = true;
                        }
                    }
                    catch (Exception e)
                    {
                        WorldAnchorFileSave?.Invoke(false, "Failed to freeze asset");
                        Debug.LogError(e);
                    }
                } catch (Exception e)
                {
                    WorldAnchorFileSave?.Invoke(false, "Failed to save file");
                    Debug.LogError(e);
                }
            }
            
            async Task<bool> HasMetaDataField(string key)
            {
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();
                
                var orgID = StreamingModelController.StreamingAsset.Value.Asset.Descriptor.OrganizationId;
                await foreach (var field in m_AssetRepository.ListFieldDefinitionsAsync(orgID, Range.All, dataSetTokenSource.Token))
                {
                    if (string.Equals(field.Descriptor.FieldKey, key))
                    {
                        return true;
                    }
                }

                return false;
            }
#endif
        }
        
        public void LoadSpatialAnchor()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _ = LoadWorldMapFile();
            
            return;

            async Task LoadWorldMapFile()
            {
                if (m_WorldMapFile == null)
                {
                    Debug.LogError("No world map file found");
                    LoadMapError?.Invoke("No ARWorldMap was found");
                    return;
                }

                if (File.Exists(localWorldMapFilePath))
                {
                    File.Delete(localWorldMapFilePath);
                }
            
                var destinationPath = Path.Combine(Application.persistentDataPath, m_WorldMapFile.Descriptor.Path);
            
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();
                var fileMetadata = StreamingModelController.StreamingAsset.Value.Asset.Metadata.Query().ExecuteAsync(dataSetTokenSource.Token);
                MetadataValue metadataValue = null;
                await foreach (var metadata in fileMetadata)
                {
                    //Debug.Log("Metadata " + metadata.Key + " " + metadata.Value + " " + metadata.Value.GetType());
                    if(!string.Equals(metadata.Key, k_MetaDataKey)) return;
                    metadataValue = metadata.Value;
                    break;
                }

                if (metadataValue == null)
                {
                    LoadMapError?.Invoke("No metadata found");
                    Debug.LogError("No metadata found");
                    return;
                }
                dataSetTokenSource?.Cancel();
                dataSetTokenSource = new CancellationTokenSource();
                m_TargetTransformValues = JsonConvert.DeserializeObject<SimpleTransformValues>(metadataValue.ToString());
                //Debug.Log(m_TargetTransformValues.Scale);
                await using var destination = File.OpenWrite(destinationPath);
                //Debug.Log("Downloading ARWorldMap...");
                
                await m_WorldMapFile.DownloadAsync(destination, null, dataSetTokenSource.Token);
                destination.Close();
                
                StartCoroutine(LoadMapFromLocalStorage());
            }

            IEnumerator LoadMapFromLocalStorage()
            {
                var sessionSubsystem = (ARKitSessionSubsystem)ARSession.subsystem;
                if(sessionSubsystem == null)
                {
                    LoadMapError?.Invoke("No ARWorldMap was found");
                    Debug.LogError("No session subsystem available. Could not load.");
                    yield break;
                }
                FileStream file;
                try
                {
                    file = File.Open(localWorldMapFilePath, FileMode.Open);
                }
                catch (FileNotFoundException)
                {
                    LoadMapError?.Invoke("No ARWorldMap was found");
                    Debug.LogError("No ARWorldMap was found. Make sure to save the ARWorldMap before attempting to load it.");
                    yield break;
                }
                
                const int bytesPerFrame = 1024 * 10;
                var bytesRemaining = file.Length;
                var binaryReader = new BinaryReader(file);
                var allBytes = new List<byte>();
                while (bytesRemaining > 0)
                {
                    var bytes = binaryReader.ReadBytes(bytesPerFrame);
                    allBytes.AddRange(bytes);
                    bytesRemaining -= bytesPerFrame;
                    yield return null;
                }
            
                var data = new NativeArray<byte>(allBytes.Count, Allocator.Temp);
                data.CopyFrom(allBytes.ToArray());
            
                if (ARWorldMap.TryDeserialize(data, out ARWorldMap worldMap))
                    data.Dispose();
            
                if (!worldMap.valid)
                {
                    LoadMapError?.Invoke("Data is not a valid ARWorldMap.");
                    Debug.LogError("Data is not a valid ARWorldMap.");
                    yield break;
                }
            
                //Debug.Log("Apply ARWorldMap to current session.");
                sessionSubsystem.ApplyWorldMap(worldMap);
                file.Close();
                File.Delete(localWorldMapFilePath);
            }
#endif
        }
        
        private void Move(Vector3 dir)
        {
            TransformController.Instance.transform.position += dir;
        }

        public void Scale(float value)
        {
            TransformController.Instance.transform.localScale = Vector3.one * value;
        }

        public void ResetPosition()
        {
            TransformController.Instance.transform.position = m_OriginalPosition;
        }
        
        public void ResetRotation()
        {
            TransformController.Instance.transform.rotation = m_OriginalRotation;
        }

        public void RotateZ(float newValue)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(rotation.eulerAngles.x, rotation.eulerAngles.z, newValue);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void RotateY(float newValue)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(rotation.eulerAngles.x, newValue, rotation.eulerAngles.z);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void RotateX(float newValue)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(newValue, rotation.eulerAngles.y, rotation.eulerAngles.z);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void RotateZBy(float value)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(rotation.eulerAngles.x, rotation.eulerAngles.y, rotation.eulerAngles.z + value);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void RotateYBy(float value)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(rotation.eulerAngles.x, rotation.eulerAngles.y + value, rotation.eulerAngles.z);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void RotateXBy(float value)
        {
            Quaternion rotation = TransformController.Instance.transform.rotation;
            rotation.eulerAngles = new Vector3(rotation.eulerAngles.x + value, rotation.eulerAngles.y, rotation.eulerAngles.z);
            TransformController.Instance.transform.rotation = rotation;
        }

        public void MoveZPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.z = value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveYPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.y = value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveXPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.x = value;
            TransformController.Instance.transform.position = originalPos;
        }
        
        public void MoveZPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.z += value;
            TransformController.Instance.transform.position = originalPos;
        }
        
        public void MoveYPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.y += value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveXPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.x += value;
            TransformController.Instance.transform.position = originalPos;
        }

        private void SwitchToState(ARState newState)
        {
            m_ARState = newState;
            Debug.Log("ARState: " + m_ARState);
            switch (m_ARState)
            {
                case ARState.Placing:
                    m_ARPlaneManager.enabled = true;
                    foreach (var trackable in m_ARPlaneManager.trackables)
                    {
                        trackable.gameObject.SetActive(true);
                    }
                    m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Disable();
                    m_TwoFingerDragDelta.inputActionReference.action.Disable();
                    m_PinchDelta.inputActionReference.action.Disable();
                    TransformController.Instance.transform.localScale = Vector3.one;
                    TransformController.Instance.gameObject.SetActive(false);
                    m_PlaceTimer = Time.timeSinceLevelLoad;
                    break;
                
                case ARState.Positioning:
                    m_ARPlaneManager.enabled = true;
                    foreach (var trackable in m_ARPlaneManager.trackables)
                    {
                        trackable.gameObject.SetActive(true);
                    }
                    m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Disable();
                    m_TwoFingerDragDelta.inputActionReference.action.Enable();
                    m_PinchDelta.inputActionReference.action.Enable();
                    break;
                
                case ARState.ConfirmPosition:
                    foreach (var trackable in m_ARPlaneManager.trackables)
                    {
                        trackable.gameObject.SetActive(false);
                    }
                    m_ARPlaneManager.enabled = false;
                    m_TwoFingerDragDelta.inputActionReference.action.Disable();
                    m_PinchDelta.inputActionReference.action.Disable();
                    break;
            }
        }
        
        
        public override void OnNavigationOptionEnable()
        {
            XROriginGO.SetActive(true);
            ARSession.gameObject.SetActive(true);
            HasWriteAccess = false;
            isWorldMapFound = false;
            if (!NetworkDetector.RequestedOfflineMode)
            {
                AssetsController.CheckHaveWriteAccess?.Invoke(StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectDescriptor, (hasPermission) =>
                {
                    HasWriteAccess = hasPermission;
                });
            }
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            TransformController.Instance.transform.localScale = Vector3.one;
            StreamingModelController.AddObserver?.Invoke(navigationCamera);
            ARStateChange?.Invoke(ARState.Placing);
            m_AROcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
            m_ARMeshManager.enabled = false;
            if(m_ARMeshManager.meshes != null){
                foreach (var mesh in m_ARMeshManager.meshes)
                {
                    mesh.gameObject.SetActive(false);
                }
            }
            
            if(!IsWorldMapSupported) return;
            _ = CheckIfWorldMapExists();
            return;
        }
        
        public async Task CheckIfWorldMapExists()
        {
            dataSetTokenSource?.Cancel();
            dataSetTokenSource = new CancellationTokenSource();
                
            await foreach(var dataset in StreamingModelController.StreamingAsset.Value.Asset.ListDatasetsAsync(Range.All, dataSetTokenSource.Token))
            {
                var properties = await dataset.GetPropertiesAsync(CancellationToken.None);
                if(!string.Equals(properties.Name, k_DataSetName)) continue;
                m_ARAnchorMapDataSet = dataset;
                break;
            }
                
            if (m_ARAnchorMapDataSet == null)
            {
                WorldAnchorFileLoad?.Invoke(false);
                return;
            }
                
            dataSetTokenSource?.Cancel();
            dataSetTokenSource = new CancellationTokenSource();
            await foreach (var dataFile in m_ARAnchorMapDataSet.ListFilesAsync(Range.All, dataSetTokenSource.Token))
            {
                if (!string.Equals(Path.GetExtension(dataFile.Descriptor.Path), k_WorldMapFileFormat)) continue;
                m_WorldMapFile = dataFile;
                isWorldMapFound = true;
                WorldAnchorFileLoad?.Invoke(true);
                return;
            }
            WorldAnchorFileLoad?.Invoke(false);
        }

        public override void OnNavigationOptionDisable()
        {
            ARSession.gameObject.SetActive(false);
            XROriginGO.SetActive(false);
            TransformController.Instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_ScreenSpaceSelectInput.tapStartPositionInput.inputActionReference.action.Disable();
            m_TwoFingerDragDelta.inputActionReference.action.Disable();
            m_PinchDelta.inputActionReference.action.Disable();
            TransformController.Instance.transform.localScale = Vector3.one;
            
            // Deactivate ARPlane objects
            foreach (var plane in m_ARPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }

        private IEnumerator CheckARSupport()
        {
            if(m_Initialized) yield break;
            m_Initialized = true;
            yield return new WaitForEndOfFrame();
            if (!XRGeneralSettings.Instance.Manager.isInitializationComplete)
            {
                yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
                if (XRGeneralSettings.Instance.Manager.activeLoader == null)
                {
                    yield break;
                }
                XRGeneralSettings.Instance.Manager.StartSubsystems();
            }
            
            if(ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            {
                yield return ARSession.CheckAvailability();
            }

            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                    yield break;
                case ARSessionState.NeedsInstall:
                    yield return ARSession.Install();
                    break;
            }

            if(ARSession.state == ARSessionState.Ready)
            {
                m_IsSupported = true;
            }
        }

        public override bool IsSupported()
        {
#if UNITY_EDITOR
            return true;
#else
            return m_Initialized && m_IsSupported;
#endif
        }

        public override GameObject GetNavigationGameObject()
        {
            return null;
        }

        public override void SetDefaultView() { }
        public override void FocusToPoint(DoubleBounds bounds)
        {
            
        }
    }
}
