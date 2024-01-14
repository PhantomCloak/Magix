#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

public class CloudResourceBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }

        string filePath = Path.Combine(resourcesPath, "editor_id.txt");

        File.WriteAllText(filePath, InstanceManager.ResourceAPI.EditorUserId);

        AssetDatabase.Refresh();
    }
}
#endif

