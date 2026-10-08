# Sea Danger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** While a run's player sails open water, the saga sends sea creatures, and the coasts' flyers, scaled
by heat. A Ward fitting and better coin drops let the player answer it.

**Architecture:**
- Pure rules, unit-tested under Mono:
  - `RunMode/SeaDanger.cs`: the dial, the voyage clock, the creature choice;
  - `RunMode/RunCoins.cs`;
  - the Ward in `RunMode/ShipFittings.cs`.
- A thin game side:
  - `RunMode/Unity/SeaWatch.cs`: polled once a second; spawns, prunes, and the ward pulse;
  - `RunMode/Unity/CoinDrops.cs`.
- Small hooks in `RunService`, `RunStorage` and `Configuration`.

**Tech Stack:**
- C# for .NET Framework 4.7.2, **C# 7.3**: no switch expressions, no `??=`, no `is not`.
- Unity 6000.0.75, Valheim 1.0.17 (`assembly_valheim`).
- The repo's own test runner, `Tests/run_tests.sh` (Mono `csc`; `Check.That(bool, string)`).

**Spec:** `docs/superpowers/specs/2026-10-08-sea-danger-design.md` (read it first).

## Global Constraints

- **Act gate:** Act II on, `SeaDanger.FirstActIndex = 1`. The Ashlands act is index 6, and flyer acts are Mountain 3,
  Plains 4, Mistlands 5, Ashlands 6.
- **Prefabs:** `Serpent`, `BonemawSerpent`, `Hatchling`, `Deathsquito`, `Gjall`, `FallenValkyrie`; `Coins` for the
  coin item.
- **The voyage clock:** roll every 10 s; the first 60 s of a voyage are quiet; a voyage ends after 120 s not at sea;
  90 s cooldown; at most 2 live encounters; the flyer share is 2/3.
- **The dial:**
  - `h = clamp(heat / FullHeat, 0, 1)`;
  - gap `G = 1 / (PeakPerMinute × h)` minutes, counting the cooldown;
  - `rate = 1 / max(G − 1.5, 1/6)` per minute;
  - `chance per roll = 1 − exp(−(1/6) / max(G − 1.5, 1/6))`.
- **Levels:** 1 for `h < 1/3`, 2 for `h < 2/3`, 3 above. High heat is `h ≥ 2/3`: Drakes 2 (otherwise 1), Deathsquitos 3
  (otherwise 2).
- **Ward:** prices 100, 250, 450; radius 20, 25, 30 m; damage 20, 40, 70 lightning; a pulse every 3 s; offered
  only after the raven's sea line (`seaRavenTold`); listed after Wind-horn and before Fire-tar.
- **Config defaults:**

  | Setting | Default |
  |---|---|
  | `runSeaDanger` | `true` |
  | `runSeaFullHeat` | `40` |
  | `runSeaPeakPerMinute` | `0.5` |
  | `runTrollCoinMultiplier` | `3` |
  | `runSeaCoinMultiplier` | `1` |

- **Coins:**
  - troll extra = `(multiplier − 1) ×` a roll of 20–30;
  - sea base: Serpent 30, Bonemaw 60, Drake 10, Deathsquito 5, Gjall 40, FallenValkyrie 60, times the level,
    times the multiplier.
- **The raven line:** "The sea has found your wake. The hotter you burn, the more of it comes. A ward at the helm
  keeps the worst of it off."
- **Messages per creature** (exact):

  | Creature | Message |
  |---|---|
  | Serpent | "Something rises off the bow." |
  | Bonemaw | "The boiling sea gives up a Bonemaw." |
  | Drake | "Drakes come down off the mountains." |
  | Deathsquito | "Something whines over the water." |
  | Gjall | "A Gjall drifts out over the water." |
  | FallenValkyrie | "A fallen valkyrie rises over the ash coast." |

- **Spawned creatures are never saved** (`ZDO.Persistent = false`), hunt the player (`MonsterAI.SetHuntPlayer(true)`),
  and are alerted (`BaseAI.Alert()`).
- **Never break a run:** every game-side entry point catches and `LogOnce`s.
- **Build loop** (CLAUDE.md, RESUME):
  1. commit;
  2. tag with `Scripts/nextversion.sh`;
  3. `Scripts/setversion.sh`;
  4. `msbuild Valheim.sln -p:Configuration=Debug -v:minimal`;
  5. `bash Tests/run_tests.sh`;
  6. `bash Scripts/stage_windows.sh`;
  7. `git checkout -- dist/windows/patcher/Patcher.exe`;
  8. commit "build: …";
  9. push the branch and the tag;
  10. `bash Scripts/deploy_local.sh`.

  New `.cs` files must be added to `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj` (`<Compile Include=…>`), since
  the csproj lists sources explicitly. New test classes must be registered in `Tests/TestMain.cs`.
- **Commit trailer:**
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01VkpcyyQ86pSouuPbxs5C9f
  ```

## Review Focus

1. **Heat drops to 0 in the middle of a voyage** (a death costs heat). Expected: no more encounters, and a blown
   horn calls nothing. Pinned by Task 1's `ChancePerRoll(0, …) == 0`; Task 4's `SeaWatch.Tick` skips a Certain turn
   at `h == 0`.
2. **A creature dies or despawns while the limit is full.** Expected: the limit frees, and later rolls happen.
   Pinned by Task 1's voyage test (`Ended` frees a slot and never goes below 0); Task 4 prunes dead, destroyed and
   far creatures every second.
3. **A ship near land, in a fjord or a bay.** Expected: no sea creature spawns on land; the roll is skipped instead.
   Task 4's `SpawnSwimmer` tries 8 spots and needs open water.
4. **An old run save without `shipWard` or `seaRavenTold`.** Expected: it loads with no ward and no line said.
   JsonUtility leaves missing fields at their defaults; checked in Task 4 Step 6.
5. **Tamed animals aboard (a wolf, a lox) and the saga's speakers near a dock.** Expected: the ward never
   strikes them. Task 5's pulse skips the tamed, non-enemies, and anything immune to lightning; all speakers are
   immune.

---

### Task 1: The rules — `SeaDanger.cs`

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/SeaDanger.cs`
- Create: `Tests/SeaDangerTests.cs`
- Modify: `Tests/TestMain.cs` (register `SeaDangerTests.Run();` after `DevSeaTests.Run();`)
- Modify: `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj` (add `<Compile Include="RunMode\SeaDanger.cs" />` after `RunMode\DevSea.cs`)

**Interfaces:**
- Produces:
  - `enum SeaCoast { Mountain, Plains, Mistlands, Ashlands }`;
  - `enum SeaCreature { Serpent, Bonemaw, Drake, Deathsquito, Gjall, FallenValkyrie }`;
  - `enum SeaTurn { None, Roll, Certain }`;
  - `class SeaEncounter { SeaCreature Creature; string Prefab; int Count; int Level; bool Flies; SeaCoast? From; string Message; }`.
  - `static class SeaDanger` with:
    - the constants `FirstActIndex, AshlandsActIndex, RollSeconds, QuietSeconds, VoyageEndSeconds, CooldownSeconds, MaxLive, FlyerShare`;
    - `float Dial(float heat, float fullHeat)`, `double GapMinutes(float h, float peakPerMinute)`,
      `double ChancePerRoll(float h, float peakPerMinute)`;
    - `int Level(float h)`, `int Count(SeaCreature, float h)`, `int ActIndexOf(SeaCoast)`;
    - `SeaCreature FlyerOf(SeaCoast)`, `string PrefabOf(SeaCreature)`, `bool Flies(SeaCreature)`,
      `string MessageOf(SeaCreature)`, `IEnumerable<SeaCreature> All`;
    - `SeaEncounter Make(SeaCreature, SeaCoast?, float h)`;
    - `SeaEncounter Choose(IEnumerable<SeaCoast> coastsNear, bool onAshlandsSea, int actIndex, float h, Func<double> rng)`.
  - `class SeaVoyage` with `bool OnVoyage`, `int Live`, `SeaTurn Tick(float now, bool atSea)`, `void Arrived(float now)`,
    `void Ended()`, `void HornBlown()`.

- [ ] **Step 1: Write the failing test** `Tests/SeaDangerTests.cs`:

