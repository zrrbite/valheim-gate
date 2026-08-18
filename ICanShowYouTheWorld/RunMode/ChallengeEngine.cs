using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// CollectFood is a CATEGORY measure — "any food", not a named item — so unlike CollectItem
    /// it carries no Param and is not matched against one. Appended rather than inserted: saves
    /// store challenge ids, not kinds, but renumbering an enum the game code switches on is a
    /// trap not worth setting.
    /// </summary>
    public enum ChallengeKind { KillPrefab, ReachAltitude, BuildHeight, CollectItem, NoArmorMinutes, CollectFood }

    public class ChallengeDefinition
    {
        public string Id;
        public ChallengeKind Kind;
        public string Param;     // prefab name for KillPrefab, item name for CollectItem
        public float Target;
        public float HeatReward;
        public string Display;

        /// <summary>
        /// World-progression tier this challenge belongs to: 0 Meadows, 1 Black Forest,
        /// 2 Swamp, 3 Mountain, 4 Plains. Compared against
        /// <see cref="ChallengeEngine.MaxTier"/> so a Swamp challenge can't be dealt to a
        /// player who hasn't killed Eikthyr yet. Defaults to 0 (always drawable).
        /// </summary>
        public int Tier;
    }

    public class ActiveChallenge
    {
        public ChallengeDefinition Def;
        public float Progress;
        public bool Done => Progress >= Def.Target;
    }

    /// <summary>Keeps up to 3 distinct challenges active; each refills after its own cooldown.</summary>
    public class ChallengeEngine
    {
        private readonly List<ChallengeDefinition> pool;
        private readonly Random rng;
        private readonly float refillCooldown;
        private readonly List<ActiveChallenge> active = new List<ActiveChallenge>();
        private readonly List<float> pendingRefills = new List<float>();

        public IReadOnlyList<ActiveChallenge> Active => active;
        public event Action<ChallengeDefinition> Completed;

        /// <summary>
        /// Highest <see cref="ChallengeDefinition.Tier"/> that may be DRAWN (by
        /// <see cref="Tick"/>'s refills or by <see cref="Reroll"/>). Owned by the caller, which
        /// raises it as the world's bosses fall. The default admits the whole pool, so a caller
        /// that never sets it sees no gating at all.
        ///
        /// Deliberately not enforced by <see cref="RestoreActive"/> — see the note there.
        /// </summary>
        public int MaxTier { get; set; } = int.MaxValue;

        public ChallengeEngine(IList<ChallengeDefinition> pool, Random rng, float refillCooldownSeconds)
        {
            this.pool = pool.ToList();
            this.rng = rng;
            this.refillCooldown = refillCooldownSeconds;
        }

        public void Tick(float dt)
        {
            // (1) Fire completions and vacate their slots.
            foreach (var a in active.Where(a => a.Done).ToList())
            {
                active.Remove(a);
                pendingRefills.Add(refillCooldown);
                Completed?.Invoke(a.Def);
            }

            // (2) Decrement pending refill timers and draw replacements when ready.
            for (int i = pendingRefills.Count - 1; i >= 0; i--)
            {
                pendingRefills[i] -= dt;
                if (pendingRefills[i] <= 0f)
                {
                    pendingRefills.RemoveAt(i);
                    TryDraw(out var drawnDef);
                    if (drawnDef != null)
                        active.Add(new ActiveChallenge { Def = drawnDef });
                }
            }

            // (3) Top up to 3 total (active + pending).
            while (active.Count + pendingRefills.Count < 3 && TryDraw(out var def))
                active.Add(new ActiveChallenge { Def = def });
        }

        public void ReportKill(string prefab)
        {
            foreach (var a in active)
                if (a.Def.Kind == ChallengeKind.KillPrefab && a.Def.Param == prefab)
                    a.Progress += 1f;
        }

        public void ReportMeasure(ChallengeKind kind, string param, float value)
        {
            foreach (var a in active)
            {
                if (a.Def.Kind != kind) continue;

                // CollectItem is the only param-scoped measure: it tracks one named item, so a
                // report about a different one must not touch it. Every other kind (altitude,
                // build height, no-armor minutes, CollectFood) is a single world-wide quantity
                // and ignores param entirely.
                if (kind == ChallengeKind.CollectItem && a.Def.Param != param) continue;

                a.Progress = Math.Max(a.Progress, value);
            }
        }

        /// <summary>
        /// Replaces the active set with saved id/progress pairs, resolved against this engine's
        /// own pool. Unknown ids are ignored (so a pool that excluded, say, kill challenges will
        /// not resurrect them), duplicates are dropped, and no more than 3 slots are filled.
        /// Any shortfall is topped up by the next Tick, as usual.
        ///
        /// <see cref="MaxTier"/> is intentionally NOT applied here. Any state in which a challenge
        /// was dealt before the current gating applied can hold an above-tier active — a save
        /// written before the tier ladder existed, a world whose progression has since been rolled
        /// back, or a caller that lowers MaxTier after dealing. Silently dropping it would look
        /// like lost progress, so it stays in its slot and <see cref="IsAboveTier"/> flags it,
        /// leaving the caller to offer a way out.
        /// </summary>
        public void RestoreActive(IEnumerable<KeyValuePair<string, float>> idToProgress)
        {
            active.Clear();
            pendingRefills.Clear();
            if (idToProgress == null) return;

            var byId = pool.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<string>();

            foreach (var entry in idToProgress)
            {
                if (active.Count >= 3) break;
                if (entry.Key == null || !seen.Add(entry.Key)) continue;
                if (!byId.TryGetValue(entry.Key, out var def)) continue;

                active.Add(new ActiveChallenge { Def = def, Progress = entry.Value });
            }
        }

        /// <summary>
        /// True when the challenge in this slot sits above <see cref="MaxTier"/> — i.e. it is
        /// content the world hasn't unlocked yet, so it cannot realistically be completed.
        /// Only reachable via <see cref="RestoreActive"/> (draws are already tier-filtered).
        /// False for an out-of-range slot.
        /// </summary>
        public bool IsAboveTier(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= active.Count) return false;
            return active[slotIndex].Def.Tier > MaxTier;
        }

        public bool Reroll(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= active.Count) return false;
            var options = Drawable();
            if (options.Count == 0) return false;
            active[slotIndex] = new ActiveChallenge { Def = options[rng.Next(options.Count)] };
            return true;
        }

        private bool TryDraw(out ChallengeDefinition def)
        {
            var options = Drawable();
            def = options.Count > 0 ? options[rng.Next(options.Count)] : null;
            return def != null;
        }

        /// <summary>Pool definitions eligible right now: not already active, and within <see cref="MaxTier"/>.</summary>
        private List<ChallengeDefinition> Drawable()
        {
            var taken = active.Select(a => a.Def.Id).ToHashSet();
            return pool.Where(d => !taken.Contains(d.Id) && d.Tier <= MaxTier).ToList();
        }
    }
}
