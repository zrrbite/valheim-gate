# Windows session handoff

A two-way channel between the Mac session (which builds and orchestrates) and
Claude running on the Windows box. **Mac side writes task entries here and
pushes; Windows side executes, appends its results under the task, commits and
pushes back.** Newest task first. Keep results in this file (short) or in
files it names — never only in chat, where the other side can't see it.

Standing context for the Windows side:
- Branch for everything Run Mode: `feature/run-mode`. Never work on `main`.
- The mod installs via `dist\windows\Install-Mod.ps1` (full run re-patches;
  `-ModOnly` only when just the mod DLL changed).
- Log: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Player.log`
  — human-readable guide to reading it: [`dist/windows/CHECKING-THE-LOG.md`](dist/windows/CHECKING-THE-LOG.md)
- Run state: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICSYTW_run_*.json`
- End every commit with the Co-Authored-By trailer your session uses.

---

## 2026-09-20 - TASK: the storm eats the shield (`...20q`)

"*Since the shield should trigger an aoe on attack maybe the durability should be low to start? Must
be repaired often?*" — right that it should cost something, since the discharge was entirely free.
Aimed slightly differently, and the difference is the whole point:

**A low ceiling would tax BLOCKING.** Every ordinary parry against a greyling brings the repair trip
nearer, and the shield ends up worst at the thing shields are for. **Wear per discharge taxes the
POWER** — the part that is free and automatic. So each storm costs 12 durability out of 300
(+60/level), and ordinary blocking drains at the normal rate.

It also reads without a word of explanation: the durability bar drops visibly at the moment the
lightning goes off. The storm eats the shield.

Both numbers are SET rather than inherited, for the usual reason — the source prefab is an Ashlands
tower shield whose ceiling is a late-game number, and inheriting it would have made the cost
invisible. 300 against 12 is about twenty-five storms from full, plus block drain, so a heavy night
sends you home to a bench. Which is where this saga wants you anyway.

Charged in `PollStormward` rather than through a field on the attack, because `Attack` has no
durability cost of its own — it can spend stamina, eitr and health, but not the weapon. That poll is
already the one place that knows a discharge happened, so it is also the place that can bill it.

### Test it

- [ ] **Block twice, watch the shield's durability drop** by a visible chunk at the flash.
- [ ] **Ordinary blocking drains it slowly**, at nothing like that rate.
- [ ] **From full it takes roughly twenty-five storms** to need a bench. If that feels like a chore
      rather than a rhythm, `StormwardDischargeWear` is a one-line dial and so is the ceiling.
- [ ] **It does not go negative** and it does not break mid-block without the game's own warning.
- [ ] A repaired shield discharges as before.

---

## 2026-09-20 - TASK: review of the anvil work, three fixes (`...20p`)

Reviewed the last three builds while nobody was playing them. Three things, two of which would
have been reported as "it does not work".

**1. The re-cost could permanently never happen.** `TeachAltarPrefab` ran from `Ensure`, gated on
the ZNetScene reference changing - a ONE-SHOT with no second chance. If the rescued light's clone
was not ready on the frame it fired, `RecostAnvil` returned without re-costing, and the only retry
path was a live instance of a piece nobody could build, because the piece still wanted a Thunder
Stone from a trader two biomes away. Chicken-and-egg, and it would have looked exactly like "the
Storm-Anvil is not in my hammer". The prefab pass now retries from `TickStormAltar` until BOTH
halves have landed - a conversion the lever can find, and a price the Meadows can pay.

**2. The knockback was scaling an unknown.** `DoAreaAttack` computes force as the item's
`m_attackForce` times the attack's `m_forceMultiplier`, and a shield's own attack force is whatever
the source prefab carried - very possibly zero, since vanilla shields never attack. A multiplier of
120 on zero is zero, so the knockback would silently not exist; on a large inherited number it
would fire things into orbit. This is the same defect Thor's bow had when its pierce was inherited,
and it takes the same fix: `m_attackForce` is now SET on the item (80) and the multiplier is 1.

**3. The anvil burns what it does not recognise, and nothing said so.** Verified in the IL: after
every conversion has had its go, the default path turns `NrOfItemsIncludingStacks() / m_defaultCost`
of whatever is LEFT into coal. So a combine that is one troll hide short destroys the entire
contents. That is correct vanilla behaviour for a machine called the Obliterator, but the saga is
now inviting players to craft in it, so both the piece description and the two step hints say it
outright: *be exact; it burns whatever it does not recognise.*

### Also verified, no change needed

- `Incinerate` does read `m_conversions`, via `IncineratorConversion.AttemptCraft`, matching on each
  requirement's **display name** through `Inventory.CountItems` - so the saga's own cloned items work.
- `Awake` sorts conversions by priority, which happens BEFORE the mod appends, so the priority 100
  does nothing for an already-standing anvil. Harmless: the coal default is hardcoded to run after
  all conversions, so an exact match always wins regardless of order.
- `DoAreaAttack` skips the attacker's own GameObject twice - the player genuinely cannot be caught
  in their own discharge.

### One honest limitation of the dev key

`Backspace` spawns the anvil through `SpawnService`, which never sets `Piece.m_creator` - and the
built-piece scan requires `IsCreator()`, deliberately, so that world-generated ruins and other
players' houses do not count. So **a dev-planted anvil tests the lever and the combine, but will not
complete `mq-anvil`.** Use a real one for the step, or `mod` + `Keypad +` to force it. This is right
rather than broken, and is the sort of thing worth knowing before reporting it.

---

## 2026-09-20 - TASK: the Storm-Anvil is raised in Act I (`...20o`)

"*So how do i test the obliterator in this act1?*" - and then the better answer: "*we need to give
the player one, through a quest or otherwise. Recipe that requires items from act 1.*"

Vanilla gates the Obliterator behind a Thunder Stone bought from Haldor, who lives in the Black
Forest, so as shipped it cannot exist in the Meadows. The fishing rod had exactly this problem and
exactly this answer: the saga supplies it.

**The piece is re-costed to Act I materials**: 20 stone, 10 wood, 10 resin, and **one rescued
light**. `Piece.m_resources` and `Piece.m_craftingStation` are both public, so this is a table, not a
patch, and `m_craftingStation` is pointed at a workbench because whatever it wanted before was a
later act's station by definition.

The light is the design, not decoration. The Storm-Anvil is a machine that BREAKS light, and raising
it costs one - the act's own question asked with the player's hand on it. It is the only requirement
marked `m_recover = false`: tear the altar down and the light does not come back.

It doubles as the unlock, which is the neat part. Valheim shows a piece once every material is a
KNOWN one, so the anvil appears in the hammer at the moment the shade hands over the light it kept -
no gate to write. And because whether vanilla even puts the piece in the hammer's table is asset
behaviour this assembly cannot read, the mod adds it when absent and logs that it had to.

**New step `mq-anvil`** on the craft track, between the bow and the shield. `BuildPiece` with the
category `StormAnvil`, which tests for an `Incinerator` component - a compiled class, so it names no
asset. That matters more here than anywhere else in that table, because the anvil's prefab name is
the one fact about it nobody in this project knows.

**And a dev key for the impatient**: `Backspace` plants a Storm-Anvil in front of you and grants the
five things its combine wants, so the next action is pulling the lever. It plants by the prefab found
through the component, so it works without knowing the name. Added to `DevKeyHelp`, which is now
three lines - the banner is generated from that array, so it cannot drift.

### Test it

- [ ] **`Backspace` in dev mode**: an anvil appears and your pack fills. Pull the lever - a Stormward.
- [ ] **The honest route**: after the shade gives you its light, open the hammer near a workbench.
      **Storm-Anvil** should be listed, wanting 20 stone, 10 wood, 10 resin, 1 rescued light.
- [ ] **Raise it and `mq-anvil` completes**, paying 40 stone, 20 coal and a light back.
- [ ] **Tear it down: the light does NOT come back** (stone, wood and resin do).
- [ ] **It is called Storm-Anvil** in the hammer and on hover. The LEVER's own text will still say
      Obliterator - that string is a localisation token and is not ours.
- [ ] **Combine the shield in it**: 20 wood, 20 resin, 10 troll hide, 10 deer hide, 3 rescued lights,
      pull the lever. A Stormward, not coal.
- [ ] **Random junk still becomes coal.** The saga's conversion is priority 100 and must fire only on
      an exact match.
- [ ] Log: `The storm-altar is '<prefab>'`, `Storm-Anvil re-costed: ...`, and possibly `The
      Storm-Anvil was not in the hammer's table; added.` **Report that first line** - it is the asset
      name this whole thread has been working around.

---

## 2026-09-20 - TASK: the Storm-Anvil, and two beats for the shield quest (`...20n`)

Four things, from one conversation about making the shield's Act I quest worth doing.

**The Obliterator already does EverQuest combines.** `Incinerator.m_conversions` is a public
`List<IncineratorConversion>`, each one `{ m_requirements (item + amount), m_result, m_resultAmount,
m_priority }`. Put in exactly these things, take out exactly that thing - shipped in the base game
and used by nothing but coal. So "if the user oblitarates 4 specific things, replace with a new
object" is not a workaround; it is the feature. The saga now appends a conversion: the Stormward's
materials in, the Stormward out, with the lever, the strike and the `m_lightingAOEs` flash for free.
No patch, no new asset.

It is found by its **Incinerator component, never by name** - a prefab name is asset data this
assembly cannot verify - and the log prints what it turned out to be. Both the ZNetScene prefab and
every live instance are taught, because the prefab alone misses one already standing and instances
alone lose it when you build another.

**It is called the Storm-Anvil now.** `Piece.m_name` is ours to set. An anvil is where the blow
lands, which is exactly what this is. The lever's own hover text is a localisation token and will
still say Obliterator - a seam, not a bug.

**Two new craft-track steps**, because the shield's whole quest was a shopping list:

- `mq-shield-answer` - **let the Stormward answer three times.** The discharge is otherwise
  undiscoverable: no animation of its own, no tooltip line, no tutorial. Detected from
  `ItemData.m_lastAttackTime`, which `StartWithoutAnimation` stamps and nothing else on a shield
  touches - exact, where polling `m_blockCharges` would confuse a discharge with a decayed charge.
  The count is persisted, because measures keep their maximum and a counter that reset on resume
  would stall a half-done step.
- `mq-storm-vigil` - **stand out in a thunderstorm holding it.** LAST on the track on purpose: it
  waits on weather, which nothing can hurry, so it sits where nothing is behind it, exactly as the
  losable troll does. Matched on the substring "thunder" rather than a guessed environment name, and
  `Player.InShelter()` means a roof does not count.

**And a reward that lied.** `mq-shield` promised "Mead, and arrows enough for a god" and had no
entry in the reward table at all, so it paid nothing. Fixed.

### Test it

- [ ] **Block twice quickly, three times over, and the step completes.** Watch it count.
- [ ] **Save and resume mid-step**: progress is kept, and you do not have to start the three again.
- [ ] **`mq-shield` actually pays** mead and 60 flint arrows now.
- [ ] **A thunderstorm, no roof, shield equipped** completes the vigil. Under a roof it must NOT.
- [ ] **Find an Obliterator** (Act II, or spawn one) - it should be called **Storm-Anvil** in the
      hammer menu and on hover. Put 20 wood, 20 resin, 10 troll hide, 10 deer hide and 3 rescued
      lights in, pull the lever, and a Stormward should come out instead of coal.
- [ ] **Nothing else obliterates wrong.** Put a few random items in and pull: still coal. The saga's
      conversion has priority 100 and must only fire on an exact match.
- [ ] Log: `The storm-altar is '<prefab>'` - **write that name down**, it is the one asset name this
      whole thread was missing - and `Storm-altar combine registered: 5 things in, Stormward out`.

---

## 2026-09-20 - TASK: the Stormward answers being hit (`...20m`)

"*can we do something crzy with it? lighting and aoe when someone hits it?*", then "*the shield IS
the weapon, we need the model to be the biggest we have*", then - correctly - "*we dont have to let
it attack, if it cant. Just make it big and react to dmg.*"

**The game already had this feature and Valheim reserved a field for it.** `Humanoid.BlockAttack`
counts a successful block into `m_blockCharges` when the blocker's `m_buildBlockCharges` is set,
and on reaching `m_maxBlockCharges` it calls `m_shared.m_attack.StartWithoutAnimation(...)` and
resets the count. So the discharge is the game's OWN attack path, which matters for one reason
above all: it arrives with the player as its attacker. `DoAreaAttack` skips the attacker's own
GameObject twice, `SetAttacker` is the player, and `m_hitFriendly = false` covers tames - so
friendly fire, self-damage and skill factors all follow the normal rules. A hand-rolled `Aoe` would
have had no owner, which is the trap Thor's bow walked around with `Projectile.m_aoe`.

What it now is:

- **The biggest model in the game.** Source prefab chain is tower shields first -
  `ShieldFlametalTower`, `ShieldBlackmetalTower`, `ShieldIronTower`, then Carapace, then the old
  Serpentscale and wooden ones. Names are asset data this assembly cannot verify; the log says
  which one won. `m_timedBlockBonus` is set explicitly now, because a tower shield's own value is
  1 - no parry at all.
- **Two blocks inside five seconds and it discharges**: 5 m radius, 26 lightning (+5/level),
  12 blunt, with heavy force and stagger. `m_blockChargeDecayTime` resets the count to ZERO, not
  down by one, so a single tap from a passing boar never builds toward it. It answers a fight.
- **The damage is the shield's own** `m_damages`, because `DoAreaAttack` reads
  `m_weapon.GetDamage()`. So the item card is not lying about what it does.
- **No charge-up flash.** That was the first instinct and it is a lie: the first block would look
  exactly like the second, so the player could not tell a stored hit from a discharge. The
  discharge's flash is the only lightning and it means one thing.
- **The three layer masks `DoAreaAttack` reads are private statics**, zero until some attack has
  gone through `Attack.Start`. The mod primes them itself if they are still zero, rather than
  trusting that the player has swung something first.

It is **still in Act I** on purpose - play with it there and move it to Act II later if it is too
much for the Meadows. Moving it is a step id and a recipe gate, nothing more.

### Test it

- [ ] **The shield is huge.** Check the log line `Saga item created: Saga_Stormward from X` to see
      which prefab it got - the first few names are guesses at Ashlands/Blackmetal tower shields.
- [ ] **Block twice in quick succession and lightning goes off around you.** Things in a 5 m ring
      take damage and get thrown back hard.
- [ ] **It does not hurt you**, and it does not hurt a tamed boar standing next to you.
- [ ] **One block, then wait six seconds, then block again: nothing.** The charge decayed.
- [ ] **It still parries** - `m_timedBlockBonus` survived the move to a tower shield.
- [ ] Log line `Stormward discharge: 5m, 26 lightning, every 2 blocks, flash '...'` at run start,
      and if you see `Primed Attack.m_attackMask...` that is the mod filling in a mask the game had
      not built yet - fine, and worth knowing it happened.
- [ ] **The first discharge of a fresh session actually hits something.** That is the mask fix; if
      it whiffs, the masks are the suspect.

---

## 2026-09-20 - TASK: the BOOK becomes a book, and the GM tab gets its layout back (`...20l`)

Two things from one report: "*like we're building a story, and that should be apparent to the
user*", and "*the initial popup to start the saga, the GM tab is weird - the text doesnt fit and
there are no buttons*".

**The GM tab was one unbalanced layout group.** `DrawLobbyBody` opened a horizontal group for the
title row, opened and closed a second one for the tabs, then drew the whole GM page and `return`ed
- with the title row still open. So the blurb, the button, the KEYS heading and the scroll view
were laid out SIDE BY SIDE in a 360px row, and the method left a group on IMGUI's stack every
frame. The title row is now opened and closed before any page draws, the GM page has its own width
(470) as well as its own height, and the key rows lost the `FlexibleSpace` that was competing with
their wrapping description for the width.

**The BOOK now reads as a book.** The material was already right - every finished main-quest beat
with the line that was said when it happened - and it still read as a checklist, because a list of
deeds is not a narrative until something frames it. Four changes:

- A **title page**: `THE SAGA OF <NAME>` and the premise in one sentence.
- **Chapters have titles.** It said `ACT II`; the act card had always said `Where the Light Goes`.
- A new **`ActDefinition.Chapter`** - a prose passage, past tense, at the head of each act's
  chapter - and **`ChapterClose`**, one closing line drawn once the act is behind you. Eight of
  each written; each close ends on the question its own act failed to answer, which is the next
  act. A third voice, deliberately: the epigraph is the saga instructing the player, the raven line
  is a character speaking, this is the saga telling itself after the fact.
- The **deed and its line swapped weights**: the line is now the body text in parchment and the
  step label the marginal note. Same two strings; it was the emphasis that made it look like a
  to-do list with annotations.

`ACT II - NOW` is gone, replaced by *"Here the writing stops. The rest is yours to do."*

### Test it

- [ ] **The GM tab lays out vertically.** Blurb, then the button, then `KEYS`, then a scrollable
      table of all 34 bindings with readable descriptions. The window widens when you switch to it.
- [ ] **No IMGUI errors in the log** while the menu is open (`GUI Error`, or anything about pushing
      more GUILayouts than you are popping). That was happening every frame the GM tab was up.
- [ ] **The BOOK opens with your character's name on it** and the premise under it.
- [ ] **Each act's chapter has its title and an opening passage.** Act I's should be there from the
      first main-quest step you finish.
