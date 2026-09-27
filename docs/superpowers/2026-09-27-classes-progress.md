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

- [ ] `BoonDefinition.ClassId`; offers skip class boons; death skips class boons; `Revoke` for dev.
- [ ] `ClassDefinition` + `ClassLadder` (pure, tested); `ValidateClassLadder` at run start.
- [ ] Nine boons re-tagged, four new rows (`wrath`, `rend`, `rage`, `warcry`) — effects come in Phase 2.
- [ ] `classId` in run state; save, restore, clear.
- [ ] `BoonKeys`: Keypad7 / Keypad0 / Insert are the three rung slots; handler fires the HELD id.
- [ ] Dev keys `mod+Keypad*` (cycle class) and `mod+Keypad/` (learn all rungs); DEV-MODE.md.
- [ ] THE WAY block on the HUD above BOONS.

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

- [ ] `rage`, `rend`, `warcry`, `wrath` in `BoonEffects`; lightning flash via `SagaItems.Lightning()`.
- [ ] Hugin line when a rung comes due (`PollBosses`).

## Phase 3 — the thane — MILESTONE 3

- [ ] `mq-thane` step after `mq-bed`; `SagaNames.ThaneFound`; validator entry.
- [ ] `Thane.cs` actor (day-gated, near the bed, third palette); `PollThane` every act.
- [ ] THE WAY card (offer-card pattern, Keypad1/2/3); teaching on interact; bearing.
- [ ] Text per `scratchpad/thane-text.md` (copied into code and the bible).

## Phase 4 — docs, atlas, tag

- [ ] RESUME.md, CLAUDE.md keys, DEV-MODE.md, story bible glossary, HANDOFF TASK, atlas regenerated.
- [ ] Tag, release build, install.
