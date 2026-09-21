using HarmonyLib;
using Jotunn.Managers;
using StarLevelSystem.common;
using StarLevelSystem.Data;
using StarLevelSystem.modules.LevelSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
using static StarLevelSystem.common.DataObjects;

namespace StarLevelSystem.modules.UI {
    internal static class QuickConfigureTool {

        // --- layout constants ---
        private const float PanelW = 900f;
        private const float PanelH = 690f;
        private const float Margin = 26f;
        private const float ContentTop = 92f;
        private const float RowHeight = 34f;
        private const float SubRowHeight = 26f;
        private const float RowGap = 4f;
        private const int PageCount = 5;

        // Sample creatures data | TODO: allow selecting different creature examples?
        private const float TrollHp = 600f;
        private const float TrollDmg = 70f;
        private const float TheElderHP = 2500f;
        private const float TheElderDmg = 60f;

        private static readonly string[] CalcStyleOptions = Enum.GetNames(typeof(DataObjects.LevelupCalculationStyle));
        private static readonly string[] DisplayStyleOptions = Enum.GetNames(typeof(DataObjects.ModifierDisplayStyle));

        // Brief, player-facing descriptions for each modifier (see Package/README.md), keyed by ModifierNames.
        private static readonly Dictionary<string, string> ModifierDescriptions = new Dictionary<string, string>() {
            { "BossSummoner", "Summons minion creatures at regular intervals." },
            { "SoulEater", "Grows stronger as nearby creatures die; self-heals." },
            { "LifeLink", "Redirects some damage taken to a nearby creature." },
            { "Splitter", "Spawns replacement creatures when it dies." },
            { "Lootbags", "Tankier and faster; drops extra loot." },
            { "Fire", "Adds fire damage to its attacks." },
            { "Frost", "Adds frost damage to its attacks." },
            { "Poison", "Adds poison damage to its attacks." },
            { "Lightning", "Adds lightning damage to its attacks." },
            { "FireNova", "Explodes in fire on death, damaging nearby targets." },
            { "FrostNova", "Explodes in frost on death, damaging nearby targets." },
            { "PoisonNova", "Explodes in poison on death, damaging nearby targets." },
            { "LightningNova", "Explodes in lightning on death, damaging nearby targets." },
            { "Evolving", "Gains a level after enough kills." },
            { "ResistSlash", "Reduces damage taken from slash." },
            { "ResistBlunt", "Reduces damage taken from blunt." },
            { "ResistPierce", "Reduces damage taken from pierce (e.g. arrows)." },
            { "ResistFire", "Reduces damage taken from fire." },
            { "ResistFrost", "Reduces damage taken from frost." },
            { "ResistPoison", "Reduces damage taken from poison." },
            { "ResistSpirit", "Reduces damage taken from spirit." },
            { "Alert", "Increases the creature's hearing range." },
            { "Big", "Increases the creature's size." },
            { "Fast", "Increases the creature's movement speed." },
            { "StaminaDrain", "Its attacks drain your stamina. Dodging avoids it; blocking or parrying lessens it." },
            { "EitrDrain", "Its attacks drain your eitr. Dodging avoids it; blocking or parrying lessens it." },
            { "Brutal", "Increases the creature's attack speed." },
            { "ElementalChaos", "Adds random elemental damage on each hit." },
        };

        private static Sprite DistanceExample;
        private static Sprite ZoneExample;

        // --- runtime state ---
        private static GameObject panel;
        private static GameObject[] pageRoots;
        private static int currentPage;
        private static Text titleText;
        private static Text generatorPreviewText;
        private static Text tableWarnText;
        private static Text messageText;
        private static GameObject backBtn;
        private static GameObject cancelBtn;
        private static GameObject nextBtn;
        private static GameObject applyBtn;
        private static Text creatureExampleText;
        private static Text bossExampleText;
        private static StagedConfig staged;

        private const string LauncherEntry = "Star Level System";

        internal static void Init() {
            DistanceExample = StarLevelSystem.EmbeddedResourceBundle.LoadAsset<Sprite>("distance_rings");
            ZoneExample = StarLevelSystem.EmbeddedResourceBundle.LoadAsset<Sprite>("region_zones");

            // The corner button, the main-menu hook and the pause-menu patch all used to live here. They
            // now belong to the shared launcher in common/ConfigUI, so several mods share one button
            // instead of stacking one each in the same corner. See its README for the cross-assembly
            // contract.
            ConfigUILauncher.Init();
            ApplyRegistration();
        }

        // SettingChanged handler for the client toggle.
        public static void OnShowButtonChanged(object s, EventArgs e) {
            ApplyRegistration();
        }

        private static void ApplyRegistration() {
            if (ValConfig.ShowQuickConfigureButton.Value == false) {
                ConfigUILauncher.Unregister(LauncherEntry);
                return;
            }

            // Off-host, this tool can only half work: ApplyAndSave writes ~25 BepInEx ConfigEntry values,
            // and Jotunn only pushes a remote admin's changed entries from SynchronizeChangedConfig, which
            // is internal and fires when the ConfigurationManager window closes -- not from here. If that
            // method cannot be reached, do not offer the button off-host at all. Better to be missing than
            // to look like it worked.
            if (IsOwner() == false && CanPushRemoteConfig() == false) {
                ConfigUILauncher.Unregister(LauncherEntry);
                return;
            }

            ConfigUILauncher.Register(LauncherEntry, OpenPanel);
        }

        private static bool IsOwner() {
            return ZNet.instance == null || ZNet.instance.IsServer();
        }

        private static MethodInfo syncChangedConfig;
        private static bool syncChangedConfigResolved;

        private static bool CanPushRemoteConfig() {
            if (syncChangedConfigResolved == false) {
                syncChangedConfigResolved = true;
                syncChangedConfig = AccessTools.Method(typeof(SynchronizationManager), "SynchronizeChangedConfig");
                if (syncChangedConfig == null) {
                    Logger.LogWarning("Jotunn's SynchronizeChangedConfig could not be found, so a remote admin " +
                        "cannot push config changes. The quick configure button will not be offered off-host.");
                }
            }
            return syncChangedConfig != null;
        }

