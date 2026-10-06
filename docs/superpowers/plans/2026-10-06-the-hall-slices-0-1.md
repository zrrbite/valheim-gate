# The hall: slices 0 and 1 implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Slice 0 is a dev key that calls one Aspect to the player's side, to learn whether the finale's cast works. Slice 1 turns Act I's HEARTH questline into the hall's first section: an anchor fire, a roof that closes over it, and a floor of some length.

**Architecture:** Pure logic lives in `ICanShowYouTheWorld/RunMode/` and is unit-tested by `Tests/run_tests.sh`:

- `HallCast`: the six gods' names, trophies, powers, Aspects and after-death lines.
- `HallJudge`: the hall's length, choosing an anchor, noticing a lost one.

Game-coupled code lives in `ICanShowYouTheWorld/RunMode/Unity/`:

- `HallCall`: spawns a turned Aspect and watches it.
- `RunService`: the dev key, the hall poll, the anchor and the HALL steps.

Measures reach the step engine the way Comfort does: `_challenges.ReportMeasure(ChallengeKind.PlayerState, "<Param>", value)`.

**Tech Stack:** C# (.NET Framework 4.7.2) against Valheim 1.0.17. Build with `msbuild Valheim.sln -p:Configuration=Debug -v:minimal`, test with `bash Tests/run_tests.sh` (Mono's Roslyn `csc`).

**Spec:** `docs/superpowers/specs/2026-10-06-the-hall-design.md` (sections 3, 4, 5 and 8). Read it first.

**When:** after the owner's home test of `1.0.17-run.2026-10-06c` (owner's decision: design now, build after).

## Global Constraints

- Game names come from 1.0.17's own data (`docs/catalog/`), never from memory. Every new guessed name joins the run-start self-check (`SagaSelfCheck`).
- The Aspects are `Aspect_Eikthyr`, `Aspect_Elder`, `Aspect_Bonemass`, `Aspect_Moder`, `Aspect_Yagluth` and `Aspect_SeekerQueen`. Each has `Humanoid`, `MonsterAI` and `CharacterDrop`, faction 8 (`Boss`), and no `Tameable`. Fader is also faction 8.
- The turned example to copy is `Skeleton_Friendly`: faction 0 (`Players`) and a `Tameable` that starts tamed.
- The trophies are `TrophyEikthyr`, `TrophyTheElder`, `TrophyBonemass`, `TrophyDragonQueen`, `TrophyGoblinKing` and `TrophySeekerQueen`. The powers are `GP_Eikthyr`, `GP_TheElder`, `GP_Bonemass`, `GP_Moder`, `GP_Yagluth` and `GP_Queen`. The after-death lines are `$deadspeak_eikthyr`, `_elder`, `_bonemass`, `_moder`, `_yagluth` and `_queen`.
- APIs confirmed in 1.0.17's IL:
  - `Character.m_faction` is a public field; `Character.SetTamed(bool)` is public.
  - `BaseAI.Alert()` is public; `BaseAI.GetTargetCreature()` is public and virtual.
  - `ZNetScene.Destroy(GameObject)` is public; `ZDO.Persistent` has a public setter.
  - `Piece.GetAllPiecesInRadius(Vector3, float, List<Piece>)` and `Piece.IsCreator()`.
  - `Cover.GetCoverForPoint(Vector3, out float, out bool, float)` lives in `assembly_utils`.
- **Track ids are save data.** A resume restores a track by id. HEARTH becomes HALL by its LABEL only; its id stays `"hearth"` (`HearthTrackId`).
- **No spoilers.** Act I's hall text never mentions seats, gods or how many sections there will be.
- **Dev keys exist only in dev builds** (`RunService.DevMode`). They go through `KeyLayout` (`SagaKey`) and must pass `KeyLayoutTests`.
- **The bible's rules stand:** ravens never help; gods speak only once defeated.

## Review Focus

1. **Quitting while an Aspect is called.** It must not reload as a hostile boss-faction Aspect beside the player. `HallCall` sets `ZDO.Persistent = false` (Task 3), and the slice 0 protocol checks it (Task 4).
2. **The anchor fire is destroyed or moved.** The hall must re-anchor on the next fire among its pieces and keep finished steps finished. `HallJudge.AnchorLost` is tested in Task 5, and the re-anchor runs in Task 6.
3. **A save made before slice 1 resumes after it.** The anchor fields are missing from old saves. They must default to "no anchor" and be set from the first fire found, not crash. Task 5 restores with defaults, and Task 7 adds a manual check.
4. **No fire built yet when the roof and length steps open.** The steps must sit at zero with their hint, not throw. `PollHall` returns early with no anchor (Task 6).
5. **The dev key pressed with no boss nearby, or twice quickly.** Each press calls the next god's Aspect; several may be live; all are dismissed when the run ends or suspends (Task 3).

---

## Slice 0: can an Aspect fight on the player's side?

