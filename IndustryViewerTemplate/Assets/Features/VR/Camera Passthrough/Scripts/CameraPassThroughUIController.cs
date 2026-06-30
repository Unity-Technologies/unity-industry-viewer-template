using System;
using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using UnityEngine.UIElements;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Industry.Viewer.Assets;
using Button = Unity.AppUI.UI.Button;
using Toggle = Unity.AppUI.UI.Toggle;
using FloatField = Unity.AppUI.UI.FloatField;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using System.Collections;

namespace Unity.Industry.Viewer. VR.CameraPassThrough
{
    public class CameraPassThroughUIController: NavigationOptionUI
    {
        #region Common
        private const string k_ContentName = "ARContent";
        private const string k_ResetButtonName = "ResetButton";
        private const string k_ConfirmButtonName = "ConfirmButton";
        private const string k_OcclusionToggleName = "OcclusionToggle";
        private const string k_TransformTabName = "TransformTab";
        private const string k_PlaceOnSurfaceButtonName = "PlaceOnSurface";
        #endregion

        #region Position
        private const string k_MoveContainerName = "MoveContainer";
        private const string k_MoveIncrementStepper = "MoveStepper";
        private const string k_MoveIncrementFieldName = "MoveIncrementField";
        private const string k_MoveXStepperName = "MoveXStepper";
        private const string k_MoveYStepperName = "MoveYStepper";
        private const string k_MoveZStepperName = "MoveZStepper";
        private const string k_XPositionFieldName = "XMoveField";
        private const string k_YPositionFieldName = "YMoveField";
        private const string k_ZPositionFieldName = "ZMoveField";
        #endregion

        #region Rotation
        private const string k_RotateContainerName = "RotationContainer";
        private const string k_RotateIncrementStepper = "RotateStepper";
        private const string k_RotateIncrementFieldName = "RotateIncrementField";
        private const string k_RotateXStepperName = "RotateXStepper";
        private const string k_RotateYStepperName = "RotateYStepper";
        private const string k_RotateZStepperName = "RotateZStepper";
        private const string k_XRotationFieldName = "XRotateField";
        private const string k_YRotationFieldName = "YRotateField";
        private const string k_ZRotationFieldName = "ZRotateField";
        #endregion

        #region Scale
        private const string k_ScaleContainerName = "ScaleContainer";
        private const string k_ScaleSliderName = "ScaleSlider";
        #endregion

        #region Spatial Map

        private const string k_SpatialContainerName = "SpatialContainer";
        private const string k_SaveMapButtonName = "SaveMapButton";
        private const string k_LoadMapButtonName = "LoadMapButton";

        #endregion
        
        [SerializeField]
        private XRControllerMenu m_XRControllerMenu;
        
        private XRRoundButton m_ToolButton;
        private ActionButton m_PlaceOnSurfaceButton;
        private Button m_ResetToDefaultPositionButton,
            m_ConfirmPositionButton,
            m_SaveMapButton,
            m_LoadMapButton;
        
        SliderFloat m_ScaleSliderField;
        Toggle m_OcclusionToggle;
        FloatField m_XPositionField, m_YPositionField, m_ZPositionField, m_MoveIncrementField, m_RotateIncrementField,
            m_XRotationField, m_YRotationField, m_ZRotationField;
        Tabs m_TransformTab;
        
        private Stepper m_MoveIncrementStepper, m_RotateIncrementStepper, m_XMoveStepper, m_YMoveStepper, m_ZMoveStepper,
            m_XRotateStepper, m_YRotateStepper, m_ZRotateStepper;
        
        private bool m_HasInitialized = false;
        private CameraPassThroughController m_CameraPassThroughController;
        private ARState m_CurrentARState = ARState.Placing;
        
        VisualElement m_ContentContainer, m_MoveContainer, m_RotateContainer, m_ScaleContainer, m_SpatialContainer, m_Panel;
        
        private XRToolPanel m_XRToolPanel;
        
        #region Localisation
        
        [SerializeField]
        private LocalizedString m_lockPositionLocalizedString;
        
        [SerializeField]
        private LocalizedString m_UnlockPositionLocalizedString;
        
        #endregion
        
        [SerializeField]
        private StyleSheet m_StyleSheet;

        private void Start()
        {
            CameraPassThroughController.OnStateChange += OnARStateChange;
            m_CameraPassThroughController = GetComponent<CameraPassThroughController>();
            m_CameraPassThroughController.OnAssetPlaceOnSurfaceComplete -= OnAssetPlaceOnSurfaceComplete;
            m_CameraPassThroughController.OnAssetPlaceOnSurfaceComplete += OnAssetPlaceOnSurfaceComplete;
            ToolPanelUIController.CloseToolPanel += OnCloseToolPanel;
        }