- [ ] **Act I gets a closing passage once Eikthyr is down** and Act II has opened - and Act II does
      NOT have one while you are in it.
- [ ] **The act's title is not printed twice** where the chronicle already opened that chapter.
- [ ] Read it top to bottom. It should read like an account of the run, not a list of ticks.

---

## 2026-09-20 - THE TEST LIST for 1.0.15-run.2026-09-20j

Fourteen builds over two days, rewritten as ONE pass in the order you actually meet things. The
per-build TASK entries below keep the reasoning; this is the list to play with.

**`...20c` is staged but NOT installed** - you were playing when it was built, and replacing a
DLL the game has open fails. When you are done: `.\dist\windows\Install-Mod.ps1 -ModOnly`.

### If you only do seven things

0. **The crafting list is alphabetical.** (The free-cursor change is REVERTED - TAB as before.)
1. **`mod` + `Keypad -` is `+2h` per press again.** It says `DEV: +2h` immediately and then, a
   second and a half later, whether it is night - which is the only part that was ever broken.
2. The **BOOK** tab (was QUESTS) opens on the run's own account of itself, act by act.
3. **Thor's bow forks.** Every arrow strikes 4m around where it lands.
4. The saga menu **opens itself**, **"Not now"** keeps it away, and it has a **`GM` tab**.
5. Centre-screen lines **wrap** instead of running off the side.
6. **The HEARTH track holds the homestead** (roof, fire, pot, bed, chest), not CRAFT.
7. Afterwards, the log greps at the bottom of this page.

### 1. At the main menu

- [ ] Launch and **do not open Credits**. **No popup at all.** Silence is success.
- [ ] Under the game's own version line, one gold line at 70% size, **not overlapping**:
      `SAGA v1.0.15-run.2026-09-20j · GM`. That line is the ONLY proof the mod loaded.
- [ ] Load the character carrying **Thor's bow**. Still there. Save, quit to desktop, come back:
      still there, and no `Failed to find item prefab` in the log.

### 2. The menu is now the general menu

- [ ] Outside a run the menu has **`SAGA` and `GM` tabs** (this is a GM build; a saga-only one has
      neither tab nor GM page).
- [ ] **`GM` opens the old cheat windows** from a button, and the button says whether they are up.
- [ ] The GM page lists **every GM key and what it does** - generated from the same registry the
      input manager was built from, so it cannot drift. First time those keys have been written
      down inside the game at all.
- [ ] Start a run: **the GM tab is gone** for the duration, and GM keys stay dead.

### 3. The menu comes to you

- [ ] **The saga menu opens by itself** on loading a character. You should not have to press `End`.
- [ ] **"Not now" closes it and it stays closed for that world.** Die, respawn, walk about: still no
      menu. It re-offers only when you load a world again.
- [ ] `End` still brings it back by hand.
- [ ] Start a run, then **abandon** it: the menu does not immediately reappear.

### 4. The run window, which is now two pages - RUN and BOOK

- [ ] Tabs read **`RUN`** and **`QUESTS`**. The selected one is gold.
- [ ] **RUN** keeps the numbers, the step in play with its count, bar and clause list, TASKS and
      BOONS. SPLITS and HOMESTEAD are gone from it.
- [ ] **BOOK** (renamed from QUESTS) opens on **THE CHRONICLE**: every main-quest step you have
      finished, in order, grouped under `ACT I`, `ACT II` and so on, each with the line that was
      said when it opened. Read it top to bottom - it should read as the run's own account of itself.
- [ ] **The chronicle survives a reload.** Save, quit to the menu, resume: the book is still full.
      It is persisted precisely because a record that forgets itself is not one.
- [ ] Below the chronicle, `ACT <n> - NOW` holds the live tracks: the step in play with its **hint**
      and what it **pays**, then `N more on this track`. Future steps stay a count, never a list.
- [ ] **Centre-screen lines wrap** now instead of running off the side. If 44 characters is the
      wrong width for your screen, `runMessageWrapChars` in the config is the dial.
- [ ] The tab row **does not move** when you switch pages, and the log has no
      `Mismatched LayoutGroup` after clicking about.
- [ ] **Sub-objective counts read down a column**: `3/4  Hunt 4 Boar`, bright gold, left-aligned.
      Two clauses of different name lengths should line up with each other.

### 5. The keys

- [ ] `Keypad +` = **Shaman's Mercy** (burst heal). `Keypad -` = **Unseen** (20s, nothing sees you).
- [ ] **`mod` + `Keypad -` advances 2 game hours per press**, as it used to. It now says `DEV: +2h`
      at once and reports `it is night` / `still light` a moment afterwards, rather than reporting the
      state it was in before the jump. Press until it says night.
- [ ] **Dev keys are bare again** except two: `Keypad *`, `/`, `.`, `Enter`, `Delete`, `Home` and
      `PageUp` need NO modifier. Only the step-skip and the clock do, because those share the
      player's keys - **`Shift`/`Ctrl`/`Alt` + `Keypad +`** and **+ `Keypad -`**.
- [ ] The **DEV MODE banner is two lines and names the modifier.** It was the banner that was wrong
      last time, not the keys.
- [ ] A boon offer card shows its key beside "active", e.g. `active  [+]`.
- [ ] **Die on purpose.** A line says your things are where you fell, the HUD shows
      `Where you fell  [PgDn]`, and `PageDown` gates you back. Four-minute cooldown, and the offer
      DISAPPEARS once you have walked within 12m of the spot.
- [ ] **`PageUp` probes the item in your hands** when you are not looking at a creature. Hold Thor's
      bow, press it, and check `ICSYTW_probe.txt` lists its renderers and shader properties. That
      report is what the item glow gets written from next.

### 6. Act I, in chain order

- [ ] **The HEARTH track holds the homestead.** Read the three tracks: HEARTH must run *forage,
      roof, fire, cooking station, meal, bed, settle in, sleep, comfort, chest*, then the fishing and
      the pen. CRAFT must be tools and gear only: *axe, hammer, workbench, upgrade, the shade, Thor's
      bow, the Stormward*. The roof and fire had drifted onto CRAFT, which also put "Settle in" on a
      different track from the fire it silently needs.
- [ ] **Every step SPEAKS what it needs** as it opens - the cooking station going ON the fire, the
      bed under a roof, settle-in wanting all three at once. A step with both a story line and a hint
      says the story line first and the hint about six seconds later; **watch that the second does
      not wipe the first.**
- [ ] **Two lines never land in the same frame.** Finish something that advances two tracks at once
      (the cull, usually): you should get both lines in turn rather than a flicker.
- [ ] **Hear the raven out** - Hugin lands and states the errand.
- [ ] **Hunt a deer by daylight** - nothing rises, and the line says why.
- [ ] **Keep a watch after dark** - three whispers. The strip tells you to wait for dark, and **no
      pack and no starred deer** should appear during it. The vigil is meant to be empty.
- [ ] **Follow the pale light**, then **the race**. Every light taken drops a **Rescued light** into
      your pack; check the stack survives a portal.
- [ ] **The Breaker** - announced before nightfall, arrives at night, and **comes out by the house**
      when you are within 140m of your claimed bed. The line says so only when it actually did.
      Expect it to walk through what you built.
- [ ] **The greydwarves attack it, and they still attack you.** That three-way fight is the whole
      experiment.
- [ ] **The Breaker, failed on purpose**: let the fifteen minutes run out once and confirm it walks
      away with a line rather than standing in your meadow.
- [ ] **Hugin announces the recipes.** When the shade is paid, a raven says the bench knows the shape
      and that it stays empty until you are CARRYING the lights. When the Breaker falls, a raven says
      to improve the bench. This unlock used to be completely silent.
- [ ] **And announces them ONCE.** Save, quit to the menu, load back in: no raven repeating a recipe
      you already have. Same after a world reload, which rebuilds ObjectDB and re-registers
      everything - that is the case the guard exists for.
- [ ] **Thor's bow** - the bench will not list it until you are HOLDING a light; needs 3. It is now
      **44 pierce + 22 lightning** and wears the **Huntsman's model**. Lightning flash on impact -
      and the bolt **forks 3m around the arrow**, so a shot into a knot of greylings takes more than
      the one it hit. It must NEVER hurt you or a tamed animal.
- [ ] **The shade hands you one Rescued light** with the recipe, so the bench shows Thor's bow at
      `0/3` the moment it is taught rather than showing nothing at all.
- [ ] **The shade comes back twice.** After the Breaker falls, and again after Eikthyr, a line says
      "The shade is standing by your bed again. It waits for dark." Go home after dark, and it is
      there with a bubble and an `[E] Speak` - a rune panel, no price, nothing asked.
- [ ] **Each visit is heard once.** Speak to it, then save, quit to the menu and resume: it should not
      be standing there again with the same thing to say.
- [ ] **The second visit happens in ACT II.** Eikthyr's death flips the act, so the shade has to stand
      after the Meadows are behind you - it is attached to your bed, not to the biome.
- [ ] **The Stormward** - an **improved** workbench and 10 troll hide, and it wears the
      **serpentscale shield's** model.
- [ ] **The Gatherer is 35% bigger than its children** and arrives about **45 seconds AFTER** its
      raid, not with it. You should be in the fight before it walks in.
- [ ] **Starred greydwarves are their own colour again.** After the Gatherer fight, look at ordinary
      starred Greydwarf_Elites: none of them should be wearing its gold.
- [ ] **The claim that matters most:** miss the troll, and confirm the act still finishes with only
      the shield lost. Nothing else on any track may stall.
- [ ] Herald, then the Gatherer, then the altar, then **Eikthyr** - and the shield should visibly
      blunt his lightning.

### 7. Act II and beyond - log checks, since you will not reach them tonight

- [ ] At run start the log should carry **seven** `Saga item created: ... from X` lines: the bow, the
      Stormward, the rescued light, and the four **Stormsworn** pieces. The `from X` names the mesh
      each one resolved to, so a missing source prefab shows up there as a fallback.
- [ ] In Act II: `Saga recipe registered: Saga_storm-helm -> ...`. A
      `station prefab 'forge' has no CraftingStation` error means I guessed that name wrong - it is
      the one name in this batch I could not verify from the codebase.
- [ ] `bf-arrive` ("reach the Black Forest") is the FIRST step of Act II. You asked whether it
      existed; it does, and it only appears once Eikthyr is down.

### 8. Systems you brush against throughout

- [ ] **Shepherd says nothing** when you pick it with no animals - no "No baseline, nothing buffed",
      no "Buffed 0 pets". The GM readouts are gone from the saga's path.
- [ ] **No fishing bounty is ever dealt before you own a rod.**
- [ ] **Windfall has 3 charges** and counts them down as you spend them.
- [ ] **The stash is alphabetical**, and "Take" empties the row you clicked.
- [ ] **Quick Study** (skills x9 while held) and **Bountiful** (drops x6 while held) appear in offers
      and visibly work.
- [ ] Still unverified from 12-13 September: the **shade's greeting** and its `[E] Speak` prompt, the
      **saga dreams**, and **raids arriving as beats**.

### 9. Afterwards, in PowerShell

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" `
    -Pattern 'ICanShowYouTheWorld\] Unknown|Failed to find item prefab|has no CraftingStation|Saga item created|Saga recipe|DEV:|Mismatched'
```

`] Unknown` and `Mismatched` must both be **empty**. The rest is the new content reporting itself -
and `DEV:` answers "did that key actually fire", which is the question the last report could not.

### One caveat while judging the feel

Your config file is from **25 August** and pins `runStaminaRegenRate` to **1.5**, where the current
default is **2.5** - the file wins over the code, so you have been playing a month-old stamina
regen. Thirty-five newer settings are absent from it entirely and running on code defaults, which is
correct behaviour but means they cannot be TUNED without adding the lines by hand. Ask and I will
either add the keys or make `Load()` re-save so no future setting is invisible.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20j - the shade comes back

It had two lines and was then finished, which is thin for the only speaking part in Act I. It now
returns after the **Breaker** and after **Eikthyr**, and costs almost nothing: it already stands by the
bed at night, already greets with a bubble, already opens a rune panel. So the feature is a phase, two
strings, and a memory of what has been heard.

Three things were worth getting right.

**It is announced.** "The shade is standing by your bed again. It waits for dark." Without that line
the beat would be unfindable: the shade stands by the BED, at night, and a player who has just killed
Eikthyr two valleys away has no reason to go home and no way to know anything is waiting. A beat nobody
can find is a beat that was not built, and this mode has paid for that lesson more than once today.

**It is no longer act-limited.** `PollShade` used to be called from inside `PollDeerHerd`, which is Act
I only - and Eikthyr's death FLIPS the act, so the second visit could never have happened. It has its
own call site now. Its quest phases are still Act I's, because the tracks are; the shade is attached to
the bed rather than the biome.

**It is heard once, and remembers across a resume.** Ids in the run state, not text, so the lines can
be rewritten later without a save forgetting that the visit happened.

The remarks are a table keyed by the step they wait on, so the next one is a row. Both lines are in the
register the others set - terse, second person, nothing the world does not back - and both are about
someone else having done the thing it never could, because it is a hunter who never loosed.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20i - the invisible recipe, the bow tuned, the cursor reverted

### Why the bench had no bow

Your log had the answer: the recipe registered perfectly (`Saga recipe registered: Saga_hunters-bow ->
... (Wood 10, Resin 10, DeerHide 6, Saga_RescuedLight 3)`), so the shade and the gate both worked. The
bench hid it for a reason in Valheim's own code.

`Player.GetAvailableRecipes` lists a recipe only when its item name is in `m_knownRecipes`, and
`UpdateKnownRecipesList` adds it when `HaveRequirements(recipe, discover: true, ...)` passes - which
tests `IsKnownMaterial` for every INGREDIENT rather than the amounts. Until a Rescued light has been in
the pack once, Thor's bow is not uncraftable, it is ABSENT, with nothing greyed out to explain itself.

The root cause is structural and will bite again: lights come off the HUNT track while the bow sits on
CRAFT, and the two can be reached in either order. So **the shade now hands over one Rescued light**
with the recipe. An invisible requirement becomes a visible `0/3`, and it is the better story - the one
light the shade had left of its own hunt. Anything that gates a craft on another track's resource needs
its first unit given, or the bench lies by omission.

### The bow, down

It forked, and then it was 90 damage to everything within four metres. Now **44 pierce + 22 lightning**
(66 direct, against the Huntsman's 52) and **3m**. Both dials moved, not just the damage, because the
radius is what decides how many things a shot kills - `Projectile.m_aoe` applies the full damage to
everything inside it with no distance falloff. If it is still too strong, cut the pierce: the lightning
is the part that makes it Thor's.

### The cursor change is reverted

A clean failure and a fair one: with the pointer free, drawing the bow **drags the windows around**. Of
course it does - the mouse buttons are how you shoot AND how you move an IMGUI window, and freeing the
cursor put both on the same click. TAB works because Valheim also stops taking player input while the
inventory is up; the mod can claim the cursor half but not that half, and the cursor half alone turns
out to be the worse one.

`ModCursor` is deleted rather than left behind a flag, and the reason is in RESUME.md so nobody
rebuilds it: the missing piece is suppressing player input, not showing a pointer.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20g - the clock key goes back to +2h

Owner: "Bring back the +2 hr thing. The new jump to night doesnt work and the previous thing was
fine." Right on both counts, and the second one is mine to explain.

The step was never the problem. The problem was the MESSAGE - it read `IsNight` in the same frame as
the `SetNetTime` write, so it always reported the state before the jump. I replaced the whole key when
only its readout was broken, and the replacement failed for the mirror image of the same reason:
winding 60 net-seconds a frame is about an hour of game time per frame, so the loop blew through its
own three-day cap inside two seconds of real time while EnvMan's SMOOTHED day fraction - lerped at
0.01 a step - was still catching up with the first step. It then reported giving up, having moved the
clock three days.

So: `+2h` per press, and the readout split in two. The press says `DEV: +2h` immediately, because that
is what it knows. What the sky is doing follows a second and a half later, once EnvMan has recomputed.

Worth keeping as the lesson, since this is the third time in two days the same shape has appeared:
**do not read state in the frame you wrote it** - and when a readout is wrong, fix the readout rather
than the feature.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20f - both answers were already in the game

Two asks, and reading the IL first answered both without writing the thing I had planned.

### The crafting list: Valheim already sorts it

I was going to do surgery on `InventoryGui` - hide the recipe elements that do not match, re-stack the
survivors by index on every rebuild, put an IMGUI text field over a panel you are using. Then I read
`UpdateRecipeList`:

```
Player.m_localPlayer.TryGetUniqueKeyValue("sortcraft", out string v)
  -> Enum.TryParse<InventoryGui.SortMethod>(v, true, out method)
    -> switch (method) { Original | Name | Type | Weight | Count }
