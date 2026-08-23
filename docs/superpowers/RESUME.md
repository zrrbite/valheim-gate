# Resuming Run Mode work

Written 2026-08-23, at `0.221.12-run.alpha42.2`. This is the "pick it back up
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

- Branch **`feature/run-mode`**, 96 commits ahead of `main`. **Not merged**,
  deliberately — the mode is still being tuned in play.
- Latest tag **`0.221.12-run.alpha42.2`**, pushed. The Mac has it deployed.
- Game version 0.221.12, Unity 6000.0.61. Windows is the play machine, the Mac
  is the test/build machine, the Deck travels (and is **stale** — it still has
  an older patched assembly and needs a re-patch before use).
- Engine tests: `Tests/run_tests.sh`, 413 assertions, all passing.

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

On Windows: `git pull` → `.\Install-Mod.ps1` → the Credits menu → the popup
must read the tag you just pushed. **The version popup is the whole point of
tagging every alpha** — it is the only way to be certain which build is being
played.

## What the mode is, as of alpha42

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

Questline heat across the saga is **50** (16+10+8+8+8) — roughly ×3.2 enemy damage
by the Plains before any random task, and far steeper than anything played.

**Act I**, 15 steps, all of it doable without leaving the Meadows: craft an axe
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

**22 boons (alpha34)**, never offering one already held. Five actives on
Keypad 4-8 (Second Wind, Emberskin, Waystone, Packbrother, Windfall) and seventeen
passives across six kinds:

| Kind | Boons |
|---|---|
| Stats | Fleet-footed, Sharpened, Packmule, Hearty, **Tireless** |
| Skills | Woodsman, Hunter, Warrior |
| **Resistance** | Irongut (poison), Coldblooded (frost), Fire-blooded (fire) |
| **On-kill** | Bloodthirst (heals), Relentless (stamina) |
| **Risk** | Glass Cannon (+40% dmg, −30% HP), Reckless (+50% dmg, +25% taken) |
| **Heat** | Slow Burn (heat rises 25% slower), Forge-fed (damage scales with heat) |

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
