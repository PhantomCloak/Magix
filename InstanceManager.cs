using System;
using System.Linq;

namespace Magix
{
    public static class InstanceManager
    {
        private static ICloudResourceAPI m_ResourceAPI;
        public static ICloudScriptableObjectResolver Resolver => new DefaultCloudScriptableObjectResolverNested();

        public static ICloudResourceAPI ResourceAPI
        {
            get
            {
                if (m_ResourceAPI == null)
                {
                    var apiType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes())
                        .FirstOrDefault(type => type.IsClass && !type.IsAbstract && typeof(ICloudResourceAPI).IsAssignableFrom(type));

                    if (apiType != null)
                    {
                        m_ResourceAPI = (ICloudResourceAPI)Activator.CreateInstance(apiType);
						Logger.LogVerbose($"Created an instance of {apiType.FullName} for ICloudResourceAPI.");
                    }
                }

                return m_ResourceAPI;
            }
            set => m_ResourceAPI = value;
        }
    }
}