        private void OnDisable()
        {
            if (m_HasInitialized)
            {
                ToolPanelUIController.CloseToolPanel.Invoke();
            }
            m_ToolButton?.RemoveFromHierarchy();
            m_ToolButton = null;
            
            #region Common
            if (m_ResetToDefaultPositionButton != null)
            {
                m_ResetToDefaultPositionButton.clicked -= ResetToDefaultPositionButtonOnClicked;
                m_ResetToDefaultPositionButton = null;
            }
            if (m_ConfirmPositionButton != null)
            {
                m_ConfirmPositionButton.clicked -= ConfirmPositionButtonOnClicked;
                m_ConfirmPositionButton = null;
            }

            if (m_PlaceOnSurfaceButton != null)
            {
                m_PlaceOnSurfaceButton.clicked -= OnPlaceOnSurfaceButtonClicked;
                m_PlaceOnSurfaceButton = null;
            }
            
            m_OcclusionToggle?.UnregisterValueChangedCallback(OnOcclusionToggleChanged);
            m_OcclusionToggle = null;
            m_TransformTab?.UnregisterValueChangedCallback(OnTabChangedChanged);
            m_TransformTab = null;
            #endregion
            
            #region Position

            if (m_MoveIncrementStepper != null)
            {
                m_MoveIncrementStepper.UnregisterValueChangedCallback(OnMoveIncrementStepperChanged);
                m_MoveIncrementStepper = null;
            }

            if (m_XMoveStepper != null)
            {
                m_XMoveStepper.UnregisterValueChangedCallback(OnXMoveStepperChanged);
                m_XMoveStepper = null;
            }

            if (m_YMoveStepper != null)
            {
                m_YMoveStepper.UnregisterValueChangedCallback(OnYMoveStepperChanged);
                m_YMoveStepper = null;
            }
            
            if (m_ZMoveStepper != null)
            {
                m_ZMoveStepper.UnregisterValueChangedCallback(OnZMoveStepperChanged);
                m_ZMoveStepper = null;
            }
            
            m_XPositionField?.UnregisterValueChangingCallback(OnXPositionValueChanging);
            m_XPositionField?.UnregisterValueChangedCallback(OnXPositionValueChanged);
            m_XPositionField = null;
            
            m_YPositionField?.UnregisterValueChangedCallback(OnYPositionValueChanged);
            m_YPositionField?.UnregisterValueChangingCallback(OnYPositionValueChanging);
            m_YPositionField = null;
            
            m_ZPositionField?.UnregisterValueChangedCallback(OnZPositionValueChanged);
            m_ZPositionField?.UnregisterValueChangingCallback(OnZPositionValueChanging);
            m_ZPositionField = null;

            #endregion
            
            #region Rotation
            
            if (m_RotateIncrementStepper != null)
            {
                m_RotateIncrementStepper.UnregisterValueChangedCallback(OnRotateIncrementStepperChanged);
                m_RotateIncrementStepper = null;
            }

            if (m_XRotateStepper != null)
            {
                m_XRotateStepper.UnregisterValueChangedCallback(OnXRotateStepperChanged);
                m_XRotateStepper = null;
            }

            if (m_YRotateStepper != null)
            {
                m_YRotateStepper.UnregisterValueChangedCallback(OnYRotateStepperChanged);
                m_YRotateStepper = null;
            }
            
            if (m_ZRotateStepper != null)
            {
                m_ZRotateStepper.UnregisterValueChangedCallback(OnZRotateStepperChanged);
                m_ZRotateStepper = null;
            }

            m_XRotationField?.UnregisterValueChangingCallback(OnRotationXFieldChanging);
            m_XRotationField?.UnregisterValueChangedCallback(OnRotationXFieldChanged);
            m_XRotationField = null;
            m_YRotationField?.UnregisterValueChangingCallback(OnRotationYFieldChanging);
            m_YRotationField?.UnregisterValueChangedCallback(OnRotationYFieldChanged);
            m_YRotationField = null;
            m_ZRotationField?.UnregisterValueChangingCallback(OnRotationZFieldChanging);
            m_ZRotationField?.UnregisterValueChangedCallback(OnRotationZFieldChanged);
            m_ZRotationField = null;
            
            #endregion
            
            #region Scale
            m_ScaleSliderField?.UnregisterValueChangingCallback(OnModelScaleChanging);
            m_ScaleSliderField?.UnregisterValueChangedCallback(OnModelScaleChanged);
            m_ScaleSliderField = null;
            #endregion
            
            #region Spatial Map
            if (m_SaveMapButton != null)
            {
                m_SaveMapButton.clicked -= OnSaveMapButtonClicked;
                m_SaveMapButton = null;
            }
            
            if (m_LoadMapButton != null)
            {
                m_LoadMapButton.clicked -= OnLoadMapButtonClicked;
                m_LoadMapButton = null;
            }
            #endregion

            if (m_CameraPassThroughController != null)
            {
                m_CameraPassThroughController.OnAssetPlaceOnSurfaceComplete -= OnAssetPlaceOnSurfaceComplete;
            }
        }

