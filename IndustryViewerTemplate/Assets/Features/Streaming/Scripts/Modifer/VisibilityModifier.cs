using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.DataStreaming.Metadata;
using Unity.Cloud.DataStreaming.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    public class VisibilityModifier : Modifier, IDisposable
    {
        private Dictionary<ModelStreamId, HashSet<InstanceId>> m_CurrentExistingInstances;

        public HashSet<InstanceData> HiddenInstances => m_HiddenInstances;
        
        private HashSet<InstanceData> m_HiddenInstances;
        
        public override void Reset()
        {
            base.Reset();
            m_HiddenInstances?.Clear();
            if (m_CurrentExistingInstances == null) return;
            foreach (var key in m_CurrentExistingInstances.Keys)
            {
                InstanceUpdater.SetVisibility(key, m_CurrentExistingInstances[key], true);
            }
        }

        protected override void Update(ModelStreamId modelId, InstanceId instanceId, bool state) { }

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
            
            if(m_HiddenInstances == null || m_HiddenInstances.Count == 0 || !AllHiddenInstancesMatch(modelStreamId)) return Task.CompletedTask;
            
            var leafNodes = new HashSet<InstanceId>();
            foreach (var state in states)
            {
                leafNodes.Add(state.InstanceId);
            }
            
            _ = UpdateFilter(modelStreamId, leafNodes);
            
            return Task.CompletedTask;
        }
        
        private bool AllHiddenInstancesMatch(ModelStreamId modelStreamId)
        {
            if(m_HiddenInstances == null) return false;
            
            foreach (var instance in m_HiddenInstances)
            {
                if (instance.StreamingModel.ModelStream.Id != modelStreamId)
                {
                    return false;
                }
            }
            return true;
        }
        
        public override Task UnloadAsync(ModelStreamId modelStreamId, IEnumerable<InstanceGeometricErrorState> states)
        {
            var leafNodes = new HashSet<InstanceId>();
            foreach (var state in states)
            {
                leafNodes.Add(state.InstanceId);
                
                if (m_CurrentExistingInstances.ContainsKey(modelStreamId))
                {
                    m_CurrentExistingInstances[modelStreamId].Remove(state.InstanceId);
                }
                if(m_CurrentExistingInstances[modelStreamId].Count == 0)
                    m_CurrentExistingInstances.Remove(modelStreamId);
            }
            
            InstanceUpdater.SetVisibility(modelStreamId, leafNodes, true);
            
            return Task.CompletedTask;
        }

        public void UpdateVisibility(InstanceData instanceData, bool visible)
        {
            m_HiddenInstances ??= new HashSet<InstanceData>();
            if (visible)
            {
                m_HiddenInstances.Remove(instanceData);
            }
            else
            {
                m_HiddenInstances.Add(instanceData);
            }
            
            if (m_HiddenInstances.Count == 0)
            {
                Reset();
                return;
            }

            var modelStreamId = instanceData.StreamingModel.ModelStream.Id;
            var leafNodes = instanceData.Instance.HasChildren? 
                m_CurrentExistingInstances[modelStreamId] : 
                new HashSet<InstanceId>() {instanceData.Instance.Id};
            _ = UpdateFilter(modelStreamId, leafNodes);
        }
        
        private async Task UpdateFilter(ModelStreamId modelStreamId, HashSet<InstanceId> leafNodes)
        {
            if (m_CurrentExistingInstances == null || !m_CurrentExistingInstances.ContainsKey(modelStreamId)) return;

            var repository = GetFirstHiddenInstance(modelStreamId)?.Repository;

            if (repository == null) return;

            var instanceAncestry = await repository
                .Query()
                .Select(MetadataPathCollection.None, new OptionalData(OptionalData.Fields.AncestorIds | OptionalData.Fields.Id))
                .WhereInstanceEquals(leafNodes)
                .ToListAsync(CancellationToken.None);

            var hiddenInstances = new HashSet<InstanceId>();
            var showInstances = new HashSet<InstanceId>();

            foreach (var metadataInstance in instanceAncestry)
            {
                if (AnyHiddenInstanceMatches(x => x.Instance.Id == metadataInstance.Id))
                {
                    hiddenInstances.Add(metadataInstance.Id);
                }
                else
                {
                    bool ancestorHidden = false;
                    foreach (var ancestorId in metadataInstance.AncestorIds)
                    {
                        if (AnyHiddenInstanceMatches(x => x.Instance.Id == ancestorId && x.StreamingModel.ModelStream.Id == modelStreamId))
                        {
                            ancestorHidden = true;
                            break;
                        }
                    }

                    if (ancestorHidden)
                    {
                        hiddenInstances.Add(metadataInstance.Id);
                    }
                    else
                    {
                        showInstances.Add(metadataInstance.Id);
                    }
                }
            }

            InstanceUpdater.SetVisibility(modelStreamId, showInstances, true);
            InstanceUpdater.SetVisibility(modelStreamId, hiddenInstances, false);
        }
        
        private InstanceData GetFirstHiddenInstance(ModelStreamId modelStreamId)
        {
            if(m_HiddenInstances == null) return null;
            
            foreach (var instance in m_HiddenInstances)
            {
                if (instance.StreamingModel.ModelStream.Id == modelStreamId)
                {
                    return instance;
                }
            }
            return null;
        }

        private bool AnyHiddenInstanceMatches(Func<InstanceData, bool> predicate)
        {
            if(m_HiddenInstances == null) return false;
            
            foreach (var instance in m_HiddenInstances)
            {
                if (predicate(instance))
                {
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            m_CurrentExistingInstances?.Clear();
            m_HiddenInstances?.Clear();
            m_HiddenInstances = null;
            m_CurrentExistingInstances = null;
        }
    }
}