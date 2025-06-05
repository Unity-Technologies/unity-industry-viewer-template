using System;
using UnityEngine;
using System.Collections;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Streaming
{
    // This script manages the navigation options and player movement in a streaming session.
    // It handles the initialization, activation, and deactivation of different navigation options.
    // The script provides functionality for translating the player to a target position and rotation smoothly.
    // It includes event handlers for changing navigation options, following a presenter, and requesting the default home view.
    // The script integrates with Unity's MonoBehaviour for lifecycle management and coroutine handling.
    [DefaultExecutionOrder(90)]
    public class NavigationController : MonoBehaviour
    {
        public static Action<Vector3, Quaternion> PlayerTranslateTo;
        public static Action<bool> PauseCameraControl;
        public static Action<NavigationOption> OnNavigationOptionChanged;
        public static Action<NavigationOption> ChangeToNewNavigationOption;
        public static Action<DoubleBounds> FocusToPoint;
        public static Action<GameObject> FollowPresenter;
        public static Action RequestDefaultHomeView;
        
        public static NavigationOption CurrentNavigationOption => m_CurrentNavigationOption;
        public NavigationOption[] NavigationOptions => navigationOptions;
        
        public NavigationOption DefaultNavigationOption => defaultNavigationOption;
        
        [SerializeField]
        private NavigationOption defaultNavigationOption;
        
        [SerializeField]
        private NavigationOption[] navigationOptions;
        
        private static NavigationOption m_CurrentNavigationOption;

        private Coroutine m_TranslationCoroutine;
        
        private void Awake()
        {
            foreach (var navigationOption in navigationOptions)
            {
                navigationOption.Initialize();
                navigationOption.gameObject.SetActive(false);
            }
            
            if (defaultNavigationOption == null)
            {
                Debug.LogError("Default navigation option is not set.");
                gameObject.SetActive(false);
                return;
            }
        }

        private void Start()
        {
            ChangeToNewNavigationOption += SetNavigationOption;
            PlayerTranslateTo += OnPlayerRequestTranslateTo;
            FollowPresenter += OnFollowPresenter;
            FocusToPoint += OnFocusToPoint;
            RequestDefaultHomeView += OnRequestDefaultHomeView;
            SetNavigationOption(defaultNavigationOption);
            AssetsController.AssetSelected += OnAssetSelected;
        }

        private void OnDestroy()
        {
            ChangeToNewNavigationOption -= SetNavigationOption;
            PlayerTranslateTo -= OnPlayerRequestTranslateTo;
            FollowPresenter -= OnFollowPresenter;
            FocusToPoint -= OnFocusToPoint;
            RequestDefaultHomeView -= OnRequestDefaultHomeView;
            foreach (var navigationOption in navigationOptions)
            {
                navigationOption.Uninitialize();
            }
            AssetsController.AssetSelected -= OnAssetSelected;
        }
        
        private void OnFocusToPoint(DoubleBounds bounds)
        {
            if(m_CurrentNavigationOption.GetNavigationGameObject() == null) return;
            m_CurrentNavigationOption?.FocusToPoint(bounds);
        }
        
        private void OnAssetSelected(AssetInfo obj)
        {
            PauseCameraControl?.Invoke(false);
        }

        private void OnRequestDefaultHomeView()
        {
            CurrentNavigationOption?.SetDefaultView();
        }

        private void OnFollowPresenter(GameObject presenterObject)
        {
            var currentNavigationGameObject = m_CurrentNavigationOption.GetNavigationGameObject();
            if(currentNavigationGameObject == null) return;
            if (m_TranslationCoroutine != null)
            {
                StopCoroutine(m_TranslationCoroutine);
            }
            currentNavigationGameObject.transform.SetPositionAndRotation(presenterObject.transform.position,
                presenterObject.transform.rotation);
        }

        private void OnPlayerRequestTranslateTo(Vector3 targetPosition, Quaternion targetRotation)
        {
            var currentNavigationGameObject = m_CurrentNavigationOption.GetNavigationGameObject();
            if(currentNavigationGameObject == null) return;
            if (m_TranslationCoroutine != null)
            {
                StopCoroutine(m_TranslationCoroutine);
            }
            
            m_TranslationCoroutine = StartCoroutine(SmoothTranslateTo());
            
            IEnumerator SmoothTranslateTo(float duration = 1.0f)
            {
                PauseCameraControl?.Invoke(true);
                Vector3 startPosition = currentNavigationGameObject.transform.position;
                Quaternion startRotation = currentNavigationGameObject.transform.rotation;
                float elapsedTime = 0;

                while (elapsedTime < duration)
                {
                    var finalPos = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
                    var finalRot = Quaternion.Lerp(startRotation, targetRotation, elapsedTime / duration);
                    currentNavigationGameObject.transform.SetPositionAndRotation(finalPos, finalRot);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                currentNavigationGameObject.transform.SetPositionAndRotation(targetPosition, targetRotation);
                PauseCameraControl?.Invoke(false);
            }
        }

        private void SetNavigationOption(NavigationOption navigationOption)
        {
            if (m_CurrentNavigationOption != null)
            {
                m_CurrentNavigationOption.OnNavigationOptionDisable();
                m_CurrentNavigationOption.gameObject.SetActive(false);
                if(m_CurrentNavigationOption.NavigationCamera != navigationOption.NavigationCamera)
                {
                    m_CurrentNavigationOption.NavigationCamera?.gameObject.SetActive(false);
                }
            }
            
            m_CurrentNavigationOption = navigationOption;
            m_CurrentNavigationOption.gameObject.SetActive(true);
            if (m_CurrentNavigationOption.NavigationCamera != null)
            {
                m_CurrentNavigationOption.NavigationCamera.gameObject.SetActive(true);
            }
            m_CurrentNavigationOption.OnNavigationOptionEnable();
            OnNavigationOptionChanged?.Invoke(m_CurrentNavigationOption);
        }
    }
}
