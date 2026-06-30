using System;
using System.Collections.Generic;
using Unity.Cloud.DataStreaming.Metadata;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using UnityEngine.Localization;
using TextField = Unity.AppUI.UI.TextField;
using Unity.Industry.Viewer.Shared;

namespace Unity.Industry.Viewer.Streaming.Metadata
{
    public class MetadataToolUIController : StreamToolUIBase
    {
        private const string k_MetadataScrollViewName = "MetadataScrollView";
        private const string k_SearchFieldName = "SearchField";
        private const string k_SelectedClass = "Selected";
        
        private ScrollView m_MetadataScrollView;
        private TextField m_SearchField;
        
        [SerializeField]
        private StyleSheet m_MetadataPanelStyleSheet;
        
        [SerializeField]
        private LocalizedString m_NoMetadataFoundLocalizedString;
        
        [SerializeField]
        private LocalizedString m_OfflineAssetNotSupportedLocalizedString;
        
        [SerializeField]
        private LocalizedString m_OfflineModeNotSupportedLocalizedString;
        
        private List<MetadataInstance> m_MetadataInstancesFound;

        private void Start()
        {
            MetadataToolController.OfflineAssetSelected += OnOfflineAssetSelected;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
        }

        private void OnDestroy()
        {
            UninitializeUI();
            if (m_Controller != null)
            {
                m_Controller.ToolOpened -= OnToolOpened;
                m_Controller.ToolClosed -= OnToolClosed;
            }
            if (m_PanelDocument != null)
            {
                m_PanelDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_MetadataPanelStyleSheet);
            }
            MetadataToolController.OfflineAssetSelected -= OnOfflineAssetSelected;
            MetadataToolController.MetadataFound -= OnMetadataFound;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
        }

        private void OnNetworkStatusChanged(bool connected)
        {
            if (NetworkDetector.RequestedOfflineMode)
            {
                OnOfflineMode();
            } else if (!NetworkDetector.RequestedOfflineMode && connected)
            {
                CleanPanel();
            }
        }

        private async void OnOfflineMode()
        {
            CleanPanel();
            var text = new Text
            {
                text = await m_OfflineModeNotSupportedLocalizedString.GetTitleLocalizedStringForAppUIAsync()
            };
            m_MetadataScrollView.Add(text);
        }

        private async void OnOfflineAssetSelected()
        {
            CleanPanel();
            var text = new Text
            {
                text = await m_OfflineAssetNotSupportedLocalizedString.GetTitleLocalizedStringForAppUIAsync()
            };
            m_MetadataScrollView.Add(text);
        }

        private void CleanPanel()
        {
            m_SearchField.SetValueWithoutNotify(string.Empty);
            m_SearchField.SetEnabled(false);
            m_MetadataScrollView?.Clear();
        }

        public override void InitializeUI(UIDocument uiDocument, VisualElement parent, GameObject controller)
        {
            m_PanelDocument = uiDocument;
            if (m_PanelDocument == null) return;
            
            if (controller.TryGetComponent(out m_Controller))
            {
                m_Controller.ToolOpened += OnToolOpened;
                m_Controller.ToolClosed += OnToolClosed;
            }
            
            m_PanelDocument.rootVisualElement.AddStyleSheetIfMissing(m_MetadataPanelStyleSheet);
            
            m_MetadataScrollView = parent.Q<ScrollView>(k_MetadataScrollViewName);
            m_SearchField = parent.Q<TextField>(k_SearchFieldName);
            m_SearchField.SetEnabled(false);
            
            m_SearchField.RegisterValueChangedCallback(OnSearchFieldChanged);
            m_SearchField.RegisterValueChangingCallback(OnSearchFieldChanging);

            if (NetworkDetector.RequestedOfflineMode)
            {
                OnOfflineMode();
            }
            
            MetadataToolController.MetadataFound += OnMetadataFound;
        }

