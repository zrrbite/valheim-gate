# Act I as the centrepiece — design

Written 2026-08-23, at `0.221.12-run.alpha44`.

## Why

Act I is the part of the saga that has actually been played and enjoyed, and
the thing that draws people to Valheim in the first place is the homestead —
building somewhere to live, not racing to a boss. The mode has been treating
the Meadows as a tutorial on the way to somewhere else. It should be the
centrepiece.

Owner, verbatim: *"I want to focus 100% on act 1 for a bit. Make it really fun
and balanced. With a deep quest line and focus on homestead coziness since
that's what drew me to the game."*

## Three tracks

Act I splits into **HUNT**, **CRAFT** and **HEARTH** rather than two.

The engine is already N-ary — `SetTracks` takes a list, `TrackSlot(i)` is
`-1 - i`, and the HUD iterates `Challenges.Tracks` — so this is content and
routing, not machinery. Track 0 stays HUNT so the historical `MainQuestSlot`
(`-1`) keeps meaning what it always meant.

`Split()` gains a third bucket. Kind remains only a PROXY for track — kills go
to HUNT, everything else to CRAFT — and the hearth steps carry an explicit
`Track` override, which is the mechanism the discovery steps already use.

**Empty tracks are dropped.** Acts II-V have no hearth steps, and an empty
column on the tracker reads as a bug rather than as an absence.

Why three rather than a longer CRAFT track: a single 25-step queue puts every
slow step in front of everything behind it, and the two slowest things in the
act — breeding boar and filling a food bar — are both cozy. On their own track
they cost nothing. It also gives the homestead its own name on the tracker
instead of being "the non-kill stuff".

### The tracks

```
HUNT    boar, greylings, deer, the Herald, find the altar, Eikthyr
CRAFT   axe, hammer, bench, shelter, fire, cooking station, bed, chest
HEARTH  proper meal, settle in, sleep, comfort, trophy, fishing,
        foraging, tame a boar, a pen of three
```

`Settle in`, `Sleep through the night` and `Tame a boar` move from CRAFT to
HEARTH. None of them is a craft; they were there because CRAFT meant
"everything that is not a kill".

## Fishing

No fish stat and no trade stat exist, and the rod is Haldor-only in vanilla —
Haldor spawns in the **Black Forest**, outside the act.

**The saga grants the rod**, as the reward of the `Herald` step. The mode already
hands over bows, armour and seeds; a rod is nothing new, and it means fishing
arrives when the questline wants it rather than when the map cooperates.
Stumbling on Haldor becomes an Act II step, where the Black Forest is where
you already are.

Measured as a **reading, not a stat**: the host counts inventory items whose
drop prefab is named like a fish and which are food, reported as
`PlayerState "FishHeld"`. Counting by prefab rather than by localisation token
covers every species and both raw and cooked, and avoids naming asset data the
assembly cannot verify — the oldest landmine in this mode.

## Homeward, on a timer

Homeward is currently one charge per boss felled. It becomes **free on a ten
minute cooldown**, with boss charges still stacking on top:

```
Keypad 9:
    charge held?  -> spend it, exactly as now
    else ready?   -> free gate, start the cooldown
    else          -> "Homeward returns in 4:12"
```

Ten minutes is long enough to plan around and short enough never to strand
anyone. Charges are spent FIRST, so a boss kill still buys something the
cooldown does not.

The cooldown is session state, not run state: being sent home by a reload is
harmless, and persisting it would mean a save-scum check for no gain.

Config: `runHomewardCooldownMinutes`, default 10.

## The act ends on a kill, and that destroys tracks

`RefreshAct` swaps the whole chain on a boss death, so every unfinished step on
every track is discarded, rewards and all. HUNT is six steps and HEARTH is
nine: following the hunt naturally destroys most of the homestead.

Act I's altar step therefore carries `RequiresTrackComplete = "hearth"`. Until
the hearth is done it stays visible but dimmed, under the saga's own reason:

> *The altar sleeps. Eikthyr answers only a hunter with a home.*

A blocked step takes **no credit at all**, not merely a hidden display —
otherwise the gate would delay the label while the step quietly completed
behind it. Blocked state is recomputed every tick rather than latched, so the
gate opens on the very tick that finishes the hearth.

**A gate naming a track the act does not have counts as satisfied**, so Acts
II-V — which drop the hearth entirely — can never be deadlocked by one. That
makes a typo silently vacuous, so `ValidateActs` reports a gate naming a track
absent from its own act.

The fishing rod moved from the trophy step to the **Herald**, so it arrives on
the hunt track and never depends on hearth progress: there is room to sit by
the water before the god is called.

## What is deliberately not here

- **Planting.** Cultivated soil needs a cultivator, which needs bronze, which
  is Act II. A garden step in the Meadows would be unachievable.
- **Finding Haldor.** Act II, for the reason above.
- **Heat tuning.** Still untuned across the whole saga and still needs a real
  answer to "at what heat does it stop being fun" — see RESUME.md. Act I
  balance work here is content balance, not heat balance.

## Risks

- **Comfort 5** assumes fire + bed + chair + table reaches it with wood alone.
  If banners or rugs turn out to be needed, the target is too steep. One line.
- **The 40m pen radius** may not match how people actually build. One line.
- **Fish prefab naming** is the one place this design touches asset data. The
  runtime validator will report an unknown reward prefab for the rod and bait;
  the FishHeld count fails silently if no prefab matches, so it logs a warning
  when a fishing step is active and nothing in the world looks like a fish.
