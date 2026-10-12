using System.Reflection;

namespace OniMcp.Support
{
    internal static class OniMcpVersion
    {
        internal static readonly string Display =
            typeof(OniMcpVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(OniMcpVersion).Assembly.GetName().Version?.ToString(3)
            ?? "unknown";
    }
}
