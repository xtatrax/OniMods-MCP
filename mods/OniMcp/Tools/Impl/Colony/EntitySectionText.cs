using System.Text.RegularExpressions;
namespace OniMcp.Tools
{
    internal static class EntitySectionText
    {
        internal static object NonnegativeOrNull(double value) => double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? null : (object)value;
        internal static string Plain(string text) => text == null ? null : Regex.Replace(text, "<[^>]*>", "");
    }
}
