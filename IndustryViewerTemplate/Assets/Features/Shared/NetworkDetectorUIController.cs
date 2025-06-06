using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Shared
{
    public class NetworkDetectorUIController : MonoBehaviour
    {
        private const string k_OfflineModeVE = "OfflineModeVE";
        
        [SerializeField]
        UIDocument m_UIDocument;

        VisualElement m_offlineModeVE;
        
        private void Awake()
        {
            m_offlineModeVE = m_UIDocument.rootVisualElement.Q<VisualElement>(k_OfflineModeVE);
            //m_offlineModeVE.style.display = NetworkDetector.IsOffline ? DisplayStyle.Flex : DisplayStyle.None;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }
        
        private void OnNetworkStatusChanged(bool connected)
        {
            if(m_offlineModeVE == null) return;
            m_offlineModeVE.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnDestroy()
        {
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
        }
    }
}
