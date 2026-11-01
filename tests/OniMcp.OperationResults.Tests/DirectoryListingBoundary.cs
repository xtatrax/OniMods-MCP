using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    // Only native list inputs and filename/name access are substituted.
    // The listing, directory predicate, browser and normalizer are complete production files.
    internal static class DirectoryListingBoundary
    {
        internal static readonly List<DirectoryListingBuilding> Buildings = new List<DirectoryListingBuilding>();
        internal static int BuildingEnumerations;
        internal static int FileReads;
    }

    internal sealed class DirectoryListingBuilding
    {
        internal DirectoryListingBuilding(string name, string fileName) { Name = name; FileName = fileName; }
        internal string Name { get; private set; }
        internal string FileName { get; private set; }
        internal string GetProperName() => Name;
    }

    public static partial class WorldEditorTools
    {
        private static IEnumerable<DirectoryListingBuilding> LiveCompletedBuildings()
        {
            DirectoryListingBoundary.BuildingEnumerations++;
            return DirectoryListingBoundary.Buildings.ToArray();
        }
        private static string GetBuildingDetailFileName(DirectoryListingBuilding building) => building.FileName;
    }

    // These native branches are outside this host test; accidental calls must fail.
    internal sealed class ClusterManager
    {
        internal static ClusterManager Instance => null;
        internal int activeWorldId => throw new NotSupportedException();
        internal DirectoryListingWorld GetWorld(int worldId) => throw new NotSupportedException();
    }
    internal sealed class DirectoryListingWorld { internal DirectoryListingSize WorldSize => throw new NotSupportedException(); }
    internal struct DirectoryListingSize { internal int y => throw new NotSupportedException(); }
    internal static class SaveLoader
    {
        internal enum SaveType { both }
        internal struct SaveFileEntry
        {
            internal string path { get; set; }
            internal DateTime timeStamp { get; set; }
        }
        internal static List<SaveFileEntry> GetAllFiles(bool sort, SaveType type) => throw new NotSupportedException();
        internal static string GetActiveSaveFilePath() => throw new NotSupportedException();
    }
}

namespace OniMcp.Support
{
    internal static class OniMcpLog
    {
        internal static void Warning(string message) => throw new NotSupportedException(message);
    }
}
