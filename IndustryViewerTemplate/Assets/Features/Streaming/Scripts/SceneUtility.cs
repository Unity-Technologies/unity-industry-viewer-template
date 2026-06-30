using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Streaming
{
    public static class SceneUtility
    {
        public static bool IsMainSceneActive
        {
            get
            {
                var mainSceneController = Object.FindFirstObjectByType<MainSceneController>();
                return mainSceneController != null && mainSceneController.gameObject.scene == SceneManager.GetActiveScene();
            }
        }

        public static string GetStreamingSceneName()
        {
            var mainSceneController = Object.FindFirstObjectByType<MainSceneController>();
            return mainSceneController != null ? mainSceneController.StreamingSceneName : string.Empty;
        }
    }
}
