using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// "Is this kind of step in play right now?" — the questions the act's live systems ask before
    /// doing anything.
    ///
    /// PURE, and that is the entire point. Every one of these lived as a property on RunService,
    /// reading the same tracks but sitting in game-coupled code the unit harness cannot compile.
    /// Three shipped bugs came from exactly that blind spot, and each was one assertion away from
    /// being caught before a play session:
    ///
    ///   - DeerHuntWanted accepted only KillPrefab, so when the race step became a PlayerEvent the
    ///     hunt's own gate said the hunt was off: no lights, no packs, for nine versions.
    ///   - The spirit's rumour, bar and whispers keyed on SpiritChase.Found rather than the step,
    ///     so a completed chase kept pointing at nothing and shadowed the Herald's bearing.
    ///   - The light race's score narrated itself through fights that were not the race.
    ///
    /// A predicate that can be tested cannot rot quietly. Everything behind an interface in this
    /// mode has stayed correct all session; everything reading Player.m_localPlayer has not.
    /// </summary>
    public static class StepPredicates
    {
        /// <summary>The steps actually in play: each track's current, minus anything blocked.</summary>
        public static IEnumerable<ChallengeDefinition> Live(IReadOnlyList<QuestTrack> tracks)
        {
            if (tracks == null) yield break;

            foreach (var t in tracks)
            {
                if (t == null || t.Current == null || t.Blocked) continue;
                if (t.Current.Def != null) yield return t.Current.Def;
            }
        }

        /// <summary>
        /// A deer hunt is in play — the light race INCLUDED.
        ///
        /// The race is what the hunt IS since alpha61; a gate that only knew about kills was the
        /// bug that switched the act's centrepiece off.
        /// </summary>
        public static bool DeerHunt(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d =>
                (d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.LightTaken) ||
                (d.Kind == ChallengeKind.KillPrefab &&
                 (d.Param == SagaNames.Deer ||
                  d.Param == SagaNames.NightDeerKill ||
                  d.Param == SagaNames.HeraldKill)));

        /// <summary>Any act's light race. Act I hunts the herd; Act II robs the couriers.</summary>
        public static bool LightRace(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.LightTaken);

        /// <summary>The pale-light chase. Every spirit-facing surface must key on THIS.</summary>
        public static bool SpiritChase(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerState && d.Param == SagaNames.SpiritFound);

        public static bool Herald(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.KillPrefab && d.Param == SagaNames.HeraldKill);

        public static bool Gatherer(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.KillPrefab && d.Param == SagaNames.GathererKill);

        public static bool CourierIntercept(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Id == SagaNames.InterceptStepId);

        /// <summary>The hunter's shade should be standing: either of its steps is in play.</summary>
        public static bool Shade(IReadOnlyList<QuestTrack> tracks) =>
            ShadeFind(tracks) || ShadeDelivery(tracks);

        public static bool ShadeFind(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.ShadeFound);

        public static bool ShadeDelivery(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.ShadeDelivered);

        /// <summary>
        /// A step is DONE when some track has moved past it: its chain holds the id at a position
        /// below the track's index. Absent from every chain is not done — an act that dropped the
        /// step must not unlock what the step guards.
        ///
        /// Derived from the tracks rather than remembered, so a resume recomputes it and a
        /// dev-skip counts the same as an honest completion.
        /// </summary>
        public static bool StepDone(IReadOnlyList<QuestTrack> tracks, string stepId)
        {
            if (tracks == null || string.IsNullOrEmpty(stepId)) return false;

            foreach (var t in tracks)
            {
                if (t == null || t.Chain == null) continue;
                int at = t.Chain.FindIndex(d => d != null && d.Id == stepId);
                if (at >= 0 && t.Index > at) return true;
            }

            return false;
        }

        /// <summary>
        /// A step only the DARK can finish. The strip refuses to point at anything while this is
        /// true and the sun is up, and the act says so outright rather than letting a hunt
        /// silently refuse to count.
        /// </summary>
        public static bool DarkStep(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d =>
                (d.Kind == ChallengeKind.PlayerState && d.Param == SagaNames.SpiritFound) ||
                (d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.LightTaken) ||
                // The night watch is the act's FIRST dark step, and it belongs here for the same
                // reason as the rest: the whisper that advances it only speaks after sundown, so
                // without this the strip would never tell the player to wait for the dark — and a
                // step that cannot progress and does not say why is the worst thing this design does.
                //
                // Note it is deliberately NOT in DeerHunt: an early clause landed there by mistake
                // and the tests caught it immediately. The vigil brings no pack and no starred deer;
                // there is nothing being hunted yet, which is the whole point of it.
                (d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.NightWatch) ||
                (d.Kind == ChallengeKind.KillPrefab &&
                 (d.Param == SagaNames.NightDeerKill || d.Param == SagaNames.HeraldKill)));
    }
}
