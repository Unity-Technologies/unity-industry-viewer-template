using UnityEngine;
using System;
using Unity.AppUI.Core;
using System.Collections;

namespace Unity.Industry.Viewer.Navigation.VR
{
    [RequireComponent(typeof(WorldSpaceUIDocument))]
    public class WorldSpaceUICustomFunction : MonoBehaviour
    {
        public static Action<Transform> SetCustomFunction;
        
        WorldSpaceUIDocument m_WorldSpaceUIDocument;

        private void Awake()
        {
            m_WorldSpaceUIDocument = GetComponent<WorldSpaceUIDocument>();
        }

        private IEnumerator Start()
        {
            while (m_WorldSpaceUIDocument.targetCamera == null)
            {
                m_WorldSpaceUIDocument.targetCamera = Camera.main;
                if (m_WorldSpaceUIDocument.targetCamera != null)
                {
                    break;
                }
                yield return null;
            }
            SetCustomFunction += SetCustomFunctionInternal;
        }

        private void OnDestroy()
        {
            SetCustomFunction -= SetCustomFunctionInternal;
        }

        private void SetCustomFunctionInternal(Transform transformController)
        {
            m_WorldSpaceUIDocument.customRayFunc = () => new Ray(transformController.position, transformController.forward);
        }
    }
}