### Task 1: HallCast, the six gods as data

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/HallCast.cs`
- Create: `Tests/HallCastTests.cs`
- Modify: `Tests/TestMain.cs` (register `HallCastTests.Run();` after `KeyLayoutTests.Run();`)
- Modify: `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj` (add `<Compile Include="RunMode\HallCast.cs" />` after the `KeyLayout.cs` line; the project lists sources explicitly)

**Interfaces:**
- Produces: `HallGod` (string fields `DefeatKey`, `Name`, `Trophy`, `Power`, `Aspect`, `AspectName`, `DeadSpeak`), `HallCast.Gods` (`HallGod[]`, Acts I–VI in order), `HallCast.ByDefeatKey(string) : HallGod`, `HallCast.Next(int) : int`.

- [ ] **Step 1: Write the failing test** in `Tests/HallCastTests.cs`:

```csharp
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>The gods the hall seats and the horn calls (spec 2026-10-06-the-hall-design.md, section 5).</summary>
static class HallCastTests
{
    public static void Run()
    {
        var gods = HallCast.Gods;
        Check.That(gods.Length == 6, "six gods: Acts I to VI. Fader is the enemy, not a guest");

        Check.That(gods.Select(g => g.DefeatKey).SequenceEqual(new[]
                   { "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking", "defeated_queen" }),
                   "in act order, keyed by the world's own defeat keys");
        Check.That(gods.Select(g => g.Aspect).SequenceEqual(new[]
                   { "Aspect_Eikthyr", "Aspect_Elder", "Aspect_Bonemass", "Aspect_Moder", "Aspect_Yagluth", "Aspect_SeekerQueen" }),
                   "the Aspects are the game's own prefabs, from the catalogue");
        Check.That(gods.Select(g => g.Trophy).SequenceEqual(new[]
                   { "TrophyEikthyr", "TrophyTheElder", "TrophyBonemass", "TrophyDragonQueen", "TrophyGoblinKing", "TrophySeekerQueen" }),
                   "each god's trophy, as the boss drops it");
        Check.That(gods.Select(g => g.Power).SequenceEqual(new[]
                   { "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen" }),
                   "each god's Forsaken power");
        Check.That(gods.All(g => g.DeadSpeak.StartsWith("$deadspeak_")) &&
                   gods.Select(g => g.DeadSpeak).Distinct().Count() == 6,
                   "each god speaks its own after-death line, a localisation token");
        Check.That(!gods.Any(g => g.Aspect.Contains("Fader") || g.DefeatKey == "defeated_fader"),
                   "Fader is never called against himself");

        Check.That(HallCast.ByDefeatKey("defeated_dragon")?.Name == "Moder" && HallCast.ByDefeatKey("nope") == null,
                   "a god is found by its defeat key, and an unknown key finds none");
        Check.That(HallCast.Next(0) == 1 && HallCast.Next(5) == 0 && HallCast.Next(-1) == 0,
                   "the dev key cycles through the six and wraps");
    }
}
```

- [ ] **Step 2: Register it and run it to see it fail.** Add `HallCastTests.Run();` after `KeyLayoutTests.Run();` in `Tests/TestMain.cs`.

Run: `bash Tests/run_tests.sh`
Expected: compile errors `The name 'HallCast' does not exist in the current context`.

- [ ] **Step 3: Write `ICanShowYouTheWorld/RunMode/HallCast.cs`:**

```csharp
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>One god the hall can seat and the horn can call.</summary>
    public sealed class HallGod
    {
        public string DefeatKey;
        public string Name;
        public string Trophy;
        public string Power;
        public string Aspect;
        public string AspectName;
        public string DeadSpeak;
    }

    /// <summary>
    /// The six gods of Acts I-VI (the hall spec, section 5). Every name is read from Valheim 1.0.17's own
    /// data (docs/catalog/), not remembered, and each joins the run-start self-check.
    /// </summary>
    public static class HallCast
    {
        public static readonly HallGod[] Gods =
        {
            new HallGod { DefeatKey = "defeated_eikthyr",    Name = "Eikthyr",   Trophy = "TrophyEikthyr",     Power = "GP_Eikthyr",   Aspect = "Aspect_Eikthyr",     AspectName = "Aspect of the Lightning Stag",    DeadSpeak = "$deadspeak_eikthyr" },
            new HallGod { DefeatKey = "defeated_gdking",     Name = "The Elder", Trophy = "TrophyTheElder",    Power = "GP_TheElder",  Aspect = "Aspect_Elder",       AspectName = "Aspect of the Living Forest",     DeadSpeak = "$deadspeak_elder" },
            new HallGod { DefeatKey = "defeated_bonemass",   Name = "Bonemass",  Trophy = "TrophyBonemass",    Power = "GP_Bonemass",  Aspect = "Aspect_Bonemass",    AspectName = "Aspect of the Writhing Dead",     DeadSpeak = "$deadspeak_bonemass" },
            new HallGod { DefeatKey = "defeated_dragon",     Name = "Moder",     Trophy = "TrophyDragonQueen", Power = "GP_Moder",     Aspect = "Aspect_Moder",       AspectName = "Aspect of the Dragon Mother",     DeadSpeak = "$deadspeak_moder" },
            new HallGod { DefeatKey = "defeated_goblinking", Name = "Yagluth",   Trophy = "TrophyGoblinKing",  Power = "GP_Yagluth",   Aspect = "Aspect_Yagluth",     AspectName = "Aspect of the Twisted Soul",      DeadSpeak = "$deadspeak_yagluth" },
            new HallGod { DefeatKey = "defeated_queen",      Name = "The Queen", Trophy = "TrophySeekerQueen", Power = "GP_Queen",     Aspect = "Aspect_SeekerQueen", AspectName = "Aspect of the Crawling Matriarch", DeadSpeak = "$deadspeak_queen" },
        };

        public static HallGod ByDefeatKey(string key) => Gods.FirstOrDefault(g => g.DefeatKey == key);

        /// <summary>The dev key's cycle: the god after <paramref name="index"/>, wrapping. Anything out of range starts at the first.</summary>
        public static int Next(int index) => index < 0 || index >= Gods.Length - 1 ? 0 : index + 1;
    }
}
```

- [ ] **Step 4: Run the tests.** Run: `bash Tests/run_tests.sh`. Expected: `ALL PASS`.

- [ ] **Step 5: Add the file to the csproj, build, commit.**

Run: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal`. Expected: `ICanShowYouTheWorld -> .../ICanShowYouTheWorld.dll`, no `error`.

