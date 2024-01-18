using System.Collections.Generic;
using Magix.Editor;

namespace Magix
{
    public interface ICloudScriptableObjectResolver
    {
        public bool IsNestedResolutionSupported { get; }

        public object Resolve(CloudScriptableObject scriptableObj);
#if UNITY_EDITOR
        public string GetKeyFromObject(CloudScriptableObject scriptableObj, Environment env);
#endif
    }
}
