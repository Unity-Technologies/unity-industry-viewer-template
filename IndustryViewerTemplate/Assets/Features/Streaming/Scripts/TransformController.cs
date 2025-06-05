// The TransformController class manages the transformations of streaming models in a Unity application.
// It handles adding, removing, and updating the transforms of models, as well as managing their visibility.
// The class ensures that only one instance of the TransformController exists at a time.

using Unity.Mathematics;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cloud.DataStreaming.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(-150)]
    public class TransformController : MonoBehaviour
    {
        // Events for model addition, removal, and transform changes
        public static Action<GameObject, ITransformValuesAccessor> ModelAdded;
        public static Action<StreamingModel> ModelRemoved;
        public static Action<Transform> TransformChanged;
        
        // Singleton instance of the TransformController
        public static TransformController Instance { get; private set; }

        public List<Transform> Transforms => m_Transforms.Keys.ToList();
        
        // Dictionary to associate each Transform in the scene with the underlying transforms managed by IStage and IModelStream
        readonly Dictionary<Transform, ITransformValuesAccessor> m_Transforms = new();

        // Reference to the StreamingModelController
        StreamingModelController m_StreamingModelController;
        
        // Store original gameobject state
        private Dictionary<GameObject, bool> m_OriginalVisibility = new ();

        // Called when the script instance is being loaded
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // If an instance already exists, copy its transform and children to the new instance
                var oldController = Instance;
                transform.SetPositionAndRotation(oldController.transform.position, oldController.transform.rotation);
                transform.localScale = oldController.transform.localScale;
                StreamingModel[] models = oldController.GetComponentsInChildren<StreamingModel>(true);
                foreach (var child in models)
                {
                    if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                    AddModelToTransform(child.gameObject, child.ModelStream.Transform);
                }
                Instance = this;
                Destroy(oldController.gameObject);
            }
            else
            {
                // Set the singleton instance
                Instance = this;
            }

            // Subscribe to model addition and removal events
            ModelAdded += AddModelToTransform;
            ModelRemoved += RemoveModelTransform;
        }

        // Start is called before the first frame update
        private void Start()
        {
            // Find the StreamingModelController in the scene
            m_StreamingModelController = FindAnyObjectByType<StreamingModelController>();
            if (m_StreamingModelController != null) return;
            Debug.Log("StreamingController not found in scene.");
        }

        // Called when the script is enabled
        private void OnEnable()
        {
            // Make all children visible
            ChangeChildrenVisibility(true);
        }

        // Update is called once per frame
        private void Update()
        {
            if(m_StreamingModelController == null || m_StreamingModelController.Stage == null) return;

            // Update the transforms of all children
            foreach (Transform child in transform)
            {
                if(child == null) continue;
                if(!child.gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                if (!m_Transforms.TryGetValue(child, out var accessor))
                {
                    continue;
                }
                if (!child.hasChanged) continue;
                TransformChanged?.Invoke(child);
                child.hasChanged = false;

                var v = new TransformValues(
                    (float3)child.localPosition,
                    child.localRotation,
                    child.localScale.x);

                accessor.Set(v);
                StreamingModelController.RequestBoundsUpdate?.Invoke();
            }

            // Update the transform of the controller itself
            if (transform.hasChanged)
            {
                TransformChanged?.Invoke(transform);
                transform.hasChanged = false;

                var values = new TransformValues(
                    (float3)transform.localPosition,
                    transform.localRotation,
                    transform.localScale.x);

                m_StreamingModelController.Stage.Transform.Set(values);
                StreamingModelController.RequestBoundsUpdate?.Invoke();
            }
        }

        // Called when the script is disabled
        private void OnDisable()
        {
            // Make all children invisible
            ChangeChildrenVisibility(false);
        }

        // Changes the visibility of all children
        private void ChangeChildrenVisibility(bool visible)
        {
            m_OriginalVisibility ??= new Dictionary<GameObject, bool>();

            if (visible)
            {
                foreach (var key in m_OriginalVisibility.Keys)
                {
                    key.SetActive(m_OriginalVisibility[key]);
                }
                m_OriginalVisibility.Clear();
            }
            else
            {
                m_OriginalVisibility.Clear();
                for(var i = 0; i < transform.childCount; i++)
                {
                    var originalVisibility = transform.GetChild(i).gameObject.activeSelf;
                    m_OriginalVisibility.Add(transform.GetChild(i).gameObject, originalVisibility);
                    transform.GetChild(i).gameObject.SetActive(false);
                }
            }
        }

        // Called when the MonoBehaviour will be destroyed
        private void OnDestroy()
        {
            // Clear the singleton instance and unsubscribe from events
            if (Instance != null && Instance == this)
            {
                Instance = null;
            }
            ModelAdded -= AddModelToTransform;
            ModelRemoved -= RemoveModelTransform;
            m_Transforms.Clear();
        }

        // Removes the transform of a streaming model
        private void RemoveModelTransform(StreamingModel streamingModel)
        {
            ITransformValuesAccessor transformValuesAccessor = streamingModel.ModelStream.Transform;
            if (!m_Transforms.ContainsValue(transformValuesAccessor)) return;
            var (t, _) = m_Transforms.First(x => x.Value == transformValuesAccessor);
            t.SetParent(null);
            m_Transforms.Remove(t);
        }

        // Adds a model to the transform
        private void AddModelToTransform(GameObject go, ITransformValuesAccessor model)
        {
            go.transform.SetParent(transform, false);
            m_Transforms.Add(go.transform, model);
        }
    }
}