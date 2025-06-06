using Unity.AppUI.UI;
using UnityEngine.UIElements;
using UnityEngine;

namespace Unity.Industry.Viewer.Assets
{
    [UxmlElement("LinkedProject")]
    public partial class LinkedProjectVE : VisualElement
    {
        private VisualElement m_ProjectIcon;
        private Text m_ProjectName;
        private VisualElement m_ProjectSourceIcon;

        [UxmlAttribute]
        public string projectName
        {
            get => m_ProjectName.text;
            set => m_ProjectName.text = value;
        }

        [UxmlAttribute]
        public Color projectIconColor
        {
            get => m_ProjectIcon.style.backgroundColor.value;
            set => m_ProjectIcon.style.backgroundColor = value;
        }
        
        [UxmlAttribute]
        public bool isSourceProject
        {
            get => m_ProjectSourceIcon.ClassListContains("sourceProject");
            set {
                if (value)
                {
                    if (m_ProjectSourceIcon.ClassListContains("editProject"))
                    {
                        m_ProjectSourceIcon.RemoveFromClassList("editProject");
                    }
                    
                    if (!m_ProjectSourceIcon.ClassListContains("sourceProject"))
                    {
                        m_ProjectSourceIcon.AddToClassList("sourceProject");
                    }
                }
                else
                {
                    if (m_ProjectSourceIcon.ClassListContains("sourceProject"))
                    {
                        m_ProjectSourceIcon.RemoveFromClassList("sourceProject");
                    }
                    
                    if (!m_ProjectSourceIcon.ClassListContains("editProject"))
                    {
                        m_ProjectSourceIcon.AddToClassList("editProject");
                    }
                }
            }
        }

        public LinkedProjectVE()
        {
            AddToClassList("LinkedProject");
            m_ProjectIcon = new VisualElement() { name = "project-icon" };
            hierarchy.Add(m_ProjectIcon);
            
            m_ProjectName = new Text() { name = "project-name" };
            hierarchy.Add(m_ProjectName);
            m_ProjectSourceIcon = new VisualElement() { name = "project-source-icon", pickingMode = PickingMode.Ignore };
            hierarchy.Add(m_ProjectSourceIcon);
            isSourceProject = false;
        }
    }
}