```bash
git add ICanShowYouTheWorld/RunMode/HallCast.cs Tests/HallCastTests.cs Tests/TestMain.cs ICanShowYouTheWorld/ICanShowYouTheWorld.csproj
git commit -m "feat(run): HallCast - the six gods the hall seats and the horn calls, from the game's own names"
```

### Task 2: the dev key: modifier plus the light key calls an Aspect

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/KeyLayout.cs` (add `SagaKey.DevDot` to `ModifiedDevKeys`)
- Modify: `Tests/KeyLayoutTests.cs`
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs`: in `HandleDevInput`, the bare `DevDot` branch and a new modified one, and `DevKeyHelp`
- Modify: `dist/windows/DEV-MODE.md`: a row in both key tables

**Interfaces:**
- Consumes: `HallCast.Gods`, `HallCast.Next` (Task 1); `HallCall.Summon` (Task 3: this task only adds the key and calls `DevCallAspect()`, which Task 3 defines).
- Produces: `SagaKey.DevDot` read with a modifier.

- [ ] **Step 1: Write the failing test.** Add to `KeyLayoutTests.Run()` before `LobbyOfferTests();`:

```csharp
        Check.That(KeyLayout.ModifiedDevKeys.Contains(SagaKey.DevDot) && KeyLayout.BareDevKeys.Contains(SagaKey.DevDot),
                   "the light key has a modified layer: modifier + light calls an Aspect (the hall's slice 0)");
```

- [ ] **Step 2: Run it.** `bash Tests/run_tests.sh` → `FAIL: the light key has a modified layer...`

- [ ] **Step 3: Add `SagaKey.DevDot` to `ModifiedDevKeys`** in `KeyLayout.cs`:

```csharp
        public static readonly SagaKey[] ModifiedDevKeys =
        {
            SagaKey.DevStar, SagaKey.DevSlash, SagaKey.DevDelete, SagaKey.DevDot,
            SagaKey.DevPlus, SagaKey.DevMinus, SagaKey.DevBackspace,
        };
```

and update the enum's comment so it says that Star, Slash, Delete and Dot are read bare AND with a modifier.

- [ ] **Step 4: Run the tests.** Expected `ALL PASS`. The distinctness rule for modified dev keys now includes `.` (numpad) and `B` (laptop), and both are distinct from the others.

- [ ] **Step 5: Wire the handler.** In `RunService.HandleDevInput`, change the existing bare branch `else if (BoonKeys.Pressed(SagaKey.DevDot))` to `else if (!mod && BoonKeys.Pressed(SagaKey.DevDot))`, and add before it:

```csharp
            else if (mod && BoonKeys.Pressed(SagaKey.DevDot))
            {
                // The hall's slice 0 (spec section 8): one Aspect, turned to the player's side, to
                // learn whether the finale's cast fights, targets a boss, lasts, and looks right.
                DevCallAspect();
            }
```

In `DevKeyHelp`, extend the line that names the ways' modified keys:

```csharp
                    $"Shift/Ctrl/Alt + {K(SagaKey.DevStar)} cycle the way (all seven)   ·   + {K(SagaKey.DevSlash)} learn all its rungs   " +
                    $"·   + {K(SagaKey.DevDelete)} bow + shield + 5 lights   ·   + {K(SagaKey.DevDot)} call an Aspect (cycles the six)",
```