```csharp
using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Sea danger's rules (2026-10-08, docs/superpowers/specs/2026-10-08-sea-danger-design.md): the heat dial, the
/// voyage clock, and what comes.
/// </summary>
static class SeaDangerTests
{
    static bool Near(double a, double b) => Math.Abs(a - b) < 1e-4;

    static Func<double> Dice(params double[] rolls)
    {
        int i = 0;
        return () => rolls[Math.Min(i++, rolls.Length - 1)];
    }

    public static void Run()
    {
        // The dial.
        Check.That(SeaDanger.Dial(0f, 40f) == 0f && SeaDanger.Dial(20f, 40f) == 0.5f && SeaDanger.Dial(80f, 40f) == 1f &&
                   SeaDanger.Dial(10f, 0f) == 0f && SeaDanger.Dial(-5f, 40f) == 0f,
                   "the dial is heat over full heat, clamped to 0..1, and 0 for a nonsense full heat");
        Check.That(Near(SeaDanger.GapMinutes(1f, 0.5f), 2) && Near(SeaDanger.GapMinutes(0.5f, 0.5f), 4) &&
                   Near(SeaDanger.GapMinutes(0.25f, 0.5f), 8) && double.IsPositiveInfinity(SeaDanger.GapMinutes(0f, 0.5f)),
                   "the average gap, cooldown included: 2, 4 and 8 minutes at full, half and a quarter heat; never at 0");
        Check.That(SeaDanger.ChancePerRoll(0f, 0.5f) == 0.0 && SeaDanger.ChancePerRoll(0.5f, 0f) == 0.0,
                   "heat 0 (or no peak) never rolls an encounter");
        Check.That(Near(SeaDanger.ChancePerRoll(1f, 0.5f), 1 - Math.Exp(-(1.0 / 6) / 0.5)) &&
                   Near(SeaDanger.ChancePerRoll(0.5f, 0.5f), 1 - Math.Exp(-(1.0 / 6) / 2.5)),
                   "each 10 s roll waits out the cooldown, then delivers the gap: 0.5 min left at full heat, 2.5 at half");
        Check.That(SeaDanger.ChancePerRoll(0.25f, 0.5f) < SeaDanger.ChancePerRoll(0.5f, 0.5f) &&
                   SeaDanger.ChancePerRoll(0.5f, 0.5f) < SeaDanger.ChancePerRoll(1f, 0.5f),
                   "more heat, likelier");
        Check.That(Near(SeaDanger.ChancePerRoll(1f, 10f), 1 - Math.Exp(-1)),
                   "a gap shorter than the cooldown rolls at most one per roll-time, not faster");

        // Strength.
        Check.That(SeaDanger.Level(0f) == 1 && SeaDanger.Level(0.32f) == 1 && SeaDanger.Level(1f / 3f) == 2 &&
                   SeaDanger.Level(0.66f) == 2 && SeaDanger.Level(2f / 3f) == 3 && SeaDanger.Level(1f) == 3,
                   "no star below a third, one star below two thirds, two stars above");
        Check.That(SeaDanger.Count(SeaCreature.Drake, 0.5f) == 1 && SeaDanger.Count(SeaCreature.Drake, 0.7f) == 2 &&
                   SeaDanger.Count(SeaCreature.Deathsquito, 0.5f) == 2 && SeaDanger.Count(SeaCreature.Deathsquito, 0.7f) == 3 &&
                   SeaDanger.Count(SeaCreature.Serpent, 1f) == 1 && SeaDanger.Count(SeaCreature.Gjall, 1f) == 1,
                   "drakes 1 (2 at high heat), deathsquitos 2 (3), the rest come alone");

        // What comes.
        var plain = SeaDanger.Choose(new SeaCoast[0], false, 1, 0.5f, Dice(0.0));
        Check.That(plain.Creature == SeaCreature.Serpent && plain.Prefab == "Serpent" && !plain.Flies && plain.From == null &&
                   plain.Level == 2 && plain.Count == 1 && plain.Message == "Something rises off the bow.",
                   "open sea in Act II: a Serpent, starred by heat");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 2, 0.5f, Dice(0.0)).Creature == SeaCreature.Serpent,
                   "a mountain coast before Act IV sends a Serpent - no spoilers");
        var drake = SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 3, 0.7f, Dice(0.1, 0.0));
        Check.That(drake.Creature == SeaCreature.Drake && drake.Prefab == "Hatchling" && drake.Flies &&
                   drake.From == SeaCoast.Mountain && drake.Count == 2 && drake.Message == "Drakes come down off the mountains.",
                   "a reached mountain coast sends drakes two times in three");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 3, 0.7f, Dice(0.9)).Creature == SeaCreature.Serpent,
                   "and a Serpent the third time");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain, SeaCoast.Plains }, false, 4, 0.5f, Dice(0.1, 0.99)).Creature == SeaCreature.Deathsquito &&
                   SeaDanger.Choose(new[] { SeaCoast.Plains, SeaCoast.Mountain }, false, 4, 0.5f, Dice(0.1, 0.0)).Creature == SeaCreature.Drake,
                   "several reached coasts: one at random, in a fixed order whatever order they were found in");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mistlands, SeaCoast.Ashlands }, false, 5, 0.5f, Dice(0.1, 0.99)).Creature == SeaCreature.Gjall,
                   "an unreached coast is no candidate (the Ashlands in Act VI)");
        Check.That(SeaDanger.Choose(new SeaCoast[0], true, 6, 0.5f, Dice(0.0)).Creature == SeaCreature.Bonemaw &&
                   SeaDanger.Choose(new SeaCoast[0], true, 5, 0.5f, Dice(0.0)).Creature == SeaCreature.Serpent,
                   "the boiling sea gives up a Bonemaw - from Act VII only");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Ashlands }, true, 6, 0.5f, Dice(0.1, 0.0)).Creature == SeaCreature.FallenValkyrie,
                   "the ash coast sends fallen valkyries");
        Check.That(SeaDanger.All.Count() == 6 && SeaDanger.All.All(c => !string.IsNullOrEmpty(SeaDanger.PrefabOf(c)) &&
                   !string.IsNullOrEmpty(SeaDanger.MessageOf(c))),
                   "all six creatures have a prefab and a message");

        // The voyage clock.
        var v = new SeaVoyage();
        Check.That(v.Tick(0f, true) == SeaTurn.None && v.OnVoyage && v.Tick(59f, true) == SeaTurn.None,
                   "a voyage starts quiet: no roll in its first minute");
        Check.That(v.Tick(60f, true) == SeaTurn.Roll && v.Tick(61f, true) == SeaTurn.None && v.Tick(70f, true) == SeaTurn.Roll,
                   "then a roll every ten seconds");
        v.Arrived(70f);
        Check.That(v.Live == 1 && v.Tick(80f, true) == SeaTurn.None && v.Tick(159f, true) == SeaTurn.None &&
                   v.Tick(160f, true) == SeaTurn.Roll,
                   "an encounter starts a ninety-second cooldown");
        v.Arrived(160f);
        Check.That(v.Live == 2 && v.Tick(400f, true) == SeaTurn.None, "two live encounters: nothing more comes");
        v.Ended();
        Check.That(v.Live == 1 && v.Tick(401f, true) == SeaTurn.Roll, "one ends, and the rolls resume");
        v.Ended(); v.Ended();
        Check.That(v.Live == 0, "the live count never goes below zero");
        v.HornBlown();
        Check.That(v.Tick(411f, true) == SeaTurn.Certain && v.Tick(421f, true) == SeaTurn.Roll,
                   "the horn makes the next roll certain, once");
        // Last at sea at 421: under two minutes later (540) the voyage still holds.
        Check.That(v.Tick(430f, false) == SeaTurn.None && v.OnVoyage && v.Tick(540f, false) == SeaTurn.None && v.OnVoyage,
                   "off the ship for under two minutes: the voyage goes on");
        Check.That(v.Tick(541f, true) == SeaTurn.Roll, "and back at sea it rolls at once, not quiet");
        v.HornBlown();
        // Last at sea at 541: two minutes later (661) the voyage is over.
        Check.That(v.Tick(600f, false) == SeaTurn.None && v.OnVoyage && v.Tick(661f, false) == SeaTurn.None && !v.OnVoyage,
                   "two minutes off the sea end the voyage");
        Check.That(v.Tick(670f, true) == SeaTurn.None && v.Tick(729f, true) == SeaTurn.None && v.Tick(730f, true) == SeaTurn.Roll,
                   "the next voyage is quiet again for a minute, and the old horn was forgotten");
        var w = new SeaVoyage();
        w.HornBlown();
        Check.That(w.Tick(0f, true) == SeaTurn.None && w.Tick(30f, true) == SeaTurn.None && w.Tick(60f, true) == SeaTurn.Certain,
                   "a horn blown before the quiet minute is over waits for it");
    }
}
```

