// Only Unity's persistent-directory boundary is replaced. The path resolver is production code.
namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath { get; set; }
    }
}
