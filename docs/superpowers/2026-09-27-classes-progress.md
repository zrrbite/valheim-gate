# Classes — progress record

The plan is `docs/superpowers/specs/2026-09-27-classes-design.md` (reasoning) and the
"Integration notes" at the top of `docs/valheim-classes-plan.md` (the brief, kept current for
Claude-web). This file is the ONLY place that says how far the work has got. Each phase ends with a
milestone the owner plays before the next phase starts; nothing below a milestone is begun until
the one above has a verdict.

Legend: `[x]` done and verified by build + tests · `[p]` built, awaiting the owner's play verdict ·
`[ ]` not started.

## Phase 0 — the brief and the spec (docs only)

- [x] `docs/valheim-classes-plan.md` opens with Integration notes; §1, §2, §5, §8, §8.1, §10 marked
      superseded; §13's boon TODO answered.
- [x] `docs/superpowers/specs/2026-09-27-classes-design.md` written.

## Phase 1 — pure engine, migration, keys, state — MILESTONE 1

Built and installed as **`1.0.16-run.2026-09-27`** (commit `630bd91`; tests 617, all pass). Steam
moved the game to 1.0.16 the same morning, so the mod was rebuilt against it first (commit
`65c2159`, no source change; every reflected member still resolves). The stray tag
`1.0.15-run.2026-09-27` was cut minutes before the update was noticed and was never installed.

- [p] `BoonDefinition.ClassId`; offers skip class boons; death skips class boons; `Revoke` for dev.
- [p] `ClassDefinition` + `ClassLadder` (pure, tested); `ValidateClassLadder` at run start.
- [p] Nine boons re-tagged, four new rows (`wrath`, `rend`, `rage`, `warcry`) — effects come in Phase 2.
- [p] `classId` in run state; save, restore, clear.
- [p] `BoonKeys`: Keypad7 / Keypad0 / Insert are the three rung slots; handler fires the HELD id.
- [p] Dev keys `mod+Keypad*` (cycle class) and `mod+Keypad/` (learn all rungs); DEV-MODE.md.
- [p] THE WAY block on the HUD above BOONS.

Known and accepted for now: a save from before this build that holds `brother` with no way, whose
player then picks Völva, has `brother` and `shaman` both on Keypad7 and the first held row fires.
The four new abilities answer "not ready" until Phase 2. CLAUDE.md, RESUME.md and the story bible
still name Shaman's Mercy on Keypad+ — Phase 4 fixes the docs in one pass.

**Milestone 1 — what to play** (needs `"runDevMode": true`):
1. Start a saga. THE WAY reads "no way chosen yet".
2. `Shift+Keypad*` → Hunter. BOONS lists Hunter (Bows 50), Shepherd, Packbrother. `Keypad7` summons
   the wolf. Random-task offers never show a pet, heal or skill boon.
3. `Shift+Keypad/` → learns every rung. Hunter: `Keypad0` Menagerie, `Insert` Unseen.
4. `Shift+Keypad*` again → Völva: Hearthlight, Mending on `Keypad7`; `Keypad0` Bonecaller; `Insert`
   Thor's Wrath says "not ready"/does nothing (Phase 2). Again → Berserker: Warrior; `7/0/Ins` do
   nothing yet (Phase 2). Again → none.
5. Die with a class held: a GENERAL boon is lost, the class boons stay.
6. Quit to menu, resume: THE WAY still names the class; passives reapplied; keys work.
7. Abandon the run: `Player.log` shows loans repaid and companions despawned; no class remains.

Verdict: _pending_

## Phase 2 — the four new effects — MILESTONE 2

Built and installed as **`1.0.16-run.2026-09-27b`** (commit `a889ece`). The owner asked to test
Milestones 1 and 2 together.

- [p] `rage`, `rend`, `warcry`, `wrath` in `BoonEffects`; lightning flash via `SagaItems.Lightning()`
      with its `Aoe` stripped and a 4 s removal through `ZNetScene.Destroy`.
- [p] Hugin line when a rung comes due (`AnnounceClassRung` → `TrySpawnRaven`, once per boss count).

Numbers (first picks, unplayed): Rage ×1.5 for 15 s, Weak to blunt/slash/pierce meanwhile; Rend 5 m,
20 slash + 15 poison; Warcry 8 m, no bosses; Wrath 6 m at the aim point, 40 lightning, stagger ×1.5,
25 eitr or 30 stamina. Rend and Wrath scale ×(1 + 0.25·bosses), capped ×3.

Known: Rend's poison ticks carry no attacker, so a kill by the bleed alone may not credit the player
(kill challenges). Neutral animals count as targets, as in `StaggerAoE`. `RefreshWeaponDamage` now
rewrites every snapshotted weapon, not only the equipped one, so a weapon put away mid-rage does not
keep ×1.5 — the behaviour is unchanged for boons that never switch off.

**Milestone 2 — what to play**: the table and list are in `HANDOFF_WINDOWS.md` under this build.

Verdict: _pending_

## Phase 3 — the thane — MILESTONE 3

Built and installed as **`1.0.16-run.2026-09-27c`** (commit `8306eae`; tests 622, all pass). The
owner asked to test Milestones 1, 2 and 3 together. The play list is in `HANDOFF_WINDOWS.md`.

- [p] `mq-thane` step directly after `mq-bed` (Act I: 22 steps, heat 55); `SagaNames.ThaneFound`;
      `StepPredicates.Thane`; validator entry.
- [p] `Thane.cs` actor: Ghost, day-gated, 60–120 m from the claimed bed on level dry ground
      (three passes, logged), third palette (colour kept, darker, moss-green glow, normal size);
      `PollThane` in every act, since `StepDone` only reads the current act's chains.
- [p] THE WAY card in the offer window (Keypad1/2/3, times out like an offer, reopens on speaking);
      `TakeUpWay` = choose + his ChosenLine + a BOOK line; boon offers are OWED while the card is up.
- [p] Teaching on interact when a rung is due; bearing to him on the strip (day only for rungs).
- [p] Text from `thane-text.md` verbatim; glossary entries in the story bible.

Known: the dev step-skip completes `mq-thane` without opening the card (speak to him instead);
`ChooseClass`'s centre message shows beside his rune line; an owed boon offer is not saved across a
reload; a boon offer already up when he is spoken to hides behind the card and keeps ageing. The
dream was skipped: `SagaDreams` gates on world keys only.

Verdict: _pending_

## Phase 4 — docs, atlas, tag

- [x] RESUME.md, CLAUDE.md keys, DEV-MODE.md, story bible glossary, HANDOFF TASKs, atlas regenerated
      and republished (shipped with Milestone 3, `...27c`).
- [x] Tags, release builds, installs — one per milestone.

## Follow-up — the general pool refilled (`1.0.16-run.2026-09-27d`, commit `4a1de8f`)

Staged while the owner was testing; **not installed** until the game is closed
(`.\dist\windows\Install-Mod.ps1 -ModOnly`). Nine general boons bring the pool back to 30: Miner,
Wayfarer, Steady Hands (skill loans); Thick-skinned, Hardshell (resistances); Kindling (heat);
Stoker (risk); Mending Hands on `Keypad+` and Farsight on `Keypad-` (actives, deliberately not
loans). Found on the way and fixed: a risk boon's `Weak` no longer deletes a held resistance, and
an active's success line is now shown. Play list in `HANDOFF_WINDOWS.md` under `...27d`.

Verdict: _pending_
