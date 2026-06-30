using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Cloud.Common;
using Unity.Cloud.HighPrecision.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using TextField = Unity.AppUI.UI.TextField;
using System.Threading.Tasks;
using System.Collections;
using RaycastResult = Unity.Cloud.DataStreaming.Runtime.RaycastResult;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.Measurement
{
    struct MeasurementInfo
    {
        public MeasureSegment measureSegment;
        public MeasureLineData Data;
    }
    
    public class MeasurementToolUIController : StreamToolUIBase
    {
        public static float DraggablePadOffset;
        
        readonly int k_MaxNameLength = 64;
        private const string k_LineCreatorNameContainer = "line_creator_name_container";
        private const string k_LineCreatorConfirmationContainer = "line_creator_confirmation_container";
        private const string k_LineCreatorEditConfirmationContainer = "line_creator_edit_confirmation_container";
        private const string k_LineCreatorConfirmationClear = "line_creator_confirmation_clear";
        private const string k_LineCreatorConfirmationSave = "line_creator_confirmation_save";
        private const string k_LineCreatorUnitDropdown = "line_creator_unit_dropdown";
        private const string k_LineCreatorModeDropdown = "line_creator_mode_dropdown";
        private const string k_LineCreatorDistanceValue = "line_creator_distance_value";
        private const string k_SaveDialogFooterCancelButton = "SaveDialogFooterCancelButton";
        private const string k_SaveDialogNameField = "SaveDialogNameField";
        private const string k_SaveDialogFooterSaveButton = "SaveDialogFooterSaveButton";
        private const string k_LineCreatorNameValue = "line_creator_name_value";
        private const string k_LineCreatorEditConfirmationApply = "line_creator_edit_confirmation_apply";
        private const string k_LineListContainer = "line_list_container";
        private const string k_LineListScrollview = "line_list_scrollview";
        private const string k_LineCreatorEditConfirmationDiscard = "line_creator_edit_confirmation_discard";
        
        private MeasurementToolController m_MeasurementToolController;
        readonly List<IMeasureListItem> m_MeasureListItems = new();
        [SerializeField]
        private StyleSheet m_measurementToolStyle;
        private UIDocument m_measurementToolDocument;
        private Modal m_SaveModal;
        private ActionButton m_SavePanelCancelButton;
        private ActionButton m_SavePanelSaveButton;
        private TextField m_SavePanelNameField;
        private TextField m_NameField;
        [SerializeField]
        private Color m_SavedMeasureColor;
        [SerializeField]
        private Color m_SelectedMeasureColor;
        [SerializeField]
        private VisualTreeAsset m_MeasurementItemTemplate;
        
        private VisualElement m_LineCreatorNameContainer;
        private VisualElement m_LineCreatorConfirmationContainer;
        private VisualElement m_LineCreatorEditConfirmationContainer;
        private VisualElement m_LineListContainer;
        private ScrollView m_LineListScrollview;
        private ActionButton m_ClearButton;
        private ActionButton m_SaveButton;
        private ActionButton m_ApplyButton;
        private ActionButton m_DiscardButton;
        private Dropdown m_UnitDropdown;
        private Dropdown m_ModeDropdown;
        private Text m_ValueText;

        private Dictionary<string, MeasurementInfo> m_MeasurementInfos;
        private MeasurementInfo m_CurrentMeasurementInfo;
        private MeasureLineData m_LineDataBeforeEdit;
        
        [SerializeField]
        CursorController m_StartCursorController;
        [SerializeField]
        CursorController m_EndCursorController;
        CursorController m_CurrentActiveCursorController;
        [SerializeField]
        private DraggablePadController m_DraggablePadController;
        [SerializeField]
        private float offsetScale;

        [SerializeField] private VisualTreeAsset m_MeasurementSavePanel;
        [SerializeField] private StyleSheet m_MeasurementSavePanelStyle;
        
        [Header("Prefabs")]
        [SerializeField]
        MeasureSegment m_MeasureSegmentPrefab;
        
        #region Localization
        
        [Header("Localization")]
        [SerializeField]
        private LocalizedStringTable m_MeasureToolStringTable;
        [SerializeField]
        private LocalizedString m_AsSystemString;

        [SerializeField] private LocalizedString m_InfiniteWarning;
        
        #endregion
        
#if VR_MODE
        private XRPanel.CustomXRPanel m_SaveXRPanel;
#endif

        private void Start()
        {
            if (m_DraggablePadController != null)
            {
                m_DraggablePadController.NewCursorPosition += NewCursorPosition;
            }
            m_StartCursorController.ShowCursor(false);
            m_EndCursorController.ShowCursor(false);
            m_DraggablePadController.SetCursorController(null);
        }

        private void LateUpdate()
        {
            if (m_CurrentActiveCursorController != null)
            {
                DraggablePadOffset = m_CurrentActiveCursorController.transform.localScale.x * offsetScale;
                if (!m_DraggablePadController.IsDragging)
                {
                    m_DraggablePadController.transform.position = new Vector3(
                        m_CurrentActiveCursorController.transform.position.x,
                        m_CurrentActiveCursorController.transform.position.y - DraggablePadOffset,
                        m_CurrentActiveCursorController.transform.position.z);
                }
            }
        }
        
        private void OnDestroy()
        {
            if (m_DraggablePadController != null)
            {
                m_DraggablePadController.NewCursorPosition -= NewCursorPosition;
            }
            MeasurementToolController.UpdatedMeasurement -= OnUpdateMeasurement;
            MeasurementToolController.ResetCurrentMeasurement -= OnResetCurrentMeasurement;

            if (m_MeasurementInfos != null)
            {
                foreach (var measurementInfo in m_MeasurementInfos.Values)
                {
                    Destroy(measurementInfo.measureSegment.gameObject);
                }
            }

            if (m_CurrentMeasurementInfo.measureSegment != null)
            {
                Destroy(m_CurrentMeasurementInfo.measureSegment.gameObject);
            }
            
            if (m_PanelDocument != null)
            {
                m_PanelDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_measurementToolStyle);
            }
            
            UIUtility.RefreshEventSystem();
        }

        public override async void InitializeUI(UIDocument uiDocument, VisualElement parent, GameObject controller)
        {
            m_PanelDocument = uiDocument;
            if (!controller.TryGetComponent(out m_MeasurementToolController))
            {
                return;
            }
            
            MeasurementToolController.UpdatedMeasurement -= OnUpdateMeasurement;
            MeasurementToolController.ResetCurrentMeasurement -= OnResetCurrentMeasurement;
            MeasurementToolController.UpdatedMeasurement += OnUpdateMeasurement;
            MeasurementToolController.ResetCurrentMeasurement += OnResetCurrentMeasurement;
            
            m_PanelDocument.rootVisualElement.AddStyleSheetIfMissing(m_measurementToolStyle);
            
            m_LineCreatorNameContainer = parent.Q<VisualElement>(k_LineCreatorNameContainer);
            m_LineCreatorConfirmationContainer = parent.Q<VisualElement>(k_LineCreatorConfirmationContainer);
            m_ClearButton = m_LineCreatorConfirmationContainer.Q<ActionButton>(k_LineCreatorConfirmationClear);
            m_ClearButton.clicked += OnClearButtonClicked;
            
            m_SaveButton = m_LineCreatorConfirmationContainer.Q<ActionButton>(k_LineCreatorConfirmationSave);
            m_SaveButton.clicked += OnSaveButtonClicked;
#if UNITY_WEBGL
            m_SaveButton.SetEnabled(false);
#endif
            
            m_LineCreatorEditConfirmationContainer = parent.Q<VisualElement>(k_LineCreatorEditConfirmationContainer);
            m_LineCreatorEditConfirmationContainer?.DisplayOff();
            
            m_UnitDropdown = parent.Q<Dropdown>(k_LineCreatorUnitDropdown);
            
            var measurementFormat = new string[Enum.GetNames(typeof(MeasureFormat)).Length + 1];
            measurementFormat[0] = await m_AsSystemString.GetTitleLocalizedStringForAppUIAsync();
            for(var i = 1; i < measurementFormat.Length; i++)
            {
                measurementFormat[i] = $"@{m_MeasureToolStringTable}:" + (MeasureFormat)(i - 1);
            }
            
            m_UnitDropdown.bindItem = (item, index) => item.label = measurementFormat[index]; 
            m_UnitDropdown.sourceItems = measurementFormat;
            
            m_UnitDropdown.SetValueWithoutNotify(new []{0});
            m_UnitDropdown.RegisterValueChangedCallback(OnUnitDropdownValueChanged);
            
            m_ModeDropdown = parent.Q<Dropdown>(k_LineCreatorModeDropdown);
            var modes = Enum.GetNames(typeof(MeasureMode));
            m_ModeDropdown.bindItem = (item, index) => item.label = $"@{m_MeasureToolStringTable}:" + modes[index]; 
            m_ModeDropdown.sourceItems = modes;
            m_ModeDropdown.SetValueWithoutNotify(new []{0});
            m_ModeDropdown.RegisterValueChangedCallback(OnModeChange);
            
            m_ValueText = parent.Q<Text>(k_LineCreatorDistanceValue);
            m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0, m_MeasurementToolController.CurrentMeasureFormat);
            
            m_NameField = parent.Q<TextField>(k_LineCreatorNameValue);
            m_NameField.validateValue += ValidateValue;
            
            m_ApplyButton = parent.Q<ActionButton>(k_LineCreatorEditConfirmationApply);
            m_ApplyButton.clicked += ApplyButtonOnClicked;
            
            m_DiscardButton = parent.Q<ActionButton>(k_LineCreatorEditConfirmationDiscard);
            m_DiscardButton.clicked += DiscardButtonOnClicked;
            
            m_LineListContainer = parent.Q<VisualElement>(k_LineListContainer);
            
            m_LineListScrollview = parent.Q<ScrollView>(k_LineListScrollview);
            m_LineListScrollview.Clear();
            
            ResetUIToDefault();
#if !UNITY_WEBGL
            _ = ReadCollection();
#endif
            return;
            
            async Task ReadCollection()
            {
                var collections = await MeasureUnit.ReadCollections();
                if (collections == null) return;
                DistributeCollection(collections);
            }
        }

        public override void UninitializeUI()
        {
            if (m_ClearButton != null)
            {
                m_ClearButton.clicked -= OnClearButtonClicked;
            }

            if (m_SaveButton != null)
            {
                m_SaveButton.clicked -= OnSaveButtonClicked;
            }

            if (m_NameField != null)
            {
                m_NameField.validateValue -= ValidateValue;
            }
            
            if(m_ApplyButton != null)
            {
                m_ApplyButton.clicked -= ApplyButtonOnClicked;
            }
            
            if(m_DiscardButton != null)
            {
                m_DiscardButton.clicked -= DiscardButtonOnClicked;
            }
            m_ModeDropdown?.UnregisterValueChangedCallback(OnModeChange);
            m_UnitDropdown?.UnregisterValueChangedCallback(OnUnitDropdownValueChanged);
        }

        private void OnModeChange(ChangeEvent<IEnumerable<int>> evt)
        {
            if (m_MeasurementToolController == null) return;
            var newMode = (MeasureMode)evt.newValue.First();
            m_MeasurementToolController.SetMeasureMode(newMode);
        }

        private void ResetUIToDefault(bool resetUnit = true)
        {
            m_LineCreatorEditConfirmationContainer.DisplayOff();
            m_LineCreatorConfirmationContainer.DisplayOn();
            m_LineCreatorNameContainer.DisplayOff();
            m_LineListContainer.DisplayOn();
            var systemUnit = m_MeasurementToolController.CurrentMeasureFormat;
            if (resetUnit)
            {
                systemUnit = MeasureUnit.GetSystemUnit();
                m_MeasurementToolController.SetMeasureUnit(systemUnit);    
            }
            
            m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0, systemUnit);
            m_UnitDropdown.SetValueWithoutNotify(new []{0});
            m_UnitDropdown.SetEnabled(false);
            
            m_SaveButton.SetEnabled(false);
            m_ClearButton.SetEnabled(false);
        }

        private void DiscardButtonOnClicked()
        {
            ResetUIToDefault();
            var lineSegment = m_MeasurementInfos[m_LineDataBeforeEdit.Id].measureSegment;
            lineSegment.EndPosition = m_LineDataBeforeEdit.Anchors[1].Position;
            lineSegment.StartPosition = m_LineDataBeforeEdit.Anchors[0].Position;
            
            lineSegment.SetLabelText(m_LineDataBeforeEdit.GetFormattedDistanceString(m_LineDataBeforeEdit.MeasureFormat));

            m_MeasurementInfos.Remove(m_LineDataBeforeEdit.Id);
            var newData = new MeasurementInfo()
            {
                Data = MeasureLineData.Clone(m_LineDataBeforeEdit),
                measureSegment = lineSegment
            };
            m_MeasurementInfos.Add(m_LineDataBeforeEdit.Id, newData);
            
            var listItem = m_MeasureListItems.FirstOrDefault(x => x.Id == m_LineDataBeforeEdit.Id);
            listItem?.UpdateData(m_LineDataBeforeEdit);
            
            ClearAllSelection();
            
            Select(m_LineDataBeforeEdit);
            
            m_CurrentMeasurementInfo = new MeasurementInfo();
            m_MeasurementToolController.FinishedEdit(false, (_) =>
            {
                var systemUnityInt = (int)m_MeasurementToolController.CurrentMeasureFormat + 1;
                m_UnitDropdown.SetValueWithoutNotify(new[] { systemUnityInt });
            });
            m_StartCursorController.ShowCursor(false);
            m_EndCursorController.ShowCursor(false);
            m_CurrentActiveCursorController = null;
            m_DraggablePadController.SetCursorController(null);
        }

        private void ApplyButtonOnClicked()
        {
            m_CurrentMeasurementInfo.Data.Name = m_NameField.value;
            m_CurrentMeasurementInfo.Data.MeasureFormat = m_MeasurementToolController.CurrentMeasureFormat;
            var listItem = m_MeasureListItems.FirstOrDefault(x => x.Id == m_CurrentMeasurementInfo.Data.Id);
            listItem?.UpdateData(m_CurrentMeasurementInfo.Data);
            m_UnitDropdown.SetValueWithoutNotify(m_UnitDropdown.userData as IEnumerable<int>);
            m_MeasurementToolController.FinishedEdit(true, SavedCallBack);
            return;
            
            void SavedCallBack(bool saved)
            {
                ResetUIToDefault(false);
                ClearAllSelection();
                Select(m_CurrentMeasurementInfo.Data);
                m_CurrentMeasurementInfo = new MeasurementInfo();
                m_StartCursorController.ShowCursor(false);
                m_EndCursorController.ShowCursor(false);
                m_CurrentActiveCursorController = null;
                m_DraggablePadController.SetCursorController(null);
            }
        }

        private bool ValidateValue(string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                m_SavePanelSaveButton?.SetEnabled(false);
                m_ApplyButton?.SetEnabled(false);
                return false;
            }
            if (newValue.Length > k_MaxNameLength)
            {
                newValue = newValue.Substring(0, k_MaxNameLength);
                if (m_SavePanelNameField != null)
                {
                    m_SavePanelNameField.value = newValue;
                }

                m_NameField.value = newValue;
                return false;
            }

            if (string.IsNullOrWhiteSpace(newValue))
            {
                m_SavePanelSaveButton?.SetEnabled(false);
                m_ApplyButton?.SetEnabled(false);
                return false;
            }
            m_SavePanelSaveButton?.SetEnabled(true);
            m_ApplyButton?.SetEnabled(true);
            return true;
        }

        private void OnSaveButtonClicked()
        {
            var savePanel = m_MeasurementSavePanel.Instantiate().Children().First();
            
            m_SavePanelCancelButton = savePanel.Q<ActionButton>(k_SaveDialogFooterCancelButton);
            m_SavePanelCancelButton.clicked += SavePanelCancelButtonOnClicked;
            
            m_SavePanelSaveButton = savePanel.Q<ActionButton>(k_SaveDialogFooterSaveButton);
            m_SavePanelSaveButton.clicked += OnPanelSaveButtonClicked;
            
            m_SavePanelNameField = savePanel.Q<TextField>(k_SaveDialogNameField);
            m_SavePanelNameField.SetValueWithoutNotify(m_CurrentMeasurementInfo.Data.Name);
            m_SavePanelNameField.validateValue += ValidateValue;
            
#if !VR_MODE
            m_SaveModal = Modal.Build(m_SaveButton, savePanel);
            m_SaveModal.shown += OnSavePanelShown;
            m_SaveModal.dismissed += SavePanelModalOnDismissed;
            m_SaveModal.Show();
#else
            m_SaveXRPanel = new XRPanel.CustomXRPanel(string.Empty);
            XRPanel.Build(m_SaveXRPanel, savePanel).SetBackground(true).SetCloseButton(false).Build();
            m_SaveXRPanel.Shown += OnSavePanelShown;
            m_SaveXRPanel.Show();
#endif
            return;

#if !VR_MODE
            void SavePanelModalOnDismissed(Modal modal, DismissType arg2)
            { 
                modal.dismissed -= SavePanelModalOnDismissed;
                m_SaveModal = null;
                m_SavePanelSaveButton = null;
                m_PanelDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_MeasurementSavePanelStyle);
            }

            void OnSavePanelShown(Modal modal)
            {
                modal.shown -= OnSavePanelShown;
                m_PanelDocument.rootVisualElement.AddStyleSheetIfMissing(m_MeasurementSavePanelStyle);
            }
