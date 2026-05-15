using System.IO;
using System.Threading;
using Unity.Cloud.Collaboration;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using UnityEngine;
using System.Threading.Tasks;
using Unity.AppUI.Core;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using UnityEngine.Networking;
using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Industry.Viewer.Collaboration
{
    public class AttachmentMessageEntryController
    {
        private const string k_AttachmentPreviewName = "AttachmentPreview";
        private const string k_AttachmentNameLabelName = "AttachmentNameLabel";
        private const string k_AttachmentMoreIconButtonName = "AttachmentMoreIconButton";
        
        private readonly AnnotationEntryController _annotationEntryController;
        private static MenuBuilder _attachmentOptionsPopover;
        private readonly IAttachment _attachment;
        
        private readonly VisualElement _attachmentPreview;
        private readonly VisualElement _root;
        private readonly IconButton _attachmentMoreIconButton;

        public AttachmentMessageEntryController(AnnotationEntryController annotationEntryController,
            IAttachment attachment, VisualElement entryRoot)
        {
            _annotationEntryController = annotationEntryController;
            _attachment = attachment;
            _root = entryRoot;
            
            _attachmentPreview = entryRoot.Q<VisualElement>(k_AttachmentPreviewName);
            var attachmentNameLabel = entryRoot.Q<Text>(k_AttachmentNameLabelName);

            if (attachment is IFileAttachment fileAttachment)
            {
                attachmentNameLabel.text = Path.GetFileNameWithoutExtension(fileAttachment.FilePath);
                var canPreview = CollaborationUIUtility.IsSupportedImageFormat(fileAttachment.FilePath);
                if (canPreview)
                {
                    _ = GetThumbnailAsync(fileAttachment.FilePath);
                }
                else
                {
                    _attachmentPreview.style.backgroundImage = new StyleBackground(_annotationEntryController.CollaborationUIController.DefaultFileIcon);
                }
            } else if (attachment is ISpatial3DAttachment spatial3DAttachment)
            {
                CollaborationUIBase.GetTranslation(attachmentNameLabel, _annotationEntryController.CollaborationUIController.LocalizedStringAsset.SpatialAttachmentTitleLocalizedString);
            } else if(attachment is ISketchAttachment sketchAttachment)
            {
                _ = GetThumbnailAsync(sketchAttachment.Preview.FilePath);
                attachmentNameLabel.text = char.ToUpper(attachment.Type[0]) + attachment.Type.Substring(1) + $" {entryRoot.parent.childCount}";
            }
            else
            {
                CollaborationUIBase.GetTranslation(attachmentNameLabel, _annotationEntryController.CollaborationUIController.LocalizedStringAsset.UnknownAttachmentTitleLocalizedString);
            }
            
            _attachmentMoreIconButton = entryRoot.Q<IconButton>(k_AttachmentMoreIconButtonName);
            
            _root.RegisterCallback<DetachFromPanelEvent>(OnRemovedFromPanel);
            _attachmentMoreIconButton.clicked += OnMoreOptionButtonClicked;
        }

        private void OnMoreOptionButtonClicked()
        {
            _attachmentOptionsPopover?.Dismiss();

            _attachmentOptionsPopover = MenuBuilder.Build(_attachmentMoreIconButton)
                .AddAction(9, Download)
                .AddAction(8, Delete)
                .Pop();
            
            _attachmentOptionsPopover.dismissed += AttachmentOptionsPopoverOnDismissed;
            _attachmentOptionsPopover.Show();
            return;

            async void Download(MenuItem item)
            {
                item.label = await _annotationEntryController.CollaborationUIController.LocalizedStringAsset
                    .DownloadLocalizedString.GetTitleLocalizedStringForAppUIAsync();
#if UNITY_WEBGL && !UNITY_EDITOR
                item.SetEnabled(false);
#else
                item.SetEnabled(_attachment is IFileAttachment);
#endif
                item.clickable.clicked += OnDownloadButtonPress;
            }
            
            async void Delete(MenuItem item)
            {
                item.label = await _annotationEntryController.CollaborationUIController.LocalizedStringAsset
                    .DeleteLocalizedString.GetTitleLocalizedStringForAppUIAsync();
                item.SetEnabled(_annotationEntryController.IsCreator);
                item.clickable.clicked += OnDeleteButtonPress;
            }
        }

        private void AttachmentOptionsPopoverOnDismissed(MenuBuilder arg1, DismissType arg2)
        {
            _attachmentOptionsPopover.dismissed -= AttachmentOptionsPopoverOnDismissed;
            _attachmentOptionsPopover = null;
        }

        private async void OnDeleteButtonPress()
        {
            _attachmentOptionsPopover?.Dismiss();
            var alert = new AlertDialog()
            {
                title = await _annotationEntryController.CollaborationUIController.LocalizedStringAsset.DeleteAttachmentTitleLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                description = await _annotationEntryController.CollaborationUIController.LocalizedStringAsset.DeleteAttachmentMessageLocalizedString.GetTitleLocalizedStringForAppUIAsync(),
                variant = AlertSemantic.Destructive
            };
            alert.SetPrimaryAction(99, await _annotationEntryController.CollaborationUIController.LocalizedStringAsset.DeleteLocalizedString.GetTitleLocalizedStringForAppUIAsync(), () =>
            {
                // Delete attachment
                if(NetworkDetector.IsOffline) return;
                CollaborationController.DeleteAttachment?.Invoke(
                    _annotationEntryController.CollaborationUIController.SelectedAsset.Value, _annotationEntryController.CollaborationUIController.TokenSource,
                    _annotationEntryController.Annotation, _attachment, Callback);
            });
            alert.SetCancelAction(0, await _annotationEntryController.CollaborationUIController.LocalizedStringAsset.CancelLocalizedString.GetTitleLocalizedStringForAppUIAsync());

            var modal = Modal.Build(_attachmentMoreIconButton, alert);
            modal.Show();
            
            return;
            
            void Callback(bool success, IAnnotation annotation)
            {
                if(!success) return;
                bool isRoot = string.IsNullOrEmpty(annotation.RootAnnotationId.ToString());
                if (_attachment is ISpatial3DAttachment)
                {
                    _annotationEntryController.CollaborationUIController.DeleteSpatialAttachment(annotation, _attachment);
                }
                if (isRoot)
                {
                    CollaborationController.LoadUpdatedAnnotation.Invoke(_annotationEntryController.CollaborationUIController.SelectedAsset.Value, _annotationEntryController.CollaborationUIController.TokenSource,
                        annotation.AnnotationId, (newAnnotation) =>
                    {
                        _annotationEntryController.CollaborationUIController.AnnotationHasBeenUpdated?.Invoke(newAnnotation);
                        _annotationEntryController.CollaborationUIController.OpenRootThread(newAnnotation);
                    });
                }
                else
                {
                    _annotationEntryController.CollaborationUIController.OpenRootThread(_annotationEntryController.CollaborationUIController.CurrentAnnotation);
                }
            }
        }

        private async void OnDownloadButtonPress()
        {
            _attachmentOptionsPopover?.Dismiss();
            
            if (_attachment is IFileAttachment fileAttachment)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileAttachment.FilePath);
                var fileExtension = Path.GetExtension(fileAttachment.FilePath).Replace(".", "");
                
#if UNITY_EDITOR || UNITY_STANDALONE// || UNITY_WEBGL
                var operation = _annotationEntryController.CollaborationUIController.LocalizedStringAsset.SaveAttachmentLocalizedString
                    .GetLocalizedStringAsync();

                await operation.Task;

                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    FileBrowser.SaveFile(operation.Result, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileNameWithoutExtension, fileExtension, SaveFileCallback);
                }
                else
                {
                    Debug.LogError("Failed to get localized string for Save Attachment.");
                }
                
                void SaveFileCallback(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        LoadingUIPanel.HideLoadingPanel(null);
                        return;
                    }
                    _ = SaveFileTo(filePath);
                }
                return;
