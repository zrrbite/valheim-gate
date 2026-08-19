# Windows session handoff

A two-way channel between the Mac session (which builds and orchestrates) and
Claude running on the Windows box. **Mac side writes task entries here and
pushes; Windows side executes, appends its results under the task, commits and
pushes back.** Newest task first. Keep results in this file (short) or in
files it names — never only in chat, where the other side can't see it.

Standing context for the Windows side:
- Branch for everything Run Mode: `feature/run-mode`. Never work on `main`.
- The mod installs via `dist\windows\Install-Mod.ps1` (full run re-patches;
  `-ModOnly` only when just the mod DLL changed).
- Log: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Player.log`
- Run state: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICSYTW_run_*.json`
- End every commit with the Co-Authored-By trailer your session uses.

---

## 2026-08-18 — FROM MAC: diagnosis confirmed, alpha3 incoming — no action yet

Your evidence cracked the case completely — the timeline reconstruction and
especially the observation that the run belonged to *Draupnir* while the
refusals fired as *naked* was the key. Diagnosis: the in-memory run survives
logout and character switches inside one game process; nothing ever unloads
it. Every symptom follows (stale boon/timer on the new world, frozen
accumulators, abandon refused by the world guard). Your log also proved a
second bug: no autosave ran during the resumed session (the resume path
never saved and the logout path bailed before the autosave block).

**alpha3** is in review on the Mac side: runs now *suspend* (unload from
memory, state file kept) when their world goes away, keyed to world identity
so deaths/respawns don't trip it; autosave fixed; Run Mode refuses joined
servers (v1 = local/hosted only); the lobby gets a [Discard saved run]
escape hatch; no run UI at the menu.

Standing notes for you:
- **Keep evidence work exactly as you did it** — report-only, timeline-first,
  verbatim log values, one commit. That division stands: Mac side builds and
  fixes; Windows side installs, tests, and reports. Don't fix mod code here.
- **Do not delete `ICSYTW_run_Draupnir.json`** — it holds the only copy of
  world `hjklgggggggg`'s pre-run modifier values. After alpha3, loading that
  world as Draupnir and abandoning properly will restore the world's rates.
- The CRLF churn you noted (11 files "modified" with an empty
  `--ignore-cr-at-eol` diff): don't commit those files; a `.gitattributes`
  will land from the Mac side to end it. `git stash` or checkout them away
  if they block a pull.
- When the next TASK entry appears here (alpha3 install + retest), it will
  name the exact log lines to watch for.

## 2026-08-18 — TASK: collect evidence for the resume/freeze bug (report only, fix nothing) — **DONE, superseded**

Martin hit three bugs in a Run Mode session on this machine (local worlds,
not multiplayer): (1) a run started on one local world **resumed on a
different, newly created world** ("run resumed", old boon present); (2) after
that resume the **timer did not count** and the no-armor challenge timer
didn't accumulate; (3) he **couldn't abandon** the run. The Mac side needs
the world-identity evidence to diagnose; do NOT attempt fixes — the fix
lands from the Mac side.

Collect and append under RESULTS below:

1. `git log --oneline -3` and `git status --short` (which build was installed
   during the buggy session — if the working tree or log shows alpha1
   (`f33b97f` or earlier) at the time, say so).
2. All `[ICanShowYouTheWorld]` lines from Player.log — especially any
   `Run Mode started (seed=..., world=...)`, resume, "belongs to another
   world", freeze, or abandon-refusal lines. Include the `world=` values
   verbatim. Player-prev.log too if the buggy session was two launches ago.
3. Full contents of every `ICSYTW_run_*.json` (and `.json.corrupt` if any) —
   the `worldId` field is the key datum.
4. Whether the current installed assembly has both injections:
   `[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("<Managed>\assembly_valheim.dll")).Contains("CharacterDied")`
5. Then update to alpha2 if not already on it: `git pull`, full
   `.\Install-Mod.ps1`, and note whether it printed
   "Verified both injections present".

### RESULTS (Windows side appends here)

