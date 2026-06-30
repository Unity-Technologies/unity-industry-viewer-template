using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;
using Unity.Cloud.Collaboration;
using Unity.Industry.Viewer.Assets;
using Unity.Industry.Viewer.Shared;
using Unity.Cloud.Common;
using System.Threading.Tasks;
using Unity.Industry.Viewer.Streaming;

namespace Unity.Industry.Viewer.Collaboration
{
    public class CollaborationController : MonoBehaviour
    {
        public enum FilterType
        {
            All,
            Opened
        }

        public static CancellationTokenSource GlobalCancellationTokenSource;
        public static Action CancelRequestAction;
        public static Action<AssetInfo, CancellationTokenSource, FilterType, Action<IReadOnlyList<IAnnotation>>> QueryThreads;
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, Action<bool, IReadOnlyList<IAnnotation>>> OpenThread;
        public static Action<AssetInfo, CancellationTokenSource, AnnotationId, Action<IAnnotation>> LoadUpdatedAnnotation;
        public static Action<AssetInfo, CancellationTokenSource, string, string, List<Attachment>, GameObject, Action<bool, IAnnotation>> NewAnnotation;
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, bool, Action<IAnnotation>> ResolveOrOpenThread; // bool is true for resolve, false for open thread
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, bool, Action<bool, IAnnotation>> FollowOrUnfollowThread; // bool is true for follow, false for unfollow
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, Action<bool>> DeleteAnnotation;
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, string, List<Attachment>, Action<IAnnotation>> UpdateAnnotation;
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, string, bool, Action> AddReactionToAnnotation; // bool is true for add, false for remove
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, IAttachment, Action<bool, IAnnotation>> DeleteAttachment;
        public static Action<AssetInfo, CancellationTokenSource, IAnnotation, Attachment, Action<bool, IAnnotation>> AddAttachmentToAnnotation;

        public const string k_AssetVersionIdKey = "assetVersion";
        public const string k_AssetVersionNumberKey = "versionNumber";

        private static AnnotationManagement m_AnnotationManagement => PlatformServices.AnnotationManagement;

        private void Start()
        {
            QueryThreads += OnAssetsCollaborationStarted;
            NewAnnotation += OnCreateNewAnnotation;
            OpenThread += OnOpenThread;
            ResolveOrOpenThread += OnResolveOrOpenThread;
            FollowOrUnfollowThread += OnFollowOrUnfollowThread;
            DeleteAnnotation += OnDeleteAnnotation;
            UpdateAnnotation += OnUpdateAnnotation;
            AddReactionToAnnotation += OnAddReactionToAnnotation;
            LoadUpdatedAnnotation += OnLoadUpdatedAnnotation;
            DeleteAttachment += OnDeleteAttachment;
            AddAttachmentToAnnotation += OnAddAttachmentToAnnotation;
            CancelRequestAction += OnCancelRequest;
        }

        private void OnDestroy()
        {
            QueryThreads -= OnAssetsCollaborationStarted;
            NewAnnotation -= OnCreateNewAnnotation;
            OpenThread -= OnOpenThread;
            ResolveOrOpenThread -= OnResolveOrOpenThread;
            FollowOrUnfollowThread -= OnFollowOrUnfollowThread;
            DeleteAnnotation -= OnDeleteAnnotation;
            UpdateAnnotation -= OnUpdateAnnotation;
            AddReactionToAnnotation -= OnAddReactionToAnnotation;
            LoadUpdatedAnnotation -= OnLoadUpdatedAnnotation;
            DeleteAttachment -= OnDeleteAttachment;
            AddAttachmentToAnnotation -= OnAddAttachmentToAnnotation;
            CancelRequestAction -= OnCancelRequest;
        }

        private void OnCancelRequest()
        {
            GlobalCancellationTokenSource?.Cancel();
        }

        private void OnAddAttachmentToAnnotation(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation annotation, Attachment attachment, Action<bool, IAnnotation> callback)
        {
            _ = AddAction();
            return;

            async Task AddAction()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var success = await UploadAttachment(assetInfo, token, annotation.AnnotationId, attachment);
                callback?.Invoke(success, annotation);
            }
        }

