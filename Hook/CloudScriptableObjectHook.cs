using System.Reflection;

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

            obj = (CloudScriptableObject)InstanceManager.Resolver.Resolve(obj);

            if (!InstanceManager.Resolver.IsNestedResolutionSupported)
                return;

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
