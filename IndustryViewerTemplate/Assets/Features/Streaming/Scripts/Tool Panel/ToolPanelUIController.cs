using System;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Assets;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming
{
    public class ToolPanelUIController : MonoBehaviour
    {
        public static Action<LocalizedString, VisualElement> OpenToolPanel;
        public static Action CloseToolPanel;
        
        private const string k_ToolPanelName = "ToolPanel";
        private const string k_ToolTitleName = "ToolTitle";
        private const string k_ToolCloseButtonName = "ToolCloseButton";
        private const string k_ToolContentName = "Content";
        
        private VisualElement m_ToolPanelRoot;
        private IconButton m_CloseToolPanelButton;
        private Text m_ToolPanelTitle;
        private VisualElement m_ToolPanelContent;
        private VisualElement m_ContentPanel;
        
        [SerializeField]
        UIDocument m_UIDocument;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            OpenToolPanel += OnOpenToolPanel;
            CloseToolPanel += OnCloseToolPanel;
            InitializeUI();
        }

        private void OnDestroy()
        {
            OpenToolPanel -= OnOpenToolPanel;
            CloseToolPanel -= OnCloseToolPanel;
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_CloseToolPanelButton.clickable.clicked -= OnCloseToolPanelButtonClicked;
        }

        void InitializeUI()
        {
            var UIDocument = m_UIDocument == null? SharedUIManager.Instance.AssetsUIDocument : m_UIDocument;
            if (UIDocument == null) return;
            m_ToolPanelRoot = UIDocument.rootVisualElement.Q<VisualElement>(k_ToolPanelName);
            m_ToolPanelRoot.style.display = DisplayStyle.None;
            
            m_CloseToolPanelButton = m_ToolPanelRoot.Q<IconButton>(k_ToolCloseButtonName);
            m_CloseToolPanelButton.clickable.clicked += OnCloseToolPanelButtonClicked;
            m_ToolPanelTitle = m_ToolPanelRoot.Q<Text>(k_ToolTitleName);
            m_ContentPanel = m_ToolPanelRoot.Q<VisualElement>(k_ToolContentName);
        }
        
        private void OnOpenToolPanel(LocalizedString title, VisualElement content)
        {
            m_ToolPanelContent?.RemoveFromHierarchy();
            
            m_ToolPanelRoot.style.display = DisplayStyle.Flex;
            m_ToolPanelTitle.ClearBinding("text");
            m_ToolPanelTitle.SetBinding("text", title);
            m_ToolPanelContent = content;
            m_ContentPanel.Add(content);
        }
        
        private void OnCloseToolPanel()
        {
            m_ToolPanelRoot.style.display = DisplayStyle.None;
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_ToolPanelContent = null;
        }
        
        private void OnCloseToolPanelButtonClicked()
        {
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_ToolPanelContent = null;
            CloseToolPanel?.Invoke();
        }
    }
}
