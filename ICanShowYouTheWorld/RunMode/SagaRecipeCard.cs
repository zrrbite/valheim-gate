using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>One ingredient on a recipe card: what it is, how many are wanted, how many are held.</summary>
    public struct SagaIngredient
    {
        /// <summary>Display name, already localised by whoever built the card.</summary>
        public string Name;

        public int Need;
        public int Have;

        public bool Met => Have >= Need;
    }

    /// <summary>
    /// A saga recipe, as the FORGE page needs to show it.
    /// </summary>
    /// <remarks>
    /// A plain data type in <c>RunMode/</c> rather than a tuple in the window, for the reason every
    /// other engine type is here: the window is game-coupled and untestable, and "can this be made"
    /// is arithmetic.
    ///
    /// The page exists because the recipes were only ever visible one at a time, as the hint on the
    /// step currently in play (owner: "I sometimes found myself wondering what the recipes were").
    /// A hint answers "what is next"; it cannot answer "what should I be hoarding", which is the
    /// question a player actually has while out in the world with a full pack.
    /// </remarks>
    public sealed class SagaRecipeCard
    {
        /// <summary>The item this makes, localised.</summary>
        public string Item;

        /// <summary>Where it is made, in words the player can act on - "Storm-Anvil, in rain".</summary>
        public string Station;

        /// <summary>
        /// False while the recipe is gated behind a questline step that is not done yet.
        /// </summary>
        /// <remarks>
        /// Shown anyway, marked. Hiding the bill is what caused the complaint this page answers: a
        /// player cannot gather for a craft they are not allowed to read. What is withheld is the
        /// recipe's EXISTENCE in the game, which is the quest's business, not the page's.
        /// </remarks>
        public bool Known;

        public List<SagaIngredient> Bill = new List<SagaIngredient>();

        /// <summary>True when every ingredient is held. An empty bill is not ready; it is unknown.</summary>
        public bool Ready
        {
            get
            {
                if (Bill == null || Bill.Count == 0) return false;

                foreach (var part in Bill)
                    if (!part.Met) return false;

                return true;
            }
        }

        /// <summary>How many ingredients are satisfied, for a "3 of 5" summary.</summary>
        public int Met
        {
            get
            {
                int met = 0;
                if (Bill == null) return 0;

                foreach (var part in Bill)
                    if (part.Met) met++;

                return met;
            }
        }
    }
}
