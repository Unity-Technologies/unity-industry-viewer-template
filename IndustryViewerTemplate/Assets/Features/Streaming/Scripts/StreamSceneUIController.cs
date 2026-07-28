using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Unity.Cloud.DataStreaming.Runtime;
using Unity.AppUI.Core;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Localization;
using Unity.Industry.Viewer.Identity;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cloud.Assets;
using TextField = Unity.AppUI.UI.TextField;
using Unity.Industry.Viewer.AppSettings;
using Toggle = Unity.AppUI.UI.Toggle;
using UnityEngine.Rendering.Universal;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamSceneUIController : MonoBehaviour
    {
        private const string k_BackgroundContainerName = "BackgroundContainer";
        
        private const string k_StreamingPanelName = "StreamingContainer";

        private const string k_AssetLoaderName = "AssetLoader";
        protected const string k_AssetTitle = "AssetTitle";
        protected const string k_NewVersionButtonName = "VersionButton";
        private const string k_TopLeftBarName = "TopLeftBar";
        private const string k_TopRightBarName = "TopRightBar";
        private const string k_LayoutAssetNameTextFieldName = "LayoutAssetNameTextfield";
        private const string k_OrganizationDropdownName = "OrganizationDropdown";
        private const string k_ProjectDropdownName = "ProjectDropdown";
        private const string k_CollectionDropDownName = "CollectionDropdown";
        private const string k_CancelButtonName = "CancelButton";
        private const string k_SaveLayoutButtonName = "SaveLayoutButton";
        
        public static Action ShowFailToAddModelToast;
        public static Action<AssetInfo, AssetInfo, string, string> ShowPickSourceDialog;
        
        [SerializeField]
        private VisualTreeAsset m_streamingBarTemplate;
        
        [SerializeField]
        private StyleSheet m_StyleSheet;
        
        private VisualElement m_BackgroundContainer;
        private VisualElement m_TopStreamingBar;
        private ActionButton m_OrganizationButton => SharedUIManager.Instance.OrganizationButton;
        private VisualElement m_StreamingRoot;
        private IconButton m_BackButton;
        protected Text m_TitleText;
        protected ActionButton m_NewVersionButton;
        protected VisualElement m_AssetLoader;
        private Toast m_SaveErrorMessageToast;
        private int defaultAvatarPosition;

        [Header("Rendering Settings")]
        [SerializeField] private LocalizedString m_RenderingTitle;
        [SerializeField] protected VisualTreeAsset m_PostProcessSettingsUI;
        private Toggle m_PostProcessingToggle;

        [SerializeField] protected VisualTreeAsset m_CoincidenceBiasSettingsUI;
        [SerializeField, Tooltip("Default coincident-surface separation, in metres, applied when the " +
            "feature is on (also the initial slider position). 0.0003 = 0.3 mm.")]
        private float m_CoincidenceBias = 0.0003f;
        [SerializeField, Tooltip("Separation at 100% strength, in metres. The strength slider maps " +
            "0-100% onto 0..this. Lower it for tiny manufacturing-scale models.")]
        private float m_MaxCoincidenceBias = 0.002f;
        private Toggle m_CoincidenceBiasToggle;
        private BaseSlider<float, float> m_CoincidenceBiasSlider;
        // Remembered separation (metres) so toggling off then on restores the last strength.
        private float m_CurrentBias;
        private const string k_CoincidenceBiasToggleName = "CoincidenceBiasToggle";
        private const string k_CoincidenceBiasSliderName = "CoincidenceBiasStrengthSlider";
        // Must match the shader graph global property's Reference name ("_Bias").
        private static readonly int k_CoincidenceBiasId = Shader.PropertyToID("_Bias");

        // VR controllers can't drag the App UI TouchSliderFloat, so the VR subclass overrides this to use
        // the regular SliderFloat (arrows / input field, controller-friendly) instead.
        protected virtual bool CoincidenceBiasUsesTouchSlider => true;
        
        public IStage Stage => m_Stage;
        IStage m_Stage;
        
        IDataStreamer m_DataStreamer => PlatformServices.DataStreamer;
        
        private IconButton m_SaveLayoutButton;

        [SerializeField] protected VisualTreeAsset m_SavePanelTemplate;
        private TextField m_SaveLayoutNameTextField;
        private Dropdown m_OrganizationDropdown, m_ProjectDropdown, m_CollectionDropDown;
        private ActionButton m_CancelButton, m_ConfirmSaveLayoutButton;
        private Modal m_SaveLayoutModal;
        private bool m_HasWritePermission;
        private AuthenticationState m_AuthenticationState;
        protected WaitForEndOfFrame m_WaitForEndOfFrame;
        
        #region Localisation
        [SerializeField] protected LocalizedString m_AssetLoadFailureToast;
        [SerializeField] protected LocalizedString m_AddTitle;
        [SerializeField] protected LocalizedString m_AddDescription;
        [SerializeField] protected LocalizedString m_CloudOption;
        [SerializeField] protected LocalizedString m_LocalOption;
        [SerializeField] protected LocalizedString m_CancelOption;
        
        [SerializeField]
        protected LocalizedString m_TitleVersionLocalizedString;

        [SerializeField]
        protected LocalizedString m_NewVersionTitleLocalizedString;

        [SerializeField]
        protected LocalizedString m_NewVersionDescriptionLocalizedString;

        [SerializeField]
        protected LocalizedString m_SwitchLocalizedString;

        [SerializeField]
        protected LocalizedString m_DismissLocalizedString;

        [SerializeField]
        protected LocalizedString m_ExitTitleLocalizedString;

        [SerializeField]
        protected LocalizedString m_ExitLocalizedString;

        [SerializeField]
        protected LocalizedString m_ExitDescriptionLocalizedString;
        
        [SerializeField]
        protected LocalizedString m_NoWritePermissionLocalizedString;

        [SerializeField]
        private LocalizedString m_SaveFailureLocalizedString;
        
        #endregion

        private void Awake()
        {
            m_DataStreamer.StageCreated.Subscribe(OnStageCreated);
            m_DataStreamer.StageDestroyed.Subscribe(OnStageDestroy);
        }

        // Start is called before the first frame update
        protected virtual void Start()
        {
            StreamingModelController.FinishedAddingModel += OnLayoutLoaded;
            TransformController.ModelAdded += OnModelAdded;
            TransformController.ModelRemoved += OnModelRemoved;
            IdentityController.AuthenticationStateChangedEvent += OnAuthenticationStateChanged;
            StreamingModelController.LoadingGLBModel += ShowLoading;
            ShowFailToAddModelToast += ShowFailToAddModelToastHandler;
            ShowPickSourceDialog += ShowPickSourceDialogHandler;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            InitializeUI();
            AssetsController.AssetSelected -= OnAssetSelected;
            AssetsController.AssetSelected += OnAssetSelected;
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            AssetsController.NewVersionAvailable += OnNewVersionAvailable;
            StreamingModel.OnActivityStateChanged -= OnActivityStateChanged;
            StreamingModel.OnActivityStateChanged += OnActivityStateChanged;
            InAppSettings.SettingsPanelShow += OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed += OnSettingsPanelDismissed;

            // Coincidence-bias z-fighting mitigation defaults ON; the settings toggle turns it off and
            // the strength slider tunes the amount. base.Start() runs for the VR subclass too, so this
            // applies in both streaming scenes.
            m_CurrentBias = m_CoincidenceBias;
            Shader.SetGlobalFloat(k_CoincidenceBiasId, m_CurrentBias);
        }

        protected virtual void OnDestroy()
        {
            ExitSceneUIHandler();
            StreamingModel.OnActivityStateChanged -= OnActivityStateChanged;
            StreamingModelController.FinishedAddingModel -= OnLayoutLoaded;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            TransformController.ModelAdded -= OnModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            InAppSettings.SettingsPanelShow -= OnSettingsPanelShow;
            InAppSettings.SettingsPanelDismissed -= OnSettingsPanelDismissed;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
            ShowFailToAddModelToast -= ShowFailToAddModelToastHandler;
            ShowPickSourceDialog -= ShowPickSourceDialogHandler;
            StreamingModelController.LoadingGLBModel -= ShowLoading;
            if (m_BackButton != null)
            {
                m_BackButton.clicked -= OnBackButton;
            }

            if (m_NewVersionButton != null)
            {
                m_NewVersionButton.clicked -= OnNewVersionButtonPress;
            }

            if (m_StreamingRoot != null)
            {
                m_StreamingRoot.style.display = DisplayStyle.None;
            }
            
            m_DataStreamer.StageCreated.Unsubscribe(OnStageCreated);
            m_DataStreamer.StageDestroyed.Unsubscribe(OnStageDestroy);
            m_SaveLayoutModal?.Dismiss(DismissType.Cancel);
            if (m_Stage != null)
            {
                m_Stage.StreamingStateChanged.Unsubscribe(OnStreamingStateChanged);
            }
            m_Stage = null;

            if (m_SaveLayoutButton != null)
            {
                m_SaveLayoutButton.clicked -= SaveLayoutButtonOnClicked;
                m_SaveLayoutButton.RemoveFromHierarchy();
            }

            SharedUIManager.SelectedAsset = null;
            
            AssetsController.AssetSelected -= OnAssetSelected;
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            
            if (NetworkDetector.RequestedOfflineMode)
            {
                OfflineModeAssetsController.AssetDeselected.Invoke();
                return;
            }
            AssetsController.AssetDeselected.Invoke();
            StreamingModelController.StreamingAsset = null;
        }
        
        private void OnSettingsPanelShow(VisualElement vePanel, VisualTreeAsset titleTemplate)
        {
            var newTitle = titleTemplate.Instantiate().Children().First();
            var m_settings = m_PostProcessSettingsUI.Instantiate().Children().First();
            
            m_PostProcessingToggle = m_settings.Q<Toggle>();
            // Camera.main can be null (no active MainCamera-tagged camera) and may lack URP camera
            // data; guard both instead of NRE-ing when the settings panel opens.
            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
            {
                m_PostProcessingToggle.value = cameraData.renderPostProcessing;
            }
            m_PostProcessingToggle.RegisterValueChangedCallback(ChangePostProcessingValue);
            
            InAppSettings.InitializeSection(m_RenderingTitle, ref newTitle, m_settings);

            // Second row in the same Rendering section: the coincidence-bias (z-fighting) controls,
            // which drive the streamed-geometry shader's "_Bias" global. The toggle is the master
            // on/off; the strength slider (0-100%) tunes the separation amount.
            if (m_CoincidenceBiasSettingsUI != null)
            {
                var biasSettings = m_CoincidenceBiasSettingsUI.Instantiate().Children().First();
                bool biasEnabled = Shader.GetGlobalFloat(k_CoincidenceBiasId) > 0f;

                m_CoincidenceBiasToggle = biasSettings.Q<Toggle>(k_CoincidenceBiasToggleName);
                m_CoincidenceBiasToggle.value = biasEnabled;
                m_CoincidenceBiasToggle.RegisterValueChangedCallback(ChangeCoincidenceBiasEnabled);

                // Non-VR (desktop/mobile) uses the drag-based TouchSliderFloat; VR uses the regular
                // SliderFloat (controller-friendly). Both live in the UXML hidden; unhide the chosen one.
                m_CoincidenceBiasSlider = CoincidenceBiasUsesTouchSlider
                    ? (BaseSlider<float, float>)biasSettings.Q<TouchSliderFloat>(k_CoincidenceBiasSliderName)
                    : biasSettings.Q<SliderFloat>(k_CoincidenceBiasSliderName);
                if (m_CoincidenceBiasSlider != null)
                {
                    m_CoincidenceBiasSlider.style.display = DisplayStyle.Flex;
                    m_CoincidenceBiasSlider.SetValueWithoutNotify(BiasToStrength(m_CurrentBias));
                    m_CoincidenceBiasSlider.SetEnabled(biasEnabled);
                    m_CoincidenceBiasSlider.RegisterValueChangingCallback(ChangeCoincidenceBiasStrengthLive);
                    m_CoincidenceBiasSlider.RegisterValueChangedCallback(ChangeCoincidenceBiasStrength);
                }

                newTitle.Q<VisualElement>("Content").Add(biasSettings);
            }

            vePanel.Q<ScrollView>().Add(newTitle);
        }
        
        private void OnSettingsPanelDismissed()
        {
            m_PostProcessingToggle.UnregisterValueChangedCallback(ChangePostProcessingValue);
            m_PostProcessingToggle = null;

            if (m_CoincidenceBiasToggle != null)
            {
                m_CoincidenceBiasToggle.UnregisterValueChangedCallback(ChangeCoincidenceBiasEnabled);
                m_CoincidenceBiasToggle = null;
            }
            if (m_CoincidenceBiasSlider != null)
            {
                m_CoincidenceBiasSlider.UnregisterValueChangingCallback(ChangeCoincidenceBiasStrengthLive);
                m_CoincidenceBiasSlider.UnregisterValueChangedCallback(ChangeCoincidenceBiasStrength);
                m_CoincidenceBiasSlider = null;
            }
        }

        private void ChangePostProcessingValue(ChangeEvent<bool> evt)
        {
            var allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var camera in allCameras)
            {
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                {
                    cameraData.renderPostProcessing = evt.newValue;
                }
            }
        }

        // Master on/off for the coincidence bias: applies the remembered strength when on, 0 when off.
        private void ChangeCoincidenceBiasEnabled(ChangeEvent<bool> evt)
        {
            Shader.SetGlobalFloat(k_CoincidenceBiasId, evt.newValue ? m_CurrentBias : 0f);
            if (m_CoincidenceBiasSlider != null) m_CoincidenceBiasSlider.SetEnabled(evt.newValue);
        }

        // Strength slider (0-100%) -> separation in metres. Handle both the live drag (ValueChanging)
        // and the final release (ValueChanged) so the effect updates as the slider moves.
        private void ChangeCoincidenceBiasStrengthLive(ChangingEvent<float> evt) => ApplyCoincidenceBiasStrength(evt.newValue);

        private void ChangeCoincidenceBiasStrength(ChangeEvent<float> evt) => ApplyCoincidenceBiasStrength(evt.newValue);

        private void ApplyCoincidenceBiasStrength(float strengthPercent)
        {
            m_CurrentBias = StrengthToBias(strengthPercent);
            // Apply live only while the effect is enabled.
            if (m_CoincidenceBiasToggle != null && m_CoincidenceBiasToggle.value)
            {
                Shader.SetGlobalFloat(k_CoincidenceBiasId, m_CurrentBias);
            }
        }

        private float BiasToStrength(float biasMetres) =>
            m_MaxCoincidenceBias > 0f ? Mathf.Clamp01(biasMetres / m_MaxCoincidenceBias) * 100f : 0f;

        private float StrengthToBias(float strengthPercent) =>
            Mathf.Clamp01(strengthPercent / 100f) * m_MaxCoincidenceBias;

        protected virtual void OnNetworkStatusChanged(bool connected)
        {
            if (m_NewVersionButton != null && m_NewVersionButton.style.display == DisplayStyle.Flex)
            {
                m_NewVersionButton.SetEnabled(connected);
            }
            if (!connected || IdentityController.GuestMode || !PlatformServices.IsUserLoggedIn)
            {
                m_SaveLayoutButton?.SetEnabled(false);
                return;
            }
            var enableSaveButton = HasMultiStreamingModels();
            m_SaveLayoutButton?.SetEnabled(enableSaveButton);
        }
        
        private void OnActivityStateChanged(StreamingModel obj)
        {
            StartCoroutine(CheckTransformController());
        }

        private void OnModelRemoved(StreamingModel obj)
        {
            StartCoroutine(CheckTransformController());
        }

        private void OnModelAdded(GameObject arg1, ITransformValuesAccessor arg2)
        {
            StartCoroutine(CheckTransformController());
        }

        protected virtual IEnumerator CheckTransformController()
        {
#if UNITY_WEBGL
            yield return null;
#else
            m_WaitForEndOfFrame ??= new WaitForEndOfFrame();
            yield return m_WaitForEndOfFrame;
#endif
            var disableSaveButton =
                !HasMultiStreamingModels()
                || NetworkDetector.IsOffline
                || NetworkDetector.RequestedOfflineMode
                || IdentityController.GuestMode
                || !PlatformServices.IsUserLoggedIn;

            m_SaveLayoutButton?.SetEnabled(!disableSaveButton);

            CheckSaveButtonState();
        }

        protected virtual void SaveLayoutButtonOnClicked()
        {
            var savePanel = m_SavePanelTemplate.Instantiate().Children().First();
            m_SaveLayoutModal = Modal.Build(m_StreamingRoot, savePanel);
            m_SaveLayoutModal.shown += SaveModalOnShown;
            m_SaveLayoutModal.dismissed += OnSaveModalDismissed;
            m_SaveLayoutModal.Show();
        }

        private void SaveModalOnShown(Modal modal)
        {
            modal.shown -= SaveModalOnShown;
            NavigationController.PauseCameraControl.Invoke(true);

            InitializeSaveLayoutPanel(modal.contentView);
        }

        protected void InitializeSaveLayoutPanel(VisualElement contentView)
        {
            m_HasWritePermission = false;
            m_SaveLayoutNameTextField = contentView.Q<TextField>(k_LayoutAssetNameTextFieldName);
            m_OrganizationDropdown = contentView.Q<Dropdown>(k_OrganizationDropdownName);
            m_ProjectDropdown = contentView.Q<Dropdown>(k_ProjectDropdownName);
            m_CollectionDropDown = contentView.Q<Dropdown>(k_CollectionDropDownName);
            m_CancelButton = contentView.Q<ActionButton>(k_CancelButtonName);
            m_CancelButton.clicked += SaveLayoutPanelCancelButtonOnClicked;
            
            m_ConfirmSaveLayoutButton = contentView.Q<ActionButton>(k_SaveLayoutButtonName);
            m_ConfirmSaveLayoutButton.clicked += ConfirmSaveLayoutButtonOnClicked;

            bool isLayout = false;

            if (StreamingModelController.StreamingAsset.Value.Asset is OfflineAsset offlineAsset)
            {
                isLayout = offlineAsset.OfflineAssetInfo.layout;
            }
            else
            {
                isLayout =
                    StreamingModelController.StreamingAsset.Value.Properties.Value.Tags.Contains(StreamingUtils
                        .LayoutTag);
            }
            
            if (isLayout)
            {
                m_SaveLayoutNameTextField.SetValueWithoutNotify(StreamingModelController.StreamingAsset.Value.Properties.Value.Name);
                m_SaveLayoutNameTextField.SetEnabled(false);
                m_OrganizationDropdown.defaultMessage = AssetsController.SelectedOrganization.Name;
                m_OrganizationDropdown.SetEnabled(false);
                m_ProjectDropdown.defaultMessage = AssetsController.SelectedAssetProject.HasValue
                    ? AssetsController.SelectedAssetProject.Value.Properties.Value.Name
                    : null;
                m_ProjectDropdown.SetEnabled(false);
                m_CollectionDropDown.parent.style.display = DisplayStyle.None;
                AssetsController.CheckHaveWriteAccess?.Invoke(StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectDescriptor, hasWritePermission =>
                {
                    m_HasWritePermission = hasWritePermission;
                    if (!hasWritePermission)
                    {
                        ShowNoWritePermissionError();
                    }

                });

                CheckSaveButtonState();
            }
            else
            {
                m_ConfirmSaveLayoutButton.SetEnabled(false);
                m_OrganizationDropdown.SetEnabled(false);
                m_ProjectDropdown.SetEnabled(false);
                m_CollectionDropDown.SetEnabled(false);
                m_OrganizationDropdown.bindItem = OrganizationBinding;
                m_OrganizationDropdown.RegisterValueChangedCallback(OnOrganizationSelected);
                m_ProjectDropdown.bindItem = ProjectBinding;
                m_ProjectDropdown.RegisterValueChangedCallback(OnProjectSelected);
                m_CollectionDropDown.bindItem = CollectionBinding;
                m_SaveLayoutNameTextField.RegisterValueChangingCallback(OnSaveLayoutNameChanging);
                m_SaveLayoutNameTextField.RegisterValueChangedCallback(OnSaveLayoutNameChanged);
                AssetsController.RequestOrganizations.Invoke(results =>
                {
                    m_OrganizationDropdown.SetEnabled(true);
                    m_OrganizationDropdown.sourceItems = results;
                    m_OrganizationDropdown.SetValueWithoutNotify(null);
                    m_OrganizationDropdown.value = null;
                });
            }

            return;

            void OrganizationBinding(DropdownItem item, int index)
            {
                var org = m_OrganizationDropdown.sourceItems as List<IOrganization>;
                if(org == null) return;
                item.label = org[index].Name;
            }

            void ProjectBinding(DropdownItem item, int index)
            {
                var project = m_ProjectDropdown.sourceItems as List<AssetProjectInfo>;
                if(project == null) return;
                item.label = project[index].Properties.Value.Name;
            }
            
            void CollectionBinding(DropdownItem item, int index)
            {
                var collection = m_CollectionDropDown.sourceItems as List<IAssetCollection>;
                if(collection == null || collection.Count == 0) return;
                item.label = ReturnCollectionName(collection[index]);
            }
        }

        protected virtual void SaveLayoutPanelCancelButtonOnClicked()
        {
            m_SaveLayoutModal?.Dismiss(DismissType.Cancel);
        }

        private static string ReturnCollectionName(IAssetCollection collection)
        {
            var collectionName = collection.Descriptor.Path.GetPathComponents();
            var levels = collectionName.Length;
            return levels switch
            {
                1 => collectionName.Last(),
                2 => collectionName[0] + "/" + collectionName.Last(),
                _ => collectionName[0] + "/.../" + collectionName.Last()
            };
        }

        private void OnSaveLayoutNameChanged(ChangeEvent<string> evt)
        {
            CheckSaveButtonState();
        }

        private void OnSaveLayoutNameChanging(ChangingEvent<string> evt)
        {
            CheckSaveButtonState();
        }

        private void OnProjectSelected(ChangeEvent<IEnumerable<int>> evt)
        {
            var project = m_ProjectDropdown.sourceItems as List<AssetProjectInfo>;
            if(project == null) return;
            var selectedProject = project.ElementAt(evt.newValue.First());
            
            m_SaveErrorMessageToast?.Dismiss();
            m_CollectionDropDown.SetEnabled(false);
            m_CollectionDropDown.SetValueWithoutNotify(null);
            m_CollectionDropDown.sourceItems = null;
            
            AssetsController.CheckHaveWriteAccess?.Invoke(selectedProject.AssetProject.Descriptor, hasWritePermission =>
            {
                if (!hasWritePermission)
                {
                    ShowNoWritePermissionError();
                }

                m_HasWritePermission = hasWritePermission;
                CheckSaveButtonState();
            });

            AssetsController.GetAssetCollectionsForProject.Invoke(selectedProject, results =>
            {
                m_CollectionDropDown.SetEnabled(results != null && results.Count > 0);
                m_CollectionDropDown.sourceItems = results;
                m_CollectionDropDown.SetValueWithoutNotify(null);
            });
        }

        protected virtual async void ShowNoWritePermissionError()
        {
            m_SaveErrorMessageToast = Toast.Build(m_StreamingRoot, await m_NoWritePermissionLocalizedString.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Indefinite).SetStyle(NotificationStyle.Negative);
            m_SaveErrorMessageToast.Show();
        }

        private void OnOrganizationSelected(ChangeEvent<IEnumerable<int>> evt)
        {
            var org = m_OrganizationDropdown.sourceItems as List<IOrganization>;
            m_SaveErrorMessageToast?.Dismiss();
            if(org == null) return;
            var selectedOrg = org.ElementAt(evt.newValue.First());
            m_ProjectDropdown.SetEnabled(false);
            m_CollectionDropDown.SetEnabled(false);
            
            m_ProjectDropdown.SetValueWithoutNotify(null);
            m_CollectionDropDown.SetValueWithoutNotify(null);
            
            m_ProjectDropdown.sourceItems = null;
            m_CollectionDropDown.sourceItems = null;
            
            AssetsController.RequestAssetProjects.Invoke(selectedOrg, (org, results) =>
            {
                m_ProjectDropdown.SetEnabled(results != null && results.Count > 0);
                m_ProjectDropdown.sourceItems = results;
                m_ProjectDropdown.SetValueWithoutNotify(null);
                m_ProjectDropdown.value = null;
            });
        }

        protected void CheckSaveButtonState()
        {
            if (m_ConfirmSaveLayoutButton == null) return;

            var allowToSave =
                m_HasWritePermission
                && !string.IsNullOrWhiteSpace(m_SaveLayoutNameTextField.value)
                && HasMultiStreamingModels();

            m_ConfirmSaveLayoutButton.SetEnabled(allowToSave);
        }
        
        private void OnSaveModalDismissed(Modal arg1, DismissType arg2)
        {
            m_SaveLayoutModal.dismissed -= OnSaveModalDismissed;
            m_SaveErrorMessageToast?.Dismiss();
            if (arg2 != DismissType.Action)
            {
                NavigationController.PauseCameraControl.Invoke(false);
                return;
            }
            SaveLayoutAction();
        }

        protected void SaveLayoutAction()
        {
            IAssetProject project = null;
            IAssetCollection collection = null;

            if (!StreamingModelController.IsLayoutAsset)
            {
                project = ((List<AssetProjectInfo>)m_ProjectDropdown.sourceItems).ElementAt(m_ProjectDropdown.value.First()).AssetProject;
                collection = m_CollectionDropDown.value == null || !m_CollectionDropDown.value.Any() ? null :
                    ((List<IAssetCollection>)m_CollectionDropDown.sourceItems).ElementAt(m_CollectionDropDown.value.First());
            }

            StartCoroutine(CaptureScreenShot(OnCaptureFinished));
            
            return;
            
            void OnCaptureFinished(Texture2D screenshot)
            {
                SaveLayoutController.SaveLayout(m_SaveLayoutNameTextField.value?.Trim(), project, collection, screenshot, SaveCompleteCallback);
            }
        }
        
        protected virtual IEnumerator CaptureScreenShot(Action<Texture2D> callback)
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.style.display = DisplayStyle.None;
            yield return new WaitForEndOfFrame();
            var screenShot = ScreenCapture.CaptureScreenshotAsTexture();
            yield return new WaitForEndOfFrame();
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            ScaleTexture(screenShot, 320, 180, out var scaledTexture);
            yield return new WaitForEndOfFrame();
            callback?.Invoke(scaledTexture);
        }
        
        protected void ScaleTexture(Texture2D input, int targetWidth, int targetHeight, out Texture2D output)
        {
            output = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, true);
            var pixels = output.GetPixels(0);
            var incX = (float)1 / input.width * ((float)input.width / targetWidth);
            var incY = (float)1 / input.height * ((float)input.height / targetHeight);
                
            for (var px = 0; px < pixels.Length; px++)
                pixels[px] = input.GetPixelBilinear(incX * ((float)px % targetWidth), incY * Mathf.Floor((float)px / targetWidth));

            output.SetPixels(pixels, 0);
            output.Apply();
        }

        protected virtual void ConfirmSaveLayoutButtonOnClicked()
        {
            m_SaveLayoutModal?.Dismiss(DismissType.Action);
        }
        
        private void SaveCompleteCallback(AssetInfo? newAssetInfo, string message)
        {
            LoadingUIPanel.HideLoadingPanel?.Invoke(HideAfterAction);
            return;

            async void HideAfterAction()
            {
                NavigationController.PauseCameraControl?.Invoke(false);
                if (!newAssetInfo.HasValue)
                {
                    m_SaveErrorMessageToast?.Dismiss();
                    
                    var messageText = message.Contains("Forbidden") || message.Contains("Not Authorized")
                        ? await m_NoWritePermissionLocalizedString.GetTitleLocalizedStringForAppUIAsync()
                        : await m_SaveFailureLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                    ShowSaveCompletedToast(messageText);
                    return;
                }
                AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
                AssetsController.AssetSelected?.Invoke(newAssetInfo.Value);
            }
        }

        protected virtual void ShowSaveCompletedToast(string messageText)
        {
            m_SaveErrorMessageToast = Toast.Build(m_StreamingRoot, messageText, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
            m_SaveErrorMessageToast.Show();
        }

        protected static bool HasMultiStreamingModels()
        {
            var transformController = TransformController.Instance;
            if (transformController == null || transformController.transform == null)
            {
                return false;
            }

            var streamingModels = 0;
            for (var i = 0; i < transformController.transform.childCount; i++)
            {
                var child = transformController.transform.GetChild(i);
                // Only can save when there is more than one streaming model active
                if (child.gameObject.CompareTag(StreamingUtils.StreamModelTag) && child.gameObject.activeSelf)
                {
                    streamingModels++;
                    if (streamingModels > 1) return true;
                }
            }

            return false;
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            m_AuthenticationState = state;
            if (state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                StreamSceneController.ExitSceneConfirmed?.Invoke();
            }
        }

        protected void OnAssetSelected(AssetInfo assetInfo)
        {
            if (m_NewVersionButton != null)
            {
                m_NewVersionButton.style.display = DisplayStyle.None;
                m_NewVersionButton.SetEnabled(false);
            }

            AssignAssetNameTitle(assetInfo);
            
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            AssetsController.NewVersionAvailable += OnNewVersionAvailable;

            if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                LoadingUIPanel.ShowLoadingPanel?.Invoke(null);
            }
        }

        private static void OnLayoutLoaded()
        {
            LoadingUIPanel.HideLoadingPanel?.Invoke(null);
        }

        protected virtual async void OnNewVersionAvailable(AssetInfo newVersionAsset)
        {
            if(NetworkDetector.IsOffline) return;
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            var dialog = new AlertDialog()
            {
                title = await m_NewVersionTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_NewVersionDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Confirmation
            };
            dialog.SetPrimaryAction(99, await m_SwitchLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                AssetsController.AssetSelected?.Invoke(AssetsController.NewerVersionAsset.Value);
            });
            dialog.SetSecondaryAction(98,await m_DismissLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                //Enable new version available button
                m_NewVersionButton.style.display = DisplayStyle.Flex;
                m_NewVersionButton.SetEnabled(true);
            });
            var modal = Modal.Build(m_StreamingRoot, dialog);
            
            modal.Show();
        }
        
        protected virtual async void ShowPickSourceDialogHandler(AssetInfo onlineAssetInfo, AssetInfo offlineAssetInfo, string targetName, string anchorId)
        {
            //Ask user if he wants to add the asset from the local storage
            var whichDataSourceDialog = new AlertDialog
            {
                title = await m_AddTitle.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_AddDescription.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Default,
                primaryButton =
                {
                    leadingIcon = "broadcast"
                }
            };

            var titleLabel = whichDataSourceDialog.Q<LocalizedTextElement>("appui-dialog__header");
            titleLabel.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    { "asset", onlineAssetInfo.Properties.Value.Name }
                }
            };

            whichDataSourceDialog.SetPrimaryAction(97, await m_CloudOption.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                StreamingModelController.AddStreamModel?.Invoke(onlineAssetInfo, targetName, null, anchorId);
            });
            whichDataSourceDialog.SetSecondaryAction(96, await m_LocalOption.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                StreamingModelController.AddStreamModel?.Invoke(offlineAssetInfo, targetName, null, anchorId);
            });
            whichDataSourceDialog.SetCancelAction(0, await m_CancelOption.GetTitleLocalizedStringForAppUIAsync());
                    
            var dataSourceModal = Modal.Build(m_StreamingRoot, whichDataSourceDialog);
            
            dataSourceModal.dismissed += (modal, type) =>
            {
                StreamingModelController.PauseAddingModel = false;
            };
            
            dataSourceModal.Show();
        }

        protected virtual async void ShowFailToAddModelToastHandler()
        {
            //Give feedback to user that he is not part of the organization of the asset
            var toast = Toast.Build(m_StreamingRoot, await m_AssetLoadFailureToast.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Short).SetStyle(NotificationStyle.Negative);
            toast.Show();
        }

        private void OnStageDestroy()
        {
            if (m_Stage != null)
            {
                m_Stage.StreamingStateChanged.Unsubscribe(OnStreamingStateChanged);
            }
            m_Stage = null;
        }

        private void OnStageCreated(IStage stage)
        {
            m_Stage = stage;
            m_Stage.StreamingStateChanged.Subscribe(OnStreamingStateChanged);
        }

        private void OnStreamingStateChanged(StreamingState state)
        {
            if(m_AssetLoader == null) return;
            ShowLoading(state.IsStreamingInProgress);
        }
        
        private void ShowLoading(bool visible)
        {
            m_AssetLoader.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        protected virtual void DisablePreviousSceneUI()
        {
            var uiDocument = SharedUIManager.Instance.AssetsUIDocument;
            
            m_BackgroundContainer = uiDocument.rootVisualElement.Q<VisualElement>(k_BackgroundContainerName);
            
            SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
            SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.None;
            m_BackgroundContainer.style.display = DisplayStyle.None;
            
            m_OrganizationButton.style.display = DisplayStyle.None;
        }

        protected virtual void InitializeUI()
        {
            DisablePreviousSceneUI();
            
            var uiDocument = SharedUIManager.Instance.AssetsUIDocument;
            
            m_StreamingRoot = uiDocument.rootVisualElement.Q<VisualElement>(k_StreamingPanelName);
            m_StreamingRoot.style.display = DisplayStyle.Flex;

            var topLeftBar = uiDocument.rootVisualElement.Q<VisualElement>(k_TopLeftBarName);
            
            m_TopStreamingBar = m_streamingBarTemplate.Instantiate().Children().First();
            topLeftBar.Add(m_TopStreamingBar);
            
            m_TitleText = m_TopStreamingBar.Q<Text>(k_AssetTitle);

            AssignAssetNameTitle(StreamingModelController.StreamingAsset.Value);
            
            m_NewVersionButton = m_TopStreamingBar.Q<ActionButton>(k_NewVersionButtonName);
            m_NewVersionButton.style.display = DisplayStyle.None;
            m_NewVersionButton.clicked += OnNewVersionButtonPress;

            var topCenterBar =
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>("TopCenterBar");
            topCenterBar.Add(m_NewVersionButton);

            m_AssetLoader = m_TopStreamingBar.Q<VisualElement>(k_AssetLoaderName);
            m_AssetLoader.style.display = DisplayStyle.None;
            
            m_SaveLayoutButton = new IconButton()
            {
                icon = "upload",
                style =
                {
                    width = new Length(40f, LengthUnit.Pixel),
                    height = new Length(40f, LengthUnit.Pixel),
                    marginLeft = new Length(16f, LengthUnit.Pixel)
                }
            };
            var topRightBar =
                SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_TopRightBarName);
            
            m_BackButton = new IconButton
            {
                name = "SceneBackButton",
                icon = "exit-streaming"
            };
            m_BackButton.clicked += OnBackButton;
            topRightBar.Insert(0, m_BackButton);
            
            var settingsButton = topRightBar.Q<IconButton>("SettingsButton");
            topRightBar.Insert(topRightBar.IndexOf(settingsButton) + 1, m_SaveLayoutButton);
            m_SaveLayoutButton.clicked += SaveLayoutButtonOnClicked;
            m_SaveLayoutButton.SetEnabled(false);
            var avatar = topRightBar.Q<Avatar>();

            defaultAvatarPosition = topRightBar.IndexOf(avatar);
            topRightBar.Remove(avatar);
            topRightBar.Add(avatar);
        }

        protected virtual async void AssignAssetNameTitle(AssetInfo asset)
        {
            string assetName = string.Empty;
            var version = 0;
            if (asset.Asset is OfflineAsset offlineAsset)
            {
                assetName = offlineAsset.OfflineAssetInfo.assetName;
                version = offlineAsset.OfflineAssetInfo.assetVersion;
            }
            else
            {
                assetName = asset.Properties.Value.Name;
                version = asset.Properties.Value.FrozenSequenceNumber;
            }

            m_TitleText.text = await m_TitleVersionLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            
            m_TitleText.variables = new object[]
            {
                new Dictionary<string, object>()
                {
                    {"name", assetName},
                    {"num", version}
                }
            };
        }

        protected void OnNewVersionButtonPress()
        {
            m_NewVersionButton.style.display = DisplayStyle.None;
            if (AssetsController.NewerVersionAsset.HasValue)
            {
                AssetsController.AssetSelected?.Invoke(AssetsController.NewerVersionAsset.Value);
            }
            else
            {
                Debug.LogError("New version asset is null");
            }
        }

        protected virtual void ExitSceneUIHandler()
        {
            var topRightBar = SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.Q<VisualElement>(k_TopRightBarName);
            var avatar = topRightBar.Q<Avatar>();
            topRightBar.Remove(avatar);
            topRightBar.Insert(defaultAvatarPosition, avatar);
            
            m_TopStreamingBar.RemoveFromHierarchy();
            m_BackButton.RemoveFromHierarchy();
            m_NewVersionButton?.RemoveFromHierarchy();
            m_StreamingRoot.style.display = DisplayStyle.None;
            
            ReenablePreviousSceneUI();
        }

        protected virtual void ReenablePreviousSceneUI()
        {
            if (!PlatformServices.IsUserLoggedIn)
            {
                if(NetworkDetector.IsOffline && NetworkDetector.RequestedOfflineMode)
                {
                    ShowAssetsUI();
                }
                else
                {
                    ShowLoginUI();
                }
            }
            else
            {
                ShowAssetsUI();
                
                if (NetworkDetector.RequestedOfflineMode)
                {
                    SharedUIManager.Organization = null;
                }
            }
            
            m_BackgroundContainer.style.display = DisplayStyle.Flex;
            return;

            void ShowLoginUI()
            {
                SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.Flex;
                m_OrganizationButton.style.display = DisplayStyle.None;
                SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
            }

            void ShowAssetsUI()
            {
                if (NetworkDetector.IsOffline)
                {
                    m_OrganizationButton.style.display = NetworkDetector.RequestedOfflineMode? DisplayStyle.Flex: DisplayStyle.None;
                    SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.None;
                }
                else
                {
                    SharedUIManager.Instance.AssetsContainer.style.display = DisplayStyle.Flex;
                    m_OrganizationButton.style.display = DisplayStyle.Flex;
                }
                SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.None;
            }
        }

        protected virtual async void OnBackButton()
        {
            var dialog = new AlertDialog()
            {
                title = await m_ExitTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await m_ExitDescriptionLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Confirmation
            };
            dialog.SetPrimaryAction(95, await m_ExitLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                StreamSceneController.ExitSceneConfirmed?.Invoke();
            });
            dialog.SetCancelAction(0, await m_CancelOption.GetTitleLocalizedStringForAppUIAsync());
            var modal = Modal.Build(m_BackButton, dialog);

            modal.Show();
        }
    }
}
