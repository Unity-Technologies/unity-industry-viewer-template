using UnityEngine;
using Unity.Industry.Viewer.Streaming;
using Unity.AppUI.Core;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Button = Unity.AppUI.UI.Button;
using FloatField = Unity.AppUI.UI.FloatField;
using Toggle = Unity.AppUI.UI.Toggle;
using System.Linq;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using UnityEngine.Localization;

namespace Unity.Industry.Viewer.Navigation.MobileAR
{
    [DefaultExecutionOrder(-110)]
    public class MobileARUIController : NavigationOptionUI
    {
        #region Common
        private const string k_ContentName = "Content";
        private const string k_ResetButtonName = "ResetButton";
        private const string k_ConfirmButtonName = "ConfirmButton";
        private const string k_OcclusionToggleName = "OcclusionToggle";
        private const string k_TransformTabName = "TransformTab";
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
        
        private Button m_ResetToDefaultPositionButton,
            m_ConfirmPositionButton,
            m_SaveMapButton,
            m_LoadMapButton;
        TouchSliderFloat m_ScaleSliderField;
        Toggle m_OcclusionToggle;
        FloatField m_XPositionField, m_YPositionField, m_ZPositionField, m_MoveIncrementField, m_RotateIncrementField,
            m_XRotationField, m_YRotationField, m_ZRotationField;
        Tabs m_TransformTab;
        
        private Stepper m_MoveIncrementStepper, m_RotateIncrementStepper, m_XMoveStepper, m_YMoveStepper, m_ZMoveStepper,
            m_XRotateStepper, m_YRotateStepper, m_ZRotateStepper;
        
        VisualElement m_ContentContainer, m_MoveContainer, m_RotateContainer, m_ScaleContainer, m_SpatialContainer;
        
        [SerializeField]
        MobileARController m_ARController;
        
        private bool m_HasIntialised;

        private ARState m_CurrentState = ARState.Placing;

        #region Localisation
        
        [SerializeField]
        private LocalizedString m_lockPositionLocalizedString;
        
        [SerializeField]
        private LocalizedString m_UnlockPositionLocalizedString;
        
        [SerializeField]
        private LocalizedString m_ARAnchorAlignedLocalizedString;

        [SerializeField]
        private LocalizedString m_ARAnchorSavedLocalizedString;

        [SerializeField]
        private LocalizedString m_FailSaveARAnchorUnknownLocalizedString;

        [SerializeField]
        private LocalizedString m_ScanTipsLocalizedString;

        [SerializeField]
        private LocalizedString m_ConfirmTipsLocalizedString;

        [SerializeField]
        private LocalizedString m_LoadFailLocalizedString;
        
        [SerializeField]
        private LocalizedString m_FailSaveWriteAccessLocalizedString;
        
        #endregion

        private void Awake()
        {
            MobileARController.ARStateChange += OnARStateChanged;
            MobileARController.TwoFingerDragging += OnTwoFingerDragging;
            MobileARController.PinchScaling += OnPinchScaling;
            MobileARController.WorldMapSaveReady += OnWorldMapSaveReady;
            MobileARController.WorldAnchorFileSave += OnWorldAnchorFileSave;
            MobileARController.WorldAnchorFileLoad += OnWorldAnchorFileLoad;
            MobileARController.WorldAnchorAligned += OnWorldAnchorAligned;
            MobileARController.LoadMapError += OnLoadMapError;
        }

        private void Start()
        {
            m_ARController ??= GetComponent<MobileARController>();
            ToolPanelUIController.CloseToolPanel += OnCloseToolPanel;
        }

        private void OnDisable()
        {
            if (m_HasIntialised)
            {
                ToolPanelUIController.CloseToolPanel.Invoke();
            }
            
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
        }

        private void OnDestroy()
        {
            MobileARController.ARStateChange -= OnARStateChanged;
            MobileARController.TwoFingerDragging -= OnTwoFingerDragging;
            MobileARController.PinchScaling -= OnPinchScaling;
            MobileARController.WorldMapSaveReady -= OnWorldMapSaveReady;
            MobileARController.WorldAnchorFileSave -= OnWorldAnchorFileSave;
            MobileARController.WorldAnchorFileLoad -= OnWorldAnchorFileLoad;
            MobileARController.WorldAnchorAligned -= OnWorldAnchorAligned;
            MobileARController.LoadMapError -= OnLoadMapError;
            ToolPanelUIController.CloseToolPanel -= OnCloseToolPanel;
        }

        private void OnCloseToolPanel()
        {
            m_HasIntialised = false;
            if (m_CurrentState == ARState.Positioning)
            {
                MobileARController.ARStateChange?.Invoke(ARState.ConfirmPosition);
            }
        }

