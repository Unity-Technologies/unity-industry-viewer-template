using UnityEngine;

namespace Unity.Industry.Viewer.Shared
{
    public class ZoneScale : MonoBehaviour
    {
        [SerializeField]
        float m_Size = 1.0f;

        [SerializeField]
        float m_Smooth = 0.2f;

        [SerializeField]
        bool m_Clamp;

        [SerializeField]
        Vector2 m_ClampValues = new Vector2(1f, 10f);

        float m_CurrentScale;
        float m_CurrentVelocity;
        
        void OnEnable()
        {
            m_CurrentScale = 0.0f;
            m_CurrentVelocity = 0.0f;
        }
        
        void LateUpdate()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            var cameraTransform = mainCamera.transform;

            var cameraPosition = cameraTransform.position;
            var deltaToCamera = cameraPosition - transform.position;

            var targetScale = m_Size * deltaToCamera.magnitude * 0.1f;

            m_CurrentScale = Mathf.SmoothDamp(m_CurrentScale, targetScale, ref m_CurrentVelocity, m_Smooth);

            if (m_Clamp)
            {
                m_CurrentScale = Mathf.Clamp(m_CurrentScale, m_ClampValues.x, m_ClampValues.y);
            }

            transform.localScale = Vector3.one * m_CurrentScale;
        }
    }
}
