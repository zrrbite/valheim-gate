# Windows install kit

**Note for Claude running on the Windows box** (written from the Mac,
2026-08-16 — read this before doing anything).

SSH from the Mac to this machine isn't working yet, so the normal push-based
deploy (`Scripts/upload_windows.sh`) can't be used. This folder is the
fallback: everything needed to install the mod locally, pulled through git.

## Just run this

From this folder, in PowerShell:

```powershell
.\Install-Mod.ps1
```

Then start Valheim and open the **Credits** menu — that's what activates the
mod. The popup should read **v0.221.12-1**.

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
point into `FejdStartup.OnCredits()` twice.

The mod DLL itself is pure IL and identical on every platform, so the one in
`patcher\` is exactly what runs on the Deck and the Mac.

## Contents

| File | Purpose |
|---|---|
| `Install-Mod.ps1` | The installer |
| `patcher\Patcher.exe` | Mono.Cecil IL patcher (.NET Framework; 4.8 ships with Win11) |
| `patcher\Mono.Cecil*.dll` | Patcher dependencies |
| `patcher\ICanShowYouTheWorld.dll` | The mod — also the symbol source the patcher reads |

Built from tag **`0.221.12-1`** against Valheim 0.221.12 / Unity 6000.0.61.
The script warns if this machine's Steam buildid differs from the tested one
(`21981559`); it will still patch correctly, but the mod DLL was compiled
against that version's API, so a large version gap wants a rebuild on the Mac.

## Caveats

- **Untested on Windows.** Written on macOS and never executed against a real
  Windows install — if something fails, the failure is worth reporting back
  rather than working around silently.
- A Steam game update overwrites `assembly_valheim.dll`; re-run the script
  afterwards.
- These binaries are refreshed by hand from the Mac. If the mod source has
  moved on, they're stale — check the repo's latest tag.
- Once SSH works, prefer `Scripts/upload_windows.sh` from the Mac; see
  `SSH_SETUP.md`, which has an open section on why key auth is currently
  refused.
