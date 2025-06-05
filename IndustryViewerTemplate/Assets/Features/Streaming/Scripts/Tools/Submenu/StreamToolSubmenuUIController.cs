using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(-100)]
    public class StreamToolSubmenuUIController : StreamToolsUIController
    {
        private void Start()
        {
            InitializeUI();
            StreamToolSubmenuController.InitializeTools += OnInitializeTools;
        }

        private void OnDestroy()
        {
            m_SubToolScrollList.Clear();
            m_SubToolScrollList.style.display = DisplayStyle.None;
            m_toolButtons?.Clear();
            StreamToolSubmenuController.InitializeTools -= OnInitializeTools;
        }

        protected override void InitializeUI()
        {
            m_UIDocument = SharedUIManager.Instance.AssetsUIDocument;
            
            m_SubToolScrollList = m_UIDocument.rootVisualElement.Q<ScrollView>(k_SubToolScrollListName);
            m_SubToolScrollList.style.display = DisplayStyle.None;
        }
        
        private void OnInitializeTools(StreamingToolAsset[] toolAssets)
        {
            DistributeToolsIcons(toolAssets, string.Empty, m_SubToolScrollList, ref m_toolButtons);
        }
    }
}
