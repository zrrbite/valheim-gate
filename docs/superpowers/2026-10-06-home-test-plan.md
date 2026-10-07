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
3. **Launch.** The main menu's version line has a gold **`SAGA v1.0.17-run.2026-10-07b · DEV`** under it.
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
    in the stash: take them out. Look at the helm: **Shift+E** opens the card.
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

41. The Mac already has `1.0.17-run.2026-10-07b` and `"runKeyLayout": "laptop"` (the old config is
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
- `Select-String Player.log -Pattern "Hildir accepts"` (item 25).
- For each item that didn't match: its **number**, and what you saw instead.
- Your calls on **12** (three abilities by the Elder?) and **39** (boat prices, the cone, the horn's timing).
- Anything that felt wrong, even if it worked. That is half of what this test is for.

## Not in this test

- **The hall** (rebuilding Valheim), **the horn at Fader** and **Valheim Smith** are designed and specced,
  but not built. They start after this test, from what it brings back.
- **Act VIII** (the Deep North) has its real names but a stand-in story.
- **The heat curve** is still untuned.
