using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Which of a step's lines are owed right now — its <see cref="ChallengeDefinition.Opening"/>,
    /// its <see cref="ChallengeDefinition.Hint"/>, or both.
    ///
    /// PURE, deliberately, and for the reason the gate predicates were extracted: everything in
    /// this mode that stayed behind an interface has kept working, and everything that read
    /// Player.m_localPlayer has needed a play session to find out it was broken.
    ///
    /// The rule is one sentence — say a step's line the first time it becomes the step in play,
    /// once per run — and it has exactly one trap in it. A RESUME seats a step that has been
    /// current for an hour, and announcing it again would open the session with a line about
    /// something the player finished thinking about long ago. So the FIRST observation only
    /// establishes a baseline and says nothing; every change after that speaks.
    ///
    /// That is the same shape as ForestWatch's chop baseline, and for the same reason: this class
    /// cannot tell "just happened" from "was already true", so it refuses to guess and treats
    /// whatever it finds on arrival as history.
    ///
    /// Hints count as something to say (2026-09-19). A Hint answers "what does this step actually
    /// NEED", and it existed only in the Run window — which a player deep in a build is not
    /// looking at. The hints were written for failures already seen in play ("settle in" needing a
    /// fire, a cooking station going ON the fire), so a hint nobody reads is a failure already paid
    /// for and not yet fixed. A step with both says its opening first and its hint after a pause;
    /// see RunService.PollStepOpenings, since the pause is a Unity concern and this class is pure.
    /// </summary>
    public class StepOpenings
    {
        private readonly HashSet<string> _seen = new HashSet<string>();
        private bool _baselined;

        /// <summary>
        /// Records what is in play and returns the lines owed for anything newly in play.
        ///
        /// Steps with nothing to say are still recorded — they are not owed a line, but they have
        /// been seen, and a step that came back would not be new.
        /// </summary>
        public List<ChallengeDefinition> Observe(IEnumerable<ChallengeDefinition> live)
        {
            var owed = new List<ChallengeDefinition>();

            foreach (var def in (live ?? Enumerable.Empty<ChallengeDefinition>())
                         .Where(d => d != null && !string.IsNullOrEmpty(d.Id)))
            {
                if (!_seen.Add(def.Id)) continue;
                if (_baselined && HasSomethingToSay(def)) owed.Add(def);
            }

            _baselined = true;
            return owed;
        }

        /// <summary>True when this step has any line worth saying as it opens.</summary>
        public static bool HasSomethingToSay(ChallengeDefinition def) =>
            def != null && (!string.IsNullOrEmpty(def.Opening) || !string.IsNullOrEmpty(def.Hint));

        /// <summary>
        /// The FIRST line a step says: its opening if it has one, otherwise its hint.
        ///
        /// Order matters and is not arbitrary. An opening is a statement about the world and reads
        /// as one; a hint is an instruction. A step with both leads with the statement, because a
        /// step that opens by telling you what to do has no moment left to be about anything.
        /// </summary>
        public static string LineFor(ChallengeDefinition def) =>
            def == null ? null
                : !string.IsNullOrEmpty(def.Opening) ? def.Opening
                : def.Hint;
    }
}
