# Act III Voices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Give Act III the drowned one and the Bog Witch, the Stormward's reforge, and the chain changes in the spec.

**Architecture:** Pure gates in `SagaNames`/`StepPredicates` (tested). `SagaSpeaker` gains a virtual body prefab, a spawn hook and body identity. `DrownedOne` is the second speaker on it. `HaldorVoice` generalises into `TraderVoice` (talk, give entry, proximity event, alt action through a child trigger); Haldor and the Bog Witch are configurations. `SagaItems` gains the Ironbound Stormward, sharing the Stormward's storm machinery.

**Tech Stack:** C# / .NET 4.7.2, Valheim 1.0.16, Mono msbuild, `Tests/run_tests.sh`.

**Spec:** `docs/superpowers/specs/2026-10-05-act-three-voices-design.md`

## Global Constraints

- Build clean, `ALL PASS`; no line the world does not back; speakers dressed via `ApplyWhenSettled`; prices by shared name.
- Act I's speakers untouched. Act II's behaviour unchanged by the `TraderVoice` refactor.
- Act III: 12 steps → 15; MARSH steps carry `Track = MarshTrackId`; `sw-letgo` carries `Track = CraftTrackId` (a KillPrefab would otherwise land on HUNT).

## Review Focus

1. The drowned one's death must not be raised by `FenWatch` and must not count for any other draugr — checked before `_fen`, matched by ZDO id.
2. After he dies he must not respawn — `wanted` ends with `sw-letgo` done.
3. Plain Use on the Bog Witch still opens her shop — the alt child delegates.
4. Haldor still works exactly as in `...05e` after the refactor.
5. The Ironbound Stormward discharges, flashes, wears and counts answers like the Stormward.

## Tasks

1. **Pure gates** — `WitchMet`, `DrownedFound`, `DrownedKill`; `WitchFind`, `DrownedFind`, `DrownedLetGo`, `StepLive`; tests. *(done first, TDD)*
2. **`SagaSpeaker` extensions** — virtual `BodyPrefab`; `OnSpawned(Character)` hook; `IsBody(Character)`; `BodyCharacter`.
3. **`DrownedOne` + `CreatureDressing.Drowned()`** — spot at nearest `SunkenCrypt4`, shallows allowed; phases Speak / Wait / LetGo; untame at 35% health in LetGo.
4. **`TraderVoice`** — generalise `HaldorVoice`; Haldor config unchanged; Bog Witch config with proximity event and the reforge alt action.
5. **Ironbound Stormward** — `SagaItems` definition sharing the Stormward's tune and storm; `IsStormShield`; flash for both; per-shield wear; `EquippedStormward` matches both.
6. **Chain + wiring** — `SwampChain` edits (witch, mead base, reforge, drowned, let go; cut chart; fermenter hint); cuirass gate `sw-drowned`; `PollDrownedOne`, the witch's voice; death hook before `FenWatch` with a released light; validators; location registry log.
7. **Docs, build, ship** — bible glossary + Act III paragraph; `THE-SAGA.md` Act III rewritten as built; atlas; RESUME; HANDOFF task; tag, build, stage, deploy, push.
