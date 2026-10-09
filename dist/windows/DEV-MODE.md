# Dev mode — testing shortcuts

Skip the waiting parts of a play-test: step completion, the night gate,
material farming, and the light race's setup.

## Turning it on

**Dev builds only.** A saga-only build (the one handed to somebody else) ignores this setting
entirely — no shortcuts, no red line in the Run window. Check the main menu badge ends in `· DEV`.

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

**Seven are bare. Five want a modifier.** (Plus two temporary MacBook keys, `↓` and `mod` + `Keypad .`, at the end of the table.) `Keypad +` and `Keypad -` need one
because those two keys were the player's — **Mending** (then Shaman's Mercy)
and **Unseen** activated on a bare press. Since the ways (classes, 2026-09-27)
those two moved to the ways' rungs (Mending is the Völva's `[0]`, Unseen the
Hunter's `[Ins]`) and the keys are free, but the modifier stays: moving a
tester's hands twice is worse than a modifier that no longer guards anything.
`Backspace` needs one for a different reason: it is the only
dev key that **builds**. And `Keypad *` and `Keypad /` have a **second layer**
with a modifier — the ways — while the bare press does what it always did.
`Shift`, `Ctrl` or `Alt` all count; use whichever hand is free.

| Key | Effect |
|---|---|
| `mod` + `Keypad +` | Complete the current step on **every** unblocked track |
| `mod` + `Keypad -` | Push the clock forward **2 game hours** (press until it says "it is night") |
| `Keypad *` | A chest's worth of materials, **into the stash** |
| `Keypad .` | Drop a **deer's light** at your feet |
| `Keypad /` | **God mode** + a fighter's kit **+75% speed** (toggle) |
| `Keypad Enter` | **Gate to your claimed bed**, free, no cooldown |
| `Delete` | **Slay everything hostile within 10m** |
| `Home` | **Teleport to the map cursor** (the GM mod's own teleport) |
| `PageUp` | **Dump what the creature in view is made of** to the log |
| `mod` + `Backspace` | Plant a **Storm-Anvil**, claimed, plus the bow, the shield and both bills |
| `mod` + `Keypad *` | **Cycle the way** through all seven and back to none. Gives back the old way's boons first |
| `mod` + `Delete` | **Thor's bow, the Stormward and five rescued lights** straight into the pack, no anvil, no lever (bare `Delete` slays) |
| `mod` + `Keypad /` | **Learn every rung** of the held way now, without the bosses the ladder asks for |
| `↓` (and `mod` + `↓`) | **Go to** the next of the saga's places (with a modifier, the one before). **Temporary**, see below |
| `mod` + `Keypad .` | **A ship at sea**: a Karve on the nearest deep water, you at its helm. **Temporary**, see below |

Keys only work during an active run.

### `↓`: go to (temporary, for testing on a MacBook)

Added 2026-10-06 for testing on a MacBook with no mouse, where walking two kilometres to a speaker
is out. It is the same key in both layouts, and it will be **removed** once testing is back at a full
keyboard: every piece of it is marked `MACBOOK-TEMP` (`grep -rn MACBOOK-TEMP`).

Each press goes to the next place on the list; `mod` + `↓` goes back one. The message says where:
`DEV: → Haldor's camp (2 of 5)`. The list is built fresh each press, current act first:

- every speaker who has a spot: the shade, Thjalfi, the thane, and from Act II on the act's speaker.
  A speaker chooses his spot when his step first wants him, so he is on the list from then;
- the current act's boss altar;
- Haldor's camp, the Bog Witch's hut and Hildir's camp.

You land a few metres to one side, facing the place. A far hop shows the loading spin, as the map
teleport does. Walk the last bit: the Bog Witch's hut stands on stilts.

### `mod` + `Keypad .` / `mod` + `B`: a ship at sea (temporary, for testing on a MacBook)

Added 2026-10-06 with the go-to key, and removed with it. A **Karve** on the nearest deep water,
claimed as yours (so it counts as built, the helm offers fittings, and a hammer takes it down), and
you put at its helm. B for boat on the laptop; bare `B` is still the deer's light.

- **Sea within 40 m:** it is built there and then, and you are put on deck at the helm.
- **Farther (up to 4 km):** you are teleported to the shore short of it first, with the loading spin,
  and the ship is built once you are there. The message says how far: `DEV: the nearest sea is 840 m away`.
- **The helm is taken for you** once you stand on the deck: `DEV: at the helm.` If that hasn't happened
  within twelve seconds (over-weight, say), it tells you to press `E` at the helm yourself.
- The prow points out to sea, away from where you stood. `W` sails; `Shift` + `E` opens the fittings.
- **Aboard a ship over open water, `mod` + `B` calls the sea instead** (2026-10-08): an encounter now, chosen
  as normal for the place and by your heat, ignoring the odds, the quiet minute, the cooldown and the limit.
  `Player.log` says `Sea:` and what came.

**On a laptop** (no numpad; `runKeyLayout: "laptop"` in the config, since 2026-10-06) the
numpad keys move to letters Valheim leaves free, and the rest stay:

| Numpad | Laptop |
|---|---|
| `Keypad *` (and `mod` + `*`) | `Z` (and `mod` + `Z`) |
| `Keypad /` (and `mod` + `/`) | `0` on the main row (and `mod` + `0`) |
| `Keypad .` | `B` |
| `Keypad Enter` | `Backspace`, bare (`mod` + `Backspace` is still the anvil kit) |
| `mod` + `Keypad +` / `mod` + `Keypad -` | `mod` + `P` / `mod` + `N` (bare `P` and `N` are the player's) |
| `Delete`, `Home`, `PageUp` | the same; on a MacBook `fn` + `Backspace`, `fn` + `←`, `fn` + `↑` |
| `↓` (go to, temporary) | the same |
| `mod` + `Keypad .` (ship, temporary) | `mod` + `B` (B for boat) |

Dev keys stand down while you are typing (chat, console, a text field), so a `Z` in a chat
line plants nothing.

**In the game, `Shift` + `End` opens this table** in a window in the middle of the screen, in your
layout's keys, and closes it again (since 2026-10-07). The HUD keeps one red line, `DEV · Shift+End:
dev keys`, so dev mode is never on unseen. The window is generated from `RunService.DevKeyTable`,
the one place the descriptions live, beside the handler that reads the keys. Until 2026-10-07 it
was six red lines above QUESTS, which the owner found were "clogging up the run-ui". The help went
stale once, which is the whole reason this page's history below exists.

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
missed modifier can hide a whole layer is three keys wide instead of nine.

### Why `Backspace` joined them (2026-09-20)

Not because it collides — it collides with nothing. Because it is the only key
in the layer that **leaves something behind**. Everything else here is
transient: a toggle, a teleport, a report, a light that burns out, a pack of
materials. `Backspace` raises a permanent, claimed building, so an accidental
press is the only one in the set that has to be cleaned up afterwards. It is
also a large key next to ones used in ordinary play, which makes the accident
easy (owner, testing with a world full of them: *"oK, BACKSPACE, should be mod
+ backspace, heh. Ive got anvils all over"*).

So the rule has two clauses now, and both are narrow: **a modifier where a key
collides, and where an accident persists.** Nothing else in the dev layer
builds, so nothing else qualifies.

**To clear the ones already standing:** they were planted with `SetCreator`, so
they are yours — a hammer should remove them like any piece you placed.

That key reported the wrong thing for a long time, and the play log of 2026-09-20 caught it:
twenty-six consecutive `DEV: +2h - still light` lines. The clock WAS moving; the message was
read in the same frame as the write, and `EnvMan` only recomputes `s_isNight` in its
`FixedUpdate`, from a day fraction it lerps toward at 0.01 a step. So the answer printed was
always the state before the jump.

The press now says only `DEV: +2h`, and what the sky is doing follows a second and a half
later, once that is knowable. A "wind forward until it is night" version was tried and
reverted at the owner's request - it blew through its own three-day cap inside two seconds
of real time while the smoothed fraction was still catching up with the first step, then
reported giving up having moved the clock three days.

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
  arrows, food, seeds, a fishing rod and bait, surtling cores, 500 coins for the
  helm's ship fittings…) **into the
  run's stash**, not your pockets — the raw materials alone are several
  hundred weight, and granted to the inventory they left you over-encumbered
  on the spot. Withdraw what the moment needs from the stash panel.
- **`/` (god)** toggles the mod's god mode — normally gated off during a run —
  and grants bronze arms plus the best food the game has (serpent stew, blood
  pudding, sausages) the first time it turns on, plus +75% run/walk speed for
  as long as it is on. Top-tier food is where Valheim HP actually comes from,
  so "more hp" means eating better, not a bigger chestpiece. Toggle it OFF to test dying, which is itself part of the
  design (death costs heat and a boon).
- **`mod` + `*` (way)** takes up the next way through the same `ChooseClass` the
  thane will use, so the passive and rung 1 arrive as they would in play. A real
  run never changes its way; this revokes the old one's boons (repaid on the
  ordinary Lost path) before choosing the next, which is the only place a way
  is ever taken back.
- **`mod` + `/` (rungs)** grants every rung of the held way at once — rung 3
  wants two gods down otherwise. The tempering after that (Bonemass to the
  Queen) is not granted: it follows the gods the world records as fallen, which
  neither this key nor `mod` + `+` can set (a bare `Delete` on a real god does).
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
