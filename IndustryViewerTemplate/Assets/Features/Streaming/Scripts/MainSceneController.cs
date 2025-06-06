using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Streaming
{
    public class MainSceneController : MonoBehaviour
    {
        public static Action StartStreaming;
        
        [SerializeField]
        private string streamingSceneName;

        [SerializeField]
        private bool keepMainSceneCameraActive = false;
        
        [SerializeField] private Camera mainSceneCamera;

        private void Start()
        {
            StartStreaming += OnStartStreaming;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
        
        private void OnDestroy()
        {
            StartStreaming -= OnStartStreaming;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene fromScene, Scene toScene)
        {
            if (keepMainSceneCameraActive) return;
            mainSceneCamera.gameObject.SetActive(string.Equals(toScene.name, gameObject.scene.name));
        }

        private void OnStartStreaming()
        {
            SceneManager.LoadScene(streamingSceneName, LoadSceneMode.Additive);
        }

        private void OnSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
        {
            if (!string.Equals(loadedScene.name, streamingSceneName))
            {
                return;
            }

            if (!keepMainSceneCameraActive)
            {
                mainSceneCamera.gameObject.SetActive(false);
            }
            
            SceneManager.SetActiveScene(loadedScene);
        }
    }
}
