using Unity.Industry.Viewer.AppSettings;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using Unity.Industry.Viewer.Shared;
using UnityEngine.UIElements;
using UnityEngine.Localization;

namespace Unity.Industry.Viewer.VR
{
    public class XRInAppSettingUIController : InAppSettings
    {
        public static UIDocument fpsUIDocument;
        
        [SerializeField]
        private XRControllerMenu m_XRControllerMenu;
        
        private XRRoundButton m_XRSettingsButton;
        
        [SerializeField]
        private Texture2D m_SettingsIcon;
        
        [SerializeField]
        private LocalizedString m_SettingsUITitle;
        
        protected override void Start()
        {
            base.Start();
            fpsUIDocument = m_FPSUIDocument;
            SceneManager.activeSceneChanged += OnSceneManagerOnactiveSceneChanged;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.activeSceneChanged -= OnSceneManagerOnactiveSceneChanged;
        }

        private void OnSceneManagerOnactiveSceneChanged(Scene previewActiveScene, Scene newActiveScene)
        {
            if (newActiveScene != gameObject.scene)
            {
                //Initialize the XRControllerMenu if it is not initialized
                if (m_XRSettingsButton == null)
                {
                    m_XRControllerMenu ??= new XRControllerMenu();
                    m_XRControllerMenu.Initialize();
                    m_XRSettingsButton = new XRRoundButton
                    {
                        IconTexture = m_SettingsIcon
                    };
                    m_XRSettingsButton.clicked += OnSettingsButtonClicked;
                    m_XRControllerMenu.Add(m_XRSettingsButton);
                }
            }
            else
            {
                // If the scene is the same as the current one, we can remove the XRSettingsButton, back to main scene
                if (m_XRSettingsButton == null) return;
                m_XRSettingsButton.clicked -= OnSettingsButtonClicked;
                m_XRSettingsButton.RemoveFromHierarchy();
                m_XRSettingsButton = null;
            }
        }

        protected override void OnSettingsButtonClicked()
        {
            if (SceneManager.GetActiveScene() == gameObject.scene)
            {
                base.OnSettingsButtonClicked();
            }
            else
            {
                //In 3D streaming, the settings should be opened in XR Panel
                var settingsPanelClone = settingPanel.Instantiate().Children().First();
                var newXRPanel = new XRPanel.CustomXRPanel(m_SettingsUITitle.GetTitleLocalizedStringForAppUI());
                XRPanel.Build(newXRPanel, settingsPanelClone).Build();
                newXRPanel.Shown += OnXRSettingsPanelShown;
                newXRPanel.Dismissed += OnXRSettingsPanelDismissed;
                newXRPanel.Show();
            }
        }

        private void OnXRSettingsPanelDismissed(XRPanel.CustomXRPanel obj)
        {
            obj.Dismissed -= OnXRSettingsPanelDismissed;
            SettingsPanelDismissed?.Invoke();
            m_XRSettingsButton.primary = false;
        }

        private void OnXRSettingsPanelShown(XRPanel.CustomXRPanel obj)
        {
            obj.Shown -= OnXRSettingsPanelShown;
            SettingsPanelShow?.Invoke(obj.Content, m_SettingsUITitleTemplate);
            m_XRSettingsButton.primary = true;
        }

        protected override void UpdateRefreshRateSlider(VisualElement content)
        {
            //Hide the refresh rate slider in XR settings as it is not applicable in VR
            var slider = content.Q(k_RefreshRateSlider);
            if (slider == null)
            {
                Debug.LogError("Refresh Rate Slider not found in the content.");
                return;
            }
            slider.parent.style.display = DisplayStyle.None;
        }

        public void TwoDSettingsButtonDisplay(bool display)
        {
            if (SettingsButton != null)
            {
                SettingsButton.style.display = display ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
