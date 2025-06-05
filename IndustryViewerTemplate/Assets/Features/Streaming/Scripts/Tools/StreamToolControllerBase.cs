using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public abstract class StreamToolControllerBase : MonoBehaviour
    {
        public Action ToolOpened;
        public Action ToolClosed;
        
        public abstract void OnToolOpened();
        
        public abstract void OnToolClosed();
    }
}
