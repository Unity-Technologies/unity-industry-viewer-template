using System.Linq;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    public class FlyCameraNavigationUIController : NavigationOptionUI
    {
        const string k_MoveSensitivitySlider = "MoveSensitivitySlider";
        const string k_RotationSensitivitySlider = "RotationSensitivitySlider";
        const string k_JoystickToggle = "JoystickToggle";
        const string k_LeftStickBackground = "left_stick_background";
        const string k_LeftStickHandle = "left_stick";
        const string k_FlyCameraUI = "CameraTouchControlUI";

        [SerializeField]
        private FlyCameraNavigationController m_FlyCameraNavigationController;
        
        [SerializeField]
        private FlyCameraInputSystemController m_CameraInputSystemController;
        
        [Header("Touch Control Joysticks")]
        [SerializeField]
        UIDocument m_UIDocument;
        [SerializeField]
        bool forceJoystickOnDebug;

        VisualElement m_ControllerUI;
        GamepadStick m_LeftStick;
        StickSettings m_LeftStickSettings;

        [Header("Left Stick")]
        [SerializeField]
        private float initialLeftStickMultiplier = 1f;
        [SerializeField]
        private bool invertLeftStickYAxis = false;
        [SerializeField]
        private bool invertLeftStickXAxis = false;

        private IconButton m_HomeButton;
        
        private TouchSliderFloat m_MoveSensitivitySlider;
        private TouchSliderFloat m_RotationSensitivitySlider;
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
                CheckForTouchScreen();
            }
            
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
                m_ControllerUI.style.display = DisplayStyle.None;
            }

            if (m_HomeButton != null)
            {
                m_HomeButton.style.display = DisplayStyle.None;
            }
            
            InAppSettings.SettingsPanelShown -= SettingsPanelUp;
            if (m_SettingsPanel != null && m_SettingsPanel.Contains(m_Title))
                m_SettingsPanel.Q<ScrollView>().Remove(m_Title);
        }

        private void OnDestroy()
        {
            NavigationController.PauseCameraControl -= PauseCameraControl;
            m_LeftStick?.UnregisterCallbacks();

            InputSystem.onDeviceChange -= InputSystemOnDeviceChange;
            
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
            m_JoystickToggle = panel.Q<AppUI.UI.Toggle>(k_JoystickToggle);

            m_JoystickToggle.RegisterValueChangedCallback(evt =>
            {
                showJoystick = evt.newValue;
                joystickToggled = true;
                CheckForTouchScreen();
            });

            m_JoystickToggle.SetValueWithoutNotify(showJoystick);
            
            m_JoystickToggle.SetEnabled(!controlIsPaused);
            
            m_MoveSensitivitySlider.RegisterValueChangingCallback(OnMoveSensitivityChanging);
            m_MoveSensitivitySlider.RegisterValueChangedCallback(OnMoveSensitivityChanged);
            
            m_RotationSensitivitySlider.RegisterValueChangingCallback(OnRotateSensitivityChanging);
            m_RotationSensitivitySlider.RegisterValueChangedCallback(OnRotateSensitivityChanged);
            
            m_MoveSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.MoveSensitivity);
            m_RotationSensitivitySlider.SetValueWithoutNotify(m_CameraInputSystemController.RotateSensitivity);
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

        public override void CreatePanel()
        {
            
        }

        protected override void ChangeCameraTitle(VisualTreeAsset titleTemplate)
        {
            var titleText = m_Title.Q<Text>("Title");
            titleText.ClearBinding("text");
            titleText.SetBinding("text", m_FlyCameraNavigationController.NavigationName);
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
            m_ControllerUI = m_UIDocument.rootVisualElement.Q<VisualElement>(k_FlyCameraUI);
            CheckForTouchScreen();

            m_LeftStickSettings.invertStickYAxis = invertLeftStickYAxis;
            m_LeftStickSettings.invertStickXAxis = invertLeftStickXAxis;
            m_LeftStickSettings.initialStickMultiplier = initialLeftStickMultiplier;
            m_LeftStickSettings.cameraInputSystemController = m_CameraInputSystemController;
            m_LeftStick = new GamepadStick(m_ControllerUI, k_LeftStickBackground, k_LeftStickHandle, false, m_LeftStickSettings);
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