- [ ] **Step 6: Document it.** In `dist/windows/DEV-MODE.md`, add a row to the numpad table, `| mod + Keypad . | **Call an Aspect** to your side for a minute: the next of the six each press (the hall's test) |`, and to the laptop table, `| mod + Keypad . | mod + B |`.

- [ ] **Step 7: Commit with Task 3** (it does not build until `DevCallAspect` exists).

### Task 3: HallCall: a turned Aspect, watched and dismissed

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/Unity/HallCall.cs`
- Modify: `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj` (add `<Compile Include="RunMode\Unity\HallCall.cs" />`)
- Modify: `ICanShowYouTheWorld/Core/Configuration.cs` (`runHallCallSeconds`, default 60)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs`: `_hallCall` field, `DevCallAspect()`, a tick call, dismissal on run end and suspend, and the `CheckHallCast()` self-check

**Interfaces:**
- Consumes: `HallCast.Gods`, `HallGod` (Task 1).
- Produces: `HallCall.Summon(HallGod god, Vector3 at, float seconds) : bool`, `HallCall.Tick(float dt)`, `HallCall.DismissAll()`, `HallCall.LiveCount : int`.

- [ ] **Step 1: Add the config setting.** In `Configuration.cs`, beside `runKeyLayout`, mirror its three edits:
  - in the interface: `float RunHallCallSeconds { get; set; }`
  - the field: `[SerializeField] private float runHallCallSeconds = 60f;`
  - the property: `public float RunHallCallSeconds { get => runHallCallSeconds; set => runHallCallSeconds = value; }`

- [ ] **Step 2: Write `HallCall.cs`:**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The seated gods, called to the player's side (the hall spec, section 5). Slice 0 is its test: one
    /// Aspect at a time from a dev key, with its life logged once a second.
    /// </summary>
    /// <remarks>
    /// An Aspect is a Humanoid with a MonsterAI, faction Boss (8), no Tameable - and Fader is faction
    /// Boss too, so untouched they would not fight him. It is turned the way the game's own
    /// Skeleton_Friendly is set up: the Players faction, tamed. Its ZDO is made non-persistent, so a
    /// quit mid-call cannot save it into the world and reload it as a hostile boss-faction Aspect.
    /// </remarks>
    internal sealed class HallCall
    {
        private sealed class Called
        {
            public HallGod God;
            public Character Body;
            public float Left;
            public float NextLog;
        }

        private readonly List<Called> _live = new List<Called>();

        public int LiveCount => _live.Count;

        public bool Summon(HallGod god, Vector3 at, float seconds)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(god.Aspect) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Hall call: no prefab '{god.Aspect}'.");
                return false;
            }

            var obj = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            var body = obj.GetComponent<Character>();
            if (body == null)
            {
                ZNetScene.instance.Destroy(obj);
                Debug.LogWarning($"[ICanShowYouTheWorld] Hall call: '{god.Aspect}' has no Character.");
                return false;
            }

            var view = obj.GetComponent<ZNetView>();
            if (view != null && view.GetZDO() != null) view.GetZDO().Persistent = false;

            body.m_faction = Character.Faction.Players;
            body.SetTamed(true);
            body.GetComponent<BaseAI>()?.Alert();

            _live.Add(new Called { God = god, Body = body, Left = seconds, NextLog = 0f });
            Debug.Log($"[ICanShowYouTheWorld] Hall call: {god.AspectName} ({god.Aspect}) called for {seconds:0}s, " +
                      $"faction {body.m_faction}, tamed {body.IsTamed()}.");
            return true;
        }

        public void Tick(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var c = _live[i];
                c.Left -= dt;
                c.NextLog -= dt;

                if (ReferenceEquals(c.Body, null) || c.Body == null || c.Body.IsDead())
                {
                    Debug.Log($"[ICanShowYouTheWorld] Hall call: {c.God.AspectName} is gone ({c.Left:0}s left).");
                    _live.RemoveAt(i);
                    continue;
                }

                if (c.NextLog <= 0f)
                {
                    c.NextLog = 1f;
                    var target = c.Body.GetComponent<BaseAI>()?.GetTargetCreature();
                    var player = Player.m_localPlayer;
                    float toPlayer = player != null ? Vector3.Distance(player.transform.position, c.Body.transform.position) : -1f;
                    Debug.Log($"[ICanShowYouTheWorld] Hall call: {c.God.Aspect} hp {c.Body.GetHealth():0}/{c.Body.GetMaxHealth():0}, " +
                              $"target '{(target != null ? target.m_name : "none")}', {toPlayer:0.0}m from the player, {c.Left:0}s left.");
                }

                if (c.Left <= 0f) Dismiss(i);
            }
        }

        public void DismissAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Dismiss(i);
        }

        private void Dismiss(int i)
        {
            var c = _live[i];
            _live.RemoveAt(i);
            try
            {
                if (!ReferenceEquals(c.Body, null) && c.Body != null && ZNetScene.instance != null)
                    ZNetScene.instance.Destroy(c.Body.gameObject);
                Debug.Log($"[ICanShowYouTheWorld] Hall call: {c.God.AspectName} fades.");
            }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Hall call dismiss failed: {ex.Message}"); }
        }
    }
}
```

`IsTamed()`, `GetHealth()`, `GetMaxHealth()` and `IsDead()` are public on `Character`. Confirm each in the IL before relying on it (`grep "end of method Character::IsTamed"` in an `ikdasm` dump). If the no-argument `IsTamed()` is not public, log the ZDO's `tamed` bool instead.

- [ ] **Step 3: Wire it into `RunService`.**
  - **The field:** `private readonly HallCall _hallCall = new HallCall();` and `private int _devAspect = -1;`.
  - **The tick:** in `Tick(float dt)`, where the run is active, call `_hallCall.Tick(dt);`.
  - **Dismissal:** call `_hallCall.DismissAll();` wherever the run ends or suspends, at the same places the run unapplies its boon effects (`SafeUnapplyAllBoonEffects()` callers).
  - **The dev action:**

```csharp
        private void DevCallAspect()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            _devAspect = HallCast.Next(_devAspect);
            var god = HallCast.Gods[_devAspect];
            var at = player.transform.position + player.transform.forward * 5f;
            bool ok = _hallCall.Summon(god, at, _cfg.RunHallCallSeconds);
            DevMessage(ok ? $"DEV: {god.AspectName} answers for {_cfg.RunHallCallSeconds:0}s ({_hallCall.LiveCount} called)."
                          : $"DEV: {god.Aspect} could not be called - see the log.");
        }