```

With no key set the method is `Original` - which IS the unpredictable order. Writing `"Name"` into
that key makes the GAME sort its own list with its own comparator. Nothing reflected into, nothing
re-laid-out, no code of ours between you and the panel. The only other writer of that key in the whole
assembly is `Terminal.InitTerminal`, so it is a console setting and the feature was simply
undiscoverable; `sortcraft Name` in the console does it by hand.

`CraftingSort` writes it once per character, and ONLY when the key is absent or explicitly `Original` -
somebody who chose `Type` on purpose has said what they want. `runSortCraftingByName` turns it off.

With the list alphabetical, a search box is worth far less. Say if you still want one.

### The cursor: `m_mouseCapture` is what vanilla F1 toggles

Valheim keeps the cursor locked while you play and frees it in exactly one place,
`GameCamera.UpdateMouseCapture`, which unlocks and shows it when `m_mouseCapture` is false OR one of
the game's own panels is up. TAB works because it satisfies the second clause. `ModCursor` satisfies
the first, for as long as one of the mod's windows is open.

The reason that is safe rather than clever: `m_mouseCapture` is the field **vanilla F1 toggles** -
`ZInput.GetKeyDown((KeyCode)282)` in that same method, and 282 is F1. So this is not a new state
invented by the mod, it is the state the game itself enters on a documented key. It is also only
reachable through one private field, so the alternative - writing `ZCursor` directly every frame and
fighting `UpdateMouseCapture` for it - would have been OUR state rather than the game's.

Written every frame while a window is up, because `UpdateMouseCapture` also runs every frame. Given
back exactly once when the last window closes, and only if this class was what took it - press F1
yourself and the cursor you asked for stays.

Counted as "a window": F1's cheat UI and End's saga menu. NOT the tracker panel, which is on screen
for a whole act while Hunter's Eye is held and has nothing to click on it.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20d - the book, the fork, the wrap, and a lying dev key

Four of your observations, plus one your LOG found that you had not mentioned.

### The dev clock key was reporting the wrong answer every single time

Twenty-six consecutive `DEV: +2h - still light` lines in your log, across three sittings. The clock
was moving the whole time. The message was not.

`EnvMan` recomputes `s_isNight` in its `FixedUpdate`, from a day fraction it lerps toward with
`Mathf.LerpAngle` at 0.01 per step - so reading `IsNight` in the same frame as the `SetNetTime` write
always returned the state BEFORE the jump. The key worked; the only thing it ever told you was stale.
A key that plainly does nothing is better than that, because at least it does not make you press it
eleven times.

It is now a skip that spans frames and asks the GAME whether it has arrived, rather than doing
arithmetic on a day length and a fraction whose rescaling is EnvMan's business. One press, and it says
`DEV: it is night.` when it is there. Capped at three in-game days so a world that never darkens stops
rather than winding into next week. And it TOGGLES - press it at night and it winds back to daylight,
which "Hunt a deer by daylight" needs and which the old key had no way to give you.

This is the second time in two days that the fix was "stop reading state in the frame you wrote it",
after the creature dressing. Worth remembering as a shape.

### Also in your log, and all good

All seven saga items resolved to their FIRST-choice mesh - `BowHuntsman`, `ShieldSerpentscale`,
`HelmetBronze`, `ArmorIronChest`, `ArmorWolfLegs`, `CapeLox`, `Wisp` - so no fallback was needed
anywhere. No `] Unknown`, no `Failed to find item prefab`, and **no `Mismatched`**, which is the one
that mattered: clicking between the tabs leaves IMGUI's layout stacks intact. Both recipes were taught
exactly once.

Still unverified: `forge`. You did not reach Act II, so the one prefab name I could not check from the
codebase is still unchecked.

### The BOOK

Renamed from QUESTS, and it is the shorter word and the truer one: a quest log lists what is
outstanding, and this page's centre of gravity is now what the run has already been through.

`RunService` keeps a **chronicle** - one entry per main-quest step finished, carrying the act numeral,
the step, and the line that was spoken when it opened. The book opens on that, grouped into acts, and
the live tracks come after it under `ACT <n> - NOW`. A book whose first page is a to-do list is a
to-do list.

It is kept by the service rather than read off the tracks because the tracks cannot answer it: they
are re-seated when an act flips, so by Act III nothing in memory remembers Act I. And it is persisted,
because a record that forgets itself on resume is not a record. Encoded as `numeral|display|opening`
in one list, since that DTO goes through Unity's `JsonUtility` and cannot nest.

### Thor's bow forks

Right, and for the right reason: a bolt that stops at one deer is a nail, not lightning.
`Projectile.m_aoe` is the game's OWN area path, which is why it is used instead of spawning an `Aoe`
component of our own - the projectile already knows its owner, so `m_hitOwner` and
`m_noDamageFriendly` mean what they say and you cannot be caught in your own storm. A bare `Aoe`
spawned through `m_spawnOnHit` is never given an owner at all and would have had nothing to exempt.

Four metres: about two deer or a knot of greylings. Eikthyr's own stomp is wider; this is a bow.

### The big yellow text wraps

Valheim's centre message is one long line at a large size and does not wrap. Wrapped on OUR side
rather than by reaching into `MessageHud`'s text component, because these are our strings and that
component is shared with every message the game itself raises. Breaks on spaces only, so a long word
overhangs rather than being cut in half, and an existing newline resets the count.
`runMessageWrapChars` (default 44) is the dial, config rather than a constant for the same reason
`RunHudMenuOffset` is: the right number depends on the screen.

### Not built: search at the workbench

The one I did not do, and I want to be straight about why rather than quietly dropping it.

It is real UI surgery on Valheim's own crafting panel, and I cannot play-test. The research is done
and the members are all there - `InventoryGui.m_availableRecipes`, `m_recipeListRoot`,
`m_recipeListSpace`, `m_recipeListBaseSize`, `m_recipeElementPrefab` - so filtering means hiding the
elements that do not match and RE-STACKING the rest by index, every time the game rebuilds the list.
The part I will not guess at is the text field: an IMGUI input competing with Valheim's own keyboard
handling, over a panel the player is using, where being wrong breaks crafting itself. Breaking the
crafting window would be far worse than not having search in it.

So it wants its own build and its own play-test, not a corner of a batch of five. Say the word and it
is the next thing.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20c - the general menu, and a flavour baked into the DLL

Both halves of what we agreed last night, built while you tested `...20b`. **Staged, not installed** -
the game was open.

### The flavour is a build-time constant, not a config flag

You wanted GM absent rather than unreachable, and a config bool cannot give that: the recipient owns
the config file. So it is baked in, by the machinery `Version.cs` already uses -
`VersionTemplate.cs` gains `__FLAVOUR__` beside `__VERSION__`, and `Scripts/setversion.sh` fills it:

```bash
Scripts/build_windows.sh --release               # "gm"   - the saga plus the old cheat mod
Scripts/build_windows.sh --release --saga-only   # "saga" - the saga alone
```

Not `#if` and two project configurations. Two reasons, the first decisive: the saga-only binary is
the one you never play, so it is the one that would break silently. And the legacy `CheatCommands`
pipeline must survive in BOTH flavours because Run Mode's boons ride it, so the conditional surface
would have scattered across three files. One binary, one code path, one constant.

**GM is the default.** You play every build and hand one out rarely, so the accident to avoid is
silently crippling your own build.

### Three guards, because the remaining accident is shipping the wrong DLL

- The **badge** reads `SAGA v<build> - GM` in a GM build and says nothing extra in a saga one.
- **`make_release.sh` reads the flavour back out of the DLL** and refuses to package a GM build
  unless passed `--gm`. The zip is named for the flavour too, so one sitting in a downloads folder
  still answers the question.
- The value is read from the ARTEFACT, never taken on trust from whoever ran the build.

That last one needed a fix worth recording. `ModVersion.FlavourMarker` exists solely to be
greppable: the scripts decode the DLL as UTF-16 because .NET user strings live in the #US heap that
way, but FIELD names are UTF-8 in #Strings - so there is nothing to anchor a search for "FLAVOUR" to,
and "gm" and "saga" are far too common to match alone. My first attempt found nothing at all, silently.
Round-tripped both ways before shipping: `--saga-only` then read back `saga`, restore then read back `gm`.

### What saga-only actually switches off

`Cheat.cs` does not **register** the GM bindings - `F1` and `End` survive, and neither is a cheat -
so `CommandRegistry.All` is empty and there is nothing to press or to list. `UIManager` does not draw
the GM windows. What it does NOT touch is `CheatCommands` itself, which both flavours need because
the saga's boons ride that pipeline. Doors, not floor.

### The general menu

Outside a run the menu has `SAGA` and `GM` tabs, in a GM build only. The GM page is a door onto the
windows that already exist plus - for the first time anywhere in the game - the cheat mod's key
table, generated from `CommandRegistry.All`, the same list the input manager was registered from. A
binding that exists is listed; one that was skipped is not. Third application of that principle after
`BoonKeys` and `DevKeyHelp`, and it is here because this codebase has twice shipped a command nobody
could discover.

Narrower than your sentence in two places, both deliberate. The GM page keys on the BUILD FLAVOUR,
not dev mode - those turned out to be different switches, and `runDevMode` keeps its own meaning as
the tester's step-skips. And there is no GM tab during a run: the input gate exists because heat and
score assume GM is dead, and a menu does not get to negotiate that.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20b - two pages, and a menu that opens itself

### The split, and the rule behind it

> The RUN page holds what you act on. The QUESTS page holds what you have done and the detail
> behind it.

That rule is broader than "move the quests out", and it is the part worth arguing with. By it:

- **The step in play STAYS on the RUN page**, with its count, its bar, its clause list and its
  bearing. There is a decision recorded in `RunWindow` that the questline is pinned above the scroll
  because it is the one thing saying where the run is GOING - learned in play, and not undone by a
  tidy-up. The clause counts stay for the same reason they were just made legible: they are read
  mid-fight.
- **SPLITS and HOMESTEAD move.** Records, not decisions. This also retires a workaround: the
  homestead panel was switched OFF by default because it "was competing for room with the three quest
  tracks" - a room problem, answered better by a page than by hiding the records. It now shows on the
  QUESTS page unconditionally, and `RunShowHomestead` decides only whether it ALSO appears on RUN.
- **The hint moves.** Three reasons in order of weight: it is spoken aloud when the step opens now,
  so the panel copy was a second telling; it was the largest variable-height thing on a row that has
  to stay compact; and it had been narrowed to "only before you make progress", which meant the one
  place to re-read it vanished the moment you started.
- **TASKS and BOONS stay.** Live choices.

### The log

No new state at all. `QuestTrack` already carries the whole `Chain` and the `Index` into it, so the
page is a different reading of what the HUD already had: everything before the index is done, the
index is in play, the rest is to come.

Finished steps are why the page is worth having. A completed step used to simply vanish, so a run had
no memory the player could read - which for a mode calling itself a saga is the page it was missing.
Each one shows its `Opening` line where it has one, because that line WAS the beat.

Steps still to come are a COUNT and never a list. Naming them would spoil the act, and the same
objection already took reward text off the step rows. A number answers "how much of this act is left"
without answering "what happens next".

### The menu opens itself

Three things had to be right, and two of them were traps.

**It keys on entering a WORLD, not on a fresh Player.** The player reference is the signal already
sitting in `DetectRespawnAndReapplyPassives`, and using it would have re-opened the lobby every time
the player died outside a run. Worse, the obvious guard is wrong the same way: a death leaves
`Player.m_localPlayer` null for about ten seconds while `Game.RequestRespawn` works, so resetting the
"already offered" flag on a null player would re-offer on every respawn. Only a world actually going
away re-arms it.

**It belongs in `TickInner`'s INACTIVE branch.** My first version sat further down, where everything
returns early when no run is running - which is precisely the state the offer exists for. It could
never have fired.

**And `UIManager.OnGUI` had to let the pass through.** It returns early when nothing is visible, which
is exactly the state the auto-open starts from, so the door could never have been opened from inside
`RunWindow.Draw`.

Both visibility flips are deferred to a **Layout** event, the one pass where the set of live windows
may change - the rule `ApplyPendingActions` already obeyed. And the lobby has a **"Not now"** button,
because a window that appears unbidden and can only be dismissed by a key nobody told you about is
worse than the key press it replaced.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-20 - TASK: 1.0.15-run.2026-09-20 - the Stormsworn

Built on "use your best judgement", so here is the judgement, which is the part worth arguing with.

### What the pieces are FOR

Five pieces of +armour would have been a grind with a story bolted on, and a one-per-act limit would
only have paced the grind. So each piece answers the thing its OWN act kills people with:

| Act | Piece | Answers | Cloned from | Gated on |
|---|---|---|---|---|
| I | Stormward (shield, already shipped) | Lightning, Blunt | ShieldSerpentscale | `mq-troll` |
| II Black Forest | Stormsworn helm | Blunt | HelmetBronze | `bf-bronze` |
| III Swamp | Stormsworn cuirass | Poison | ArmorIronChest | `sw-ironbar` |
| IV Mountain | Stormsworn greaves | Frost | ArmorWolfLegs | `mt-silver` |
| V Plains | Stormsworn mantle | Fire | CapeLox | `pl-berserker` |

The set is then not a stat total but a record of what the run survived, and each piece is worth
wearing into a later act precisely because of where it was made. The Stormward becomes the first
piece of it retroactively, which is why the Act II step opens with "the storm gave you a shield.
That was the first piece."

Valheim adds resistances across equipped items by itself, so the full kit IS the set bonus. No
`m_setStatusEffect`: that wants a StatusEffect asset this build cannot verify, and an invisible set
bonus is worse than an honest one made of parts. All four are `Resistant`, never `VeryResistant` -
four pieces already add up to something the player will feel, and the saga does not hand out a fight
that cannot hurt you.

### Two decisions with a failure mode behind them

**No rescued lights in any of these recipes**, though the symmetry begs for it. Lights come from the
deer hunt and the couriers, which `PollLights` restricts to Acts I and II - so a light cost in Act IV
would be a `CollectItem` step nobody could finish, and an unfinishable step is a stalled act. The
same reasoning that made Act I's bow safe makes this unsafe.

**And no Guck in the Act V cape**, which the first draft had. Guck is a Swamp item and the cape is an
Act V craft: asking for it there is asking the player to have hoarded two acts back. Needles are what
the Plains drops.

Each gate is a step that PROVES the materials and the station are in hand - bronze forged, iron
carried, silver carried, a berserker down - so the bench learns the shape at the moment the shape is
makeable. Hugin announces each one, through the `TaughtLine` machinery from `...19n`.

### The probe now reads items

`PageUp` falls back to the prefab of whatever is in your hands when you are looking at no creature.
The PREFAB, not VisEquipment's instantiated copy, and that is the useful choice: the prefab is the
object `SagaItems` clones, so its renderers and materials are the ones a tint would write to.

That is the groundwork for the glow, which is the real answer to "the models are a bit simple" - a
mesh swap is a different plain mesh, while a tint and a self-lit emission is the trick
`CreatureDressing` already plays on creatures. It is not written yet because a shader property name
that does not exist fails SILENTLY, which is the one class of bug this mode keeps paying play
sessions to find. One probe run and it can be written from measurement.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19o - a play report, worked through

Seven notes from the run. One of them turned out to be two bugs wearing each other's clothes.

### The bright greydwarves and the invisible Gatherer are the SAME bug

