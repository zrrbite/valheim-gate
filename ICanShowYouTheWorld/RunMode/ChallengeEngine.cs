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
    ///
    /// BuildPiece is "the player has built one of these", and is param-scoped for the same reason:
    /// Param names a CATEGORY of building piece — "Fire", "Bed", "Chest", "Door" — and a report
    /// about one must not credit a challenge asking for another. The categories deliberately name
    /// COMPILED Valheim types (Fireplace, Bed, Container, Door) rather than prefab names, which are
    /// Unity asset data this build cannot verify and which fail silently when wrong; resolving a
    /// category to a component belongs to the caller, which reports only what it can currently see.
    /// That last part is why targets are absolute rather than incremental: the caller re-reports
    /// every poll and stops reporting once the player walks away from their house, so progress
    /// relies on ReportMeasure's max-semantics to latch.
    /// </summary>
    public enum ChallengeKind { KillPrefab, ReachAltitude, BuildHeight, CollectItem, NoArmorMinutes, CollectFood, StatDelta, BuildPiece }

    public class ChallengeDefinition
    {
        public string Id;
        public ChallengeKind Kind;
        public string Param;     // prefab name for KillPrefab, item name for CollectItem
        public float Target;
        public float HeatReward;
        public string Display;
        /// <summary>Bitmask of Heightmap.Biome values (as int) where this quest makes sense;
        /// 0 = anywhere. The engine knows nothing about biomes — the host expresses "player has
        /// been there" through <see cref="ChallengeEngine.ExternalFilter"/>.</summary>
        public int Biomes;

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

        /// <summary>
        /// Marks this definition as a link in the MAIN QUEST chain (see
        /// <see cref="ChallengeEngine.SetMainChain"/>) rather than a random task. The engine uses
        /// it for nothing at all — the chain is identified by the list it was handed, not by this
        /// flag — but the <see cref="ChallengeEngine.Completed"/> event carries only a definition,
        /// so this is how the host tells "the questline advanced, hand out its reward" from "a
        /// random task finished, offer a boon".
        /// </summary>
        public bool MainQuest;

        /// <summary>
        /// A standing task: instead of vacating its slot on completion, it pays out and starts
        /// over at zero in the same slot, for as long as the run lasts. The engine's reliable
        /// faucet — every other task either finishes for good or waits on a refill timer and a
        /// random draw, so a player who has run the pool dry (or whose biome filter is narrow)
        /// can be left with nothing to work toward.
        ///
        /// Pair it with <see cref="Opener"/> to make it permanent: openers are seated on a fresh
        /// engine and excluded from every random draw, so exactly one copy exists and no refill
        /// can ever deal a second.
        /// </summary>
        public bool Repeatable;

        /// <summary>
        /// Player-facing description of what completing this step GIVES, e.g. "Bow + 40 arrows".
        /// UI copy only: the engine is pure and hands out nothing, so the actual grant lives
        /// host-side, keyed by <see cref="Id"/>. Null/empty for anything with no reward to show.
        /// </summary>
        public string RewardText;

        /// <summary>
        /// Gate on whether this definition may be DEALT at all: the run must already have built a
        /// piece of the named category (the same vocabulary
        /// <see cref="ChallengeKind.BuildPiece"/> uses). Null or empty means no gate.
        ///
        /// The engine does not read this — it rides <see cref="ExternalFilter"/>, host-side, next
        /// to the biome gate, because only the host knows what the run has built. It exists here
        /// so the requirement is authored on the definition it belongs to rather than as a special
        /// case buried in a lambda.
        ///
        /// The point is that a task can be impossible to complete without being impossible to
        /// notice: "Open 8 doors" dealt to a player with no door is not a challenge, it is a dead
        /// slot they must pay heat to reroll.
        /// </summary>
        public string RequiresBuilt;

        /// <summary>
        /// When set (non-null, non-empty), this definition is a COMPOSITE/multi-objective
        /// challenge: <see cref="Kind"/>/<see cref="Param"/>/<see cref="Target"/> above are
        /// unused and <see cref="ActiveChallenge.Done"/> instead requires every sub's own
        /// progress to reach its own target. Null or empty means "today's single-objective
        /// behaviour", unchanged.
        ///
        /// Restricted to <see cref="ChallengeKind.KillPrefab"/>, <see cref="ChallengeKind.CollectItem"/>,
        /// <see cref="ChallengeKind.CollectFood"/> and <see cref="ChallengeKind.BuildPiece"/> — the
        /// ABSOLUTE-quantity measures.
        /// <see cref="ChallengeKind.StatDelta"/> is deliberately excluded: it needs a
        /// per-objective <see cref="ActiveChallenge.Baseline"/> snapshot taken at deal time, and
        /// a composite's ActiveChallenge has exactly one Baseline field, not one per sub. Rather
        /// than half-solve that (which sub owns the shared baseline?), composites simply don't
        /// use StatDelta. Nothing enforces this at runtime — it is a pool-authoring rule, kept
        /// simple on purpose.
        /// </summary>
        public List<SubObjective> Subs;
    }

    /// <summary>
    /// One measurable clause of a composite <see cref="ChallengeDefinition"/> — "kill 1 boar",
    /// "hold 5 raspberries". See <see cref="ChallengeDefinition.Subs"/> for the kind restriction.
    /// </summary>
    public class SubObjective
    {
        public ChallengeKind Kind;
        public string Param;
        public float Target;

        /// <summary>Short player-facing text for this one clause, e.g. "Kill 1 Boar".</summary>
        public string Label;
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

        /// <summary>
        /// Per-sub progress for a composite challenge (<see cref="ChallengeDefinition.Subs"/>
        /// non-empty), index-parallel with it. Null for a simple challenge. Allocated (to
        /// Subs.Count, all zero) whenever a composite slot is dealt, rerolled into, or restored —
        /// see <see cref="ChallengeEngine"/>'s MakeActive/RestoreActive.
        /// </summary>
        public List<float> SubProgress;

        /// <summary>
        /// A composite challenge is done when EVERY sub's progress has reached its own target;
        /// a simple one keeps the original single-target behaviour. A missing/short SubProgress
        /// entry reads as zero rather than throwing, matching the malformed-save tolerance the
        /// rest of Run Mode's persistence uses.
        /// </summary>
        public bool Done
        {
            get
            {
                if (Def.Subs == null || Def.Subs.Count == 0) return Progress >= Def.Target;

                for (int i = 0; i < Def.Subs.Count; i++)
                {
                    float p = SubProgress != null && i < SubProgress.Count ? SubProgress[i] : 0f;
                    if (p < Def.Subs[i].Target) return false;
                }
                return true;
            }
        }
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

        /// <summary>The ordered main-quest chain, or empty when none was set. See <see cref="SetMainChain"/>.</summary>
        private List<ChallengeDefinition> mainChain = new List<ChallengeDefinition>();

        /// <summary>Position in <see cref="mainChain"/>; equal to its Count once the chain is exhausted.</summary>
        private int mainIndex;

        private ActiveChallenge mainQuest;

        public IReadOnlyList<ActiveChallenge> Active => active;
        public event Action<ChallengeDefinition> Completed;

        /// <summary>
        /// The one main-quest step in play right now, in a RESERVED slot of its own — it is not in
        /// <see cref="Active"/>, does not count against the three random slots, is never drawn or
        /// rerolled, and is not filtered by <see cref="MaxTier"/> or <see cref="ExternalFilter"/>.
        /// Null when no chain was set or the chain has been exhausted.
        /// </summary>
        public ActiveChallenge CurrentMainQuest => mainQuest;

        /// <summary>
        /// The slot index that addresses <see cref="CurrentMainQuest"/> in
        /// <see cref="ReportSlotMeasure"/>. Negative on purpose: the main quest sits outside the
        /// active list, so it cannot share that list's index space, and every other negative index
        /// keeps its existing "ignored" behaviour.
        /// </summary>
        public const int MainQuestSlot = -1;

        /// <summary>
        /// How far along the main-quest chain the run is: 0 = the first step, Count = exhausted.
        /// Persisted by the host so a resumed run picks the questline up where it left off.
        ///
        /// The setter re-seats the current step with ZERO progress and fires nothing — assigning
        /// an index is a restore, not a completion. Use <see cref="RestoreMainQuest"/> to bring
        /// part-finished progress back with it.
        /// </summary>
        public int MainQuestIndex
        {
            get => mainIndex;
            set => RestoreMainQuest(value, 0f);
        }

        /// <summary>
        /// Installs the ordered main-quest chain and starts it at step 0. The chain is kept
        /// entirely apart from the random pool: its definitions are never drawn, never rerolled
        /// into a slot, and never tier- or filter-gated, so a questline step cannot be lost to the
        /// rng or to a world whose progression hasn't caught up yet.
        ///
        /// A null or empty chain simply means "no questline" — <see cref="CurrentMainQuest"/> stays
        /// null and nothing else about the engine changes.
        /// </summary>
        public void SetMainChain(List<ChallengeDefinition> chain)
        {
            mainChain = chain == null ? new List<ChallengeDefinition>() : chain.ToList();
            mainIndex = 0;
            mainQuest = mainIndex < mainChain.Count ? MakeActive(mainChain[mainIndex]) : null;
        }

        /// <summary>
        /// Puts the chain back at <paramref name="index"/> with <paramref name="progress"/> on that
        /// step, WITHOUT firing <see cref="Completed"/> for any step it skips past — a resume must
        /// restore a position, not replay the rewards that got the run there.
        ///
        /// Call after <see cref="SetMainChain"/> (which defines what the index means). An index at
        /// or beyond the chain's length is the exhausted state and leaves
        /// <see cref="CurrentMainQuest"/> null; a negative one is clamped to the start, matching
        /// the malformed-save tolerance the rest of the restore path uses.
        /// </summary>
        public void RestoreMainQuest(int index, float progress) => RestoreMainQuest(index, progress, null);

        /// <summary>
        /// Restores the questline position, preferring the saved step's ID over its index.
        ///
        /// The index alone is only meaningful against the exact chain that wrote it, and the chain
        /// is content that changes between builds. Reordering it — as alpha17 did, inserting two
        /// steps and moving the deer hunt — silently reattributes an old save's position to a
        /// different step, and since a step is complete the moment Progress >= Target, a position
        /// carried over from a longer objective fires an instant, unearned completion (rewards and
        /// all) on the first tick.
        ///
        /// So: find the saved ID and resume exactly where the player was. Failing that (a save from
        /// before IDs were written, or a step that no longer exists), keep the index but DROP the
        /// progress — the player repeats a step at worst, rather than being handed one.
        /// </summary>
        public void RestoreMainQuest(int index, float progress, string id)
        {
            int byId = id == null ? -1 : mainChain.FindIndex(d => d.Id == id);
            bool trusted = byId >= 0;

            mainIndex = Math.Max(0, trusted ? byId : index);

            if (mainIndex >= mainChain.Count)
            {
                mainQuest = null;
                return;
            }

            mainQuest = MakeActive(mainChain[mainIndex]);
            mainQuest.Progress = trusted ? Math.Max(0f, progress) : 0f;
        }

        /// <summary>
        /// Highest <see cref="ChallengeDefinition.Tier"/> that may be DRAWN (by
        /// <see cref="Tick"/>'s refills or by <see cref="Reroll"/>). Owned by the caller, which
        /// raises it as the world's bosses fall. The default admits the whole pool, so a caller
        /// that never sets it sees no gating at all.
        ///
        /// Deliberately not enforced by <see cref="RestoreActive"/> — see the note there.
        /// </summary>
        public int MaxTier { get; set; } = int.MaxValue;

        /// <summary>Optional host-supplied gate on what may be DEALT (drawn or rerolled into).
        /// Null = everything eligible. Already-active challenges are unaffected by later changes.</summary>
        public Func<ChallengeDefinition, bool> ExternalFilter { get; set; }

        public ChallengeEngine(IList<ChallengeDefinition> pool, Random rng, float refillCooldownSeconds)
        {
            this.pool = pool.ToList();
            this.rng = rng;
            this.refillCooldown = refillCooldownSeconds;
            this.openers = new Queue<ChallengeDefinition>(this.pool.Where(d => d.Opener));
        }

        public void Tick(float dt)
        {
            // (0) The main quest advances first, so a host handler that reads CurrentMainQuest
            // during the event already sees the NEXT step rather than the one it just finished
            // (the finished definition is the event's own argument).
            if (mainQuest != null && mainQuest.Done)
            {
                var finished = mainQuest.Def;
                mainIndex++;
                mainQuest = mainIndex < mainChain.Count ? MakeActive(mainChain[mainIndex]) : null;
                Completed?.Invoke(finished);
            }

            // (1) Fire completions and vacate their slots — except a Repeatable, which keeps its
            // slot and starts over. Its progress is reset BEFORE the event fires, so a handler
            // that reads Active during the callback sees the fresh objective rather than a
            // finished one it would report as complete a second time.
            foreach (var a in active.Where(a => a.Done).ToList())
            {
                if (a.Def.Repeatable)
                {
                    a.Progress = 0f;
                    // NaN re-arms the host's StatDelta baseline sync, so the next round measures
                    // from where this one ended instead of instantly completing again.
                    a.Baseline = float.NaN;
                    if (a.SubProgress != null)
                        for (int i = 0; i < a.SubProgress.Count; i++) a.SubProgress[i] = 0f;

                    Completed?.Invoke(a.Def);
                    continue;
                }

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
                        active.Add(MakeActive(drawnDef));
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

                active.Add(MakeActive(def));
                dealtAnything = true;
            }
        }

        public void ReportKill(string prefab)
        {
            foreach (var a in active) CreditKill(a, prefab);

            // The main quest measures through exactly the same reports as a random slot; it just
            // lives outside the active list.
            if (mainQuest != null) CreditKill(mainQuest, prefab);
        }

        public void ReportMeasure(ChallengeKind kind, string param, float value)
        {
            foreach (var a in active) CreditMeasure(a, kind, param, value);

            if (mainQuest != null) CreditMeasure(mainQuest, kind, param, value);
        }

        private static void CreditKill(ActiveChallenge a, string prefab)
        {
            if (a.Def.Kind == ChallengeKind.KillPrefab && a.Def.Param == prefab)
                a.Progress += 1f;

            CreditKillSub(a, prefab);
        }

        private static void CreditMeasure(ActiveChallenge a, ChallengeKind kind, string param, float value)
        {
            // Runs unconditionally, ahead of the simple-challenge returns below: a composite's own
            // top-level Kind/Param are unused filler (see ChallengeDefinition.Subs), so the
            // simple-path checks must never gate whether a composite's subs get a look at this
            // report.
            CreditMeasureSub(a, kind, param, value);

            if (a.Def.Kind != kind) return;

            // CollectItem, StatDelta and BuildPiece are the param-scoped measures: each tracks ONE
            // named thing (an item, a lifetime stat, a category of building piece), so a report
            // about a different one must not touch it. Every other kind (altitude, build height,
            // no-armor minutes, CollectFood) is a single world-wide quantity and ignores param
            // entirely.
            if ((kind == ChallengeKind.CollectItem || kind == ChallengeKind.StatDelta ||
                 kind == ChallengeKind.BuildPiece) &&
                a.Def.Param != param) return;

            a.Progress = Math.Max(a.Progress, value);
        }

        /// <summary>
        /// Credits a composite's KillPrefab subs matching <paramref name="prefab"/>: +1, capped
        /// at that sub's own target. A kill is an EVENT, not a measured quantity, so this uses
        /// increment-and-cap rather than <see cref="CreditMeasureSub"/>'s max-semantics — the same
        /// distinction <see cref="ReportKill"/> and <see cref="ReportMeasure"/> already draw for
        /// simple challenges.
        /// </summary>
        private static void CreditKillSub(ActiveChallenge a, string prefab)
        {
            if (a.Def.Subs == null || a.Def.Subs.Count == 0 || a.SubProgress == null) return;

            for (int i = 0; i < a.Def.Subs.Count; i++)
            {
                var sub = a.Def.Subs[i];
                if (sub.Kind != ChallengeKind.KillPrefab || sub.Param != prefab) continue;
                if (i >= a.SubProgress.Count) continue;

                a.SubProgress[i] = Math.Min(sub.Target, a.SubProgress[i] + 1f);
            }
        }

        /// <summary>
        /// Credits a composite's CollectItem/CollectFood/BuildPiece subs from a measure report, with
        /// the same max-semantics and param-scoping <see cref="ReportMeasure"/> uses for simple
        /// challenges. KillPrefab subs are not touched here — see <see cref="CreditKillSub"/>.
        ///
        /// BuildPiece qualifies on the rule composites actually require (see
        /// <see cref="ChallengeDefinition.Subs"/>): it is an absolute quantity needing no per-sub
        /// <see cref="ActiveChallenge.Baseline"/>. StatDelta stays excluded for exactly the reason
        /// it always was — one Baseline per slot, not one per sub.
        /// </summary>
        private static void CreditMeasureSub(ActiveChallenge a, ChallengeKind kind, string param, float value)
        {
            if (a.Def.Subs == null || a.Def.Subs.Count == 0 || a.SubProgress == null) return;
            if (kind != ChallengeKind.CollectItem && kind != ChallengeKind.CollectFood &&
                kind != ChallengeKind.BuildPiece) return;

            for (int i = 0; i < a.Def.Subs.Count; i++)
            {
                var sub = a.Def.Subs[i];
                if (sub.Kind != kind) continue;
                if ((kind == ChallengeKind.CollectItem || kind == ChallengeKind.BuildPiece) &&
                    sub.Param != param) continue;
                if (i >= a.SubProgress.Count) continue;

                a.SubProgress[i] = Math.Max(a.SubProgress[i], Math.Min(sub.Target, value));
            }
        }

        /// <summary>
        /// Reports progress to ONE slot, with the same max-semantics as
        /// <see cref="ReportMeasure"/>. Out-of-range indices are ignored.
        ///
        /// Exists because param-scoping isn't fine enough for
        /// <see cref="ChallengeKind.StatDelta"/>. Two slots can measure the same stat with
        /// DIFFERENT baselines — the pool holds one definition per stat, but a resumed run can
        /// restore a half-done "chop trees" alongside a freshly dealt one, and the caller computes
        /// a separate delta for each. A param-scoped report would hand both slots whichever delta
        /// was computed last, silently crediting the newer slot with progress the older one earned.
        /// The caller knows which slot each number belongs to, so it says so.
        /// </summary>
        public void ReportSlotMeasure(int slotIndex, float value)
        {
            // The main quest has its own baseline for exactly the same reason a random slot does,
            // so it needs the same slot-addressed report — see MainQuestSlot.
            if (slotIndex == MainQuestSlot)
            {
                if (mainQuest != null) mainQuest.Progress = Math.Max(mainQuest.Progress, value);
                return;
            }

            if (slotIndex < 0 || slotIndex >= active.Count) return;

            var a = active[slotIndex];
            a.Progress = Math.Max(a.Progress, value);
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
            RestoreActive(idToProgress, null, null);

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
        public void RestoreActive(IEnumerable<KeyValuePair<string, float>> idToProgress, IList<float> baselines) =>
            RestoreActive(idToProgress, baselines, null);

        /// <summary>
        /// As <see cref="RestoreActive(IEnumerable{KeyValuePair{string, float}}, IList{float})"/>,
        /// additionally restoring each composite slot's <see cref="ActiveChallenge.SubProgress"/>.
        ///
        /// <paramref name="subProgress"/> follows the same SAVED-sequence indexing as
        /// <paramref name="baselines"/> — an entry dropped as unknown/duplicate/over-cap still
        /// consumes its index. Each element is that slot's per-sub values, in
        /// <see cref="ChallengeDefinition.Subs"/> order; missing, null, or short entries read as
        /// zero for the uncovered subs rather than throwing — a hand-edited or pre-composite save
        /// must never crash a resume, it just restarts those subs at zero.
        /// </summary>
        public void RestoreActive(
            IEnumerable<KeyValuePair<string, float>> idToProgress,
            IList<float> baselines,
            IList<List<float>> subProgress)
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
                    Baseline = baselines != null && index < baselines.Count ? baselines[index] : float.NaN,
                    SubProgress = BuildSubProgress(
                        def, subProgress != null && index < subProgress.Count ? subProgress[index] : null)
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
        ///
        /// A <see cref="ChallengeDefinition.Repeatable"/> slot refuses outright. It is the run's
        /// standing task, and since openers can never be redrawn, spending heat to reroll it would
        /// destroy it permanently — a purchase no player would knowingly make.
        /// </summary>
        public bool Reroll(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= active.Count) return false;
            if (active[slotIndex].Def.Repeatable) return false;
            var options = Drawable();
            if (options.Count == 0) return false;
            active[slotIndex] = MakeActive(options[rng.Next(options.Count)]);
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
            return pool.Where(d => !d.Opener && !taken.Contains(d.Id) && d.Tier <= MaxTier
                              && (ExternalFilter == null || ExternalFilter(d))).ToList();
        }

        /// <summary>
        /// Fresh ActiveChallenge for a just-dealt/rerolled-into slot: zeroed progress, and — for a
        /// composite definition — a zeroed SubProgress list allocated to Subs.Count. Every path
        /// that deals a NEW slot (Tick's opener/random draw, Tick's refill draw, Reroll) goes
        /// through here so none of them can forget the allocation.
        /// </summary>
        private static ActiveChallenge MakeActive(ChallengeDefinition def) => new ActiveChallenge
        {
            Def = def,
            SubProgress = BuildSubProgress(def, null)
        };

        /// <summary>
        /// A composite definition's per-sub progress list, seeded from <paramref name="saved"/>
        /// where possible: each sub reads its saved value if there is one, else zero. Null for a
        /// non-composite definition. Never throws on a null/short/over-long saved list — it is
        /// simply padded with zeros or truncated to Subs.Count.
        /// </summary>
        private static List<float> BuildSubProgress(ChallengeDefinition def, IList<float> saved)
        {
            if (def.Subs == null || def.Subs.Count == 0) return null;

            var result = new List<float>(def.Subs.Count);
            for (int i = 0; i < def.Subs.Count; i++)
                result.Add(saved != null && i < saved.Count ? saved[i] : 0f);
            return result;
        }
    }
}
