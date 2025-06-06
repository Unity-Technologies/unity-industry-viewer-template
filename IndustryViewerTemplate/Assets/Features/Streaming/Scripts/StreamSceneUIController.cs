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
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Unity.Industry.Viewer.Identity;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cloud.Assets;
using TextField = Unity.AppUI.UI.TextField;

namespace Unity.Industry.Viewer.Streaming
{
    public class StreamSceneUIController : MonoBehaviour
    {
        private const string k_BackgroundContainerName = "BackgroundContainer";
        private const string k_OrganizationButton = "OrganizationButton";
        
        private const string k_StreamingPanelName = "StreamingContainer";

        private const string k_AssetLoaderName = "AssetLoader";
        private const string k_AssetTitle = "AssetTitle";
        private const string k_NewVersionButtonName = "VersionButton";
        private const string k_TopLeftBarName = "TopLeftBar";
        private const string k_TopRightBarName = "TopRightBar";
        private const string k_LayoutAssetNameTextFieldName = "LayoutAssetNameTextfield";
        private const string k_OrganizationDropdownName = "OrganizationDropdown";
        private const string k_ProjectDropdownName = "ProjectDropdown";
        private const string k_CollectionDropDownName = "CollectionDropdown";
        private const string k_CancelButtonName = "CancelButton";
        private const string k_SaveLayoutButtonName = "SaveLayoutButton";
        
        public static Action ShowFailToAddModelToast;
        public static Action<AssetInfo, AssetInfo> ShowPickSourceDialog;
        
        [SerializeField]
        private VisualTreeAsset m_streamingBarTemplate;
        
        [SerializeField]
        private StyleSheet m_StyleSheet;
        
        private VisualElement m_BackgroundContainer;
        private VisualElement m_TopStreamingBar;
        private ActionButton m_OrganizationButton => SharedUIManager.Instance.OrganizationButton;
        
        private VisualElement m_StreamingRoot;
        private IconButton m_BackButton;
        private Text m_TitleText;
        private ActionButton m_NewVersionButton;
        private VisualElement m_AssetLoader;
        private Toast m_SaveErrorMessageToast;
        
        IStage m_Stage;
        
        IDataStreamer m_DataStreamer => PlatformServices.DataStreamer;
        
        private IconButton m_SaveLayoutButton;

        [SerializeField] private VisualTreeAsset m_SavePanelTemplate;
        private TextField m_SaveLayoutNameTextField;
        private Dropdown m_OrganizationDropdown, m_ProjectDropdown, m_CollectionDropDown;
        private ActionButton m_CancelButton, m_ConfirmSaveLayoutButton;
        private Modal m_SaveLayoutModal;
        private bool m_HasWritePermission;
        private AuthenticationState m_AuthenticationState;
        private WaitForEndOfFrame m_WaitForEndOfFrame;
        
        #region Localisation
        [SerializeField] private LocalizedString m_AssetLoadFailureToast;
        [SerializeField] private LocalizedString m_AddTitle;
        [SerializeField] private LocalizedString m_AddDescription;
        [SerializeField] private LocalizedString m_CloudOption;
        [SerializeField] private LocalizedString m_LocalOption;
        [SerializeField] private LocalizedString m_CancelOption;
        
        [SerializeField]
        private LocalizedString m_TitleVersionLocalizedString;

        [SerializeField]
        private LocalizedString m_NewVersionTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_NewVersionDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_SwitchLocalizedString;

        [SerializeField]
        private LocalizedString m_DismissLocalizedString;

        [SerializeField]
        private LocalizedString m_ExitTitleLocalizedString;

        [SerializeField]
        private LocalizedString m_ExitLocalizedString;

        [SerializeField]
        private LocalizedString m_ExitDescriptionLocalizedString;

        [SerializeField]
        private LocalizedString m_CancelLocalizedString;
        
        [SerializeField]
        private LocalizedString m_NoWritePermissionLocalizedString;

        [SerializeField]
        private LocalizedString m_SaveFailureLocalizedString;
        
        #endregion

        private void Awake()
        {
            m_DataStreamer.StageCreated.Subscribe(OnStageCreated);
            m_DataStreamer.StageDestroyed.Subscribe(OnStageDestroy);
        }