        // Reflection into a private Jotunn method, knowingly: it is the only way a remote admin's
        // ConfigEntry edits reach the server without opening the ConfigurationManager window. Guarded, and
        // the registration above declines to offer the button at all when it is missing.
        private static void PushRemoteConfigChanges() {
            if (IsOwner() || CanPushRemoteConfig() == false) { return; }
            try {
                syncChangedConfig.Invoke(SynchronizationManager.Instance, null);
            } catch (Exception e) {
                Logger.LogWarning($"Could not push config changes to the server: {e.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------------
        //  Panel Creation
        // ------------------------------------------------------------------------------------------------

        internal static void OpenPanel() {
            staged = StagedConfig.Snapshot();
            // Through ClosePanel so the widget references and any live edit-result subscription are torn
            // down too. Escape and scene changes destroy the panel GameObject directly, which leaves
            // those behind.
            ClosePanel();
            try {
                BuildPanel();
            } catch (Exception e) {
                Logger.LogWarning($"QuickConfigureTool failed to build panel: {e}");
                if (panel != null) { UnityEngine.Object.Destroy(panel); panel = null; }
                return;
            }
            currentPage = 0;
            ShowPage(0);
        }

        private static void ClosePanel() {
            // Unhook first: the panel can also be destroyed by Escape or by a scene change, and a
            // subscription that outlives the window would write into destroyed Text components.
            UnhookEditResults();
            pendingRemoteEdits.Clear();
            messageText = null;
            generatorPreviewText = null;
            tableWarnText = null;
            titleText = null;
            if (panel != null) {
                UnityEngine.Object.Destroy(panel);
                panel = null;
            }
        }

        private static void BuildPanel() {
            // Through ConfigUI.CreatePanel, never a raw CreateWoodpanel: that is the only thing that
            // attaches ConfigUIInputGuard. Without it every keystroke typed into this panel's ~25 input
            // fields also reaches the game, so entering a number walks the character around.
            panel = ConfigUI.CreatePanel("StarLevelSystem - Quick Configure", PanelW, PanelH, out Transform body, out titleText);
            ConfigUI.AddCloseX(body, PanelW, ClosePanel);

            // Build out the page skeletons
            pageRoots = new GameObject[PageCount];
            for (int i = 0; i < PageCount; i++) {
                pageRoots[i] = ConfigUI.NewRect(
                    name: "Page" + i, 
                    parent: panel.transform,
                    x: Margin,
                    y: ContentTop,
                    w: PanelW - 2 * Margin,
                    h: PanelH - ContentTop - 70f);
            }

            BuildScalingPage(pageRoots[0].transform);
            BuildStatsPage(pageRoots[1].transform);
            BuildModifiersPage(pageRoots[2].transform);
            BuildRaidsPage(pageRoots[3].transform);
            BuildNemesisPage(pageRoots[4].transform);

            float navY = PanelH - 56f;
            // Status line between the nav buttons. ConfigUI.SetMessages exists for exactly this and had
            // no caller, which is why a refused save produced nothing an admin could see.
            messageText = ConfigUI.AddText(panel.transform, Margin + 300f, navY + 4f,
                PanelW - 2 * Margin - 490f, RowHeight, "", 13, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.4f));
            backBtn = ConfigUI.AddButton(panel.transform, Margin, navY, 130f, "< Back", () => ShowPage(currentPage - 1));
            cancelBtn = ConfigUI.AddButton(panel.transform, Margin + 150f, navY, 130f, "Cancel", ClosePanel);
            nextBtn = ConfigUI.AddButton(panel.transform, PanelW - Margin - 170f, navY, 170f, "Next >", () => ShowPage(currentPage + 1));
            applyBtn = ConfigUI.AddButton(panel.transform, PanelW - Margin - 170f, navY, 170f, "Apply & Save", ApplyAndSave);
        }

        // Moves to the current page
        // Sets the Title
        private static void ShowPage(int page) {
            currentPage = Mathf.Clamp(page, 0, PageCount - 1);
            for (int i = 0; i < pageRoots.Length; i++) {
                pageRoots[i].SetActive(i == currentPage);
            }
            string[] names = { "Scaling Mechanisms", "Stats & Level Generator", "Modifiers", "Raids", "Nemesis System" };
            titleText.text = $"StarLevelSystem - {names[currentPage]}  (Page {currentPage + 1} of {PageCount})";

            backBtn.SetActive(currentPage > 0);
            bool last = currentPage == PageCount - 1;
            nextBtn.SetActive(!last);
            applyBtn.SetActive(last);
            if (currentPage == 1) { UpdateExampleMath(); }
        }

        // ------------------------------------------------------------------------------------------------
        //  Pages
        // ------------------------------------------------------------------------------------------------

        private static void BuildScalingPage(Transform parent) {
            const float ColWidth = 760f;   // left config column + gap + image(300)
            const float ImgW = 300f;
            const float ImgH = 168f;
            const float LeftColW = ColWidth - ImgW;   // configuration area to the left of the example image

            // Build each row as its own container, collect them in order, then space the column out in one pass.
            List<GameObject> column = new List<GameObject> {
                ConfigUI.AddHeaderRow(parent, LeftColW, "$sls_cfg_scaling_selection_header", TextAnchor.MiddleCenter),
                ConfigUI.AddTextRow(parent, ColWidth, 34f, "Here are some high level configurations from the mod, many more things can be customized within the yaml configuration.", 13, GUIManager.Instance.ValheimBeige, TextAnchor.UpperCenter),
                AddScalingFeatureRow(parent, ColWidth, ImgW, ImgH,
                    DistanceExample,
                    "$sls_cfg_distance_scale_header",
                    "$sls_cfg_distance_scale_desc",
                    staged.enableDistance,
                    v => staged.enableDistance = v,
                    "$sls_cfg_distance_overlay_toggle",
                    staged.enableDistanceOverlay,
                    v => staged.enableDistanceOverlay = v),
                ConfigUI.AddDividerRow(parent, ColWidth),
                AddScalingFeatureRow(parent, ColWidth, ImgW, ImgH,
                    ZoneExample,
                    "$sls_cfg_zone_scale_header",
                    "$sls_cfg_zone_scale_desc",
                    staged.enableZone,
                    v => staged.enableZone = v,
                    "$sls_cfg_zone_overlay_toggle",
                    staged.enableZoneOverlay,
                    v => staged.enableZoneOverlay = v),
                ConfigUI.AddDividerRow(parent, ColWidth),
                ConfigUI.AddToggleRow(parent, ColWidth, 360f, "$sls_cfg_conditional_scale_header", staged.enableConditional, v => staged.enableConditional = v, true),
                ConfigUI.AddTextRow(parent, ColWidth, 24f, "$sls_cfg_conditional_scale_desc", 13, GUIManager.Instance.ValheimBeige),
            };
            // Center the column within the page root so it isn't left-biased (page root is PanelW - 2*Margin wide).
            float colOffsetX = Mathf.Max(0f, (PanelW - 2 * Margin - ColWidth) * 0.5f);
            ConfigUI.LayoutColumn(column, colOffsetX, 4f);
        }

        private static void BuildStatsPage(Transform parent) {
            const float RightColumnX = 450f;
            const float LeftColWidth = 430f;
            const float RightColWidth = PanelW - 2 * Margin - RightColumnX;
            const float LabelWidth = 168f;
            const float SliderWidth = 150f;
            const float ValueWidth = 60f;
            const float StartY = 2f;
            const float DividerH = 12f;

            // Left column - stat multipliers and multiplayer scaling. A full-width divider separates the
            // creature stats (above) from the boss stats (below).
            List<GameObject> left = new List<GameObject> {
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Per-level stats"),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Creature HP / level", 0f, 5f, staged.creatureHpPerLevel, false, v => { staged.creatureHpPerLevel = v; UpdateExampleMath(); }),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Creature dmg / level", 0f, 2f, staged.creatureDmgPerLevel, false, v => { staged.creatureDmgPerLevel = v; UpdateExampleMath(); }),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Max level (stars)", 1f, 200f, staged.maxLevel, true, v => { staged.maxLevel = (int)v; UpdateExampleMath(); }),
                ConfigUI.AddDividerRow(parent, PanelW - 2 * Margin, DividerH),   // spans both columns, between creature and boss sections
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Boss HP / level", 0f, 5f, staged.bossHpPerLevel, false, v => { staged.bossHpPerLevel = v; UpdateExampleMath(); }),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Boss dmg / level", 0f, 5f, staged.bossDmgPerLevel, false, v => { staged.bossDmgPerLevel = v; UpdateExampleMath(); }),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Max boss level", 1f, 200f, staged.maxBossLevel, true, v => { staged.maxBossLevel = (int)v; UpdateExampleMath(); }),
                ConfigUI.AddSpacerRow(parent, LeftColWidth, 4f),
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Multiplayer scaling"),
                ConfigUI.AddToggleRow(parent, LeftColWidth, LabelWidth + 170f, "Enemies gain HP with more players", staged.mpHealth, v => staged.mpHealth = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "HP per extra player", 0f, 0.99f, staged.mpHealthMod, false, v => staged.mpHealthMod = v),
                ConfigUI.AddToggleRow(parent, LeftColWidth, LabelWidth + 170f, "Enemies gain dmg with more players", staged.mpDamage, v => staged.mpDamage = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Dmg per extra player", 0f, 2f, staged.mpDamageMod, false, v => staged.mpDamageMod = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Players needed nearby", 1f, 20f, staged.mpRequiredPlayers, true, v => staged.mpRequiredPlayers = (int)v),
            };
            ConfigUI.LayoutColumn(left, 0f, StartY);

            // Right column - example previews aligned to the matching left-column stat rows: the creature (Troll)
            // example sits beside the Creature HP/dmg sliders, the boss (The Elder) example beside the Boss HP/dmg
            // sliders. The default level generator is laid out below them.
            const float RowPitch = RowHeight + RowGap;
            GameObject exHeader = ConfigUI.AddHeaderRow(parent, RightColWidth, "Example scaling preview");
            ConfigUI.PositionRow(exHeader, RightColumnX, StartY);

            GameObject creatureEx = ConfigUI.AddTextRow(parent, RightColWidth, 80f, "", 15, GUIManager.Instance.ValheimBeige);
            creatureExampleText = creatureEx.GetComponentInChildren<Text>();
            ConfigUI.PositionRow(creatureEx, RightColumnX, StartY + RowPitch);          // aligns with "Creature HP / level"

            // The divider between the creature and boss sections pushes the boss rows down by its height + gap;
            // keep the boss example (and the generator below it) aligned to that shift.
            float bossShift = DividerH + RowGap;
            GameObject bossEx = ConfigUI.AddTextRow(parent, RightColWidth, 80f, "", 15, GUIManager.Instance.ValheimBeige);
            bossExampleText = bossEx.GetComponentInChildren<Text>();
            ConfigUI.PositionRow(bossEx, RightColumnX, StartY + 4 * RowPitch + bossShift);   // aligns with "Boss HP / level"

            // Default level generator below the previews. The Gaussian offset row is tracked so it can be shown
            // only when the Gaussian curve style is selected.
            float genStartY = StartY + 6 * RowPitch + bossShift + 8f;
            GameObject tableWarnRow = null;
            List<GameObject> gaussianRows = new List<GameObject>();
            List<GameObject> gen = new List<GameObject> { ConfigUI.AddHeaderRow(parent, RightColWidth, "Default level generator") };

            // Rows that only mean anything once the generator is switched on.
            List<GameObject> genBody = new List<GameObject>();

