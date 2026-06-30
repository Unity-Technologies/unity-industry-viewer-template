using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Industry.Viewer.Streaming
{
    [Serializable]
    public class ResourceLimitEntry
    {
        public RuntimePlatform Platform;
        public int MaxResources;
        [Tooltip("Check this for VR/XR builds — standalone headsets (e.g. Quest) that aren't tethered to a PC.")]
        [FormerlySerializedAs("IsNonTethered")]
        public bool IsVRBuild;
    }
    
    [CreateAssetMenu(fileName = "Resource Limit Asset", menuName = "IVT/Streaming/Resource Limit Asset")]
    public class ResourceLimitAsset : ScriptableObject
    {
        public ResourceLimitEntry[] ResourceLimits => _resourceLimits;
        [SerializeField]
        private ResourceLimitEntry[] _resourceLimits;
    }
}