"some greydwarfs had the very bright model when it wasn't even courier time yet" and "I almost didnt
see the gatherer in the huge battle" have one cause, and it is in `CreatureDressing`. The class was
already careful about the obvious hazard - it writes `renderer.materials`, never `sharedMaterials`,
with a comment saying why. What it did not account for is WHEN it runs. Every named creature is
dressed immediately after `Instantiate`, which is before Unity calls `LevelEffects.Start`, and that
method (read out of this build's IL) does this:

```
transform.localScale = (setup.m_scale, ...)                       // our ScaleMultiplier, gone
key = Utils.GetPrefabName(character) + level
if (m_materials.TryGetValue(key, out cached))
    mainRender.sharedMaterials[0] = cached                        // our colour, gone
else
    mainRender.sharedMaterials[0] = new Material(sharedMaterials[0])   // a copy of OUR gold
    ... hue/saturation/value/emissive ...
    m_materials[key] = that                                      // cached, FOR THE PREFAB
```

`m_materials` is `static`. The Gatherer is `SetLevel(2..3)`, so if it was the first starred
`Greydwarf_Elite` of the session, Valheim copied our gold into its cache and handed it to every
starred elite that spawned afterwards - for the rest of the process. The Gatherer stopped being the
only gold thing in the forest, and any scale we gave it had been discarded a frame earlier.

`CreatureDressing.ApplyWhenSettled` is the fix: the look lands two frames after the spawn, once
LevelEffects has taken its scale and seeded its cache from the vanilla material. Nothing re-runs the
setup afterwards (`OnLevelSet` fires on a level CHANGE, and the level is set before `Start`
subscribes), so what we write last stays. All five dressing sites use it, and `RunService.Tick` drives
the two-frame queue.

With the multiplier now actually working, the Gatherer is `ScaleMultiplier = 1.35f`.

### The Gatherer also arrived at the wrong moment

The raid and the arrival were on the same tick, so the oldest splinter walked in alongside thirty of
its children. The forest now moves first and the Gatherer comes through 45 seconds later, into a
fight the player is already committed to. Session state, so a reload between the two costs only a
repeated announcement.

### The Breaker comes out by the house

"would it be sweet if we spawned it near our house so that it risks ruining everything?" - yes, and a
troll is the only thing in Act I that takes buildings down. Within 140m of the claimed bed it arrives
34m from the HOUSE instead of 34m from the player. Bounded on purpose: spawned at a house 900m away it
would walk for the full fifteen minutes and never be seen. `CameOutAtHome` exists so the line about
the house is said only on the nights it is true.

### Thor's bow was worse than a quest drop

"Finebow does 50 dmg, but thor a lot less pierce?" Correct, and worse than it looks: the bow INHERITED
its pierce from whatever prefab the fallback chain resolved, so the saga's signature weapon had no
number of its own. Cut from the 32-pierce Finewood bow, it was then outclassed by the 52-pierce
Huntsman that `mq-herald` hands over four steps later.

Every channel is now SET, not inherited: **58 pierce (+6/level), 32 lightning (+6/level)**, and poison,
fire, frost and spirit explicitly zeroed so a fallback prefab cannot leak its own damage into a weapon
named for the storm.

### The models

"The storm shield/thor bow models are a bit simple" - they were the plainest mesh in each class. Now
that no stat is inherited, the source prefab is purely which model the item wears, so: the bow wears
`BowHuntsman` and the Stormward wears `ShieldSerpentscale`, each with a fallback chain. Watch the
`Saga item created: ... from X` log line to see which resolved.

This is a mesh swap, not a bespoke model. A real look of their own needs an AssetBundle built in Unity
6000.0.x - a decision, not a detail. The cheaper middle step is a tint and a glow, the same trick
`CreatureDressing` plays on creatures; it needs one dev-probe run on the two items to learn their
shader property names, since a wrong name there fails SILENTLY.

### The x/y counts

`  . Hunt 4 Boar   3/4` put the only figure that moves at a ragged right edge, muted, at the size of
everything else. It is now a fixed-width left column in bright gold - `3/4  Hunt 4 Boar` - so two
clauses of different name lengths line up and the column can be read down. Same in the HUD strip,
which is the copy read mid-fight.

### Port to corpse

`PageDown`, four-minute cooldown, no charges - a death is not an achievement. It refuses when there is
nothing out there, and it FORGETS the corpse once the player has been within 12m of it, so it cannot
decay into a permanent teleport to a place they once died. Deliberately not
`PlayerProfile.m_deathPoint`, which has no clear and would answer "yes, there is a corpse" for the
rest of the run.

### Answered, not built

**"Im not sure there was a 'locate dark forest' step?"** There is: `bf-arrive`,
`ChallengeKind.ReachBiome` on `BlackForest`, and it is the FIRST step of Act II - so it only appears
once Eikthyr is down. You had not got there.

**Storm armour, one piece per act.** Not built; see the reply. It is a good idea with one real
question in it, and that question is what the answer should be decided on.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19n - the quest hints learn to speak

Your note: "we should consider if we need to add more ravens/stones or other types of quest hints
where it makes sense". Two places where it clearly made sense, both of them holes rather than
additions.

### Hints were written for the panel and the panel is not open

Every `Hint` on a step exists because of a failure already seen in play - "settle in" needing a fire
as well as a roof, a cooking station going ON the fire and not beside it. They were only ever drawn
in the Run window, and a player halfway through building a house is not looking at the Run window.
A hint nobody reads is a failure already paid for and not yet fixed.

So a step now says its hint as it opens. `StepOpenings` treats a hint as something to say, and
`StepOpenings.LineFor` picks which line leads: the opening if there is one, because an opening is a
statement about the world and a hint is an instruction, and a step that opens by telling you what to
do has no moment left to be about anything.

A step with BOTH says both, six seconds apart, through a new paced queue in `RunService`. That queue
also fixes something older and quieter: Valheim's centre message replaces itself, so two lines in one
frame were one line plus a flicker - which is what happened every time a single kill advanced two
tracks. Every step line goes through the queue now and they are spoken one at a time.

### The recipe unlock was silent

The saga's one event that changes what the WORLD can do, rather than what you are being asked to do,
and it announced itself to `Player.log`. The bow's step opens when the shade is found - minutes
before the lights are in hand - so a player who walked to the bench at the wrong moment found
nothing there and had every reason to think the step was broken.

Hugin says it now, at the moment it becomes true: `SagaRecipeDefinition.TaughtLine`, on the
definition beside the ingredients it describes, delivered by the existing `TrySpawnRaven` and falling
back to a plain message if the bird cannot be had. The lines are deliberately not shopping lists -
the step's hint recites the amounts and is now spoken too. What they carry is the event, plus the one
thing the bench will not tell you: it stays empty until the lights are in your PACK.

Two traps, both of which have bitten this codebase before and both handled the same way as
`StepOpenings`. The unlock is tracked separately from the registration, because a world load rebuilds
`ObjectDB` and re-registers everything - which would re-teach a recipe you had been crafting with for
an hour. And the first `Ensure` of a run is a silent baseline, so a RESUMED run treats what it finds
already unlocked as history.

### What I did not build

Stones. `Raven.AddTempText` takes a label, and a non-empty one opens Valheim's own parchment reader -
a page you read instead of a toast you miss, with no new prefab. That is the cheap version of the
"stone" and it is one argument away. A genuinely placeable saga runestone would want a Piece prefab
and ZDO persistence, which is a bigger thing than a line of text. Worth deciding after you see how a
labelled raven reads.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19m - three corrections from the first minutes

All three were yours to find and mine to have caused.

### The dev keys were never broken. The help text was.

"Oh we changed it to shift + ? I didnt know... Its just that the help text didnt reflect that."
That is the whole fault: the Run window's DEV MODE banner still read `+complete -time *items ...`
after every one of those keys had moved behind Shift. The tester trusted the line, pressed the bare
keys, got nothing, and correctly concluded the layer was broken.

Two fixes, and the second is the one that matters.

1. **The modifier is narrower.** A modifier only earns its place where there is a second layer to
   separate, and nothing in the saga binds `Keypad * / . Enter`, `Delete`, `Home` or `PageUp`. Those
   are bare again. Only `Keypad +` and `Keypad -` want `Shift`/`Ctrl`/`Alt` - any of the three -
   because the player's Shaman's Mercy and Unseen own the bare press. The boon handler also stands
   down while a modifier is held, so the step-skip cannot double as a cast that eats a charge.

2. **The help text now lives beside the keys.** `RunService.DevKeyHelp` is the single source and the
   HUD renders it, so adding a dev key and telling the tester about it are edits to the same
   screenful of code. This is the same medicine as `BoonKeys`, which exists because a boon that
   activated perfectly and never named its key had already happened once. Same failure, second
   surface. Worth remembering that the pattern generalises: **anything the player has to press needs
   its name generated from the thing that reads it.**

And because the log could not tell a dead binding from an unheld modifier, **every dev key now logs
its line** as well as showing it. Next time the question is a grep.

### The hearth has its homestead back

Five steps - the roof, the fire, the cooking station, the bed and the chest - carried no explicit
`Track`, so `Split()` routed them by `Kind` and filed them under CRAFT. Worse than untidy: "Settle
in" needs a roof AND a fire, "Sleep through the night" needs a bed, and those prerequisites had
ended up on a DIFFERENT track from the steps requiring them. That is the invisible-prerequisite bug
from alpha26 back by another route. The five now name HEARTH, and the foraging step moved ahead of
the roof so it still opens the track (its `ItemsPickedUp` is measured from when it appears, so a
late one asks for berries after berries have stopped being interesting).

CRAFT is now tools and weapons: axe, hammer, workbench, upgrade, the shade, the bow, the shield.
HEARTH is a roof, a fire, a pot, a meal, a bed, a night's sleep, comfort, a box, the fishing, the pen.

### Shepherd stops talking like a cheat menu

`PetBuff.BuffAllPets` printed "No baseline, nothing buffed" whenever the pen was empty or held only
animals carrying no weapons - a GM diagnostic, shown to a player who had only picked a boon ("the
shephard quest says 'no baseline', which is confusing for a player"). `BuffAllPets`,
`ComputeGroupBaseline` and `ResetPetBuffs` take `quiet` now, and the saga passes it on all three
paths: gain, refresh and loss. Same leak as the god-mode warning this boon produced once before, and
the same fix shape - the legacy statics are fine to ride, but their *voice* belongs to whoever typed
the command.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19k - the menu, tidied

Two things from the first launch of the test run, both yours.

**No popup on success.** It only appears if initialisation FAILED now. A dialog to dismiss on every
launch is a toll for something that worked, and the menu's version line already says the mod is in.
The failure popup is also more robust than it was: if the popup system is not live yet the notice
is queued instead of dropped.

**The overlapping text is fixed.** The cause was mine: the appended line read
"VALHEIM: THE SAGA  v1.0.15-run.2026-09-19j" at full size, which is LONGER than the game's own
"Version 1.0.15 (n-40)", so TMP wrapped it to a third line and the block overflowed its rect and
drew over itself. It now reads `SAGA v1.0.15-run.2026-09-19k` on one line at 70% size - comfortably
narrower than the line above it - and word wrapping is turned off on that label as belt and braces.

Note the consequence: with the popup gone, **that line is the only proof the mod loaded**. If it is
absent, the mod is not in - check the log for `Could not brand the menu version label`.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19j - the Stormward

Installed here already (`-ModOnly`). Act I's last craft step, and the saga's second item of its
own.

**Stormward**, a shield: block 60 (+8/level, blackmetal tier), deflection 40, a 2.5x timed-block
bonus, **very resistant to lightning** and resistant to blunt. Thor's bow turns Eikthyr's storm
outward; this turns it aside. A shield with a named thing to answer is a different object from a
shield with a bigger number, and lightning is what the god at the end of this act does.

**Recipe**, at an IMPROVED workbench (level 2): 20 wood, 20 resin, **10 troll hide**, 10 deer hide
and **3 rescued lights**. It unlocks when the Breaker's step is done.

**The troll hide is the join, and it is the point.** The Breaker is losable, so a missed deadline
now costs you something you can hold: the recipe is on the bench and the hide is not. That is safe
to allow because the shield is the LAST step on the CRAFT track - nothing waits behind it, and an
unfinished craft track when the boss falls is the cost of rushing rather than a stalled act. The
hunt track, which ends at Eikthyr, is untouched either way.

Gating on a losable step is also safe for a subtler reason worth knowing: a failed step still
ADVANCES its track, so `StepDone` answers true whether the troll died or walked away. The bench
learns the shape either way.

**Three rescued lights again**, the same price as the bow, deliberately: a player who raced badly
can afford one of the two. That is a decision rather than a shortage.

**What to watch for:** the recipe appearing after the troll (and needing a level-2 bench - it will
not show at an unimproved one); the block actually holding against Eikthyr's lightning; and the
case that matters most - miss the troll on purpose once and confirm the act still finishes, with
only the shield lost.

Act I is 21 questline steps now.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19i - the Breaker, and the forest on your side

Installed here already (`-ModOnly`). The troll is Act I's mini-boss now, and one fight has
temporary allies.

**The troll moved from Act II to Act I**, as you asked. It is placed after the race: you have just
spent a night watching the forest CARRY lights away, and now something comes through that does not
want them at all. It keeps everything that made it good in Act II - named, announced, and the
saga's first LOSABLE step at fifteen run-minutes. A troll is weather, not a garrison.

It is SPAWNED, because the Meadows have no trolls - beside you, at night, like the Gatherer, so it
can never land somewhere unloaded. Unstarred on purpose: a troll against flint is already the
hardest thing in the act.

**The allies of convenience come from the game's own rule, not from forcing the AI.** I read
`BaseAI.IsEnemy` out of this build's IL: a ForestMonster is hostile to every faction except
AnimalsVeg, Boss and its own. So moving the troll off ForestMonsters is the entire mechanism -
one field, no reflection, nothing re-applied every tick against an AI that re-picks its own target
a second later. It is set to Demon, which leaves nothing in the world on its side: not the forest,
not the wildlife, not your raised skeletons, and not you.

They are NOT friendly and NOT tamed. They still want you dead; they want the troll dead more, and
standing between the two is your problem - which is what you asked for.

**Rewards re-pointed for the Meadows:** troll hide, deer trophies it had broken and was carrying,
coin and amber. The AncientSeeds it used to pay are gone - those are the Elder's key and belong an
act away, and handing them out here would let you walk into Act II holding its finale.

**What to watch for:** the announcement before nightfall and the arrival line; greydwarves actually
attacking it (that is the whole experiment); whether they also keep attacking YOU, which they
should; whether the fight is survivable in Act I gear with the forest helping; and the fifteen-minute
loss - let it run out once and it should walk away with a line rather than stand in your meadow.

Act I is 20 questline steps now; Act II is 9. Saga questline heat is unchanged at 53.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19h - Act I, before the light

Installed here already (`-ModOnly`). Three new beats on the HUNT track, between clearing the
greylings and the pale light. The story bible is updated to match; it is still the source of
truth.

The hunt track used to run "kill 6 greylings" straight into "follow the pale light", so
everything the act is about arrived as whispers while you were busy with an axe. Now:

**1. Hear the raven out.** Hugin finds you and states the errand: *"He did not send you for the
antlered one. He sent you to find where the light is going."* Odin's audit is the frame of the
whole saga and had never been in the chain. It retries until the bird actually speaks, and after
twenty attempts delivers the line plainly instead - a missing raven prefab must cost the staging,
not the saga.

**2. Hunt a deer by daylight.** And nothing rises: *"Nothing rose. They bank it while the sun is
up — there is no light in a deer at noon. Come back when it is dark."* Deliberately
anticlimactic. After it, the nocturnal rule is yours rather than ours, and it needs no second
explanation. The line only fires while the step is live, so it will not nag you for the rest of
the act.

**3. Keep a watch after dark.** Stay out past sundown until the meadows have whispered three
times. The whispers already existed with nothing depending on them; now they are the step. It is
the first thing in the saga that asks you to be in the dark with nothing to kill.

**What to watch for:** the raven actually landing and speaking (check the log if it does not - it
says when it falls back to a plain line); a daylight deer kill advancing the step and saying its
line; the strip telling you to wait for dark during the vigil; and the vigil NOT spawning packs or
starred deer, since nothing is being hunted yet.

**On the numbers.** Act I gains three questline steps, so +3 heat and +6 max health. Saga
questline heat goes 50 to 53. Heat has never been tuned, so this nudges a curve nobody has felt as
designed - worth a verdict on whether the act's first hour now runs long.

One thing the tests caught while building this and I want on the record: the dark-rule clause
landed in `DeerHunt` instead of `DarkStep` on the first pass, which would have spawned the pack
during a vigil that is supposed to be empty. Two assertions found it before the build shipped.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19g - keys are per-mode, and the saga says them

Installed here already (`-ModOnly`). Your two points, both taken.

**Keys ARE scoped per mode, and reuse is fine.** That was already true for the cheat mod
versus the saga: GM bindings go through `InputManager.Gate`, which makes every GM command dead
while a run is live, and its own comment says it exists precisely to resolve the Keypad1-7
collision with boon keys. So `Home` meaning one thing to the cheat mod and another to the saga
is the design, not a clash.

What was NOT scoped was the two layers INSIDE saga mode. The dev keys and the boon actives are
read from the same handler in the same mode, and dev had squatted on nine bare keys. So:

**Every dev key is now Shift + the same key.** `Shift`+`Keypad +`, `Shift`+`Delete`, and so on -
`dist/windows/DEV-MODE.md` has the full table, and it gained the two rows it had been missing
(`Home` teleport, `PageUp` creature dump). The rule fits in a line: a saga key is the player's,
the same key with Shift is the tester's.

**Which freed the good keys**, so the two new actives moved onto the numpad with the rest:

| Key | Boon |
|---|---|
| `Keypad +` | Shaman's Mercy |
| `Keypad -` | Unseen |

**And the saga now always indicates the key**, which was your condition. It had been stated in
THREE places - the input handler, a switch in the HUD, and a hand-written "[Ins]" at the end of
some descriptions - so an active could work perfectly and never tell you what pressed it. There
is now one table, `BoonKeys`: the input handler walks it, the HUD labels from it, and the offer
card reads "active  [+]" from it. Adding an active is one row, and forgetting to show its key is
no longer possible. The hand-written keys came out of the four descriptions that had them.

**What to watch for:** the offer card showing the key beside "active"; the held-boon strip showing
it too; `Keypad +` healing rather than completing a step; and `Shift`+`Keypad +` still completing
the step. Dev mode is on in your config, so that last one is testable immediately.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19f - two actives from the old mod

Installed here already (`-ModOnly`). Both are GM commands you already had, given a cooldown and
a reason - which is what BoonEffects is for.

**Shaman's Mercy** - `[PgDn]`, 90s cooldown. Casts the AoE heal (`CheatCommands.CastHealAOE`,
the `DvergerStaffHeal_aoe` prefab) where you stand. A BURST, where Second Wind is a 10s window
of AoE Renewal - that is the whole difference between them, and why both are worth a slot.

**Unseen** - `[Bksp]`, 150s cooldown, lasts 20s. Ghost mode (`CheatCommands.ToggleGhostMode`),
then off again on a timer. Short on purpose: it is a total answer to every melee in the game, so
its value should be "get out of this", not "win this".

**On the keys.** Every numpad symbol is already a DEV binding (KeypadPlus/Minus/Multiply/Divide)
and Keypad1-3 are the offer's own pick keys, so an active there would show "[1]" on a held boon
and "[1] pick" on an offer at the same time. PgDn and Backspace are unambiguous. Say the word if
you want them somewhere else - it is one line each.

**Both go through the legacy god-mode bracket**, like Second Wind and Emberskin, because both
commands are gated on `RequireGodMode` and a run forces that flag off. Unbracketed they would
refuse in every fair run while printing a GM warning - exactly how Shepherd's silent no-op was
found.

**What to watch for:** the heal actually healing you and your tames (and the GM-flavoured "Heal
AOE cast" line it prints - tell me if that should be a saga line instead); Unseen making enemies
lose you and, critically, wearing off after 20s rather than sticking; and a death mid-window
turning it back off rather than leaving you invisible for the rest of the run.

The pool is 30 boons.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19e - Bountiful

Installed here already (`-ModOnly`). One new boon, from your ask for a drop-multiplier one.

**Bountiful** (passive): *"Everything the land yields comes in far greater measure."* It
multiplies the run's baseline resource rate by 2, so x6 while held. `runResourceBoonMultiplier`
in the config if 2 is the wrong number.

It is deliberately the counterpart to Windfall rather than a second copy of it: Windfall doubles
what is ALREADY in the pack, once, and Bountiful multiplies what the land gives up for the rest
of the run. A burst against a rate - both are worth holding.

The plumbing from Quick Study was generalised rather than copied: `WorldModifiers.ApplyBoostedRate`
now writes any one rate key as baseline x boon, and `RunService.RefreshRateBoons` rewrites BOTH
rates on every call. That costs two key writes and buys the invariant that matters - the rates are
a pure function of (config, held boons), so nothing can drift whatever order anything ran in.

**What to watch for:** the offer listing Bountiful; drops visibly multiplying while it is held
(a tree is the quickest test); and the rate going back to plain x3 if a death takes the boon away.
The pool is 28 boons now.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 - TASK: 1.0.15-run.2026-09-19d - the bow costs light now

Installed here already (`-ModOnly`). One change, from your note that Thor's bow was too simple
to craft.

**The lights you rescue are an ITEM now.** "Rescued light", cloned from the Mistlands Wisp -
which is already exactly this thing: a caught light, a material, with its own glow. One drops
into your pack for every light you take back off the forest, and for every stray you find.

**Thor's bow asks for three of them**, on top of 10 wood, 10 resin and 6 deer hide. The other
three are what the Meadows give anybody; the light is the only ingredient that has to be won -
and it comes from the HUNT track while the bow sits on the CRAFT one, so the two tracks finally
ask something of each other.

**One thing to expect, and it is the game's rule not ours:** the bench will not list the bow
until you are HOLDING a light. Valheim only offers recipes whose every ingredient the player has
seen at least once. So after paying the shade the bench may show nothing, and one light fixes it.
The step's hint now says so outright.

**Why this cannot lock the questline.** The Gatherer frees `Clamp(lightsLost, 2, 6)` lights when
it dies, so the worse the race goes the more it is carrying - a forfeited race (8 lost) frees six.
Lose every light and the bow is still craftable; you just have to take them off the thing that
stole them, which is the better story anyway. Forfeiting does NOT grant items, deliberately: "the
lights are gone, and the trophies with them" would be a lie if the pack filled up regardless.

**What to watch for:** a "Rescued light" appearing each time you walk into one; the stack surviving
a portal (it is weightless and teleportable on purpose); the bench listing Thor's bow once you hold
one; and the three being consumed on the craft.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 — TASK: 1.0.15-run.2026-09-19c — four things from the run

Installed here already (`-ModOnly`; the Patcher did not change). All four came out of your
own play notes on `...19b`.

**1. No fishing task before you own a rod.** `ChallengeDefinition` gained a `RequiresItem`
gate — the tool sibling of `RequiresBuilt` — and both pool bounties (`c-fishhaul`,
`c-fishcook`) now carry `RequiresItem = "FishingRod"`. It reads the LIVE inventory when a
slot is dealt, so a rod left in a chest withholds the task, and selling one afterwards does
not retract a task already in play.

The questline's own fishing steps are untouched and were never the problem: `mq-rest` hands
you the rod and sits ahead of `mq-fish` on the hearth track. What you saw was a POOL slot.

**2. Windfall has three charges.** Was one, by an old ruling; the card now reads
"3 charges, never refills" and the number in the text and the number in the code are the
same constant, so they cannot drift. Each spend reports what is left.

**3. The stash is sorted alphabetically.** Sorted in the LIST, not in the view — the list is
what "Take" indexes into, so sorting only the display would have been a mapping to get
wrong. Two rows sharing a prefab order by quality. A resumed stash comes back sorted
whatever order the save file held.

**4. A new boon: Quick Study.** *"Every skill rises far faster, in whatever you do."* Passive.
It multiplies the run's SkillGainRate by 3 on top of the baseline 3x, so 9x while held.
Unlike Woodsman/Hunter/Warrior it is not a one-off grant into one skill — it pays out in
whatever you actually spend the saga doing. `runSkillBoonMultiplier` in the config if 3x is
the wrong number.

**What to watch for:** that a fishing bounty now only ever appears with a rod on you; that
Windfall says "2 left" and then "1 left" rather than going quiet; that the stash reads
alphabetically and "Take" empties the row you clicked; and that Quick Study visibly moves
skills when held. Tests: 561 assertions, all passing.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-09-19 — TASK: 1.0.15-run.2026-09-19b — the mod loads itself, and says so

Built and installed on this box already. Just play it. (`...-19` was the full install, since
the Patcher changed; `...-19b` added the menu line and went in with `-ModOnly` — which was
also a live test of the new stale-patch guard, and it passed.)

**0. The main menu's version line should now read two lines:**

```
Version 1.0.15 (n-40)
VALHEIM: THE SAGA  v1.0.15-run.2026-09-19b
```

the second in gold. That is the standing answer to "is the mod loaded" — the popup says it
once and is then gone. If the gold line is missing while the popup appeared, the log will
say `Could not brand the menu version label` with the reason.

**Valheim updated to 1.0.15 today at 12:25** and Steam put a vanilla assembly back, which
is why nothing loaded before this build. Unity is unchanged at 6000.0.75, and 1.0.15 broke
nothing the mod calls: all 316 member references from the built DLL into the game's
assemblies still resolve, and the rebuild needed no source change.

**1. The mod loads at startup — do NOT open Credits.** That is the whole test. The version
popup should appear at the main menu on its own and read **v1.0.15-run.2026-09-19b**.

Why it moved: a saga item is only known to the game while the mod is loaded, so loading a
character before visiting Credits left Thor's bow an unresolved prefab name. Valheim drops
it from the pack and the next save writes it gone, silently. The entry point is now
`FejdStartup.Start()` as well as `OnCredits`.

**What to check, in order:**

1. Launch the game. Popup at the main menu, right version, without touching Credits.
2. **Load the character carrying Thor's bow** — straight in, no Credits visit. The bow must
   still be there. Then save, quit to desktop, come back, and check it is still there.
   In `Player.log` there must be **no** `Failed to find item prefab`.
3. **Quit to the main menu from inside a world.** The start scene reloads and calls the
   entry point again, which should log a line and do nothing else — no second popup. Then
   press a GM hotkey once and make sure it fires ONCE (a doubled command registry would
   double-fire, which is what a lapsed guard would look like).
4. **Open Credits deliberately.** A log line, no dialog, nothing breaks.
5. Then the four builds still without a verdict from 12-13 September: Thor's bow on the
   bench after paying the shade and the flash on impact, the shade's greeting and its
   "[E] Speak" prompt, the saga dreams, and raids arriving as beats.

The log line to grep for either way:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" `
    -Pattern 'ICanShowYouTheWorld|Failed to find item prefab'
```

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-18 — FROM MAC: diagnosis confirmed, alpha3 incoming — no action yet

Your evidence cracked the case completely — the timeline reconstruction and
especially the observation that the run belonged to *Draupnir* while the
refusals fired as *naked* was the key. Diagnosis: the in-memory run survives
logout and character switches inside one game process; nothing ever unloads
it. Every symptom follows (stale boon/timer on the new world, frozen
accumulators, abandon refused by the world guard). Your log also proved a
second bug: no autosave ran during the resumed session (the resume path
never saved and the logout path bailed before the autosave block).

**alpha3** is in review on the Mac side: runs now *suspend* (unload from
memory, state file kept) when their world goes away, keyed to world identity
so deaths/respawns don't trip it; autosave fixed; Run Mode refuses joined
servers (v1 = local/hosted only); the lobby gets a [Discard saved run]
escape hatch; no run UI at the menu.

Standing notes for you:
- **Keep evidence work exactly as you did it** — report-only, timeline-first,
  verbatim log values, one commit. That division stands: Mac side builds and
  fixes; Windows side installs, tests, and reports. Don't fix mod code here.
- **Do not delete `ICSYTW_run_Draupnir.json`** — it holds the only copy of
  world `hjklgggggggg`'s pre-run modifier values. After alpha3, loading that
  world as Draupnir and abandoning properly will restore the world's rates.
- The CRLF churn you noted (11 files "modified" with an empty
  `--ignore-cr-at-eol` diff): don't commit those files; a `.gitattributes`
  will land from the Mac side to end it. `git stash` or checkout them away
  if they block a pull.
- When the next TASK entry appears here (alpha3 install + retest), it will
  name the exact log lines to watch for.

## 2026-08-18 — TASK: collect evidence for the resume/freeze bug (report only, fix nothing) — **DONE, superseded**

Martin hit three bugs in a Run Mode session on this machine (local worlds,
not multiplayer): (1) a run started on one local world **resumed on a
different, newly created world** ("run resumed", old boon present); (2) after
that resume the **timer did not count** and the no-armor challenge timer
didn't accumulate; (3) he **couldn't abandon** the run. The Mac side needs
the world-identity evidence to diagnose; do NOT attempt fixes — the fix
lands from the Mac side.

Collect and append under RESULTS below:

1. `git log --oneline -3` and `git status --short` (which build was installed
   during the buggy session — if the working tree or log shows alpha1
   (`f33b97f` or earlier) at the time, say so).
2. All `[ICanShowYouTheWorld]` lines from Player.log — especially any
   `Run Mode started (seed=..., world=...)`, resume, "belongs to another
   world", freeze, or abandon-refusal lines. Include the `world=` values
   verbatim. Player-prev.log too if the buggy session was two launches ago.
3. Full contents of every `ICSYTW_run_*.json` (and `.json.corrupt` if any) —
   the `worldId` field is the key datum.
4. Whether the current installed assembly has both injections:
   `[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("<Managed>\assembly_valheim.dll")).Contains("CharacterDied")`
5. Then update to alpha2 if not already on it: `git pull`, full
   `.\Install-Mod.ps1`, and note whether it printed
   "Verified both injections present".

### RESULTS (Windows side appends here)

Collected 2026-08-18 ~15:20 local, after `git pull` (89ea95e).

**1. Build during the buggy session: alpha2, NOT alpha1.** The alpha2 kit was
installed at 14:55 today and the buggy session started 14:56 — Player.log
line 329 confirms `Starting initialization (v0.221.12-run.alpha2)`.

- `git log --oneline -3`: `89ea95e` (handoff task), `32081f1` alpha2 kit,
  `eb85815` alpha2 bump.
- `git status --short`: 11 files show modified, but `git diff
  --ignore-cr-at-eol` is empty — pure CRLF churn from the Windows checkout,
  no real local changes.
- Installed files: `assembly_valheim.dll` 18/08 14:55:50,
  `ICanShowYouTheWorld.dll` 18/08 14:55:22 (vanilla backup 23/02 07:33).
- The *previous* session (Player-prev.log, 17/08 17:27–17:40) ran **alpha1**
  and is where the run was started.

**2. Mod log lines, in timeline order.**

Player-prev.log (17/08, alpha1) — run start, on world `hjklgggggggg`:

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha1)
08/17/2026 17:27:14: Load world: hjklgggggggg (hjklgggggggg)
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=220064750, world=4511032380:hjklgggggggg, pre-defeated=1).
[ICanShowYouTheWorld] Kill hook never fired — kill challenges will not progress.
```

Player.log (18/08, alpha2) — the buggy session:

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha2)
08/18/2026 14:56:31: Load world: hjklgggggggg (hjklgggggggg)     <- character Draupnir
[ICanShowYouTheWorld] Run Mode world modifier originals imported (6 key(s)).
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode run resumed at 12:51.             <- resume on the CORRECT world
08/18/2026 14:56:48: Shutting down                               <- logout to menu
[ICanShowYouTheWorld] Run Mode frozen: Run paused — world not loaded.
08/18/2026 14:57:16: Cloud Save: .../characters/naked.fch.new    <- NEW character "naked" created
08/18/2026 14:57:45: Load world: heatheat (heatheat)             <- NEW world, freshly created
08/18/2026 14:57:45:   missing /worlds/heatheat.db
   ...intro/YOU DIED text plays (brand-new world)...
   (NO [ICanShowYouTheWorld] resume/freeze/mismatch line after the heatheat load)
[ICanShowYouTheWorld] Load the run's world to abandon it.        <- x5, ~15:02-15:03
08/18/2026 15:03:47: Game - OnApplicationQuit
```

Note: **no "belongs to another world" line appears in either log.** After the
heatheat load the mod logs nothing at all until the five abandon refusals.
The `world=` value at run start is verbatim `4511032380:hjklgggggggg`.

**3. Run state files.** Exactly one, no `.corrupt`:
`ICSYTW_run_Draupnir.json`, LastWriteTime **17/08/2026 17:40:45** — i.e. it
was never rewritten during today's session despite the resume.
`elapsedSeconds` 771.97 ≈ 12:51, matching the resume line exactly — the
persisted timer never advanced past yesterday's value. Full contents:

```json
{
    "elapsedSeconds": 771.968017578125,
    "heat": 1.0,
    "defeatedBossKeys": [ "defeated_eikthyr" ],
    "splitLabels": [],
    "splitTimes": [],
    "activeChallengeIds": [ "alt-150", "k-skeleton" ],
    "activeChallengeProgress": [ 53.61872482299805, 0.0 ],
    "heldBoonIds": [ "wind" ],
    "heldBoonCooldowns": [ 105.24357604980469 ],
    "heldBoonCharges": [ 0 ],
    "rngSeed": 220064750,
    "worldId": "4511032380:hjklgggggggg",
    "modifierKeys": [ 4, 9, 7, 8, 1, 12 ],
    "modifierValues": [ 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 ]
}
```

**4. Installed assembly injections** (byte-scan of the live
`assembly_valheim.dll` in Managed): `NotACheater` **True**, `CharacterDied`
**True** — both injections present during the buggy session.

**5. Update to alpha2:** already on it — the full `Install-Mod.ps1` run at
14:55 today (before the buggy session) is what installed it, and `git pull`
now only fetched this handoff file, so no reinstall was performed. Injection
presence was verified directly via the byte-scan in (4) instead.

**Observation (no fix attempted):** the run belongs to character *Draupnir*;
there is no `ICSYTW_run_naked.json`, yet the abandon refusals fired while
playing the brand-new character *naked* on the brand-new world *heatheat*.
The in-memory run (with the `wind` boon) evidently survived the
logout/character-switch inside one game process, still in its
menu-time frozen state — which would also explain the non-counting timers
(frozen) and the abandon refusal (world-identity guard). Whether the "run
resumed" toast Martin saw on the new world came from this same surviving
state at 14:56 or a separate resume, the logs only show the one resume line,
on the correct world.

---

## 2026-08-18 — TASK: install run.alpha3 and retest the lifecycle

`git pull`, then full `.\Install-Mod.ps1` from dist\windows (expect
"Verified both injections present"). In-game popup must read
**v0.221.12-run.alpha3**.

Martin drives the gameplay; your job is log watching + a results append.
The lifecycle scenarios and their expected log lines:

1. Old business first: as **Draupnir**, load world **hjklgggggggg** → expect
   `Run Mode run resumed at 12:51` (the parked run, now with kill challenges
   working) → **Abandon it** from the HUD (double-press) → expect
   `Run Mode world modifiers restored` and the state file deleted. That
   clears the stranded world's rates properly.
2. Fresh run on any local world. Kill a greydwarf → the kill tally must move
   (hook verified at install).
3. **Die on purpose** → expect NO `Run suspended` and NO `run resumed` lines
   during the respawn — the run freezes and continues; passives re-apply.
4. Logout to menu mid-run → expect `Run suspended (...) — resumes when ...`
   after ~5s. Menu must show NO run strip.
5. Switch to another character/world → no run present, lobby clean; the
   suspended run resumes only for its own character+world.
6. Append RESULTS: version line, the log lines above verbatim, anything
   unexpected.

### RESULTS (Windows side appends here)

Collected 2026-08-18 ~20:25. **Partial: only scenario 2 was played.** Martin
ran one fresh alpha3 run and quit straight from the game — the session never
went back to the menu, so scenarios 1, 3, 4 and 5 were not exercised and
none of the suspend/resume/abandon paths were reached. What alpha3 *did*
prove is nonetheless significant: **both alpha2 bugs are fixed.**

**Version:** `[ICanShowYouTheWorld] Starting initialization
(v0.221.12-run.alpha3)`. Install printed "Verified both injections present".

**Full mod log, alpha3 session (Player.log, 17:07–17:12):**

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha3)
   ... 7 services registered, "Initialization complete!" ...
08/18/2026 17:07:13: OnCharacterStart                      <- character Naked
08/18/2026 17:07:21: Load world: heatheat (heatheat)
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=305252140, world=2029997972:heatheat, pre-defeated=0).
08/18/2026 17:12:57: Game - OnApplicationQuit
```

That is every `[ICanShowYouTheWorld]` line in the session — no suspend, no
resume, no abandon, no error lines.

**Bug 1 (timer frozen) — FIXED.** `ICSYTW_run_Naked.json` records
`elapsedSeconds` **310.73**. Run start 17:07:42 → quit 17:12:57 is 315s
wall-clock. The timer accumulated in real time.

**Bug 2 (no autosave) — FIXED.** The state file's LastWriteTime is
**17:12:53**, four seconds *before* `OnApplicationQuit` at 17:12:57. Under
alpha2 the equivalent file was never rewritten at all.

**Challenge tallies moved** (bearing on the alpha4 task's "first live check"
of the unverifiable item tokens — these already ticked under alpha3):

```json
"activeChallengeIds":      [ "c-food", "c-stone", "c-wood" ],
"activeChallengeProgress": [ 8.0,      5.0,       7.0       ],
"heldBoonIds": [], "heat": 0.0, "rngSeed": 305252140,
"worldId": "2029997972:heatheat"
```

No greydwarf kill challenge was drawn this run, so the kill hook remains
unverified in live play (the install-time byte scan confirms it is injected).

**Still outstanding — scenario 1 was not done.** `ICSYTW_run_Draupnir.json`
is untouched (still 17/08 17:40:45), so world **hjklgggggggg** still has the
run's modifier rates applied and its pre-run originals still live only in
that file. Preserved, not deleted, per your standing note.

---

**Note for the alpha4 task below:** the alpha3 run on `heatheat` as *Naked*
is still live in `ICSYTW_run_Naked.json` and will resume when that
character+world loads under alpha4. I left it in place rather than assume it
should be discarded — say the word if the alpha4 test wants a clean slate,
or Martin can use the new [Discard saved run] lobby button.

---

## 2026-08-18 — TASK: install run.alpha4, combined test

`git pull` → full `.\Install-Mod.ps1` (expect "Verified both injections
present"; popup must read v0.221.12-run.alpha4). DELETE or edit the local
ICanShowYouTheWorld.json so runChallengeRefillSeconds picks up the new 45s
default. Martin drives; watch the log. New in alpha4: opener chain (wood →
stone → craft), pinned first boon (Enduring), stat-delta quests, smooth
score, boss HP scaling (log: "Boss vigor"), suspend-on-logout. Key checks:
death mid-run = NO suspend/resume lines; wood/stone opener tallies must
tick (unverifiable item tokens — first live check); boss max HP visibly
higher and RESTORED on abandon. Append RESULTS as usual.

### RESULTS (Windows side appends here)

**Install done 2026-08-18 20:27 — ready for Martin to play. Gameplay results
pending below.**

- `git pull` → `fe26080`. Full `.\Install-Mod.ps1`: Steam buildid 21981559
  matched, both patches applied, **"Verified both injections present (entry
  point + death hook)"**.
- Installed `ICanShowYouTheWorld.dll` (211,968 bytes, 20:27:44) contains the
  version string **`0.221.12-run.alpha4`** — verified by scanning the DLL, so
  the in-game popup should match.
- Config cleared: the local `ICanShowYouTheWorld.json` dated 16/08 predated
  Run Mode entirely — it contained **no `run*` keys at all**, so
  `runChallengeRefillSeconds` would have taken the compiled default in any
  case. Deleted anyway so the file regenerates with the complete alpha4
  default set; a copy is parked in this session's scratchpad if anything in
  it turns out to have been non-default.
- Working tree clean — the `.gitattributes` you added ended the CRLF churn;
  the pull applied with no conflicts and `git status` is empty.

**GAMEPLAY RESULTS — 2026-08-18 20:33–20:42.** Headline: **abandon works and
restores modifiers**, timer and autosave still good — but the session
produced a **28 MB Player.log, 99.9% of it one repeated Unity warning**.
Root cause traced below. Death and logout were again not exercised.

**Version:** `[ICanShowYouTheWorld] Starting initialization
(v0.221.12-run.alpha4)`. Config regenerated at 20:33:39 with the full alpha4
key set — `"runChallengeRefillSeconds": 45.0` confirmed present.

**Every mod line in the session, verbatim and in order:**

```
[ICanShowYouTheWorld] Starting initialization (v0.221.12-run.alpha4)
   ... 7 services registered, "Initialization complete!" ...
08/18/2026 20:33:45: OnCharacterStart                    <- Naked
08/18/2026 20:33:51: Load world: heatheat (heatheat)
[ICanShowYouTheWorld] Run Mode world modifier originals imported (6 key(s)).
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode run resumed at 05:10.
[ICanShowYouTheWorld] Run Mode world modifiers restored (6 key(s)).
[ICanShowYouTheWorld] Run Mode run abandoned.
[ICanShowYouTheWorld] Run Mode baseline world modifiers applied (resource=3, skill=3, moveStamina=0.5, staminaRegen=1.5).
[ICanShowYouTheWorld] Run Mode started (seed=317655781, world=2029997972:heatheat, pre-defeated=0).
08/18/2026 20:42:34: Game - OnApplicationQuit
```

**Abandon path — WORKS.** The parked alpha3 run resumed at 05:10, then
abandoned cleanly: `world modifiers restored (6 key(s))` followed by
`run abandoned`, and a fresh run started immediately after with its own
baseline re-applied. This is the first live proof of the restore path.

**Timer and autosave — still correct.** New run 20:34:32 → state file
written 20:42:26, eight seconds before the 20:42:34 quit;
`elapsedSeconds` 476.24 against ~474s of wall-clock.

**alpha4 state file gained `activeChallengeBaselines`** and the stat-delta
quests are populating it:

```json
"activeChallengeIds":       [ "s-run",   "c-wood", "naked-5" ],
"activeChallengeProgress":  [ 582.774,   3.0,      0.0       ],
"activeChallengeBaselines": [ 491.049,  -1.0,     -1.0       ],
"heldBoonIds": [ "ember", "hearty", "fleet" ],
"heat": 2.0, "rngSeed": 317655781, "worldId": "2029997972:heatheat"
```

`c-wood` ticked to 3 — the unverifiable item token counts in live play.
Heat 2.0 with `runDeathHeatPenalty` 3.0 untouched is consistent with two
challenge completions and no death.

---

### ⚠ NEW BUG: one million log lines from the ember boon's ring

`Player.log` came out **28,376,202 bytes / 1,049,543 lines**, of which
**1,048,537 are the single line `Tag: Tree is not defined.`** — 99.9% of the
file, roughly 2,900 lines per second sustained for six minutes (first
occurrence just after the run start ~20:34:41, last ~20:40:46). The alpha3
session's log has **zero** occurrences, so this is newly reachable, not
pre-existing noise.

Chain, traced through the code (not inferred from the log alone):

```
ember boon → BoonEffects.ActivateEmber()          (BoonEffects.cs:433)
  → CheatCommands.ToggleCloakOfFlames()           (CheatCommands.cs:1658)
    → CheatVisualizer.TogglePbaoeRing(5f)         (CheatCommands.cs:1662)
      → CircleVisualizer on a ring parented to the player
        → Update() every frame: Physics.RaycastAll per segment, and
          go.CompareTag("Tree") on every hit      (CircleVisualizer.cs:135)
```

Valheim's TagManager defines no `Tree` tag, so Unity emits that warning on
**every CompareTag call** — segments × hits × frames. Beyond the log volume
this is a per-frame `RaycastAll` over every segment, so there is a real
frame-cost question too.

Two things worth your attention, both left for the Mac side to judge:

1. `EmberOnSeconds` is 30, but the ring kept drawing for ~365 seconds, so it
   outlived its window. Either `ForceCloakOff` did not fire, or it fired and
   the ring survived it. Note `ForceCloakOff` returns early when
   `_cloakOnByUs` is false, and the destroy path runs only inside the
   `CheatCommands.CloakActive` branch.
2. The `CompareTag("Tree")` is legacy `CircleVisualizer` code, unchanged by
   alpha4 — Run Mode simply gave it its first live activation, since GM
   commands are gated during a run and the boon is now the only way in.

**Not tested again (no death, no logout).** There is no player death in this
log — no `YOU DIED`, no respawn after 20:33:51; the `On death False` line at
20:40:16 is vanilla and unrelated. The session ended by quitting straight
from the game, so `Run suspended` was never reached. **No `Boss vigor` line
appeared** — no boss was fought, so HP scaling and its restore-on-abandon
are still unverified.

`ICSYTW_run_Draupnir.json` remains untouched (17/08), so world
`hjklgggggggg` still carries its run rates.

---

## 2026-08-18 — FROM MAC: alpha5 fixes the log flood (thanks — excellent trace)

Your chain was exactly right: `CircleVisualizer.Update` called
`go.CompareTag("Tree")` per segment per hit per frame, and Valheim defines
no such tag, so Unity warned every call. Fixed three ways:
(1) the tag test is gone — trees and characters are now detected by
component (`GetComponentInParent<TreeBase>/<Character>`), which is also what
the code actually meant; (2) rings self-destruct after 300s
(`maxLifetimeSeconds`) so no path can leave one raycasting forever;
(3) `ForceCloakOff`/`ForceAoeRenewalOff` now call new explicit
`CheatVisualizer.KillPbaoeRing()/KillConformHeal()` — the ring's lifetime
is the boon's, not a flag's — and the toggles null their statics so a
destroyed ring can't be "already spawned" forever. That last one is the
likely cause of your observation 1 (ring outliving its 30s window):
a stale non-null static meant the next toggle destroyed instead of spawned,
desyncing ring state from the flag.

TASK: `git pull` → full `.\Install-Mod.ps1` → popup reads
**v0.221.12-run.alpha25** (alpha6 = alpha5's log fix + the new Pugilist boon:
melee attacks cost zero stamina; bows/crossbows unaffected). Then: activate the ember boon (Keypad5), confirm
the ring appears and disappears after ~30s, and confirm the log stays small
(`(Get-Item Player.log).Length` after a few minutes — expect KB, not MB) and
contains no "Tag: Tree is not defined." Still outstanding from before:
death mid-run (no suspend/resume lines) and logout suspend. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha8 (pace + quests + ability bar)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
Changes from Martin's alpha7 play-test: collect/kill targets cut roughly in
half (Hold 25 Wood, 10 food, Run 400m, Kill 6 Greydwarves...); ~17 new
quests including **composite multi-objective quests** with per-objective
text ("First Blood: kill 1 Boar / gather 5 food"); biome kill contracts;
and an **ability bar** under the timer showing usable actives with their
key, green when ready.

Watch for: any quest whose counter never moves — several mob prefab names
(Greyling, Ghost, Surtling, Greydwarf_Elite, Lox, Deathsquito) could NOT be
verified from the assembly (Unity asset data, not IL literals), so a wrong
name fails silently as a dead quest. Note which ones tick and which don't.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha9 — themed HUD, foundation-first

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
alpha9 = alpha8's pacing (halved targets, more small quests, ability bar)
+ the RESTYLED HUD (dark panels, serif font, real progress bars, cooldown
wipes on ability slots, boon-offer fade-in, heat pulse, gold completion
flash) and composite quests PARKED (all dealt quests are one-liners again;
the engine stays for the future story system). Watch: HUD legibility at
your resolution (uiScale in the config if needed); prefab-name counters
(Greyling/Ghost/Surtling/Greydwarf_Elite/Lox/Deathsquito); still
outstanding: death mid-run + logout suspend log checks. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha10 — THE RATE FIX (this one matters)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
Two field bugs from Martin's alpha8 session fixed:
1. **World-modifier rates were never live.** Valheim caches every rate as a
   static on Game, refreshed only by UpdateWorldRates (normally at world
   load) — so ×3 resources/skills and heat's enemy scaling never actually
   applied mid-session. Now refreshed after every write. VERIFY: chop a
   small beech during a run — expect ~3 wood, not 1; abandon → expect ~1.
2. **Biome-gated quests.** Mob quests only deal after the player has
   visited the mob's biome this run (ghosts additionally moved to tier 2).
   VERIFY: fresh run in Meadows → no ghost/draugr/wolf quests in early
   deals.
Append RESULTS including the wood-count check numbers.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-19 — TASK: alpha11 — Act I main questline

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
New: the QUEST section (gold, pinned above TASKS): Craft an axe → bow+40
arrows; Kill 3 Deer → leather armor; Kill 4 Greydwarves → helmet/cape/flint
arrows; Defeat Eikthyr → antler pickaxe. Item rewards go to inventory
(dropped at feet if full). WoodCutting is boosted to 100 for the run
(snapshotted, restored at run end — verify it restores after abandon!).
Watch: every quest step's counter (mob prefab names unverifiable — a wrong
one stalls the chain silently); reward grants (a wrong ITEM name logs an
error and grants nothing — report any). Plus the standing checks: wood
count ~3x mid-run, biome gating, death/logout log silence. Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha15 — boon descriptions in the offer

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.
Martin's note: "it's not apparent what the skills do" — the offer showed
only a name and passive/active. Each of the 13 boons now carries a
one-line `Description` rendered under its name in the offer panel, and the
panel grew (460x200) to fit three columns of wrapped text.