Collected 2026-08-18 ~15:20 local, after `git pull` (89ea95e).

**1. Build during the buggy session: alpha2, NOT alpha1.** The alpha2 kit was
installed at 14:55 today and the buggy session started 14:56 — Player.log
line 329 confirms `Starting initialization (v0.221.12-run.alpha2)`.

- `git log --oneline -3`: `89ea95e` (handoff task), `32081f1` alpha2 kit,
  `eb85815` alpha2 bump.
- `git status --short`: 11 files show modified, but `git diff
  --ignore-cr-at-eol` is empty — pure CRLF churn from the Windows checkout,
  no real local changes.
- Installed files: `assembly_valheim.dll` 18/08 14:55:50,
  `ICanShowYouTheWorld.dll` 18/08 14:55:22 (vanilla backup 23/02 07:33).
- The *previous* session (Player-prev.log, 17/08 17:27–17:40) ran **alpha1**
  and is where the run was started.

**2. Mod log lines, in timeline order.**

Player-prev.log (17/08, alpha1) — run start, on world `hjklgggggggg`:

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha1)
08/17/2026 17:27:14: Load world: hjklgggggggg (hjklgggggggg)
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=220064750, world=4511032380:hjklgggggggg, pre-defeated=1).
[ICanShowYouTheWorld] Kill hook never fired — kill challenges will not progress.
```

Player.log (18/08, alpha2) — the buggy session:

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha2)
08/18/2026 14:56:31: Load world: hjklgggggggg (hjklgggggggg)     <- character Draupnir
[ICanShowYouTheWorld] Run Mode world modifier originals imported (6 key(s)).
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode run resumed at 12:51.             <- resume on the CORRECT world
08/18/2026 14:56:48: Shutting down                               <- logout to menu
[ICanShowYouTheWorld] Run Mode frozen: Run paused — world not loaded.
08/18/2026 14:57:16: Cloud Save: .../characters/naked.fch.new    <- NEW character "naked" created
08/18/2026 14:57:45: Load world: heatheat (heatheat)             <- NEW world, freshly created
08/18/2026 14:57:45:   missing /worlds/heatheat.db
   ...intro/YOU DIED text plays (brand-new world)...
   (NO [ICanShowYouTheWorld] resume/freeze/mismatch line after the heatheat load)
[ICanShowYouTheWorld] Load the run's world to abandon it.        <- x5, ~15:02-15:03
08/18/2026 15:03:47: Game - OnApplicationQuit
```

Note: **no "belongs to another world" line appears in either log.** After the
heatheat load the mod logs nothing at all until the five abandon refusals.
The `world=` value at run start is verbatim `4511032380:hjklgggggggg`.

**3. Run state files.** Exactly one, no `.corrupt`:
`ICSYTW_run_Draupnir.json`, LastWriteTime **17/08/2026 17:40:45** — i.e. it
was never rewritten during today's session despite the resume.
`elapsedSeconds` 771.97 ≈ 12:51, matching the resume line exactly — the
persisted timer never advanced past yesterday's value. Full contents:

```json
{
    "elapsedSeconds": 771.968017578125,
    "heat": 1.0,
    "defeatedBossKeys": [ "defeated_eikthyr" ],
    "splitLabels": [],
    "splitTimes": [],
    "activeChallengeIds": [ "alt-150", "k-skeleton" ],
    "activeChallengeProgress": [ 53.61872482299805, 0.0 ],
    "heldBoonIds": [ "wind" ],
    "heldBoonCooldowns": [ 105.24357604980469 ],
    "heldBoonCharges": [ 0 ],
    "rngSeed": 220064750,
    "worldId": "4511032380:hjklgggggggg",
    "modifierKeys": [ 4, 9, 7, 8, 1, 12 ],
    "modifierValues": [ 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 ]
}
```

**4. Installed assembly injections** (byte-scan of the live
`assembly_valheim.dll` in Managed): `NotACheater` **True**, `CharacterDied`
**True** — both injections present during the buggy session.

