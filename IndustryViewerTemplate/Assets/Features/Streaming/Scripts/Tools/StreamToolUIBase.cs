using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming
{
    public abstract class StreamToolUIBase : MonoBehaviour
    {
        public VisualTreeAsset ToolUIAsset => m_toolUIAsset;
        
        [SerializeField] private VisualTreeAsset m_toolUIAsset;
        
        protected StreamToolControllerBase m_Controller;

        public abstract void InitializeUI(VisualElement parent, GameObject controller);
        
        public abstract void UninitializeUI();

        private void Awake()
        {
            m_Controller = GetComponent<StreamToolControllerBase>();
        }
    }
}
