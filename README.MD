# Star Level System Expanded - Fixed

A maintained fork of [Star Level System Expanded](https://github.com/MidnightsFX/Valheim_Star_Levels_Expanded)
by **MidnightsFX**, who wrote the mod and holds the copyright on everything this fork builds on.
This fork is maintained by **Neocor** and, like the original, is licensed under
[GPL-3.0](LICENSE).

**What is different here:** a 50-point audit of the 1.10.2 release and the fixes for it - the config
panel no longer leaks keystrokes into the game, Escape closes it, and it stops rewriting the level
curve on save; the conditional boss tiers, zone scaling, tame breeding and level caps work as
documented; the yaml files are validated and report every problem rather than the first; and the panel
is localized. The full list with file and line references is in [AUDIT.md](AUDIT.md), and the
per-release summary is in [the changelog](StarLevelSystem/Package/CHANGELOG.md).

The plugin GUID is unchanged (`MidnightsFX.StarLevelSystem`), so this is a drop-in replacement that
keeps your existing config file. For the same reason it cannot be installed alongside the original.

---


A Valheim mod that replaces the vanilla two-star cap with a configurable level system: more star
levels, per-biome and per-creature curves, distance and zone based scaling, creature modifiers, custom
raids, a nemesis system and location resets.

The full documentation lives in [StarLevelSystem/Package/README.md](StarLevelSystem/Package/README.md).

Every config file is generated with a commented header that documents its own schema; those headers are
the authoritative reference for options the README does not cover.
