using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Magix.Utils;
using UnityEngine;
using Magix.Diagnostics;

namespace Magix
{
    [JsonConverter(typeof(UnityScriptableObjectSerializer))]
    public class CloudScriptableObject : ScriptableObject
    {
        [NonSerialized] public bool IsInit = false;
        [NonSerialized] public bool IsInitInProgress = false;
        [NonSerialized] public bool IsExist = false;
        [NonSerialized] public object Original;

        internal static Dictionary<string, string> PreloadedCloudResourceJsons = null;

        public CloudScriptableObject()
        {
            if (InstanceManager.ResourceAPI == null)
            {
                Debug.LogWarning("InstanceManager.ResourceAPI haven't initialized yet. Please initialize before use it");
                return;
            }
        }

        public static void Initialize(Action<bool> callback)
        {
            var handle = ThreadContextManager.GetSynchronizeCallbackHandler(callback);
            InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (res) =>
            {
                if (PreloadedCloudResourceJsons == null)
                {
                    CloudScriptableObject.PreloadedCloudResourceJsons = res
                                    .Where(obj => obj.Key.StartsWith("Production") || obj.Key.StartsWith("Development"))
                                    .ToDictionary(obj => obj.Key, obj => obj.Value);
                }

                handle.Invoke(true);
            });
        }

#if UNITY_EDITOR
        internal void InitializeResource(Action<bool> success, Magix.Editor.Environment environment)
        {
            if (IsInitInProgress)
            {
                return;
            }

            IsInitInProgress = true;

            var key = InstanceManager.Resolver.GetKeyFromObject(this, environment);
            InstanceManager.ResourceAPI.CheckVariableIsExist(InstanceManager.ResourceAPI.EditorUserId, key, (exist) =>
            {
                IsInit = true;
                IsInitInProgress = false;
                success.Invoke(exist);
            });
        }
#endif
    }
}
