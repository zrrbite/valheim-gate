# Windows install kit

**Note for Claude running on the Windows box** (written from the Mac,
2026-08-16 — read this before doing anything).

SSH from the Mac to this machine isn't working yet, so the normal push-based
deploy (`Scripts/upload_windows.sh`) can't be used. This folder is the
fallback: everything needed to install the mod locally, pulled through git.

## Testing a new build

**[TEST-PLAN.md, and DEV-MODE.md for the testing shortcut keys](TEST-PLAN.md)** — what to check, ordered by what catches the most
for the least play. Start there after any install.

## After installing, check the log

**[CHECKING-THE-LOG.md](CHECKING-THE-LOG.md)** — one page, one command, one
minute. The mod validates every creature/item/reward name it uses when a run
starts; a wrong one fails silently and costs an evening of play to find. Worth
doing after any build that changed quest content.

## Just run this

From this folder, in PowerShell:

```powershell
.\Install-Mod.ps1
```

Then just start Valheim — since 2026-09-19 the mod loads itself at startup, and the
version popup appears at the main menu on its own. **The installer prints the exact
version the popup should read**, taken from the DLL it just installed, so it is right on
every build rather than whatever was current when this page was written.

**How to tell it loaded**, at a glance and at any time: the main menu's version line gains a
second line in gold —

```
Version 1.0.15 (n-40)
VALHEIM: THE SAGA  v1.0.15-run.2026-09-19
```

That is the standing answer. The popup says the same thing once and is then dismissed
forever, which is no use an hour later.

Opening the **Credits** menu still works and is now simply unnecessary. The move was not
cosmetic: a saga item is only known to the game while the mod is loaded, so loading a
character before visiting Credits used to drop Thor's bow out of the pack, permanently and
without a word.

**Picking up a mod change** (the common case — no Valheim update involved) only
needs the mod DLL, which is identical on every platform:

```powershell
git pull
.\Install-Mod.ps1 -ModOnly
```

A full run is only required after the *game* updates, since that overwrites
`assembly_valheim.dll` and the patch has to be redone.

Roll back at any time:

```powershell
.\Install-Mod.ps1 -Restore
```

If the Steam library isn't auto-detected, pass it explicitly:

```powershell
.\Install-Mod.ps1 -ManagedPath "D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed"
```

## What it does, and why it's built this way

1. Finds Valheim via the Steam registry keys and `libraryfolders.vdf`.
2. Backs up `assembly_valheim.dll` to `assembly_valheim.dll.vanilla` (once).
3. Runs the bundled `Patcher.exe` on **this machine's own** assembly.
4. Copies the patched assembly and `ICanShowYouTheWorld.dll` into `Managed\`.

**The assembly must be patched here, locally.** Per-platform game binaries
differ in real code, not just build metadata, so a patched assembly built on
the Mac or the Deck will not do — that's why this ships the patcher rather
than a ready-made DLL. Measured on 2026-08-16, both at 0.221.12: same 1078
types, but the macOS build carries an extra method
(`UpscaledFrameBuffer::GetScreenScaleFactor()`) and 193 more bytes of IL than
the Linux one.

**Patching always starts from the vanilla backup**, never from the installed
file. Re-patching an already-patched assembly would inject the mod's entry
point twice.

The patched assembly carries a marker naming the entry point
(`ICSYTW_EntryPoint_FejdStartup_Start`). `-ModOnly` refuses an assembly that lacks it,
because an older Patcher's injection passes every other check while loading the mod from
the wrong place.

The mod DLL itself is pure IL and identical on every platform, so the one in
`patcher\` is exactly what runs on the Deck and the Mac.

## Contents

| File | Purpose |
|---|---|
| `Install-Mod.ps1` | The installer |
| `patcher\Patcher.exe` | Mono.Cecil IL patcher (.NET Framework; 4.8 ships with Win11) |
| `patcher\Mono.Cecil*.dll` | Patcher dependencies |
| `patcher\ICanShowYouTheWorld.dll` | The mod — also the symbol source the patcher reads |

Built from the **`feature/run-mode`** branch (Run Mode preview). The installer
prints the version it installed and the popup should match it — nothing here
names a version, so nothing here can go stale. The game version the mod was
built against is the first part of that version (`1.0.12-run.2026-09-12` was
built against Valheim 1.0.12).

**The script refuses to install a mod built for a different game version** than
the one on disk — it reads both out of the DLLs. A game update is not something
the installer can fix: the mod has to be rebuilt against the new assembly, and
the update usually changes something the mod calls (1.0 changed five signatures,
added a `Hoverable` member, and moved Unity from 6000.0.61 to 6000.0.75). What
the rebuild involves is in the repo's `CLAUDE.md` under "Update Scenarios".

It also refreshes its vanilla backup whenever the installed assembly is
unpatched, which is what a Steam update leaves behind. Until 2026-09-12 the
backup was taken once and kept forever, so after the 1.0 update the script
patched the *old* game's assembly and installed it into the new game, which
then failed at startup with `TypeLoadException`s against the newer `Splatform`.
If you are ever in that state, Steam's *Verify integrity of game files* puts the
real vanilla back.

## Caveats

- A Steam game update overwrites `assembly_valheim.dll`; re-run the script
  afterwards. If the game *version* changed, rebuild the mod first — the script
  will tell you so and stop.
- The mod can be built on this Windows box too: Visual Studio 2022's MSBuild
  builds `ICanShowYouTheWorld\ICanShowYouTheWorld.csproj` directly (the Patcher
  project needs a NuGet restore, but the bundled `Patcher.exe` does the same
  job). The Mac shell scripts run under Git Bash, `Tests
un_tests.sh` excepted
  (it wants `mcs`/`mono`; Roslyn's `csc.exe` from the VS install compiles the
  same sources).
- These binaries are refreshed by hand from the Mac. If the mod source has
  moved on, they're stale — check the repo's latest tag.
- Once SSH works, prefer `Scripts/upload_windows.sh` from the Mac; see
  `SSH_SETUP.md`, which has an open section on why key auth is currently
  refused.
