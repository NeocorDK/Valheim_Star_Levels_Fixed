using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Logger = StarLevelSystem.Logger;

#pragma warning disable IDE0130
namespace StarLevelSystem.common {
#pragma warning restore IDE0130

    // Finds every key in a yaml document that the target type has no member for.
    //
    // YamlDotNet stops at the FIRST unmatched property, so the strict pass in YamlConfigFile could only
    // ever name one: a file with five typos showed one and dropped the other four silently, and the
    // admin fixed them one restart at a time. The tolerant pass that keeps the file usable is exactly the
    // pass that hides the rest.
    //
    // This walks the parsed object graph (which the tolerant deserializer produces in full) against the
    // CLR type by reflection instead of relying on the parser, so it reports all of them in one go and can
    // run ConfigValidation.SuggestKey over the real member names for a "did you mean" - the one thing
    // SuggestKey was written for and was never wired to.
    internal static class UnknownKeyScan {

        // Keeps a pathological file from producing a wall of log. Anything past this is almost always the
        // same mistake repeated, or a document whose shape is wrong rather than its keys.
        private const int MaxReported = 12;

        internal static List<string> Find(object graph, Type expected) {
            List<string> found = new List<string>();
            try {
                Walk(graph, expected, "", found);
            } catch (Exception e) {
                // A best-effort diagnostic must never be the reason a config load fails.
                Logger.LogDebug($"Unknown-key scan stopped early: {e.Message}");
            }
            return found;
        }

        private static void Walk(object node, Type expected, string path, List<string> found) {
            if (node == null || expected == null || found.Count >= MaxReported) { return; }

            if (node is IDictionary mapping) {
                Type dictValue = GenericArgument(expected, typeof(IDictionary<,>), 1);
                if (dictValue != null) {
                    // A dictionary's keys are data (prefab names, biomes, levels), not member names.
                    foreach (DictionaryEntry entry in mapping) {
                        Walk(entry.Value, dictValue, Join(path, Convert.ToString(entry.Key)), found);
                    }
                    return;
                }

                // A plain object: every key here should name one of its members.
                Dictionary<string, MemberInfo> members = MembersOf(expected);
                foreach (DictionaryEntry entry in mapping) {
                    if (found.Count >= MaxReported) { return; }
                    string key = Convert.ToString(entry.Key);
                    if (string.IsNullOrEmpty(key)) { continue; }
                    if (members.TryGetValue(key.ToLowerInvariant(), out MemberInfo member) == false) {
                        found.Add($"{Join(path, key)} is not a setting this mod has." +
                            ConfigValidation.SuggestKey(key, NamesOf(members)));
                        continue;
                    }
                    Walk(entry.Value, TypeOf(member), Join(path, key), found);
                }
                return;
            }

            if (node is IList list) {
                Type itemType = ElementType(expected);
                if (itemType == null) { return; }
                for (int i = 0; i < list.Count; i++) {
                    Walk(list[i], itemType, $"{path}[{i}]", found);
                }
            }
            // Scalars have nothing to check: a bad value is the deserializer's business, not this scan's.
        }

        private static string Join(string path, string key) {
            return string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
        }

        private static Dictionary<string, MemberInfo> MembersOf(Type type) {
            Dictionary<string, MemberInfo> members = new Dictionary<string, MemberInfo>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (PropertyInfo property in type.GetProperties(flags)) {
                if (property.GetIndexParameters().Length > 0) { continue; }
                members[property.Name.ToLowerInvariant()] = property;
            }
            foreach (FieldInfo field in type.GetFields(flags)) {
                members[field.Name.ToLowerInvariant()] = field;
            }
            return members;
        }

        // The scan matches case-insensitively, like the deserializer, but the suggestion should show the
        // member as it is actually spelled.
        private static List<string> NamesOf(Dictionary<string, MemberInfo> members) {
            List<string> names = new List<string>();
            foreach (MemberInfo member in members.Values) { names.Add(member.Name); }
            return names;
        }

        private static Type TypeOf(MemberInfo member) {
            if (member is PropertyInfo property) { return property.PropertyType; }
            if (member is FieldInfo field) { return field.FieldType; }
            return null;
        }

        private static Type ElementType(Type type) {
            if (type.IsArray) { return type.GetElementType(); }
            return GenericArgument(type, typeof(IEnumerable<>), 0);
        }

        // Walks the type itself and its interfaces so SortedDictionary<int, float>, Dictionary<string, T>
        // and a bare IDictionary<K, V> all resolve the same way.
        private static Type GenericArgument(Type type, Type openInterface, int index) {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == openInterface) {
                return type.GetGenericArguments()[index];
            }
            foreach (Type candidate in type.GetInterfaces()) {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openInterface) {
                    return candidate.GetGenericArguments()[index];
                }
            }
            return null;
        }
    }
}
