using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    /// <summary>
    /// Provides resouce limiter values for a <see cref="StreamingModelController"/>.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(StreamingResourceLimiterSettings), menuName = "ScriptableObjects/" + nameof(StreamingResourceLimiterSettings))]
    public class StreamingResourceLimiterSettings : ScriptableObject
    {
        [Header("Maximum Triangle Count")]
        [Tooltip("Sets the maximum triangle count")]
        public int maxTriangleCount = 10000000;
    }
}
