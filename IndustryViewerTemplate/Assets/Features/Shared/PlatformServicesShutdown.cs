using UnityEngine;

namespace Unity.Industry.Viewer.Shared
{
    [DefaultExecutionOrder(int.MaxValue)]
    public class PlatformServicesShutdown : MonoBehaviour
    {
        void OnDestroy()
        {
            PlatformServices.Shutdown();
        }
    }
}
