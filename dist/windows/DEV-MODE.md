# Dev mode — testing shortcuts

Skip the waiting parts of a play-test: step completion, the night gate,
material farming, and the light race's setup.

## Turning it on

Edit the mod's config JSON and set:

```json
"runDevMode": true
```

The file lives at:

| Platform | Path |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICanShowYouTheWorld.json` |
| Linux / Steam Deck | `~/.config/unity3d/IronGate/Valheim/ICanShowYouTheWorld.json` |
| macOS | `~/Library/Application Support/unity3d/IronGate/Valheim/ICanShowYouTheWorld.json` |

Restart the game to apply (config is read at startup). The Run window shows
**DEV MODE** in red while the flag is on — if you don't see it, the flag is
not on.

It ships `false` and nothing in the mod ever turns it on by itself.

## The keys

**Seven are bare. Two want a modifier.** Only `Keypad +` and `Keypad -` need
one, because those two keys are also the player's — **Shaman's Mercy** and
**Unseen** activate on a bare press. `Shift`, `Ctrl` or `Alt` all count; use
whichever hand is free.

| Key | Effect |
|---|---|
| `mod` + `Keypad +` | Complete the current step on **every** unblocked track |
| `mod` + `Keypad -` | Push the clock forward **2 game hours** (press until "it is night") |
| `Keypad *` | A chest's worth of materials, **into the stash** |
| `Keypad .` | Drop a **deer's light** at your feet |
| `Keypad /` | **God mode** + a fighter's kit **+75% speed** (toggle) |
| `Keypad Enter` | **Gate to your claimed bed**, free, no cooldown |
| `Delete` | **Slay everything hostile within 10m** |
| `Home` | **Teleport to the map cursor** (the GM mod's own teleport) |
| `PageUp` | **Dump what the creature in view is made of** to the log |

Keys only work during an active run.

The Run window's own DEV MODE banner prints this same table in short form, generated from
`RunService.DevKeyHelp` — the one place the strings live. It went stale once, which is the
whole reason this page's history below exists.

### Why it is not "Shift plus everything"

It was, briefly, and it was wrong — though what actually bit was the help text, not the
modifier: the banner still listed the bare keys, so the tester pressed them and reported
the layer dead. A help line that is wrong is worse than no help line at all.

The modifier itself was also over-applied. The reasoning behind the blanket modifier was
sound as far as it went: unlike the GM mod's bindings, which `InputManager.Gate`
makes dead during a run, dev and the player's actives are read from the *same*
handler in the same mode, so a modifier is the only thing that can separate the
two layers. What that argument does not justify is applying it to keys where
there is no second layer — nothing in the saga binds `Keypad * / . Enter`,
`Delete`, `Home` or `PageUp`, so a modifier there protected nothing and cost a
tester their muscle memory ("the dev mode commands dont seem to work * / - +,
etc.").

The rule now states only what is true, which also means the surface where a
missed modifier can hide a whole layer is two keys wide instead of nine.

Every dev key now **also writes its line to `Player.log`**, prefixed
`[ICanShowYouTheWorld] DEV:`. That is deliberate: when the Shift layer was
reported broken the log could neither confirm nor deny a single press, so the
fix had to be reasoned from the keymap rather than from evidence. Next time it
is a grep.

## What each is for

- **`+` (skip)** advances by setting the step's progress to its target, so
  completion runs the *ordinary* path — rewards land, boon grants fire, the
  forfeit check runs. What you see after a skip is what a real player would
  see after finishing the step. It completes every track's current step at
  once; there is no per-track version.
- **`-` (clock)** exists for the night-gated hunt. It moves the world clock
  via the network time, a fixed step per press, and tells you whether it is
  night yet after each press.
- **`*` (materials)** puts a fixed kit (wood, stone, ores, nails, hides,
  arrows, food, seeds, a fishing rod and bait, surtling cores…) **into the
  run's stash**, not your pockets — the raw materials alone are several
  hundred weight, and granted to the inventory they left you over-encumbered
  on the spot. Withdraw what the moment needs from the stash panel.
- **`/` (god)** toggles the mod's god mode — normally gated off during a run —
  and grants bronze arms plus the best food the game has (serpent stew, blood
  pudding, sausages) the first time it turns on, plus +75% run/walk speed for
  as long as it is on. Top-tier food is where Valheim HP actually comes from,
  so "more hp" means eating better, not a bigger chestpiece. Toggle it OFF to test dying, which is itself part of the
  design (death costs heat and a boon).
- **`Enter` (home)** teleports to your claimed bed with no charge and no
  cooldown — the real Homeward's economy is not usually the thing under test.
  Needs a claimed bed, and says so if there is none.
- **`Delete` (slay)** kills every non-tamed creature within 10m through the
  ordinary damage path, so deaths still count: a nuked deer drops its light,
  fires its quest, draws its pack. The fast path tests the same machinery as
  the slow one.
- **`.` (light)** spawns a deer's light six metres ahead, exactly as if a
  deer had just died there — timer, bar and scoreboard all live. The race is
  otherwise the hardest moment in the act to reach (a deer, at night, while
  the step is active); this tests the pickup without needing all three.

## Honesty notes

- Anything you do with these keys still **writes to the run's real state**:
  skipped steps pay their rewards, granted materials are real items, and a
  light you let fade counts on the forfeit scoreboard. There is no sandbox.
  Use a throwaway world/character for testing, same as the release notes say.
- The **DEV MODE** line in the HUD is deliberate and not removable while the
  flag is on. If a screenshot or a bug report shows it, the run had shortcuts
  available — that context matters when judging what "happened".
- Turn it off by setting the flag back to `false` (or deleting the line) and
  restarting.
