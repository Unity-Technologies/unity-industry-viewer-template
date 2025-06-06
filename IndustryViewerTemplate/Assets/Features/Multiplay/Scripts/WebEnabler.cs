using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Unity.Industry.Viewer.Multiplay
{
    public class WebEnabler : MonoBehaviour
    {
        [SerializeField]
        UnityTransport transport;
        
        void Awake()
        {
#if ENABLE_MULTIPLAY && UNITY_WEBGL
            //When using WebGL, we need to use WebSockets
            transport.UseWebSockets = true;
#endif
        }
    }
}
