using UnityEngine;
using Unity.Netcode;

namespace Unity.Industry.Viewer.Vivox
{
    public class VivoxSyncAvatar : NetworkBehaviour
    {
        [SerializeField]
        private Transform mouthTransform;
        
        private NetworkVariable<float> m_VoiceLevel = new NetworkVariable<float>();
        
        public override void OnNetworkSpawn()
        {
            m_VoiceLevel.OnValueChanged += OnVoiceLevelChanged;
            if(!IsOwner) return;
            VivoxController.ParticipantAudioEnergyChanged += OnParticipantAudioEnergyChanged;
        }
        
        public override void OnNetworkDespawn()
        {
            m_VoiceLevel.OnValueChanged -= OnVoiceLevelChanged;
            if(!IsOwner) return;
            VivoxController.ParticipantAudioEnergyChanged -= OnParticipantAudioEnergyChanged;
        }

        private void OnVoiceLevelChanged(float previousValue, float newValue)
        {
            mouthTransform.localScale = new Vector3(1f, newValue, 1f);
        }

        private void OnParticipantAudioEnergyChanged(double energy)
        {
            if(!IsOwner) return;
            m_VoiceLevel.Value = (float)energy;
            m_VoiceLevel.CheckDirtyState();
        }
    }
}
