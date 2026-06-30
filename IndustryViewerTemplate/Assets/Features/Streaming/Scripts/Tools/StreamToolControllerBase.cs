using System;
using UnityEngine;
using RuntimeGizmos;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public abstract class StreamToolControllerBase : MonoBehaviour
    {
        public Action ToolOpened;
        public Action ToolClosed;

        public abstract void OnToolOpened();

        public abstract void OnToolClosed();

        // Returns true when a world-space UI element should swallow this ray — i.e. a UI collider is
        // hit and either there is no stage hit, or the UI is in front of the stage hit. Shared by the
        // tool controllers that raycast the streamed model through world-space UI.
        protected static bool WorldSpaceUiBlocksRay(Ray ray, float maxDistance, bool hasStageHit, Vector3 stageHitPoint)
        {
            if (!Physics.Raycast(ray, out var hit, maxDistance, LayerMask.GetMask("UI")))
            {
                return false;
            }
            if (!hasStageHit)
            {
                return true;
            }
            float uiDistance = Vector3.Dot(hit.point - ray.origin, ray.direction);
            float stageDistance = Vector3.Dot(stageHitPoint - ray.origin, ray.direction);
            return uiDistance < stageDistance;
        }

        // Wires the shared "pause camera control while a gizmo axis is grabbed" handlers. Idempotent:
        // unsubscribes first so repeated calls don't double-subscribe.
        protected void SubscribeGizmoHandlers(TransformGizmo gizmo)
        {
            gizmo.OnHandlerSelected -= OnGizmoHandlerSelected;
            gizmo.OnHandlerReleased -= OnGizmoHandlerReleased;
            gizmo.OnHandlerSelected += OnGizmoHandlerSelected;
            gizmo.OnHandlerReleased += OnGizmoHandlerReleased;
        }

        protected void UnsubscribeGizmoHandlers(TransformGizmo gizmo)
        {
            gizmo.OnHandlerSelected -= OnGizmoHandlerSelected;
            gizmo.OnHandlerReleased -= OnGizmoHandlerReleased;
        }

        private void OnGizmoHandlerSelected()
        {
            NavigationController.PauseCameraControl?.Invoke(true);
        }

        private void OnGizmoHandlerReleased()
        {
            NavigationController.PauseCameraControl?.Invoke(false);
        }
    }
}
