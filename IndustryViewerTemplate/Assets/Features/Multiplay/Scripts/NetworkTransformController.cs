using Unity.Industry.Viewer.Streaming;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Unity.Industry.Viewer.Multiplay
{
    // This script manages the networked transform of player objects in a multiplayer session using Unity Netcode.
    // It handles reparenting player objects to the transform of this controller upon client connection.
    // The script listens for session owner promotion events to change ownership of the network object.
    // It integrates with the MultiplayController to manage player objects and session-related events.
    public class NetworkTransformController : NetworkBehaviour
    {
        MultiplayController m_MultiplayController;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public override void OnNetworkSpawn()
        {
            NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
        }

        private void Start()
        {
            m_MultiplayController = FindAnyObjectByType<MultiplayController>();
            
            MultiplayController.OnClientConnected += OnClientConnected;

            if (m_MultiplayController == null) return;
            foreach (var playerObject in m_MultiplayController.PlayerObjects.Values)
            {
                if(playerObject.TryGetComponent(out NetworkPlayerController playerController))
                {
                    playerController.Reparent(transform);
                }
            }
        }
        
        private void OnDestroy()
        {
            MultiplayController.OnClientConnected -= OnClientConnected;
        }

        public override void OnNetworkDespawn()
        {
            NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                GameObject newTransformController = new GameObject("Stage Transform");
                newTransformController.transform.SetPositionAndRotation(transform.position, transform.rotation);
                newTransformController.transform.localScale = transform.localScale;
                newTransformController.AddComponent<TransformController>();
            }
        }
        
        private void OnClientConnected(ulong newClientTd, GameObject playerObject)
        {
            if(playerObject.TryGetComponent(out NetworkPlayerController playerController))
            {
                playerController.Reparent(transform);
            }
        }
        
        private void OnSessionOwnerPromoted(ulong sessionownerpromoted)
        {
            if (NetworkManager.LocalClientId == sessionownerpromoted)
            {
                NetworkObject.ChangeOwnership(sessionownerpromoted);
            }
        }
    }
}
