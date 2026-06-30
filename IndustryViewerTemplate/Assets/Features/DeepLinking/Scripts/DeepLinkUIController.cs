using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AppUI.Core;
using Unity.AppUI.UI;
using Unity.Cloud.Assets;
using Unity.Cloud.Identity;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Streaming;
using Unity.Industry.Viewer.Streaming.AddModel;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using AssetInfo = Unity.Industry.Viewer.Assets.AssetInfo;
#region VR
using Unity.Industry.Viewer.VR;
#endregion


namespace Unity.Industry.Viewer.DeepLinking
{
    /// <summary>
    /// This controller handles deep linking UI logic.
    /// </summary>
    public class DeepLinkUIController : MonoBehaviour
    {
        private const string k_TopRightBarName = "TopRightBar";

        private IconButton m_StreamingDeepLinkButton;
        private Modal m_Modal;

        #region Localization

        [SerializeField]
        public LocalizedString m_StreamingDeepLinkButtonTooltip;

        [SerializeField]
        public LocalizedString m_CopyDeepLinkToClipboardMessage;

        [SerializeField]
        public LocalizedString m_CopyDeepLinkToClipboardErrorMessage;

        [SerializeField]
        public LocalizedString m_OfflineModeTitle;

        [SerializeField]
        public LocalizedString m_OfflineModeMessage;

        [SerializeField]
        public LocalizedString m_OfflineModeOkButton;

        [SerializeField]
        public LocalizedString m_AccessErrorMessage;

        #endregion

        [SerializeField]
        public StyleSheet m_StyleSheet;

        #region VR

        [Header("VR")]
        [SerializeField]
        private XRControllerMenu m_XRControllerMenu;
        [SerializeField]
        private Texture2D m_DeepLinkingIcon;
        private XRRoundButton m_XRDeepLinkingButton;
        private XRPanel.AlertXRPanel m_XRAlertPanel;
        #endregion

        private void Start()
        {
            DeepLinkController.AccessErrorAction += ShowAccessErrorMessage;
            DeepLinkController.CreationErrorAction += CreationErrorMessage;
            DeepLinkController.CreatedLinkAction += ShowCreatedLinkMessage;
            DeepLinkController.NotSupportedAction += ShowNotSupportedDialog;
            DeepLinkController.ShowSelectionUIAction += ShowAssetSelectionUI;
            DeepLinkController.ShowOrganizationUIAction += SelectOrganizationInUI;
            NetworkDetector.OnNetworkStatusChanged += OnNetworkStatusChanged;
            StreamSceneController.ExitSceneConfirmed += OnExitSceneConfirmed;
            MainSceneController.StartStreaming += InitializeUI;
            StreamAssetUIController.DeepLinkCreationHandler += OnAssetSelectionDeepLinkButtonClicked;
            SharedUIManager.AssetSelected += OnAssetSelected;
            AssetsUIBaseController.OnAssetProjectsLoadedEvent += SelectProjectInUI;
        }

        private void OnDestroy()
        {
            DeepLinkController.CreationErrorAction -= CreationErrorMessage;
            DeepLinkController.AccessErrorAction -= ShowAccessErrorMessage;
            DeepLinkController.CreatedLinkAction -= ShowCreatedLinkMessage;
            DeepLinkController.ShowSelectionUIAction -= ShowAssetSelectionUI;
            NetworkDetector.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            DeepLinkController.NotSupportedAction -= ShowNotSupportedDialog;
            DeepLinkController.ShowOrganizationUIAction -= SelectOrganizationInUI;
            StopAllCoroutines();
            StreamSceneController.ExitSceneConfirmed -= OnExitSceneConfirmed;
            MainSceneController.StartStreaming -= InitializeUI;
            StreamAssetUIController.DeepLinkCreationHandler -= OnAssetSelectionDeepLinkButtonClicked;
            SharedUIManager.AssetSelected -= OnAssetSelected;
            AssetsUIBaseController.OnAssetProjectsLoadedEvent -= SelectProjectInUI;
            AssetsController.AllAssetsLoaded -= SelectAssetInUI;

            m_StreamingDeepLinkButton = null;
        }

