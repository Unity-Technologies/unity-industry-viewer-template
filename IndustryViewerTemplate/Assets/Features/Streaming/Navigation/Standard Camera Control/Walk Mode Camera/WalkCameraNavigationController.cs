using Unity.Cloud.HighPrecision.Runtime;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    public class WalkCameraNavigationController : StandardCameraNavigationOption
    {
        [SerializeField]
        private WalkCameraInputSystemController cameraController;

        [SerializeField]
        private WalkModeCameraController walkModeCameraController;

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
            walkModeCameraController.TranslateTo(GetNavigationGameObject(), position, rotation);
        }

        public override void FollowPresenter(GameObject presenterObject)
        {
            if (GetNavigationGameObject() == null) return;
            if (presenterObject == null)
            {
                Debug.Log("Presenter object is null, cannot follow.");
                return;
            }
            walkModeCameraController.ApplyNewPositionRotation(presenterObject.transform.position, presenterObject.transform.rotation);
        }

        protected override void OnBoundsUpdated(DoubleBounds bounds, bool multipleBounds)
        {
            m_CurrentBounds = bounds;
            if (!multipleBounds)
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
