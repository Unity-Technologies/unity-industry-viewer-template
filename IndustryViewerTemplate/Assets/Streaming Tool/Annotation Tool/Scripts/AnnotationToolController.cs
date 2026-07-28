using System;
using Unity.Industry.Viewer.Collaboration;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Shared;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.Annotation
{
    public class AnnotationToolController: StreamToolControllerBase
    {
        // hitModel is the specific streamed model child the ray hit (null on a miss/UI-blocked), so the
        // annotation marker can be anchored to that model and follow it when it's moved in a layout.
        public Action<Vector3?, bool, int?, Transform> OnNewAnnotationPositionDefining;
        
        [HideInInspector]
        public CollaborationController.FilterType CurrentFilterType = CollaborationController.FilterType.All;

        StreamingModelController m_StreamingModelController;
        
        [SerializeField]
        LayerMask m_UILayerMask;
        
        public CancellationTokenSource TokenSource;

        private void Start()
        {
            m_StreamingModelController = FindFirstObjectByType<StreamingModelController>();
        }

        public override void OnToolOpened()
        {
            if(NetworkDetector.IsOffline || !PlatformServices.IsUserLoggedIn) return;
            ToolOpened?.Invoke();
            CurrentFilterType = CollaborationController.FilterType.All;
        }

        public override void OnToolClosed()
        {
            ToolClosed?.Invoke();
            CollaborationController.CancelRequestAction?.Invoke();
            UnsubscribeInteraction();
        }

        public void SubscribeInteraction()
        {
            #if VR_MODE
            VRInteractionController.SubscribeSingleActivate(this, OnVRControllerSingleActivated);
            VRInteractionController.SubscribeControllerMoved(this, OnVRControllerMoved);
            #else
            InteractionController.SubscribePointerMove(this, OnPointerMove);
            InteractionController.SubscribeTap(this, TapAction);
            #endif
        }

        #if VR_MODE
        private void OnVRControllerMoved(Ray ray, int instanceId)
        {
            _ = RayCast(ray, false, instanceId);
        }

        private void OnVRControllerSingleActivated(Ray ray, int instanceId)
        {
            _ = RayCast(ray, true, instanceId);
        }
        #endif


        public void UnsubscribeInteraction()
        {
            #if VR_MODE
            VRInteractionController.UnsubscribeSingleActivate(this);
            VRInteractionController.UnsubscribeControllerMoved(this);
            #else
            InteractionController.UnsubscribePointerMove(this);
            InteractionController.UnsubscribeTap(this);
            #endif
        }

#if !VR_MODE
        private void TapAction(Vector3 position)
        {
            //Raycast again to get final position -> useful for Mobile touch screen
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            _ = RayCast(ray, true);
        }

        private void OnPointerMove(Vector3 position)
        {
            //Raycast
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            _ = RayCast(ray);
        }
#endif

        private async Task RayCast(Ray ray, bool isFinalPosition = false, int? instanceId = null)
        {
            var raycastResult = await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, m_StreamingModelController.ActiveCamera.farClipPlane, RaycastOptions.ExcludeHiddenInstances | RaycastOptions.ExcludeNormalFromResult);
            if (raycastResult.InstanceId == InstanceId.None)
            {
                OnNewAnnotationPositionDefining?.Invoke(null, isFinalPosition, instanceId, null);
                return;
            }

            Vector3? hitPoint = raycastResult.Point.ToVector3();
            if (Physics.Raycast(ray, out var hit, m_StreamingModelController.ActiveCamera.farClipPlane, m_UILayerMask))
            {
                var uiRaycastPoint = hit.point;
                        
                // Calculate distances along the ray using dot product
                float uiDistance = Vector3.Dot(uiRaycastPoint - ray.origin, ray.direction);
                float stageDistance = Vector3.Dot(hitPoint.Value - ray.origin, ray.direction);

                bool isUIInFront = uiDistance < stageDistance;

                if (isUIInFront)
                {
                    OnNewAnnotationPositionDefining?.Invoke(null, isFinalPosition, instanceId, null);
                    return;
                }
            }

            // Resolve the hit model only on the final placement (not on every hover/move), so the marker
            // anchors to the model it's actually placed on and we avoid a per-move hierarchy scan.
            Transform hitModel = isFinalPosition ? StreamingModel.FindByModelStreamId(raycastResult.ModelId)?.transform : null;
            OnNewAnnotationPositionDefining?.Invoke(hitPoint, isFinalPosition, instanceId, hitModel);
        }
    }
}
