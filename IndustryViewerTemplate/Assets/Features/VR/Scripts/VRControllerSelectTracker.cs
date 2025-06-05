using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Industry.Viewer.Navigation.VR
{
    public class VRControllerSelectTracker : MonoBehaviour
    {
        [SerializeField]
        private InputActionProperty m_SelectInputAction;

        private InputAction m_SelectAction;
        
        [SerializeField]
        private float m_RayLength = 200;
        
        private int k_UIMask => LayerMask.GetMask("UI");
        
        private void OnEnable()
        {
            m_SelectAction ??= m_SelectInputAction.reference.action ?? m_SelectInputAction.action;
            m_SelectAction.Enable();
        }

        private void Start()
        {
            m_SelectAction.performed += OnSelect;
        }
        
        private void OnDisable()
        {
            m_SelectAction.Disable();
            
        }

        private void OnDestroy()
        {
            m_SelectAction.performed -= OnSelect;
        }

        private void OnSelect(InputAction.CallbackContext obj)
        {
            if (!CheckHit()) return;
            WorldSpaceUICustomFunction.SetCustomFunction?.Invoke(transform);
        }
        
        bool CheckHit()
        {
            var ray = new Ray(transform.position, transform.forward);
            
            return Physics.Raycast(ray, out _, m_RayLength, k_UIMask);
        }
    }
}
