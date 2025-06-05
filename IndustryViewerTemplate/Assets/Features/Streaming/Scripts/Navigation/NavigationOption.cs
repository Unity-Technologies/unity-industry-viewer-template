using UnityEngine;
using UnityEngine.Localization;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(100)]
    public abstract class NavigationOption : MonoBehaviour
    {
        public LocalizedString NavigationName => navigationName;
        public NavigationOptionUI NavigationOptionUIComponent => navigationOptionUIComponent;
        
        public Camera NavigationCamera => navigationCamera;
        
        [SerializeField]
        private LocalizedString navigationName;
        
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
    }
}
