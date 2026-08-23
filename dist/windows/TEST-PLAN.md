# Test plan — alpha39

Covers everything shipped since the last real play session (alpha33). Six builds
went out in that window, so this is ordered by **what catches the most for the
least play**, not by build number.

Install, then work down. Anything marked **BUG** is worth stopping to report.

---

## Minute one — three checks, before you do anything else

**1. Version popup reads `v0.221.12-run.alpha42.2`.**
The installer prints the version it installed; the popup must match. If it doesn't,
the install didn't take and nothing below means anything.

**2. NO boss pin on the map at all.**
Since alpha39 an altar is pinned only when the questline asks you to find it — in
Act I, after the Herald falls. Any boss pin at run start = the gate didn't take.

(The vanilla rune stone near spawn still works if you read it. That's you choosing
to skip the mystery, and it's left alone on purpose.)

**3. The log grep.** Start a run, then:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld.*Unknown"
```

Silence is the pass. This checks every creature, item, reward, biome and location
name across all five acts — see [CHECKING-THE-LOG.md](CHECKING-THE-LOG.md).

---

## The HUD (alpha38 — new, and only needs a look)

Should read:

```
SAGA — ACT I
THE MEADOWS
  0:04:12              Heat 3
  Saga score 96
  QUESTS
    HUNT   Hunt 5 Boar        2/5
    CRAFT  Craft an axe       0/1
           A stone axe. …
```

- The act is the headline; the clock is small.
- **The act line appears ONCE.** Twice = a regression.
- **Question for you:** with the timer demoted, does it still tell you what you
  need mid-fight?

---

## Act I, in play order

**Hints.** Steps that need explaining carry a line under them. **Any hint that is
factually wrong is worth reporting** — I wrote them from knowledge of the game, not
from anything the code could verify.

**+health earned** appears in the HUD after your first completion and climbs by 2
each time. This is the answer to heat going up.

**Starred deer.** Roughly half the deer in Act I should be visibly bigger and take
several arrows. Deer still can't hurt you — that's a hard engine limit, not a bug.

**Deer kills draw greylings** about one time in three. If it fires every time or
never, tell me the config knob feels wrong.

**Bed before settle-in.** CRAFT order should be `… fire → cooking station → bed →
settle in → sleep → chest`. Settle-in asking before you have a bed = **BUG**.

**The stash.** Own window bottom-left, beside the tracker, scrollable. "Deposit
materials" should take ore/wood/hides and **leave your food and arrows alone**.
Suspend and resume — contents must survive.

---

## The Herald (the one I most want checked)

**alpha38 was broken here** — the Herald was spawning outside the loaded area, so
the game kept culling it and the mod kept spawning replacements at new random
spots, once a second. There were dozens; none where the bearing pointed.

alpha39 remembers a PLACE instead. What should happen now:

- **The bearing is on the always-on strip** under the timer — *"The Herald's tracks
  lead north-east, 140m"* — visible without opening the run window, updating as you
  walk.
- Walk it. The distance should count DOWN steadily. **It must not jump somewhere
  new.**
- The Herald appears when you get within ~60m of where it's been pointing.
- **Exactly one.** If you ever see two, the respawn loop is still alive.
- It survives a save/resume in the same place.
- **Killing an ordinary deer must NOT complete the Herald step.** If it does, that
  is the most important bug on this page.

---

## The act transition (never yet seen working)

Find Eikthyr's altar → the discovery step completes → kill him. Then:

1. **ACT II — THE BLACK FOREST** banner
2. HUNT and CRAFT reseat with Black Forest steps
3. **A Homeward charge** is granted — Keypad 9 returns you to your bed
4. The Elder's altar pin should **NOT** appear yet — not until Act II's own
   discovery step comes up, after the troll

Any of those four missing → **BUG**.

Homeward with no bed claimed should refuse and say so, **without** spending the
charge.

---

## Act II — the thing that was broken

**"Build a smelter" must be buildable the moment it's asked for.** The mining step
before it now pays 8 surtling cores. If you find yourself hunting burial chambers
for cores, the fix didn't work.

---

## Boons — two specific checks

**1. Hearty + Glass Cannon together, then finish or abandon the run.**
Your max health must be **exactly what it was before the run**. This is the bug
alpha35 fixed and it is the one I'd most like confirmed, because getting it wrong
permanently alters your character.

**2. Forge-fed should go DOWN after a death.**
Its damage scales with heat, and dying costs heat. If it only ever ratchets up,
that's the failure mode I designed against.

Also: **a resistance boon offered in Act I is a BUG** — Irongut needs 1 boss down,
the other two need 2.

---

## What I'm not asking you to test

Acts III–V. They're written and name-validated but nobody has been there. If you
get that far, the useful feedback isn't bugs — it's **what those acts should
actually be about**. They're deliberately thinner than I and II.

---

## Reporting

Paste log lines verbatim; they name exact steps and items. For anything else, "the
X felt Y" is genuinely enough — most of the good changes this week came from a
sentence, not a report.
