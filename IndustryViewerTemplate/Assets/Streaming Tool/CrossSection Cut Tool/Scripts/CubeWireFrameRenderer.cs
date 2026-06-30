using UnityEngine;

namespace Unity.Industry.Viewer.Streaming.CrossSection
{
    public class CubeWireFrameRenderer : MonoBehaviour
    {
        // Assign the 6 LineRenderers in the Inspector (from the child objects)
        LineRenderer[] _FaceRenderers = new LineRenderer[6];
        [SerializeField]
        private Material lineMaterial;
        private float lineWidth = 0.005f;
        private Vector3 meshBoundsCenter;
        private Vector3 meshBoundsExtents;
        
        [SerializeField]
        private float m_MaxLineWidth = 0.1f;
        [SerializeField]
        private float m_MinLineWidth = 0.001f;
        
        [SerializeField]
        private float m_LineWidthDistanceFactor = 0.1f;

        void Start()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var bounds = meshFilter.sharedMesh.bounds;
                meshBoundsCenter = bounds.center;
                meshBoundsExtents = bounds.extents;
            }
            else
            {
                Debug.LogError("MeshFilter or Mesh not found on parent object.");
                enabled = false;
            }

            for (var i = 0; i < _FaceRenderers.Length; i++)
            {
                var child = new GameObject("WireFace_" + i);
                child.transform.SetParent(transform, false);
                _FaceRenderers[i] = child.AddComponent<LineRenderer>();
                _FaceRenderers[i].useWorldSpace = true;
                _FaceRenderers[i].loop = true;
                _FaceRenderers[i].positionCount = 4;
                _FaceRenderers[i].startWidth = lineWidth;
                _FaceRenderers[i].endWidth = lineWidth;
                _FaceRenderers[i].numCapVertices = 2;
                _FaceRenderers[i].numCornerVertices = 2;
                _FaceRenderers[i].material = lineMaterial;
            }
        }

        void Update()
        {
            // Camera.main can be null when the active camera isn't tagged MainCamera (e.g. AR/passthrough);
            // keep the previous line width instead of dereferencing null every frame.
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                lineWidth = Mathf.Lerp(m_MinLineWidth, m_MaxLineWidth,
                    (transform.position - mainCamera.transform.position).magnitude * m_LineWidthDistanceFactor);
            }
            UpdateWireframeFaces();
        }

        void UpdateWireframeFaces()
        {
            // Use mesh bounds center and extents for accurate corners
            Vector3 c0 = meshBoundsCenter + new Vector3(-meshBoundsExtents.x, -meshBoundsExtents.y, -meshBoundsExtents.z);
            Vector3 c1 = meshBoundsCenter + new Vector3( meshBoundsExtents.x, -meshBoundsExtents.y, -meshBoundsExtents.z);
            Vector3 c2 = meshBoundsCenter + new Vector3( meshBoundsExtents.x,  meshBoundsExtents.y, -meshBoundsExtents.z);
            Vector3 c3 = meshBoundsCenter + new Vector3(-meshBoundsExtents.x,  meshBoundsExtents.y, -meshBoundsExtents.z);
            Vector3 c4 = meshBoundsCenter + new Vector3(-meshBoundsExtents.x, -meshBoundsExtents.y,  meshBoundsExtents.z);
            Vector3 c5 = meshBoundsCenter + new Vector3( meshBoundsExtents.x, -meshBoundsExtents.y,  meshBoundsExtents.z);
            Vector3 c6 = meshBoundsCenter + new Vector3( meshBoundsExtents.x,  meshBoundsExtents.y,  meshBoundsExtents.z);
            Vector3 c7 = meshBoundsCenter + new Vector3(-meshBoundsExtents.x,  meshBoundsExtents.y,  meshBoundsExtents.z);

            // Transform corners to world space
            c0 = transform.TransformPoint(c0);
            c1 = transform.TransformPoint(c1);
            c2 = transform.TransformPoint(c2);
            c3 = transform.TransformPoint(c3);
            c4 = transform.TransformPoint(c4);
            c5 = transform.TransformPoint(c5);
            c6 = transform.TransformPoint(c6);
            c7 = transform.TransformPoint(c7);

            // TOP Face (Corners: 3, 2, 6, 7)
            if (_FaceRenderers[0] != null)
            {
                _FaceRenderers[0].SetPositions(new[] { c3, c2, c6, c7 });
                _FaceRenderers[0].startWidth = lineWidth;
                _FaceRenderers[0].endWidth = lineWidth;
            }
                

            // BOTTOM Face (Corners: 0, 1, 5, 4)
            if (_FaceRenderers[1] != null)
            {
                _FaceRenderers[1].SetPositions(new[] { c0, c1, c5, c4 });
                _FaceRenderers[1].startWidth = lineWidth;
                _FaceRenderers[1].endWidth = lineWidth;
            }
            
            // FRONT Face (Corners: 4, 5, 6, 7)
            if (_FaceRenderers[2] != null)
            {
                _FaceRenderers[2].SetPositions(new[] { c4, c5, c6, c7 });
                _FaceRenderers[2].startWidth = lineWidth;
                _FaceRenderers[2].endWidth = lineWidth;
            }

            // BACK Face (Corners: 0, 1, 2, 3)
            if (_FaceRenderers[3] != null)
            {
                _FaceRenderers[3].SetPositions(new[] { c0, c1, c2, c3 });
                _FaceRenderers[3].startWidth = lineWidth;
                _FaceRenderers[3].endWidth = lineWidth;
            }
                

            // LEFT Face (Corners: 0, 3, 7, 4)
            if (_FaceRenderers[4] != null)
            {
                _FaceRenderers[4].SetPositions(new[] { c0, c3, c7, c4 });
                _FaceRenderers[4].startWidth = lineWidth;
                _FaceRenderers[4].endWidth = lineWidth;
            }
                

            // RIGHT Face (Corners: 1, 2, 6, 5)
            if (_FaceRenderers[5] != null)
            {
                _FaceRenderers[5].SetPositions(new[] { c1, c2, c6, c5 });
                _FaceRenderers[5].startWidth = lineWidth;
                _FaceRenderers[5].endWidth = lineWidth;
            }
                
        }
    }
}