        // Start is called before the first frame update
        void Start()
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
        }

        private void OnDestroy()
        {
            ExitSceneUIHandler();
            StreamingModel.OnActivityStateChanged -= OnActivityStateChanged;
            StreamingModelController.FinishedAddingModel -= OnLayoutLoaded;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            TransformController.ModelAdded -= OnModelAdded;
            TransformController.ModelRemoved -= OnModelRemoved;
            IdentityController.AuthenticationStateChangedEvent -= OnAuthenticationStateChanged;
            ShowFailToAddModelToast -= ShowFailToAddModelToastHandler;
            ShowPickSourceDialog -= ShowPickSourceDialogHandler;
            StreamingModelController.LoadingGLBModel -= ShowLoading;
            m_BackButton.clicked -= OnBackButton;
            m_NewVersionButton.clicked -= OnNewVersionButtonPress;
            m_StreamingRoot.style.display = DisplayStyle.None;
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

        private void OnNetworkStatusChanged(bool connected)
        {
            if (m_NewVersionButton != null && m_NewVersionButton.style.display == DisplayStyle.Flex)
            {
                m_NewVersionButton.SetEnabled(connected);
            }
            if (!connected || IdentityController.GuestMode)
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

        IEnumerator CheckTransformController()
        {
#if UNITY_WEBGL
            yield return null;
#else
            m_WaitForEndOfFrame ??= new WaitForEndOfFrame();
            yield return m_WaitForEndOfFrame;
#endif
            var enableSaveButton = HasMultiStreamingModels();
            if (!enableSaveButton || NetworkDetector.IsOffline || NetworkDetector.RequestedOfflineMode || IdentityController.GuestMode)
            {
                m_SaveLayoutButton?.SetEnabled(false);
                yield break;
            }
            m_SaveLayoutButton?.SetEnabled(true);
        }

        private void SaveLayoutButtonOnClicked()
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
            m_HasWritePermission = false;
            m_SaveLayoutNameTextField = modal.contentView.Q<TextField>(k_LayoutAssetNameTextFieldName);
            m_OrganizationDropdown = modal.contentView.Q<Dropdown>(k_OrganizationDropdownName);
            m_ProjectDropdown = modal.contentView.Q<Dropdown>(k_ProjectDropdownName);
            m_CollectionDropDown = modal.contentView.Q<Dropdown>(k_CollectionDropDownName);
            m_CancelButton = modal.contentView.Q<ActionButton>(k_CancelButtonName);
            m_CancelButton.clicked += () => m_SaveLayoutModal?.Dismiss(DismissType.Cancel);
            
            m_ConfirmSaveLayoutButton = modal.contentView.Q<ActionButton>(k_SaveLayoutButtonName);
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
                m_ProjectDropdown.defaultMessage = AssetsController.SelectedAssetProject.Value.Properties.Value.Name;
                m_ProjectDropdown.SetEnabled(false);
                m_CollectionDropDown.parent.style.display = DisplayStyle.None;
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
                    m_SaveErrorMessageToast = Toast.Build(m_StreamingRoot, string.Empty, NotificationDuration.Indefinite).SetStyle(NotificationStyle.Negative);
                    m_SaveErrorMessageToast.shown += OnSaveErrorMessageToastShown;
                    m_SaveErrorMessageToast.Show();
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
            
            void OnSaveErrorMessageToastShown(Toast toast)
            {
                toast.shown -= OnSaveErrorMessageToastShown;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_NoWritePermissionLocalizedString);
            }
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
            
            AssetsController.RequestAssetProjects.Invoke(selectedOrg, results =>
            {
                m_ProjectDropdown.SetEnabled(results != null && results.Count > 0);
                m_ProjectDropdown.sourceItems = results;
                m_ProjectDropdown.SetValueWithoutNotify(null);
                m_ProjectDropdown.value = null;
            });
        }

