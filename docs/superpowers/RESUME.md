# Resuming Run Mode work

Written 2026-08-23, restructured and last updated 2026-10-06 at `1.0.17-run.2026-10-06f`. This is the
"pick it back up without re-deriving anything" page: where the work stands, the loop it moves in, the
landmines, and the decisions not to re-open. Its history (the dated status bullets, the alpha era, the
"Done" write-ups) moved verbatim to [`RESUME-history.md`](RESUME-history.md) on 2026-10-06.

The design lives in [specs/](specs/2026-08-16-run-mode-design.md), the hard-won
lessons in [build notes](2026-08-16-run-mode-build-notes.md), the Windows
channel in [`../../HANDOFF_WINDOWS.md`](../../HANDOFF_WINDOWS.md).

## To start a session

Open Claude Code in the repo and say something like:

> Read `docs/superpowers/RESUME.md`. We're continuing Run Mode. Here's what I
> found in play: …

Everything below is what that file tells it.

If the owner has been through the home test plan, start from what it brought back: the self-check
block first, then the numbered items that did not match.

## Where things stand

**2026-10-06, `1.0.17-run.2026-10-06f` (dev)**, on `feature/run-mode`: not merged, deliberately, since
the mode is still being tuned in play. Valheim 1.0.17, Unity 6000.0.75.

