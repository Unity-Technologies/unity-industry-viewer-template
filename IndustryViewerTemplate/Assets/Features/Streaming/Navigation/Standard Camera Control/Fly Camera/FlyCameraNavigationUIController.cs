using Unity.AppUI.UI;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using Unity.Industry.Viewer.Streaming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    public class FlyCameraNavigationUIController : NavigationJoysticksOptionUI
    {
        const string k_MoveSensitivitySlider = "MoveSensitivitySlider";
        const string k_RotationSensitivitySlider = "RotationSensitivitySlider";

        [SerializeField]
        private FlyCameraNavigationController m_FlyCameraNavigationController;

        [SerializeField]
        private FlyCameraInputSystemController m_CameraInputSystemController;

        private TouchSliderFloat m_MoveSensitivitySlider;
        private TouchSliderFloat m_RotationSensitivitySlider;

        protected override void OnEnable()
        {
            base.m_baseCameraInputSystemController = m_CameraInputSystemController;
            base.OnEnable();

            ShowOrCreateHomeButton();

            InAppSettings.SettingsPanelShown += SettingsPanelUp;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            HideHomeButton();

            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            DestroyHomeButton();
        }

        protected override void InitialUI(VisualElement panel)
        {
            base.InitialUI(panel);

            m_MoveSensitivitySlider = WireSensitivitySlider(panel, k_MoveSensitivitySlider,
                m_CameraInputSystemController.MoveSensitivity, m_CameraInputSystemController.UpdateMoveSensitivity);
            m_RotationSensitivitySlider = WireSensitivitySlider(panel, k_RotationSensitivitySlider,
                m_CameraInputSystemController.RotateSensitivity, m_CameraInputSystemController.UpdateRotateSensitivity);
        }
    }
}