            void Relayout() {
                bool on = staged.useGenerator;
                foreach (GameObject row in genBody) {
                    if (row == null) { continue; }
                    if (gaussianRows.Contains(row)) {
                        row.SetActive(on && staged.generator.LevelupCalculationStyle == LevelupCalculationStyle.Gaussian);
                    } else if (row == tableWarnRow) {
                        row.SetActive(on && TableShapeMissing(staged.generator));
                    } else {
                        row.SetActive(on);
                    }
                }
                ConfigUI.LayoutColumn(gen, RightColumnX, genStartY);
                UpdateGeneratorPreview();
            }

            // Opting in is explicit. A generator replaces DefaultCreatureLevelUpChance wholesale on the
            // next load, so it must never appear in the file just because someone opened this panel.
            gen.Add(ConfigUI.AddToggleRow(parent, RightColWidth, LabelWidth + 80f, "Use level generator",
                staged.useGenerator, v => { staged.useGenerator = v; Relayout(); }, true));

            void AddBodyRow(GameObject row) { gen.Add(row); genBody.Add(row); }

            // "Curve start"/"Curve end", not "Min level"/"Max level": these shape one curve inside
            // LevelSettings.yaml and are not the global star cap, which is the MaxLevel slider on the
            // left. The old labels read as a guaranteed minimum, which this is not - the real floor is
            // BiomeMinLevelOverride / CreatureMinLevelOverride.
            GameObject curveStartRow = null, curveEndRow = null;

            // Pushing the corrected value back into the other slider matters: nothing else updates a
            // slider's displayed number, so a silent correction would leave the panel showing a range it
            // is not going to save. The re-entrant call settles immediately because the second pass finds
            // the two already in order.
            void SyncSlider(GameObject row, float value) {
                Slider s = row == null ? null : row.GetComponentInChildren<Slider>();
                if (s != null && Mathf.Approximately(s.value, value) == false) { s.value = value; }
            }

