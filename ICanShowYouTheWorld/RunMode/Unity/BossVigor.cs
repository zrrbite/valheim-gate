using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Scales a boss's max health to the power the player has banked, so a run that stockpiled
    /// boons and heat doesn't walk over the altar fight it earned them for.
    ///
    /// Deliberately per-INSTANCE. The multiplier goes through <see cref="Character.SetMaxHealth"/>,
    /// which writes s_maxHealth on that character's own ZDO — nothing shared, nothing on the
    /// prefab, and nothing that outlives the creature. The alternative (editing the prefab's
    /// Humanoid, the way the mod's weapon-damage cheat edits ItemData.m_shared) would leak a
    /// buffed boss into every later world on this install; there is no unapply for that.
    ///
    /// Each boss is treated exactly once, at first sight, and the multiplier is frozen from the
    /// heat and boon count of that moment. Re-reading it later would let the player farm the
    /// boss's own HP up and down by picking boons mid-fight, and would compound multiplicatively
    /// on every scan.
    /// </summary>
    public class BossVigor
    {
        private const float ScanIntervalSeconds = 2f;
        private const float ScanRadius = 60f;

        /// <summary>How close to its max a boss's health must be to count as "not yet engaged" — see <see cref="Treat"/>.</summary>
        private const float FullHealthEpsilon = 0.01f;

        /// <summary>What was done to one boss, so it can be undone when the run ends.</summary>
        private struct Treatment
        {
            public Character Boss;
            public float OriginalMaxHealth;
        }

        /// <summary>
        /// Bosses already scaled, keyed by instance id (ids don't survive a run, and neither does
        /// this map). The Character reference is held so the scaling can actually be UNDONE —
        /// an id alone can't find its way back to the object. Holding it pins only the small C#
        /// wrapper, not the native object, and the map is bounded by the handful of bosses a run
        /// can walk past.
        /// </summary>
        private readonly Dictionary<int, Treatment> _treated = new Dictionary<int, Treatment>();

        /// <summary>Reused scan buffer. Character.GetCharactersInRange APPENDS (verified in IL), so this is cleared first.</summary>
        private readonly List<Character> _scratch = new List<Character>();

        private float _timer;

        /// <summary>Set once if a scan throws, so a broken lookup can't log every two seconds for a whole run.</summary>
        private bool _logged;

        /// <summary>
        /// Advances the scan clock and, every <see cref="ScanIntervalSeconds"/>, scales any
        /// not-yet-treated boss within <see cref="ScanRadius"/> of <paramref name="origin"/>.
        /// Call only while a run is active and unfrozen; never throws.
        /// </summary>
        /// <param name="multiplier">
        /// Max-health multiplier to freeze onto bosses first seen in this scan. Values at or below
        /// 1 are a no-op, but the boss is still marked treated: its multiplier was decided here.
        /// </param>
        public void Tick(float dt, Vector3 origin, float multiplier)
        {
            _timer += dt;
            if (_timer < ScanIntervalSeconds) return;
            _timer = 0f;

            try
            {
                _scratch.Clear();
                Character.GetCharactersInRange(origin, ScanRadius, _scratch);

                foreach (var c in _scratch)
                {
                    if (c == null || !c.IsBoss()) continue;

                    int id = c.GetInstanceID();
                    if (_treated.ContainsKey(id)) continue;

                    // Recorded even when Treat declines to scale (not the owner, dead, a nonsense
                    // max): "treated" means "decided about", and re-deciding on a later scan is
                    // exactly the compounding this set exists to prevent. A zero original marks
                    // the nothing-to-undo case for RestoreAll.
                    _treated[id] = new Treatment { Boss = c, OriginalMaxHealth = Treat(c, multiplier) };
                }
            }
            catch (Exception ex)
            {
                if (_logged) return;
                _logged = true;
                Debug.LogError($"[ICanShowYouTheWorld] Boss vigor scan failed (further occurrences suppressed): {ex}");
            }
            finally
            {
                // Holding Character references between scans would pin destroyed objects alive.
                _scratch.Clear();
            }
        }

        /// <summary>
        /// Puts every scaled boss back to the max health it had before Run Mode touched it, then
        /// forgets them all. Called when a run ends — the loan of power the boons are is worth
        /// nothing if the world keeps the interest.
        ///
        /// A boss that has been UNLOADED (world gone, zone unloaded, or it simply died) cannot be
        /// restored: its ZDO isn't in memory to write to. That is an accepted residual — it is
        /// world-local, and bosses are mortal — but it is logged rather than passed over in
        /// silence, because it is the one case where a run leaves a mark on a world after it ends.
        ///
        /// Restoring a DAMAGED boss lets SetMaxHealth clamp its current health down to the
        /// restored maximum, which is the correct direction: it gives back nothing the player
        /// hasn't already fought for.
        /// </summary>
        public void RestoreAll()
        {
            int restored = 0;
            int stranded = 0;

            foreach (var entry in _treated.Values)
            {
                // Nothing was changed for this one (Treat declined), so there is nothing to undo.
                if (entry.OriginalMaxHealth <= 0f) continue;

                try
                {
                    // Unity's == is what's wanted: a destroyed or unloaded Character reads as null.
                    if (entry.Boss == null || !entry.Boss.IsOwner())
                    {
                        stranded++;
                        continue;
                    }

                    entry.Boss.SetMaxHealth(entry.OriginalMaxHealth);
                    restored++;
                }
                catch (Exception ex)
                {
                    stranded++;
                    Debug.LogError($"[ICanShowYouTheWorld] Boss vigor restore failed: {ex}");
                }
            }

            if (restored > 0 || stranded > 0)
            {
                Debug.Log($"[ICanShowYouTheWorld] Boss vigor unwound: {restored} boss(es) restored, " +
                          $"{stranded} left scaled (unloaded or not ours at run end).");
            }

            Reset();
        }

        /// <summary>Forgets every treated boss and resets the clock. Private: the only supported way to end a run's scaling is <see cref="RestoreAll"/>, which unwinds first.</summary>
        private void Reset()
        {
            _treated.Clear();
            _timer = 0f;
            _logged = false;
            _scratch.Clear();
        }

        /// <summary>
        /// Raises one boss's max health.
        ///
        /// SetMaxHealth only clamps current health DOWNWARD (verified in IL) — raising the ceiling
        /// leaves the creature on its old hit points, which for a boss met at full health would
        /// make the whole multiplier cosmetic: same HP to chew through, bigger number over it.
        /// So a boss still at full health is topped up to its new maximum, which is not a heal but
        /// the "scaled before it took damage" case the design asks for. A boss already damaged
        /// keeps the hit points it has: no free health mid-fight, just a higher ceiling it can
        /// never reach.
        ///
        /// Skipped entirely when this machine doesn't own the character — the ZDO write would be
        /// discarded, or fought over, on somebody else's creature. (Run Mode is local-only, so
        /// this should always pass; it costs nothing to be sure.)
        /// </summary>
        /// <returns>
        /// The max health this boss had BEFORE scaling, for <see cref="RestoreAll"/> to put back;
        /// 0 when nothing was changed and there is therefore nothing to undo.
        /// </returns>
        private static float Treat(Character c, float multiplier)
        {
            if (multiplier <= 1f || !c.IsOwner()) return 0f;

            float max = c.GetMaxHealth();
            if (max <= 0f || float.IsNaN(max) || float.IsInfinity(max)) return 0f;

            float health = c.GetHealth();
            if (health <= 0f) return 0f; // Already dead or dying; leave it alone.

            float scaled = max * multiplier;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled)) return 0f;

            bool wasUntouched = health >= max - FullHealthEpsilon;

            c.SetMaxHealth(scaled);
            if (wasUntouched) c.SetHealth(scaled);

            Debug.Log($"[ICanShowYouTheWorld] Boss vigor: {c.name} max HP {max:0} -> {scaled:0} (x{multiplier:0.00}).");
            return max;
        }
    }
}
