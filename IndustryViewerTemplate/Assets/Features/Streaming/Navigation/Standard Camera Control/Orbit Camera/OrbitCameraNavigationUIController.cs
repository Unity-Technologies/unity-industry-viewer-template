using Unity.AppUI.UI;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Streaming;
using UnityEngine;
using UnityEngine.UIElements;

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

        private TouchSliderFloat m_OrbitSensitivitySlider;
        private TouchSliderFloat m_PanSensitivitySlider;
        private TouchSliderFloat m_ZoomSensitivitySlider;

        private void OnEnable()
        {
            ShowOrCreateHomeButton();

            InAppSettings.SettingsPanelShown += SettingsPanelUp;
        }

        private void OnDisable()
        {
            HideHomeButton();

            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        private void OnDestroy()
        {
            DestroyHomeButton();
        }

        protected override void InitialUI(VisualElement panel)
        {
            m_OrbitSensitivitySlider = WireSensitivitySlider(panel, k_OrbitSensitivitySlider,
                m_CameraInputSystemController.OrbitSensitivity, m_CameraInputSystemController.UpdateOrbitSensitivity);
            m_PanSensitivitySlider = WireSensitivitySlider(panel, k_PanSensitivitySlider,
                m_CameraInputSystemController.PanSensitivity, m_CameraInputSystemController.UpdatePanSensitivity);
            m_ZoomSensitivitySlider = WireSensitivitySlider(panel, k_ZoomSensitivitySlider,
                m_CameraInputSystemController.ZoomSensitivity, m_CameraInputSystemController.UpdateZoomSensitivity);
        }
    }
}
