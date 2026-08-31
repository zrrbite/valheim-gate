# Checking the log after a run

A one-page guide for the Windows box. Written for a human, not for Claude.

Since alpha27 the mod checks **every** creature, item, reward and biome name it
uses against Valheim's own registries when a run starts, and complains in the log
about any it cannot find. This matters because those names are Unity asset data:
they cannot be verified when the mod is built, and a wrong one **fails silently** —
a kill counter that never moves, a quest that is never offered, a reward that
never arrives. Before this check existed, the only way to find one was to play
until something felt stuck, which cost several builds.

So: one minute here saves an evening.

---

## Do this

1. Start Valheim, open the **Credits** menu (that's what loads the mod).
2. Check the version popup says the tag you just installed.
3. **Start a run** — the check runs at run start, not at game start.
4. Alt-tab out and run the command below in PowerShell.

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld.*Unknown"
```

**No output is the good result.** It means every name in all five acts resolved.

---

## The fish check (alpha49 and later)

**You can skip this one.** It is groundwork, not a check — nothing today depends
on it, and it will not tell you anything is wrong.

The mod prints what the world's fish weigh, because weights and species lists
are asset data that the compiled assembly cannot see:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "Fish available"
```

You get one line listing every edible fish and its weight:

```
[ICanShowYouTheWorld] Fish available: Fish1(2), Fish2(2), ..., FishRaw(0.5)
```

This section used to say the line "sets the *Land a big one (4.0+)* and *A
varied catch (3 species)* thresholds". **Those steps do not exist.** They were
planned, the measuring and the validation for them were both built, and the
steps themselves were never written — so the line has had nothing to feed since
the day it was added. Every fishing step in the game counts fish rather than
weighing them: `FishHeld` at 1, 5 and 8, `CookedFishHeld` at 3 and 5, and
fishing skill 10. All reachable, none affected by this.

It is still worth having, because the numbers are the answer to a question
somebody will ask. **The heaviest fish in the game weighs 2.0**, so a "big one"
step must be at or under that, and there are fifteen edible fish prefabs, so a
species count has plenty of room. If those steps ever get written, that is where
their numbers come from — and the validator will complain in plain numbers if
one asks for more than the world has.

(Through alpha88 the line also listed `HelmetFishingHat(3)`, a hat, as the
heaviest fish. That would have certified an unreachable 3.0 threshold as fine.
Fixed in alpha89.)

### Everything from a new build in one command

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld.*(Fish available|Unknown|wants|weighs|species)"
```

Catches the fish list, any misspelled reward name, and any impossible
threshold. If only the fish line comes back, everything resolved.

---

## If something prints

Each line names exactly what is broken. Paste the whole line back — every one of
these is a one-line fix on the Mac side.

| Line | What it means |
|---|---|
| `Unknown CREATURE names` | A kill quest can never progress. Its counter will sit at 0 forever. |
| `Unknown ITEM names` | A collect quest can never progress. |
| `Unknown REWARD prefabs` | That reward will not be granted when its step completes. |
| `Unknown BUILD categories` | A typo in our own vocabulary — the quest is never dealt at all. |
| `Unknown BIOME names` | An act's *arrival* step can never complete, which stalls the act at its first beat. |
| `Unknown LOCATION names` | A *find the …* step can never complete, and everything behind it on that track is unreachable. The same line lists every name ZoneSystem really has — paste it and it is a one-word fix. |

None of these break the run you are in — nothing is disabled on a miss. They just
mean some piece of content quietly does not work.

---

## The creature probe (alpha83 and later)

**Not in the log.** `PageUp` writes its own file, beside the config:

```powershell
notepad "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\ICSYTW_probe.txt"
```

It dumps what the creature you are looking at is actually made of: the transform
hierarchy, every renderer and material, and each unique shader's full property
table with the live values. That is the groundwork for giving the saga's named
creatures a look of their own — the shader slots and whether their textures are
CPU-readable decide which approaches are open, and none of it can be read from
the compiled game.

- Needs `RunDevMode` **and** a live run. No toast at all means one of those is off.
- Targets the creature nearest your crosshair within 40m.
- **Appends.** Probe a plain greydwarf first for untouched values, then a branded
  one — by then the mod has written to its materials, so the second reading shows
  our changes rather than stock ones. Send the whole file.

---

## Also worth grabbing

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld.*(Migrated|Duplicate|does not end on|is used by both|no lightning)"
```

| Line | What it means |
|---|---|
| `Migrated a pre-track save` | Your old save's questline position was moved onto one of the two new tracks. Worth a glance at the other track to check it looks sane rather than half-done. |
| `Duplicate questline step ids` | Two quest steps share an id. A resume could seat the wrong one. Send it. |
| `hunt track does not end on its boss` | An act cannot end. Send it. |
| `Build category ... is used by both` | A later act's build step will complete instantly for free. Send it. |
| `No lightning effect prefab resolved` | Cosmetic only — deer deaths just won't flash. Not a bug, but tell me and I'll try other names. |

---

## Everything the mod said

If you would rather not filter, this dumps all of it:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld"
```

To put it in a file you can attach or paste:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\Player.log" -Pattern "ICanShowYouTheWorld" |
    ForEach-Object { $_.Line } |
    Set-Content "$env:USERPROFILE\Desktop\icsytw-log.txt"
```

---

## Notes

- **The log resets each time Valheim starts.** If you want the lines from a
  session, grab them before relaunching.
- `Player.log` is the *current* session; `Player-prev.log` in the same folder is
  the one before, if you relaunched too quickly.
- The run's own save state, if it ever needs inspecting, is
  `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICSYTW_run_*.json` — one file
  per character.
- Full folder:
  `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\` — paste that into Explorer's
  address bar to get there.

---

## On the Mac

Everything above applies, but the log lives somewhere else than the config —
Windows keeps them in one folder, macOS does not.

| What | Where |
|---|---|
| Log | `~/Library/Logs/IronGate/Valheim/Player.log` |
| Config, run saves, probe file | `~/Library/Application Support/IronGate/Valheim/` |

There is a `Player.log` symlink in the config folder pointing at the real one, so
either path works for reading.

```bash
grep ICanShowYouTheWorld ~/Library/Logs/IronGate/Valheim/Player.log
open ~/Library/Application\ Support/IronGate/Valheim/ICSYTW_probe.txt
```
