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
