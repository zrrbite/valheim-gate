namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Whether a kill was Thor's bow's (or Last Light's), for the bow's kill count and the two pool
    /// tasks that read it.
    /// </summary>
    /// <remarks>
    /// The count used to require LIGHTNING in the victim's last hit, on the reasoning that no other
    /// bow does lightning. True, but the Hunter's Elemental Arrows turn the bow to fire or frost,
    /// and from then on nothing it killed was counted - "Thunder at range" became unwinnable for
    /// exactly the player most likely to be dealt it.
    ///
    /// The game's HitData carries no weapon, so the bow is recognised from the player's side
    /// instead: the saga already watches every arrow Thor's bow looses (SagaItems.TickStrikes), and
    /// stamps the moment the last one left the string. A Bows-skill kill by the player within a few
    /// seconds of a storm arrow is the storm's. Drawing any other bow clears the stamp, so a plain
    /// bow's arrow loosed after a storm arrow is not mistaken for one. Lightning in the hit still
    /// counts on its own, as it always did.
    ///
    /// Still never "was the player holding the bow when something died" - the skill and attacker
    /// checks keep a wolf's kill from being credited to the archer standing next to it.
    /// </remarks>
    internal static class StormBowCredit
    {
        /// <summary>How long after a storm arrow leaves the string a kill can still be its.</summary>
        public const float ArrowSeconds = 8f;

        public static bool Credits(bool byPlayer, bool bowSkill, float lightning, float secondsSinceStormArrow) =>
            byPlayer && bowSkill &&
            (lightning > 0f || (secondsSinceStormArrow >= 0f && secondsSinceStormArrow <= ArrowSeconds));
    }
}
