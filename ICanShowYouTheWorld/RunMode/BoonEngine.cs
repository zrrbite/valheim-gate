using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    public class BoonDefinition
    {
        public string Id;
        public string Display;
        public string Description;
        public bool IsPassive;
        public float CooldownSeconds; // 0 = no cooldown (passive or charge-based)

        /// <summary>
        /// Bosses that must be down before this boon may be OFFERED. 0 (the default) means always.
        ///
        /// Exists for boons that are worthless until the run has got somewhere — a frost resistance
        /// offered in the Meadows costs the player one of three options on something that will do
        /// nothing for hours. It is the boon pool's equivalent of the challenge pool's
        /// <c>MaxTier</c>, which has gated content by world progression since alpha11.
        ///
        /// Compared against <see cref="BoonEngine.DefeatedBosses"/>, which the host derives from the
        /// world rather than storing — the same reading the acts use.
        /// </summary>
        public int MinBosses;
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

        /// <summary>
        /// Boon id to place at index 0 of this engine's FIRST offer, so a run's opening pick is a
        /// designed one rather than whatever the rng produced. The remaining two options are drawn
        /// at random as usual, and every offer after the first is untouched. Null (the default)
        /// leaves the engine fully random, which is what every existing caller and test sees.
        ///
        /// A pin that isn't available when the first offer is built — not in the pool, or already
        /// held as a passive — is simply not honoured; that offer is random and still counts as
        /// the first. Failing loudly here would mean refusing to offer anything at all.
        /// </summary>
        public string FirstOfferPin;

        /// <summary>
        /// How many bosses this world has down, for <see cref="BoonDefinition.MinBosses"/>. Owned by
        /// the host, which derives it from the world rather than storing it — the same reading the
        /// act index takes. The default of 0 means an unset caller sees only ungated boons, which is
        /// the safe direction: a boon offered too early is a wasted pick, one offered too late is
        /// merely absent.
        /// </summary>
        public int DefeatedBosses;

        /// <summary>Drops the current offer without picking from it. Used by the timeout and by tests.</summary>
        public void ClearOffer() => offer.Clear();

        /// <summary>True once an offer has actually been produced, which is what spends the pin.</summary>
        private bool firstOfferMade;

        public BoonEngine(IList<BoonDefinition> pool, Random rng, float offerTimeoutSeconds)
        {
            this.pool = pool.ToList();
            this.rng = rng;
            this.offerTimeout = offerTimeoutSeconds;
        }

        public void CreateOffer()
        {
            if (offer.Count > 0) return;
            // Nothing already held is ever offered again — passive or active (owner, alpha18:
            // "we shouldn't offer boons we already have, I was offered many I already had").
            // Actives used to be exempt so that a second Waystone pick could buy another charge,
            // but with four of them in the pool that exemption turned most offers into a list of
            // things the player already owned. Waystone earns its charges from boss kills instead.
            var heldIds = new HashSet<string>(held.Select(h => h.Def.Id));
            var options = pool
                .Where(d => !heldIds.Contains(d.Id))
                .Where(d => d.MinBosses <= DefeatedBosses)
                .ToList();
            if (options.Count == 0) return;

            // The pin takes slot 0 and is removed from the draw pool, so the two random options
            // beside it stay distinct from it and from each other.
            if (!firstOfferMade && FirstOfferPin != null)
            {
                var pinned = options.FirstOrDefault(d => d.Id == FirstOfferPin);
                if (pinned != null)
                {
                    options.Remove(pinned);
                    offer.Add(pinned);
                }
            }

            while (offer.Count < 3 && options.Count > 0)
            {
                var pick = options[rng.Next(options.Count)];
                options.Remove(pick);
                offer.Add(pick);
            }

            offerAge = 0f;
            firstOfferMade = true;
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
            RestoreHeld(idToCooldown, null);
        }

        /// <summary>
        /// Same as <see cref="RestoreHeld(IEnumerable{KeyValuePair{string, float}})"/>, plus a
        /// charges list positioned by index against <paramref name="idToCooldown"/>'s own
        /// enumeration order (before duplicate/unknown filtering) — the same pairing the caller
        /// already uses to zip ids with cooldowns. Missing or short lists default to 0 charges.
        /// </summary>
        public void RestoreHeld(IEnumerable<KeyValuePair<string, float>> idToCooldown, IEnumerable<int> charges)
        {
            held.Clear();
            if (idToCooldown == null) return;

            var byId = pool.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<string>();
            var chargeList = charges?.ToList();

            int index = -1;
            foreach (var entry in idToCooldown)
            {
                index++;
                if (entry.Key == null || !seen.Add(entry.Key)) continue;
                if (!byId.TryGetValue(entry.Key, out var def)) continue;

                int charge = (chargeList != null && index < chargeList.Count) ? chargeList[index] : 0;
                held.Add(new HeldBoon { Def = def, CooldownRemaining = entry.Value, Charges = charge });
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