Watch for: any of the three columns clipping its text at the panel's
bottom edge, or the panel overflowing at a non-default UI scale — screenshot
if so and note your scale. The wording is a claim about behaviour, so also
flag any description that doesn't match what the boon actually does in play
(e.g. Waystone says "next boss altar, one charge"). Append RESULTS.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha16 — Packbrother replaces Packleader

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin's call: Packleader (buff your tames) is dead in a run — you never
reach a tame before the clock matters. It is GONE, replaced by
**Packbrother**: an active on **Keypad 7**, 4-minute cooldown, that summons
a tamed wolf which follows and fights for you. Two alive at a time; a third
summon replaces the oldest. One star per boss felled (capped at two stars).

The summon is loaned like everything else: its ZDO is marked
non-persistent, so it is never written into the world save, and it is
dismissed when the run ends, when the boon is lost to death, and on suspend.

Watch for, in priority order:
1. **Any wolf that survives the run.** Abandon a run with two wolves alive,
   then check they are gone; repeat with the wolves left behind in an
   unloaded zone (summon, run 200m away, abandon) and go back to look.
   Then quit to menu, reload the world, and confirm no wolves.
2. **The prefab name.** "Wolf" is asset data and cannot be verified outside
   the game; if the popup says `Missing prefab: Wolf`, report it — that is
   the whole boon dead and it needs a different name.