- [ ] **Step 2: Register and run, expecting a compile failure.** Add `SeaDangerTests.Run();` after `DevSeaTests.Run();` in
  `Tests/TestMain.cs`, then `bash Tests/run_tests.sh`. Expected: `error CS0103: The name 'SeaDanger' does not exist`.

- [ ] **Step 3: Write `ICanShowYouTheWorld/RunMode/SeaDanger.cs`:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Land near the ship whose flyers can come out over the water, by the act it belongs to.</summary>
    public enum SeaCoast { Mountain, Plains, Mistlands, Ashlands }

    public enum SeaCreature { Serpent, Bonemaw, Drake, Deathsquito, Gjall, FallenValkyrie }

    /// <summary>What the voyage clock says this second: nothing, a roll of the dice, or a certain encounter (the horn).</summary>
    public enum SeaTurn { None, Roll, Certain }

    /// <summary>One encounter: what comes, how many, how strong, from where, and what is said.</summary>
    public sealed class SeaEncounter
    {
        public SeaCreature Creature;
        public string Prefab;
        public int Count;
        public int Level;
        public bool Flies;
        public SeaCoast? From;
        public string Message;
    }

    /// <summary>
    /// The sea answers the heat (2026-10-08; docs/superpowers/specs/2026-10-08-sea-danger-design.md). The owner:
    /// "It would be nice to have waters be more treacherous". Heat 0 is vanilla's sea; from there the average gap
    /// between encounters, cooldown included, falls to 2 minutes at full heat.
    /// </summary>
    /// <remarks>
    /// Pure: the game side (SeaWatch) reads the ship, the land and the heat, and asks these rules what happens.
    /// </remarks>
    public static class SeaDanger
    {
        public const int FirstActIndex = 1;      // Act II
        public const int AshlandsActIndex = 6;   // Act VII
        public const float RollSeconds = 10f;
        public const float QuietSeconds = 60f;
        public const float VoyageEndSeconds = 120f;
        public const float CooldownSeconds = 90f;
        public const int MaxLive = 2;
        public const double FlyerShare = 2.0 / 3.0;

        /// <summary>How far up the dial heat has the sea: 0 (calm) to 1 (full).</summary>
        public static float Dial(float heat, float fullHeat)
        {
            if (fullHeat <= 0f || heat <= 0f) return 0f;
            return Math.Min(1f, heat / fullHeat);
        }

        /// <summary>The average minutes between encounters, cooldown included; infinite when the sea is calm.</summary>
        public static double GapMinutes(float h, float peakPerMinute) =>
            h <= 0f || peakPerMinute <= 0f ? double.PositiveInfinity : 1.0 / (peakPerMinute * h);

        /// <summary>
        /// The chance that a 10 s roll brings an encounter. Rolls happen only outside the cooldown, so the rate is
        /// what is left of the gap after it - never more than one per roll-time.
        /// </summary>
        public static double ChancePerRoll(float h, float peakPerMinute)
        {
            double gap = GapMinutes(h, peakPerMinute);
            if (double.IsInfinity(gap)) return 0.0;
            double rollMinutes = RollSeconds / 60.0;
            double wait = Math.Max(gap - CooldownSeconds / 60.0, rollMinutes);
            return 1.0 - Math.Exp(-rollMinutes / wait);
        }

        /// <summary>The game's level: 1 (no star), 2 (one star), 3 (two stars).</summary>
        public static int Level(float h) => h < 1f / 3f ? 1 : h < 2f / 3f ? 2 : 3;

        private static bool HighHeat(float h) => h >= 2f / 3f;

        public static int Count(SeaCreature creature, float h)
        {
            switch (creature)
            {
                case SeaCreature.Drake: return HighHeat(h) ? 2 : 1;
                case SeaCreature.Deathsquito: return HighHeat(h) ? 3 : 2;
                default: return 1;
            }
        }

        public static int ActIndexOf(SeaCoast coast)
        {
            switch (coast)
            {
                case SeaCoast.Mountain: return 3;
                case SeaCoast.Plains: return 4;
                case SeaCoast.Mistlands: return 5;
                default: return AshlandsActIndex;
            }
        }

        public static SeaCreature FlyerOf(SeaCoast coast)
        {
            switch (coast)
            {
                case SeaCoast.Mountain: return SeaCreature.Drake;
                case SeaCoast.Plains: return SeaCreature.Deathsquito;
                case SeaCoast.Mistlands: return SeaCreature.Gjall;
                default: return SeaCreature.FallenValkyrie;
            }
        }

        public static string PrefabOf(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return "BonemawSerpent";
                case SeaCreature.Drake: return "Hatchling";
                case SeaCreature.Deathsquito: return "Deathsquito";
                case SeaCreature.Gjall: return "Gjall";
                case SeaCreature.FallenValkyrie: return "FallenValkyrie";
                default: return "Serpent";
            }
        }

        public static bool Flies(SeaCreature creature) => creature != SeaCreature.Serpent && creature != SeaCreature.Bonemaw;

        public static string MessageOf(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return "The boiling sea gives up a Bonemaw.";
                case SeaCreature.Drake: return "Drakes come down off the mountains.";
                case SeaCreature.Deathsquito: return "Something whines over the water.";
                case SeaCreature.Gjall: return "A Gjall drifts out over the water.";
                case SeaCreature.FallenValkyrie: return "A fallen valkyrie rises over the ash coast.";
                default: return "Something rises off the bow.";
            }
        }

        public static IEnumerable<SeaCreature> All => (SeaCreature[])Enum.GetValues(typeof(SeaCreature));

        public static SeaEncounter Make(SeaCreature creature, SeaCoast? from, float h) => new SeaEncounter
        {
            Creature = creature,
            Prefab = PrefabOf(creature),
            Count = Count(creature, h),
            Level = Level(h),
            Flies = Flies(creature),
            From = from,
            Message = MessageOf(creature),
        };

        /// <summary>
        /// What comes: a reached coast's flyer two times in three, else the sea's own creature - a Bonemaw on the
        /// Ashlands sea once Act VII is reached, otherwise a Serpent. A coast of an act not yet reached is no
        /// candidate (no spoilers). <paramref name="rng"/> returns [0, 1).
        /// </summary>
        public static SeaEncounter Choose(IEnumerable<SeaCoast> coastsNear, bool onAshlandsSea, int actIndex, float h, Func<double> rng)
        {
            var reached = (coastsNear ?? Enumerable.Empty<SeaCoast>())
                .Distinct()
                .Where(c => actIndex >= ActIndexOf(c))
                .OrderBy(c => c)
                .ToList();

            if (reached.Count > 0 && rng() < FlyerShare)
            {
                var coast = reached[Math.Min(reached.Count - 1, (int)(rng() * reached.Count))];
                return Make(FlyerOf(coast), coast, h);
            }

            var sea = onAshlandsSea && actIndex >= AshlandsActIndex ? SeaCreature.Bonemaw : SeaCreature.Serpent;
            return Make(sea, null, h);
        }
    }

    /// <summary>
    /// The voyage clock: when a roll is due. A voyage starts on the first second at sea, is quiet for its first
    /// minute, and ends after two minutes off the sea. After each encounter, a cooldown; at most two at once.
    /// </summary>
    public sealed class SeaVoyage
    {
        private float _startedAt = float.NaN;
        private float _lastAtSea;
        private float _nextRollAt;
        private float _cooldownUntil = float.NegativeInfinity;
        private int _live;
        private bool _horn;

        public bool OnVoyage => !float.IsNaN(_startedAt);
        public int Live => _live;

        /// <summary>Once a second. Says whether to roll now, and keeps the voyage's clock.</summary>
        public SeaTurn Tick(float now, bool atSea)
        {
            if (!atSea)
            {
                if (OnVoyage && now - _lastAtSea >= SeaDanger.VoyageEndSeconds)
                {
                    _startedAt = float.NaN;
                    _horn = false;
                }
                return SeaTurn.None;
            }

            if (!OnVoyage)
            {
                _startedAt = now;
                _nextRollAt = now + SeaDanger.QuietSeconds;
            }
            _lastAtSea = now;

            if (now < _nextRollAt || now < _cooldownUntil || _live >= SeaDanger.MaxLive) return SeaTurn.None;

            _nextRollAt = now + SeaDanger.RollSeconds;
            if (_horn)
            {
                _horn = false;
                return SeaTurn.Certain;
            }
            return SeaTurn.Roll;
        }

        /// <summary>An encounter came: it counts toward the limit, and the cooldown starts.</summary>
        public void Arrived(float now)
        {
            _live++;
            _cooldownUntil = now + SeaDanger.CooldownSeconds;
        }

        /// <summary>An encounter is over (all dead, gone, or far behind).</summary>
        public void Ended()
        {
            if (_live > 0) _live--;
        }

        /// <summary>The Wind-horn: the next roll is certain. Forgotten when the voyage ends.</summary>
        public void HornBlown() => _horn = true;
    }
}
```

- [ ] **Step 4: Add the source to the csproj** (`<Compile Include="RunMode\SeaDanger.cs" />` after `RunMode\DevSea.cs`) and
  run `bash Tests/run_tests.sh`. Expected: `ALL PASS`, with the SeaDanger lines `ok`.

- [ ] **Step 5: Commit:**
  `git add ICanShowYouTheWorld/RunMode/SeaDanger.cs ICanShowYouTheWorld/ICanShowYouTheWorld.csproj Tests/SeaDangerTests.cs Tests/TestMain.cs`,
  then `git commit -m "feat(run): sea danger's rules - the heat dial, the voyage clock, and what comes"` (with the trailer).

### Task 2: Coins — `RunCoins.cs`

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/RunCoins.cs`
- Create: `Tests/RunCoinsTests.cs`
- Modify: `Tests/TestMain.cs` (`RunCoinsTests.Run();` after `SeaDangerTests.Run();`)
- Modify: the csproj (`<Compile Include="RunMode\RunCoins.cs" />` after `RunMode\SeaDanger.cs`)

