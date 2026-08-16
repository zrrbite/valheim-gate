using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    public class BoonDefinition
    {
        public string Id;
        public string Display;
        public bool IsPassive;
        public float CooldownSeconds; // 0 = no cooldown (passive or charge-based)
    }

    public class HeldBoon
    {
        public BoonDefinition Def;
        public float CooldownRemaining;
        public int Charges;
    }

    /// <summary>Offers, held boons, cooldowns; power is loaned per-run.</summary>
    public class BoonEngine
    {
        private readonly List<BoonDefinition> pool;
        private readonly Random rng;
        private readonly float offerTimeout;
        private readonly List<BoonDefinition> offer = new List<BoonDefinition>();
        private readonly List<HeldBoon> held = new List<HeldBoon>();
        private float offerAge;

        public IReadOnlyList<BoonDefinition> CurrentOffer => offer;
        public IReadOnlyList<HeldBoon> Held => held;
        public event Action<BoonDefinition> Gained;
        public event Action<BoonDefinition> Lost;

        public BoonEngine(IList<BoonDefinition> pool, Random rng, float offerTimeoutSeconds)
        {
            this.pool = pool.ToList();
            this.rng = rng;
            this.offerTimeout = offerTimeoutSeconds;
        }

        public void CreateOffer()
        {
            if (offer.Count > 0) return;
            var heldPassives = new HashSet<string>(held.Where(h => h.Def.IsPassive).Select(h => h.Def.Id));
            var options = pool.Where(d => !heldPassives.Contains(d.Id)).ToList();
            if (options.Count == 0) return;
            for (int i = 0; i < 3 && options.Count > 0; i++)
            {
                var pick = options[rng.Next(options.Count)];
                options.Remove(pick);
                offer.Add(pick);
            }
            offerAge = 0f;
        }

        public bool Pick(int index)
        {
            if (index < 0 || index >= offer.Count) return false;
            var def = offer[index];
            offer.Clear();
            held.Add(new HeldBoon { Def = def });
            Gained?.Invoke(def);
            return true;
        }

        /// <summary>
        /// Replaces the held set with saved id/cooldown pairs, resolved against this engine's
        /// own pool. Unknown and duplicate ids are ignored. Deliberately silent — it raises no
        /// Gained events, so the caller reapplies effects for whatever ends up held.
        /// </summary>
        public void RestoreHeld(IEnumerable<KeyValuePair<string, float>> idToCooldown)
        {
            held.Clear();
            if (idToCooldown == null) return;

            var byId = pool.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<string>();

            foreach (var entry in idToCooldown)
            {
                if (entry.Key == null || !seen.Add(entry.Key)) continue;
                if (!byId.TryGetValue(entry.Key, out var def)) continue;

                held.Add(new HeldBoon { Def = def, CooldownRemaining = entry.Value });
            }
        }

        public void Tick(float dt)
        {
            if (offer.Count > 0)
            {
                offerAge += dt;
                if (offerAge >= offerTimeout) offer.Clear();
            }
            foreach (var h in held)
                if (h.CooldownRemaining > 0f)
                    h.CooldownRemaining = Math.Max(0f, h.CooldownRemaining - dt);
        }

        public HeldBoon RemoveLatest()
        {
            if (held.Count == 0) return null;
            var last = held[held.Count - 1];
            held.RemoveAt(held.Count - 1);
            Lost?.Invoke(last.Def);
            return last;
        }
    }
}
