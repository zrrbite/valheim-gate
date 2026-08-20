# Run Mode ("Saga") — Design

*2026-08-16 — designed with Martin; approved in conversation.*

## Concept

A per-character game mode that makes Valheim a repeatable, scored challenge
run: fresh world, all boss altars pinned from the start, timer running.
Baseline empowerment removes the grind; random challenges roll in during
play; completing one raises **Heat** (enemies get stronger) *and* deals a
pick-1-of-3 **boon** drawn from the mod's cheat arsenal, rationed into
bounded powers. Score = speed × heat at the final boss kill. Death drains
heat and removes the latest boon. Boons and heat are per-run; only the
record persists on the character.

Design pillars, in order:
1. **Feels like Valheim** — danger, food, terrain, and boss mechanics stay
   vanilla. Empowerment only removes waiting and grinding.
2. **Never brick a run** — bad challenge rolls are skippable (reroll costs
   heat); deaths cost progress, not the run.
3. **Power is loaned** — every advantage during a run is earned in-run and
   evaporates at the end.

## Run lifecycle

- **Activation:** `End` (currently unbound) toggles the **Run window**.
  Outside a run it is the lobby: config summary + "Start run". During a run
  it is the **Heat HUD**: timer, boss splits, heat, active challenges, boons.
- **Starting a run:** verifies cheats are off, locks the cheat arsenal
  (see Enforcement), pins all boss altars via the existing
  `BossData.Reveal`, applies baseline empowerment, starts the clock.
- **During:** bosses can be killed in any order; each boss death records a
  split. Challenges roll continuously (see Challenges).
- **Ending:** the run completes when the configured final boss (default
  Yagluth) dies — score is computed and, if a best, persisted. Abandoning a
  run is always available from the Run window and restores sandbox mode
  immediately, recording nothing.
- **Session survival:** multi-hour runs must survive quit/relaunch. Full run
  state (elapsed time, heat, splits, active challenges with progress, held
  boons and cooldowns) persists on every change and resumes with the
  character.

## UI

- **F1 keeps meaning "the mod's UI".** Sandbox: the existing cheat windows
  (unchanged). During a run: F1 shows the Heat HUD *instead* — the cheat UI
  does not exist while a run is live, making the lockout visible.
- A **minimal always-on strip** (timer + heat only) renders during runs even
  when the UI is toggled off.
- **Boon offers** appear as a modal-ish IMGUI panel: three boons, chosen
  with `Keypad 1/2/3`, offer expires after a config timeout if ignored. No
  banking offers.
- All new UI uses the `uiScale` machinery added 2026-08-16.

## Baseline empowerment

Active only while a run is live; all config-tunable:

| Dial | Default |
|---|---|
| Drop multiplier (kills and pickups) | ×3 |
| Skill gain multiplier | ×3 |
| Smelting/crafting wait times | instant |
| Out-of-combat stamina drain | −50% |

No combat power in the baseline — combat advantage comes only from boons.

## Challenges

- Always **3 active**. Completing one rolls a replacement after a short
  cooldown (default 2 min). Rerolling an unwanted challenge costs heat
  (default −1) — the escape hatch for bad rolls.
- Completing a challenge: **+heat** (per-challenge weight) and a **boon
  offer**.
- v1 pool, grouped by verification mechanism:
  - **Poll-verified** (Update-loop checks, no new hooks): build to X meters
    above terrain; reach altitude/biome; collect or craft X of item
    (inventory polling); wear no armor for X minutes.
  - **Event-verified** (needs the new death hook): kill X of mob type; take
    no damage for X minutes (damage hook, stretch goal — ship if the
    `Character.Damage` injection proves as easy as `OnDeath`).
- Challenge definitions (type, target, count, heat reward) live in config as
  a data-driven pool so the set can grow without code changes.

## Heat

- A single number. Challenges raise it, deaths and rerolls lower it. Floor 0.
- **Effect:** HP/damage multiplier on hostile creatures, applied by
  proximity polling (same pattern the pet buffs use today):
  `multiplier = 1 + heat × heatCreatureWeight` (default weight 0.05).
- **Score** at final boss: `(parTimeSeconds / actualSeconds) × (1 + heat ×
  heatScoreWeight)`. Par time and both weights in config.

