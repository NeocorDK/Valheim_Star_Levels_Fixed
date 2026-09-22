using Jotunn;
using Jotunn.Managers;
using StarLevelSystem.common;
using StarLevelSystem.modules;
using StarLevelSystem.modules.AnimationAndSpeed;
using StarLevelSystem.modules.CreatureSetup;
using StarLevelSystem.modules.Damage;
using StarLevelSystem.modules.Health;
using StarLevelSystem.modules.LevelSystem;
using StarLevelSystem.modules.Sizes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StarLevelSystem.common.DataObjects;

namespace StarLevelSystem.Data
{
    public static class LevelSystemData
    {

        // Assigned by the static constructor below, deliberately NOT by a field initializer here. Static
        // field initializers run in textual order and DefaultConfiguration is declared after this line,
        // so "= DefaultConfiguration" assigned null and left this field null until the first config load
        // -- every unguarded reader in that window threw.
        public static DataObjects.CreatureLevelSettings SLE_Level_Settings;

        static LevelSystemData() {
            SLE_Level_Settings = DefaultConfiguration;
        }

        public static readonly DataObjects.CreatureLevelSettings DefaultConfiguration = new DataObjects.CreatureLevelSettings()
        {
            // Named, reusable level generators. These are examples and are NOT referenced anywhere by default.
            // Reference them by name from any DefaultLevelupGeneratorRefs / LevelupGeneratorRefs list (biome,
            // creature, conditional, raid, or nemesis spawn); the generated curve overwrites that section's chances.
            CustomLevelupGenerators = new Dictionary<string, List<LevelGenerator>>() {
                { "early_game", new List<LevelGenerator>() {
                        new LevelGenerator() { MinLevel = 0, MaxLevel = 6, LevelUpChance = 0.2f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                    }
                },
                { "late_game", new List<LevelGenerator>() {
                        new LevelGenerator() { MinLevel = 1, MaxLevel = 25, LevelUpChance = 0.35f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Gaussian, GaussianOffset = 0.25f }
                    }
                },
            },
            DefaultCreatureLevelUpChance = new SortedDictionary<int, float>() {
                { 1, 20f },
                { 2, 10f },
                { 3, 5f },
                { 4, 2f },
                { 5, 1f },
                { 6, 0.5f },
                { 7, 0.25f },
                { 8, 0.125f },
                { 9, 0.0625f },
                { 10, 0.0312f },
                { 11, 0.0156f },
                { 12, 0.0078f },
                { 13, 0.0039f },
                { 14, 0.0019f },
                { 15, 0.0015f },
                { 16, 0.0010f },
                { 17, 0.0009f },
                { 18, 0.0008f },
                { 19, 0.0007f },
                { 20, 0.0006f },
                { 21, 0.0005f },
                { 22, 0.0004f },
                { 23, 0.0003f },
                { 24, 0.0002f },
                { 25, 0.0001f },
            },
            BiomeConfiguration = new Dictionary<Heightmap.Biome, DataObjects.BiomeSpecificSetting>()
            {
                { Heightmap.Biome.All, new DataObjects.BiomeSpecificSetting()
                    {
                        SpawnRateModifier = 1.1f,
                        DistanceScaleModifier = 1.5f,
                        DamageRecievedModifiers = new Dictionary<DataObjects.DamageType, float>() {
                            {DataObjects.DamageType.Poison, 1.5f } 
                        },
                        NightSettings = new BiomeNightSettings() {
                            NightLevelUpChanceScaler = 1.5f,
                        }
                    }
                },
                { Heightmap.Biome.Meadows, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 4,
                    }
                },
                { Heightmap.Biome.BlackForest, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 6,
                    }
                },
                { Heightmap.Biome.Swamp, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 10,
                    }
                },
                { Heightmap.Biome.Mountain, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 14,
                    }
                },
                { Heightmap.Biome.Plains, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 18,
                    }
                },
                { Heightmap.Biome.Mistlands, new DataObjects.BiomeSpecificSetting()
                    {
                        BiomeMaxLevelOverride = 22,
                    }
                },
                { Heightmap.Biome.AshLands, new DataObjects.BiomeSpecificSetting()
                    {
                        DistanceScaleModifier = 0.5f,
                        BiomeMaxLevelOverride = 26,
                    }
                },
                { Heightmap.Biome.DeepNorth, new DataObjects.BiomeSpecificSetting()
                    {
                        DistanceScaleModifier = 0.5f,
                        BiomeMaxLevelOverride = 26,
                    }
                }
            },

            CreatureConfiguration = new Dictionary<string, DataObjects.CreatureSpecificSetting>() {
                { "piece_TrainingDummy", new DataObjects.CreatureSpecificSetting()
                    {
                        CreatureMaxLevelOverride = 11,
                        SpawnRateModifier = 1f,
                        CreatureBaseValueModifiers = new Dictionary<CreatureBaseAttribute, float>() {
                            { DataObjects.CreatureBaseAttribute.BaseHealth, 1.05f },
                            { DataObjects.CreatureBaseAttribute.BaseDamage, 0.95f },
                            { DataObjects.CreatureBaseAttribute.Speed, 1.05f },
                            { DataObjects.CreatureBaseAttribute.Size, 1.05f },
                            { DataObjects.CreatureBaseAttribute.AttackSpeed, 1.05f },
                        },
                        CreaturePerLevelValueModifiers = new Dictionary<DataObjects.CreaturePerLevelAttribute, float>() {
                            { DataObjects.CreaturePerLevelAttribute.HealthPerLevel, 0.3f },
                            { DataObjects.CreaturePerLevelAttribute.DamagePerLevel, 0.05f },
                            { DataObjects.CreaturePerLevelAttribute.SizePerLevel, 0.005f }
                        },
                        CustomCreatureLevelUpChance = new SortedDictionary<int, float>()
                        {
                            {1, 100 },
                            {2, 100 },
                            {3, 0 },
                        }
                    }
                },
                { "Lox", new DataObjects.CreatureSpecificSetting()
                    {
                        CreaturePerLevelValueModifiers = new Dictionary<DataObjects.CreaturePerLevelAttribute, float>() {
                            { DataObjects.CreaturePerLevelAttribute.SpeedPerLevel, -0.01f },
                        }
                    }
                },
                { "Troll", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                        CreaturePerLevelValueModifiers = new Dictionary<DataObjects.CreaturePerLevelAttribute, float>() {
                            { DataObjects.CreaturePerLevelAttribute.SizePerLevel, 0.05f },
                        }
                    }
                },
                { "Bjorn", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                        CreaturePerLevelValueModifiers = new Dictionary<DataObjects.CreaturePerLevelAttribute, float>() {
                            { DataObjects.CreaturePerLevelAttribute.SizePerLevel, 0.05f },
                        }
                    }
                },
                { "GoblinBruteBros", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
                { "GoblinShaman_Hildir", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
                { "GoblinBrute_Hildir", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
                { "Skeleton_Hildir", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
                { "Fenring_Cultist_Hildir", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
                { "Charred_Melee_Dyrnwyn", new DataObjects.CreatureSpecificSetting()
                    {
                        SpawnRateModifier = 1f,
                    }
                },
            },

            EnableDistanceLevelBonus = true,
            DistanceLevelBonus = new SortedDictionary<int, SortedDictionary<int, float>>()
            {
                { 750, new SortedDictionary<int, float>() {
                        { 1, 15f },
                        { 2, 5f },
                        { 3, 1f },
                    }
                },
                { 1200, new SortedDictionary<int, float>() {
                        { 1, 18f },
                        { 2, 7f },
                        { 3, 1.5f },
                        { 4, 0.5f },
                    }
                },
                { 2000, new SortedDictionary<int, float>() {
                        { 1, 24f },
                        { 2, 10f },
                        { 3, 3f },
                        { 4, 1f },
                        { 5, 0.5f },
                    }
                },
                { 3000, new SortedDictionary<int, float>() {
                        { 1, 32f },
                        { 2, 16f },
                        { 3, 8f },
                        { 4, 4f },
                        { 5, 2f },
                        { 6, 1f },
                    }
                },
                { 4000, new SortedDictionary<int, float>() {
                        { 1, 40f },
                        { 2, 25f },
                        { 3, 12f },
                        { 4, 6f },
                        { 5, 3f },
                        { 6, 2f },
                        { 7, 1f },
                        { 8, 0.5f },
                    }
                },
                { 5000, new SortedDictionary<int, float>() {
                        { 1, 50f },
                        { 2, 35f },
                        { 3, 18f },
                        { 4, 9f },
                        { 5, 6f },
                        { 6, 3f },
                        { 7, 2f },
                        { 8, 1f },
                        { 9, 0.5f },
                        { 10, 0.25f },
                    }
                },
                { 6000, new SortedDictionary<int, float>() {
                        { 1, 60f },
                        { 2, 40f },
                        { 3, 20f },
                        { 4, 10f },
                        { 5, 8f },
                        { 6, 6f },
                        { 7, 4f },
                        { 8, 2f },
                        { 9, 1f },
                        { 10, 0.5f },
                        { 11, 0.25f },
                        { 12, 0.12f },
                    }
                },
                { 7000, new SortedDictionary<int, float>() {
                        { 1, 70f },
                        { 2, 50f },
                        { 3, 25f },
                        { 4, 12f },
                        { 5, 10f },
                        { 6, 8f },
                        { 7, 6f },
                        { 8, 4f },
                        { 9, 2f },
                        { 10, 1f },
                        { 11, 0.5f },
                        { 12, 0.25f },
                        { 13, 0.12f },
                        { 14, 0.06f },
                    }
                },
                { 8000, new SortedDictionary<int, float>() {
                        { 1, 80f },
                        { 2, 60f },
                        { 3, 40f },
                        { 4, 20f },
                        { 5, 15f },
                        { 6, 12f },
                        { 7, 10f },
                        { 8, 8f },
                        { 9, 6f },
                        { 10, 4f },
                        { 11, 2f },
                        { 12, 1f },
                        { 13, 0.5f },
                        { 14, 0.25f },
                        { 15, 0.12f },
                        { 16, 0.06f },
                    }
                },
                { 9100, new SortedDictionary<int, float>() {
                        { 1, 100f },
                        { 2, 80f },
                        { 3, 60f },
                        { 4, 40f },
                        { 5, 30f },
                        { 6, 20f },
                        { 7, 16f },
                        { 8, 14f },
                        { 9, 12f },
                        { 10, 10f },
                        { 11, 8f },
                        { 12, 4f },
                        { 13, 2f },
                        { 14, 1f },
                        { 15, 0.5f },
                        { 16, 0.25f },
                        { 17, 0.12f },
                        { 18, 0.06f },
                    }
                }
            },
            LevelupWeightTablesBySpan = new Dictionary<int, SortedDictionary<int, float>>() {
                { 4, new SortedDictionary<int, float>() { { 1, 30f }, { 2, 15f },   { 3, 5f },      { 4, 0.01f } } },
                { 5, new SortedDictionary<int, float>() { { 1, 30f }, { 2, 16f },   { 3, 6.8333f }, { 4, 2.5f }, { 5, 0.01f } } },
                { 6, new SortedDictionary<int, float>() { { 1, 30f }, { 2, 17.5f }, { 3, 8.5f },    { 4, 3.0f }, { 5, 1.0f }, { 6, 0.01f } } },
            },
            EnableConditionalCreatureLevelupChance = false,
            ConditionalCreatureLevelupChance = new Dictionary<string, Dictionary<Heightmap.Biome, ConditionalLevelupChance>>() {
                { "defeated_fader", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 6, MaxLevel = 30, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 5, MaxLevel = 24, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Swamp, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 4, MaxLevel = 20, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mountain, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 3, MaxLevel = 16, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Plains, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mistlands, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_queen", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 6, MaxLevel = 30, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 5, MaxLevel = 24, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Swamp, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 4, MaxLevel = 20, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mountain, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 3, MaxLevel = 16, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Plains, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mistlands, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_goblinking", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 5, MaxLevel = 24, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 4, MaxLevel = 20, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Swamp, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 3, MaxLevel = 16, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mountain, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Plains, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_dragon", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 4, MaxLevel = 20, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 3, MaxLevel = 16, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Swamp, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Mountain, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_bonemass", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 3, MaxLevel = 16, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.Swamp, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_gdking", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 2, MaxLevel = 12, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    },
                    {
                        Heightmap.Biome.BlackForest, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }},
                { "defeated_eikthyr", new Dictionary<Heightmap.Biome, ConditionalLevelupChance>() {
                    {
                        Heightmap.Biome.Meadows, new ConditionalLevelupChance() {
                            LevelupGenerators = new List<LevelGenerator>() {
                                new LevelGenerator() { MinLevel = 1, MaxLevel = 8, LevelUpChance = 0.25f, LevelupCalculationStyle = DataObjects.LevelupCalculationStyle.Exponential }
                            }
                        }
                    }
                }}
            }
        };


        // Validate hook for LevelSettings.yaml.
        //
        // This was the only config file with no validator at all, while it carries the arithmetic every
        // other system reads: a curve whose thresholds do not descend, a LevelUpChance authored as 25
        // instead of 0.25, or a Table generator with no table for its span all produce a level
        // distribution nobody wrote, and every one of them used to land silently.
        //
        // An empty document is the only ERROR - nothing downstream can do anything with it. The rest are
        // warnings: the file is still usable, and refusing it outright would lock an admin out over one
        // bad line in one biome.
        internal static ValidationReport ValidateLevelSettings(CreatureLevelSettings next, CreatureLevelSettings previous) {
            ValidationReport report = new ValidationReport();
            if (next == null) { return report.Error("it parsed to nothing"); }

            bool hasDefaultCurve = next.DefaultCreatureLevelUpChance != null && next.DefaultCreatureLevelUpChance.Count > 0;
            bool hasDefaultGenerators = LevelGeneratorResolver.HasGenerators(next.DefaultLevelupGenerators, next.DefaultLevelupGeneratorRefs);
            if (hasDefaultCurve == false && hasDefaultGenerators == false) {
                // DetermineLevelRollResult walks an empty table without selecting anything and the minimum
                // clamp turns that into level 1, so this silently removes stars from the entire world.
                return report.Error("DefaultCreatureLevelUpChance is empty and no DefaultLevelupGenerators are configured, which puts every creature at level 1");
            }
            if (hasDefaultCurve) { CheckCurve(next.DefaultCreatureLevelUpChance, "DefaultCreatureLevelUpChance", report); }

            HashSet<string> knownGenerators = new HashSet<string>();
            if (next.CustomLevelupGenerators != null) {
                foreach (KeyValuePair<string, List<LevelGenerator>> kvp in next.CustomLevelupGenerators) {
                    knownGenerators.Add(kvp.Key);
                }
                foreach (KeyValuePair<string, List<LevelGenerator>> kvp in next.CustomLevelupGenerators) {
                    CheckGenerators(kvp.Value, $"CustomLevelupGenerators['{kvp.Key}']", next, report);
                }
            }

            CheckGenerators(next.DefaultLevelupGenerators, "DefaultLevelupGenerators", next, report);
            CheckGeneratorRefs(next.DefaultLevelupGeneratorRefs, "DefaultLevelupGeneratorRefs", knownGenerators, report);

            if (next.BiomeConfiguration != null) {
                foreach (KeyValuePair<Heightmap.Biome, BiomeSpecificSetting> kvp in next.BiomeConfiguration) {
                    if (kvp.Value == null) { continue; }
                    string where = $"BiomeConfiguration[{kvp.Key}]";
                    CheckCurve(kvp.Value.CustomCreatureLevelUpChance, $"{where}.CustomCreatureLevelUpChance", report);
                    CheckGenerators(kvp.Value.LevelupGenerators, $"{where}.LevelupGenerators", next, report);
                    CheckGeneratorRefs(kvp.Value.LevelupGeneratorRefs, $"{where}.LevelupGeneratorRefs", knownGenerators, report);
                    if (kvp.Value.BiomeMinLevelOverride > 0 && kvp.Value.BiomeMaxLevelOverride > 0 && kvp.Value.BiomeMinLevelOverride > kvp.Value.BiomeMaxLevelOverride) {
                        report.Warn($"{where} has BiomeMinLevelOverride {kvp.Value.BiomeMinLevelOverride} above BiomeMaxLevelOverride {kvp.Value.BiomeMaxLevelOverride}; the maximum wins and the minimum is unreachable.");
                    }
                }
            }

            if (next.CreatureConfiguration != null) {
                foreach (KeyValuePair<string, CreatureSpecificSetting> kvp in next.CreatureConfiguration) {
                    if (kvp.Value == null) { continue; }
                    string where = $"CreatureConfiguration['{kvp.Key}']";
                    CheckCurve(kvp.Value.CustomCreatureLevelUpChance, $"{where}.CustomCreatureLevelUpChance", report);
                    CheckGenerators(kvp.Value.LevelupGenerators, $"{where}.LevelupGenerators", next, report);
                    CheckGeneratorRefs(kvp.Value.LevelupGeneratorRefs, $"{where}.LevelupGeneratorRefs", knownGenerators, report);
                }
            }

            if (next.ConditionalCreatureLevelupChance != null) {
                foreach (KeyValuePair<string, Dictionary<Heightmap.Biome, ConditionalLevelupChance>> byKey in next.ConditionalCreatureLevelupChance) {
                    if (byKey.Value == null) { continue; }
                    foreach (KeyValuePair<Heightmap.Biome, ConditionalLevelupChance> byBiome in byKey.Value) {
                        if (byBiome.Value == null) { continue; }
                        string where = $"ConditionalCreatureLevelupChance['{byKey.Key}'][{byBiome.Key}]";
                        CheckGenerators(byBiome.Value.LevelupGenerators, $"{where}.LevelupGenerators", next, report);
                        CheckGeneratorRefs(byBiome.Value.LevelupGeneratorRefs, $"{where}.LevelupGeneratorRefs", knownGenerators, report);
                    }
                }
            }

            if (next.LevelupWeightTablesBySpan != null) {
                foreach (KeyValuePair<int, SortedDictionary<int, float>> kvp in next.LevelupWeightTablesBySpan) {
                    if (kvp.Value == null || kvp.Value.Count == 0) {
                        report.Warn($"LevelupWeightTablesBySpan[{kvp.Key}] is empty; a Table generator with that span falls back to the Exponential curve.");
                        continue;
                    }
                    if (kvp.Value.Count < kvp.Key) {
                        report.Warn($"LevelupWeightTablesBySpan[{kvp.Key}] has only {kvp.Value.Count} entries for a {kvp.Key}-level span; a Table generator using it falls back to the Exponential curve.");
                    }
                    CheckCurve(kvp.Value, $"LevelupWeightTablesBySpan[{kvp.Key}]", report);
                }
            }

            return report;
        }

        // Thresholds are consumed by DetermineLevelRollResult, which selects the FIRST level whose
        // threshold the roll clears. A curve that does not descend therefore makes every level after the
        // rise unreachable - the earlier, easier entry always wins.
        private static void CheckCurve(SortedDictionary<int, float> curve, string where, ValidationReport report) {
            if (curve == null || curve.Count == 0) { return; }
            float previous = 0f;
            int previousLevel = 0;
            foreach (KeyValuePair<int, float> kvp in curve) {
                if (kvp.Value > 100f) {
                    report.Warn($"{where} sets level {kvp.Key} to {kvp.Value}, above the 100 roll ceiling; that level can never be rolled.");
                } else if (kvp.Value < 0f) {
                    report.Warn($"{where} sets level {kvp.Key} to {kvp.Value}; thresholds are a 0-100 roll requirement and a negative one always succeeds.");
                }
                if (previousLevel != 0 && kvp.Value > previous) {
                    report.Warn($"{where} rises from {previous} at level {previousLevel} to {kvp.Value} at level {kvp.Key}; thresholds must descend, so level {kvp.Key} is unreachable.");
                }
                previous = kvp.Value;
                previousLevel = kvp.Key;
            }
        }

        private static void CheckGenerators(List<LevelGenerator> generators, string where, CreatureLevelSettings settings, ValidationReport report) {
            if (generators == null) { return; }
            for (int i = 0; i < generators.Count; i++) {
                LevelGenerator gen = generators[i];
                if (gen == null) { continue; }
                string label = $"{where}[{i}]";
                if (gen.LevelUpChance < 0f || gen.LevelUpChance > 1f) {
                    // GetLevelUpDefinition clamps LevelUpChance * 100 into 0.01..100, so 25 reads as 100
                    // and 0.25 reads as 25 - a factor-of-four difference with no complaint.
                    report.Warn($"{label} has LevelUpChance {gen.LevelUpChance}; it is a 0-1 fraction (0.25 means 25%) and is clamped, so anything above 1 behaves as 1.");
                }
                if (gen.MinLevel > gen.MaxLevel) {
                    report.Warn($"{label} has MinLevel {gen.MinLevel} above MaxLevel {gen.MaxLevel}; they are swapped on use, so the curve is not the one written.");
                }
                if (gen.MinLevel < 1) {
                    report.Warn($"{label} has MinLevel {gen.MinLevel}; levels start at 1 (level 1 has no stars).");
                }
                if (gen.LevelupCalculationStyle != LevelupCalculationStyle.Table) { continue; }
                int span = Mathf.Abs(gen.MaxLevel - gen.MinLevel) + 1;
                SortedDictionary<int, float> shape = null;
                settings.LevelupWeightTablesBySpan?.TryGetValue(span, out shape);
                if (shape == null || shape.Count < span) {
                    report.Warn($"{label} uses the Table style but LevelupWeightTablesBySpan has no {span}-entry table for its span; it falls back to the Exponential curve.");
                }
            }
        }

        private static void CheckGeneratorRefs(List<string> refs, string where, HashSet<string> known, ValidationReport report) {
            if (refs == null) { return; }
            foreach (string name in refs) {
                if (name == null || known.Contains(name)) { continue; }
                report.Warn($"{where} names '{name}', which CustomLevelupGenerators does not define." + ConfigValidation.SuggestKey(name, known));
            }
        }

        // Everything that has to happen once new level settings exist, whatever produced them -- a hand
        // edit, a server broadcast, or the in-game editor. Registered as the Apply hook for
        // LevelSettings.yaml, so all three routes run identically.
        internal static void ApplyLoaded(DataObjects.CreatureLevelSettings parsed) {
            // A structurally valid but empty document deserializes to a non-null object with null
            // sections, and assigning it silently drops every creature to level 1. RaidsData and
            // LocationResetData already guard this; levels did not.
            if (parsed == null) {
                Logger.LogWarning("Level settings parsed to nothing; keeping the built-in defaults.");
                parsed = DefaultConfiguration;
            }
            SLE_Level_Settings = parsed;
            // Snapshot BEFORE the generators are expanded. ApplyLevelupGenerators overwrites
            // DefaultCreatureLevelUpChance (and the biome/creature curves) in place, so without this copy
            // the hand-authored curves are gone from memory as soon as a generator exists -- and the
            // in-game editor, which re-serializes these settings, would write the expansion back to disk
            // as though the admin had typed it, with no way to get the original curve back.
            AuthoredLevelSettings = CloneSettings(parsed);
            ApplyLevelupGenerators();
            Logger.LogDebug("Loaded new Star Level Creature settings, updating loaded creatures...");
            DistanceScaleSystem.DelayedMinimapSetup();
            CompositeLazyCache.FlushCache();
            ConditionalScaleSystem.ResetCache();
            // Coroutine rather than a straight loop: this walks every Character in the scene and is
            // budgeted per frame. Harmless at startup, where the scene is empty.
            // Character.GetAllCharacters() is the live registry - Resources.FindObjectsOfTypeAll also
            // walked every loaded asset (including prefabs) synchronously on each reload.
            // Only one pass runs at a time: two overlapping passes share the ForceUpdateHealth/Size
            // flags, and whichever finished first cleared them mid-run for the other.
            var runner = TaskRunner.Run();
            if (runningAttributeUpdate != null) {
                runner.StopCoroutine(runningAttributeUpdate);
                runningAttributeUpdate = null;
            }
            runningAttributeUpdate = runner.StartCoroutine(UpdateCreatureAttributes(new List<Character>(Character.GetAllCharacters())));
        }

        private static Coroutine runningAttributeUpdate;

        // The settings exactly as they were authored, before ApplyLevelupGenerators expanded any generator
        // into the levelup-chance tables. This is what an editor must serialize from; SLE_Level_Settings
        // carries the expansion. Null only if the copy itself failed, in which case callers fall back.
        internal static DataObjects.CreatureLevelSettings AuthoredLevelSettings;

        // Deep copy through the yaml round trip, the same way the in-game editor copies config it must not
        // mutate in place.
        private static DataObjects.CreatureLevelSettings CloneSettings(DataObjects.CreatureLevelSettings source) {
            if (source == null) { return null; }
            try {
                return DataObjects.yamlDeserializer.Deserialize<DataObjects.CreatureLevelSettings>(
                    DataObjects.yamlSerializer.Serialize(source));
            } catch (Exception e) {
                Logger.LogWarning($"Could not snapshot the authored level settings: {e.Message}");
                return null;
            }
        }

        // Expands any configured level generators (inline or referenced via CustomLevelupGenerators) into the
        // levelup-chance tables of the default/biome/creature sections, overwriting the existing chances when
        // generators are present. Runs against the freshly deserialized settings so the shared DefaultConfiguration
        // static is never mutated.
        private static void ApplyLevelupGenerators() {
            CreatureLevelSettings settings = SLE_Level_Settings;
            if (settings == null) { return; }

            SortedDictionary<int, float> defaultChances = LevelGeneratorResolver.BuildLevelupChance(settings.DefaultLevelupGenerators, settings.DefaultLevelupGeneratorRefs);
            if (defaultChances != null) { settings.DefaultCreatureLevelUpChance = defaultChances; }

            if (settings.BiomeConfiguration != null) {
                foreach (BiomeSpecificSetting biome in settings.BiomeConfiguration.Values) {
                    if (biome == null) { continue; }
                    SortedDictionary<int, float> biomeChances = LevelGeneratorResolver.BuildLevelupChance(biome.LevelupGenerators, biome.LevelupGeneratorRefs);
                    if (biomeChances != null) { biome.CustomCreatureLevelUpChance = biomeChances; }
                }
            }

            if (settings.CreatureConfiguration != null) {
                foreach (CreatureSpecificSetting creature in settings.CreatureConfiguration.Values) {
                    if (creature == null) { continue; }
                    SortedDictionary<int, float> creatureChances = LevelGeneratorResolver.BuildLevelupChance(creature.LevelupGenerators, creature.LevelupGeneratorRefs);
                    if (creatureChances != null) { creature.CustomCreatureLevelUpChance = creatureChances; }
                }
            }
        }

        internal static IEnumerator UpdateCreatureAttributes(List<Character> characters) {
            int i = 0;
            WaitForSeconds sleep = new WaitForSeconds(0.1f);
            HealthModifications.ForceUpdateHealth = true;
            // Without this, SetSizeModification short-circuits on the persisted SLS_SIZE and a changed
            // Size/SizePerLevel would not apply to already-spawned creatures.
            SizeModifications.ForceUpdateSize = true;
            try {
                foreach (var character in characters) {
                    if (i >= ValConfig.NumberOfCacheUpdatesPerFrame.Value) {
                        yield return sleep;
                        i = 0;
                    }
                    if (character == null || character.m_nview == null || character.m_nview.IsValid() == false) { continue; }
                    CreatureSetupControl.CreatureSetup(character, delay: 0);
                    i++;
                }
            } finally {
                // Also runs when a newer config apply stops this coroutine mid-pass (iterator
                // disposal executes finally blocks), so the force flags can't be left stuck on.
                HealthModifications.ForceUpdateHealth = false;
                SizeModifications.ForceUpdateSize = false;
                runningAttributeUpdate = null;
            }
        }
    }
}
