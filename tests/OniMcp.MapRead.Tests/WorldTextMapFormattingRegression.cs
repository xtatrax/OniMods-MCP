namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        // Only neighboring formatter dependencies are substituted; view
        // normalization is linked from the production source.
        private sealed class CellSummary
        {
            public char Symbol { get; set; }
            public string ElementId { get; set; }
        }
        private static string EscapeMarkdown(string value) { return value; }

        internal static void RunTextMapFormattingRegressions()
        {
            int assertions = 0;
            void Check(bool ok, string message)
            {
                assertions++;
                if (!ok) throw new InvalidOperationException(message);
            }
            foreach (string view in new[] { "base", "power", "gas_conduits", "liquid_conduits", "solid_conveyor", "logic", "temperature" })
            {
                Check(NormalizeTextMapView(view) == view, "canonical view preserved: " + view);
                Check(NormalizeTextMapView("  " + view.ToUpperInvariant() + "  ") == view, "case and whitespace normalized: " + view);
            }
            foreach (string empty in new[] { null, "", "  " })
                Check(NormalizeTextMapView(empty) == "base", "omitted view defaults to base");
            foreach (var aliases in new[]
            {
                new[] { "base", "terrain", "normal", "none" },
                new[] { "power", "electric", "electrical" },
                new[] { "gas_conduits", "gas", "gas_pipe", "gas_pipes", "gas_conduit" },
                new[] { "liquid_conduits", "liquid", "liquid_pipe", "liquid_pipes", "liquid_conduit" },
                new[] { "solid_conveyor", "solid", "shipping", "conveyor", "solid_conduit", "solid_conduits" },
                new[] { "logic", "automation" },
                new[] { "temperature", "temp", "thermal", "heat" }
            })
                foreach (string alias in aliases.Skip(1))
                    Check(NormalizeTextMapView(alias) == aliases[0], "legacy alias retained: " + alias);
            foreach (string unknown in new[] { "unknown", "gas_conduitz", "oxygen", "power,logic" })
            {
                bool rejected = false;
                try { NormalizeTextMapView(unknown); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "unsupported view must not silently return base: " + unknown);
            }
            Console.WriteLine("PASS: " + assertions + " production legacy JSON map view assertions");
        }
    }
}
