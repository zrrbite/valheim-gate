# Run Mode — build notes and review findings

*This file is the RECORD of what review and play-testing caught, and why the
code is shaped the way it is. For current status and how to resume, see
[RESUME.md](RESUME.md) — as of 2026-08-22 the branch is at
`0.221.12-run.alpha25`, pushed and play-tested, still unmerged.*

*Status when the initial build finished, 2026-08-17: all 13 tasks implemented
and reviewed; final whole-branch review done, its five merge-blockers fixed and
re-verified, plus one residual (freeze-gated rerolls).*

## Baseline empowerment (as of alpha17)

Resource ×3, skill ×3, move-stamina ×0.5, stamina-regen ×2.5, **all stamina
costs ×0.75**, **zero stamina cost for melee weapons and tools** (ranged pays
25%). WoodCutting no longer opens at the cap: as of alpha17 it is the axe
step's reward at 25, and grows from there.

Two of those came from play-testing rather than design. Zero-cost melee
started life as the Pugilist boon in alpha6 and was promoted to baseline:
"not having any stamina is just annoying." The `StaminaRate` modifier and the
regen bump followed the same complaint in alpha16 ("we run out of sta WAY too
fast") — `StaminaRate` is the global lever, since `Player.UseStamina` scales
*every* cost by it, which is how it reaches blocking, dodging and jumping that
the other two modifiers miss. All of it is config-driven (`RunStaminaRate`,
`RunStaminaRegenRate`, …), so tuning it needs no rebuild.

Skills the run raises are LOANED: snapshotted before the change and written
back at run end, so a run leaves the character's own progression where it
found it. That now includes the questline's skill rewards, not just
WoodCutting — and a loan is never recorded when the player already exceeds the
grant, or giving it back would confiscate what they earned.

## Smoke checklist (Martin, on the Mac)

1. Launch Valheim → Credits → mod loads (log shows RunService registered).
2. `End` → lobby appears; F1 still shows GM windows (no run yet).
3. Start Run → bosses pinned, timer strip top-center, F1 now shows Heat HUD,
   numpad GM keys dead (try Keypad0 — no god mode, no toast).
4. Complete an easy challenge (hold 100 wood) → heat rises, boon offer panel
   → pick with Keypad2 → boon in HUD. Check a greydwarf hits harder.
5. Keypad4/5 must NOT fire GM Cloak/Immunity toasts (gate + boon routing).
6. Die on purpose → heat −3, newest boon gone; respawn → passive boons still
   apply (fleet speed).
7. Quit to menu, relaunch, rejoin → run resumes (elapsed/heat/challenges).
8. Abandon → GM mode instantly back; drop a beech: normal wood amounts
   (ResourceRate restored).
9. `Player.log` free of [ICanShowYouTheWorld] errors throughout.

Spec amendments awaiting sign-off (from final review): hardcoded v1 challenge
pool (config-driven is v2), no config clamping, boon magnitudes (fleet
+2×increment, sharp ×1.2), Waystone = nearest undefeated altar.

---

*Earlier status (2026-08-16, after Task 10):*

Companion documents: the design is
[specs/2026-08-16-run-mode-design.md](specs/2026-08-16-run-mode-design.md),
the task breakdown is
[plans/2026-08-16-run-mode.md](plans/2026-08-16-run-mode.md). Execution ran
task-by-task with a fresh implementer per task, an adversarial review after
each, and fix rounds until the review came back clean.

## Where things stand

| Task | State |
|---|---|
| 1. Test harness (mcs/mono, pure-logic only) | done |
| 2. HeatModel / RunScore / HeatEffects | done |
| 3. ChallengeEngine | done |
| 4. BoonEngine | done |
| 5. GameEvents + patcher `Character.OnDeath` injection | done |
| 6. Run Mode config block | done |
| 7. Run-state JSON + `Player.m_customData` permanent record | done |
| 8. WorldModifiers (global-key empowerment + heat) | done |
| 9. RunService orchestration | done (2 fix rounds) |
| 10. Boon effects | done (2 fix rounds) |
| 11. Enforcement gate (GM hotkeys dead during runs) | **next** |
| 12. Run UI (lobby, Heat HUD, boon offer, strip, `End` binding) | pending |
| 13. Final review + deploy + in-game smoke test | pending |

## What review caught before it reached the game

Worth reading before touching this code — most of these are properties of the
codebase, not one-off slips.

### The legacy/service split (bit us three separate times)

The mod has two parallel cheat-state worlds: the legacy statics in
`CheatCommands` (driven every frame by `CheatCommands.HandlePeriodic` via
`Cheat.cs`) and the newer DI services (`CombatService`, `BuffService`, …)
whose `HandlePeriodic` has **zero call sites**. Nothing syncs the two.

- God-mode force-off at run start targeted `ICombatService.GodMode`, which
  nothing ever sets — the real flag is `CheatCommands.GodMode` (Keypad0).
- The wind/ember boons flipped `IBuffService.AOERenewalActive/CloakActive` —
  flags nothing reads. The live driver reads the legacy statics.
- The god-mode bypass bracket initially flipped the service flag while the
  gate it was bypassing read the legacy one.

**Rule of thumb: an effect must ride the pipeline that is actually ticked.**
Today that is the legacy one.

### Permanent world corruption on resume (worst find of the build)

Valheim persists valued global keys with the world. The resume path
re-applied baseline/heat modifiers and captured the *run's own* inflated
values as the "pre-run originals" — so finishing a resumed run "restored"
3× resources and heat-scaled enemies onto the world forever. Fixed by
persisting the six original values in `RunSaveState` at StartRun and
importing them on resume before anything is re-applied. Saves predating the
field are refused (deleted with an announcement) rather than guessed at.

### Boons must actually evaporate

The spec pillar is "power is loaned". The existing Speed++/Damage++ cheats
are destructive: `SetSpeed` collapses walk speed into run speed and rewrites
jump force; `ApplySuperWeapon` writes damage *absolutely into
`item.m_shared`* — the per-prefab block, affecting every instance of that
item, permanently. Fleet/sharp now snapshot exact values on apply and
restore them on unapply/run-end. Snapshots are keyed by the `SharedData`
(prefab) block, not the `ItemData` instance — instance keying would
double-multiply after a respawn hands out fresh instances.

### Unity's destroyed-object equality trap

Respawn detection compared the cached `Player` with `!= null`. Unity
overloads that operator: a destroyed object compares equal to null, so the
check was permanently false and passive boons silently vanished on every
death. `ReferenceEquals` is the correct tool when caching Unity objects
across destruction.

### Other confirmed-and-fixed findings

- `ToggleGodMode()` as a force-off primitive **granted a buff**: it also
  resets the guardian-power cooldown and refreshes all food to 25 min. A
  side-effect-free `SetGodMode(bool)` now exists on both worlds.
- ChallengeEngine's refill cooldown was global — a second completion delayed
  the first slot's refill. Now per-slot timers.
- Rerolls were free at zero heat (heat floors at 0). Now gated on
  affordability.
- Waystone's teleport charge refilled on every save/reload (charges weren't
  persisted; re-apply re-granted). Charges now round-trip through
  `RunSaveState.heldBoonCharges`.
