# The saga gets acts

Written 2026-08-22, shipped in `0.221.12-run.alpha28`.

From play feedback: *"when I completed Act 1 it just said 'Act 1 complete' and
then nothing more happened."*

That was literally true. `RunWindow.cs:631` printed a hardcoded string whenever
the quest chain ran out, and there was no Act II — after Eikthyr the run was
random tasks and a boss table, with no thread. This gives the saga five acts and
makes crossing between them an event.

Also here: a one-line HUD fix from the same session.

## The HUD label

`BoonStatus` writes into a fixed 104px column (`BoonStatusWidth`). Passives print
"always on" there; actives printed `ready  [Keypad 8]`, which does not fit and
spilled over.

Actives now read **"Activated"** — the parallel to "always on", and nothing more.
The key and live state are not lost: the activation strip above the list already
shows `[8] Windfall x1`, which is where to read them. `ActivationKey` became dead
and was deleted rather than left as a second keybind table to drift out of sync.

## Acts as data

```csharp
class ActDefinition {
    string Id;              // "act2"
    string Numeral;         // "II"
    string Title;           // "The Black Forest"
    string BossDefeatKey;   // "defeated_gdking"
    List<ChallengeDefinition> Chain;
}
```

Five acts, aligned one-to-one with the boss table that already existed. The
engine is untouched — `SetMainChain` always took any list; it just used to be
handed the same one forever.

`Banner` ("ACT II — THE BLACK FOREST") heads the quest section and announces a
transition; `Label` is the prose form for logs.

## The current act is derived, never stored

Act index = **the number of bosses the world records as dead** — the same reading
`RefreshMaxTier` already takes.

This gets several things right without any new state:

- It cannot drift from the world.
- A resume recomputes it rather than trusting a save, so no new save field and no
  migration.
- Start a run on a world where Eikthyr is already dead and you correctly begin in
  Act II rather than replaying the Meadows.

`CurrentActIndex` returns the *current* index unchanged when `ZoneSystem` is
null — a momentary missing world must not read as "no bosses dead" and throw the
run back to Act I.

### The ordering that matters

The transition hooks the existing boss poll, right after `RefreshMaxTier`. That
is safe because `_challenges.Tick` runs **every frame** while the boss poll runs
**once a second**: by the time the poll reads the defeat key, the boss step's own
completion — and its reward — has already fired many times over.

Moving the transition into the per-frame path without rechecking that would
swap the chain out from under an uncompleted final step and silently eat the
act's last reward. The code says so where it happens.

`RefreshAct(announce:)` distinguishes a transition from a restore. Crossing into
an act mid-run deserves a banner; starting or resuming into one is just where the
run already was, and announcing it every resume would be noise.

## Content

**Act II — The Black Forest → The Elder** is written in full, on Act I's terms:
small steps, item rewards, every measure already proven. `MineHits` and
`CraftsOrUpgrades` are `PlayerStatType`s; `Smelter` is a compiled class, so the
smelter step carries no more risk than the cooking station did.

The boss step kills `gd_king`. The boss *table* holds `GDKing` — that is the
**location**, a different string. Precisely the confusion the alpha27 validator
now catches on first launch rather than after an unwinnable act.

Ancient Seeds are handed over rather than farmed, exactly as deer trophies are in
Act I: the Elder's altar wants three, they drop from shamans and brutes, and an
act finale never gates on drop luck.

**Acts III–V get three or four steps each.** Deliberately thin: enough that
beating a boss is never the dead end Act I was, and no more than that until
someone has played that far and can say what belongs there. Thin is honest;
absent is a bug.

## Invariants, checked twice

`ActDefinitionTests` asserts the rules an act must satisfy — but only against a
stand-in table, since the real one lives in game-coupled code the unit harness
excludes. So `ValidateActs` asserts the same rules against the **real** table at
run start:

- **Step ids unique across the whole saga.** This is the dangerous one:
  `RestoreMainQuest` resolves a saved position by id against the current act's
  chain, so an id appearing in two acts lets a resume seat the wrong act's step —
  and a step completes the moment `Progress >= Target`, so that fires an
  unearned completion, rewards included, on the first tick.
- Every act ends on its boss, which is what makes "the chain ran out" and "the
  act is over" the same event.
- Every act's boss key exists in the boss table.

## Testing

299 assertions, up from 276.

The transition, the HUD and `ValidateActs` live in `RunMode/Unity/**`, which
`Tests/run_tests.sh` excludes as game-coupled — play-test verified, see the
alpha28 task in `HANDOFF_WINDOWS.md`.

## Decided, not yet built (alpha29)

Captured here so the decisions survive the session:

- **Deer focus in Act I**, all four ideas approved: starred deer ("Eikthyr's
  Herd"), a named 2-star Herald as a late Act I step, contested kills drawing
  greylings to a carcass, and lightning on the kill. Note the constraint that
  shaped these: **deer cannot be made to attack** — they run `AnimalAI`, which has
  no attack at all, and giving them one is Unity asset work this build cannot do.
- **Universal storage** as a run-window stash panel: deposit and withdraw
  anywhere, persisted with the run, storing prefab + count + quality + variant so
  upgraded gear survives (durability resets to full). Rejected: making every
  built chest share one inventory, which would need a third patcher injection and
  a re-patch of every machine.
