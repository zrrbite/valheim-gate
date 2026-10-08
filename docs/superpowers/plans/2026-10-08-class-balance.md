# The Ways, Balanced — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every way (class) an always-on engine and a short-cooldown verb, measured against the Hunter made honest, grow every way with a tempering per god from Bonemass to the Queen, tilt the boon wheel toward each way, and cap stacked weapon bonuses.

**Architecture:** Every number and rule lives in one pure class, `RunMode/WayRules.cs`, unit-tested by `Tests/run_tests.sh`. The game side lives in a new partial of `BoonEffects` (`RunMode/Unity/BoonEffects.Ways.cs`), ticked once a second from `RunService.PollCompanionPassives`, plus two status-effect subclasses in `RunMode/Unity/WayEffects.cs`. No Patcher change: every hook is the game's own (the research is `docs/superpowers/notes/2026-10-08-way-hooks.md`, Task 0).

**Tech Stack:** C# 7.3, .NET Framework 4.7.2, Unity 6000.0.75 (Valheim 1.0.17), Mono msbuild, the repo's `Check.That` test harness.

**Spec:** `docs/superpowers/specs/2026-10-08-class-balance-design.md`

## Global Constraints

- C# **7.3**: no switch expressions, no `??=`, no ranges, no records. Local functions, tuples and `out var` are fine.
- Pure code goes in `ICanShowYouTheWorld/RunMode/*.cs` (compiled by `Tests/run_tests.sh` with every `Tests/*.cs`), must not
  reference UnityEngine, and every new file is also added to `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj` as
  `<Compile Include="RunMode\<File>.cs" />`. Game code goes in `ICanShowYouTheWorld/RunMode/Unity/`.
- A new test class is registered in `Tests/TestMain.cs` (`XTests.Run();`).
- **Buffs to a creature are per creature, never through `m_shared`** (spec, shared rule 2). `m_shared` is one object
  for every copy of an item: writing it changes the wild ones too. The player's own weapon product
  (`RefreshWeaponDamage`) is the one existing exception and stays player-only.
- **Everything is a loan** (shared rule 3): every effect comes off at run end through `BoonEffects.Unapply`/`UnapplyAll`,
  and a status effect is re-laid on respawn by `RunService.ReapplyPassiveBoonEffects` (passives are re-applied there
  through `Apply`, so `Apply` must be idempotent).
- **The star rule** (shared rule 1): `TameStars.SummonLevel(gods)` and `TameStars.WithShepherd(level)`, already built in
  `...08i`. Retinue cap stays `BoonEffects.MaxCompanions = 4`.
- **No new keys** (shared rule 5). Rungs keep `U I O` (laptop) / `Keypad 7 0 Insert` (numpad).
- **The card says what the kit does** (shared rule 6): every changed number changes `RunService.DefaultBoons()`'s
  `Description`, the way's `ClassDefinition.Description` (`RunMode/ClassLadder.cs`), and the thane's line in
  `RunMode/Unity/Thane.cs` where it names the kit.
- **No spoilers** (shared rule 7): a tempering line names its god only once that god is the next to fall or fallen
  (`WayRules.TemperShown`).
- Gods felled = `_defeatedBossCount()` in `BoonEffects`, `DefeatedBosses` in `RunService`.
- Every commit ends with the two lines:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and
  `Claude-Session: https://claude.ai/code/session_01VkpcyyQ86pSouuPbxs5C9f`.
- Verification commands, used throughout:
  - tests: `bash Tests/run_tests.sh` (ends `ALL PASS`);
  - build: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal` (exit 0);
  - references: `bash Scripts/check_refs.sh` (ends `0 unresolved.`).
- **Do not tag, stage or deploy per task.** Task 13 builds the release once, and only when the owner says the current
  builds have been played (memory: build no faster than the owner can test).

## Review Focus

1. **A way's effect outliving the way** — laid down at the thane (respec), lost to the dev class cycle, or the run ending:
   every engine, status effect, standing song, turret and loan must come off. Each task's `Unapply` case is the gate.
2. **A reload or respawn mid-effect** — Guard, Forge-skin and the swim effect are status effects, cleared by death; the
   respawn's `ReapplyPassiveBoonEffects` must put them back, and a resume must not double them.
3. **Saved runs holding a boon id this plan removes** (`tide`, `fairwind`, `reinforce`) — `RestoreHeld` drops unknown
   ids silently; `LearnDueClassBoons` must then grant the replacements (`undertow`, `stormcaller`, `watchpost`).
   Task 9 and Task 10 each test `ClassLadder.Due` on a held set with the old id.
4. **The ballista** — it targets every awake non-player (deer too) and its bolts have no owner, so a stray one can hit
   the player or a tame (vanilla behaviour). It must never be saved, never drop its build cost, and come down with the
   run. Task 10.
5. **Fury counting the mod's own hits** — Rend, Wrath, the Ward and Undertow all go through `Character.Damage` with the
   player as attacker, so each target adds to `EnemyHits`. For a Berserker that is the design (Rend's hits are Fury
   hits); the Ward is a fitting anyone can buy, so a Berserker at sea builds Fury from it. Accepted, and said in Task 5.

---

## Rulings already made (from the research, 2026-10-08)

- **The ballista's bolts** are `TurretBoltWood`, `TurretBolt` (black metal) and `TurretBoltFlametal`; the spec's
  "bone, iron, blackmetal" are crossbow bolts the ballista does not accept. Wood before Bonemass, black metal from
  Bonemass, flametal from the Queen. Cost if wrong: a different bolt.
- **The Hunter's Moder tempering** becomes "Thor's bow's element strikes half again as hard". The spec's "fire burns
  and frost slows half again as long" would need the game's own burn and slow durations per hit, which no hook exposes.
  Cost if wrong: a different flavour of the same strengthening.
- **The Húskarl's Bonemass tempering** becomes "Shield Bash reaches 6 m" (from 4): Shield Bash already staggers
  everything in front. Cost if wrong: none.
- **Reckless and Blood Rage** (found in the research, not in the spec): Reckless never applied its +50% (the multiplier
  is declared and unused), and both costs are the game's `Weak` (x1.5) where the cards say 25%. Task 1 makes both
  honest: Reckless joins the weapon product, both costs become `SlightlyWeak` (x1.25).
- **Tide-borne does nothing in water** (research §7): `Player.UpdateStats` zeroes stamina regen while swimming, so its
  +8 regen never offsets the drain. The spec already retires it into the Sæfari's passive; Task 8 uses the game's
  swim-stamina modifier instead.

---

### Task 0: Keep the research

**Files:**
- Create: `docs/superpowers/notes/2026-10-08-way-hooks.md`

- [ ] **Step 1: Copy the research report into the repo**

```bash
mkdir -p docs/superpowers/notes
cp /Users/martinkjeldsen/.claude/jobs/ee1f2a11/tmp/hooks-report.md docs/superpowers/notes/2026-10-08-way-hooks.md
```

If that path is gone (a new machine, a cleaned job folder), write the note from this plan's "Rulings" section and
each task's "Why" lines instead; the plan does not depend on the file.

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/notes/2026-10-08-way-hooks.md
git commit -m "docs: the hooks behind the ways' engines - what the 1.0.17 IL offers, no Patcher change"
```

---

### Task 1: Reckless and Blood Rage honest, and the damage ceiling

**Why:** Reckless's `RecklessDamageMultiplier` (BoonEffects.cs:69) is never applied, so the boon is all cost; the cost
is `Weak` (x1.5) where its card says 25%. The spec caps the weapon product at x2.5 (spec, "The damage ceiling").

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/WayRules.cs`
- Create: `Tests/WayRulesTests.cs`
- Modify: `Tests/TestMain.cs`, `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj`
- Modify: `ICanShowYouTheWorld/RunMode/Unity/BoonEffects.cs` (Apply `case "reckless"` ~line 541; Unapply `case "reckless"`
  ~line 672; `RefreshDamageModifiers` ~line 1216; `RefreshWeaponDamage` ~line 1110), `RunService.cs`, `RunWindow.cs`
  (`DrawBoonsPage`)

**Interfaces:**
- Produces: `public static class WayRules` with `public const float WeaponCeiling = 2.5f;` and
  `public static float WeaponProduct(IEnumerable<float> factors)`.

- [ ] **Step 1: Write the failing test**

`Tests/WayRulesTests.cs`:

```csharp
using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The ways' numbers (2026-10-08, class balance): docs/superpowers/specs/2026-10-08-class-balance-design.md.
/// </summary>
static class WayRulesTests
{
    public static void Run()
    {
        // The damage ceiling.
        Check.That(WayRules.WeaponProduct(new float[0]) == 1f, "no weapon bonus: x1");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 1.2f, 1.5f }) - 1.8f) < 0.001f, "bonuses multiply below the ceiling");
        Check.That(WayRules.WeaponProduct(new[] { 1.2f, 1.4f, 1.2f, 1.5f, 1.5f }) == 2.5f,
                   "Sharpened, Glass Cannon, Stoker, Reckless and Fury together stop at x2.5, not x4.5");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 0.8f }) - 0.8f) < 0.001f, "a factor below one still applies");
        Check.That(WayRules.WeaponProduct(null) == 1f, "nothing at all is x1, not an error");
    }
}
```

Register it in `Tests/TestMain.cs` after `TameStarsTests.Run();`:

```csharp
        WayRulesTests.Run();
```

- [ ] **Step 2: Run it to verify it fails**

Run: `bash Tests/run_tests.sh`
Expected: compile error `The name 'WayRules' does not exist in the current context`.

- [ ] **Step 3: Write the minimal implementation**

`ICanShowYouTheWorld/RunMode/WayRules.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The ways' numbers and rules (2026-10-08, class balance; docs/superpowers/specs/2026-10-08-class-balance-design.md).
    /// Pure, so every number is tested; BoonEffects applies them.
    /// </summary>
    public static class WayRules
    {
        /// <summary>Stacked weapon bonuses stop here (spec, "The damage ceiling"): x4.5 was reachable.</summary>
        public const float WeaponCeiling = 2.5f;

        /// <summary>The product of the live weapon factors, capped at <see cref="WeaponCeiling"/>.</summary>
        public static float WeaponProduct(IEnumerable<float> factors)
        {
            float p = 1f;
            if (factors != null)
                foreach (var f in factors) p *= f;
            return Math.Min(WeaponCeiling, p);
        }
    }
}
```

Add to the csproj, after `<Compile Include="RunMode\TameStars.cs" />`:

```xml
    <Compile Include="RunMode\WayRules.cs" />
```

- [ ] **Step 4: Run it to verify it passes**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The game side**

In `BoonEffects.RefreshWeaponDamage`, replace

```csharp
            float product = 1f;
            foreach (var m in _weaponMultipliers.Values) product *= m;
```

with

```csharp
            // Capped (2026-10-08): Sharpened, Glass Cannon, Stoker, Reckless, Forge-fed and Fury held together
            // reached about x4.5. See WayRules.WeaponCeiling.
            float product = WayRules.WeaponProduct(_weaponMultipliers.Values);
```

In `Apply`, take `"reckless"` out of the resistance group (`case "irongut": ... case "reckless": ApplyDamageModifier(boonId);`)
and give it its own case directly after that group:

```csharp
                case "reckless":
                    // Both halves (2026-10-08): the +50% was declared and never applied, so the boon was all cost.
                    ApplyDamageModifier(boonId);
                    ApplyWeaponMultiplier(RecklessDamageMultiplier, "reckless");
                    break;
```

In `Unapply`, take `"reckless"` out of its group the same way and add:

```csharp
                case "reckless":
                    UnapplyDamageModifier(boonId);
                    RemoveWeaponMultiplier(boonId);
                    break;
```

In `RefreshDamageModifiers`, in the Reckless/rage block, replace `HitData.DamageModifier.Weak` with
`HitData.DamageModifier.SlightlyWeak` (three places), and replace the comment's first sentence with:

```csharp
            // Reckless's cost, and Blood Rage's. "SlightlyWeak" is the game's x1.25 - the cards' "25% more"; it was
            // "Weak" (x1.5) until 2026-10-08, half again what the cards said.
```

The spec also asks the BOONS page to say when the ceiling is reached, so a fifth damage card does not look like a dud.
In `RefreshWeaponDamage`, before the cap, record the raw product:

```csharp
            float raw = 1f;
            foreach (var m in _weaponMultipliers.Values) raw *= m;
            WeaponCeilingReached = raw > WayRules.WeaponCeiling;
