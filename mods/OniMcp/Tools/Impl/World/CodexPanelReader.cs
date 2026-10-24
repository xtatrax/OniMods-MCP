using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OniMcp.Tools
{
    // Explicit field schemas only: never enumerate properties, invoke formatters or Configure UI.
    // Private backing fields are version-sensitive; missing fields remain visible in the result.
    internal static class CodexPanelReader
    {
        private static readonly Dictionary<string, string[]> Schemas = new Dictionary<string, string[]>
        {
            ["CodexConversionPanel"] = Fields("title ins outs Converter aidIcon"),
            ["ElementUsage"] = Fields("tag amount continuous customFormating"),
            ["CodexConversionPanel+IconSettings"] = Fields("spriteName tooltip"),
            ["CodexRecipePanel"] = Fields("complexRecipe recipe useFabricatorForTitle <linkID>k__BackingField"),
            ["ComplexRecipe"] = Fields("id recipeCategoryID ingredients results time consumedHEP producedHEP customName description fabricators requiredTech runTimeDescription"),
            ["ComplexRecipe+RecipeElement"] = Fields("material possibleMaterials possibleMaterialAmounts <amount>k__BackingField temperatureOperation storeElement inheritElement facadeID doNotConsume"),
            ["Recipe"] = Fields("Ingredients Result ResultElementOverride recipeDescription OutputUnits FabricationTime TechUnlock fabricators"),
            ["Recipe+Ingredient"] = Fields("tag amount"),
            ["CodexTemperatureTransitionPanel"] = Fields("sourceElement transitionType"),
            ["Element"] = Fields("id tag <name>k__BackingField lowTemp highTemp lowTempTransitionTarget highTempTransitionTarget highTempTransitionOreID highTempTransitionOreMassConversion lowTempTransitionOreID lowTempTransitionOreMassConversion sublimateId convertId sublimateRate sublimateEfficiency sublimateProbability offGasPercentage"),
            ["CodexElementCategoryList"] = Fields("<categoryTag>k__BackingField"),
            ["CodexConfigurableConsumerRecipePanel"] = Fields("data"),
            ["SpiceGrinder+Option"] = Fields("Id Spice name fullDescription spiceDescription ingredientDescriptions"),
            ["Database.Spice"] = Fields("Ingredients TotalKG"),
            ["Database.Spice+Ingredient"] = Fields("IngredientSet AmountKG")
        };

        private static string[] Fields(string value) => value.Split(' ');
        internal static bool IsKnown(object value, string name) => value != null
            && value.GetType().Assembly == typeof(CodexText).Assembly && value.GetType().FullName == name;

        internal static bool TryField(object value, string name, out object result)
        {
            result = null;
            if (value == null) return false;
            var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;
            result = field.GetValue(value);
            return true;
        }

        internal static Dictionary<string, object> Read(object widget)
        {
            if (widget == null || !Schemas.ContainsKey(widget.GetType().FullName)
                || widget.GetType().Assembly != typeof(CodexText).Assembly) return null;
            var issues = new List<string>();
            int remainingNodes = 4096;
            object data = null;
            try { data = Value(widget, issues, 0, ref remainingNodes); }
            catch (Exception ex) { issues.Add("field_read_failed:" + ex.GetType().Name); }
            var result = new Dictionary<string, object>
            {
                ["type"] = widget.GetType().Name,
                ["data"] = data,
                ["dataFormat"] = "raw_game_fields",
                ["unitsNote"] = "Native values: element temperatures are kelvin; recipe time is seconds. Conversion amounts use tag-dependent units and continuous flags. Custom formatters are not executed.",
                ["issues"] = issues,
                // These are definition fields, not a reconstruction of native rendered UI.
                ["renderedUiComplete"] = false
            };
            if (IsKnown(widget, "CodexElementCategoryList"))
                result["categoryMembers"] = CodexCategoryMemberReader.Read(widget, issues, ref remainingNodes);
            if (IsKnown(widget, "CodexTemperatureTransitionPanel"))
            {
                var fallback = CodexSublimationReader.Read(widget, issues, ref remainingNodes);
                if (fallback != null) result["prefabSublimatesFallback"] = fallback;
            }
            if (IsKnown(widget, "CodexConversionPanel"))
            {
                var light = CodexConverterLightReader.Read(widget, issues, ref remainingNodes);
                if (light != null) result["converterNominalLight"] = light;
            }
            return result;
        }

        private static object Value(object value, List<string> issues, int depth, ref int remainingNodes)
        {
            if (value == null) return null;
            if (--remainingNodes < 0 || depth > 12)
            {
                if (!issues.Contains("data_limit")) issues.Add("data_limit");
                return null;
            }
            var type = value.GetType();
            if (value is string || value is bool || type.IsPrimitive || value is decimal) return value;
            if (type.IsEnum) return Enum.GetName(type, value) ?? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (value is Tag tag) return tag.Name;
            if (value is UnityEngine.Object unity) return new Dictionary<string, object> { ["assetName"] = unity.name };
            if (value is Delegate)
            {
                issues.Add("formatter_not_executed");
                return new Dictionary<string, object> { ["callbackPresent"] = true };
            }
            // Do not execute arbitrary mod enumerators.
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var list = new List<object>();
                foreach (object child in (IEnumerable)value)
                {
                    if (remainingNodes <= 0) { issues.Add("data_limit"); break; }
                    list.Add(Value(child, issues, depth + 1, ref remainingNodes));
                }
                return list;
            }
            string[] fields;
            if (type.Assembly != typeof(CodexText).Assembly || !Schemas.TryGetValue(type.FullName, out fields))
            {
                issues.Add("unsupported_data_type:" + type.FullName);
                return null;
            }
            var result = new Dictionary<string, object>();
            foreach (string field in fields)
            {
                object child;
                if (!TryField(value, field, out child))
                {
                    issues.Add("missing_field:" + type.FullName + "." + field);
                    continue;
                }
                string key = field.StartsWith("<", StringComparison.Ordinal) ? field.Substring(1, field.IndexOf('>') - 1) : field;
                result[key] = Value(child, issues, depth + 1, ref remainingNodes);
            }
            if (IsKnown(value, "ElementUsage"))
            {
                var formatter = ((ElementUsage)value).customFormating;
                // Delegate equality requires this exact native static method and single invocation.
                if (formatter != null && formatter.Equals((Func<Tag, float, bool, string>)GameUtil.GetFormattedPreyConsumptionValuePerCycle))
                {
                    if (remainingNodes < 5)
                    {
                        if (!issues.Contains("data_limit")) issues.Add("data_limit");
                    }
                    else
                    {
                        remainingNodes -= 5;
                        result["amountSemantics"] = new Dictionary<string, object>
                        {
                            ["rawUnit"] = "entity_count_per_second", ["nativeDisplayUnit"] = "entity_count_per_cycle",
                            ["source"] = "GameUtil.GetFormattedPreyConsumptionValuePerCycle", ["formatterExecuted"] = false
                        };
                    }
                }
            }
            return result;
        }
    }
}
