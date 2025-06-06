using System.Linq;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using Unity.Industry.Viewer.AppSettings;

namespace Unity.Industry.Viewer.Navigation.WalkModeCamera
{
    public class WalkCameraNavigationUIController : NavigationOptionUI
    {
        const string k_MoveSensitivitySlider = "MoveSensitivitySlider";
        const string k_RotationSensitivitySlider = "RotationSensitivitySlider";
        const string k_CameraHeightSlider = "CameraHeightSlider";
        const string k_JoystickToggle = "JoystickToggle";
        const string k_LeftStickBackground = "left_stick_background";
        const string k_LeftStickHandle = "left_stick";
        const string k_RightStickBackground = "right_stick_background";
        const string k_RightStickHandle = "right_stick";
        const string k_WalkCameraUI = "CameraTouchControlUI";

        [SerializeField]
        private WalkCameraNavigationController m_WalkCameraNavigationController;
        [SerializeField]
        private WalkCameraInputSystemController m_CameraInputSystemController;
        private float m_CameraHeight;

        private Color m_SemiTransparent = new Color(1, 1, 1, 0.1f);

        [Header("Touch Control Joysticks")]
        [SerializeField]
        UIDocument m_UIDocument;

        VisualElement m_ControllerUI;
        GamepadStick m_LeftStick, m_RightStick;
        StickSettings m_LeftStickSettings, m_RightStickSettings;

        [Header("Left Stick")]
        [SerializeField]
        private float initialLeftStickMultiplier = 1f;
        [SerializeField]
        private bool invertLeftStickYAxis = false;
        [SerializeField]
        private bool invertLeftStickXAxis = false;

        [Header("Right Stick")]
        [SerializeField]
        private float initialRightStickMultiplier = 1f;
        [SerializeField]
        private bool invertRightStickYAxis = false;
        [SerializeField]
        private bool invertRightStickXAxis = false;

        private TouchSliderFloat m_MoveSensitivitySlider;
        private TouchSliderFloat m_RotationSensitivitySlider;
        private TouchSliderFloat m_CameraHeightSlider;
        private AppUI.UI.Toggle m_JoystickToggle;
        private bool showJoystick;
        private bool joystickToggled;
        private bool controlIsPaused;
        private bool originalJoyStickState;

        private void OnEnable()
        {
            if (m_ControllerUI == null)
            {
                InitializeTouchControlsUI();
            }
            else
            {
                m_LeftStick?.TurnOnUI();
                m_RightStick?.TurnOnUI();
                CheckForTouchScreen();
            }

            

            InAppSettings.SettingsPanelShown += SettingsPanelUp;
        }
        

        private void Start()
        {
            NavigationController.PauseCameraControl += PauseCameraControl;
            InputSystem.onDeviceChange += InputSystemOnDeviceChange;
        }

        private void OnDisable()
        {
            if (m_ControllerUI != null)
            {
                m_LeftStick.ResetUI();
                m_RightStick.ResetUI();
                m_ControllerUI.style.display = DisplayStyle.None;
            }

            
            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        private void OnDestroy()
        {
            NavigationController.PauseCameraControl -= PauseCameraControl;
            m_LeftStick?.UnregisterCallbacks();
            m_RightStick?.UnregisterCallbacks();
            InputSystem.onDeviceChange -= InputSystemOnDeviceChange;
        }

        private void PauseCameraControl(bool pause)
        {
            controlIsPaused = pause;
            if (pause)
            {
                originalJoyStickState = showJoystick;
                if (gameObject.activeSelf)
                {
                    m_ControllerUI.style.display = DisplayStyle.None;
                }
                showJoystick = false;
            }
            else
            {
                if (gameObject.activeSelf)
                {
                    m_ControllerUI.style.display = originalJoyStickState ? DisplayStyle.Flex : DisplayStyle.None;
                }
                showJoystick = originalJoyStickState;
            }
        }

        protected override void InitialUI(VisualElement panel)
        {
            m_MoveSensitivitySlider = panel.Q<TouchSliderFloat>(k_MoveSensitivitySlider);
            m_RotationSensitivitySlider = panel.Q<TouchSliderFloat>(k_RotationSensitivitySlider);
            m_CameraHeightSlider = panel.Q<TouchSliderFloat>(k_CameraHeightSlider);
            m_JoystickToggle = panel.Q<AppUI.UI.Toggle>(k_JoystickToggle);

            m_MoveSensitivitySlider.RegisterValueChangingCallback(OnMoveSensitivityChanging);
            m_MoveSensitivitySlider.RegisterValueChangedCallback(OnMoveSensitivityChanged);

            m_RotationSensitivitySlider.RegisterValueChangingCallback(OnRotateSensitivityChanging);
            m_RotationSensitivitySlider.RegisterValueChangedCallback(OnRotateSensitivityChanged);

            m_CameraHeightSlider.RegisterValueChangingCallback(OnCameraHeightChanging);
            m_CameraHeightSlider.RegisterValueChangedCallback(OnCameraHeightChanged);

            m_JoystickToggle.RegisterValueChangedCallback(evt =>
            {
                showJoystick = evt.newValue;
                joystickToggled = true;
                CheckForTouchScreen();
            });

            m_JoystickToggle.SetValueWithoutNotify(showJoystick);

            m_JoystickToggle.SetEnabled(!controlIsPaused);
            
            m_MoveSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.MoveSensitivity);
            m_RotationSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.RotateSensitivity);
            m_CameraHeight = m_CameraInputSystemController.WalkModeMoveController.CharacterHeight;
            m_CameraHeightSlider.SetValueWithoutNotify(m_CameraHeight);
        }

