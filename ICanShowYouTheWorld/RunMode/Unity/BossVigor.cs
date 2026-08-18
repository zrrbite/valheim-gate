using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Scales a boss's max health to the power the player has banked, so a run that stockpiled
    /// boons and heat doesn't walk over the altar fight it earned them for.
    ///
    /// EVERY piece of state this needs lives in the boss's own ZDO, not in this object. That is
    /// not a detail — it is the whole design, and the first version got it wrong.
    /// <see cref="Character.SetMaxHealth"/> writes <c>ZDOVars.s_maxHealth</c>, which Valheim SAVES
    /// WITH THE WORLD; an in-memory "already treated" set dies with the session, so a boss that was
    /// scaled, unloaded and loaded again looked untreated, and its already-scaled max health was
    /// read as the original and scaled AGAIN — 1.5x, then 2.25x, compounding every reload, with no
    /// record anywhere of what the number had once been.
    ///
    /// So the true pre-Run-Mode maximum is stamped into the ZDO under
    /// <see cref="OriginalMaxHealthKey"/> the first time a boss is touched, and every later
    /// computation starts from THAT rather than from whatever the health currently is. Treatment
    /// becomes idempotent across reloads, and a boss left scaled by an earlier session heals itself
    /// the next time a run sees it — it is re-derived from the stored original, not stacked on.
    ///
    /// Per-instance only: this touches one creature's ZDO. Nothing shared, nothing on the prefab.
    /// </summary>
    public class BossVigor
    {
        private const float ScanIntervalSeconds = 2f;
        private const float ScanRadius = 60f;

        /// <summary>How close to its max a boss's health must be to count as "not yet engaged" — see <see cref="Treat"/>.</summary>
        private const float FullHealthEpsilon = 0.01f;

        /// <summary>
        /// ZDO key holding a boss's max health as it was BEFORE Run Mode ever touched it. Present
        /// and non-zero means "already treated, and this is the number to compute from"; absent or
        /// zero means untouched. Namespaced to this mod because it is written into the player's
        /// world save and outlives every run.
        /// </summary>
        private const string OriginalMaxHealthKey = "ICSYTW_vigor_orig";

        /// <summary>
        /// Bosses this RUN has treated, by ZDOID — the stable, save-surviving identity, unlike the
        /// instance id the first version used. Used only to know what to unwind at run end; the
        /// authority on whether a boss has been treated is its ZDO, not this set.
        /// </summary>
        private readonly HashSet<ZDOID> _treated = new HashSet<ZDOID>();

        /// <summary>Reused scan buffer. Character.GetCharactersInRange APPENDS (verified in IL), so this is cleared first.</summary>
        private readonly List<Character> _scratch = new List<Character>();

        private float _timer;

        /// <summary>Set once if a scan throws, so a broken lookup can't log every two seconds for a whole run.</summary>
        private bool _logged;

        /// <summary>
        /// Advances the scan clock and, every <see cref="ScanIntervalSeconds"/>, scales any boss
        /// within <see cref="ScanRadius"/> of <paramref name="origin"/> that this run hasn't
        /// treated yet. Call only while a run is active and unfrozen; never throws.
        /// </summary>
        /// <param name="multiplier">
        /// Max-health multiplier to freeze onto bosses first seen in this scan, applied to the
        /// boss's TRUE original. Clamped to a floor of 1 — Run Mode may make a boss tougher, never
        /// weaker than vanilla.
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

                    var zdo = ZdoOf(c);
                    if (zdo == null) continue; // Not networked yet this frame — next scan will get it.
                    if (_treated.Contains(zdo.m_uid)) continue;

                    // Only recorded when the treatment actually landed. A frame where the boss was
                    // momentarily not ours, or at zero health, must not permanently blacklist it:
                    // the next scan two seconds later gets another go.
                    if (Treat(c, zdo, multiplier)) _treated.Add(zdo.m_uid);
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
        /// Puts every boss this run scaled back to the max health it had before Run Mode touched
        /// it, and clears the marker from its ZDO. Called when a run ends — the loan of power the
        /// boons are is worth nothing if the world keeps the interest.
        ///
        /// Works from the ZDO, so an UNLOADED boss is restored too: its ZDO is still in ZDOMan even
        /// when no Character exists, and <c>s_maxHealth</c> is exactly what
        /// <see cref="Character.GetMaxHealth"/> reads back when it loads (verified in IL). A boss
        /// that is loaded goes through SetMaxHealth instead, so its health clamp runs.
        ///
        /// A ZDO that no longer exists means the boss is DEAD. That is not a failure and is not
        /// counted as one — there is simply nothing left to restore.
        /// </summary>
        public void RestoreAll()
        {
            int restored = 0;
            int dead = 0;
            int failed = 0;

            foreach (var id in _treated)
            {
                try
                {
                    var zdo = ZDOMan.instance?.GetZDO(id);
                    if (zdo == null)
                    {
                        dead++;
                        continue;
                    }

                    float original = zdo.GetFloat(OriginalMaxHealthKey, 0f);
                    if (original <= 0f)
                    {
                        // No marker: either never really treated, or something else cleared it.
                        dead++;
                        continue;
                    }

                    var loaded = LoadedCharacter(id);
                    if (loaded != null) loaded.SetMaxHealth(original);
                    else zdo.Set(ZDOVars.s_maxHealth, original);

                    // Clear the marker last: while it is set, a later run would treat this boss as
                    // already-known and rebuild from it. Once the world is back to vanilla for this
                    // creature, the next run must be free to snapshot it afresh.
                    zdo.Set(OriginalMaxHealthKey, 0f);
                    restored++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogError($"[ICanShowYouTheWorld] Boss vigor restore failed for {id}: {ex}");
                }
            }

            if (restored > 0 || failed > 0)
            {
                Debug.Log($"[ICanShowYouTheWorld] Boss vigor unwound: {restored} restored, " +
                          $"{dead} already gone, {failed} failed.");
            }

            Reset();
        }

        /// <summary>Forgets this run's treated set and resets the clock. Private: the only supported way to end a run's scaling is <see cref="RestoreAll"/>, which unwinds first.</summary>
        private void Reset()
        {
            _treated.Clear();
            _timer = 0f;
            _logged = false;
            _scratch.Clear();
        }

        /// <summary>
        /// Raises one boss's max health to <c>original * multiplier</c>, where "original" is the
        /// value stamped into its ZDO the first time any run touched it.
        ///
        /// Computing from the stored original rather than from the current maximum is what stops
        /// the escalation: a boss scaled by an earlier session, or by an earlier moment of this
        /// one, is re-derived rather than re-multiplied, so the answer is the same however many
        /// times this runs.
        ///
        /// SetMaxHealth only clamps current health DOWNWARD (verified in IL) — raising the ceiling
        /// leaves the creature on its old hit points, which for a boss met at full health would
        /// make the whole multiplier cosmetic: same HP to chew through, bigger number over it. So a
        /// boss still at full health is topped up to its new maximum, which is not a heal but the
        /// "scaled before it took damage" case the design asks for. A boss already damaged keeps
        /// the hit points it has.
        ///
        /// Skipped when this machine doesn't own the character — the ZDO write would be discarded,
        /// or fought over, on somebody else's creature. (Run Mode is local-only, so this should
        /// always pass; it costs nothing to be sure.)
        /// </summary>
        /// <returns>True when the treatment landed and this boss should be tracked for unwinding.</returns>
        private static bool Treat(Character c, ZDO zdo, float multiplier)
        {
            if (!c.IsOwner()) return false;

            float health = c.GetHealth();
            if (health <= 0f) return false; // Already dead or dying; leave it alone.

            float original = zdo.GetFloat(OriginalMaxHealthKey, 0f);
            if (original <= 0f)
            {
                original = c.GetMaxHealth();
                if (!IsUsable(original)) return false;
                zdo.Set(OriginalMaxHealthKey, original);
            }

            // Floored at 1: Run Mode exists to make the fight harder, and a low-heat, no-boon
            // opening must never hand the player a boss WEAKER than the game shipped.
            float scaled = original * Mathf.Max(1f, multiplier);
            if (!IsUsable(scaled)) return false;

            float currentMax = c.GetMaxHealth();
            bool wasUntouched = health >= currentMax - FullHealthEpsilon;

            c.SetMaxHealth(scaled);
            if (wasUntouched) c.SetHealth(scaled);

            Debug.Log($"[ICanShowYouTheWorld] Boss vigor: {c.name} max HP {currentMax:0} -> {scaled:0} " +
                      $"(original {original:0}, x{Mathf.Max(1f, multiplier):0.00}).");
            return true;
        }

        /// <summary>The live Character for a ZDOID, or null when that boss isn't loaded right now.</summary>
        private static Character LoadedCharacter(ZDOID id)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return null;

            var go = scene.FindInstance(id);
            return go == null ? null : go.GetComponent<Character>();
        }

        private static ZDO ZdoOf(Character c)
        {
            // Character.m_nview is `family` (protected) in the assembly, so it can't be read
            // directly from here — the ZNetView is fetched off the GameObject instead.
            var view = c.GetComponent<ZNetView>();
            return view == null ? null : view.GetZDO();
        }

        private static bool IsUsable(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
