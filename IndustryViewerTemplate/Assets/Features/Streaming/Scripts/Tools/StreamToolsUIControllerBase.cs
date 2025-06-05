using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamToolsUIControllerBase : MonoBehaviour
    {
        public static Action<StreamingToolAsset, GameObject, bool> UpdateToolPanel;
    }
}