        private void OnRotateSensitivityChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateRotateSensitivity(evt.newValue);
        }

        private void OnRotateSensitivityChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateRotateSensitivity(evt.newValue);
        }

        private void OnMoveSensitivityChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateMoveSensitivity(evt.newValue);
        }

        private void OnMoveSensitivityChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.UpdateMoveSensitivity(evt.newValue);
        }

        private void OnCameraHeightChanging(ChangingEvent<float> evt)
        {
            m_CameraInputSystemController.WalkModeMoveController.CharacterHeight = evt.newValue;
            m_CameraHeight = evt.newValue;
        }

        private void OnCameraHeightChanged(ChangeEvent<float> evt)
        {
            m_CameraInputSystemController.WalkModeMoveController.CharacterHeight = evt.newValue;
            m_CameraHeight = evt.newValue;
        }

        public override void CreatePanel()
        {
            
        }

        protected override void ChangeCameraTitle(VisualTreeAsset titleTemplate)
        {
            var titleText = m_Title.Q<Text>("Title");
            titleText.ClearBinding("text");
            titleText.SetBinding("text", m_WalkCameraNavigationController.NavigationName);
        }

        private void InputSystemOnDeviceChange(InputDevice arg1, InputDeviceChange arg2)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }
            CheckForTouchScreen();
        }

        private void InitializeTouchControlsUI()
        {
            if (m_UIDocument == null) return;
            m_ControllerUI = m_UIDocument.rootVisualElement.Q<VisualElement>(k_WalkCameraUI);
            CheckForTouchScreen();

            m_LeftStickSettings.invertStickYAxis = invertLeftStickYAxis;
            m_LeftStickSettings.invertStickXAxis = invertLeftStickXAxis;
            m_LeftStickSettings.initialStickMultiplier = initialLeftStickMultiplier;
            m_LeftStickSettings.cameraInputSystemController = m_CameraInputSystemController;
            m_LeftStick = new GamepadStick(m_ControllerUI, k_LeftStickBackground, k_LeftStickHandle, false, m_LeftStickSettings);

            m_RightStickSettings.invertStickYAxis = invertRightStickYAxis;
            m_RightStickSettings.invertStickXAxis = invertRightStickXAxis;
            m_RightStickSettings.initialStickMultiplier = initialRightStickMultiplier;
            m_RightStickSettings.cameraInputSystemController = m_CameraInputSystemController;
            m_RightStick = new GamepadStick(m_ControllerUI, k_RightStickBackground, k_RightStickHandle, true, m_RightStickSettings);
        }

        private void CheckForTouchScreen()
        {
            if(controlIsPaused)return;
            if (showJoystick)
            {
                m_ControllerUI.style.display = DisplayStyle.Flex;
                joystickToggled = false;
                return;
            }

            if (!joystickToggled)
            {
                showJoystick = InputSystem.devices.Any(inputDevice => inputDevice is Touchscreen);
                m_JoystickToggle?.SetValueWithoutNotify(showJoystick);
            }
            m_ControllerUI.style.display = showJoystick ? DisplayStyle.Flex : DisplayStyle.None;
            joystickToggled = false;
        }
    }
}