        private void OnDestroy()
        {
            CameraPassThroughController.OnStateChange -= OnARStateChange;
            ToolPanelUIController.CloseToolPanel -= OnCloseToolPanel;
        }
        
        private void OnPlaceOnSurfaceButtonClicked()
        {
            m_CameraPassThroughController?.PlaceOnSurface();
        }
        
        private void OnAssetPlaceOnSurfaceComplete()
        {
            var position = TransformController.Instance.transform.position;
            m_YPositionField.SetValueWithoutNotify(position.y);
        }
        
        private void OnCloseToolPanel()
        {
            m_HasInitialized = false;
            if (m_CurrentARState == ARState.Positioning)
            {
                CameraPassThroughController.OnStateChange?.Invoke(ARState.ConfirmPosition);
            }
        }

        private void OnARStateChange(ARState state)
        {
            bool justPlaced = m_CurrentARState == ARState.Placing;
            m_CurrentARState = state;
            switch (state)
            {
                case ARState.Positioning:
                    InitializeMenuButton();
                    if (!m_HasInitialized)
                    {
                        CreatePanel();
                    }

                    StartCoroutine(WaitForUpdateUI());
                    
                    m_ResetToDefaultPositionButton.SetEnabled(true);
                    m_ContentContainer.SetEnabled(true);
                    m_ConfirmPositionButton.title = m_lockPositionLocalizedString.GetTitleLocalizedStringForAppUI();
                    m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
                    m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
                    m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
                    
                    m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
                    m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
                    m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);

                    m_ScaleSliderField.SetValueWithoutNotify(TransformController.Instance.transform.localScale.x);

                    m_SpatialContainer.style.display = m_CameraPassThroughController.IsWorldMapSupported? DisplayStyle.Flex: DisplayStyle.None;
                    break;
                
                case ARState.ConfirmPosition:
                    InitializeMenuButton();
                    if (!m_HasInitialized)
                    {
                        return;
                    }
                    m_ResetToDefaultPositionButton.SetEnabled(false);
                    m_ConfirmPositionButton.title =
                        m_UnlockPositionLocalizedString.GetTitleLocalizedStringForAppUI();
                    m_ContentContainer.SetEnabled(false);
                    m_SpatialContainer.style.display = m_CameraPassThroughController.IsWorldMapSupported? DisplayStyle.Flex: DisplayStyle.None;
                    break;
            }
            return;

            IEnumerator WaitForUpdateUI()
            {
                yield return new WaitForEndOfFrame();
                m_TransformTab.value = justPlaced? 2 : m_TransformTab.value;
            }
        }

        private void InitializeMenuButton()
        {
            if(m_ToolButton != null) return;
            m_XRControllerMenu ??= new XRControllerMenu();
            m_XRControllerMenu.Initialize();
            m_ToolButton = new XRRoundButton
            {
                primary = false
            };
            m_ToolButton.TopPadding = 20f;
            m_ToolButton.AddToClassList("MainToolIcon");
            m_ToolButton.IconTexture = NavigationIcon;
            m_ToolButton.clicked += TogglePanel;
            m_ToolButton.primary = m_HasInitialized;
            m_XRControllerMenu.Add(m_ToolButton);
        }

        private void TogglePanel()
        {
            if (m_HasInitialized)
            {
                ToolPanelUIController.CloseToolPanel?.Invoke();
                return;
            }
            CreatePanel();
        }

