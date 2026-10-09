using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The ways' numbers (2026-10-08, class balance): docs/superpowers/specs/2026-10-08-class-balance-design.md.
/// </summary>
static class WayRulesTests
{
    public static void Run()
    {
        // The damage ceiling.
        Check.That(WayRules.WeaponProduct(new float[0]) == 1f, "no weapon bonus: x1");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 1.2f, 1.5f }) - 1.8f) < 0.001f, "bonuses multiply below the ceiling");
        Check.That(WayRules.WeaponProduct(new[] { 1.2f, 1.4f, 1.2f, 1.5f, 1.5f }) == 2.5f,
                   "Sharpened, Glass Cannon, Stoker, Reckless and Fury together stop at x2.5, not x4.5");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 0.8f }) - 0.8f) < 0.001f, "a factor below one still applies");
        Check.That(WayRules.WeaponProduct(null) == 1f, "nothing at all is x1, not an error");

        // Tempering: one per god from Bonemass to the Queen.
        Check.That(!WayRules.Tempered(2, TemperSlot.Rung1) && WayRules.Tempered(3, TemperSlot.Rung1) &&
                   !WayRules.Tempered(3, TemperSlot.Rung2) && WayRules.Tempered(4, TemperSlot.Rung2) &&
                   WayRules.Tempered(5, TemperSlot.Rung3) && !WayRules.Tempered(5, TemperSlot.Engine) &&
                   WayRules.Tempered(6, TemperSlot.Engine),
                   "Bonemass tempers rung I, Moder rung II, Yagluth rung III, the Queen the engine");
        Check.That(!WayRules.TemperShown(1, TemperSlot.Rung1) && WayRules.TemperShown(2, TemperSlot.Rung1) &&
                   !WayRules.TemperShown(2, TemperSlot.Rung2),
                   "a tempering is shown only once its god is the next to fall - no names from later acts");
        foreach (var way in ClassLadder.Catalog())
            foreach (TemperSlot slot in Enum.GetValues(typeof(TemperSlot)))
                Check.That(!string.IsNullOrEmpty(WayRules.TemperLine(way.Id, slot)), $"{way.Id} has a {slot} tempering line");
        Check.That(WayRules.TemperLine("nobody", TemperSlot.Rung1) == "", "an unknown way has none, not an error");

        // Menagerie's beasts by biome.
        Check.That(WayRules.MenagerieBeasts(0).SequenceEqual(new[] { "Boar", "Hen", "Chicken" }),
                   "the Meadows' beasts at first - no Lox to reroll for in Act II");
        Check.That(WayRules.MenagerieBeasts(3).Contains("Wolf") && !WayRules.MenagerieBeasts(3).Contains("Lox"),
                   "the wolf after Bonemass, when the Mountains open");
        Check.That(WayRules.MenagerieBeasts(4).Contains("Lox") && !WayRules.MenagerieBeasts(5).Contains("Asksvin") &&
                   WayRules.MenagerieBeasts(6).Contains("Asksvin"),
                   "the Lox after Moder, the Asksvin after the Queen");

        // The Hunter's tempering.
        Check.That(WayRules.PackSize(2) == 2 && WayRules.PackSize(3) == 3, "two wolves at a time; three after Bonemass");
        Check.That(WayRules.BowElementScale(3) == 1f && WayRules.BowElementScale(4) == 1.5f, "the bow's element x1.5 after Moder");
        Check.That(WayRules.UnseenSeconds(4) == 20f && WayRules.UnseenSeconds(5) == 30f, "Unseen 20 s, 30 s after Yagluth");
        Check.That(WayRules.PackRegenPerSecond(5) == 0f && WayRules.PackRegenPerSecond(6) == 2f, "the pack mends 2 a second after the Queen");

        // The Völva.
        Check.That(WayRules.HearthlightPerSecond(0) == 3f && WayRules.HearthlightPerSecond(2) == 5f &&
                   WayRules.HearthlightPerSecond(5) == 8f,
                   "Hearthlight: 3 a second in the Meadows, +1 per god, to 8");
        Check.That(WayRules.HearthlightPerSecond(6) == 12f && WayRules.HearthlightPerSecond(20) == 12f,
                   "the Queen's tempering lifts it to 12 at once - true the moment its line turns green");
        Check.That(WayRules.BoneCount(2) == 2 && WayRules.BoneCount(3) == 3, "two skeletons, three after Bonemass");
        Check.That(WayRules.MendingCooldown(3) == 90f && WayRules.MendingCooldown(4) == 60f, "Mending every 90 s, 60 after Moder");
        Check.That(WayRules.WrathRadius(4) == 6f && WayRules.WrathRadius(5) == 9f, "Thor's Wrath 6 m, 9 after Yagluth");

        // The Berserker's Fury.
        var fury = new FuryMeter();
        fury.SetMax(WayRules.FuryMax(0));
        fury.Hit(3, 0f);
        Check.That(fury.Stacks == 3 && Math.Abs(fury.Bonus - 0.15f) < 0.001f && !fury.Bloodied, "+5% a hit");
        fury.Hit(20, 0.5f);
        Check.That(fury.Stacks == 10 && Math.Abs(fury.Bonus - 0.5f) < 0.001f && fury.Bloodied, "to +50% at ten; Bloodied from five");
        fury.Tick(1.4f);
        Check.That(fury.Stacks == 10, "no fading within a second of the last hit");
        fury.Tick(1.6f); fury.Tick(2.6f);
        Check.That(fury.Stacks == 8, "then one hit's worth a second");
        fury.Fill(3f, 18f);
        fury.Tick(10f);
        Check.That(fury.Stacks == 10, "Blood Rage fills it and holds it");
        fury.Tick(19f);
        Check.That(fury.Stacks == 9, "and lets go when it ends");
        var cut = new FuryMeter(); cut.SetMax(10); cut.Fill(0f, 100f); cut.Release(5f);
        cut.Tick(5.5f);
        Check.That(cut.Stacks == 10, "a rage cut short lets go where it was cut");
        cut.Tick(6.5f);
        Check.That(cut.Stacks == 9, "and fades from there");
        var open = new FuryMeter(); open.SetMax(10); open.Fill(0f, float.PositiveInfinity);
        open.Tick(1000f);
        Check.That(open.Stacks == 10, "an open-ended hold stays full however long it runs");
        open.Release(1000f); open.Tick(1000.5f);
        Check.That(open.Stacks == 10, "and a Release lets go at once, with a second's grace");
        open.Tick(1001.5f);
        Check.That(open.Stacks == 9, "then fades from the release");
        var half = new FuryMeter(); half.SetMax(10); half.AddHalf(0f);
        Check.That(half.Stacks == 5, "Warcry fills half");
        Check.That(WayRules.FuryMax(5) == 10 && WayRules.FuryMax(6) == 15, "fifteen hits, +75%, after the Queen");
        Check.That(WayRules.RageSeconds(3) == 15f && WayRules.RageSeconds(4) == 25f, "Blood Rage 15 s, 25 after Moder");
        Check.That(WayRules.RendRadius(2) == 5f && WayRules.RendRadius(3) == 7.5f, "Rend 5 m, half again after Bonemass");
        Check.That(WayRules.WarcryRadius(4) == 8f && WayRules.WarcryRadius(5) == 12f, "Warcry 8 m, 12 after Yagluth");

        // The Húskarl's Guard.
        Check.That(WayRules.GuardBlockHeal(0, false) == 4f && WayRules.GuardParryHeal(0, false) == 10f, "a block heals 4, a parry 10");
        Check.That(WayRules.GuardBlockHeal(0, true) == 8f && WayRules.GuardParryHeal(0, true) == 20f, "double behind Shield Wall");
        Check.That(WayRules.GuardBlockHeal(6, false) == 8f && WayRules.GuardBlockHeal(6, true) == 16f, "and double again after the Queen");
        Check.That(WayRules.GuardStaminaRefund == 0.5f, "half the block's stamina back");
        Check.That(WayRules.BashRadius(2) == 4f && WayRules.BashRadius(3) == 6f, "Shield Bash 4 m, 6 after Bonemass");
        Check.That(WayRules.WallSeconds(3) == 20f && WayRules.WallSeconds(4) == 30f, "Shield Wall 20 s, 30 after Moder");
        Check.That(WayRules.LastStandSeconds(4) == 6f && WayRules.LastStandSeconds(5) == 10f, "Last Stand 6 s, 10 after Yagluth");

        // The Skald's songs.
        Check.That(WayRules.MarchSpeed(2) == 0.2f && WayRules.MarchSpeed(3) == 0.3f, "the Marching Song +20%, +30% after Bonemass");
        Check.That(WayRules.WarDamage(3) == 1.25f && WayRules.WarDamage(4) == 1.35f, "the War Song +25%, +35% after Moder");
        Check.That(WayRules.BragiPerSecond(0) == 3f && WayRules.BragiPerSecond(2) == 5f && WayRules.BragiPerSecond(3) == 6f &&
                   WayRules.BragiPerSecond(4) == 6f,
                   "Bragi mends 3 a second, +1 per god, to 6");
        Check.That(WayRules.BragiPerSecond(5) == 9f && WayRules.BragiPerSecond(9) == 9f,
                   "Yagluth's tempering lifts it to 9 at once - true the moment its line turns green");
        Check.That(WayRules.CrescendoDue(20f, 0f) && !WayRules.CrescendoDue(19.9f, 0f) && WayRules.CrescendoDue(0f, float.NegativeInfinity),
                   "a crescendo at most every twenty seconds; the first is free");
        Check.That(WayRules.SongsAtOnce(5) == 1 && WayRules.SongsAtOnce(6) == 2, "two songs at once after the Queen");

        // The Sæfari at sea.
        Check.That(WayRules.SaefariPrice(50) == 25 && WayRules.SaefariPrice(200) == 100 && WayRules.SaefariPrice(450) == 225 &&
                   WayRules.SaefariPrice(25) == 13,
                   "half price, rounded up");
        Check.That(WayRules.SeaShipTier(0) == 1 && WayRules.SeaShipTier(2) == 3 && WayRules.SeaShipTier(3) == 3,
                   "her ship sails a tier above what is fitted, to III");
        Check.That(WayRules.SaefariCoinFactor == 2, "the sea pays her double");

        // The Sæfari on foot.
        Check.That(WayRules.LandWardTier(0, 0) == 1 && WayRules.LandWardTier(1, 0) == 1 && WayRules.LandWardTier(2, 0) == 2 &&
                   WayRules.LandWardTier(3, 0) == 2,
                   "on land her Ward is one tier weaker than at sea, never below I");
        Check.That(WayRules.LandWardTier(3, 6) == 3, "after the Queen it is as strong as at sea");
        Check.That(WayRules.UndertowRadius(2) == 6f && WayRules.UndertowRadius(3) == 9f, "Undertow 6 m, 9 after Bonemass");
        Check.That(WayRules.StormcallerSeconds(3) == 20f && WayRules.StormcallerSeconds(4) == 30f, "Stormcaller 20 s, 30 after Moder");
        Check.That(WayRules.SeaLegsSeconds(4) == 300f && WayRules.SeaLegsSeconds(5) == 600f, "Sea Legs 5 min, 10 after Yagluth");

        // The Smiðr.
        Check.That(WayRules.ArmourFactor(5) == 1.5f && WayRules.ArmourFactor(6) == 2f, "armour half again, doubled after the Queen");
        Check.That(WayRules.WatchPosts(2) == 1 && WayRules.WatchPosts(3) == 2, "one watch-post, two after Bonemass");
        Check.That(WayRules.WatchPostBolt(2) == "TurretBoltWood" && WayRules.WatchPostBolt(3) == "TurretBolt" &&
                   WayRules.WatchPostBolt(6) == "TurretBoltFlametal",
                   "wooden missiles, black metal from Bonemass, flametal from the Queen (the ballista's own bolts)");
        Check.That(WayRules.FieldForgeSeconds(3) == 90f && WayRules.FieldForgeSeconds(4) == 180f, "the Field Forge 90 s, 3 min after Moder");
        Check.That(WayRules.MastersMinuteSeconds(4) == 60f && WayRules.MastersMinuteSeconds(5) == 120f, "the Master's Minute, 2 after Yagluth");
    }
}
