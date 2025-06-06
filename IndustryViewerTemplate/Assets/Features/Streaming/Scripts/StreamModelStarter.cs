using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public class StreamModelStarter : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            StreamingModelController.AddStreamModel?.Invoke(StreamingModelController.StreamingAsset.Value);
        }
    }
}
