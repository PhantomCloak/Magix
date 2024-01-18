using System;
using System.Collections.Generic;

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
