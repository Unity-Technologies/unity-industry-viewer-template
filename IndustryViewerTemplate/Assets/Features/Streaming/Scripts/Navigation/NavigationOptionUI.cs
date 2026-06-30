using System;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(101)]
    public abstract class NavigationOptionUI : MonoBehaviour
    {
        [SerializeField]
        private Texture2D navigationIcon;
        public Texture2D NavigationIcon => navigationIcon;

        [SerializeField]
        private LocalizedString navigationName;
        public LocalizedString NavigationName => navigationName;

        [SerializeField]
        protected VisualTreeAsset navigationOptionUIAsset;
        public VisualTreeAsset NavigationOptionUIAsset => navigationOptionUIAsset;

        protected VisualElement m_SettingsPanel;
        protected VisualElement m_Title;
        private IconButton m_HomeButton;

        protected abstract void InitialUI(VisualElement panel);

        public virtual void CreatePanel()
        {
            // Do nothing by default
        }

        protected virtual async void ChangeCameraTitle(VisualTreeAsset titleTemplate)
        {
            var titleText = m_Title.Q<Text>("Title");
            titleText.text = await navigationName.GetTitleLocalizedStringForAppUIAsync();
        }

        protected void SettingsPanelUp(VisualElement settingsWindow, VisualTreeAsset titleTemplate)
        {
            m_SettingsPanel = settingsWindow;
            m_Title = titleTemplate.Instantiate();
            ChangeCameraTitle(titleTemplate);
            // Insert the title as the element after the General Settings in it's Scroll View
            m_SettingsPanel.Q<ScrollView>().Insert(1, m_Title);
            var m_CameraSettings = navigationOptionUIAsset.Instantiate();
            m_Title.Q<VisualElement>("Content").Add(m_CameraSettings);
            InitialUI(m_CameraSettings);
        }

        // Shared "go to default/home view" button shown in the streaming panel's bottom-left
        // container. Used by navigation options that expose a home view (Fly, Orbit); options
        // without one (e.g. Walk) simply never call these.
        protected void ShowOrCreateHomeButton()
        {
            if (m_HomeButton == null)
            {
                var uiDocument = SharedUIManager.Instance.AssetsUIDocument;
                var streamingContainer = uiDocument.rootVisualElement.Q<VisualElement>(StreamingUtils.StreamingPanelName);
                var bottomLeftContainer = streamingContainer.Q<VisualElement>(StreamingUtils.BottomLeftContainerName);

                m_HomeButton = new IconButton()
                {
                    icon = "camera-overhead"
                };
                m_HomeButton.AddToClassList(StreamingUtils.BottomLeftButtonStyleName);
                m_HomeButton.clicked += OnHomeButtonClicked;
                bottomLeftContainer.Insert(bottomLeftContainer.childCount, m_HomeButton);
            }
            else
            {
                m_HomeButton.style.display = DisplayStyle.Flex;
            }
        }

        protected void HideHomeButton()
        {
            if (m_HomeButton != null)
            {
                m_HomeButton.style.display = DisplayStyle.None;
            }
        }

        protected void DestroyHomeButton()
        {
            if (m_HomeButton != null)
            {
                m_HomeButton.clicked -= OnHomeButtonClicked;
                m_HomeButton.RemoveFromHierarchy();
            }
        }

        private void OnHomeButtonClicked()
        {
            NavigationController.RequestDefaultHomeView?.Invoke();
        }

        // Wires a sensitivity TouchSliderFloat: both the live (changing) and committed (changed)
        // callbacks forward to the same handler, and the control is seeded without notifying.
        protected static TouchSliderFloat WireSensitivitySlider(VisualElement panel, string elementName,
            float initialValue, Action<float> onValueChanged)
        {
            var slider = panel.Q<TouchSliderFloat>(elementName);
            slider.RegisterValueChangingCallback(evt => onValueChanged(evt.newValue));
            slider.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }
    }
}
