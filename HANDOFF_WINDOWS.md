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
  — human-readable guide to reading it: [`dist/windows/CHECKING-THE-LOG.md`](dist/windows/CHECKING-THE-LOG.md)
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
**v0.221.12-run.alpha25** (alpha6 = alpha5's log fix + the new Pugilist boon:
melee attacks cost zero stamina; bows/crossbows unaffected). Then: activate the ember boon (Keypad5), confirm
the ring appears and disappears after ~30s, and confirm the log stays small
(`(Get-Item Player.log).Length` after a few minutes — expect KB, not MB) and
contains no "Tag: Tree is not defined." Still outstanding from before:
death mid-run (no suspend/resume lines) and logout suspend. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha8 (pace + quests + ability bar)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
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

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
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

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
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

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
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

---

## 2026-08-20 — TASK: alpha15 — boon descriptions in the offer

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
Martin's note: "it's not apparent what the skills do" — the offer showed
only a name and passive/active. Each of the 13 boons now carries a
one-line `Description` rendered under its name in the offer panel, and the
panel grew (460x200) to fit three columns of wrapped text.

Watch for: any of the three columns clipping its text at the panel's
bottom edge, or the panel overflowing at a non-default UI scale — screenshot
if so and note your scale. The wording is a claim about behaviour, so also
flag any description that doesn't match what the boon actually does in play
(e.g. Waystone says "next boss altar, one charge"). Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha16 — Packbrother replaces Packleader

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin's call: Packleader (buff your tames) is dead in a run — you never
reach a tame before the clock matters. It is GONE, replaced by
**Packbrother**: an active on **Keypad 7**, 4-minute cooldown, that summons
a tamed wolf which follows and fights for you. Two alive at a time; a third
summon replaces the oldest. One star per boss felled (capped at two stars).

The summon is loaned like everything else: its ZDO is marked
non-persistent, so it is never written into the world save, and it is
dismissed when the run ends, when the boon is lost to death, and on suspend.

Watch for, in priority order:
1. **Any wolf that survives the run.** Abandon a run with two wolves alive,
   then check they are gone; repeat with the wolves left behind in an
   unloaded zone (summon, run 200m away, abandon) and go back to look.
   Then quit to menu, reload the world, and confirm no wolves.
2. **The prefab name.** "Wolf" is asset data and cannot be verified outside
   the game; if the popup says `Missing prefab: Wolf`, report it — that is
   the whole boon dead and it needs a different name.
3. Does the wolf actually engage hostiles and follow you? Does it survive
   long enough to matter?
4. Known and deliberate: **your wolf's kills count toward kill contracts.**
   Say whether that feels good or cheap in play.

Also in this build: an existing run saved under alpha15 or earlier that is
still HOLDING Packleader will silently lose that boon slot on resume (the
engine drops boon ids it no longer knows). Expected, not a bug to report.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha17 — THE PERCENTAGE FIX (this one matters most)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

**Root cause found for "I chop a tree and get 1 wood" and "we run out of
stamina way too fast".** Valheim reads its world-modifier rate keys as
PERCENTAGES — `Game.UpdateWorldRates` does `rate = stored / 100` (verified in
the IL). This mod wrote bare multipliers, so every empowerment was silently
inverted into a crippling penalty:

| we asked for | the game actually applied |
|---|---|
| resources x3 | x0.03 (Ceil floored every drop to 1) |
| stamina regen x1.5 | x0.015 (stamina never came back) |
| skill gain x3 | x0.03 |
| heat's enemy damage | x0.01-0.02 — heat made enemies WEAKER |

Worse, restoring an untouched key wrote back "1" — i.e. 1%. **Any world that
has finished or abandoned a run under a previous build is sitting on
resourcerate=1 permanently, in and out of Run Mode.** alpha17 detects a stored
rate below 5% as damage from an earlier build and clears the key at run end,
so starting and ending one run on an affected world repairs it.

**Expect the difficulty to change sharply.** Heat now genuinely scales enemy
damage for the first time, and movement stamina now genuinely costs (it was
effectively free at 0.005x). Baseline is resources x3, skill x3, move stamina
x0.5, stamina regen x2.5, all stamina costs x0.75.

Also in this build: questline reordered (axe → hammer → workbench →
greydwarves → deer → Eikthyr; the deer step pays Eikthyr's 2 summoning
trophies); the axe step grants WoodCutting/Axes/Bows at 25 and WoodCutting no
longer starts at 100; three skill boons (Woodsman/Hunter/Warrior); a standing
repeatable task ("Heed Hugin 5 times") that pays a boon every time it fills;
50 wood + 20 stone granted on every boss kill.

