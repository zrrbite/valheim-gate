using System.Collections.Generic;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The arithmetic behind the FORGE page.
///
/// Small, and worth pinning for one reason: <c>Ready</c> is the only thing on that page a player
/// will act on. A card that says "ready" and is not sends them home across a map for nothing,
/// which is a worse outcome than the page not existing — and the ordering that puts ready cards
/// first is driven by the same two properties.
/// </summary>
static class SagaRecipeCardTests
{
    static SagaRecipeCard Card(params (int need, int have)[] bill)
    {
        var card = new SagaRecipeCard { Item = "thing", Station = "somewhere", Known = true };
        foreach (var (need, have) in bill)
            card.Bill.Add(new SagaIngredient { Name = "part", Need = need, Have = have });

        return card;
    }

    public static void Run()
    {
        // An ingredient is met when you have AT LEAST as many as it wants. Surplus is not a problem:
        // the anvil takes what it needs and — this is the part that costs materials — burns the rest,
        // but that is the lever's warning to give, not this card's.
        Check.That(new SagaIngredient { Need = 3, Have = 3 }.Met, "exactly enough is enough");
        Check.That(new SagaIngredient { Need = 3, Have = 9 }.Met, "more than enough is enough");
        Check.That(!new SagaIngredient { Need = 3, Have = 2 }.Met, "one short is short");
        Check.That(new SagaIngredient { Need = 0, Have = 0 }.Met, "a free ingredient is always met");

        Check.That(Card((10, 10), (5, 5)).Ready, "every part held is ready");
        Check.That(!Card((10, 10), (5, 4)).Ready, "one part short is not ready");
        Check.That(!Card((10, 0), (5, 0)).Ready, "nothing held is not ready");

        // An EMPTY bill is not ready. This is the case that matters, because it is what a card looks
        // like when the recipe could not be read at all — a station prefab that does not resolve, an
        // ingredient the game does not have. "Every ingredient is satisfied" is vacuously true of no
        // ingredients, and a page that answered "ready" there would be confidently wrong about the
        // exact recipes that are broken.
        Check.That(!Card().Ready, "an empty bill is unknown, not ready");
        Check.That(Card().Met == 0, "and nothing in it is met");

        // Met is the count the page shows as "3 of 5", and is what the ordering uses.
        Check.That(Card((1, 1), (1, 0), (1, 1)).Met == 2, "Met counts the satisfied parts");
        Check.That(Card((1, 1), (1, 1)).Met == 2, "including when that is all of them");

        // Defensive: the window walks Bill, and a null one must not be what discovers that.
        var wiped = Card((1, 1));
        wiped.Bill = null;
        Check.That(!wiped.Ready, "a null bill is not ready");
        Check.That(wiped.Met == 0, "and counts nothing rather than throwing");
    }
}