**Interfaces:**
- Consumes: `SeaCreature` (Task 1).
- Produces:
  - `static class RunCoins` with `const int TrollRollMin = 20, TrollRollMax = 30`;
  - `int ExtraTrollCoins(float multiplier, int vanillaRoll)`;
  - `int SeaCoins(SeaCreature creature, int level, float multiplier)`;
  - `int SeaBase(SeaCreature creature)`.

- [ ] **Step 1: Write the failing test** `Tests/RunCoinsTests.cs`:

```csharp
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Coins for the fittings (2026-10-08). The owner: "money is difficult to come by, so maybe we could increase the coin
/// drops for trolls?" - trolls drop three times their coins during a run, and the creatures the sea sends pay.
/// </summary>
static class RunCoinsTests
{
    public static void Run()
    {
        Check.That(RunCoins.ExtraTrollCoins(3f, 25) == 50 && RunCoins.ExtraTrollCoins(3f, 20) == 40,
                   "at x3 a troll's own 20-30 coins get twice as many again beside them");
        Check.That(RunCoins.ExtraTrollCoins(1f, 25) == 0 && RunCoins.ExtraTrollCoins(0.5f, 25) == 0 && RunCoins.ExtraTrollCoins(0f, 25) == 0,
                   "x1 or less adds nothing (and never takes coins away)");
        Check.That(RunCoins.ExtraTrollCoins(2.5f, 20) == 30, "a fractional multiplier rounds");

        Check.That(RunCoins.SeaCoins(SeaCreature.Serpent, 1, 1f) == 30 && RunCoins.SeaCoins(SeaCreature.Serpent, 3, 1f) == 90 &&
                   RunCoins.SeaCoins(SeaCreature.Bonemaw, 3, 1f) == 180 && RunCoins.SeaCoins(SeaCreature.Drake, 2, 1f) == 20 &&
                   RunCoins.SeaCoins(SeaCreature.Deathsquito, 1, 1f) == 5 && RunCoins.SeaCoins(SeaCreature.Gjall, 1, 1f) == 40 &&
                   RunCoins.SeaCoins(SeaCreature.FallenValkyrie, 2, 1f) == 120,
                   "the sea pays a base per creature, times its level");
        Check.That(RunCoins.SeaCoins(SeaCreature.Serpent, 2, 2f) == 120 && RunCoins.SeaCoins(SeaCreature.Serpent, 2, 0f) == 0 &&
                   RunCoins.SeaCoins(SeaCreature.Serpent, 0, 1f) == 0 && RunCoins.SeaCoins(SeaCreature.Serpent, 2, -1f) == 0,
                   "times the config's multiplier; nothing for 0, a negative multiplier, or a level below 1");
    }
}
```

- [ ] **Step 2: Register and run, expecting a failure.** `bash Tests/run_tests.sh`. Expected: `error CS0103: The name 'RunCoins' does not exist`.

- [ ] **Step 3: Write `ICanShowYouTheWorld/RunMode/RunCoins.cs`:**

```csharp
using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Coins that pay for the ship's fittings (2026-10-08). The fittings, ward included, cost about 2,400 coins, and
    /// a vanilla troll drops 20-30: during a run trolls drop more, and the creatures the sea sends pay too.
    /// </summary>
    public static class RunCoins
    {
        /// <summary>A vanilla troll's coin drop, which the extra is rolled from.</summary>
        public const int TrollRollMin = 20;
        public const int TrollRollMax = 30;

        /// <summary>
        /// The coins the saga adds beside a troll's own drop: (multiplier - 1) times a roll like the troll's, so x3
        /// means three times the coins in all. Never negative.
        /// </summary>
        public static int ExtraTrollCoins(float multiplier, int vanillaRoll) =>
            multiplier <= 1f || vanillaRoll <= 0 ? 0 : (int)Math.Round((multiplier - 1f) * vanillaRoll);

        public static int SeaBase(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return 60;
                case SeaCreature.Drake: return 10;
                case SeaCreature.Deathsquito: return 5;
                case SeaCreature.Gjall: return 40;
                case SeaCreature.FallenValkyrie: return 60;
                default: return 30;
            }
        }

        /// <summary>What a creature the sea sent drops: its base, times its level (1-3), times the config's multiplier.</summary>
        public static int SeaCoins(SeaCreature creature, int level, float multiplier) =>
            level < 1 || multiplier <= 0f ? 0 : (int)Math.Round(SeaBase(creature) * level * multiplier);
    }
}
```

- [ ] **Step 4: Add it to the csproj and run** `bash Tests/run_tests.sh`. Expected: `ALL PASS`.

- [ ] **Step 5: Commit:** `git commit -m "feat(run): coins for the fittings - trolls pay more, and the sea pays"` (the four files, with the trailer).

### Task 3: The Ward fitting — `ShipFittings.cs`

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/ShipFittings.cs`
- Modify: `Tests/ShipFittingsTests.cs`

**Interfaces:**
- Produces:
  - `FittingKind.Ward` (appended after `WindHorn`) and `ShipFittingState.Ward`;
  - `ShipFittings.WardPulseSeconds` (3f), `ShipFittings.WardRadius(int tier)`, `ShipFittings.WardDamage(int tier)`;
  - `ShipFittings.Offers(ShipFittingState state, bool fireTarTold, bool wardTold = false)`.
  - `Summary` order: Sail, Hull, Ward, Wind-horn, Fire-tar.

- [ ] **Step 1: Add the failing tests.** In `Tests/ShipFittingsTests.cs`:
  1. Replace the Fire-tar check (`told.Count == 4 … "… four keys at most"`) with:

```csharp
        Check.That(told.Count == 4 && told[3].Kind == FittingKind.FireTar && told[3].Price == 400,
                   "Fire-tar is offered once the Ashlands act is told, at four hundred, last");
