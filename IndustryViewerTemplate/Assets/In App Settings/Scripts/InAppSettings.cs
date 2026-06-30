using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Identity;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Shared;
using Toggle = Unity.AppUI.UI.Toggle;
using System.Threading.Tasks;
using Button = Unity.AppUI.UI.Button;

namespace Unity.Industry.Viewer.AppSettings
{
    public struct LogInfo
    {
        public LogType Type  { get; private set; }
        public string StackTrace { get; private set; }
        public string Message { get; private set; }
        
        public LogInfo(LogType type, string stackTrace, string message)
        {
            Type = type;
            StackTrace = stackTrace;
            Message = message;
        }
    }
    
    public class InAppSettings : MonoBehaviour
    {
        public static Action<VisualElement, VisualTreeAsset> SettingsPanelShow;
        public static Action<VisualElement, VisualTreeAsset> SettingsPanelShown;
        public static Action SettingsPanelDismissed;
        
        private const string k_SettingsButton = "SettingsButton";
        private const string k_VersionLabel = "VersionLabel";
        protected const string k_RefreshRateSlider = "RefreshRateSlider";
        private const string k_FPSToggle = "FPSToggle";
        private const string k_LogConsoleToggle = "LogConsoleToggle";
        private const string k_FPSLabel = "FPSLabel";
        private const string k_LanguageDropdownName = "LanguageDropdown";
        private const string k_OfflineToggleName = "OfflineToggle";
        private const string k_LogConsole = "LogConsole";
        private const string k_LogCheckBox = "LogCheckBox";
        private const string k_WarningCheckBox = "WarningCheckBox";
        private const string k_ErrorCheckBox = "ErrorCheckBox";
        private const string k_ClearLogsButton = "ClearButton";
        private const string k_CopyLogsButton = "CopyButton";
        
        [SerializeField] private UIDocument m_UIDocument;
        [SerializeField] protected UIDocument m_FPSUIDocument;

        [SerializeField]
        protected VisualTreeAsset settingPanel;
        
        [SerializeField]
        protected VisualTreeAsset m_SettingsUITitleTemplate;
        
        protected IconButton SettingsButton;
        
        [SerializeField]
        private VisualTreeAsset m_GeneralSettingsTemplate;

        [SerializeField]
        private StyleSheet m_StyleSheet;

        [SerializeField] protected int logsHistory = 100;
        
        private Text m_VersionLabel;
        private TouchSliderInt m_RefreshRateSlider;
        private Checkbox m_FPSToggle;
        private Text m_FPSLabel;
        protected VisualElement m_logConsoleParent;
        private ListView m_LogConsole;
        private Checkbox m_LogsCheckbox;
        private Checkbox m_WarningsCheckbox;
        private Checkbox m_ErrorsCheckbox;
        private Checkbox m_ShowLogsCheckbox;
        private Button m_ClearLogsButton;
        private Button m_CopyLogsButton;
        private Dropdown m_LanguageDropdown;
        private Toggle m_OfflineToggle;

        private bool showFPS = false;
        private float deltaTime = 0.0f;
        private float currentFPS;
        private string m_FPSLocalizedString;
        private List<LogInfo> m_LogInfos = new List<LogInfo>();
        [SerializeField] private LocalizedString m_GeneralLocalizedString;
        [SerializeField] private LocalizedString m_FPSTextLocalizedString;

        protected virtual void Awake()
        {
            m_logConsoleParent = m_FPSUIDocument.rootVisualElement.Q<VisualElement>(k_LogConsole);
            m_LogConsole = m_logConsoleParent.Q<ListView>();
            m_LogConsole.makeItem = LogConsoleListViewMakeItemElement;
            m_LogConsole.bindItem = LogConsoleListViewBindItem;
            m_LogsCheckbox = m_logConsoleParent.Q<Checkbox>(k_LogCheckBox);
            m_LogsCheckbox.RegisterValueChangedCallback(OnCheckBoxValueChanged);
            m_WarningsCheckbox = m_logConsoleParent.Q<Checkbox>(k_WarningCheckBox);
            m_WarningsCheckbox.RegisterValueChangedCallback(OnCheckBoxValueChanged);
            m_ErrorsCheckbox = m_logConsoleParent.Q<Checkbox>(k_ErrorCheckBox);
            m_ErrorsCheckbox.RegisterValueChangedCallback(OnCheckBoxValueChanged);
            m_ClearLogsButton = m_logConsoleParent.Q<Button>(k_ClearLogsButton);
            m_ClearLogsButton.clicked += OnClearLogButtonPress;
            m_CopyLogsButton = m_logConsoleParent.Q<Button>(k_CopyLogsButton);
            m_CopyLogsButton.clicked += OnCopyLogsButtonPress;
            m_logConsoleParent.RegisterCallback<GeometryChangedEvent>(OnConsoleShown);
            Application.logMessageReceived += OnLogMessageReceived;
        }

