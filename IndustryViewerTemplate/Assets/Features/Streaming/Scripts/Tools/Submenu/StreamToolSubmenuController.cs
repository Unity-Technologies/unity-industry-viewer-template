using System;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public class StreamToolSubmenuController : MonoBehaviour
    {
        public static Action<StreamingToolAsset[]> InitializeTools;
        
        public StreamingToolAsset[] SubmenuToolAssets => submenuToolAssets;
        
        [SerializeField]
        private StreamingToolAsset[] submenuToolAssets;

        private void Start()
        {
            InitializeTools?.Invoke(submenuToolAssets);
        }
    }
}
