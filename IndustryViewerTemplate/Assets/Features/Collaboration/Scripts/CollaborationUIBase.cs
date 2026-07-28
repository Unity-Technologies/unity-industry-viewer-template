using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Button = Unity.AppUI.UI.Button;
using Unity.Cloud.Identity;
using Unity.Cloud.Collaboration;
using System.Collections;
using Unity.Industry.Viewer.Shared;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Identity;
using System.Collections.Generic;
using System.Linq;
using System;
using Unity.AppUI.Core;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Industry.Viewer.Collaboration
{
    public abstract class CollaborationUIBase : MonoBehaviour
    {
        public const string THREAD_URL_TEMPLATE = "https://cloud.unity.com/home/organizations/{0}/projects/{1}/assets?assetId={2}:{3}&tab=collaboration&rootAnnotationId={4}";

        private const string k_AnnotationRootName = "AnnotationRoot";
        private const string k_AnnotationParentName = "AnnotationParent";
        private const string k_ReplyParentName = "ReplyParent";
        private const string k_AttachmentIconButtonName = "AttachmentIconButton";
        private const string k_SendIconButtonName = "SendIconButton";
        private const string k_BackButtonName = "BackButton";
        private const string k_NewCommentButtonName = "NewCommentButton";
        public const string k_UnResolvedIconName = "check-circle";
        public const string k_ResolvedIconName = "check-fill";
        private const string k_CaretDownIconName = "caret-down";
        private const string k_CaretUpIconName = "caret-up";
        public const string k_ResolvedClassNam = "resolved";
        private const string k_ResolveActionButtonName = "ResolvedButton";
        private const string k_ThreadTitleName = "ThreadTitle";
        private const string k_ThreadMenuIconButtonName = "ThreadMenuIconButton";
        private const string k_ThreadFilterOptionsButtonName = "ThreadFilterOptionsButton";
        private const string k_AttachmentGridViewName = "AttachmentGridView";
        private const string k_TextAreaFocusClassName = "focused";
        private const string k_ReadingAnnotationClassName = "read-annotation";
        private const string k_EditingReplyClassName = "editing-reply";
        private const string k_HasAttachmentClassName = "has-attachment";
        private const string k_ReadyToSendClassName = "ready-to-send";
        
        [HideInInspector]
        public int AttachmentGridViewColumnCount = 3;

        public Action<IAnnotation> AnnotationHasBeenUpdated;
        public Action<IAnnotation> RootAnnotationDeleted;
        
        [SerializeField]
        protected StyleSheet annotationStylesheet;
        
        #region Annotation
        protected float k_AnnotationItemSpacing = 10f;
        
        [SerializeField]
        protected VisualTreeAsset annotationItemTemplate;

        #endregion
        
        [SerializeField]
        protected VisualTreeAsset commentUITemplate;
        
        public VisualTreeAsset ReactionUITemplate => reactionUITemplate;
        
        [SerializeField]
        protected VisualTreeAsset reactionUITemplate;
        
        protected TextArea m_TextArea;
        protected ScrollView m_AnnotationContainer;
        protected VisualElement m_CommentContainer, m_AnnotationRoot;
        protected VisualElement m_ReplyContainer;
        private VisualElement m_ThreadTitleContainer;
        protected IconButton m_SendIconButton, m_AttachmentIconButton, m_ThreadMenuIconButton;
        protected GridView m_AttachmentGridView;
        private ActionButton m_BackToAllThreadsButton;
        private IconButton m_NewCommentButton;
        private ActionButton m_ResolveButton;
        private ActionButton m_ThreadFilterOptionsButton;

        private Popover m_PopoverMenu;
        
        #region Attachment

        public VisualTreeAsset AttachmentMessageEntryTemplate => attachmentMessageEntryTemplate;
        
        [SerializeField]
        protected VisualTreeAsset attachmentMessageEntryTemplate;
        
        [SerializeField]
        protected VisualTreeAsset attachmentUITemplate;
        
        public Texture2D DefaultFileIcon => defaultFileIcon;
        
        [SerializeField]
        protected Texture2D defaultFileIcon;

        #endregion

        #region Thread Menu
        
        private Button m_FollowThreadButton;
        private Button m_UnfollowThreadButton;
        private Button m_CopyLinkButton;
        private Button m_DeleteThreadButton;

        #endregion
        
        public IUserInfo UserInfo => m_UserInfo;
        protected IUserInfo m_UserInfo;
        protected bool m_Initialized;
        public bool ReadingThread => m_currentRootAnnotation != null;
        protected CollaborationController m_CollaborationController;
        public IAnnotation CurrentAnnotation => m_currentRootAnnotation;
        protected IAnnotation m_currentRootAnnotation;
        private WaitForSeconds m_WaitForUIUpdate;

        [HideInInspector]
        public bool isCurrentThreadSubscribed;
        
        public abstract AssetInfo? SelectedAsset { get; }

        public abstract GameObject SpatialAttachment { get; }
        
        public CancellationTokenSource TokenSource { get; private set; }

        public CollaborationController.FilterType FilterType = CollaborationController.FilterType.All;
        
        public LocalizedStringAsset LocalizedStringAsset;

        protected Text m_GuestModeText;

        public async void InsertCollaborationNotAvailable(VisualElement container)
        {
            container.Clear();
            string message = string.Empty;
            if (NetworkDetector.IsOffline || IdentityController.GuestMode)
            {
                message = NetworkDetector.IsOffline? await LocalizedStringAsset.CurrentlyOfflineLocalizedString.GetTitleLocalizedStringForAppUIAsync()
                    : await LocalizedStringAsset.GuestModeLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
            else
            {
                message = await LocalizedStringAsset.NotLoggedInLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            }
            m_GuestModeText = new Text(message);
            container.Add(m_GuestModeText);
        }
        
        public virtual async void InitializeUI(UIDocument uiDoc, VisualElement contentContainer,
            CollaborationController.FilterType filterType)
        {
            TokenSource ??= new CancellationTokenSource();
            m_AnnotationRoot = contentContainer.Q<VisualElement>(k_AnnotationRootName);
            
            m_AnnotationContainer = contentContainer.Q<ScrollView>(k_AnnotationParentName);
            m_ReplyContainer = contentContainer.Q<VisualElement>(k_ReplyParentName);
            m_SendIconButton = contentContainer.Q<IconButton>(k_SendIconButtonName);
            m_AttachmentIconButton = contentContainer.Q<IconButton>(k_AttachmentIconButtonName);

            m_TextArea = m_ReplyContainer.Q<TextArea>();
            var textField = m_TextArea.Q<UnityEngine.UIElements.TextField>(); 
            textField.multiline = true;
            
            CollaborationUIUtility.SetupMentionComposer(m_TextArea, () => SelectedAsset);

            m_TextArea.RegisterCallback<FocusInEvent>(OnTextAreaFocus);
            m_TextArea.RegisterCallback<FocusOutEvent>(OnTextAreaLoseFocus);
            m_TextArea.RegisterValueChangingCallback(OnTextAreaValueChanging);
            m_TextArea.RegisterValueChangedCallback(OnTextAreaValueChanged);
            m_BackToAllThreadsButton = contentContainer.Q<ActionButton>(k_BackButtonName);
            m_BackToAllThreadsButton.clicked += BackToAllThreadsButtonOnClicked;
            
            m_SendIconButton.RegisterCallback<ClickEvent>(SendIconClicked);
            m_NewCommentButton = contentContainer.Q<IconButton>(k_NewCommentButtonName);
            m_NewCommentButton.clicked += OnNewCommentButtonClicked;

            m_ResolveButton = contentContainer.Q<ActionButton>(k_ResolveActionButtonName);
            m_ResolveButton.clicked += OnResolveButtonClicked;
                
            m_ThreadTitleContainer = contentContainer.Q<VisualElement>(k_ThreadTitleName);

            m_ThreadMenuIconButton = contentContainer.Q<IconButton>(k_ThreadMenuIconButtonName);
            m_ThreadMenuIconButton?.RegisterCallback<ClickEvent>(OnThreadMenuClicked);
                
            m_ThreadFilterOptionsButton = contentContainer.Q<ActionButton>(k_ThreadFilterOptionsButtonName);
            m_ThreadFilterOptionsButton.label = filterType == CollaborationController.FilterType.All? 
                    await LocalizedStringAsset.AllThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync():
                    await LocalizedStringAsset.OpenThreadOnlyLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            m_ThreadFilterOptionsButton.clicked += ThreadFilterOptionsButtonOnClicked;
                
            m_AttachmentIconButton.RegisterCallback<ClickEvent>(OnAddAttachmentIconClicked);
#if UNITY_WEBGL && !UNITY_EDITOR
            m_AttachmentIconButton.SetEnabled(false);
#endif
            m_AttachmentGridView = contentContainer.Q<GridView>(k_AttachmentGridViewName);
            m_AttachmentGridView.columnCount = AttachmentGridViewColumnCount;
            m_AttachmentGridView.makeItem = AttachmentGridViewItem;
            m_AttachmentGridView.bindItem = BindAttachmentGridViewItem;
            m_AttachmentGridView.unbindItem = UnbindAttachmentItem;
            
            uiDoc.rootVisualElement.AddStyleSheetIfMissing(annotationStylesheet);
            
            if (!m_ReplyContainer.ClassListContains(k_ReadyToSendClassName))
            {
                m_ReplyContainer.AddToClassList(k_ReadyToSendClassName);
            }
        }

        public virtual void UninitializeUI()
        {
            if (m_BackToAllThreadsButton != null)
            {
                m_BackToAllThreadsButton.clicked -= BackToAllThreadsButtonOnClicked;
            }
            m_TextArea?.UnregisterCallback<FocusInEvent>(OnTextAreaFocus);
            m_TextArea?.UnregisterCallback<FocusOutEvent>(OnTextAreaLoseFocus);
            m_TextArea?.UnregisterValueChangingCallback(OnTextAreaValueChanging);
            m_TextArea?.UnregisterValueChangedCallback(OnTextAreaValueChanged);
            if (m_NewCommentButton != null)
            {
                m_NewCommentButton.clicked -= OnNewCommentButtonClicked;
            }

            if (m_ResolveButton != null)
            {
                m_ResolveButton.clicked -= OnResolveButtonClicked;
            }

            m_ThreadMenuIconButton?.UnregisterCallback<ClickEvent>(OnThreadMenuClicked);
            
            if (m_ThreadFilterOptionsButton != null)
            {
                m_ThreadFilterOptionsButton.clicked -= ThreadFilterOptionsButtonOnClicked;
            }

            m_AttachmentIconButton?.UnregisterCallback<ClickEvent>(OnAddAttachmentIconClicked);
            m_SendIconButton?.UnregisterCallback<ClickEvent>(SendIconClicked);
        }

        public static async void GetTranslation(Text label, LocalizedString localizedString)
        {
            label.text = await localizedString.GetTitleLocalizedStringForAppUIAsync();
        }
        
        public virtual void OnAnnotationLoaded(IReadOnlyList<IAnnotation> listOfAnnotations)
        {
            m_AnnotationContainer.Clear();
            if (listOfAnnotations == null) return;
            _ = Populate();
            return;

            async Task Populate()
            {
                m_UserInfo ??= await PlatformServices.CompositeAuthenticator.GetUserInfoAsync();
                int index = 0;
                foreach (var annotation in listOfAnnotations)
                {
                    if(annotation.HasDraftReply.HasValue && annotation.HasDraftReply.Value) continue; // Skip annotations with draft replies
                    
                    var newAnnotation = annotationItemTemplate.Instantiate().Children().First();
                    
                    bool isCreator = annotation.CreatedBy == m_UserInfo.UserId.ToString();
                    AnnotationEntryController entryController =
                        new AnnotationEntryController(m_CollaborationController, this, annotation, newAnnotation, isCreator, ReadingThread);
                    
                    newAnnotation.userData = entryController;
                    
                    m_AnnotationContainer.Add(newAnnotation);

                    if (index < listOfAnnotations.Count - 1)
                    {
                        newAnnotation.style.marginBottom = new Length(k_AnnotationItemSpacing, LengthUnit.Pixel);
                    }
                    index++;
                }
            }
        }
        
        public void DeleteAttachment(GridView gridView, Attachment attachment)
        {
            var existing = gridView.itemsSource as List<Attachment>;
            if (existing == null || existing.Count == 0) return;
            var toRemove = existing.FirstOrDefault(a => a == attachment);
            if (toRemove == null) return;
            existing.Remove(toRemove);
            gridView.itemsSource = existing;
            if (gridView.itemsSource == null || gridView.itemsSource.Count == 0)
            {
                gridView.style.display = DisplayStyle.None;
                if (m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
                {
                    m_ReplyContainer.RemoveFromClassList(k_HasAttachmentClassName);
                }

                if (string.IsNullOrEmpty(m_TextArea.value))
                {
                    if (m_TextArea.ClassListContains(k_TextAreaFocusClassName))
                    {
                        m_TextArea.RemoveFromClassList(k_TextAreaFocusClassName);
                    }
                    if (m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
                    {
                        m_ReplyContainer.RemoveFromClassList(k_EditingReplyClassName);
                    }
                }
            }
            if (gridView == m_AttachmentGridView)
            {
                CollaborationUIUtility.CheckValidInput(m_TextArea, m_AttachmentGridView, m_SendIconButton);
            }
        }
        
        public virtual async void OpenRootThread(IAnnotation annotation)
        {
            if (NetworkDetector.IsOffline) return;
            LoadingUIPanel.HideLoadingPanel(null);
            m_currentRootAnnotation = annotation;
            m_AnnotationContainer.Clear();
            m_ReplyContainer.style.display = DisplayStyle.Flex;
            if (!m_AnnotationContainer.ClassListContains(k_ReadingAnnotationClassName))
            {
                m_AnnotationContainer.AddToClassList(k_ReadingAnnotationClassName);
            }
            m_TextArea.placeholder = await LocalizedStringAsset.ReplyPlaceHolderLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            m_TextArea.SetValueWithoutNotify(string.Empty);
            CollaborationUIUtility.ClearMentions(m_TextArea);
            m_NewCommentButton.SetEnabled(true);
            CollaborationUIUtility.CheckValidInput(m_TextArea, m_AttachmentGridView, m_SendIconButton);
            m_AttachmentGridView.itemsSource = null;
            m_AttachmentGridView.style.display = DisplayStyle.None;
            m_BackToAllThreadsButton.style.display = DisplayStyle.Flex;
            m_ThreadFilterOptionsButton.style.display = DisplayStyle.None;
            m_ThreadTitleContainer.style.display = DisplayStyle.Flex;
            bool hasResolved = annotation.Resolved.HasValue;
            m_ThreadMenuIconButton.SetEnabled(true);
            m_ResolveButton.SetEnabled(true);
            m_ResolveButton.label = hasResolved ? await LocalizedStringAsset.ResolvedLocalizedString.GetTitleLocalizedStringForAppUIAsync(): string.Empty;
            m_ResolveButton.icon = hasResolved ? k_ResolvedIconName : k_UnResolvedIconName;
            if (hasResolved)
            {
                if (!m_ResolveButton.ClassListContains(k_ResolvedClassNam))
                {
                    m_ResolveButton.AddToClassList(k_ResolvedClassNam);
                }
            }
            else
            {
                if (m_ResolveButton.ClassListContains(k_ResolvedClassNam))
                {
                    m_ResolveButton.RemoveFromClassList(k_ResolvedClassNam);
                }
            }
            
            if (m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_HasAttachmentClassName);
            }
            if (m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_EditingReplyClassName);
            }
            if (m_ReplyContainer.ClassListContains(k_ReadyToSendClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_ReadyToSendClassName);
            }
            CollaborationController.OpenThread?.Invoke(SelectedAsset.Value, TokenSource, annotation, OnThreadOpen);
        }

        private void OnThreadOpen(bool isFollowing, IReadOnlyList<IAnnotation> replies)
        {
            isCurrentThreadSubscribed = isFollowing;
            OnAnnotationLoaded(replies);
        }
        
        public void QueryThread(CollaborationController.FilterType filterType)
        {
            if(NetworkDetector.IsOffline) return;
            m_currentRootAnnotation = null;
            ResetUIToDefault();
            CollaborationController.QueryThreads?.Invoke(SelectedAsset.Value, TokenSource, filterType, OnAnnotationLoaded);
        }
        
        public void UnbindAttachmentItem(VisualElement element, int index)
        {
            if (element.userData is AttachmentUploadEntryController attachmentUploadEntryController)
            {
                attachmentUploadEntryController.Dispose();
            }
        }
        
        public VisualElement AttachmentGridViewItem()
        {
            return attachmentUITemplate.Instantiate().Children().First();
        }
        
        private void BindAttachmentGridViewItem(VisualElement element, int index)
        {
            BindAttachmentGridViewItem(m_AttachmentGridView, element, index);
        }
        
        public void BindAttachmentGridViewItem(GridView gridView, VisualElement element, int index)
        {
            var attachment = (Attachment)gridView.itemsSource[index];
            var controller = new AttachmentUploadEntryController(this, gridView, attachment, element);
            element.userData = controller;
        }
        
        private void OnAddAttachmentIconClicked(ClickEvent evt)
        {
            if(evt.target != m_AttachmentIconButton) return;
            SharedUIManager.Instance.AssetsContainer.SetEnabled(false);

            StartCoroutine(WaitForTranslation());
            return;

            IEnumerator WaitForTranslation()
            {
                var operation = LocalizedStringAsset.SelectAttachmentLocalizedString.GetLocalizedStringAsync();
                yield return operation;
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    FileBrowser.OpenFiles(operation.Result, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "", OpenFilesCallback);
                }
                else
                {
                    Debug.LogError("Failed to get localized string for file dialog.");
                }
            }
            
            void OpenFilesCallback(string[] filePaths)
            {
                StartCoroutine(WaitForAddAttachment(filePaths));
            }

            IEnumerator WaitForAddAttachment(string[] filePaths)
            {
                var waitForEndOfFrame = new WaitForEndOfFrame();
                if (filePaths == null || filePaths.Length == 0)
                {
                    yield return waitForEndOfFrame;
                    SharedUIManager.Instance.AssetsContainer.SetEnabled(true);
                    yield break;
                }
                
                foreach (var filePath in filePaths)
                {
                    bool isRepeated = CollaborationUIUtility.RepeatedAttachment(m_AttachmentGridView, null, filePath, out var type);
                    if (isRepeated)
                    {
                        ShowRepeatedAttachmentMessage(type);
                        continue;
                    }
            
                    m_AttachmentGridView.itemsSource ??= new List<Attachment>();
                    var existing = m_AttachmentGridView.itemsSource as List<Attachment>;
            
                    var newAttachment = new Attachment(filePath);
                    m_AttachmentGridView.style.display = DisplayStyle.Flex;
            
                    existing.Add(newAttachment);
                    m_AttachmentGridView.itemsSource = existing;
                    yield return waitForEndOfFrame;
                }
                m_WaitForUIUpdate ??= new WaitForSeconds(0.25f);
                yield return m_WaitForUIUpdate;
                SharedUIManager.Instance.AssetsContainer.SetEnabled(true);
#if UNITY_STANDALONE || UNITY_EDITOR
                m_TextArea.Focus();
#else
                WhenTextFieldIsFocused();
#endif
            }
        }
        
        public async void ShowRepeatedAttachmentMessage(AddAttachmentFailType type)
        {
            string message = string.Empty;
            switch (type)
            {
                case AddAttachmentFailType.DuplicateFileName:
                    message = await LocalizedStringAsset.DuplicateFileNameLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                    break;
                
                case AddAttachmentFailType.DuplicateFilePath:
                    message = await LocalizedStringAsset.DuplicateFilePathLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                    break;
            }

            var toast = Toast.Build(m_CommentContainer, message, NotificationDuration.Short)
                .SetStyle(NotificationStyle.Negative);
            toast.Show();
        }
        
        private void ThreadFilterOptionsButtonOnClicked()
        {
            var menu = MenuBuilder.Build(m_ThreadFilterOptionsButton)
                .AddAction(9999, AllThreadButton)
                .AddAction(0, BindItemFunc)
                .Pop();
            
            menu.shown += MenuOnShown;
            menu.dismissed += MenuOnDismissed;
            menu.Show();
            return;
            
            void MenuOnDismissed(MenuBuilder arg1, DismissType arg2)
            {
                arg1.dismissed -= MenuOnDismissed;
                CollaborationUIUtility.JustDismissedPopover = true;
                ResetDismissedPopover();
                m_ThreadFilterOptionsButton.trailingIcon = k_CaretDownIconName;
            }
            
            void MenuOnShown(MenuBuilder builder)
            {
                builder.shown -= MenuOnShown;
                CollaborationUIUtility.NamePopover?.Dismiss();
                m_ThreadFilterOptionsButton.trailingIcon = k_CaretUpIconName;
            }

            async void BindItemFunc(MenuItem item)
            {
                item.label = await LocalizedStringAsset.OpenThreadOnlyLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = true;
                item.value = FilterType == CollaborationController.FilterType.Opened;
                item.clickable.clicked += OnOpenThreadsClicked;
            }

            async void AllThreadButton(MenuItem item)
            {
                item.label = await LocalizedStringAsset.AllThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = true;
                item.value = FilterType == CollaborationController.FilterType.All;
                item.clickable.clicked += OnAllThreadsClicked;
            }
        }

        public void ResetDismissedPopover()
        {
            StartCoroutine(ResetValue());
            return;

            IEnumerator ResetValue()
            {
                yield return new WaitForEndOfFrame();
                CollaborationUIUtility.JustDismissedPopover = false;
            }
        }
        
        private async void OnOpenThreadsClicked()
        {
            if(NetworkDetector.IsOffline) return;
            m_ThreadFilterOptionsButton.label = await LocalizedStringAsset.OpenThreadOnlyLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            FilterType = CollaborationController.FilterType.Opened;
            QueryThread(FilterType);
        }
        
        private async void OnAllThreadsClicked()
        {
            if(NetworkDetector.IsOffline) return;
            m_ThreadFilterOptionsButton.label = await LocalizedStringAsset.AllThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
            FilterType = CollaborationController.FilterType.All;
            QueryThread(FilterType);
        }
        
        private void OnThreadMenuClicked(ClickEvent evt)
        {
            ThreadMenuOpen(m_ThreadMenuIconButton, m_currentRootAnnotation, isCurrentThreadSubscribed, out var _);
        }
        
        public void ThreadMenuOpen(VisualElement reference, IAnnotation annotation, bool isCurrentlySubscribed, out MenuBuilder menuBuilder)
        {
            menuBuilder = MenuBuilder.Build(reference)
                .AddAction(999, FollowThread)
                .AddAction(998, UnfollowThread)
                .AddAction(997, CopyLink)
                .AddDivider()
                .AddAction(0, DeleteThread)
                .SetPlacement(PopoverPlacement.BottomEnd)
                .Pop();

            menuBuilder.shown += OnMenuShown;
            menuBuilder.dismissed += OnMenuDismissed;

            menuBuilder.Show();
            return;

            async void FollowThread(MenuItem item)
            {
                item.label = await LocalizedStringAsset.FollowThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.active = true;
                item.style.display = isCurrentlySubscribed? DisplayStyle.None: DisplayStyle.Flex;
                item.clickable.clicked += OnFollowThreadClicked;
            }
            
            async void UnfollowThread(MenuItem item)
            {
                item.label = await LocalizedStringAsset.UnfollowThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.active = true;
                item.style.display = isCurrentlySubscribed? DisplayStyle.Flex: DisplayStyle.None;
                item.clickable.clicked += OnUnfollowThreadClicked;
            }
            
            async void CopyLink(MenuItem item)
            {
                item.label = await LocalizedStringAsset.CopyLinkLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.active = true;
                item.clickable.clicked += OnCopyLinkClicked;
            }
            
            async void DeleteThread(MenuItem item)
            {
                item.label = await LocalizedStringAsset.DeleteThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.active = true;
                bool isCreator = annotation.CreatedBy == m_UserInfo.UserId.ToString();
                item.SetEnabled(isCreator);
                item. clickable. clicked += OnDeleteThreadClicked;
            }
            
            void OnMenuShown(MenuBuilder obj)
            {
                CollaborationUIUtility.NamePopover?.Dismiss();
                obj.shown -= OnMenuShown;
            }
            
            void OnMenuDismissed(MenuBuilder arg1, DismissType arg2)
            {
                CollaborationUIUtility.JustDismissedPopover = true;
                ResetDismissedPopover();
                arg1.dismissed -= OnMenuDismissed;
            }
            
            async void OnDeleteThreadClicked()
            {
                m_PopoverMenu?.Dismiss();
                if(NetworkDetector.IsOffline) return;
                var deleteAlert = new AlertDialog
                {
                    title = await LocalizedStringAsset.DeleteThreadLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                    description = await LocalizedStringAsset.DeleteThreadMessageLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                    variant = AlertSemantic.Destructive
                };
                deleteAlert.SetPrimaryAction(99, await LocalizedStringAsset.DeleteLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
                {
                    CollaborationController.DeleteAnnotation?.Invoke(SelectedAsset.Value, TokenSource, annotation, (success) =>
                    {
                        if (success)
                        {
                            RootAnnotationDeleted?.Invoke(annotation);
                            BackToAllThreadsButtonOnClicked();
                        }
                    });
                });
            
                deleteAlert.SetCancelAction(0, await LocalizedStringAsset.CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());
                var modal = Modal.Build(reference, deleteAlert);
                modal.Show();
            }
            
            void OnFollowThreadClicked()
            {
                m_PopoverMenu?.Dismiss();
                if(NetworkDetector.IsOffline) return;
                CollaborationController.FollowOrUnfollowThread?.Invoke(SelectedAsset.Value, TokenSource, annotation, true, UpdateSubscriptionState);
            }
            
            void OnUnfollowThreadClicked()
            {
                m_PopoverMenu?.Dismiss();
                if(NetworkDetector.IsOffline) return;
                CollaborationController.FollowOrUnfollowThread?.Invoke(SelectedAsset.Value, TokenSource, annotation, false, UpdateSubscriptionState);
            }

            void UpdateSubscriptionState(bool isFollowing, IAnnotation annotationToCheck)
            {
                isCurrentThreadSubscribed = isFollowing;
                foreach (var visualElement in m_AnnotationContainer.contentContainer.Children())
                {
                    if(visualElement.userData.GetType() != typeof(AnnotationEntryController)) continue;
                    var controller = (AnnotationEntryController)visualElement.userData;
                    if (controller.Annotation.AnnotationId == annotationToCheck.AnnotationId)
                    {
                        _ = controller.CheckSubscription();
                    }
                }
            }
            
            void OnCopyLinkClicked()
            {
                m_PopoverMenu?.Dismiss();
                if(!SelectedAsset.HasValue) return;
                var orgId = SelectedAsset.Value.Asset.Descriptor.OrganizationId;
                var projectId = SelectedAsset.Value.Asset.Descriptor.ProjectId;
                var assetId = SelectedAsset.Value.Asset.Descriptor.AssetId;
                var versionId = SelectedAsset.Value.Asset.Descriptor.AssetVersion;
                var threadId = annotation.AnnotationId;
                var link = string.Format(THREAD_URL_TEMPLATE, orgId, projectId, assetId, versionId, threadId);
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(link);
#else
                GUIUtility.systemCopyBuffer = link;
#endif
            }
        }
        
        private void OnResolveButtonClicked()
        {
            if(NetworkDetector.IsOffline) return;
            if (m_currentRootAnnotation == null) return;
            bool isResolved = m_currentRootAnnotation.Resolved.HasValue;
            m_ResolveButton.SetEnabled(false);
            CollaborationController.ResolveOrOpenThread?.Invoke(SelectedAsset.Value, TokenSource, m_currentRootAnnotation, !isResolved, OnResolvedFinished);
        }

        private void OnResolvedFinished(IAnnotation newAnnotation)
        {
            AnnotationHasBeenUpdated?.Invoke(newAnnotation);
            OpenRootThread(newAnnotation);
        }

        protected virtual void OnNewCommentButtonClicked()
        {
            NewCommentUIActive(() =>
            {
                _ = PlaceHolderText();
                m_TextArea.Focus();
            });
            return;

            async Task PlaceHolderText()
            {
                m_TextArea.placeholder = await LocalizedStringAsset.StartThreadPlaceHolderLocalizedString
                    .GetTitleLocalizedStringForAppUIAsync();
            }
        }

        protected void NewCommentUIActive(Action onUISetupCallback)
        {
            TokenSource?.Cancel();
            m_AttachmentGridView.itemsSource = null;
            m_AttachmentGridView.style.display = DisplayStyle.None;
            m_BackToAllThreadsButton.style.display = DisplayStyle.Flex;
            m_ThreadFilterOptionsButton.style.display = DisplayStyle.None;
            m_ThreadFilterOptionsButton.style.display = DisplayStyle.None;
            m_currentRootAnnotation = null;
            m_NewCommentButton.SetEnabled(false);
            m_AnnotationContainer.Clear();
            m_ReplyContainer.style.display = DisplayStyle.Flex;
            m_TextArea.SetValueWithoutNotify(string.Empty);
            CollaborationUIUtility.ClearMentions(m_TextArea);
            m_SendIconButton.SetEnabled(false);
            m_ThreadMenuIconButton.SetEnabled(false);
            m_ResolveButton.SetEnabled(false);
            m_ResolveButton.label = string.Empty;
            m_ResolveButton.icon = k_UnResolvedIconName;
            if (m_ResolveButton.ClassListContains(k_ResolvedClassNam))
            {
                m_ResolveButton.RemoveFromClassList(k_ResolvedClassNam);
            }
            if (m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_HasAttachmentClassName);
            }
            if (m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_EditingReplyClassName);
            }
            onUISetupCallback?.Invoke();
        }
        
        private void SendIconClicked(ClickEvent evt)
        {
            if(NetworkDetector.IsOffline) return;
            LoadingUIPanel.ShowLoadingPanel?.Invoke(() =>
            {
                string rootId = ReadingThread? m_currentRootAnnotation.AnnotationId.ToString(): string.Empty;
                string text = CollaborationUIUtility.GetCloudText(m_TextArea);
                var attachments = m_AttachmentGridView.itemsSource as List<Attachment>;
                
                CollaborationController.NewAnnotation?.Invoke(SelectedAsset.Value, TokenSource, rootId, text, attachments, SpatialAttachment, OnAnnotationCreated);
            });
        }
        
        protected virtual void OnAnnotationCreated(bool success, IAnnotation newAnnotation)
        {
            //Debug.Log("Succeed " + success);
            ResetReplyInput();
            LoadingUIPanel.HideLoadingPanel?.Invoke(() =>
            {
                if (success)
                {
                    var isNewAnnotationARoot =
                        newAnnotation.RootAnnotationId == null;
                    OpenRootThread(isNewAnnotationARoot ? newAnnotation : m_currentRootAnnotation);
                }
                else
                {
                    QueryThread(FilterType);
                }
            });
            return;
            
            void ResetReplyInput()
            {
                m_TextArea.SetValueWithoutNotify(string.Empty);
                CollaborationUIUtility.ClearMentions(m_TextArea);
                if (m_TextArea.ClassListContains(k_TextAreaFocusClassName))
                {
                    m_TextArea.RemoveFromClassList(k_TextAreaFocusClassName);
                }
                m_SendIconButton.SetEnabled(false);
                if (m_ReplyContainer.ClassListContains(k_ReadyToSendClassName))
                {
                    m_ReplyContainer.RemoveFromClassList(k_ReadyToSendClassName);
                }
            }
        }
        
        public virtual void BackToAllThreadsButtonOnClicked()
        {
            if(NetworkDetector.IsOffline) return;
            QueryThread(FilterType);
        }
        
        private void OnTextAreaValueChanged(ChangeEvent<string> evt)
        {
            CollaborationUIUtility.CheckValidInput(m_TextArea, m_AttachmentGridView, m_SendIconButton);
        }

        private void OnTextAreaValueChanging(ChangingEvent<string> evt)
        {
            if (SelectedAsset.HasValue)
            {
                CollaborationUIUtility.OnTextAreaValueChanging(SelectedAsset.Value, evt);
            }
            CollaborationUIUtility.CheckValidInput(m_TextArea, m_AttachmentGridView, m_SendIconButton);
        }
        
        private void UpdateTextAreaStateWhenFocusChanged()
        {
            CollaborationUIUtility.CheckValidInput(m_TextArea, m_AttachmentGridView, m_SendIconButton);
        }
        
        private void OnTextAreaLoseFocus(FocusOutEvent evt)
        {
            UpdateTextAreaStateWhenFocusChanged();
            StartCoroutine(UIUpdate());
            return;
            
            IEnumerator UIUpdate()
            {
                m_WaitForUIUpdate ??= new WaitForSeconds(0.25f);
                yield return m_WaitForUIUpdate;
                var existing = m_AttachmentGridView.itemsSource as List<Attachment>;
                bool hasAttachment = existing != null && existing.Count > 0;
                if (!hasAttachment && !CollaborationUIUtility.JustDismissedPopover && CollaborationUIUtility.NamePopover == null)
                {
                    if (m_TextArea.ClassListContains(k_TextAreaFocusClassName))
                    {
                        m_TextArea.RemoveFromClassList(k_TextAreaFocusClassName);
                    }
                    if (m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
                    {
                        m_ReplyContainer.RemoveFromClassList(k_EditingReplyClassName);
                    }
                    
                    if (m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
                    {
                        m_ReplyContainer.RemoveFromClassList(k_HasAttachmentClassName);
                    }
                }
            }
        }
        
        private void OnTextAreaFocus(FocusInEvent evt)
        {
            WhenTextFieldIsFocused();
        }

        private void WhenTextFieldIsFocused()
        {
            UpdateTextAreaStateWhenFocusChanged();
            if (!m_TextArea.ClassListContains(k_TextAreaFocusClassName))
            {
                m_TextArea.AddToClassList(k_TextAreaFocusClassName);
            }
            
            if (!m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
            {
                m_ReplyContainer.AddToClassList(k_EditingReplyClassName);
            }
            var existing = m_AttachmentGridView.itemsSource as List<Attachment>;
            bool hasAttachment = existing != null && existing.Count > 0;
            if (hasAttachment && !m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
            {
                m_ReplyContainer.AddToClassList(k_HasAttachmentClassName);
            }
            
            if (m_ReplyContainer.ClassListContains(k_ReadyToSendClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_ReadyToSendClassName);
            }
        }
        
        protected void ClearToken()
        {
            TokenSource?.Cancel();
            TokenSource?.Dispose();
            TokenSource = null;
        }

        public void ResetUIToDefault()
        {
            if (m_ReplyContainer.ClassListContains(k_HasAttachmentClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_HasAttachmentClassName);
            }
            if (m_ReplyContainer.ClassListContains(k_EditingReplyClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_EditingReplyClassName);
            }
            if (m_TextArea.ClassListContains(k_TextAreaFocusClassName))
            {
                m_TextArea.RemoveFromClassList(k_TextAreaFocusClassName);
            }

            if (m_AttachmentGridView != null)
            {
                m_AttachmentGridView.itemsSource = null;
            }
            m_ReplyContainer?.DisplayOff();
            if (m_AnnotationContainer.ClassListContains(k_ReadingAnnotationClassName))
            {
                m_AnnotationContainer.RemoveFromClassList(k_ReadingAnnotationClassName);
            }
            if (m_ReplyContainer.ClassListContains(k_ReadyToSendClassName))
            {
                m_ReplyContainer.RemoveFromClassList(k_ReadyToSendClassName);
            }
            m_BackToAllThreadsButton.style.display = DisplayStyle.None;
            m_ThreadTitleContainer.style.display = DisplayStyle.None;
            m_ThreadFilterOptionsButton.style.display = DisplayStyle.Flex;
            m_ThreadFilterOptionsButton.trailingIcon = k_CaretDownIconName;
            m_AnnotationContainer.Clear();
            m_PopoverMenu?.Dismiss();
            m_NewCommentButton.SetEnabled(true);
        }
        
        public abstract void DeleteSpatialAttachment(IAnnotation annotation, IAttachment attachment);
    }
}