3. Does the wolf actually engage hostiles and follow you? Does it survive
   long enough to matter?
4. Known and deliberate: **your wolf's kills count toward kill contracts.**
   Say whether that feels good or cheap in play.

Also in this build: an existing run saved under alpha15 or earlier that is
still HOLDING Packleader will silently lose that boon slot on resume (the
engine drops boon ids it no longer knows). Expected, not a bug to report.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha17 — THE PERCENTAGE FIX (this one matters most)

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

**Root cause found for "I chop a tree and get 1 wood" and "we run out of
stamina way too fast".** Valheim reads its world-modifier rate keys as
PERCENTAGES — `Game.UpdateWorldRates` does `rate = stored / 100` (verified in
the IL). This mod wrote bare multipliers, so every empowerment was silently
inverted into a crippling penalty:

| we asked for | the game actually applied |
|---|---|
| resources x3 | x0.03 (Ceil floored every drop to 1) |
| stamina regen x1.5 | x0.015 (stamina never came back) |
| skill gain x3 | x0.03 |
| heat's enemy damage | x0.01-0.02 — heat made enemies WEAKER |

Worse, restoring an untouched key wrote back "1" — i.e. 1%. **Any world that
has finished or abandoned a run under a previous build is sitting on
resourcerate=1 permanently, in and out of Run Mode.** alpha17 detects a stored
rate below 5% as damage from an earlier build and clears the key at run end,
so starting and ending one run on an affected world repairs it.

**Expect the difficulty to change sharply.** Heat now genuinely scales enemy
damage for the first time, and movement stamina now genuinely costs (it was
effectively free at 0.005x). Baseline is resources x3, skill x3, move stamina
x0.5, stamina regen x2.5, all stamina costs x0.75.

Also in this build: questline reordered (axe → hammer → workbench →
greydwarves → deer → Eikthyr; the deer step pays Eikthyr's 2 summoning
trophies); the axe step grants WoodCutting/Axes/Bows at 25 and WoodCutting no
longer starts at 100; three skill boons (Woodsman/Hunter/Warrior); a standing
repeatable task ("Heed Hugin 5 times") that pays a boon every time it fills;
50 wood + 20 stone granted on every boss kill.

Watch for, in priority order:
1. **Wood per tree.** Fell a beech: expect roughly 3x vanilla, NOT 1.
2. **Whether an old world repairs itself.** On a world you have run before,
   start a run and abandon it, then check wood drops OUTSIDE Run Mode. The log
   will say "was N% — treated as damage from an earlier build".
3. **Is heat now too punishing?** Report the heat level where it stops being
   fun — that number is a config change, not a rebuild.
4. Stamina: still too tight, or now too generous?
5. Skill levels after a run ENDS: Axes/Bows/WoodCutting must not be lower than
   before the run started. The run gives back only what it lent.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha18 — Meadows questline steps, no duplicate boon offers

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Both from Martin's alpha17 play-test ("the pacing feels better"):

