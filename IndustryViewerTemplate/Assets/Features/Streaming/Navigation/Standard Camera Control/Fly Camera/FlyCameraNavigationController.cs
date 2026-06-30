using Unity.Cloud.HighPrecision.Runtime;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    public class FlyCameraNavigationController : StandardCameraNavigationOption
    {
        [SerializeField]
        private FlyCameraInputSystemController cameraController;

        [SerializeField]
        FreeFlyCamera freeFlyCamera;

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
            if(m_CurrentBounds == default) return;
            cameraController.SetView(m_CurrentBounds);
        }

        public override void FocusToPoint(DoubleBounds bounds)
        {
            cameraController.GoTo(bounds);
        }

        public override void TranslateTo(Vector3 position, Quaternion rotation)
        {
            if(GetNavigationGameObject() == null) return;
            freeFlyCamera.TranslateTo(GetNavigationGameObject(), position, rotation);
        }

        public override void FollowPresenter(GameObject presenterObject)
        {
            if(GetNavigationGameObject() == null) return;
            freeFlyCamera.FollowPresenter(presenterObject, GetNavigationGameObject());
        }

        protected override void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate)
        {
            m_CurrentBounds = bounds;
            if (!skipCameraUpdate)
            {
                cameraController.SetView(bounds);
            }
            else
            {
                cameraController.UpdateView(bounds);
                cameraController.SetSpeedSettings(bounds);
            }
        }
    }
}
