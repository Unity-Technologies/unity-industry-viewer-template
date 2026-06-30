using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using UnityEngine;
using Unity.AppUI.UI;
using Unity.Cloud.Collaboration;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using Unity.Industry.Viewer.Identity;

namespace Unity.Industry.Viewer.Collaboration
{
    public class CollaborationUIController: CollaborationUIBase
    {
        [SerializeField]
        private UIDocument m_UIDocument;
        
        private const string k_AssetInfoPanelRootName = "AssetInfoContainer";
        private const string k_CommentContainerName = "CommentsContainer";
        private const string k_TabsName = "Tabs";
        
        private Tabs m_Tabs;
       
        private VisualElement m_InfoPanel;

        public override AssetInfo? SelectedAsset => SharedUIManager.SelectedAsset.HasValue?
            SharedUIManager.SelectedAsset: null;

        public override GameObject SpatialAttachment => null;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            CollaborationController.UpdateAnnotation += OnUpdateAnnotation;
            SharedUIManager.AssetSelected += OnAssetSelected;
            m_CollaborationController = FindFirstObjectByType<CollaborationController>();
            m_UIDocument ??= GetComponent<UIDocument>();
            if (m_UIDocument == null)
            {
                Debug.LogError("UIDocument component is missing.");
                return;
            }
            m_InfoPanel = m_UIDocument.rootVisualElement.Q<VisualElement>(k_AssetInfoPanelRootName);
            m_InfoPanel.RegisterCallback<GeometryChangedEvent>(OnInfoPanelGeometryChanged);
            m_Tabs = m_InfoPanel.Q<Tabs>(k_TabsName);
            m_Tabs?.RegisterValueChangedCallback(OnTabsValueChanged);
            m_CommentContainer = m_InfoPanel.Q<VisualElement>(k_CommentContainerName);
            AssetsController.OrganizationsLoaded += CollaborationUIUtility.OnOrganizationsLoaded;
        }

        private void OnDestroy()
        {
            CollaborationController.UpdateAnnotation -= OnUpdateAnnotation;
            SharedUIManager.AssetSelected -= OnAssetSelected;
            m_InfoPanel.UnregisterCallback<GeometryChangedEvent>(OnInfoPanelGeometryChanged);
            m_Tabs?.UnregisterValueChangedCallback(OnTabsValueChanged);
            AssetsController.OrganizationsLoaded -= CollaborationUIUtility.OnOrganizationsLoaded;
            UninitializeUI();
        }

        private void OnInfoPanelGeometryChanged(GeometryChangedEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target == null) return;
            if (target.resolvedStyle.display == DisplayStyle.None)
            {
                ClearUIAndStop();
            }
        }

        private void OnUpdateAnnotation(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation arg1, string arg2, List<Attachment> attachments, Action<IAnnotation> callback)
        {
            LoadingUIPanel.ShowLoadingPanel(null);
        }

        private void OnAssetSelected(AssetInfo obj)
        {
            if (m_Initialized && m_CommentContainer.style.display == DisplayStyle.Flex)
            {
                ClearUIAndStop();
            }
        }

        private void ClearUIAndStop()
        {
            ClearToken();
            m_AnnotationContainer?.Clear();
            m_currentRootAnnotation = null;
        }
        
        private void OnTabsValueChanged(ChangeEvent<int> evt)
        {
            if (evt.newValue != 1)
            {
                if (m_CommentContainer.style.display == DisplayStyle.Flex)
                {
                    // The comment container is now hidden, we can perform layout updates here if needed
                    ClearUIAndStop();
                }
                return;
            }

            if (NetworkDetector.IsOffline || IdentityController.GuestMode)
            {
                m_Initialized = false;
                InsertCollaborationNotAvailable(m_CommentContainer);
                return;
            }
            
            m_GuestModeText?.RemoveFromHierarchy();
            
            if (!m_Initialized)
            {
                m_Initialized = true;
                var commentUI = commentUITemplate.Instantiate().Children().First();
                m_CommentContainer.Add(commentUI);
                InitializeUI(SharedUIManager.Instance.AssetsUIDocument, commentUI, FilterType);
            }

            ResetUIToDefault();
            
            m_currentRootAnnotation = null;
            
            // The comment container is now visible, we can perform layout updates here if needed
            CollaborationController.QueryThreads?.Invoke(SelectedAsset.Value, TokenSource, FilterType, OnAnnotationLoaded);
        }

        public override void UninitializeUI()
        {
            base.UninitializeUI();
            if (m_UIDocument == null) return;
            if (m_UIDocument?.rootVisualElement?.styleSheets == null) return;
            m_UIDocument.rootVisualElement.RemoveStyleSheetIfPresent(annotationStylesheet);
        }

        public override void DeleteSpatialAttachment(IAnnotation annotation, IAttachment attachment)
        {
            //Do nothing as spatial attachment is not shown in the scene
        }
    }
}