1. **Two more starter-zone steps before the greydwarves**, and armor now
   arrives a piece at a time instead of all at once:
   workbench → leather tunic; **Hunt 5 Boar** → leather leggings + 50 wood
   arrows; **Kill 4 Necks** → wooden shield + 20 flint arrows; greydwarves →
   helmet + cape + 30 flint arrows. Act I is now: axe → hammer → workbench →
   boar → necks → greydwarves → deer (Eikthyr's trophies) → Eikthyr.
2. **A boon you already hold is never offered again.** Held passives were
   already excluded; the four ACTIVES were not, which is what made offers
   repeat. Waystone's charges came from re-picking it, so it now gains a
   charge on every boss kill instead.

Watch for:
- `ShieldWood` is the one item name in this build that has never been granted
  before — if the shield doesn't arrive after the Necks step, check the log for
  a grant error and report the line.
- Boar and Neck counters ticking (both prefab names are already used elsewhere
  in the pool, so they should be safe).
- Offers: three DISTINCT boons you don't already hold, every time.
- With the pool at 16 and duplicates excluded, a long run can run the pool dry.
  If an offer ever shows fewer than three options, say so — that is the signal
  the pool needs more boons rather than a bug.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha19 — the greydwarf step was in the wrong biome

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin: greydwarves live in the Black Forest, so that questline step sent the
player out of the starter zone before the Meadows boss. It is now **Kill 6
Greylings** (the weaker meadows cousin, hence 6 rather than 4). Same step ID,
so a run part-way through it keeps its progress — kills already banked count
toward the new target.

Act I is now entirely Meadows-doable: axe → hammer → workbench → 5 Boar →
4 Necks → 6 Greylings → 3 Deer (Eikthyr's trophies) → Eikthyr. Greydwarves
remain in the RANDOM pool, correctly gated to the Black Forest.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha20 — shelter and rest before Eikthyr

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Two steps added between the greylings and the deer hunt:

- **Raise a roof (15 pieces)** → 100 wood + 50 stone to finish it.
- **Sleep through the night** → a hot meal and 20 flint arrows.

Both ride stats the game keeps itself — `Builds` counts every piece placed,
`Sleep` counts every night slept — so neither can silently stall the chain the
way a check against a named building piece would. Sleeping is the real test of
a shelter (roof, fire, bed, no monsters at the door) and it puts the player at
the boss in daylight.

Act I is now: axe → hammer → workbench → 5 Boar → 4 Necks → 6 Greylings →
roof → sleep → 3 Deer (Eikthyr's trophies) → Eikthyr.

Watch for: **`CookedMeat`** is the one unverified item name here (the shelter
step pays wood and stone, both proven). If no meat arrives after sleeping,
grab the grant error from the log. Also worth reporting: whether 15 pieces is
the right size for "a shelter" or feels like busywork.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha21 — HUD geometry + Hunter's Eye boon

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

Martin reported the run HUD was cramped and scrolled BOTH ways, and that some
held boons showed their "passive" tag with **no name beside it**. Same root
cause: the task and boon rows were wider than the 340px window, and when a
horizontal group overflows, GUILayout squeezes the flexible parts to zero — a
word-wrapping label squeezed to zero renders as nothing at all.

Fixed by sizing every row against a declared `HudContentWidth` (window minus
padding minus scrollbar), widening the HUD to 420, scaling its height to the
game window (360-720 instead of a fixed 480), giving the boon name and status
columns explicit widths, and turning the horizontal scrollbar off outright so
a future overflow shows up as wrapping rather than sideways dragging.

New boon: **Hunter's Eye** (passive) — a panel on the LEFT listing every
creature within 70m by distance, with a health bar, capped at 10 rows. It is
the GM mode's Tracking window earned as a boon. Pure observation, so there is
nothing to unwind when it is lost.

Watch for:
- Any row still clipping or scrolling sideways, at whatever UI scale you use.
- Held boon names all present now (that was the invisible-headline bug).
- Hunter's Eye: does the left panel collide with anything at your resolution?

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha22 — the Necks step is now about building

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

"Kill 4 Necks" is gone. Rather than drop a building step into its slot and
leave two of them back to back, the act was reordered so building, living and
resting run in sequence:

axe → hammer → workbench → 5 Boar → **Raise a roof (15 pieces)** → 6 Greylings
→ **Settle in (2 min at home)** → Sleep through the night → 3 Deer → Eikthyr.

**Settle in** measures `TimeInBase`, which Valheim accrues half a second at a
time and ONLY while `Player.IsSafeInHome` — a check that runs through
`GetBaseValue`, so it needs real comfort (roof and fire), not just walls. It
therefore cannot be completed before the roof step, which is why the roof moved
ahead of it rather than the new step landing in the Necks slot.

The Necks step's reward (wooden shield + 20 flint arrows) moved to Settle in.

Watch for: whether "Settle in" ticks up at all once you have a fire and a roof
— if the counter sits at 0/120 while you are clearly at home, report it and I
will switch the measure. Two minutes should pass while you are crafting anyway;
say so if it turns into standing around.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha23 — tracker polish, and the HUD stands aside

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

All four from Martin's alpha21/22 play-test:

1. **The tracker no longer sits on the top-left readout.** It moved down the
   left edge (30% of window height), clear of health/stamina/food.
2. **Each species has its own colour**, keyed on `Character.m_name` (the shared
   localization token, so level stars and "(Clone)" don't split a species).
   The colour is what the eye tracks when rows re-sort by distance.
3. **The bar is now distance, not health** — full at the edge of the eye's
   reach, draining to empty as something arrives, so it reads as a countdown.
   Health moved to a percentage at the end of the row.
4. **The run HUD and tracker hide while the inventory/crafting window or the
   map is open** (`InventoryGui.IsVisible` / `Minimap.IsOpen`, both public
   statics). The timer strip stays: it is a thin line along the top edge that
   nothing else uses. Standing aside beats moving — there is no spot free of
   both the crafting panel and the game's own readouts.

Watch for: the HUD coming BACK when you close the crafting window (if it ever
stays hidden, that is the important bug here, and `End` still toggles it);
species colours holding steady as things move; and whether the tracker's new
position clashes with anything at your resolution.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha24 — five fixes from the alpha23 play-test

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

1. **The HUD slides left instead of hiding** when the crafting window or map
   opens, so both stay readable AND the HUD's buttons stay clickable (hiding
   put reroll/abandon out of reach). The shift is a config number,
   `runHudMenuOffset`, default 470 — edit the JSON, restart, no rebuild, since
   the right value depends on resolution and UI scale.
2. **Mining tasks are Tier 2 now**, i.e. after the first boss. They were Tier 1,
   and MaxTier is `defeatedBosses + 1`, so Tier 1 is drawable from minute one —
   asking for mining before the antler pickaxe exists.
3. **Necks are gone from the random pool too.** The questline step went in
   alpha22; this was the other place they were still being asked for.
4. **Hunter's Eye is baseline, not a boon.** Always on, out of the offer pool,
   and listed in the HUD's BOONS section as "always on" alongside Pugilist.
5. **The tracker moved to the BOTTOM-LEFT**, off the hotbar and the top-left
   readouts. It is a draggable window and keeps a dragged position until the
   game window changes size — so if the corner is still wrong, drag it and tell
   me where you put it.

Watch for: the HUD landing somewhere sensible with the crafting window open at
your resolution (report the overlap and I will change the one number), and the
tracker not clashing with the hotbar.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-20 — TASK: alpha25 — smaller roof, food instead of timber

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha25**.

1. **The roof step wants 6 pieces, not 15.**
2. **The map hides the run window again**; the crafting window still gets the
   slide-left treatment. Nothing on the HUD is worth reading over the map, and
   unlike the crafting bench there is nothing to click there either.
3. **Boss kills now pay FOOD for the tier just cleared** instead of timber and
   stone, and the questline's material rewards are cut roughly to a third
   (axe 50→25 wood, hammer 100→40, roof 100→40). Those numbers were set while
   the 3x resource rate was silently inert, so they were compensating for a bug
   rather than balancing a reward.

Boss food by bosses felled: Eikthyr → cooked meat + honey; Elder → sausages +
carrot soup; Bonemass → turnip stew + serpent stew; Moder → wolf skewers +
onion soup; Yagluth → lox pie + blood pudding.

Watch for: **every one of those food names is unverified** (Unity asset data,
invisible from the assembly). A wrong one logs an error in GrantItem and grants
nothing — the run is unharmed, but tell me which line appears. Eikthyr's tier
is the only one you will reach soon; the rest can wait for a longer run.

Also worth a verdict: 6 pieces — still busywork, or about right?

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha26 — fire, bed, chest; the door task waits for a door

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha26**.

Act I is 13 steps now, up from 10. The three new ones are homestead beats, each
sitting immediately before the step that already secretly needed it:

```
 5 Raise a roof (6)     →   6 BUILD A FIRE   →   7 Kill 6 Greylings
 8 Settle in (2 min)    →   9 BUILD A BED    →  10 Sleep through the night
                          11 BUILD A CHEST   →  12 Hunt 3 Deer  →  13 Eikthyr
```

"Settle in" never accrued without a fire and "Sleep" never worked without a bed
— those prerequisites were real all along, just invisible. Now they are steps.

1. **Do the three new steps detect correctly?** They look for a piece *you*
   built carrying a `Fireplace` / `Bed` / `Container` component, within 20m of
   you, checked once a second. A campfire, any bed, any chest should each tick
   its step within about a second of being placed. If one sits at 0/1 while the
   thing is plainly standing in front of you, say which — and try walking closer
   before you call it (20m is `runBuildScanRadius` in the config, hot-editable).

2. **"Open 8 doors" should not appear until you have built a door.** It used to
   be drawable from minute one, when you have no hammer. Build a door, and it
   becomes eligible for the next refill — it will not appear instantly, since it
   still has to win a random draw.

3. **Two new item names, both unverified** (asset data, invisible from the
   assembly): `Flint` and `Resin`, in the fire and bed rewards. A wrong one logs
   loudly in `GrantItem` and grants nothing. Tell me if either line appears.
   `DeerHide`, `Wood` and `ArrowFlint` in those same rewards are already proven.

4. **Does the bed step strand you?** It lands at #9 but deer hide normally comes
   at #12, so the fire step at #6 hands over 6 hides to cover it. If the bed
   still sends you hunting, the hide count is wrong.

5. **Act I now pays 13 heat instead of 10** — enemies hit ~1.65× instead of
   ~1.50× by the time you reach Eikthyr, before random tasks. This is the first
   build where that curve is worth an opinion. Does the boss fight feel
   meaningfully hotter, or is it lost in the noise?

Known and accepted, not a bug: start a run standing in a base you already built
and the fire/bed/chest steps complete immediately, rewards and all. The mode is
built for a fresh start.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha27 — tracker colours, cooking, Windfall, name validator

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha27**.

**1. Check the log FIRST, before playing.** This build validates every creature,
item and reward name against the game's own registries at run start. Start a run,
then search the log for `[ICanShowYouTheWorld] Unknown`. Anything it prints is a
real bug I can fix in one line — **please paste those lines back verbatim**. It
should finally settle `ShieldWood`, `CookedMeat`, `Flint`, `Resin`, `RawMeat`,
`$item_cookedmeat` and all nine boss foods without you having to reach them.
Silence means every name in the pool resolves.

**2. Tracker colours.** Boar and deer both read white because the hash put them
on the same slot — and greyling, neck, greydwarf, troll and crow were ALL sharing
one colour. Palette is 10 now and no two species on screen can share. Expected
oddity: a species can change colour when another walks into range and takes the
slot it wanted. Tell me if that reads as worse than the collision did.

**3. Act I is 14 steps** — a cooking station after the fire (step 7), paying 8
raw meat to cook on it.

**4. Windfall** — new boon, **Keypad 8**, one charge, never refills. Doubles
every stack you carry. Two things to judge:
   - Does it fire at all, and does overflow drop at your feet rather than vanish?
   - **Is it too strong?** It doubles food too, and food is your health and
     stamina bar. Filling your pack with cooked meals and then pressing it is the
     obvious exploit. You picked everything-stackable over materials-only knowing
     that — this is the run where you find out. One word changes it.

**5. New multi-objective quests**: Hearth and Home, Provisions, Fill the Larder,
Meadow Cull, Night Watch. Provisions and Fill the Larder only appear once you own
a cooking station.

**6. Act I heat floor is 14 now** (was 10 two builds ago). Enemies hit ~1.70x by
Eikthyr before any random task.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha28 — the saga gets acts

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha28**.

Straight from your verdict. "Act 1 complete and then nothing" was literally what
the code did — a hardcoded string, and no Act II behind it.

**1. The quest panel is now headed with the act**: `ACT I — THE MEADOWS`. When
Eikthyr falls you should get a banner announcement — **ACT II — THE BLACK
FOREST** — and the questline should immediately seat its first step (mine the
Black Forest). That transition is the single most important thing to confirm.

**2. Act II is written in full**, seven steps: mine → build a smelter → forge
three bronze things → 10 greydwarves → 3 brutes → a troll → The Elder. The troll
step hands over the three Ancient Seeds his altar wants, so you never farm for
them — same principle as the deer trophies.

**3. Acts III, IV and V exist but are THIN** — three or four steps each. That is
on purpose: enough that no boss is a dead end, not enough to be a real design. If
you get that far, tell me what those acts should actually be about.

**4. Boon list says "Activated"** instead of "Keypad 8" overflowing the column.
The key is still on the strip above as `[8]`.

**5. If you resume your existing run and Eikthyr is already dead, it will jump
straight into Act II.** That is correct — the act is derived from the world, not
the save — but it will look abrupt the first time. Not a bug.

**6. Check the log again for `[ICanShowYouTheWorld] Unknown`.** Act II–V added a
lot of new names (`gd_king`, `Draugr`, `Blob`, `Hatchling`, `Dragon`, `Lox`,
`GoblinBrute`, plus every new reward item). The validator checks ALL acts at run
start, so one launch in Act I tells us whether Act V is sound. This is the single
most useful thing you can paste back.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha29 — the acts get filled in, and boats behave

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha29**.

Acts III–V were placeholders in alpha28. Now every act is 7–9 real steps with a
building beat and mob beats of its own.

**1. `[ICanShowYouTheWorld] Unknown` in the log — still the first thing to check,
and this build has the most new names yet.** Every creature and item across all
five acts is validated at run start. `Leech`, `StoneGolem`, `Fenring`,
`Deathsquito`, `Windmill`, `Thistle`, `SurtlingCore`, `MeadFrostResist`,
`ArrowNeedle`, `CapeLox` and about twenty more are unproven. One launch in Act I
checks all of them. Paste back anything it names.

**2. Each act now opens with an arrival step** — "Reach the Black Forest",
"Reach the Swamp" and so on. Should tick the moment you set foot in the biome.

**3. Boats: you should NEVER see a boat quest unless water is genuinely in play.**
They are pool-only and gated on having been on the Ocean biome *and* owning a
boat. If a boat quest shows up on a world where you have never sailed, that is a
bug worth reporting. If you sail a lot and never see one, tell me that too — the
Ocean gate is deliberately conservative and may be too tight.

**4. Build steps per act**: smelter + portal (Act II), fermenter (Act III),
windmill (Act V). **The Mountains have no build step on purpose** — Valheim has no
distinctively mountain-built piece with its own class, and I would rather admit
that than invent filler. A category can only be used by one act, because the
built-piece latch runs for the whole run.

**5. The fermenter step is a hint, not decoration** — poison resistance mead is
the Bonemass fight, so Act III makes you build the thing that brews it.

**6. Act II now has a portal step.** Combined with the stash (still unbuilt),
watch whether travel stops mattering entirely — that would be too much.

**7. Total questline heat is now 44** across the saga — roughly **×3.2 enemy
damage by the Plains** before any random task. That is much steeper than anything
played so far. If the Plains feel impossible rather than hard, the weights are
config and it is a number, not a rebuild.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha30 — Eikthyr's Herd, and the stash

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha30**.

The two things deferred twice so the acts got played first.

**1. The stash — press `End`, it is the top section.** "Deposit materials" empties
every unequipped **material** into it (not food, not arrows, not gear — those
stay on you deliberately, a button that stashed your dinner mid-fight would be a
trap). "Take" per row pulls a kind back. It follows you anywhere, survives a
logout, and is meant to make moving house between acts painless. Things to judge:
   - Is materials-only the right filter, or do you want food/arrows in there too?
   - Does it survive a suspend/resume with contents intact?

**2. Deer in Act I are Eikthyr's now.** Roughly half the deer you meet get a star
— visibly bigger, several times the health, faster. One arrow will not do it any
more. **Deer still cannot hurt you** — they run the game's passive animal AI,
which has no attack at all, and giving them one is asset work I cannot do. So the
hunt got harder to *catch* rather than dangerous.

**3. Killing a deer may draw greylings** to the carcass (about 1 in 3). That is
where the danger comes from. If it fires too often and hunting becomes a chore,
`runDeerGreylingChance` is config.

**4. There may be lightning** when a deer dies — pure flavour, and the one thing
here riding an unverifiable prefab name. It tries several candidates and stays
silent if none exist, so **no lightning is not a bug**, just an unlucky guess.
Tell me if you see it, and tell me if the log says "no lightning effect prefab
resolved".

**5. New Act I step 14: "Hunt Eikthyr's Herald"** — a named two-star deer that
spawns near you when that step comes up, between the deer hunt and Eikthyr. It
should announce itself. **Killing an ordinary deer must NOT complete it** — it is
matched by identity, not by species. If a normal deer finishes that step, that is
a real bug. If the Herald never appears, also a bug: it is meant to re-spawn
whenever the step is current and none is standing.

**6. Act I is 15 steps now**, questline heat 45 across the saga.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha31 — the stash gets its own window

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha31**.

Small one, straight from your note that the stash cluttered the Run window.

**The stash is now its own panel, bottom-left, immediately right of the tracker.**
It is up whenever a run is, it is draggable, and the list **scrolls** — it can hold
128 kinds, so it had to. The header and the "Deposit materials" button stay put
outside the scroll, so the one control you always want never scrolls away.

The Run window is back to what it was before alpha30: timer, heat, score,
questline, splits, tasks, boons.

Worth a look: at your resolution, does the stash window sit clear of both the
tracker and the HUD? Both windows remember where you drag them until the game
window resizes.

Everything from alpha26-30 is still in this build and still unplayed — the log
grep for `[ICanShowYouTheWorld] Unknown` remains the highest-value minute you can
spend.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-22 — TASK: alpha32 — two questlines, and heat becomes a dial

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha32**.

Your idea, and your line about it — *"the good thing about dual paths is that you
can decide if you want the heat"* — turned out to be the whole design, so I built
it that way.

**1. The quest panel now shows TWO rows: HUNT and CRAFT.** They advance
independently. Every act's kills are on one, everything else on the other:

```
ACT I — THE MEADOWS
  HUNT   Hunt 5 Boar          2/5
  CRAFT  Build a cooking station   0/1
         Reward: Meat to cook on it
```

**2. This is the difficulty dial.** Every questline step pays heat, so working
both tracks makes you stronger AND hotter; running the hunt track straight to the
boss keeps you cool, poorer and lower-scoring. **That is the thing to judge in
play: does that trade feel like a real decision, or is one path obviously right?**

**3. The act still ends when the boss dies** — the boss is the last hunt step. If
your craft track is unfinished when the boss falls, those steps are gone. That is
the cost of rushing and it is intentional; tell me if it feels punitive rather
than like a choice.

**4. Act IV's craft track is only two steps** (arrive, mine silver). The Mountains
have no distinctive building, so that act just offers less optional heat. Say if
it reads as thin rather than as a lull.

**5. Your existing save will migrate.** It has one questline position from before
the split; it gets looked up across both tracks and seats whichever owns it, with
the other starting at that act's beginning. The log says
`Migrated a pre-track save: '<step>' resumed on the <TRACK> track`. **If you see
that line, check the other track looks sane rather than half-done.**

**6. Watch the flashes.** Each row flashes gold on its OWN advance. If completing
a kill also flashes the craft row, that's a bug.

### RESULTS (Windows side appends here)

**"Craft an axe didn't register."** Confirmed and fixed in alpha33 — see below.
**Do not play alpha32**; the entire CRAFT track was stalled at its first step.

---

## 2026-08-22 — TASK: alpha33 — fixes the dead CRAFT track

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha33**.

**alpha32 was broken and this is the fix. Skip alpha32.**

You found it on the first step: "Craft an axe" never registered. It was not just
that step — **ten steps across the saga could never register**, and all of them
were on the CRAFT track:

```
Act I    mq-axe, mq-hammer, mq-bench, mq-shelter, mq-home, mq-rest
Act II   bf-copper, bf-bronze
Act III  sw-iron
Act IV   mt-silver
```

What happened: those steps measure a lifetime player stat as a DELTA, so each
needs a "zero point" snapshot taken when it's dealt. When I split one questline
into two in alpha32, I updated the code that *reads* those stats to walk both
tracks, but not the code that *takes the snapshot* — it still only did the first
track, which is HUNT. So every CRAFT step of that kind sat un-baselined and was
silently skipped forever. The build/arrive steps were fine, which is why the track
looked alive rather than obviously dead.

The real cause was two copies of "walk the actives and the questlines" that drifted
apart. They're now one shared enumeration, so they cannot disagree again.

**Also added: this class of failure is now loud.** If a stat-based questline step
ever sits without its zero point, the log says so and names the step:

```
Questline step 'mq-axe' on the CRAFT track has no stat baseline —
it can never register progress. This is a bug in the mod, not the save.
```

**What to check:** start a run, craft an axe, and watch the CRAFT row tick to 1/1.
Then just play — every step that was dead should now count. And the usual grep,
which will now also catch this if I ever do it again.

### RESULTS (Windows side appends here)

**Confirmed working.** Axe registered; the `Unknown` grep came back empty, which
closed the asset-name blocker for all five acts in one launch.

---

## 2026-08-22 — TASK: alpha34 — the boon pool gets teeth

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha34**.

Straight from your note that the stamina boons felt lacklustre. You said three —
it was **five** (Enduring, Vigorous, Cat's Breath, Marathoner, Acrobat), all
competing with a baseline that already gives stamina ×0.5 cost and ×2.5 regen.
That's why they felt flat.

**1. They're now one boon: `Tireless`** — max stamina, faster recovery, cheaper
dodges. The four freed slots went on new categories. Pool is 22.

**2. Ten new boons, four categories the pool had none of:**

| | |
|---|---|
| **Irongut / Coldblooded / Fire-blooded** | Resistant to poison / frost / fire |
| **Bloodthirst / Relentless** | Kills heal you / restore stamina |
| **Glass Cannon** | +40% damage, **−30% max health** |
| **Reckless** | +50% damage, **you take 25% more** |
| **Slow Burn** | Heat rises 25% slower |
| **Forge-fed** | Weapons hit harder the hotter the run |

**3. Boons can now have downsides** — Glass Cannon and Reckless. Both say so in
their description. Tell me if a cost ever surprises you; that would be a bug in
the wording, not the boon.

**4. Resistances won't be offered early** — Irongut needs 1 boss down, the other
two need 2. Frost resistance in the Meadows would waste one of your three options.
**If you ever see a resistance offered in Act I, that's a bug.**

**5. Forge-fed is the one to watch.** Its damage moves with heat, which nothing
else in the mode does. It should get stronger as the run heats up and weaker after
a death drops your heat. If it ever feels like it ratchets up and never comes
down, say so — that's the failure mode I designed against.

**6. Sharpened now also covers weapons you craft AFTER taking it.** It didn't
before; that was a quiet flaw found while making three damage boons coexist.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha35 — your four Act I/II notes, and a bug they surfaced

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha35**.
(The installer now prints the version it actually installed, so that line is
always right from here on.)

All four of your notes, plus something they exposed.

**1. Bed before "settle in".** You were right — the CRAFT track really did ask you
to settle into a home you had no bed for. Now `…fire → cook → bed → settle in →
sleep → chest`.

**2. Homeward.** Every boss kill grants a charge; **Keypad 9** returns you to your
claimed bed. Charges accumulate and persist. Waystone (Keypad 6) got you *to* the
altar; this is the leg home, which was the one gap left after the stash removed
the hauling. If you have no bed claimed it refuses and says so rather than
spending the charge — **tell me if it ever strands you somewhere unexpected.**

**3. The smelter was worse than "too soon".** It needs **surtling cores**, which
come from burial chambers, and the chain didn't hand any over until the *portal*
step — two steps later. So it was quietly sending you crypt-hunting. The mining
step before it now pays 8 cores, so the smelter is buildable the moment it's
asked for.

**4. Every completion now pays +2 max health**, quest step or random task alike,
shown in the HUD as "+N health earned". Act I should reach Eikthyr around +40.
It's a loan like everything else — it goes away when the run ends.

**Armor isn't in it, and can't be:** Valheim computes armor from equipped items,
and the only damage-modifier steps it has are far too coarse for "a tiny bit". So
you got the health half, done properly, rather than a fudge.

**5. What your request surfaced:** `Hearty` (+15 health) and `Glass Cannon`
(−7.5 health) **both** write the same field, and the old mechanism gave each its
own idea of "the original". Hold both and the second records the first's boosted
value as pristine — then whichever ends first restores a number that was never
original, leaving you permanently altered *after the run*. That shipped in
alpha34. Adding a third claimant would have made it worse, so it's rebuilt: one
pristine value per field, everything recomputed from it. Third time today I've
had to make that same correction, so it's now a tested class of its own.

**Worth checking in play:** take Hearty and Glass Cannon together, finish a run,
and confirm your health is back to normal afterwards.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha36 — acts have to be earned now

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha36**.

Your note: *"you just get handed every step without any work. Is it possible to
add a discovery step before a mini boss and boss?"*

**1. Every act now ends: … → FIND THE ALTAR → kill the boss.** You have to
actually reach the boss's altar (within 30m) before the kill step appears. The
world always generates one, so it can never be unfindable — but you do have to go
looking.

Two things worth knowing:
   - **Waystone skips it.** It teleports you to the next undefeated altar, which
     completes the discovery. That's deliberate; spending a boon to skip travel
     was your own call.
   - Each discovery step pays that boss's summoning items and the right mead, so
     arriving is when you get handed what the fight needs.

**2. The Herald is a real hunt now.** It used to spawn 24m away — you turned round
and it was there. Now it's **150–250m out**, and you get a direction: announced on
spawn, and a live "Tracks lead north-east, 140m" line under the step. Hunter's Eye
picks it up at 70m for the last stretch.

**If the bearing ever stops updating or points somewhere wrong, that's a bug** —
it should go null rather than go stale.

**3. Quest hints.** Steps whose requirements aren't obvious now carry a line saying
what they need — 18 of them. Both times you lost time in play were "I didn't know
what this needed", so:

```
Build a smelter
  ▓▓░░░░  0/1
  Stone, and surtling cores from the burial chambers.
  Reward: More ore than it can hold
```

**Tell me if any hint is wrong.** I wrote them from knowledge of the game, not
from anything the assembly could confirm — a wrong hint is worse than none.

**4. The quest panel is getting tall** — two tracks, each up to five lines. I'd
rather you see it than have me trim it blind. If it crowds the timer, say so.

**5. Heat is 50 across the saga now** (was 45).

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha37 — the altar pins, corrected

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha37**.

You asked: *"will the altar be visible on the map though?"* It was — **all five**,
pinned at run start, since long before discovery steps existed. Which made
alpha36's discovery step a walk to a dot you were handed in minute one, i.e.
exactly the "handed without any work" it was meant to fix.

**Now only the current act's altar is pinned**, appearing as that act begins. Kill
Eikthyr and the Elder's altar shows up with the ACT II banner.

Pinning nothing at all was the other option and I rejected it: vanilla hands out
Vegvisirs precisely because searching a biome blind is miserable, and a Plains
altar can be a very long way from where its act starts.

**What to check:** at run start you should see ONE boss pin, not five. After each
boss falls, the next act's altar should appear within a second or so.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha38 — it's a saga, and now it says so

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha38**.

Your call: *"less of a speed-run-mod and more of a more complete Valheim
experience… let's lean into the saga mode."*

Worth knowing the scoring already agreed with you before either of us said it:
score is `par/(par+time) × (1 + heat×0.1)`, so heat MULTIPLIES and time only
divides. A 3-hour thorough saga scores about double a 1-hour thin one. The mode
stopped rewarding speed a while back; only the presentation still said otherwise.

**1. The HUD leads with the ACT, not the clock:**

```
SAGA — ACT I
THE MEADOWS
  2:14:07              Heat 8.5
  Saga score 310
  +40 health earned    Homeward x2 [9]
  QUESTS
    HUNT   …
    CRAFT  …
```

The act line also used to appear twice; now once.

**2. "Begin the saga" / "Abandon the saga"**, and the messages say saga too.

**3. Nothing about balance changed** — same formula, same numbers. This is
presentation catching up with what the mode already was, so there's nothing new to
play-test beyond "does it read right".

**What I want your eye on:** with the timer demoted, does the HUD still tell you
what you need at a glance mid-fight? The clock is now the same size as heat.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha39 — the Herald was never where the bearing said

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha39**.

You weren't failing to find it. **There were dozens of them.**

alpha36 moved the Herald's spawn to 150–250m, which put it outside the loaded area
— so the game culled it and released its (non-persistent) record, which made the
mod think none was standing, which made it **spawn another one, once a second, at
a new random spot.** The bearing only appeared in the instants where a freshly
spawned one still existed. That's your "sometimes I get hints".

**Three fixes:**

**1. The run now remembers a PLACE, not a creature.** When the hunt begins it picks
a spot 150–250m out and keeps it. The Herald only materialises when you get within
60m of that spot. No respawn loop, and the ground never moves under you — including
across a save and resume.

**2. The bearing is on the always-on strip now**, not just inside the run window —
*"The Herald's tracks lead north-east, 140m"*, updating as you walk. Previously it
only drew inside the quest panel, so while actually playing there was nothing to
follow. That's the "more frequent hints" you asked for, as a standing line rather
than a message every thirty seconds.

**3. The boss marker is gated on the hunt.** Your request: no Eikthyr pin until the
Herald is dead. Generalised — **an act's altar is pinned when its discovery step
becomes current**, so the map is never ahead of the questline. At run start there
should now be **no boss pin at all**.

The vanilla rune stone near spawn still works if you read it. That's you choosing
to skip the mystery, and I left it alone deliberately — destroying a world object
is the one thing this mode can't give back.

**What to check:** start the Herald step, walk the bearing, and confirm you find
exactly ONE Herald where it said. If you ever see two, or the bearing jumps
somewhere new, the fix didn't take.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha40 — Eikthyr was on the wrong questline

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha40**.

**1. You were right about "Kill Eikthyr" under CRAFT.** What you saw was *"Find
Eikthyr's altar"* sitting at the end of the crafting questline — the discovery step
was on the wrong track.

The split routes steps by their KIND (kills → HUNT, everything else → CRAFT), and a
discovery step isn't a kill, so it quietly fell into CRAFT. I designed it as "HUNT
track, immediately before the boss" and the automatic routing put it elsewhere
without saying anything. Steps can now name their track explicitly, and the five
discovery steps do.

The validator also gained a check for this exact shape — the old one only asked
"does HUNT end on the boss", which stayed true while the step before it went
missing. An invariant that only looks at the last item can't see an absent one.

**2. The bottom-left panels moved right**, clear of the health/food readout.
`runSidePanelX` in the config (default 320) if it's still not right for your
resolution — both panels are draggable too, and remember where you put them until
the window resizes.

**What to check:** the CRAFT track should now end at "Build a chest", and HUNT
should read `… → Hunt the Herald → Find Eikthyr's altar → Defeat Eikthyr`.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha41 — farming in the Black Forest

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha41**.

Your note: *"when entering black forest you start getting seeds. There should be
some FARMING quests. Plant seeds, tame boar."*

I guessed wrong when you asked — I thought planting would have no honest measure.
`Plant` turns out to be a compiled class, so a growing crop is detectable exactly
as a campfire is. No asset names anywhere in this.

**Act II's CRAFT track gains three steps**, after the portal — infrastructure
first, then settling in:

```
… → Build a portal → Plant a crop (10 seeds) → Tame a boar → Build a beehive
```

The plant step pays a **queen bee**, so the hive is buildable when it's asked for
— same lesson the smelter's surtling cores taught.

**The interesting bit:** "plant 10 seeds" needed the build detector to learn to
COUNT rather than just answer "have you built one". It counts what's near you
(20m), so **keep the crop in one plot** — a field spread across two bases would
never read as ten at once. Harvesting a finished crop doesn't take the step back.

**What to check:**
- Does the plant counter climb as you plant? `0/10 → 10/10`
- Does taming a boar tick immediately when it finishes calming?
- New item names to watch in the log grep: `CarrotSeeds`, `QueenBee`, `Carrot`.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — TASK: alpha42 — Valheim: The Saga, on the loading screen

`git pull` → full `.\Install-Mod.ps1` → popup **v0.221.12-run.alpha42**.

**1. Loading screens are branded** — while a saga is live or one is waiting to
resume:

```
            VALHEIM: THE SAGA
           Act II — The Black Forest
```

Typographic rather than a picture, deliberately: nothing has to be drawn, nothing
extra ships beside the DLL, and it can name the act you're loading into — which a
static image never could. If you'd rather have real artwork later, the plumbing to
swap the actual loading image exists (`Texture2D.LoadImage`); it just needs someone
to make the art, which isn't me.

**A vanilla world on a vanilla save still looks completely vanilla.** The branding
only appears when the mode is actually in play, same rule as everything else here.

**2. The game's own loading tips gained seven saga lines** — heat being a choice,
the stash following you, the Herald's tracks being on the strip, and so on. Each is
something the mode does that you could reasonably not know.

**3. Panels moved back left** — 320 overshot ("TOO far right"). Now 190, which
should clear the health bar without stranding them mid-screen. `runSidePanelX` if
it's still not right, and both windows are draggable.

### RESULTS (Windows side appends here)

*(pending)*

---

## 2026-08-23 — BUILD: alpha42.2 — the version, in-game

`git pull` → `.\Install-Mod.ps1 -ModOnly` → the installer prints the version; the
popup and the places below should all agree.

**First build using the new numbering.** `alpha<N>.<BUILD>` — N for a mechanic or
content change worth a brief, BUILD for a fix or a nudge. Every deployed build
still gets a unique version, because the popup is the only proof of what you're
running; "small change, skip the bump" is never the answer.

**The version now shows in three places you'd actually look:**
- the lobby header, beside **VALHEIM: THE SAGA**
- the run HUD header, beside the act
- under the loading-screen title card

It used to exist only in the Credits popup at activation, which is exactly when
you're not wondering — whereas mid-run, after four builds in an afternoon, is.

Also caught while adding the numbering: the installer's version regex would have
matched `alpha42` out of `alpha42.1` and printed a version that doesn't exist.
Fixed and verified against the real DLL.

### RESULTS (Windows side appends here)

*(pending)*
