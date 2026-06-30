using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System.Threading.Tasks;
using  UnityEngine.Networking;

namespace Unity.Industry.Viewer.Collaboration
{
    public class AttachmentUploadEntryController : IDisposable
    {
        private readonly CollaborationUIBase _uiController;

        private const string k_RemoveAttachmentButtonName = "RemoveAttachmentButton";
        private const string k_PreviewIconName = "PreviewIcon";
        
        public readonly Attachment Attachment;
        public readonly VisualElement EntryElement;
        private readonly GridView m_GridView;
        
        private IconButton m_deleteIconButton;
        private VisualElement m_iconElement;
        private Text m_FileNameText;

        public AttachmentUploadEntryController(CollaborationUIBase uiController, GridView gridView, Attachment attachment, VisualElement entryElement)
        {
            Attachment = attachment;
            EntryElement = entryElement;
            _uiController = uiController;
            m_GridView = gridView;
            m_deleteIconButton = EntryElement.Q<IconButton>(k_RemoveAttachmentButtonName);
            m_deleteIconButton.clicked += OnDeleteClicked;
#if UNITY_STANDALONE || UNITY_EDITOR
            m_deleteIconButton.style.display = DisplayStyle.None;
            EntryElement.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            EntryElement.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
#else
            m_deleteIconButton.style.display = DisplayStyle.Flex;
#endif
            m_iconElement = EntryElement.Q<VisualElement>(k_PreviewIconName);
            m_FileNameText = EntryElement.Q<Text>();
            //EntryElement.RegisterCallback<FocusInEvent>(OnFocus);
            //EntryElement.RegisterCallback<FocusOutEvent>(OnFocusOut);
            m_FileNameText.text = attachment.FileName;
            var canPreview = CollaborationUIUtility.IsSupportedImageFormat(attachment.FileName);
            if (canPreview)
            {
                _ = LoadPreviewImageAsync();
            }
            else
            {
                ShowFileIcon();
            }
        }

        private void OnDeleteClicked()
        {
            _uiController.DeleteAttachment(m_GridView, Attachment);
        }

        private async Task LoadPreviewImageAsync()
        {
            try
            {
                var texture = await LoadTextureAsync(Attachment.FilePath);
                if (texture != null)
                {
                    m_iconElement.style.backgroundImage = new StyleBackground(texture);
                }
                else
                {
                    ShowFileIcon();
                }
            }
            catch (Exception e)
            {
                Debug.Log("Failed to load attachment preview: " + e.Message);
                ShowFileIcon();
            }

            return;

            static async Task<Texture2D> LoadTextureAsync(string path)
            {
                string uri;
                if (path.StartsWith("file://"))
                {
                    uri = path;
                }
                else
                {
                    // Convert to proper file URI based on platform
                    if (Application.platform == RuntimePlatform.WindowsEditor ||
                        Application.platform == RuntimePlatform.WindowsPlayer)
                    {
                        // Windows: file:///C:/path/to/file
                        uri = "file:///" + path.Replace('\\', '/');
                    }
                    else
                    {
                        // macOS/Linux: file:///path/to/file
                        uri = "file://" + path;
                    }
                }

                using var uwr = UnityWebRequestTexture.GetTexture(uri);
                var operation = uwr.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load texture: {uwr.error}");
                    return null;
                }

                return DownloadHandlerTexture.GetContent(uwr);
            }
        }

        private void ShowFileIcon()
        {
            m_iconElement.style.backgroundImage = new StyleBackground(_uiController.DefaultFileIcon);
        }
        
        /*private void OnFocusOut(FocusOutEvent evt)
        {
            m_deleteIconButton.style.display = DisplayStyle.None;
        }

        private void OnFocus(FocusInEvent evt)
        {
            m_deleteIconButton.style.display = DisplayStyle.Flex;
        }*/

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            m_deleteIconButton.style.display = DisplayStyle.None;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            m_deleteIconButton.style.display = DisplayStyle.Flex;
        }

        public void Dispose()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            EntryElement?.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            EntryElement?.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
#endif
            if (m_deleteIconButton != null)
            {
                m_deleteIconButton.clicked -= OnDeleteClicked;
            }
        }
    }
}
