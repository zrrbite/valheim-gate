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
        Check.That(first.Count == 3 && first.Where(o => o.Kind != FittingKind.WindHorn).All(o => o.Tier == 1 && o.Price == 50),
                   "a bare ship is offered Sail I and Hull I, at fifty coins each");
        Check.That(first.Any(o => o.Kind == FittingKind.WindHorn && o.Price == 200) && first[2].Kind == FittingKind.WindHorn,
                   "and the Wind-horn, at two hundred, third on the card");
        Check.That(!first.Any(o => o.Kind == FittingKind.FireTar),
                   "no Fire-tar before the Ashlands act - offering it would name the boiling sea in Act II");
        var told = ShipFittings.Offers(none, fireTarTold: true);
        Check.That(told.Count == 4 && told[3].Kind == FittingKind.FireTar && told[3].Price == 400,
                   "Fire-tar is offered once the Ashlands act is told, at four hundred, last - four keys at most");

        var sail2 = ShipFittings.Bought(ShipFittings.Bought(none, FittingKind.Sail), FittingKind.Sail);
        Check.That(sail2.Sail == 2 && sail2.Hull == 0, "buying raises one fitting a tier at a time");
        Check.That(ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Price == 300 &&
                   ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Tier == 3,
                   "the card offers the NEXT tier, at its own price");

        var full = new ShipFittingState { Sail = 3, Hull = 3, FireTar = 1, WindHorn = 1 };
        Check.That(ShipFittings.Offers(full, true).Count == 0, "a fully fitted ship is offered nothing");
        Check.That(ShipFittings.Bought(full, FittingKind.Sail).Sail == 3 && ShipFittings.Bought(full, FittingKind.FireTar).FireTar == 1 &&
                   ShipFittings.Bought(full, FittingKind.WindHorn).WindHorn == 1,
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
                   ShipFittings.Summary(full) == "Sail III · Hull III · Wind-horn · Fire-tar",
                   "the HUD's summary names what the ship carries, nothing for a bare one");
        Check.That(first.All(o => !string.IsNullOrEmpty(o.Name) && !string.IsNullOrEmpty(o.Effect)),
                   "every offer has a name and says what it does");
        Check.That(!ShipFittings.HasWindHorn(none) && ShipFittings.HasWindHorn(ShipFittings.Bought(none, FittingKind.WindHorn)),
                   "the horn is owned once bought");

        // The god's wind: the prow within forty degrees of the pinned altar, aboard, god pinned.
        Check.That(System.Math.Abs(SagaWind.Heading(0f, 1f)) < 0.01f && System.Math.Abs(SagaWind.Heading(1f, 0f) - 90f) < 0.01f &&
                   System.Math.Abs(SagaWind.Heading(-1f, 0f) - 270f) < 0.01f,
                   "heading: 0 along +z, 90 along +x, 270 along -x");
        Check.That(SagaWind.Between(350f, 10f) == 20f && SagaWind.Between(10f, 350f) == 20f && SagaWind.Between(0f, 180f) == 180f,
                   "the angle between headings wraps around north");
        Check.That(SagaWind.GodWindBlows(true, true, 10f, 45f) && SagaWind.GodWindBlows(true, true, 340f, 15f),
                   "a prow within forty degrees of the altar gets the god's wind, across north too");
        Check.That(!SagaWind.GodWindBlows(true, true, 90f, 0f), "a prow turned away sails the real wind");
        Check.That(!SagaWind.GodWindBlows(false, true, 0f, 0f) && !SagaWind.GodWindBlows(true, false, 0f, 0f),
                   "only aboard, and only once the god is pinned - it never points where the map does not");

        Check.That(SagaWind.HornCooldownLeft(100f, float.NegativeInfinity) == 0f, "a horn never blown is ready");
        Check.That(SagaWind.HornCooldownLeft(100f, 50f) == SagaWind.HornCooldownSeconds - 50f &&
                   SagaWind.HornCooldownLeft(50f + SagaWind.HornCooldownSeconds, 50f) == 0f,
                   "ten minutes to recover after a blow");
    }
}
