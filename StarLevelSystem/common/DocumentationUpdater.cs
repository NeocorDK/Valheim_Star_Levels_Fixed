using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static StarLevelSystem.common.DataObjects;

namespace StarLevelSystem.common {
    internal static class DocumentationUpdater {

        // Writes ConfigReference.md next to the yaml files.
        //
        // Roughly 200 [Description] attributes on the config classes had exactly one consumer - this
        // class - whose entry point was commented out, and which only logged markdown into the BepInEx
        // console when it was not. So the attributes documented the config to nobody, and the yaml
        // headers in StarLevelConfigFiles were a second, hand-maintained source of truth that drifted
        // from them. Writing a file is what makes the attributes worth keeping accurate: an admin can
        // read it, and a drift between it and a header is visible.
        //
        // Off by default because it is a developer/admin aid and this runs on every client. The switch
        // is client-side for the same reason.
        public static void UpdateDocumentation(string directory) {
            if (ValConfig.OutputConfigDocumentation == null || ValConfig.OutputConfigDocumentation.Value == false) { return; }

            try {
                StringBuilder document = new StringBuilder();
                document.AppendLine("# Star Levels Expanded - configuration reference").AppendLine();
                document.AppendLine("Generated from the mod's own type definitions. The yaml file headers hold the")
                    .AppendLine("worked examples; this is the exhaustive list of keys, types and defaults.").AppendLine();

                foreach (Type root in DocumentedRoots) {
                    document.Append(ToMarkdown(root));
                }

                string path = Path.Combine(directory, "ConfigReference.md");
                File.WriteAllText(path, document.ToString());
                Logger.LogInfo($"Wrote the configuration reference to {path}.");
            } catch (Exception e) {
                // A documentation aid must never be the reason startup fails.
                Logger.LogWarning($"Could not write the configuration reference: {e.Message}");
            }
        }

        // One root per yaml file. ToMarkdown walks into everything they reference, so this only needs the
        // top of each tree.
        private static readonly Type[] DocumentedRoots = new Type[] {
            typeof(CreatureLevelSettings),
            typeof(CreatureColorizationSettings),
            typeof(LootSettings),
            typeof(CreatureModifierCollection),
            typeof(RaidConfiguration),
            typeof(NemesisConfiguration),
            typeof(LocationResetConfiguration),
        };

        // Generate a markdown style document showing all of the documented object details
        public static string ToMarkdown(Type root) {
            var sb = new StringBuilder();
            var visited = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(root);

            while (queue.Count > 0) {
                var t = queue.Dequeue();
                if (!visited.Add(t)) continue;

                var (typeDesc, _) = ReadDocs(t);
                sb.AppendLine($"## `{t.Name}`").AppendLine();
                if (!string.IsNullOrEmpty(typeDesc)) sb.AppendLine(typeDesc).AppendLine();

                sb.AppendLine("| Property | Type | Default | Description |");
                sb.AppendLine("|---|---|---|---|");

                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                    var (desc, def) = ReadDocs(p);
                    sb.AppendLine($"| `{p.Name}` | `{FriendlyTypeName(p.PropertyType)}` | {FormatDefault(def)} | {desc} |");

                    foreach (var nested in NestedUserTypes(p.PropertyType))
                        queue.Enqueue(nested);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // Read the attribution for descriptions and default value
        static (string description, object defaultValue) ReadDocs(MemberInfo member) {
            string desc = member.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            object def = member.GetCustomAttribute<DefaultValueAttribute>()?.Value;
            return (desc, def);
        }

        // Defines the typing of an object for documentation
        static string FriendlyTypeName(Type t) {
            if (t == typeof(string)) return "string";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(object)) return "object";

            var nullable = Nullable.GetUnderlyingType(t);
            if (nullable != null) return FriendlyTypeName(nullable) + "?";

            if (t.IsArray) return FriendlyTypeName(t.GetElementType()) + "[]";

            if (t.IsGenericType) {
                var name = t.Name.Substring(0, t.Name.IndexOf('`'));
                var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyTypeName));
                return $"{name}<{args}>";
            }

            return t.Name;
        }

        // Format the default value for documentation, handling special cases for common types
        static string FormatDefault(object value) {
            if (value == null) return "—";
            if (value is bool b) return b ? "`true`" : "`false`";
            if (value is string s) return s.Length == 0 ? "`\"\"`" : $"`\"{s}\"`";
            if (value is float f) return $"`{f.ToString(System.Globalization.CultureInfo.InvariantCulture)}f`";
            if (value is double d) return $"`{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}`";
            if (value is Enum e) return $"`{e.GetType().Name}.{e}`";
            if (value is IFormattable fmt) return $"`{fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture)}`";
            return $"`{value}`";
        }

        // Crawls through types to find all nested user-defined types for documentation purposes, handling arrays, generics, and nullable types
        static IEnumerable<Type> NestedUserTypes(Type t) {
            var nullable = Nullable.GetUnderlyingType(t);
            if (nullable != null) t = nullable;

            if (t.IsArray) {
                foreach (var nested in NestedUserTypes(t.GetElementType()))
                    yield return nested;
                yield break;
            }

            if (t.IsGenericType) {
                foreach (var arg in t.GetGenericArguments())
                    foreach (var nested in NestedUserTypes(arg))
                        yield return nested;
                yield break;
            }

            if (IsUserType(t))
                yield return t;
        }

        static bool IsUserType(Type t) {
            return t.Assembly == Assembly.GetExecutingAssembly()
            && !t.IsEnum
            && !t.IsPrimitive
            && t != typeof(string)
            && t != typeof(decimal);
        }
    }
}