Watch for, in priority order:
1. **Wood per tree.** Fell a beech: expect roughly 3x vanilla, NOT 1.
2. **Whether an old world repairs itself.** On a world you have run before,
   start a run and abandon it, then check wood drops OUTSIDE Run Mode. The log
   will say "was N% — treated as damage from an earlier build".
3. **Is heat now too punishing?** Report the heat level where it stops being
   fun — that number is a config change, not a rebuild.
4. Stamina: still too tight, or now too generous?
5. Skill levels after a run ENDS: Axes/Bows/WoodCutting must not be lower than
   before the run started. The run gives back only what it lent.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha18 — Meadows questline steps, no duplicate boon offers

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Both from Martin's alpha17 play-test ("the pacing feels better"):

1. **Two more starter-zone steps before the greydwarves**, and armor now
   arrives a piece at a time instead of all at once:
   workbench → leather tunic; **Hunt 5 Boar** → leather leggings + 50 wood
   arrows; **Kill 4 Necks** → wooden shield + 20 flint arrows; greydwarves →
   helmet + cape + 30 flint arrows. Act I is now: axe → hammer → workbench →
   boar → necks → greydwarves → deer (Eikthyr's trophies) → Eikthyr.
2. **A boon you already hold is never offered again.** Held passives were
   already excluded; the four ACTIVES were not, which is what made offers
   repeat. Waystone's charges came from re-picking it, so it now gains a
   charge on every boss kill instead.

Watch for:
- `ShieldWood` is the one item name in this build that has never been granted
  before — if the shield doesn't arrive after the Necks step, check the log for
  a grant error and report the line.
- Boar and Neck counters ticking (both prefab names are already used elsewhere
  in the pool, so they should be safe).
- Offers: three DISTINCT boons you don't already hold, every time.
- With the pool at 16 and duplicates excluded, a long run can run the pool dry.
  If an offer ever shows fewer than three options, say so — that is the signal
  the pool needs more boons rather than a bug.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha19 — the greydwarf step was in the wrong biome

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin: greydwarves live in the Black Forest, so that questline step sent the
player out of the starter zone before the Meadows boss. It is now **Kill 6
Greylings** (the weaker meadows cousin, hence 6 rather than 4). Same step ID,
so a run part-way through it keeps its progress — kills already banked count
toward the new target.

Act I is now entirely Meadows-doable: axe → hammer → workbench → 5 Boar →
4 Necks → 6 Greylings → 3 Deer (Eikthyr's trophies) → Eikthyr. Greydwarves
remain in the RANDOM pool, correctly gated to the Black Forest.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha20 — shelter and rest before Eikthyr

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Two steps added between the greylings and the deer hunt:

- **Raise a roof (15 pieces)** → 100 wood + 50 stone to finish it.
- **Sleep through the night** → a hot meal and 20 flint arrows.

Both ride stats the game keeps itself — `Builds` counts every piece placed,
`Sleep` counts every night slept — so neither can silently stall the chain the
way a check against a named building piece would. Sleeping is the real test of
a shelter (roof, fire, bed, no monsters at the door) and it puts the player at
the boss in daylight.

Act I is now: axe → hammer → workbench → 5 Boar → 4 Necks → 6 Greylings →
roof → sleep → 3 Deer (Eikthyr's trophies) → Eikthyr.

Watch for: **`CookedMeat`** is the one unverified item name here (the shelter
step pays wood and stone, both proven). If no meat arrives after sleeping,
grab the grant error from the log. Also worth reporting: whether 15 pieces is
the right size for "a shelter" or feels like busywork.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha21 — HUD geometry + Hunter's Eye boon

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin reported the run HUD was cramped and scrolled BOTH ways, and that some
held boons showed their "passive" tag with **no name beside it**. Same root
cause: the task and boon rows were wider than the 340px window, and when a
horizontal group overflows, GUILayout squeezes the flexible parts to zero — a
word-wrapping label squeezed to zero renders as nothing at all.

Fixed by sizing every row against a declared `HudContentWidth` (window minus
padding minus scrollbar), widening the HUD to 420, scaling its height to the
game window (360-720 instead of a fixed 480), giving the boon name and status
columns explicit widths, and turning the horizontal scrollbar off outright so
a future overflow shows up as wrapping rather than sideways dragging.

New boon: **Hunter's Eye** (passive) — a panel on the LEFT listing every
creature within 70m by distance, with a health bar, capped at 10 rows. It is
the GM mode's Tracking window earned as a boon. Pure observation, so there is
nothing to unwind when it is lost.

Watch for:
- Any row still clipping or scrolling sideways, at whatever UI scale you use.
- Held boon names all present now (that was the invisible-headline bug).
- Hunter's Eye: does the left panel collide with anything at your resolution?

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha22 — the Necks step is now about building

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

"Kill 4 Necks" is gone. Rather than drop a building step into its slot and
leave two of them back to back, the act was reordered so building, living and
resting run in sequence:

axe → hammer → workbench → 5 Boar → **Raise a roof (15 pieces)** → 6 Greylings
→ **Settle in (2 min at home)** → Sleep through the night → 3 Deer → Eikthyr.

**Settle in** measures `TimeInBase`, which Valheim accrues half a second at a
time and ONLY while `Player.IsSafeInHome` — a check that runs through
`GetBaseValue`, so it needs real comfort (roof and fire), not just walls. It
therefore cannot be completed before the roof step, which is why the roof moved
ahead of it rather than the new step landing in the Necks slot.

The Necks step's reward (wooden shield + 20 flint arrows) moved to Settle in.

Watch for: whether "Settle in" ticks up at all once you have a fire and a roof
— if the counter sits at 0/120 while you are clearly at home, report it and I
will switch the measure. Two minutes should pass while you are crafting anyway;
say so if it turns into standing around.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha23 — tracker polish, and the HUD stands aside

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

All four from Martin's alpha21/22 play-test:

1. **The tracker no longer sits on the top-left readout.** It moved down the
   left edge (30% of window height), clear of health/stamina/food.
2. **Each species has its own colour**, keyed on `Character.m_name` (the shared
   localization token, so level stars and "(Clone)" don't split a species).
   The colour is what the eye tracks when rows re-sort by distance.
3. **The bar is now distance, not health** — full at the edge of the eye's
   reach, draining to empty as something arrives, so it reads as a countdown.
   Health moved to a percentage at the end of the row.
4. **The run HUD and tracker hide while the inventory/crafting window or the
   map is open** (`InventoryGui.IsVisible` / `Minimap.IsOpen`, both public
   statics). The timer strip stays: it is a thin line along the top edge that
   nothing else uses. Standing aside beats moving — there is no spot free of
   both the crafting panel and the game's own readouts.

Watch for: the HUD coming BACK when you close the crafting window (if it ever
stays hidden, that is the important bug here, and `End` still toggles it);
species colours holding steady as things move; and whether the tracker's new
position clashes with anything at your resolution.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha24 — five fixes from the alpha23 play-test

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

1. **The HUD slides left instead of hiding** when the crafting window or map
   opens, so both stay readable AND the HUD's buttons stay clickable (hiding
   put reroll/abandon out of reach). The shift is a config number,
   `runHudMenuOffset`, default 470 — edit the JSON, restart, no rebuild, since
   the right value depends on resolution and UI scale.
2. **Mining tasks are Tier 2 now**, i.e. after the first boss. They were Tier 1,
   and MaxTier is `defeatedBosses + 1`, so Tier 1 is drawable from minute one —
   asking for mining before the antler pickaxe exists.
3. **Necks are gone from the random pool too.** The questline step went in
   alpha22; this was the other place they were still being asked for.
4. **Hunter's Eye is baseline, not a boon.** Always on, out of the offer pool,
   and listed in the HUD's BOONS section as "always on" alongside Pugilist.
5. **The tracker moved to the BOTTOM-LEFT**, off the hotbar and the top-left
   readouts. It is a draggable window and keeps a dragged position until the
   game window changes size — so if the corner is still wrong, drag it and tell
   me where you put it.

Watch for: the HUD landing somewhere sensible with the crafting window open at
your resolution (report the overlap and I will change the one number), and the
tracker not clashing with the hotbar.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha25 — smaller roof, food instead of timber

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

1. **The roof step wants 6 pieces, not 15.**
2. **The map hides the run window again**; the crafting window still gets the
   slide-left treatment. Nothing on the HUD is worth reading over the map, and
   unlike the crafting bench there is nothing to click there either.
3. **Boss kills now pay FOOD for the tier just cleared** instead of timber and
   stone, and the questline's material rewards are cut roughly to a third
   (axe 50→25 wood, hammer 100→40, roof 100→40). Those numbers were set while
   the 3x resource rate was silently inert, so they were compensating for a bug
   rather than balancing a reward.

Boss food by bosses felled: Eikthyr → cooked meat + honey; Elder → sausages +
carrot soup; Bonemass → turnip stew + serpent stew; Moder → wolf skewers +
onion soup; Yagluth → lox pie + blood pudding.

Watch for: **every one of those food names is unverified** (Unity asset data,
invisible from the assembly). A wrong one logs an error in GrantItem and grants
nothing — the run is unharmed, but tell me which line appears. Eikthyr's tier
is the only one you will reach soon; the rest can wait for a longer run.

Also worth a verdict: 6 pieces — still busywork, or about right?

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha26 — fire, bed, chest; the door task waits for a door

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha26**.

Act I is 13 steps now, up from 10. The three new ones are homestead beats, each
sitting immediately before the step that already secretly needed it:

```
 5 Raise a roof (6)     →   6 BUILD A FIRE   →   7 Kill 6 Greylings
 8 Settle in (2 min)    →   9 BUILD A BED    →  10 Sleep through the night
                          11 BUILD A CHEST   →  12 Hunt 3 Deer  →  13 Eikthyr
```

"Settle in" never accrued without a fire and "Sleep" never worked without a bed
— those prerequisites were real all along, just invisible. Now they are steps.

1. **Do the three new steps detect correctly?** They look for a piece *you*
   built carrying a `Fireplace` / `Bed` / `Container` component, within 20m of
   you, checked once a second. A campfire, any bed, any chest should each tick
   its step within about a second of being placed. If one sits at 0/1 while the
   thing is plainly standing in front of you, say which — and try walking closer
   before you call it (20m is `runBuildScanRadius` in the config, hot-editable).

2. **"Open 8 doors" should not appear until you have built a door.** It used to
   be drawable from minute one, when you have no hammer. Build a door, and it
   becomes eligible for the next refill — it will not appear instantly, since it
   still has to win a random draw.

3. **Two new item names, both unverified** (asset data, invisible from the
   assembly): `Flint` and `Resin`, in the fire and bed rewards. A wrong one logs
   loudly in `GrantItem` and grants nothing. Tell me if either line appears.
   `DeerHide`, `Wood` and `ArrowFlint` in those same rewards are already proven.

4. **Does the bed step strand you?** It lands at #9 but deer hide normally comes
   at #12, so the fire step at #6 hands over 6 hides to cover it. If the bed
   still sends you hunting, the hide count is wrong.

5. **Act I now pays 13 heat instead of 10** — enemies hit ~1.65× instead of
   ~1.50× by the time you reach Eikthyr, before random tasks. This is the first
   build where that curve is worth an opinion. Does the boss fight feel
   meaningfully hotter, or is it lost in the noise?

Known and accepted, not a bug: start a run standing in a base you already built
and the fire/bed/chest steps complete immediately, rewards and all. The mode is
built for a fresh start.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha27 — tracker colours, cooking, Windfall, name validator

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha27**.

**1. Check the log FIRST, before playing.** This build validates every creature,
item and reward name against the game's own registries at run start. Start a run,
then search the log for `[ICanShowYouTheWorld] Unknown`. Anything it prints is a
real bug I can fix in one line — **please paste those lines back verbatim**. It
should finally settle `ShieldWood`, `CookedMeat`, `Flint`, `Resin`, `RawMeat`,
`$item_cookedmeat` and all nine boss foods without you having to reach them.
Silence means every name in the pool resolves.

**2. Tracker colours.** Boar and deer both read white because the hash put them
on the same slot — and greyling, neck, greydwarf, troll and crow were ALL sharing
one colour. Palette is 10 now and no two species on screen can share. Expected
oddity: a species can change colour when another walks into range and takes the
slot it wanted. Tell me if that reads as worse than the collision did.

**3. Act I is 14 steps** — a cooking station after the fire (step 7), paying 8
raw meat to cook on it.

**4. Windfall** — new boon, **Keypad 8**, one charge, never refills. Doubles
every stack you carry. Two things to judge:
   - Does it fire at all, and does overflow drop at your feet rather than vanish?
   - **Is it too strong?** It doubles food too, and food is your health and
     stamina bar. Filling your pack with cooked meals and then pressing it is the
     obvious exploit. You picked everything-stackable over materials-only knowing
     that — this is the run where you find out. One word changes it.

**5. New multi-objective quests**: Hearth and Home, Provisions, Fill the Larder,
Meadow Cull, Night Watch. Provisions and Fill the Larder only appear once you own
a cooking station.

**6. Act I heat floor is 14 now** (was 10 two builds ago). Enemies hit ~1.70x by
Eikthyr before any random task.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha28 — the saga gets acts

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha28**.

Straight from your verdict. "Act 1 complete and then nothing" was literally what
the code did — a hardcoded string, and no Act II behind it.

**1. The quest panel is now headed with the act**: `ACT I — THE MEADOWS`. When
Eikthyr falls you should get a banner announcement — **ACT II — THE BLACK
FOREST** — and the questline should immediately seat its first step (mine the
Black Forest). That transition is the single most important thing to confirm.

**2. Act II is written in full**, seven steps: mine → build a smelter → forge
three bronze things → 10 greydwarves → 3 brutes → a troll → The Elder. The troll
step hands over the three Ancient Seeds his altar wants, so you never farm for
them — same principle as the deer trophies.

**3. Acts III, IV and V exist but are THIN** — three or four steps each. That is
on purpose: enough that no boss is a dead end, not enough to be a real design. If
you get that far, tell me what those acts should actually be about.

**4. Boon list says "Activated"** instead of "Keypad 8" overflowing the column.
The key is still on the strip above as `[8]`.

**5. If you resume your existing run and Eikthyr is already dead, it will jump
straight into Act II.** That is correct — the act is derived from the world, not
the save — but it will look abrupt the first time. Not a bug.

**6. Check the log again for `[ICanShowYouTheWorld] Unknown`.** Act II–V added a
lot of new names (`gd_king`, `Draugr`, `Blob`, `Hatchling`, `Dragon`, `Lox`,
`GoblinBrute`, plus every new reward item). The validator checks ALL acts at run
start, so one launch in Act I tells us whether Act V is sound. This is the single
most useful thing you can paste back.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha29 — the acts get filled in, and boats behave

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha29**.

Acts III–V were placeholders in alpha28. Now every act is 7–9 real steps with a
building beat and mob beats of its own.

**1. `[ICanShowYouTheWorld] Unknown` in the log — still the first thing to check,
and this build has the most new names yet.** Every creature and item across all
five acts is validated at run start. `Leech`, `StoneGolem`, `Fenring`,
`Deathsquito`, `Windmill`, `Thistle`, `SurtlingCore`, `MeadFrostResist`,
`ArrowNeedle`, `CapeLox` and about twenty more are unproven. One launch in Act I
checks all of them. Paste back anything it names.

**2. Each act now opens with an arrival step** — "Reach the Black Forest",
"Reach the Swamp" and so on. Should tick the moment you set foot in the biome.

**3. Boats: you should NEVER see a boat quest unless water is genuinely in play.**
They are pool-only and gated on having been on the Ocean biome *and* owning a
boat. If a boat quest shows up on a world where you have never sailed, that is a
bug worth reporting. If you sail a lot and never see one, tell me that too — the
Ocean gate is deliberately conservative and may be too tight.

**4. Build steps per act**: smelter + portal (Act II), fermenter (Act III),
windmill (Act V). **The Mountains have no build step on purpose** — Valheim has no
distinctively mountain-built piece with its own class, and I would rather admit
that than invent filler. A category can only be used by one act, because the
built-piece latch runs for the whole run.

**5. The fermenter step is a hint, not decoration** — poison resistance mead is
the Bonemass fight, so Act III makes you build the thing that brews it.

**6. Act II now has a portal step.** Combined with the stash (still unbuilt),
watch whether travel stops mattering entirely — that would be too much.

**7. Total questline heat is now 44** across the saga — roughly **×3.2 enemy
damage by the Plains** before any random task. That is much steeper than anything
played so far. If the Plains feel impossible rather than hard, the weights are
config and it is a number, not a rebuild.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha30 — Eikthyr's Herd, and the stash

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha30**.

The two things deferred twice so the acts got played first.

**1. The stash — press `End`, it is the top section.** "Deposit materials" empties
every unequipped **material** into it (not food, not arrows, not gear — those
stay on you deliberately, a button that stashed your dinner mid-fight would be a
trap). "Take" per row pulls a kind back. It follows you anywhere, survives a
logout, and is meant to make moving house between acts painless. Things to judge:
   - Is materials-only the right filter, or do you want food/arrows in there too?
   - Does it survive a suspend/resume with contents intact?

**2. Deer in Act I are Eikthyr's now.** Roughly half the deer you meet get a star
— visibly bigger, several times the health, faster. One arrow will not do it any
more. **Deer still cannot hurt you** — they run the game's passive animal AI,
which has no attack at all, and giving them one is asset work I cannot do. So the
hunt got harder to *catch* rather than dangerous.

**3. Killing a deer may draw greylings** to the carcass (about 1 in 3). That is
where the danger comes from. If it fires too often and hunting becomes a chore,
`runDeerGreylingChance` is config.

**4. There may be lightning** when a deer dies — pure flavour, and the one thing
here riding an unverifiable prefab name. It tries several candidates and stays
silent if none exist, so **no lightning is not a bug**, just an unlucky guess.
Tell me if you see it, and tell me if the log says "no lightning effect prefab
resolved".

**5. New Act I step 14: "Hunt Eikthyr's Herald"** — a named two-star deer that
spawns near you when that step comes up, between the deer hunt and Eikthyr. It
should announce itself. **Killing an ordinary deer must NOT complete it** — it is
matched by identity, not by species. If a normal deer finishes that step, that is
a real bug. If the Herald never appears, also a bug: it is meant to re-spawn
whenever the step is current and none is standing.

**6. Act I is 15 steps now**, questline heat 45 across the saga.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha31 — the stash gets its own window

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha31**.

Small one, straight from your note that the stash cluttered the Run window.

**The stash is now its own panel, bottom-left, immediately right of the tracker.**
It is up whenever a run is, it is draggable, and the list **scrolls** — it can hold
128 kinds, so it had to. The header and the "Deposit materials" button stay put
outside the scroll, so the one control you always want never scrolls away.

The Run window is back to what it was before alpha30: timer, heat, score,
questline, splits, tasks, boons.

Worth a look: at your resolution, does the stash window sit clear of both the
tracker and the HUD? Both windows remember where you drag them until the game
window resizes.

Everything from alpha26-30 is still in this build and still unplayed — the log
grep for `[ICanShowYouTheWorld] Unknown` remains the highest-value minute you can
spend.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha32 — two questlines, and heat becomes a dial

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha32**.

Your idea, and your line about it — *"the good thing about dual paths is that you
can decide if you want the heat"* — turned out to be the whole design, so I built
it that way.

**1. The quest panel now shows TWO rows: HUNT and CRAFT.** They advance
independently. Every act's kills are on one, everything else on the other:

```
ACT I — THE MEADOWS
  HUNT   Hunt 5 Boar          2/5
  CRAFT  Build a cooking station   0/1
         Reward: Meat to cook on it
```

**2. This is the difficulty dial.** Every questline step pays heat, so working
both tracks makes you stronger AND hotter; running the hunt track straight to the
boss keeps you cool, poorer and lower-scoring. **That is the thing to judge in
play: does that trade feel like a real decision, or is one path obviously right?**

**3. The act still ends when the boss dies** — the boss is the last hunt step. If
your craft track is unfinished when the boss falls, those steps are gone. That is
the cost of rushing and it is intentional; tell me if it feels punitive rather
than like a choice.

**4. Act IV's craft track is only two steps** (arrive, mine silver). The Mountains
have no distinctive building, so that act just offers less optional heat. Say if
it reads as thin rather than as a lull.

**5. Your existing save will migrate.** It has one questline position from before
the split; it gets looked up across both tracks and seats whichever owns it, with
the other starting at that act's beginning. The log says
`Migrated a pre-track save: '<step>' resumed on the <TRACK> track`. **If you see
that line, check the other track looks sane rather than half-done.**

**6. Watch the flashes.** Each row flashes gold on its OWN advance. If completing
a kill also flashes the craft row, that's a bug.

### RESULTS (Windows side appends here)

**"Craft an axe didn't register."** Confirmed and fixed in alpha33 — see below.
**Do not play alpha32**; the entire CRAFT track was stalled at its first step.

---

## 2026-08-22 — TASK: alpha33 — fixes the dead CRAFT track

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha33**.

**alpha32 was broken and this is the fix. Skip alpha32.**

You found it on the first step: "Craft an axe" never registered. It was not just
that step — **ten steps across the saga could never register**, and all of them
were on the CRAFT track:

```
Act I    mq-axe, mq-hammer, mq-bench, mq-shelter, mq-home, mq-rest
Act II   bf-copper, bf-bronze
Act III  sw-iron
Act IV   mt-silver
```

What happened: those steps measure a lifetime player stat as a DELTA, so each
needs a "zero point" snapshot taken when it's dealt. When I split one questline
into two in alpha32, I updated the code that *reads* those stats to walk both
tracks, but not the code that *takes the snapshot* — it still only did the first
track, which is HUNT. So every CRAFT step of that kind sat un-baselined and was
silently skipped forever. The build/arrive steps were fine, which is why the track
looked alive rather than obviously dead.

The real cause was two copies of "walk the actives and the questlines" that drifted
apart. They're now one shared enumeration, so they cannot disagree again.

**Also added: this class of failure is now loud.** If a stat-based questline step
ever sits without its zero point, the log says so and names the step:

```
Questline step 'mq-axe' on the CRAFT track has no stat baseline —
it can never register progress. This is a bug in the mod, not the save.
```

**What to check:** start a run, craft an axe, and watch the CRAFT row tick to 1/1.
Then just play — every step that was dead should now count. And the usual grep,
which will now also catch this if I ever do it again.

### RESULTS (Windows side appends here)

**Confirmed working.** Axe registered; the `Unknown` grep came back empty, which
closed the asset-name blocker for all five acts in one launch.

---

## 2026-08-22 — TASK: alpha34 — the boon pool gets teeth

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha34**.

Straight from your note that the stamina boons felt lacklustre. You said three —
it was **five** (Enduring, Vigorous, Cat's Breath, Marathoner, Acrobat), all
competing with a baseline that already gives stamina ×0.5 cost and ×2.5 regen.
That's why they felt flat.

**1. They're now one boon: `Tireless`** — max stamina, faster recovery, cheaper
dodges. The four freed slots went on new categories. Pool is 22.

**2. Ten new boons, four categories the pool had none of:**

| | |
|---|---|
| **Irongut / Coldblooded / Fire-blooded** | Resistant to poison / frost / fire |
| **Bloodthirst / Relentless** | Kills heal you / restore stamina |
| **Glass Cannon** | +40% damage, **−30% max health** |
| **Reckless** | +50% damage, **you take 25% more** |
| **Slow Burn** | Heat rises 25% slower |
| **Forge-fed** | Weapons hit harder the hotter the run |

**3. Boons can now have downsides** — Glass Cannon and Reckless. Both say so in
their description. Tell me if a cost ever surprises you; that would be a bug in
the wording, not the boon.

**4. Resistances won't be offered early** — Irongut needs 1 boss down, the other
two need 2. Frost resistance in the Meadows would waste one of your three options.
**If you ever see a resistance offered in Act I, that's a bug.**

**5. Forge-fed is the one to watch.** Its damage moves with heat, which nothing
else in the mode does. It should get stronger as the run heats up and weaker after
a death drops your heat. If it ever feels like it ratchets up and never comes
down, say so — that's the failure mode I designed against.

**6. Sharpened now also covers weapons you craft AFTER taking it.** It didn't
before; that was a quiet flaw found while making three damage boons coexist.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha35 — your four Act I/II notes, and a bug they surfaced

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha35**.
(The installer now prints the version it actually installed, so that line is
always right from here on.)

All four of your notes, plus something they exposed.

**1. Bed before "settle in".** You were right — the CRAFT track really did ask you
to settle into a home you had no bed for. Now `…fire → cook → bed → settle in →
sleep → chest`.

**2. Homeward.** Every boss kill grants a charge; **Keypad 9** returns you to your
claimed bed. Charges accumulate and persist. Waystone (Keypad 6) got you *to* the
altar; this is the leg home, which was the one gap left after the stash removed
the hauling. If you have no bed claimed it refuses and says so rather than
spending the charge — **tell me if it ever strands you somewhere unexpected.**

**3. The smelter was worse than "too soon".** It needs **surtling cores**, which
come from burial chambers, and the chain didn't hand any over until the *portal*
step — two steps later. So it was quietly sending you crypt-hunting. The mining
step before it now pays 8 cores, so the smelter is buildable the moment it's
asked for.

**4. Every completion now pays +2 max health**, quest step or random task alike,
shown in the HUD as "+N health earned". Act I should reach Eikthyr around +40.
It's a loan like everything else — it goes away when the run ends.

**Armor isn't in it, and can't be:** Valheim computes armor from equipped items,
and the only damage-modifier steps it has are far too coarse for "a tiny bit". So
you got the health half, done properly, rather than a fudge.

**5. What your request surfaced:** `Hearty` (+15 health) and `Glass Cannon`
(−7.5 health) **both** write the same field, and the old mechanism gave each its
own idea of "the original". Hold both and the second records the first's boosted
value as pristine — then whichever ends first restores a number that was never
original, leaving you permanently altered *after the run*. That shipped in
alpha34. Adding a third claimant would have made it worse, so it's rebuilt: one
pristine value per field, everything recomputed from it. Third time today I've
had to make that same correction, so it's now a tested class of its own.

**Worth checking in play:** take Hearty and Glass Cannon together, finish a run,
and confirm your health is back to normal afterwards.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha36 — acts have to be earned now

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha36**.

Your note: *"you just get handed every step without any work. Is it possible to
add a discovery step before a mini boss and boss?"*

**1. Every act now ends: … → FIND THE ALTAR → kill the boss.** You have to
actually reach the boss's altar (within 30m) before the kill step appears. The
world always generates one, so it can never be unfindable — but you do have to go
looking.

Two things worth knowing:
   - **Waystone skips it.** It teleports you to the next undefeated altar, which
     completes the discovery. That's deliberate; spending a boon to skip travel
     was your own call.
   - Each discovery step pays that boss's summoning items and the right mead, so
     arriving is when you get handed what the fight needs.

**2. The Herald is a real hunt now.** It used to spawn 24m away — you turned round
and it was there. Now it's **150–250m out**, and you get a direction: announced on
spawn, and a live "Tracks lead north-east, 140m" line under the step. Hunter's Eye
picks it up at 70m for the last stretch.

**If the bearing ever stops updating or points somewhere wrong, that's a bug** —
it should go null rather than go stale.

**3. Quest hints.** Steps whose requirements aren't obvious now carry a line saying
what they need — 18 of them. Both times you lost time in play were "I didn't know
what this needed", so:

```
Build a smelter
  ▓▓░░░░  0/1
  Stone, and surtling cores from the burial chambers.
  Reward: More ore than it can hold
```

**Tell me if any hint is wrong.** I wrote them from knowledge of the game, not
from anything the assembly could confirm — a wrong hint is worse than none.

**4. The quest panel is getting tall** — two tracks, each up to five lines. I'd
rather you see it than have me trim it blind. If it crowds the timer, say so.

**5. Heat is 50 across the saga now** (was 45).

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha37 — the altar pins, corrected

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha37**.

You asked: *"will the altar be visible on the map though?"* It was — **all five**,
pinned at run start, since long before discovery steps existed. Which made
alpha36's discovery step a walk to a dot you were handed in minute one, i.e.
exactly the "handed without any work" it was meant to fix.

**Now only the current act's altar is pinned**, appearing as that act begins. Kill
Eikthyr and the Elder's altar shows up with the ACT II banner.

Pinning nothing at all was the other option and I rejected it: vanilla hands out
Vegvisirs precisely because searching a biome blind is miserable, and a Plains
altar can be a very long way from where its act starts.

**What to check:** at run start you should see ONE boss pin, not five. After each
boss falls, the next act's altar should appear within a second or so.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha38 — it's a saga, and now it says so

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha38**.

Your call: *"less of a speed-run-mod and more of a more complete Valheim
experience… let's lean into the saga mode."*

Worth knowing the scoring already agreed with you before either of us said it:
score is `par/(par+time) × (1 + heat×0.1)`, so heat MULTIPLIES and time only
divides. A 3-hour thorough saga scores about double a 1-hour thin one. The mode
stopped rewarding speed a while back; only the presentation still said otherwise.

**1. The HUD leads with the ACT, not the clock:**

```
SAGA — ACT I
THE MEADOWS
  2:14:07              Heat 8.5
  Saga score 310
  +40 health earned    Homeward x2 [9]
  QUESTS
    HUNT   …
    CRAFT  …
```

The act line also used to appear twice; now once.

**2. "Begin the saga" / "Abandon the saga"**, and the messages say saga too.

**3. Nothing about balance changed** — same formula, same numbers. This is
presentation catching up with what the mode already was, so there's nothing new to
play-test beyond "does it read right".

**What I want your eye on:** with the timer demoted, does the HUD still tell you
what you need at a glance mid-fight? The clock is now the same size as heat.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha39 — the Herald was never where the bearing said

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha39**.

You weren't failing to find it. **There were dozens of them.**

alpha36 moved the Herald's spawn to 150–250m, which put it outside the loaded area
— so the game culled it and released its (non-persistent) record, which made the
mod think none was standing, which made it **spawn another one, once a second, at
a new random spot.** The bearing only appeared in the instants where a freshly
spawned one still existed. That's your "sometimes I get hints".

**Three fixes:**

**1. The run now remembers a PLACE, not a creature.** When the hunt begins it picks
a spot 150–250m out and keeps it. The Herald only materialises when you get within
60m of that spot. No respawn loop, and the ground never moves under you — including
across a save and resume.

**2. The bearing is on the always-on strip now**, not just inside the run window —
*"The Herald's tracks lead north-east, 140m"*, updating as you walk. Previously it
only drew inside the quest panel, so while actually playing there was nothing to
follow. That's the "more frequent hints" you asked for, as a standing line rather
than a message every thirty seconds.

**3. The boss marker is gated on the hunt.** Your request: no Eikthyr pin until the
Herald is dead. Generalised — **an act's altar is pinned when its discovery step
becomes current**, so the map is never ahead of the questline. At run start there
should now be **no boss pin at all**.

The vanilla rune stone near spawn still works if you read it. That's you choosing
to skip the mystery, and I left it alone deliberately — destroying a world object
is the one thing this mode can't give back.

**What to check:** start the Herald step, walk the bearing, and confirm you find
exactly ONE Herald where it said. If you ever see two, or the bearing jumps
somewhere new, the fix didn't take.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha40 — Eikthyr was on the wrong questline

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha40**.

**1. You were right about "Kill Eikthyr" under CRAFT.** What you saw was *"Find
Eikthyr's altar"* sitting at the end of the crafting questline — the discovery step
was on the wrong track.

The split routes steps by their KIND (kills → HUNT, everything else → CRAFT), and a
discovery step isn't a kill, so it quietly fell into CRAFT. I designed it as "HUNT
track, immediately before the boss" and the automatic routing put it elsewhere
without saying anything. Steps can now name their track explicitly, and the five
discovery steps do.

The validator also gained a check for this exact shape — the old one only asked
"does HUNT end on the boss", which stayed true while the step before it went
missing. An invariant that only looks at the last item can't see an absent one.

**2. The bottom-left panels moved right**, clear of the health/food readout.
`runSidePanelX` in the config (default 320) if it's still not right for your
resolution — both panels are draggable too, and remember where you put them until
the window resizes.

**What to check:** the CRAFT track should now end at "Build a chest", and HUNT
should read `… → Hunt the Herald → Find Eikthyr's altar → Defeat Eikthyr`.

### RESULTS (Windows side appends here)

*(pending)*
