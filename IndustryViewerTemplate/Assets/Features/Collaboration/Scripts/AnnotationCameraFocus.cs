using System.Linq;
using UnityEngine;
using Unity.Cloud.Collaboration;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Collaboration
{
    // Teleports the navigation camera to the viewpoint saved on a collaboration annotation.
    // Spatial annotations store the creator's camera pose (world position + Euler rotation) in
    // ICameraDetails; clicking a comment/annotation focuses the active camera onto that pose.
    // Reuses NavigationController.FocusToSavedView, which restores the pose per nav mode (orbit
    // also sets its look-at pivot) and is a safe no-op where there is no NavigationController
    // (e.g. the Main scenes) and for AR navigation.
    public static class AnnotationCameraFocus
    {
        public static void FocusToAnnotationCamera(IAnnotation annotation)
        {
            // Per-model anchor (multi-model layout): if the annotation stored a model-local pose, resolve
            // the model and focus relative to it so the viewpoint follows the model when it's moved. The
            // Local frame has no camera rotation, so reconstruct it as a look-at toward the marker point
            // (both points are model-local, so the orientation follows the model's rotation too).
            var spatial = annotation?.Attachments?
                .OfType<ISpatial3DAttachment>()
                .FirstOrDefault(a => a.Local != null && a.Camera != null);
            if (spatial != null)
            {
                Transform modelChild = StreamingModel.FindModelTransformByAnchorId(spatial.Local.ParentId);
                if (modelChild != null)
                {
                    Vector3 camWorld = modelChild.TransformPoint(new Vector3(
                        spatial.Local.CameraPosition.X, spatial.Local.CameraPosition.Y, spatial.Local.CameraPosition.Z));
                    Vector3 markerWorld = modelChild.TransformPoint(new Vector3(
                        spatial.Local.Position.X, spatial.Local.Position.Y, spatial.Local.Position.Z));
                    Vector3 forward = markerWorld - camWorld;
                    if (forward.sqrMagnitude > 1e-6f)
                    {
                        // Reconstruct orientation as a look-at toward the marker, using the model's up so the
                        // horizon follows the model's roll; swap to the model's forward as the up reference
                        // when the view is near-parallel to that up (avoids a degenerate LookRotation, e.g.
                        // looking straight down at the marker).
                        Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized, modelChild.up)) > 0.99f
                            ? modelChild.forward
                            : modelChild.up;
                        Quaternion camWorldRot = Quaternion.LookRotation(forward.normalized, up);
                        NavigationController.FocusToSavedView?.Invoke(camWorld, camWorldRot);
                        ApplyFieldOfView(spatial.Camera);
                        return;
                    }
                }
            }

            var camera = GetCameraDetails(annotation);
            if (camera == null) return;

            // Root-relative fallback (single-model / web / legacy): the pose is stored in the model root's
            // frame (same as the marker). Convert back to world so it stays correct wherever the model is placed.
            Vector3 localPos = new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z);
            Quaternion localRot = Quaternion.Euler(camera.Rotation.X, camera.Rotation.Y, camera.Rotation.Z);

            Transform modelRoot = TransformController.Instance != null ? TransformController.Instance.transform : null;
            Vector3 worldPos = modelRoot != null ? modelRoot.TransformPoint(localPos) : localPos;
            Quaternion worldRot = modelRoot != null ? modelRoot.rotation * localRot : localRot;

            NavigationController.FocusToSavedView?.Invoke(worldPos, worldRot);
            ApplyFieldOfView(camera);
        }

        // Restores the saved field of view (perspective only), like the SDK's spatial-attachment sample.
        private static void ApplyFieldOfView(ICameraDetails camera)
        {
            if (camera == null || !camera.FieldOfView.HasValue) return;
            var option = NavigationController.CurrentNavigationOption;
            // Skip when the active navigation can't be focused (e.g. AR placement has no nav GameObject, so
            // the view didn't move) — otherwise we'd distort the live camera's FOV without a pose change.
            if (option == null || option.GetNavigationGameObject() == null) return;
            var navCamera = option.NavigationCamera;
            if (navCamera != null) navCamera.fieldOfView = camera.FieldOfView.Value;
        }

        // The project writes the camera onto the spatial attachment; also honour a camera set
        // directly on the annotation. Returns null when the annotation carries no viewpoint.
        private static ICameraDetails GetCameraDetails(IAnnotation annotation)
        {
            if (annotation == null) return null;
            if (annotation.Camera != null) return annotation.Camera;
            return annotation.Attachments?
                .OfType<ISpatial3DAttachment>()
                .FirstOrDefault(attachment => attachment.Camera != null)?.Camera;
        }
    }
}
