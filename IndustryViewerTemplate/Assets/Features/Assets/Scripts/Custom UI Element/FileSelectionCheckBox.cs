using UnityEngine.UIElements;
using Unity.AppUI.UI;

namespace Unity.Industry.Viewer.Assets
{
    [UxmlElement]
    public partial class FileSelectionCheckBox : Checkbox
    {
        private Icon _icon;

        public FileSelectionCheckBox()
        {
            _icon = new Icon
            {
                iconName = string.Empty
            };
            hierarchy.Insert(1, _icon);
            emphasized = true;
            AddToClassList("file-selection-checkbox");
        }
    }
}