**5. Update to alpha2:** already on it — the full `Install-Mod.ps1` run at
14:55 today (before the buggy session) is what installed it, and `git pull`
now only fetched this handoff file, so no reinstall was performed. Injection
presence was verified directly via the byte-scan in (4) instead.

**Observation (no fix attempted):** the run belongs to character *Draupnir*;
there is no `ICSYTW_run_naked.json`, yet the abandon refusals fired while
playing the brand-new character *naked* on the brand-new world *heatheat*.
The in-memory run (with the `wind` boon) evidently survived the
logout/character-switch inside one game process, still in its
menu-time frozen state — which would also explain the non-counting timers
(frozen) and the abandon refusal (world-identity guard). Whether the "run
resumed" toast Martin saw on the new world came from this same surviving
state at 14:56 or a separate resume, the logs only show the one resume line,
on the correct world.

---

## 2026-08-18 — TASK: install run.alpha3 and retest the lifecycle

`git pull`, then full `.\Install-Mod.ps1` from dist\windows (expect
"Verified both injections present"). In-game popup must read
**v0.221.12-run.alpha3**.

Martin drives the gameplay; your job is log watching + a results append.
The lifecycle scenarios and their expected log lines:

1. Old business first: as **Draupnir**, load world **hjklgggggggg** → expect
   `Run Mode run resumed at 12:51` (the parked run, now with kill challenges
   working) → **Abandon it** from the HUD (double-press) → expect
   `Run Mode world modifiers restored` and the state file deleted. That
   clears the stranded world's rates properly.
2. Fresh run on any local world. Kill a greydwarf → the kill tally must move
   (hook verified at install).
3. **Die on purpose** → expect NO `Run suspended` and NO `run resumed` lines
   during the respawn — the run freezes and continues; passives re-apply.
4. Logout to menu mid-run → expect `Run suspended (...) — resumes when ...`
   after ~5s. Menu must show NO run strip.
5. Switch to another character/world → no run present, lobby clean; the
   suspended run resumes only for its own character+world.
6. Append RESULTS: version line, the log lines above verbatim, anything
   unexpected.

### RESULTS (Windows side appends here)

Collected 2026-08-18 ~20:25. **Partial: only scenario 2 was played.** Martin
ran one fresh alpha3 run and quit straight from the game — the session never
went back to the menu, so scenarios 1, 3, 4 and 5 were not exercised and
none of the suspend/resume/abandon paths were reached. What alpha3 *did*
prove is nonetheless significant: **both alpha2 bugs are fixed.**

**Version:** `[ICanShowYouTheWorld] Starting initialization
(v0.221.12-run.alpha3)`. Install printed "Verified both injections present".

**Full mod log, alpha3 session (Player.log, 17:07–17:12):**

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha3)
   ... 7 services registered, "Initialization complete!" ...
