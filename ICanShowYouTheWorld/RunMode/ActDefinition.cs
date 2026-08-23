using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// One act of the saga: a named stretch of the run, its questline, and the boss that ends it.
    ///
    /// Acts exist because the run used to stop having a thread the moment Eikthyr fell — the
    /// questline ran out and the HUD printed a hardcoded "Act I complete" with nothing after it.
    /// An act is simply the next chain plus a name to put on it.
    ///
    /// Which act is current is NOT stored here or anywhere else: it is derived from how many bosses
    /// the world has recorded as dead (see RunService.CurrentActIndex). Deriving it means it cannot
    /// drift from the world, a run started on a world that already killed Eikthyr correctly begins
    /// in Act II, and a resume recomputes it rather than trusting a save.
    /// </summary>
    public class ActDefinition
    {
        /// <summary>Stable id, e.g. "act2". Used in logs and tests, never shown to the player.</summary>
        public string Id;

        /// <summary>Roman numeral shown in the HUD, e.g. "II".</summary>
        public string Numeral;

        /// <summary>The act's name, e.g. "The Black Forest".</summary>
        public string Title;

        /// <summary>
        /// The global key set when this act's boss dies — the same string the boss table holds.
        /// This is what makes an act's END observable: the act is over precisely when the world
        /// says this key is set.
        /// </summary>
        public string BossDefeatKey;

        /// <summary>
        /// This act's questlines, handed to <see cref="ChallengeEngine.SetTracks"/> when the act
        /// becomes current. Two of them: HUNT and CRAFT.
        ///
        /// They run side by side because one chain forced an order — to reach the next kill you had
        /// to build the next building. Two tracks let the player choose which thread to pull, and
        /// since every step pays heat, that choice is the difficulty dial: pursue both and you are
        /// stronger but hotter, rush the boss and you are safer with a lower score.
        ///
        /// The BOSS lives on the hunt track, which is what still makes "the act is over" observable:
        /// the act flips on the world's defeated-boss count either way, so an unfinished craft track
        /// is simply unfinished. Rushing has a real cost, and nothing new has to be persisted.
        /// </summary>
        public List<QuestTrack> Tracks = new List<QuestTrack>();

        /// <summary>
        /// Which tracks should be seated for <paramref name="actIndex"/>: the act's own, plus any
        /// UNFINISHED track from an earlier act whose id this act does not reuse.
        ///
        /// It exists because an act ends when its boss dies, and the boss lives on the hunt track.
        /// With tracks of unequal length, following the shortest one to the end would discard
        /// whatever the others had left — and since a player can summon a boss whenever they like,
        /// no questline gate could prevent that. Carrying the work forward can.
        ///
        /// Only an id the new act does not reuse may carry: "hunt" and "craft" exist in every act,
        /// so a leftover would collide with the new act's own track on save. In practice that
        /// means Act I's hearth, which is exactly the homestead work this protects.
        ///
        /// <paramref name="live"/> is the current seating, empty at run start. Empty means "seat
        /// it and let the save decide"; a track present but exhausted, or absent entirely, has
        /// already been finished or dropped and does not come back.
        /// </summary>
        public static List<QuestTrack> SeatingFor(
            IList<ActDefinition> acts, int actIndex, IList<QuestTrack> live)
        {
            var seated = new List<QuestTrack>();
            if (acts == null || actIndex < 0 || actIndex >= acts.Count) return seated;

            seated.AddRange(acts[actIndex].Tracks.Where(t => t != null));

            for (int i = 0; i < actIndex; i++)
            {
                foreach (var t in acts[i].Tracks.Where(t => t != null))
                {
                    if (seated.Any(x => x.Id == t.Id)) continue;

                    if (live != null && live.Count > 0)
                    {
                        var already = live.FirstOrDefault(x => x != null && x.Id == t.Id);
                        if (already == null || already.Current == null) continue;
                    }

                    seated.Add(t);
                }
            }

            return seated;
        }

        /// <summary>Every step across every track — for validation and for the name manifest.</summary>
        public IEnumerable<ChallengeDefinition> AllSteps =>
            Tracks.Where(t => t != null && t.Chain != null).SelectMany(t => t.Chain);

        /// <summary>"ACT II — THE BLACK FOREST", the banner the HUD and the announcement use.</summary>
        public string Banner => $"ACT {Numeral} — {Title.ToUpperInvariant()}";

        /// <summary>"Act II — The Black Forest", for prose contexts.</summary>
        public string Label => $"Act {Numeral} — {Title}";
    }
}