```

- [ ] **Step 4: Add the self-check.** Add `CheckHallCast();` after `CheckKeys();` in the self-check sequence, and:

```csharp
        /// <summary>The hall's cast (spec section 5): every Aspect a creature, every trophy an item, every power a status effect, every line a translation.</summary>
        private void CheckHallCast()
        {
            if (_selfCheck == null) return;
            try
            {
                var scene = ZNetScene.instance;
                var odb = ObjectDB.instance;
                if (scene == null || odb == null) { _selfCheck.Fallback("The hall's cast", "the game was not ready - not checked this run"); return; }

                _selfCheck.AllOf("The hall's Aspects", HallCast.Gods.Length,
                    HallCast.Gods.Where(g => scene.GetPrefab(g.Aspect)?.GetComponent<Character>() == null).Select(g => g.Aspect).ToList(),
                    "those gods cannot be called");
                _selfCheck.AllOf("The gods' trophies", HallCast.Gods.Length,
                    HallCast.Gods.Where(g => odb.GetItemPrefab(g.Trophy) == null).Select(g => g.Trophy).ToList(),
                    "those places can never be raised");
                _selfCheck.AllOf("The gods' powers", HallCast.Gods.Length,
                    HallCast.Gods.Where(g => odb.GetStatusEffect(g.Power.GetStableHashCode()) == null).Select(g => g.Power).ToList(),
                    "those places grant nothing");
                _selfCheck.AllOf("The gods' last words", HallCast.Gods.Length,
                    HallCast.Gods.Where(g => Localization.instance == null || Localization.instance.Localize(g.DeadSpeak) == g.DeadSpeak).Select(g => g.DeadSpeak).ToList(),
                    "those gods arrive silent");
            }
            catch (Exception ex) { LogOnce("self-check-hall", ex); }
        }
```

- [ ] **Step 5: Build and test.** `msbuild Valheim.sln -p:Configuration=Debug -v:minimal`, then `bash Tests/run_tests.sh`. Expected: a clean build and `ALL PASS`.

- [ ] **Step 6: Commit (Tasks 2 and 3 together).**

```bash
git add -A
git commit -m "feat(run): the hall's slice 0 - a dev key calls one Aspect to the player's side, watched and dismissed"
```

### Task 4: build it and run the test protocol

**Files:**
- Modify: `HANDOFF_WINDOWS.md` (a TASK at the top)
- Modify: `docs/superpowers/RESUME.md` (a bullet at the top of "Where things stand")

- [ ] **Step 1: Write the protocol** as the HANDOFF task:

```markdown
## (date) - TASK: can an Aspect fight for you? (the hall's slice 0; the version Scripts/nextversion.sh printed)