```

  2. Change the `full` state to `new ShipFittingState { Sail = 3, Hull = 3, FireTar = 1, WindHorn = 1, Ward = 3 }`.
  3. Change the summary's expected text to `"Sail III · Hull III · Ward III · Wind-horn · Fire-tar"`.
  4. Add, before the god's-wind checks:

```csharp
        // The ward (2026-10-08): offered once the raven has spoken of the sea, between the horn and Fire-tar.
        Check.That(!ShipFittings.Offers(none, true).Any(o => o.Kind == FittingKind.Ward),
                   "no Ward before the raven's sea line - the player would not know what it is for");
        var warded = ShipFittings.Offers(none, true, wardTold: true);
        Check.That(warded.Count == 5 && warded[3].Kind == FittingKind.Ward && warded[3].Tier == 1 && warded[3].Price == 100 &&
                   warded[3].Name == "Ward I" && warded[4].Kind == FittingKind.FireTar,
                   "then Ward I, at a hundred, after the horn and before Fire-tar: five lines at most");
        var ward2 = ShipFittings.Bought(ShipFittings.Bought(none, FittingKind.Ward), FittingKind.Ward);
        Check.That(ward2.Ward == 2 && ShipFittings.Offers(ward2, false, true).First(o => o.Kind == FittingKind.Ward).Price == 450 &&
                   ShipFittings.Bought(full, FittingKind.Ward).Ward == 3 && !ShipFittings.Offers(full, true, true).Any(),
                   "Ward II costs 250, III 450, and III is the top");
        Check.That(ShipFittings.Offers(new ShipFittingState { Ward = 1 }, false, true).First(o => o.Kind == FittingKind.Ward).Price == 250,
                   "the card offers the ward's NEXT tier");
        Check.That(ShipFittings.WardRadius(0) == 0f && ShipFittings.WardRadius(1) == 20f && ShipFittings.WardRadius(3) == 30f &&
                   ShipFittings.WardRadius(9) == 30f && ShipFittings.WardDamage(0) == 0f && ShipFittings.WardDamage(1) == 20f &&
                   ShipFittings.WardDamage(2) == 40f && ShipFittings.WardDamage(3) == 70f && ShipFittings.WardPulseSeconds == 3f,
                   "the ward: 20/25/30 m, 20/40/70 lightning every 3 s, clamped");
        Check.That(ShipFittings.Tier(new ShipFittingState { Ward = 2 }, FittingKind.Ward) == 2 &&
                   ShipFittings.Summary(new ShipFittingState { Sail = 1, Ward = 1 }) == "Sail I · Ward I",
                   "the ward has a tier and shows in the summary");
```

- [ ] **Step 2: Run** `bash Tests/run_tests.sh`. Expected: a compile failure on `Ward`/`wardTold`.

- [ ] **Step 3: Change `ShipFittings.cs`:**
  - `public enum FittingKind { Sail, Hull, FireTar, WindHorn, Ward }`.
  - In `ShipFittingState`, add `public int Ward;` after `WindHorn`.
  - Below `HullFactors`, add:

```csharp
        private static readonly int[] WardPrices = { 100, 250, 450 };
        private static readonly float[] WardRadii = { 0f, 20f, 25f, 30f };
        private static readonly float[] WardDamages = { 0f, 20f, 40f, 70f };

        /// <summary>How often the ward strikes, in seconds.</summary>
        public const float WardPulseSeconds = 3f;
```

  - In `Tier`'s switch, add `case FittingKind.Ward: return state.Ward;` before `default`.
  - After `HullDamageFactor(ShipFittingState)`, add:

```csharp
        /// <summary>
        /// The ward (2026-10-08): while the player is aboard, a lightning pulse every few seconds strikes every hostile
        /// within this radius of the ship - the sea's answer to the sea danger, passive, so it needs no key.
        /// </summary>
        public static float WardRadius(int tier) => WardRadii[Clamp(tier, WardRadii.Length - 1)];
        public static float WardDamage(int tier) => WardDamages[Clamp(tier, WardDamages.Length - 1)];
```

  - Change the `Offers` signature to `Offers(ShipFittingState state, bool fireTarTold, bool wardTold = false)`, update its
    `<summary>`/`<paramref>` with "`wardTold` - the raven has spoken of the sea", and insert before the Fire-tar block:

```csharp
            // The ward: offered once the raven has said the sea came for you, so the player knows what it is for.
            if (wardTold && state.Ward < MaxTier(FittingKind.Ward))
            {
                int t = state.Ward + 1;
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.Ward, Tier = t, Price = WardPrices[t - 1],
                    Name = "Ward " + Roman(t),
                    Effect = $"aboard: lightning strikes attackers within {WardRadius(t):0} m every {WardPulseSeconds:0} s",
                });
            }
