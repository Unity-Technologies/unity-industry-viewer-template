using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Assets;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamToolData
    {
        public StreamingToolAsset toolAsset { get; private set; }

        public StreamToolData(StreamingToolAsset toolAsset)
        {
            this.toolAsset = toolAsset;
        }

        public void OnButtonPress()
        {
            StreamToolsController.ToolSelected?.Invoke(toolAsset);
        }
    }
    
    [DefaultExecutionOrder(-100)]
    public class StreamToolsUIController : StreamToolsUIControllerBase
    {
        protected Dictionary<StreamingToolAsset, ActionButton> m_toolButtons;

        private const string k_MainToolIconClassName = "MainToolIcon";
        
        private const string k_ToolScrollListName = "ToolScrollList";
        protected const string k_SubToolScrollListName = "SubToolScrollList";
        
        protected UIDocument m_UIDocument;
        private ScrollView m_ToolScrollList;
        protected ScrollView m_SubToolScrollList;
        
        // Start is called before the first frame update
        void Start()
        {
            StreamToolsController.ToolInitializing += OnToolInitializing;
            StreamToolsController.ToolActiveChanged += OnToolActiveChanged;
            ToolPanelUIController.CloseToolPanel += CloseToolPanel;
            UpdateToolPanel += OnUpdateToolPanel;

            InitializeUI();
        }

        private void OnDestroy()
        {
            StreamToolsController.ToolInitializing -= OnToolInitializing;
            StreamToolsController.ToolActiveChanged -= OnToolActiveChanged;
            ToolPanelUIController.CloseToolPanel -= CloseToolPanel;
            UpdateToolPanel -= OnUpdateToolPanel;
            m_toolButtons?.Clear();
            m_ToolScrollList?.Clear();
            m_SubToolScrollList?.Clear();
        }

        private void CloseToolPanel()
        {
            StreamToolsController.DisableAllTools?.Invoke();
        }

        private void OnUpdateToolPanel(StreamingToolAsset toolAsset, GameObject controller, bool active)
        {
            if (active)
            {
                //Add tool to panel
                if(controller.TryGetComponent(out StreamToolUIBase toolUI))
                {
                    if(controller.TryGetComponent(out StreamToolControllerBase toolController))
                    {
                        toolController.OnToolOpened();
                    }

                    VisualElement toolPanel = null;
                    if (toolUI.ToolUIAsset != null)
                    {
                        toolPanel = toolUI.ToolUIAsset.Instantiate().Children().First();
                        toolPanel.userData = controller;
                        toolPanel.style.flexGrow = 1;
                    }
                    
                    toolUI.InitializeUI(toolPanel, controller);
                    if (toolPanel != null)
                    {
                        ToolPanelUIController.OpenToolPanel?.Invoke(toolAsset.ToolName, toolPanel);
                    }
                }
            }
            else
            {
                //Remove tool from panel
                ToolPanelUIController.CloseToolPanel?.Invoke();
            }
        }

        protected virtual void InitializeUI()
        {
            m_UIDocument = SharedUIManager.Instance.AssetsUIDocument;
            m_ToolScrollList = m_UIDocument.rootVisualElement.Q<ScrollView>(k_ToolScrollListName);
            
            m_SubToolScrollList = m_UIDocument.rootVisualElement.Q<ScrollView>(k_SubToolScrollListName);
            m_SubToolScrollList.style.display = DisplayStyle.None;
        }

        private void OnToolActiveChanged(StreamingToolAsset toolAsset, bool active)
        {
            if(!m_toolButtons.TryGetValue(toolAsset, out var button)) return;
            button.accent = active;
            button.selected = active;
        }

        private void OnToolInitializing(StreamingToolAsset[] tools)
        {
            DistributeToolsIcons(tools, k_MainToolIconClassName, m_ToolScrollList, ref m_toolButtons);
        }

        protected void DistributeToolsIcons(StreamingToolAsset[] tools, string styleClassName, ScrollView scrollView,
            ref Dictionary<StreamingToolAsset, ActionButton> toolButtonDict)
        {
            if (tools == null || tools.Length == 0)
            {
                scrollView.style.display = DisplayStyle.None;
                return;
            }
            scrollView.style.display = DisplayStyle.Flex;
            scrollView.contentContainer.style.alignSelf = Align.Center;
            foreach (var toolAsset in tools)
            {
                var newButton = new ActionButton
                {
                    quiet = true
                };
                if (!string.IsNullOrEmpty(styleClassName))
                {
                    newButton.AddToClassList(styleClassName);
                }
                
                Icon icon = newButton.Q<Icon>("appui-actionbutton__icon");
                icon.image = toolAsset.toolIcon;
                var newButtonData = new StreamToolData(toolAsset);
                newButton.userData = newButtonData;
                newButton.ClearBinding("tooltip");
                newButton.SetBinding("tooltip", toolAsset.ToolName);

                newButton.clicked += newButtonData.OnButtonPress;
                toolButtonDict ??= new Dictionary<StreamingToolAsset, ActionButton>();
                toolButtonDict.Add(toolAsset, newButton);
                scrollView.Add(newButton);
            }
        }
    }
}