        protected override void InitialUI(VisualElement panel)
        {
            #region Common

            m_Panel = panel;
            
            m_ContentContainer = panel.Q<VisualElement>(k_ContentName);
            
            m_ResetToDefaultPositionButton = panel.Q<Button>(k_ResetButtonName);
            m_ResetToDefaultPositionButton.clicked += ResetToDefaultPositionButtonOnClicked;
            
            m_ConfirmPositionButton = panel.Q<Button>(k_ConfirmButtonName);
            m_ConfirmPositionButton.clicked += ConfirmPositionButtonOnClicked;

            m_OcclusionToggle = panel.Q<Toggle>(k_OcclusionToggleName);
            m_OcclusionToggle.RegisterValueChangedCallback(OnOcclusionToggleChanged);
            m_OcclusionToggle.SetValueWithoutNotify(false);

            if (!m_CameraPassThroughController.MeshManagerSupported)
            {
                m_OcclusionToggle.parent.style.display = DisplayStyle.None;
            }
            
            m_TransformTab = panel.Q<Tabs>(k_TransformTabName);
            m_TransformTab.RegisterValueChangedCallback(OnTabChangedChanged);
            
            m_MoveContainer = panel.Q<VisualElement>(k_MoveContainerName);
            m_RotateContainer = panel.Q<VisualElement>(k_RotateContainerName);
            m_ScaleContainer = panel.Q<VisualElement>(k_ScaleContainerName);
            
            #endregion

            #region Position

            m_MoveIncrementStepper = panel.Q<Stepper>(k_MoveIncrementStepper);
            m_MoveIncrementStepper.RegisterValueChangedCallback(OnMoveIncrementStepperChanged);
            
            m_XMoveStepper = panel.Q<Stepper>(k_MoveXStepperName);
            m_XMoveStepper.RegisterValueChangedCallback(OnXMoveStepperChanged);

            m_YMoveStepper = panel.Q<Stepper>(k_MoveYStepperName);
            m_YMoveStepper.RegisterValueChangedCallback(OnYMoveStepperChanged);
            
            m_ZMoveStepper = panel.Q<Stepper>(k_MoveZStepperName);
            m_ZMoveStepper.RegisterValueChangedCallback(OnZMoveStepperChanged);
            
            m_MoveIncrementField = panel.Q<FloatField>(k_MoveIncrementFieldName);
            
            m_XPositionField = panel.Q<FloatField>(k_XPositionFieldName);
            m_YPositionField = panel.Q<FloatField>(k_YPositionFieldName);
            m_ZPositionField = panel.Q<FloatField>(k_ZPositionFieldName);
            
            m_XPositionField.RegisterValueChangingCallback(OnXPositionValueChanging);
            m_XPositionField.RegisterValueChangedCallback(OnXPositionValueChanged);
            
            m_YPositionField.RegisterValueChangedCallback(OnYPositionValueChanged);
            m_YPositionField.RegisterValueChangingCallback(OnYPositionValueChanging);
            
            m_ZPositionField.RegisterValueChangedCallback(OnZPositionValueChanged);
            m_ZPositionField.RegisterValueChangingCallback(OnZPositionValueChanging);

            m_MoveIncrementField.SetValueWithoutNotify(0.1f);
            
            #endregion

            #region Rotation
            m_RotateIncrementField = panel.Q<FloatField>(k_RotateIncrementFieldName);
            
            m_RotateIncrementStepper = panel.Q<Stepper>(k_RotateIncrementStepper);
            m_RotateIncrementStepper.RegisterValueChangedCallback(OnRotateIncrementStepperChanged);
            
            m_XRotateStepper = panel.Q<Stepper>(k_RotateXStepperName);
            m_XRotateStepper.RegisterValueChangedCallback(OnXRotateStepperChanged);

            m_YRotateStepper = panel.Q<Stepper>(k_RotateYStepperName);
            m_YRotateStepper.RegisterValueChangedCallback(OnYRotateStepperChanged);
            
            m_ZRotateStepper = panel.Q<Stepper>(k_RotateZStepperName);
            m_ZRotateStepper.RegisterValueChangedCallback(OnZRotateStepperChanged);
            
            m_XRotationField = panel.Q<FloatField>(k_XRotationFieldName);
            m_XRotationField.RegisterValueChangingCallback(OnRotationXFieldChanging);
            m_XRotationField.RegisterValueChangedCallback(OnRotationXFieldChanged);
            m_YRotationField = panel.Q<FloatField>(k_YRotationFieldName);
            m_YRotationField.RegisterValueChangingCallback(OnRotationYFieldChanging);
            m_YRotationField.RegisterValueChangedCallback(OnRotationYFieldChanged);
            m_ZRotationField = panel.Q<FloatField>(k_ZRotationFieldName);
            m_ZRotationField.RegisterValueChangingCallback(OnRotationZFieldChanging);
            m_ZRotationField.RegisterValueChangedCallback(OnRotationZFieldChanged);

            m_RotateIncrementField.SetValueWithoutNotify(5f);
            
            #endregion

            #region Scale

            m_ScaleSliderField = panel.Q<SliderFloat>(k_ScaleSliderName);
            m_ScaleSliderField.style.display = DisplayStyle.Flex;
            m_ScaleSliderField.RegisterValueChangingCallback(OnModelScaleChanging);
            m_ScaleSliderField.RegisterValueChangedCallback(OnModelScaleChanged);

            #endregion
            
            m_PlaceOnSurfaceButton = panel.Q<ActionButton>(k_PlaceOnSurfaceButtonName);
            m_PlaceOnSurfaceButton.clicked += OnPlaceOnSurfaceButtonClicked;
            
            #region Spatial Map
            m_SpatialContainer = panel.Q<VisualElement>(k_SpatialContainerName);
            
            m_SaveMapButton = panel.Q<Button>(k_SaveMapButtonName);
            m_SaveMapButton.clicked += OnSaveMapButtonClicked;
            m_SaveMapButton.SetEnabled(false);
                
            m_LoadMapButton = panel.Q<Button>(k_LoadMapButtonName);
            m_LoadMapButton.clicked += OnLoadMapButtonClicked;
#if !UNITY_IOS
            m_LoadMapButton.SetEnabled(false);
#else
            m_LoadMapButton.SetEnabled(m_CameraPassThroughController.isWorldMapFound);
#endif
            
            m_SpatialContainer.style.display = m_CameraPassThroughController.IsWorldMapSupported? DisplayStyle.Flex : DisplayStyle.None;
            #endregion
            
            m_Panel.RegisterCallback<DetachFromPanelEvent>(OnPanelDetachFromPanel);
            
            m_ScaleSliderField.SetValueWithoutNotify(TransformController.Instance.transform.localScale.x);
            
            m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
            m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
            m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
            
            m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
            m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
            m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);
            