- The kill-hook grace window was measured from mod init (Credits) instead of
  run start.
- Runs kept accruing time in the main menu (no world-unload freeze), and a
  run could continue against a *different* world than it started in. Frozen
  + world-identity-checked now.
- The death window NREs in legacy `AoeRegen`/`DamageAoE` (unguarded
  `m_localPlayer`) became reachable through boons; guarded.
- Silent-forever failure: Tick's log-once containment could mask a
  permanently dead run. Five consecutive failures now surface a HUD notice.

## Parked with ruling (revisit at final review)

- **Keypad1/2/3 boon picks collide with GM hotkeys** — resolved by design by
  Task 11's input gate (GM bindings swallowed during runs). Verify in the
  Task 13 smoke test, including that Keypad4/5 no longer double-fire.

The full deferred-minor list (GC-pressure nits, sticky HUD notices,
Max-polling auto-complete-on-draw semantics, resume RNG position, etc.)
lives in the execution ledger at `.superpowers/sdd/2026-08-16-run-mode/progress.md`
(git-ignored, machine-local) and is triaged at the final whole-branch review.

## Resuming

Say the word; execution resumes at Task 11 (brief already extracted). After
Tasks 11–12: final whole-branch review, then build → patch → deploy on the
Mac and the in-game checklist (Credits → `End` lobby → start run → challenge
→ boon pick → death penalty → quit/resume → abandon → GM mode restored).
