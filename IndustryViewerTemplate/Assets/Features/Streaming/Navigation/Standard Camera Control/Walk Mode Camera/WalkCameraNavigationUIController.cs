using Unity.AppUI.UI;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    public class WalkCameraNavigationUIController : NavigationJoysticksOptionUI
    {
        const string k_MoveSensitivitySlider = "MoveSensitivitySlider";
        const string k_RotationSensitivitySlider = "RotationSensitivitySlider";
        const string k_CameraHeightSlider = "CameraHeightSlider";

        [SerializeField]
        private WalkCameraNavigationController m_WalkCameraNavigationController;

        [SerializeField]
        private WalkCameraInputSystemController m_CameraInputSystemController;

        private float m_CameraHeight;
        private TouchSliderFloat m_MoveSensitivitySlider;
        private TouchSliderFloat m_RotationSensitivitySlider;
        private TouchSliderFloat m_CameraHeightSlider;

        protected override void OnEnable()
        {
            base.m_baseCameraInputSystemController = m_CameraInputSystemController;
            base.OnEnable();

            InAppSettings.SettingsPanelShown += SettingsPanelUp;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        protected override void InitialUI(VisualElement panel)
        {
            base.InitialUI(panel);

            m_MoveSensitivitySlider = WireSensitivitySlider(panel, k_MoveSensitivitySlider,
                m_CameraInputSystemController.MoveSensitivity, m_CameraInputSystemController.UpdateMoveSensitivity);
            m_RotationSensitivitySlider = WireSensitivitySlider(panel, k_RotationSensitivitySlider,
                m_CameraInputSystemController.RotateSensitivity, m_CameraInputSystemController.UpdateRotateSensitivity);

            m_CameraHeight = m_CameraInputSystemController.WalkModeMoveController.CharacterHeight;
            m_CameraHeightSlider = WireSensitivitySlider(panel, k_CameraHeightSlider, m_CameraHeight, value =>
            {
                m_CameraInputSystemController.WalkModeMoveController.CharacterHeight = value;
                m_CameraHeight = value;
            });
        }
    }
}
