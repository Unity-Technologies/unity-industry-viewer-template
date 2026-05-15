using System;
using Unity.Cloud.Collaboration;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Collaboration;
using System.Collections.Generic;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Identity;
using System.Collections;
using UnityEngine.EventSystems;

namespace Unity.Industry.Viewer.Streaming.Annotation
{
    public class AnnotationToolUIController: StreamToolUIBase
    {
        private const string k_AnnotationRootName = "AnnotationRoot";
        
        AnnotationToolController annotationToolController;
        
        [SerializeField]
        private StyleSheet m_StyleSheet;
        
        [SerializeField]
        CollaborationUIHelper m_CollaborationUIHelper;

        [SerializeField] private GameObject m_sceneMarkupPrefab;
        
        private GameObject currentSceneMarkupInstance;

        private Dictionary<AnnotationId, SpatialMarkupController> m_AnnotationToSpatialController;

#if VR_MODE
        private int? _markupInstanceId;
#endif

        private void Start()
        {
            m_CollaborationUIHelper.AnnotationHasBeenUpdated += OnAnnotationHasBeenUpdated;
        }

        private void OnDestroy()
        {
            m_CollaborationUIHelper.AnnotationHasBeenUpdated -= OnAnnotationHasBeenUpdated;
            m_CollaborationUIHelper.TokenSource?.Cancel();
            DeselectAllMarkUp();
            if (m_PanelDocument != null)
            {
                if (m_PanelDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
                {
                    m_PanelDocument.rootVisualElement.styleSheets.Remove(m_StyleSheet);
                }
            }
            if (m_AnnotationToSpatialController != null)
            {
                foreach (var controller in m_AnnotationToSpatialController.Values)
                {
                    Destroy(controller.gameObject);
                }
            }
            m_AnnotationToSpatialController?.Clear();
            m_AnnotationToSpatialController = null;
            RemoveUnfinishedEntry();
            // There is a bug in Unity that multiple gameobject will be recreated when reopening this tool again.
            // Here is a workaround to fix this for now.
            // This can be removed when Unity fixed this issue.
            GameObject workaroundObject = new GameObject("temp");
            CoroutineRunner coroutineRunner = workaroundObject.AddComponent<CoroutineRunner>();
            coroutineRunner.RunCoroutine(RefreshEventSystem(), null);
            return;

            IEnumerator RefreshEventSystem()
            {
                if(EventSystem.current.gameObject == null) yield break;
                yield return null;
                var eventGameObject = EventSystem.current.gameObject;
                eventGameObject?.SetActive(false);
                yield return null;
                eventGameObject?.SetActive(true);
            }
        }

        private void OnAnnotationHasBeenUpdated(IAnnotation newAnnotation)
        {
            if(m_AnnotationToSpatialController == null || !m_AnnotationToSpatialController.TryGetValue(newAnnotation.AnnotationId, out var spatialController))
                return;
            spatialController.UpdateAnnotation(newAnnotation);
        }

        public override void InitializeUI(UIDocument uiDocument, VisualElement parent, GameObject controller)
        {
            m_PanelDocument = uiDocument;
            
            if (!m_PanelDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                m_PanelDocument.rootVisualElement.styleSheets.Add(m_StyleSheet);
            }
            
            VisualElement root = parent.Q<VisualElement>(k_AnnotationRootName);
            
            if (NetworkDetector.IsOffline || IdentityController.GuestMode || !PlatformServices.IsUserLoggedIn)
            {
                m_CollaborationUIHelper.InsertCollaborationNotAvailable(root);
                return;
            }
            
            root.style.marginBottom = new Length(10, LengthUnit.Pixel);
            annotationToolController = controller.GetComponent<AnnotationToolController>();
            
            annotationToolController.OnNewAnnotationPositionDefining -= OnNewAnnotationPositionDefining;
            annotationToolController.OnNewAnnotationPositionDefining += OnNewAnnotationPositionDefining;
            
            if (m_Controller == null)
            {
                Debug.LogError("AnnotationToolController component is missing.");
                return;
            }

            m_CollaborationUIHelper.AttachmentGridViewColumnCount = 2;
            m_CollaborationUIHelper.InitializeUI(uiDocument, parent, annotationToolController.CurrentFilterType);
            m_CollaborationUIHelper.ResetUIToDefault();

            CollaborationController.QueryThreads?.Invoke(m_CollaborationUIHelper.SelectedAsset.Value,
                m_CollaborationUIHelper.TokenSource,
                annotationToolController.CurrentFilterType, OnAnnotationLoaded);
        }

        private void OnAnnotationLoaded(IReadOnlyList<IAnnotation> replies)
        {
            m_CollaborationUIHelper?.OnAnnotationLoaded(replies);
        }

        private void OnNewAnnotationPositionDefining(Vector3? position, bool isFinalPosition, int? instanceId)
        {
            if (position.HasValue)
            {
                if (currentSceneMarkupInstance == null)
                {
                    currentSceneMarkupInstance = Instantiate(m_sceneMarkupPrefab, TransformController.Instance.transform);
#if VR_MODE
                    _markupInstanceId = instanceId;
#endif
                }
                
#if VR_MODE
                if (_markupInstanceId != instanceId) return;
#endif
                
                currentSceneMarkupInstance.transform.position = position.Value + Vector3.up * 0.01f;
                
                if (isFinalPosition)
                {
                    annotationToolController.UnsubscribeInteraction();
                    m_CollaborationUIHelper.FinishedPlacingSceneMarkup(currentSceneMarkupInstance);
                }
            }
            else
            {
#if VR_MODE
                if (_markupInstanceId != instanceId) return;
#endif
                
                RemoveUnfinishedEntry();
            }
        }

        public void CreateSpatialMarkup(IAnnotation annotation, ISpatial3DAttachment spatial3DAttachment, GameObject sceneMarkupInstance)
        {
            if (m_AnnotationToSpatialController != null && m_AnnotationToSpatialController.ContainsKey(annotation.AnnotationId))
                return;
            
            GameObject newSceneMarkupInstance = sceneMarkupInstance;
            if (newSceneMarkupInstance == null)
            {
                newSceneMarkupInstance = Instantiate(m_sceneMarkupPrefab, TransformController.Instance.transform);
                newSceneMarkupInstance.transform.localPosition = new Vector3(spatial3DAttachment.Position.X, spatial3DAttachment.Position.Y, spatial3DAttachment.Position.Z);
            }
            
            if(newSceneMarkupInstance.TryGetComponent(out SpatialMarkupController spatialController))
            {
                m_AnnotationToSpatialController ??= new Dictionary<AnnotationId, SpatialMarkupController>();
                m_AnnotationToSpatialController.TryAdd(annotation.AnnotationId, spatialController);
                spatialController.Initialize(annotation, spatial3DAttachment, m_CollaborationUIHelper);
                spatialController.Select(sceneMarkupInstance != null);
            }

            if (sceneMarkupInstance != null && sceneMarkupInstance == currentSceneMarkupInstance)
            {
                currentSceneMarkupInstance = null;
            }
        }

        public void SelectMarkUp(IAnnotation annotation)
        {
            DeselectAllMarkUp();
            if(m_AnnotationToSpatialController == null || !m_AnnotationToSpatialController.TryGetValue(annotation.AnnotationId, out var spatialController))
                return;
            spatialController.Select(true);
        }

        public void DeleteMarkUp(IAnnotation annotation)
        {
            if(m_AnnotationToSpatialController == null)
                return;
            if (m_AnnotationToSpatialController.TryGetValue(annotation.AnnotationId, out var spatialController))
            {
                Destroy(spatialController.gameObject);
                m_AnnotationToSpatialController.Remove(annotation.AnnotationId);
            }
        }

        public void DeselectAllMarkUp()
        {
            if(m_AnnotationToSpatialController == null)
                return;
            foreach (var controller in m_AnnotationToSpatialController.Values)
            {
                controller.Select(false);
            }
        }

        public override void UninitializeUI()
        {
            m_CollaborationUIHelper.UninitializeUI();
            annotationToolController.OnNewAnnotationPositionDefining -= OnNewAnnotationPositionDefining;
            RemoveUnfinishedEntry();
        }

        public void RemoveUnfinishedEntry()
        {
            if (currentSceneMarkupInstance != null)
            {
                Destroy(currentSceneMarkupInstance);
                currentSceneMarkupInstance = null;
            }
        }
    }
}
