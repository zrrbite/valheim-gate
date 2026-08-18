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
    ///
    /// StatDelta is param-scoped like CollectItem: Param names ONE of Valheim's lifetime player
    /// stats (a PlayerStatType member, as a string) and the target is a DELTA measured from the
    /// value that stat held when the challenge was dealt — see <see cref="ActiveChallenge.Baseline"/>.
    /// Resolving the name and computing the delta belongs to the caller; the engine only matches
    /// on the string.
    /// </summary>
    public enum ChallengeKind { KillPrefab, ReachAltitude, BuildHeight, CollectItem, NoArmorMinutes, CollectFood, StatDelta }

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

        /// <summary>
        /// Part of the fixed opening chain: dealt in pool order, ahead of any random draw, on a
        /// FRESH engine that has dealt nothing yet. Opener definitions are excluded from every
        /// random draw and from rerolls, so once one has been completed or rerolled away it is
        /// gone for the rest of the run — the chain is a scripted first few minutes, not a set of
        /// challenges that can come round again.
        /// </summary>
        public bool Opener;
    }

    public class ActiveChallenge
    {
        public ChallengeDefinition Def;
        public float Progress;

        /// <summary>
        /// Deal-time snapshot of the lifetime stat a <see cref="ChallengeKind.StatDelta"/> challenge
        /// measures — the zero point its target counts up from. NaN means "not taken yet".
        ///
        /// The engine only STORES this: it has no idea what a PlayerStatType is, and taking the
        /// snapshot needs the live game. The caller fills it in the moment a slot is dealt (and
        /// carries it across a save, via the baselines argument of <see cref="ChallengeEngine.RestoreActive"/>) —
        /// re-baselining on resume against an already-higher lifetime value would silently un-earn
        /// whatever progress the player had banked.
        ///
        /// Meaningless for every other kind, which measure absolute quantities, and left NaN there.
        /// </summary>
        public float Baseline = float.NaN;

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

        /// <summary>Opener-flagged definitions in pool order, consumed from the front by the first deal.</summary>
        private readonly Queue<ChallengeDefinition> openers;

        /// <summary>
        /// Set the moment this engine deals or is handed ANY challenge. The opening chain is only
        /// offered while this is false, which is the whole of the "fresh run only" rule: a resumed
        /// run gets its actives from <see cref="RestoreActive"/> (which sets this even when it
        /// restores nothing), so its shortfall refills randomly instead of replaying the opening.
        /// </summary>
        private bool dealtAnything;

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
            this.openers = new Queue<ChallengeDefinition>(this.pool.Where(d => d.Opener));
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

            // (3) Top up to 3 total (active + pending). On a fresh engine the opening chain
            // claims the first slots, in pool order, before any random draw gets a look in.
            // Latched BEFORE the loop, not read from dealtAnything inside it: the flag is set by
            // the loop's own first add, so testing it per-iteration would deal opener #1 and then
            // draw the other two slots at random.
            bool fresh = !dealtAnything;

            while (active.Count + pendingRefills.Count < 3)
            {
                // Openers are deliberately NOT filtered by MaxTier. The chain is the scripted
                // opening of a run and is tier-0 by construction, so a tier test could only ever
                // silently drop a link out of it — turning a designed progression into a random
                // deal with no way to tell.
                ChallengeDefinition def =
                    fresh && openers.Count > 0 ? openers.Dequeue()
                    : TryDraw(out var drawn) ? drawn
                    : null;

                if (def == null) break;

                active.Add(new ActiveChallenge { Def = def });
                dealtAnything = true;
            }
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

                // CollectItem and StatDelta are the param-scoped measures: each tracks ONE named
                // thing (an item, a lifetime stat), so a report about a different one must not
                // touch it. Every other kind (altitude, build height, no-armor minutes,
                // CollectFood) is a single world-wide quantity and ignores param entirely.
                if ((kind == ChallengeKind.CollectItem || kind == ChallengeKind.StatDelta) &&
                    a.Def.Param != param) continue;

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
        public void RestoreActive(IEnumerable<KeyValuePair<string, float>> idToProgress) =>
            RestoreActive(idToProgress, null);

        /// <summary>
        /// As <see cref="RestoreActive(IEnumerable{KeyValuePair{string, float}})"/>, additionally
        /// restoring each slot's <see cref="ActiveChallenge.Baseline"/>.
        ///
        /// <paramref name="baselines"/> is indexed against the SAVED sequence, not against the
        /// slots that survive it: entries dropped here (unknown id, duplicate, past the 3-slot cap)
        /// still consume their index. Saves store the two as parallel lists written from the same
        /// active set, so any other alignment would hand a restored challenge somebody else's
        /// zero point — and a wrong baseline is worse than none, since it silently shifts every
        /// future progress report. A short or absent list leaves the remainder NaN, which is
        /// exactly the "caller must snapshot this" state a freshly dealt slot is in.
        /// </summary>
        public void RestoreActive(IEnumerable<KeyValuePair<string, float>> idToProgress, IList<float> baselines)
        {
            active.Clear();
            pendingRefills.Clear();

            // Unconditional, and set even when nothing is restored: a resumed run must never
            // replay the opening chain. Restored actives take precedence, and any shortfall is
            // topped up by an ordinary random draw.
            dealtAnything = true;

            if (idToProgress == null) return;

            var byId = pool.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<string>();

            int index = -1;
            foreach (var entry in idToProgress)
            {
                index++;
                if (active.Count >= 3) break;
                if (entry.Key == null || !seen.Add(entry.Key)) continue;
                if (!byId.TryGetValue(entry.Key, out var def)) continue;

                active.Add(new ActiveChallenge
                {
                    Def = def,
                    Progress = entry.Value,
                    Baseline = baselines != null && index < baselines.Count ? baselines[index] : float.NaN
                });
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

        /// <summary>
        /// Swaps a slot for a fresh RANDOM draw. Rerolling an opener therefore leaves the chain:
        /// <see cref="Drawable"/> excludes openers, so the replacement is an ordinary challenge and
        /// the discarded link never comes back.
        /// </summary>
        public bool Reroll(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= active.Count) return false;
            var options = Drawable();
            if (options.Count == 0) return false;
            active[slotIndex] = new ActiveChallenge { Def = options[rng.Next(options.Count)] };
            dealtAnything = true;
            return true;
        }

        private bool TryDraw(out ChallengeDefinition def)
        {
            var options = Drawable();
            def = options.Count > 0 ? options[rng.Next(options.Count)] : null;
            return def != null;
        }

        /// <summary>
        /// Pool definitions eligible for a RANDOM deal right now: not an opener, not already
        /// active, and within <see cref="MaxTier"/>.
        ///
        /// Openers are excluded outright rather than merely deprioritised, and that exclusion is
        /// what makes "never redealt" true: the opening chain is reachable only through the
        /// fresh-engine path in <see cref="Tick"/>, so a completed or rerolled-away opener can
        /// never come back through a refill or a reroll.
        /// </summary>
        private List<ChallengeDefinition> Drawable()
        {
            var taken = active.Select(a => a.Def.Id).ToHashSet();
            return pool.Where(d => !d.Opener && !taken.Contains(d.Id) && d.Tier <= MaxTier).ToList();
        }
    }
}