```

with `public bool WeaponCeilingReached { get; private set; }` beside `_weaponMultipliers`; set it false where
`UnapplyWeaponMultipliers` clears them. Expose it from `RunService` as
`public bool WeaponCeilingReached => _active && _boonEffects != null && _boonEffects.WeaponCeilingReached;`, and in
`RunWindow.DrawBoonsPage`, under the BOONS header:

```csharp
            if (_concrete != null && _concrete.WeaponCeilingReached)
            {
                GUI.contentColor = RunTheme.TextMuted;
                GUILayout.Label($"  your weapon bonuses are at their ceiling (x{WayRules.WeaponCeiling:0.#})", RunTheme.Small);
                GUI.contentColor = Color.white;
            }
```

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add ICanShowYouTheWorld/RunMode/WayRules.cs Tests/WayRulesTests.cs Tests/TestMain.cs ICanShowYouTheWorld/ICanShowYouTheWorld.csproj ICanShowYouTheWorld/RunMode/Unity/BoonEffects.cs ICanShowYouTheWorld/RunMode/Unity/RunService.cs ICanShowYouTheWorld/RunMode/Unity/RunWindow.cs
git commit -m "fix(run): Reckless gives its +50%, both costs are the cards' 25%, and weapon bonuses cap at x2.5"
```

---

### Task 2: Tempering, and Menagerie by biome