        private void OnLoadMapError(string errorMessage)
        {
            //Debug.LogError(errorMessage);
            SharedUIManager.HideLoadingModal();
            var toast = Toast.Build(m_SaveMapButton, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
            toast.shown += LoadMapErrorToast;
            toast.Show();
            return;
            
            void LoadMapErrorToast(Toast obj)
            {
                obj.shown -= LoadMapErrorToast;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_LoadFailLocalizedString);
            }
        }

        private void OnWorldAnchorAligned()
        {
            SharedUIManager.HideLoadingModal();
            m_ResetToDefaultPositionButton.SetEnabled(true);
            m_ConfirmPositionButton.SetEnabled(true);
            m_TransformTab.SetEnabled(true);
            m_MoveContainer.SetEnabled(true);
            m_RotateContainer.SetEnabled(true);
            m_ScaleContainer.SetEnabled(true);
            
            m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
            m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
            m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
                    
            m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
            m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
            m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);

            m_ScaleSliderField.SetValueWithoutNotify(TransformController.Instance.transform.localScale.x);
            
            var toast = Toast.Build(m_LoadMapButton, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Positive);
            
            toast.shown += AlignedToast;

            toast.Show();
            return;

            void AlignedToast(Toast obj)
            {
                obj.shown -= AlignedToast;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_ARAnchorAlignedLocalizedString);
            }
        }

        private void OnWorldAnchorFileLoad(bool fileFound)
        {
            m_LoadMapButton.SetEnabled(fileFound);
        }

        private void OnWorldAnchorFileSave(bool saved, string errorMessage)
        {
            SharedUIManager.HideLoadingModal();
            _ = m_ARController.CheckIfWorldMapExists();
            m_MoveContainer.SetEnabled(true);
            m_RotateContainer.SetEnabled(true);
            m_ScaleContainer.SetEnabled(true);
            m_TransformTab.SetEnabled(true);
            switch (m_ARController.CurrentARState)
            {
                case ARState.Positioning:
                    m_ResetToDefaultPositionButton.SetEnabled(false);
                    m_ConfirmPositionButton.SetEnabled(true);
                    break;
                
                case ARState.ConfirmPosition:
                    m_ResetToDefaultPositionButton.SetEnabled(true);
                    m_ConfirmPositionButton.SetEnabled(false);
                    break;
            }
            
            if (saved)
            {
                var toast = Toast.Build(m_SaveMapButton, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Positive);
                
                toast.shown += AnchorSavedToast;
                toast.Show();
            }
            else
            {
                var toast = Toast.Build(m_SaveMapButton, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
                toast.shown += AnchorSavedFailedToast;
                toast.Show();
            }

            return;

            void AnchorSavedToast(Toast obj)
            {
                obj.shown -= AnchorSavedToast;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_ARAnchorSavedLocalizedString);
            }
            
            void AnchorSavedFailedToast(Toast obj)
            {
                obj.shown -= AnchorSavedFailedToast;
                var text = obj.view.Q<LocalizedTextElement>("appui-toast__message");
                if (errorMessage.Contains("Forbidden") || errorMessage.Contains("Not Authorized"))
                {
                    text.SetBinding("text", m_FailSaveWriteAccessLocalizedString);
                }
                else
                {
                    text.SetBinding("text", m_ARAnchorSavedLocalizedString);
                }
            }
        }

        private void OnWorldMapSaveReady(bool saveReady)
        {
            if(IdentityController.GuestMode)
            {
                m_SaveMapButton?.SetEnabled(false);
                return;
            }

            if (!m_ARController.HasWriteAccess)
            {
                m_SaveMapButton?.SetEnabled(false);
                return;
            }
            m_SaveMapButton?.SetEnabled(saveReady);
        }
        
        private void OnPinchScaling()
        {
            if (!m_HasIntialised)
            {
                CreatePanel();
            }
            m_ContentContainer.SetEnabled(true);
            m_TransformTab.value = 2;
            m_ScaleSliderField.SetValueWithoutNotify(TransformController.Instance.transform.localScale.x);
        }

        private void OnTwoFingerDragging()
        {
            if (!m_HasIntialised)
            {
                CreatePanel();
            }
            
            m_ContentContainer.SetEnabled(true);
            
            m_TransformTab.value = 0;
            
            var position = TransformController.Instance.transform.position;
            m_XPositionField.SetValueWithoutNotify(position.x);
            m_YPositionField.SetValueWithoutNotify(position.y);
            m_ZPositionField.SetValueWithoutNotify(position.z);
        }

        private void OnARStateChanged(ARState newState)
        {
            m_CurrentState = newState;
            Toast toast = null;
            switch (newState)
            {
                case ARState.Placing:
                    string k_StreamingPanelName = "StreamingContainer";
                    var streamingContainer = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_StreamingPanelName);
                    toast = Toast.Build(streamingContainer, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Informative);
                    
                    toast.shown += ScanTipsToast;

                    toast.Show();
                    break;
                
                case ARState.Positioning:
                    if (!m_HasIntialised)
                    {
                        CreatePanel();
                    }
                    m_ResetToDefaultPositionButton.SetEnabled(true);
                    m_ContentContainer.SetEnabled(true);
                    m_ConfirmPositionButton.ClearBinding("title");
                    m_ConfirmPositionButton.SetBinding("title", m_lockPositionLocalizedString);
                    m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
                    m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
                    m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
                    
                    m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
                    m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
                    m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);

                    m_ScaleSliderField.SetValueWithoutNotify(TransformController.Instance.transform.localScale.x);

                    m_SpatialContainer.style.display = m_ARController.IsWorldMapSupported ? DisplayStyle.Flex : DisplayStyle.None;
                    toast = Toast.Build(m_ContentContainer, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Informative);
                    toast.shown += ShowConfirmToast;
                    toast.Show();
                    break;
                
                case ARState.ConfirmPosition:
                    if (!m_HasIntialised)
                    {
                        return;
                    }
                    m_ResetToDefaultPositionButton.SetEnabled(false);
                    m_ConfirmPositionButton.ClearBinding("title");
                    m_ConfirmPositionButton.SetBinding("title", m_UnlockPositionLocalizedString);
                    m_ContentContainer.SetEnabled(false);
                    m_SpatialContainer.style.display = m_ARController.IsWorldMapSupported ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
            }

            return;

            void ShowConfirmToast(Toast obj)
            {
                obj.shown -= ShowConfirmToast;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_ConfirmTipsLocalizedString);
            }
            
            void ScanTipsToast(Toast obj)
            {
                obj.shown -= ScanTipsToast;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_ScanTipsLocalizedString);
            }
        }

        protected override void InitialUI(VisualElement panel)
        {
            #region Common
            
            m_ContentContainer = panel.Q<VisualElement>(k_ContentName);
            
            m_ResetToDefaultPositionButton = panel.Q<Button>(k_ResetButtonName);
            m_ResetToDefaultPositionButton.clicked += ResetToDefaultPositionButtonOnClicked;
            
            m_ConfirmPositionButton = panel.Q<Button>(k_ConfirmButtonName);
            m_ConfirmPositionButton.clicked += ConfirmPositionButtonOnClicked;

            m_OcclusionToggle = panel.Q<Toggle>(k_OcclusionToggleName);
            m_OcclusionToggle.RegisterValueChangedCallback(OnOcclusionToggleChanged);
            m_OcclusionToggle.SetValueWithoutNotify(false);

            if (Application.platform == RuntimePlatform.Android || !m_ARController.MeshManagerSupported)
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

            m_MoveIncrementField.SetValueWithoutNotify(1f);
            
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

            m_ScaleSliderField = panel.Q<TouchSliderFloat>(k_ScaleSliderName);
            m_ScaleSliderField.incrementFactor = 0.01f;
            m_ScaleSliderField.RegisterValueChangingCallback(OnModelScaleChanging);
            m_ScaleSliderField.RegisterValueChangedCallback(OnModelScaleChanged);

            #endregion
            
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
            m_LoadMapButton.SetEnabled(m_ARController.isWorldMapFound);
#endif
            
            m_SpatialContainer.style.display = m_ARController.IsWorldMapSupported? DisplayStyle.Flex : DisplayStyle.None;
            #endregion

            m_HasIntialised = true;
        }

        private void OnZMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.MoveZPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_ZPositionField.SetValueWithoutNotify(position.z);
        }

        private void OnYMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.MoveYPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_YPositionField.SetValueWithoutNotify(position.y);
        }

        private void OnXMoveStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.MoveXPositionBy(m_MoveIncrementField.value * evt.newValue);
            var position = TransformController.Instance.transform.position;
            m_XPositionField.SetValueWithoutNotify(position.x);
        }

        private void OnZRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.RotateZBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_ZRotationField.SetValueWithoutNotify(rotation.z);
        }

        private void OnYRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.RotateYBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_YRotationField.SetValueWithoutNotify(rotation.y);
        }

        private void OnXRotateStepperChanged(ChangeEvent<int> evt)
        {
            m_ARController.RotateXBy(m_RotateIncrementField.value * evt.newValue);
            var rotation = TransformController.Instance.transform.rotation.eulerAngles;
            m_XRotationField.SetValueWithoutNotify(rotation.x);
        }

        private void OnRotateIncrementStepperChanged(ChangeEvent<int> evt)
        {
            m_RotateIncrementField.value += evt.newValue;
        }

        private void OnMoveIncrementStepperChanged(ChangeEvent<int> evt)
        {
            m_MoveIncrementField.value += evt.newValue;
        }

        public override void CreatePanel()
        {
            if (NavigationOptionUIAsset == null || m_HasIntialised) return;
            var navigationOptionUIAsset = NavigationOptionUIAsset.Instantiate().Children().First();
            ToolPanelUIController.OpenToolPanel(m_ARController.NavigationName, navigationOptionUIAsset);
            InitialUI(navigationOptionUIAsset);
            OnARStateChanged(m_CurrentState);
        }

        private void OnLoadMapButtonClicked()
        {
            SharedUIManager.ShowLoadingModal();
            m_SaveMapButton.SetEnabled(false);
            m_LoadMapButton.SetEnabled(false);
            m_MoveContainer.SetEnabled(false);
            m_RotateContainer.SetEnabled(false);
            m_ScaleContainer.SetEnabled(false);
            m_TransformTab.SetEnabled(false);
            m_ResetToDefaultPositionButton.SetEnabled(false);
            m_ConfirmPositionButton.SetEnabled(false);
            m_ARController?.LoadSpatialAnchor();
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
            m_ARController?.SaveSpatialAnchor();

            SharedUIManager.ShowLoadingModal();
        }

        void OnModelScaleChanged(ChangeEvent<float> evt)
        {
            m_ARController?.Scale(evt.newValue);
        }

        void OnModelScaleChanging(ChangingEvent<float> evt)
        {
            m_ARController?.Scale(evt.newValue);
        }
        
        void OnRotationZFieldChanged(ChangeEvent<float> evt)
        {
            m_ARController?.RotateZ(evt.newValue);
        }

        void OnRotationZFieldChanging(ChangingEvent<float> evt)
        {
            m_ARController?.RotateZ(evt.newValue);
        }
        
        void OnRotationYFieldChanged(ChangeEvent<float> evt)
        {
            m_ARController?.RotateY(evt.newValue);
        }

        void OnRotationYFieldChanging(ChangingEvent<float> evt)
        {
            m_ARController?.RotateY(evt.newValue);
        }
        
        void OnRotationXFieldChanged(ChangeEvent<float> evt)
        {
            m_ARController?.RotateX(evt.newValue);
        }

        void OnRotationXFieldChanging(ChangingEvent<float> evt)
        {
            m_ARController?.RotateX(evt.newValue);
        }
        
        void OnZPositionValueChanging(ChangingEvent<float> evt)
        {
            m_ARController?.MoveZPosition(evt.newValue);
        }

        void OnZPositionValueChanged(ChangeEvent<float> evt)
        {
            m_ARController?.MoveZPosition(evt.newValue);
        }
        
        void OnYPositionValueChanging(ChangingEvent<float> evt)
        {
            m_ARController?.MoveYPosition(evt.newValue);
        }

        void OnYPositionValueChanged(ChangeEvent<float> evt)
        {
            m_ARController?.MoveYPosition(evt.newValue);
        }
        
        void OnXPositionValueChanged(ChangeEvent<float> evt)
        {
            m_ARController?.MoveXPosition(evt.newValue);
        }
        
        void OnXPositionValueChanging(ChangingEvent<float> evt)
        {
            m_ARController?.MoveXPosition(evt.newValue);
        }

        private void OnTabChangedChanged(ChangeEvent<int> evt)
        {
            m_MoveContainer.style.display = evt.newValue == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_RotateContainer.style.display = evt.newValue == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            m_ScaleContainer.style.display = evt.newValue == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnOcclusionToggleChanged(ChangeEvent<bool> evt)
        {
            MobileARController.RequestOcclusionOnOff?.Invoke(evt.newValue);
        }

        private void ConfirmPositionButtonOnClicked()
        {
            if (m_CurrentState == ARState.Positioning)
            {
                MobileARController.ARStateChange?.Invoke(ARState.ConfirmPosition);
            } else if (m_CurrentState == ARState.ConfirmPosition)
            {
                MobileARController.ARStateChange?.Invoke(ARState.Positioning);
            }
        }

        private void ResetToDefaultPositionButtonOnClicked()
        {
            m_ARController.ResetPosition();
            m_ARController.ResetRotation();
            m_XPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.x);
            m_YPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.y);
            m_ZPositionField.SetValueWithoutNotify(TransformController.Instance.transform.position.z);
            
            m_XRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.x);
            m_YRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.y);
            m_ZRotationField.SetValueWithoutNotify(TransformController.Instance.transform.rotation.eulerAngles.z);
        }
    }
}
