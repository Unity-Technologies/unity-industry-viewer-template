using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Industry.Viewer.Shared.Editor
{
    [CustomEditor(typeof(PlatformServicesInitialization))]
    public class PlatformServicesInitializationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var scriptField = new PropertyField(serializedObject.FindProperty("m_Script"));
            scriptField.SetEnabled(false);
            root.Add(scriptField);

            root.Add(new PropertyField(serializedObject.FindProperty("serviceAccountCredentials")));

            var vpcProp = serializedObject.FindProperty("vpcCredentials");
            root.Add(new PropertyField(vpcProp));

            var pinCertProp = serializedObject.FindProperty("pinCertificate");
            var pinCertField = new PropertyField(pinCertProp);
            pinCertField.style.display = vpcProp.objectReferenceValue != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            root.Add(pinCertField);

            root.TrackPropertyValue(vpcProp, p =>
            {
                var hasVpc = p.objectReferenceValue != null;
                pinCertField.style.display = hasVpc ? DisplayStyle.Flex : DisplayStyle.None;
                if (!hasVpc)
                {
                    pinCertProp.boolValue = false;
                    serializedObject.ApplyModifiedProperties();
                }
            });

            return root;
        }
    }
}
