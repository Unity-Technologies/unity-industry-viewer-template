using System;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Industry.Viewer.Streaming.Hierarchy;
using RuntimeGizmos;
using System.Collections;
using Unity.Industry.Viewer.VR;

public class XRHierarchyUIController : MonoBehaviour
{
    [SerializeField]
    private GameObject m_XRHierarchyUI;

    [SerializeField]
    private string m_AnchorGameObjectName = "Front Facing UI";
    
    [SerializeField]
    private GameObject m_UIAnchor;
    
#if VR_MODE
    
    private GameObject m_UIInstance;
    
    private VisualElement m_Root;
    
    private SliderFloat m_SliderFloat;
    
    private ActionButton m_ResetButton;
    
    private XRRoundButton m_MoveButton;
    private XRRoundButton m_RotateButton;
    
    private GridViewManager m_GridViewManager => m_HierarchyController.GridViewManager;

    private HierarchyToolController m_HierarchyController;
    
    private GameObject m_Selection;
    
    private UIDocument m_UIDocument;
    
    private UIDocument m_ModeSwitcherUIDocument;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject anchorGameObject = GameObject.Find(m_AnchorGameObjectName);
        if (anchorGameObject == null) return;
        m_UIAnchor.SetActive(true);
        m_HierarchyController ??= GetComponent<HierarchyToolController>();
        m_UIInstance = Instantiate(m_XRHierarchyUI, anchorGameObject.transform);
        m_UIInstance.transform.localPosition = Vector3.zero;
        m_UIInstance.transform.localRotation = Quaternion.identity;
        UIDocument uiDocument = m_UIInstance.GetComponentInChildren<UIDocument>();
        m_SliderFloat = uiDocument.rootVisualElement.Q<SliderFloat>();
        m_ResetButton = uiDocument.rootVisualElement.Q<ActionButton>();
        m_Root = m_ResetButton.parent;
        m_Root?.SetEnabled(false);
        m_ResetButton.clicked += OnResetPosition;
        m_SliderFloat.RegisterValueChangingCallback(OnGridSizeChanging);
        m_SliderFloat.RegisterValueChangedCallback(OnGridSizeChanged);
        m_ModeSwitcherUIDocument = m_UIAnchor.GetComponentInChildren<UIDocument>(true);
        StartCoroutine(WaitForGridViewManager());
        StartCoroutine(WaitForUIUpdate());
        return;
        
        IEnumerator WaitForGridViewManager()
        {
            while (m_GridViewManager == null)
            {
                yield return null;
            }
            m_SliderFloat.SetValueWithoutNotify(m_GridViewManager.GetGridUnit());
        }

        IEnumerator WaitForUIUpdate()
        {
            yield return new WaitForEndOfFrame();
            m_ModeSwitcherUIDocument.enabled = true;
            m_MoveButton = m_ModeSwitcherUIDocument.rootVisualElement.Q<XRRoundButton>("MoveButton");
            m_RotateButton = m_ModeSwitcherUIDocument.rootVisualElement.Q<XRRoundButton>("RotateButton");
            m_MoveButton.primary = true;
            m_RotateButton.primary = false;

            m_MoveButton.clicked += OnMoveButtonClicked;
            m_RotateButton.clicked += OnRotateButtonClicked;
        
            m_ModeSwitcherUIDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
    }
    
    private void Update()
    {
        if (!m_ModeSwitcherUIDocument.enabled || TransformGizmo.Instance == null)
        {
            return;
        }
        if (TransformGizmo.Instance.mainTargetRoot == null)
        {
            m_ModeSwitcherUIDocument.rootVisualElement.style.display = DisplayStyle.None;
            m_Root.SetEnabled(false);
        }
        else
        {
            m_ModeSwitcherUIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            m_Root.SetEnabled(true);
        }
    }

    private void OnDestroy()
    {
        if (m_UIInstance != null)
        {
            Destroy(m_UIInstance);
        }

        if (m_MoveButton != null)
        {
            m_MoveButton.clicked -= OnMoveButtonClicked;
        }

        if (m_RotateButton != null)
        {
            m_RotateButton.clicked -= OnRotateButtonClicked;
        }
        
        m_SliderFloat?.UnregisterValueChangingCallback(OnGridSizeChanging);
        m_SliderFloat?.UnregisterValueChangedCallback(OnGridSizeChanged);
        if (m_ResetButton != null)
        {
            m_ResetButton.clicked -= OnResetPosition;
        }
    }

    private void OnMoveButtonClicked()
    {
        if(m_MoveButton.primary) return;
        m_MoveButton.primary = true;
        m_RotateButton.primary = false;
        m_HierarchyController.SwitchGizmoMode(TransformType.Move);
    }

    private void OnRotateButtonClicked()
    {
        if(m_RotateButton.primary) return;
        m_RotateButton.primary = true;
        m_MoveButton.primary = false;
        m_HierarchyController.SwitchGizmoMode(TransformType.Rotate);
    }

    private void OnResetPosition()
    {
        TransformGizmo.Instance.mainTargetRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
    
    private void OnGridSizeChanged(ChangeEvent<float> evt)
    {
        ChangeGrid(evt.newValue);
    }

    private void OnGridSizeChanging(ChangingEvent<float> evt)
    {
        ChangeGrid(evt.newValue);
    }

    private void ChangeGrid(float sliderValue)
    {
        float value = Mathf.Round(sliderValue * 10) / 10;
        m_GridViewManager?.SetGridUnit(value);
        if (TransformGizmo.Instance == null) return;
        TransformGizmo.Instance.movementSnap = value;
        TransformGizmo.Instance.rotationSnap = value;
    }
#endif
}
