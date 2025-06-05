using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Navigation.OrbitCamera
{
    public class OrbitCameraNavigationController : NavigationOption
    {
        [SerializeField]
        private OrbitCameraInputSystemController cameraController;

        DoubleBounds m_CurrentBounds;
        
        public override void Initialize()
        {
            navigationOptionUIComponent ??= GetComponent<NavigationOptionUI>();
            StreamingModelController.BoundsUpdated += OnBoundsUpdated;
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
            cameraController?.HomeView();
        }

        public override void FocusToPoint(DoubleBounds bounds)
        {
            cameraController.SetLookAt(bounds, true);
        }

        private void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate)
        {
            m_CurrentBounds = bounds;
            if (!skipCameraUpdate)
            {
                cameraController.UpdateView(bounds);
                cameraController.SetView(bounds);
            }
            else
            {
                cameraController.UpdateView(bounds);
                cameraController.SetBoundSettings(bounds);
            }
        }
    }
}
