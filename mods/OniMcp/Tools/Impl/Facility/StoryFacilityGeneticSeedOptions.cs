using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class StoryFacilityTools
    {
        private static List<Dictionary<string, object>> GetGeneticSeedOptions(GeneticAnalysisStation.StatesInstance station)
        {
            var options = new List<Dictionary<string, object>>();
            var catalog = PlantSubSpeciesCatalog.Instance;
            var discovered = DiscoveredResources.Instance;
            if (catalog == null || discovered == null)
                return options;
            HashSet<Tag> forbiddenSeeds;
            string permissionReason;
            bool permissionAvailable = TryReadGeneticForbiddenSeeds(station, out forbiddenSeeds, out permissionReason);
            foreach (Tag species in catalog.GetAllDiscoveredSpecies())
            {
                var subspecies = catalog.GetAllSubSpeciesForSpecies(species);
                if (subspecies == null || subspecies.Count <= 1)
                    continue;
                Tag seed = GetSeedIDFromPlantID(species);
                if (!seed.IsValid || !discovered.IsDiscovered(seed))
                    continue;
                bool forbidden = permissionAvailable && forbiddenSeeds.Contains(seed);
                options.Add(new Dictionary<string, object>
                {
                    ["speciesId"] = species.Name,
                    ["seedId"] = seed.Name,
                    ["name"] = seed.ProperName(),
                    ["allowed"] = permissionAvailable ? (object)!forbidden : JValue.CreateNull(),
                    ["forbidden"] = permissionAvailable ? (object)forbidden : JValue.CreateNull(),
                    ["subSpeciesCount"] = subspecies.Count,
                    ["permissionState"] = permissionAvailable ? "observed" : "unavailable",
                    ["permissionReason"] = permissionReason == null ? (object)JValue.CreateNull() : permissionReason
                });
            }
            return options.OrderBy(option => option["name"].ToString()).ToList();
        }

        private static bool TryReadGeneticForbiddenSeeds(GeneticAnalysisStation.StatesInstance station,
            out HashSet<Tag> forbiddenSeeds, out string reason)
        {
            forbiddenSeeds = null;
            reason = "station_unavailable";
            if (station == null)
                return false;
            try
            {
                var owner = typeof(GeneticAnalysisStation.StatesInstance);
                var field = owner.GetField("forbiddenSeeds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                reason = "forbidden_cache_unavailable";
                if (field == null || field.DeclaringType != owner || field.FieldType != typeof(HashSet<Tag>)
                    || !field.IsPrivate || field.IsStatic)
                    return false;
                forbiddenSeeds = (HashSet<Tag>)field.GetValue(station);
                reason = "forbidden_cache_uninitialized";
                if (forbiddenSeeds == null)
                    return false;
                reason = "forbidden_cache_comparer_unsupported";
                if (!ReferenceEquals(forbiddenSeeds.Comparer, EqualityComparer<Tag>.Default))
                {
                    forbiddenSeeds = null;
                    return false;
                }
                reason = null;
                return true;
            }
            catch
            {
                forbiddenSeeds = null;
                reason = "forbidden_cache_read_failed";
                return false;
            }
        }

        private static Tag GetSeedIDFromPlantID(Tag speciesID)
        {
            GameObject prefab = Assets.TryGetPrefab(speciesID);
            if (prefab == null)
                return Tag.Invalid;
            SeedProducer component = prefab.GetComponent<SeedProducer>();
            if (component == null)
                return Tag.Invalid;
            return component.seedInfo.seedId;
        }
    }
}
