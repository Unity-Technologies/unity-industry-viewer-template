using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Unity.AppUI.UI;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Unity.Industry.Viewer.VR
{
    public class XRPanel : MonoBehaviour
    {
        private const string k_ContentContainerName = "Content-Container";
        private const string k_TitleName = "Title";
        private const string k_CloseButtonName = "Close-Button";
        private const string k_MessageContainerName = "Message-Containter";
        private const string k_PrimaryButtonName = "Primary-Button";
        private const string k_SecondaryButtonName = "Secondary-Button";
        private const string k_CancelButtonName = "Cancel-Button";
        private const string k_DescriptionName = "Description";
        private const string k_RootMainName = "main-container";
        private const string k_DividerName = "Divider";
        private const string k_BackgroundName = "Background";

        private static XRPanel Instance;
        
        private VisualElement m_RootElement, m_ContainerElement, m_MessageContainerElement, m_DividerElement, m_BackgroundElement;
        private IconButton m_CloseButton;
        private ActionButton m_PrimaryButton, m_SecondaryButton, m_CancelButton;
        private Text m_TitleText, m_DescriptionText;
        private Color m_defaultBackgroundColor;
        private Dictionary<UIDocument, (bool, bool?)> UIDocumentEnabledStates;
        
        [SerializeField]
        private UIDocument m_UIDocument;
        
        [SerializeField]
        private LazyFollow m_LazyFollow;
        
        private CustomXRPanel _mCurrentPanel;
        
        private XRPokeFilter m_PokeFilter;
        
        private BoxCollider m_BoxCollider;

        private float defaultZOffset;

        private void Awake()
        {
            Instance = this;
            defaultZOffset = m_LazyFollow.targetOffset.z;
            m_RootElement = m_UIDocument.rootVisualElement.Q<VisualElement>(k_RootMainName);
            m_RootElement?.RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
        }

        private IEnumerator Start()
        {
            m_ContainerElement = m_UIDocument.rootVisualElement.Q<VisualElement>(k_ContentContainerName);
            m_TitleText = m_UIDocument.rootVisualElement.Q<Text>(k_TitleName);
            m_CloseButton = m_UIDocument.rootVisualElement.Q<IconButton>(k_CloseButtonName);
            m_MessageContainerElement = m_UIDocument.rootVisualElement.Q<VisualElement>(k_MessageContainerName);
            m_PrimaryButton = m_UIDocument.rootVisualElement.Q<ActionButton>(k_PrimaryButtonName);
            m_SecondaryButton = m_UIDocument.rootVisualElement.Q<ActionButton>(k_SecondaryButtonName);
            m_CancelButton = m_UIDocument.rootVisualElement.Q<ActionButton>(k_CancelButtonName);
            m_DescriptionText = m_UIDocument.rootVisualElement.Q<Text>(k_DescriptionName);
            m_DividerElement = m_UIDocument.rootVisualElement.Q<VisualElement>(k_DividerName);
            m_BackgroundElement = m_UIDocument.rootVisualElement.Q<VisualElement>(k_BackgroundName);
            m_CloseButton.clicked += OnCloseButtonClicked;
            m_CancelButton.clicked += OnCloseButtonClicked;
            m_PokeFilter = GetComponent<XRPokeFilter>();
            m_RootElement.style.display = DisplayStyle.None;
            yield return null;
            CheckCollider();
            yield return new WaitForEndOfFrame();
            m_defaultBackgroundColor = m_BackgroundElement.resolvedStyle.backgroundColor;
        }
        
        private void OnDestroy()
        {
            m_RootElement?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            if (m_CloseButton != null)
            {
                m_CloseButton.clicked -= OnCloseButtonClicked;
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.clicked -= OnCloseButtonClicked;
            }
            Instance = null;
        }

        private void CheckCollider()
        {
            // Ensure that we have only one BoxCollider on the XRPanel
            var allColliders = GetComponents<BoxCollider>().ToList();
            while (allColliders.Count > 1)
            {
                var colliderToRemove = allColliders.Last();
                Destroy(colliderToRemove);
                allColliders.Remove(colliderToRemove);
            }
            m_BoxCollider = allColliders.FirstOrDefault();
            m_PokeFilter.pokeCollider = m_BoxCollider;
            m_BoxCollider.enabled = m_RootElement.resolvedStyle.display != DisplayStyle.None;
        }

        private void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            CheckCollider();
        }

        private void OnCloseButtonClicked()
        {
            _mCurrentPanel?.Dismiss();
        }

        public static XRPanelBuilder Build(CustomXRPanel panel, VisualElement customContent)
        {
            return new XRPanelBuilder(panel, customContent);
        }
        
        public class XRPanelBuilder
        {
            private CustomXRPanel _panel;
            private VisualElement _customContent;
            private bool _background;
            private bool _closeButton;
            private float _zOffset = 0f; // Default z-offset

            public XRPanelBuilder(CustomXRPanel panel, VisualElement customContent)
            {
                _panel = panel;
                _customContent = customContent;
                _panel.SetCustomContent(customContent);
                _background = true;
                _closeButton = true; // Default to showing close button
                _panel.Background.style.backgroundColor = Instance.m_defaultBackgroundColor;
                _panel.CloseButton.style.display = DisplayStyle.Flex;
                _zOffset = Instance.defaultZOffset;
                Instance.m_LazyFollow.targetOffset = new Vector3(Instance.m_LazyFollow.targetOffset.x, Instance.m_LazyFollow.targetOffset.y, _zOffset);
            }

            public XRPanelBuilder SetBackground(bool value)
            {
                _background = value;
                return this;
            }

            public XRPanelBuilder SetCloseButton(bool value)
            {
                _closeButton = value;
                return this;
            }

            public XRPanelBuilder SetZOffset(float value)
            {
                _zOffset = value;
                return this;
            }

            public void Build()
            {
                XRPanel.Build(_panel, _customContent);
                // Apply background and close button settings here
                // Example:
                _panel.Background.style.backgroundColor = !_background ? new StyleColor(Color.clear) : new StyleColor(Instance.m_defaultBackgroundColor);
                _panel.CloseButton.style.display = _closeButton ? DisplayStyle.Flex : DisplayStyle.None;
                Instance.m_LazyFollow.targetOffset = new Vector3(0f, 0f, _zOffset);
            }
        }
        
        public class AlertXRPanel : CustomXRPanel
        {
            private Action m_PrimaryButtonAction;
            private Action m_SecondaryButtonAction;
            public Text DescriptionText => Instance.m_DescriptionText;
            public Text TitleText => Instance.m_TitleText;
            public ActionButton PrimaryButton => Instance.m_PrimaryButton;
            public ActionButton SecondaryButton => Instance.m_SecondaryButton;
            public ActionButton CancelButton => Instance.m_CancelButton;

            public AlertXRPanel(string titleText, string description, bool lockOtherUI = true)
                : base(titleText, lockOtherUI)
            {
                Instance.m_MessageContainerElement.style.display = DisplayStyle.Flex;
                Instance.m_DescriptionText.text = description;
                Instance.m_PrimaryButton.style.display = DisplayStyle.None;
                Instance.m_SecondaryButton.style.display = DisplayStyle.None;
                Instance.m_CancelButton.style.display = DisplayStyle.None;
                Instance.m_BackgroundElement.style.backgroundColor = Instance.m_defaultBackgroundColor;
                Instance.m_CloseButton.style.display = DisplayStyle.Flex;
                Instance.m_LazyFollow.targetOffset = new Vector3(Instance.m_LazyFollow.targetOffset.x, Instance.m_LazyFollow.targetOffset.y, Instance.defaultZOffset);
            }

            public override void Show()
            {
                if (Instance._mCurrentPanel != null)
                {
                    return;
                }
                
                Instance.m_RootElement.style.display = DisplayStyle.Flex;
                Instance.m_PokeFilter.enabled = true;
                
                Instance.m_MessageContainerElement.style.display = DisplayStyle.Flex;
                
                Instance.m_PrimaryButton.style.display = !string.IsNullOrEmpty(Instance.m_PrimaryButton.label)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                
                Instance.m_SecondaryButton.style.display = !string.IsNullOrEmpty(Instance.m_SecondaryButton.label)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                
                Instance.m_CancelButton.style.display = !string.IsNullOrEmpty(Instance.m_CancelButton.label)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                
                Instance.m_CloseButton.style.display = DisplayStyle.None;
                
                SetLockUI();

                Instance._mCurrentPanel = this;
                Shown?.Invoke(this);
            }
            
            public void SetPrimaryButton(string text, Action onClick)
            {
                Instance.m_PrimaryButton.label = text;
                m_PrimaryButtonAction = onClick;
                m_PrimaryButtonAction += Dismiss;
                Instance.m_PrimaryButton.clicked -= m_PrimaryButtonAction;
                Instance.m_PrimaryButton.clicked += m_PrimaryButtonAction;
                Instance.m_PrimaryButton.selected = true;
                Instance.m_PrimaryButton.accent = true;
            }

            public void SetSecondaryButton(string text, Action onClick)
            {
                Instance.m_SecondaryButton.label = text;
                m_SecondaryButtonAction = onClick;
                m_SecondaryButtonAction += Dismiss;
                Instance.m_SecondaryButton.clicked -= m_SecondaryButtonAction;
                Instance.m_SecondaryButton.clicked += m_SecondaryButtonAction;
            }
            
            public void SetCancelButton(string text)
            {
                Instance.m_CancelButton.label = text;
            }

            public override void Dismiss()
            {
                base.Dismiss();
                Instance.m_PrimaryButton.clicked -= m_PrimaryButtonAction;
                Instance.m_SecondaryButton.clicked -= m_SecondaryButtonAction;
                Instance.m_PrimaryButton.label = string.Empty;
                Instance.m_SecondaryButton.label = string.Empty;
                Instance.m_CancelButton.label = string.Empty;
                Instance.m_PrimaryButton.selected = false;
                Instance.m_PrimaryButton.accent = false;
                m_PrimaryButtonAction = null;
                m_SecondaryButtonAction = null;
                Instance.m_DescriptionText.text = string.Empty;
                Instance.m_TitleText.text = string.Empty;
            }
        }

        public class CustomXRPanel
        {
            public VisualElement Background => Instance.m_BackgroundElement;
            public IconButton CloseButton => Instance.m_CloseButton;
            public VisualElement Content => m_Content;
            public UIDocument UIDocument => Instance.m_UIDocument;
            private VisualElement m_Content;
            public bool LockOtherUI;

            public CustomXRPanel(string titleText, bool lockOtherUI = true)
            {
                LockOtherUI = lockOtherUI;
                Instance.m_TitleText.text = titleText;
                Instance.m_TitleText.style.display = string.IsNullOrEmpty(titleText) ? DisplayStyle.None : DisplayStyle.Flex;
                Instance.m_DividerElement.style.display = string.IsNullOrEmpty(titleText) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            
            internal void SetCustomContent(VisualElement content)
            {
                m_Content = content;
            }

            public Action<CustomXRPanel> Shown;
            public Action<CustomXRPanel> Dismissed;

            public virtual void Show()
            {
                if(Instance._mCurrentPanel != null) return; 
                
                Instance.m_BoxCollider ??= Instance.GetComponent<BoxCollider>();
                if (Instance.m_BoxCollider != null)
                {
                    Instance.m_BoxCollider.enabled = true;
                }
                
                if (m_Content != null)
                {
                    Instance.m_MessageContainerElement.style.display = DisplayStyle.None;
                    Instance.m_ContainerElement.Add(m_Content);
                }
                Instance.m_RootElement.style.display = DisplayStyle.Flex;
                Instance.m_PokeFilter.enabled = true;

                SetLockUI();

                Instance._mCurrentPanel = this;
                Shown?.Invoke(this);
            }

            protected void SetLockUI()
            {
                if (LockOtherUI)
                {
                    UIDocument[] uiDocuments =
                        FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var uiDoc in uiDocuments)
                    {
                        if(uiDoc.rootVisualElement == null || uiDoc == Instance.m_UIDocument || 
                           uiDoc == XRInAppSettingUIController.fpsUIDocument) continue;
                        Instance.UIDocumentEnabledStates ??= new Dictionary<UIDocument, (bool, bool?)>();

                        bool? boxColliderEnabled = null;
                        
                        if (uiDoc.gameObject.TryGetComponent(out BoxCollider boxCollider))
                        {
                            boxColliderEnabled = boxCollider.enabled;
                        }
                        
                        Instance.UIDocumentEnabledStates.Add(uiDoc, (uiDoc.rootVisualElement.enabledSelf, boxColliderEnabled));
                        uiDoc.rootVisualElement.SetEnabled(false);
                        if (boxColliderEnabled.HasValue)
                        {
                            boxCollider.enabled = false;
                        }
                    }

                    Instance.m_LazyFollow.enabled = true;
                }
                else
                {
                    Instance.m_LazyFollow.enabled = false;
                }
            }

            public virtual void Dismiss()
            {
                if(Instance._mCurrentPanel == null) return; 
                if (Instance.UIDocumentEnabledStates != null)
                {
                    foreach (var uiDoc in Instance.UIDocumentEnabledStates.Keys)
                    {
                        if(uiDoc == null) continue;
                        uiDoc.rootVisualElement.SetEnabled(Instance.UIDocumentEnabledStates[uiDoc].Item1);
                        if (Instance.UIDocumentEnabledStates[uiDoc].Item2.HasValue)
                        {
                            uiDoc.GetComponent<BoxCollider>().enabled = Instance.UIDocumentEnabledStates[uiDoc].Item2.Value;
                        }
                    }
                    Instance.UIDocumentEnabledStates.Clear();
                    Instance.UIDocumentEnabledStates = null;
                }
                m_Content?.RemoveFromHierarchy();
                Instance.m_RootElement.style.display = DisplayStyle.None;
                Instance.m_BackgroundElement.style.backgroundColor = Instance.m_defaultBackgroundColor;
                Instance.m_CloseButton.style.display = DisplayStyle.Flex;
                Instance.m_PokeFilter.enabled = false;
                Instance._mCurrentPanel = null;
                
                Dismissed?.Invoke(this);
            }
        }
    }
}