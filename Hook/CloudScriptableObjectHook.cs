using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Magix
{
    public class CloudScriptableObjectHook
    {
        private static Dictionary<string, object> CacheDirect = new Dictionary<string, object>();
        private static Dictionary<string, object> CacheAll = new Dictionary<string, object>();
        private static Dictionary<object, object> CacheMono = new Dictionary<object, object>();

        public static object LoadResource(object obj)
        {
            if (obj == null)
                return null;

            bool existOnCache = CacheMono.ContainsKey(obj);

            if (existOnCache)
            {
                return CacheMono[obj];
            }

            FillChildResource((CloudScriptableObject)obj);

            if (!existOnCache)
            {
                CacheMono.Add(obj, obj);
            }

            return obj;
        }

        public static T LoadResourceDirect<T>(string path) where T : Object
        {
            bool existOnCache = CacheDirect.ContainsKey(path);

            if (existOnCache)
            {
                return (T)CacheDirect[path];
            }

            var obj = Resources.Load<T>(path);

            if (typeof(T).IsSubclassOf(typeof(CloudScriptableObject)))
            {
                FillChildResource(obj as CloudScriptableObject);
            }

            if (!existOnCache)
            {
                CacheDirect.Add(path, obj as object);
            }

            return obj;
        }

        public static T[] LoadResourceAllDirect<T>(string path) where T : Object
        {
            bool existOnCache = CacheAll.ContainsKey(path);

            if (existOnCache)
            {
                return (T[])CacheAll[path];
            }

            var objs = Resources.LoadAll<T>(path);

            if (typeof(T).IsSubclassOf(typeof(CloudScriptableObject)))
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    FillChildResource(objs[i] as CloudScriptableObject);
                }
            }

            if (!existOnCache)
            {
                CacheAll.Add(path, objs);
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
