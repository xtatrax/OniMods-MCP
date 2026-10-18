using System;
using System.Collections.Generic;
using System.Text;

internal readonly struct HashedString : IEquatable<HashedString>
{
    private readonly int value;
    internal HashedString(int value) { this.value = value; }
    public bool Equals(HashedString other) => value == other.value;
    public override bool Equals(object other) => other is HashedString && Equals((HashedString)other);
    public override int GetHashCode() => value;
    public static bool operator ==(HashedString left, HashedString right) => left.Equals(right);
    public static bool operator !=(HashedString left, HashedString right) => !left.Equals(right);
}
internal static class OverlayModes
{
    internal static class None { internal static readonly HashedString ID = new HashedString(0); }
    internal static class Power { internal static readonly HashedString ID = new HashedString(1); }
    internal static class LiquidConduits { internal static readonly HashedString ID = new HashedString(2); }
    internal static class GasConduits { internal static readonly HashedString ID = new HashedString(3); }
    internal static class Logic { internal static readonly HashedString ID = new HashedString(4); }
    internal static class SolidConveyor { internal static readonly HashedString ID = new HashedString(5); }
    internal static class Temperature { internal static readonly HashedString ID = new HashedString(6); }
    internal static class Oxygen { internal static readonly HashedString ID = new HashedString(7); }
    internal static class Light { internal static readonly HashedString ID = new HashedString(8); }
    internal static class Decor { internal static readonly HashedString ID = new HashedString(9); }
    internal static class Disease { internal static readonly HashedString ID = new HashedString(10); }
    internal static class Radiation { internal static readonly HashedString ID = new HashedString(11); }
    internal static class TileMode { internal static readonly HashedString ID = new HashedString(12); }
    internal static class Crop { internal static readonly HashedString ID = new HashedString(13); }
    internal static class Harvest { internal static readonly HashedString ID = new HashedString(14); }
    internal static class Rooms { internal static readonly HashedString ID = new HashedString(15); }
}
internal static class Grid
{
    internal const int WidthInCells = 100;
    internal const int HeightInCells = 100;
    internal static byte[] Visible = new byte[WidthInCells * HeightInCells];
    internal static bool[] Solid = new bool[WidthInCells * HeightInCells];
    internal static int[] WorldIdx = new int[WidthInCells * HeightInCells];
    internal static bool[] WorldValid = new bool[WidthInCells * HeightInCells];
    internal static bool IsValidCell(int cell) => cell >= 0 && cell < WidthInCells * HeightInCells;
    internal static bool IsWorldValidCell(int cell) => IsValidCell(cell) && WorldValid[cell];
    internal static bool IsVisible(int cell) => Visible[cell] != 0;
    internal static int XYToCell(int x, int y) => x + y * WidthInCells;
}
internal sealed class ClusterManager
{
    internal static ClusterManager Instance;
    internal int activeWorldId;
}
internal sealed class Game
{
    internal static Game Instance;
    internal EntombedItemVisualizer Visualizer;
    internal int ComponentReads;
    internal T GetComponent<T>() where T : class { ComponentReads++; return Visualizer as T; }
}
internal sealed class EntombedItemVisualizer
{
    internal readonly HashSet<int> Cells = new HashSet<int>();
    internal readonly List<int> Queries = new List<int>();
    internal bool IsEntombedItem(int cell) { Queries.Add(cell); return Cells.Contains(cell); }
}
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static string MarkerTest(int cell) => ReadBuriedMarker(cell);
        internal static string MarkerSummaryTest(int x1, int x2, int y1, int y2)
        {
            var text = new StringBuilder();
            AppendBuriedMarkerSummary(text, x1, x2, y1, y2);
            return text.ToString();
        }
    }
}
