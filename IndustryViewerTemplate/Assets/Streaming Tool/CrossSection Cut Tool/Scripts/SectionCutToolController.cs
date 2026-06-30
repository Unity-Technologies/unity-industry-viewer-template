using UnityEngine;
using Unity.Cloud.HighPrecision.Runtime;
using RuntimeGizmos;
using System.Collections;
using UnityEngine.InputSystem;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.CrossSection
{
    public class SectionCutToolController : StreamToolControllerBase
    {
        private const string k_WorldToCube = "_WorldToCube";
        
        private static int WorldToCubeShaderId => Shader.PropertyToID(k_WorldToCube);

        private const string k_EnableClipping = "_Clipping";
        
        public static int EnableClippingShaderId => Shader.PropertyToID(k_EnableClipping);

        private static string k_Flipped = "_Invert";
        
        private const float rotateHandleSizeFactor = 0.5f;
        
        public static int FlippedShaderId => Shader.PropertyToID(k_Flipped);

        private StreamingModelController m_StreamingModelController;

        [SerializeField] private GameObject m_BoxVisualPrefab;
        
        private GameObject m_BoxVisual;

        private Vector3 m_LastPos;
        private Quaternion m_LastRot;
        private Vector3 m_LastScale;

        [SerializeField]
        private LayerMask m_RuntimeHandleLayerMask;
        
        private TransformGizmo m_TransformGizmo;
        
        [SerializeField] private InputActionProperty m_PointerClick;
        [SerializeField] private InputActionProperty m_PointerPress;
        [SerializeField] private InputActionProperty m_PointerMove;

        private GameObject m_GizmoAnchor;
        private TransformType m_CurrentTransformType = TransformType.Move;
        
        private float m_TargetHandleSizeFactor = 1f;
        
        private void Awake()
        {
            m_StreamingModelController = FindFirstObjectByType<StreamingModelController>();
            if(m_StreamingModelController == null) return;
            Shader.SetGlobalFloat(EnableClippingShaderId, 1f);
            Shader.SetGlobalFloat(FlippedShaderId, 0);
        }

        private IEnumerator Start()
        {
            if(m_StreamingModelController == null)
                yield break;
            
            var boundary = m_StreamingModelController.Stage.GetWorldBounds();

            while(boundary.Size.ToVector3() == Vector3.zero)
            {
                yield return null;
                boundary = m_StreamingModelController.Stage.GetWorldBounds();
            }

            Initialize(boundary);
        }
        
        private void Update()
        {
            m_TransformGizmo?.SetDistanceMultiplier(m_TargetHandleSizeFactor);
            
            if (m_BoxVisual == null)
                return;
            
            if (TransformController.Instance.transform.hasChanged)
            {
                var boundary = m_StreamingModelController.Stage.GetWorldBounds();
                if (m_TransformGizmo != null)
                {
                    if (m_TransformGizmo.transformType == TransformType.Move || m_TransformGizmo.transformType == TransformType.Scale)
                    {
                        var newPos = boundary.Center.ToVector3() + new Vector3(-(float)boundary.Size.x / 2,
                            -(float)boundary.Size.y / 2, -(float)boundary.Size.z / 2);
                        m_GizmoAnchor.transform.SetPositionAndRotation(newPos, Quaternion.identity);
                    }
                    else
                    {
                        m_GizmoAnchor.transform.SetPositionAndRotation(boundary.Center.ToVector3(), Quaternion.identity);
                    }
                }
            }

            if (m_TransformGizmo != null && m_TransformGizmo.transformType == TransformType.Rotate)
            {
                m_BoxVisual.transform.rotation = m_GizmoAnchor.transform.rotation;
            }
            
            if (m_LastPos == m_GizmoAnchor.transform.position && m_LastRot == m_GizmoAnchor.transform.rotation &&
                m_LastScale == m_GizmoAnchor.transform.localScale) return;
            
            m_LastPos = m_GizmoAnchor.transform.position;
            m_LastRot = m_GizmoAnchor.transform.rotation;
            m_LastScale = m_GizmoAnchor.transform.localScale;
                
            Matrix4x4 worldToCube = Matrix4x4.TRS(
                m_BoxVisual.transform.position,
                m_BoxVisual.transform.rotation,
                m_GizmoAnchor.transform.localScale
            ).inverse;
                
            Shader.SetGlobalMatrix(WorldToCubeShaderId, worldToCube);
        }

        private void OnDestroy()
        {
            if (m_BoxVisual != null)
            {
                Destroy(m_BoxVisual);
            }
            StreamingModelController.PauseWireframeMode.Invoke(false);
            Shader.SetGlobalFloat(EnableClippingShaderId, 0f);
            Destroy(m_GizmoAnchor);
            DestroyTransformGizmo();
        }

        private void Initialize(DoubleBounds boundary)
        {
            Vector3 center = boundary.Center.ToVector3();
            Vector3 halfScale = boundary.Extents.ToVector3();
            Vector3 cornerWorldPos =
                center
                - TransformController.Instance.transform.transform.right * halfScale.x
                - TransformController.Instance.transform.transform.up * halfScale.y
                - TransformController.Instance.transform.transform.forward * halfScale.z;
            
            m_GizmoAnchor = new GameObject("GizmoAnchor")
            {
                transform =
                {
                    position = cornerWorldPos,
                    localScale = boundary.Size.ToVector3(),
                    rotation = TransformController.Instance.transform.rotation
                }
            };

            if (m_BoxVisualPrefab != null)
            {
                m_BoxVisual = Instantiate(m_BoxVisualPrefab, m_GizmoAnchor.transform);
                m_BoxVisual.transform.SetPositionAndRotation(boundary.Center.ToVector3(), TransformController.Instance.transform.rotation);
                m_BoxVisual.transform.localScale = Vector3.one;
            }
                
            
            Matrix4x4 worldToCube = Matrix4x4.TRS(
                m_BoxVisual.transform.position,
                m_BoxVisual.transform.rotation,
                boundary.Size.ToVector3()
            ).inverse;
            
            Shader.SetGlobalMatrix(WorldToCubeShaderId, worldToCube);
            
            m_LastPos = m_GizmoAnchor.transform.position;
            m_LastRot = m_GizmoAnchor.transform.rotation;
            m_LastScale = m_GizmoAnchor.transform.localScale;
            
            CreateTransformGizmo(TransformType.Move);
            StreamingModelController.PauseWireframeMode.Invoke(true);
        }

        private void CreateTransformGizmo(TransformType type)
        {
            if(m_TransformGizmo != null) return;
            m_TransformGizmo ??= TransformGizmo.CreateHandle(m_StreamingModelController.ActiveCamera, m_GizmoAnchor.transform)
                .WithActions(m_PointerMove.action, m_PointerPress.action)
                .WithLayer(m_RuntimeHandleLayerMask);
            m_TransformGizmo.snapOverride = false;
            m_TargetHandleSizeFactor = type == TransformType.Rotate? rotateHandleSizeFactor: 1f;
            m_TransformGizmo.SetType(type);
            m_TransformGizmo.SetSpace(TransformSpace.Local);
            SubscribeGizmoHandlers(m_TransformGizmo);
#if !VR_MODE
            m_PointerPress.action.Enable();
            m_PointerClick.action.Enable();
            m_PointerMove.action.Enable();
#else
            VRInteractionController.SubscribeControllerMoved(this, OnVRControllerMoves);
            VRInteractionController.SubscribePressActivate(this, OnVRControllerPress);
#endif
        }

        private void DestroyTransformGizmo()
        {
            if (m_TransformGizmo != null)
            {
                UnsubscribeGizmoHandlers(m_TransformGizmo);
                Destroy(m_TransformGizmo);
            }
#if !VR_MODE
            m_PointerPress.action.Disable();
            m_PointerClick.action.Disable();
            m_PointerMove.action.Disable();
#else
            VRInteractionController.UnsubscribeControllerMoved(this);
            VRInteractionController.UnsubscribePressActivate(this);
#endif
            m_TransformGizmo = null;
        }
        
#if VR_MODE
        private void OnVRControllerPress(Ray ray, bool press, int rayID)
        {
            m_TransformGizmo?.SelectAction?.Invoke(press, ray, rayID);
        }

        private void OnVRControllerMoves(Ray ray, int rayID)
        {
            m_TransformGizmo?.InputMoveAction?.Invoke(ray, rayID);
        }
#endif

        public void ResetSectionCut(TransformType  transformType)
        {
            if(m_BoxVisual == null) return;
            var boundary = m_StreamingModelController.Stage.GetWorldBounds();
            
            Shader.SetGlobalFloat(FlippedShaderId, 0);
            if (transformType == TransformType.Move || transformType == TransformType.Scale)
            {
                Vector3 center = boundary.Center.ToVector3();
                Vector3 halfScale = boundary.Extents.ToVector3();
                Vector3 cornerWorldPos =
                    center
                    - TransformController.Instance.transform.transform.right * halfScale.x
                    - TransformController.Instance.transform.transform.up * halfScale.y
                    - TransformController.Instance.transform.transform.forward * halfScale.z;
                
                m_GizmoAnchor.transform.SetPositionAndRotation(cornerWorldPos, TransformController.Instance.transform.rotation);
                m_GizmoAnchor.transform.localScale = boundary.Size.ToVector3();
                m_BoxVisual.transform.localScale = Vector3.one;
            }
            else
            {
                m_GizmoAnchor.transform.localScale = boundary.Size.ToVector3();
                m_GizmoAnchor.transform.SetPositionAndRotation(boundary.Center.ToVector3(), TransformController.Instance.transform.rotation);
                m_BoxVisual.transform.localScale = boundary.Size.ToVector3();
            }
            m_BoxVisual.transform.position = boundary.Center.ToVector3();
            m_BoxVisual.transform.rotation = TransformController.Instance.transform.rotation;
            CreateTransformGizmo(transformType);
            ShowSectionBox(true);
        }

        public void ShowSectionBox(bool value)
        {
            m_BoxVisual.SetActive(value);
        }

        public void ShowGizmo(bool show, int transformType)
        {
            if (show)
            {
                TransformType type = TransformType.Move;
                switch (transformType)
                {
                    case 0:
                        type = TransformType.Move;
                        break;
                    
                    case 1:
                        type = TransformType.Rotate;
                        break;
                    
                    case 2:
                        type = TransformType.Scale;
                        break;
                }
                CreateTransformGizmo(type);
            }
            else
            {
                DestroyTransformGizmo();
            }
        }

        public void SetGizmoMode(TransformType type)
        {
            bool recreate = false;
            if (m_TransformGizmo)
            {
                recreate = true;
                DestroyTransformGizmo();
            }
            
            if ((m_CurrentTransformType == TransformType.Scale ||
                 m_CurrentTransformType == TransformType.Move) &&
                type == TransformType.Rotate)
            {
                // Moving from Scale/Move to Rotate
                m_BoxVisual.transform.SetParent(null);
                m_BoxVisual.transform.localScale = m_GizmoAnchor.transform.localScale;
                m_GizmoAnchor.transform.SetPositionAndRotation(m_BoxVisual.transform.position, m_BoxVisual.transform.rotation);
            } else if (m_CurrentTransformType == TransformType.Rotate &&
                       (type == TransformType.Scale || type == TransformType.Move))
            {
                // Moving from Rotate to Scale/Move
                Vector3 center = m_BoxVisual.transform.position;
                Vector3 halfScale = 0.5f * m_BoxVisual.transform.localScale;
                Vector3 cornerWorldPos =
                    center
                    - m_BoxVisual.transform.right * halfScale.x
                    - m_BoxVisual.transform.up * halfScale.y
                    - m_BoxVisual.transform.forward * halfScale.z;
                m_GizmoAnchor.transform.SetPositionAndRotation(cornerWorldPos, m_BoxVisual.transform.rotation);
                m_BoxVisual.transform.SetParent(m_GizmoAnchor.transform);
                m_BoxVisual.transform.localScale = Vector3.one;
            }
            m_CurrentTransformType = type;
            m_TargetHandleSizeFactor = type == TransformType.Rotate? rotateHandleSizeFactor: 1f;
            if (recreate)
            {
                StartCoroutine(Wait());
            }
            return;

            IEnumerator Wait()
            {
                yield return null;
                CreateTransformGizmo(type);
            }
        }
        

        public override void OnToolOpened()
        {
            ToolOpened?.Invoke();
        }

        public override void OnToolClosed()
        {
            ToolClosed?.Invoke();
        }
    }
}
