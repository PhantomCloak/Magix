using System;
using System.Linq;
using Magix.Diagnostics;

namespace Magix
{
    public static class InstanceManager
    {
        private static ICloudResourceAPI m_ResourceAPI;
        public static ICloudScriptableObjectResolver Resolver => new DefaultCloudScriptableObjectResolver();

        public static ICloudResourceAPI ResourceAPI
        {
            get
            {
#if UNITY_EDITOR
                if (m_ResourceAPI == null)
                {
                    var apiType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes())
                        .FirstOrDefault(type => type.IsClass && !type.IsAbstract && typeof(ICloudResourceAPI).IsAssignableFrom(type));

                    if (apiType != null)
                    {
                        m_ResourceAPI = (ICloudResourceAPI)Activator.CreateInstance(apiType);
                        MagixLogger.LogVerbose($"Created an instance of {apiType.FullName} for ICloudResourceAPI.");
                    }
                }
#endif

                return m_ResourceAPI;
            }
            set => m_ResourceAPI = value;
        }
    }
}
