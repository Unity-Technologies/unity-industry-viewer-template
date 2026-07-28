using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Navigation.OrbitCamera
{
    public class OrbitCameraNavigationController : StandardCameraNavigationOption
    {
        [SerializeField]
        private OrbitCameraInputSystemController cameraController;

        [SerializeField]
        private FreeOrbitCamera freeOrbitCamera;

        public override void OnNavigationOptionEnable()
        {
            NavigationController.RequestDefaultHomeView -= SetDefaultView;
            NavigationController.RequestDefaultHomeView += SetDefaultView;
            base.OnNavigationOptionEnable();
        }

        public override void OnNavigationOptionDisable()
        {
            NavigationController.RequestDefaultHomeView -= SetDefaultView;
        }

        public override void SetDefaultView()
        {
            cameraController?.HomeView();
        }

        public override void FocusToPoint(DoubleBounds bounds)
        {
            cameraController.SetLookAt(bounds, true);
        }

        public override void TranslateTo(Vector3 position, Quaternion rotation)
        {
            if(GetNavigationGameObject() == null) return;
            freeOrbitCamera.TranslateTo(GetNavigationGameObject(), position, rotation);
        }

        public override void FocusToSavedView(Vector3 position, Quaternion rotation)
        {
            if (GetNavigationGameObject() == null) return;
            // Restore the exact camera pose, and set the orbit look-at pivot along the view direction
            // (roughly at the model) so orbiting stays stable instead of snapping around a stale pivot.
            Vector3 forward = rotation * Vector3.forward;
            // Guard against bounds not yet initialised (a default DoubleBounds is a zero-size box at the
            // origin): fall back to a bounded pivot distance ahead of the camera instead of the origin.
            Bounds bounds = (Bounds)m_CurrentBounds;
            float distance = bounds.size.sqrMagnitude > 1e-6f
                ? Mathf.Max(0.5f, Vector3.Distance(position, bounds.center))
                : 5f;
            cameraController.RestoreView(position, position + forward * distance);
        }

        public override void FollowPresenter(GameObject presenterObject)
        {
            if (GetNavigationGameObject() == null) return;
            freeOrbitCamera.FollowPresenter(presenterObject, GetNavigationGameObject());
        }

        protected override void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate)
        {
            m_CurrentBounds = bounds;
            if (!skipCameraUpdate)
            {
                cameraController.UpdateView(bounds);
                cameraController.SetView(bounds, NavigationController.StartingPosition.HasValue);
            }
            else
            {
                cameraController.UpdateView(bounds);
                cameraController.SetBoundSettings(bounds);
            }
        }
    }
}
