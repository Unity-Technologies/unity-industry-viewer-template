using Unity.Industry.Viewer.Streaming;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System.Linq;
using Unity.Industry.Viewer.Assets;

namespace Unity.Industry.Viewer.Navigation.VR
{
    public class VRMovementUIController : NavigationOptionUI
    {
        private const string k_FlySensitivitySlider = "FlySensitivitySlider";
        
        private IconButton m_HomeButton;
        
        private SliderFloat m_FlySensitivitySlider;
        
        [SerializeField]
        private VRMovementController m_VRFlyNavigationController;
        
        private void OnEnable()
        {
            if (m_HomeButton == null)
            {
                var UIDocument = SharedUIManager.Instance.AssetsUIDocument;
                var streamingContainer = UIDocument.rootVisualElement.Q<VisualElement>(StreamingUtils.StreamingPanelName);
                var bottomLeftContainer = streamingContainer.Q<VisualElement>(StreamingUtils.BottomLeftContainerName);
                
                m_HomeButton = new IconButton()
                {
                    icon = "camera-overhead"
                };
                m_HomeButton.AddToClassList(StreamingUtils.BottomLeftButtonStyleName);
                    
                m_HomeButton.clicked += OnHomeButtonClicked;
                
                bottomLeftContainer.Insert(bottomLeftContainer.childCount, m_HomeButton);
                
            } else 
            {
                m_HomeButton.style.display = DisplayStyle.Flex;
            }
        }

        private void Start()
        {
            m_VRFlyNavigationController ??= GetComponent<VRMovementController>();
        }

        private void OnDisable()
        {
            if(m_HomeButton != null)
            {
                m_HomeButton.style.display = DisplayStyle.None;
            }
        }
        
        private void OnDestroy()
        {
            if(m_HomeButton != null)
            {
                m_HomeButton.clicked -= OnHomeButtonClicked;
                m_HomeButton.RemoveFromHierarchy();
            }
        }
        
        private void OnHomeButtonClicked()
        {
            m_VRFlyNavigationController.SetHomeView();
        }
        
        protected override void InitialUI(VisualElement panel)
        {
            m_FlySensitivitySlider = panel.Q<SliderFloat>(k_FlySensitivitySlider);
            m_FlySensitivitySlider.RegisterValueChangedCallback(OnFlySensitivitySliderValueChanged);
            m_FlySensitivitySlider.RegisterValueChangingCallback(OnFlySensitivitySliderValueChanging);
            
            m_FlySensitivitySlider.SetValueWithoutNotify(m_VRFlyNavigationController.MoveSensitivity);
        }

        private void OnFlySensitivitySliderValueChanging(ChangingEvent<float> evt)
        {
            m_VRFlyNavigationController.UpdateMoveSensitivity(evt.newValue);
        }

        private void OnFlySensitivitySliderValueChanged(ChangeEvent<float> evt)
        {
            m_VRFlyNavigationController.UpdateMoveSensitivity(evt.newValue);
        }

        public override void CreatePanel()
        {
            if (NavigationOptionUIAsset == null) return;
            var navigationOptionUIAsset = NavigationOptionUIAsset.Instantiate().Children().First();
            ToolPanelUIController.OpenToolPanel(m_VRFlyNavigationController.NavigationName, navigationOptionUIAsset);
            InitialUI(navigationOptionUIAsset);
        }
    }
}
