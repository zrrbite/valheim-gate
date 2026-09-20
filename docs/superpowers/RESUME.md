# Resuming Run Mode work

Written 2026-08-23, last updated 2026-09-20 at `1.0.15-run.2026-09-20w`. This is the "pick it back up
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
- Latest tag **`1.0.15-run.2026-09-20w`**; builds are date-based since 2026-08-31
  (`Scripts/nextversion.sh`). The game moved to **1.0.12 on 2026-09-12** and to
  **1.0.15 on 2026-09-19**; both times the mod was rebuilt and installed on Windows
  the same day. **1.0.15 needed no source change** — all 316 member references from
  the built DLL into the game's assemblies still resolved, and Unity stayed at
  6000.0.75. The Deck and the Mac are now TWO game versions behind and each needs its
  own download → patch → upload before use.
- **Built 2026-09-19 evening to 2026-09-20**, all awaiting a play verdict. In the order
  they were found, because several were each other's cause:
  - `CreatureDressing.ApplyWhenSettled` — named creatures are dressed TWO FRAMES after
    the spawn. Dressing at spawn ran before `LevelEffects.Start`, which sets `localScale`
    (discarding our multiplier) and, on a cache miss, copies the renderer's current
    material into a **static** dictionary keyed by prefab+level. The Gatherer is
    `SetLevel(2..3)`, so it was handing its gold to every starred `Greydwarf_Elite` in the
    session. One bug, two play reports. **Anything that dresses a creature must use
    ApplyWhenSettled.**
  - Saga items set every stat explicitly instead of inheriting from the source prefab. The
    source is now purely which MESH the item wears. Thor's bow: 58 pierce, 32 lightning.
  - The Stormsworn: one armour piece per act, Acts II to V, each resisting what its own act
    kills people with. No rescued lights in those recipes — lights are Acts I and II only,
    and a `CollectItem` step nobody can finish is a stalled act.
  - Step hints are SPOKEN as a step opens, through a paced queue (Valheim's centre message
    replaces itself, so two lines in one frame were one line plus a flicker).
  - Recipe unlocks are announced by Hugin (`SagaRecipeDefinition.TaughtLine`), tracked
    separately from registration because a world load re-registers everything.
  - `PageDown` gates back to where you died; dev keys are bare again except `Keypad +/-`;
    the dev help line is generated from `RunService.DevKeyHelp` beside the keys it describes.
  - `nextversion.sh` takes the letter after the highest, not the first gap.
- **One unverified guess**: the forge's prefab name in the three Stormsworn forge recipes is
  `"forge"`. Nothing else in the repo references it. It fails loudly — look for
  `station prefab 'forge' has no CraftingStation` in the log in Act II.
- **The entry point moved on 2026-09-19**: `NotACheater.Run()` is injected into
  `FejdStartup.Start()` (and still into `OnCredits`, where it is a no-op). The mod
  loads itself at startup; "open Credits first" is no longer a rule anybody has to
  remember, and no longer a way to lose Thor's bow.
- Engine tests: `Tests/run_tests.sh`, 572 assertions, all passing. The script now
  falls back to Visual Studio's Roslyn `csc.exe` when `mcs`/`mono` are absent, so the
  suite runs on the Windows box too.
- Game version 1.0.15, Unity 6000.0.75 (was 0.221.12 / 6000.0.61 until 2026-09-12,
  then 1.0.12). Windows is the play machine and BUILDS everything now
  (`Scripts/build_windows.sh` for the mod, and the Patcher too since 2026-09-19); the
  Mac is the other build machine; the Deck travels. **Both the Mac and the Deck are
  stale** — 0.221.12-era patched assemblies, and the old entry point.

### Confirmed in play

- **The light race, end to end** (alpha70) — wisp on a night deer kill, the
  pack, the red urgency bar, boss music, the take, the scoreboard. Owner:
  "super difficult — very nice."
- **The Act I → II transition choreography** — card, raven, premise whisper.
- **The Gatherer** — the fight, the hoard drop, the arrival tier readout.
- **Act II's couriers** (alpha79) — "worked perfectly, along with the hints."
  That closes the longest-running unverified thing in the mode.
- Resume after quitting mid-run, the Herald hunt, fishing, the wisp model.

### Still unverified in play

- The Gatherer arriving FATTER after lost lights; the forfeit at 8 lost.
- **Everything in alpha80** (below): combined kill lists, the single Hugin, the
  Waystone's target and landing, the biome bearing, Act III's scrap-iron step.
- Acts III–V beyond their opening beats.
- **Heat tuning — open since alpha17**, and still the oldest thing on this list.

### What alpha80 changed (from one play session's notes)

Nine items, all owner-reported:

