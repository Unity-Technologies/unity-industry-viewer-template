using System;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Identity;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamSceneController : MonoBehaviour
    {
        public static Action ExitSceneConfirmed;

        private void Start()
        {
            ExitSceneConfirmed += OnExitSceneConfirmed;
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
        }

        private void OnDestroy()
        {
            ExitSceneConfirmed -= OnExitSceneConfirmed;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            if (state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                OnExitSceneConfirmed();
            }
        }

        private void OnExitSceneConfirmed()
        {
            var originalScene = SharedUIManager.Instance.AssetsUIDocument.gameObject.scene;
            SceneManager.SetActiveScene(originalScene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != originalScene)
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
        }
    }
}
