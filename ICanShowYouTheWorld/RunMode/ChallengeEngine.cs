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

    /// <summary>Keeps up to 3 distinct challenges active; refills after a cooldown.</summary>
    public class ChallengeEngine
    {
        private readonly List<ChallengeDefinition> pool;
        private readonly Random rng;
        private readonly float refillCooldown;
        private readonly List<ActiveChallenge> active = new List<ActiveChallenge>();
        private float cooldownRemaining;

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
            // Fire completions and vacate their slots.
            foreach (var a in active.Where(a => a.Done).ToList())
            {
                active.Remove(a);
                cooldownRemaining = refillCooldown;
                Completed?.Invoke(a.Def);
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dt;
                if (cooldownRemaining > 0f) return;
            }
            while (active.Count < 3 && TryDraw(out var def))
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
