using System;
using System.Collections.Generic;

// Only the game / PLib boundary is stubbed. Tests compile the production config code.
namespace OniMcp.Support
{
    public static class OniMcpPaths
    {
        public static string ConfigPath { get; set; }
        public static string ModPath { get; set; }
    }

    public static class OniMcpLog
    {
        public static void Warning(string message) { }
        public static void Debug(string message) { }
    }
}

namespace OniMcp.Server
{
    public partial class McpHttpServer
    {
        public static McpHttpServer Instance { get; set; }
        public void RestartServer() { }
        internal string SettingsHtmlForTest(string message = null) => RenderSettingsHtml(message);
        internal static string LocalizedTemplateForTest(string template, bool forFormatting) => SettingsHtml(template, forFormatting);
        private static Dictionary<string, string> ParseQueryString(string input) => throw new NotSupportedException();
        private static void SendHtml(System.Net.HttpListenerResponse response, string html, int status) => throw new NotSupportedException();
    }

    internal static class MainThreadBridge
    {
        internal static void EnqueueDeferred(Action callback) => throw new NotSupportedException();
    }

    internal static class HttpRequestBody
    {
        internal const int MaxSettingsBytes = 65536;
        internal static string Read(System.IO.Stream stream, System.Text.Encoding encoding, long length, int max) => throw new NotSupportedException();
    }

    internal sealed class RequestBodyTooLargeException : Exception
    {
    }
}

namespace UnityEngine
{
    public class MonoBehaviour { }
    public static class Application
    {
        public static string LastOpenedUrl { get; private set; }
        public static void OpenURL(string url) { LastOpenedUrl = url; }
    }
}

namespace PeterHan.PLib
{
    public sealed class DynamicOptionAttribute : Attribute
    {
        public DynamicOptionAttribute(Type handler) { }
    }
}

namespace PeterHan.PLib.Options
{
    public interface IOptions
    {
        IEnumerable<IOptionsEntry> CreateOptions();
        void OnOptionsChanged();
    }

    public interface IOptionsEntry { }

    public sealed class ConfigFileAttribute : Attribute
    {
        public ConfigFileAttribute(string filename, bool shared) { }
    }

    public sealed class ModInfoAttribute : Attribute
    {
        public ModInfoAttribute(string url, string image) { }
    }

    public sealed class OptionAttribute : Attribute
    {
        public string Title { get; }
        public string Tooltip { get; }
        public string Category { get; }

        public OptionAttribute(string title, string tooltip, string category)
        {
            Title = title;
            Tooltip = tooltip;
            Category = category;
        }
    }

    public sealed class TextBlockOptionsEntry : IOptionsEntry
    {
        public OptionAttribute Option { get; }
        public TextBlockOptionsEntry(string name, OptionAttribute option) { Option = option; }
    }

    public sealed class ButtonOptionsEntry : IOptionsEntry
    {
        public string Name { get; }
        public OptionAttribute Option { get; }
        public object Value { get; set; }
        public ButtonOptionsEntry(string name, OptionAttribute option)
        {
            Name = name;
            Option = option;
        }
    }
}

namespace OniMcp.Config
{
    public sealed class MaskedTokenOptionsEntry : PeterHan.PLib.Options.IOptionsEntry { }
}

public sealed class Game
{
    public static Game Instance { get; set; }
    public bool Loading { get; set; }
    public bool IsLoading() => Loading;
}
public sealed class Immigration
{
    public static Immigration Instance { get; set; }
    public bool ImmigrantsAvailable { get; set; }
}
namespace OniMcp
{
    internal static class ToolCallSpeechOverlay
    {
        internal static void ShowNearPlayerMouse(string text) { }
        internal static void NotifyMissingDescription(string name) { }
    }
}