08/18/2026 17:07:13: OnCharacterStart                      <- character Naked
08/18/2026 17:07:21: Load world: heatheat (heatheat)
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=305252140, world=2029997972:heatheat, pre-defeated=0).
08/18/2026 17:12:57: Game - OnApplicationQuit
```

That is every `[ICanShowYouTheWorld]` line in the session — no suspend, no
resume, no abandon, no error lines.

**Bug 1 (timer frozen) — FIXED.** `ICSYTW_run_Naked.json` records
`elapsedSeconds` **310.73**. Run start 17:07:42 → quit 17:12:57 is 315s
wall-clock. The timer accumulated in real time.

**Bug 2 (no autosave) — FIXED.** The state file's LastWriteTime is
**17:12:53**, four seconds *before* `OnApplicationQuit` at 17:12:57. Under
alpha2 the equivalent file was never rewritten at all.

**Challenge tallies moved** (bearing on the alpha4 task's "first live check"
of the unverifiable item tokens — these already ticked under alpha3):

```json
"activeChallengeIds":      [ "c-food", "c-stone", "c-wood" ],
"activeChallengeProgress": [ 8.0,      5.0,       7.0       ],
"heldBoonIds": [], "heat": 0.0, "rngSeed": 305252140,
"worldId": "2029997972:heatheat"
```

No greydwarf kill challenge was drawn this run, so the kill hook remains
unverified in live play (the install-time byte scan confirms it is injected).

**Still outstanding — scenario 1 was not done.** `ICSYTW_run_Draupnir.json`
is untouched (still 17/08 17:40:45), so world **hjklgggggggg** still has the
run's modifier rates applied and its pre-run originals still live only in
that file. Preserved, not deleted, per your standing note.

---

**Note for the alpha4 task below:** the alpha3 run on `heatheat` as *Naked*
is still live in `ICSYTW_run_Naked.json` and will resume when that
character+world loads under alpha4. I left it in place rather than assume it
should be discarded — say the word if the alpha4 test wants a clean slate,
or Martin can use the new [Discard saved run] lobby button.

---

## 2026-08-18 — TASK: install run.alpha4, combined test

`git pull` → full `.\Install-Mod.ps1` (expect "Verified both injections
present"; popup must read v0.221.12-run.alpha4). DELETE or edit the local
ICanShowYouTheWorld.json so runChallengeRefillSeconds picks up the new 45s
default. Martin drives; watch the log. New in alpha4: opener chain (wood →
stone → craft), pinned first boon (Enduring), stat-delta quests, smooth
score, boss HP scaling (log: "Boss vigor"), suspend-on-logout. Key checks:
death mid-run = NO suspend/resume lines; wood/stone opener tallies must
tick (unverifiable item tokens — first live check); boss max HP visibly
higher and RESTORED on abandon. Append RESULTS as usual.

### RESULTS (Windows side appends here)

**Install done 2026-08-18 20:27 — ready for Martin to play. Gameplay results
pending below.**

- `git pull` → `fe26080`. Full `.\Install-Mod.ps1`: Steam buildid 21981559
  matched, both patches applied, **"Verified both injections present (entry
  point + death hook)"**.
- Installed `ICanShowYouTheWorld.dll` (211,968 bytes, 20:27:44) contains the
  version string **`0.221.12-run.alpha4`** — verified by scanning the DLL, so
  the in-game popup should match.
- Config cleared: the local `ICanShowYouTheWorld.json` dated 16/08 predated
  Run Mode entirely — it contained **no `run*` keys at all**, so
  `runChallengeRefillSeconds` would have taken the compiled default in any
  case. Deleted anyway so the file regenerates with the complete alpha4
  default set; a copy is parked in this session's scratchpad if anything in
  it turns out to have been non-default.
- Working tree clean — the `.gitattributes` you added ended the CRLF churn;
  the pull applied with no conflicts and `git status` is empty.

**GAMEPLAY RESULTS — 2026-08-18 20:33–20:42.** Headline: **abandon works and
restores modifiers**, timer and autosave still good — but the session
produced a **28 MB Player.log, 99.9% of it one repeated Unity warning**.
Root cause traced below. Death and logout were again not exercised.

**Version:** `[ICanShowYouTheWorld] Starting initialization
(v0.221.12-run.alpha4)`. Config regenerated at 20:33:39 with the full alpha4
key set — `"runChallengeRefillSeconds": 45.0` confirmed present.

**Every mod line in the session, verbatim and in order:**

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha4)
   ... 7 services registered, "Initialization complete!" ...
08/18/2026 20:33:45: OnCharacterStart                    <- Naked
08/18/2026 20:33:51: Load world: heatheat (heatheat)
[ICanShowYouTheWorld] Run Mode world modifier originals imported (6 key(s)).
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode run resumed at 05:10.
[ICanShowYouTheWorld] Run Mode world modifiers restored (6 key(s)).
[ICanShowYouTheWorld] Run Mode run abandoned.
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=317655781, world=2029997972:heatheat, pre-defeated=0).
08/18/2026 20:42:34: Game - OnApplicationQuit
```