            m_HasInitialized = true;
            
            if (m_CurrentARState == ARState.ConfirmPosition)
            {
                m_ConfirmPositionButton.title =
                    m_UnlockPositionLocalizedString.GetTitleLocalizedStringForAppUI();
                m_ResetToDefaultPositionButton?.SetEnabled(false);
                m_ScaleContainer.DisplayOff();
                m_RotateContainer.DisplayOff();
                m_MoveContainer.DisplayOn();
                m_ContentContainer.SetEnabled(false);
            }
            else
            {
                m_ConfirmPositionButton.title = m_lockPositionLocalizedString.GetTitleLocalizedStringForAppUI();
            }

            m_XRToolPanel ??= FindFirstObjectByType<XRToolPanel>();
            if(m_XRToolPanel == null) return;
            m_XRToolPanel.UIDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);
        }
        
        private void OnSaveMapButtonClicked()
        {
            m_SaveMapButton.SetEnabled(false);
            m_LoadMapButton.SetEnabled(false);
            m_MoveContainer.SetEnabled(false);
            m_RotateContainer.SetEnabled(false);
            m_ScaleContainer.SetEnabled(false);
            m_TransformTab.SetEnabled(false);
            m_ResetToDefaultPositionButton.SetEnabled(false);
            m_ConfirmPositionButton.SetEnabled(false);
            //Current disabled due to not being supported
            //m_CameraPassThroughController?.SaveSpatialAnchor();

            LoadingUIPanel.ShowLoadingPanel?.Invoke(null);
        }
        
        private void OnLoadMapButtonClicked()
        {
            LoadingUIPanel.ShowLoadingPanel?.Invoke(null);
            m_SaveMapButton.SetEnabled(false);
            m_LoadMapButton.SetEnabled(false);
            m_MoveContainer.SetEnabled(false);
            m_RotateContainer.SetEnabled(false);
            m_ScaleContainer.SetEnabled(false);
            m_TransformTab.SetEnabled(false);
            m_ResetToDefaultPositionButton.SetEnabled(false);
            m_ConfirmPositionButton.SetEnabled(false);
            //Current disabled due to not being supported
            //m_CameraPassThroughController?.LoadSpatialAnchor();
        }
        
        void OnModelScaleChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.Scale(evt.newValue);
        }

        void OnModelScaleChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.Scale(evt.newValue);
        }
        
        void OnRotationZFieldChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateZ(evt.newValue);
        }

        void OnRotationZFieldChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateZ(evt.newValue);
        }
        
        void OnRotationYFieldChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateY(evt.newValue);
        }

        void OnRotationYFieldChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateY(evt.newValue);
        }
        
        void OnRotationXFieldChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateX(evt.newValue);
        }

        void OnRotationXFieldChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.RotateX(evt.newValue);
        }
        
        private void OnZRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.RotateZBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_ZRotationField.SetValueWithoutNotify(rotation.z);
        }

        private void OnYRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.RotateYBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_YRotationField.SetValueWithoutNotify(rotation.y);
        }

        private void OnXRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.RotateXBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_XRotationField.SetValueWithoutNotify(rotation.x);
        }
        
        private void OnPanelDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_Panel.UnregisterCallback<DetachFromPanelEvent>(OnPanelDetachFromPanel);
            m_HasInitialized = false;
            if (m_ToolButton != null)
            {
                m_ToolButton.primary = false;
            }
            if(m_XRToolPanel == null) return;
            m_XRToolPanel.UIDocument.rootVisualElement.RemoveStyleSheetIfPresent(m_StyleSheet);
        }
        
        private void OnRotateIncrementStepperChanged(ChangeEvent<int> evt)
        {
            m_RotateIncrementField.value = Mathf.Max(5f, m_RotateIncrementField.value + evt.newValue * 5f);
        }
        
        void OnZPositionValueChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveZPosition(evt.newValue);
        }

        void OnZPositionValueChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveZPosition(evt.newValue);
        }
        
        void OnYPositionValueChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveYPosition(evt.newValue);
        }

        void OnYPositionValueChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveYPosition(evt.newValue);
        }
        
        void OnXPositionValueChanged(ChangeEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveXPosition(evt.newValue);
        }
        
        void OnXPositionValueChanging(ChangingEvent<float> evt)
        {
            m_CameraPassThroughController?.MoveXPosition(evt.newValue);
        }

        private void OnZMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.MoveZPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_ZPositionField.SetValueWithoutNotify(position.z);
        }

        private void OnYMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.MoveYPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_YPositionField.SetValueWithoutNotify(position.y);
        }

        private void OnXMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_CameraPassThroughController.MoveXPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_XPositionField.SetValueWithoutNotify(position.x);
        }
        
        private void OnMoveIncrementStepperChanged(ChangeEvent<int> evt)
        {
            m_MoveIncrementField.value = Mathf.Max(0.1f, m_MoveIncrementField.value + evt.newValue * 0.1f);
        }
        
        private void OnTabChangedChanged(ChangeEvent<int> evt)
        {
            m_MoveContainer.style.display = evt.newValue == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_RotateContainer.style.display = evt.newValue == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            m_ScaleContainer.style.display = evt.newValue == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void OnOcclusionToggleChanged(ChangeEvent<bool> evt)
        {
            CameraPassThroughController.RequestOcclusionOnOff?.Invoke(evt.newValue);
        }
        
        private void ConfirmPositionButtonOnClicked()
        {
            if (m_CurrentARState == ARState.Positioning)
            {
                CameraPassThroughController.OnStateChange?.Invoke(ARState.ConfirmPosition);
            } else if (m_CurrentARState == ARState.ConfirmPosition)
            {
                CameraPassThroughController.OnStateChange?.Invoke(ARState.Positioning);
            }
        }

        public override void CreatePanel()
        {
            //if(m_CurrentARState == ARState.Initializing || m_CurrentARState == ARState.Placing) return;
            if (NavigationOptionUIAsset == null || m_HasInitialized) return;
            StreamToolsController.DisableAllTools?.Invoke(true);
            if (ToolPanelUIController.IsOpened)
            {
                ToolPanelUIController.CloseToolPanel?.Invoke();
            }
            var navigationOptionUIAsset = NavigationOptionUIAsset.Instantiate().Children().First();
            ToolPanelUIController.OpenToolPanel(m_CameraPassThroughController.NavigationName, navigationOptionUIAsset, false);
            InitialUI(navigationOptionUIAsset);
            if (m_ToolButton != null)
            {
                m_ToolButton.primary = true;
            }
            m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
            m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
            m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
                    
            m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
            m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
            m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);
        }
        
        private void ResetToDefaultPositionButtonOnClicked()
        {
            m_CameraPassThroughController.ResetPosition();
            m_CameraPassThroughController.ResetRotation();
            m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
            m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
            m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
            
            m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
            m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
            m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);
        }
    }
}
