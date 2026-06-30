using System;
using Unity.AppUI.UI;
using UnityEngine.UIElements;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using Unity.AppUI.Core;
using UnityEngine;

namespace Unity.Industry.Viewer.Collaboration
{
    public class UserTaggingButtonController: IDisposable
    {
        public readonly IUserInfo UserInfo;
        public UserId UserId => UserInfo.UserId;
        public string Username => UserInfo.Name;
        public string UserEmail => UserInfo.Email;
        private static TextArea _currentTextArea;
        private static int anchorIndex;
        private static Popover _currentPopover;
        private readonly NameSuggestionButton _button;

        public UserTaggingButtonController(IUserInfo userInfo, NameSuggestionButton button, TextArea currentTextArea, ref Popover currentPopover)
        {
            _currentTextArea ??= currentTextArea;
            _currentPopover ??= currentPopover;
            UserInfo = userInfo;
            _button = button;
            _button.label = UserInfo.Name + "\n<size=90%>" + UserInfo.Email + "</size>";
            _button.quiet = true;
            _button.focusable = false;
            _button.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _button.RegisterCallback<ClickEvent>(OnButtonClicked);
            _button.AvatarLabel = CollaborationUIUtility.ReturnInitials(Username);
            _button.Avatar.backgroundColor = new Optional<Color>(CollaborationUIUtility.GetRandomBackgroundColorAsUnityColor(Username));
        }

        private void OnButtonClicked(ClickEvent evt)
        {
            if(evt.target != _button) return;
            if (_currentTextArea == null) return;
            Click();
            _currentTextArea.Focus();
        }

        public void Click()
        {
            // _currentPopover is static and nulled by Dispose() (fired on any button's detach),
            // so it can be null here when invoked from the keyboard path.
            _currentPopover?.Dismiss();
            CollaborationUIUtility.InsertNameTagging(UserInfo, _currentTextArea);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Dispose();
        }

        public void Dispose()
        {
            _currentPopover = null;
            _currentTextArea = null;
            _button.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _button.UnregisterCallback<ClickEvent>(OnButtonClicked);
        }
    }
}
