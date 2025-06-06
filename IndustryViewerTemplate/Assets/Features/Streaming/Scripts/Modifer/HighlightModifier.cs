using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    public class HighlightModifier : Modifier, IDisposable
    {
        private Dictionary<ModelStreamId, HashSet<InstanceId>> m_CurrentExistingInstances;
        
        private HashSet<InstanceId> m_InstanceIds = new();
        
        /// <summary>
        /// Value used when removing the highlight state on an instance.
        /// </summary>
        static readonly Color32 k_DefaultColor = new(0, 0, 0, 0);

        /// <summary>
        /// Value used when highlighting an instance.
        /// </summary>
        static Color32 _highlightColor = new(0, 200, 255, 255);

        public HighlightModifier(Color color)
        {
            _highlightColor = (Color32)color;
        }

        /// <inheritdoc cref="ModifierSample.Update(ModelStreamId,InstanceId,bool)"/>
        protected override void Update(ModelStreamId modelId, InstanceId instanceId, bool state)
        {
            var color = state ? _highlightColor : k_DefaultColor;

            // Highlight the selected instance.

            try
            {
                InstanceUpdater.SetHighlight(modelId, new[] { instanceId }, color);
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        
        public override Task LoadAsync(ModelStreamId modelStreamId, IEnumerable<InstanceGeometricErrorState> states)
        {
            base.LoadAsync(modelStreamId, states);
            m_CurrentExistingInstances ??= new Dictionary<ModelStreamId, HashSet<InstanceId>>();

            foreach (var errorState in states)
            {
                if (!m_CurrentExistingInstances.ContainsKey(modelStreamId))
                {
                    m_CurrentExistingInstances.Add(modelStreamId, new HashSet<InstanceId>());
                }

                m_CurrentExistingInstances[modelStreamId].Add(errorState.InstanceId);
            }
            
            return Task.CompletedTask;
        }
        
        public override Task UnloadAsync(ModelStreamId modelStreamId, IEnumerable<InstanceGeometricErrorState> states)
        {
            if (m_CurrentExistingInstances == null)
            {
                return Task.CompletedTask;
            }
            
            foreach (var state in states)
            {
                if (m_CurrentExistingInstances.ContainsKey(modelStreamId))
                {
                    m_CurrentExistingInstances[modelStreamId].Remove(state.InstanceId);
                }
                if(m_CurrentExistingInstances[modelStreamId].Count == 0)
                    m_CurrentExistingInstances.Remove(modelStreamId);
            }
            
            return Task.CompletedTask;
        }

        public void UpdateList(ModelStreamId modelStreamId, HashSet<InstanceId> instanceIds, bool state)
        {
            var color = state ? _highlightColor : k_DefaultColor;
            m_InstanceIds = instanceIds;
            InstanceUpdater.SetHighlight(modelStreamId, m_InstanceIds, color);
        }

        public override void Reset()
        {
            base.Reset();

            if (m_CurrentExistingInstances != null)
            {
                foreach (var key in m_CurrentExistingInstances.Keys)
                {
                    InstanceUpdater.SetHighlight(key, m_CurrentExistingInstances[key], k_DefaultColor);
                }
            }
            
            m_InstanceIds?.Clear();
        }

        public void Dispose()
        {
            m_CurrentExistingInstances?.Clear();
            m_InstanceIds?.Clear();
            m_InstanceIds = null;
            m_CurrentExistingInstances = null;
        }
    }
}
