
using System.Reflection;
using UnityEngine;

namespace Magix
{
    public class CloudResourceHook
    {
        public static object LoadResource(object obj)
        {
            if (obj == null)
                return null;

            var targetObject = (CloudScriptableObject)obj;

            var result = Resources.Load(targetObject.name);

            string prefix = "";
#if DEVELOPMENT_BUILD
			prefix = "Development-";
#else
            prefix = "Production-";
#endif

            JsonUtility.FromJsonOverwrite(CloudScriptableObject.PreloadedCloudResourceJsons[prefix + "res-" + targetObject.name], result);

			//
			FillChildResource((CloudScriptableObject)obj);

            return obj;
        }

        private static void FillChildResource(CloudScriptableObject obj)
        {
            if (obj == null)
                return;

            string prefix = "";
#if DEVELOPMENT_BUILD
			prefix = "Development-";
#else
            prefix = "Production-";
#endif
            JsonUtility.FromJsonOverwrite(CloudScriptableObject.PreloadedCloudResourceJsons[prefix + "res-" + obj.name], obj);


            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                // Check if the field is of type CloudScriptableObject or derived from it
                if (typeof(CloudScriptableObject).IsAssignableFrom(field.FieldType))
                {
                    CloudScriptableObject child = field.GetValue(obj) as CloudScriptableObject;

                    // Recursively process the child
                    FillChildResource(child);
                }
            }
        }

    }
}
