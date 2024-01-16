using System.Reflection;
using System.Linq;
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

        private static void FillChildResource(CloudScriptableObject obj)
        {
            if (obj == null)
                return;

            var searchKey = CloudScriptableObject.PreloadedCloudResourceJsons.FirstOrDefault(x => x.Key.EndsWith(obj.name)).Key;

            if (searchKey == null)
            {
                Debug.Log("Cannot find entry: " + searchKey);
                return;
            }

            JsonUtility.FromJsonOverwrite(CloudScriptableObject.PreloadedCloudResourceJsons[searchKey], obj);


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
