# Home test plan: everything from 2026-10-05 and 2026-10-06

For the owner, at home on the Windows machine. It covers every build from `1.0.16-run.2026-10-05`
to `1.0.17-run.2026-10-06f`: about 85 commits, **none of which has been played**. It is in the order
that tests the most with the least: setup, then a new character through Act I, then act by act.

Each item says **do**, then what you should **see**. When something does not match, note the item
number and paste the log lines it names. The story as it should unfold is `docs/SAGA-WALKTHROUGH.md`.
What got built and why is in [`2026-10-06-overview.md`](2026-10-06-overview.md).

**Going back.** Every major build has a tag: `git tag -l 'saga/*' -n1`. `saga/before-2026-10-05` is the
last build you played, and undoes all of it. To install one: `git checkout <tag>`, then
`.\dist\windows\Install-Mod.ps1 -ModOnly`.

---

## 0. Setup (5 minutes)

1. **Install.** `git pull`, then **`.\dist\windows\Install-Mod.ps1` without `-ModOnly`**. Steam moved the
   game to 1.0.17, which replaced the patched game file, so the full install has to re-patch it. If it
   refuses with a version mismatch, Steam has not finished updating: let it finish and run it again.
2. **Config** (`%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICanShowYouTheWorld.json`):
   - `"runDevMode": true` gives the shortcuts. `mod` means Shift, Ctrl or Alt held:
     - `mod` + `Keypad +` completes the current step.
     - `Delete` slays everything hostile within 10 m.
     - `Home` teleports to the map cursor.
     - `Keypad *` puts materials and 500 coins in the stash.
     - `Keypad Enter` takes you home.

     The full table is `dist/windows/DEV-MODE.md`.
   - Nothing is needed to reach Acts VI and VII: since `...06d` the saga ends at **Fader** by default. If
     your config still has `"runFinalBossKey": "defeated_goblinking"` from before, delete that line.
   - Leave `runKeyLayout` out: Windows keeps the numpad.
3. **Launch.** The main menu's version line has a gold **`SAGA v1.0.17-run.2026-10-08 · DEV`** under it.
   Anything else means the wrong build is installed.

## 1. The first minute of a run (10 minutes)

