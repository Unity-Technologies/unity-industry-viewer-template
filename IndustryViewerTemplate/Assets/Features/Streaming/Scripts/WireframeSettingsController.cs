using UnityEngine;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine.UIElements;
using System.Linq;
using Unity.Industry.Viewer.AppSettings;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Industry.Viewer.Streaming
{
    public class WireframeSettingsController : MonoBehaviour
    {
        private const string k_WireframeToggleName = "WireframeToggle";
        
        IStage m_Stage;
        
        IDataStreamer m_DataStreamer => PlatformServices.DataStreamer;
        
        [SerializeField] private VisualTreeAsset m_WireframeSettingsUI;
        Toggle m_WireframeToggle;

        [SerializeField] private LocalizedString m_StreamTitle;
        
        void Awake()
        {
            m_DataStreamer.StageCreated.Subscribe(OnStageCreated);
            m_DataStreamer.StageDestroyed.Subscribe(OnStageDestroyed);
        }

        private void Start()
        {
            InAppSettings.SettingsPanelShow += OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed += OnSettingsPanelDismissed;
        }

        private void OnDestroy()
        {
            InAppSettings.SettingsPanelShow -= OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed -= OnSettingsPanelDismissed;
            m_DataStreamer.StageCreated.Unsubscribe(OnStageCreated);
            m_DataStreamer.StageDestroyed.Unsubscribe(OnStageDestroyed);
            m_Stage = null;
        }

        private void OnSettingsPanelShow(VisualElement vePanel, VisualTreeAsset titleTemplate)
        {
            if(m_Stage == null || !m_Stage.Wireframe.Enabled) return;
            var newTitle = titleTemplate.Instantiate().Children().First();
            var m_settings = m_WireframeSettingsUI.Instantiate().Children().First();
            
            m_WireframeToggle = m_settings.Q<Toggle>(k_WireframeToggleName);
            m_WireframeToggle.value = m_Stage.Wireframe.Mode == WireframeModes.Wireframe;
            m_WireframeToggle.RegisterValueChangedCallback(OnWireframeToggleValueChanged);
            
            InAppSettings.InitializeSection(m_StreamTitle, ref newTitle, m_settings);
            vePanel.Q<ScrollView>().Add(newTitle);
        }
        
        private void OnSettingsPanelDismissed()
        {
            if(m_Stage == null || !m_Stage.Wireframe.Enabled) return;
            m_WireframeToggle.UnregisterValueChangedCallback(OnWireframeToggleValueChanged);
            m_WireframeToggle = null;
        }

        private void OnWireframeToggleValueChanged(ChangeEvent<bool> evt)
        {
            if(m_Stage == null) return;
            m_Stage.Wireframe.Mode = evt.newValue? WireframeModes.Wireframe : WireframeModes.Shaded;
        }

        private void OnStageDestroyed()
        {
            m_Stage = null;
        }

        private void OnStageCreated(IStage obj)
        {
            m_Stage = obj;
        }
    }
}