1. **Combined kill lists.** An act's POPULATIONS are now ONE composite step
   counting every quarry at once — "8 Draugr, 5 Blobs, 3 Leeches" — instead of
   a queue in which the thing in front of you did not count. `mq-cull`,
   `bf-cull`, `sw-cull`, `mt-cull`, `pl-cull`.

   **What is NOT in a list is the point** (alpha80.1, owner: "It would also be
   a shame if we just scrunged everything into a kill list"). The heavyweights
   are steps of their own, each because the story bible already had a reason:
   Brutes are FED splinters, so killing one is a withdrawal; the Troll is what
   a splinter becomes when it is never fed — the one thing in the forest that
   breaks light instead of carrying it; the Abomination is the marsh's "never
   let go" standing up; Golems are a watch outliving its post; Berserkers are
   overseers keeping a quota on an empty field. Each says its line once, when
   it becomes current, via the new `ChallengeDefinition.Opening` +
   `StepOpenings` (pure, tested — its whole job is telling "just became
   current" from "was already current on resume").

   A composite pays health and heat PER CLAUSE, so pacing is unchanged.
2. **One Hugin.** The game guards its own raven with `Raven.IsInstantiated()`
   in both `Tutorial.SpawnRaven` and `GuidePoint.Start`; ours did not, so two
   live Ravens shared one static text list and both flew in for the SAME line.
   Ours now honours the guard and delivers beats as `Raven.AddTempText`, so the
   vanilla tutorial Hugin keeps working (and keeps feeding the RavenTalk task)
   while our beats get their own lines. Keys are scoped to the run seed —
   `Player.m_shownTutorials` persists per CHARACTER, and the saga is replayable.
3. **One act banner.** The centre-screen Message duplicated the transition card
   in smaller type. Card only.
4. **The stash left the always-on panels.** Only the tracker stays up with the
   HUD hidden; the stash comes back with `End`.
5. **God mode regenerates.** Valheim's god mode is NOT invulnerability —
   `Character.ApplyDamage` takes the hit and clamps the RESULT to 1 — so a
   god-mode player under fire sits at one hit point. A periodic tick now heals
   ~34% of max four times a second while it is on.
6. **The Waystone goes to the NEXT boss, not the nearest.** It picked by
   distance and sent a player with Bonemass alive to Moder.
7. **The Waystone lands OUTSIDE the altar** — 28m out, on the approach bearing,
   above water. Bonemass's centre point is inside a closed skull.
8. **The biome bearing** — alpha80 pointed it AT the boss altar and alpha81 took
   that back. Aiming at the altar produced "the Black Forest lies north, 2500m"
   with black forest twenty seconds away, and it contradicted the step it
   serves: `ReachBiome` matches the BIOME, not the instance, so any black forest
   completes it. The arrow points at the nearest patch again, and the
   disagreement with the map pin is now EXPLAINED instead of hidden — when the
   act's altar is in a different patch (2x further and 500m+ beyond), the line
   adds "The Elder's own lies further north-east". `BossAltarIn` is cached, since
   `FindClosestLocation` scans every generated location and the HUD reads this
   every frame.

   **Over-world text is outlined now** (`RunTheme.ShadowedLabel` +
   `AccentGoldBright`). The bearing sits on the world with no panel behind it,
   and a mid gold on a bright background is an invisible line (owner: "the font
   we use to indicate distance to biomes is too dark"). The panel's copy went
   from `Small` at 72% alpha to `Body` at full.

   **Dev mode gained `Home` = teleport to map cursor** — the GM mod's teleport,
   reachable during a run. `Home` rather than `Insert` because `Insert` is the
   menagerie boon. DEV ONLY on purpose: free travel is not a small change to a
   mode whose score divides by time; Waystone and Homeward are the in-play
   answers and both cost something.
9. **Act III's craft track IS the iron pipeline now**: reach the swamp → haul
   20 Scrap Iron (`sw-scrap`) → smelt 10 bars (`sw-ironbar`, lifted off the
   marsh track where it sat behind a boat). The old `sw-iron` (60 MineHits) is
   GONE — it was the same activity as hauling the scrap, so the scrap step
   would have completed on the tick after it was dealt, and `MineHits` is
   incremented by `MineRock.RPC_Hit`/`MineRock5.DamageArea`, i.e. any rock
   anywhere, so "Dig up the crypts" was satisfiable in the Black Forest.

   **Act IV's `mt-silver` got the same treatment** (alpha80.3): it is
   `CollectItem $item_silverore`, 15 — silver ore exists only under the
   mountains, so the step now means what it says.

   Picking that number needed a fact the assembly cannot see, so the run-start
   validator gained a **carry-weight check**: it reads every item's
   `m_shared.m_weight` from ObjectDB, prints what each collect step's target
   actually WEIGHS against `Player.GetMaxCarryWeight()`, and logs an error if
   an amount could never be held. That constraint is real and symptomless —
   CollectItem latches on what is in the inventory, so the whole amount must be
   carried at once and extra trips do not help. Grep the log for `Collect '`.

Two supporting fixes that shipped with them, both worth knowing about:

- `ChallengeEngine.RestoreTrack` now carries **sub-progress**, and **clamps a
  stale index** back onto the chain when the saved step id no longer exists.
  Without the clamp, shortening a track (which merging kill steps does) would
  have let the first resume run off the end — and off the end reads as "this
  questline is finished" everywhere, including for the act's boss.
- `DevCompleteCurrent` fills the CLAUSES. A composite ignores `Progress`, so
  the dev skip would have silently done nothing on exactly the new content.

### Custom looks for named creatures (alpha82)

`RunMode/Unity/CreatureDressing.cs`. The Herald, the Gatherer and the couriers
now look like what the story says they are, with **no shipped assets at all**.

The four shader properties Valheim's own creature shader exposes —
`_Hue`, `_Saturation`, `_Value`, `_EmissionColor` — were read out of
`LevelEffects.SetupLevelVisualization` in this build's IL, not remembered. That
is how starred creatures are recoloured, so it is known-good.

**The one thing not copied from LevelEffects is how it writes them.** It edits
`sharedMaterials` through a static per-prefab cache, which is right for "every
two-star greydwarf looks like this" and catastrophic here — it would repaint
every greydwarf in the world. `CreatureDressing` touches `renderer.materials`,
which Unity instantiates per renderer, so the change lands on one creature.

- **The Gatherer** glows in proportion to the lights it ate — the same number as
  its health bonus and its arrival line, said a third way.
- **Couriers** carry a real point light, which is the half that makes one
  findable through trees at night. A star and a hover name are invisible at 40m.
- **The Herald** is pale and cool-lit, the opposite of the forest's things.

**What still needs a real pipeline: a different SILHOUETTE.** Colour, size and
light are free; a new mesh needs an AssetBundle built in Unity 6000.0.x, shipped
beside the DLL, loaded at runtime with the game's shaders re-bound by name. That
is a decision, not a detail — see the note at the top of `CreatureDressing`.

### Act structure

**Act I is still the focus** and is playable end to end. Design in
`specs/2026-08-23-act-one-homestead-design.md`; the seven-act plan in
`specs/2026-08-23-act-questline-plans.md`; the story itself in
`specs/2026-08-27-story-bible.md`. Acts II–V exist and open correctly; their
middles are thinner than Act I's.

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

## Done 2026-09-20 (`...20c`): the general menu, and the flavour baked into the DLL

Agreed and built the same day. Kept for the REASONING; the diff is in git.

> Owner: "I've actually been thinking about the 'run' menu, that it should be a sort of general
> menu. IF it's built with -Dev mode we can access both the GM mod of old and the new Saga mode and
> we can use the menu to go back and forth. Saga mode would then have the dev commands we have now.
> If it's built without dev mode then only the saga would be available."

This closes the **saga-only build** goal that has been open since 2026-09-19, and it closes it
better than the plan it replaces. Three things fall out of it.

**The switch is BAKED INTO THE DLL, not read from the config.** Asked and answered
(owner: "I dont want anyone to be able to reach the GM mod but me, so i think it should be
absent?"). A config bool does not meet that, because the recipient owns the config file.

Do it the way `Version.cs` is already done: `ICanShowYouTheWorld/VersionTemplate.cs` is sed-filled
into `Assets/Version.cs` by `Scripts/setversion.sh`. Add a second baked constant beside `VERSION`
- `GM_ENABLED` - written by the same script from a `--saga-only` flag on
`Scripts/build_windows.sh`. The gates then read a `const bool` nobody outside the build can touch.

**Not two project configurations and not `#if`.** Two reasons, the first decisive: the saga-only
binary is the one the owner never plays, so it is the one that would break silently - the worst
property a shipped artefact can have. And because the legacy `CheatCommands` pipeline must STAY
(the saga rides it), the conditional surface would be scattered across `Cheat.cs`, `UIManager.cs`
and `InputManager.cs`, which is exactly where "works in my build" lives. One binary, one code path,
one constant.

**Have the badge say which build it is.** `MenuBadge` already prints `SAGA v<build>`; a GM build
should say so (`SAGA v<build> - GM`). The failure this prevents is handing out the wrong DLL and
nobody being able to tell.

**Say plainly what this does and does not buy.** Valheim ships its own cheats: `F5` plus the
`-console` launch option gives `devcommands`, and with it god, fly, spawn and kill. So none of this
stops a determined person - it stops an idle one, and it stops the saga's score from being quietly
meaningless for someone who never asked for GM. That is worth doing; "security" is not the claim.

**`runDevMode` keeps its name and its meaning.** It stays the TESTER's switch - step-skips, kits,
the clock - and does not become the GM switch after all, since those are now two different things
living in two different places. Renaming it would also have dropped the owner's 25-August config
silently to a new default, which is the trap `runStaminaRegenRate` is already sitting in.

**Build it on the pages, not a new window.** `RunWindow.HudPage` and its tab row exist as of
`...20b`. Outside a run the pages are the saga lobby and - in dev only - a GM page that toggles the
EXISTING cheat windows rather than re-implementing them. During a run the GM page is absent: the
input gate exists because heat and score assume GM is dead, and that is not negotiable by a menu.

What "saga-only" has to switch off, concretely: `InputManager` must not REGISTER the GM bindings
(not merely gate them), `UIManager` must not draw the GM windows, and F1 must do nothing outside the
Heat HUD. What it must NOT switch off is the legacy `CheatCommands` pipeline itself - Run Mode's own
effects ride it (`WithLegacyGodModeBracket`), so this is hiding entry points, never deleting the
layer.

Worth naming as a pre-existing hole rather than a new one: in a dev build, GM is available outside a
run today, so a world can be god-built and then played as a saga. Per-run fairness does not claim
otherwise, but if that should be closed, it is a separate decision.

## Done 2026-09-20 (`...20b`): the QUESTS page and the self-opening menu

Both built the same evening they were agreed. Kept here because the REASONING is what a later
change needs, not the diff.

### 1. A QUESTS page, and a rule about what belongs where

The Run window has grown to act headline, score/health/gates, QUESTS (three tracks with their
sub-objectives), SPLITS, HOMESTEAD, TASKS and BOONS in one scroll. Owner: "Its getting kind of
cluttered, so im wondering if we should do a seperate quest log that has the main quests which gives
us a chance to be a bit more descriptive, leaving some spare room in the main run menu."

The rule to build to, which is broader than the request and is the useful part:

> **The HUD holds what you act on. The log holds what you have done and what is coming.**

So:

- **Quests stay on the HUD, summarised** - one line per track (label, current step, count). They do
  NOT move out wholesale: there is a decision recorded in `RunWindow` that the main questline is
  pinned above the scroll because "it is the one thing on this HUD that says where the run is GOING,
  so it must never scroll out of view", and that was learned in play.
- **Everything around them moves**: sub-objective lists, hints, blocked reasons, reward text.
- **SPLITS and HOMESTEAD move too.** Both are records, not decisions, and they are spending HUD
  height on information nobody acts on mid-fight. TASKS and BOONS stay - those are live choices.
- **The page shows COMPLETED steps.** This is the prize, and it is nearly free: `QuestTrack` already
  carries `Chain` plus `Index`, so everything before the index is done, the index is current, and
  everything after is upcoming. No new state. A finished step currently just vanishes, so the run has
  no memory the player can read - and for a mode whose pitch is a saga, "what have I done so far" is
  the page it is missing. Each done step can carry its `Opening` line as the record of that beat,
  which is where the descriptive room actually pays off.
- **No new key.** The keypad and nav cluster are saturated, and a log is a PAGE of the run window
  rather than a separate mode, so a small tab row at the top (`RUN | QUESTS`) costs nothing. If a key
  is wanted later, `End` cycling the two pages is the honest version.

The code shape is known: `DrawStash`, `DrawTracker` and `DrawOffer` are already separate windows with
their own body methods, so this is a fifth instance of an existing pattern.

### 2. The saga menu opens itself on spawn

Owner: "I'd also like to trigger the Saga menu on char spawn instead of having to press END. User can
always CANCEL instead of starting the saga run."

Same reasoning as the entry point moving off the Credits menu: a mode you have to remember to open is
a mode that gets forgotten. `UIManager.ToggleRunWindow()` -> `RunWindow.ToggleVisible()` is the
existing door; this needs a `Show()` and a trigger.

Three things to get right, in the order they will bite:

1. **Trigger on entering a WORLD, not on a fresh Player instance.** `DetectRespawnAndReapplyPassives`
   already watches the player reference, but using it would re-open the lobby every time the player
   dies outside a run, which is precisely when they do not want a menu. The signal wanted is
   `WorldIdentifier()` going non-null with a player present - one offer per world load, tracked by a
   flag that is cleared on suspend/world change.
2. **Only when no run is active.** During a live run the HUD is the correct window and the lobby is
   not; and a resumed run must not be interrupted by an offer to begin one.
3. **It needs a visible CANCEL.** The lobby today has "Begin the saga" and "Discard saved run" and is
   dismissed by pressing `End` again - which is fine for a window you opened deliberately and wrong
   for one that opened itself. A window that appears unbidden and can only be closed by a key nobody
   told you about is worse than the key press it replaced.

Note that `UIManager` does no cursor management at all, so the lobby's buttons work on Valheim's own
cursor state. That is already true of every button in there, so it is a fact to preserve rather than
a problem to solve - but it is the first thing to check if the auto-opened window turns out to be
unclickable.

## The loop

Every alpha follows the same seven steps. It takes about a minute.

```bash
# 1. change code, then:
msbuild Valheim.sln -p:Configuration=Debug -v:minimal   # must be clean
Tests/run_tests.sh                                      # must say ALL PASS

# 2. bump ICanShowYouTheWorld/Assets/Version.cs
#    alpha<N>   for a mechanic/content change worth a play-test brief
#    alpha<N>.<B> for a fix, a tuning number, a nudged panel
#    ALWAYS bump one of them: the popup is the only proof of what is being played
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

On Windows: `git pull` → `.\Install-Mod.ps1` → the popup at the main menu must read
the tag you just pushed. **The version popup is the whole point of tagging every
build** — it is the only way to be certain which one is being played. Since the entry
point moved it appears on its own, with no Credits visit.

`-ModOnly` is the fast path for a mod-only change, and it now refuses an assembly
patched by an older Patcher (it looks for the `ICSYTW_EntryPoint_FejdStartup_Start`
stamp). **Whenever the Patcher itself changed, run the full install.**

## What the mode is, as of alpha43

> **IDENTITY, decided 2026-08-23 (owner):** *"In a way this is developing into less
> of a speed-run-mod and more of a more complete Valheim experience."* — *"yeah
> let's lean into the saga mode."*
>
> **This is a guided Valheim campaign with escalating stakes, not a speedrun.** The
> scoring already agreed before anyone said it out loud: score is
> `100 × par/(par+time) × (1 + heat×0.1)`, so heat MULTIPLIES while time only
> divides — a 3-hour thorough saga scores roughly double a 1-hour thin one. The
> presentation was changed in alpha38 to match the arithmetic: the ACT is the HUD's
> headline, the clock is one number on a strip, and the score is called "Saga
> score".
>
> Player-facing strings say "saga"; config keys, save files, class names and the
> branch deliberately still say "run" — renaming those would touch the config file
> and the save file names for zero player benefit.
>
> **Do not re-add speedrun pressure without a decision to reverse this.** If it ever
> comes up again, the levers are `runParTimeMinutes` (240) and
> `runHeatScoreWeight` (0.1), both config.

**Five acts**, one per boss. All five are written: I The Meadows (16 steps) →
Eikthyr, II The Black Forest (10) → The Elder, III The Swamp (8) → Bonemass,
IV The Mountains (8) → Moder, V The Plains (8) → Yagluth.

**Every act ends `… → find the altar → kill the boss` (alpha36).** A
`ChallengeKind.DiscoverLocation` step makes the finale earned rather than handed
over. The altar was chosen over a rune stone because the world generator
guarantees one per boss and a linear chain must never be unfinishable — the same
reasoning as the boat steps. Waystone legitimately skips it. See
[the design](specs/2026-08-23-discovery-and-hints-design.md).

**Only the CURRENT act's altar is pinned (alpha37).** All five used to be pinned at
run start, which handed the player the whole saga in minute one and made the
discovery step a walk to a known dot. Each now appears as its act begins. Pinning
nothing was rejected for the same reason rune stones were — vanilla hands out
Vegvisirs precisely because searching a biome blind is miserable.

**Steps carry a HINT line** where the requirement is not self-evident (18 of
them). Written after two play sessions lost time to "I didn't know what this
needed" — the smelter's surtling cores, and a home needing a fire. A hint on
"Kill 5 Boar" would be noise, so most steps have none.

**Each act runs TWO questlines side by side (alpha32)** — a HUNT track of its
kills and a CRAFT track of everything else, advancing independently, both shown in
the HUD. `RunService.Split` cuts an act's steps by `Kind`, so a step added later
lands on the right track without anyone remembering to put it there.

> **This is the difficulty dial, and it was the owner's framing:** *"the good
> thing about dual paths is that you can decide if you want the heat."* Every
> questline step pays heat, so working both tracks makes you stronger AND hotter;
> running the hunt track straight at the boss keeps you cool, poorer and
> lower-scoring. It also reframes the short craft tracks in the later acts (Act IV
> has two steps) — those acts simply offer less optional heat, rather than being
> thin. See [the design](specs/2026-08-22-quest-tracks-design.md).

The BOSS is the last step of every hunt track, which is what keeps "the act is
over" observable. An unfinished craft track when the boss falls is simply
unfinished — the cost of rushing, and nothing extra to persist.

Which act is current is **derived from the world's defeated-boss count**, never
stored — so it cannot drift, a resume recomputes it, and a run started on a world
that already killed Eikthyr correctly begins in Act II. The transition rides the
1 Hz boss poll, which is safe only because `_challenges.Tick` runs every frame
and has therefore already fired the finishing step's reward. See the
[acts](specs/2026-08-22-acts-design.md) and
[act content](specs/2026-08-22-act-content-design.md) designs.

Two rules the content follows, both learned the hard way:

- **The chain asks for destinations; the pool asks for transport.** The chain is
  linear with no skip, so a boat step would hard-stall a run on a world where the
  biome is walkable. Acts open with `ReachBiome` ("Reach the Swamp") which is true
  however you travelled; boat quests are pool-only, gated on `Biomes = Ocean` AND
  `RequiresBuilt = "Ship"`, so a landlocked run is never dealt one.
- **A build category may appear in ONE act only.** `_builtSeen` latches for the
  whole run, so a category an earlier act satisfied auto-completes a later act's
  step the moment it is dealt. `ValidateActs` enforces this. The Mountains
  therefore have **no** build step — no distinctively mountain-built piece has a
  compiled class, and filler would be worse than an extra fight.

Questline heat across the saga is **54** (21+9+8+8+8) — roughly ×3.2 enemy damage
by the Plains before any random task, and far steeper than anything played.

**Act I**, 21 questline steps, all of it doable without leaving the Meadows (the hunt track opens with the raven's errand, a daylight kill that yields nothing, and a night watch - see the `...-19h` note below): craft an axe
→ craft a hammer → build a workbench → hunt 5 boar → raise a roof (6 pieces) →
**build a fire** → **build a cooking station** → kill 6 greylings → **build a
bed** → settle in (2 min at home) → sleep through the night → **build a chest**
→ hunt 3 deer (pays Eikthyr's summoning trophies) → **hunt Eikthyr's Herald** →
defeat Eikthyr.

**Eikthyr's Herd (alpha30)** runs for Act I only: about half the deer you meet
get a star, a deer's death may draw greylings and may crack with lightning, and
the Herald is a named two-star deer spawned for its own step. The constraint that
shaped all of it: **deer cannot be made to attack** — `AnimalAI` has no attack,
so the hunt got harder to CATCH rather than dangerous, and the danger comes from
what the noise attracts. The Herald is matched by ZDOID rather than by species,
since it is an ordinary Deer wearing a name and any deer would otherwise finish
its step; its synthetic kill name is the one entry in `SyntheticCreatureNames`,
exempt from the validator. See [the design](specs/2026-08-22-deer-and-stash-design.md).

**The stash (alpha30)** is run state, not a chest — reachable wherever the run
window opens, so it follows you between bases and acts. Since **alpha31** it is its
own scrollable window beside the tracker rather than a section of the Run HUD
(owner: "it clutters the main Run window"). "Deposit materials" moves every
unequipped `ItemType.Material`; food, arrows and gear deliberately stay on you. Quality and variant are part of an entry's identity (a level-3 axe is not a
level-1 axe); durability is not stored, so a withdrawn tool returns at full.

The homestead steps (alpha26-27) each sit immediately before the step that
already, silently, depended on them: `TimeInBase` only accrues while
`IsSafeInHome`, which needs a roof AND a fire, and `Sleep` needs a bed. They
measure with `ChallengeKind.BuildPiece`, which asks whether the player has built
a piece carrying a COMPILED component (`Fireplace`, `Bed`, `Container`, `Door`,
`CookingStation`) rather than naming a prefab — see the
[alpha26](specs/2026-08-22-homestead-steps-design.md) and
[alpha27](specs/2026-08-22-alpha27-design.md) designs. Act I's questline heat
went from 10 to 14 across the two.

**Baseline empowerment**, every run, no picking: resources ×3, skill gain ×3,
move stamina ×0.5, stamina regen ×2.5, all stamina costs ×0.75, free melee and
tools (ranged pays 25%), and the Hunter's Eye tracker panel.

**30 boons**, never offering one already held. Nine actives - Keypad 4-8 plus 0 and Insert
(Second Wind, Emberskin, Waystone, Packbrother, Windfall, Bonecaller, Menagerie), plus
`Keypad +` and `Keypad -` (Shaman's Mercy, Unseen); the rest are passives, across seven kinds:

| Kind | Boons |
|---|---|
| Stats | Fleet-footed, Sharpened, Packmule, Hearty, **Tireless** |
| Skills | Woodsman, Hunter, Warrior, **Quick Study** (skill gain ×3 on the baseline) |
| **Rates** | **Bountiful** (resource drops ×2 on the baseline) |
| **Resistance** | Irongut (poison), Coldblooded (frost), Fire-blooded (fire) |
| **On-kill** | Bloodthirst (heals), Relentless (stamina) |
| **Risk** | Glass Cannon (+40% dmg, −30% HP), Reckless (+50% dmg, +25% taken) |
| **Heat** | Slow Burn (heat rises 25% slower), Forge-fed (damage scales with heat) |
| Pets | Shepherd, Hearthlight, and the tracker panel (Hunter's Eye) |

Quick Study and Bountiful are the two that ride WORLD keys rather than player state, which is
why they are handled in `RunService.RefreshRateBoons` and not in `BoonEffects` — see the
`...-19e` note below.

> **Why (owner, alpha33):** *"we need more boon types. There are like three sta
> ones, and they seem a bit lack luster since we already regen quite fast."* It
> was FIVE, and they competed with a baseline that already gives stamina ×0.5 cost
> and ×2.5 regen — they were solving a solved problem. Merged into Tireless; the
> freed slots bought four categories the pool had none of. Risk boons are the first
> with a real COST, which turns an offer from "which number goes up" into a
> decision. See [the design](specs/2026-08-22-boon-rebalance-design.md).

`BoonDefinition.MinBosses` gates offers on world progression (resistances only) —
the boon pool's equivalent of the challenge pool's `MaxTier`. **Note the pin fails
silently:** `FirstBoonPin` naming a boon that no longer exists is ignored rather
than logged, which nearly shipped un-steering every opening offer when the stamina
merge deleted `"enduring"`.

> **Windfall is deliberately strong, owner's call (alpha27):** one charge, never
> refills, doubles every stack whose `m_maxStackSize > 1`. That includes FOOD,
> and food is the health and stamina bar — filling a pack with cooked meals and
> then pressing it is the obvious exploit, chosen knowingly over a materials-only
> version. If play says it is too much, it is one word in `ActivateWindfall`.

**Boss kills** pay food for the tier just cleared, refill Waystone, and grant a
**Homeward** charge (Keypad 9) — a trip back to your claimed bed. Homeward is a run
mechanic rather than a boon on purpose: a boon competes with 21 others for three
slots, and "the trip home is solved" has to be true every run or it is not solved.
Waystone carries you TO the next altar; Homeward is the leg back.

**Every completion pays +2 max health** (alpha35), questline step or random task
alike, accumulating and loaned like everything else — Act I reaches Eikthyr around
+40. Heat is what a completion costs; health is what it pays back. Armor is NOT
part of it and cannot be: the game computes armor from equipped items, and its
damage-modifier steps are far too coarse for a small increment. See
[the design](specs/2026-08-23-completion-rewards-design.md).

> **Design ruling, kept (owner, alpha25):** boss spoils are FOOD, not gear or
> materials, and that is the point — they hand over the health and stamina pool
> an hour of farming and cooking would have bought, while leaving the cookpot
> entirely usable for anyone who wants to cook. Reward the effect, leave the
> activity optional. It also self-balances for a timed mode: food buffs expire,
> so the spoils are a strong opening for the next biome rather than a permanent
> bump that compounds across a long run.

## Pointing at things (alpha43)

Two steps in the saga name something you have to FIND rather than do, and both
now put a bearing on the always-on strip through one property,
`IRunService.QuestBearing`, which returns a finished sentence or null.

- **The Herald** (`DeerHerd`) — a named deer, Act I's climax.
- **The biome an act opens on** (`BiomeCompass`) — rings outward through
  `WorldGenerator.GetBiome` for the nearest ground of the target biome, nearest
  first, so the answer is its closest EDGE rather than its middle.

Both cache a **place, not a direction**. That is the whole lesson of the
alpha38 Herald bug: a target re-decided every few seconds produces a bearing
that jumps, and a bearing that jumps is worse than none. The biome search costs
~2,000 noise lookups and is read from `OnGUI`, so it recomputes only every 5s
or after 40m of movement — and never while you are already standing in the
biome, where it would read as a bug.

## Waiting on a human

None of these are blocked on code — they are blocked on someone playing.

> **Closed 2026-08-23:** the Herald hunt is **confirmed working in play** on
> alpha42.2 — one Herald, bearing counting down the whole approach. The
> target-position fix holds.

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
4. ~~**Unverified asset names.**~~ **CLOSED 2026-08-22, in play on alpha33.** The
   `[ICanShowYouTheWorld] Unknown` grep came back **empty**, and the validator
   checks every name in ALL FIVE ACTS at run start — so one launch in Act I
   settled the lot: `ShieldWood`, `CookedMeat`, `Flint`, `Resin`, `RawMeat`,
   `$item_cookedmeat`, all nine boss-spoils foods, every Act II-V creature
   (`gd_king`, `Draugr`, `Blob`, `Leech`, `StoneGolem`, `Fenring`, `Hatchling`,
   `Dragon`, `Deathsquito`, `Lox`, `GoblinBrute`, `GoblinKing`) and ~25 later-act
   reward items. **Acts II-V are content-verified without having been played.**

   Keep running the grep after any build that adds names — it stays the cheapest
   minute in the loop — but the backlog it was built to clear is gone. See
   [`../../dist/windows/CHECKING-THE-LOG.md`](../../dist/windows/CHECKING-THE-LOG.md).
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
9. **The Act I → Act II transition**, never seen firing. Killing Eikthyr should
   banner "ACT II — THE BLACK FOREST" and immediately seat the first Black Forest
   step.
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

## Parked ideas

- **A quest book instead of the tracker panel** (owner, 2026-09-12, prompted
  by the story *bible*): an in-world asset — a book the player opens — showing
  the saga's progress, in place of the panel on the right. Also the natural
  home for a quest's HINT if hints should be found rather than shown (the Act I
  item quest's hint placement is undecided for exactly this reason). Needs the
  asset pipeline decision `CreatureDressing` is already waiting on: a book is a
  mesh and a UI, and both want an AssetBundle built in Unity 6000.0.x. Until
  then the tracker is the book.

## Done 2026-09-19: the entry point, and Valheim 1.0.15

**The mod loads at startup now.** `NotACheater.Run()` is injected at the start of
`FejdStartup.Start()` as well as `OnCredits`. The reason was Thor's bow: a saga item is
only known to the game while the mod is loaded, so loading a character before visiting
Credits left the bow an unresolved name — `Inventory.AddItem` logs
`Failed to find item prefab`, drops it, and the next save writes the pack without it.

What the change needed beyond the Patcher, all of it load-bearing:

- **`Start`, not `Awake`.** Every `Awake` in the menu scene has run by the time `Start`
  does, including `UnifiedPopup`'s — and `UnifiedPopup.instance` is assigned in its
  `OnEnable`, so a popup pushed too early is swallowed into a `ZLog` error. The
  activation popup is now *queued* by `Run()` and shown by `CheatController.Update` on
  the first frame `UnifiedPopup.IsAvailable()` says yes, which is correct from any
  injection site.
- **`Run()` catches everything.** `ModBootstrap` rethrows on failure. At `OnCredits`
  that broke the credits screen; at `Start` it would break the main menu. A mod that
  cannot load is the mod's problem.
- **Re-entry logs, it does not pop.** Returning to the main menu from a world reloads
  the start scene and runs `Start` again, so the second call is ordinary rather than
  exceptional.
- **The Patcher stamps `ICSYTW_EntryPoint_FejdStartup_Start`** — an empty marker type,
  no members and no external references. A byte scan for `NotACheater` only answers
  "patched at some point", which is exactly how an assembly patched before the move
  would pass verification and still load the mod from the wrong place.
  `Install-Mod.ps1 -ModOnly` and `Scripts/config.sh check_injections` both look for it.
  Three places share that name; change the entry point and all three change.
- **The Patcher can be built on Windows now.** It wants a NuGet restore, which is why
  `build_windows.sh` never built it — but the four `Mono.Cecil*.dll` bundled in
  `dist/windows/patcher/` are exactly the 0.11.4 the csproj HintPaths expect, so
  dropping them into `packages/Mono.Cecil.0.11.4/lib/net40/` is the whole restore.
  Worth remembering: `stage_windows.sh` copies `Patcher.exe` only *if* a local build
  exists, so a Patcher change could otherwise ship behind a stale committed exe.

**Still to do on the other machines:** the Deck and the Mac carry 0.221.12-era patched
assemblies and must each be re-downloaded, re-patched with the new Patcher and
re-uploaded. Until then they are on the old entry point and the old game.

**`...-19b` added the standing proof** (owner: "is there some way i can tell the mod is
loaded? Can we add some text to the main menu?"). `MenuBadge.cs` appends a gold
`VALHEIM: THE SAGA` line plus the build to the main menu's own version label. The game's
label was chosen over an IMGUI overlay because it is already placed, styled, scaled to the
resolution and where a player looks for a version — an overlay would have to guess all four
and would guess differently on every display. It is written by reflection
(`FejdStartup.m_versionLabel` is a `TMPro.TMP_Text`, and the mod does not reference
TextMeshPro), re-applied whenever the line lacks it, since `SetupGui` writes that text
*after* our entry point runs and a reloaded start scene brings a fresh label.

A bolder slot exists if the corner is too quiet: `FejdStartup.m_moddedText`, the GameObject
IronGate activates when `Game.isModded`. It was left alone on purpose — setting that flag
also feeds `Achievements.IsCheatedAtAll`, and the object's own text is a localised token
rather than anything the saga would want to say.

**`...-19c` answered four notes from the first real run of the day**, all owner-reported:

1. **A fishing bounty was dealt before the player owned a rod.** `ChallengeDefinition` gained
   `RequiresItem`, the tool sibling of `RequiresBuilt`, and both pool fishing bounties carry
   it. It reads the LIVE inventory at deal time rather than latching like `_builtSeen`: a rod
   can be lost or left in a chest, and "did you ever hold one" would deal the task to someone
   who can no longer fish. The questline's fishing steps were never at fault — `mq-rest`
   grants the rod and precedes `mq-fish` on the hearth track.
2. **Windfall carries three charges**, reversing the alpha27 one-charge ruling on the owner's
   word. The number lives in one constant that the boon's DESCRIPTION is built from, because a
   card and a code path disagreeing about it would be worse than a number that needs a rebuild.
3. **The stash is sorted alphabetically** — in the LIST, not the view. Withdrawal is addressed
   by index into that list across a frame boundary, so sorting only the display would have
   meant mapping a row back to a real index, and that mapping is the thing to get wrong. The
   deferred stash actions also swapped order (withdraw before deposit) since a sorted insert
   can now shift rows.
4. **A new boon, Quick Study** — the first skill boon that is not a one-off grant. It rides the
   world's `SkillGainRate` key, which is why it lives in `RunService.RefreshSkillGain` rather
   than in `BoonEffects`: the key belongs to the WORLD, is guarded by world identity and has
   its pre-run original in the run state. It is recomputed from the CONFIG baseline every time
   it is written, never from the key's current contents — which is how a second writer on one
   value stays safe next to `ApplyBaseline`, the lesson this mode has paid for three times.
   `runSkillBoonMultiplier` (3) is config.

**`...-19d` made the rescued lights real** (owner: "Thors bow is a bit simple to craft. Can we
make some of the light we collect a part of the recipe?"). A light was a number on a scoreboard;
a recipe needs an ItemDrop, so the number became an object - "Rescued light", cloned from the
Mistlands Wisp, which is already a caught light in item form. One per light credited, at the two
places a light is credited and nowhere else. Thor's bow now asks for three.

Three things that shaped it:

- **The forfeit path grants nothing.** It advances the race step precisely because the race was
  lost, and "the lights are gone, and the trophies with them" would be a lie if the pack filled
  up anyway. The scoreboard and the pack are allowed to diverge: `Taken` is what the run rescued
  and never goes down, the item is what you still have.
- **It cannot lock the chain**, which is why a won thing could go on it at all. The Gatherer frees
  `Clamp(lightsLost, 2, 6)` lights on death, so a forfeited race (8 lost) frees six - losing every
  light moves where they must be taken from rather than putting the bow out of reach.
- **The bench will not list the bow until the player HOLDS a light**, because Valheim only offers
  recipes whose every ingredient has been seen. That is a cliff right after paying the shade, so
  the step's hint says it outright. `SagaItemDefinition` also gained `SourceFallbacks`, since a
  game update that moved `Wisp` would otherwise take the bow's recipe down with it, silently.

**`...-19e` added Bountiful** (owner: "add a boon where you increase the drop multiplier") - a
passive that multiplies the baseline resource rate by 2, so x6 while held. It is the counterpart
to Windfall rather than a second copy: Windfall doubles what is already in the pack, once, while
this multiplies what the land gives up for the rest of the run.

Quick Study's plumbing was GENERALISED rather than copied, which is the part worth knowing:
`WorldModifiers.ApplyBoostedRate(key, baseRate, boonMultiplier)` writes any one rate key, and
`RunService.RefreshRateBoons` rewrites both rates on every call. The rates are therefore a pure
function of (config, held boons) - which is the only honest way to have two writers on one world
key, and the discipline the mode has paid for three times. Any further rate boon is now one line
in the table, one id in `IsRateBoon`, and one call in `RefreshRateBoons`.

The pool is **30 boons**. `runResourceBoonMultiplier` (2) and `runSkillBoonMultiplier` (3) are
config.

**`...-19f` added two actives that already existed** (owner: "we also need the shaman heal as an
activated ability boon", "and invisibility activated ability boon", "I have both in my old mod
code" - and they were there):

- **Shaman's Mercy** `[PgDn]`, 90s - `CheatCommands.CastHealAOE`, the `DvergerStaffHeal_aoe`
  prefab cast where the player stands. A burst, where Second Wind is a ten-second window of AoE
  Renewal. Both are worth a slot precisely because of that difference.
- **Unseen** `[Bksp]`, 150s, 20s window - `CheatCommands.ToggleGhostMode` with a scheduled off.

Both ride the LEGACY statics rather than new code, which is what `BoonEffects` is for: a boon is
a legacy cheat with a cooldown and a reason. Both therefore need `WithLegacyGodModeBracket`,
since both commands are gated on `RequireGodMode` and a run forces that flag off - unbracketed
they refuse in every fair run while printing a GM warning, which is how Shepherd's silent no-op
was caught. Unseen also follows Emberskin exactly on the flag-before-the-call ordering and the
`ForceGhostOff` unwind, reached from Unapply, from UnapplyAll's finally, and from the pending
timer - a stranded ghost mode would be a player invisible for the rest of the run with nothing
left that knows to undo it.

**Keys:** every numpad symbol is a DEV binding and Keypad1-3 are the offer's pick keys, so these
went on PgDn and Backspace. Unambiguous beat tidy; both are one line to move.

**`...-19g` scoped the keys properly** (owner: "the keys could be bound to each game mode right?
Home means something for the original cheat mod and something different for saga mode", "so it's
OK to reuse?", "as long as the key is indicated by the saga mode"). Yes on all three, and the
answers were not symmetrical:

- **Across modes, reuse was already safe.** `InputManager.Gate` makes every GM command dead while
  a run is live, and its own comment says it exists to resolve exactly the Keypad1-7 collision
  with boon keys. A key may mean one thing to the cheat mod and another to the saga.
- **Within saga mode it was NOT scoped**, and that is what pushed the two new actives onto PgDn
  and Backspace: dev input and boon activation are read from the same handler, and dev held nine
  bare keys. **Every dev key is now Shift + the same key**, which freed the numpad symbols, and
  Shaman's Mercy and Unseen moved to `Keypad +` and `Keypad -`. `DEV-MODE.md` also gained the two
  rows it had never documented (`Home`, `PageUp`).
- **The key is now always shown**, because it is stated ONCE. It had been in three places - the
  input chain, a switch in `RunWindow`, and a hand-written "[Ins]" tail on some descriptions -
  which is how an active could work perfectly and never say what pressed it. `BoonKeys` (Unity
  layer, because `KeyCode` is UnityEngine and RunMode's pure half compiles without Unity) is now
  the definition: the input handler walks it, the HUD labels from it, and the offer card prints
  "active  [+]" from it. One row per active, and the label cannot go missing.

**`...-19h` gave Act I an opening arc** (owner: "let's review the act1 story. I think it's coming
together nicely but can we make it more intricate? Add some more detail? A section before we start
chasing the light?"). The hunt track ran "kill 6 greylings" straight into "follow the pale light",
so everything the act is ABOUT arrived as whispers while the player was busy with an axe. The
bible says the act's one rule is "taught before it is tested"; in the code it was only narrated.
Three beats now teach it:

1. **`mq-errand` - hear the raven out.** Odin's audit, in the chain instead of the scenery: it had
   only ever spoken at act transitions, which is the one moment the player is already being told
   something. `PollRavenErrand` retries until Hugin actually speaks and, after twenty attempts,
   delivers the line plainly - a chain step must never be unfinishable, and this one depends on a
   prefab the mod does not own.
2. **`mq-daylight` - hunt a deer by daylight**, and nothing rises. A quest whose whole content is
   that nothing happens. `DeerHerd.DayDeerKillName` is the daylight twin of the existing night
   synthetic, exempt in `SyntheticCreatureNames`, and the "nothing rose" line fires only while the
   step is live so it does not nag for the rest of the act.
3. **`mq-watch` - keep a watch after dark**, measured in three nightly whispers. The whispers were
   atmosphere with nothing depending on them; now they are the step, and it is the first thing in
   the saga that asks the player to be in the dark with nothing to kill.

**The bug the tests caught**, worth remembering because it is the exact class `StepPredicates` was
extracted to prevent: the dark-rule clause landed in `DeerHunt` rather than `DarkStep` on the first
pass, which would have spawned the pack and the starred deer during a vigil that is meant to be
empty, while leaving the strip silent about waiting for dark. Two assertions found it before the
build shipped. Predicates that can be tested cannot rot quietly.

**Numbers:** Act I goes to 19 questline steps, so +3 heat and +6 max health, and saga questline
heat is 53. Heat remains untuned, so this nudges a curve nobody has felt as designed.

**`...-19i` moved the troll to Act I and gave one fight allies** (owner: "it would become amazing
if we could have a troll as a mini boss too in the story... and maybe we'll have temporary allies
for this fight", then "I definitely think we should move the troll to act 1. Allies by convenience.
I'd have to dodge not to get aggro etc").

The troll was already a mini-boss - `bf-troll`, Act II, timed and losable - so this MOVED it rather
than adding a second one. The fiction was always Act I's: the one thing that breaks light instead
of carrying it. In the Black Forest the player meets it after a whole act of ordinary trolls; in
the Meadows it arrives while a troll is still the largest thing they have ever seen. It sits after
the race, keeps the fifteen-minute clock, and is spawned beside the player at night because the
Meadows have no trolls (`TheBreaker`, modelled on `TheGatherer`).

**The allies are the game's own faction rule, and this is the part worth remembering.**
`BaseAI.IsEnemy`, read out of this build's IL: a ForestMonster is hostile to every faction except
AnimalsVeg, Boss and its own. So moving the troll OFF ForestMonsters is the entire mechanism - one
field, no reflection, and nothing re-applied every tick against an AI that would re-pick its own
target a second later. `Character.Faction.Demon` leaves nothing on its side: not the forest, not
the wildlife, not the player's raised skeletons, and not the player.

Deliberately NOT friendly and NOT tamed. The greydwarves still want the player dead and simply want
the troll dead more, which was the owner's own framing. Nothing persists: the ZDO is
non-persistent and the faction change lives on one instance.

Rewards moved with the step and were re-pointed at the Meadows - the AncientSeeds came out, since
those are the Elder's key and would have let a player enter Act II holding its finale.

Act I is 20 questline steps, Act II is 9, saga questline heat unchanged at 53.

**`...-19j` added the Stormward** (owner: "another craft quest before we take on this boss? A
shield maybe. A very powerful shield"). Act I's last craft step and the saga's second item of its
own: block 60 (+8/level), deflection 40, a 2.5x timed-block bonus, VeryResistant to lightning and
Resistant to blunt. Thor's bow turns Eikthyr's storm outward and this turns it aside; a shield with
a named thing to answer is a different object from a shield with a bigger number.

Two structural decisions in it:

- **Its hide is the Breaker's**, which is where the losable mini-boss finally costs something the
  player can hold. Safe only because the shield is the LAST step of the CRAFT track: nothing waits
  behind it, and an unfinished craft track when the boss falls is the documented cost of rushing.
  The hunt track, which ends at Eikthyr, cannot be touched by it.
- **Gating a recipe on a losable step is safe**, which is not obvious: a failed step still ADVANCES
  its track, so `StepDone` answers true whether the troll died or walked away. The bench learns the
  shape either way and the missing hide is the loss.

Three rescued lights, the same price as the bow, so a player who raced badly can afford one of the
two - a decision rather than a shortage. `SagaNames.BreakerStepId` now holds the step id, since four
places ask about it (spawner, death hook, clock, recipe gate).

Act I is 21 questline steps; saga questline heat 54.

**`...-19k` tidied the main menu**, both from the first launch of the test run. The success popup
is gone (owner: "I guess we dont need to show the popup except if something fails when the mod is
loaded") - it appears only on a failed initialisation now, and that path queues the notice rather
than dropping it when the popup system is not yet live.

And the badge overlapped itself, with a screenshot to prove it. The cause was the line being
LONGER than the game's own "Version 1.0.15 (n-40)": TMP wrapped it to a third line and the block
overflowed the label's rect and drew over itself. It is now `SAGA <build>` on one line at 70%,
narrower than the line above, with word wrapping turned off as belt and braces. **With the popup
gone that line is the only proof the mod loaded**, so keeping it short is a correctness
requirement rather than taste.

Waiting on the owner's play-test of `1.0.15-run.2026-09-19k` - see the TASK entry in
[`../../HANDOFF_WINDOWS.md`](../../HANDOFF_WINDOWS.md). Beyond the entry point itself,
still unverified from 12-13 September: Thor's bow on the bench after paying the shade
and its flash on impact, the shade's greeting and its "[E] Speak" prompt, the saga
dreams, and raids arriving as beats.
