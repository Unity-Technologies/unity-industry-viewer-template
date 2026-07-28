using Unity.Cloud.Collaboration;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Unity.Industry.Viewer.Assets;
using Unity.AppUI.Core;
using Unity.Industry.Viewer.Shared;
using Avatar = Unity.AppUI.UI.Avatar;
using Button = Unity.AppUI.UI.Button;
using MessageType = Unity.Cloud.Collaboration.MessageType;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Industry.Viewer.Collaboration
{
    public class AnnotationEntryController
    {
        private const string k_AnnotationAuthorNameLabel = "NameLabel";
        private const string k_AnnotationsTimeLabel = "TimeLabel";
        
        private const string k_ReplyLabel = "ReplyLabel";
        private const string k_MessageLabel = "AnnotationMessageLabel";
        private const string k_MenuIconButtonName = "MenuIconButton";
        private const string k_ResolveIconButtonName = "ResolveIconButton";

        #region Edit Mode

        private static Action<IAnnotation> AnnotationEditModeEnter;
        private const string k_EditContainerName = "EditContainer";
        private const string k_SaveEditButtonName = "SaveEditButton";
        private const string k_AttachmentIconButtonName = "AttachmentIconButton";
        private const string k_CancelEditButtonName = "CancelEditButton";
        
        private VisualElement m_EditContainer;
        private Button m_SaveEditButton;
        private IconButton m_AttachmentIconButton;
        private Button m_CancelEditButton;
        private TextArea m_EditTextArea;
        private GridView m_AttachmentGridView;

        #endregion
        
        #region Reaction

        private const string k_ReactionIconButtonName = "ReactionIconButton";
        private const string k_ReactionContainerName = "ReactionContainer";
        private VisualElement m_ReactionContainer;
        private IconButton m_ReactionIconButton;
        
        #endregion
        
        #region Attachment
        
        private const string k_AttachmentContainerName = "AttachmentContainer";
        private const string k_AttachmentCountLabelName = "AttachmentCountLabel";
        private const string k_AttachmentListContainerName = "AttachmentListContainer";
        private VisualElement m_attachmentContainer, m_attachmentListContainer;
        private Text m_attachmentCountLabel;
        
        #endregion
        
        public static CollaborationController CollaborationController { get; private set; }
        public readonly CollaborationUIBase CollaborationUIController;
        private static Popup m_Popover;
        
        public readonly IAnnotation Annotation;
        public readonly VisualElement AnnotationEntryRoot;

        private IconButton m_MenuIconButton;
        private IconButton m_ResolveIconButton;
        private Text m_NameLabel;
        private Text m_ReplyLabel;
        private Text m_MessageLabel;
        public readonly bool IsCreator;

        private bool m_PausingMenu;

        private bool IsSubscribed { get; set; }
        
        public AnnotationEntryController(CollaborationController collaborationController,
            CollaborationUIBase collaborationUIController, 
            IAnnotation annotation, VisualElement entryVE, bool isCreator, bool isOpeningThread)
        {
            CollaborationUIController = collaborationUIController;

            if (CollaborationController == null)
            {
                CollaborationController = collaborationController;
            }
            Annotation = annotation;
            
            AnnotationEntryRoot = entryVE;
            IsCreator = isCreator;
            m_NameLabel = entryVE.Q<Text>(k_AnnotationAuthorNameLabel);
            m_ReplyLabel = entryVE.Q<Text>(k_ReplyLabel);
            m_ResolveIconButton = AnnotationEntryRoot.Q<IconButton>(k_ResolveIconButtonName);
            
            AnnotationEntryRoot.RegisterCallback<DetachFromPanelEvent>(OnRootDetachFromPanel);
#if UNITY_STANDALONE || UNITY_EDITOR
            AnnotationEntryRoot.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            AnnotationEntryRoot.RegisterCallback<PointerLeaveEvent>(OnPointerOut);
#endif
            _ = GetCreatorName();
            
            _ = CheckSubscription();
            
            var timeLabel = AnnotationEntryRoot.Q<Text>(k_AnnotationsTimeLabel);
            timeLabel.text = GetDateTimeLabel(DateTime.UtcNow - annotation.Created.ToUniversalTime());

            if (annotation.Updated != annotation.Created)
            {
                timeLabel.text = "Edited, " + timeLabel.text;
            }

            if (isOpeningThread)
            {
                //Reading the thread, hide the number of replies.
                m_ResolveIconButton.style.display = DisplayStyle.None;
                m_ReplyLabel.style.display = DisplayStyle.None;
                if (Annotation.TargetContext.TryGetValue(CollaborationController.k_AssetVersionNumberKey,
                        out var versionNumber))
                {
                    //If it is a reply, show the version number it was made on
                    timeLabel.text = timeLabel.text + " • Ver." + versionNumber;
                }
                
                //Edit mode
                m_EditContainer = AnnotationEntryRoot.Q<VisualElement>(k_EditContainerName);
                m_EditContainer.style.display = DisplayStyle.None;
                m_SaveEditButton = m_EditContainer.Q<Button>(k_SaveEditButtonName);
                m_AttachmentIconButton = m_EditContainer.Q<IconButton>(k_AttachmentIconButtonName);
                m_CancelEditButton = m_EditContainer.Q<Button>(k_CancelEditButtonName);
                m_EditTextArea = m_EditContainer.Q<TextArea>();
            
                m_CancelEditButton.clicked += OnCancelEditClicked;
                m_EditTextArea.RegisterValueChangingCallback(OnEditTextAreaValueChanging);
                m_EditTextArea.RegisterValueChangedCallback(OnEditTextAreaValueChanged);
                
                CollaborationUIUtility.SetupMentionComposer(m_EditTextArea, () => CollaborationUIController.SelectedAsset);

                m_SaveEditButton.clicked += OnSaveEditClicked;
                m_AttachmentGridView = m_EditContainer.Q<GridView>();
                m_AttachmentGridView.makeItem = CollaborationUIController.AttachmentGridViewItem;
                m_AttachmentGridView.bindItem = BindItem;
                m_AttachmentGridView.unbindItem = collaborationUIController.UnbindAttachmentItem;
                m_AttachmentGridView.columnCount = CollaborationUIController.AttachmentGridViewColumnCount;
                m_AttachmentIconButton.clicked += AttachmentIconButtonOnClicked;
#if UNITY_WEBGL && !UNITY_EDITOR
                m_AttachmentIconButton.SetEnabled(false);
#endif
                AnnotationEditModeEnter += OnAnnotationEditModeEnter;
                
                //Reactions
                m_ReactionContainer = AnnotationEntryRoot.Q<VisualElement>(k_ReactionContainerName);
                m_ReactionContainer.style.display = Annotation.Reactions.Count > 0 ? DisplayStyle.Flex: DisplayStyle.None;

                if (m_ReactionContainer.style.display == DisplayStyle.Flex)
                {
                    m_ReactionIconButton = m_ReactionContainer.Q<IconButton>(k_ReactionIconButtonName);
                }
                else
                {
                    m_ReactionIconButton = AnnotationEntryRoot.Q<IconButton>(k_ReactionIconButtonName);
                }
                m_ReactionIconButton.style.display = DisplayStyle.Flex;
                m_ReactionIconButton.clicked += OnReactionIconButtonClicked;
                foreach (var reactionDataKey in CollaborationUIUtility.ReactionData.Keys)
                {
                    var reactionCode = CollaborationUIUtility.ReactionData[reactionDataKey];
                    var reactionButton = m_ReactionContainer.Q<Button>(reactionDataKey);
                    if (Annotation.Reactions.Any(x => x.Code == reactionCode))
                    {
                        var reactionCount = Annotation.Reactions.First(x => x.Code == reactionCode).Count;
                        reactionButton.style.display = reactionCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                        reactionButton.title = reactionCount.ToString();
                        reactionButton.userData = reactionCode;
                        reactionButton.RegisterCallback<ClickEvent>(OnEmojiButtonClicked);
                    }
                    else
                    {
                        reactionButton.style.display = DisplayStyle.None;
                    }
                }
                
                //Attachment
                m_attachmentContainer = AnnotationEntryRoot.Q<VisualElement>(k_AttachmentContainerName);
                m_attachmentListContainer = AnnotationEntryRoot.Q<VisualElement>(k_AttachmentListContainerName);
                m_attachmentCountLabel = AnnotationEntryRoot.Q<Text>(k_AttachmentCountLabelName);
                if (Annotation.Attachments != null && Annotation.Attachments.Count > 0)
                {
                    m_attachmentContainer.style.display = DisplayStyle.Flex;

                    CollaborationUIBase.GetTranslation(m_attachmentCountLabel, collaborationUIController.LocalizedStringAsset
                        .AttachmentCountLocalizedString);

                    m_attachmentCountLabel.variables = new object[]
                    {
                        new Dictionary<string, object>()
                        {
                            { "attachment_count", Annotation.Attachments.Count }
                        }
                    };
                    
                    int index = 0;
                    foreach (var attachment in Annotation.Attachments)
                    {
                        var newAttachmentEntry = collaborationUIController.AttachmentMessageEntryTemplate.Instantiate()
                            .Children().First();
                        m_attachmentListContainer.Add(newAttachmentEntry);
                        var newAttachmentController = new AttachmentMessageEntryController(this, attachment, newAttachmentEntry);
                        newAttachmentEntry.userData = newAttachmentController;
                        if (index > 0)
                        {
                            newAttachmentEntry.style.marginTop = new Length(8f, LengthUnit.Pixel);
                        }
                        index++;
                    }
                }
            }
            else
            {
                m_ResolveIconButton.style.display = DisplayStyle.Flex;
                m_ReplyLabel.style.display = Annotation.ReplyCount.HasValue? DisplayStyle.Flex : DisplayStyle.None;
                if (Annotation.ReplyCount.HasValue)
                {
                    if (Annotation.ReplyCount.Value == 0)
                    {
                        CollaborationUIBase.GetTranslation(m_ReplyLabel, CollaborationUIController.LocalizedStringAsset.ReplyLocalizedString);
                        m_ReplyLabel.variables?.Clear();
                        m_ReplyLabel.variables = null;
                    }
                    else
                    {
                        CollaborationUIBase.GetTranslation(m_ReplyLabel, CollaborationUIController.LocalizedStringAsset.ReplyCountLocalizedString);
                        m_ReplyLabel.variables = new object[]
                        {
                            new Dictionary<string,object>()
                            {
                                { "reply_count", Annotation.ReplyCount.Value }
                            }
                        };
                    }
                }
                AnnotationEntryRoot.AddToClassList("cursor--pointer");
                AnnotationEntryRoot.RegisterCallback<ClickEvent>(OnCommentClicked);
                bool isResolved = Annotation.Resolved.HasValue;
                m_ResolveIconButton.icon = isResolved
                    ? CollaborationUIBase.k_ResolvedIconName
                    : CollaborationUIBase.k_UnResolvedIconName;
                if (isResolved)
                {
                    m_ResolveIconButton.AddToClassList(CollaborationUIBase.k_ResolvedClassNam);
                }
                m_ResolveIconButton.clicked += ResolveIconButtonOnClicked;
            }
            
            m_MessageLabel = AnnotationEntryRoot.Q<Text>(k_MessageLabel);

            if (annotation.MessageType == MessageType.User)
            {
                m_MessageLabel.text = CollaborationUIUtility.ParseUserTags(annotation.Text);
            } else if (annotation.MessageType == MessageType.Resolve)
            {
                CollaborationUIBase.GetTranslation(m_MessageLabel, CollaborationUIController.LocalizedStringAsset.HasResolvedLocalizedString);
            } else if (annotation.MessageType == MessageType.Unresolve)
            {
                CollaborationUIBase.GetTranslation(m_MessageLabel, CollaborationUIController.LocalizedStringAsset.HasReopenedLocalizedString);
            }

            m_MessageLabel.SetDisplay(!string.IsNullOrWhiteSpace(m_MessageLabel.text));

            m_MenuIconButton = AnnotationEntryRoot.Q<IconButton>(k_MenuIconButtonName);
            m_MenuIconButton.style.display = annotation.MessageType == MessageType.User? DisplayStyle.Flex: DisplayStyle.None;;
            m_MenuIconButton.clicked += OnMenuButtonClicked;
#if UNITY_STANDALONE || UNITY_EDITOR
            ShowOrHideMenu(false);
#else
            ShowOrHideMenu(true);
#endif
        }

        private void AttachmentIconButtonOnClicked()
        {
            OpenFilesPanel((filePaths) =>
            {
                if(filePaths == null || filePaths.Length == 0) return;

                foreach (var filePath in filePaths)
                {
                    bool repeated = CollaborationUIUtility.RepeatedAttachment(
                        m_AttachmentGridView,
                        Annotation,
                        filePath, out AddAttachmentFailType type);
                    
                    if (repeated)
                    {
                        CollaborationUIController.ShowRepeatedAttachmentMessage(type);
                        return;
                    }
                
                    var newAttachment = new Attachment(filePath);
                    m_AttachmentGridView.itemsSource ??= new List<Attachment>();
                    var existingList = m_AttachmentGridView.itemsSource as List<Attachment>;
                    existingList.Add(newAttachment);
                    m_AttachmentGridView.itemsSource = existingList;
                }
                
                _ = WaitForUIUpdate();
            });
            return;

            async Task WaitForUIUpdate()
            {
                await Task.Delay(500);
                CollaborationUIUtility.CheckValidInput(m_EditTextArea, m_AttachmentGridView, m_SaveEditButton);
            }
        }

        private void BindItem(VisualElement element, int index)
        {
            CollaborationUIController.BindAttachmentGridViewItem(m_AttachmentGridView, element, index);
        }

        private void OnPointerOut(PointerLeaveEvent evt)
        {
            if(m_PausingMenu) return;
            ShowOrHideMenu(false);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            ShowOrHideMenu(true);
        }

        private void ShowOrHideMenu(bool show)
        {
            if (CollaborationUIController.ReadingThread)
            {
                if (m_ReactionContainer.style.display == DisplayStyle.None)
                {
                    m_ReactionIconButton.style.display = show? DisplayStyle.Flex: DisplayStyle.None;
                }
            }
            else
            {
                bool isResolved = Annotation.Resolved.HasValue;
                if (!isResolved)
                {
                    m_ResolveIconButton.style.display = show? DisplayStyle.Flex: DisplayStyle.None;
                }
            }
            m_MenuIconButton.style.display = Annotation.MessageType == MessageType.User && show? DisplayStyle.Flex: DisplayStyle.None;;
        }

        private void ResolveIconButtonOnClicked()
        {
            if(NetworkDetector.IsOffline) return;
            bool isResolved = Annotation.Resolved.HasValue;
            CollaborationController.ResolveOrOpenThread?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource, Annotation, !isResolved, (newAnnotation) =>
            {
                CollaborationUIController.QueryThread(CollaborationUIController.FilterType);
            });
        }

        public async Task CheckSubscription()
        {
            if (!CollaborationUIController.ReadingThread)
            {
                IsSubscribed = await CollaborationController.IsUserFollowing(CollaborationUIController.SelectedAsset.Value, Annotation, new CancellationTokenSource());
            }
            else
            {
                IsSubscribed = CollaborationUIController.isCurrentThreadSubscribed;
            }
        }

        private void OnReactionIconButtonClicked()
        {
            m_Popover?.Dismiss();
            var reactionMenu = CollaborationUIController.ReactionUITemplate.Instantiate().Children().First();
            m_Popover = Popover.Build(m_ReactionIconButton, reactionMenu)
                .SetPlacement(PopoverPlacement.Bottom).SetArrowVisible(false);

            foreach (var reactionDataKey in CollaborationUIUtility.ReactionData.Keys)
            {
                var iconButton = reactionMenu.Q<IconButton>(reactionDataKey);
                iconButton.userData = CollaborationUIUtility.ReactionData[reactionDataKey];
                iconButton.RegisterCallback<ClickEvent>(OnEmojiButtonClicked);
            }
            
            (m_Popover as Popover).dismissed += OnReactionMenuDismissed;
            
            m_Popover.Show();
            return;
            
            void OnReactionMenuDismissed(Popover popover, DismissType arg2)
            {
                popover.dismissed -= OnReactionMenuDismissed;
                foreach (var reactionDataKey in CollaborationUIUtility.ReactionData.Keys)
                {
                    var iconButton = popover.contentView.Q<IconButton>(reactionDataKey);
                    iconButton.UnregisterCallback<ClickEvent>(OnEmojiButtonClicked);
                }
                m_Popover = null;
            }
        }
        
        private void OnEmojiButtonClicked(ClickEvent evt)
        {
            m_Popover?.Dismiss();
            if(NetworkDetector.IsOffline) return;
            var iconButton = evt.currentTarget as VisualElement;
            LoadingUIPanel.ShowLoadingPanel(() =>
            {
                if(string.IsNullOrEmpty(iconButton.userData.ToString())) return;
                if(string.IsNullOrEmpty(iconButton.userData.ToString())) return;
                var reaction = iconButton.userData.ToString();
                bool hasReaction = Annotation.Reactions.Any(x => x.Code == reaction && x.UserIds.Contains(CollaborationUIController.UserInfo.UserId.ToString()));
                CollaborationController.AddReactionToAnnotation?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource, Annotation, reaction, !hasReaction, () =>
                {
                    bool isRoot = Annotation.RootAnnotationId == null;
                    CollaborationController.LoadUpdatedAnnotation?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource,
                        isRoot? Annotation.AnnotationId: Annotation.RootAnnotationId.Value, LoadCallback);
                });
            });
            return;
                
            void LoadCallback(IAnnotation annotation)
            {
                CollaborationUIController.AnnotationHasBeenUpdated?.Invoke(annotation);
                CollaborationUIController.OpenRootThread(annotation);
            }
        }

        private void OnAnnotationEditModeEnter(IAnnotation editingAnnotation)
        {
            if(m_EditContainer.style.display == DisplayStyle.None) return;
            if (Annotation == editingAnnotation) return;
            OnCancelEditClicked();
        }

        private void OnSaveEditClicked()
        {
            if(NetworkDetector.IsOffline) return;
            var existingList = m_AttachmentGridView.itemsSource as List<Attachment>;
            CollaborationController.UpdateAnnotation?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource, Annotation,
                CollaborationUIUtility.GetCloudText(m_EditTextArea), existingList, Callback);
            return;

            void Callback(IAnnotation resultAnnotation)
            {
                bool isRoot = resultAnnotation.RootAnnotationId == null;
                if (isRoot)
                {
                    CollaborationController.LoadUpdatedAnnotation.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource,
                        resultAnnotation.AnnotationId, (newAnnotation) =>
                    {
                        CollaborationUIController.AnnotationHasBeenUpdated?.Invoke(newAnnotation);
                        CollaborationUIController.OpenRootThread(newAnnotation);
                    });
                }
                else
                {
                    CollaborationUIController.OpenRootThread(CollaborationUIController.CurrentAnnotation);
                }
            }
        }

        private void OnEditTextAreaValueChanged(ChangeEvent<string> evt)
        {
            CollaborationUIUtility.CheckValidInput(m_EditTextArea, m_AttachmentGridView, m_SaveEditButton);
        }

        private void OnEditTextAreaValueChanging(ChangingEvent<string> evt)
        {
            if (CollaborationUIController.SelectedAsset.HasValue)
            {
                CollaborationUIUtility.OnTextAreaValueChanging(CollaborationUIController.SelectedAsset.Value, evt);
            }
            CollaborationUIUtility.CheckValidInput(m_EditTextArea, m_AttachmentGridView, m_SaveEditButton);
        }

        private void OnCancelEditClicked()
        {
            m_MessageLabel.SetDisplay(!string.IsNullOrWhiteSpace(m_MessageLabel.text));
            m_EditContainer.DisplayOff();
            CollaborationUIUtility.ClearMentions(m_EditTextArea);
            m_AttachmentGridView.itemsSource = null;
            m_AttachmentGridView.style.display = DisplayStyle.None;
        }

        private void OnMenuButtonClicked()
        {
            m_Popover?.Dismiss();

            if (CollaborationUIController.CurrentAnnotation == null)
            {
                m_PausingMenu = true;
                CollaborationUIController.ThreadMenuOpen(m_MenuIconButton, Annotation, IsSubscribed, out var menuBuilder);
                m_Popover = menuBuilder;
                menuBuilder.dismissed += OnMenuDismissed;

                return;
            }

            m_PausingMenu = true;
            m_Popover = MenuBuilder.Build(m_MenuIconButton)
                .AddAction(99, AddAttachment)
                .AddAction(98, CopyLink)
                .AddAction(97, Edit)
                .AddAction(96, Delete)
                .Pop();

            (m_Popover as MenuBuilder).dismissed += OnMenuDismissed;
            m_Popover.Show();

            async void AddAttachment(MenuItem item)
            {
                item.label = await CollaborationUIController.LocalizedStringAsset.AddAttachmentLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
#if UNITY_WEBGL && !UNITY_EDITOR
                item.SetEnabled(false);
#else
                item.SetEnabled(IsCreator);
#endif
                item.clickable.clicked += OnAddAttachmentClicked;
            }
            
            async void CopyLink(MenuItem item)
            {
                item.label = await CollaborationUIController.LocalizedStringAsset.CopyLinkLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.clickable.clicked += OnCopyLinkClicked;
            }
            
            async void Edit(MenuItem item)
            {
                item.label = await CollaborationUIController.LocalizedStringAsset.EditLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.SetEnabled(IsCreator);
                item.clickable.clicked += OnEditorButtonClicked;
            }
            
            async void Delete(MenuItem item)
            {
                item.label = await CollaborationUIController.LocalizedStringAsset.DeleteLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.selectable = false;
                item.SetEnabled(IsCreator);
                item.clickable.clicked += OnDeleteCommentClicked;
            }
            
            void OnMenuDismissed(MenuBuilder arg1, DismissType arg2)
            {
                arg1.dismissed -= OnMenuDismissed;
                m_PausingMenu = false;
#if UNITY_STANDALONE || UNITY_EDITOR
                if (arg2 == DismissType.OutOfBounds)
                {
                    ShowOrHideMenu(false);
                }
#else
                ShowOrHideMenu(true);
#endif
            }
        }

        private void OnAddAttachmentClicked()
        {
            m_Popover?.Dismiss();
            OpenFilesPanel((filePaths) =>
            {
                if(NetworkDetector.IsOffline) return;
                if(filePaths == null || filePaths.Length == 0) return;
                
                LoadingUIPanel.ShowLoadingPanel(() =>
                {
                    foreach (var filePath in filePaths)
                    {
                        bool repeated = CollaborationUIUtility.RepeatedAttachment(
                            m_AttachmentGridView,
                            Annotation,
                            filePath, out var type);
                        if (repeated)
                        {
                            CollaborationUIController.ShowRepeatedAttachmentMessage(type);
                            return;
                        }
                        var newAttachment = new Attachment(filePath);
                        CollaborationController.AddAttachmentToAnnotation?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource,
                            Annotation, newAttachment, Callback);
                    }
                });
            });
            return;
            
            void Callback(bool success, IAnnotation arg2)
            {
                LoadingUIPanel.HideLoadingPanel(null);
                if(!success) return;
                bool isRoot = arg2.RootAnnotationId == null;
                if (isRoot)
                {
                    CollaborationController.LoadUpdatedAnnotation.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource,
                        arg2.AnnotationId, (newAnnotation) =>
                    {
                        CollaborationUIController.AnnotationHasBeenUpdated?.Invoke(newAnnotation);
                        CollaborationUIController.OpenRootThread(newAnnotation);
                    });
                }
                else
                {
                    CollaborationUIController.OpenRootThread(CollaborationUIController.CurrentAnnotation);
                }
            }
        }

        private async void OpenFilesPanel(Action<string[]> callback)
        {
            SharedUIManager.Instance.AssetsContainer.SetEnabled(false);

            var operation = CollaborationUIController.LocalizedStringAsset.SelectAttachmentLocalizedString
                .GetLocalizedStringAsync();

            await operation.Task;

            if (operation.Status == AsyncOperationStatus.Succeeded)
            {
                FileBrowser.OpenFiles(operation.Result, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "", OpenFilesCallback);
            }
            else
            {
                Debug.LogError("Failed to load localized string for file dialog title.");
            }

            void OpenFilesCallback(string[] filePath)
            {
                _ = WaitForAddAttachment();
                callback?.Invoke(filePath);
            }
            return;
            
            async Task WaitForAddAttachment()
            {
                await Task.Delay(500);
                SharedUIManager.Instance.AssetsContainer.SetEnabled(true);
            }
        }

        private void OnEditorButtonClicked()
        {
            m_Popover?.Dismiss();
            m_MessageLabel.style.display = DisplayStyle.None;
            m_EditContainer.style.display = DisplayStyle.Flex;
            m_EditContainer.SetEnabled(true);
            // Stored mentions (:user[name]{#id}) become plain "@Name" text backed by spans,
            // so they can be edited like fresh ones and are re-converted at save time.
            CollaborationUIUtility.LoadCloudTextIntoComposer(m_EditTextArea, Annotation.Text);
            CollaborationUIUtility.CheckValidInput(m_EditTextArea, m_AttachmentGridView, m_SaveEditButton);
            m_EditTextArea.Focus();
            AnnotationEditModeEnter?.Invoke(Annotation);
        }

        private void OnDeleteCommentClicked()
        {
            m_Popover?.Dismiss();
            if(NetworkDetector.IsOffline) return;
            LoadingUIPanel.ShowLoadingPanel?.Invoke(() =>
            {
                CollaborationController.DeleteAnnotation?.Invoke(CollaborationUIController.SelectedAsset.Value, CollaborationUIController.TokenSource,
                    Annotation, DeleteCompleted);
            });
            return;

            void DeleteCompleted(bool success)
            {
                LoadingUIPanel.HideLoadingPanel?.Invoke(() =>
                {
                    if(!success) return;
                    if (Annotation.RootAnnotationId == null)
                    {
                        CollaborationUIController.RootAnnotationDeleted?.Invoke(Annotation);
                        CollaborationUIController.BackToAllThreadsButtonOnClicked();
                    }
                    else
                    {
                        CollaborationUIController.OpenRootThread(CollaborationUIController.CurrentAnnotation);
                    }
                });
            }
        }

        private void OnCopyLinkClicked()
        {
            m_Popover?.Dismiss();
            if(!CollaborationUIController.SelectedAsset.HasValue) return;
            bool isRoot = Annotation.RootAnnotationId == null;
            var orgId = CollaborationUIController.SelectedAsset.Value.Asset.Descriptor.OrganizationId;
            var projectId = CollaborationUIController.SelectedAsset.Value.Asset.Descriptor.ProjectId;
            var assetId = CollaborationUIController.SelectedAsset.Value.Asset.Descriptor.AssetId;
            var versionId = CollaborationUIController.SelectedAsset.Value.Asset.Descriptor.AssetVersion;
            var threadId = !isRoot ? Annotation.RootAnnotationId.Value : Annotation.AnnotationId;
            var link = string.Format(CollaborationUIBase.THREAD_URL_TEMPLATE, orgId, projectId, assetId, versionId, threadId);
            if (!isRoot)
            {
                link += $"&annotationId={Annotation.AnnotationId}";
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(link);
#else
            GUIUtility.systemCopyBuffer = link;
#endif
        }

        private void OnCommentClicked(ClickEvent evt)
        {
            if(CollaborationUIUtility.JustDismissedPopover) return;
            if (evt.target != AnnotationEntryRoot) return;
            var clickedElement = evt.currentTarget as VisualElement;
            if (clickedElement?.userData != this) return;
            if(NetworkDetector.IsOffline) return;
            //Open thread
            CollaborationUIController.OpenRootThread(Annotation);
            AnnotationCameraFocus.FocusToAnnotationCamera(Annotation);
        }

        private async Task GetCreatorName()
        {
            var returnedName = await CollaborationUIUtility.GetMemberName(CollaborationUIController.SelectedAsset.Value.Asset.Descriptor.OrganizationId, Annotation.CreatedBy);

            if (returnedName == string.Empty)
            {
                var operation = CollaborationUIController.LocalizedStringAsset.UnknownUserLocalizedString.GetLocalizedStringAsync();
                await operation.Task;
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    m_NameLabel.text = operation.Result;
                } else
                {
                    m_NameLabel.text = "Unknown User";
                    Debug.Log("Failed to load localized string for Unknown User.");
                }
            }
            else
            {
                m_NameLabel.text = returnedName;
            }
            
            var avatar = AnnotationEntryRoot.Q<Avatar>();
            avatar.backgroundColor = new Optional<Color>(CollaborationUIUtility.GetRandomBackgroundColorAsUnityColor(m_NameLabel.text));
            var initials = avatar.Q<Text>();
            initials.text = CollaborationUIUtility.ReturnInitials(m_NameLabel.text);
        }
        
        private void OnRootDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (m_CancelEditButton != null)
            {
                m_CancelEditButton.clicked -= OnCancelEditClicked;
            }
            
            if (m_MenuIconButton != null)
            {
                m_MenuIconButton.clicked -= OnMenuButtonClicked;
            }
            
            if (m_SaveEditButton != null)
            {
                m_SaveEditButton.clicked -= OnSaveEditClicked;
            }
            
            if (m_ReactionIconButton != null)
            {
                m_ReactionIconButton.clicked -= OnReactionIconButtonClicked;
            }

            if (m_ResolveIconButton != null)
            {
                m_ResolveIconButton.clicked -= ResolveIconButtonOnClicked;
            }

            if (m_AttachmentIconButton != null)
            {
                m_AttachmentIconButton.clicked -= AttachmentIconButtonOnClicked;
            }
            
            if (m_ReactionContainer != null)
            {
                var allReactionButtons = m_ReactionContainer.Query<Button>().ToList();
                foreach (var button in allReactionButtons)
                {
                    button.UnregisterCallback<ClickEvent>(OnEmojiButtonClicked);
                }
            }
            
            AnnotationEditModeEnter -= OnAnnotationEditModeEnter;
            AnnotationEntryRoot.UnregisterCallback<DetachFromPanelEvent>(OnRootDetachFromPanel);
            AnnotationEntryRoot.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            AnnotationEntryRoot.UnregisterCallback<PointerLeaveEvent>(OnPointerOut);
            m_EditTextArea?.UnregisterValueChangingCallback(OnEditTextAreaValueChanging);
            m_EditTextArea?.UnregisterValueChangedCallback(OnEditTextAreaValueChanged);

            AnnotationEntryRoot.UnregisterCallback<ClickEvent>(OnCommentClicked);
            m_Popover?.Dismiss();
        }

        private static string GetDateTimeLabel(TimeSpan timeDiff)
        {
            int secondsInMinute = 60;
            int minutesInHour = 60;
            int hoursInDay = 24;
            int daysInWeek = 7;
            int daysInYear = 365;

            string value;

            if (timeDiff.TotalDays > daysInYear) value = $"{(int)Math.Floor(timeDiff.TotalDays / daysInYear)} years";
            else if (timeDiff.TotalDays > daysInWeek) value = $"{(int)Math.Floor(timeDiff.TotalDays / daysInWeek)} weeks";
            else if (timeDiff.TotalHours > hoursInDay) value = $"{(int)Math.Floor(timeDiff.TotalHours / hoursInDay)} days";
            else if (timeDiff.TotalMinutes > minutesInHour) value = $"{(int)Math.Floor(timeDiff.TotalMinutes / minutesInHour)} hours";
            else if (timeDiff.TotalSeconds > secondsInMinute) value = $"{(int)Math.Floor(timeDiff.TotalSeconds / secondsInMinute)} minutes";
            else value = $"{(int)timeDiff.TotalSeconds} seconds";

            string result = $"Created {value} ago";
            //result += edited ? " (Edited)" : string.Empty;

            return result;
        }
    }
}
