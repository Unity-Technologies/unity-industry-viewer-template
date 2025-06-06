using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Unity.Cloud.AppLinking.Runtime;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class PreBuildNamespace : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    //Make a unique namespace for each build this is for web redirecting to the correct build in machine
    public void OnPreprocessBuild(BuildReport report)
    {
        // Get the current BuildTargetGroup
        BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
        
        // Convert BuildTargetGroup to NamedBuildTarget
        NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
        
        string namespaceString = PlayerSettings.GetApplicationIdentifier(namedBuildTarget).Replace(" ", "-");
        
        string[] parts = namespaceString.Split('.');
        string result = string.Concat(parts.Select(part => part[0]));

        // Access the version from player settings
        string productVersion = PlayerSettings.bundleVersion.Replace(".", "-");
        
        string dateTimeString = DateTime.Now.ToString("yyyyMMddHHmm");
        //string hashedString = HashAndLimit(dateTimeString, 7);

        var newInstanceId = $"{result}-{productVersion}-{dateTimeString}";
        // Append the product version to the defined namespace.
        UnityCloudPlayerSettings.Instance.SetAppNamespace(newInstanceId);
        
        AssetDatabase.SaveAssets();
    }
    
    private static string HashAndLimit(string input, int length)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            string base64Hash = Convert.ToBase64String(hashBytes);
            return Regex.Replace(base64Hash.Substring(0, length), @"[^a-zA-Z0-9\-]", "-");;
        }
    }
}