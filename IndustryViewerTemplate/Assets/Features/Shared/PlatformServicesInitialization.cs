using UnityEngine;
using System.Threading.Tasks;

namespace Unity.Industry.Viewer.Shared
{
    [DefaultExecutionOrder(int.MinValue)]
    public class PlatformServicesInitialization : MonoBehaviour
    {
        [Tooltip("Scriptable Object that contains a key and secret to authenticate as a service account.")]
        [SerializeField] private ServiceAccountCredentials serviceAccountCredentials;
        
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void StartServices()
        {
            PlatformServices.Create(serviceAccountCredentials);
            _ = Initialize();
            return;

            async Task Initialize()
            {
                await PlatformServices.InitializeAsync();
            }
        }
    }
}
