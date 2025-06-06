using System;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.DataStreaming.Metadata;

namespace Unity.Industry.Viewer.Streaming
{
    public class InstanceData : IEquatable<InstanceData>
    {
        /// <summary>
        /// Used as a placeholder forcing the tree items to be expandable when the <see cref="MetadataQuery"/> was not yet executed.
        /// </summary>
        public static InstanceData Placeholder { get; } = new(null, null, null);

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="instance"><see cref="MetadataInstance"/> to represent in the tree.</param>
        public InstanceData(MetadataInstance instance, StreamingModel streamingModel, IMetadataRepository repository)
        {
            Instance = instance;
            StreamingModel = streamingModel;
            Repository = repository;
        }

        /// <summary>
        /// <see cref="MetadataInstance"/> to represent in the tree.
        /// </summary>
        public MetadataInstance Instance { get; }

        /// <summary>
        /// <see langword="true"/> if this instance is a placeholder and does not represent a real <see cref="MetadataInstance"/>,
        /// <see langword="false"/> otherwise.
        /// </summary>
        public bool IsPlaceholder => Instance is null;

        /// <summary>
        /// Text to display in the tree as the main label.
        /// </summary>
        public string Name => Instance?.Name ?? "";

        public StreamingModel StreamingModel { get; }
        
        public IModelStream StreamModel => StreamingModel.ModelStream;
        
        public IMetadataRepository Repository { get; }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Instance?.Id, StreamModel);
        }
        
        
        public bool Equals(InstanceData other)
        {
            return Instance.Id == other.Instance.Id && StreamModel == other.StreamModel;

        }
    }
}