        private void ShowCreatedLinkMessage()
        {
            ShowInfoMessage(m_CopyDeepLinkToClipboardMessage);
        }

        private void CreationErrorMessage()
        {
            ShowErrorMessage(m_CopyDeepLinkToClipboardErrorMessage);
        }

        private void ShowAccessErrorMessage()
        {
            ShowErrorMessage(m_AccessErrorMessage);
        }

        private async void InitializeUI()
        {
            #if VR_MODE
            SceneManager.activeSceneChanged += OnStreamingLoaded;
            #else
            if (m_StreamingDeepLinkButton != null)
            {
                m_StreamingDeepLinkButton.style.display = DisplayStyle.Flex;
                SetStreamingDeepLinkButtonState();
                return;
            }

            UIDocument m_UIDocument = SharedUIManager.Instance.AssetsUIDocument;

            m_UIDocument.rootVisualElement.AddStyleSheetIfMissing(m_StyleSheet);

            var topRightBar = m_UIDocument.rootVisualElement.Q<VisualElement>(k_TopRightBarName);

            m_StreamingDeepLinkButton = new IconButton()
            {
                name = "DeepLinkButton",
                icon = "share-network",
                tooltip = await m_StreamingDeepLinkButtonTooltip.GetTitleLocalizedStringForAppUIAsync()
            };

            m_StreamingDeepLinkButton.clicked += OnStreamingDeepLinkButtonClicked;
            topRightBar.Add(m_StreamingDeepLinkButton);
            SetStreamingDeepLinkButtonState();
            #endif
        }
        
        private void OnNetworkStatusChanged(bool connected)
        {
            SetStreamingDeepLinkButtonState();
        }

#if VR_MODE
        private void OnStreamingLoaded(Scene arg0, Scene arg1)
        {
            SceneManager.activeSceneChanged -= OnStreamingLoaded;
            m_XRDeepLinkingButton = new XRRoundButton()
            {
                IconTexture = m_DeepLinkingIcon,
            };
            m_XRDeepLinkingButton.clicked += OnStreamingDeepLinkButtonClicked;
            m_XRControllerMenu?.Initialize();
            m_XRControllerMenu?.Add(m_XRDeepLinkingButton);
            m_XRDeepLinkingButton.SetEnabled(DeepLinkController.IsDeepLinkCreationEnabled);
        }
#endif

        public void SetStreamingDeepLinkButtonState()
        {
            m_XRDeepLinkingButton?.SetEnabled(DeepLinkController.IsDeepLinkCreationEnabled);
            m_StreamingDeepLinkButton?.SetEnabled(DeepLinkController.IsDeepLinkCreationEnabled);
        }

        private void OnExitSceneConfirmed()
        {
            if (m_XRDeepLinkingButton != null)
            {
                m_XRDeepLinkingButton.clicked -= OnStreamingDeepLinkButtonClicked;
                m_XRDeepLinkingButton.RemoveFromHierarchy();
                m_XRDeepLinkingButton = null;
            }
            if (m_StreamingDeepLinkButton == null) return;
            m_StreamingDeepLinkButton.DisplayOff();
        }

        private void OnStreamingDeepLinkButtonClicked()
        {
            if (!StreamingModelController.StreamingAsset.HasValue)
            {
                Debug.LogError("Deep Linking: streaming deep link button clicked but no asset in StreamingModelController.StreamingAsset.");
                return;
            }
            _ = DeepLinkController.Instance.CreateDeepLinkAndCopyToClipboardAsync(StreamingModelController.StreamingAsset.Value);
        }

        private void OnAssetSelectionDeepLinkButtonClicked(VisualElement messageReferenceView)
        {
            if (!SharedUIManager.SelectedAsset.HasValue)
            {
                Debug.LogError("Deep Linking: asset selection deep link button clicked but no asset in SharedUIManager.SelectedAsset.");
                return;
            }

            _ = DeepLinkController.Instance.CreateDeepLinkAndCopyToClipboardAsync(SharedUIManager.SelectedAsset.Value);
        }

