using StarLevelSystem.common;
using StarLevelSystem.Data;
using System.Collections.Generic;
using static Heightmap;
using static StarLevelSystem.common.DataObjects;

namespace StarLevelSystem.modules.LevelSystem {
    internal static class ConditionalScaleSystem {

        internal static Dictionary<Heightmap.Biome, SortedDictionary<int, float>>  CurrentGlobalKeyConditionalLevelup = new Dictionary<Heightmap.Biome, SortedDictionary<int, float>>();
        private static readonly Dictionary<Heightmap.Biome, List<LevelGenerator>> resolvedByBiome = new Dictionary<Heightmap.Biome, List<LevelGenerator>>();
        // The current global key used to select Level Generators and levelup chances
        private static string CurrentGlobalKey = null;
        private static bool cacheValid = false;

        internal static SortedDictionary<int, float> GetConditionalLevelupChance(Heightmap.Biome biome) {
            var settings = LevelSystemData.SLE_Level_Settings;
            if (settings == null || settings.EnableConditionalCreatureLevelupChance == false || settings.ConditionalCreatureLevelupChance == null) {
                return null;
            }

            // The active key is re-derived on every call instead of being trusted from the cache. These
            // are WORLD global keys, set through ZoneSystem, and nothing local raises an event when one
            // changes -- the only invalidation hooks were on Player.AddUniqueKey/RemoveUniqueKey, and a
            // dedicated server has no local Player at all, so the cache stayed valid for the whole
            // lifetime of the process and defeating a boss changed nothing until a restart.
            //
            // Resolving the key is a handful of hash lookups. The expensive part is expanding the
            // generators below, and that still only reruns when the active key actually changes.
            string activeKey = ResolveActiveGlobalKey(settings.ConditionalCreatureLevelupChance);
            if (cacheValid == false || activeKey != CurrentGlobalKey) { RebuildCache(activeKey); }

            if (CurrentGlobalKeyConditionalLevelup.TryGetValue(biome, out SortedDictionary<int, float> gen)) { return gen; }
            // Documented fallback: an 'All' entry inside a conditional block covers every biome that does
            // not name itself. Only the exact-biome lookup existed, so an entry written with just 'All'
            // applied to nothing.
            if (CurrentGlobalKeyConditionalLevelup.TryGetValue(Heightmap.Biome.All, out SortedDictionary<int, float> fallback)) { return fallback; }
            return null;
        }

        // Level bounds contributed by the active conditional generators for a biome, if any.
        //
        // The generators are documented to replace the biome's Min/Max as well as its curve, and nothing
        // implemented that half: the shipped Meadows tier runs 6..30 while the biome caps at 4, so the
        // roller hit its "key >= maxLevel" stop on the very first entry and every Meadows creature came
        // out at exactly level 6. resolvedByBiome was already being filled for this and never read.
        internal static bool TryGetConditionalLevelRange(Heightmap.Biome biome, out int min, out int max) {
            min = 0;
            max = 0;
            var settings = LevelSystemData.SLE_Level_Settings;
            if (settings == null || settings.EnableConditionalCreatureLevelupChance == false || settings.ConditionalCreatureLevelupChance == null) {
                return false;
            }
            string activeKey = ResolveActiveGlobalKey(settings.ConditionalCreatureLevelupChance);
            if (cacheValid == false || activeKey != CurrentGlobalKey) { RebuildCache(activeKey); }

            if (resolvedByBiome.TryGetValue(biome, out List<LevelGenerator> generators) == false) {
                if (resolvedByBiome.TryGetValue(Heightmap.Biome.All, out generators) == false) { return false; }
            }
            if (generators == null || generators.Count == 0) { return false; }

            min = int.MaxValue;
            foreach (LevelGenerator gen in generators) {
                if (gen == null) { continue; }
                int lo = gen.MinLevel < gen.MaxLevel ? gen.MinLevel : gen.MaxLevel;
                int hi = gen.MinLevel < gen.MaxLevel ? gen.MaxLevel : gen.MinLevel;
                if (lo < min) { min = lo; }
                if (hi > max) { max = hi; }
            }
            if (min == int.MaxValue) { min = 0; return false; }
            return true;
        }

        // The first configured key the world currently has set, or null when none of them is. Entries are
        // expected to be authored highest tier first, since the first match wins.
        private static string ResolveActiveGlobalKey(Dictionary<string, Dictionary<Heightmap.Biome, ConditionalLevelupChance>> conditional) {
            if (conditional == null || ZoneSystem.instance == null) { return null; }
            foreach (KeyValuePair<string, Dictionary<Heightmap.Biome, ConditionalLevelupChance>> entry in conditional) {
                if (entry.Key != null && ZoneSystem.instance.GetGlobalKey(entry.Key)) { return entry.Key; }
            }
            return null;
        }

        private static void RebuildCache(string activeKey) {
            CurrentGlobalKey = activeKey;
            CurrentGlobalKeyConditionalLevelup.Clear();
            resolvedByBiome.Clear();

            Dictionary<string, Dictionary<Heightmap.Biome, ConditionalLevelupChance>> conditional = LevelSystemData.SLE_Level_Settings?.ConditionalCreatureLevelupChance;
            if (conditional == null || ZoneSystem.instance == null) { cacheValid = true; return; }

            // Nothing resolved means cache the empty result so we don't rebuild every call
            if (CurrentGlobalKey == null || !conditional.TryGetValue(CurrentGlobalKey, out Dictionary<Heightmap.Biome, ConditionalLevelupChance> biomeMap) || biomeMap == null) {
                cacheValid = true;
                return;
            }
            foreach (KeyValuePair<Heightmap.Biome, ConditionalLevelupChance> kvp in biomeMap) {
                if (kvp.Value == null) { continue; }
                List<LevelGenerator> generators = LevelGeneratorResolver.Resolve(kvp.Value.LevelupGenerators, kvp.Value.LevelupGeneratorRefs);
                if (generators.Count == 0) { continue; }
                resolvedByBiome[kvp.Key] = generators;
                SortedDictionary<int, float> levelupChance = new SortedDictionary<int, float>();
                bool clamped = false;
                foreach (var levelgen in generators) {
                    LevelGeneratorResolver.MergeGeneratorCurve(levelupChance, levelgen, ref clamped);
                }
                if (clamped) {
                    Logger.LogWarning($"ConditionalCreatureLevelupChance['{CurrentGlobalKey}'][{kvp.Key}]: stacked generators summed past the 100 roll ceiling and were clamped. Generators add rather than override.");
                }
                CurrentGlobalKeyConditionalLevelup[kvp.Key] = levelupChance;
            }
            Logger.LogDebug($"BossScaleSystem: resolved conditional levelup generators for '{CurrentGlobalKey}' across {resolvedByBiome.Count} biome(s).");
            cacheValid = true;
        }

        internal static void ResetCache() {
            cacheValid = false;
            CurrentGlobalKey = null;
            resolvedByBiome.Clear();
        }
    }
}
