using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Ship fittings bought with gold: the tiers, the prices, what each does, and what the helm's card
/// offers when - Fire-tar never before the Ashlands act.
/// </summary>
static class ShipFittingsTests
{
    public static void Run()
    {
        var none = new ShipFittingState();

        var first = ShipFittings.Offers(none, fireTarTold: false);
        Check.That(first.Count == 2 && first.All(o => o.Tier == 1 && o.Price == 50),
                   "a bare ship is offered Sail I and Hull I, at fifty coins each");
        Check.That(!first.Any(o => o.Kind == FittingKind.FireTar),
                   "no Fire-tar before the Ashlands act - offering it would name the boiling sea in Act II");
        Check.That(ShipFittings.Offers(none, fireTarTold: true).Any(o => o.Kind == FittingKind.FireTar && o.Price == 400),
                   "Fire-tar is offered once the Ashlands act is told, at four hundred");

        var sail2 = ShipFittings.Bought(ShipFittings.Bought(none, FittingKind.Sail), FittingKind.Sail);
        Check.That(sail2.Sail == 2 && sail2.Hull == 0, "buying raises one fitting a tier at a time");
        Check.That(ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Price == 300 &&
                   ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Tier == 3,
                   "the card offers the NEXT tier, at its own price");

        var full = new ShipFittingState { Sail = 3, Hull = 3, FireTar = 1 };
        Check.That(ShipFittings.Offers(full, true).Count == 0, "a fully fitted ship is offered nothing");
        Check.That(ShipFittings.Bought(full, FittingKind.Sail).Sail == 3 && ShipFittings.Bought(full, FittingKind.FireTar).FireTar == 1,
                   "buying past the top tier changes nothing");

        Check.That(ShipFittings.SailMultiplier(0) == 1f && ShipFittings.SailMultiplier(1) == 1.15f &&
                   ShipFittings.SailMultiplier(3) == 1.5f && ShipFittings.SailMultiplier(9) == 1.5f,
                   "sail and oars: +15/30/50%, clamped");
        Check.That(ShipFittings.HullDamageFactor(0) == 1f && ShipFittings.HullDamageFactor(2) == 0.5f &&
                   ShipFittings.HullDamageFactor(3) == 0.25f && ShipFittings.HullDamageFactor(-1) == 1f,
                   "hull damage x0.75/0.5/0.25, clamped");
        Check.That(!ShipFittings.AshlandsReady(none) && ShipFittings.AshlandsReady(full), "Fire-tar makes a ship Ashlands-ready");

        Check.That(ShipFittings.Summary(none) == "" &&
                   ShipFittings.Summary(new ShipFittingState { Sail = 2, Hull = 1 }) == "Sail II · Hull I" &&
                   ShipFittings.Summary(full) == "Sail III · Hull III · Fire-tar",
                   "the HUD's summary names what the ship carries, nothing for a bare one");
        Check.That(first.All(o => !string.IsNullOrEmpty(o.Name) && !string.IsNullOrEmpty(o.Effect)),
                   "every offer has a name and says what it does");
    }
}
