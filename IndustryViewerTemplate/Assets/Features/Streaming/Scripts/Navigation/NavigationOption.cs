using UnityEngine;
using UnityEngine.Localization;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public abstract class NavigationOption : MonoBehaviour
    {
        public LocalizedString NavigationName => navigationOptionUIComponent?.NavigationName;
        public NavigationOptionUI NavigationOptionUIComponent => navigationOptionUIComponent;
        public Camera NavigationCamera => navigationCamera;

        [SerializeField]
        protected Camera navigationCamera;
        
        [SerializeField]
        protected NavigationOptionUI navigationOptionUIComponent;
        
        public abstract void Initialize();
        
        public abstract void Uninitialize();

        public abstract void OnNavigationOptionEnable();
        
        public abstract void OnNavigationOptionDisable();

        public abstract bool IsSupported();
        
        public abstract GameObject GetNavigationGameObject();

        public abstract void SetDefaultView();
        
        public abstract void FocusToPoint(DoubleBounds bounds);
        
        public abstract void TranslateTo(Vector3 position, Quaternion rotation);

        // Restores a saved camera viewpoint (world position + rotation). Defaults to a teleport;
        // orbit overrides this to also set its look-at pivot, otherwise a plain teleport leaves the
        // orbit pivot stale and the view snaps when the user next orbits.
        public virtual void FocusToSavedView(Vector3 position, Quaternion rotation) => TranslateTo(position, rotation);

        public abstract void FollowPresenter(GameObject presenter);
    }
}
