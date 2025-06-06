using System;
using Unity.AppUI.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using UnityEngine.Localization;

namespace Unity.Industry.Viewer.Shared
{
    [UxmlElement]
    public partial class CustomAlertDialog : BaseDialog, IDismissInvocator
    {
        public event Action<DismissType> dismissRequested;
        
        readonly ActionButton m_CancelButton;

        readonly ActionButton m_PrimaryButton;

        readonly ActionButton m_SecondaryButton;

        AlertSemantic m_Variant = AlertSemantic.Default;
        
        /// <summary>
        /// The AlertDialog primary action button.
        /// </summary>
        public ActionButton primaryButton => m_PrimaryButton;

        /// <summary>
        /// The AlertDialog secondary action button.
        /// </summary>
        public ActionButton secondaryButton => m_SecondaryButton;

        /// <summary>
        /// The AlertDialog cancel action button.
        /// </summary>
        public ActionButton cancelButton => m_CancelButton;

        private Action m_SecondaryAction;
        private Action m_PrimaryAction;

        public CustomAlertDialog()
        {
            focusable = true;
            delegatesFocus = true;
            
            RegisterCallback<FocusInEvent>(OnFocusIn);
            
            m_PrimaryButton = new ActionButton { name = AlertDialog.primaryActionUssClassName, style =
            {
                marginLeft = new Length(6, LengthUnit.Pixel)
            }};
            m_PrimaryButton.AddToClassList(AlertDialog.primaryActionUssClassName);

            m_SecondaryButton = new ActionButton { name = AlertDialog.secondaryActionUssClassName, style =
            {
                marginLeft = new Length(6, LengthUnit.Pixel)
            }};
            m_SecondaryButton.AddToClassList(AlertDialog.secondaryActionUssClassName);

            m_CancelButton = new ActionButton { name = AlertDialog.cancelActionUssClassName };
            m_CancelButton.AddToClassList(AlertDialog.cancelActionUssClassName);
            
            actionContainer.Add(m_CancelButton);
            actionContainer.Add(m_SecondaryButton);
            actionContainer.Add(m_PrimaryButton);

            m_PrimaryButton.AddToClassList(Styles.hiddenUssClassName);
            m_SecondaryButton.AddToClassList(Styles.hiddenUssClassName);
            m_CancelButton.AddToClassList(Styles.hiddenUssClassName);
            
            m_PrimaryButton.clicked += OnPrimaryActionClicked;
            m_SecondaryButton.clicked += OnSecondaryActionClicked;
            m_CancelButton.clicked += OnCancelActionClicked;
            
            variant = AlertSemantic.Default;
        }

        public CustomAlertDialog(string displayTitle, string displayMessage) : this()
        {
            title = displayTitle;
            m_Header.text = displayTitle;
            m_Header.RemoveFromClassList(Styles.hiddenUssClassName);
            m_Content.text = displayMessage;
        }
        
        public CustomAlertDialog(LocalizedString displayTitle, LocalizedString displayMessage) : this()
        {
            title = " ";
            m_Header.ClearBinding("text");
            m_Header.SetBinding("text", displayTitle);
            m_Header.RemoveFromClassList(Styles.hiddenUssClassName);
            m_Content.ClearBinding("text");
            m_Content.SetBinding("text", displayMessage);
        }
        
        void OnCancelActionClicked()
        {
            dismissRequested?.Invoke(DismissType.Manual);
        }

        void OnSecondaryActionClicked()
        {
            m_SecondaryAction?.Invoke();
            dismissRequested?.Invoke(DismissType.Action);
        }

        void OnPrimaryActionClicked()
        {
            m_PrimaryAction?.Invoke();
            dismissRequested?.Invoke(DismissType.Action);
        }
        
        void OnFocusIn(FocusInEvent evt)
        {
            schedule.Execute(DeferFocusFirstAction);
            UnregisterCallback<FocusInEvent>(OnFocusIn);
        }
        
        public void SetPrimaryAction(string displayText, bool accent, Action callback, string icon = "")
        {
            m_PrimaryAction = callback;
            m_PrimaryButton.accent = accent;
            m_PrimaryButton.selected = accent;
            m_PrimaryButton.icon = icon;
            m_PrimaryButton.label = displayText;
            m_PrimaryButton.userData = m_PrimaryAction;
            m_PrimaryButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        public void SetPrimaryAction(LocalizedString displayText, bool accent, Action callback, string icon = "")
        {
            m_PrimaryAction = callback;
            m_PrimaryButton.accent = accent;
            m_PrimaryButton.selected = accent;
            m_PrimaryButton.icon = icon;
            m_PrimaryButton.ClearBinding("label");
            m_PrimaryButton.SetBinding("label", displayText);
            m_PrimaryButton.userData = m_PrimaryAction;
            m_PrimaryButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        public void SetSecondaryAction(string displayText, bool accent, Action callback, string icon = "")
        {
            m_SecondaryAction = callback;
            m_SecondaryButton.accent = accent;
            m_SecondaryButton.selected = accent;
            m_SecondaryButton.icon = icon;
            m_SecondaryButton.label = displayText;
            m_SecondaryButton.userData = m_PrimaryAction;
            m_SecondaryButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        public void SetSecondaryAction(LocalizedString displayText, bool accent, Action callback, string icon = "")
        {
            m_SecondaryAction = callback;
            m_SecondaryButton.accent = accent;
            m_SecondaryButton.selected = accent;
            m_SecondaryButton.icon = icon;
            m_SecondaryButton.ClearBinding("label");
            m_SecondaryButton.SetBinding("label", displayText);
            m_SecondaryButton.userData = m_PrimaryAction;
            m_SecondaryButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        public void SetCancelAction(string displayText, string icon = "")
        {
            m_CancelButton.label = displayText;
            m_CancelButton.icon = icon;
            m_CancelButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        public void SetCancelAction(LocalizedString displayText, string icon = "")
        {
            m_CancelButton.ClearBinding("label");
            m_CancelButton.SetBinding("label", displayText);
            m_CancelButton.icon = icon;
            m_CancelButton.RemoveFromClassList(Styles.hiddenUssClassName);
        }
        
        void DeferFocusFirstAction()
        {
            if (m_PrimaryButton.userData != null)
                m_PrimaryButton.Focus();
            else if (m_SecondaryButton.userData != null)
                m_SecondaryButton.Focus();
            else if (m_CancelButton.userData != null)
                m_CancelButton.Focus();
        }
        
        [UxmlAttribute]
        [Header("Alert Dialog")]
        public AlertSemantic variant
        {
            get => m_Variant;
            set
            {
                RemoveFromClassList(GetVariantUssClassName(m_Variant));
                m_Variant = value;
                AddToClassList(GetVariantUssClassName(m_Variant));
            }
        }
    }
}
