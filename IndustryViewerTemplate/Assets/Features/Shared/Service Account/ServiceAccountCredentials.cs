using UnityEngine;
using System;

namespace Unity.Industry.Viewer.Shared
{
    [CreateAssetMenu(fileName = "ServiceAccountCredentials", menuName = "UC/Assets/Service Account Credentials")]
    public class ServiceAccountCredentials : ScriptableObject
    {
        [SerializeField] private string serviceAccountKey;
        [SerializeField] private string serviceAccountSecret;

        public string Credentials => string.IsNullOrEmpty(serviceAccountKey) || string.IsNullOrEmpty(serviceAccountSecret) ?
            string.Empty : ToBase64($"{serviceAccountKey}:{serviceAccountSecret}");
        
        public string OrganizationId => organizationId;
        public string OrganizationName => organizationName;
        
        [Tooltip("Unity Cloud Organization ID to be used for finding assets.")]
        [SerializeField] private string organizationId;
        [Tooltip("Unity Cloud Organization ID to be used for finding assets.")]
        [SerializeField] private string organizationName;

        private string ToBase64(string value)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        }
    }
}
