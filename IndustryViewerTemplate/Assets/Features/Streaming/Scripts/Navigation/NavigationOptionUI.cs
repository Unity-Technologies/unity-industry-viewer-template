using Unity.AppUI.UI;
using Unity.Industry.Viewer.AppSettings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using static System.Collections.Specialized.BitVector32;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(101)]
    public abstract class NavigationOptionUI : MonoBehaviour
    {
        public Texture2D NavigationIcon => navigationIcon;

        [SerializeField]
        protected Texture2D navigationIcon;

        protected VisualElement m_SettingsPanel;
        protected VisualElement m_Title;

        public VisualTreeAsset NavigationOptionUIAsset => navigationOptionUIAsset;

        [SerializeField]
        protected VisualTreeAsset navigationOptionUIAsset;

        protected abstract void InitialUI(VisualElement panel);

        public abstract void CreatePanel();

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

        protected virtual void ChangeCameraTitle(VisualTreeAsset titleTemplate)
        { }
    }
}
