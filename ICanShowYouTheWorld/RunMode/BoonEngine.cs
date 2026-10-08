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

        /// <summary>
        /// The way (class) this boon belongs to, or null for a general boon.
        ///
        /// Null is Odin's loan: offered by the wheel, taken back on death. A class id means the
        /// boon is part of that way's kit — never offered, never taken on death, and reachable only
        /// through <see cref="BoonEngine.Grant"/>. Classes are boons rather than a second system
        /// so that apply, repay, save, restore and the ability bar all stay one path; this field
        /// is the whole of the difference.
        /// </summary>
        public string ClassId;

        /// <summary>
        /// Draw weight in the offer roll; 1 is normal, 0 is treated as 1. Exists because a boon
        /// with prerequisites the player worked for (Shepherd wants a tame) deserves better odds
        /// than one that is always relevant — a uniform draw made the pet boon a rare sight in
        /// exactly the runs that built a pen for it.
        /// </summary>
        public int Weight = 1;
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
        private bool stowed;
        private int owed;

        /// <summary>
        /// The offer, shown or stowed - what a key or a click picks from only while <see cref="OfferShown"/>.
        /// </summary>
        public IReadOnlyList<BoonDefinition> CurrentOffer => offer;

        /// <summary>
        /// The card is up: the choice keys mean its lines. Since 2026-10-08 the timeout STOWS an offer instead
        /// of throwing it away (the owner watched one vanish while still choosing): the card steps aside, the
        /// actives get their keys back, and the offer waits for <see cref="Recall"/> (End).
        /// </summary>
        public bool OfferShown => offer.Count > 0 && !stowed;

        /// <summary>An offer waits, its card stepped aside.</summary>
        public bool OfferStowed => offer.Count > 0 && stowed;

        /// <summary>Offers owed beyond the current one: tasks finished while one waited, dealt as each is picked.</summary>
        public int OffersOwed => owed;

        /// <summary>Every offer waiting, the current one included - the strip's flag and the run save count these.</summary>
        public int OffersWaiting => (offer.Count > 0 ? 1 : 0) + owed;
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

        /// <summary>
        /// The pool's definition for an id, held or not, or null if the pool has no such boon.
        ///
        /// For naming what the player does not hold yet - the HUD lists a way's later rungs before
        /// they are learned, and <see cref="Held"/> cannot name what is not in it.
        /// </summary>
        public BoonDefinition Definition(string id) =>
            string.IsNullOrEmpty(id) ? null : pool.FirstOrDefault(d => d.Id == id);

        /// <summary>Drops the current offer without picking from it. Used by tests; the timeout only stows now.</summary>
        public void ClearOffer()
        {
            offer.Clear();
            stowed = false;
        }

        /// <summary>The card steps aside, the offer kept (the timeout, or End closing the run window).</summary>
        public void Stow()
        {
            if (offer.Count > 0) stowed = true;
        }

        /// <summary>A waiting offer's card comes back, with a fresh timeout. False when nothing waits.</summary>
        public bool Recall()
        {
            if (offer.Count == 0) return false;
            stowed = false;
            offerAge = 0f;
            return true;
        }

        /// <summary>
        /// A reload: deals one offer afresh for what waited, stowed under its flag, and owes the rest. Call it after
        /// the held set and <see cref="DefeatedBosses"/> are restored, so the deal sees what this run may be offered.
        /// </summary>
        public void RestoreWaiting(int count)
        {
            offer.Clear();
            owed = 0;
            stowed = false;
            if (count <= 0) return;
            CreateOffer();
            if (offer.Count == 0) return;
            owed = count - 1;
            stowed = true;
        }

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
            // One already waits: this one is owed, not lost, and a stowed card comes back with the news.
            if (offer.Count > 0)
            {
                owed++;
                Recall();
                return;
            }
            // Nothing already held is ever offered again — passive or active (owner, alpha18:
            // "we shouldn't offer boons we already have, I was offered many I already had").
            // Actives used to be exempt so that a second Waystone pick could buy another charge,
            // but with four of them in the pool that exemption turned most offers into a list of
            // things the player already owned. Waystone earns its charges from boss kills instead.
            var heldIds = new HashSet<string>(held.Select(h => h.Def.Id));
            var options = pool
                .Where(d => !heldIds.Contains(d.Id))
                .Where(d => d.MinBosses <= DefeatedBosses)
                // The wheel is Odin's. A way's kit is taught at the graves, never dealt.
                .Where(d => d.ClassId == null)
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
                // Weighted, without replacement. A linear walk over summed weights: the pool is
                // tens of entries, not thousands, and obvious beats clever here.
                int total = 0;
                foreach (var o in options) total += Math.Max(1, o.Weight);

                int roll = rng.Next(total);
                BoonDefinition pick = options[options.Count - 1];
                foreach (var o in options)
                {
                    roll -= Math.Max(1, o.Weight);
                    if (roll < 0) { pick = o; break; }
                }

                options.Remove(pick);
                offer.Add(pick);
            }

            offerAge = 0f;
            stowed = false;
            firstOfferMade = true;
        }

        /// <summary>
        /// Grants a boon outright, without an offer. Returns false if the id is unknown or already
        /// held.
        ///
        /// For boons a QUESTLINE STEP awards rather than the offer wheel — the homestead handing
        /// over what it earned. It raises Gained like any other acquisition, so the effect is
        /// applied and repaid through exactly the same path; nothing about a granted boon is a
        /// special case after this line.
        /// </summary>
        public bool Grant(string boonId)
        {
            if (string.IsNullOrEmpty(boonId)) return false;
            if (held.Any(h => h.Def.Id == boonId)) return false;

            var def = pool.FirstOrDefault(b => b.Id == boonId);
            if (def == null) return false;

            held.Add(new HeldBoon { Def = def });
            Gained?.Invoke(def);
            return true;
        }

        public bool Pick(int index)
        {
            if (index < 0 || index >= offer.Count) return false;
            var def = offer[index];
            offer.Clear();
            stowed = false;
            held.Add(new HeldBoon { Def = def });
            Gained?.Invoke(def);

            // The next owed offer comes at once; if the pool has nothing left to deal, nothing is owed.
            if (owed > 0)
            {
                owed--;
                CreateOffer();
                if (offer.Count == 0) owed = 0;
            }
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
            if (offer.Count > 0 && !stowed)
            {
                offerAge += dt;
                if (offerAge >= offerTimeout) stowed = true;
            }
            foreach (var h in held)
                if (h.CooldownRemaining > 0f)
                    h.CooldownRemaining = Math.Max(0f, h.CooldownRemaining - dt);
        }

        /// <summary>
        /// Takes the newest GENERAL boon, or nothing. Death lets the world collect a LOAN; what the
        /// thane taught was not lent, so class boons are stepped over rather than taken — and if
        /// only class boons are held, the death costs no boon at all and nothing is raised.
        /// </summary>
        public HeldBoon RemoveLatest()
        {
            for (int i = held.Count - 1; i >= 0; i--)
            {
                if (held[i].Def.ClassId != null) continue;
                var last = held[i];
                held.RemoveAt(i);
                Lost?.Invoke(last.Def);
                return last;
            }
            return null;
        }

        /// <summary>
        /// Removes one held boon by id, raising Lost so its effect is repaid on the usual path.
        /// Returns false if it is not held.
        ///
        /// Not a death rule — <see cref="RemoveLatest"/> is that. This is for giving back a whole
        /// way at once (the dev layer switching class), which has to name its boons because the
        /// newest-first order says nothing about which way a boon came from.
        /// </summary>
        public bool Revoke(string boonId)
        {
            int i = held.FindIndex(h => h.Def.Id == boonId);
            if (i < 0) return false;
            var gone = held[i];
            held.RemoveAt(i);
            Lost?.Invoke(gone.Def);
            return true;
        }
    }
}