            curveStartRow = ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Curve start level", 1f, 50f, staged.generator.MinLevel, true, v => {
                staged.generator.MinLevel = (int)v;
                if (staged.generator.MaxLevel < staged.generator.MinLevel) {
                    staged.generator.MaxLevel = staged.generator.MinLevel;
                    SyncSlider(curveEndRow, staged.generator.MaxLevel);
                }
                Relayout();
            });
            AddBodyRow(curveStartRow);
            curveEndRow = ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Curve end level", 1f, 200f, staged.generator.MaxLevel, true, v => {
                staged.generator.MaxLevel = (int)v;
                if (staged.generator.MinLevel > staged.generator.MaxLevel) {
                    staged.generator.MinLevel = staged.generator.MaxLevel;
                    SyncSlider(curveStartRow, staged.generator.MinLevel);
                }
                Relayout();
            });
            AddBodyRow(curveEndRow);
            AddBodyRow(ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Level-up chance", 0f, 1f, staged.generator.LevelUpChance, false, v => {
                staged.generator.LevelUpChance = v;
                UpdateGeneratorPreview();
            }));
            AddBodyRow(ConfigUI.AddEnumCycleRow(parent, RightColWidth, LabelWidth, 150f, "Curve style", CalcStyleOptions, (int)staged.generator.LevelupCalculationStyle, i => {
                staged.generator.LevelupCalculationStyle = (LevelupCalculationStyle)i;
                Relayout();
            }));
            GameObject offsetRow = ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Gaussian offset", -1f, 1f, staged.generator.GaussianOffset, false, v => {
                staged.generator.GaussianOffset = v;
                UpdateGeneratorPreview();
            });
            gaussianRows.Add(offsetRow);
            AddBodyRow(offsetRow);
            // Width of the bell. It used to be driven by the level-up chance slider, which is why that
            // slider appeared to do nothing under this style.
            GameObject spreadRow = ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Gaussian spread", 0.05f, 1f, staged.generator.GaussianSpread, false, v => {
                staged.generator.GaussianSpread = v;
                UpdateGeneratorPreview();
            });
            gaussianRows.Add(spreadRow);
            AddBodyRow(spreadRow);
            AddBodyRow(ConfigUI.AddSliderRow(parent, RightColWidth, LabelWidth, SliderWidth, ValueWidth, "Night multiplier", 0f, 5f, staged.generator.NightMultiplier, false, v => staged.generator.NightMultiplier = v));

            // Table style only works for level counts that have a hand-authored shape in
            // LevelupWeightTablesBySpan, and there is no editor for those here. Saying so beats the
            // silent collapse to a single level that used to happen.
            tableWarnRow = ConfigUI.AddTextRow(parent, RightColWidth, 30f, "", 12, new Color(1f, 0.6f, 0.4f));
            tableWarnText = tableWarnRow.GetComponentInChildren<Text>();
            AddBodyRow(tableWarnRow);

            // A line of plain arithmetic beneath the sliders. Without it every curve style is edited
            // blind: the styles differ enormously and none of them is visible until you are in the world.
            GameObject previewRow = ConfigUI.AddTextRow(parent, RightColWidth, 46f, "", 12, GUIManager.Instance.ValheimBeige);
            generatorPreviewText = previewRow.GetComponentInChildren<Text>();
            AddBodyRow(previewRow);

            Relayout();
            UpdateExampleMath();
        }

        private static void BuildRaidsPage(Transform parent) {
            const float FullWidth = PanelW - 2 * Margin;
            const float LeftColWidth = 430f;
            const float LabelWidth = 235f, SliderWidth = 130f, ValueWidth = 56f;
            const float ToggleLabelWidth = 300f;
            const float StartY = 4f;

            // Full-width header + intro across the top.
            GameObject header = ConfigUI.AddHeaderRow(parent, FullWidth, "Raids", TextAnchor.MiddleCenter);
            ConfigUI.PositionRow(header, 0f, StartY);
            GameObject intro = ConfigUI.AddTextRow(parent, FullWidth, 40f, "StarLevelSystem replaces vanilla raids with its own configurable raids. Detailed per-raid settings live in RaidSettings.yaml.", 13, GUIManager.Instance.ValheimBeige, TextAnchor.UpperCenter);
            ConfigUI.PositionRow(intro, 0f, StartY + RowHeight + RowGap);
            float colStartY = StartY + RowHeight + RowGap + 40f + 8f;

            // Left column - global raid settings.
            List<GameObject> left = new List<GameObject> {
                ConfigUI.AddToggleRow(parent, LeftColWidth, ToggleLabelWidth, "Enable SLS Raids", staged.enableSlsRaids, v => staged.enableSlsRaids = v, true),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Raid frequency (lower = more often)", 0.1f, 10f, staged.raidEventRate, false, v => staged.raidEventRate = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Minutes between checks", 1f, 120f, staged.raidCheckMinutes, true, v => staged.raidCheckMinutes = (int)v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Max attempts / player", 0f, 50f, staged.maxRaidAttempts, true, v => staged.maxRaidAttempts = (int)v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LabelWidth, SliderWidth, ValueWidth, "Max active raids", 1f, 20f, staged.maxActiveRaids, true, v => staged.maxActiveRaids = (int)v),
            };
            ConfigUI.LayoutColumn(left, 0f, colStartY);

            // Right side - scrollable list of every configured raid, each with an enable/disable toggle.
            // Disabled raids keep their config in RaidSettings.yaml and are simply marked Enabled = false.
            const float ScrollX = 446f;
            const float ScrollW = 402f;
            const float ScrollH = 398f;
            ConfigUI.AddText(parent, ScrollX, colStartY, ScrollW, RowHeight, "Enable / disable raids", 16, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);
            GameObject scrollHolder = ConfigUI.NewRect("RaidScrollHolder", parent, ScrollX, colStartY + RowHeight, ScrollW, ScrollH);
            GameObject scrollCanvas = GUIManager.Instance.CreateScrollView(
                scrollHolder.transform, false, true, 8f, 4f,
                GUIManager.Instance.ValheimScrollbarHandleColorBlock, new Color(0f, 0f, 0f, 0.5f),
                ScrollW, ScrollH);
            Transform content = scrollCanvas.transform.Find("Scroll View/Viewport/Content");
            if (content != null) {
                float contentW = ScrollW - 16f;   // minus the vertical scrollbar + border (handleSize + 2*border)
                List<RaidDefinition> raids = staged.raidSource?.Raids;
                if (raids != null) {
                    foreach (RaidDefinition raid in raids.OrderBy(r => r.Name)) {
                        string raidName = raid.Name;   // capture for the closure
                        AddRaidEntry(content, contentW, raid, staged.raidsOn.Contains(raidName), on => {
                            if (on) { staged.raidsOn.Add(raidName); }
                            else { staged.raidsOn.Remove(raidName); }
                        });
                    }
                }
            }
        }

        // A single raid line: enable toggle on the left, prettified name + a brief spawn summary to its right.
        private static void AddRaidEntry(Transform content, float width, RaidDefinition raid, bool enabled, Action<bool> onChange) {
            GameObject row = ConfigUI.NewLayoutRow(content, width, 44f);
            GameObject tgo = GUIManager.Instance.CreateToggle(row.transform, 22f, 22f);
            tgo.transform.SetParent(row.transform, false);
            RectTransform trt = (RectTransform)tgo.transform;
            trt.localScale = Vector3.one;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(2f, -3f);
            Toggle tg = tgo.GetComponent<Toggle>();
            tg.isOn = enabled;
            tg.onValueChanged.AddListener(b => onChange(b));

            const float TextX = 32f;
            ConfigUI.AddText(row.transform, TextX, 0f, width - TextX - 4f, 20f, PrettifyRaidName(raid.Name), 14, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimOrange);
            int types = raid.Spawns != null ? raid.Spawns.Select(sp => sp.PrefabName).Distinct().Count() : 0;
            string sub = $"{types} creature type{(types == 1 ? "" : "s")}  ·  {raid.Duration:0}s";
            ConfigUI.AddText(row.transform, TextX, 20f, width - TextX - 4f, 24f, sub, 12, TextAnchor.UpperLeft, GUIManager.Instance.ValheimBeige);
        }

        // "army_eikthyr" -> "Army Eikthyr", "gjall_ambush" -> "Gjall Ambush".
        private static string PrettifyRaidName(string name) {
            if (string.IsNullOrEmpty(name)) { return name; }
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace('_', ' '));
        }

        private static void BuildNemesisPage(Transform parent) {
            const float LeftColWidth = 400f;
            const float LeftLabelWidth = 200f, LeftSliderWidth = 130f, LeftValueWidth = 60f;
            const float RightColumnX = 440f;
            const float RightColWidth = PanelW - 2 * Margin - RightColumnX;
            const float RightLabelWidth = 210f, RightSliderWidth = 120f, RightValueWidth = 60f;
            const float StartY = 4f;

            // Full-width intro describing the system.
            GameObject intro = ConfigUI.AddTextRow(parent, PanelW - 2 * Margin, 40f, "The Nemesis system is a personal game manager that can tune up or down the world around you or your group.", 14, GUIManager.Instance.ValheimBeige);
            ConfigUI.PositionRow(intro, 0f, StartY);
            float colStartY = StartY + 44f;

            // Left column - core nemesis settings.
            List<GameObject> left = new List<GameObject> {
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Nemesis settings"),
                ConfigUI.AddToggleRow(parent, LeftColWidth, LeftLabelWidth + 60f, "Enable Nemesis system", staged.enableNemesis, v => staged.enableNemesis = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Action cooldown (sec)", 0f, 120f, staged.nemCooldown, false, v => staged.nemCooldown = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Influence radius (m)", 0f, 1000f, staged.nemInfluence, false, v => staged.nemInfluence = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Min spawn distance (m)", 0f, 500f, staged.nemMinSpawn, false, v => staged.nemMinSpawn = v),
            };
            ConfigUI.LayoutColumn(left, 0f, colStartY);

            // Right column - core subset of the score system.
            List<GameObject> right = new List<GameObject> {
                ConfigUI.AddHeaderRow(parent, RightColWidth, "Score system"),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Neutral score", 0f, 20000f, staged.neutralScore, true, v => staged.neutralScore = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Min score", 0f, 20000f, staged.minScore, true, v => staged.minScore = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Max score", 0f, 20000f, staged.maxScore, true, v => staged.maxScore = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Decay per update", 0f, 2000f, staged.decayPerUpdate, true, v => staged.decayPerUpdate = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Score interval (sec)", 1f, 120f, staged.scoreInterval, true, v => staged.scoreInterval = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Boss-kill bonus", 0f, 5000f, staged.bossKillBonus, true, v => staged.bossKillBonus = v),
                ConfigUI.AddSliderRow(parent, RightColWidth, RightLabelWidth, RightSliderWidth, RightValueWidth, "Death score reduction", 0f, 5000f, staged.deathReduction, true, v => staged.deathReduction = v),
            };
            ConfigUI.LayoutColumn(right, RightColumnX, colStartY);
        }

        private static void BuildModifiersPage(Transform parent) {
            // Left column holds all of the numeric/toggle config; sliders are kept narrow so their value
            // boxes don't run into the scroll view on the right.
            const float LeftColWidth = 430f;
            const float LeftLabelWidth = 200f, LeftSliderWidth = 120f, LeftValueWidth = 56f;
            const float ToggleLabelWidth = 300f;
            const float StartY = 4f;

            List<GameObject> left = new List<GameObject> {
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Creature modifiers"),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Max major modifiers", 0f, 6f, staged.maxMajor, true, v => staged.maxMajor = (int)v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Max minor modifiers", 0f, 6f, staged.maxMinor, true, v => staged.maxMinor = (int)v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Major modifier chance", 0f, 1f, staged.chanceMajor, false, v => staged.chanceMajor = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Minor modifier chance", 0f, 1f, staged.chanceMinor, false, v => staged.chanceMinor = v),
                ConfigUI.AddToggleRow(parent, LeftColWidth, ToggleLabelWidth, "Limit modifier count to star level", staged.limitToStarLevel, v => staged.limitToStarLevel = v),
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Boss modifiers"),
                ConfigUI.AddToggleRow(parent, LeftColWidth, ToggleLabelWidth, "Bosses can have modifiers", staged.enableBossMods, v => staged.enableBossMods = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Boss modifier chance", 0f, 1f, staged.chanceBoss, false, v => staged.chanceBoss = v),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Max boss modifiers", 0f, 6f, staged.maxBossMods, true, v => staged.maxBossMods = (int)v),
                ConfigUI.AddHeaderRow(parent, LeftColWidth, "Modifier display"),
                ConfigUI.AddSliderRow(parent, LeftColWidth, LeftLabelWidth, LeftSliderWidth, LeftValueWidth, "Max name prefixes", 0f, 6f, staged.prefixLimit, true, v => staged.prefixLimit = (int)v),
                ConfigUI.AddToggleRow(parent, LeftColWidth, ToggleLabelWidth, "Minor modifiers first in name", staged.minorFirst, v => staged.minorFirst = v),
                ConfigUI.AddEnumCycleRow(parent, LeftColWidth, LeftLabelWidth, 150f, "Icon display style", DisplayStyleOptions, (int)staged.displayStyle, i => staged.displayStyle = (ModifierDisplayStyle)i),
            };
            ConfigUI.LayoutColumn(left, 0f, StartY);

            // Right side - scrollable list of every modifier defined in Modifiers.yaml, grouped by category,
            // each with an enable/disable toggle and a brief description.
            const float ScrollX = 446f;
            const float ScrollW = 400f;
            const float ScrollH = 478f;
            ConfigUI.AddText(parent, ScrollX, StartY, ScrollW, RowHeight, "Enable / disable modifiers", 16, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);
            GameObject scrollHolder = ConfigUI.NewRect("ModScrollHolder", parent, ScrollX, StartY + RowHeight, ScrollW, ScrollH);
            GameObject scrollCanvas = GUIManager.Instance.CreateScrollView(
                scrollHolder.transform, false, true, 8f, 4f,
                GUIManager.Instance.ValheimScrollbarHandleColorBlock, new Color(0f, 0f, 0f, 0.5f),
                ScrollW, ScrollH);
            Transform content = scrollCanvas.transform.Find("Scroll View/Viewport/Content");
            if (content != null) {
                float contentW = ScrollW - 16f;   // minus the vertical scrollbar + border (handleSize + 2*border)
                AddModifierCategory(content, contentW, "Boss modifiers", ModifierType.Boss, staged.modifierSource?.BossModifiers);
                AddModifierCategory(content, contentW, "Major modifiers", ModifierType.Major, staged.modifierSource?.MajorModifiers);
                AddModifierCategory(content, contentW, "Minor modifiers", ModifierType.Minor, staged.modifierSource?.MinorModifiers);
            }
        }

        // Adds a category header followed by one toggle row per modifier defined in that category.
        private static void AddModifierCategory(Transform content, float width, string label, ModifierType type, Dictionary<string, CreatureModifierConfiguration> dict) {
            if (dict == null || dict.Count == 0) { return; }
            AddModifierCategoryHeader(content, width, label);
            HashSet<string> enabled = staged.modifierOn[type];
            foreach (string name in dict.Keys.OrderBy(n => n)) {
                string modName = name;   // capture for the closure
                AddModifierEntry(content, width, modName, enabled.Contains(modName), on => {
                    if (on) { staged.modifierOn[type].Add(modName); }
                    else { staged.modifierOn[type].Remove(modName); }
                });
            }
        }

        private static void AddModifierCategoryHeader(Transform content, float width, string label) {
            GameObject row = ConfigUI.NewLayoutRow(content, width, 30f);
            ConfigUI.AddText(row.transform, 2f, 4f, width - 4f, 24f, label, 16, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);
        }

        // A single modifier line: enable toggle on the left, prettified name + brief description to its right.
        private static void AddModifierEntry(Transform content, float width, string name, bool enabled, Action<bool> onChange) {
            GameObject row = ConfigUI.NewLayoutRow(content, width, 48f);
            GameObject tgo = GUIManager.Instance.CreateToggle(row.transform, 22f, 22f);
            tgo.transform.SetParent(row.transform, false);
            RectTransform trt = (RectTransform)tgo.transform;
            trt.localScale = Vector3.one;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(2f, -3f);
            Toggle tg = tgo.GetComponent<Toggle>();
            tg.isOn = enabled;
            tg.onValueChanged.AddListener(b => onChange(b));

            const float TextX = 32f;
            ConfigUI.AddText(row.transform, TextX, 0f, width - TextX - 4f, 20f, Prettify(name), 14, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimOrange);
            string desc = ModifierDescriptions.TryGetValue(name, out string d) ? d : "";
            ConfigUI.AddText(row.transform, TextX, 20f, width - TextX - 4f, 26f, desc, 12, TextAnchor.UpperLeft, GUIManager.Instance.ValheimBeige);
        }

        // "ResistPierce" -> "Resist Pierce", "BossSummoner" -> "Boss Summoner".
        private static string Prettify(string name) => Regex.Replace(name, "(\\B[A-Z])", " $1");

        // True when the Table style is selected but no hand-authored shape exists for this level count,
        // which is the case for every span the panel's own sliders reach by default.
        private static bool TableShapeMissing(LevelGenerator gen) {
            if (gen == null || gen.LevelupCalculationStyle != LevelupCalculationStyle.Table) { return false; }
            int min = Mathf.Min(gen.MinLevel, gen.MaxLevel);
            int max = Mathf.Max(gen.MinLevel, gen.MaxLevel);
            if (max == min) { return false; }
            Dictionary<int, SortedDictionary<int, float>> tables = LevelSystemData.SLE_Level_Settings?.LevelupWeightTablesBySpan;
            if (tables == null) { return true; }
            return tables.TryGetValue(max - min + 1, out SortedDictionary<int, float> shape) == false
                || shape == null || shape.Count < max - min + 1;
        }

        // Plain arithmetic under the generator sliders. The four curve styles produce wildly different
        // distributions from the same inputs, and none of it was visible until the admin was back in the
        // world looking at creatures, which is how the broken ones went unnoticed.
        private static void UpdateGeneratorPreview() {
            if (staged?.generator == null) { return; }

            if (tableWarnText != null) {
                int levels = Mathf.Abs(staged.generator.MaxLevel - staged.generator.MinLevel) + 1;
                tableWarnText.text = TableShapeMissing(staged.generator)
                    ? $"No LevelupWeightTablesBySpan entry for {levels} levels - the Exponential curve is used instead. Add one to LevelSettings.yaml or narrow the range."
                    : "";
            }

            if (generatorPreviewText == null) { return; }
            try {
                SortedDictionary<int, float> curve = staged.generator.GetLevelUpDefinition(quiet: true);
                if (curve == null || curve.Count == 0) { generatorPreviewText.text = ""; return; }

                int min = int.MaxValue, max = int.MinValue;
                foreach (int lvl in curve.Keys) {
                    if (lvl < min) { min = lvl; }
                    if (lvl > max) { max = lvl; }
                }
                // threshold[k] is 100 * P(level > k), because the roller walks levels upward and takes the
                // first whose threshold the roll clears.
                float stayAtMin = 100f - Mathf.Clamp(curve[min], 0f, 100f);
                string line = $"Rolls: level {min} = {stayAtMin:0.0}%";
                int mid = min + (max - min) / 2;
                if (mid > min) { line += $"   >= {mid} = {CurveChance(curve, mid)}"; }
                if (max > min) { line += $"   {max} (top) = {CurveChance(curve, max)}"; }
                generatorPreviewText.text = line;
            } catch (Exception e) {
                generatorPreviewText.text = "";
                Logger.LogDebug($"Could not preview the level generator curve: {e.Message}");
            }
        }

        // P(level >= k) is the threshold recorded against the level below it.
        private static string CurveChance(SortedDictionary<int, float> curve, int level) {
            return curve.TryGetValue(level - 1, out float t) ? $"{Mathf.Clamp(t, 0f, 100f):0.00}%" : "-";
        }

        private static void UpdateExampleMath() {
            if (staged == null) { return; }
            if (creatureExampleText != null) {
                int maxStars = Mathf.Max(0, staged.maxLevel);
                int medStars = Mathf.Max(0, Mathf.CeilToInt(maxStars / 2f));
                creatureExampleText.text = FormatCreatureExample("Troll", TrollHp, TrollDmg, medStars, maxStars, staged.creatureHpPerLevel, staged.creatureDmgPerLevel);
            }
            if (bossExampleText != null) {
                int maxStars = Mathf.Max(0, staged.maxBossLevel);
                int medStars = Mathf.Max(0, Mathf.CeilToInt(maxStars / 2f));
                bossExampleText.text = FormatCreatureExample("The Elder (boss)", TheElderHP, TheElderDmg, medStars, maxStars, staged.bossHpPerLevel, staged.bossDmgPerLevel);
            }
        }

        private static string FormatCreatureExample(string name, float baseHp, float baseDmg, int medStars, int maxStars, float hpMul, float dmgMul) {
            float Hp(int stars) => baseHp * (1f + hpMul * stars);
            float Dmg(int stars) => baseDmg * (1f + dmgMul * stars);
            return $"{name} (base {baseHp:0} HP / {baseDmg:0} dmg)\n" +
                   $"  Min (0 stars):   {Hp(0):0} HP    {Dmg(0):0} dmg\n" +
                   $"  Median ({medStars} stars):   {Hp(medStars):0} HP    {Dmg(medStars):0} dmg\n" +
                   $"  Max ({maxStars} stars):   {Hp(maxStars):0} HP    {Dmg(maxStars):0} dmg\n";
        }

        // ------------------------------------------------------------------------------------------------
        //  Apply
        // ------------------------------------------------------------------------------------------------

        // A yaml document the panel wants to save, paired with the file it belongs to.
        private readonly struct PendingEdit {
            internal readonly YamlConfigFile File;
            internal readonly string Yaml;
            internal PendingEdit(YamlConfigFile file, string yaml) { File = file; Yaml = yaml; }
        }

        // Files sent to the server whose answer has not arrived yet, and everything that went wrong in
        // the current save attempt.
        private static readonly HashSet<string> pendingRemoteEdits = new HashSet<string>();
        private static readonly List<string> applyErrors = new List<string>();
        private static bool editResultsHooked;

        private static void SetMessage(string text) {
            if (messageText != null) { messageText.text = text ?? ""; }
        }

        private static void ApplyAndSave() {
            applyErrors.Clear();
            pendingRemoteEdits.Clear();
            SetMessage("");

            try {
                bool isOwner = ZNet.instance == null || ZNet.instance.IsServer();

                // Every document is built first and nothing is written until all of them are known to be
                // good. Writing the ~25 ConfigEntry values up front, as this used to, fired their
                // SettingChanged handlers immediately - so a yaml file rejected afterwards left half the
                // configuration applied and live with nothing to roll it back.
                List<PendingEdit> edits = new List<PendingEdit>();
                BuildLevelEdit(edits);
                BuildModifierEdit(edits);
                BuildRaidEdit(edits);
                BuildNemesisEdit(edits);

                if (isOwner) {
                    foreach (PendingEdit edit in edits) {
                        ValidationReport report = edit.File.DryRun(edit.Yaml, out string parseError);
                        if (parseError != null) {
                            applyErrors.Add($"{edit.File.FileName}: {parseError}");
                        } else if (report != null && report.HasErrors) {
                            applyErrors.Add($"{edit.File.FileName}: {string.Join(" ", report.Errors.ToArray())}");
                        }
                    }
                    if (applyErrors.Count > 0) {
                        ReportFailure("Nothing was saved");
                        return;
                    }
                }

                WriteConfigEntries();

                if (isOwner) {
                    foreach (PendingEdit edit in edits) {
                        if (YamlConfigManager.ApplyEdited(edit.File, edit.Yaml, out string message) == false) {
                            applyErrors.Add($"{edit.File.FileName}: {message}");
                        }
                    }
                    if (applyErrors.Count > 0) {
                        ReportFailure("Some settings were not saved");
                        return;
                    }
                    Logger.LogInfo("QuickConfigureTool applied and saved configuration.");
                    ClosePanel();
                    return;
                }

                // Off-host the server owns these files, so they go up for validation there instead of
                // being written locally. ApplyEdited refuses on any non-server machine, and this panel
                // used to log that refusal and close anyway - so a remote admin's modifier, raid and
                // nemesis edits vanished while the window behaved exactly as though they had been saved.
                PushRemoteConfigChanges();
                HookEditResults();
                foreach (PendingEdit edit in edits) {
                    if (ConfigNetwork.RequestEdit(edit.File, edit.Yaml, out string refusal)) {
                        pendingRemoteEdits.Add(edit.File.FileName);
                    } else {
                        applyErrors.Add($"{edit.File.FileName}: {refusal}");
                    }
                }
                if (pendingRemoteEdits.Count > 0) {
                    SetMessage($"Sent to the server, waiting for {pendingRemoteEdits.Count} file(s)...");
                    return;
                }
                if (applyErrors.Count > 0) {
                    ReportFailure("Nothing was saved");
                    return;
                }
                Logger.LogInfo("QuickConfigureTool applied configuration.");
                ClosePanel();
            } catch (Exception e) {
                // ClosePanel is deliberately NOT in a finally: a mid-apply failure leaves configuration
                // half-written, and closing the window over it is how an admin ends up believing the save
                // landed. Leave the panel up with their edits intact.
                SetMessage("Save failed - see the log for details.");
                Logger.LogWarning($"QuickConfigureTool failed to apply configuration: {e}");
            }
        }

        private static void ReportFailure(string headline) {
            string detail = string.Join("   ", applyErrors.ToArray());
            SetMessage($"{headline}: {detail}");
            Logger.LogWarning($"QuickConfigureTool - {headline}: {detail}");
        }

        private static void HookEditResults() {
            if (editResultsHooked) { return; }
            editResultsHooked = true;
            ConfigNetwork.EditResult += OnRemoteEditResult;
        }

        private static void UnhookEditResults() {
            if (editResultsHooked == false) { return; }
            editResultsHooked = false;
            ConfigNetwork.EditResult -= OnRemoteEditResult;
        }

        // The server's verdict on one uploaded file. The panel stays open until every file it sent has
        // been answered, so a refusal is visible rather than inferred from the log.
        private static void OnRemoteEditResult(YamlConfigFile file, bool accepted, string message) {
            if (file == null) { return; }
            pendingRemoteEdits.Remove(file.FileName);
            if (accepted == false) {
                applyErrors.Add($"{file.FileName}: {message}");
                Logger.LogWarning($"The server refused {file.FileName}: {message}");
            }
            if (pendingRemoteEdits.Count > 0) {
                SetMessage($"Waiting for the server ({pendingRemoteEdits.Count} file(s) left)...");
                return;
            }
            if (applyErrors.Count > 0) {
                ReportFailure("The server refused some settings");
                return;
            }
            Logger.LogInfo("The server accepted the configuration.");
            ClosePanel();
        }

        private static void WriteConfigEntries() {
            ValConfig.EnableDistanceLevelScalingBonus.Value = staged.enableDistance;
            ValConfig.EnableMapRingsForDistanceBonus.Value = staged.enableDistanceOverlay;
            ValConfig.EnableZoneScalingBonus.Value = staged.enableZone;
            ValConfig.EnableZoneMapOverlay.Value = staged.enableZoneOverlay;

            ValConfig.EnemyHealthMultiplier.Value = staged.creatureHpPerLevel;
            ValConfig.EnemyDamageLevelMultiplier.Value = staged.creatureDmgPerLevel;
            ValConfig.BossEnemyHealthMultiplier.Value = staged.bossHpPerLevel;
            ValConfig.BossEnemyDamageMultiplier.Value = staged.bossDmgPerLevel;
            ValConfig.MaxLevel.Value = staged.maxLevel;
            ValConfig.MaxBossLevel.Value = staged.maxBossLevel;

            ValConfig.EnableMultiplayerEnemyHealthScaling.Value = staged.mpHealth;
            ValConfig.MultiplayerEnemyHealthModifier.Value = staged.mpHealthMod;
            ValConfig.EnableMultiplayerEnemyDamageScaling.Value = staged.mpDamage;
            ValConfig.MultiplayerEnemyDamageModifier.Value = staged.mpDamageMod;
            ValConfig.MultiplayerScalingRequiredPlayersNearby.Value = staged.mpRequiredPlayers;

            ValConfig.MaxMajorModifiersPerCreature.Value = staged.maxMajor;
            ValConfig.MaxMinorModifiersPerCreature.Value = staged.maxMinor;
            ValConfig.ChanceMajorModifier.Value = staged.chanceMajor;
            ValConfig.ChanceMinorModifier.Value = staged.chanceMinor;
            ValConfig.LimitCreatureModifiersToCreatureStarLevel.Value = staged.limitToStarLevel;
            ValConfig.EnableBossModifiers.Value = staged.enableBossMods;
            ValConfig.ChanceOfBossModifier.Value = staged.chanceBoss;
            ValConfig.MaxBossModifiersPerBoss.Value = staged.maxBossMods;
            ValConfig.LimitCreatureModifierPrefixes.Value = staged.prefixLimit;
            ValConfig.MinorModifiersFirstInName.Value = staged.minorFirst;
            ValConfig.ModifierIconDisplayStyle.Value = staged.displayStyle.ToString();

            // "Enable SLS Raids" is the inverse of vanilla raids.
            ValConfig.UseVanillaRaidConfiguration.Value = !staged.enableSlsRaids;
            ValConfig.RaidEventRate.Value = staged.raidEventRate;
            ValConfig.ServerTimeBetweenRaidStartChecks.Value = staged.raidCheckMinutes;
            ValConfig.MaxRaidAttemptsPerPlayer.Value = staged.maxRaidAttempts;
            ValConfig.MaxActiveRaids.Value = staged.maxActiveRaids;

            ValConfig.EnableNemesisSystem.Value = staged.enableNemesis;

            // Those assignments rewrote the .cfg once each. Tell the watcher this was us, or it reports
            // the change back and triggers a reload plus a second round of SettingChanged handlers.
            ValConfig.RefreshOwnConfigStamp();
        }

        // Through a deserialized copy, not the live object: the live settings can BE the shared static
        // default (LevelSystemData re-points there whenever a parse fails), so mutating in place would
        // corrupt the defaults for the rest of the session.
        private static void BuildLevelEdit(List<PendingEdit> edits) {
            // From the authored settings, not the live ones: SLE_Level_Settings has had any generator
            // already expanded over its chance tables, so serializing that would write the
            // machine-generated curve back as if the admin had typed it.
            CreatureLevelSettings levelSource = LevelSystemData.AuthoredLevelSettings ?? LevelSystemData.SLE_Level_Settings;
            if (levelSource == null) {
                applyErrors.Add("level settings are not loaded");
                return;
            }
            CreatureLevelSettings settings = DataObjects.yamlDeserializer.Deserialize<CreatureLevelSettings>(
                DataObjects.yamlSerializer.Serialize(levelSource));
            settings.EnableConditionalCreatureLevelupChance = staged.enableConditional;
            if (staged.useGenerator) {
                // Edit entry zero in place. This is a real list of per-prefab generators and the panel
                // only ever shows the first; replacing the list deleted every other entry an admin had
                // hand-authored.
                if (settings.DefaultLevelupGenerators == null || settings.DefaultLevelupGenerators.Count == 0) {
                    settings.DefaultLevelupGenerators = new List<LevelGenerator> { staged.generator };
                } else {
                    settings.DefaultLevelupGenerators[0] = staged.generator;
                }
            } else {
                // Switched off: drop the generators so the authored DefaultCreatureLevelUpChance takes
                // effect again.
                settings.DefaultLevelupGenerators = null;
            }
            // Through ApplyEdited rather than File.WriteAllText: a bare write drops the documented header
            // block, which is the only in-file explanation these settings have.
            edits.Add(new PendingEdit(YamlConfigManager.LevelSettings, DataObjects.yamlSerializer.Serialize(settings)));
        }

        // Only when a toggle actually changed, so touching the sliders alone does not rewrite the file.
        // Disabled modifiers keep their config and are simply marked Enabled = false.
        private static void BuildModifierEdit(List<PendingEdit> edits) {
            if (ModifiersChanged() == false) { return; }
            CreatureModifierCollection src = DataObjects.yamlDeserializer.Deserialize<CreatureModifierCollection>(
                DataObjects.yamlSerializer.Serialize(staged.modifierSource));
            ApplyEnabledFlags(src.BossModifiers, staged.modifierOn[ModifierType.Boss]);
            ApplyEnabledFlags(src.MajorModifiers, staged.modifierOn[ModifierType.Major]);
            ApplyEnabledFlags(src.MinorModifiers, staged.modifierOn[ModifierType.Minor]);
            edits.Add(new PendingEdit(YamlConfigManager.ModifierSettings, DataObjects.yamlSerializer.Serialize(src)));
        }

        // Per-raid enable/disable lives in RaidDefinition.Enabled; every other per-raid setting is
        // preserved by copying the whole document.
        private static void BuildRaidEdit(List<PendingEdit> edits) {
            if (RaidsChanged() == false) { return; }
            RaidConfiguration raidCFG = DataObjects.yamlDeserializer.Deserialize<RaidConfiguration>(
                DataObjects.yamlSerializer.Serialize(staged.raidSource));
            foreach (RaidDefinition raid in raidCFG.Raids) {
                raid.Enabled = staged.raidsOn.Contains(raid.Name);
            }
            edits.Add(new PendingEdit(YamlConfigManager.RaidSettings, DataObjects.yamlSerializer.Serialize(raidCFG)));
        }

        private static void BuildNemesisEdit(List<PendingEdit> edits) {
            if (NemesisChanged() == false) { return; }
            NemesisConfiguration nemesisCFG = DataObjects.yamlDeserializer.Deserialize<NemesisConfiguration>(
                DataObjects.yamlSerializer.Serialize(staged.nemesisSource));
            nemesisCFG.NemesisActionCooldownSeconds = staged.nemCooldown;
            nemesisCFG.NemesisInfluenceRadius = staged.nemInfluence;
            nemesisCFG.NemesisMinSpawnDistance = staged.nemMinSpawn;
            if (nemesisCFG.ScoreSystem == null) { nemesisCFG.ScoreSystem = new NemesisScore(); }
            nemesisCFG.ScoreSystem.NeutralScore = staged.neutralScore;
            nemesisCFG.ScoreSystem.MinScore = staged.minScore;
            nemesisCFG.ScoreSystem.MaxScore = staged.maxScore;
            nemesisCFG.ScoreSystem.DecayPerUpdate = staged.decayPerUpdate;
            nemesisCFG.ScoreSystem.ScoreIntervalSeconds = staged.scoreInterval;
            nemesisCFG.ScoreSystem.BossKillBonus = staged.bossKillBonus;
            nemesisCFG.ScoreSystem.DeathScoreReduction = staged.deathReduction;
            edits.Add(new PendingEdit(YamlConfigManager.NemesisSettings, DataObjects.yamlSerializer.Serialize(nemesisCFG)));
        }

        // True if any modifier's staged enable state differs from its current Enabled flag.
        private static bool ModifiersChanged() {
            if (staged?.modifierSource == null) { return false; }
            return EnabledDiffers(staged.modifierSource.BossModifiers, staged.modifierOn[ModifierType.Boss])
                || EnabledDiffers(staged.modifierSource.MajorModifiers, staged.modifierOn[ModifierType.Major])
                || EnabledDiffers(staged.modifierSource.MinorModifiers, staged.modifierOn[ModifierType.Minor]);
        }

        private static bool EnabledDiffers(Dictionary<string, CreatureModifierConfiguration> dict, HashSet<string> enabledNames) {
            if (dict == null) { return false; }
            foreach (KeyValuePair<string, CreatureModifierConfiguration> kv in dict) {
                if (kv.Value.Enabled != enabledNames.Contains(kv.Key)) { return true; }
            }
            return false;
        }

        // Writes the staged on/off state back onto each modifier's Enabled flag.
        private static void ApplyEnabledFlags(Dictionary<string, CreatureModifierConfiguration> dict, HashSet<string> enabledNames) {
            if (dict == null) { return; }
            foreach (KeyValuePair<string, CreatureModifierConfiguration> kv in dict) {
                kv.Value.Enabled = enabledNames.Contains(kv.Key);
            }
        }

        // True if any staged nemesis YAML value differs from the live config (so we only rewrite when changed).
        private static bool NemesisChanged() {
            NemesisConfiguration n = staged?.nemesisSource;
            if (n == null) { return false; }
            NemesisScore sc = n.ScoreSystem ?? new NemesisScore();
            return n.NemesisActionCooldownSeconds != staged.nemCooldown
                || n.NemesisInfluenceRadius != staged.nemInfluence
                || n.NemesisMinSpawnDistance != staged.nemMinSpawn
                || sc.NeutralScore != staged.neutralScore
                || sc.MinScore != staged.minScore
                || sc.MaxScore != staged.maxScore
                || sc.DecayPerUpdate != staged.decayPerUpdate
                || sc.ScoreIntervalSeconds != staged.scoreInterval
                || sc.BossKillBonus != staged.bossKillBonus
                || sc.DeathScoreReduction != staged.deathReduction;
        }

        // True if any raid's staged enable state differs from its current Enabled flag.
        private static bool RaidsChanged() {
            if (staged?.raidSource?.Raids == null) { return false; }
            foreach (RaidDefinition raid in staged.raidSource.Raids) {
                if (raid.Enabled != staged.raidsOn.Contains(raid.Name)) { return true; }
            }
            return false;
        }

        // Row based dual config, for scalars
        private static GameObject AddScalingFeatureRow(Transform parent, float colWidth, float imgW, float imgH, Sprite sprite, string mainLabel, string description, bool mainValue, Action<bool> onMain, string subLabel, bool subValue, Action<bool> onSub) {
            const float SubIndent = 24f;
            const float MainToggleSize = 26f;
            const float SubToggleSize = 22f;
            const float ToggleGap = 8f;   // gap between a toggle and the title to its right
            const float DescH = 72f;      // up to ~4 wrapped lines; the row is tall so the description has room
            float leftColW = colWidth - imgW;   // configuration area to the left of the example image
            GameObject row = ConfigUI.NewRow(parent, colWidth, imgH);

            // Example image on the right
            GameObject go = ConfigUI.NewUI("Image", row.transform, typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(imgW, imgH);
            rt.anchoredPosition = new Vector2(colWidth - imgW, 0f);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;   // 256x171 letterboxes inside the box
            img.raycastTarget = false;

            // Vertically center the (main toggle + description + sub toggle) block in the image box
            float blockH = RowHeight + 2f + DescH + 4f + SubRowHeight;   // 34 + 2 + 72 + 4 + 26 = 138
            float topY = Mathf.Max(0f, (imgH - blockH) * 0.5f);

            // Main toggle, directly to the left of its title
            GameObject mainToggleGO = GUIManager.Instance.CreateToggle(row.transform, MainToggleSize, MainToggleSize);
            mainToggleGO.transform.SetParent(row.transform, false);
            RectTransform mrt = (RectTransform)mainToggleGO.transform;
            mrt.localScale = Vector3.one;
            mrt.anchorMin = new Vector2(0f, 1f); mrt.anchorMax = new Vector2(0f, 1f); mrt.pivot = new Vector2(0f, 1f);
            mrt.anchoredPosition = new Vector2(0f, -(topY + 3f));
            Toggle mt = mainToggleGO.GetComponent<Toggle>();
            mt.isOn = mainValue;
            mt.onValueChanged.AddListener(b => onMain(b));

            float mainLabelX = MainToggleSize + ToggleGap;
            ConfigUI.AddText(row.transform, mainLabelX, topY, leftColW - mainLabelX - 12f, RowHeight, mainLabel, 18, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimOrange);

            // Description directly under the main label (full left column up to the image)
            float descY = topY + RowHeight + 2f;
            ConfigUI.AddText(row.transform, 0f, descY, leftColW - 12f, DescH, description, 14, TextAnchor.UpperLeft, GUIManager.Instance.ValheimBeige);

            // Sub toggle (smaller, indented, below the description), directly to the left of its title
            float subY = descY + DescH + 4f;
            GameObject subToggleGO = GUIManager.Instance.CreateToggle(row.transform, SubToggleSize, SubToggleSize);
            subToggleGO.transform.SetParent(row.transform, false);
            RectTransform srt = (RectTransform)subToggleGO.transform;
            srt.localScale = Vector3.one;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(SubIndent, -(subY + 2f));
            Toggle st = subToggleGO.GetComponent<Toggle>();
            st.isOn = subValue;
            st.onValueChanged.AddListener(b => onSub(b));

            float subLabelX = SubIndent + SubToggleSize + ToggleGap;
            ConfigUI.AddText(row.transform, subLabelX, subY, leftColW - subLabelX - 12f, SubRowHeight, subLabel, 14, TextAnchor.MiddleLeft);

            return row;
        }


        private class StagedConfig {
            public bool enableDistance, enableDistanceOverlay;
            public bool enableZone, enableZoneOverlay;
            public bool enableConditional;

            public float creatureHpPerLevel, creatureDmgPerLevel, bossHpPerLevel, bossDmgPerLevel;
            public int maxLevel, maxBossLevel;

            public bool mpHealth, mpDamage;
            public float mpHealthMod, mpDamageMod;
            public int mpRequiredPlayers;

            // The default level generator, and whether the config actually has one. These are separate on
            // purpose: the sliders always need something to show, but writing a generator the admin never
            // asked for replaces their hand-authored DefaultCreatureLevelUpChance with a machine-generated
            // curve. useGenerator is what the admin opted into, generator is only the shape.
            public LevelGenerator generator;
            public bool useGenerator;

            public int maxMajor, maxMinor, maxBossMods, prefixLimit;
            public float chanceMajor, chanceMinor, chanceBoss;
            public bool limitToStarLevel, enableBossMods, minorFirst;
            public ModifierDisplayStyle displayStyle;

            // Modifier enable/disable: the live active collection (read-only here) and the set of names
            // currently toggled on per category. Toggling off then removes the entry when saved.
            public CreatureModifierCollection modifierSource;
            public Dictionary<ModifierType, HashSet<string>> modifierOn;

            // Raids (BepInEx ConfigEntries). enableSlsRaids is the inverse of UseVanillaRaidConfiguration.
            public bool enableSlsRaids;
            public float raidEventRate;
            public int raidCheckMinutes, maxRaidAttempts, maxActiveRaids;

            // Per-raid enable/disable. raidSource is the live raid config (read-only here); raidsOn holds the
            // names of the raids currently toggled on. Toggling off then marks Enabled = false when saved.
            public RaidConfiguration raidSource;
            public HashSet<string> raidsOn;

            // Nemesis system. enableNemesis is a ConfigEntry; the rest live in the NemesisSettings YAML
            // (NemesisSystemData.SLE_Nemesis_Settings). nemesisSource is kept for apply + change detection.
            public bool enableNemesis;
            public float nemCooldown, nemInfluence, nemMinSpawn;
            public float neutralScore, minScore, maxScore, decayPerUpdate, scoreInterval, bossKillBonus, deathReduction;
            public NemesisConfiguration nemesisSource;

            public static StagedConfig Snapshot() {
                StagedConfig s = new StagedConfig {
                    enableDistance = ValConfig.EnableDistanceLevelScalingBonus.Value,
                    enableDistanceOverlay = ValConfig.EnableMapRingsForDistanceBonus.Value,
                    enableZone = ValConfig.EnableZoneScalingBonus.Value,
                    enableZoneOverlay = ValConfig.EnableZoneMapOverlay.Value,

                    creatureHpPerLevel = ValConfig.EnemyHealthMultiplier.Value,
                    creatureDmgPerLevel = ValConfig.EnemyDamageLevelMultiplier.Value,
                    bossHpPerLevel = ValConfig.BossEnemyHealthMultiplier.Value,
                    bossDmgPerLevel = ValConfig.BossEnemyDamageMultiplier.Value,
                    maxLevel = ValConfig.MaxLevel.Value,
                    maxBossLevel = ValConfig.MaxBossLevel.Value,

                    mpHealth = ValConfig.EnableMultiplayerEnemyHealthScaling.Value,
                    mpHealthMod = ValConfig.MultiplayerEnemyHealthModifier.Value,
                    mpDamage = ValConfig.EnableMultiplayerEnemyDamageScaling.Value,
                    mpDamageMod = ValConfig.MultiplayerEnemyDamageModifier.Value,
                    mpRequiredPlayers = ValConfig.MultiplayerScalingRequiredPlayersNearby.Value,

                    maxMajor = ValConfig.MaxMajorModifiersPerCreature.Value,
                    maxMinor = ValConfig.MaxMinorModifiersPerCreature.Value,
                    chanceMajor = ValConfig.ChanceMajorModifier.Value,
                    chanceMinor = ValConfig.ChanceMinorModifier.Value,
                    limitToStarLevel = ValConfig.LimitCreatureModifiersToCreatureStarLevel.Value,
                    enableBossMods = ValConfig.EnableBossModifiers.Value,
                    chanceBoss = ValConfig.ChanceOfBossModifier.Value,
                    maxBossMods = ValConfig.MaxBossModifiersPerBoss.Value,
                    prefixLimit = ValConfig.LimitCreatureModifierPrefixes.Value,
                    minorFirst = ValConfig.MinorModifiersFirstInName.Value,

                    enableSlsRaids = !ValConfig.UseVanillaRaidConfiguration.Value,
                    raidEventRate = ValConfig.RaidEventRate.Value,
                    raidCheckMinutes = ValConfig.ServerTimeBetweenRaidStartChecks.Value,
                    maxRaidAttempts = ValConfig.MaxRaidAttemptsPerPlayer.Value,
                    maxActiveRaids = ValConfig.MaxActiveRaids.Value,

                    enableNemesis = ValConfig.EnableNemesisSystem.Value,
                };

                // The authored copy, not the live one: SLE_Level_Settings has already had any generator
                // expanded into its chance tables, and editing from that would write the expansion back.
                CreatureLevelSettings settings = LevelSystemData.AuthoredLevelSettings ?? LevelSystemData.SLE_Level_Settings;
                s.enableConditional = settings != null && settings.EnableConditionalCreatureLevelupChance;
                s.useGenerator = settings?.DefaultLevelupGenerators != null && settings.DefaultLevelupGenerators.Count > 0;
                s.generator = CloneOrDefaultGenerator(settings, s.maxLevel);

                if (!Enum.TryParse(ValConfig.ModifierIconDisplayStyle.Value, out ModifierDisplayStyle ds)) {
                    ds = ModifierDisplayStyle.Stars;
                }
                s.displayStyle = ds;

                s.modifierSource = CreatureModifiersData.ActiveCreatureModifiers;
                s.modifierOn = new Dictionary<ModifierType, HashSet<string>>() {
                    { ModifierType.Boss, KeysOf(s.modifierSource?.BossModifiers) },
                    { ModifierType.Major, KeysOf(s.modifierSource?.MajorModifiers) },
                    { ModifierType.Minor, KeysOf(s.modifierSource?.MinorModifiers) },
                };

                NemesisConfiguration nemesisCFG = NemesisSystemData.SLE_Nemesis_Settings;
                s.nemesisSource = nemesisCFG;
                if (nemesisCFG != null) {
                    s.nemCooldown = nemesisCFG.NemesisActionCooldownSeconds;
                    s.nemInfluence = nemesisCFG.NemesisInfluenceRadius;
                    s.nemMinSpawn = nemesisCFG.NemesisMinSpawnDistance;
                    NemesisScore score = nemesisCFG.ScoreSystem ?? new NemesisScore();
                    s.neutralScore = score.NeutralScore;
                    s.minScore = score.MinScore;
                    s.maxScore = score.MaxScore;
                    s.decayPerUpdate = score.DecayPerUpdate;
                    s.scoreInterval = score.ScoreIntervalSeconds;
                    s.bossKillBonus = score.BossKillBonus;
                    s.deathReduction = score.DeathScoreReduction;
                }

                s.raidSource = RaidsData.SLE_Raid_Settings;
                s.raidsOn = new HashSet<string>();
                if (s.raidSource?.Raids != null) {
                    foreach (RaidDefinition raid in s.raidSource.Raids) {
                        if (raid.Enabled) { s.raidsOn.Add(raid.Name); }
                    }
                }
                return s;
            }

            // Names of the entries that are currently enabled (entries default to enabled).
            private static HashSet<string> KeysOf(Dictionary<string, CreatureModifierConfiguration> dict) {
                HashSet<string> set = new HashSet<string>();
                if (dict == null) { return set; }
                foreach (KeyValuePair<string, CreatureModifierConfiguration> kv in dict) {
                    if (kv.Value.Enabled) { set.Add(kv.Key); }
                }
                return set;
            }

            // Prepopulate the configurable level generator from the existing default generator if one is
            // set. When none is set this returns a starting shape for the sliders ONLY -- it is not
            // written unless the admin turns the generator on, because a generator overwrites
            // DefaultCreatureLevelUpChance wholesale on the next load.
            private static LevelGenerator CloneOrDefaultGenerator(CreatureLevelSettings settings, int maxLevel) {
                LevelGenerator src = null;
                if (settings?.DefaultLevelupGenerators != null && settings.DefaultLevelupGenerators.Count > 0) {
                    src = settings.DefaultLevelupGenerators[0];
                }
                if (src == null) {
                    return new LevelGenerator {
                        MinLevel = 1,
                        MaxLevel = Mathf.Max(1, maxLevel),
                        LevelUpChance = 0.2f,
                        LevelupCalculationStyle = LevelupCalculationStyle.Exponential,
                        GaussianOffset = 0f,
                        GaussianSpread = 0.5f,
                        NightMultiplier = 1f,
                    };
                }
                return new LevelGenerator {
                    PrefabName = src.PrefabName,
                    MinLevel = src.MinLevel,
                    MaxLevel = src.MaxLevel,
                    LevelUpChance = src.LevelUpChance,
                    LevelupCalculationStyle = src.LevelupCalculationStyle,
                    GaussianOffset = src.GaussianOffset,
                    GaussianSpread = src.GaussianSpread,
                    NightMultiplier = src.NightMultiplier,
                };
            }
        }
    }
}
