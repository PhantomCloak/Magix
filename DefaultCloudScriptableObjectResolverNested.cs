using System.Linq;
using UnityEngine;

namespace Magix
{
    public class DefaultCloudScriptableObjectResolverNested : ICloudScriptableObjectResolver
    {
        public bool IsNestedResolutionSupported { get => true; }

        public object Resolve(CloudScriptableObject scriptableObj)
        {
			string environmentSearchStr = string.Empty;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
			environmentSearchStr = Environment.Development.ToString();
#else
			environmentSearchStr = Environment.Production.ToString();
#endif
            var duplicateKeyName = CloudScriptableObject.PreloadedCloudResourceJsons.Count(x => x.Key.StartsWith(environmentSearchStr) && x.Key.EndsWith(scriptableObj.name)) > 1;

            if (duplicateKeyName)
            {
                UnityEngine.Debug.LogError("Duplicate key exist on resolving the object, this likely to cause undefined behaviour.");
            }

            var searchKey = CloudScriptableObject.PreloadedCloudResourceJsons.FirstOrDefault(x => x.Key.StartsWith(environmentSearchStr) &&  x.Key.EndsWith(scriptableObj.name)).Key;

            if (searchKey == null)
            {
                UnityEngine.Debug.Log("Cannot find entry: " + searchKey);
                return null;
            }

            JsonUtility.FromJsonOverwrite(CloudScriptableObject.PreloadedCloudResourceJsons[searchKey], scriptableObj);

            return scriptableObj;
        }

#if UNITY_EDITOR
        public string GetKeyFromObject(CloudScriptableObject scriptableObj, Environment env)
        {
            return $"{env}-res-{scriptableObj.name}";
        }
#endif
    }
}