```

  - In `Bought`, copy `Ward = s.Ward` into `next`, and add `case FittingKind.Ward: next.Ward = Math.Min(next.Ward + 1, MaxTier(kind)); break;`.
  - In `Summary`, after the Hull line, add `if (state.Ward > 0) parts.Add("Ward " + Roman(state.Ward));`.
  - `MaxTier` needs no change: Ward falls to the default, 3.

- [ ] **Step 4: Run** `bash Tests/run_tests.sh`. Expected: `ALL PASS`.

- [ ] **Step 5: Commit:** `git commit -m "feat(run): the Ward fitting - a lightning pulse around the ship, offered once the sea has come"` (with the trailer).

### Task 4: The sea comes — `SeaWatch`, config, state and hooks

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/Unity/SeaWatch.cs`
- Modify: `ICanShowYouTheWorld/Core/Configuration.cs` (interface near line 110, fields near line 345, properties near line 439)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunStorage.cs` (state fields near lines 50–60)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs` (the poll near line 2730; `HandleHornInput` near 7131; the dev
  `DevShip` branch in `HandleDevInput`; the two reset blocks near 2371 and 9961; load near 9976; save near 10206;
  `LogSelfCheck`'s list near 7799; `DevKeyTable`)
- Modify: the csproj (`<Compile Include="RunMode\Unity\SeaWatch.cs" />` after `RunMode\Unity\DevShip.cs`)

**Interfaces:**
- Consumes: `SeaDanger`, `SeaVoyage`, `SeaEncounter`, `SeaTurn`, `SeaCoast`, `SeaCreature` (Task 1); `ShipFittings.Ward*` (Task 3).
- Produces:
  - `struct SeaSettings { bool Enabled; float Heat; float FullHeat; float PeakPerMinute; int ActIndex; int WardTier; }`.
  - `class SeaWatch` with:
    - `SeaWatch(Action<string> say, Action firstEncounter, System.Random rng)`;
    - `void Tick(Player, SeaSettings)`, `bool Force(Player, SeaSettings)`, `void HornBlown()`, `void Reset()`;
    - `bool IsSent(Character c, out SeaCreature creature, out int level)`, used by Task 5.
  - On `RunService`: `_seaRavenTold`; on `IConfiguration`: `RunSeaDanger`, `RunSeaFullHeat`, `RunSeaPeakPerMinute`,
    `RunTrollCoinMultiplier`, `RunSeaCoinMultiplier`.

- [ ] **Step 1: Config.**
  - In `IConfiguration`, after `string RunKeyLayout { get; set; }`, add:

```csharp
        bool RunSeaDanger { get; set; }
        float RunSeaFullHeat { get; set; }
        float RunSeaPeakPerMinute { get; set; }
        float RunTrollCoinMultiplier { get; set; }
        float RunSeaCoinMultiplier { get; set; }
```

  - After the `runKeyLayout` field, add:

```csharp
        // Sea danger (docs/superpowers/specs/2026-10-08-sea-danger-design.md): the sea sends creatures as heat rises.
        // Full danger at this heat; the average minutes between encounters is 1 / (peak x heat/full), cooldown included.
        [SerializeField] private bool runSeaDanger = true;
        [SerializeField] private float runSeaFullHeat = 40f;
        [SerializeField] private float runSeaPeakPerMinute = 0.5f;
        // Coins for the fittings: trolls drop this many times their coins during a run; the sea's creatures pay x this.
        [SerializeField] private float runTrollCoinMultiplier = 3f;
        [SerializeField] private float runSeaCoinMultiplier = 1f;
```

  - After the `RunKeyLayout` property, add:

```csharp
        public bool RunSeaDanger { get => runSeaDanger; set => runSeaDanger = value; }
        public float RunSeaFullHeat { get => runSeaFullHeat; set => runSeaFullHeat = value; }
        public float RunSeaPeakPerMinute { get => runSeaPeakPerMinute; set => runSeaPeakPerMinute = value; }
        public float RunTrollCoinMultiplier { get => runTrollCoinMultiplier; set => runTrollCoinMultiplier = value; }
        public float RunSeaCoinMultiplier { get => runSeaCoinMultiplier; set => runSeaCoinMultiplier = value; }
```

- [ ] **Step 2: Run state.** In `RunStorage.cs`'s state class, add `public int shipWard;` after `public int shipWindHorn;`, and
  after `public bool godWindTold;`:

```csharp
        /// <summary>Whether the raven has said the sea came for you (the first encounter); it also unlocks the Ward.</summary>
        public bool seaRavenTold;
```

- [ ] **Step 3: Write `ICanShowYouTheWorld/RunMode/Unity/SeaWatch.cs`:**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>What SeaWatch needs to know each second, from the run.</summary>
    internal struct SeaSettings
    {
        public bool Enabled;
        public float Heat;
        public float FullHeat;
        public float PeakPerMinute;
        public int ActIndex;
        public int WardTier;
    }

    /// <summary>
    /// The sea answers the heat - the game side of <see cref="SeaDanger"/> (2026-10-08; see the spec,
    /// docs/superpowers/specs/2026-10-08-sea-danger-design.md). Once a second: is the player at sea, is a roll due,
    /// what comes; then it spawns them off the bow or from the coast, hunting the player and never saved; and it
    /// keeps count of what it sent, for the limit and for the coins.
    /// </summary>
    internal sealed class SeaWatch
    {
        private const float RingNear = 150f;
        private const float RingFar = 300f;
        private const int RingDirections = 16;
        private const float EncounterReach = 300f;

        private sealed class Group
        {
            public SeaCreature Creature;
            public int Level;
            public readonly List<Character> Members = new List<Character>();
        }

        private readonly Action<string> _say;
        private readonly Action _firstEncounter;
        private readonly System.Random _rng;
        private readonly List<Group> _groups = new List<Group>();
        private SeaVoyage _voyage = new SeaVoyage();
        private bool _noSerpentLogged;

        public SeaWatch(Action<string> say, Action firstEncounter, System.Random rng)
        {
            _say = say;
            _firstEncounter = firstEncounter;
            _rng = rng ?? new System.Random();
        }

        /// <summary>A run starts or ends: forget the voyage and what was sent (they vanish with their area).</summary>
        public void Reset()
        {
            _groups.Clear();
            _voyage = new SeaVoyage();
        }

        public void HornBlown() => _voyage.HornBlown();

        /// <summary>Once a second while a run is live.</summary>
        public void Tick(Player player, SeaSettings s)
        {
            if (player == null) return;
            Prune(player);

            if (!s.Enabled || s.ActIndex < SeaDanger.FirstActIndex) return;
            var ship = Ship.GetLocalShip();
            var turn = _voyage.Tick(Time.time, AtSea(ship, true));
            if (turn == SeaTurn.None) return;

            float h = SeaDanger.Dial(s.Heat, s.FullHeat);
            if (h <= 0f) return;   // heat 0: the sea is vanilla's, and the horn calls nothing
            bool comes = turn == SeaTurn.Certain || _rng.NextDouble() < SeaDanger.ChancePerRoll(h, s.PeakPerMinute);
            if (comes) Encounter(player, ship, s, h, turn == SeaTurn.Certain ? "the horn" : "rolled");
        }

        /// <summary>Dev: an encounter now, if the player is aboard a ship over open water. False otherwise.</summary>
        public bool Force(Player player, SeaSettings s)
        {
            var ship = Ship.GetLocalShip();
            if (player == null || !AtSea(ship, false)) return false;
            Encounter(player, ship, s, SeaDanger.Dial(s.Heat, s.FullHeat), "dev");
            return true;
        }

        /// <summary>Whether <paramref name="c"/> is one the sea sent this run, and as what.</summary>
        public bool IsSent(Character c, out SeaCreature creature, out int level)
        {
            foreach (var g in _groups)
                foreach (var m in g.Members)
                    if (ReferenceEquals(m, c))
                    {
                        creature = g.Creature;
                        level = g.Level;
                        return true;
                    }
            creature = SeaCreature.Serpent;
            level = 0;
            return false;
        }

        private static bool OverOpenWater(Vector3 p)
        {
            var wg = WorldGenerator.instance;
            var zs = ZoneSystem.instance;
            return wg != null && zs != null && wg.GetHeight(p.x, p.z) <= zs.m_waterLevel - 1f;
        }

        /// <summary>Aboard a ship over open water - and, for the rolls, moving (sail or oars).</summary>
        private static bool AtSea(Ship ship, bool moving) =>
            ship != null && OverOpenWater(ship.transform.position) && (!moving || Mathf.Abs(ship.GetSpeed()) >= 1f);

        private void Prune(Player player)
        {
            Vector3 at = player.transform.position;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                // Unity's null is wanted here: a destroyed creature is gone.
                _groups[i].Members.RemoveAll(c => c == null || c.IsDead() ||
                                                  Vector3.Distance(c.transform.position, at) > EncounterReach);
                if (_groups[i].Members.Count > 0) continue;
                _groups.RemoveAt(i);
                _voyage.Ended();
            }
        }

        private void Encounter(Player player, Ship ship, SeaSettings s, float h, string why)
        {
            Vector3 at = ship.transform.position;
            var coasts = CoastsNear(at);
            var wg = WorldGenerator.instance;
            bool ashSea = wg != null && wg.GetBiome(at) == Heightmap.Biome.AshLands;
            var enc = SeaDanger.Choose(coasts.Keys, ashSea, s.ActIndex, h, _rng.NextDouble);

            var prefab = Prefab(enc.Prefab);
            if (prefab == null)
            {
                // A creature the game does not have: a Serpent instead (the self-check names it).
                enc = SeaDanger.Make(SeaCreature.Serpent, null, h);
                prefab = Prefab(enc.Prefab);
            }
            if (prefab == null)
            {
                if (!_noSerpentLogged) Debug.LogWarning("[ICanShowYouTheWorld] Sea: no Serpent prefab - the sea stays calm.");
                _noSerpentLogged = true;
                return;
            }

            var group = new Group { Creature = enc.Creature, Level = enc.Level };
            Vector3 dir;
            if (enc.Flies && enc.From.HasValue && coasts.TryGetValue(enc.From.Value, out dir)) SpawnFlyers(prefab, enc, at, dir, group);
            else if (!enc.Flies) SpawnSwimmers(prefab, enc, ship, group);

            if (group.Members.Count == 0)
            {
                Debug.Log($"[ICanShowYouTheWorld] Sea: no room for {enc.Prefab} ({why}) - no open water ahead.");
                return;
            }

            _groups.Add(group);
            _voyage.Arrived(Time.time);
            _firstEncounter?.Invoke();
            _say?.Invoke(enc.Message);
            Debug.Log($"[ICanShowYouTheWorld] Sea: {group.Members.Count} x {enc.Prefab}, level {enc.Level} ({why}); " +
                      $"heat {s.Heat:0.#}, dial {h:0.00}, gap {SeaDanger.GapMinutes(h, s.PeakPerMinute):0.#} min.");
        }

        private static GameObject Prefab(string name) => ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;

        /// <summary>Each reached flyer coast within 300 m, with the direction it lies in from the ship.</summary>
        private static Dictionary<SeaCoast, Vector3> CoastsNear(Vector3 at)
        {
            var found = new Dictionary<SeaCoast, Vector3>();
            var wg = WorldGenerator.instance;
            var zs = ZoneSystem.instance;
            if (wg == null || zs == null) return found;

            foreach (float r in new[] { RingNear, RingFar })
                for (int i = 0; i < RingDirections; i++)
                {
                    float a = i * Mathf.PI * 2f / RingDirections;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var p = at + dir * r;
                    if (wg.GetHeight(p.x, p.z) <= zs.m_waterLevel + 1f) continue;
                    SeaCoast coast;
                    if (!CoastOf(wg.GetBiome(p), out coast) || found.ContainsKey(coast)) continue;
                    found[coast] = dir;
                }
            return found;
        }

        private static bool CoastOf(Heightmap.Biome biome, out SeaCoast coast)
        {
            switch (biome)
            {
                case Heightmap.Biome.Mountain: coast = SeaCoast.Mountain; return true;
                case Heightmap.Biome.Plains: coast = SeaCoast.Plains; return true;
                case Heightmap.Biome.Mistlands: coast = SeaCoast.Mistlands; return true;
                case Heightmap.Biome.AshLands: coast = SeaCoast.Ashlands; return true;
                default: coast = SeaCoast.Mountain; return false;
            }
        }

        /// <summary>Sea creatures surface 50-70 m ahead, within 45 degrees of the heading, on open water. Up to 8 tries.</summary>
        private void SpawnSwimmers(GameObject prefab, SeaEncounter enc, Ship ship, Group group)
        {
            var zs = ZoneSystem.instance;
            if (zs == null) return;
            Vector3 fwd = ship.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            for (int tries = 0; tries < 8; tries++)
            {
                float angle = (float)(_rng.NextDouble() * 90.0 - 45.0);
                float dist = 50f + (float)_rng.NextDouble() * 20f;
                Vector3 spot = ship.transform.position + Quaternion.Euler(0f, angle, 0f) * fwd * dist;
                if (!OverOpenWater(spot)) continue;
                spot.y = zs.m_waterLevel;
                for (int i = 0; i < enc.Count; i++) Make(prefab, spot + Side(i), enc.Level, ship.transform.position, group);
                return;
            }
        }

        /// <summary>Flyers come from the coast's side, 40-60 m out and about 15 m up.</summary>
        private void SpawnFlyers(GameObject prefab, SeaEncounter enc, Vector3 ship, Vector3 dir, Group group)
        {
            var zs = ZoneSystem.instance;
            if (zs == null) return;
            float dist = 40f + (float)_rng.NextDouble() * 20f;
            Vector3 spot = ship + dir * dist;
            spot.y = zs.m_waterLevel + 15f;
            for (int i = 0; i < enc.Count; i++) Make(prefab, spot + Side(i), enc.Level, ship, group);
        }

        private static Vector3 Side(int i) => i == 0 ? Vector3.zero : new Vector3((i % 2 * 2 - 1) * 3f * ((i + 1) / 2), 0f, 0f);

        private static void Make(GameObject prefab, Vector3 at, int level, Vector3 lookAt, Group group)
        {
            Vector3 look = lookAt - at;
            look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
            var inst = UnityEngine.Object.Instantiate(prefab, at, rot);
            var ch = inst != null ? inst.GetComponent<Character>() : null;
            if (ch == null) return;

            // Set at full health: SetLevel recomputes max health from the current value (DeerHerd).
            if (level > 1) ch.SetLevel(level);

            // Never saved, like the Herald and the contest pack: nothing piles up in the player's world.
            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo != null) zdo.Persistent = false;

            var monster = inst.GetComponent<MonsterAI>();
            if (monster != null) monster.SetHuntPlayer(true);
            var ai = inst.GetComponent<BaseAI>();
            if (ai != null) ai.Alert();

            group.Members.Add(ch);
        }
    }
}
```

- [ ] **Step 4: Hooks in `RunService.cs`.**
  - **Fields, near `_godWindTold`:**

```csharp
        // The sea answers the heat (2026-10-08): see SeaWatch and SeaDanger.
        private const string SeaRavenLine =
            "The sea has found your wake. The hotter you burn, the more of it comes. A ward at the helm keeps the worst of it off.";
        private bool _seaRavenTold;
        private SeaWatch _sea;
        private SeaWatch Sea => _sea ?? (_sea = new SeaWatch(Message, SeaFirstEncounter, _rng));

        private SeaSettings SeaSettingsNow() => new SeaSettings
        {
            Enabled = _cfg.RunSeaDanger,
            Heat = _heat.Heat,
            FullHeat = _cfg.RunSeaFullHeat,
            PeakPerMinute = _cfg.RunSeaPeakPerMinute,
            ActIndex = _actIndex,
            WardTier = _fittings.Ward,
        };

        /// <summary>The raven speaks of the sea once a run, at its first encounter; that also unlocks the Ward.</summary>
        private void SeaFirstEncounter()
        {
            if (_seaRavenTold) return;
            _seaRavenTold = true;
            if (!TrySpawnRaven("sea", SeaRavenLine)) Message(SeaRavenLine);
            SaveState();
        }

        private void PollSea()
        {
            try { Sea.Tick(Player.m_localPlayer, SeaSettingsNow()); }
            catch (Exception ex) { LogOnce("sea", ex); }
        }
