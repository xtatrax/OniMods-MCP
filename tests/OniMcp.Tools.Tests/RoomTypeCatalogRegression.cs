using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

// Only the initialized game/database boundary is replaced. The catalog handler
// is compiled from production and does not require any established Room objects.
internal static class RoomTypeCatalogRegression
{
    internal static void Run()
    {
        Game.Instance = null;
        Db.Reads = 0;
        Check(RoomTypeCatalogTools.ReadRoomTypes(null).IsError && Db.Reads == 0,
            "menu reads must not initialize the database");
        Game.Instance = new Game();
        Check(RoomTypeCatalogTools.ReadRoomTypes(null).IsError && Db.Reads == 0,
            "loading-screen reads must not initialize the database before the room system exists");
        Game.Instance.roomProber = new object();
        Db.Current = new Db();
        Check(RoomTypeCatalogTools.ReadRoomTypes(null).IsError,
            "uninitialized room database must return a controlled error");

        var neutral = new RoomType { Id = "Neutral", Name = "多目的室", CriteriaText = "", EffectsText = "効果なし" };
        var hall = new RoomType
        {
            Id = "GreatHall", Name = "大広間", description = "食事をする部屋",
            CriteriaText = "12～120タイル\n食卓\n娯楽設備\n装飾品",
            EffectsText = "士気 +6", effect = "Morale", effects = new[] { "GreatHall" }, priority = 2,
            category = new RoomType { Id = "Food", Name = "食事" }
        };
        Db.Current.RoomTypes = new RoomTypeDatabase { resources = new List<RoomType> { neutral, null, hall } };
        JObject all = Body(RoomTypeCatalogTools.ReadRoomTypes(null));
        Check((int)all["matched"] == 2 && (int)all["returned"] == 2,
            "definitions must include unestablished room types and ignore null entries");
        Check((string)all["scope"] == "room_type_definitions", "definitions must not masquerade as room state");
        Check((string)all["roomTypes"][1]["criteria"] == hall.CriteriaText
            && (string)all["roomTypes"][1]["effectText"] == hall.EffectsText,
            "native localized criteria and effects must be preserved");
        Check((string)all["roomTypes"][1]["categoryId"] == "Food"
            && (string)all["roomTypes"][1]["effects"][0] == "GreatHall",
            "category and effect identifiers must be preserved");

        hall.CriteriaReads = 0;
        JObject limited = Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{limit:1}")));
        Check((int)limited["matched"] == 2 && (int)limited["returned"] == 1
            && (int)limited["truncated"] == 1 && hall.CriteriaReads == 0,
            "limits must bound formatting while reporting all matches");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{limit:0}")))["returned"] == 1,
            "nonpositive limits must clamp to one");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{type:'greathall'}")))["returned"] == 1,
            "type id search must ignore case");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{query:'大広'}")))["returned"] == 1,
            "localized names must be searchable");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{includeNeutral:false}")))["returned"] == 1,
            "neutral definitions must be optionally excluded");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{type:'GreatHall',query:'多目的'}")))["matched"] == 0,
            "type and query filters must both apply");
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{query:'missing'}")))["matched"] == 0,
            "unknown type searches must return an empty catalog");
        var many = new List<RoomType>();
        for (int index = 0; index < 400; index++)
            many.Add(new RoomType { Id = "Type" + index, Name = "Room " + index });
        Db.Current.RoomTypes.resources = many;
        Check((int)Body(RoomTypeCatalogTools.ReadRoomTypes(null))["returned"] == 80,
            "default catalog limit must remain 80");
        JObject capped = Body(RoomTypeCatalogTools.ReadRoomTypes(JObject.Parse("{limit:999}")));
        Check((int)capped["returned"] == 300 && (int)capped["truncated"] == 100,
            "oversized catalog requests must clamp to 300 and expose truncation");
        Game.Instance = null;
        Db.Current = null;
        Console.WriteLine("PASS: room type catalog host regression (game database boundary substituted)");
    }

    private static JObject Body(OniMcp.Core.CallToolResult result)
    {
        Check(!result.IsError, "catalog read failed");
        return JObject.Parse(result.Content[0].Text);
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}

internal sealed partial class Game
{
    internal static Game Instance { get; set; }
    internal object roomProber { get; set; }
}

internal sealed class Db
{
    internal static Db Current { get; set; }
    internal static int Reads { get; set; }
    internal RoomTypeDatabase RoomTypes { get; set; }
    internal static Db Get() { Reads++; return Current; }
}

internal sealed class RoomTypeDatabase
{
    internal List<RoomType> resources { get; set; }
}

internal sealed class RoomType
{
    internal string Id { get; set; }
    internal string Name { get; set; }
    internal RoomType category { get; set; }
    internal string description { get; set; }
    internal string effect { get; set; }
    internal string[] effects { get; set; }
    internal int priority { get; set; }
    internal string CriteriaText { get; set; }
    internal string EffectsText { get; set; }
    internal int CriteriaReads { get; set; }
    internal string GetCriteriaString() { CriteriaReads++; return CriteriaText; }
    internal string GetRoomEffectsString() { return EffectsText; }
}
