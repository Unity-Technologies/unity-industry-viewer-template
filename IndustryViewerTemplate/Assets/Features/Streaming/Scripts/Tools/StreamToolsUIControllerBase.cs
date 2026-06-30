using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.AppUI.UI;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamToolsUIControllerBase : MonoBehaviour
    {
        public class StreamToolData
        {
            private StreamingToolAsset toolAsset { get; set; }

            public StreamToolData(StreamingToolAsset toolAsset)
            {
                this.toolAsset = toolAsset;
            }

            public void OnButtonPress()
            {
                StreamToolsController.ToolSelected?.Invoke(toolAsset);
            }
        }
        
        public static Action<StreamingToolAsset, GameObject, bool> UpdateToolPanel;
        
        public Dictionary<StreamingToolAsset, IPressable> ToolButtons => m_toolButtons;
        
        protected Dictionary<StreamingToolAsset, IPressable> m_toolButtons;

        // The UIDocument that hosts the tool panel. Derived classes point this at their own
        // document (the shared assets document for desktop/tablet, the XR panel document for VR)
        // so the shared OnUpdateToolPanel logic targets the correct UI.
        protected virtual UIDocument ToolPanelUIDocument => null;

        protected void CloseToolPanel()
        {
            StreamToolsController.DisableAllTools?.Invoke(false);
        }

        protected void OnUpdateToolPanel(StreamingToolAsset toolAsset, GameObject controller, bool active)
        {
            if (active)
            {
                //Add tool to panel
                if (controller.TryGetComponent(out StreamToolUIBase toolUI))
                {
                    if (controller.TryGetComponent(out StreamToolControllerBase toolController))
                    {
                        toolController.OnToolOpened();
                    }

                    VisualElement toolPanel = null;
                    if (toolUI.ToolUIAsset != null)
                    {
                        toolPanel = toolUI.ToolUIAsset.Instantiate().Children().First();
                        toolPanel.userData = controller;
                    }

                    toolUI.InitializeUI(ToolPanelUIDocument, toolPanel, controller);
                    if (toolPanel != null)
                    {
                        ToolPanelUIController.OpenToolPanel?.Invoke(toolAsset.ToolName, toolPanel, toolAsset.resizablePanel);
                    }
                }
            }
            else
            {
                //Remove tool from panel
                ToolPanelUIController.CloseToolPanel?.Invoke();
            }
        }
    }
}