- **Seven acts, all built and voiced, and the saga ends at Fader** (the `runFinalBossKey` default since
  `...06d`; a config that already names a boss keeps it, so delete a `defeated_goblinking` line). **The
  Deep North is the epilogue after Fader** (decision #3, reaffirmed 2026-10-06): Act VIII in the code,
  with the game's real names and a stand-in story, reached only with the key moved past Fader. Told
  [as played](../SAGA-WALKTHROUGH.md), [as myth](../THE-SAGA.md), and in the
  [story bible](specs/2026-08-27-story-bible.md), which wins.
- **The charred one stays for his last line after a won saga** (`...06f`; the owner: "Someone might find
  him"). His spot is saved on the character until the line is heard (`Afterword`, `TickAfterword`).
- **Nothing built on 2026-10-05 or 2026-10-06 has been played.** The owner tests from the
  [home test plan](2026-10-06-home-test-plan.md); the per-build checks are the TASKs atop `HANDOFF_WINDOWS.md`.
- **First read of any build: `grep "Saga self-check" Player.log`** (OK / FALLBACK / MISSING, worst
  first). Its first reading found Haldor asking for a trophy no troll drops (fixed in `...06c`), so since
  `...06d` a line, **Quest items can be got**, checks that everything the saga asks for can be had.
- **Machines:** the Mac is current (`...06f`, laptop keys). Windows needs a **full** install for 1.0.17.
  The Steam Deck has an old patched assembly.
- **Written up where it lives:** laptop keys and clickable cards (CLAUDE.md); the hall
  ([spec](specs/2026-10-06-the-hall-design.md), [slices 0–1 plan](plans/2026-10-06-the-hall-slices-0-1.md));
  Valheim Smith (private repo `zrrbite/valheim-smith`); the [overview of 2026-10-05/06](2026-10-06-overview.md);
  the game [catalogue](../catalog/).
- **Open:**
  1. **Heavy UI cleanup.** The run window is too big. Owner's idea: TAB shows it beside the inventory,
     hidden otherwise.
  2. **The heat curve is untuned** (since alpha17; see "Waiting on a human").
  3. **The Steam Deck** needs download, patch and upload before it is used again.
  4. **The thane's Teach phase is dead code** since rungs are taught where the god falls
     (`TeachClassRung`). Remove it after the home test.
  5. **The hall** waits for the home test; slice 0's verdict shapes slice 4.
  6. **Valheim Smith's BepInEx test on the Mac** waits for the owner. The Mac already allows BepInEx's
     loader, and BepInEx can run side by side with the saga.
- **Pages:** [the Saga Atlas](https://claude.ai/artifact/8EvSbu7GH5SQ1Fq9Md83ca) (regenerate with
  `python3 Scripts/saga_atlas.py`, then REPUBLISH THE HTML TO THAT SAME URL; each act's status and the
  version are hand-written and go stale) and [the Storm-Anvil test path](https://claude.ai/artifact/XdbeDYL6m7f2t5MioVmqJ9).

## The loop

Every build goes the same way, and every build is tagged: the gold `SAGA v<build>` line under the main
menu's version is the only proof of what is being played. Versions are date-based,
`<game version>-run.<YYYY-MM-DD>[letter]` (CLAUDE.md, "Version management").

```bash
# 1. Commit the change. Then commit its docs: RESUME, CLAUDE.md where keys or rules moved, and a
#    TASK at the top of HANDOFF_WINDOWS.md saying what to look for in this build.
# 2. Tag the version on that docs commit, then write it into Version.cs (the dev flavour).
git tag "$(bash Scripts/nextversion.sh)"
bash Scripts/setversion.sh
# 3. Build and test.
msbuild Valheim.sln -p:Configuration=Debug -v:minimal   # must be clean
bash Tests/run_tests.sh                                  # must say ALL PASS
# 4. Stage the Windows kit. It refuses if the built DLL's version is not the newest tag.
bash Scripts/stage_windows.sh
git checkout -- dist/windows/patcher/Patcher.exe        # the Mac's recompiled Patcher is not staged
# 5. Commit the build: Version.cs and the staged DLL.
git add ICanShowYouTheWorld/Assets/Version.cs dist/windows/patcher/ICanShowYouTheWorld.dll
git commit -m "build: <version> (dev) — <what changed>; staged, deployed on the Mac"
# 6. A major build gets an annotated milestone tag on that build: commit.
git tag -a saga/<name> -m "<version>: <what changed>"
# 7. Push the branch and both tags.
git push origin feature/run-mode && git push origin <version> saga/<name>
# 8. Deploy on the Mac. It re-signs the bundle (CLAUDE.md, "macOS").
bash Scripts/deploy_local.sh
```

Then launch: the badge must read `SAGA v<version> · DEV`, and the first run's self-check must have no
new MISSING line.

**Order matters:** tag before `setversion.sh`, and stage after building, or `stage_windows.sh` refuses.
`setversion.sh --saga-only` bakes the saga-only flavour for a release, and `Scripts/make_release.sh`
refuses to package a dev build (CLAUDE.md, "Build flavours").

**Going back.** `git tag -l 'saga/*' -n1` lists the milestones; `saga/before-2026-10-05` is the last
build the owner played. The `saga/` prefix keeps them out of the version scripts, which read only tags
starting with a digit. On Windows: `git checkout saga/<name>`, then `.\dist\windows\Install-Mod.ps1
-ModOnly`. On the Mac: check out, build, `bash Scripts/deploy_local.sh`.

**On Windows:** `git pull`, then `.\dist\windows\Install-Mod.ps1`. `-ModOnly` is the fast path when only
the mod changed, and it refuses an assembly patched by an older Patcher (it looks for the
`ICSYTW_EntryPoint_FejdStartup_Start` stamp). **Run the full install whenever the Patcher changed or
Steam replaced the game assembly**, which every Valheim update does. There is no popup on success; one
appears only if the mod failed to load.

**After a Valheim update:** `bash Scripts/check_refs.sh` first, then the recipe in CLAUDE.md ("Update
Scenarios").

## Waiting on a human

None of these are blocked on code — they are blocked on someone playing.

*(2026-10-06: these are the alpha era's open play verdicts, still unanswered as far as this page
knows; their numbers predate Acts III–VII's voices. The closed ones, the alpha-era "Still unverified
in play" list and "the note above" that item 7 points at are in [`RESUME-history.md`](RESUME-history.md).
Windfall carries three charges since `...-19c`.)*

1. **Repair the other worlds.** Builds before alpha17 wrote world-modifier
   rates as bare multipliers into keys Valheim reads as percentages, and
   restored untouched keys as `1` (= 1%). Any world that finished or abandoned
   a run under alpha1-16 is **permanently degraded — one wood per tree, in and
   out of Run Mode**. alpha17+ repairs a world when a run both starts and ends
   on it, but only that world. Start and abandon one run on each save that
   matters (Deck and Windows have their own).
2. **Re-tune heat.** Its enemy scaling was divided by 100 for the mode's entire
   life, so the curve has never once been felt as designed. Needs a real answer
   to "at what heat does it stop being fun". `runHeatEnemyDamageWeight` and
   `runHeatEnemyLevelUpWeight` are config, so it is a number, not a rebuild.
3. **`runHudMenuOffset`** (default 470) — how far the HUD slides left when the
   crafting window opens. Resolution- and UI-scale-dependent.

*(4 is closed: [`RESUME-history.md`](RESUME-history.md).)*

5. **Lifecycle scenarios**, never proven in play: death mid-run must NOT log
   suspend/resume; logging out must suspend; switching world or character must
   not carry a run across.
6. **Build detection**, never seen running: do the fire/cooking/bed/chest steps
   tick within a second of placing the piece, and does "Open 8 doors" stay out
   of the pool until a door exists? `runBuildScanRadius` (default 20) is the
   knob if detection needs the player to stand implausibly close.
7. **Is Windfall too strong?** See the note above — it doubles food. This is the
   first build where anyone can answer.
8. **Tracker colours**, reworked in alpha27: a species can now change colour when
   another walks into range and claims the slot it preferred. Better than boar
   and deer both reading white, but worth a verdict.

*(9 is closed: [`RESUME-history.md`](RESUME-history.md).)*

10. **The boat gate.** A boat quest must NEVER appear on a world where water is
    not in play — and equally, if a run sails a lot and still never sees one, the
    Ocean gate is too tight. Both directions are worth a report.
11. **Is the saga's heat curve survivable?** 44 questline heat by the Plains,
    ×3.2 enemy damage, on a model that has never been tuned. Config, not code.

12. **The stash**, never used in play: does "Deposit materials" take the right
    things (materials only — food and arrows stay on you, on purpose), and do its
    contents survive a suspend/resume?
13. **Eikthyr's Herd**, never seen: do starred deer make the hunt better or just
    slower? Do contested kills fire too often (`runDeerGreylingChance`)? Does the
    Herald appear, announce itself, and — critically — is it impossible to finish
    its step by killing an ordinary deer? Is there lightning, or does the log say
    no effect prefab resolved?

## Landmines

### Building on the Mac (learned 2026-10-05)

The Mac had not built this branch since August; four things stood in the way, all fixed or
documented now:

- **The mod compiles against `Patcher/bin/Debug/patched/assembly_valheim.dll`**, which is not in
  git. Make it from the repo's vanilla `libraries/assembly_valheim.dll` with the bundled patcher,
  resolving dependencies from the Mac install (the repo's `libraries/` lacks
  `SoftReferenceableAssets`):
  `mono dist/windows/patcher/Patcher.exe libraries/assembly_valheim.dll Patcher/bin/Debug/patched/assembly_valheim.dll dist/windows/patcher/ICanShowYouTheWorld.dll "<Mac Managed folder>"`
- **The Patcher project needs `packages/Mono.Cecil.0.11.4/lib/net40/`** (no nuget here): copy
  `dist/windows/patcher/Mono.Cecil*.dll` into it. Also untracked.
- **Tests:** `Tests/run_tests.sh` now prefers Mono's Roslyn `csc` over `mcs`, which cannot parse
  foreach deconstruction (the tests are written on Windows against Roslyn).
- **`deploy_local.sh` retakes its vanilla backup** whenever the installed assembly is unpatched.
  It used to keep the first one forever, so after a Steam update the version guard compared
  against the old game and refused. The 0.221.12 backup is kept beside it as `.vanilla.0.221.12`.
- Scripts are not all executable on the Mac: run them as `bash Scripts/x.sh`.

### Game facts the Acts II–VII reviews paid for (2026-10-05)

Each of these was a stall or a spoiler that read fine and failed in play. Check the IL (Cecil) before
trusting a name or a field's meaning; the self-check above catches the names, not these:

- **Tamed is an enemy of every monster.** A tamed speaker is hunted by whatever passes. Every speaker
  is immune (`SagaSpeaker.MakeImmune`); the drowned one lifts it for his last beat (`Unshield`).
- **`DamageModifiers.m_nonPlayer` is a damage TYPE**, not "damage from non-players". Setting it
  immune changes nothing.
- **`WorldGenerator.GetBiome` calls the boiling sea AshLands.** "In the Ashlands" needs dry ground
  too: not swimming, not in water, not on a ship.
- **Hammer build previews carry a `Fireplace` with no valid `ZNetView`.** Skip invalid views per
  object in any fire scan, or one preview throws the whole scan away.
- **A `StolenLights` release is a pickable item.** Freeing a light with it refunded the payment; the
  freed light is `RisingLight`, a visual only.
- **`StepPredicates.StepDone` reads only the CURRENT act's chains.** Anything that must outlive its
  act (Stormsworn recipes, the FORGE page's anvil shapes) asks `RecipeStepDone`.
- **The FORGE page lists only what the saga has told.** Bench recipes once Hugin announces them,
  anvil shapes once `AnvilTaughtBy` says their step opened, never a repair. A new anvil shape with no
  teacher stays hidden; give it one.
- **Light rises only in the dark** is a story-bible rule the code has to keep: the drowned one's
  light and the lantern-keeper's freeing wait for night.

### The ones that each cost a build

The full list with reasoning is in the [build notes](2026-08-16-run-mode-build-notes.md).
The five that have each cost a build:

- **World-modifier rates are PERCENTAGES.** `Game.UpdateWorldRates` divides by
  100. Write through `WorldModifiers.SetRate`, never `SetGlobalKey` directly.
- **The legacy/service split.** `CheatCommands` statics are ticked every frame;
  the DI services' `HandlePeriodic` has zero call sites. An effect must ride the
  pipeline that actually runs.
- **Unity's destroyed-object equality.** A destroyed object compares `== null`.
  Cached Unity references need `ReferenceEquals`.
- **Two lenders on one value will corrupt each other** unless the pristine value is
  held ONCE PER VALUE and everything is recomputed from it. Hearty and Glass Cannon
  both write `m_baseHP`, and per-lender snapshots let the second record the first's
  boosted value as "original" — leaving the player permanently altered after the
  run. The same correction was needed three times in one day (weapon damage, damage
  modifiers, player fields), so the arithmetic now lives in the tested
  `LoanLedger`. If a new effect shares a value with an existing one, use it.
- **Anything a run grants must be given back**, and given back *correctly* —
  skills return what was LENT (subtract the loan), not the pre-run level, or the
  run confiscates what the player earned.
- **A StatDelta step needs a BASELINE before it can measure anything**, and a step
  without one is skipped silently, forever. alpha32 shipped with ten dead steps —
  the whole StatDelta half of the CRAFT track, "Craft an axe" included — because
  the baseline sync kept its own copy of "the actives plus the questline" and was
  not updated when one questline became two. It now shares `MeasuredChallenges()`
  with the polls so the two cannot disagree, and an un-baselined step logs loudly.
  **The general rule: two copies of the same enumeration will drift.**
- **Asset names cannot be verified at BUILD time** — but since alpha27 they are
  verified at RUN time. `ValidateAssetNames` resolves every creature, item and
  reward name against ZNetScene and ObjectDB at run start and logs the failures,
  so a wrong name is now loud on first launch instead of a quest that silently
  never completes. **It came back clean on alpha33 across all five acts**, which
  retired a blocker that had been open for weeks. The preference order is
  unchanged and still right — a `PlayerStatType` or a compiled component beats a
  name — but a name is no longer a gamble you only settle in play.

### Three more, from the 2026-10-05 builds

- **Never put a collider under a ship.** Its trigger events reach `Ship.OnTriggerExit`, which throws
  the player overboard. The helm's second key has no collider of its own for that reason (`Shipwright`).
- **The god's wind reads the altar off the map's pins**, every few seconds until it finds one, rather
  than remembering it from the moment the saga pinned it: the saga pins once, at the discovery step,
  and a resumed run never pins again (`RunService.ActAltarOnMap`).
- **The myth's `##` headings must stay the act titles.** `SagaTale.Split` cuts `docs/THE-SAGA.md` on
  them, and a title with no tale is reported at run start, never guessed. The Deep North's missing tale
  is the self-check's one expected FALLBACK.

### Standing rules from the alpha era

Their reasoning moved to [`RESUME-history.md`](RESUME-history.md) ("What the mode is, as of alpha43").
The rules still hold:

- **A guided campaign, not a speedrun** (owner, 2026-08-23). Heat multiplies the score while time only
  divides it. Do not re-add speedrun pressure without a decision to reverse this; the levers are
  `runParTimeMinutes` and `runHeatScoreWeight`, both config.
- **The chain asks for destinations; the pool asks for transport.** No boat step in a questline chain.
- **A build category may appear in ONE act only** (`ValidateActs`), because `_builtSeen` latches for
  the whole run.
- **Boss spoils are food** (owner, alpha25): reward the effect, leave the activity optional.

### Do not free the cursor for the mod's windows (tried, reverted 2026-09-20)

`ModCursor` held `GameCamera.m_mouseCapture` false while a mod window was open, which is the state
vanilla F1 puts the game in, so the pointer appeared and the windows were clickable without TAB.
Reverted within the hour: with the pointer free, **drawing the bow drags the windows around**. The
mouse buttons are how you shoot AND how you move an IMGUI window, and a free cursor puts both on the
same click.

TAB works because Valheim ALSO stops taking player input while the inventory is up
(`Player.TakeInput()` consults `InventoryGui.IsVisible()` and friends). The mod can claim the cursor
half of that and not the input half, and the cursor half on its own is worse than nothing. Faking the
input half means making the game believe one of its own panels is open, which is not ours to do.

If it is ever wanted again, the only honest route is a mode the player opts into explicitly - not a
thing that happens because a window is up.

### Two shapes worth remembering from 2026-09-20

**Do not read state in the frame you wrote it.** Three times in two days, and the third time it cost
a working feature: the dev clock's "+2h" was replaced wholesale when only its READOUT was broken, and
the replacement ("wind forward until the game says night") failed to the mirror image of the same lag -
60 net-seconds a frame is an hour of game time a frame, so it blew through its own three-day cap in two
seconds of real time while EnvMan's smoothed fraction was still catching up with the first step.
Reverted at the owner's request. **When a readout is wrong, fix the readout, not the feature.** `CreatureDressing` lost its scale
and colour because it wrote materials before `LevelEffects.Start` ran; the dev clock key reported
"still light" twenty-six times because it read `EnvMan.s_isNight` in the same frame as its
`SetNetTime`, and EnvMan only recomputes that in `FixedUpdate` from a fraction it lerps toward. Both
fixes were the same: span frames, and ask the game whether it has arrived.

**A help line that is WRONG is worse than none.** The dev banner, and then the dev clock's message.
In both cases the tester trusted it and concluded a working feature was broken. Where a key or a
number is shown to a person, generate it from the thing that reads it - `BoonKeys`,
`RunService.DevKeyHelp`, the GM page's table from `CommandRegistry.All`.

## Verify claims against the IL, not memory

Most real bugs in this project were found by reading Valheim's own code. Dump it
once per session:

```bash
ikdasm libraries/assembly_valheim.dll > /tmp/valheim.il
grep -n "SomeMethod() cil managed" -A 30 /tmp/valheim.il
```

That is how the percentage bug, `Player.UseStamina`'s stamina multiplier,
`ZDOMan.DestroyZDO`'s ownership check, and the `Sleep`/`TimeInBase` stat
increments were all confirmed. If a change depends on what the game does, read
it there first.

### Read the IL before building the workaround

Twice on 2026-09-20 the feature already existed in the game and the plan was to build over the top
of it.

**Alphabetical crafting** was going to be a filter and a text field laid over `InventoryGui`.
`UpdateRecipeList` turned out to read a player key, `sortcraft`, parse it as
`InventoryGui.SortMethod` (`Original|Name|Type|Weight|Count`) and sort its own list. One key write
replaced the whole plan. See `CraftingSort`.

**Freeing the mouse** was going to mean driving `ZCursor` every frame and fighting
`GameCamera.UpdateMouseCapture` for it. That method turned out to key off one bool,
`m_mouseCapture` - the same bool **vanilla F1 toggles**. Setting it is entering a state the game
ships rather than inventing one. See `ModCursor`.

The habit that paid: before writing anything that reaches into the game's UI, dump the method with
Cecil and look for the setting.

## Four decisions taken 2026-09-20 (owner, asked directly)

Recorded here because each one closes off alternatives that would otherwise be re-proposed by
anybody reading the issues cold. Each is also written up on its GitHub issue.

**1. Act I's light economy stays exactly even** (#7). Seven rescued lights are needed and seven
are available; the Gatherer's freed hoard is the only slack. A player who loses lights to the
forest may finish the act with ONE storm item, and that is the design rather than a balance bug.
Rejected: raising the race target, cheapening an item, scaling the hoard to what was lost.

**2. No AssetBundle pipeline. Reuse what ships** (#15). This is the standing answer, not a
deferral. Every saga asset so far is an existing prefab wearing different numbers - the Stormward
is a flametal tower shield, the rescued light is a Wisp, `Tutorial.instance.m_ravenPrefab` is the
raven - and it has worked every time at no toolchain cost, in a repo where a Valheim update
already means re-patch and rebuild. What it closes off: the quest book as an in-world mesh, custom
creature looks beyond the four shader properties `CreatureDressing` can already shift, item tints,
and any new model. **The parked quest-book idea below is therefore parked for good** in its
asset form; the tracker IS the book.

**3. The Deep North is an epilogue, not Act VIII** (#14). Act VII is now the ending - the hall in
the Ashlands, the feast, Fader (#17, #13) - and a ninth boss after a climax reads as an
afterthought. Its premise also duplicates Act III's marsh. One beat, reachable only after Fader.
The docs should stop counting to eight: seven acts and an epilogue. The `FrozenKing` names still
want validating, because the startup placeholder warning is a claim about the world.

> **Reaffirmed 2026-10-06 (owner).** The Deep North is the epilogue after Fader, and since `...06d`
> Fader is the default final boss, so the seven acts are reached without editing the config. The code
> still carries the epilogue as Act VIII (the act table, `DeepNorthChain`) with a stand-in story,
> reached only with `runFinalBossKey` moved past Fader. Its names are no longer guesses: they were read
> out of the game's own data on 2026-10-06, and the self-check's "Act VIII's god" line checks them on
> every launch. The atlas and the bible's act table still show eight rows.

**4. The feast: empty high seats by default, and a god who comes if you earned it** (#17). One
named seat per felled god, empty unless something specific was done in that god's own act. The
empty-seat path must be complete and winnable on its own, because it is the version most players
get. Build order: the shade's seat first, then the five empty named seats, then ONE god as an
experiment - a boss fighting for you needs its faction flipped and can look ridiculous.

## Parked ideas

- **A quest book instead of the tracker panel** (owner, 2026-09-12, prompted
  by the story *bible*): an in-world asset — a book the player opens — showing
  the saga's progress, in place of the panel on the right. Also the natural
  home for a quest's HINT if hints should be found rather than shown (the Act I
  item quest's hint placement is undecided for exactly this reason). Needs the
  asset pipeline decision `CreatureDressing` was waiting on: a book is a
  mesh and a UI, and both want an AssetBundle built in Unity 6000.0.x.
  **Settled 2026-09-20: there will be no AssetBundle pipeline** (see the decisions
  above), so the tracker is the book permanently. What is still open is whether the
  BOOK page's *presentation* can carry more of the feeling - that costs nothing.

### Parked: search at the crafting bench

Asked 2026-09-20 and PARKED the same day, because alphabetical sorting turned out to be most of what
it was for (see above). Worth building only if the owner asks again.

It is UI surgery on Valheim's own crafting panel, and breaking the crafting window is far worse than
not having search in it. The research is done and every member exists: `InventoryGui.m_availableRecipes`
(private list), `m_recipeListRoot`, `m_recipeListSpace`, `m_recipeListBaseSize`, `m_recipeElementPrefab`.
Filtering therefore means hiding the elements that do not match and RE-STACKING the rest by index,
every time the game rebuilds the list - the game positions them by index times `m_recipeListSpace`, so
hidden ones leave gaps otherwise.

The part not to guess at is the INPUT. An IMGUI text field competing with Valheim's own keyboard
handling, over a panel the player is actively using. Valheim's `TakeInput` is false while the inventory
is open, which is the reason to think letters will not reach the game - but "is the reason to think"
is not "was play-tested", and this one has to be.

So: its own build, its own play-test, and behind `runDevMode` for the first outing.