        protected virtual void Start()
        {
            m_UIDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);
            SettingsButton = m_UIDocument.rootVisualElement.Q<IconButton>(k_SettingsButton);
            SettingsButton.clickable.clicked += OnSettingsButtonClicked;
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

        protected virtual void OnDestroy()
        {
            m_FPSTextLocalizedString.StringChanged -= FPSTextLocalizedStringOnStringChanged;
            SettingsButton.clickable.clicked -= OnSettingsButtonClicked;
            SettingsPanelShow -= OnSettingsPanelShow;
            Application.logMessageReceived -= OnLogMessageReceived;
            m_LogsCheckbox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
            m_WarningsCheckbox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
            m_ErrorsCheckbox.UnregisterValueChangedCallback(OnCheckBoxValueChanged);
            m_ClearLogsButton.clicked -= OnClearLogButtonPress;
            m_CopyLogsButton.clicked -= OnCopyLogsButtonPress;
            m_logConsoleParent.UnregisterCallback<GeometryChangedEvent>(OnConsoleShown);
        }

        private void OnConsoleShown(GeometryChangedEvent evt)
        {
            var element = evt.currentTarget as VisualElement;
            if(element == null) return;
            if (element.style.display == DisplayStyle.Flex)
            {
                m_CopyLogsButton.SetEnabled(m_LogInfos != null && m_LogInfos.Count > 0);
            }
        }

        private void OnCopyLogsButtonPress()
        {
            if(m_LogInfos == null || m_LogInfos.Count == 0) return;
            var logsText = string.Join("\n", m_LogInfos.Select(log => $"[{log.Type}] {log.Message}\n{log.StackTrace}"));
            GUIUtility.systemCopyBuffer = logsText;
        }

        private void OnClearLogButtonPress()
        {
            m_LogInfos.Clear();
            m_CopyLogsButton.SetEnabled(false);
            ApplyLogs();
        }

        private void OnCheckBoxValueChanged(ChangeEvent<CheckboxState> evt)
        {
            ApplyLogs();
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if(m_LogConsole == null) return;
            m_LogInfos.Add(new LogInfo(type, stackTrace, condition));
            if(m_LogInfos.Count > logsHistory)
            {
                m_LogInfos.RemoveAt(0);
            }
            m_CopyLogsButton.SetEnabled(true);
            ApplyLogs();
        }

        private void ApplyLogs()
        {
            if(m_LogInfos == null || m_LogConsole == null)
            {
                if (m_LogConsole != null)
                {
                    m_LogConsole.itemsSource = null;
                    m_LogConsole.Rebuild();
                }
                return;
            }
            var filteredLogs = m_LogInfos.Where(log =>
                (log.Type == LogType.Log && m_LogsCheckbox.value == CheckboxState.Checked) ||
                (log.Type == LogType.Warning && m_WarningsCheckbox.value == CheckboxState.Checked) ||
                (log.Type == LogType.Error && m_ErrorsCheckbox.value == CheckboxState.Checked)).ToList();

            if (filteredLogs.Count == 0)
            {
                filteredLogs = null;
            }
            
            m_LogConsole.itemsSource = filteredLogs;
            m_LogConsole.Rebuild();
        }

        private void LogConsoleListViewBindItem(VisualElement text, int index)
        {
            if(m_LogConsole == null) return;
            var logs =  m_LogConsole.itemsSource as List<LogInfo>;
            if(logs == null || logs.Count == 0) return;
            var log = logs[index];
            switch (log.Type)
            {
                case LogType.Log when m_LogsCheckbox.value == CheckboxState.Unchecked:
                    text.style.display = DisplayStyle.None;
                    return;
                case LogType.Warning when m_WarningsCheckbox.value == CheckboxState.Unchecked:
                    text.style.display = DisplayStyle.None;
                    return;
                case LogType.Error when m_ErrorsCheckbox.value == CheckboxState.Unchecked:
                    text.style.display = DisplayStyle.None;
                    return;
            }

            var textElement = text as Text;
            if (textElement != null)
            {
                textElement.style.marginBottom = new Length(5, LengthUnit.Pixel);
                textElement.text = $"[{log.Type}] {log.Message}\n{log.StackTrace}";
                if (log.Type == LogType.Warning)
                {
                    textElement.style.color = Color.yellow;
                } else if (log.Type == LogType.Log)
                {
                    textElement.style.color = Color.white;
                }
                else
                {
                    textElement.style.color = Color.red;
                }
            }
        }
        