        private async void OnMetadataFound(List<MetadataInstance> found)
        {
            if (NetworkDetector.RequestedOfflineMode)
            {
                OnOfflineMode();
                return;
            }
            if (found == null)
            {
                CleanPanel();
                return;
            }
            
            m_MetadataInstancesFound ??= new List<MetadataInstance>();
            m_MetadataInstancesFound.Clear();
            m_MetadataInstancesFound.AddRange(found);
            
            foreach (var eachInstance in found)
            {
                GetMetadataObjectContent(eachInstance.Properties, string.Empty);
            }

            if (m_MetadataScrollView.childCount != 0)
            {
                m_SearchField.SetEnabled(true);
                return;
            }
            
            var text = new Text
            {
                text = await m_NoMetadataFoundLocalizedString.GetTitleLocalizedStringForAppUIAsync()
            };
            m_MetadataScrollView.Add(text);
        }
        
        private void OnSearchFieldChanging(ChangingEvent<string> evt)
        {
            if(m_MetadataInstancesFound == null || m_MetadataInstancesFound.Count == 0) return;
            OnSearchFiltered(evt.newValue);
        }

        private void OnSearchFieldChanged(ChangeEvent<string> evt)
        {
            if(m_MetadataInstancesFound == null || m_MetadataInstancesFound.Count == 0) return;
            OnSearchFiltered(evt.newValue);
        }

        private void OnSearchFiltered(string searchValue)
        {
            m_MetadataScrollView?.Clear();
            
            foreach (var eachInstance in m_MetadataInstancesFound)
            {
                GetMetadataObjectContent(eachInstance.Properties, searchValue);
            }
        }
        
        private void GetMetadataObjectContent(IReadOnlyDictionary<string, IMetadataValue> metadata, string searchValue, VisualElement parent = null)
        {
            foreach (var key in metadata.Keys)
            {
                bool add = false;
                if(string.IsNullOrEmpty(searchValue))
                {
                    add = true;
                }
                else
                {
                    if (key.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 || 
                        metadata[key].ToString().IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        add = true;
                    }
                }
                
                if(!add) continue;
                
                var metadataValue = metadata[key];

                GetMetadataObjectContent(key, metadataValue, parent);
            }
        }
        
        private void GetMetadataObjectContent(string key, IMetadataValue value, VisualElement parent)
        {
            var newMetadataContainer = new VisualElement();
            newMetadataContainer.AddToClassList("MetadataEntry");
            newMetadataContainer.RegisterCallback<ClickEvent>(OnMetadataEntryClicked);

            var newKeyText = new Text($"{key}");
            newKeyText.AddToClassList("MetadataKey");
            var newValueText = new Text(value.ToString());
            newValueText.AddToClassList("MetadataValue");
            newMetadataContainer.Add(newKeyText);
            newMetadataContainer.Add(newValueText);

            if (parent == null)
            {
                m_MetadataScrollView.Add(newMetadataContainer);
            }
            else
            {
                parent.Add(newMetadataContainer);
            }
        }

        private void OnMetadataEntryClicked(ClickEvent evt)
        {
            var allSelected = m_MetadataScrollView.Query(className: k_SelectedClass).ToList();;
            foreach (var selected in allSelected)
            {
                selected.RemoveFromClassList(k_SelectedClass);
            }
            
            var target = evt.currentTarget as VisualElement;
            if (target == null)
            {
                return;
            }
            var isSelected = target.ClassListContains(k_SelectedClass);
            if (isSelected)
            {
                target.RemoveFromClassList(k_SelectedClass);
            }
            else
            {
                target.AddToClassList(k_SelectedClass);
            }
        }

        private void OnToolOpened()
        {
            m_MetadataScrollView?.Clear();
            if (NetworkDetector.RequestedOfflineMode)
            {
                OnOfflineMode();
            }
        }
        
        private void OnToolClosed()
        {
            m_MetadataScrollView?.Clear();
        }

        public override void UninitializeUI()
        {
            m_MetadataScrollView?.Clear();
            m_SearchField?.UnregisterValueChangedCallback(OnSearchFieldChanged);
            m_SearchField?.UnregisterValueChangingCallback(OnSearchFieldChanging);
        }
    }
}
