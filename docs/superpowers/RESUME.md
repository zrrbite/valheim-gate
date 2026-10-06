# Resuming Run Mode work

Written 2026-08-23, last updated 2026-10-06 at `1.0.17-run.2026-10-06c`. This is the "pick it back up
without re-deriving anything" page: where the work stands, the loop it moves
in, and the questions that are waiting on a human.

The design lives in [specs/](specs/2026-08-16-run-mode-design.md), the hard-won
lessons in [build notes](2026-08-16-run-mode-build-notes.md), the Windows
channel in [`../../HANDOFF_WINDOWS.md`](../../HANDOFF_WINDOWS.md).

## To start a session

Open Claude Code in the repo and say something like:

> Read `docs/superpowers/RESUME.md`. We're continuing Run Mode. Here's what I
> found in play: …

Everything below is what that file tells it.

## Where things stand

- **Plans ready to build after the home test** (2026-10-06): the hall, slices 0–1
  ([`plans/2026-10-06-the-hall-slices-0-1.md`](plans/2026-10-06-the-hall-slices-0-1.md): an Aspect on the player's
  side, then Act I's long fire), and Valheim Smith's milestones 0–1 (private on GitHub: `zrrbite/valheim-smith`,
  `docs/plans/`; Tasks 5–6 already built on branch `milestone-1-skeleton`). The Mac already allows BepInEx's loader (entitlements `allow-dyld-environment-variables` and
  `disable-library-validation`) and is a universal binary, so Rosetta is a real fallback.
- **The owner's home test plan** for everything from 2026-10-05/06 (setup, a new character, act by act, boats, the
  reward, the MacBook): [`2026-10-06-home-test-plan.md`](2026-10-06-home-test-plan.md). Start the next session from what it brings back.
- **2026-10-06: the first self-check ever read** (the Mac, `...06`): 47 OK, 1 FALLBACK (the Deep North has no
  tale, expected), 1 MISSING, a real Act II stall fixed in `...06c`. Haldor asked for `TrophyForestTroll`, but
  trolls drop only `TrophyFrostTroll`; both items exist and both read "Troll Trophy". **Lesson:** an item that
  exists is not an item anyone can get. The drop-table check caught what the name check passed.
- **2026-10-06: a laptop layout, clickable cards, and the lobby after the intro** (`...06b`,
  `saga/laptop-keys`). The owner's MacBook could not choose boons: no numpad, and no Mac has `Insert`.
  `runKeyLayout: "laptop"` moves every saga key onto letters Valheim leaves free (pure `KeyLayout`, tested
  against 1.0.17's own bindings; see CLAUDE.md's key section). Every card also takes TAB and a click, through
  the same `RunService.ChooseFromCard` the keys use. Player keys now respect `Player.TakeInput`, so typing in
  chat fires nothing. And the lobby no longer opens over a new character's Valkyrie intro (`LobbyOffer`,
  gated on `Player.InCutscene`). The Mac's config is set to `laptop` (backup `.bak-2026-10-06`).
- **2026-10-06: the game is 1.0.17** (Unity still 6000.0.75), with no source change: `bash Scripts/check_refs.sh`
  found every reference resolving. Rebuilt as `1.0.17-run.2026-10-06`, which is the `...06` build under the new
  prefix, deployed on the Mac. **Windows needs a FULL install** (top TASK in `HANDOFF_WINDOWS.md`). The Deck is
  further behind than ever.
- **The one-page overview of 2026-10-05/06** (what got built, the story, the boats, the new ideas and
  where each stands): [`2026-10-06-overview.md`](2026-10-06-overview.md).
- **2026-10-06: the Deep North has its real names** (`...06`, `saga/deep-north-names`, staged and
  deployed on the Mac). This is now the build to test: everything below about `...05q` still holds, since
  `...06` contains it. Its checks are the top TASK in `HANDOFF_WINDOWS.md`.
  - **How the names were found.** The Mac's own `Player.log` "Boss registry" lines gave the altar
    (`offeraltar_FrozenKing_bossroom`, boss `FrozenKing`, empty bowl key) and the location `DN_Bossroom`.
    The key is asset data, so 1.0.16's bundles were unpacked with the new `Scripts/unpack_bundle.py`
    (pure Python; the bundles are LZ4, so a raw grep finds only fragments).
  - **The Frozen King is fought in three forms.** `FrozenKing` (chained, spawned by the altar) sets
    `defeated_frozenking`. `FrozenKing_p2` summons the seven earlier gods (`FrozenKing_P2_Summon_Eikthyr`
    … `_Fader`). `FrozenKing_p3` sets **`defeated_frozenking_p3`**, the key the Deep North's raids stop on
    and the form the all-bosses achievement names. **Act VIII ends on the third form**: the first form's
    key would end the act mid-fight. A Vegvisir (`$hud_pin_dnboss`) pins `DN_Bossroom`. The same data
    confirms the Queen's and Fader's keys.
  - **In the code.** The boss table, Act VIII and `DeepNorthChain` use the real names, the
    `SagaNames` stand-ins are gone, and `Placeholder` is off, so the validator checks the act (the flag
    is kept for the next biome). Two new self-check lines: **"Act N's god"** checks that the creature the
    boss step kills sets the act's own key (`SagaSelfCheck.GodsKey`, tested), and **"The gods' altars"**
    checks that every altar is a known location. The Boss registry line now prints `boss sets=`.
  - **Still open.** Whether `FrozenKing_p3` is a spawnable prefab whose kill is reported under that
    name: the first launch's self-check says so, and the act ends on the key either way. The STORY is
    still a stand-in (chapter, two steps), and decision #3 below (the Deep North as an epilogue after
    Fader, not Act VIII) is still unresolved: the code and the docs disagree. That decision is the
    owner's.
