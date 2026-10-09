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

        Check.That(ShipFittings.Offers(new ShipFittingState(), false, true, saefari: true).Select(o => o.Price)
                       .SequenceEqual(new[] { 25, 25, 100, 50 }),
                   "a Sæfari pays half: Sail I 25, Hull I 25, the Wind-horn 100, Ward I 50");

        // Her card is hers (task 8 review): her ship sails a tier above what is fitted, so the card states the tier it
        // will sail at, and never offers a tier III that already sails.
        var mixed = ShipFittings.TierAbove(new ShipFittingState { Sail = 0, Hull = 1, Ward = 2, FireTar = 1, WindHorn = 1 });
        Check.That(mixed.Sail == 1 && mixed.Hull == 2 && mixed.Ward == 3 && mixed.FireTar == 1 && mixed.WindHorn == 1,
                   "a Sæfari's ship sails a tier above each of Sail, Hull and Ward; the rest is as bought");
        var topped = ShipFittings.TierAbove(new ShipFittingState { Sail = 3, Hull = 3, Ward = 3 });
        Check.That(topped.Sail == 3 && topped.Hull == 3 && topped.Ward == 3 && topped.FireTar == 0 && topped.WindHorn == 0,
                   "and stays at III");
        var bare = ShipFittings.TierAbove(null);
        Check.That(bare.Sail == 1 && bare.Hull == 1 && bare.Ward == 1 && bare.FireTar == 0 && bare.WindHorn == 0,
                   "a bare ship's tier above is I; no state is a bare state");

        var holdsSail2 = new ShipFittingState { Sail = 2 };
        Check.That(!ShipFittings.Offers(holdsSail2, false, saefari: true).Any(o => o.Kind == FittingKind.Sail),
                   "a Sæfari holding Sail II is offered no Sail: II already sails at III");
        Check.That(ShipFittings.Offers(holdsSail2, false).Any(o => o.Kind == FittingKind.Sail && o.Tier == 3),
                   "while anyone else is still offered Sail III");
        var wardII = new ShipFittingState { Ward = 2 };
        Check.That(!ShipFittings.Offers(wardII, false, true, saefari: true).Any(o => o.Kind == FittingKind.Ward) &&
                   !ShipFittings.Offers(new ShipFittingState { Hull = 2 }, false, saefari: true).Any(o => o.Kind == FittingKind.Hull),
                   "nor Hull III or Ward III");
        var herSail = ShipFittings.Offers(none, false, saefari: true).First(o => o.Kind == FittingKind.Sail);
        Check.That(herSail.Tier == 1 && herSail.Price == 25 && herSail.Effect.StartsWith("+30%"),
                   "her Sail I (25 coins) states tier II's push, +30%, because that is what her ship will sail at");
        var herSail2 = ShipFittings.Offers(new ShipFittingState { Sail = 1 }, false, saefari: true).First(o => o.Kind == FittingKind.Sail);
        Check.That(herSail2.Tier == 2 && herSail2.Price == 75 && herSail2.Effect.StartsWith("+50%"),
                   "her Sail II (half of 150, rounded up) states III's +50%");
        Check.That(ShipFittings.Offers(none, false, saefari: true).First(o => o.Kind == FittingKind.Hull).Effect == "the ship takes half the damage",
                   "her Hull I states tier II's half");
        Check.That(ShipFittings.Offers(none, false, true, saefari: true).First(o => o.Kind == FittingKind.Ward).Effect.Contains("within 25 m"),
                   "her Ward I states tier II's 25 m");
        Check.That(ShipFittings.Offers(none, false, true).First(o => o.Kind == FittingKind.Ward).Effect.Contains("within 20 m") &&
                   ShipFittings.Offers(none, false).First(o => o.Kind == FittingKind.Sail).Effect.StartsWith("+15%"),
                   "everyone else's card states the tier it sells");
        // The Ward's line says where it works: aboard for everyone, and for the Sæfari ashore too, a tier weaker.
        Check.That(ShipFittings.Offers(none, false, true).First(o => o.Kind == FittingKind.Ward).Effect ==
                   "aboard: lightning strikes attackers within 20 m every 3 s",
                   "everyone else's Ward works only aboard, and says so");
        Check.That(ShipFittings.Offers(none, false, true, saefari: true).First(o => o.Kind == FittingKind.Ward).Effect ==
                   "lightning strikes attackers within 25 m every 3 s, a tier weaker on land",
                   "hers walks with her: the sea's tier, and a tier weaker on land");
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

        // Who the ward strikes: attackers only. BaseAI.IsEnemy(player, c) is true for nearly every wild creature,
        // so "an enemy" alone would kill the deer by the dock and the boar being tamed (final review, 2026-10-08).
        var attacker = new WardTarget { Enemy = true, Monster = true, Alerted = true };
        Check.That(ShipFittings.WardStrikes(attacker), "an alerted hostile monster - the sea's own, a hunting troll - is struck");
        Check.That(!ShipFittings.WardStrikes(new WardTarget { Enemy = true, Monster = true, Alerted = false }),
                   "a calm one is not: a wild boar or wolf, and the one being tamed (taming needs it calm)");
        Check.That(!ShipFittings.WardStrikes(new WardTarget { Enemy = true, Monster = false, Alerted = true }),
                   "nor an animal - a deer or hare's alert is flight, not attack");
        Check.That(!ShipFittings.WardStrikes(new WardTarget { Player = true, Enemy = true, Monster = true, Alerted = true }) &&
                   !ShipFittings.WardStrikes(new WardTarget { Tamed = true, Enemy = true, Monster = true, Alerted = true }) &&
                   !ShipFittings.WardStrikes(new WardTarget { Dead = true, Enemy = true, Monster = true, Alerted = true }) &&
                   !ShipFittings.WardStrikes(new WardTarget { Enemy = false, Monster = true, Alerted = true }) &&
                   !ShipFittings.WardStrikes(new WardTarget { LightningImmune = true, Enemy = true, Monster = true, Alerted = true }),
                   "and never a player, the tamed, the dead, a friend, or what lightning cannot hurt (the saga's speakers)");

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
