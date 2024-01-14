
public static class InstanceManager
{
    private static ICloudResourceAPI m_ResourceAPI;
    public static ICloudResourceAPI ResourceAPI
    {
        get
        {
            if (m_ResourceAPI == null)
                return new CloudApi();
            return m_ResourceAPI;
        }
        set => m_ResourceAPI = value;
    }
}