        private static async Task<bool> UploadSpatialAttachment(AssetInfo assetInfo, CancellationTokenSource token,
            AnnotationId annotationId, GameObject spatialAttachment)
        {
            SpatialPosition position = new SpatialPosition(spatialAttachment.transform.localPosition.x,
                spatialAttachment.transform.localPosition.y, spatialAttachment.transform.localPosition.z);

            SpatialPosition cameraPosition = new SpatialPosition(Camera.main.transform.position.x,
                Camera.main.transform.position.y, Camera.main.transform.position.z);

            SpatialRotation cameraRotation = new SpatialRotation(Camera.main.transform.rotation.eulerAngles.x,
                Camera.main.transform.rotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.z);

            ICameraDetails newCameraDetails = new CameraDetails(
                position:  cameraPosition,
                rotation: cameraRotation,
                fieldOfView: Camera.main.fieldOfView
            );

            var newSpatialAttachmentRequest = new CreateSpatial3DAttachmentRequest(
                label: annotationId.ToString(),
                position: position,
                camera: newCameraDetails
            );

            var done = false;
            GlobalCancellationTokenSource = token;
            var info = RetrieveInfo(assetInfo);
            try
            {
                await m_AnnotationManagement.CreateAnnotationAttachmentAsync(
                    new AnnotationReference(info.ProjectId, annotationId),
                    newSpatialAttachmentRequest,
                    token.Token);

                token.Token.ThrowIfCancellationRequested();

                done = true;
            }
            catch (OperationCanceledException oe)
            {
                Debug.Log("Upload attachment cancelled: " + (oe.Message ?? oe.ToString()));
            }
            catch (Exception e)
            {
                Debug.Log("Failed to upload attachment: " + (e.Message ?? e.ToString()));
            }
            return done;
        }

        private static async Task<bool> UploadAttachment(AssetInfo assetInfo, CancellationTokenSource token, AnnotationId annotationId, Attachment attachment)
        {
            var newAttachmentRequest = new CreateFileAttachmentRequest(
                filePath: attachment.FileName,
                fileSize: attachment.FileSize,
                fileType: attachment.FileType,
                contentType: attachment.ContentType
            );
            bool done = false;
            var info = RetrieveInfo(assetInfo);
            GlobalCancellationTokenSource = token;
            try
            {
                var newAttachmentRequestAsync = await m_AnnotationManagement.CreateAnnotationAttachmentAsync(
                    new AnnotationReference(info.ProjectId, annotationId),
                    newAttachmentRequest,
                    token.Token);

                token.Token.ThrowIfCancellationRequested();

                var attachmentId = newAttachmentRequestAsync.AttachmentId;
                var attachmentUploadUrl = newAttachmentRequestAsync.UploadUrl;

                // Only proceed if upload succeeds
                await PlatformServices.UploadContentAsync(new Uri(attachmentUploadUrl), attachment.FilePath, token.Token);

                token.Token.ThrowIfCancellationRequested();

                await PlatformServices.AnnotationManagement.FinalizeAnnotationAttachmentAsync(
                    new AttachmentReference(info.ProjectId, annotationId, attachmentId),
                    attachment.FileName,
                    token.Token);

                token.Token.ThrowIfCancellationRequested();

                done = true;
            }
            catch (OperationCanceledException oe)
            {
                Debug.Log("Upload attachment cancelled: " + (oe.Message ?? oe.ToString()));
            }
            catch (Exception e)
            {
                Debug.Log("Failed to upload attachment: " + (e.Message ?? e.ToString()));
            }
            return done;
        }