Dev build, dev mode on. Stand near a boss (summon one at its altar, or use any act's boss). Press
mod + Keypad . (laptop: mod + B). Each press calls the next god's Aspect for a minute. For each, note:

1. Does it attack the BOSS, or stand idle, or attack you?
2. How long does it live? Does it vanish after one attack, or stay the minute?
3. Does it look right beside you, or ridiculous?
4. Quit to the menu while one is called, reload: is it gone? (It must be.)

`Select-String Player.log -Pattern "Hall call"` gives its life second by second: hp, target, distance.
Self-check: "The hall's Aspects / trophies / powers / last words" all OK.
```

- [ ] **Step 2: The build loop** (RESUME, "The loop"): tag with `Scripts/nextversion.sh`, then `bash Scripts/setversion.sh`, build, `bash Tests/run_tests.sh`, `bash Scripts/stage_windows.sh`, `git checkout -- dist/windows/patcher/Patcher.exe`. Commit as `build: <version> ...`, add the milestone tag `saga/hall-aspect-test`, push the branch and both tags, then `bash Scripts/deploy_local.sh` on the Mac.

- [ ] **Step 3: Record the verdict** in RESUME and the spec (section 5) once the owner has played it, choosing from: Aspects / fallback 1 (the gods' creatures turned) / fallback 2 (the Fallen Warriors) / fallback 3 (powers, no creatures). **Slice 4's plan is written from that verdict.**

---

## Slice 1: Act I's section, the long fire

### Task 5: HallJudge, the hall's length and its anchor

**Files:**
- Create: `ICanShowYouTheWorld/RunMode/HallJudge.cs`
- Create: `Tests/HallJudgeTests.cs`
- Modify: `Tests/TestMain.cs` (register after `HallCastTests.Run();`), `ICanShowYouTheWorld/ICanShowYouTheWorld.csproj`

**Interfaces:**
- Produces: `struct HallPoint { public float X, Y, Z; }`; `HallJudge.Length(IList<HallPoint>) : float`; `HallJudge.PickAnchor(IList<HallPoint> fires, HallPoint player, float maxDistance) : HallPoint?`; `HallJudge.AnchorLost(HallPoint anchor, IList<HallPoint> fires, float tolerance) : bool`.

- [ ] **Step 1: Write the failing test** `Tests/HallJudgeTests.cs`:

```csharp
using System.Collections.Generic;
using ICanShowYouTheWorld.RunMode;

/// <summary>The hall's measures (spec section 4): pure, given points the game side gathered.</summary>
static class HallJudgeTests
{
    static HallPoint P(float x, float y, float z) => new HallPoint { X = x, Y = y, Z = z };

    public static void Run()
    {
        Check.That(HallJudge.Length(new List<HallPoint>()) == 0f && HallJudge.Length(new List<HallPoint> { P(1, 0, 1) }) == 0f,
                   "no hall, or one piece, has no length");
        Check.That(System.Math.Abs(HallJudge.Length(new List<HallPoint> { P(0, 0, 0), P(3, 0, 4), P(1, 0, 1) }) - 5f) < 0.001f,
                   "length is the longest horizontal distance between any two pieces");
        Check.That(System.Math.Abs(HallJudge.Length(new List<HallPoint> { P(0, 0, 0), P(0, 30, 0) })) < 0.001f,
                   "height is not length: a tower is not a hall");

        var fires = new List<HallPoint> { P(20, 0, 0), P(3, 0, 0), P(4, 0, 0) };
        var anchor = HallJudge.PickAnchor(fires, P(0, 0, 0), 10f);
        Check.That(anchor.HasValue && anchor.Value.X == 3f, "the anchor is the player's nearest fire");
        Check.That(!HallJudge.PickAnchor(new List<HallPoint> { P(20, 0, 0) }, P(0, 0, 0), 10f).HasValue,
                   "a fire too far away is not the hall's");

        Check.That(!HallJudge.AnchorLost(P(3, 0, 0), new List<HallPoint> { P(3.4f, 0, 0) }, 1f),
                   "a fire within the tolerance is still the anchor (rebuilt in place)");
        Check.That(HallJudge.AnchorLost(P(3, 0, 0), new List<HallPoint> { P(9, 0, 0) }, 1f) &&
                   HallJudge.AnchorLost(P(3, 0, 0), new List<HallPoint>(), 1f),
                   "no fire near the anchor means it is lost and the hall re-anchors");
    }
}
```

- [ ] **Step 2: Register it and see it fail.** Expected: `The name 'HallJudge' does not exist`.

- [ ] **Step 3: Write `HallJudge.cs`:**

```csharp
using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>A piece's position, without Unity, so the hall's measures are testable.</summary>
    public struct HallPoint { public float X, Y, Z; }

    /// <summary>
    /// The hall's measures (spec section 4). The game side gathers the player's own pieces within the
    /// hall's radius of its anchor fire and hands their positions here.
    /// </summary>
    public static class HallJudge
    {
        /// <summary>The longest horizontal distance between any two pieces: how long the hall is. Height does not count.</summary>
        public static float Length(IList<HallPoint> pieces)
        {
            float best = 0f;
            for (int i = 0; i < pieces.Count; i++)
                for (int j = i + 1; j < pieces.Count; j++)
                    best = Math.Max(best, Flat(pieces[i], pieces[j]));
            return best;
        }

        /// <summary>The fire nearest the player, if one is within <paramref name="maxDistance"/>: it becomes the hall's centre.</summary>
        public static HallPoint? PickAnchor(IList<HallPoint> fires, HallPoint player, float maxDistance)
        {
            HallPoint? best = null;
            float bestDistance = maxDistance;
            foreach (var fire in fires)
            {
                float d = Flat(fire, player);
                if (d > bestDistance) continue;
                bestDistance = d;
                best = fire;
            }
            return best;
        }

        /// <summary>True when no fire stands within <paramref name="tolerance"/> of the anchor: the hall must re-anchor.</summary>
        public static bool AnchorLost(HallPoint anchor, IList<HallPoint> fires, float tolerance)
        {
            foreach (var fire in fires)
                if (Flat(fire, anchor) <= tolerance) return false;
            return true;
        }

        private static float Flat(HallPoint a, HallPoint b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
```

- [ ] **Step 4: Run the tests.** Expected `ALL PASS`.
- [ ] **Step 5: Add it to the csproj, build, commit:** `git commit -m "feat(run): HallJudge - the hall's length and its anchor, pure and tested"`.

### Task 6: the anchor and the hall poll in RunService

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunStorage.cs`, `RunSaveState` (four fields)
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs`: the anchor field, restore and capture, `PollHall()`, setting the anchor when `mq-fire` completes
- Modify: `ICanShowYouTheWorld/Core/Configuration.cs` (`runHallRadius`, default 25)

**Interfaces:**
- Consumes: `HallJudge`, `HallPoint` (Task 5).
- Produces: PlayerState measures `"HallRoof"` (0 or 1) and `"HallLength"` (metres), reported from the slow poll.

- [ ] **Step 1: The save fields.** In `RunSaveState`, after `godWindTold`:

```csharp
        /// <summary>The hall's anchor fire (the hall spec, section 4). Missing from saves before it: no anchor.</summary>
        public bool hallAnchorSet;
        public float hallAnchorX, hallAnchorY, hallAnchorZ;
```

- [ ] **Step 2: Restore and capture,** beside `shipFittingsTold` in both places. Restore: `_hallAnchor = s.hallAnchorSet ? new HallPoint { X = s.hallAnchorX, Y = s.hallAnchorY, Z = s.hallAnchorZ } : (HallPoint?)null;`. Capture: `hallAnchorSet = _hallAnchor.HasValue, hallAnchorX = _hallAnchor?.X ?? 0f, hallAnchorY = _hallAnchor?.Y ?? 0f, hallAnchorZ = _hallAnchor?.Z ?? 0f,`. Declare `private HallPoint? _hallAnchor;`, and clear it wherever a fresh run starts (beside `_fittings` being reset).

- [ ] **Step 3: The config.** `runHallRadius` (float, default 25), with the same three edits as `runHallCallSeconds`.

- [ ] **Step 4: Set the anchor when `mq-fire` completes.** In `OnChallengeCompleted`, add:

```csharp
                if (def.Id == "mq-fire" && !_hallAnchor.HasValue) _hallAnchor = AnchorFromNearestFire(10f);
```

and the helper:

```csharp
        /// <summary>The player's own fires near them, as points.</summary>
        private List<HallPoint> OwnFiresNear(Vector3 centre, float radius)
        {
            var fires = new List<HallPoint>();
            _pieceBuffer.Clear();
            Piece.GetAllPiecesInRadius(centre, radius, _pieceBuffer);
            foreach (var piece in _pieceBuffer)
            {
                if (piece == null || !piece.IsCreator()) continue;
                if (piece.GetComponentInChildren<Fireplace>(true) == null) continue;
                var p = piece.transform.position;
                fires.Add(new HallPoint { X = p.x, Y = p.y, Z = p.z });
            }
            return fires;
        }

        private HallPoint? AnchorFromNearestFire(float maxDistance)
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;
            var p = player.transform.position;
            var anchor = HallJudge.PickAnchor(OwnFiresNear(p, maxDistance), new HallPoint { X = p.x, Y = p.y, Z = p.z }, maxDistance);
            if (anchor.HasValue) Debug.Log($"[ICanShowYouTheWorld] The hall's fire: {anchor.Value.X:0},{anchor.Value.Z:0}.");
            return anchor;
        }
```

- [ ] **Step 5: The poll.** Call `PollHall();` after `PollBuiltPieces();` in the slow poll, and add:

```csharp
        /// <summary>
        /// The hall's measures, for its steps (spec section 4): a roof over the anchor fire, and the hall's
        /// length. With no anchor yet (no fire built, or a save from before the hall) it reports nothing,
        /// and the steps wait with their hints. A lost anchor re-anchors on the next of the player's own
        /// fires among the hall's pieces.
        /// </summary>
        private void PollHall()
        {
            try
            {
                if (_challenges == null) return;
                if (!_hallAnchor.HasValue) _hallAnchor = AnchorFromNearestFire(10f);
                if (!_hallAnchor.HasValue) return;

                var a = _hallAnchor.Value;
                var centre = new Vector3(a.X, a.Y, a.Z);
                float radius = _cfg.RunHallRadius;

                var fires = OwnFiresNear(centre, radius);
                if (HallJudge.AnchorLost(a, fires, 1f))
                {
                    var moved = HallJudge.PickAnchor(fires, a, radius);
                    if (!moved.HasValue) return;
                    _hallAnchor = moved;
                    a = moved.Value;
                    centre = new Vector3(a.X, a.Y, a.Z);
                    Debug.Log($"[ICanShowYouTheWorld] The hall's fire moved to {a.X:0},{a.Z:0}.");
                }

                var points = new List<HallPoint>();
                _pieceBuffer.Clear();
                Piece.GetAllPiecesInRadius(centre, radius, _pieceBuffer);
                foreach (var piece in _pieceBuffer)
                {
                    if (piece == null || !piece.IsCreator()) continue;
                    var p = piece.transform.position;
                    points.Add(new HallPoint { X = p.x, Y = p.y, Z = p.z });
                }

                Cover.GetCoverForPoint(centre + Vector3.up, out float _, out bool underRoof, 0.5f);

                _challenges.ReportMeasure(ChallengeKind.PlayerState, "HallRoof", underRoof ? 1f : 0f);
                _challenges.ReportMeasure(ChallengeKind.PlayerState, "HallLength", HallJudge.Length(points));
            }
            catch (Exception ex) { LogOnce("hall-poll", ex); }
        }
```

The `0.5f` minimum cover is the value the game's own shelter test passes. Confirm at the call site in `Player.UpdateEnvStatusEffects` in an `ikdasm` dump, and use that value if it differs.

- [ ] **Step 6: Build, run the tests, commit:** `git commit -m "feat(run): the hall's anchor fire and its poll - roof and length, re-anchoring when the fire is lost"`.

### Task 7: HEARTH becomes HALL: the label and two steps

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/Unity/RunService.cs`: `TrackTable`, and Act I's chain after `mq-comfort`
- Modify: `docs/SAGA-WALKTHROUGH.md` (Act I) and `docs/superpowers/specs/2026-08-27-story-bible.md` (the hall's beginning)

- [ ] **Step 1: The label only:** in `TrackTable`, `(HearthTrackId, "HALL"),`. The id stays `"hearth"`. Saves restore by id.

- [ ] **Step 2: Two steps after `mq-comfort`** (on the same track, `Track = HearthTrackId`). The text names no seat and no god:

```csharp
            new ChallengeDefinition
            {
                // The hall's first section (spec section 3): the long fire. The anchor is the fire from
                // mq-fire; Cover is the game's own shelter test.
                Id = "hall-roof", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState, Param = "HallRoof",
                Target = 1, Display = "A roof that closes over the long fire",
                Hint = "Build over the fire you first lit, until no sky shows above it.",
            },
            new ChallengeDefinition
            {
                Id = "hall-length", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState, Param = "HallLength",
                Target = 12, Display = "A hall twelve paces long",
                Hint = "Lengthen the house around the fire. Floor, wall or beam - whatever stands counts.",
            },
```

- [ ] **Step 3: Build, run the tests, commit.** Check the run-start log for `Duplicate questline step ids` (there must be none). Commit: `git commit -m "feat(run): HEARTH becomes HALL - the long fire's roof and length (the hall's slice 1)"`.

- [ ] **Step 4: Manual checks** (in the slice 1 HANDOFF task):
  - A fresh run: build a fire, and `Player.log` says "The hall's fire".
  - Roof it: "A roof that closes over the long fire" completes.
  - Lengthen it to 12 m: the length step completes.
  - Destroy the fire and build another inside: "The hall's fire moved".
  - **Resume a save made before this build:** no exception, and the anchor is taken from the nearest fire.

### Task 8: build, stage, deploy, document

- [ ] **Step 1: The build loop** as in Task 4, with the milestone tag `saga/hall-long-fire`.
- [ ] **Step 2: Docs.** Add a RESUME bullet, and a HANDOFF task with Task 7's manual checks. In CLAUDE.md's saga paragraph, one line: the hall's measures are PlayerState params `HallRoof` and `HallLength`, reported by `RunService.PollHall`.

---

## After these slices

Slices 2–4 of the spec (Acts II–VI with the gods' places and powers; Act VII with Odin and the horn;
the call at Fader) get their own plans once slices 0 and 1 have been played. Slice 0's verdict decides
slice 4's cast, and slice 1 shows whether the hall's checking feels right before it carries six gods.