        private void CheckSaveButtonState()
        {
            var allowToSave = m_HasWritePermission && m_SaveLayoutNameTextField.value.Length > 0;
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

            IAssetProject project = null;
            IAssetCollection collection = null;
            if (!StreamingModelController.StreamingAsset.Value.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                project = ((List<AssetProjectInfo>)m_ProjectDropdown.sourceItems).ElementAt(m_ProjectDropdown.value.First()).AssetProject;
                collection = m_CollectionDropDown.value == null || !m_CollectionDropDown.value.Any() ? null :
                    ((List<IAssetCollection>)m_CollectionDropDown.sourceItems).ElementAt(m_CollectionDropDown.value.First());
            }

            StartCoroutine(CaptureScreenShot(OnCaptureFinished));
            
            return;
            
            void OnCaptureFinished(Texture2D screenshot)
            {
                SaveLayoutController.SaveLayout(m_SaveLayoutNameTextField.value, project, collection, screenshot, SaveCompleteCallback);
            }
        }

        private IEnumerator CaptureScreenShot(Action<Texture2D> callback)
        {
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.style.display = DisplayStyle.None;
            
            yield return new WaitForEndOfFrame();
            var screenShot = ScreenCapture.CaptureScreenshotAsTexture();
            yield return new WaitForEndOfFrame();
            SharedUIManager.Instance.AssetsUIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            ScaleTexture(screenShot, 320, 180, out var scaledTexture);
            yield return new WaitForEndOfFrame();
            callback?.Invoke(scaledTexture);
            
            yield break;
            void ScaleTexture(Texture2D input, int targetWidth, int targetHeight, out Texture2D output)
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
        }

        private void ConfirmSaveLayoutButtonOnClicked()
        {
            m_SaveLayoutModal?.Dismiss(DismissType.Action);
        }
        
        private void SaveCompleteCallback(AssetInfo? newAssetInfo, string message)
        {
            SharedUIManager.HideLoadingModal(() =>
            {
                NavigationController.PauseCameraControl.Invoke(false);
                if (!newAssetInfo.HasValue)
                {
                    m_SaveErrorMessageToast?.Dismiss();
                    m_SaveErrorMessageToast = Toast.Build(m_StreamingRoot, string.Empty, NotificationDuration.Long).SetStyle(NotificationStyle.Negative);
                    m_SaveErrorMessageToast.shown += OnSaveErrorMessageToastShown;
                    m_SaveErrorMessageToast.Show();
                    return;
                }
                AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
                AssetsController.AssetSelected?.Invoke(newAssetInfo.Value);
            });
            return;
            
            void OnSaveErrorMessageToastShown(Toast toast)
            {
                toast.shown -= OnSaveErrorMessageToastShown;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                
                if (message.Contains("Forbidden") || message.Contains("Not Authorized"))
                {
                    text.SetBinding("text", m_NoWritePermissionLocalizedString);
                }
                else
                {
                    text.SetBinding("text", m_SaveFailureLocalizedString);
                }
            }
        }

        private static bool HasMultiStreamingModels()
        {
            var streamingModels = 0;
            if (TransformController.Instance == null || TransformController.Instance.transform == null)
            {
                return false;
            }
            for(var i = 0; i < TransformController.Instance.transform.childCount; i++)
            {
                if(!TransformController.Instance.transform.GetChild(i).gameObject.CompareTag(StreamingUtils.StreamModelTag)) continue;
                //Only can save when there is more than one streaming model active
                if(!TransformController.Instance.transform.GetChild(i).gameObject.activeSelf) continue;
                streamingModels++;
            }
            return streamingModels > 1;
        }

        private void OnAuthenticationStateChanged(AuthenticationState state)
        {
            m_AuthenticationState = state;
            if (state is AuthenticationState.AwaitingLogout or AuthenticationState.LoggedOut)
            {
                StreamSceneController.ExitSceneConfirmed?.Invoke();
            }
        }

