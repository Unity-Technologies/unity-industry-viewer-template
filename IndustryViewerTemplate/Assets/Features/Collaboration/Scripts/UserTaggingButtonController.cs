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
        // Per-button targets: the popover can be rebuilt for another composer while a
        // previous one's dismissal is still animating, so shared/static slots would
        // route the insert into the wrong composer — or into nothing after a Dispose.
        private readonly TextArea _textArea;
        private readonly Popover _popover;
        private readonly NameSuggestionButton _button;

        public UserTaggingButtonController(IUserInfo userInfo, NameSuggestionButton button, TextArea currentTextArea, ref Popover currentPopover)
        {
            _textArea = currentTextArea;
            _popover = currentPopover;
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
            if (_textArea == null) return;
            Click();
            _textArea.Focus();
        }

        public void Click()
        {
            _popover?.Dismiss();
            CollaborationUIUtility.InsertNameTagging(UserInfo, _textArea);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Dispose();
        }

        public void Dispose()
        {
            _button.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _button.UnregisterCallback<ClickEvent>(OnButtonClicked);
        }
    }
}
