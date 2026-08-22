# Resuming Run Mode work

Written 2026-08-22, at `0.221.12-run.alpha29`. This is the "pick it back up
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

- Branch **`feature/run-mode`**, 81 commits ahead of `main`. **Not merged**,
  deliberately — the mode is still being tuned in play.
- Latest tag **`0.221.12-run.alpha29`**, pushed. The Mac has it deployed.
- Game version 0.221.12, Unity 6000.0.61. Windows is the play machine, the Mac
  is the test/build machine, the Deck travels (and is **stale** — it still has
  an older patched assembly and needs a re-patch before use).
- Engine tests: `Tests/run_tests.sh`, 305 assertions, all passing.

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

## What the mode is, as of alpha29

**Five acts**, one per boss, each a questline chain ending on its own boss kill.
All five are now written: I The Meadows (14 steps) → Eikthyr, II The Black Forest
(9) → The Elder, III The Swamp (7) → Bonemass, IV The Mountains (7) → Moder,
V The Plains (7) → Yagluth. Every act opens on an arrival step and carries mob
beats plus (except the Mountains) a building beat.

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

Questline heat across the saga is **44** (14+9+7+7+7) — roughly ×3.2 enemy damage
by the Plains before any random task, and far steeper than anything played.

**Act I**, 14 steps, all of it doable without leaving the Meadows: craft an axe
→ craft a hammer → build a workbench → hunt 5 boar → raise a roof (6 pieces) →
**build a fire** → **build a cooking station** → kill 6 greylings → settle in
(2 min at home) → **build a bed** → sleep through the night → **build a chest**
→ hunt 3 deer (pays Eikthyr's summoning trophies) → defeat Eikthyr.

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

**17 boons**, never offering one already held: ten passives (Fleet-footed,
Sharpened, Packmule, Hearty, Enduring, Vigorous, Cat's Breath, Marathoner,
Acrobat, plus the three skill boons Woodsman/Hunter/Warrior), and five actives
on Keypad 4-8 (Second Wind, Emberskin, Waystone, Packbrother, Windfall).

> **Windfall is deliberately strong, owner's call (alpha27):** one charge, never
> refills, doubles every stack whose `m_maxStackSize > 1`. That includes FOOD,
> and food is the health and stamina bar — filling a pack with cooked meals and
> then pressing it is the obvious exploit, chosen knowingly over a materials-only
> version. If play says it is too much, it is one word in `ActivateWindfall`.

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
4. **Read the log for `[ICanShowYouTheWorld] Unknown`** after any run start.
   alpha27 validates every creature, item and reward name against ZNetScene and
   ObjectDB and logs the failures — so this entry is no longer "play until
   something feels stuck", it is "read one line". It should settle `ShieldWood`,
   `CookedMeat`, `Flint`, `Resin`, `RawMeat`, `$item_cookedmeat` and all nine
   boss foods in one launch. Anything it names is a one-line fix.
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

## Decided, not yet built (next up)

Both approved in the alpha28 session; detail in
[the acts design](specs/2026-08-22-acts-design.md).

- **Deer focus in Act I** — all four: starred deer ("Eikthyr's Herd"), a named
  2-star Herald as a late step, contested kills drawing greylings to a carcass,
  and lightning on the kill. The constraint that shaped them: **deer cannot be
  made to attack**, since they run `AnimalAI` which has no attack at all.
- **Universal storage** as a run-window stash panel — deposit/withdraw anywhere,
  persisted with the run, storing prefab + count + quality + variant. Rejected:
  making every built chest share one inventory, which needs a third patcher
  injection and a re-patch of every machine.

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
- **Asset names cannot be verified at BUILD time** — but since alpha27 they are
  verified at RUN time. `ValidateAssetNames` resolves every creature, item and
  reward name against ZNetScene and ObjectDB at run start and logs the failures,
  so a wrong name is now loud on first launch instead of a quest that silently
  never completes. The preference order is unchanged and still right — a
  `PlayerStatType` or a compiled component beats a name — but a name is no
  longer a gamble you only settle in play.

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