        private void OnAssetSelected(AssetInfo assetInfo)
        {
            if (m_NewVersionButton != null)
            {
                m_NewVersionButton.style.display = DisplayStyle.None;
                m_NewVersionButton.SetEnabled(false);
            }
            
            if (m_TitleVersionLocalizedString.TryGetValue("name", out var nameValue))
            {
                ((StringVariable)nameValue).Value = assetInfo.Properties.Value.Name;
            }
            else
            {
                m_TitleVersionLocalizedString.Add("name", new StringVariable { Value = assetInfo.Properties.Value.Name});
            }
            
            
            if (m_TitleVersionLocalizedString.TryGetValue("num", out var verValue))
            {
                ((IntVariable)verValue).Value = assetInfo.Properties.Value.FrozenSequenceNumber;
            }
            else
            {
                m_TitleVersionLocalizedString.Add("num", new IntVariable { Value = assetInfo.Properties.Value.FrozenSequenceNumber});
            }
            
            m_TitleText.SetBinding("text", m_TitleVersionLocalizedString);
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            AssetsController.NewVersionAvailable += OnNewVersionAvailable;

            if (assetInfo.Properties.Value.Tags.Contains(StreamingUtils.LayoutTag))
            {
                SharedUIManager.ShowLoadingModal();
            }
        }

        private static void OnLayoutLoaded()
        {
            SharedUIManager.HideLoadingModal();
        }

        private void OnNewVersionAvailable(AssetInfo newVersionAsset)
        {
            if(NetworkDetector.IsOffline) return;
            AssetsController.NewVersionAvailable -= OnNewVersionAvailable;
            var dialog = new CustomAlertDialog(m_NewVersionTitleLocalizedString, m_NewVersionDescriptionLocalizedString)
            {
                variant = AlertSemantic.Confirmation
            };
            dialog.SetPrimaryAction(m_SwitchLocalizedString, true, () =>
            {
                AssetsController.AssetSelected?.Invoke(AssetsController.NewerVersionAsset.Value);
            });
            dialog.SetSecondaryAction(m_DismissLocalizedString, false, () =>
            {
                //Enable new version available button
                m_NewVersionButton.style.display = DisplayStyle.Flex;
                m_NewVersionButton.SetEnabled(true);
            });
            var modal = Modal.Build(m_StreamingRoot, dialog);
            
            modal.Show();
        }
        
        private void ShowPickSourceDialogHandler(AssetInfo onlineAssetInfo, AssetInfo offlineAssetInfo)
        {
            if (m_AddTitle.TryGetValue("asset", out var assetNameValue))
            {
                ((StringVariable)assetNameValue).Value = onlineAssetInfo.Properties.Value.Name;
            }
            else
            {
                m_AddTitle.Add("asset", new StringVariable() {Value = onlineAssetInfo.Properties.Value.Name});
            }
            
            //Ask user if he wants to add the asset from the local storage
            var whichDataSourceDialog = new CustomAlertDialog(m_AddTitle, m_AddDescription)
            {
                title = " ",
                description = string.Empty,
                variant = AlertSemantic.Default
            };
                    
            whichDataSourceDialog.SetPrimaryAction(m_CloudOption, true, () =>
            {
                StreamingModelController.AddStreamModel?.Invoke(onlineAssetInfo);
            }, "broadcast");
            whichDataSourceDialog.SetSecondaryAction(m_LocalOption, false, () =>
            {
                StreamingModelController.AddStreamModel?.Invoke(offlineAssetInfo);
            });
            whichDataSourceDialog.SetCancelAction(m_CancelOption);
                    
            var dataSourceModal = Modal.Build(m_StreamingRoot, whichDataSourceDialog);
            
            dataSourceModal.dismissed += (modal, type) =>
            {
                StreamingModelController.PauseAddingModel = false;
            };
            
            dataSourceModal.Show();
        }

