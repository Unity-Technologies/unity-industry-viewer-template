using System;
using System.Collections.Generic;
using Unity.Cloud.Collaboration;
using Unity.Industry.Viewer.Collaboration;
using UnityEngine;
using Unity.Industry.Viewer.Assets;
using System.Threading.Tasks;
using Unity.Industry.Viewer.Shared;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming.Annotation
{
    public class CollaborationUIHelper: CollaborationUIBase
    {
        private const string k_In3DView = "in-3d-view";
        private const string k_NoMarginClass = "NoMargin";
        private const string k_ForMarginClass = "ForMargin";
        
        [SerializeField]
        AnnotationToolUIController m_AnnotationToolUIController;
        
        [SerializeField]
        AnnotationToolController annotationToolController;

        public override AssetInfo? SelectedAsset => StreamingModelController.StreamingAsset;
        public override GameObject SpatialAttachment => _tempSceneMarkupInstance;

        private GameObject _tempSceneMarkupInstance;

        private void Start()
        {
            RootAnnotationDeleted += OnRootAnnotationDeleted;
        }

        private void OnDestroy()
        {
            ClearToken();
            RootAnnotationDeleted -= OnRootAnnotationDeleted;
        }

        public override void InitializeUI(UIDocument uiDoc, VisualElement contentContainer, CollaborationController.FilterType filterType)
        {
            m_CollaborationController = FindFirstObjectByType<CollaborationController>();
            if(m_CollaborationController == null)
            {
                Debug.LogError("Collaboration Controller not found in the scene.");
                return;
            }
            base.InitializeUI(uiDoc, contentContainer, filterType);
            if (!m_AnnotationContainer.ClassListContains(k_In3DView))
            {
                m_AnnotationContainer.AddToClassList(k_In3DView);
            }
        }

        private void OnRootAnnotationDeleted(IAnnotation annotation)
        {
            m_AnnotationToolUIController.DeleteMarkUp(annotation);
        }

        protected override void OnNewCommentButtonClicked()
        {
            NewCommentUIActive(() =>
            {
                _ = GetTranslationForTextArea();
            });
            annotationToolController.SubscribeInteraction();
            m_AnnotationToolUIController.DeselectAllMarkUp();
            return;

            async Task GetTranslationForTextArea()
            {
                m_TextArea.placeholder = await LocalizedStringAsset.StartThreadPlaceHolderLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
        }

        public override void BackToAllThreadsButtonOnClicked()
        {
            base.BackToAllThreadsButtonOnClicked();
            m_AnnotationToolUIController.DeselectAllMarkUp();
            annotationToolController.UnsubscribeInteraction();
            m_AnnotationToolUIController.RemoveUnfinishedEntry();
            _tempSceneMarkupInstance = null;
        }

        protected override void OnAnnotationCreated(bool success, IAnnotation newAnnotation)
        {
            base.OnAnnotationCreated(success, newAnnotation);
            if (success)
            {
                if (_tempSceneMarkupInstance == null) return;
                CheckAndCreateSpatialMarkup(newAnnotation, _tempSceneMarkupInstance);
                _tempSceneMarkupInstance = null;
            }
            else
            {
                m_AnnotationToolUIController.RemoveUnfinishedEntry();
            }
        }

        public override void OnAnnotationLoaded(IReadOnlyList<IAnnotation> listOfAnnotations)
        {
            m_AnnotationContainer.Clear();
            if (listOfAnnotations == null) return;
            _ = Populate();
            return;
            
            async Task Populate()
            {
                m_UserInfo ??= await PlatformServices.CompositeAuthenticator.GetUserInfoAsync();
                int index = 0;
                foreach (var annotation in listOfAnnotations)
                {
                    var newAnnotation = annotationItemTemplate.Instantiate().Children().First();
                    
                    bool isCreator = annotation.CreatedBy == m_UserInfo.UserId.ToString();
                    AnnotationEntryController entryController =
                        new AnnotationEntryController(m_CollaborationController, this, annotation, newAnnotation, isCreator, ReadingThread);
                    
                    newAnnotation.userData = entryController;
                    
                    m_AnnotationContainer.Add(newAnnotation);

                    if (index < listOfAnnotations.Count - 1)
                    {
                        newAnnotation.style.marginBottom = new Length(k_AnnotationItemSpacing, LengthUnit.Pixel);
                    }
                    
                    //Spatial Markup
                    CheckAndCreateSpatialMarkup(annotation, null);
                    
                    index++;
                }

                var marginEntries = m_AnnotationContainer.Query<VisualElement>(className: k_ForMarginClass).ToList();
                foreach (var entry in marginEntries)
                {
                    entry.AddToClassList(k_NoMarginClass);
                }
            }
        }

        public override void OpenRootThread(IAnnotation annotation)
        {
            base.OpenRootThread(annotation);
            m_AnnotationToolUIController.SelectMarkUp(annotation);
        }

        private void CheckAndCreateSpatialMarkup(IAnnotation annotation, GameObject currentSceneMarkupInstance)
        {
            if (annotation.Attachments.Any(x => x is ISpatial3DAttachment))
            {
                var spatialAttachment = (ISpatial3DAttachment)annotation.Attachments.First(x => x is ISpatial3DAttachment);
                m_AnnotationToolUIController.CreateSpatialMarkup(annotation, spatialAttachment, currentSceneMarkupInstance);
            }
        }

        public void FinishedPlacingSceneMarkup(GameObject currentSceneMarkupInstance)
        {
            m_TextArea.Focus();
            _tempSceneMarkupInstance = currentSceneMarkupInstance;
        }

        public override void DeleteSpatialAttachment(IAnnotation annotation, IAttachment attachment)
        {
            m_AnnotationToolUIController.DeleteMarkUp(annotation);
        }
    }
}
