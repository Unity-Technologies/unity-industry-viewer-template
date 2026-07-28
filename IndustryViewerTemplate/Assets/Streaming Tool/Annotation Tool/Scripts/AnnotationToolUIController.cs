using System;
using Unity.Cloud.Collaboration;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Collaboration;
using System.Collections.Generic;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Identity;

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
            // Re-anchor markers whose model streams in / is layout-restored / syncs from another client
            // after the marker was created, so cross-session and multiplayer anchoring hold regardless of
            // load order.
            TransformController.ModelAdded += OnModelAddedReanchor;
            TransformController.ModelRemoved += OnModelRemovedReanchor;
        }

        private void OnDestroy()
        {
            m_CollaborationUIHelper.AnnotationHasBeenUpdated -= OnAnnotationHasBeenUpdated;
            TransformController.ModelAdded -= OnModelAddedReanchor;
            TransformController.ModelRemoved -= OnModelRemovedReanchor;
            m_CollaborationUIHelper.TokenSource?.Cancel();
            DeselectAllMarkUp();
            if (m_PanelDocument != null)
            {
                m_PanelDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_StyleSheet);
            }
            if (m_AnnotationToSpatialController != null)
            {
                foreach (var controller in m_AnnotationToSpatialController.Values)
                {
                    // A marker may be parented under a model that was already destroyed during teardown.
                    if (controller != null) Destroy(controller.gameObject);
                }
            }
            m_AnnotationToSpatialController?.Clear();
            m_AnnotationToSpatialController = null;
            RemoveUnfinishedEntry();
            UIUtility.RefreshEventSystem();
        }

        private void OnAnnotationHasBeenUpdated(IAnnotation newAnnotation)
        {
            if(m_AnnotationToSpatialController == null || !m_AnnotationToSpatialController.TryGetValue(newAnnotation.AnnotationId, out var spatialController))
                return;
            spatialController.UpdateAnnotation(newAnnotation);
        }

        // When a model becomes available (streamed in, layout-restored, or synced from another client),
        // re-anchor any already-created markers that belong to it but fell back to the root because the
        // model wasn't present when the marker was created.
        private void OnModelAddedReanchor(GameObject modelObject, ITransformValuesAccessor _)
        {
            if (m_AnnotationToSpatialController == null || modelObject == null) return;
            modelObject.TryGetComponent(out StreamingModel addedModel);
            foreach (var spatialController in m_AnnotationToSpatialController.Values)
            {
                if (spatialController == null) continue;
                var local = spatialController.Attachment?.Local;
                if (local == null) continue;
                // Match by AnchorId (falling back to gameObject.name for legacy annotations), mirroring
                // StreamingModel.FindModelTransformByAnchorId.
                bool matches = (addedModel != null && local.ParentId == addedModel.AnchorId) || local.ParentId == modelObject.name;
                if (!matches) continue;
                var markerTransform = spatialController.transform;
                if (markerTransform.parent == modelObject.transform) continue;
                markerTransform.SetParent(modelObject.transform, false);
                markerTransform.localPosition = new Vector3(local.Position.X, local.Position.Y, local.Position.Z);
            }
        }

        // When a model is removed, re-parent its markers to the root so they survive the model's
        // destruction (matching the root-relative fallback shown while the model is absent); they
        // re-anchor automatically via OnModelAddedReanchor if the model is added back.
        private void OnModelRemovedReanchor(StreamingModel streamingModel)
        {
            if (m_AnnotationToSpatialController == null || streamingModel == null || TransformController.Instance == null) return;
            var root = TransformController.Instance.transform;
            foreach (var spatialController in m_AnnotationToSpatialController.Values)
            {
                if (spatialController == null) continue;
                var markerTransform = spatialController.transform;
                if (markerTransform.parent != streamingModel.transform) continue;
                markerTransform.SetParent(root, true);
            }
        }

        public override void InitializeUI(UIDocument uiDocument, VisualElement parent, GameObject controller)
        {
            m_PanelDocument = uiDocument;
            
            m_PanelDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);
            
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

        private void OnNewAnnotationPositionDefining(Vector3? position, bool isFinalPosition, int? instanceId, Transform hitModel)
        {
            if (position.HasValue)
            {
                if (currentSceneMarkupInstance == null)
                {
                    // Anchor the marker to the specific model it was placed on (falls back to the root
                    // when nothing was hit) so it follows that model when it's moved in a layout.
                    Transform markupParent = hitModel != null ? hitModel : TransformController.Instance.transform;
                    currentSceneMarkupInstance = Instantiate(m_sceneMarkupPrefab, markupParent);
#if VR_MODE
                    _markupInstanceId = instanceId;
#endif
                }
                
#if VR_MODE
                if (_markupInstanceId != instanceId) return;
#endif

                // hitModel is only resolved on the final placement; re-anchor the (possibly root-parented)
                // preview marker to the model actually hit so it follows that model when moved.
                if (hitModel != null && currentSceneMarkupInstance.transform.parent != hitModel)
                {
                    currentSceneMarkupInstance.transform.SetParent(hitModel, true);
                }

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
                // If the annotation is anchored to a specific model (multi-model layout), parent the
                // marker under that model so it follows the model when moved; otherwise use the root.
                var local = spatial3DAttachment.Local;
                Transform modelChild = local != null ? StreamingModel.FindModelTransformByAnchorId(local.ParentId) : null;
                if (modelChild != null)
                {
                    newSceneMarkupInstance = Instantiate(m_sceneMarkupPrefab, modelChild);
                    newSceneMarkupInstance.transform.localPosition = new Vector3(local.Position.X, local.Position.Y, local.Position.Z);
                }
                else
                {
                    newSceneMarkupInstance = Instantiate(m_sceneMarkupPrefab, TransformController.Instance.transform);
                    newSceneMarkupInstance.transform.localPosition = new Vector3(spatial3DAttachment.Position.X, spatial3DAttachment.Position.Y, spatial3DAttachment.Position.Z);
                }
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
            // annotationToolController is null when InitializeUI returned early (offline/guest/not
            // logged in), so guard before unsubscribing to avoid an NRE on teardown.
            if (annotationToolController != null)
            {
                annotationToolController.OnNewAnnotationPositionDefining -= OnNewAnnotationPositionDefining;
            }
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
