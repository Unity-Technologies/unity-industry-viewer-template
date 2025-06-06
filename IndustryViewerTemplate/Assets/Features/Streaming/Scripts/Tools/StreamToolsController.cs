using System;
using System.Linq;
using UnityEngine;
using Unity.Industry.Viewer.Assets;
using UnityEngine.SceneManagement;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public class StreamToolsController : MonoBehaviour
    {
        public static Action<StreamingToolAsset> ToolSelected;
        public static Action<StreamingToolAsset[]> ToolInitializing;
        public static Action<StreamingToolAsset, bool> ToolActiveChanged;
        public static Action DisableAllTools;
        
        private (StreamingToolAsset toolAsset, GameObject toolInstance) m_currentActiveTool;
        
        [SerializeField]
        private StreamingToolAsset[] streamingToolAssets;
        
        private void Start()
        {
            ToolSelected += OnToolSelected;
            DisableAllTools += OnDisableAllTools;
            ToolInitializing?.Invoke(streamingToolAssets);
            StreamSceneController.ExitSceneConfirmed += OnExitSceneConfirmed;
            AssetsController.AssetSelected += OnAssetSelected;
            foreach (var tool in streamingToolAssets)
            {
                if (tool.toolPrefab.TryGetComponent(out StreamToolSubmenuController submenuController))
                {
                    foreach (var submenuControllerSubmenuToolAsset in submenuController.SubmenuToolAssets)
                    {
                        if (submenuControllerSubmenuToolAsset.sceneListener == null) continue;
                        var listener = Instantiate(submenuControllerSubmenuToolAsset.sceneListener);
                        SceneManager.MoveGameObjectToScene(listener, gameObject.scene);
                    } 
                }
                if(tool.sceneListener == null) continue;
                var otherListener = Instantiate(tool.sceneListener);
                SceneManager.MoveGameObjectToScene(otherListener, gameObject.scene);
            }
        }

        private void OnDestroy()
        {
            ToolSelected -= OnToolSelected;
            DisableAllTools -= OnDisableAllTools;
            AssetsController.AssetSelected -= OnAssetSelected;
            StreamSceneController.ExitSceneConfirmed -= OnExitSceneConfirmed;
        }

        private void OnDisableAllTools()
        {
            foreach (var tool in streamingToolAssets)
            {
                ToolActiveChanged?.Invoke(tool, false);
            }
            
            if (m_currentActiveTool == default) return;
            
            if(m_currentActiveTool.toolInstance.TryGetComponent(out StreamToolControllerBase controller))
            {
                controller.OnToolClosed();
            }
            
            Destroy(m_currentActiveTool.toolInstance);
            m_currentActiveTool = default;
        }

        // Turn off all tools when asset is updated
        private void OnAssetSelected(AssetInfo assetInfo)
        {
            StreamToolsUIControllerBase.UpdateToolPanel?.Invoke(m_currentActiveTool.toolAsset, null, false);
            OnDisableAllTools();
        }

        private void OnToolSelected(StreamingToolAsset toolAsset)
        {
            var pass = streamingToolAssets.Any(tool => tool == toolAsset);

            if(!pass) return;

            if (m_currentActiveTool != default)
            {
                if(m_currentActiveTool.toolInstance.TryGetComponent(out StreamToolControllerBase controller))
                {
                    controller.OnToolClosed();
                }
                Destroy(m_currentActiveTool.toolInstance);
                var currentTool = m_currentActiveTool.toolAsset;
                ToolActiveChanged?.Invoke(m_currentActiveTool.toolAsset, false);
                StreamToolsUIControllerBase.UpdateToolPanel?.Invoke(m_currentActiveTool.toolAsset, null, false);
                if (currentTool == toolAsset)
                {
                    m_currentActiveTool = default;
                    return;
                }
            }
            m_currentActiveTool.toolInstance = Instantiate(toolAsset.toolPrefab);
            SceneManager.MoveGameObjectToScene(m_currentActiveTool.toolInstance, gameObject.scene);
            m_currentActiveTool.toolAsset = toolAsset;
            ToolActiveChanged?.Invoke(toolAsset, true);
            StreamToolsUIControllerBase.UpdateToolPanel?.Invoke(toolAsset, m_currentActiveTool.toolInstance, true);
        }
        
        private void OnExitSceneConfirmed()
        {
            if (m_currentActiveTool == default) return;
            if(m_currentActiveTool.toolInstance.TryGetComponent(out StreamToolControllerBase controller))
            {
                controller.OnToolClosed();
            }
            Destroy(m_currentActiveTool.toolInstance);
            ToolActiveChanged?.Invoke(m_currentActiveTool.toolAsset, false);
            StreamToolsUIControllerBase.UpdateToolPanel?.Invoke(m_currentActiveTool.toolAsset, null, false);
        }
    }
}