**Why:** spec, "Tempering" and "The Hunter" (Menagerie's roster grows by biome).

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/WayRules.cs`, `Tests/WayRulesTests.cs`
- Modify: `ICanShowYouTheWorld/RunMode/ClassLadder.cs` (`BossAtCount`), `Tests/ClassLadderTests.cs`
- Modify: `ICanShowYouTheWorld/RunMode/Unity/BoonEffects.cs` (`ActivateMenagerie` ~line 3600)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunWindow.cs` (`DrawBoonsPage` ~line 2409)

**Interfaces:**
- Produces: `public enum TemperSlot { Rung1 = 0, Rung2 = 1, Rung3 = 2, Engine = 3 }`;
  `WayRules.TemperGods` (`int[] { 3, 4, 5, 6 }`); `WayRules.Tempered(int gods, TemperSlot slot)`;
  `WayRules.TemperShown(int gods, TemperSlot slot)`; `WayRules.TemperLine(string classId, TemperSlot slot)`;
  `WayRules.MenagerieBeasts(int gods)` (`IList<string>`). `ClassLadder.AfterLine(6) == "after the Queen"`.

- [ ] **Step 1: Write the failing tests**

Append to `WayRulesTests.Run()`:

```csharp
        // Tempering: one per god from Bonemass to the Queen.
        Check.That(!WayRules.Tempered(2, TemperSlot.Rung1) && WayRules.Tempered(3, TemperSlot.Rung1) &&
                   !WayRules.Tempered(3, TemperSlot.Rung2) && WayRules.Tempered(4, TemperSlot.Rung2) &&
                   WayRules.Tempered(5, TemperSlot.Rung3) && !WayRules.Tempered(5, TemperSlot.Engine) &&
                   WayRules.Tempered(6, TemperSlot.Engine),
                   "Bonemass tempers rung I, Moder rung II, Yagluth rung III, the Queen the engine");
        Check.That(!WayRules.TemperShown(1, TemperSlot.Rung1) && WayRules.TemperShown(2, TemperSlot.Rung1) &&
                   !WayRules.TemperShown(2, TemperSlot.Rung2),
                   "a tempering is shown only once its god is the next to fall - no names from later acts");
        foreach (var way in ClassLadder.Catalog())
            foreach (TemperSlot slot in Enum.GetValues(typeof(TemperSlot)))
                Check.That(!string.IsNullOrEmpty(WayRules.TemperLine(way.Id, slot)), $"{way.Id} has a {slot} tempering line");
        Check.That(WayRules.TemperLine("nobody", TemperSlot.Rung1) == "", "an unknown way has none, not an error");

        // Menagerie's beasts by biome.
        Check.That(WayRules.MenagerieBeasts(0).SequenceEqual(new[] { "Boar", "Hen", "Chicken" }),
                   "the Meadows' beasts at first - no Lox to reroll for in Act II");
        Check.That(WayRules.MenagerieBeasts(3).Contains("Wolf") && !WayRules.MenagerieBeasts(3).Contains("Lox"),
                   "the wolf after Bonemass, when the Mountains open");
        Check.That(WayRules.MenagerieBeasts(4).Contains("Lox") && !WayRules.MenagerieBeasts(5).Contains("Asksvin") &&
                   WayRules.MenagerieBeasts(6).Contains("Asksvin"),
                   "the Lox after Moder, the Asksvin after the Queen");
```

Append to `ClassLadderTests.Run()`:

```csharp
        Check.That(ClassLadder.AfterLine(6) == "after the Queen", "the Queen is named for the engine's tempering");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors naming `TemperSlot`, `Tempered`, `MenagerieBeasts`.

- [ ] **Step 3: Implement**

Add to `WayRules` (and `using System.Linq;` is not needed):

```csharp
        /// <summary>Gods felled for each tempering: Bonemass, Moder, Yagluth, the Queen (spec, "Tempering").</summary>
        public static readonly int[] TemperGods = { 3, 4, 5, 6 };

        public static bool Tempered(int gods, TemperSlot slot) => gods >= TemperGods[(int)slot];

        /// <summary>Shown once its god is the next to fall: naming a later act's god would spoil it.</summary>
        public static bool TemperShown(int gods, TemperSlot slot) => gods >= TemperGods[(int)slot] - 1;

        private static readonly Dictionary<string, string[]> TemperLines = new Dictionary<string, string[]>
        {
            { "hunter",    new[] { "Packbrother calls three", "Thor's bow's element strikes half again as hard",
                                   "Unseen lasts 30 s", "your pack mends 2 a second" } },
            { "volva",     new[] { "Bonecaller raises three", "Mending every 60 s", "Thor's Wrath reaches 9 m",
                                   "Hearthlight mends up to 12 a second" } },
            { "berserker", new[] { "Rend reaches half again as far", "Blood Rage holds 25 s", "Warcry reaches 12 m",
                                   "Fury to +75% (fifteen hits)" } },
            { "huskarl",   new[] { "Shield Bash reaches 6 m", "Shield Wall stands 30 s", "Last Stand lasts 10 s",
                                   "Guard mends double" } },
            { "skald",     new[] { "the Marching Song: +30%", "the War Song: +35%", "Bragi's saga mends up to 9 a second",
                                   "two songs at once" } },
            { "saefari",   new[] { "Undertow reaches 9 m", "Stormcaller lasts 30 s", "Sea Legs lasts 10 min",
                                   "your Ward on land is as strong as at sea" } },
            { "smidr",     new[] { "two watch-posts at once", "the Field Forge stands 3 min", "the Master's Minute lasts 2 min",
                                   "Forge-skin: armour doubled" } },
        };

        /// <summary>What a tempering does, for the HUD's BOONS page. Empty for an unknown way.</summary>
        public static string TemperLine(string classId, TemperSlot slot) =>
            classId != null && TemperLines.TryGetValue(classId, out var lines) ? lines[(int)slot] : "";

        /// <summary>
        /// Menagerie's beasts by biome (spec, "The Hunter"): it rolled from all six from Act II, so a Hunter rerolled every
        /// 90 s until a Lox (1000 health) came.
        /// </summary>
        public static IList<string> MenagerieBeasts(int gods)
        {
            var beasts = new List<string> { "Boar", "Hen", "Chicken" };
            if (gods >= 3) beasts.Add("Wolf");
            if (gods >= 4) beasts.Add("Lox");
            if (gods >= 6) beasts.Add("Asksvin");
            return beasts;
        }
```

And, outside the class, in the same namespace:

```csharp
    /// <summary>What a god tempers: a way's three rungs, then its engine.</summary>
    public enum TemperSlot { Rung1 = 0, Rung2 = 1, Rung3 = 2, Engine = 3 }
```

In `ClassLadder.BossAtCount`, add `{ 6, "the Queen" }` after `{ 5, "Yagluth" }`.

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: Menagerie, game side**

In `BoonEffects.ActivateMenagerie`, replace

```csharp
            var available = MenagerieRoster.Where(n => scene.GetPrefab(n) != null).ToList();
```

with

```csharp
            // By biome (2026-10-08): the beasts the gods felled have opened, not all six from the start.
            var opened = WayRules.MenagerieBeasts(_defeatedBossCount());
            var available = MenagerieRoster.Where(n => opened.Contains(n) && scene.GetPrefab(n) != null).ToList();
```

Change the Menagerie card in `RunService.DefaultBoons()` to:
`Description = "Odin lends a beast — of the lands you have opened. Cast again to trade it back."`

- [ ] **Step 6: The tempering on the BOONS page**

In `RunWindow.DrawBoonsPage`, after `DrawWayKit(run, way);` add `DrawWayTempering(way);`, and add the method beside
`DrawWayKit`:

```csharp
        /// <summary>
        /// The way's tempering (2026-10-08): one per god from Bonemass to the Queen, shown once its god is the next to
        /// fall, "tempered" in the ready colour once it has.
        /// </summary>
        private void DrawWayTempering(ClassDefinition way)
        {
            int gods = _concrete != null ? _concrete.DefeatedBosses : 0;
            foreach (TemperSlot slot in Enum.GetValues(typeof(TemperSlot)))
            {
                if (!WayRules.TemperShown(gods, slot)) continue;
                bool done = WayRules.Tempered(gods, slot);
                GUI.contentColor = done ? RunTheme.CompleteGreen : RunTheme.TextMuted;
                string when = done ? "tempered" : ClassLadder.AfterLine(WayRules.TemperGods[(int)slot]);
                GUILayout.Label($"  {when}: {WayRules.TemperLine(way.Id, slot)}", RunTheme.Small);
            }
            GUI.contentColor = Color.white;
        }
```

- [ ] **Step 7: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 8: Commit**

```bash
git add ICanShowYouTheWorld/RunMode/WayRules.cs Tests/WayRulesTests.cs ICanShowYouTheWorld/RunMode/ClassLadder.cs Tests/ClassLadderTests.cs ICanShowYouTheWorld/RunMode/Unity/BoonEffects.cs ICanShowYouTheWorld/RunMode/Unity/RunWindow.cs ICanShowYouTheWorld/RunMode/Unity/RunService.cs
git commit -m "feat(run): tempering per god from Bonemass to the Queen, shown on the BOONS page; Menagerie's beasts by biome"
```

---

### Task 3: The engines' host, and the Hunter's tempering

**Why:** every engine ticks once a second; the Hunter's engine (the pack) exists, and gets its tempering here.

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/Unity/BoonEffects.cs` (class declaration line 43: `public class BoonEffects` →
  `public partial class BoonEffects`; `ActivateBrother`; the Unseen window `UnseenOnSeconds`)
- Create: `ICanShowYouTheWorld/RunMode/Unity/BoonEffects.Ways.cs`
- Modify: `ICanShowYouTheWorld/RunMode/Unity/SagaItems.cs` (`ApplyBowElement` ~line 1160)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs` (`PollCompanionPassives`)
- Modify: `ICanShowYouTheWorld/RunMode/WayRules.cs`, `Tests/WayRulesTests.cs`, the csproj

**Interfaces:**
- Consumes: `WayRules.Tempered`, `TemperSlot` (Task 2).
- Produces: `public void BoonEffects.TickWays()` (called once a second); `WayRules.PackSize(int gods)`,
  `WayRules.BowElementScale(int gods)`, `WayRules.UnseenSeconds(int gods)`, `WayRules.PackRegenPerSecond(int gods)`;
  `SagaItems.ElementScale` (static float, default 1).

- [ ] **Step 1: Write the failing tests**

Append to `WayRulesTests.Run()`:

```csharp
        // The Hunter's tempering.
        Check.That(WayRules.PackSize(2) == 2 && WayRules.PackSize(3) == 3, "two wolves at a time; three after Bonemass");
        Check.That(WayRules.BowElementScale(3) == 1f && WayRules.BowElementScale(4) == 1.5f, "the bow's element x1.5 after Moder");
        Check.That(WayRules.UnseenSeconds(4) == 20f && WayRules.UnseenSeconds(5) == 30f, "Unseen 20 s, 30 s after Yagluth");
        Check.That(WayRules.PackRegenPerSecond(5) == 0f && WayRules.PackRegenPerSecond(6) == 2f, "the pack mends 2 a second after the Queen");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors naming `PackSize`, `BowElementScale`, `UnseenSeconds`, `PackRegenPerSecond`.

- [ ] **Step 3: Implement the rules**

Add to `WayRules`:

```csharp
        // --- The Hunter ---
        public static int PackSize(int gods) => Tempered(gods, TemperSlot.Rung1) ? 3 : 2;
        public static float BowElementScale(int gods) => Tempered(gods, TemperSlot.Rung2) ? 1.5f : 1f;
        public static float UnseenSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 30f : 20f;
        public static float PackRegenPerSecond(int gods) => Tempered(gods, TemperSlot.Engine) ? 2f : 0f;
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The host**

Change line 43 of `BoonEffects.cs` to `public partial class BoonEffects`. Create
`ICanShowYouTheWorld/RunMode/Unity/BoonEffects.Ways.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The ways' always-on engines (2026-10-08, class balance; docs/superpowers/specs/2026-10-08-class-balance-design.md).
    /// Ticked once a second by RunService.PollCompanionPassives. Each engine keys on its way's PASSIVE boon id, so it
    /// runs exactly while the way is held, and Unapply of that id takes it down.
    /// </summary>
    public partial class BoonEffects
    {
        private bool Holds(string id) => _heldBoons().Any(h => h.Def.Id == id);

        /// <summary>Once a second while a run is live.</summary>
        public void TickWays()
        {
            int gods = _defeatedBossCount();
            try { TickPackRegen(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Pack regen: " + e.Message); }
        }

        /// <summary>The Hunter's engine tempered by the Queen: every animal on her side within 30 m mends.</summary>
        private void TickPackRegen(int gods)
        {
            float rate = WayRules.PackRegenPerSecond(gods);
            if (rate <= 0f || !Holds("shepherd")) return;
            var player = Player.m_localPlayer;
            if (player == null) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 30f, list);
            foreach (var c in list)
                if (c != null && !c.IsPlayer() && c.IsTamed() && !c.IsDead()) c.Heal(rate, false);
        }
    }
}
```

Add `<Compile Include="RunMode\Unity\BoonEffects.Ways.cs" />` to the csproj after `RunMode\Unity\BoonEffects.cs`.

In `RunService.PollCompanionPassives`, add after the Hearthlight line:

```csharp
            try { _boonEffects.TickWays(); }
            catch (Exception ex) { LogOnce("ways-tick", ex); }
```

- [ ] **Step 6: Packbrother's pack size**

In `BoonEffects`, add beside `_companions`:

```csharp
        /// <summary>Packbrother's wolves, oldest first: two at a time, three after Bonemass (WayRules.PackSize).</summary>
        private readonly List<ZDOID> _pack = new List<ZDOID>();
```

Replace `private bool ActivateBrother() => Summon(CompanionPrefab, 1, named: true);` with:

```csharp
        /// <summary>
        /// One wolf per call, and the pack is at most WayRules.PackSize: the card's "two at a time" was never held to
        /// (2026-10-08) - only the retinue cap of four was. The oldest wolf goes home for the new one.
        /// </summary>
        private bool ActivateBrother()
        {
            var man = ZDOMan.instance;
            _pack.RemoveAll(id => man == null || man.GetZDO(id) == null || !_companions.Contains(id));
            while (_pack.Count >= WayRules.PackSize(_defeatedBossCount()))
            {
                DespawnCompanion(_pack[0]);
                _pack.RemoveAt(0);
            }
            if (!Summon(CompanionPrefab, 1, named: true)) return false;
            _pack.Add(_companions[_companions.Count - 1]);
            return true;
        }
```

In `DespawnAllCompanions`, add `_pack.Clear();` beside `_menagerie = ZDOID.None;`.

- [ ] **Step 7: Unseen's tempering**

In `ActivateUnseen`, replace `SchedulePending("unseen", UnseenOnSeconds, ForceGhostOff);` with
`SchedulePending("unseen", WayRules.UnseenSeconds(_defeatedBossCount()), ForceGhostOff);`. Keep the `UnseenOnSeconds`
constant only if another line still reads it; otherwise delete it.

- [ ] **Step 8: Thor's bow's element scale**

In `SagaItems`, add:

```csharp
        /// <summary>The Hunter's Moder tempering (2026-10-08): the element x1.5. Set by RunService; 1 otherwise.</summary>
        public static float ElementScale = 1f;
```

In `ApplyBowElement`, multiply the first two locals:

```csharp
            float e = (lastLight ? LastLightElement : ThorsBowLightning) * ElementScale;
            float ep = (lastLight ? LastLightElementPerLevel : ThorsBowLightningPerLevel) * ElementScale;
```

In `RunService.PollCompanionPassives`, add:

```csharp
            // The Hunter's Moder tempering: Thor's bow's element x1.5, re-applied when it changes.
            try
            {
                float scale = _classId == "hunter" ? WayRules.BowElementScale(DefeatedBosses) : 1f;
                if (Math.Abs(SagaItems.ElementScale - scale) > 0.001f)
                {
                    SagaItems.ElementScale = scale;
                    _items.SetThorsBowElement(_items.ThorsBowElement);
                }
            }
            catch (Exception ex) { LogOnce("bow-scale", ex); }
```

In `RunService.EndRun`, after the shipwright restore, add `SagaItems.ElementScale = 1f;` so the bow ships unscaled.

- [ ] **Step 9: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 10: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode ICanShowYouTheWorld/ICanShowYouTheWorld.csproj Tests
git commit -m "feat(run): the ways' engines tick once a second; the Hunter's tempering - three wolves, the bow's element, Unseen, the pack mends"
```

---

### Task 4: The Völva — a real healing aura, and her dead first

**Why:** spec, "The Völva". Hearthlight was 4 health every 5 s (0.8/s).

**Files:**
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `ClassLadder.cs` (the Völva's `Rungs` and `Description`),
  `Tests/ClassLadderTests.cs`, `BoonEffects.cs` (`RefreshHearthlight` ~line 1340, `ActivateBonecaller`,
  `ActivateShamanHeal`, `ActivateWrath`), `RunService.cs` (`DefaultBoons`: bonecaller, hearthlight), `Thane.cs` (the
  Völva's choice line)

**Interfaces:**
- Produces: `WayRules.HearthlightPerSecond(int gods)`, `WayRules.BoneCount(int gods)`, `WayRules.MendingCooldown(int gods)`,
  `WayRules.WrathRadius(int gods)`.

- [ ] **Step 1: Write the failing tests**

Append to `WayRulesTests.Run()`:

```csharp
        // The Völva.
        Check.That(WayRules.HearthlightPerSecond(0) == 3f && WayRules.HearthlightPerSecond(2) == 5f &&
                   WayRules.HearthlightPerSecond(5) == 8f && WayRules.HearthlightPerSecond(9) == 8f,
                   "Hearthlight: 3 a second in the Meadows, +1 per god, to 8");
        Check.That(WayRules.HearthlightPerSecond(6) == 9f && WayRules.HearthlightPerSecond(20) == 12f,
                   "after the Queen its ceiling is 12");
        Check.That(WayRules.BoneCount(2) == 2 && WayRules.BoneCount(3) == 3, "two skeletons, three after Bonemass");
        Check.That(WayRules.MendingCooldown(3) == 90f && WayRules.MendingCooldown(4) == 60f, "Mending every 90 s, 60 after Moder");
        Check.That(WayRules.WrathRadius(4) == 6f && WayRules.WrathRadius(5) == 9f, "Thor's Wrath 6 m, 9 after Yagluth");
```

Append to `ClassLadderTests.Run()`:

```csharp
        Check.That(ClassLadder.Find("volva").Rungs[0].SequenceEqual(new[] { "bonecaller" }) &&
                   ClassLadder.Find("volva").Rungs[1].SequenceEqual(new[] { "shaman" }),
                   "the Völva's dead come first, Mending second (class balance, 2026-10-08)");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors for the new `WayRules` members, and the `FAIL` line for the Völva's rung order.

- [ ] **Step 3: Implement**

Add to `WayRules`:

```csharp
        // --- The Völva ---
        public static float HearthlightPerSecond(int gods) =>
            Math.Min(Tempered(gods, TemperSlot.Engine) ? 12f : 8f, 3f + Math.Max(0, gods));
        public static int BoneCount(int gods) => Tempered(gods, TemperSlot.Rung1) ? 3 : 2;
        public static float MendingCooldown(int gods) => Tempered(gods, TemperSlot.Rung2) ? 60f : 90f;
        public static float WrathRadius(int gods) => Tempered(gods, TemperSlot.Rung3) ? 9f : 6f;
```

In `ClassLadder`, the Völva:

```csharp
                Description = "A mending warmth follows you and everyone at your side. The dead rise at your word from " +
                              "the start; later you can pour the warmth out where you stand, and after that the sky " +
                              "answers where you point.",
                PassiveBoonIds = new[] { "hearthlight" },
                // Her dead first (class balance, 2026-10-08): the aura needs allies to mend from the start.
                Rungs = new[] { new[] { "bonecaller" }, new[] { "shaman" }, new[] { "wrath" } },
```

In `RunService.DefaultBoons()`: Bonecaller `CooldownSeconds = 120f`; Hearthlight
`Description = "A mending warmth: you and every ally within 15 m heal 3 a second, more with every god felled."`

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The game side**

Replace the body of `RefreshHearthlight(bool held)` with a once-a-second pulse (it is called every poll second since
`...08i`):

```csharp
        public void RefreshHearthlight(bool held)
        {
            if (!held || Time.time < _hearthlightAt) return;
            _hearthlightAt = Time.time + 1f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            // Every second, 3 a second in the Meadows, +1 per god (WayRules.HearthlightPerSecond); no number pops - it
            // would be one a second. Character.Heal routes to the owner, so tames mend from this client.
            float hp = WayRules.HearthlightPerSecond(_defeatedBossCount());
            try
            {
                player.Heal(hp, false);
                var list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 15f, list);
                foreach (var c in list)
                    if (c != null && !c.IsPlayer() && c.IsTamed() && !c.IsDead()) c.Heal(hp, false);
            }
            catch { /* a missed pulse is a missed pulse */ }
        }
```

`ActivateBonecaller`: `private bool ActivateBonecaller() => Summon(BonePrefab, WayRules.BoneCount(_defeatedBossCount()), named: false);`

`ActivateShamanHeal`: replace `held.CooldownRemaining = held.Def.CooldownSeconds;` with
`held.CooldownRemaining = WayRules.MendingCooldown(_defeatedBossCount());`

`ActivateWrath`: replace every read of the `WrathRadius` constant with `WayRules.WrathRadius(_defeatedBossCount())`, and
delete the constant.

In `Thane.cs`, the Völva's choice line (search `case "volva"` in the choice-line switch): replace the clause that orders
her kit with "The dead rise at your word from the first. Then the warmth you can pour out, and last, the sky."; keep
the rest of the line.

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the Völva - Hearthlight mends 3 a second to 8, her dead come first, and her tempering"
```

---

### Task 5: The Berserker — Fury, and Bloodied

**Why:** spec, "The Berserker". Hits are read from the game's own `EnemyHits` counter (research §1): it counts every
creature a hit lands on, sweeps per target, Rend's and the Ward's too (Review Focus 5). Damage dealt comes from
`Character.m_onDamaged`.

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/FuryMeter.cs`; test in `WayRulesTests.cs`
- Modify: `WayRules.cs`, `BoonEffects.Ways.cs`, `BoonEffects.cs` (`ActivateRage`, `EndRage`, `ActivateWarcry`,
  `ActivateRend`, Unapply `case "warrior"`), `RunService.cs` (`DefaultBoons`: warrior, rage, warcry), `ClassLadder.cs`
  (Berserker `Description`), the csproj

**Interfaces:**
- Produces: `public sealed class FuryMeter` with `int Stacks`, `int Max`, `float Bonus`, `bool Bloodied`,
  `void Hit(int n, float now)`, `void Tick(float now)`, `void Fill(float now, float holdUntil)`, `void AddHalf(float now)`,
  `void Release(float now)`, `void Reset()`, `void SetMax(int max)`. `WayRules.FuryMax(int gods)`, `WayRules.RageSeconds(int gods)`, `WayRules.RendRadius(int gods)`,
  `WayRules.WarcryRadius(int gods)`, `WayRules.BloodiedShare = 0.1f`.

- [ ] **Step 1: Write the failing tests**

Append to `WayRulesTests.Run()`:

```csharp
        // The Berserker's Fury.
        var fury = new FuryMeter();
        fury.SetMax(WayRules.FuryMax(0));
        fury.Hit(3, 0f);
        Check.That(fury.Stacks == 3 && Math.Abs(fury.Bonus - 0.15f) < 0.001f && !fury.Bloodied, "+5% a hit");
        fury.Hit(20, 0.5f);
        Check.That(fury.Stacks == 10 && Math.Abs(fury.Bonus - 0.5f) < 0.001f && fury.Bloodied, "to +50% at ten; Bloodied from five");
        fury.Tick(1.4f);
        Check.That(fury.Stacks == 10, "no fading within a second of the last hit");
        fury.Tick(1.6f); fury.Tick(2.6f);
        Check.That(fury.Stacks == 8, "then one hit's worth a second");
        fury.Fill(3f, 18f);
        fury.Tick(10f);
        Check.That(fury.Stacks == 10, "Blood Rage fills it and holds it");
        fury.Tick(19f);
        Check.That(fury.Stacks == 9, "and lets go when it ends");
        var cut = new FuryMeter(); cut.SetMax(10); cut.Fill(0f, 100f); cut.Release(5f);
        cut.Tick(5.5f);
        Check.That(cut.Stacks == 10, "a rage cut short lets go where it was cut");
        cut.Tick(6.5f);
        Check.That(cut.Stacks == 9, "and fades from there");
        var half = new FuryMeter(); half.SetMax(10); half.AddHalf(0f);
        Check.That(half.Stacks == 5, "Warcry fills half");
        Check.That(WayRules.FuryMax(5) == 10 && WayRules.FuryMax(6) == 15, "fifteen hits, +75%, after the Queen");
        Check.That(WayRules.RageSeconds(3) == 15f && WayRules.RageSeconds(4) == 25f, "Blood Rage 15 s, 25 after Moder");
        Check.That(WayRules.RendRadius(2) == 5f && WayRules.RendRadius(3) == 7.5f, "Rend 5 m, half again after Bonemass");
        Check.That(WayRules.WarcryRadius(4) == 8f && WayRules.WarcryRadius(5) == 12f, "Warcry 8 m, 12 after Yagluth");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors naming `FuryMeter` and the new `WayRules` members.

- [ ] **Step 3: Implement**

`ICanShowYouTheWorld/RunMode/FuryMeter.cs`:

```csharp
using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The Berserker's Fury (2026-10-08, class balance): +5% weapon damage per landed hit, to +50% at ten (fifteen
    /// after the Queen). One hit's worth fades per second once a second passes without one; Blood Rage fills it and
    /// holds it. Pure: BoonEffects feeds it the game's EnemyHits counter.
    /// </summary>
    public sealed class FuryMeter
    {
        public const float PerHit = 0.05f;
        public const int BloodiedAt = 5;
        public const float FadeAfter = 1f;

        private float _lastHit = float.NegativeInfinity;
        private float _lastFade;
        private float _holdUntil = float.NegativeInfinity;

        public int Stacks { get; private set; }
        public int Max { get; private set; } = 10;
        public float Bonus => Stacks * PerHit;
        public bool Bloodied => Stacks >= BloodiedAt;

        public void SetMax(int max)
        {
            Max = Math.Max(1, max);
            if (Stacks > Max) Stacks = Max;
        }

        public void Hit(int n, float now)
        {
            if (n <= 0) return;
            Stacks = Math.Min(Max, Stacks + n);
            _lastHit = now;
            _lastFade = now;
        }

        public void Tick(float now)
        {
            if (now < _holdUntil) { Stacks = Max; _lastHit = now; _lastFade = now; return; }
            // A hold that has ended counts as the last hit: fading starts a second after it, not from before it.
            if (_holdUntil > _lastHit) { _lastHit = _holdUntil; _lastFade = _holdUntil; }
            if (Stacks == 0 || now - _lastHit < FadeAfter) return;
            int fades = (int)Math.Floor(now - Math.Max(_lastFade, _lastHit + FadeAfter - 1f));
            if (fades <= 0) return;
            Stacks = Math.Max(0, Stacks - fades);
            _lastFade += fades;
        }

        public void Fill(float now, float holdUntil)
        {
            Stacks = Max;
            _holdUntil = holdUntil;
            _lastHit = now;
            _lastFade = now;
        }

        public void AddHalf(float now) => Hit((Max + 1) / 2, now);

        /// <summary>Blood Rage ends (its timer, or a death): the hold stops now, and the stacks fade from here.</summary>
        public void Release(float now)
        {
            if (_holdUntil > now) _holdUntil = now;
        }

        public void Reset()
        {
            Stacks = 0;
            _holdUntil = float.NegativeInfinity;
        }
    }
}
```

Add `<Compile Include="RunMode\FuryMeter.cs" />` to the csproj. Add to `WayRules`:

```csharp
        // --- The Berserker ---
        public const float BloodiedShare = 0.1f;
        public static int FuryMax(int gods) => Tempered(gods, TemperSlot.Engine) ? 15 : 10;
        public static float RageSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 25f : 15f;
        public static float RendRadius(int gods) => Tempered(gods, TemperSlot.Rung1) ? 7.5f : 5f;
        public static float WarcryRadius(int gods) => Tempered(gods, TemperSlot.Rung3) ? 12f : 8f;
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`. If the fade test fails, fix `FuryMeter.Tick`, not the test: two `Tick`s at 1.6 and 2.6 after a
hit at 0.5 must take exactly two stacks.

- [ ] **Step 5: The game side**

In `BoonEffects.Ways.cs`, add:

```csharp
        private readonly FuryMeter _fury = new FuryMeter();
        private int _lastEnemyHits = -1;
        private float _lastFuryBonus = -1f;

        /// <summary>The game's own landed-hit counter (PlayerStatType.EnemyHits): one per creature a hit lands on.</summary>
        private static int ReadEnemyHits()
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || profile.m_playerStats == null || profile.m_playerStats.Length == 0) return -1;
            return profile.m_playerStats[0].m_stats.TryGetValue(PlayerStatType.EnemyHits, out float v) ? (int)v : 0;
        }

        /// <summary>
        /// Fury, once a second while the Berserker's "warrior" is held: new landed hits since the last tick, the fade,
        /// and the weapon factor "fury" (inside the x2.5 ceiling). Bloodied heals through OnFoeDamaged.
        /// </summary>
        private void TickFury(int gods)
        {
            if (!Holds("warrior"))
            {
                if (_lastFuryBonus >= 0f) { RemoveWeaponMultiplier("fury"); _lastFuryBonus = -1f; }
                _fury.Reset();
                _lastEnemyHits = -1;
                return;
            }

            _fury.SetMax(WayRules.FuryMax(gods));
            int hits = ReadEnemyHits();
            if (hits >= 0)
            {
                if (_lastEnemyHits >= 0 && hits > _lastEnemyHits) _fury.Hit(hits - _lastEnemyHits, Time.time);
                _lastEnemyHits = hits;
            }
            _fury.Tick(Time.time);

            if (Math.Abs(_fury.Bonus - _lastFuryBonus) > 0.001f)
            {
                _lastFuryBonus = _fury.Bonus;
                if (_fury.Bonus > 0f) ApplyWeaponMultiplier(1f + _fury.Bonus, "fury");
                else RemoveWeaponMultiplier("fury");
            }

            SubscribeFoes();
        }

        /// <summary>m_onDamaged on every creature within 30 m, idempotently (-= then +=), for Bloodied.</summary>
        private void SubscribeFoes()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 30f, list);
            foreach (var c in list)
            {
                if (c == null || c.IsPlayer()) continue;
                c.m_onDamaged -= OnFoeDamaged;
                c.m_onDamaged += OnFoeDamaged;
            }
        }

        /// <summary>Bloodied: at five Fury or more, each hit the player lands heals 10% of the damage it dealt.</summary>
        private void OnFoeDamaged(float damage, Character attacker)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || !ReferenceEquals(attacker, player) || !_fury.Bloodied || !Holds("warrior")) return;
                player.Heal(damage * WayRules.BloodiedShare, false);
            }
            catch { /* a missed heal is a missed heal */ }
        }
```

and in `TickWays`, after the pack-regen line:

```csharp
            try { TickFury(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Fury: " + e.Message); }
```

In `BoonEffects.cs`:
- `ActivateRage`: delete the `_weaponMultipliers.ContainsKey("rage")` check and replace it with
  `if (IsWindowOpen("rage")) { LastActivationMessage = "The rage is already on you."; return false; }`; replace
  `ApplyWeaponMultiplier(RageMultiplier, "rage");` with
  `float until = Time.time + WayRules.RageSeconds(_defeatedBossCount()); _fury.Fill(Time.time, until);`, and replace
  `SchedulePending("rage", RageSeconds, EndRage);` with
  `SchedulePending("rage", WayRules.RageSeconds(_defeatedBossCount()), EndRage);`. Delete the `RageMultiplier` and
  `RageSeconds` constants.
- `EndRage`: replace `try { RemoveWeaponMultiplier("rage"); } finally { UnapplyDamageModifier("rage"); }` with
  `try { _fury.Release(Time.time); } finally { UnapplyDamageModifier("rage"); }` (the stacks then fade).
- `ActivateWarcry`: read `WayRules.WarcryRadius(_defeatedBossCount())` instead of `WarcryRadius`, delete the constant,
  and after the stagger loop add `if (Holds("warrior")) _fury.AddHalf(Time.time);`.
- `ActivateRend`: read `WayRules.RendRadius(_defeatedBossCount())` instead of `RendRadius`; delete the constant.
- `Unapply`: add `case "warrior": UnapplySkillBoon(boonId); RemoveWeaponMultiplier("fury"); _fury.Reset(); _lastFuryBonus = -1f; break;`
  — first check how `warrior` is unapplied today (it is in the skill-boon group); keep that call and add the Fury lines.

Cards in `RunService.DefaultBoons()`:
- warrior: `"Axe, sword and club skill to 50. Fury: every hit that lands +5% damage, to +50% at ten; from five, each hit heals a tenth of its damage."`
- rage: `"Fills your Fury and holds it full for 15 s. You take 25% more while it lasts."`
- warcry: `"Stagger every foe within eight metres, not the gods, and fill half your Fury."`

The Berserker's `Description` in `ClassLadder`: append `" Every blow you land feeds your fury, and a full fury feeds you."`

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode ICanShowYouTheWorld/ICanShowYouTheWorld.csproj Tests
git commit -m "feat(run): the Berserker's Fury and Bloodied, from the game's own hit counter; Blood Rage fills it, Warcry half"
```

---

### Task 6: The Húskarl — Guard

**Why:** spec, "The Húskarl". Blocks and parries have no counter; a status-effect subclass on the player is called by
the game on every block (`ModifyBlockStaminaUsage`) and on every parry attempt (`ModifyTimedBlockBonus`), and its
`OnDamaged` arms it first so the inventory tooltip's call is ignored (research §2).

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/Unity/WayEffects.cs`
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `BoonEffects.cs` (Apply/Unapply `case "hirdman"`, `ActivateBulwark`,
  `ActivateLastStand`, `ActivateBash`), `BoonEffects.Ways.cs`, `RunService.cs` (`DefaultBoons`: hirdman, bulwark,
  laststand; `LogSelfCheck`'s list), the csproj

**Interfaces:**
- Produces: `WayRules.GuardBlockHeal(int gods, bool wall)`, `WayRules.GuardParryHeal(int gods, bool wall)`,
  `WayRules.GuardStaminaRefund = 0.5f`, `WayRules.BashRadius(int gods)`, `WayRules.WallSeconds(int gods)`,
  `WayRules.LastStandSeconds(int gods)`; `internal sealed class GuardEffect : SE_Stats` with static
  `Action<bool> OnGuard` (true = parry).

- [ ] **Step 1: Write the failing tests**

```csharp
        // The Húskarl's Guard.
        Check.That(WayRules.GuardBlockHeal(0, false) == 4f && WayRules.GuardParryHeal(0, false) == 10f, "a block heals 4, a parry 10");
        Check.That(WayRules.GuardBlockHeal(0, true) == 8f && WayRules.GuardParryHeal(0, true) == 20f, "double behind Shield Wall");
        Check.That(WayRules.GuardBlockHeal(6, false) == 8f && WayRules.GuardBlockHeal(6, true) == 16f, "and double again after the Queen");
        Check.That(WayRules.GuardStaminaRefund == 0.5f, "half the block's stamina back");
        Check.That(WayRules.BashRadius(2) == 4f && WayRules.BashRadius(3) == 6f, "Shield Bash 4 m, 6 after Bonemass");
        Check.That(WayRules.WallSeconds(3) == 20f && WayRules.WallSeconds(4) == 30f, "Shield Wall 20 s, 30 after Moder");
        Check.That(WayRules.LastStandSeconds(4) == 6f && WayRules.LastStandSeconds(5) == 10f, "Last Stand 6 s, 10 after Yagluth");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors for the new members.

- [ ] **Step 3: Implement the rules**

```csharp
        // --- The Húskarl ---
        public const float GuardStaminaRefund = 0.5f;
        private static float GuardFactor(int gods, bool wall) => (wall ? 2f : 1f) * (Tempered(gods, TemperSlot.Engine) ? 2f : 1f);
        public static float GuardBlockHeal(int gods, bool wall) => 4f * GuardFactor(gods, wall);
        public static float GuardParryHeal(int gods, bool wall) => 10f * GuardFactor(gods, wall);
        public static float BashRadius(int gods) => Tempered(gods, TemperSlot.Rung1) ? 6f : 4f;
        public static float WallSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 30f : 20f;
        public static float LastStandSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 10f : 6f;
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The effect**

`ICanShowYouTheWorld/RunMode/Unity/WayEffects.cs`:

```csharp
using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The Húskarl's Guard (2026-10-08). The game calls a status effect's ModifyBlockStaminaUsage once per block
    /// (Humanoid.BlockAttack) and ModifyTimedBlockBonus only on a parry attempt; OnDamaged comes first in the same
    /// RPC_Damage, so arming there filters out the inventory tooltip's call. m_blockStaminaUseModifier = -0.5 is the
    /// half-stamina refund, through the game's own SE_Stats arithmetic.
    /// </summary>
    internal sealed class GuardEffect : SE_Stats
    {
        /// <summary>Raised once per block: true for a parry, false for a plain block. Set by BoonEffects.</summary>
        public static Action<bool> OnGuard;

        private bool _armed;
        private bool _parry;

        public override void OnDamaged(HitData hit, Character attacker)
        {
            base.OnDamaged(hit, attacker);
            _armed = true;
            _parry = false;
        }

        public override void ModifyTimedBlockBonus(ref float timedBlockBonus)
        {
            base.ModifyTimedBlockBonus(ref timedBlockBonus);
            if (_armed) _parry = true;
        }

        public override void ModifyBlockStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            base.ModifyBlockStaminaUsage(baseStaminaUse, ref staminaUse);
            if (!_armed) return;
            _armed = false;
            try { OnGuard?.Invoke(_parry); }
            catch { /* a missed heal is a missed heal */ }
        }
    }
}
```

Add `<Compile Include="RunMode\Unity\WayEffects.cs" />` to the csproj.

In `BoonEffects.Ways.cs`:

```csharp
        private const string GuardName = "ICSYTW_Guard";
        private static readonly int GuardHash = GuardName.GetStableHashCode();
        private static GuardEffect _guard;

        private static GuardEffect Guard()
        {
            if (_guard != null) return _guard;
            var se = ScriptableObject.CreateInstance<GuardEffect>();
            se.name = GuardName;
            se.m_name = "Guard";
            se.m_tooltip = "What lands on your shield comes back to you.";
            se.m_ttl = 0f;
            se.m_blockStaminaUseModifier = -WayRules.GuardStaminaRefund;
            _guard = se;
            return se;
        }

        private static bool _guardLogged;

        /// <summary>The Húskarl's passive: lays Guard on the player (idempotent; re-run on respawn).</summary>
        private void ApplyGuard()
        {
            var seman = Player.m_localPlayer?.GetSEMan();
            if (seman == null || seman.HaveStatusEffect(GuardHash)) return;
            GuardEffect.OnGuard = OnGuard;
            seman.AddStatusEffect(Guard());
            if (!_guardLogged)
            {
                _guardLogged = true;
                // The first launch's proof that a mod-defined StatusEffect subclass works (research: likely, not proven).
                Debug.Log($"[ICanShowYouTheWorld] Guard on: SE '{GuardName}' ({seman.HaveStatusEffect(GuardHash)}).");
            }
        }

        private void UnapplyGuard()
        {
            Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(GuardHash, quiet: true);
        }

        private void OnGuard(bool parry)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            int gods = _defeatedBossCount();
            bool wall = IsWindowOpen("bulwark");
            player.Heal(parry ? WayRules.GuardParryHeal(gods, wall) : WayRules.GuardBlockHeal(gods, wall), false);
        }
```

In `BoonEffects.cs`:
- Apply `case "hirdman"`: after `ApplyFieldBoost(boonId);` add `ApplyGuard();`.
- Unapply: add `UnapplyGuard();` to `hirdman`'s unapply (find its case; if it falls to `default`, give it a case that
  does what `default` does for it plus `UnapplyGuard();`).
- `ActivateBash`: `WayRules.BashRadius(_defeatedBossCount())` instead of `BashRadius`; delete the constant.
- `ActivateBulwark`: `WayRules.WallSeconds(_defeatedBossCount())` instead of `BulwarkSeconds` in its `SchedulePending`.
- `ActivateLastStand`: `WayRules.LastStandSeconds(_defeatedBossCount())` instead of `LastStandSeconds`.

Cards: hirdman `"Blocking and spear skill to 50, +20 max health. Guard: a block costs half the stamina and heals 4, a parry heals 10."`;
bulwark `"Twenty seconds of a wall: blows of every kind land softer, and Guard heals double."`.

Self-check: in `RunService`, beside `CheckSea()`'s call in the self-check block, add a line that reports whether the
Guard effect can be made: `_selfCheck.Ok("Way effects", "a mod StatusEffect can be made")` when
`ScriptableObject.CreateInstance<GuardEffect>() != null`, else `_selfCheck.Missing("Way effects", "Guard will not work")`.

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode ICanShowYouTheWorld/ICanShowYouTheWorld.csproj Tests
git commit -m "feat(run): the Húskarl's Guard - blocks cost half and heal 4, parries heal 10, double behind the Wall"
```

---

### Task 7: The Skald — the standing song

**Why:** spec, "The Skald". One song always playing, chosen with the rung keys; switching plays a 5 s crescendo at
most every 20 s. Allies get War Song through a per-creature `SE_Stats` (`m_modifyAttackSkill = All`,
`m_damageModifier`), which the game applies to tames' attacks (research §3); the player's War Song stays in the
weapon product, inside the ceiling.

**Files:**
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `BoonEffects.cs` (`ActivateMarch`/`EndMarch`, `ActivateWarsong`/`EndWarsong`,
  `ActivateBragi`; Unapply), `BoonEffects.Ways.cs`, `RunService.cs` (`DefaultBoons`: march, warsong, bragi with
  `CooldownSeconds = 0f`), `ClassLadder.cs` (Skald `Description`)

**Interfaces:**
- Produces: `public enum SkaldSong { None, March, War, Bragi }`; `WayRules.MarchSpeed(int gods)`,
  `WayRules.WarDamage(int gods)`, `WayRules.BragiPerSecond(int gods)`, `WayRules.CrescendoSeconds = 5f`,
  `WayRules.CrescendoEvery = 20f`, `WayRules.CrescendoDue(float now, float last)`, `WayRules.SongsAtOnce(int gods)`.

- [ ] **Step 1: Write the failing tests**

```csharp
        // The Skald's songs.
        Check.That(WayRules.MarchSpeed(2) == 0.2f && WayRules.MarchSpeed(3) == 0.3f, "the Marching Song +20%, +30% after Bonemass");
        Check.That(WayRules.WarDamage(3) == 1.25f && WayRules.WarDamage(4) == 1.35f, "the War Song +25%, +35% after Moder");
        Check.That(WayRules.BragiPerSecond(0) == 3f && WayRules.BragiPerSecond(4) == 6f && WayRules.BragiPerSecond(5) == 8f &&
                   WayRules.BragiPerSecond(9) == 9f,
                   "Bragi mends 3 a second, +1 per god, to 6 - to 9 after Yagluth");
        Check.That(WayRules.CrescendoDue(20f, 0f) && !WayRules.CrescendoDue(19.9f, 0f) && WayRules.CrescendoDue(0f, float.NegativeInfinity),
                   "a crescendo at most every twenty seconds; the first is free");
        Check.That(WayRules.SongsAtOnce(5) == 1 && WayRules.SongsAtOnce(6) == 2, "two songs at once after the Queen");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors for the new members.

- [ ] **Step 3: Implement the rules**

```csharp
        // --- The Skald ---
        public const float CrescendoSeconds = 5f;
        public const float CrescendoEvery = 20f;
        public static float MarchSpeed(int gods) => Tempered(gods, TemperSlot.Rung1) ? 0.3f : 0.2f;
        public static float WarDamage(int gods) => Tempered(gods, TemperSlot.Rung2) ? 1.35f : 1.25f;
        public static float BragiPerSecond(int gods) =>
            Math.Min(Tempered(gods, TemperSlot.Rung3) ? 9f : 6f, 3f + Math.Max(0, gods));
        public static bool CrescendoDue(float now, float last) => now - last >= CrescendoEvery;
        public static int SongsAtOnce(int gods) => Tempered(gods, TemperSlot.Engine) ? 2 : 1;
```

and, outside the class:

```csharp
    /// <summary>The Skald's standing songs, one per rung.</summary>
    public enum SkaldSong { None, March, War, Bragi }
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The game side**

In `BoonEffects.Ways.cs`:

```csharp
        private SkaldSong _song = SkaldSong.None;
        private SkaldSong _secondSong = SkaldSong.None;
        private float _crescendoAt = float.NegativeInfinity;
        private float _crescendoUntil = float.NegativeInfinity;

        private const string WarSongAllyName = "ICSYTW_WarSongAlly";
        private static readonly int WarSongAllyHash = WarSongAllyName.GetStableHashCode();

        /// <summary>A rung key: switch to that song; a crescendo if one is due.</summary>
        private bool SwitchSong(SkaldSong song)
        {
            if (_song == song) { LastActivationMessage = "That song is already sung."; return false; }
            _secondSong = _song;
            _song = song;
            if (WayRules.CrescendoDue(Time.time, _crescendoAt))
            {
                _crescendoAt = Time.time;
                _crescendoUntil = Time.time + WayRules.CrescendoSeconds;
                LastActivationMessage = "The song swells.";
            }
            ApplySongs();
            return true;
        }

        private bool Sung(SkaldSong s, int gods) => _song == s || (WayRules.SongsAtOnce(gods) > 1 && _secondSong == s);

        /// <summary>The standing songs' player halves, as loans (the existing lenders "march" and "warsong").</summary>
        private void ApplySongs()
        {
            int gods = _defeatedBossCount();
            float boost = Time.time < _crescendoUntil ? 2f : 1f;
            var player = Player.m_localPlayer;
            if (player == null) return;

            RepayLender("march");
            if (Sung(SkaldSong.March, gods))
            {
                SyncLoanOwner(player);
                LendFieldFraction(player, "RunSpeed", "march", WayRules.MarchSpeed(gods) * boost);
                LendFieldFraction(player, "WalkSpeed", "march", WayRules.MarchSpeed(gods) * boost);
                LendField(player, "StaminaRegen", "march", MarchStaminaRegen * boost);
            }

            if (Sung(SkaldSong.War, gods)) ApplyWeaponMultiplier(1f + (WayRules.WarDamage(gods) - 1f) * boost, "warsong");
            else RemoveWeaponMultiplier("warsong");
        }

        /// <summary>Once a second: the allies' halves, Bragi's mending, and the crescendo running out.</summary>
        private void TickSongs(int gods)
        {
            if (!Holds("poet")) { if (_song != SkaldSong.None) EndSongs(); return; }
            if (_song == SkaldSong.None) return;
            if (_crescendoUntil > 0f && Time.time >= _crescendoUntil) { _crescendoUntil = float.NegativeInfinity; ApplySongs(); }

            var player = Player.m_localPlayer;
            if (player == null) return;
            float boost = Time.time < _crescendoUntil ? 2f : 1f;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 15f, list);

            bool bragi = Sung(SkaldSong.Bragi, gods);
            bool war = Sung(SkaldSong.War, gods);
            float heal = WayRules.BragiPerSecond(gods) * boost;
            if (bragi) player.Heal(heal, false);
            foreach (var c in list)
            {
                if (c == null || c.IsPlayer() || !c.IsTamed() || c.IsDead()) continue;
                if (bragi) c.Heal(heal, false);
                if (war) LayWarSong(c, 1f + (WayRules.WarDamage(gods) - 1f) * boost);
            }
        }

        /// <summary>The War Song on one ally: a 3 s SE_Stats, re-laid each second while in reach (per creature).</summary>
        private static void LayWarSong(Character c, float factor)
        {
            var view = c.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            if (!view.IsOwner()) view.ClaimOwnership();
            var seman = c.GetSEMan();
            if (seman == null) return;
            seman.RemoveStatusEffect(WarSongAllyHash, quiet: true);
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = WarSongAllyName;
            se.m_name = "War Song";
            se.m_ttl = 3f;
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = factor;
            seman.AddStatusEffect(se);
        }

        private void EndSongs()
        {
            _song = _secondSong = SkaldSong.None;
            _crescendoUntil = float.NegativeInfinity;
            RepayLender("march");
            RemoveWeaponMultiplier("warsong");
        }
```

and in `TickWays`: `try { TickSongs(gods); } catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Songs: " + e.Message); }`.

In `BoonEffects.cs`:
- `Activate`: `case "march": return SwitchSong(SkaldSong.March);`, `case "warsong": return SwitchSong(SkaldSong.War);`,
  `case "bragi": return SwitchSong(SkaldSong.Bragi);`. Delete `ActivateMarch`, `ActivateWarsong`, `ActivateBragi`,
  `EndMarch`, `EndWarsong`, the `MarchSeconds`, `MarchSpeedFraction`, `WarsongMultiplier`, `WarsongSeconds` and
  `BragiHealFraction` constants (keep `MarchStaminaRegen`).
- `Unapply`: `case "march": case "warsong": case "bragi": EndSongs(); break;` (replacing the `EndMarch`/`EndWarsong` cases),
  and `case "poet":` add `EndSongs();` to whatever it does today.

Cards, `CooldownSeconds = 0f` on all three:
- march `"Sing it and it stays: +20% speed, and breath that comes back half again as fast. Switching songs: a crescendo, every 20 s."`
- warsong `"Sing it and it stays: +25% damage for you and every ally within 15 m."`
- bragi `"Sing it and it stays: you and every ally within 15 m mend 3 a second, more with every god felled."`

The Skald's `Description` in `ClassLadder`: "You sing, and the song stays: first a marching song, later a war song for you
and yours, and after that Bragi's own saga, which mends all who hear it. Switch songs and it swells. His flask is yours
the moment you take his name."

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.` If `LendFieldFraction` or `MarchStaminaRegen` have other names, use
the existing ones the deleted `ActivateMarch` used.

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the Skald's songs stand - one always sung, for him and his allies, with a crescendo on the switch"
```

---

### Task 8: The Sæfari at sea — half-price fittings, a tier above, double coins, the water never tires her

**Why:** spec, "The Sæfari" (passive). Tide-borne's +8 regen did nothing in water (research §7); an `SE_Stats` with
`m_swimStaminaUseModifier = -1` zeroes the drain.

**Files:**
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `ShipFittings.cs` (`Offers` gains `bool halfPrice = false`),
  `Tests/ShipFittingsTests.cs`, `BoonEffects.Ways.cs`, `BoonEffects.cs` (Apply/Unapply `case "seafarer"`),
  `RunService.cs` (every `ShipFittings.Offers(` call, `BuyFitting`'s price, `SeaSettingsNow`, the ship's Sail/Hull reads,
  the coin drop at ~line 9351; `DefaultBoons`: seafarer)

**Interfaces:**
- Produces: `WayRules.SaefariPrice(int price)`, `WayRules.SeaWardTier(int bought)`, `WayRules.SeaShipTier(int bought)`,
  `WayRules.SaefariCoinFactor = 2`; `ShipFittings.Offers(ShipFittingState, bool fireTarTold, bool wardTold = false, bool halfPrice = false)`.

- [ ] **Step 1: Write the failing tests**

In `WayRulesTests.Run()`:

```csharp
        // The Sæfari at sea.
        Check.That(WayRules.SaefariPrice(50) == 25 && WayRules.SaefariPrice(200) == 100 && WayRules.SaefariPrice(450) == 225 &&
                   WayRules.SaefariPrice(25) == 13,
                   "half price, rounded up");
        Check.That(WayRules.SeaShipTier(0) == 1 && WayRules.SeaShipTier(2) == 3 && WayRules.SeaShipTier(3) == 3,
                   "her ship sails a tier above what is fitted, to III");
        Check.That(WayRules.SaefariCoinFactor == 2, "the sea pays her double");
```

In `ShipFittingsTests.Run()`:

```csharp
        Check.That(ShipFittings.Offers(new ShipFittingState(), false, true, halfPrice: true).Select(o => o.Price)
                       .SequenceEqual(new[] { 25, 25, 100, 50 }),
                   "a Sæfari pays half: Sail I 25, Hull I 25, the Wind-horn 100, Ward I 50");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors for the new members and the `halfPrice` parameter.

- [ ] **Step 3: Implement**

`WayRules`:

```csharp
        // --- The Sæfari ---
        public const int SaefariCoinFactor = 2;
        public static int SaefariPrice(int price) => (price + 1) / 2;
        public static int SeaShipTier(int bought) => Math.Min(3, Math.Max(0, bought) + 1);
        public static int SeaWardTier(int bought) => SeaShipTier(bought);
```

`ShipFittings.Offers`: add the parameter `bool halfPrice = false` and, before `return offers;`:

```csharp
            if (halfPrice)
                foreach (var o in offers) o.Price = WayRules.SaefariPrice(o.Price);
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The game side**

In `RunService`, add `private bool IsSaefari => _active && _classId == "saefari" && HoldsBoon("seafarer");` and:
- pass `halfPrice: IsSaefari` to every `ShipFittings.Offers(` call (two today, ~lines 6990 and 7032);
- where the ship's sail and hull are applied (search `SailMultiplier(` and `HullDamageFactor(` in `RunService` and
  `Shipwright.cs`), use an effective state: add
  `private ShipFittingState EffectiveFittings() => IsSaefari ? new ShipFittingState { Sail = WayRules.SeaShipTier(_fittings.Sail), Hull = WayRules.SeaShipTier(_fittings.Hull), FireTar = _fittings.FireTar, WindHorn = _fittings.WindHorn, Ward = WayRules.SeaWardTier(_fittings.Ward) } : _fittings;`
  and pass `EffectiveFittings()` where `_fittings` is handed to the shipwright for its numbers (not where it is bought
  or saved);