        public async void ShowErrorMessage(LocalizedString message)
        {
            if (m_XRDeepLinkingButton == null)
            {
                var messageReferenceView = SharedUIManager.Instance.AssetsContainer;

                var errorMessageToast = Toast
                    .Build(
                        messageReferenceView,
                         await message.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Negative);

                errorMessageToast.Show();
            }
            else
            {
                var xrErrorMessageToast = XRToastPanel.Build(await message.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Negative);
                xrErrorMessageToast.Show();
            }
        }

        public async void ShowInfoMessage(LocalizedString message)
        {
            if (m_XRDeepLinkingButton == null)
            {
                var messageReferenceView = SharedUIManager.Instance.AssetsContainer;

                var infoMessageToast = Toast
                    .Build(
                        messageReferenceView,
                        await message.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Default);

                infoMessageToast.Show();
            }
            else
            {
                var xrInfoMessageToast = XRToastPanel.Build(await message.GetTitleLocalizedStringForAppUIAsync(), NotificationDuration.Long)
                    .SetStyle(NotificationStyle.Default);
                xrInfoMessageToast.Show();
            }
        }

        public async void ShowNotSupportedDialog()
        {
            if (m_XRDeepLinkingButton == null)
            {
                if (m_Modal != null)
                {
                    Debug.LogWarning("Deep linking: modal is already shown.");
                    return;
                }

                var messageDialog = new AlertDialog()
                {
                    title = await m_OfflineModeTitle.GetTitleLocalizedStringForAppUIAsync(),
                    description = await m_OfflineModeMessage.GetTitleLocalizedStringForAppUIAsync(),
                    variant = AlertSemantic.Default
                };

                messageDialog.SetCancelAction(0, await m_OfflineModeOkButton.GetTitleLocalizedStringForAppUIAsync());

                m_Modal = Modal.Build(SharedUIManager.Instance.AssetsUIDocument.rootVisualElement, messageDialog);
                m_Modal.Show();
                m_Modal.dismissed += (modal, dismissType) =>
                {
                    m_Modal = null;
                };
            }
            else
            {
                if (m_XRAlertPanel != null)
                {
                    Debug.LogWarning("Deep linking: modal is already shown.");
                    return;
                }
                m_XRAlertPanel = new XRPanel.AlertXRPanel(await 
                    m_OfflineModeTitle.GetTitleLocalizedStringForAppUIAsync(),
                    await m_OfflineModeMessage.GetTitleLocalizedStringForAppUIAsync());
                
                m_XRAlertPanel.SetCancelButton(await m_OfflineModeOkButton.GetTitleLocalizedStringForAppUIAsync());
                
                m_XRAlertPanel.Dismissed += Dismissed;

                m_XRAlertPanel.Show();
                
                void Dismissed(XRPanel.CustomXRPanel obj)
                {
                    m_XRAlertPanel = null;
                }
            }
        }

        private async void OnAssetSelected(AssetInfo info)
        {
            var assetInfoPanelRoot = SharedUIManager.Instance.AssetsContainer?.Q<VisualElement>(StreamAssetUIController.k_AssetInfoPanelRootName);
            var copyDeepLinkButton = assetInfoPanelRoot?.Q<ActionButton>(StreamAssetUIController.k_CopyDeepLinkButtonName);
            if (SceneManager.GetActiveScene() != gameObject.scene)
            {
                //Hide this when the add model tool is active
                copyDeepLinkButton?.DisplayOff();
                return;
            }
            bool enableDeepLinkButton;

            if (info.Asset is OfflineAsset)
            {
                copyDeepLinkButton?.DisplayOff();
                return;
            }
            
            if (!info.Properties.HasValue)
            {
                // No properties to classify the asset; can't build a deep link, so hide the button.
                copyDeepLinkButton?.DisplayOff();
                return;
            }

            if (!info.Properties.Value.IsLayout())
            {
                enableDeepLinkButton = await StreamAssetUIController.HasStreamableDataset(info);
                
            }
            else
            {
                enableDeepLinkButton = true;
            }

            await Task.Yield();
            
            if (copyDeepLinkButton == null)
            {
                copyDeepLinkButton = assetInfoPanelRoot?.Q<ActionButton>(StreamAssetUIController.k_CopyDeepLinkButtonName);
                if (copyDeepLinkButton == null)
                {
                    return;
                }
            }

            copyDeepLinkButton.DisplayOff();
            if (!DeepLinkController.IsDeepLinkCreationEnabled)
            {
                return;
            }

            copyDeepLinkButton.SetDisplay(enableDeepLinkButton);
        }

