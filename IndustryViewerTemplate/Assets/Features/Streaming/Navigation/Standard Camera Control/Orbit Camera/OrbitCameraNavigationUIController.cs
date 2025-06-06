using UnityEngine;
using Unity.AppUI.UI;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Streaming;
using System.Linq;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Navigation.OrbitCamera
{
    public class OrbitCameraNavigationUIController : NavigationOptionUI
    {
        private const string k_OrbitSensitivitySlider = "OrbitSensitivitySlider";
        private const string k_PanSensitivitySlider = "PanSensitivitySlider";
        private const string k_ZoomSensitivitySlider = "ZoomSensitivitySlider";
        
        [SerializeField]
        private OrbitCameraNavigationController m_OrbitCameraNavigationController;
        
        [SerializeField]
        private OrbitCameraInputSystemController m_CameraInputSystemController;
        
        private IconButton m_HomeButton;
        
        private TouchSliderFloat m_OrbitSensitivitySlider;
        private TouchSliderFloat m_PanSensitivitySlider;
        private TouchSliderFloat m_ZoomSensitivitySlider;
        
        private void OnEnable()
        {
            if (m_HomeButton == null)
            {
                var UIDocument = SharedUIManager.Instance.AssetsUIDocument;
                var streamingContainer = UIDocument.rootVisualElement.Q<VisualElement>(StreamingUtils.StreamingPanelName);
                var bottomLeftContainer = streamingContainer.Q<VisualElement>(StreamingUtils.BottomLeftContainerName);
                
                m_HomeButton = new IconButton()
                {
                    icon = "camera-overhead"
                };
                m_HomeButton.AddToClassList(StreamingUtils.BottomLeftButtonStyleName);
                    
                m_HomeButton.clicked += OnHomeButtonClicked;
                
                bottomLeftContainer.Insert(bottomLeftContainer.childCount, m_HomeButton);
                
            } else 
            {
                m_HomeButton.style.display = DisplayStyle.Flex;
            }

            InAppSettings.SettingsPanelShown += SettingsPanelUp;
        }
        
        private void OnDisable()
        {
            if(m_HomeButton != null)
            {
                m_HomeButton.style.display = DisplayStyle.None;
            }

            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        private void OnDestroy()
        {
            if(m_HomeButton != null)
            {
                m_HomeButton.clicked -= OnHomeButtonClicked;
                m_HomeButton.RemoveFromHierarchy();
            }
        }
        
        private void OnHomeButtonClicked()
        {
            NavigationController.RequestDefaultHomeView?.Invoke();
        }
        
        protected override void InitialUI(VisualElement panel)
        {
            m_OrbitSensitivitySlider = panel.Q<TouchSliderFloat>(k_OrbitSensitivitySlider);
            m_PanSensitivitySlider = panel.Q<TouchSliderFloat>(k_PanSensitivitySlider);
            m_ZoomSensitivitySlider = panel.Q<TouchSliderFloat>(k_ZoomSensitivitySlider);
            
            m_OrbitSensitivitySlider.RegisterValueChangingCallback(OnOrbitSensitivityChanging);
            m_OrbitSensitivitySlider.RegisterValueChangedCallback(OnOrbitSensitivityChanged);
            
            m_PanSensitivitySlider.RegisterValueChangingCallback(OnPanSensitivityChanging);
            m_PanSensitivitySlider.RegisterValueChangedCallback(OnPanSensitivityChanged);
            
            m_ZoomSensitivitySlider.RegisterValueChangingCallback(OnZoomSensitivityChanging);
            m_ZoomSensitivitySlider.RegisterValueChangedCallback(OnZoomSensitivityChanged);
            
            m_OrbitSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.OrbitSensitivity);
            m_PanSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.PanSensitivity);
            m_ZoomSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.ZoomSensitivity);
        }

        public override void CreatePanel()
        {
            
        }

        protected override void ChangeCameraTitle(VisualTreeAsset titleTemplate)
        {
            var titleText = m_Title.Q<Text>("Title");
            titleText.ClearBinding("text");
            titleText.SetBinding("text", m_OrbitCameraNavigationController.NavigationName);
        }

        private void OnZoomSensitivityChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateZoomSensitivity(evt.newValue);
        }

        private void OnZoomSensitivityChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateZoomSensitivity(evt.newValue);
        }

        private void OnPanSensitivityChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.UpdatePanSensitivity(evt.newValue);
        }

        private void OnPanSensitivityChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.UpdatePanSensitivity(evt.newValue);
        }

        private void OnOrbitSensitivityChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateOrbitSensitivity(evt.newValue);
        }

        private void OnOrbitSensitivityChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateOrbitSensitivity(evt.newValue);
        }
    }
}
