#if UNITY_EDITOR
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Magix
{
    public static class CloudScriptableObjectSceneHook
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void OnBootHook()
        {
            SceneManager.sceneUnloaded += OnSceneLoading;
        }

        private static void OnSceneLoading(Scene scene)
        {
        }
    }


    [InitializeOnLoad]
    public class EditorStartup
    {
        private static Queue<Action> mainThreadActions = new Queue<Action>();

        static EditorStartup()
        {
            if (!InstanceManager.ResourceAPI.IsLoggedIn)
            {
                InstanceManager.ResourceAPI.EditorLogin(() =>
                {
                    InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (status, result) =>
                    {
                        foreach (var resource in result)
                        {
                            if (!(resource.Key.StartsWith("Production") || resource.Key.StartsWith("Development")))
                                continue;

                            var resourcePath = resource.Key.Split('-').Last();

                            mainThreadActions.Enqueue(() =>
                            {
                                if (!Resources.Load(resourcePath))
                                {
                                    Logger.LogWarn($"CloudResource '{resourcePath}' is referenced in the cloud but not found in the local Resources directory.");
                                }
                            });
                        }
                    });
                });
            }
            EditorApplication.update += ProcessMainThreadActions;
        }

        private static void ProcessMainThreadActions()
        {
            while (mainThreadActions.Count > 0)
            {
                Action action = mainThreadActions.Dequeue();
                action?.Invoke();
            }
        }
    }
}
#endif
