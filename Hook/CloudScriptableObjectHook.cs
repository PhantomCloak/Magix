using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Magix
{
    public class CloudScriptableObjectHook
    {
        public static object LoadResource(object obj)
        {
            if (obj == null)
                return null;

            FillChildResource((CloudScriptableObject)obj);

            return obj;
        }

        public static T LoadResourceDirect<T>(string path) where T : Object
        {
            var obj = Resources.Load<T>(path);

            if (typeof(T).IsSubclassOf(typeof(CloudScriptableObject)))
            {
                FillChildResource(obj as CloudScriptableObject);
            }

            return obj;
        }

        public static T[] LoadResourceAllDirect<T>(string path) where T : Object
        {
            var objs = Resources.LoadAll<T>(path);

            if (typeof(T).IsSubclassOf(typeof(CloudScriptableObject)))
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    FillChildResource(objs[i] as CloudScriptableObject);
                }
            }

            return objs;
        }

        private static void FillChildResource(CloudScriptableObject obj)
        {
            if (obj == null)
                return;

            obj = (CloudScriptableObject)InstanceManager.Resolver.Resolve(obj);

            if (!InstanceManager.Resolver.IsNestedResolutionSupported)
                return;

            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                if (typeof(CloudScriptableObject).IsAssignableFrom(field.FieldType))
                {
                    CloudScriptableObject child = field.GetValue(obj) as CloudScriptableObject;
                    FillChildResource(child);
                }
            }
        }

    }
}
