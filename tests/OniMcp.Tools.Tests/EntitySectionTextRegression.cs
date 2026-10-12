using System;
using OniMcp.Tools;
internal static class EntitySectionTextRegression
{
    internal static void Run()
    {
        if (EntitySectionText.Plain("充電量が<link=green>グリーン</link>で発電機を止めます。") != "充電量がグリーンで発電機を止めます。") throw new Exception("description lost surrounding text");
        if (EntitySectionText.Plain("<b>電力</b>: 10 kJ\n<color=red>熱</color>: 1.25 kDTU/s") != "電力: 10 kJ\n熱: 1.25 kDTU/s") throw new Exception("multiple descriptor values lost");
        if (EntitySectionText.Plain(null) != null) throw new Exception("missing text must remain unknown");
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
            if (EntitySectionText.NonnegativeOrNull(value) != null) throw new Exception("non-finite/sentinel work number treated as observed");
        if (!Equals(EntitySectionText.NonnegativeOrNull(0), 0d) || !Equals(EntitySectionText.NonnegativeOrNull(30), 30d)) throw new Exception("valid work number changed");
        Console.WriteLine("Entity section text: surrounding sentences and descriptor values preserved");
    }
}