- **Nothing built on 2026-10-05 has been played** (`...05f` to `...05q`: Acts III–VII voiced, the saga's
  reward, the self-check, ship fittings, the winds). The owner tests at home from one ordered plan in
  their todo repo (`~/Development/todo/TODO.md`, section "Valheim: The Saga"); the per-build checks are
  the TASKs in `HANDOFF_WINDOWS.md`. **Start from what that test brings back** (self-check block,
  errors, feel of fitting prices, the 40° cone, the horn's 2 and 10 minutes) before Act VIII.
- **Going back (owner, 2026-10-05): every major build gets a named, annotated milestone tag** on its
  `build:` commit (the one carrying the staged Windows DLL), so any of them can be checked out and
  installed as-is. `git tag -l 'saga/*' -n1` lists them. Today's: `saga/before-2026-10-05` (the last
  build the owner played — undo the whole day here), `saga/rungs-at-the-kill`, `saga/act2-voices` …
  `saga/act6-voices`. The `saga/` prefix keeps them out of the version scripts, which only read tags
  starting with a digit. To go back on Windows: `git checkout saga/act4-voices`, then
  `.\dist\windows\Install-Mod.ps1 -ModOnly`; on the Mac, check out, build, `Scripts/deploy_local.sh`.
- **First thing on any new build: `grep "Saga self-check" Player.log`** (since `...05n`). One block
  at run start, a line per name the saga guesses (speaker bodies and places, traders, Hildir's chests,
  item looks, flametal, plains foods, every quest name), OK / FALLBACK / MISSING, worst first. MISSING
  is a step that cannot finish; FALLBACK is a guess to correct. `RunService.LogSelfCheck`, pure
  `SagaSelfCheck`. A new guessed name belongs in it.

- Branch **`feature/run-mode`**, not merged, deliberately — the mode is still
  being tuned in play.
- **The Saga Atlas** — one page with every questline in the saga, lane by lane across
  all eight acts, plus the arc, the light economy and a build log:
  <https://claude.ai/artifact/8EvSbu7GH5SQ1Fq9Md83ca>
  Regenerate it with `python Scripts/saga_atlas.py` and REPUBLISH TO THAT SAME URL, so
  the owner's link keeps working. The lanes are read out of `RunService.cs`, so the page
  cannot drift from the code — but the per-act STATUS and the version in its meta line
  are hand-written claims and go stale on their own.
- **The Storm-Anvil test path** — the focused page for the shade/light/anvil chain:
  <https://claude.ai/artifact/XdbeDYL6m7f2t5MioVmqJ9>

## Do not free the cursor for the mod's windows (tried, reverted 2026-09-20)

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

## Read the IL before building the workaround

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

## Parked: search at the crafting bench

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

## Two shapes worth remembering from 2026-09-20

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

## The loop

Every alpha follows the same seven steps. It takes about a minute.

```bash
# 1. change code, then:
msbuild Valheim.sln -p:Configuration=Debug -v:minimal   # must be clean
Tests/run_tests.sh                                      # must say ALL PASS

# 2. bump ICanShowYouTheWorld/Assets/Version.cs
#    alpha<N>   for a mechanic/content change worth a play-test brief
#    alpha<N>.<B> for a fix, a tuning number, a nudged panel
#    ALWAYS bump one of them: the menu badge is the only proof of what is being played
# 3. refresh the Windows kit + docs
cp ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll dist/windows/patcher/
sed -i '' 's/alphaN/alphaN+1/g' dist/windows/README.md HANDOFF_WINDOWS.md

# 4. append a TASK section to HANDOFF_WINDOWS.md saying what to look for
# 5. commit, tag, push
git tag 0.221.12-run.alphaN+1
git push origin feature/run-mode && git push origin 0.221.12-run.alphaN+1

# 6. deploy to the Mac
Scripts/deploy_local.sh
```

On Windows: `git pull` → `.\Install-Mod.ps1` → the gold `SAGA v<build>` line under the
main menu's version must read the tag you just pushed. **That badge is the whole point of
tagging every build** — it is the only way to be certain which one is being played. There
is no popup on success since 2026-09-19; one appears only if the mod failed to load.

`-ModOnly` is the fast path for a mod-only change, and it now refuses an assembly
patched by an older Patcher (it looks for the `ICSYTW_EntryPoint_FejdStartup_Start`
stamp). **Whenever the Patcher itself changed, run the full install.**

## Waiting on a human

None of these are blocked on code — they are blocked on someone playing.

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