        public void ShowAssetSelectionUI()
        {
            if (SharedUIManager.Instance.AssetsContainer.IsDisplayOn())
            {
                Debug.Log("Deep linking: Asset selection UI is already open.");
                return;
            }

            // Open the Streaming Asset selection UI
            var addModelController = FindFirstObjectByType<AddModelToolUIController>();
            if (addModelController == null)
            {
                Debug.LogError("Deep linking: AddModelToolUIController not found in the scene.");
                return;
            }

            addModelController.Show();
        }

        public void SelectOrganizationInUI()
        {
            var orgs = SharedUIManager.Instance.OrganizationButton.userData as List<IOrganization>;
            if (orgs == null)
            {
                Debug.Log("Deep linking: no organizations loaded.");
                DeepLinkController.ClearCurrentLink();
                return;
            }

            var organization = orgs.FirstOrDefault(org => org.Id == DeepLinkController.AssetProject.Descriptor.OrganizationId);
            if (organization == null)
            {
                Debug.Log($"Deep linking: organization '{DeepLinkController.AssetProject.Descriptor.OrganizationId}' not found across loaded ones.");
                DeepLinkController.ClearCurrentLink();
                return;
            }

            var currentOrganization = SharedUIManager.Organization;
            if (currentOrganization?.Id == organization.Id)
            {
                Debug.Log($"Deep linking: organization '{DeepLinkController.AssetProject.Descriptor.OrganizationId}' is already active.");
                SelectProjectInUI(currentOrganization);
                return;
            }

            Debug.Log($"Deep linking: link organization is '{DeepLinkController.AssetProject.Descriptor.OrganizationId}', current is '{currentOrganization?.Id}'.");

            var assetsUIToolkitController = FindFirstObjectByType<AssetsUIToolkitController>();
            if (assetsUIToolkitController == null)
            {
                Debug.LogError($"Deep linking: AssetsUIToolkitController not found. Can't select organization '{DeepLinkController.AssetProject.Descriptor.OrganizationId}'.");
                return;
            }

            Debug.Log($"Deep linking: set current organization to '{DeepLinkController.AssetProject.Descriptor.OrganizationId}' and wait for projects...");
            assetsUIToolkitController.SelectOrganization(organization);
            // After loading of projects, SelectProjectInUI via AssetsUIToolkitController.OnAssetProjectsLoadedEvent will be called.
        }

        private void SelectProjectInUI(IOrganization organization)
        {
            var assetProject = DeepLinkController.AssetProject;
            if (assetProject == null) return;

            if (organization?.Id != assetProject.Descriptor.OrganizationId)
            {
                Debug.Log($"Deep linking: projects loaded for organization '{organization?.Id}' and needed for '{assetProject.Descriptor.OrganizationId}'. Waiting...");
                return;
            }

            var projectList = SharedUIManager.Instance?.AssetProjectScrollList;
            if (projectList == null)
            {
                Debug.LogError("Deep linking: AssetProjectScrollList is null.");
                DeepLinkController.ClearCurrentLink();
                return;
            }

            // Find the ActionButton whose userData is an AssetProjectInfo with matching id
            var projectButton = projectList.Query<ActionButton>()
                .ToList()
                .FirstOrDefault(actionButton => actionButton.userData is AssetProjectInfo info && info.AssetProject?.Descriptor == assetProject.Descriptor);

            if (projectButton == null)
            {
                //Debug.Log($"Deep linking: project button for project '{assetProject.Name}' not found in AssetProjectScrollList.");
                // Do not clear the link here, as we may have a valid project but it is not yet loaded in the UI.
                return;
            }

            var info = (AssetProjectInfo)projectButton.userData;
            //Debug.Log($"Deep linking: project button '{projectButton.label}' for project '{info.AssetProject.Name}' has been found. Click and wait for assets...");

            var assetController = FindFirstObjectByType<AssetsUIToolkitController>();
            if (assetController == null)
            {
                Debug.LogError("Deep linking: AssetsUIToolkitController not found in the scene.");
                DeepLinkController.ClearCurrentLink();
                return;
            }

            AssetsController.AllAssetsLoaded += SelectAssetInUI;
            StartCoroutine(SelectProject());

            IEnumerator SelectProject()
            {
                yield return new WaitForEndOfFrame();

                // Close Project to let system open it again
                AssetsUIBaseController.CloseProjectNode(projectButton);

                // Open Project
                assetController.OnAssetProjectButtonClick(info, projectButton);
                projectList.ScrollTo(projectButton);

                SelectCollectionInUIAsync();
            }
        }

