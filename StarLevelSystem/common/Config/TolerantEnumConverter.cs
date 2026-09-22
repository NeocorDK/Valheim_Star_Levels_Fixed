using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

#pragma warning disable IDE0130
namespace StarLevelSystem.common {
#pragma warning restore IDE0130

    // Turns a misspelled enum VALUE from a fatal parse error into one warning and one inert setting.
    //
    // IgnoreUnmatchedProperties only covers unrecognised KEYS. An unrecognised value -- `Skill: Al` --
    // still throws out of the deserializer and takes the whole file with it, which is the single most
    // likely mistake an admin makes in a hand-written config. This claims every enum type and answers a
    // bad value with the enum's zero member plus a warning that lists every legal name, which is the
    // most useful thing you can put in front of someone who just typed one wrong.
    //
    // Falling back to the zero member is safe for the enums this is aimed at: they are written so that
    // the default means "do nothing" (None, Off), so a failed parse leaves that one setting inert rather
    // than silently doing something the admin did not ask for. Check that holds for your own enums
    // before relying on it, and use SetFallback for any where it does not.
    internal class TolerantEnumConverter : IYamlTypeConverter {
        private static readonly Dictionary<Type, object> fallbacks = new Dictionary<Type, object>();

        // While a collector is set, bad values are handed to the caller instead of logged.
        //
        // A bad enum used to be logged and nothing more, so it never reached the ValidationReport: the
        // editor's upload path asked the server whether a document was clean, got an empty warning list
        // back and told the admin it had applied perfectly, while a misspelled Faction was quietly
        // turning raid creatures into allies. The same logging also fired during DryRun, filling the log
        // with warnings about candidate documents that were then rejected.
        [ThreadStatic] private static List<string> collector;

        internal static void BeginCollecting() {
            collector = new List<string>();
        }

        // Always paired with BeginCollecting, including on the failure paths - a collector left set would
        // swallow the warnings from every later parse on this thread.
        internal static List<string> EndCollecting() {
            List<string> collected = collector;
            collector = null;
            return collected ?? new List<string>();
        }

        // Override the value used when a scalar does not parse, for an enum whose zero member is not the
        // harmless one.
        internal static void SetFallback(Type enumType, object fallback) {
            if (enumType == null || enumType.IsEnum == false) { return; }
            fallbacks[enumType] = fallback;
        }

        public bool Accepts(Type type) {
            return type != null && type.IsEnum;
        }

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) {
            Scalar scalar = parser.Consume<Scalar>();
            string raw = scalar.Value;

            if (string.IsNullOrWhiteSpace(raw) == false) {
                try {
                    // Enum.Parse rather than the generic TryParse: this is a Type, not a T, and net48 has
                    // no non-generic TryParse overload. It also handles a comma-separated flags list for
                    // free. Bad values are rare enough that the throw costs nothing worth optimising.
                    return Enum.Parse(type, raw.Trim(), true);
                } catch (Exception) {
                    // Fall through to the warning below.
                }
            }

            object fallback = FallbackFor(type);
            string problem = $"line {scalar.Start.Line}: '{raw}' is not a valid {type.Name}. Using {fallback}. " +
                $"Valid values: {string.Join(", ", Enum.GetNames(type))}.";
            if (collector != null) { collector.Add(problem); } else { Logger.LogWarning(problem); }
            return fallback;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer) {
            // Plain scalar, exactly what the built-in enum handling emits. Claiming the write side as well
            // as the read side keeps one converter in charge of the whole round trip.
            emitter.Emit(new Scalar(value == null ? "" : value.ToString()));
        }

        private static object FallbackFor(Type type) {
            if (fallbacks.TryGetValue(type, out object registered)) { return registered; }
            // The enum's zero member, whether or not it has a name.
            return Activator.CreateInstance(type);
        }
    }
}
