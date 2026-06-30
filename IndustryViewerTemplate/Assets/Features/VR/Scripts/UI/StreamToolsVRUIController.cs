using System;
using Unity.Industry.Viewer.Streaming;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Shared;

namespace Unity.Industry.Viewer.VR
{
    public class StreamToolsVRUIController : StreamToolsUIControllerBase
    {
        private const string k_MainToolIconClassName = "MainToolIcon";

        [SerializeField]
        private XRControllerMenu m_XRControllerMenu;

        [SerializeField]
        UIDocument m_XRPanelUIDocument;

        protected override UIDocument ToolPanelUIDocument => m_XRPanelUIDocument;

        private void Start()
        {
            m_XRControllerMenu ??= new XRControllerMenu();
            m_XRControllerMenu.Initialize();
            StreamToolsController.ToolInitializing += OnToolInitializing;
            StreamToolsController.ToolActiveChanged += OnToolActiveChanged;
            ToolPanelUIController.CloseToolPanel += CloseToolPanel;
            UpdateToolPanel += OnUpdateToolPanel;
        }

        private void OnDestroy()
        {
            StreamToolsController.ToolInitializing -= OnToolInitializing;
            StreamToolsController.ToolActiveChanged -= OnToolActiveChanged;
            ToolPanelUIController.CloseToolPanel -= CloseToolPanel;
            UpdateToolPanel -= OnUpdateToolPanel;
            if (m_toolButtons != null)
            {
                foreach (var toolButton in m_toolButtons.Values)
                {
                    XRRoundButton xrRoundButton = toolButton as XRRoundButton;
                    xrRoundButton?.RemoveFromHierarchy();
                }
                m_toolButtons.Clear();
            }
        }

        private void OnToolActiveChanged(StreamingToolAsset toolAsset, bool active)
        {
            if(m_toolButtons == null || !m_toolButtons.TryGetValue(toolAsset, out var button)) return;
            var xrButton = button as XRRoundButton;
            xrButton.primary = active;
        }

        private void OnToolInitializing(StreamingToolAsset[] tools)
        {
            if (tools == null || tools.Length == 0)
            {
                return;
            }
            for (var i = 0; i < tools.Length; i++)
            {
                var newButton = new XRRoundButton
                {
                    primary = false
                };
                if (i != 0)
                {
                    newButton.TopPadding = 20f;
                }
                if (!string.IsNullOrEmpty(k_MainToolIconClassName))
                {
                    newButton.AddToClassList(k_MainToolIconClassName);
                }

                newButton.IconTexture = tools[i].toolIcon;

                var newButtonData = new StreamToolData(tools[i]);
                newButton.userData = newButtonData;
                newButton.tooltip = tools[i].ToolName.GetTitleLocalizedStringForAppUI();

                newButton.clicked += newButtonData.OnButtonPress;
                m_toolButtons ??= new Dictionary<StreamingToolAsset, IPressable>();
                m_toolButtons.Add(tools[i], newButton);
                m_XRControllerMenu.Add(newButton);
            }
        }
    }
}
