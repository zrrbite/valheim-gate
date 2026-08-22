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

None of these break the run you are in — nothing is disabled on a miss. They just
mean some piece of content quietly does not work.

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