#else
            void OnSavePanelShown(XRPanel.CustomXRPanel panel)
            {
                panel.Shown -= OnSavePanelShown;
                panel.UIDocument.rootVisualElement.AddStyleSheetIfMissing(m_MeasurementSavePanelStyle);
            }
#endif
            
            void SavePanelCancelButtonOnClicked()
            {
                m_SaveModal?.Dismiss(DismissType.Cancel);
#if VR_MODE
                m_SaveXRPanel?.Dismiss();
#endif
            }
            
            void OnPanelSaveButtonClicked()
            {
                m_NameField.SetValueWithoutNotify(m_SavePanelNameField.value);
                m_MeasurementToolController.SaveCurrentMeasurement(m_SavePanelNameField.value, 
                    m_SavedMeasureColor,
                    m_UnitDropdown.value.First() == 0,
                    SavedCallback);
            }
        }

        private void SavedCallback(bool success, List<MeasureLineData> newCollection)
        {
            m_SaveModal?.Dismiss(DismissType.Action);
#if VR_MODE
            m_SaveXRPanel?.Dismiss();
#endif
            DistributeCollection(newCollection);
        }

        private void DistributeCollection(List<MeasureLineData> collection)
        {
            var oldShownItems = new List<string>();
            if (m_MeasureListItems != null && m_MeasureListItems.Any())
            {
                foreach (var item in m_MeasureListItems)
                {
                    if (item.IsShown && collection.Any(lineData => lineData.Id == item.Id))
                    {
                        oldShownItems.Add(item.Id);
                    }
                }
            }

            Clear();
            
            foreach (var lineData in collection)
            {
                AddMeasureLine(lineData);
            }

            if (oldShownItems.Any())
            {
                var shownItems = m_MeasureListItems.Where(item => oldShownItems.Contains(item.Id));
                foreach (var item in shownItems)
                {
                    item.Show(true);
                }
            }
        }

        private void AddMeasureLine(MeasureLineData lineData)
        {
            var item = new MeasureItem(lineData, m_MeasurementItemTemplate);
            item.OnClick += Select;
            item.OnView += OnView;
            item.OnEdit += OnEdit;
            item.OnDelete += OnDelete;

            m_MeasureListItems.Add(item);

            m_LineListScrollview.Add(item.Root);
            
            if(m_MeasurementInfos != null && m_MeasurementInfos.ContainsKey(lineData.Id)) return;
            m_MeasurementInfos ??= new Dictionary<string, MeasurementInfo>();
            var newMeasurementInfo = new MeasurementInfo
            {
                Data = lineData,
                measureSegment = Instantiate(m_MeasureSegmentPrefab)
            };
            
            m_MeasurementInfos.Add(lineData.Id, newMeasurementInfo);
            
            ApplyMeasureLineData(newMeasurementInfo.measureSegment, lineData, lineData.Color);
        }

        private void OnEdit(MeasureLineData lineData)
        {
            m_UnitDropdown.userData = m_UnitDropdown.value;
            MeasurementToolController.ResetCurrentMeasurement?.Invoke(false);
            m_LineCreatorEditConfirmationContainer.DisplayOn();
            m_LineCreatorConfirmationContainer.DisplayOff();
            m_LineCreatorNameContainer.DisplayOn();
            m_LineListContainer.DisplayOff();
            Select(lineData);
            m_LineDataBeforeEdit = MeasureLineData.Clone(lineData);
            m_NameField.SetValueWithoutNotify(lineData.Name);
            m_ValueText.text = lineData.GetFormattedDistanceString(lineData.MeasureFormat);
            m_UnitDropdown.SetValueWithoutNotify(new []{(int)lineData.MeasureFormat + 1});
            m_UnitDropdown.SetEnabled(true);
            m_ModeDropdown.SetEnabled(false);
            m_CurrentMeasurementInfo = m_MeasurementInfos[lineData.Id];
            m_MeasurementToolController.SetEditingMeasurement(lineData);
            SetCursorController(lineData.Anchors[0], m_SelectedMeasureColor, m_StartCursorController);
            SetCursorController(lineData.Anchors[1], m_SelectedMeasureColor, m_EndCursorController);
            ApplyMeasureLineData(m_CurrentMeasurementInfo.measureSegment, lineData, m_SelectedMeasureColor);
        }

        private void OnDelete(MeasureLineData deleteLine)
        {
            MeasureUnit.DeleteMeasurement(deleteLine, DeleteCallback);
            return;
            
            void DeleteCallback(bool deleted, List<MeasureLineData> newCollection)
            {
                if(!deleted) return;
                m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0f, MeasureUnit.GetSystemUnit());
                Destroy(m_MeasurementInfos[deleteLine.Id].measureSegment.gameObject);
                m_MeasurementInfos.Remove(deleteLine.Id);
                DistributeCollection(newCollection);
            }
        }

        private void OnView(MeasureLineData line, bool visible)
        {
            m_MeasurementInfos[line.Id].measureSegment.SetVisible(visible);
        }

        private void Select(MeasureLineData newSelectedLine)
        {
            string currentSelection = string.Empty;
            bool hasSelection = m_MeasureListItems.Any(x => x.IsSelected);
            if (hasSelection)
            {
                currentSelection = m_MeasureListItems.First(x => x.IsSelected).Id;
            }
            ClearAllSelection();
            m_UnitDropdown.SetValueWithoutNotify(new []{0});
            
            m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0f, MeasureUnit.GetSystemUnit());
            
            if (currentSelection == newSelectedLine.Id) return;
            
            var item = m_MeasureListItems.FirstOrDefault(i => i.Id == newSelectedLine.Id);
            if (item == null) return;
            item.Select(true);
            
            m_MeasurementInfos[newSelectedLine.Id].measureSegment.SetColor(m_SelectedMeasureColor);
            Debug.Log(newSelectedLine.MeasureFormat);
            m_ValueText.text = newSelectedLine.GetFormattedDistanceString(newSelectedLine.MeasureFormat);
            var measureFormatInt = (int)newSelectedLine.MeasureFormat + 1;
            m_UnitDropdown.SetValueWithoutNotify(new []{measureFormatInt});
        }

        private void ClearAllSelection()
        {
            if (m_MeasurementInfos != null)
            {
                foreach (var item in m_MeasurementInfos.Values)
                {
                    item.measureSegment.SetColor(m_SavedMeasureColor);
                }
            }

            if (m_MeasureListItems != null)
            {
                foreach (var measureListItem in m_MeasureListItems)
                {
                    measureListItem.Select(false);
                }
            }
        }

        private void Clear()
        {
            m_LineListScrollview?.Clear();
            m_MeasureListItems.Clear();
        }

        private void OnUnitDropdownValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            if (m_MeasurementToolController == null) return;
            var index = evt.newValue.First();
            MeasureFormat newFormat = MeasureUnit.GetSystemUnit();
            if (index != 0)
            {
                newFormat = (MeasureFormat)(index - 1);
            }
            m_MeasurementToolController.SetMeasureUnit(newFormat);
            float currentValue = 0; 
            if (m_CurrentMeasurementInfo.Data != null)
            {
                currentValue = m_CurrentMeasurementInfo.Data.DistanceInMeters;
                if (index == 0)
                {
                    m_CurrentMeasurementInfo.Data.MeasureFormat = MeasureUnit.GetSystemUnit();
                }
                else
                {
                    m_CurrentMeasurementInfo.Data.MeasureFormat = newFormat;
                }
            }

            var value = MeasureUnit.GetDistanceFormattedString(currentValue, newFormat);
            m_ValueText.text = value;
            if (m_CurrentMeasurementInfo.measureSegment != null)
            {
                m_CurrentMeasurementInfo.measureSegment.SetLabelText(value);
            }
        }

        private void OnClearButtonClicked()
        {
            StartCoroutine(WaitForUIUpdate());
            return;
            
            IEnumerator WaitForUIUpdate()
            {
                yield return new WaitForEndOfFrame();
                if(m_SaveModal != null) yield break;
                MeasurementToolController.ResetCurrentMeasurement?.Invoke(true);
            }
        }

        private void OnResetCurrentMeasurement(bool resetUnit)
        {
            if(m_CurrentMeasurementInfo.measureSegment != null)
                Destroy(m_CurrentMeasurementInfo.measureSegment.gameObject);
            m_CurrentMeasurementInfo = new MeasurementInfo();
            m_StartCursorController.ShowCursor(false);
            m_EndCursorController.ShowCursor(false);
            m_CurrentActiveCursorController = null;
            m_DraggablePadController.SetCursorController(null);
            m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0, m_MeasurementToolController.CurrentMeasureFormat);
            m_ClearButton.SetEnabled(false);
            m_SaveButton.SetEnabled(false);
            m_UnitDropdown.SetEnabled(false);
            if (resetUnit)
            {
                m_UnitDropdown.SetValueWithoutNotify(new []{0});
            }
            m_SaveButton.SetEnabled(false);
            m_ModeDropdown.SetEnabled(true);
        }
        
        private void SetCursorController(Anchor anchor, Color cursorColor, CursorController cursorController)
        {
            if (anchor == null || cursorController == null) return;
            if (!cursorController.IsCursorVisible())
            {
                MoveAnchorInstance(cursorController, cursorColor, anchor);
                m_CurrentActiveCursorController = cursorController;
                DraggablePadOffset = m_CurrentActiveCursorController.transform.localScale.x * offsetScale;
                m_DraggablePadController.SetCursorController(cursorController);
            }
        }
        
        private async void OnUpdateMeasurement()
        {
            if(m_MeasurementToolController == null || m_MeasurementToolController.MeasureLineData == null)
                return;
            
            ClearAllSelection();
            
            var measureLineData = m_MeasurementToolController.MeasureLineData;
            var firstAnchor = measureLineData.Anchors.Count > 0 ? measureLineData.Anchors[0] : null;
            var lastAnchor = measureLineData.Anchors.Count > 1 ? measureLineData.Anchors[^1] : null;
            
            m_ModeDropdown.SetEnabled(firstAnchor != null && lastAnchor == null);
            m_UnitDropdown.SetEnabled(firstAnchor != null);
            var measureFormatInt = (int)m_MeasurementToolController.CurrentMeasureFormat + 1;
            m_UnitDropdown.SetValueWithoutNotify(new []{measureFormatInt});
            
            if (firstAnchor != null && lastAnchor == null)
            {
                m_ValueText.text = MeasureUnit.GetDistanceFormattedString(0f, m_MeasurementToolController.CurrentMeasureFormat);
            }
            
            SetCursorController(firstAnchor, measureLineData.Color, m_StartCursorController);
            if (m_LineCreatorConfirmationContainer.IsDisplayOn())
            {
                m_ClearButton.SetEnabled(true);
            }
            if (m_MeasurementToolController.CurrentMeasureMode == MeasureMode.Orthogonal && lastAnchor == null)
            {
                // In orthogonal mode, only one anchor found, tell user that this is not a valid measurement
#if !VR_MODE
                var toast = Toast
                    .Build(m_LineCreatorConfirmationContainer,
                        await m_InfiniteWarning.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Warning);
                toast.Show();
#else
                var toast = XRToastPanel.Build(await m_InfiniteWarning.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Warning);
                toast.Show();
#endif
                return;
            }
            SetCursorController(lastAnchor, measureLineData.Color, m_EndCursorController);
            if (m_LineCreatorConfirmationContainer.IsDisplayOn())
            {
                m_ClearButton.SetEnabled(true);
            }
            
            if(firstAnchor == null || lastAnchor == null)
                return;
            
            if (m_LineCreatorConfirmationContainer.IsDisplayOn())
            {
#if !UNITY_WEBGL
                m_SaveButton.SetEnabled(true);
#endif
            }
            
            DrawLine(measureLineData);
        }
        
        private void NewCursorPosition(Vector3 newPosition, Ray? ray)
        {
            if(m_CurrentActiveCursorController == null) return;
#if !VR_MODE
            // Camera.main can be null when the active camera isn't tagged MainCamera (e.g. mobile AR);
            // abort the cursor update rather than NRE building the ray.
            var mainCamera = Camera.main;
            if (mainCamera == null) return;
            ray = new Ray(mainCamera.transform.position, (newPosition - mainCamera.transform.position).normalized);
#endif
            int index = m_CurrentActiveCursorController == m_StartCursorController ? 0 : 1;
            m_MeasurementToolController?.UpdateCurrentAnchorPosition(index, ray.Value, CallbackResult);
            return;

            void CallbackResult(RaycastResult result)
            {
                if(result.InstanceId == InstanceId.None) return;
                m_CurrentActiveCursorController.transform.SetPositionAndRotation(result.Point.ToVector3(), Quaternion.LookRotation(result.Normal));
                m_CurrentMeasurementInfo.Data = m_MeasurementToolController.MeasureLineData;
                if (index == 0)
                {
                    m_CurrentMeasurementInfo.measureSegment.StartPosition = m_CurrentMeasurementInfo.Data.Anchors[0].Position;
                }
                else
                {
                    m_CurrentMeasurementInfo.measureSegment.EndPosition = m_CurrentMeasurementInfo.Data.Anchors[1].Position;
                }
                
                var value = m_CurrentMeasurementInfo.Data.GetFormattedDistanceString(m_CurrentMeasurementInfo.Data
                    .MeasureFormat);
                m_CurrentMeasurementInfo.measureSegment.SetLabelText(value);
                m_ValueText.text = value;
            }
        }

        public void PickCursor(CursorController cursorController)
        {
            m_CurrentActiveCursorController = cursorController;
            m_DraggablePadController.SetCursorController(m_CurrentActiveCursorController);
        }
        
        private void DrawLine(MeasureLineData data)
        {
            m_CurrentMeasurementInfo = new MeasurementInfo()
            {
                Data =  data,
                measureSegment =  Instantiate(m_MeasureSegmentPrefab)
            };
            if (data.Anchors.Count >= 2)
            {
                ApplyMeasureLineData(m_CurrentMeasurementInfo.measureSegment, data, data.Color);
                m_ValueText.text = data.GetFormattedDistanceString(data.MeasureFormat);
                m_UnitDropdown.SetEnabled(true);
            }

            if (m_LineCreatorConfirmationContainer.IsDisplayOn())
            {
                m_ClearButton.SetEnabled(true);
            }
        }

        private static void ApplyMeasureLineData(MeasureSegment measureSegment, MeasureLineData data, Color color)
        {
            measureSegment.StartPosition = data.Anchors[0].Position;
            measureSegment.EndPosition = data.Anchors[1].Position;
            measureSegment.SetLabelText(data.GetFormattedDistanceString(data.MeasureFormat));
            measureSegment.SetColor(color);
        }

        private static void MoveAnchorInstance(CursorController cursorController, Color anchorColor, Anchor anchor)
        {
            if (anchor == null) return;
            cursorController.transform.SetPositionAndRotation(anchor.Position, Quaternion.LookRotation(anchor.Normal));
            cursorController.ShowCursor(true);
            cursorController.SetColor(anchorColor);
        }
    }
}
