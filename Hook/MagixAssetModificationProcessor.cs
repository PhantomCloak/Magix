#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Magix.Diagnostics;

namespace Magix.Editor
{
    public class MagixAssetModificationProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            var cloudResource = AssetDatabase.LoadAssetAtPath<CloudScriptableObject>(assetPath);

            if (cloudResource != null && cloudResource.IsExist)
            {
                bool deleteResult = EditorUtility.DisplayDialog(
                "Delete CloudResource",
                $"Are you sure you want to delete the CloudResource '{cloudResource.name}'? This action cannot be undone.",
                "Yes, Delete",
                "Cancel");

                if (!deleteResult)
                {
                    MagixLogger.LogVerbose("Deletion of CloudResource cancelled by user: " + cloudResource.name);
                    return AssetDeleteResult.DidNotDelete;
                }

                int ctx = 0;
                foreach (var env in MagixConfig.Environments)
                {
                    InstanceManager.ResourceAPI.DeleteVariableCloud(env + "-res-" + cloudResource.name, (success) =>
                    {
                        ctx++;
                        if (ctx >= MagixConfig.Environments.Length)
                            MagixLogger.LogVerbose("Cloud resource deleted successfully");
                    });
                }


                MagixLogger.LogVerbose("User confirmed deletion of CloudResource: " + cloudResource.name);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<CloudScriptableObject>(sourcePath);

            if (asset == null)
                return AssetMoveResult.DidNotMove;

            string copyPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
            CloudScriptableObject copy = Object.Instantiate(asset);
            AssetDatabase.CreateAsset(copy, copyPath);
            AssetDatabase.SaveAssets();

            Logger.LogWarn("Attempted to rename a CloudScriptableObject. To ensure backward compatibility, a duplicating operation was performed instead.");

            return AssetMoveResult.FailedMove;

        }
    }
}
#endif