## Boons

- Curated pool wrapping existing service powers into **bounded** versions —
  passives with small magnitudes, actives with cooldowns or charges:

| Boon | Shape | Wraps |
|---|---|---|
| Fleet-footed | +15% run speed, passive | speed modifier |
| Second Wind | AoE heal, 2-min cooldown | AoE heal |
| Waystone | 1 charge: teleport to any altar | TeleportService |
| Sharpened | +10% damage, passive | damage modifier |
| Emberskin | 30 s Cloak of Flames, 3-min cooldown | CombatService |
| Packbrother | summons a tamed wolf, 4-min cooldown | ZNetScene spawn |

> As shipped the pool is larger than this table and the magnitudes differ
> (Sharpened is ×1.2, Fleet is two speed increments, and seven more passives
> exist). `RunService.DefaultBoons()` is the authority; this table records the
> v1 intent. Packleader — pets +50% HP — was cut in alpha16: a run reaches no
> tames before the clock matters, it fired once at pick time so later tames
> were never buffed, and its damage path wrote permanent per-prefab values.

- Offer = 3 distinct boons drawn from the pool minus already-held passives.
  Actives bind to a small set of run-mode keys shown in the HUD.
- Boons stack across the run and are erased when it ends (or one is lost to
  death).

## Death

Corpse run and item recovery stay vanilla; the clock never stops.
Additionally: heat −N (default 3, floor 0) and the most recently gained
boon is removed.

## Enforcement

While a run is live:
- `CommandRegistry` cheat commands are disabled at dispatch (single gate in
  InputManager), so the lockout cannot be bypassed by keys.
- God mode is force-disabled at run start.
- Only run-mode inputs respond: Run window toggle, boon picks/activations,
  and F1 (which shows the Heat HUD).
- Abandoning the run re-enables everything instantly.

## Persistence

Two tiers:
- **Permanent record — the Character-info bytes** Martin identified:
  bosses-killed flags (cross-world), best score, runs completed. Small,
  fixed-size, travels with the character between worlds.
- **Live run state — per-character JSON** next to the existing mod config,
  keyed by character ID: elapsed, heat, splits, challenge progress, boons,
  cooldowns. Written on every state change; deleted on run end/abandon.

## Architecture

- **`IRunService`** (new, registered in `ModBootstrap` like existing
  services): run lifecycle, timer, heat, score, persistence. Owns:
  - **`ChallengeSystem`** — pool, rolling, poll- and event-verification.
  - **`BoonSystem`** — pool, offers, active-boon state and cooldowns.
- **`RunWindow`** — new IMGUI window in the UIManager pattern (lobby / Heat
  HUD / boon offer / always-on strip).
- **`GameEvents`** (new static class): receives patcher-injected callbacks
  and exposes C# events the systems subscribe to.
- **Patcher change:** second injection — `Character.OnDeath` → call
  `ICanShowYouTheWorld.GameEvents.OnCharacterDeath(this)`. Same Cecil
  pattern as the `FejdStartup.OnCredits` entry point. (Stretch:
  `Character.Damage` → `OnCharacterDamaged`.) Boss-death detection also
  rides this hook, replacing any need to poll global keys.
- **Config:** one new `RunMode` section holding every dial named above.

## Error handling

- Run state JSON corrupt/missing on resume → offer "resume lost, abandon
  run" in the Run window rather than crashing or silently discarding.
- Patcher hook absent (old patched assembly): event-verified challenges are
  excluded from the roll pool and the Run window shows a "re-patch for kill
  challenges" notice. Poll-verified mode still works, and boss deaths fall
  back to polling the world's `defeated_*` global keys so runs remain
  completable.
- Config values clamped to sane ranges on load (existing Configuration
  pattern).

## Testing

- Build/iterate on the Mac (fast local loop, `deploy_local.sh`).
- Balance tuning through config only — no redeploys.
- Deck/Windows via the usual deploy paths once the loop is fun. Windows kit
  needs a Patcher refresh in `dist/windows/` since the patcher changes.

## Out of scope for v1

- Hardcore mode (death ends run) — a later toggle.
- Challenge types needing new hooks beyond death/damage.
- Any multiplayer/co-op considerations.
- Leaderboards beyond the character's own best score.
