using System.Collections.Generic;

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
        /// This act's questline, handed to <see cref="ChallengeEngine.SetMainChain"/> when the act
        /// becomes current. Every chain ends with its own boss kill, so a chain running out and the
        /// act ending are the same event — except in the final act, where running out means the
        /// saga is done.
        /// </summary>
        public List<ChallengeDefinition> Chain = new List<ChallengeDefinition>();

        /// <summary>"ACT II — THE BLACK FOREST", the banner the HUD and the announcement use.</summary>
        public string Banner => $"ACT {Numeral} — {Title.ToUpperInvariant()}";

        /// <summary>"Act II — The Black Forest", for prose contexts.</summary>
        public string Label => $"Act {Numeral} — {Title}";
    }
}
