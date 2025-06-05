using Unity.AppUI.UI;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Streaming
{
    [UxmlElement("ProgressActionButton")]
    public partial class ProgressActionButton : ActionButton
    {
        private CircularProgress Progress => this.Q<CircularProgress>();
        private Icon Icon => this.Q<Icon>();
        
        public ProgressActionButton()
        {
            AddToClassList("ProgressActionButton");
            var progress = new CircularProgress
            {
                variant = AppUI.UI.Progress.Variant.Determinate,
                size = Size.S
            };
            hierarchy.Insert(0, progress);
            progress.style.display = DisplayStyle.None;
        }
        
        public void ShowProgress()
        {
            Progress.style.display = DisplayStyle.Flex;
            Icon.style.display = DisplayStyle.None;
            SetEnabled(false);
        }
        
        public void HideProgress()
        {
            SetEnabled(true);
            Progress.style.display = DisplayStyle.None;
            Icon.style.display = DisplayStyle.Flex;
        }
    }
}
