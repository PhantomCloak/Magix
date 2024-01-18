using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Magix
{
    [JsonConverter(typeof(UnityScriptableObjectSerializer))]
    [CreateAssetMenu(fileName = "CResource", menuName = "New Cloud Resource")]
    public class CloudScriptableObject : ScriptableObject
    {
        [NonSerialized] public bool IsInit = false;
        [NonSerialized] public bool IsInitInProgress = false;
        [NonSerialized] public bool IsExist = false;
        [NonSerialized] public object Original;

        public static Dictionary<string, string> PreloadedCloudResourceJsons { get; set; }

        public CloudScriptableObject()
        {
            if (InstanceManager.ResourceAPI == null)
            {
                Debug.LogWarning("InstanceManager.ResourceAPI haven't initialized yet. Please initialize before use it");
                return;
            }

#if UNITY_EDITOR
            if (!InstanceManager.ResourceAPI.IsLoggedIn)
            {
                InstanceManager.ResourceAPI.EditorLogin();
            }
#endif
        }

        public static void Initialize(Action<bool, string> callback)
        {
            var handle = ThreadContextManager.GetSynchronizeCallbackHandler(callback);
            TextAsset textFile = Resources.Load<TextAsset>("editor_id");
            InstanceManager.ResourceAPI.GetAllEntriesUser(InstanceManager.ResourceAPI.EditorUserId, (succ, res) =>
            {
                CloudScriptableObject.PreloadedCloudResourceJsons = res
                    .Where(obj => obj.Key.StartsWith("Production") || obj.Key.StartsWith("Development"))
                    .ToDictionary(obj => obj.Key, obj => obj.Value);

                handle.Invoke(true, string.Empty);
            });
        }

#if UNITY_EDITOR
        internal void InitializeResource(Action<bool> success, Environment environment)
        {
            if (IsInitInProgress)
            {
                return;
            }

            IsInitInProgress = true;

            InstanceManager.ResourceAPI.CheckVariableIsExist(InstanceManager.ResourceAPI.EditorUserId,
                    InstanceManager.Resolver.GetKeyFromObject(this, environment),
                    (suc, exist) =>
            {
                IsInit = true;
                IsInitInProgress = false;

                if (!suc)
                {
                    success.Invoke(false);
                    return;
                }

                IsExist = exist;
                success.Invoke(true);
            });
        }
#endif
    }
}