#endif
                
#if UNITY_IOS || UNITY_ANDROID
                var destinationFilePath = Path.Combine(Application.temporaryCachePath, fileAttachment.FilePath);
                _ = SaveFileTo(destinationFilePath, () =>
                {
                    FileBrowser.ExportFile(destinationFilePath, (success) =>
                    {
                        File.Delete(destinationFilePath);
                        LoadingUIPanel.HideLoadingPanel(null);
                    });
                });
                return;
#endif
            }
            return;

            async Task SaveFileTo(string path, Action callback = null)
            {
                LoadingUIPanel.ShowLoadingPanel(null);
                var downloadUrl = await GetDownloadUrlAsync((_attachment as IFileAttachment).FilePath);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    LoadingUIPanel.HideLoadingPanel(null);
                    return;
                }
                var bytes = await PlatformServices.DownloadContentAsync(new Uri(downloadUrl), CancellationToken.None);
                await File.WriteAllBytesAsync(path, bytes);
                if (callback == null)
                {
                    LoadingUIPanel.HideLoadingPanel(null);
                    return;
                }
                callback.Invoke();
            }
        }

        private void OnRemovedFromPanel(DetachFromPanelEvent evt)
        {
            _root.UnregisterCallback<DetachFromPanelEvent>(OnRemovedFromPanel);
            _attachmentMoreIconButton.clicked -= OnMoreOptionButtonClicked;
            _attachmentPreview.style.backgroundImage = null;
        }

        private async Task GetThumbnailAsync(string fileName)
        {
            try
            {
                var thumbnailUrl = await AnnotationEntryController.CollaborationController.ReturnAttachmentDownloadUrl(
                    _annotationEntryController.CollaborationUIController.SelectedAsset.Value,
                    _annotationEntryController.Annotation,
                    _attachment,
                    fileName,
                    new CancellationTokenSource());
                if (string.IsNullOrEmpty(thumbnailUrl)) return;

                using var uwr = UnityWebRequestTexture.GetTexture(thumbnailUrl);
                var operation = uwr.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load texture: {uwr.error}");
                    return;
                }

                var texture = DownloadHandlerTexture.GetContent(uwr);
                _attachmentPreview.style.backgroundImage = new StyleBackground(texture);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        
        private async Task<string> GetDownloadUrlAsync(string fileName)
        {
            var downloadUrl = await AnnotationEntryController.CollaborationController.ReturnAttachmentDownloadUrl(
                _annotationEntryController.CollaborationUIController.SelectedAsset.Value,
                _annotationEntryController.Annotation,
                _attachment,
                fileName, new CancellationTokenSource());
            return downloadUrl;
        }
    }
}
