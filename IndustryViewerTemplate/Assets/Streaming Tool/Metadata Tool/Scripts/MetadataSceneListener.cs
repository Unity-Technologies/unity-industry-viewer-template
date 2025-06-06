using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming.Metadata
{
    public class MetadataSceneListener : MonoBehaviour
    {
        public ModelStreamId SelectedModelID => m_selectedModelID;
        public InstanceId SelectedInstanceID => m_selectedInstanceID;
        
        private ModelStreamId m_selectedModelID;
        private InstanceId m_selectedInstanceID;
        
        private void Start()
        {
            MetadataToolController.InstanceSelected += OnInstanceSelected;
        }
        
        private void OnDestroy()
        {
            MetadataToolController.InstanceSelected -= OnInstanceSelected;
        }

        private void OnInstanceSelected(ModelStreamId modelID, InstanceId instanceID)
        {
            m_selectedModelID = modelID;
            m_selectedInstanceID = instanceID;
        }
    }
}
