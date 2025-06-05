using System;
using System.Collections.Generic;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    public abstract class Modifier : InstanceModifier
    {
        /// <summary>
        /// Stores the selected <see cref="InstanceId"/>s that have been altered.
        /// </summary>
        protected readonly List<Key> m_Selection = new();

        /// <summary>
        /// Empty the <see cref="m_Selection"/>. This will not update the <see cref="InstanceId"/>s, use <see cref="Reset"/> in that case.
        /// </summary>
        public void Clear() => m_Selection.Clear();

        /// <summary>
        /// Get the amount of selected <see cref="InstanceId"/>s.
        /// </summary>
        public int Count => m_Selection.Count;

        /// <summary>
        /// Reset the <see cref="InstanceId"/>s part of the selection to their original state.
        /// </summary>
        public virtual void Reset()
        {
            foreach (var key in m_Selection)
                Update(key.ModelId, key.InstanceId, false);

            m_Selection.Clear();
        }

        /// <summary>
        /// Toggle the state of the given <paramref name="instanceId"/>. If it was selected, it will be unselected and vice-versa.
        /// </summary>
        /// <param name="modelId">Owner of the instance to be altered.</param>
        /// <param name="instanceId">Instance requested to be changed.</param>
        public void Update(ModelStreamId modelId, InstanceId instanceId)
        {
            var key = new Key(modelId, instanceId);

            var selected = m_Selection.Contains(key);
            if (selected)
                m_Selection.Remove(key);
            else
                m_Selection.Add(key);

            Update(modelId, instanceId, !selected);
        }
        
        /// <summary>
        /// Check if the given <paramref name="instanceId"/> is part of the selection.
        /// </summary>
        /// <param name="modelId"></param>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public virtual bool Contains(ModelStreamId modelId, InstanceId instanceId)
        {
            return m_Selection.Contains(new Key(modelId, instanceId));
        }

        /// <summary>
        /// Implement this method to update the <paramref name="instanceId"/> state.
        /// </summary>
        /// <param name="modelId">Owner of the instance to be altered.</param>
        /// <param name="instanceId">Instance requested to be changed.</param>
        /// <param name="state"><see langword="true"/> if the <paramref name="instanceId"/> state is active; <see langword="false"/> if the <paramref name="instanceId"/> must be reset to it's default state.</param>
        protected abstract void Update(ModelStreamId modelId, InstanceId instanceId, bool state);
        
        public readonly struct Key : IEquatable<Key>
        {
            /// <summary>
            /// Owner of the <see cref="InstanceId"/>.
            /// </summary>
            public readonly ModelStreamId ModelId;

            /// <summary>
            /// Id of the instance.
            /// </summary>
            public readonly InstanceId InstanceId;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="modelId">Owner of the <see cref="InstanceId"/>.</param>
            /// <param name="instanceId">ID of the instance.</param>
            public Key(ModelStreamId modelId, InstanceId instanceId)
            {
                ModelId = modelId;
                InstanceId = instanceId;
            }

            /// <summary>
            /// Evaluate if the given <paramref name="other"/> is equal to this instance.
            /// </summary>
            /// <param name="other">Compare with this other <see cref="Key"/>.</param>
            /// <returns>Returns <see langword="true"/> if both <see cref="Key"/>s are equals; <see langword="false"/> otherwise.</returns>
            public bool Equals(Key other)
            {
                return ModelId.Equals(other.ModelId) && InstanceId.Equals(other.InstanceId);
            }

            /// <inheritdoc cref="Equals(Key)"/>
            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            /// <summary>
            /// Returns a hash code for this instance.
            /// </summary>
            /// <returns>
            /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
            /// </returns>
            public override int GetHashCode()
            {
                return HashCode.Combine(ModelId, InstanceId);
            }
        }
    }
}
