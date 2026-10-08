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
                   "Fire-tar is offered once the Ashlands act is told, at four hundred, last");

        var sail2 = ShipFittings.Bought(ShipFittings.Bought(none, FittingKind.Sail), FittingKind.Sail);
        Check.That(sail2.Sail == 2 && sail2.Hull == 0, "buying raises one fitting a tier at a time");
        Check.That(ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Price == 300 &&
                   ShipFittings.Offers(sail2, false).First(o => o.Kind == FittingKind.Sail).Tier == 3,
                   "the card offers the NEXT tier, at its own price");

        var full = new ShipFittingState { Sail = 3, Hull = 3, FireTar = 1, WindHorn = 1, Ward = 3 };
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
                   ShipFittings.Summary(full) == "Sail III · Hull III · Ward III · Wind-horn · Fire-tar",
                   "the HUD's summary names what the ship carries, nothing for a bare one");
        Check.That(first.All(o => !string.IsNullOrEmpty(o.Name) && !string.IsNullOrEmpty(o.Effect)),
                   "every offer has a name and says what it does");
        Check.That(!ShipFittings.HasWindHorn(none) && ShipFittings.HasWindHorn(ShipFittings.Bought(none, FittingKind.WindHorn)),
                   "the horn is owned once bought");

        // The ward (2026-10-08): offered once the raven has spoken of the sea, between the horn and Fire-tar.
        Check.That(!ShipFittings.Offers(none, true).Any(o => o.Kind == FittingKind.Ward),
                   "no Ward before the raven's sea line - the player would not know what it is for");
        var warded = ShipFittings.Offers(none, true, wardTold: true);
        Check.That(warded.Count == 5 && warded[3].Kind == FittingKind.Ward && warded[3].Tier == 1 && warded[3].Price == 100 &&
                   warded[3].Name == "Ward I" && warded[4].Kind == FittingKind.FireTar,
                   "then Ward I, at a hundred, after the horn and before Fire-tar: five lines at most");
        var ward2 = ShipFittings.Bought(ShipFittings.Bought(none, FittingKind.Ward), FittingKind.Ward);
        Check.That(ward2.Ward == 2 && ShipFittings.Offers(ward2, false, true).First(o => o.Kind == FittingKind.Ward).Price == 450 &&
                   ShipFittings.Bought(full, FittingKind.Ward).Ward == 3 && !ShipFittings.Offers(full, true, true).Any(),
                   "Ward II costs 250, III 450, and III is the top");
        Check.That(ShipFittings.Offers(new ShipFittingState { Ward = 1 }, false, true).First(o => o.Kind == FittingKind.Ward).Price == 250,
                   "the card offers the ward's NEXT tier");
        Check.That(ShipFittings.WardRadius(0) == 0f && ShipFittings.WardRadius(1) == 20f && ShipFittings.WardRadius(3) == 30f &&
                   ShipFittings.WardRadius(9) == 30f && ShipFittings.WardDamage(0) == 0f && ShipFittings.WardDamage(1) == 20f &&
                   ShipFittings.WardDamage(2) == 40f && ShipFittings.WardDamage(3) == 70f && ShipFittings.WardPulseSeconds == 3f,
                   "the ward: 20/25/30 m, 20/40/70 lightning every 3 s, clamped");
        Check.That(ShipFittings.Tier(new ShipFittingState { Ward = 2 }, FittingKind.Ward) == 2 &&
                   ShipFittings.Summary(new ShipFittingState { Sail = 1, Ward = 1 }) == "Sail I · Ward I",
                   "the ward has a tier and shows in the summary");

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
