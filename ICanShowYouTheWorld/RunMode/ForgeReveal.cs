using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Which of the Storm-Anvil's shapes the FORGE page may show.
    /// </summary>
    /// <remarks>
    /// The bench's recipes were hidden until Hugin announced them (owner, 2026-09-28), and the
    /// anvil's were left out of that rule on the argument that the anvil is a place the player has
    /// found, not a thing they were told. That held while the anvil made only Act I's two weapons.
    /// It stopped holding when the anvil learned to MEND the storm shields (2026-10-05): the
    /// Ironbound Stormward's repair is a conversion like any other, so the page gave Act III's
    /// shield a card on the first night (owner: "'Forge' still reveals the recipes of items that
    /// havent been discovered - major spoiler").
    ///
    /// So the anvil follows the bench's rule now: a shape is shown once the step that TEACHES it
    /// has opened, and a repair - the shield alone in the box - is not a recipe at all and is never
    /// shown. The shields' own descriptions say where they are mended.
    /// </remarks>
    internal static class ForgeReveal
    {
        /// <summary>A repair: the result alone in the box, giving itself back whole.</summary>
        public static bool IsRepair(string result, IReadOnlyList<string> bill) =>
            !string.IsNullOrEmpty(result) && bill != null && bill.Count == 1 && bill[0] == result;

        /// <summary>
        /// Whether a conversion gets a card. <paramref name="taughtBy"/> is the step whose opening
        /// teaches the shape; none means nobody teaches it, and an untaught shape is a secret - a
        /// conversion added later without a teacher stays off the page rather than spoiling it.
        /// </summary>
        public static bool ShowAnvilCard(string result, IReadOnlyList<string> bill, string taughtBy, Func<string, bool> reached) =>
            !IsRepair(result, bill) && !string.IsNullOrEmpty(taughtBy) && reached != null && reached(taughtBy);
    }
}
