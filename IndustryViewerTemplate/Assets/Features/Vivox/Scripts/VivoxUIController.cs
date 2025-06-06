using System.Collections.Generic;
using Unity.Industry.Viewer.AppSettings;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Vivox;
using Avatar = Unity.AppUI.UI.Avatar;
using System.Linq;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Vivox
{
    public class VivoxUIController : MonoBehaviour
    {
        private const string k_AvatarName = "IdentityAvatar";
        private const string k_PresentationButtonName = "PresentationModeButton";
        private const string k_InputDeviceDropdownName = "InputDeviceDropdown";
        private const string k_InputEffectiveDeviceLabelName = "InputEffectiveDeviceLabel";
        private const string k_InputVolumeSliderName = "InputVolumeSlider";
        private const string k_OutputDeviceDropdownName = "OutputDeviceDropdown";
        private const string k_OutputEffectiveDeviceLabelName = "OutputEffectiveDeviceLabel";
        private const string k_OutputVolumeSliderName = "OutputVolumeSlider";

        private const int k_Volume = 50;
        
        private UIDocument m_UIDocument;
        
        [SerializeField]
        private StyleSheet m_StyleSheet;
        
        private MicComponent m_MicButton;

        [SerializeField] private VisualTreeAsset m_VivoxSettingsUI;
        
        private Dropdown m_InputDeviceDropdown;
        private Dropdown m_OutputDeviceDropdown;
        private Text m_InputEffectiveDeviceLabel;
        private Text m_OutputEffectiveDeviceLabel;
        private TouchSliderFloat m_InputVolumeSlider;
        private TouchSliderFloat m_OutputVolumeSlider;

        #region Localisation

        [SerializeField]
        private LocalizedString m_MuteLocalizedString;

        [SerializeField]
        private LocalizedString m_UnmuteLocalizedString;
        
        VisualElement m_VivoxSettingsUIPanel;
        private VisualElement m_Title;

        #endregion
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            VivoxController.ChannelJoined += OnChannelJoined;
            VivoxController.ChannelLeft += OnChannelLeft;
            VivoxController.ParticipantAudioEnergyChanged += OnParticipantAudioEnergyChanged;
        }

        private void OnDestroy()
        {
            InAppSettings.SettingsPanelShow -= OnSettingsPanelShow;
            VivoxController.ChannelJoined -= OnChannelJoined;
            VivoxController.ChannelLeft -= OnChannelLeft;
            VivoxController.ParticipantAudioEnergyChanged -= OnParticipantAudioEnergyChanged;
            
            if (SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Remove(m_StyleSheet);
            }
            
            if(m_MicButton == null) return;
            m_MicButton.RemoveFromHierarchy();
            m_MicButton.clicked -= OnMicButtonClicked;
        }

        private void OnParticipantAudioEnergyChanged(double energy)
        {
            if(m_MicButton == null) return;
            m_MicButton.SetVoiceLevel((float)energy);
        }

        private void OnChannelLeft()
        {
            InAppSettings.SettingsPanelShow -= OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed -= OnSettingsPanelDismissed;
            VivoxService.Instance.AvailableInputDevicesChanged -= InstanceOnAvailableInputDevicesChanged;
            VivoxService.Instance.AvailableOutputDevicesChanged -= InstanceOnAvailableOutputDevicesChanged;
            VivoxService.Instance.EffectiveInputDeviceChanged -= OnEffectInputDeviceChanged;
            VivoxService.Instance.EffectiveOutputDeviceChanged -= OnEffectOutputDeviceChanged;
            m_MicButton?.RemoveFromHierarchy();
            m_VivoxSettingsUIPanel?.RemoveFromHierarchy();
            m_Title?.RemoveFromHierarchy();
            OnSettingsPanelDismissed();
        }

        private void OnChannelJoined()
        {
            InAppSettings.SettingsPanelShow -= OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed -= OnSettingsPanelDismissed;
            VivoxService.Instance.AvailableOutputDevicesChanged -= InstanceOnAvailableOutputDevicesChanged;
            VivoxService.Instance.AvailableInputDevicesChanged -= InstanceOnAvailableInputDevicesChanged;
            VivoxService.Instance.EffectiveInputDeviceChanged -= OnEffectInputDeviceChanged;
            VivoxService.Instance.EffectiveOutputDeviceChanged -= OnEffectOutputDeviceChanged;
            
            InAppSettings.SettingsPanelShow += OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed += OnSettingsPanelDismissed;
            VivoxService.Instance.AvailableOutputDevicesChanged += InstanceOnAvailableOutputDevicesChanged;
            VivoxService.Instance.AvailableInputDevicesChanged += InstanceOnAvailableInputDevicesChanged;
            VivoxService.Instance.EffectiveInputDeviceChanged += OnEffectInputDeviceChanged;
            VivoxService.Instance.EffectiveOutputDeviceChanged += OnEffectOutputDeviceChanged;
            InitializeUI();
        }

        private void OnEffectOutputDeviceChanged()
        {
            if (m_OutputEffectiveDeviceLabel != null)
            {
                m_OutputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveOutputDevice.DeviceName;
            }
        }

        private void OnEffectInputDeviceChanged()
        {
            if (m_InputEffectiveDeviceLabel != null)
            {
                m_InputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveInputDevice.DeviceName;
            }
        }

        private void OnSettingsPanelDismissed()
        {
            m_OutputDeviceDropdown = null;
            m_InputDeviceDropdown = null;
            
            m_InputVolumeSlider?.UnregisterValueChangingCallback(OnInputVolumeChanging);
            m_InputVolumeSlider?.UnregisterValueChangedCallback(OnInputVolumeChanged);
            
            m_OutputVolumeSlider?.UnregisterValueChangingCallback(OnOutputVolumeChanging);
            m_OutputVolumeSlider?.UnregisterValueChangedCallback(OnOutputVolumeChanged);
            
            m_InputVolumeSlider = null;
            m_OutputVolumeSlider = null;

            m_InputEffectiveDeviceLabel = null;
            m_OutputEffectiveDeviceLabel = null;

            m_VivoxSettingsUIPanel = null;
            m_Title = null;
        }

        private void InstanceOnAvailableOutputDevicesChanged()
        {
            RefreshUI();
        }

        private void InstanceOnAvailableInputDevicesChanged()
        {
            RefreshUI();
        }

        private void OnSettingsPanelShow(VisualElement vePanel, VisualTreeAsset titleTemplate)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#endif
            
            m_Title = titleTemplate.Instantiate().Children().First();

            m_VivoxSettingsUIPanel = m_VivoxSettingsUI.Instantiate().Children().First();
            
            m_InputDeviceDropdown = m_VivoxSettingsUIPanel.Q<Dropdown>(k_InputDeviceDropdownName);
            m_OutputDeviceDropdown = m_VivoxSettingsUIPanel.Q<Dropdown>(k_OutputDeviceDropdownName);
            m_InputEffectiveDeviceLabel = m_VivoxSettingsUIPanel.Q<Text>(k_InputEffectiveDeviceLabelName);
            m_OutputEffectiveDeviceLabel = m_VivoxSettingsUIPanel.Q<Text>(k_OutputEffectiveDeviceLabelName);
            m_InputVolumeSlider = m_VivoxSettingsUIPanel.Q<TouchSliderFloat>(k_InputVolumeSliderName);
            m_OutputVolumeSlider = m_VivoxSettingsUIPanel.Q<TouchSliderFloat>(k_OutputVolumeSliderName);

            m_InputDeviceDropdown.bindItem = InputDeviceDropdownBindItem;
            m_InputDeviceDropdown.bindTitle = InputDeviceDropdownBindTitle;

            m_OutputDeviceDropdown.bindItem = OutputDeviceDropdownBindItem;
            m_OutputDeviceDropdown.bindTitle = OutputDeviceDropdownBindTitle;
            
            m_InputVolumeSlider.RegisterValueChangingCallback(OnInputVolumeChanging);
            m_InputVolumeSlider.RegisterValueChangedCallback(OnInputVolumeChanged);
            
            m_OutputVolumeSlider.RegisterValueChangingCallback(OnOutputVolumeChanging);
            m_OutputVolumeSlider.RegisterValueChangedCallback(OnOutputVolumeChanged);
            
            RefreshUI();
            
            InAppSettings.InitializeSection("Vivox", ref m_Title, m_VivoxSettingsUIPanel);
            
            vePanel.Q<ScrollView>().Add(m_Title);
        }

        private void OnOutputVolumeChanged(ChangeEvent<float> evt)
        {
            VivoxService.Instance.SetOutputDeviceVolume((int)Mathf.Lerp(-k_Volume, k_Volume, evt.newValue));
        }

        private void OnOutputVolumeChanging(ChangingEvent<float> evt)
        {
            VivoxService.Instance.SetOutputDeviceVolume((int)Mathf.Lerp(-k_Volume, k_Volume, evt.newValue));
        }

        private void OnInputVolumeChanged(ChangeEvent<float> evt)
        {
            VivoxService.Instance.SetInputDeviceVolume((int)Mathf.Lerp(-k_Volume, k_Volume, evt.newValue));
        }

        private void OnInputVolumeChanging(ChangingEvent<float> evt)
        {
            VivoxService.Instance.SetInputDeviceVolume((int)Mathf.Lerp(-k_Volume, k_Volume, evt.newValue));
        }

        private void RefreshUI()
        {
            if (m_InputDeviceDropdown != null)
            {
                m_InputDeviceDropdown.style.display = DisplayStyle.Flex;
                m_InputDeviceDropdown.sourceItems = VivoxService.Instance.AvailableInputDevices;
                var index = VivoxService.Instance.AvailableInputDevices.IndexOf(VivoxService.Instance.ActiveInputDevice);
                var value = new[] {index};
                m_InputDeviceDropdown.defaultValue = value;
                m_InputDeviceDropdown.value = value;
                m_InputDeviceDropdown.SetValueWithoutNotify(value);
                
                if (VivoxService.Instance.ActiveInputDevice.DeviceName.Contains("Default System Device"))
                {
                    m_InputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveInputDevice.DeviceName;
                    m_InputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    m_InputEffectiveDeviceLabel.style.display = DisplayStyle.None;
                }
            }

            m_InputVolumeSlider?.SetValueWithoutNotify(Mathf.InverseLerp(-55, 55, VivoxService.Instance.InputDeviceVolume));

            if (m_OutputDeviceDropdown != null)
            {
                m_OutputDeviceDropdown.style.display = DisplayStyle.Flex;
                m_OutputDeviceDropdown.sourceItems = VivoxService.Instance.AvailableOutputDevices;
                var index = VivoxService.Instance.AvailableOutputDevices.IndexOf(VivoxService.Instance.ActiveOutputDevice);
                var value = new[] {index};
                m_OutputDeviceDropdown.defaultValue = value;
                m_OutputDeviceDropdown.value = value;
                m_OutputDeviceDropdown.SetValueWithoutNotify(value);
                
                if (VivoxService.Instance.ActiveOutputDevice.DeviceName.Contains("Default System Device"))
                {
                    m_OutputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveOutputDevice.DeviceName;
                    m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.None;
                }
            }

            m_OutputVolumeSlider?.SetValueWithoutNotify(Mathf.InverseLerp(-55, 55, VivoxService.Instance.OutputDeviceVolume));
        }
        
        private void OutputDeviceDropdownBindTitle(DropdownItem arg1, IEnumerable<int> arg2)
        {
            if (arg2 == null || !arg2.Any())
            {
                arg1.label = VivoxService.Instance.ActiveOutputDevice.DeviceName;
                
                if (VivoxService.Instance.ActiveOutputDevice.DeviceName.Contains("Default System Device"))
                {
                    m_OutputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveOutputDevice.DeviceName;
                    m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.None;
                }
                
                return;
            }

            var device = VivoxService.Instance.AvailableOutputDevices[arg2.First()];
            if (device == VivoxService.Instance.ActiveOutputDevice)
            {
                return;
            }
            arg1.label = device.DeviceName;

            if (device.DeviceName.Contains("Default System Device"))
            {
                m_OutputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveOutputDevice.DeviceName;
                m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_OutputEffectiveDeviceLabel.style.display = DisplayStyle.None;
            }
            
            VivoxService.Instance.SetActiveOutputDeviceAsync(device);
        }

        private void InputDeviceDropdownBindTitle(DropdownItem arg1, IEnumerable<int> arg2)
        {
            if (arg2 == null || !arg2.Any())
            {
                arg1.label = VivoxService.Instance.ActiveInputDevice.DeviceName;
                
                if (VivoxService.Instance.ActiveInputDevice.DeviceName.Contains("Default System Device"))
                {
                    m_InputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveInputDevice.DeviceName;
                    m_InputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    m_InputEffectiveDeviceLabel.style.display = DisplayStyle.None;
                }
                return;
            }
            var device = VivoxService.Instance.AvailableInputDevices[arg2.First()];
            if (device == VivoxService.Instance.ActiveInputDevice)
            {
                return;
            }
            arg1.label = device.DeviceName;
            
            if (device.DeviceName.Contains("Default System Device"))
            {
                m_InputEffectiveDeviceLabel.text = VivoxService.Instance.EffectiveInputDevice.DeviceName;
                m_InputEffectiveDeviceLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_InputEffectiveDeviceLabel.style.display = DisplayStyle.None;
            }
            
            VivoxService.Instance.SetActiveInputDeviceAsync(device);
        }

        private void InputDeviceDropdownBindItem(DropdownItem arg1, int arg2)
        {
            arg1.label = VivoxService.Instance.AvailableInputDevices[arg2].DeviceName;
        }

        private void OutputDeviceDropdownBindItem(DropdownItem arg1, int arg2)
        {
            arg1.label = VivoxService.Instance.AvailableOutputDevices[arg2].DeviceName;
        }
        
        private void InitializeUI()
        {
            var avatar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<Avatar>(k_AvatarName);

            if (!SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.styleSheets.Add(m_StyleSheet);
            }

            var micButton = avatar.parent.Q<MicComponent>();
            if (micButton != null)
            {
                micButton.RemoveFromHierarchy();
                micButton.clicked -= OnMicButtonClicked;
            }

            m_MicButton = new MicComponent(VivoxService.Instance.IsInputDeviceMuted);
            UpdateToolTips();
            m_MicButton.clicked += OnMicButtonClicked;

            avatar.parent.Insert(0, m_MicButton);
        }

        private void OnMicButtonClicked()
        {
            if (VivoxService.Instance.IsInputDeviceMuted)
            {
                VivoxService.Instance.UnmuteInputDevice();
                m_MicButton.SetMuted(false);
            }
            else
            {
                VivoxService.Instance.MuteInputDevice();
                m_MicButton.SetMuted(true);
            }

            UpdateToolTips();
        }

        private void UpdateToolTips()
        {
            m_MicButton.ClearBinding("tooltip");
            m_MicButton.SetBinding("tooltip", VivoxService.Instance.IsInputDeviceMuted ? m_UnmuteLocalizedString : m_MuteLocalizedString);
        }
    }
}
