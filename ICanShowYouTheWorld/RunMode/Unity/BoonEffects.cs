using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Turns boon gain/loss/activation into calls against the mod's existing cheat services
    /// (<see cref="ICombatService"/> via <see cref="CheatCommands"/>, <see cref="IBuffService"/>,
    /// <see cref="IPetService"/>, <see cref="ITeleportService"/>). RunService owns the lifecycle
    /// (which boon fired, when a run ends) and wires its three seams — ApplyBoonEffect,
    /// UnapplyBoonEffect, UnapplyAllBoonEffects — to this class's methods; this class only knows
    /// how to turn a boon id into game effects.
    ///
    /// Every entry point swallows its own exceptions where it matters (the god-mode bracket is
    /// try/finally so a throwing toggle can never leave real invulnerability switched on).
    /// </summary>
    public class BoonEffects
    {
        private readonly Func<IReadOnlyList<HeldBoon>> _heldBoons;
        private readonly Func<IEnumerable<string>> _undefeatedBossLocations;

        // Toggle safety: IBuffService exposes ON/OFF toggles, not setters. Only flip a toggle
        // back OFF if this class is the one that flipped it ON — never stomp a state the player
        // set for themselves outside Run Mode.
        private bool _aoeRenewalOnByUs;
        private bool _cloakOnByUs;

        private struct PendingOff
        {
            public float Remaining;
            public Action Off;
        }

        // Timed ON/OFF scheduling for the active boons (wind's 10s heal window, ember's 30s
        // burn window). RunService.Tick drives this every frame while a run is active.
        private readonly List<PendingOff> _pending = new List<PendingOff>();

        public BoonEffects(Func<IReadOnlyList<HeldBoon>> heldBoons, Func<IEnumerable<string>> undefeatedBossLocations)
        {
            _heldBoons = heldBoons ?? (() => Array.Empty<HeldBoon>());
            _undefeatedBossLocations = undefeatedBossLocations ?? (() => Enumerable.Empty<string>());
        }

        // --- Public surface (RunService's boon seams) ---

        public void Apply(string boonId)
        {
            switch (boonId)
            {
                case "fleet":
                    CheatCommands.SpeedUp();
                    CheatCommands.SpeedUp();
                    break;

                case "sharp":
                    CheatCommands.IncreaseDamageCounter();
                    CheatCommands.IncreaseDamageCounter();
                    break;

                case "pack":
                    WithGodModeBypass(() => ModBootstrap.GetService<IPetService>()?.BuffAllPets(false));
                    break;

                case "way":
                    // Single-charge active: the charge is granted the moment the boon is picked,
                    // not on first activation.
                    var held = FindHeld("way");
                    if (held != null) held.Charges = 1;
                    break;

                // wind/ember have no effect on gain — only on activation (Keypad4/5).
            }
        }

        public void Unapply(string boonId)
        {
            switch (boonId)
            {
                case "fleet":
                    CheatCommands.SpeedDown();
                    CheatCommands.SpeedDown();
                    break;

                case "sharp":
                    CheatCommands.DecreaseDamageCounter();
                    CheatCommands.DecreaseDamageCounter();
                    break;

                case "wind":
                    // Losing the boon (e.g. death) mid-window must not leave the AoE heal running.
                    ForceAoeRenewalOff();
                    break;

                case "ember":
                    ForceCloakOff();
                    break;

                // pack: buffs fade naturally, nothing to unwind.
                // way: charges are just data — nothing to unwind.
            }
        }

        public bool Activate(string boonId)
        {
            switch (boonId)
            {
                case "wind": return ActivateWind();
                case "ember": return ActivateEmber();
                case "way": return ActivateWay();
                default: return false;
            }
        }

        /// <summary>
        /// Fires every pending timed effect immediately and unwinds every currently-held boon.
        /// Called on run finish/abandon — a cheat toggle must never survive past the run that
        /// turned it on.
        /// </summary>
        public void UnapplyAll()
        {
            foreach (var pending in _pending) SafeInvoke(pending.Off);
            _pending.Clear();

            var held = _heldBoons();
            if (held != null)
            {
                // Copy first: Unapply must never be able to mutate the collection it's iterating.
                foreach (var h in held.ToList()) Unapply(h.Def.Id);
            }

            // Belt and suspenders in case a toggle was flipped on outside the pending-timer path.
            ForceAoeRenewalOff();
            ForceCloakOff();
        }

        /// <summary>Advances timed ON/OFF windows. Call once per frame while a run is active.</summary>
        public void Tick(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var entry = _pending[i];
                entry.Remaining -= dt;
                if (entry.Remaining <= 0f)
                {
                    _pending.RemoveAt(i);
                    SafeInvoke(entry.Off);
                }
                else
                {
                    _pending[i] = entry;
                }
            }
        }

        // --- Actives ---

        private bool ActivateWind()
        {
            var held = FindHeld("wind");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var buffs = ModBootstrap.GetService<IBuffService>();
            if (buffs == null) return false;

            if (!buffs.AOERenewalActive)
            {
                WithGodModeBypass(buffs.ToggleAoeRenewal);
                _aoeRenewalOnByUs = true;
                SchedulePending(10f, ForceAoeRenewalOff);
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private bool ActivateEmber()
        {
            var held = FindHeld("ember");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var buffs = ModBootstrap.GetService<IBuffService>();
            if (buffs == null) return false;

            if (!buffs.CloakActive)
            {
                WithGodModeBypass(buffs.ToggleCloakOfFlames);
                _cloakOnByUs = true;
                SchedulePending(30f, ForceCloakOff);
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private bool ActivateWay()
        {
            var held = FindHeld("way");
            if (held == null || held.Charges <= 0) return false;

            var zone = ZoneSystem.instance;
            var player = Player.m_localPlayer;
            if (zone == null || player == null) return false;

            Vector3 playerPos = player.transform.position;
            Vector3? bestPos = null;
            float bestDist = float.MaxValue;

            foreach (var locName in _undefeatedBossLocations())
            {
                if (!zone.FindClosestLocation(locName, playerPos, out ZoneSystem.LocationInstance loc)) continue;

                float dist = Vector3.Distance(playerPos, loc.m_position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPos = loc.m_position;
                }
            }

            if (bestPos == null) return false;

            var teleport = ModBootstrap.GetService<ITeleportService>();
            if (teleport == null) return false;

            teleport.TeleportTo(bestPos.Value + Vector3.up * 2f);
            held.Charges--;
            return true;
        }

        // --- Toggle-safety helpers ---

        private void ForceAoeRenewalOff()
        {
            if (!_aoeRenewalOnByUs) return;
            _aoeRenewalOnByUs = false;

            var buffs = ModBootstrap.GetService<IBuffService>();
            if (buffs != null && buffs.AOERenewalActive) WithGodModeBypass(buffs.ToggleAoeRenewal);
        }

        private void ForceCloakOff()
        {
            if (!_cloakOnByUs) return;
            _cloakOnByUs = false;

            var buffs = ModBootstrap.GetService<IBuffService>();
            if (buffs != null && buffs.CloakActive) WithGodModeBypass(buffs.ToggleCloakOfFlames);
        }

        /// <summary>
        /// AoE Renewal, Cloak of Flames and the pet buff all gate themselves behind
        /// ICombatService's god-mode flag (see BuffService/PetService), which RunService
        /// deliberately forces OFF for the whole run. Boons are earned rewards, not the god-mode
        /// cheat menu, so the flag is bracketed on just long enough for the single synchronous
        /// call it gates and put back exactly as found — no frame is ever rendered with it set,
        /// and a throwing toggle still restores it via finally.
        /// </summary>
        private static void WithGodModeBypass(Action action)
        {
            if (action == null) return;

            var combat = ModBootstrap.GetService<ICombatService>();
            bool wasOn = combat != null && combat.GodMode;
            if (combat != null && !wasOn) combat.SetGodMode(true);
            try
            {
                action();
            }
            finally
            {
                if (combat != null && !wasOn) combat.SetGodMode(false);
            }
        }

        private void SchedulePending(float seconds, Action off)
        {
            _pending.Add(new PendingOff { Remaining = seconds, Off = off });
        }

        private HeldBoon FindHeld(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Def.Id == boonId) return list[i];
            }
            return null;
        }

        private static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[ICanShowYouTheWorld] BoonEffects timed-off action failed: {ex}"); }
        }
    }
}
