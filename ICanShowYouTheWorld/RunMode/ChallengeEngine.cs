using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    public enum ChallengeKind { KillPrefab, ReachAltitude, BuildHeight, CollectItem, NoArmorMinutes }

    public class ChallengeDefinition
    {
        public string Id;
        public ChallengeKind Kind;
        public string Param;     // prefab name for KillPrefab, item name for CollectItem
        public float Target;
        public float HeatReward;
        public string Display;
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
                if (kind == ChallengeKind.CollectItem && a.Def.Param != param) continue;
                a.Progress = Math.Max(a.Progress, value);
            }
        }

        /// <summary>
        /// Replaces the active set with saved id/progress pairs, resolved against this engine's
        /// own pool. Unknown ids are ignored (so a pool that excluded, say, kill challenges will
        /// not resurrect them), duplicates are dropped, and no more than 3 slots are filled.
        /// Any shortfall is topped up by the next Tick, as usual.
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

        public bool Reroll(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= active.Count) return false;
            var taken = active.Select(a => a.Def.Id).ToHashSet();
            var options = pool.Where(d => !taken.Contains(d.Id)).ToList();
            if (options.Count == 0) return false;
            active[slotIndex] = new ActiveChallenge { Def = options[rng.Next(options.Count)] };
            return true;
        }

        private bool TryDraw(out ChallengeDefinition def)
        {
            var taken = active.Select(a => a.Def.Id).ToHashSet();
            var options = pool.Where(d => !taken.Contains(d.Id)).ToList();
            def = options.Count > 0 ? options[rng.Next(options.Count)] : null;
            return def != null;
        }
    }
}
