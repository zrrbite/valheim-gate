using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Haldor, given a voice for Act II of a run: a <see cref="TraderVoice"/> configuration.
    /// </summary>
    /// <remarks>
    /// WHAT HE KNOWS. His camp sits on the couriers' road. They pass every night, laden, and never
    /// stop - splinters carry light and do not take goods, which is why a trader in a starving
    /// forest is never robbed. The one thing he fears is a troll: in the saga, the only thing that
    /// BREAKS light instead of carrying it. Bring him a troll's head and he tells you where the
    /// couriers go, which is what puts the Elder's altar on the map.
    ///
    /// The give entry sets a run-scoped key (<see cref="SagaNames.HaldorKey"/>) and is offered only
    /// while his ask is the current step, so he cannot promise a pin the questline has not reached.
    /// </remarks>
    internal static class HaldorVoice
    {
        /// <summary>The troll's trophy, by prefab. Its drop chance is logged at run start.</summary>
        public const string TrophyPrefab = "TrophyForestTroll";

        public static readonly string[] Talk =
        {
            "They come past every night. Little ones, glowing like lanterns. Never once stop to trade.",
            "Funny thing. They carry more than they could ever eat, and they never look at my wares.",
            "I keep my lamps low after dark. Not for the little ones. For the big ones.",
            "Want to know where the light goes? I've watched them for years. It'll cost you a troll's head.",
        };

        /// <summary>Once he has told: the same talk without the ask, which would otherwise outlive its answer.</summary>
        public static readonly string[] TalkAfterTold = Talk.Take(3).ToArray();

        /// <summary>
        /// Said through his own accept line. "I've marked it" is backed: the altar step becomes
        /// current the moment the key is seen, and the pin follows on the same poll.
        /// </summary>
        public const string RevealLine =
            "Ha! That one won't be knocking my camp over. Fair's fair, then. Every night they go the same way — " +
            "deeper, to where the trees are oldest. There's a ring of stone in there, and something under it that's " +
            "never once come up to collect. I've marked it on your map.";

        public static TraderVoice Create() =>
            new TraderVoice("Haldor", "haldor", Talk, TalkAfterTold)
            {
                GivePrefab = TrophyPrefab,
                GiveDialog = RevealLine,
            };
    }

    /// <summary>
    /// The Bog Witch, given a voice for Act III: a <see cref="TraderVoice"/> configuration with a
    /// proximity step and the Stormward's reforge at her hands.
    /// </summary>
    /// <remarks>
    /// The one living thing in the marsh, because nothing keeps hold of her. Her talk is the act's
    /// preparation: Bonemass is decided before the fight. Her shop stays her own; the reforge is the
    /// alt-use, through <see cref="TraderAltTalk"/>.
    /// </remarks>
    internal static class BogWitchVoice
    {
        public static readonly string[] Talk =
        {
            "Nothing rots here. Did you notice? Nothing lets go long enough to rot.",
            "The marsh keeps everything. The iron, the water, the men. Even its smell.",
            "I live here because nothing here can keep hold of me. I don't let it.",
            "The big one is decided before you ever see him. Poison, child. Brew against it, or don't go.",
            "That shield of yours has a storm in it. Bring it to me with iron and old bark, and I'll make it heavier.",
        };

        /// <summary>Once the reforge is done, the shield line goes - it would outlive its answer.</summary>
        public static readonly string[] TalkAfter = Talk.Take(4).ToArray();

        public const string ReforgeLine =
            "There. Iron where the hide was, bark where the frame was, and the storm still in it. " +
            "It will hold longer than you will. Most things here do.";

        public const string ReforgeShortLine =
            "The shield, ten iron, ten of the old bark. All of it, or I can't help you.";

        /// <summary>What the reforge takes, by shared name. Validated at run start.</summary>
        public static readonly (string token, int amount, string label)[] ReforgePrice =
        {
            (SagaItems.StormwardName, 1, "Stormward"),
            ("$item_iron", 10, "iron"),
            ("$item_elderbark", 10, "ancient bark"),
        };

        public static TraderVoice Create() =>
            new TraderVoice("The Bog Witch", "bogwitch", Talk, TalkAfter)
            {
                MetRange = 6f,
                AltHover = "Reforge the Stormward",
            };
    }
}
