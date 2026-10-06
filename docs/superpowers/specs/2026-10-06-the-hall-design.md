# The hall: rebuilding Valheim, and calling down the gods at Fader

Status: designed with the owner on 2026-10-06, section by section; this document awaits the owner's
review. Build after the owner's home test of `1.0.17-run.2026-10-06`, one slice at a time, each slice
played and adjusted before the next.

## 1. Why

The owner, 2026-10-06:

> since this is a building game, if we could make some quests around rebuilding Valheim (a section for
> each act) until the Ashlands act, where we complete the build and can call down the gods we killed to
> help us against Fader.

This joins three older threads:

- **GitHub #17**, "build the halls of Valhalla, with guidance". A hall the player builds; the saga says
  what is missing and the player solves it; a verdict at the end.
- **Decision #4 (2026-09-20)**. Empty high seats by default, and a god who comes only if earned. The path
  with every seat empty must be complete and winnable on its own.
- **Using what the game ships.** Asked the same morning, with a mix of three ways in: a part in the story,
  a use in play, and a big moment. The catalogue (`docs/catalog/`) found the cast for the finale already
  in the game: seven Aspects of the earlier bosses.

**Shape (owner's choice): the hall is the spine.** Every act has a HALL questline. The underused cast
attaches to the hall where it fits, and the rest of the catalogue stays on a backlog (section 7).

## 2. Decisions taken (owner, 2026-10-06)

- **The hall stands where the player lives.** One hall at home, growing around the first long fire.
- **Guidance, not blueprints.** The saga says what a section needs; the player decides how it looks.
- **A god's place is raised in the act after his death.** His trophy, hung on an item stand beside a
  seat. A trophy exists only once its god is dead, and that death ends his act, so each section from
  Act II on opens with the previous god's place. A place raised later, any time before Fader, still
  counts.
- **The hall replaces the sacrificial stones** (owner, 2026-10-06: "it would be kind of interesting if we
  DIDN'T use the stones to hang the boss trophies but something else that we construct"). A god's place in
  the hall is where his power is claimed (section 4a). The vanilla stones at the spawn are no part of the
  saga.
- **The call happens only at Fader.** Bosses II–VI stay as hard as they are now.
- **One horn, all at once.** One blow during the fight brings down every god whose place was raised.

## 3. The sections

Each section uses what its act's biome unlocks. Pieces are counted by what they are made of (section 4),
so any shape counts.

| Act | Section | What the saga asks for | Opens with the place (and power) of |
|---|---|---|---|
| I | **The long fire** | A floor of some length; a roof that closes over a long fire. Act I's HEARTH questline becomes this, with its existing beats and rewards kept. | (none: no god has fallen) |
| II | **The frame** | Posts and walls of core wood (`RoundLog`); a door. | **Eikthyr**: his antlers beside a seat |
| III | **Iron and light** | Iron-bound beams (`Iron`); sconces. The **Bog Witch's candle wicks** (she sells `CandleWick`) light the hall. | **The Elder** |
| IV | **Stone** | Stone walls and pillars (`Stone`, from the stonecutter); rugs. | **Bonemass**, on the **Stone Throne** (stone and wolf pelt) |
| V | **The roof and the feast** | A shingle roof (`Tar`); the **Long Heavy Table**; banners. Act V's STEADING feast at home becomes the hall's feast, with **Hildir's fireworks**. | **Moder** |
| VI | **Black marble** | Columns and floor (`BlackMarble`); dvergr light. | **Yagluth**, on the **Black Marble Throne** |
| VII | **The finished hall** | Grausten and ashwood (`Grausten`, `Blackwood`); the **Bone Throne** at the head of the hall, the player's own seat. Then Odin's verdict and the horn (section 5). | **The Queen** |

The named thrones are suggestions the hint offers. Any seat counts.

Fader is the enemy, not a guest. Six gods can be called: Eikthyr, the Elder, Bonemass, Moder, Yagluth and
the Queen.

**When the hall is finished** (the last beat before the horn):

- **Odin's verdict.** The game's own cloaked figure (`odin`, which appears and vanishes in vanilla) stands
  in the hall, looks, and goes, without a word. The myth already says he "is not a god who explains
  himself".
- **The horn** is forged next (section 5).

## 4. How a section is checked

This reuses what Act I's HEARTH questline does. Pieces are recognised by compiled component classes or by
their own recipe, never by asset names.

- **The anchor.** The fire built for Act I's first hall step becomes the hall's centre.
  - Only pieces the player built count (`Piece.GetCreator`), within about 25 m of the anchor (config).
  - If the anchor is destroyed, the next fire built inside a roofed space among the hall's pieces takes
    over.
  - Persisted with the run state, per world.
- **Measures:**
  - **Roof over the fire:** the game's own shelter test, the one behind "Rested" (`Cover`).
  - **Floor length:** the hall's longest horizontal extent, measured from its pieces.
  - **Material:** a count of hall pieces whose `Piece.m_resources` include that act's material. The
    material names are item names, so they join the self-check.
  - **A god's place:** an `ItemStand` holding that god's trophy, within about 3 m of a seat (`Chair`).
    Each place needs its own seat: one seat cannot serve two trophies.
  - **Comfort, doors, light:** `GetComfortLevel`, `Door`, and light sources by class.
- **Guidance.** A step shows what is still missing ("it has no roof", "nowhere to sit beneath the Elder's
  head", "12 of 20 stone pieces"). Hugin says it once, when the step opens.
- **What is earned stays earned.** A god's place, once seen, is latched in the run state, and the call
  reads the latched set, not what is standing on the day. A place can latch at any time before Fader, so
  a late one counts. The act's step for it is the prompt, not the deadline.
- **Pieces already standing count**, as in the HEARTH questline. A second run on the same world inherits
  its hall; a fresh world starts from nothing.
- **Questlines.** Act I's HEARTH becomes HALL. Acts II–VII each gain a HALL questline. Act V's STEADING
  feast moves into the hall's section. An unfinished section carries into the next act, as the
  HEARTH questline already does.

## 4a. A god's place is where his power is claimed

Every act from II to VII already has a "Claim X's power" step (`bf-power`, `sw-power`, `mt-power`,
`pl-power`, `mi-power`, `as-power`). Each sits in the act after its god's death, which is exactly where
that god's place now goes. **The two become one beat: "Raise Eikthyr's place in your hall."**

- **Raising the place grants the power.** When a god's place latches (section 4), the saga gives the
  player that god's Forsaken power with the game's own `Player.SetGuardianPower`, and the step completes.
  The step stops measuring the `SetGuardianPower` stat, which the vanilla stones also increment.
- **Switching powers happens at home.** Looking at a raised place shows "[Shift + E] Take up <god>'s
  power", the same hover-and-key pattern as the helm's fitting card (`Shipwright`). The hall becomes where
  the player chooses which god walks with them.
- **The stones are left alone, not broken.** A power taken at the spawn's stones still works, as in
  vanilla, but it raises no place, so that god does not come at Fader. The saga never sends the player
  there, and the step's hint names the hall. Blocking the stones would need a hook the patcher does not
  have, for nothing a player would miss.
- **Trophies stay on the stands**, as they stay on the stones in vanilla, so the hall shows every god the
  player has felled.

## 5. The horn and the call

**The cast.** The game's Aspects, named in its own English text:

| God | Aspect | Prefab | HP |
|---|---|---|---|
| Eikthyr | Aspect of the Lightning Stag | `Aspect_Eikthyr` | 3,000 |
| The Elder | Aspect of the Living Forest | `Aspect_Elder` | 1,600 |
| Bonemass | Aspect of the Writhing Dead | `Aspect_Bonemass` | 1,600 |
| Moder | Aspect of the Dragon Mother | `Aspect_Moder` | 1,500 |
| Yagluth | Aspect of the Twisted Soul | `Aspect_Yagluth` | 1,700 |
| The Queen | Aspect of the Crawling Matriarch | `Aspect_SeekerQueen` | 1,700 |

(`Aspect_Fader`, the Emerald Flame, also exists. Fader is the enemy, so it is not called.)

**The horn.**

- It is the last step of Act VII's section, so it exists only once the hall is finished.
- It is forged at the **Storm-Anvil**, in the rain as the anvil requires, from **Eikthyr's hard antler**
  (`HardAntler`) and **flametal** (`FlametalNew`): the first god's gift and the last act's metal. This uses
  the anvil's existing combine (`Incinerator.m_conversions`) and gives Act I's Obliterator its second great
  job.
- It is a saga item like Thor's bow: an existing model with new numbers, picked from the catalogue.
- It is blown with **`↓`**, the one free arrow key. The Wind-horn is `↑`: up calls the wind, down calls
  the gods down.

**The call.**

- Only during a fight with Fader: a live Fader within range. Anywhere else, Hugin says the horn is for
  one fight.
- One blow summons, in a ring around the player, the Aspect of every god whose place is latched.
  - Each arrives speaking that god's own after-death line from the game (`deadspeak_*`). This keeps the
    bible's rule that gods speak only once defeated.
  - They fight for about a minute, then fade.
- **One blow per attempt.** If Fader kills the player and is summoned again, the horn can be blown again.
- The duration, the Aspects' damage, and the call's range are config numbers, so the fight is tuned in
  play without a rebuild.
- **No places raised:** the horn still exists and the blow brings nobody. Hugin says the high seats are
  empty. Fader is fought alone, and the saga stays winnable (decision #4).

**The risk, tested first** (slice 0, section 8). The Aspects are in the `Boss` faction, the same as Fader,
so by default they would not fight him.

- The plan is to turn them the way the game's own `Skeleton_Friendly` is turned: a player-side faction,
  tamed. The test reads that prefab first.
- They were built for the Frozen King's scripted second form (there is an `aspect_aoe_explosion`), so
  they may attack once and vanish rather than stay and fight.
- **Fallbacks, in order:**
  1. The gods' own creature prefabs, turned. Decision #4 warned they "can look ridiculous".
  2. The **Fallen Warriors** of the Ancestral Memorial: the game's named Norse dead, with duel lines like
     "You are worthy". They are humanoid and easier to turn.
  3. No creatures: the blow grants every seated god's power at once, for a minute.

## 6. Who else comes to the hall

- **The Bog Witch** (Act III): her candle wicks light the hall, which takes her beyond her one act.
- **Hildir from Act II.** Her first chest quest, the Smouldering Tomb with its boss Brenna in the Black
  Forest, is unused. Meeting her there gives her three acts (II, IV and V). It is an optional questline,
  like her other two, and her third chest (Act V) unlocks the fireworks for the feast.
- **Haldor**, as a trader voice: one line per act about the hall's state ("A hall without a door is a barn
  with ideas"). His shop stays his, and his talk is restored afterwards.
- **Odin**: the verdict (section 3).
- **The trophies** are the gods' places. **The Obliterator** forges the horn.
- **Munin stays out** (`munin:false`, unchanged).

## 7. Backlog: decide with the Deep North

Not designed here, because each depends on whether the Deep North is an epilogue after Fader (decision #3)
or Act VIII, which the code currently has.

- **The Valkyrie.** The game's own ending: `Valkyrie_End`, "Journey to Valhalla", with Hugin's and Munin's
  farewells. Whether she comes for the living one after Fader, and whether they go, is a story decision.
- **The Frozen King's mirror.** In his second form he summons the same Aspects against the player. It is
  the natural sequel to the horn, if the Deep North becomes the epilogue.
- **The rest of the catalogue**, to be pulled in act by act when it helps: Hervor's journal, the frozen
  ships of earlier settlers, the captives of Mörkhalla, the hidden White Deer, the traders' companions
  (Halstein, Blåbär and Hallon, Kvastur), and the Fallen Warriors beyond the fallback.

## 8. Build order and testing

Each slice is played before the next, gets a `saga/` milestone tag, and may change what comes after it.

0. **The Aspect test.** A dev-only key calls one chosen Aspect to the player's side, near any boss. It
   answers: does it fight for the player, does it target the boss, how long does it live, does it look
   right. Its answer chooses the finale's cast (section 5's fallbacks).
1. **Act I's section.** HEARTH becomes HALL: the anchor, the roof over the long fire, the floor's length.
   This proves the checking (section 4).
2. **Acts II–VI**, one slice each: the section, the previous god's place and power, and the cast that comes with it
   (Hildir from Act II, the witch's wicks in Act III, Haldor's lines throughout).
3. **Act VII**: the finished hall, the Queen's place, the Bone Throne, Odin's verdict, the horn.
4. **The call at Fader.**

**Testing:**

- **Pure and unit-tested** (in `Tests/`), given a list of pieces:
  - which qualities a section meets
  - which trophy goes with which seat
  - how much of the act's material is present
  - when a place latches
- **The self-check** gains every new guessed name: the six Aspects, the six trophies, the six guardian
  powers (`GP_Eikthyr`, `GP_TheElder`, `GP_Bonemass`, `GP_Moder`, `GP_Yagluth`, `GP_Queen`, from the
  catalogue; `Player.SetGuardianPower(string)` confirmed in 1.0.17's IL), `HardAntler`,
  `FlametalNew`, the material items, `CandleWick`, the `odin` prefab, the horn's model, and
  `Skeleton_Friendly` as the turning example.
- **The existing dev key** that completes a step lets any section be reached quickly. A dev key for the
  call (slice 0's, kept) tests the finale without the whole saga.

## 9. Rules this must keep

- **Ravens never help.** Odin only witnesses; the horn is the player's own work.
- **Gods speak only once defeated.** The Aspects speak the dead gods' own lines.
- **No spoilers.** A section's text names only gods already felled, and the BOOK shows a section only once
  its act has begun. Act I's hall never says how many seats there will be, or for whom.
- **The path with empty seats is complete** (decision #4).
- **Traders' shops stay theirs**, and their talk is restored after.
- **Anything that outlives its act** asks `RecipeStepDone`, never `StepDone` (a 2026-10-05 lesson). This
  applies to the latched places and to the horn's step.
- **Act I's light economy is unchanged.** HEARTH's beats and rewards carry over into HALL as they are
  (decision #1).
