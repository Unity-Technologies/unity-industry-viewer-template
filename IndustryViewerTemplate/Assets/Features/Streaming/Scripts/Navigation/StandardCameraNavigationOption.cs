using UnityEngine;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    // Shared base for the standard (non-AR / non-VR) camera navigation options: Fly, Orbit and Walk.
    // It centralises the navigation-option lifecycle that is identical across those modes — the
    // bounds-update subscription, navigation UI lookup, support reporting, observer registration and
    // the navigation GameObject accessor.
    //
    // Mode-specific camera behaviour (responding to bounds, default view, focus, translate, follow)
    // stays in the derived classes: each mode drives a different camera-controller type (Fly and Walk
    // derive from CameraInputSystemController, Orbit uses its own controller), so those calls cannot
    // be shared through a common type. Serialized camera references also remain on the derived classes,
    // which keeps existing scene/prefab wiring intact.
    public abstract class StandardCameraNavigationOption : NavigationOption
    {
        protected DoubleBounds m_CurrentBounds;

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
            StreamingModelController.AddObserver?.Invoke(navigationCamera);
        }

        public override void OnNavigationOptionDisable() { }

        public override bool IsSupported()
        {
            return true;
        }

        public override GameObject GetNavigationGameObject()
        {
            return navigationCamera.gameObject;
        }

        protected abstract void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate);
    }
}
