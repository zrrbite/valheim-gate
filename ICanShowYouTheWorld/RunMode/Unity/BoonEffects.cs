using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Turns boon gain/loss/activation into calls against the mod's existing cheat surfaces.
    /// RunService owns the lifecycle (which boon fired, when a run ends) and wires its three
    /// seams — ApplyBoonEffect, UnapplyBoonEffect, UnapplyAllBoonEffects — to this class's
    /// methods; this class only knows how to turn a boon id into game effects.
    ///
    /// Two cheat-state worlds coexist in this codebase: the legacy <see cref="CheatCommands"/>
    /// statics (driven every frame by <c>CheatCommands.PeriodicManager</c>) and the newer
    /// per-interface services (whose own periodic driver, <c>BuffService.HandlePeriodic</c>,
    /// has zero call sites). wind/ember ride the LEGACY toggles because that's the one actually
    /// ticking; pack goes through the modern <see cref="IPetService"/> since it's a one-shot
    /// action, not a periodic effect, so which world it lives in doesn't matter.
    ///
    /// fleet/sharp never touch either cheat's shared/global counters (SpeedUp/SpeedDown collapse
    /// walk into run and stomp jump; the damage-counter mechanism writes an ABSOLUTE, per-prefab
    /// value into the weapon's shared damage block). Instead they snapshot the player/item state
    /// on Apply and restore that exact snapshot on Unapply — "power is loaned", never permanent.
    ///
    /// mule/hearty/enduring are the same bargain in its simplest form: one plain float field on
    /// Player each (carry weight, base HP, base stamina), snapshotted and restored. They go
    /// through the shared <see cref="FieldBoost"/> table rather than three copies of fleet's code,
    /// and unlike fleet they refuse to apply twice to one player — the respawn re-apply path can
    /// legitimately reach them a second time, and stacking there would also snapshot the boosted
    /// value as the "original".
    /// </summary>
    public class BoonEffects
    {
        private const float SharpDamageMultiplier = 1.2f;
        private const float FleetSpeedIncrements = 2f;
        private const float WindOnSeconds = 10f;
        private const float EmberOnSeconds = 30f;

        private const float MuleCarryWeightBonus = 100f;   // vanilla Player.m_maxCarryWeight is 300

        // Vanilla Player.m_baseHP is 25, so the originally specified +25 was a flat doubling of
        // base HP — and it compounds with food and with Enduring. Trimmed to +15 (a 60% lift)
        // pending a play-test.
        private const float HeartyBaseHpBonus = 15f;
        private const float EnduringBaseStaminaBonus = 25f;

        private readonly Func<IReadOnlyList<HeldBoon>> _heldBoons;
        private readonly Func<IEnumerable<string>> _undefeatedBossLocations;

        private struct PendingOff
        {
            public string Key;
            public float Remaining;
            public Action Off;
        }

        // Timed ON/OFF scheduling for the active boons (wind's 10s heal window, ember's 30s burn
        // window). Keyed so a lose-then-regain cycle can't have a stale timer cut a fresh window
        // short (RemovePending is called both when (re)scheduling and when forcing off).
        private readonly List<PendingOff> _pending = new List<PendingOff>();

        // Toggle safety: the legacy statics are TOGGLES, not setters. Only flip one back off if
        // this class is the one that flipped it on — never stomp a state the player set for
        // themselves outside Run Mode.
        private bool _aoeRenewalOnByUs;
        private bool _cloakOnByUs;

        // fleet: a single snapshot — CreateOffer excludes held passives, so at most one fleet can
        // ever be held at a time.
        private struct FleetSnapshot
        {
            public float RunSpeed, WalkSpeed, JumpForce, JumpForceForward;
        }
        private FleetSnapshot _fleetSnapshot;
        private bool _fleetSnapshotTaken;

        /// <summary>
        /// mule/hearty/enduring: one plain float field on Player each, so they share a single
        /// snapshot-add-restore mechanism instead of three copies of fleet's code.
        ///
        /// All three fields are read live by the game every frame — GetMaxCarryWeight() reads
        /// m_maxCarryWeight, GetTotalFoodValue() reads m_baseHP/m_baseStamina (both verified
        /// against assembly_valheim's IL) — so raising the field is the whole effect. They are
        /// also plain instance fields on Player, NOT shared/prefab state, which is what makes a
        /// snapshot honest: the loan is per-character and dies with the run.
        /// </summary>
        private sealed class FieldBoost
        {
            public string Id;
            public float Amount;
            public Func<Player, float> Get;
            public Action<Player, float> Set;

            /// <summary>The player the snapshot was taken FROM. Guards against double-applying to one player, and against restoring into a different one.</summary>
            public Player Owner;
            public float Original;
            public bool Taken;
        }

        private readonly List<FieldBoost> _fieldBoosts = new List<FieldBoost>
        {
            new FieldBoost
            {
                Id = "mule", Amount = MuleCarryWeightBonus,
                Get = p => p.m_maxCarryWeight, Set = (p, v) => p.m_maxCarryWeight = v
            },
            new FieldBoost
            {
                Id = "hearty", Amount = HeartyBaseHpBonus,
                Get = p => p.m_baseHP, Set = (p, v) => p.m_baseHP = v
            },
            new FieldBoost
            {
                Id = "enduring", Amount = EnduringBaseStaminaBonus,
                Get = p => p.m_baseStamina, Set = (p, v) => p.m_baseStamina = v
            },
        };

        // sharp: keyed by the SHARED damage block (ItemDrop.ItemData.m_shared), not the ItemData
        // instance. m_shared is per-PREFAB, not per-instance — a fresh ItemData handed out after
        // respawn still points at the same SharedData object the pre-death item used. Keying by
        // instance would treat that as "new gear", re-snapshot an already-1.2x'd value, and stack
        // to 1.44x, with Unapply later stomping the prefab's true original with whichever
        // snapshot happened to restore last. Keying by the shared block itself makes "already
        // boosted" a property of the block, not the transient instance pointing at it.
        private readonly Dictionary<ItemDrop.ItemData.SharedData, HitData.DamageTypes> _sharpSnapshots =
            new Dictionary<ItemDrop.ItemData.SharedData, HitData.DamageTypes>();

        /// <summary>Set by a failed Activate() with a boon-specific reason; null means "not ready" is generic enough.</summary>
        public string LastActivationMessage { get; private set; }

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
                    ApplyFleet();
                    break;

                case "sharp":
                    ApplySharp();
                    break;

                case "pack":
                    WithServiceGodModeBracket(() => Resolve<IPetService>()?.BuffAllPets(false));
                    break;

                case "way":
                    // Actives aren't excluded from re-offer once held (only passives are), so a
                    // second "way" pick is a SECOND held entry — target the newest one and grant
                    // it its own charge rather than refilling whichever entry matched first.
                    var held = FindNewestHeld("way");
                    if (held != null) held.Charges++;
                    break;

                // wind/ember have no effect on gain — only on activation (Keypad4/5).

                default:
                    // mule/hearty/enduring, which are one Player field each; anything else
                    // (wind/ember, an id from a newer save) finds no match and does nothing.
                    ApplyFieldBoost(boonId);
                    break;
            }
        }

        public void Unapply(string boonId)
        {
            switch (boonId)
            {
                case "fleet":
                    UnapplyFleet();
                    break;

                case "sharp":
                    UnapplySharp();
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

                default:
                    // mule/hearty/enduring put their snapshotted Player field back; pack/way and
                    // any unknown id fall through here and do nothing.
                    UnapplyFieldBoost(boonId);
                    break;
            }
        }

        public bool Activate(string boonId)
        {
            LastActivationMessage = null;
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
        /// Called on run finish/abandon/failed-resume — a cheat toggle or a snapshot boost must
        /// never survive past the run that granted it. Each per-boon unapply is isolated so one
        /// throwing boon can't stop the rest from being cleaned up; the toggle safety clears run
        /// in a finally so they fire even if something above throws.
        /// </summary>
        public void UnapplyAll()
        {
            try
            {
                foreach (var pending in _pending.ToList()) SafeInvoke(pending.Off);
                _pending.Clear();

                var held = _heldBoons();
                if (held != null)
                {
                    foreach (var h in held.ToList())
                    {
                        string id = h.Def.Id;
                        SafeInvoke(() => Unapply(id));
                    }
                }
            }
            finally
            {
                ForceAoeRenewalOff();
                ForceCloakOff();
            }
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

        // --- fleet ---

        private void ApplyFleet()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            _fleetSnapshot = new FleetSnapshot
            {
                RunSpeed = player.m_runSpeed,
                WalkSpeed = player.m_walkSpeed,
                JumpForce = player.m_jumpForce,
                JumpForceForward = player.m_jumpForceForward
            };
            _fleetSnapshotTaken = true;

            float increment = Resolve<IConfiguration>()?.SpeedIncrement ?? 0.5f;
            float boost = increment * FleetSpeedIncrements;
            player.m_runSpeed += boost;
            player.m_walkSpeed += boost;
            // Jump is snapshotted (for an exact restore) but deliberately left untouched here.
        }

        private void UnapplyFleet()
        {
            if (!_fleetSnapshotTaken) return;
            _fleetSnapshotTaken = false;

            var player = Player.m_localPlayer;
            if (player == null) return;

            player.m_runSpeed = _fleetSnapshot.RunSpeed;
            player.m_walkSpeed = _fleetSnapshot.WalkSpeed;
            player.m_jumpForce = _fleetSnapshot.JumpForce;
            player.m_jumpForceForward = _fleetSnapshot.JumpForceForward;
        }

        // --- mule / hearty / enduring (single-field passives) ---

        /// <summary>
        /// Snapshots and raises this boon's Player field, if the id names one.
        ///
        /// Re-applying against the SAME player is a no-op rather than a second addition: unlike
        /// fleet, this path is reachable twice for one player, because RunService's respawn
        /// detector re-applies every held passive on a Player reference change, and a stale
        /// _trackedPlayer would otherwise stack the bonus and (worse) snapshot the already-boosted
        /// value as "original". Against a NEW player it re-snapshots and re-applies, which is the
        /// whole point of the respawn path — death hands back a Player with vanilla fields.
        /// </summary>
        private void ApplyFieldBoost(string boonId)
        {
            var boost = FindFieldBoost(boonId);
            if (boost == null) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (boost.Taken && ReferenceEquals(boost.Owner, player)) return;

            boost.Owner = player;
            boost.Original = boost.Get(player);
            boost.Taken = true;
            boost.Set(player, boost.Original + boost.Amount);
        }

        /// <summary>
        /// Puts the snapshotted value back, but only into the player it was taken from — restoring
        /// one character's number onto another's fields would hand out (or confiscate) a permanent
        /// bonus. When that player is gone the snapshot is simply dropped: its fields went with it.
        /// </summary>
        private void UnapplyFieldBoost(string boonId)
        {
            var boost = FindFieldBoost(boonId);
            if (boost == null || !boost.Taken) return;

            var owner = boost.Owner;
            boost.Taken = false;
            boost.Owner = null;

            // Unity's == (not ReferenceEquals) is what's wanted here: a destroyed player must read
            // as null, since writing fields on it is pointless and a fresh one has vanilla values.
            if (owner == null) return;

            boost.Set(owner, boost.Original);
        }

        private FieldBoost FindFieldBoost(string boonId)
        {
            foreach (var b in _fieldBoosts)
            {
                if (b.Id == boonId) return b;
            }
            return null;
        }

        // --- sharp ---

        private void ApplySharp()
        {
            var inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null) return;

            foreach (var item in inventory.GetEquippedItems())
            {
                if (item == null || !item.IsWeapon()) continue;

                var shared = item.m_shared;
                if (shared == null || _sharpSnapshots.ContainsKey(shared)) continue; // already boosted — don't stack

                _sharpSnapshots[shared] = DamageHelpers.Copy(shared.m_damages);
                shared.m_damages = DamageHelpers.Scaled(shared.m_damages, SharpDamageMultiplier);
            }
        }

        private void UnapplySharp()
        {
            foreach (var kvp in _sharpSnapshots)
            {
                var shared = kvp.Key;
                if (shared == null) continue; // guard: no longer reachable, nothing to restore
                shared.m_damages = kvp.Value;
            }
            _sharpSnapshots.Clear();
        }

        // --- actives ---

        private bool ActivateWind()
        {
            var held = FindHeld("wind");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (CheatCommands.AOERenewalActive)
            {
                // Already on (ours or the player's) — nothing to (re)activate, don't burn the cooldown.
                LastActivationMessage = "AoE Renewal is already active.";
                return false;
            }

            // Flag set BEFORE the call, not after: ToggleAoeRenewal can flip the live static and
            // THEN throw (e.g. CheatVisualizer failing) — setting the flag first means a later
            // step throwing (scheduling, cooldown) can never skip it and strand an on-but-
            // unflagged effect. A throw from the toggle call itself rolls the flag back, since in
            // that case we can't tell whether the live flag actually flipped.
            _aoeRenewalOnByUs = true;
            try
            {
                WithLegacyGodModeBracket(CheatCommands.ToggleAoeRenewal);
            }
            catch
            {
                _aoeRenewalOnByUs = false;
                throw;
            }

            RemovePending("wind");
            SchedulePending("wind", WindOnSeconds, ForceAoeRenewalOff);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private bool ActivateEmber()
        {
            var held = FindHeld("ember");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (CheatCommands.CloakActive)
            {
                LastActivationMessage = "Cloak of Flames is already active.";
                return false;
            }

            // See ActivateWind for why this is set before the call, not after.
            _cloakOnByUs = true;
            try
            {
                WithLegacyGodModeBracket(CheatCommands.ToggleCloakOfFlames);
            }
            catch
            {
                _cloakOnByUs = false;
                throw;
            }

            RemovePending("ember");
            SchedulePending("ember", EmberOnSeconds, ForceCloakOff);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private bool ActivateWay()
        {
            var held = FindHeldWithCharge("way");
            if (held == null)
            {
                LastActivationMessage = "No Waystone charge available.";
                return false;
            }

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

            if (bestPos == null)
            {
                LastActivationMessage = "No undefeated altar found.";
                return false;
            }

            var teleport = Resolve<ITeleportService>();
            if (teleport == null) return false;

            teleport.TeleportTo(bestPos.Value + Vector3.up * 2f);
            held.Charges--;
            return true;
        }

        // --- toggle-safety helpers ---

        private void ForceAoeRenewalOff()
        {
            RemovePending("wind");
            if (!_aoeRenewalOnByUs) return;
            _aoeRenewalOnByUs = false;

            if (CheatCommands.AOERenewalActive) WithLegacyGodModeBracket(CheatCommands.ToggleAoeRenewal);
        }

        private void ForceCloakOff()
        {
            RemovePending("ember");
            if (!_cloakOnByUs) return;
            _cloakOnByUs = false;

            if (CheatCommands.CloakActive) WithLegacyGodModeBracket(CheatCommands.ToggleCloakOfFlames);
        }

        /// <summary>
        /// AoE Renewal / Cloak of Flames gate on the LEGACY CheatCommands.GodMode flag, which
        /// RunService forces off for the whole run. The flag is bracketed on just long enough
        /// for the one synchronous toggle call it gates, and restored via CheatCommands.SetGodMode
        /// (the side-effect-free setter) — no frame is ever rendered with it set.
        /// </summary>
        private static void WithLegacyGodModeBracket(Action action) =>
            WithGodModeBracket(CheatCommands.SetGodMode, () => CheatCommands.GodMode, action);

        /// <summary>Same bracket, for the modern-service god-mode flag that gates IPetService.BuffAllPets.</summary>
        private static void WithServiceGodModeBracket(Action action)
        {
            var combat = Resolve<ICombatService>();
            if (combat == null)
            {
                action?.Invoke();
                return;
            }
            WithGodModeBracket(combat.SetGodMode, () => combat.GodMode, action);
        }

        /// <summary>
        /// weTurnedOn is latched BEFORE calling setGodMode(true) — CombatService/CheatCommands
        /// both assign their flag before touching the player, so if that call throws partway,
        /// the flag can already be true; latching first guarantees the finally still tries to
        /// put it back regardless of where the throw happens.
        /// </summary>
        private static void WithGodModeBracket(Action<bool> setGodMode, Func<bool> getGodMode, Action action)
        {
            if (action == null) return;

            bool weTurnedOn = false;
            try
            {
                if (!getGodMode())
                {
                    weTurnedOn = true;
                    setGodMode(true);
                }
                action();
            }
            finally
            {
                if (weTurnedOn) setGodMode(false);
            }
        }

        private void SchedulePending(string key, float seconds, Action off)
        {
            _pending.Add(new PendingOff { Key = key, Remaining = seconds, Off = off });
        }

        private void RemovePending(string key)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Key == key) _pending.RemoveAt(i);
            }
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

        /// <summary>Most-recently-picked entry for an id that can be held more than once (any active).</summary>
        private HeldBoon FindNewestHeld(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Def.Id == boonId) return list[i];
            }
            return null;
        }

        private HeldBoon FindHeldWithCharge(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            foreach (var h in list)
            {
                if (h.Def.Id == boonId && h.Charges > 0) return h;
            }
            return null;
        }

        /// <summary>Never throws — ServiceContainer.Instance.TryGet returns null rather than raising when unregistered.</summary>
        private static T Resolve<T>() where T : class => ServiceContainer.Instance.TryGet<T>();

        private static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[ICanShowYouTheWorld] BoonEffects action failed: {ex}"); }
        }
    }
}
