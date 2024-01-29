#if UNITY_EDITOR
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.Collections.Generic;
using System;
using Magix.Diagnostics;

namespace Magix.Editor
{
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
                    InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (result) =>
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
                                    MagixLogger.LogWarn($"CloudResource '{resourcePath}' is referenced in the cloud but not found in the local Resources directory.");
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
