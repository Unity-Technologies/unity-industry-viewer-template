using Unity.Cloud.Collaboration;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
#if VR_MODE
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
#endif

namespace Unity.Industry.Viewer.Streaming.Annotation
{
    public class SpatialMarkupController : MonoBehaviour
    {
        private static CollaborationUIHelper m_CollaborationUIHelper;
        private const string k_Selected = "Selected";
        private const string k_Hover = "Hover";
        private const string k_BG = "BG";
        private const string k_Cursor = "cursor--pointer";
        
        private IAnnotation _annotation;
        private ISpatial3DAttachment _attachment;
        
        [SerializeField]
        private UIDocument _document;

        private List<VisualElement> bgs;
        
        public void Initialize(IAnnotation annotation, ISpatial3DAttachment attachment, CollaborationUIHelper collaborationUIHelper)
        {
            if (m_CollaborationUIHelper == null || m_CollaborationUIHelper != null && m_CollaborationUIHelper != collaborationUIHelper)
            {
                m_CollaborationUIHelper = collaborationUIHelper;
            }

            _annotation = annotation;
            _attachment = attachment;
            bgs = _document.rootVisualElement.Query<VisualElement>(className: k_BG).ToList();
            _document.rootVisualElement.RegisterCallback<ClickEvent>(OnClick);
            _document.rootVisualElement.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _document.rootVisualElement.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            foreach (var bg in bgs)
            {
                if (!bg.ClassListContains(k_Cursor))
                {
                    bg.AddToClassList(k_Cursor);
                }
            }
            #if VR_MODE
            if (TryGetComponent(out XRSimpleInteractable interactable))
            {
                interactable.enabled = true;
            }
            if(TryGetComponent(out XRPokeFilter pokeFilter))
            {
                pokeFilter.enabled = true;
            }
            #endif
        }

        private void OnDestroy()
        {
            if(_document == null) return;
            _document?.rootVisualElement?.UnregisterCallback<ClickEvent>(OnClick);
            _document?.rootVisualElement?.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            _document?.rootVisualElement?.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            Hover(false);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            Hover(true);
        }
        
        private void OnClick(ClickEvent evt)
        {
            Hover(false);
            Select(true);
            m_CollaborationUIHelper.OpenRootThread(_annotation);
        }
        
        private void AddRemoveClass(string className, bool add)
        {
            if(bgs == null) return;
            foreach (var bg in bgs)
            {
                if (add)
                {
                    if (!bg.ClassListContains(className))
                    {
                        bg.AddToClassList(className);
                    }
                }
                else
                {
                    if(bg.ClassListContains(className))
                    {
                        bg.RemoveFromClassList(className);
                    }
                }
            }
        }

        private void Hover(bool value)
        {
            AddRemoveClass(k_Hover, value);
        }

        public void Select(bool value)
        {
            AddRemoveClass(k_Selected, value);
        }

        public void UpdateAnnotation(IAnnotation annotation)
        {
            _annotation = annotation;
        }
    }
}
