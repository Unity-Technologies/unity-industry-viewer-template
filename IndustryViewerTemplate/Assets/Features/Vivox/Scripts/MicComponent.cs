using Unity.AppUI.UI;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Vivox
{
    public class MicComponent : IconButton
    {
        static readonly string k_MicrophoneContainerUssClassName = "container__microphone";
        static readonly string k_MicrophoneVoiceLevelUssClassName = "image__microphone-voice-level";
        static readonly string k_MicrophoneIconName = "microphone";
        static readonly string k_MicrophoneSlashIconName = "microphone-slash";
        readonly VoiceLevelIndicator m_VoiceLevelIndicator;
        
        public MicComponent(bool muted, Action clickEvent = null)
            : base("", clickEvent)
        {
            AddToClassList("mic");

            quiet = true;
            
            var microphoneIcon = this.Q<Icon>(iconUssClassName);
            
            var microphoneContainer = new VisualElement { name = k_MicrophoneContainerUssClassName, pickingMode = PickingMode.Ignore};
            microphoneContainer.AddToClassList(k_MicrophoneContainerUssClassName);

            m_VoiceLevelIndicator = new VoiceLevelIndicator { name = k_MicrophoneVoiceLevelUssClassName , pickingMode = PickingMode.Ignore };
            m_VoiceLevelIndicator.AddToClassList(k_MicrophoneVoiceLevelUssClassName);

            var localScale = Vector3.one;
            localScale.y = 0;
            m_VoiceLevelIndicator.style.scale = new Scale(localScale);

            microphoneContainer.hierarchy.Add(m_VoiceLevelIndicator);
            microphoneIcon.parent?.hierarchy.Add(microphoneContainer);
            microphoneContainer.hierarchy.Add(microphoneIcon);

            SetMuted(muted);
        }
        
        public void SetMuted(bool muted)
        {
            icon = muted ? k_MicrophoneSlashIconName : k_MicrophoneIconName;
            m_VoiceLevelIndicator.style.display = !muted? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void SetVoiceLevel(float voiceLevel)
        {
            m_VoiceLevelIndicator.SetVoiceLevel(voiceLevel);
        }
    }
}