        private void OnDeleteAttachment(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation arg1, IAttachment arg2, Action<bool, IAnnotation> arg3)
        {
            _ = DeleteAction();
            return;

            async Task DeleteAction()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);
                var success = false;
                try
                {
                    await m_AnnotationManagement.DeleteAttachmentAsync(
                        new AttachmentReference(info.ProjectId, arg1.AnnotationId, arg2.AttachmentId),
                        token.Token);
                    success = true;
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    arg3?.Invoke(success, arg1);
                }
            }
        }

        private void OnAddReactionToAnnotation(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation annotation, string code, bool add, Action callback)
        {
            _ = AddReaction();
            return;

            async Task AddReaction()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);
                try
                {
                    if (add)
                    {
                        await m_AnnotationManagement.CreateAnnotationReactionAsync(
                            new AnnotationReference(info.ProjectId, annotation.AnnotationId),
                            code, token.Token);
                    } else {
                        await m_AnnotationManagement.DeleteAnnotationReactionAsync(
                            new AnnotationReference(info.ProjectId, annotation.AnnotationId),
                            code, token.Token);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    callback?.Invoke();
                }
            }
        }

        private void OnLoadUpdatedAnnotation(AssetInfo assetInfo, CancellationTokenSource token, AnnotationId annotationId, Action<IAnnotation> callback)
        {
            _ = LoadUpdated();
            return;

            async Task LoadUpdated()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);
                IAnnotation newAnnotation = null;
                try
                {
                    newAnnotation = await m_AnnotationManagement.ReadAnnotationAsync(
                        new AnnotationReference(info.ProjectId, annotationId),
                        token.Token);
                } catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    callback?.Invoke(newAnnotation);
                }
            }
        }

        private void OnUpdateAnnotation(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation annotationToUpdate, string text, List<Attachment> attachments, Action<IAnnotation> callback)
        {
            _ = UpdateAction();
            return;

            async Task UpdateAction()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);
                try
                {
                    if (!string.Equals(annotationToUpdate.Text, text))
                    {
                        await m_AnnotationManagement.UpdateAnnotationAsync(
                            new AnnotationReference(info.ProjectId, annotationToUpdate.AnnotationId),
                            new UpdateAnnotationData(text: text), token.Token);

                        token?.Cancel();
                        token = new CancellationTokenSource();
                        GlobalCancellationTokenSource = token;
                    }

                    if (attachments != null && attachments.Count > 0)
                    {
                        foreach (var attachment in attachments)
                        {
                            _ = await UploadAttachment(assetInfo, token, annotationToUpdate.AnnotationId, attachment);
                        }
                    }
                } catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    // Always fire the callback so the caller's loading/edit UI is released even if
                    // the update or an attachment upload threw.
                    callback?.Invoke(annotationToUpdate);
                }
            }
        }

        private void OnDeleteAnnotation(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation annotationToDelete, Action<bool> onComplete)
        {
            _ = DeleteAction();
            return;

            async Task DeleteAction()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);
                try
                {
                    await m_AnnotationManagement.DeleteAnnotationAsync(
                        new AnnotationReference(info.ProjectId, annotationToDelete.AnnotationId),
                        token.Token);
                } catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                    onComplete?.Invoke(false);
                    return;
                }
                onComplete?.Invoke(true);
            }
        }

        private void OnFollowOrUnfollowThread(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation annotation, bool follow, Action<bool, IAnnotation> callback)
        {
            _ = FollowOrUnfollow();
            return;

            async Task FollowOrUnfollow()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);

                // Optimistic default so a failed call still reports the requested state and the
                // follow/unfollow toggle is never left stuck; a later refresh reconciles it.
                bool isUserFollowing = follow;
                try
                {
                    if (follow)
                    {
                        await m_AnnotationManagement.SubscribeToAnnotationThreadAsync(
                            new AnnotationReference(info.ProjectId, annotation.AnnotationId),
                            token.Token);
                    } else
                    {
                        await m_AnnotationManagement.UnsubscribeFromAnnotationThreadAsync(
                            new AnnotationReference(info.ProjectId, annotation.AnnotationId),
                            token.Token);
                    }

                    token?.Cancel();
                    token = new CancellationTokenSource();
                    GlobalCancellationTokenSource = token;
                    isUserFollowing = await IsUserFollowing(assetInfo, annotation, token);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    callback?.Invoke(isUserFollowing, annotation);
                }
            }
        }

        public async Task<string> ReturnAttachmentDownloadUrl(AssetInfo assetInfo, IAnnotation annotation, IAttachment attachment, string fileName,
            CancellationTokenSource token)
        {
            var info = RetrieveInfo(assetInfo);
            GlobalCancellationTokenSource = token;
            const int maxRetries = 10;
            const int delayMs = 200;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await m_AnnotationManagement.ReadAnnotationFileAttachmentDownloadUrlAsync(
                        new AttachmentReference(info.ProjectId, annotation.AnnotationId, attachment.AttachmentId),
                        filePath: fileName,
                        cancellationToken: token.Token
                    );
                    if (!string.IsNullOrEmpty(result.Url))
                    {
                        return result.Url;
                    }
                }
                catch (Exception e)
                {
                    //Debug.Log($"Attempt {attempt} failed: {e.Message ?? e.ToString()}");
                }
                if (attempt < maxRetries)
                    await Task.Delay(delayMs);
            }
            return string.Empty;
        }

        public static async Task<bool> IsUserFollowing(AssetInfo assetInfo, IAnnotation annotation, CancellationTokenSource token)
        {
            var info = RetrieveInfo(assetInfo);
            GlobalCancellationTokenSource = token;
            var result = await m_AnnotationManagement.IsSubscribedToAnnotationThreadAsync(
                new AnnotationReference(info.ProjectId, annotation.AnnotationId),
                token.Token);
            return result.IsSubscribed;
        }

        private void OnCreateNewAnnotation(AssetInfo assetInfo, CancellationTokenSource token, string rootAnnotationId, string message, List<Attachment> attachments, GameObject spatialAttachment, Action<bool, IAnnotation> onComplete)
        {
            bool isStartingNewThread = rootAnnotationId == string.Empty;
            _ = NewAnnotation();
            return;

            async Task NewAnnotation()
            {
                var info = RetrieveInfo(assetInfo);

                var targetContext = new Dictionary<string, string>
                {
                    { k_AssetVersionIdKey, info.assetVersion.ToString() },
                    { k_AssetVersionNumberKey, info.assetVersionNumber.ToString() }
                };
                bool success = false;
                IAnnotation newAnnotation = null;
                try
                {
                    token?.Cancel();
                    token = new CancellationTokenSource();
                    GlobalCancellationTokenSource = token;
                    var newAnnotationId = await m_AnnotationManagement.CreateAnnotationAsync(
                        new StringReference(info.ProjectId, info.target),
                        new CreateAnnotationData(
                            targetContext: targetContext,
                            text: message,
                            rootAnnotationId: isStartingNewThread ? null : new AnnotationId(rootAnnotationId)
                        ),
                        token.Token);
                    token.Token.ThrowIfCancellationRequested();

                    bool hasAttachments = attachments != null && attachments.Count > 0;
                    bool allAttachmentsUploaded = true;

                    if (hasAttachments)
                    {
                        Debug.Log("Uploading " + attachments.Count + " attachments");
                        token.Token.ThrowIfCancellationRequested();

                        foreach (var attachment in attachments)
                        {
                            Debug.Log("Uploading attachment: " + attachment.FileName);
                            var uploadSuccess = await UploadAttachment(assetInfo, token, newAnnotationId, attachment);
                            if (!uploadSuccess)
                            {
                                allAttachmentsUploaded = false;
                                break; // or continue, depending on your needs
                            }
                        }
                    }

                    if (spatialAttachment != null)
                    {
                        var uploadSuccess = await UploadSpatialAttachment(assetInfo, token, newAnnotationId, spatialAttachment);
                        if (!uploadSuccess)
                        {
                            allAttachmentsUploaded = false;
                        }
                    }

                    newAnnotation = await m_AnnotationManagement.ReadAnnotationAsync(
                        new AnnotationReference(info.ProjectId, newAnnotationId),
                        token.Token);

                    token.Token.ThrowIfCancellationRequested();

                    success = !string.IsNullOrEmpty(newAnnotationId.ToString()) && allAttachmentsUploaded;

                }
                catch (OperationCanceledException oe)
                {
                    Debug.Log(oe.Message ?? oe.ToString());
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    onComplete?.Invoke(success, success ? newAnnotation : null);
                }
            }
        }

        private void OnResolveOrOpenThread(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation arg1, bool resolve, Action<IAnnotation> callback)
        {
            _ = ResolveOrOpen();
            return;

            async Task ResolveOrOpen()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);

                // Fall back to the original annotation if the re-read fails, so the callback always
                // runs with a non-null value and the resolve button is never left disabled.
                IAnnotation updatedAnnotation = arg1;
                try
                {
                    if (resolve)
                    {
                        await m_AnnotationManagement.ResolveAnnotationAsync(
                            new AnnotationReference(info.ProjectId, arg1.AnnotationId),
                            token.Token);
                    }
                    else
                    {
                        await m_AnnotationManagement.UnresolveAnnotationAsync(
                            new AnnotationReference(info.ProjectId, arg1.AnnotationId),
                            token.Token);
                    }

                    token?.Cancel();
                    token = new CancellationTokenSource();
                    GlobalCancellationTokenSource = token;
                    updatedAnnotation = await m_AnnotationManagement.ReadAnnotationAsync(
                        new AnnotationReference(info.ProjectId, arg1.AnnotationId),
                        token.Token);
                } catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }
                finally
                {
                    callback?.Invoke(updatedAnnotation);
                }
            }
        }

        private void OnOpenThread(AssetInfo assetInfo, CancellationTokenSource token, IAnnotation rootThread, Action<bool, IReadOnlyList<IAnnotation>> callback)
        {
            var annotationList = new List<IAnnotation> { rootThread };
            _ = ReadReplies();
            return;

            async Task ReadReplies()
            {
                token?.Cancel();
                token = new CancellationTokenSource();
                GlobalCancellationTokenSource = token;
                var info = RetrieveInfo(assetInfo);

                IsSubscribedToAnnotationThreadResult isSubscribed = default;
                try
                {
                    isSubscribed = await m_AnnotationManagement.IsSubscribedToAnnotationThreadAsync(
                        new AnnotationReference(info.ProjectId, rootThread.AnnotationId),
                        token.Token);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message ?? e.ToString());
                }

                string nextPage = null;

                do
                {
                    token?.Cancel();
                    token = new CancellationTokenSource();
                    GlobalCancellationTokenSource = token;
                    var replyAnnotations = m_AnnotationManagement.ReadRepliesAsync(
                        new AnnotationReference(info.ProjectId, rootThread.AnnotationId),
                        new FilteringOptionsWithStatusFilter(next: nextPage, limit: 100, sortingOrder: SortOrder.Ascending),
                        token.Token);

                    var result = await HandleRequest(token, replyAnnotations);

                    if (result.Annotations != null && result.Annotations.Count > 0)
                    {
                        annotationList.AddRange(result.Annotations.Where(x => string.Equals(x.Status, "Active")).ToList());
                    }
                    nextPage = result.Next;
                } while(!string.IsNullOrEmpty(nextPage));
                callback?.Invoke(isSubscribed.IsSubscribed, annotationList);
            }
        }

        private void OnAssetsCollaborationStarted(AssetInfo assetInfo, CancellationTokenSource token, FilterType filterType, Action<IReadOnlyList<IAnnotation>> callback)
        {
            var info = RetrieveInfo(assetInfo);

            if(info.ProjectId == ProjectId.None || info.AssetId == AssetId.None) return;
            _ = QueryAnnotationsAsync();
            return;

            async Task QueryAnnotationsAsync()
            {
                string queryNext = null;

                List<IAnnotation> resultAnnotation = new List<IAnnotation>();

                do
                {
                    token?.Cancel();
                    token = new CancellationTokenSource();
                    GlobalCancellationTokenSource = token;
                    var allAnnotations = m_AnnotationManagement.ReadAnnotationsAsync(
                        new StringReference(info.ProjectId, info.target + "/**"),
                        new FilteringOptions(next: queryNext, limit: 100, sortingOrder: SortOrder.Descending),
                        token.Token);

                    if(token.IsCancellationRequested) return;

                    var result = await HandleRequest(token, allAnnotations);
                    if (result.Annotations == null)
                    {
                        callback?.Invoke(null);
                        return;
                    }

                    queryNext = result.Next;

                    resultAnnotation.AddRange(result.Annotations.Where(x => x.RootAnnotationId == null && string.Equals(x.Status, "Active")).ToList());

                } while (!string.IsNullOrEmpty(queryNext));

                if(filterType == FilterType.Opened)
                {
                    resultAnnotation = resultAnnotation.Where(x => !x.Resolved.HasValue).ToList();
                }
                if(token.IsCancellationRequested) return;
                callback?.Invoke(resultAnnotation.Count > 0 ? resultAnnotation : null);
            }
        }

        private async Task<TResult> HandleRequest<TResult>(CancellationTokenSource token, Task<TResult> requestTask)
        {
            await HandleRequest(token, requestTask, default);
            return requestTask.IsCompletedSuccessfully ? requestTask.Result : default;
        }

        private async Task HandleRequest(CancellationTokenSource token, Task requestTask, Action<bool> callback = null)
        {
            bool result = false;

            try
            {
                await requestTask;

                token.Token.ThrowIfCancellationRequested();

                result = true;
            }
            catch (OperationCanceledException oe)
            {
                Debug.Log(oe.Message ?? oe.ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e.Message ?? e.ToString());
            }

            callback?.Invoke(result);
        }

        private static (ProjectId ProjectId, AssetId AssetId, AssetVersion assetVersion, int assetVersionNumber, string target) RetrieveInfo(AssetInfo assetInfo)
        {
            ProjectId projectId = ProjectId.None;
            AssetId assetId = AssetId.None;
            AssetVersion? assetVersion = null;
            var assetVersionNumber = -1;
            if (assetInfo.Asset is not OfflineAsset offlineAsset)
            {
                projectId = assetInfo.Asset.Descriptor.ProjectId;
                assetId = assetInfo.Asset.Descriptor.AssetId;
                assetVersion = assetInfo.Asset.Descriptor.AssetVersion;
                assetVersionNumber = assetInfo.Properties.Value.FrozenSequenceNumber;
            }
            else
            {
                projectId = offlineAsset.Descriptor.ProjectId;
                assetId = offlineAsset.Descriptor.AssetId;
                assetVersion = offlineAsset.Descriptor.AssetVersion;
                assetVersionNumber = offlineAsset.OfflineAssetInfo.assetVersion;
            }

            string target = $"assets/projects/{projectId}/assets/{assetId}";
            return (projectId, assetId, assetVersion.Value, assetVersionNumber, target);
        }
    }
}
