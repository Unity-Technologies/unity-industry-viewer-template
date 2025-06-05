using Unity.Cloud.HighPrecision.Runtime;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    public class FlyCameraNavigationController : NavigationOption
    {
        [SerializeField]
        private FlyCameraInputSystemController cameraController;

        DoubleBounds m_CurrentBounds;
        
        public override void Initialize()
        {
            StreamingModelController.BoundsUpdated += OnBoundsUpdated;
            navigationOptionUIComponent ??= GetComponent<NavigationOptionUI>();
        }
        
        public override void Uninitialize()
        {
            StreamingModelController.BoundsUpdated -= OnBoundsUpdated;
        }

        public override void OnNavigationOptionEnable()
        {
            NavigationController.RequestDefaultHomeView -= SetDefaultView;
            NavigationController.RequestDefaultHomeView += SetDefaultView;
            StreamingModelController.AddObserver?.Invoke(navigationCamera);
        }

        public override void OnNavigationOptionDisable()
        {
            NavigationController.RequestDefaultHomeView -= SetDefaultView;
        }

        public override bool IsSupported()
        {
            return true;
        }

        public override GameObject GetNavigationGameObject()
        {
            return navigationCamera.gameObject;
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

        private void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate)
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
