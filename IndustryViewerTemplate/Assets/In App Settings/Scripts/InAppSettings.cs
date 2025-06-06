using System;
using System.Linq;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Identity;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Shared;
using UnityEngine.SceneManagement;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Industry.Viewer.AppSettings
{
    public class InAppSettings : MonoBehaviour
    {
        public static Action<VisualElement, VisualTreeAsset> SettingsPanelShow;
        public static Action<VisualElement, VisualTreeAsset> SettingsPanelShown;
        public static Action SettingsPanelDismissed;
        
        private const string k_SettingsButton = "SettingsButton";
        private const string k_VersionLabel = "VersionLabel";
        private const string k_RefreshRateSlider = "RefreshRateSlider";
        private const string k_FPSToggle = "FPSToggle";
        private const string k_FPSLabel = "FPSLabel";
        private const string k_LanguageDropdownName = "LanguageDropdown";
        private const string k_OfflineToggleName = "OfflineToggle";
        
        [SerializeField] private UIDocument m_UIDocument;
        [SerializeField] private UIDocument m_FPSUIDocument;

        [SerializeField]
        private VisualTreeAsset settingPanel;
        
        [SerializeField]
        private VisualTreeAsset m_SettingsUITitleTemplate;
        
        private IconButton m_SettingsButton;
        
        [SerializeField]
        private VisualTreeAsset m_GeneralSettingsTemplate;

        [SerializeField]
        private StyleSheet m_StyleSheet;
        
        private Text m_VersionLabel;
        private TouchSliderInt m_RefreshRateSlider;
        private Checkbox m_FPSToggle;
        private Text m_FPSLabel;
        private Dropdown m_LanguageDropdown;
        private Toggle m_OfflineToggle;

        private bool showFPS = false;
        private float deltaTime = 0.0f;
        private float currentFPS;
        private string m_FPSLocalizedString;
        [SerializeField] private LocalizedString m_GeneralLocalizedString;
        [SerializeField] private LocalizedString m_FPSTextLocalizedString;

        private void Start()
        {
            if (!m_UIDocument.rootVisualElement.styleSheets.Contains(m_StyleSheet))
            {
                m_UIDocument.rootVisualElement.styleSheets.Add(m_StyleSheet);
            }
            m_SettingsButton = m_UIDocument.rootVisualElement.Q<IconButton>(k_SettingsButton);
            m_SettingsButton.clickable.clicked += OnSettingsButtonClicked;
            SettingsPanelShow += OnSettingsPanelShow;
            m_FPSTextLocalizedString.StringChanged += FPSTextLocalizedStringOnStringChanged;
            Application.targetFrameRate = (int) Screen.currentResolution.refreshRateRatio.value;
        }

        private void Update()
        {
            
            if(!showFPS) return;
            
            
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            currentFPS = 1.0f / deltaTime;
            m_FPSLabel.text = $"{m_FPSLocalizedString}: {currentFPS:0.}";
        }

        private void OnDestroy()
        {
            m_FPSTextLocalizedString.StringChanged -= FPSTextLocalizedStringOnStringChanged;
            m_SettingsButton.clickable.clicked -= OnSettingsButtonClicked;
            SettingsPanelShow -= OnSettingsPanelShow;
        }

        private void FPSTextLocalizedStringOnStringChanged(string value)
        {
            m_FPSLocalizedString = value;
        }

        private void OnSettingsPanelShow(VisualElement arg1, VisualTreeAsset template)
        {
            var newTitle = template.Instantiate().Children().First();
            var generalSettings = m_GeneralSettingsTemplate.Instantiate().Children().First();

            m_VersionLabel = generalSettings.Q<Text>(k_VersionLabel);
            m_VersionLabel.text = $"{Application.version}";
            
            m_RefreshRateSlider = generalSettings.Q<TouchSliderInt>(k_RefreshRateSlider);
            m_RefreshRateSlider.highValue = (int)Screen.currentResolution.refreshRateRatio.value;
            m_RefreshRateSlider.SetValueWithoutNotify(Application.targetFrameRate == -1 ? (int)Screen.currentResolution.refreshRateRatio.value : Application.targetFrameRate);
            m_RefreshRateSlider.RegisterValueChangingCallback(OnRefreshRateChanging);
            m_RefreshRateSlider.RegisterValueChangedCallback(OnRefreshRateChanged);
            
            m_FPSToggle = generalSettings.Q<Checkbox>(k_FPSToggle);
            m_FPSToggle.SetValueWithoutNotify(showFPS ? CheckboxState.Checked : CheckboxState.Unchecked);
            m_FPSToggle.RegisterValueChangedCallback(evt =>
            {
                showFPS = evt.newValue == CheckboxState.Checked;
                m_FPSLabel ??= m_FPSUIDocument.rootVisualElement.Q<Text>(k_FPSLabel);
                m_FPSLabel.style.display = showFPS ? DisplayStyle.Flex : DisplayStyle.None;
            });
            
            m_LanguageDropdown = generalSettings.Q<Dropdown>(k_LanguageDropdownName);
            m_LanguageDropdown.bindItem = LanguageDropdownBindItem;
            m_LanguageDropdown.sourceItems = LocalizationSettings.AvailableLocales.Locales;
            //find the index of LocalizationSettings.SelectedLocale within the sourceItems
            var selectedLocale = LocalizationSettings.SelectedLocale;
            var selectedIndex = LocalizationSettings.AvailableLocales.Locales.IndexOf(selectedLocale);
            m_LanguageDropdown.selectedIndex = selectedIndex;
            m_LanguageDropdown.RegisterValueChangedCallback(evt =>
            {
                //Change to the selected locale
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[evt.newValue.First()];
            });
            
            m_OfflineToggle = generalSettings.Q<Toggle>(k_OfflineToggleName);
#if !UNITY_EDITOR && UNITY_WEBGL
            m_OfflineToggle.parent.style.display = DisplayStyle.None;
#else
            m_OfflineToggle.SetValueWithoutNotify(NetworkDetector.RequestedOfflineMode);
            m_OfflineToggle.RegisterValueChangedCallback(OnOfflineModeRequestValueChanged);
#endif
            
            m_OfflineToggle.SetEnabled(!IdentityController.GuestMode);

            InitializeSection(m_GeneralLocalizedString, ref newTitle, generalSettings);
            arg1.Q<ScrollView>().Insert(0, newTitle);
            // This is to add camera settings if navigating a model
            SettingsPanelShown?.Invoke(arg1, template);
            return;

            void LanguageDropdownBindItem(DropdownItem item, int arg2)
            {
                item.label = LocalizationSettings.AvailableLocales.Locales[arg2].Identifier.CultureInfo.NativeName;
            }
        }
        
        private void OnOfflineModeRequestValueChanged(ChangeEvent<bool> evt)
        {
            NetworkDetector.RequestedOfflineMode = evt.newValue;
        }
        
        private void OnRefreshRateChanging(ChangingEvent<int> evt)
        {
            Application.targetFrameRate = evt.newValue;
        }
        
        private void OnRefreshRateChanged(ChangeEvent<int> evt)
        {
            Application.targetFrameRate = evt.newValue;
        }

        private void OnSettingsButtonClicked()
        {
            var settingsPanelClone = settingPanel.Instantiate().Children().First();
            var popover = Popover.Build(m_SettingsButton, settingsPanelClone).SetOutsideClickDismiss(true)
                .SetArrowVisible(false);
            
            popover.shown += PopoverOnShown;
            popover.dismissed += PopoverOnDismissed;
            popover.Show();
        }

        private void PopoverOnDismissed(Popover arg1, DismissType arg2)
        {
            arg1.dismissed -= PopoverOnDismissed;
            SettingsPanelDismissed?.Invoke();
        }

        private void PopoverOnShown(Popover obj)
        {
            obj.shown -= PopoverOnShown;
            SettingsPanelShow?.Invoke(obj.contentView, m_SettingsUITitleTemplate);
        }

        public static void InitializeSection(string name, ref VisualElement section, VisualElement content)
        {
            section.Q<Text>("Title").text = name;
            section.Q<VisualElement>("Content").Add(content);
        }
        
        public static void InitializeSection(LocalizedString localizedString, ref VisualElement section, VisualElement content)
        {
            var titleText = section.Q<Text>("Title");
            titleText.ClearBinding("text");
            titleText.SetBinding("text", localizedString);
            section.Q<VisualElement>("Content").Add(content);
        }
    }
}