        private void ShowFailToAddModelToastHandler()
        {
            //Give feedback to user that he is not part of the organization of the asset
            
            var toast = Toast.Build(m_StreamingRoot, string.Empty, NotificationDuration.Short).SetStyle(NotificationStyle.Negative);
            toast.shown += OnFailureToastShown;
            toast.Show();
            
            void OnFailureToastShown(Toast toast)
            {
                toast.shown -= OnFailureToastShown;
                var text = toast.view.Q<LocalizedTextElement>("appui-toast__message");
                text.SetBinding("text", m_AssetLoadFailureToast);
            }
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

        private void InitializeUI()
        {
            var uiDocument = SharedUIManager.Instance.AssetsUIDocument;
            
            m_BackgroundContainer = uiDocument.rootVisualElement.Q<VisualElement>(k_BackgroundContainerName);
            
            SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
            SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.None;
            m_BackgroundContainer.style.display = DisplayStyle.None;
            
            m_OrganizationButton.style.display = DisplayStyle.None;
            
            m_StreamingRoot = uiDocument.rootVisualElement.Q<VisualElement>(k_StreamingPanelName);
            m_StreamingRoot.style.display = DisplayStyle.Flex;

            var topLeftBar = uiDocument.rootVisualElement.Q<VisualElement>(k_TopLeftBarName);
            
            m_TopStreamingBar = m_streamingBarTemplate.Instantiate().Children().First();
            topLeftBar.Add(m_TopStreamingBar);
            
            m_TitleText = m_TopStreamingBar.Q<Text>(k_AssetTitle);

            string assetName = string.Empty;
            var version = 0;
            if (StreamingModelController.StreamingAsset.Value.Asset is OfflineAsset offlineAsset)
            {
                assetName = offlineAsset.OfflineAssetInfo.assetName;
                version = offlineAsset.OfflineAssetInfo.assetVersion;
            }
            else
            {
                assetName = StreamingModelController.StreamingAsset.Value.Properties.Value.Name;
                version = StreamingModelController.StreamingAsset.Value.Properties.Value.FrozenSequenceNumber;
            }

            if (m_TitleVersionLocalizedString.TryGetValue("name", out var nameValue))
            {
                ((StringVariable)nameValue).Value = assetName;
            }
            else
            {
                m_TitleVersionLocalizedString.Add("name", new StringVariable { Value = assetName});
            }
            
            if (m_TitleVersionLocalizedString.TryGetValue("num", out var verValue))
            {
                ((IntVariable)verValue).Value = version;
            }
            else
            {
                m_TitleVersionLocalizedString.Add("num", new IntVariable { Value = version});
            }
            
            m_TitleText.SetBinding("text", m_TitleVersionLocalizedString);
            
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
            };
            m_BackButton.AddToClassList("scene-back-button");
            m_BackButton.clicked += OnBackButton;
            topRightBar.Insert(0, m_BackButton);
            
            var settingsButton = topRightBar.Q<IconButton>("SettingsButton");
            topRightBar.Insert(topRightBar.IndexOf(settingsButton) + 1, m_SaveLayoutButton);
            m_SaveLayoutButton.clicked += SaveLayoutButtonOnClicked;
            m_SaveLayoutButton.SetEnabled(false);
        }

        private void OnNewVersionButtonPress()
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

        private void ExitSceneUIHandler()
        {
            m_TopStreamingBar.RemoveFromHierarchy();
            m_BackButton.RemoveFromHierarchy();
            m_NewVersionButton?.RemoveFromHierarchy();
            m_StreamingRoot.style.display = DisplayStyle.None;

            if (m_AuthenticationState == AuthenticationState.AwaitingLogout ||
                m_AuthenticationState == AuthenticationState.LoggedOut)
            {
                SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.Flex;
                m_OrganizationButton.style.display = DisplayStyle.None;
                SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.None;
            }
            else
            {
                SharedUIManager.Instance.AssetsRoot.style.display = DisplayStyle.Flex;
                SharedUIManager.Instance.IdentityContainer.style.display = DisplayStyle.None;
                m_OrganizationButton.style.display = DisplayStyle.Flex;
            }
            
            m_BackgroundContainer.style.display = DisplayStyle.Flex;
        }

        private void OnBackButton()
        {
            var dialog = new CustomAlertDialog(m_ExitTitleLocalizedString, m_ExitDescriptionLocalizedString)
            {
                title = " ",
                description = string.Empty,
                variant = AlertSemantic.Confirmation
            };
            dialog.SetPrimaryAction(m_ExitLocalizedString, true, () =>
            {
                StreamSceneController.ExitSceneConfirmed?.Invoke();
            });
            dialog.SetCancelAction(m_CancelLocalizedString);
            var modal = Modal.Build(m_BackButton, dialog);

            modal.Show();
        }
    }
}