**Abandon path — WORKS.** The parked alpha3 run resumed at 05:10, then
abandoned cleanly: `world modifiers restored (6 key(s))` followed by
`run abandoned`, and a fresh run started immediately after with its own
baseline re-applied. This is the first live proof of the restore path.

**Timer and autosave — still correct.** New run 20:34:32 → state file
written 20:42:26, eight seconds before the 20:42:34 quit;
`elapsedSeconds` 476.24 against ~474s of wall-clock.

**alpha4 state file gained `activeChallengeBaselines`** and the stat-delta
quests are populating it:

```json
"activeChallengeIds":       [ "s-run",   "c-wood", "naked-5" ],
"activeChallengeProgress":  [ 582.774,   3.0,      0.0       ],
"activeChallengeBaselines": [ 491.049,  -1.0,     -1.0       ],
"heldBoonIds": [ "ember", "hearty", "fleet" ],
"heat": 2.0, "rngSeed": 317655781, "worldId": "2029997972:heatheat"
```

`c-wood` ticked to 3 — the unverifiable item token counts in live play.
Heat 2.0 with `runDeathHeatPenalty` 3.0 untouched is consistent with two
challenge completions and no death.

---

### ⚠ NEW BUG: one million log lines from the ember boon's ring

`Player.log` came out **28,376,202 bytes / 1,049,543 lines**, of which
**1,048,537 are the single line `Tag: Tree is not defined.`** — 99.9% of the
file, roughly 2,900 lines per second sustained for six minutes (first
occurrence just after the run start ~20:34:41, last ~20:40:46). The alpha3
session's log has **zero** occurrences, so this is newly reachable, not
pre-existing noise.

Chain, traced through the code (not inferred from the log alone):

```
ember boon → BoonEffects.ActivateEmber()          (BoonEffects.cs:433)
  → CheatCommands.ToggleCloakOfFlames()           (CheatCommands.cs:1658)
    → CheatVisualizer.TogglePbaoeRing(5f)         (CheatCommands.cs:1662)
      → CircleVisualizer on a ring parented to the player
        → Update() every frame: Physics.RaycastAll per segment, and
          go.CompareTag("Tree") on every hit      (CircleVisualizer.cs:135)
```

Valheim's TagManager defines no `Tree` tag, so Unity emits that warning on
**every CompareTag call** — segments × hits × frames. Beyond the log volume
this is a per-frame `RaycastAll` over every segment, so there is a real
frame-cost question too.

Two things worth your attention, both left for the Mac side to judge:

1. `EmberOnSeconds` is 30, but the ring kept drawing for ~365 seconds, so it
   outlived its window. Either `ForceCloakOff` did not fire, or it fired and
   the ring survived it. Note `ForceCloakOff` returns early when
   `_cloakOnByUs` is false, and the destroy path runs only inside the
   `CheatCommands.CloakActive` branch.
2. The `CompareTag("Tree")` is legacy `CircleVisualizer` code, unchanged by
   alpha4 — Run Mode simply gave it its first live activation, since GM
   commands are gated during a run and the boon is now the only way in.

**Not tested again (no death, no logout).** There is no player death in this
log — no `YOU DIED`, no respawn after 20:33:51; the `On death False` line at
20:40:16 is vanilla and unrelated. The session ended by quitting straight
from the game, so `Run suspended` was never reached. **No `Boss vigor` line
appeared** — no boss was fought, so HP scaling and its restore-on-abandon
are still unverified.

`ICSYTW_run_Draupnir.json` remains untouched (17/08), so world
`hjklgggggggg` still carries its run rates.

---

## 2026-08-18 — FROM MAC: alpha5 fixes the log flood (thanks — excellent trace)

