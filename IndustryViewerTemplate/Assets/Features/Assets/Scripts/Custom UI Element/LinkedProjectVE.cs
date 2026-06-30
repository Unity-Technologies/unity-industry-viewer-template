using Unity.AppUI.UI;
using UnityEngine.UIElements;
using UnityEngine;

namespace Unity.Industry.Viewer.Assets
{
    [UxmlElement("LinkedProject")]
    public partial class LinkedProjectVE : VisualElement
    {
        private static readonly Color k_ProjectDefaultColor = new Color32(74, 74, 74, 255);
        private static readonly Color[] k_ProjectColors = new[]
        {
                HexToColor("#E93D82"),
                HexToColor("#FE6D15F7"),
                HexToColor("#FFA600"),
                HexToColor("#12A594"),
                HexToColor("#3E63DD"),
                HexToColor("#6E56CF")
        };

        private static Color GetProjectColor(string projectId)
        {
            if (string.IsNullOrEmpty(projectId) || projectId.Length == 0)
            {
                return k_ProjectDefaultColor;
            }

            // Get the last character of the project ID
            char lastChar = projectId[projectId.Length - 1];

            // Get the unicode value of the last character and use it as an index with modulo
            int colorIndex = lastChar % k_ProjectColors.Length;
            return k_ProjectColors[colorIndex];
        }

        // Helper method to convert hex color strings to Unity Color
        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            return k_ProjectDefaultColor;
        }

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

        public void SetColorFromProjectId(string projectId)
        {
            projectIconColor = GetProjectColor(projectId);
        }
    }
}
