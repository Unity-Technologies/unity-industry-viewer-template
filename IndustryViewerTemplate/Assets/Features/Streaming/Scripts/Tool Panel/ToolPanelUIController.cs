using System;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine.UIElements;
using System.Collections;

namespace Unity.Industry.Viewer.Streaming
{
    public class ToolPanelUIController : MonoBehaviour
    {
        public static Action<LocalizedString, VisualElement, bool> OpenToolPanel;
        public static Action CloseToolPanel;
        public static bool IsOpened;
        
        protected const string k_ToolPanelName = "ToolPanel";
        protected const string k_ToolTitleName = "ToolTitle";
        protected const string k_ToolCloseButtonName = "ToolCloseButton";
        protected const string k_ToolContentName = "Content";
        
        protected VisualElement m_ToolPanelRoot;
        protected IconButton m_CloseToolPanelButton;
        protected Text m_ToolPanelTitle;
        protected VisualElement m_ToolPanelContent;
        protected VisualElement m_ContentPanel;
        
        protected VisualElement m_ResizeHandle;
        private bool m_Resizing = false;
        private float m_StartWidth;
        private Vector2 m_StartPointer;
        private float m_OriginalWidth;
        private bool m_InitializedHandle = false;
        
        [SerializeField]
        protected UIDocument m_UIDocument;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            IsOpened = false;
            OpenToolPanel += OnOpenToolPanel;
            CloseToolPanel += OnCloseToolPanel;
            InitializeUI();
        }

        protected virtual void OnDestroy()
        {
            IsOpened = false;
            OpenToolPanel -= OnOpenToolPanel;
            CloseToolPanel -= OnCloseToolPanel;
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_CloseToolPanelButton.clickable.clicked -= OnCloseToolPanelButtonClicked;
            
            m_ResizeHandle?.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            m_ResizeHandle?.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            m_ResizeHandle?.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected virtual void InitializeUI()
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

        protected async void AddContentToPanel(LocalizedString title, VisualElement content)
        {
            if (m_ToolPanelContent != null)
            {
                m_ToolPanelContent.RemoveFromHierarchy();
            }
            
            m_ToolPanelRoot.style.display = DisplayStyle.Flex;
            m_ToolPanelTitle.text = await title.GetTitleLocalizedStringForAppUIAsync();
            m_ToolPanelContent = content;
            m_ContentPanel.Add(content);
        }
        
        protected virtual void OnOpenToolPanel(LocalizedString title, VisualElement content, bool resizable)
        {
            IsOpened = true;
            AddContentToPanel(title, content);
            SetupResizeHandle(resizable);
        }

        // Wires (once) and shows/hides the drag-to-resize handle. Shared by the desktop
        // panel and the VR world-space panel (XRToolPanel) so both behave identically.
        protected void SetupResizeHandle(bool resizable)
        {
            m_ResizeHandle = m_ToolPanelRoot.Q<VisualElement>("ResizeHandle");
            if (m_ResizeHandle == null) return;
            if (resizable)
            {
                if (!m_InitializedHandle)
                {
                    m_InitializedHandle = true;
                    m_ResizeHandle.RegisterCallback<PointerDownEvent>(OnPointerDown);
                    m_ResizeHandle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                    m_ResizeHandle.RegisterCallback<PointerUpEvent>(OnPointerUp);
                }
                m_ResizeHandle.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_ResizeHandle.style.display = DisplayStyle.None;
            }

            StartCoroutine(WaitForUIRefresh());
            return;

            IEnumerator WaitForUIRefresh()
            {
                yield return new WaitForEndOfFrame();
                m_OriginalWidth = m_ToolPanelRoot.resolvedStyle.width;
            }
        }
        
        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_Resizing) return;
            float delta = m_StartPointer.x - evt.position.x; // Invert direction
            float target = Mathf.Clamp(m_StartWidth + delta, m_OriginalWidth, m_OriginalWidth * 2.5f);
            ApplyResizeWidth(target);
        }

        // Applies the dragged width. Desktop sets the panel element's width directly; the VR
        // world-space panel overrides this to grow the fixed-size UIDocument surface instead
        // (setting the element width there would just overflow and clip the fixed quad).
        protected virtual void ApplyResizeWidth(float width)
        {
            m_ToolPanelRoot.style.width = width;
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            m_Resizing = false;
            m_ResizeHandle.ReleasePointer(evt.pointerId); // Release pointer
        }
        
        void OnPointerDown(PointerDownEvent evt)
        {
            m_Resizing = true;
            if (m_OriginalWidth == 0)
            {
                m_OriginalWidth = m_StartWidth;
            }
            m_StartWidth = m_ToolPanelRoot.resolvedStyle.width;
            m_StartPointer = evt.position;
            m_ResizeHandle.CapturePointer(evt.pointerId); // Capture pointer
            evt.StopPropagation();
        }
        
        private void OnCloseToolPanel()
        {
            m_ToolPanelRoot.style.display = DisplayStyle.None;
            ContentReset();
        }

        protected virtual void ContentReset()
        {
            IsOpened = false;
            if (m_OriginalWidth != 0)
            {
                m_ToolPanelRoot.style.width = m_OriginalWidth;
            }
            m_ToolPanelContent?.RemoveFromHierarchy();
            m_ToolPanelContent = null;
            if(m_ResizeHandle != null)
            {
                m_ResizeHandle.style.display = DisplayStyle.None;
            }
        }
        
        protected virtual void OnCloseToolPanelButtonClicked()
        {
            ContentReset();
            CloseToolPanel?.Invoke();
            StreamToolsController.DisableAllTools?.Invoke(false);
        }
    }
}