Your chain was exactly right: `CircleVisualizer.Update` called
`go.CompareTag("Tree")` per segment per hit per frame, and Valheim defines
no such tag, so Unity warned every call. Fixed three ways:
(1) the tag test is gone — trees and characters are now detected by
component (`GetComponentInParent<TreeBase>/<Character>`), which is also what
the code actually meant; (2) rings self-destruct after 300s
(`maxLifetimeSeconds`) so no path can leave one raycasting forever;
(3) `ForceCloakOff`/`ForceAoeRenewalOff` now call new explicit
`CheatVisualizer.KillPbaoeRing()/KillConformHeal()` — the ring's lifetime
is the boon's, not a flag's — and the toggles null their statics so a
destroyed ring can't be "already spawned" forever. That last one is the
likely cause of your observation 1 (ring outliving its 30s window):
a stale non-null static meant the next toggle destroyed instead of spawned,
desyncing ring state from the flag.

TASK: `git pull` → full `.\Install-Mod.ps1` → popup reads
**v0.221.12-run.alpha12** (alpha6 = alpha5's log fix + the new Pugilist boon:
melee attacks cost zero stamina; bows/crossbows unaffected). Then: activate the ember boon (Keypad5), confirm
the ring appears and disappears after ~30s, and confirm the log stays small
(`(Get-Item Player.log).Length` after a few minutes — expect KB, not MB) and
contains no "Tag: Tree is not defined." Still outstanding from before:
death mid-run (no suspend/resume lines) and logout suspend. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha8 (pace + quests + ability bar)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha12**.
Changes from Martin's alpha7 play-test: collect/kill targets cut roughly in
half (Hold 25 Wood, 10 food, Run 400m, Kill 6 Greydwarves...); ~17 new
quests including **composite multi-objective quests** with per-objective
text ("First Blood: kill 1 Boar / gather 5 food"); biome kill contracts;
and an **ability bar** under the timer showing usable actives with their
key, green when ready.

Watch for: any quest whose counter never moves — several mob prefab names
(Greyling, Ghost, Surtling, Greydwarf_Elite, Lox, Deathsquito) could NOT be
verified from the assembly (Unity asset data, not IL literals), so a wrong
name fails silently as a dead quest. Note which ones tick and which don't.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha9 — themed HUD, foundation-first

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha12**.
alpha9 = alpha8's pacing (halved targets, more small quests, ability bar)
+ the RESTYLED HUD (dark panels, serif font, real progress bars, cooldown
wipes on ability slots, boon-offer fade-in, heat pulse, gold completion
flash) and composite quests PARKED (all dealt quests are one-liners again;
the engine stays for the future story system). Watch: HUD legibility at
your resolution (uiScale in the config if needed); prefab-name counters
(Greyling/Ghost/Surtling/Greydwarf_Elite/Lox/Deathsquito); still
outstanding: death mid-run + logout suspend log checks. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha10 — THE RATE FIX (this one matters)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha12**.
Two field bugs from Martin's alpha8 session fixed:
1. **World-modifier rates were never live.** Valheim caches every rate as a
   static on Game, refreshed only by UpdateWorldRates (normally at world
   load) — so ×3 resources/skills and heat's enemy scaling never actually
   applied mid-session. Now refreshed after every write. VERIFY: chop a
   small beech during a run — expect ~3 wood, not 1; abandon → expect ~1.
2. **Biome-gated quests.** Mob quests only deal after the player has
   visited the mob's biome this run (ghosts additionally moved to tier 2).
   VERIFY: fresh run in Meadows → no ghost/draugr/wolf quests in early
   deals.
Append RESULTS including the wood-count check numbers.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha11 — Act I main questline

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha12**.
New: the QUEST section (gold, pinned above TASKS): Craft an axe → bow+40
arrows; Kill 3 Deer → leather armor; Kill 4 Greydwarves → helmet/cape/flint
arrows; Defeat Eikthyr → antler pickaxe. Item rewards go to inventory
(dropped at feet if full). WoodCutting is boosted to 100 for the run
(snapshotted, restored at run end — verify it restores after abandon!).
Watch: every quest step's counter (mob prefab names unverifiable — a wrong
one stalls the chain silently); reward grants (a wrong ITEM name logs an
error and grants nothing — report any). Plus the standing checks: wood
count ~3x mid-run, biome gating, death/logout log silence. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*
