# SSH Setup for Deploy Targets

The MacBook is the build machine. It patches assemblies and pushes them to
every other machine, so each target needs an SSH server the Mac can reach with
key authentication. This guide covers enabling it on **SteamOS (Steam Deck)**
and **Windows 11**. macOS needs nothing — it deploys to itself with a local
copy (`Scripts/deploy_local.sh`).

| Target | Server | Deploy scripts | Notes |
|---|---|---|---|
| Steam Deck | `sshd` (preinstalled, disabled) | `download.sh`, `upload_hax.sh`, `upload_valheim.sh` | Enablement can be lost on SteamOS updates |
| Windows 11 | OpenSSH Server (optional feature) | `download_windows.sh`, `upload_windows.sh` | Admin accounts use a different `authorized_keys` |
| macOS | none needed | `download_macos.sh`, `deploy_local.sh` | Local copy; bundle must be re-signed |

---

## On the Mac (once)

Create a key if you don't have one, and confirm it's loaded:

```bash
ls ~/.ssh/id_ed25519.pub || ssh-keygen -t ed25519
ssh-add -l
```

Optional but recommended — give each target a name in `~/.ssh/config` so the
IP lives in one place:

```
Host deck
    HostName 192.168.86.42
    User deck

Host winbox
    HostName 192.168.86.50
    User martin
```

Both machines should get a **DHCP reservation** on the router. `config.sh`
hardcodes addresses, and a lease change breaks deploys with a confusing
"No route to host".

---

## Steam Deck (SteamOS)

SteamOS ships `sshd` but leaves it disabled, and the `deck` user has no
password until you set one.

1. **Switch to Desktop Mode** — Steam button → Power → Switch to Desktop.
2. **Set a password** (required before `sudo` works). Open Konsole:
   ```bash
   passwd
   ```
3. **Enable and start the server:**
   ```bash
   sudo systemctl enable --now sshd
   ```
4. **Find the address** — Settings → Internet → your network → Details, or
   `ip addr show` in Konsole.
5. **Install the Mac's key** (run on the Mac):
   ```bash
   ssh-copy-id deck@192.168.86.42
   ```
6. **Verify:**
   ```bash
   ssh deck@192.168.86.42 'echo ok'
   ```

**Gotchas**

- **SteamOS updates can undo this.** The root filesystem is immutable and
  gets replaced on update, so the enabled-service symlink may not survive; a
  major update can also disturb `~/.ssh`. If deploys suddenly fail with
  `Permission denied (publickey,password)` or connection refused right after
  an update, re-run steps 3 and 5 — that pairing was seen in this project on
  2026-08-16.
- **The Deck must be awake.** Suspended or in a game with the screen off, it
  drops off the network; `Operation timed out` usually means asleep, while
  `No route to host` means it is reachable-ish but not listening yet (still
  booting).
- Don't bother with `steamos-readonly disable` for this — enabling sshd
  doesn't need it.

---

## Windows 11

Run everything below in **PowerShell as Administrator**.

1. **Install the server feature:**
   ```powershell
   Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
   ```
   (Equivalent GUI path: Settings → System → Optional features → Add an
   optional feature → "OpenSSH Server".)

2. **Start it, and have it start at boot:**
   ```powershell
   Start-Service sshd
   Set-Service -Name sshd -StartupType Automatic
   ```

3. **Confirm the firewall rule exists** (the installer usually adds it):
   ```powershell
   Get-NetFirewallRule -Name *ssh*
   ```
   If nothing comes back:
   ```powershell
   New-NetFirewallRule -Name sshd -DisplayName 'OpenSSH Server (sshd)' `
     -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
   ```
   Also set the network profile to **Private**; the Public profile blocks
   inbound connections regardless of the rule.

4. **Install the Mac's public key.** This is where most setups fail, because
   the location depends on whether the account is an administrator.

   Print the key on the Mac:
   ```bash
   cat ~/.ssh/id_ed25519.pub
   ```

   **Standard (non-admin) account** — paste it into:
   ```
   C:\Users\<you>\.ssh\authorized_keys
   ```

   **Administrator account** — Windows OpenSSH *ignores* the per-user file and
   reads a shared one instead. Paste the key into
   `C:\ProgramData\ssh\administrators_authorized_keys`, then fix the ACLs, or
   sshd will silently refuse it:
   ```powershell
   icacls.exe "C:\ProgramData\ssh\administrators_authorized_keys" /inheritance:r `
     /grant "Administrators:F" /grant "SYSTEM:F"
   ```
   This behaviour comes from the `Match Group administrators` block at the
   bottom of `C:\ProgramData\ssh\sshd_config`.

5. **Restart the service and verify from the Mac:**
   ```powershell
   Restart-Service sshd
   ```
   ```bash
   ssh martin@192.168.86.50 'echo ok'
   ```

**Gotchas**

- **Leave the default shell as `cmd.exe`.** Switching it to PowerShell (the
  `DefaultShell` registry value) is a popular tweak that breaks `scp`/`sftp`
  on some builds — and `scp` is exactly what the deploy scripts use.
- **Spaces in the Steam path.** `scp` hands remote paths to `cmd.exe`, and the
  default library lives under `C:\Program Files (x86)`. `Scripts/win_common.sh`
  quotes around it, but if the machine has a second Steam library on a
  space-free path (e.g. `D:/SteamLibrary/...`), point `WIN_VALHEIM_MANAGED`
  there and the problem disappears.
- **Fast Startup / sleep** will make the box unreachable in the same way a
  suspended Deck is. Consider disabling hibernation-based fast startup if
  deploys keep timing out.

Then set the host in `Scripts/config.sh`:

```bash
WIN_HOST="martin@192.168.86.50"
WIN_VALHEIM_MANAGED="D:/SteamLibrary/steamapps/common/Valheim/valheim_Data/Managed"
```

---

## macOS (no SSH required)

The Mac is the build machine and deploys to its own Steam install by copying
files, so no server is involved. If you ever want to push *to* the Mac from
another machine: System Settings → General → Sharing → **Remote Login**.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Operation timed out` | Machine asleep or off the network | Wake it; check the IP hasn't moved |
| `No route to host` | Host up but not listening yet, or wrong IP | Wait for boot; confirm with `ping` |
| `Permission denied (publickey,password)` | Key missing on the target — on Windows, usually the admin `authorized_keys` trap | Re-run `ssh-copy-id` (Deck) or fix `administrators_authorized_keys` + ACLs (Windows) |
| `could not read Username for 'https://github.com'` | Unrelated — that's git, not the deploy target | Use the SSH remote: `git remote set-url origin git@github.com:…` |
| scp fails only on paths with spaces | Remote `cmd.exe` quoting | Use a space-free Steam library path |
| Worked yesterday, fails after an OS update | SteamOS reset the service/keys | Re-run `sudo systemctl enable --now sshd` and `ssh-copy-id` |