4. **Self-check.** Start a run on any world, then:
   `Select-String "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "Saga self-check"`.
   **See:** the header line, then MISSING and FALLBACK lines first.
   - **Expected:** 0 MISSING, and 1 FALLBACK (`The saga's myth: no tale yet for What the Cold Keeps`,
     because the Deep North's story is a stand-in).
   - **New since the Mac's first reading:**
     - `Haldor's troll head: TrophyFrostTroll, chance 0.5 per kill` is OK. It was MISSING: a real Act II
       stall, fixed in `...06c`.
     - `Act I's god` … `Act VIII's god`: each `'<creature>' sets '<key>'`, all OK. Act VIII reads
       `'FrozenKing_p3' sets 'defeated_frozenking_p3'`.
     - `The gods' altars: all 8 resolve`.
     - `Keys (numpad layout): all 33 resolve` (31 plus the two temporary MacBook keys).
     - `Sea creatures: all 6 resolve` (`...08`), and `The ward's spark: fx_chainlightning_hit` (`...08d`). A
       FALLBACK on the spark names the effect used instead; none at all means the Ward strikes unseen.
     - `Way effects` and `The Smiðr's watch-post`, both OK, in the build with the class balance (8n).
     - `Quest items can be got: all N resolve` (`...06d`). Every item a quest asks for is something the game
       drops, crafts, sells, mines or converts, or the saga grants. **A MISSING line here names the item and
       who asks for it: paste it back**, since it is either a step that can never finish or a source this
       check does not know yet. Its first reading on the Mac (`...06h`) found one of each, both fixed in
       `...06j`: Act I's "Cook 5 meat" asked for an item that doesn't exist, and the saga's own items were
       misread as lost.

   **Copy the whole block** for the next session.
5. **No errors.** `Select-String Player.log -Pattern "ICanShowYouTheWorld.*Exception"` returns nothing.

## 2. A new character, through Act I (30–60 minutes)

Make a **new character** for this part: three of these checks only happen on a first spawn.

6. **The menu waits for the intro** (`...06b`). On first spawn, the Valkyrie flies you in.
   **See:** no saga menu during the flight; it appears once you land.
7. **The FORGE page keeps its secrets** (`...05n`). Start the saga, `End`, open FORGE.
   **See:** "Nothing is known yet". No Stormward, Ironbound Stormward or Last Light anywhere. Thor's bow
   appears only once crafting it is your step.
8. **Clicking a choice** (`...06b`). When a boon offer comes up, press **TAB**.
   **See:** each boon has a **Choose** button; click one and it is taken. With TAB closed, the buttons
   are greyed out and swinging a weapon never picks anything. Keypad 1–3 work as before.
8b. **A boon offer waits** (`...08e`). Let an offer sit for 45 s without choosing.
   **See:**
   - the card steps aside, and a gold `Boon offer waits · End` appears right of the strip;
   - your actives' keys work again;
   - **End** brings the card back with the Run window, and End again steps it aside;
   - finish a second task while one waits: the card comes back saying "one more after this", and picking
     deals the next at once;
   - quit and reload with one waiting: the flag is still there (the three choices may differ).
8d. **A run resumes after a full restart** (`...08g`). Mid-run, quit Valheim completely, start it again, and
   load the character. **See:** "Run resumed", and the Herald's bearing pointing where it did. Before `...08g`
   this deleted the run (`Failed to resume run` in `Player.log`); a failure now says so on screen and keeps
   `ICSYTW_run_<name>.failed-<time>.json` beside the config.
8e. **Shepherd is one star** (`...08i`). As a Hunter in the Meadows, summon the wolves (`U`/`[7]`).
   **See:** one star on each (a wolf's 80 health doubled), not the old near-immortal 5000; a wild boar still
   dies to a few hits and bites as before; the hearth boar shows one star while you hold Shepherd, and loses it
   when the way is laid down or the run ends. As a Völva, Hearthlight in `...08i` mends 4 every five seconds (before
   it, it only pulsed when heat changed); the class-balance build mends every second (8g).

**8f to 8n are for the build that carries the class balance** (`docs/superpowers/plans/2026-10-08-class-balance.md`).
It is built on the branch but not in a build yet, so they wait for it. Dev keys make them quick: `mod` + `Keypad *`
takes the next way (Hunter, Völva, Berserker, Húskarl, Skald, Sæfari, Smiðr, then none; laptop `mod` + `Z`),
`mod` + `Keypad /` learns every rung (laptop `mod` + `0`), and `mod` + `Keypad +` completes the step (laptop
`mod` + `P`). A way's three rungs are `[7]`, `[0]` and `[Ins]` (laptop `U`, `I`, `O`).

8f. **The Hunter's pack** (class balance). As a Hunter in the Meadows, press `U`/`[7]`, then press it again
   after each step below.
   **See:**
   - the first press brings both wolves at once, one star each (8e), and the key cools for 240 s: pressed during
     that, it says "Packbrother not ready.";
   - after the 240 s, with both standing, it says "Your pack is already with you." and does not cool down;
   - strike one wolf down yourself (or let it fall in a fight): the next press brings back that one, and the one
     still standing stays. Two stand, never more;
   - `I`/`[0]` (Menagerie) lends a boar, a hen or a chicken, and nothing else, and cools for 90 s after each lend:
     press it each time it is ready (each press trades the beast back and rolls again), and no wolf comes before
     Bonemass falls. A Lox joins the roll after Moder, an Asksvin after the Queen;
   - summons and Menagerie together never pass four;
   - after Eikthyr falls, a new wolf has two stars, the most there is.

   Bonemass tempers it: Packbrother calls three, so three wolves stand (the first press after Bonemass brings the
   third).

8g. **The Völva's aura** (class balance). As a Völva, get hurt, then stand still.
   **See:**
   - no green number over your head, at any time (Hearthlight used to print one);
   - the health bar climbs about 3 a second wherever you stand, so about 30 in ten seconds. No hearth is needed.
     A hurt wolf beside you climbs too;
   - after Eikthyr it is 4 a second, one more for each god, to 8;
   - `U`/`[7]` raises two skeletons from the choice. They follow and fight, with no star in the Meadows. The key
     then cools for 120 s; after it, with both standing, it says "Your dead already stand with you." and does not
     cool down, and once one has fallen the next press raises only that one;
   - Mending is `I`/`[0]` now (after Eikthyr), and Thor's Wrath `O`/`[Ins]` (after the Elder). Neither is on `[7]`.

   Bonemass tempers it: Bonecaller raises three.

8h. **The Berserker's Fury** (class balance). As a Berserker, swing at greylings one after another, then stop.
   **See:**
   - the damage numbers over them climb with each blow that lands, about 5% a blow, to half again at ten;
   - from the fifth blow, each blow also heals you a little, a tenth of the damage it does;
   - stand still: a second after the last blow it fades one blow a second, and is gone within about ten;
   - `U`/`[7]` (Rend) sweeps around you, and every foe it hits counts as a blow;
   - `I`/`[0]` (Blood Rage, after Eikthyr) fills Fury at once and holds it full for 15 s. You take a quarter more
     while it lasts. It is no longer a x1.5 of its own;
   - `O`/`[Ins]` (Warcry, after the Elder) staggers every foe within 8 m, but not the gods, and fills half of
     Fury;
   - the BOONS page says "your weapon bonuses are at their ceiling (x2.5)" once the bonuses reach it. Sharpened,
     Glass Cannon and Reckless together already do.

   **Your call:** with those held, does Fury still feel like it matters? It has no meter on screen. Would you
   want one?

   Bonemass tempers it: Rend reaches half again as far (7.5 m).

8i. **The Húskarl's Guard** (class balance). As a Húskarl, raise your shield against a greyling. Once, try to raise
   it just as the blow lands (a parry). Compare with a block you made before taking up the way.
   **See:**
   - a block costs half the stamina, and your health steps up by 4;
   - a parry steps it up by 10;
   - `I`/`[0]` (Shield Wall, after Eikthyr): for its 20 s both are doubled, to 8 and 20;
   - `U`/`[7]` (Shield Bash) and `O`/`[Ins]` (Last Stand) work as before.

   **The Guard is a status effect the mod defines itself, so this is its first proof in the game.** If health
   never moves on a block, see 8n.

   Bonemass tempers it: Shield Bash reaches 6 m.

8j. **The Skald's song** (class balance). As a Skald, learn every rung (`mod` + `Keypad /`, laptop `mod` + `0`),
   then press `I`, `O` and `U` in that order (`[0]`, `[Ins]`, `[7]`).
   **See:**
   - the Marching Song is already playing from the choice, with no key pressed: +20% speed, and breath back half
     again as fast. Pressing `U`/`[7]` now, before any switch, only says "That song is already sung.";
   - `I`/`[0]` switches to the War Song and says "The song swells.": the first switch always swells, the new song
     at double strength for 5 s;
   - `O`/`[Ins]` within 20 s switches to the Saga of Bragi and says "The Saga of Bragi.": no swell, only the name;
   - `U`/`[7]` within 20 s of the swell switches back and says "The Marching Song.";
   - a song stays until you switch: no timer, no cooldown on the key. Pressing the one already sung says "That
     song is already sung.";
   - 20 s or more after the swell, a switch says "The song swells." again;
   - the War Song adds a quarter to your damage and to your tames' within 15 m; Bragi mends you and them by 3 a
     second, one more for each god, to 6.

   Bonemass tempers it: the Marching Song is +30%.

8k. **The Sæfari's Ward on foot** (class balance). As a Sæfari, stay ashore and let a greyling notice you.
   **See:**
   - lightning strikes it within 20 m, every 3 s, with a spark where it lands. This is Ward I, and you bought
     nothing. A grazing deer or a calm boar is never struck;
   - `U`/`[7]` (Undertow, from the choice) throws everything within 6 m back, staggered and wet. With nobody
     there it says "Nothing within the wave's reach.";
   - `I`/`[0]` (Stormcaller, after Eikthyr): for 20 s the Ward strikes every second, twice as far;
   - `O`/`[Ins]` is Sea Legs (after the Elder).

   Then take a ship (`mod` + `Keypad .`, laptop `mod` + `B`; checks 36 and 41). **See:**
   - Sail I and Hull I cost 25 (50 for anyone else), the Wind-horn 100 (200). The third tier of Sail, Hull and
     Ward is never offered, because her ship sails a tier above what is fitted;
   - swimming does not move the stamina bar;
   - aboard, Undertow also bursts from the ship.

   Bonemass tempers it: Undertow reaches 9 m.

   **Your call:** her Ward works on land before the raven has said anything about the sea. Does that read as an
   early reveal?

8l. **The Smiðr's watch-post** (class balance). As a Smiðr on open ground, press `U`/`[7]`, and stay out of its
   line.
   **See:**
   - a loaded ballista rises about 3 m ahead of you. It shoots greylings and deer, and never you or your tames. A
     stray bolt can still hit anyone;
   - it shoots every non-player that is not tamed: deer, boars, and a boar you are still taming. **Your call:** is
     that wanted?
   - raised inside a hall, it stands on the floor, not on the roof;
   - it stays until it is destroyed or you raise another: the second press (after the 120 s cooldown) takes the
     first down;
   - on a ship or over open water it says "No footing for a ballista here.";
   - quit to the menu and load again: it is gone;
   - what you wear and hold never wears: the durability bar does not drop;
   - `I`/`[0]` (Field Forge) and `O`/`[Ins]` (Master's Minute) work as before.

   **Walls, on a THROWAWAY piece away from your base.** Build a beam out past what its support allows, near you:
   it stands. Walk 25 m off: it falls. **Do not try this on the base.** What you build past the limits stands only
   while you are near, and the card now says so.

   Bonemass tempers it: two watch-posts at once.

8m. **A tempering shows only when its god is next** (class balance). Open the BOONS page (`End`, then BOONS).
   **See:**
   - with only Eikthyr down, no tempering line at all;
   - with the Elder down, "after Bonemass: ..." under your way;
   - when Bonemass falls, the line turns green and reads "tempered: ...", and the number it names is the new one;
   - Moder, Yagluth and the Queen do the same for the second rung, the third rung and the engine.

   The gods have to fall for real here. The step-skip (`mod` + `Keypad +`, laptop `mod` + `P`) moves the quest,
   not the world's keys, and `mod` + `Keypad /` (laptop `mod` + `0`) teaches rungs, not tempering. To fell a god:
   use the step-skip to reach the altar's step, `↓` to hop to the altar, summon or find the god, then press bare
   `Delete` within 10 m (the same key in both layouts; on a MacBook `fn` + `Backspace`). It kills through the
   game's own damage, so the world's defeat key is set, as in check 12.

   The god count is the WORLD's keys, not the run's. To test tempering from zero gods, use a fresh world.

8n. **The ways' log lines** (class balance). Start a run, then
   `Select-String Player.log -Pattern "Saga self-check"`.
   **See:** "Way effects" and "The Smiðr's watch-post", both OK.
   Then `Select-String Player.log -Pattern "Guard on|Tide-borne on|Guard failed|Tide-borne failed"`.
   **See:** "Guard on: SE '...' (True)" when a Húskarl takes up the way or resumes it, and "Tide-borne on: SE '...'
   (True)" for a Sæfari, once per launch. A "(False)", a "Guard failed" or a "Tide-borne failed" is a bug: paste it
   back.

8c. **The Gatherer stays ashore** (`...08f`). With its step open at night, sail out to open water.
   **See:** if it is out, "The Gatherer will not follow you onto the sea." and it is gone; nothing comes
   while you are afloat. Land at night: "Something heavy is coming through the trees", over land.
9. **Typing is safe** (`...06b`). With an ability ready, open chat and type a line containing a number.
   **See:** nothing fires.
10. **Act I's speakers can't be killed by monsters** (`...05n`). The shade, Thjalfi and the thane stay
    standing while greylings hit them.
11. **Choose a way at the thane.** Its first ability is on `[7]`.
12. **Abilities are taught where the god falls** (`...05c`). Kill Eikthyr (dev: `Delete` within 10 m).
    **See:** within a second, Hugin and a centre line say "The way of the X deepens: <ability> [0]",
    and `[0]` works at once. There is no walk back to the thane. The Skills window's way skill reads
    33, then 66.
    **Your call:** is having all three abilities by the Elder too early?
13. **The saga so far** (`...05o`). After Eikthyr, chat says "The skalds have written 'The Stolen
    Light'". `End`, then BOOK, then **Read your saga so far**.
    **See:** the browser opens Act I's chapter with your deeds under it, then "the rest is not written
    yet". Nothing of Act II.
14. **Quit to the menu right after a boss kill, and resume.** **See:** nothing is taught or announced
    twice.

## 3. Act by act (as far as you get; dev keys make it quick)

**Act II, the barrow-keeper and Haldor** (`...05e`, fixed in `...06c`)

15. After "Take cores from the dead", a grey ghost with a green glow stands at a burial chamber's
    door, with a map pin. Use: his speech, and "Speak with the barrow-keeper" completes.
16. Later, "Carry a light down to him": rob a courier at night and bring its light. His hover shows
    0/1, then 1/1; Use, and the **Stormsworn helm** appears at the forge.
17. **Haldor.** "Find the trader", then "Bring Haldor a troll's trophy". Kill a troll, put its "Troll
    Trophy" on the hotbar, look at him and press its key.
    **See:** his reveal line, the trophy taken, and **the Elder's altar pin appears**. This is the step
    `...06c` fixed.
18. **The pin must NOT appear earlier**, not while you are still looking for the chambers or the trader.
19. Abandon the run and talk to Haldor: his ordinary lines are back.

**Act III, the drowned one and the Bog Witch** (`...05f`)

20. At the nearest sunken crypt: a pale draugr who doesn't attack, with a pin. Use: "Speak with the
    drowned one" completes, and the **Stormsworn cuirass** is at the improved forge.
21. "Let him go": he turns hostile at about a third of his health. Kill him: a light rises at night
    where he fell, and he does **not** come back as a skeleton. Killing any other draugr must not count.
22. **The Bog Witch.** Walk up to her and "Find the Bog Witch" completes. Plain Use opens her shop. Her
    hover shows a second line, "Shift + Use — Reforge the Stormward". With the Stormward, 10 iron and
    10 ancient bark, Shift+Use gives the **Ironbound Stormward**. If that hover line is missing, say so:
    it is the riskiest mechanism in this act.

**Act IV, the frozen one and Hildir** (`...05g`)

23. At the treeline nearest home: a frost-white ghost with an amber glow, with a pin. Use before a fire:
    nothing. Build and light a fire within about 5 m, then Use: "Wake the frozen one" completes.
24. "Carry an egg down to him": bring a dragon egg (heavy, and it can't go through a portal). His hover
    goes from 0/1 to 1/1; Use: the **Stormsworn greaves** are at the forge.
25. **Hildir** (the optional PEAK questline): walk up to her and "Find Hildir" completes. Bring her
    Howling Cavern chest: "Bring back her chest" completes. **Moder must not wait for this.**
    Paste `Select-String Player.log -Pattern "Hildir accepts"`.

**Act V, the harvester and the feast** (`...05h`)

26. A faded-gold ghost by the nearest stone ring, with a pin. Use: his speech.
27. "Eat from the field": eat three foods at once, one of them barley bread, lox pie or fish wraps, then
    Use on him. The **Stormsworn mantle** is at the artisan table.
28. **STEADING** (optional): the windmill, Hildir's Sealed Tower chest, and "A feast at your own table":
    at home, three foods with one from the plains. **Yagluth must not wait for it.**

**Act VI, the lantern-keeper** (`...05i`; reached by default since `...06d`)

29. A dvergr with a lantern glow at a dvergr site in the Mistlands. Speak. At night, give him a rescued
    light or a caught wisp: a light rises above him and fades, and "Set a light free" completes. It
    can't be picked up again (`...05m`).
30. **The Borrowed Light** at a galdr table (5 wisps, 12 silver): it clears the mist and lights the area
    while held.

**Act VII, the charred one and Last Light** (`...05k`, `...05m`)

31. Once you stand on **dry** Ashlands ground near where you landed: a burned man with an ember glow,
    who doesn't attack, with a pin.
32. Build a fire beside him. With a bow, 10 flametal and 3 lights (rescued or wisps), Use and choose
    "Let it burn": **Last Light** is in the pack. It strikes like Thor's bow, cycles elements for a
    Hunter, and always adds some fire.

**Through every act**

33. **Storm shields mend at the anvil only** (`...05j`). A worn Stormward or Ironbound **alone** in the
    Storm-Anvil's box, lever pulled in rain or thunder: it comes back at full durability. With anything
    else in the box it is refused, never turned to coal. Workbenches can't repair them.
34. **Speakers can't be killed by monsters, in any act** (`...05m`). The drowned one is the exception:
    you kill him, at the end.
35. **A Hunter's fire or frost kills count** toward "Thunder at range" (`...05n`).

## 4. Boats (20 minutes; dev makes it quick)

36. **Fittings** (`...05p`). Build a raft; the raven says the helm takes gold. `Keypad *` puts 500 coins
    in the stash: take them out. Look at the helm: **Shift+E** opens the card, and again closes it (`...08c`).
    **See:** Sail I and Hull I, 50 coins each. Keypad 1–2 (or TAB and **Buy**) buys.
    - Sail should feel faster under sail **and** oars.
    - Walk the deck after buying: no "Player over board" in the log.
    - `End`, FORGE, SHIP lists what is fitted.
37. **The god's wind** (`...05q`). With the act's god pinned on the map, sail with the prow toward it.
    **See:** the wind swings behind you, a "The god's wind" icon shows, and the raven speaks once. Turn
    90° away and the real wind returns within a second or two.
38. **The Wind-horn** (`...05q`). Buy it at the helm (200 coins), then **↑** at sea.
    **See:** two minutes of wind at your back in any direction, and the ability bar counting down ten
    minutes. On land it tells you to blow it at sea.
38b. **The sea answers the heat** (`...08`). With heat above 0 from Act II, sail open water for a few minutes.
    **See:**
    - the raven's line once, then a message per encounter;
    - a Serpent ahead, or flyers near a reached act's coast, starred as heat rises;
    - after the raven, the helm offers **Ward I** (100 coins); bought, it strikes what comes within 20 m, but
      never a grazing deer or a calm boar by the dock (`...08b`);
    - a troll drops 60-90 coins, and a sea creature drops coins;
    - `Select-String Player.log -Pattern "Sea:"` lists each encounter.

    Dev: Shift + Keypad `.` aboard calls one now.
39. **Your call:** do the fitting prices fit the gold you actually find? Is the 40° cone right? Are two
    minutes on and ten off right?

## 5. Winning, and the reward (`...05l`)

40. To try it quickly, set `"runFinalBossKey": "defeated_eikthyr"` and win after Eikthyr. Otherwise win
    for real: the default ending is Fader since `...06d`.
    **See:** "The skalds have finished your saga.", and the browser opens **The Saga of <your
    character>**: the myth up to your last act, your deeds under each, and a reckoning. Nothing from an
    act past the one you ended in. The lobby then shows **Read your saga**.
    Remove that `runFinalBossKey` line afterwards, so the saga ends at Fader again.

40b. **The charred one waits for his last line** (`...06f`). After winning at Fader, go back to the landing
    in the Ashlands where you first came ashore. There's no map pin: you have to find him.
    **See:** he is standing there, greets you with "Go home.", and when spoken to says "So that is where
    it all went…". Quit and reload: he's gone. If you haven't been back before reloading, he is still
    waiting there. `Select-String Player.log -Pattern "charred one stays|last line"`.

## 6. On the MacBook (whenever you have it)

41. The Mac already has `1.0.17-run.2026-10-08` and `"runKeyLayout": "laptop"` (the old config is
    beside it as `.bak-2026-10-06`).
    - **Choosing:** `J K L` choose on every card (`H U I O` for the fourth to seventh lines), or press
      TAB and click.
    - **Abilities:** your way's three are `U I O`; the others are `J K L Y P N`; Homeward is `9`.
    - **The HUD names every key**, so you don't need to learn them first.
    - **Dev keys:** `Shift` + `End` (`Shift` + `fn` + `→`) opens a window listing them all, in the laptop's
      keys. `DEV-MODE.md` has the same table.
    - `End` is fn + →; F1 is fn + F1.
    - **Go to (temporary, `...06g`):** `↓` hops to the next of the saga's places, Shift + `↓` back one.
      The message says where (`DEV: → Haldor's camp (2 of 5)`). With `0` (god mode) and fn + Backspace
      (slay within 10 m), most of the plan can be walked without a mouse.
    - **A ship (temporary, `...06h`, boarding fixed in `...06i`):** Shift + `B` builds a Karve on the
      nearest deep water and puts you at its helm (the rudder at the stern), taking you to the sea first if
      it's far. If it says to take the helm yourself: walk to the rudder, look at it, `E`. That covers checks 36–38 without building:
      `Z` for coins, take them from the stash, then Shift + E at the helm.

## What to bring back

- The **self-check block** (item 4), and any **exception** lines (item 5).
- The "Guard on" and "Tide-borne on" lines (8n).
- `Select-String Player.log -Pattern "Hildir accepts"` (item 25).
- For each item that didn't match: its **number**, and what you saw instead.
- Your calls on **12** (three abilities by the Elder?) and **39** (boat prices, the cone, the horn's timing), and on
  **8h** (does Fury still matter once the ceiling is reached?) and **8k** (is her Ward on land an early reveal?).
- Per way (8f to 8l): which engine felt too strong or too weak.
- Anything that felt wrong, even if it worked. That is half of what this test is for.

## Not in this test

- **The hall** (rebuilding Valheim), **the horn at Fader** and **Valheim Smith** are designed and specced,
  but not built. They start after this test, from what it brings back.
- **Act VIII** (the Deep North) has its real names but a stand-in story.
- **The heat curve** is still untuned.
