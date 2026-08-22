# Resuming Run Mode work

Written 2026-08-22, at `0.221.12-run.alpha25`. This is the "pick it back up
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

- Branch **`feature/run-mode`**, 75 commits ahead of `main`. **Not merged**,
  deliberately — the mode is still being tuned in play.
- Latest tag **`0.221.12-run.alpha25`**, pushed. The Mac has it deployed.
- Game version 0.221.12, Unity 6000.0.61. Windows is the play machine, the Mac
  is the test/build machine, the Deck travels (and is **stale** — it still has
  an older patched assembly and needs a re-patch before use).
- Engine tests: `Tests/run_tests.sh`, 250 assertions, all passing.

## The loop

Every alpha follows the same seven steps. It takes about a minute.

```bash
# 1. change code, then:
msbuild Valheim.sln -p:Configuration=Debug -v:minimal   # must be clean
Tests/run_tests.sh                                      # must say ALL PASS

# 2. bump ICanShowYouTheWorld/Assets/Version.cs to the next alpha
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

On Windows: `git pull` → `.\Install-Mod.ps1` → the Credits menu → the popup
must read the tag you just pushed. **The version popup is the whole point of
tagging every alpha** — it is the only way to be certain which build is being
played.

## What the mode is, as of alpha25

**Act I** (all of it doable without leaving the Meadows): craft an axe → craft
a hammer → build a workbench → hunt 5 boar → raise a roof (6 pieces) → kill 6
greylings → settle in (2 min at home) → sleep through the night → hunt 3 deer
(pays Eikthyr's summoning trophies) → defeat Eikthyr.

**Baseline empowerment**, every run, no picking: resources ×3, skill gain ×3,
move stamina ×0.5, stamina regen ×2.5, all stamina costs ×0.75, free melee and
tools (ranged pays 25%), and the Hunter's Eye tracker panel.

**16 boons**, never offering one already held: ten passives (Fleet-footed,
Sharpened, Packmule, Hearty, Enduring, Vigorous, Cat's Breath, Marathoner,
Acrobat, plus the three skill boons Woodsman/Hunter/Warrior), and four actives
on Keypad 4-7 (Second Wind, Emberskin, Waystone, Packbrother).

**Boss kills** pay food for the tier just cleared and refill Waystone.

> **Design ruling, kept (owner, alpha25):** boss spoils are FOOD, not gear or
> materials, and that is the point — they hand over the health and stamina pool
> an hour of farming and cooking would have bought, while leaving the cookpot
> entirely usable for anyone who wants to cook. Reward the effect, leave the
> activity optional. It also self-balances for a timed mode: food buffs expire,
> so the spoils are a strong opening for the next biome rather than a permanent
> bump that compounds across a long run.

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
4. **Unverified asset names.** Item and prefab names are Unity data, invisible
   from the assembly. A wrong ITEM name logs loudly in `GrantItem` and grants
   nothing; a wrong CREATURE name fails **silently** and stalls a quest. Still
   unconfirmed in play: `ShieldWood`, `CookedMeat`, and every boss-spoils food
   past Eikthyr's tier (`Honey`, `Sausages`, `CarrotSoup`, `TurnipStew`,
   `SerpentStew`, `WolfMeatSkewer`, `OnionSoup`, `LoxMeatPie`, `BloodPudding`).
5. **Lifecycle scenarios**, never proven in play: death mid-run must NOT log
   suspend/resume; logging out must suspend; switching world or character must
   not carry a run across.

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
- **Anything a run grants must be given back**, and given back *correctly* —
  skills return what was LENT (subtract the loan), not the pre-run level, or the
  run confiscates what the player earned.
- **Asset names cannot be verified from here.** Prefer a `PlayerStatType` the
  game already counts over a named item or piece; a stat cannot silently stall
  a quest chain.

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
