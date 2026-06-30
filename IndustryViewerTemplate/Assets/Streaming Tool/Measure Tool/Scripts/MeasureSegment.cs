using UnityEngine;
using System.Collections.Generic;

namespace Unity.Industry.Viewer.Streaming.Measurement
{
    public class MeasureSegment : MonoBehaviour
    {
        [SerializeField]
        MeasureLine m_MeasureLine = new ();

        [SerializeField]
        MeasureLabel m_MeasureLabel = new ();
        
        [SerializeField]
        Transform m_StartPoint;
        
        [SerializeField]
        Transform m_EndPoint;
        
        readonly List<Renderer> m_PointRenderers = new ();

        public Vector3 StartPosition
        {
            get => m_StartPoint.position;
            set => m_StartPoint.position = value;
        }

        public Vector3 EndPosition
        {
            get => m_EndPoint.position;
            set => m_EndPoint.position = value;
        }

        public void SetLabelText(string text)
        {
            m_MeasureLabel.m_MeasureLabelElement.text = text;
        }

        void Awake()
        {
            m_PointRenderers.AddRange(m_StartPoint.GetComponentsInChildren<Renderer>());
            m_PointRenderers.AddRange(m_EndPoint.GetComponentsInChildren<Renderer>());
            m_MeasureLabel.Init();
        }

        void LateUpdate()
        {
            // Camera.main can be null when the active streaming camera isn't tagged MainCamera
            // (e.g. AR/passthrough); skip the camera-relative line update rather than NRE every frame.
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                m_MeasureLine.Update(m_StartPoint.position, m_EndPoint.position, mainCamera.transform.position);
            }
            UpdateLabel();
        }

        void UpdateLabel()
        {
            Vector3 startPosition = m_StartPoint.position;
            Vector3 endPosition = m_EndPoint.position;
            m_MeasureLabel.SetLabelPosition(startPosition, endPosition);
        }

        public void SetColor(Color color)
        {
            m_MeasureLine.SetColor(color);
            m_MeasureLabel.SetColor(color);
            
            foreach (var pointRenderer in m_PointRenderers)
            {
                pointRenderer.material.color = color;
            }
        }
        
        public void SetVisible(bool visible)
        {
            m_MeasureLabel.SetLabelVisible(visible);
            m_MeasureLine.SetVisible(visible);
            foreach (var pointRenderer in m_PointRenderers)
            {
                pointRenderer.enabled = visible;
            }
        }
    }
}