```

  - **The poll:** after `if (_active) PollWinds();`, add `if (_active) PollSea();`.
  - **The horn:** in `HandleHornInput`, after `_winds.BlowHorn(player);`, add `Sea.HornBlown();   // noise: the sea answers`.
  - **Both reset blocks** (the ones with `_godWindTold = false;`): add `_seaRavenTold = false;` and `_sea?.Reset();`.
  - **Load:** change the fittings line to
    `_fittings = new ShipFittingState { Sail = s.shipSail, Hull = s.shipHull, FireTar = s.shipFireTar, WindHorn = s.shipWindHorn, Ward = s.shipWard };`
    and add `_seaRavenTold = s.seaRavenTold;` after `_godWindTold = s.godWindTold;`.
  - **Save:** add `shipWard = _fittings.Ward,` after `shipWindHorn = _fittings.WindHorn,`, and `seaRavenTold = _seaRavenTold,` after
    `godWindTold = _godWindTold,`.
  - **The fittings card** (both `ShipFittings.Offers(_fittings, FireTarTold)` calls):
    `ShipFittings.Offers(_fittings, FireTarTold, _seaRavenTold)`.
  - **The dev ship branch** in `HandleDevInput`: replace `try { DevShipYard.Launch(Player.m_localPlayer); }` with:

```csharp
                // MACBOOK-TEMP: aboard at sea, B calls the sea instead of building another ship.
                try { if (!Sea.Force(Player.m_localPlayer, SeaSettingsNow())) DevShipYard.Launch(Player.m_localPlayer); }
```

  - **`DevKeyTable`'s ship row:** change the text to `"A Karve on the nearest sea, you at its helm; aboard at sea: the sea answers now"`.
  - **The self-check:** add `CheckSea();` after `CheckSpeakersAndTraders();` in the list, and the method next to `CheckKeys`:

```csharp
        /// <summary>The six creatures sea danger can send (2026-10-08): a missing one becomes a Serpent.</summary>
        private void CheckSea()
        {
            if (_selfCheck == null) return;
            try
            {
                var scene = ZNetScene.instance;
                if (scene == null)
                {
                    _selfCheck.Fallback("Sea creatures", "the scene was not ready - not checked this run");
                    return;
                }
                var names = SeaDanger.All.Select(SeaDanger.PrefabOf).ToList();
                var missing = names.Where(n => scene.GetPrefab(n)?.GetComponent<Character>() == null).ToList();
                _selfCheck.AllOf("Sea creatures", names.Count, missing, "a Serpent comes instead; with no Serpent the sea stays calm");
            }
            catch (Exception ex) { LogOnce("self-check-sea", ex); }
        }
```

  Check `AllOf`'s parameter types against `RunMode/Unity/SagaSelfCheck.cs` (the quest-items check passes a `List<string>`).

- [ ] **Step 5: Add `SeaWatch.cs` to the csproj, build and check references:**
  `msbuild Valheim.sln -p:Configuration=Debug -v:minimal 2>&1 | grep -E "error|ICanShowYouTheWorld ->"`, then
  `bash Tests/run_tests.sh` (`ALL PASS`) and `bash Scripts/check_refs.sh` (`0 unresolved`).

- [ ] **Step 6: Old saves.** State loads through `JsonUtility.FromJson<RunSaveState>(json)` (`RunStorage.cs:377`), which
  leaves missing fields at their defaults (0, false). So an old save loads with no ward and no sea line said;
  nothing to write. Say so in the commit message.

- [ ] **Step 7: Commit:**
  `git commit -m "feat(run): the sea comes - SeaWatch sends creatures by heat, the raven speaks once, the horn draws them, Shift+B aboard calls one"`.

### Task 5: The ward strikes, and the coins drop

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/Unity/CoinDrops.cs`
- Modify: `ICanShowYouTheWorld/RunMode/Unity/SeaWatch.cs` (the ward pulse)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs` (`OnCharacterDied`)
- Modify: the csproj (`<Compile Include="RunMode\Unity\CoinDrops.cs" />` after `RunMode\Unity\SeaWatch.cs`)

