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
- Run state: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICSYTW_run_*.json`
- End every commit with the Co-Authored-By trailer your session uses.

---

## 2026-08-18 — TASK: collect evidence for the resume/freeze bug (report only, fix nothing)

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

*(pending)*