        private void SelectCollectionInUIAsync()
        {
            if (DeepLinkController.AssetCollectionDescriptor == null) return;

            StartCoroutine(WaitForCollectionButtonAndSelect());

            IEnumerator WaitForCollectionButtonAndSelect()
            {
                var collectionDescriptorToSelect = DeepLinkController.AssetCollectionDescriptor.Value;

                // Find collection button
                ActionButton collectionButton = null;
                while (true)
                {
                    collectionButton = SharedUIManager.Instance.AssetProjectScrollList
                        .Query<ActionButton>()
                        .Where(actionButton => actionButton.userData is IAssetCollection assetCollection && assetCollection.Descriptor == collectionDescriptorToSelect)
                        .First();

                    if (collectionButton == null)
                    {
                        Debug.Log($"Deep linking: collection button for collection '{collectionDescriptorToSelect.Path}' not found in AssetProjectScrollList. Waiting...");
                        yield return null;
                    }
                    else break;
                }

                var assetCollection = (IAssetCollection)collectionButton.userData;

                // Find all parent collection buttons
                var parentCollectionButtons = new List<ActionButton>();
                var collectionButtonToCheck = collectionButton;
                while (collectionButtonToCheck.userData is IAssetCollection assetCollectionToCheck
                    && !assetCollectionToCheck.ParentPath.IsEmpty)
                {
                    var parentCollectionButton = SharedUIManager.Instance.AssetProjectScrollList
                         .Query<ActionButton>()
                         .Where(actionButton => actionButton.userData is IAssetCollection assetCollection
                                && assetCollection.Descriptor.Path == assetCollectionToCheck.ParentPath)
                         .First();
                    parentCollectionButtons.Add(parentCollectionButton);
                    collectionButtonToCheck = parentCollectionButton;
                }

                // Open all collection buttons starting from the root collection
                parentCollectionButtons.Reverse();
                foreach (var parentCollectionButton in parentCollectionButtons)
                {
                    Debug.Log($"Deep linking: open collection button '{parentCollectionButton.label}'.");
                    AssetsUIBaseController.OpenCollectionNode(parentCollectionButton);
                }

                // Select current collection button
                AssetsUIBaseController.RefreshListViewButton(collectionButton);
                SharedUIManager.AssetCollection = assetCollection;
            }
        }

