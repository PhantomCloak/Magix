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
            InstanceManager.ResourceAPI.GetAllEntriesUser("5e0d7c88-e14a-4824-8fa4-114bf74ef3ac", (succ, res) =>
            {
                foreach (var item in res)
                {
                    Debug.Log("DEBUG: " + item.Key);
                }
                CloudScriptableObject.PreloadedCloudResourceJsons = res
                    .Where(obj => obj.Key.StartsWith("Production") || obj.Key.StartsWith("Development"))
                    .ToDictionary(obj => obj.Key, obj => obj.Value);

                handle.Invoke(true, string.Empty);
            });
        }

        internal void InitializeResource(Action<bool> success, string environmentPrefix)
        {
            if (IsInitInProgress)
            {
                return;
            }

            IsInitInProgress = true;

            //Logger.LogVerbose("Checking resource if it present...");
            InstanceManager.ResourceAPI.CheckVariableIsExist(environmentPrefix + "res-" + this.name, (suc, exist) =>
            {
                //Logger.LogVerbose("Resource status in the cloud sucess: " + suc + " exist: " + exist);
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
    }
}
