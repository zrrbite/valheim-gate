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

        /// <summary>Instance ids of bosses already scaled. Cleared with the run, since ids don't survive one.</summary>
        private readonly HashSet<int> _treated = new HashSet<int>();

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
                    if (!_treated.Add(c.GetInstanceID())) continue;

                    Treat(c, multiplier);
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

        /// <summary>Forgets every treated boss and resets the clock. Called when a run ends.</summary>
        public void Reset()
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
        private static void Treat(Character c, float multiplier)
        {
            if (multiplier <= 1f || !c.IsOwner()) return;

            float max = c.GetMaxHealth();
            if (max <= 0f || float.IsNaN(max) || float.IsInfinity(max)) return;

            float health = c.GetHealth();
            if (health <= 0f) return; // Already dead or dying; leave it alone.

            float scaled = max * multiplier;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled)) return;

            bool wasUntouched = health >= max - FullHealthEpsilon;

            c.SetMaxHealth(scaled);
            if (wasUntouched) c.SetHealth(scaled);

            Debug.Log($"[ICanShowYouTheWorld] Boss vigor: {c.name} max HP {max:0} -> {scaled:0} (x{multiplier:0.00}).");
        }
    }
}
