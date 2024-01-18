using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Magix
{
    public class DefaultCloudScriptableObjectResolver : ICloudScriptableObjectResolver
    {
        public bool IsNestedResolutionSupported { get => false; }

        public object Resolve(CloudScriptableObject scriptableObj)
        {
			string environmentSearchStr = string.Empty;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
			environmentSearchStr = Environment.Development.ToString();
#else
			environmentSearchStr = Environment.Production.ToString();
#endif
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
            string assetPath = AssetDatabase.GetAssetPath(scriptableObj);
            assetPath = assetPath.Replace("Assets/Resources/", string.Empty);
            assetPath = assetPath.Replace(".asset", string.Empty);
            return $"{env}-res-{assetPath}";
        }
#endif
    }
}
