using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;
using UnityEditor;

namespace Unity.Industry.Viewer.Streaming.Editor
{
    public class IncludeShader : IPreprocessBuildWithReport
    {
     
        /*
         * This script is used to make sure to include the axis handle shader in the graphic settings
         */
        
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string shaderName = "TransformGizmo";
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError($"Shader {shaderName} not found");
                return;
            }
            
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            bool hasShader = false;
            
            SerializedProperty arrayElem;
            
            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }
            
            if(hasShader) return;
            int arrayIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(arrayIndex);
            arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
            arrayElem.objectReferenceValue = shader;

            serializedObject.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
        }
    }
}