**Interfaces:**
- Consumes: `ShipFittings.WardRadius/WardDamage/WardPulseSeconds` (Task 3); `RunCoins` (Task 2); `SeaWatch.IsSent`,
  `SeaSettings.WardTier` (Task 4).
- Produces: `static class CoinDrops { static void Drop(Vector3 at, int count) }`.

- [ ] **Step 1: The ward pulse in `SeaWatch`.**
  - Add the fields:
    - `private float _nextPulse;`
    - `private readonly List<Character> _inRange = new List<Character>();`
    - `private GameObject _spark;`
    - `private bool _sparkResolved;`
    - `private static readonly string[] SparkPrefabs = { "vfx_lightning", "fx_lightning" };`
  - In `Tick`, call `Pulse(player, s);` right after `Prune(player);`. The ward works whether sea danger is on or not.
  - In `Reset`, add `_nextPulse = 0f;`.
  - Add the methods:

```csharp
        /// <summary>
        /// The Ward (2026-10-08): every few seconds while the player is aboard, lightning strikes every hostile within
        /// the ward's radius of the ship - through the ordinary damage path, so kills count. Never the player, the
        /// tamed, non-enemies, or anything lightning cannot hurt (every saga speaker is made immune to it).
        /// </summary>
        private void Pulse(Player player, SeaSettings s)
        {
            if (s.WardTier < 1 || Time.time < _nextPulse) return;
            var ship = Ship.GetLocalShip();
            if (ship == null) return;
            _nextPulse = Time.time + ShipFittings.WardPulseSeconds;

            float damage = ShipFittings.WardDamage(s.WardTier);
            _inRange.Clear();
            Character.GetCharactersInRange(ship.transform.position, ShipFittings.WardRadius(s.WardTier), _inRange);
            foreach (var c in _inRange)
            {
                if (c == null || c.IsPlayer() || c.IsTamed() || c.IsDead() || !BaseAI.IsEnemy(player, c)) continue;
                if (c.GetDamageModifiers(null).m_lightning == HitData.DamageModifier.Immune) continue;

                var hit = new HitData();
                hit.m_damage.m_lightning = damage;
                hit.m_point = c.GetCenterPoint();
                hit.SetAttacker(player);
                c.Damage(hit);
                Spark(hit.m_point);
            }
        }

        /// <summary>A small spark where the ward strikes, if the game has the effect (asset names are guesses).</summary>
        private void Spark(Vector3 at)
        {
            if (!_sparkResolved)
            {
                _sparkResolved = true;
                var scene = ZNetScene.instance;
                if (scene != null)
                    foreach (var name in SparkPrefabs)
                    {
                        _spark = scene.GetPrefab(name);
                        if (_spark != null) break;
                    }
                if (_spark == null) Debug.Log("[ICanShowYouTheWorld] Ward: no lightning effect resolved; it strikes unseen.");
            }
            if (_spark == null) return;
            try { UnityEngine.Object.Instantiate(_spark, at, Quaternion.identity); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Ward spark failed: " + e.Message); }
        }
```

- [ ] **Step 2: Write `ICanShowYouTheWorld/RunMode/Unity/CoinDrops.cs`:**

```csharp
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Drops coins in the world: one stack of the game's own Coins item (2026-10-08, the fittings' money).</summary>
    internal static class CoinDrops
    {
        private const string CoinsPrefab = "Coins";

        public static void Drop(Vector3 at, int count)
        {
            if (count <= 0 || ObjectDB.instance == null) return;
            var prefab = ObjectDB.instance.GetItemPrefab(CoinsPrefab);
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, at + Vector3.up, Quaternion.identity);
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop != null) drop.SetStack(count);
        }
    }
}
```

- [ ] **Step 3: Coins on death.** In `RunService.OnCharacterDied`, right after `string prefabName = PrefabNameOf(c);`, add:

```csharp
                // Coins for the fittings (2026-10-08): trolls pay more during a run, and the creatures the sea sent pay.
                try
                {
                    int coins = 0;
                    if (prefabName == "Troll")
                        coins += RunCoins.ExtraTrollCoins(_cfg.RunTrollCoinMultiplier, _rng.Next(RunCoins.TrollRollMin, RunCoins.TrollRollMax + 1));
                    SeaCreature seaCreature;
                    int seaLevel;
                    if (_sea != null && _sea.IsSent(c, out seaCreature, out seaLevel))
                        coins += RunCoins.SeaCoins(seaCreature, seaLevel, _cfg.RunSeaCoinMultiplier);
                    if (coins > 0) CoinDrops.Drop(c.transform.position, coins);
                }
                catch (Exception ex) { LogOnce("run-coins", ex); }
```

- [ ] **Step 4: Add `CoinDrops.cs` to the csproj; build, test, check refs.** Build with msbuild (no `error`), then
  `bash Tests/run_tests.sh` (`ALL PASS`), then `bash Scripts/check_refs.sh` (`0 unresolved`; this confirms
  `GetDamageModifiers`, `SetAttacker`, `SetStack`, `GetItemPrefab` and `IsEnemy` resolve in 1.0.17).

- [ ] **Step 5: Commit:** `git commit -m "feat(run): the Ward strikes around the ship, and coins drop - trolls x3, and the sea pays"`.

### Task 6: Docs, the build, and the Mac

**Files:**
- Modify: `docs/SAGA-WALKTHROUGH.md` (the boats part)
- Modify: `dist/windows/DEV-MODE.md` (the dev ship section)
- Modify: `CLAUDE.md` (the self-check sentence)
- Modify: `docs/superpowers/RESUME.md`
- Modify: `docs/superpowers/2026-10-06-home-test-plan.md`
- Modify: `HANDOFF_WINDOWS.md`

- [ ] **Step 1: Walkthrough.** In the boats part, add a paragraph:
  - From Act II, when your heat is above 0, the sea sends creatures while you sail: Serpents, or a reached act's
    flyers off its coast, or a Bonemaw on the Ashlands sea.
  - The raven warns once, and then the helm offers a Ward (I–III), a lightning pulse around the ship.
  - The Wind-horn draws them.
  - Trolls drop three times their coins during a run, and the sea's creatures drop coins.
- [ ] **Step 2: `DEV-MODE.md`.** In the dev ship section, add: "Aboard a ship over open water, `mod` + `B` calls an
  encounter now instead (chosen as normal for the place, by your heat), and `Player.log` says `Sea:` and what came."
- [ ] **Step 3: `CLAUDE.md`.** Add "Sea creatures" to the self-check's description, where its lines are listed (if they are).
- [ ] **Step 4: Test plan check 38b,** after check 38:
  > **38b. The sea answers the heat.** With heat above 0 from Act II, sail open water for a few minutes.
  > - **See:** the raven's line once, then a message per encounter.
  > - A Serpent ahead, or flyers near a reached act's coast, starred as heat rises.
  > - After the raven, the helm offers Ward I (100 coins); bought, it sparks and strikes what comes within 20 m.
  > - A troll drops 60–90 coins; a sea creature drops coins.
  > - `Select-String Player.log -Pattern "Sea:"` lists each encounter.
  > - Dev: `Shift`+`Keypad .` aboard calls one now.

  Update the badge to the new build.
- [ ] **Step 5: RESUME and HANDOFF.** Add a RESUME bullet and the version line, and a HANDOFF task for the new build
  with the same checks as 38b.
- [ ] **Step 6: Commit the docs, then the build loop** (Global Constraints), ending with `bash Scripts/deploy_local.sh`
  (it must say `Bundle signature valid`).
- [ ] **Step 7: The todo repo.**
  - Update the saga section's build badge.
  - Mark the treacherous-waters idea done, pointing to the spec and the build.
  - Add a log entry and a README row.
  - Commit and push.
- [ ] **Step 8: On the Mac,** when the owner is there, with god mode (`0`), the dev ship (`Shift`+`B`) and slay
  (`fn`+`Backspace`):
  - `Shift`+`B` at sea brings a Serpent, which hunts the ship;
  - a reload removes it;
  - the raven speaks once;
  - the helm then offers Ward I;
  - with Ward I and a slain drake or deathsquito, coins float on the water;
  - five lines fit the fittings card.
