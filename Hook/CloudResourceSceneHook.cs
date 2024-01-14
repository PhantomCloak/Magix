using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Magix
{
    public static class CloudResourceSceneHook
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void OnBootHook()
        {
            Logger.Log("HEllo");
            SceneManager.sceneUnloaded += OnSceneLoading;
        }

        private static void OnSceneLoading(Scene scene)
        {
            InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (succ, res) =>
            {
                //            InstanceManager.ResourceAPI.PreloadedCloudResourceJsons = res
                //                .Where(obj => obj.Key.StartsWith("Production"))
                //                .ToDictionary(obj => obj.Key, obj => obj.Value);
                //
            });
        }
    }


    [InitializeOnLoad]
    public class EditorStartup
    {
        static EditorStartup()
        {
            Debug.Log("HELLLLLLL");
            return;
            if (!InstanceManager.ResourceAPI.IsLoggedIn)
            {
                InstanceManager.ResourceAPI.EditorLogin(() =>
                {
                    InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (status, result) =>
                    {

                    });
                });
            }
        }

    }
}