- `SeaSettingsNow`: `WardTier = IsSaefari ? WayRules.SeaWardTier(_fittings.Ward) : _fittings.Ward,`;
- the coin drop: `coins += RunCoins.SeaCoins(seaCreature, seaLevel, _cfg.RunSeaCoinMultiplier * (IsSaefari ? WayRules.SaefariCoinFactor : 1));`
  (check `SeaCoins`'s multiplier type; cast if it is an int).

In `BoonEffects.Ways.cs`:

```csharp
        private const string TideName = "ICSYTW_TideBorne";
        private static readonly int TideHash = TideName.GetStableHashCode();

        /// <summary>The Sæfari's passive: the water never tires her (the game's swim-stamina modifier, -100%).</summary>
        private void ApplyTideBorne()
        {
            var seman = Player.m_localPlayer?.GetSEMan();
            if (seman == null || seman.HaveStatusEffect(TideHash)) return;
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = TideName;
            se.m_name = "Tide-borne";
            se.m_tooltip = "The water never tires you.";
            se.m_ttl = 0f;
            se.m_swimStaminaUseModifier = -1f;
            seman.AddStatusEffect(se);
        }

        private void UnapplyTideBorne() => Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(TideHash, quiet: true);
```

`Apply case "seafarer"`: after `ApplySkillBoon(boonId);` add `ApplyTideBorne();` (give it its own case, out of the skill
group, if it is grouped). `Unapply`: add `UnapplyTideBorne();` to `seafarer`'s unapply.

Card seafarer: `"Swim skill to 60, spear skill to 50. The water never tires you; fittings cost you half, your ship sails a tier above them, and the sea pays you double."`

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the Sæfari at sea - half-price fittings, her ship a tier above, the sea pays double, the water never tires her"
```

---

### Task 9: The Sæfari on foot — her Ward on land, Undertow, Stormcaller

**Why:** spec, "The Sæfari" (rungs; the Ward on land one tier weaker, never below I). Undertow is one HitData per foe
with push, a forced stagger and Wet (research §5); the Ward's pulse gains a centre fallback (research §6).

**Files:**
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `ClassLadder.cs` (Sæfari `Rungs`, `Description`), `Tests/ClassLadderTests.cs`,
  `SeaWatch.cs` (`SeaSettings`, `Pulse`), `BoonEffects.cs` (Activate/Unapply cases; delete Tide/Fair Wind),
  `BoonEffects.Ways.cs`, `RunService.cs` (`DefaultBoons`: remove `tide`, `fairwind`; add `undertow`, `stormcaller`;
  `SeaSettingsNow`), `ShipFittings.cs` (`Summary`'s "aboard" wording), `Thane.cs` (Ragna's choice line)

**Interfaces:**
- Produces: `WayRules.LandWardTier(int bought, int gods)`, `WayRules.UndertowRadius(int gods)`,
  `WayRules.StormcallerSeconds(int gods)`, `WayRules.SeaLegsSeconds(int gods)`; `SeaSettings.WardOnFoot`,
  `SeaSettings.LandWardTier`, `SeaSettings.StormUntil` (float, `Time.time` it ends).

- [ ] **Step 1: Write the failing tests**

`WayRulesTests.Run()`:

```csharp
        // The Sæfari on foot.
        Check.That(WayRules.LandWardTier(0, 0) == 1 && WayRules.LandWardTier(1, 0) == 1 && WayRules.LandWardTier(2, 0) == 2 &&
                   WayRules.LandWardTier(3, 0) == 2,
                   "on land her Ward is one tier weaker than at sea, never below I");
        Check.That(WayRules.LandWardTier(3, 6) == 3, "after the Queen it is as strong as at sea");
        Check.That(WayRules.UndertowRadius(2) == 6f && WayRules.UndertowRadius(3) == 9f, "Undertow 6 m, 9 after Bonemass");
        Check.That(WayRules.StormcallerSeconds(3) == 20f && WayRules.StormcallerSeconds(4) == 30f, "Stormcaller 20 s, 30 after Moder");
        Check.That(WayRules.SeaLegsSeconds(4) == 300f && WayRules.SeaLegsSeconds(5) == 600f, "Sea Legs 5 min, 10 after Yagluth");
```

`ClassLadderTests.Run()`:

```csharp
        Check.That(ClassLadder.Find("saefari").Rungs[0].SequenceEqual(new[] { "undertow" }) &&
                   ClassLadder.Find("saefari").Rungs[1].SequenceEqual(new[] { "stormcaller" }),
                   "the Sæfari's rungs: Undertow, then Stormcaller (Tide-borne is her passive now, Fair Wind the horn's)");
        Check.That(ClassLadder.Due(ClassLadder.Find("saefari"), 1, new[] { "seafarer", "tide", "fairwind" })
                       .SequenceEqual(new[] { "undertow", "stormcaller" }),
                   "a save holding the old rungs is taught the new ones on resume");
```

Add `undertow` and `stormcaller` (ClassId `saefari`) to `ClassLadderTests.Pool()` in place of `tide` and `fairwind`.

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors for the new members; `FAIL` for the rung order.

- [ ] **Step 3: Implement the rules and the table**

`WayRules`:

```csharp
        public static int LandWardTier(int bought, int gods) =>
            Tempered(gods, TemperSlot.Engine) ? SeaWardTier(bought) : Math.Max(1, SeaWardTier(bought) - 1);
        public static float UndertowRadius(int gods) => Tempered(gods, TemperSlot.Rung1) ? 9f : 6f;
        public static float StormcallerSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 30f : 20f;
        public static float SeaLegsSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 600f : 300f;
```

`ClassLadder`, the Sæfari:

```csharp
                Description = "Water is yours, and your ward goes with you onto land. A wave you can throw that breaks " +
                              "what stands near; later a storm in your ward; after that sea-legs that no cold or wet can " +
                              "touch. Fittings cost you half. Her harpoon is yours the moment you take her name.",
                Rungs = new[] { new[] { "undertow" }, new[] { "stormcaller" }, new[] { "sealegs" } },
```

`RunService.DefaultBoons()`: delete the `tide` and `fairwind` lines; add

```csharp
            new BoonDefinition { Id = "undertow", ClassId = "saefari", Display = "Undertow", IsPassive = false, CooldownSeconds = 20f, Description = "A wave bursts from you: everything within six metres is staggered, thrown back and soaked." },
            new BoonDefinition { Id = "stormcaller", ClassId = "saefari", Display = "Stormcaller", IsPassive = false, CooldownSeconds = 120f, Description = "Twenty seconds in which your Ward strikes every second, twice as far." },
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The Ward on foot and Stormcaller**

`SeaWatch.SeaSettings` gains `public bool WardOnFoot; public int LandWardTier; public float StormUntil;`. In `Pulse`:

```csharp
            var ship = Ship.GetLocalShip();
            bool storm = Time.time < s.StormUntil;
            int tier = ship != null ? s.WardTier : (s.WardOnFoot ? s.LandWardTier : 0);
            if (tier < 1 || Time.time < _nextPulse) return;
            Vector3 centre = ship != null ? ship.transform.position : player.transform.position;
            _nextPulse = Time.time + (storm ? 1f : ShipFittings.WardPulseSeconds);

            float damage = ShipFittings.WardDamage(tier);
            _inRange.Clear();
            Character.GetCharactersInRange(centre, ShipFittings.WardRadius(tier) * (storm ? 2f : 1f), _inRange);
```

(replacing the old `WardTier < 1`/ship-null early return and the centre/radius lines; the strike loop is unchanged).

`RunService.SeaSettingsNow`: add
`WardOnFoot = IsSaefari, LandWardTier = IsSaefari ? WayRules.LandWardTier(_fittings.Ward, DefeatedBosses) : 0, StormUntil = _boonEffects.StormUntil,`.

In `BoonEffects.Ways.cs`:

```csharp
        /// <summary>When Stormcaller's storm ends (Time.time); SeaWatch reads it through RunService.</summary>
        public float StormUntil { get; private set; } = float.NegativeInfinity;

        private bool ActivateStormcaller()
        {
            var held = FindHeld("stormcaller");
            if (held == null || held.CooldownRemaining > 0f) return false;
            StormUntil = Time.time + WayRules.StormcallerSeconds(_defeatedBossCount());
            held.CooldownRemaining = held.Def.CooldownSeconds;
            LastActivationMessage = "The storm gathers in your ward.";
            return true;
        }

        /// <summary>Undertow: one HitData per foe - a push, a forced stagger (multiplier 100) and Wet - like Rend's path.</summary>
        private bool ActivateUndertow()
        {
            var held = FindHeld("undertow");
            if (held == null || held.CooldownRemaining > 0f) return false;
            var player = Player.m_localPlayer;
            if (player == null) return false;

            var foes = HostilesNear(player.transform.position, WayRules.UndertowRadius(_defeatedBossCount()), player, skipBosses: true);
            if (foes.Count == 0) { LastActivationMessage = "Nothing within the wave's reach."; return false; }

            Vector3 from = player.transform.position;
            foreach (var c in foes)
            {
                var hit = new HitData();
                hit.m_damage.m_blunt = 5f * ClassDamageScale();
                hit.m_pushForce = 60f;
                hit.m_staggerMultiplier = 100f;
                hit.m_statusEffectHash = SEMan.s_statusEffectWet;
                AimHit(hit, c, from, player);
                DamageOne(c, hit, "Undertow");
            }
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }
```

`BoonEffects.Activate`: replace `case "tide"` and `case "fairwind"` with `case "undertow": return ActivateUndertow();` and
`case "stormcaller": return ActivateStormcaller();`. `Unapply`: replace `case "tide"` and `case "fairwind"` with
`case "stormcaller": StormUntil = float.NegativeInfinity; break;`. Delete `ActivateTide`, `EndTide`, `ActivateFairWind`,
`EndFairWind` and their constants. `ActivateSeaLegs`: `WayRules.SeaLegsSeconds(_defeatedBossCount())` instead of
`SeaLegsSeconds`.

`ShipFittings.Offers`, the Ward's effect line: `"lightning strikes attackers within {r} m every {s} s"` (drop "aboard:" —
for a Sæfari it walks with her; for anyone else the card's "attackers" is still true aboard).

`Thane.cs`, Ragna's choice line (search `case "saefari"`): replace the kit clause with "The water is yours and your ward
walks with you; a wave you throw, a storm you call."

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.` `ClassLadder.Validate` must report nothing at run start: grep
`Validate(` problems in the next `Player.log`.

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the Sæfari on foot - her Ward walks with her, Undertow, Stormcaller; Tide-borne and Fair Wind retire"
```

---

### Task 10: The Smiðr — Forge-skin, the walls, the Watch-post

**Why:** spec, "The Smiðr". Armour x1.5 is the game's `SE_Stats.m_armorMultiplier = 0.5` (player-only, research §3).
The ballista is `piece_turret`, loaded with infinite ammo and kept off players and tames (research §4).

**Files:**
- Modify: `WayRules.cs`, `WayRulesTests.cs`, `ClassLadder.cs` (Smiðr `Rungs`, `Description`), `Tests/ClassLadderTests.cs`,
  `BoonEffects.cs` (Apply/Unapply `craftsman`; Activate; delete Reinforce's activation, keep its restore),
  `BoonEffects.Ways.cs`, `RunService.cs` (`DefaultBoons`: remove `reinforce`, add `watchpost`; craftsman's card;
  self-check names), `Thane.cs` (Dvalinn's choice line)

**Interfaces:**
- Produces: `WayRules.ArmourFactor(int gods)`, `WayRules.WatchPosts(int gods)`, `WayRules.WatchPostBolt(int gods)`,
  `WayRules.FieldForgeSeconds(int gods)`, `WayRules.MastersMinuteSeconds(int gods)`.

- [ ] **Step 1: Write the failing tests**

`WayRulesTests.Run()`:

```csharp
        // The Smiðr.
        Check.That(WayRules.ArmourFactor(5) == 1.5f && WayRules.ArmourFactor(6) == 2f, "armour half again, doubled after the Queen");
        Check.That(WayRules.WatchPosts(2) == 1 && WayRules.WatchPosts(3) == 2, "one watch-post, two after Bonemass");
        Check.That(WayRules.WatchPostBolt(2) == "TurretBoltWood" && WayRules.WatchPostBolt(3) == "TurretBolt" &&
                   WayRules.WatchPostBolt(6) == "TurretBoltFlametal",
                   "wooden missiles, black metal from Bonemass, flametal from the Queen (the ballista's own bolts)");
        Check.That(WayRules.FieldForgeSeconds(3) == 90f && WayRules.FieldForgeSeconds(4) == 180f, "the Field Forge 90 s, 3 min after Moder");
        Check.That(WayRules.MastersMinuteSeconds(4) == 60f && WayRules.MastersMinuteSeconds(5) == 120f, "the Master's Minute, 2 after Yagluth");
```

`ClassLadderTests.Run()` (and `watchpost` with ClassId `smidr` in `Pool()` in place of `reinforce`):

```csharp
        Check.That(ClassLadder.Find("smidr").Rungs[0].SequenceEqual(new[] { "watchpost" }) &&
                   ClassLadder.Find("smidr").Rungs[1].SequenceEqual(new[] { "fieldforge" }) &&
                   ClassLadder.Find("smidr").Rungs[2].SequenceEqual(new[] { "mastersminute" }),
                   "the Smiðr's rungs: the Watch-post, the Field Forge, the Master's Minute (Reinforce is his passive now)");
        Check.That(ClassLadder.Due(ClassLadder.Find("smidr"), 2, new[] { "craftsman", "fieldforge", "mastersminute", "reinforce" })
                       .SequenceEqual(new[] { "watchpost" }),
                   "a save holding Reinforce is taught the Watch-post on resume");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors and `FAIL` lines.

- [ ] **Step 3: Implement the rules and the table**

```csharp
        // --- The Smiðr ---
        public static float ArmourFactor(int gods) => Tempered(gods, TemperSlot.Engine) ? 2f : 1.5f;
        public static int WatchPosts(int gods) => Tempered(gods, TemperSlot.Rung1) ? 2 : 1;
        public static string WatchPostBolt(int gods) => gods >= 6 ? "TurretBoltFlametal" : gods >= 3 ? "TurretBolt" : "TurretBoltWood";
        public static float FieldForgeSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 180f : 90f;
        public static float MastersMinuteSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 120f : 60f;
```

`ClassLadder`, the Smiðr: `Rungs = new[] { new[] { "watchpost" }, new[] { "fieldforge" }, new[] { "mastersminute" } },`
and `Description = "What you build fights for you: a ballista raised where you stand, later a bench and a forge from nothing, and after that the master's minute, when building costs nothing. Your walls take no wear, and your skin is half again as hard. His tools are yours the moment you take his name."`

`RunService.DefaultBoons()`: delete `reinforce`; add
`new BoonDefinition { Id = "watchpost", ClassId = "smidr", Display = "Watch-post", IsPassive = false, CooldownSeconds = 120f, Description = "A ballista rises where you stand and shoots what comes, until it falls or you raise another." },`;
craftsman's card: `"Woodcutting and pickaxe skill to 50, +100 carry. Forge-skin: armour half again; your gear never wears; your walls within 20 m take no wear."`

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: The game side**

In `BoonEffects.Ways.cs`:

```csharp
        private const string ForgeSkinName = "ICSYTW_ForgeSkin";
        private static readonly int ForgeSkinHash = ForgeSkinName.GetStableHashCode();
        private float _forgeSkinFactor = -1f;
        private readonly List<ZDOID> _watchPosts = new List<ZDOID>();

        /// <summary>Forge-skin: armour x1.5 (x2 after the Queen), the game's own m_armorMultiplier (player only).</summary>
        private void ApplyForgeSkin()
        {
            var seman = Player.m_localPlayer?.GetSEMan();
            if (seman == null) return;
            float f = WayRules.ArmourFactor(_defeatedBossCount());
            if (seman.HaveStatusEffect(ForgeSkinHash) && Math.Abs(f - _forgeSkinFactor) < 0.001f) return;
            seman.RemoveStatusEffect(ForgeSkinHash, quiet: true);
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = ForgeSkinName;
            se.m_name = "Forge-skin";
            se.m_tooltip = "Your armour is half again as hard, and your gear never wears.";
            se.m_ttl = 0f;
            se.m_armorMultiplier = f - 1f;
            seman.AddStatusEffect(se);
            _forgeSkinFactor = f;
        }

        private void UnapplyForgeSkin()
        {
            Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(ForgeSkinHash, quiet: true);
            _forgeSkinFactor = -1f;
        }

        /// <summary>Once a second while "craftsman" is held: armour current, gear whole, walls within 20 m unworn.</summary>
        private void TickSmith(int gods)
        {
            if (!Holds("craftsman")) return;
            ApplyForgeSkin();
            var player = Player.m_localPlayer;
            if (player == null) return;
            foreach (var item in player.GetInventory().GetEquippedItems())
                if (item != null && item.m_shared.m_useDurability) item.m_durability = item.GetMaxDurability();
            ReinforceAround(player.transform.position);
        }

        /// <summary>Raises a ballista where the Smiðr stands: loaded for good, blind to players and tames, never saved.</summary>
        private bool ActivateWatchPost()
        {
            var held = FindHeld("watchpost");
            if (held == null || held.CooldownRemaining > 0f) return false;
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab("piece_turret") : null;
            int gods = _defeatedBossCount();
            var bolt = scene != null ? scene.GetPrefab(WayRules.WatchPostBolt(gods))?.GetComponent<ItemDrop>() : null;
            if (player == null || prefab == null || bolt == null) { LastActivationMessage = "No ballista answers."; return false; }

            var man = ZDOMan.instance;
            _watchPosts.RemoveAll(id => man == null || man.GetZDO(id) == null);
            while (_watchPosts.Count >= WayRules.WatchPosts(gods)) { DestroyByZdo(_watchPosts[0]); _watchPosts.RemoveAt(0); }

            Vector3 at = player.transform.position + player.transform.forward * 3f;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(at, out float ground, 5)) at.y = ground;
            var inst = UnityEngine.Object.Instantiate(prefab, at, Quaternion.LookRotation(player.transform.forward));
            var view = inst != null ? inst.GetComponent<ZNetView>() : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null) { if (inst != null) UnityEngine.Object.Destroy(inst); return false; }
            zdo.Persistent = false;
            if (!view.IsOwner()) view.ClaimOwnership();

            var piece = inst.GetComponent<Piece>();
            if (piece != null) { piece.m_canBeRemoved = false; piece.m_resources = new Piece.Requirement[0]; }
            var turret = inst.GetComponent<Turret>();
            if (turret != null)
            {
                turret.m_targetPlayers = false;
                turret.m_targetTamed = false;
                turret.m_maxAmmo = 0;
                turret.m_defaultAmmo = bolt;
                turret.m_returnAmmoOnDestroy = false;
            }
            _watchPosts.Add(zdo.m_uid);
            held.CooldownRemaining = held.Def.CooldownSeconds;
            LastActivationMessage = "The watch-post stands.";
            return true;
        }

        private void TakeDownWatchPosts()
        {
            foreach (var id in _watchPosts) DestroyByZdo(id);
            _watchPosts.Clear();
        }
```

`ReinforceAround(Vector3)` is `ActivateReinforce`'s loop over `WearNTear.GetAllInstances()` turned into a passive:
move that loop into `private void ReinforceAround(Vector3 centre)`, skipping pieces already in `_reinforced`, and keep
`EndReinforce` (it restores the flags) — call it from `craftsman`'s Unapply. Delete `ActivateReinforce` and its pending
schedule; keep `ReinforceRadius`.

`TickWays`: `try { TickSmith(gods); } catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Smith: " + e.Message); }`.

`BoonEffects.cs`: `Activate` — replace `case "reinforce"` with `case "watchpost": return ActivateWatchPost();`.
`Unapply` — replace `case "reinforce": EndReinforce(); break;` with `case "watchpost": TakeDownWatchPosts(); break;`, and
add to `craftsman`'s unapply `UnapplyForgeSkin(); EndReinforce();`. `Apply case "craftsman"`: add `ApplyForgeSkin();`.
`UnapplyAll`'s finally: add `SafeInvoke(TakeDownWatchPosts);`. `ActivateFieldForge`/`ActivateMastersMinute`: read
`WayRules.FieldForgeSeconds(gods)` / `WayRules.MastersMinuteSeconds(gods)` in their `SchedulePending`.

Self-check (`RunService`, beside the Guard line from Task 6): `_selfCheck.AllOf("The Smiðr's watch-post", 4, missing, "the Watch-post stands down")`
over `piece_turret`, `TurretBoltWood`, `TurretBolt`, `TurretBoltFlametal` resolved with `ZNetScene.instance.GetPrefab`.

`Thane.cs`, Dvalinn's choice line (line ~205): replace "The field forge..." clause with "He will lend you his watch-post
first, a ballista that fights for you; the bench and forge from nothing come after."

- [ ] **Step 6: Build and check**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

- [ ] **Step 7: Commit**

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the Smiðr - Forge-skin, walls that take no wear, and a watch-post ballista that fights for him"
```

---

### Task 11: The wheel's tilt

**Why:** spec, "The wheel's tilt": each way's four favoured general boons draw at double weight.

**Files:**
- Modify: `ClassLadder.cs` (`ClassDefinition.Favoured`; each way's list), `BoonEngine.cs` (`Favoured`, `CreateOffer`'s
  weight), `Tests/BoonEngineTests.cs`, `Tests/ClassLadderTests.cs`, `RunService.cs` (set `_boons.Favoured` where the way
  is taken up, laid down, and restored)

**Interfaces:**
- Produces: `public string[] ClassDefinition.Favoured`; `public ICollection<string> BoonEngine.Favoured` (a
  `HashSet<string>`, empty by default).

- [ ] **Step 1: Write the failing tests**

`BoonEngineTests.Run()`:

```csharp
        // The wheel's tilt: a favoured boon draws at double weight.
        Func<List<BoonDefinition>> two = () => new List<BoonDefinition>
        {
            new BoonDefinition { Id = "a", IsPassive = true }, new BoonDefinition { Id = "b", IsPassive = true },
            new BoonDefinition { Id = "c", IsPassive = true }, new BoonDefinition { Id = "d", IsPassive = true },
        };
        int favouredFirst = 0, plainFirst = 0;
        for (int i = 0; i < 400; i++)
        {
            var tilted = new BoonEngine(two(), new Random(i), 45f);
            tilted.Favoured.Add("a");
            tilted.CreateOffer();
            if (tilted.CurrentOffer[0].Id == "a") favouredFirst++;
            var plain = new BoonEngine(two(), new Random(i), 45f);
            plain.CreateOffer();
            if (plain.CurrentOffer[0].Id == "a") plainFirst++;
        }
        Check.That(favouredFirst > plainFirst * 1.5, $"a favoured boon leads the offer far more often ({favouredFirst} vs {plainFirst})");
```

`ClassLadderTests.Run()`:

```csharp
        Check.That(ClassLadder.Catalog().All(c => c.Favoured != null && c.Favoured.Length == 4),
                   "every way favours four general boons");
        var withFavoured = Pool();
        foreach (var id in ClassLadder.Catalog().SelectMany(c => c.Favoured))
            if (!withFavoured.Any(b => b.Id == id)) withFavoured.Add(new BoonDefinition { Id = id, IsPassive = true });
        Check.That(!ClassLadder.Validate(ClassLadder.Catalog(), withFavoured).Any(), "favoured ids the pool has pass the validator");
        var noBounty = withFavoured.Where(b => b.Id != "bounty").ToList();
        Check.That(ClassLadder.Validate(ClassLadder.Catalog(), noBounty).Any(p => p.Contains("favours 'bounty'")),
                   "a favoured id missing from the pool is caught at run start");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash Tests/run_tests.sh`
Expected: compile errors naming `Favoured`.

- [ ] **Step 3: Implement**

`ClassDefinition` (in `ClassLadder.cs` or its own file — wherever the class is declared): add
`/// <summary>Four general boons this way's wheel draws at double weight (class balance, 2026-10-08).</summary> public string[] Favoured;`.
Each way, from the spec's table:

| Way | `Favoured` |
|---|---|
| hunter | `"fleet", "farsight", "wayfarer", "bounty"` |
| volva | `"hearty", "kindling", "wind", "study"` |
| berserker | `"bloodthirst", "relentless", "sharp", "reckless"` |
| huskarl | `"thickskin", "hardshell", "hearty", "tireless"` |
| skald | `"tireless", "fleet", "stuffed", "wind"` |
| saefari | `"wayfarer", "fleet", "coldblood", "farsight"` |
| smidr | `"woodsman", "mule", "mend", "bounty"` |

`BoonEngine`: `public readonly HashSet<string> Favoured = new HashSet<string>();` (type it as `ICollection<string>` in
the property if a field is not wanted), and in `CreateOffer`'s two weight reads replace `Math.Max(1, o.Weight)` with
`WeightOf(o)`:

```csharp
        private int WeightOf(BoonDefinition d) => Math.Max(1, d.Weight) * (Favoured.Contains(d.Id) ? 2 : 1);
```

`ClassLadder.Validate`: report a favoured id missing from the pool:
`foreach (var cls in classList) foreach (var id in cls.Favoured ?? new string[0]) if (!byId.ContainsKey(id)) yield return $"class '{cls.Id}' favours '{id}', which is not in the pool";`

`RunService`: wherever `_classId` is assigned (taking up a way, laying it down, `RestoreFrom`), follow it with
`SetFavoured();`:

```csharp
        private void SetFavoured()
        {
            if (_boons == null) return;
            _boons.Favoured.Clear();
            foreach (var id in ClassLadder.Find(_classId)?.Favoured ?? new string[0]) _boons.Favoured.Add(id);
        }
```

- [ ] **Step 4: Run them to verify they pass**

Run: `bash Tests/run_tests.sh`
Expected: `ALL PASS`.

- [ ] **Step 5: Build, check and commit**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh`
Expected: build exit 0, `ALL PASS`, `0 unresolved.`

```bash
git add -A ICanShowYouTheWorld/RunMode Tests
git commit -m "feat(run): the wheel tilts toward the way - four favoured boons each, drawn at double weight"
```

---

### Task 12: The docs, and the home test plan

**Files:**
- Modify: `docs/superpowers/2026-10-06-home-test-plan.md`, `docs/superpowers/RESUME.md`, `docs/SAGA-WALKTHROUGH.md`
  (the ways' paragraph), `dist/windows/DEV-MODE.md` (if it lists a way's rungs), `CLAUDE.md` (the key list's "the three
  RUNGS of whichever class" sentence names no rung, so only if a rung id is named), the spec's "Order of work"

- [ ] **Step 1: Home test plan checks**

After check 8e, add one check per way: the engine felt in a fight, its verb, and the tempering where reachable with the
dev step-skip (`Shift + P` / `Shift + Keypad +`) and the class cycle (`Shift + *`). Each says what to look for:

- Hunter: one star per wolf, two at a time (a third call sends the oldest home); Menagerie lends only a boar, hen or
  chicken before Bonemass.
- Völva: green ticks gone (no number per second), but health climbing about 3 a second at the hearth; skeletons from
  the choice (`U`/`[7]`).
- Berserker: swing at greylings; the BOONS page or the damage numbers grow; standing still, it fades within seconds.
- Húskarl: block a greyling: stamina falls half as much, health ticks up; parry: more.
- Skald: `U I O` switch songs; the song stays; a switch within 20 s does not swell.
- Sæfari: fittings at half; Ward I strikes around you on land; Undertow throws foes back; Stormcaller's lightning
  every second.
- Smiðr: a ballista rises on `U`, shoots greylings and deer, never you; it is gone after a reload.
- `grep "Saga self-check" Player.log`: "Way effects" OK, "The Smiðr's watch-post" OK.

- [ ] **Step 2: RESUME, the walkthrough, the spec**

RESUME: one bullet naming the plan and the build. The walkthrough: each way's line updated to its engine. The spec's
"Order of work": mark 2–4 done with the build letter.

- [ ] **Step 3: Commit**

```bash
git add docs dist CLAUDE.md
git commit -m "docs: the ways balanced - the home test plan's checks per way, RESUME, the walkthrough"
```

---

### Task 13: The build (only when the owner says the current builds have been played)

- [ ] **Step 1: Ask, or read the ledger**

The owner's rule (memory, 2026-10-06): no new saga build faster than it can be tested. Build only on the owner's word.

- [ ] **Step 2: The loop**

```bash
V=$(bash Scripts/nextversion.sh) && git tag "$V" && bash Scripts/setversion.sh
msbuild Valheim.sln -p:Configuration=Debug -v:minimal && bash Tests/run_tests.sh && bash Scripts/check_refs.sh
bash Scripts/stage_windows.sh && git checkout -- dist/windows/patcher/Patcher.exe
git add ICanShowYouTheWorld/Assets/Version.cs dist/windows/patcher/ICanShowYouTheWorld.dll
git commit -m "build: $V (dev) — the ways balanced; staged"
git push origin feature/run-mode && git push origin --tags
bash Scripts/deploy_local.sh
git tag -a saga/ways-balanced -m "The ways balanced: an engine and a verb for every way" HEAD && git push origin saga/ways-balanced
```

Expected: `Bundle signature valid`.
