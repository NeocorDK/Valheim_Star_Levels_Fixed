using StarLevelSystem.common;
using StarLevelSystem.Data;
using System.Collections.Generic;
using UnityEngine;
using static StarLevelSystem.common.DataObjects;

namespace StarLevelSystem.modules.LevelSystem {
    // Resolves level generators (inline and/or referenced from CreatureLevelSettings.CustomLevelupGenerators)
    // into the levelup-chance tables consumed by the level/raid/nemesis systems. References are looked up
    // against the currently loaded settings so they always reflect the latest config.
    internal static class LevelGeneratorResolver {

        // Combines inline generators with those referenced by name from the CustomLevelupGenerators registry.
        internal static List<LevelGenerator> Resolve(List<LevelGenerator> inline, List<string> refs) {
            List<LevelGenerator> result = new List<LevelGenerator>();
            if (inline != null) { result.AddRange(inline); }
            if (refs != null && refs.Count > 0) {
                Dictionary<string, List<LevelGenerator>> registry = LevelSystemData.SLE_Level_Settings?.CustomLevelupGenerators;
                foreach (string name in refs) {
                    if (name == null) { continue; }
                    if (registry != null && registry.TryGetValue(name, out List<LevelGenerator> gens) && gens != null) {
                        result.AddRange(gens);
                    } else {
                        Logger.LogWarning($"Levelup generator reference '{name}' was not found in CreatureLevelSettings.CustomLevelupGenerators.");
                    }
                }
            }
            return result;
        }

        // True when either an inline list or a list of references is configured.
        internal static bool HasGenerators(List<LevelGenerator> inline, List<string> refs) {
            return (inline != null && inline.Count > 0) || (refs != null && refs.Count > 0);
        }

        // Merges one generator's curve onto an accumulator on the 0-100 roll scale.
        //
        // Generators ADD: two 25% generators covering the same levels produce a threshold of 50, three
        // produce 75. Nothing used to stop that sum from passing 100, and a threshold above 100 can never
        // be cleared - Random.Range(0f, 100f) is exclusive at the top - so those levels dropped out of the
        // roll entirely and, when every entry went over, the roller fell through to its last-entry escape
        // and returned the top level for everything. The sum is clamped to the same 100 a single generator
        // tops out at, which keeps a stacked curve meaning what its entries say instead of silently
        // rewriting the distribution.
        internal static void MergeGeneratorCurve(SortedDictionary<int, float> accumulator, LevelGenerator gen, ref bool clamped) {
            SortedDictionary<int, float> curve = gen.GetLevelUpDefinition();
            foreach (KeyValuePair<int, float> kvp in curve) {
                float merged = accumulator.TryGetValue(kvp.Key, out float existing) ? existing + kvp.Value : kvp.Value;
                if (merged > 100f) { merged = 100f; clamped = true; }
                accumulator[kvp.Key] = merged;
            }
        }

        private static void WarnIfClamped(bool clamped, List<LevelGenerator> gens) {
            if (clamped == false) { return; }
            Logger.LogWarning($"Stacked levelup generators ({gens.Count}) summed past the 100 roll ceiling and were clamped; the levels involved are effectively unreachable. Generators add rather than override - lower LevelUpChance on the overlapping ones or narrow their MinLevel..MaxLevel.");
        }

        // Builds a merged levelup-chance table from the configured generators, or null when none are configured.
        internal static SortedDictionary<int, float> BuildLevelupChance(List<LevelGenerator> inline, List<string> refs) {
            if (!HasGenerators(inline, refs)) { return null; }
            List<LevelGenerator> gens = Resolve(inline, refs);
            if (gens.Count == 0) { return null; }
            SortedDictionary<int, float> chances = new SortedDictionary<int, float>();
            bool clamped = false;
            foreach (LevelGenerator gen in gens) {
                MergeGeneratorCurve(chances, gen, ref clamped);
            }
            WarnIfClamped(clamped, gens);
            return chances;
        }

        // Rolls a concrete level from the configured generators, or 0 when none are configured.
        internal static int RollLevel(List<LevelGenerator> inline, List<string> refs) {
            if (!HasGenerators(inline, refs)) { return 0; }
            List<LevelGenerator> gens = Resolve(inline, refs);
            if (gens.Count == 0) { return 0; }
            SortedDictionary<int, float> chances = new SortedDictionary<int, float>();
            int maxLevel = 1;
            bool clamped = false;
            foreach (LevelGenerator gen in gens) {
                MergeGeneratorCurve(chances, gen, ref clamped);
                if (gen.MaxLevel > maxLevel) { maxLevel = gen.MaxLevel; }
            }
            WarnIfClamped(clamped, gens);
            float roll = UnityEngine.Random.Range(0f, 100f);
            return LevelSelection.DetermineLevelRollResult(roll, maxLevel, chances, new SortedDictionary<int, float>(), 1f);
        }
    }
}
