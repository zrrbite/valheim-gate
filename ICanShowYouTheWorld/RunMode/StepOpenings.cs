using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Which <see cref="ChallengeDefinition.Opening"/> lines are owed right now.
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
    /// </summary>
    public class StepOpenings
    {
        private readonly HashSet<string> _seen = new HashSet<string>();
        private bool _baselined;

        /// <summary>
        /// Records what is in play and returns the lines owed for anything newly in play.
        ///
        /// Steps with no <see cref="ChallengeDefinition.Opening"/> are still recorded — they are
        /// not owed a line, but they have been seen, and a step that came back would not be new.
        /// </summary>
        public List<ChallengeDefinition> Observe(IEnumerable<ChallengeDefinition> live)
        {
            var owed = new List<ChallengeDefinition>();

            foreach (var def in (live ?? Enumerable.Empty<ChallengeDefinition>())
                         .Where(d => d != null && !string.IsNullOrEmpty(d.Id)))
            {
                if (!_seen.Add(def.Id)) continue;
                if (_baselined && !string.IsNullOrEmpty(def.Opening)) owed.Add(def);
            }

            _baselined = true;
            return owed;
        }
    }
}