        private void SelectAssetInUI(IAssetProject assetProject, CollectionDescriptor? assetCollectionDescriptor)
        {
            //Debug.Log($"Deep linking: SelectAssetInUI called for '{assetProject?.Name}\\{assetCollectionDescriptor?.Path}', link path is '{DeepLinkController.AssetProject?.Name}\\{DeepLinkController.AssetCollectionDescriptor?.Path}'.");

            if (assetProject?.Descriptor != DeepLinkController.AssetProject?.Descriptor
                && ((!DeepLinkController.AssetCollectionDescriptor.HasValue && !assetCollectionDescriptor.HasValue)
                    || DeepLinkController.AssetCollectionDescriptor == assetCollectionDescriptor)) return; // wait for proper project and collection assets to be loaded

            AssetsController.AllAssetsLoaded -= SelectAssetInUI;

            if (!DeepLinkController.AssetInfo.HasValue)
            {
                Debug.LogWarning("Deep linking: s_assetInfo is null. Cannot select asset in UI.");
                return;
            }

            var assetInfo = DeepLinkController.AssetInfo.Value;
            var assetGridView = SharedUIManager.Instance?.AssetGridView;
            if (assetGridView == null)
            {
                Debug.LogError("Deep linking: SharedUIManager.Instance?.AssetGridView is null");
                DeepLinkController.ClearCurrentLink();
                return;
            }

            StartCoroutine(ApplyAssetSelection());

            IEnumerator ApplyAssetSelection()
            {
                // Wait for two frames because AddModelToolUIController.InitializeUI waits for one.
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();

                // Wait for AssetGridView.itemsSource to be set
                List<AssetInfo> sourceItems = null;
                while (sourceItems == null)
                {
                    yield return null;
                    sourceItems = assetGridView.itemsSource as List<AssetInfo>;
                }

                // Find asset disregard of version
                var itemIndex = sourceItems.FindIndex(sourceItem =>
                    sourceItem.Asset.Descriptor.OrganizationId == assetInfo.Asset.Descriptor.OrganizationId
                    && sourceItem.Asset.Descriptor.ProjectId == assetInfo.Asset.Descriptor.ProjectId
                    && sourceItem.Asset.Descriptor.AssetId == assetInfo.Asset.Descriptor.AssetId);

                if (itemIndex < 0)
                {
                    //Debug.Log($"Deep linking: item '{assetInfo.Asset.Name}' not found in AssetGridView itemsSource.");
                    DeepLinkController.ClearCurrentLink();
                    yield break;
                }

                //Debug.Log($"Deep linking: item '{assetInfo.Asset.Name}' has been found, index is {itemIndex}. Apply grid selection...");
                assetGridView.SetSelection(itemIndex);
                StartCoroutine(ScrollToItem());

                IEnumerator ScrollToItem()
                {
                    yield return new WaitForEndOfFrame();
                    assetGridView.ScrollToItem(itemIndex);
                }

                // Select asset version
                var uiToolkitController = FindFirstObjectByType<AssetsUIToolkitController>();
                var infoController = uiToolkitController?.AssetInfoUIController;
                if (infoController == null)
                {
                    Debug.LogError("Deep linking: AssetsInfoUIToolkitController not found in the scene.");
                    DeepLinkController.ClearCurrentLink();
                    yield break;
                }

                if (!infoController.IsVisible())
                {
                    Debug.LogError("Deep linking: AssetsInfoUIToolkitController is not visible.");
                    DeepLinkController.ClearCurrentLink();
                    yield break;
                }

                Debug.Log("Deep linking: Waiting for AssetVersionDropdown.sourceItems...");
                List<AssetInfo> dropDownAssets;
                do
                {
                    yield return null;
                    dropDownAssets = infoController.AssetVersionDropdown.sourceItems as List<AssetInfo>;
                }
                while (dropDownAssets == null || dropDownAssets.Count == 0);

                var versionIndex = dropDownAssets.FindIndex(dropDownAsset => dropDownAsset.Asset.Descriptor == assetInfo.Asset.Descriptor);
                if (versionIndex < 0)
                {
                    Debug.LogError($"Deep linking: version '{assetInfo.Asset.Descriptor.AssetVersion}' not found in AssetVersionDropdown.sourceItems.");
                    DeepLinkController.ClearCurrentLink();
                    yield break;
                }

                if (!infoController.AssetVersionDropdown.enabledSelf)
                {
                    Debug.Log("Deep linking: version dropdown is disabled.");
                    DeepLinkController.ClearCurrentLink();
                    yield break;
                }

                Debug.Log($"Deep linking: select version index {versionIndex}.");
                infoController.AssetVersionDropdown.selectedIndex = versionIndex;
                DeepLinkController.ClearCurrentLink();
            }
        }
    }
}