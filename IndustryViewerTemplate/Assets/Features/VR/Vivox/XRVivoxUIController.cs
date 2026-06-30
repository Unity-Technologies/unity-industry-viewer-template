using Unity.Industry.Viewer.Vivox;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Vivox;
using Unity.AppUI.UI;

namespace Unity.Industry.Viewer.VR.Vivox
{
    public class XRVivoxUIController : VivoxUIController
    {
        private const string k_FirstNameLabelName = "First-Name-Label";

        [SerializeField]
        UIDocument m_UiDocument;

        protected override void InitializeUI()
        {
            var root = m_UiDocument.rootVisualElement;
            var firstNameText = root.Q<Text>(k_FirstNameLabelName);
            SetupMicButton(root, root, firstNameText.parent, null);
        }
    }
}
