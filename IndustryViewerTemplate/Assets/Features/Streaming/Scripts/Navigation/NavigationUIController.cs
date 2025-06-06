using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System.Collections.Generic;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Streaming
{
    [DefaultExecutionOrder(80)]
    public class NavigationUIController : MonoBehaviour
    {
        private UIDocument m_UIDocument;
        private IconButton m_NavigationButton;
        private VisualElement m_NavigationOptions;
        private VisualElement m_BottomLeftContainer;
        private VisualElement m_NavigationContainer;
        
        Dictionary<NavigationOption, IconButton> m_NavigationOptionButtons = new();
        
        [SerializeField]
        private StyleSheet m_StreamingStyleSheet;
        
        [SerializeField]
        private NavigationController m_NavigationController;

        private void OnEnable()
        {
            InitialUI();
        }

        private void Start()
        {
            NavigationController.OnNavigationOptionChanged += OnNavigationOptionChanged;
            m_NavigationController ??= GetComponent<NavigationController>();
        }

        private void OnDestroy()
        {
            m_NavigationButton.clickable.clicked -= OnNavigationButtonClicked;
            m_NavigationButton.clickable.longClicked -= OnNavigationButtonLongClick;
            m_NavigationButton.RemoveFromHierarchy();
            NavigationController.OnNavigationOptionChanged -= OnNavigationOptionChanged;
            m_NavigationContainer.RemoveFromHierarchy();
        }

        private void OnNavigationButtonLongClick()
        {
            NavigationController.CurrentNavigationOption.NavigationOptionUIComponent.CreatePanel();
        }

        private void OnNavigationOptionChanged(NavigationOption newNavigationOption)
        {
            if (m_NavigationOptions != null)
            {
                m_NavigationOptions.style.display = DisplayStyle.None;
            }
            m_NavigationButton.userData = newNavigationOption;
            SetIcon(newNavigationOption.NavigationOptionUIComponent.NavigationIcon, m_NavigationButton);
            
            m_NavigationButton.ClearBinding("tooltip");
            m_NavigationButton.SetBinding("tooltip", newNavigationOption.NavigationName);
        }

        private void InitialUI()
        {
            if(m_UIDocument != null) return;

            m_UIDocument = SharedUIManager.Instance.AssetsUIDocument;
            if(m_UIDocument == null) return;
            if (!m_UIDocument.rootVisualElement.styleSheets.Contains(m_StreamingStyleSheet))
            {
                m_UIDocument.rootVisualElement.styleSheets.Add(m_StreamingStyleSheet);
            }
            var streamingContainer = m_UIDocument.rootVisualElement.Q<VisualElement>(StreamingUtils.StreamingPanelName);
            m_BottomLeftContainer = streamingContainer.Q<VisualElement>(StreamingUtils.BottomLeftContainerName);

            m_NavigationContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    alignSelf = Align.FlexStart,
                    marginTop = new Length(20, LengthUnit.Pixel)
                }
            };
            m_NavigationButton = new IconButton();
            m_NavigationButton.AddToClassList(StreamingUtils.BottomLeftButtonStyleName);
            SetIcon(m_NavigationController.DefaultNavigationOption.NavigationOptionUIComponent.NavigationIcon, m_NavigationButton);
            
            m_NavigationButton.userData = m_NavigationController.DefaultNavigationOption;
            m_NavigationButton.ClearBinding("tooltip");
            m_NavigationButton.SetBinding("tooltip", m_NavigationController.DefaultNavigationOption.NavigationName);
            
            m_NavigationContainer.Add(m_NavigationButton);
            m_BottomLeftContainer.Add(m_NavigationContainer);
            
            m_NavigationButton.clickable.clicked += OnNavigationButtonClicked;
            m_NavigationButton.clickable.longPressDuration = 2000;
            m_NavigationButton.clickable.longClicked += OnNavigationButtonLongClick;
        }

        private void OnNavigationButtonClicked()
        {
            if (m_NavigationOptionButtons.Count == 0)
            {
                var side = m_NavigationButton.resolvedStyle.height - 30;
                var radius = new Length(side / 2, LengthUnit.Pixel);
                m_NavigationOptions = new VisualElement
                {
                    name = "NavigationOptions",
                    style =
                    {
                        marginLeft = new Length(20f, LengthUnit.Pixel),
                        height = new Length(side, LengthUnit.Pixel),
                        flexDirection = FlexDirection.Row,
                    }
                };
                m_NavigationButton.parent.Add(m_NavigationOptions);
                
                foreach (var navigationOption in m_NavigationController.NavigationOptions)
                {
                    var button = new IconButton
                    {
                        style =
                        {
                            width = side,
                            height = side,
                            marginRight = new Length(10f, LengthUnit.Pixel),
                            borderBottomLeftRadius = radius,
                            borderBottomRightRadius = radius,
                            borderTopLeftRadius = radius,
                            borderTopRightRadius = radius
                        },
                    };
                    
                    button.ClearBinding("tooltip");
                    button.SetBinding("tooltip", navigationOption.NavigationName);
                    SetIcon(navigationOption.NavigationOptionUIComponent.NavigationIcon, button);
                    button.clicked += () =>
                    {
                        NavigationController.ChangeToNewNavigationOption?.Invoke(navigationOption);
                    };
                    m_NavigationOptions.Add(button);
                    m_NavigationOptionButtons.Add(navigationOption, button);
                }
                m_NavigationOptions.style.display = DisplayStyle.None;
            }
            
            m_NavigationOptions.style.display = m_NavigationOptions.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_NavigationOptions.style.display == DisplayStyle.None)
            {
                return;
            }
            
            foreach (var navigationOption in m_NavigationController.NavigationOptions)
            {
                var show = navigationOption.IsSupported() && navigationOption != NavigationController.CurrentNavigationOption;
                m_NavigationOptionButtons[navigationOption].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private static void SetIcon(Texture2D texture, IconButton button)
        {
            var icon = button.Q<Icon>(StreamingUtils.IconButtonIconVEName);
            button.Children().First().style.display = DisplayStyle.Flex;
            icon.image = texture;
            icon.style.display = DisplayStyle.Flex;
        }
    }
}