        private VisualElement LogConsoleListViewMakeItemElement()
        {
            var text = new Text
            {
                style =
                {
                    marginBottom = new Length(5, LengthUnit.Pixel)
                }
            };
            return text;
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

            UpdateRefreshRateSlider(generalSettings);
            
            m_FPSToggle = generalSettings.Q<Checkbox>(k_FPSToggle);
            m_FPSToggle.SetValueWithoutNotify(showFPS ? CheckboxState.Checked : CheckboxState.Unchecked);
            m_FPSToggle.RegisterValueChangedCallback(OnFPSToggleValueChanged);
            
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

            m_ShowLogsCheckbox = generalSettings.Q<Checkbox>(k_LogConsoleToggle);
            m_ShowLogsCheckbox.SetValueWithoutNotify(m_logConsoleParent.style.display == DisplayStyle.Flex ? CheckboxState.Checked : CheckboxState.Unchecked);
            m_ShowLogsCheckbox.RegisterValueChangedCallback(OnShowLogsCheckBoxValueChanged);
            
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

        private void OnShowLogsCheckBoxValueChanged(ChangeEvent<CheckboxState> evt)
        {
            if (evt.newValue == CheckboxState.Checked)
            {
                m_logConsoleParent.DisplayOn();
            }
            else
            {
                m_logConsoleParent.DisplayOff();
            }
        }

        protected virtual void UpdateRefreshRateSlider(VisualElement content)
        {
            m_RefreshRateSlider = content.Q<TouchSliderInt>(k_RefreshRateSlider);
            m_RefreshRateSlider.highValue = (int)Screen.currentResolution.refreshRateRatio.value;
            m_RefreshRateSlider.SetValueWithoutNotify(Application.targetFrameRate == -1 ? (int)Screen.currentResolution.refreshRateRatio.value : Application.targetFrameRate);
            m_RefreshRateSlider.RegisterValueChangingCallback(OnRefreshRateChanging);
            m_RefreshRateSlider.RegisterValueChangedCallback(OnRefreshRateChanged);
        }

        private void OnFPSToggleValueChanged(ChangeEvent<CheckboxState> evt)
        {
            showFPS = evt.newValue == CheckboxState.Checked;
            m_FPSLabel ??= m_FPSUIDocument.rootVisualElement.Q<Text>(k_FPSLabel);
            m_FPSLabel.style.display = showFPS ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnOfflineModeRequestValueChanged(ChangeEvent<bool> evt)
        {
            NetworkDetector.RequestedOfflineMode = evt.newValue;
        }
        
        private void OnRefreshRateChanging(ChangingEvent<int> evt)
        {
            Application.targetFrameRate = evt.newValue;
        }
        
        protected void OnRefreshRateChanged(ChangeEvent<int> evt)
        {
            Application.targetFrameRate = evt.newValue;
        }

        protected virtual void OnSettingsButtonClicked()
        {
            var settingsPanelClone = settingPanel.Instantiate().Children().First();
            var popover = Popover
                .Build(SettingsButton.parent, settingsPanelClone)
                .SetOutsideClickDismiss(true)
                .SetArrowVisible(false)
                .SetPlacement(PopoverPlacement.TopRight)
                .SetOffset(-8)
                .SetCrossOffset(-8);

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
            Text titleText = section.Q<Text>("Title");
            if (!string.IsNullOrEmpty(name))
            {
                titleText.text = name;
                titleText.style.display = DisplayStyle.Flex;
            }
            else
            {
                titleText.style.display = DisplayStyle.None;
            }
            section.Q<VisualElement>("Content").Add(content);
        }
        
        public static void InitializeSection(LocalizedString localizedString, ref VisualElement section, VisualElement content)
        {
            var titleText = section.Q<Text>("Title");
            _ = GetTranslation();
            section.Q<VisualElement>("Content").Add(content);
            return;

            async Task GetTranslation()
            {
                titleText.text = await localizedString.GetTitleLocalizedStringForAppUIAsync();
            }
        }
    }
}
