using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

static class BoonEngineTests
{
    static List<BoonDefinition> Pool() => new List<BoonDefinition>
    {
        new BoonDefinition { Id="fleet",   Display="Fleet-footed", IsPassive=true },
        new BoonDefinition { Id="wind",    Display="Second Wind",  IsPassive=false, CooldownSeconds=120 },
        new BoonDefinition { Id="way",     Display="Waystone",     IsPassive=false },
        new BoonDefinition { Id="sharp",   Display="Sharpened",    IsPassive=true },
        new BoonDefinition { Id="ember",   Display="Emberskin",    IsPassive=false, CooldownSeconds=180 },
        new BoonDefinition { Id="pack",    Display="Packleader",   IsPassive=true },
    };

    public static void Run()
    {
        var b = new BoonEngine(Pool(), new Random(7), offerTimeoutSeconds: 45f);
        Check.That(b.CurrentOffer.Count == 0, "no offer initially");

        b.CreateOffer();
        Check.That(b.CurrentOffer.Count == 3, "offer has 3");
        Check.That(b.CurrentOffer.Select(x => x.Id).Distinct().Count() == 3, "offer distinct");

        var picked = b.CurrentOffer[1];
        Check.That(b.Pick(1), "pick succeeds");
        Check.That(b.CurrentOffer.Count == 0, "offer cleared after pick");
        Check.That(b.Held.Count == 1 && b.Held[0].Def.Id == picked.Id, "picked boon held");

        // Offer expiry
        b.CreateOffer();
        b.Tick(46f);
        Check.That(b.CurrentOffer.Count == 0, "offer expires after timeout");
        Check.That(b.Held.Count == 1, "expiry grants nothing");

        // Held passives never re-offered
        for (int i = 0; i < 10; i++) { b.CreateOffer(); if (b.CurrentOffer.Count > 0) b.Pick(0); }
        Check.That(b.Held.Count(h => h.Def.Id == "fleet") <= 1, "no duplicate passive");

        // Death removes newest
        var newest = b.Held.Last().Def.Id;
        var removed = b.RemoveLatest();
        Check.That(removed != null && removed.Def.Id == newest, "RemoveLatest returns newest");

        // Cooldown ticks down
        var c = new BoonEngine(Pool(), new Random(1), 45f);
        c.CreateOffer();
        var windIdx = -1;
        for (int i = 0; i < c.CurrentOffer.Count; i++) if (c.CurrentOffer[i].Id == "wind") windIdx = i;
        if (windIdx >= 0)
        {
            c.Pick(windIdx);
            c.Held[0].CooldownRemaining = 10f;
            c.Tick(4f);
            Check.That(Math.Abs(c.Held[0].CooldownRemaining - 6f) < 0.001f, "cooldown ticks");
        }

        RestoreHeldTests();
    }

    static void RestoreHeldTests()
    {
        var e = new BoonEngine(Pool(), new Random(11), 45f);
        int gained = 0;
        e.Gained += _ => gained++;

        e.RestoreHeld(new[]
        {
            new KeyValuePair<string, float>("fleet", 0f),
            new KeyValuePair<string, float>("wind", 42f),
        });
        Check.That(e.Held.Count == 2, "restore holds the saved boons");
        Check.That(e.Held[0].Def.Id == "fleet" && e.Held[1].Def.Id == "wind", "restored ids in order");
        Check.That(Math.Abs(e.Held[1].CooldownRemaining - 42f) < 0.001f, "restored cooldown");
        Check.That(gained == 0, "restore raises no Gained events");

        // Restored cooldowns keep ticking normally.
        e.Tick(2f);
        Check.That(Math.Abs(e.Held[1].CooldownRemaining - 40f) < 0.001f, "restored cooldown ticks down");

        // Restored passives are not offered again.
        e.CreateOffer();
        Check.That(!e.CurrentOffer.Any(d => d.Id == "fleet"), "restored passive is not re-offered");

        // Actives are excluded on the same terms as passives: an offer must never list something
        // the player is already carrying.
        var noDupes = new BoonEngine(Pool(), new Random(97), 60f);
        noDupes.RestoreHeld(new[]
        {
            new KeyValuePair<string, float>("way", 0f),
            new KeyValuePair<string, float>("wind", 0f),
        });
        noDupes.CreateOffer();
        Check.That(noDupes.CurrentOffer.All(d => d.Id != "way" && d.Id != "wind"),
            "a held ACTIVE is not re-offered either");
        Check.That(noDupes.CurrentOffer.Count == 3, "excluding held actives still fills the offer");

        // Unknown and duplicate ids ignored.
        var f = new BoonEngine(Pool(), new Random(11), 45f);
        f.RestoreHeld(new[]
        {
            new KeyValuePair<string, float>("nonsense", 1f),
            new KeyValuePair<string, float>("ember", 5f),
            new KeyValuePair<string, float>("ember", 9f),
        });
        Check.That(f.Held.Count == 1 && f.Held[0].Def.Id == "ember", "restore ignores unknown and duplicate ids");
        Check.That(Math.Abs(f.Held[0].CooldownRemaining - 5f) < 0.001f, "restore keeps the first of a duplicated id");

        // Restoring replaces; null clears.
        f.RestoreHeld(new[] { new KeyValuePair<string, float>("pack", 0f) });
        Check.That(f.Held.Count == 1 && f.Held[0].Def.Id == "pack", "restore replaces the previous held set");
        f.RestoreHeld(null);
        Check.That(f.Held.Count == 0, "restore(null) clears the held set");

        RestoreHeldChargesTests();
    }

    static void RestoreHeldChargesTests()
    {
        // Charges are positioned by index against the id/cooldown sequence, same pairing RunService uses.
        var g = new BoonEngine(Pool(), new Random(3), 45f);
        g.RestoreHeld(
            new[]
            {
                new KeyValuePair<string, float>("way", 0f),
                new KeyValuePair<string, float>("wind", 90f),
            },
            new[] { 2, 0 });
        Check.That(g.Held.Count == 2, "restore-with-charges holds the saved boons");
        Check.That(g.Held[0].Charges == 2, "restored way charges");
        Check.That(g.Held[1].Charges == 0, "restored wind charges default");

        // A short/missing charges list defaults the rest to 0 rather than throwing.
        var h = new BoonEngine(Pool(), new Random(3), 45f);
        h.RestoreHeld(
            new[]
            {
                new KeyValuePair<string, float>("way", 0f),
                new KeyValuePair<string, float>("fleet", 0f),
            },
            new[] { 1 });
        Check.That(h.Held[0].Charges == 1 && h.Held[1].Charges == 0, "short charges list defaults remaining entries to 0");

        // Omitting charges entirely (the single-arg overload) still defaults to 0, not a crash.
        var i = new BoonEngine(Pool(), new Random(3), 45f);
        i.RestoreHeld(new[] { new KeyValuePair<string, float>("way", 0f) });
        Check.That(i.Held[0].Charges == 0, "single-arg overload still defaults charges to 0");

        // Charges line up against the PRE-FILTER index, not the post-filter one — an unknown id
        // ahead of a known one must not shift the pairing.
        var j = new BoonEngine(Pool(), new Random(3), 45f);
        j.RestoreHeld(
            new[]
            {
                new KeyValuePair<string, float>("nonsense", 0f),
                new KeyValuePair<string, float>("way", 0f),
            },
            new[] { 9, 3 });
        Check.That(j.Held.Count == 1 && j.Held[0].Def.Id == "way" && j.Held[0].Charges == 3,
            "charges align to pre-filter index, skipping unknown ids correctly");

        FirstOfferPinTests();
    }

    /// <summary>
    /// FirstOfferPin steers the opening pick of a run into slot 0 and then gets out of the way.
    /// </summary>
    static void FirstOfferPinTests()
    {
        var p = new BoonEngine(Pool(), new Random(11), 45f) { FirstOfferPin = "pack" };
        p.CreateOffer();
        Check.That(p.CurrentOffer.Count == 3, "a pinned first offer still has three options");
        Check.That(p.CurrentOffer[0].Id == "pack", "the pinned boon is option 1 of the first offer");
        Check.That(p.CurrentOffer.Select(d => d.Id).Distinct().Count() == 3, "pinned offer options are distinct");

        // Take something OTHER than the pin, so the pin is still eligible for the next offer —
        // this is what proves the pin is spent by the first offer, not by being picked.
        Check.That(p.Pick(1), "picking a non-pinned option succeeds");
        Check.That(p.Held[0].Def.Id != "pack", "the pinned boon was not the one taken");

        // Later offers are plain random draws, so the pinned id CAN still land in slot 0 by
        // chance — asserting it never does would be testing the rng, and would fail about a
        // third of the time. What must be true is that it is no longer FORCED there: across a
        // long run of offers, at least one has something else in slot 0.
        int laterOffers = 0;
        int laterOffersLedByPin = 0;
        for (int i = 0; i < 25; i++)
        {
            p.CreateOffer();
            if (p.CurrentOffer.Count > 0)
            {
                laterOffers++;
                if (p.CurrentOffer[0].Id == "pack") laterOffersLedByPin++;
            }
            // Let the offer expire rather than picking it, so the held set (and with it the
            // option pool) stops changing and only the pin behaviour is under test.
            p.Tick(50f);
        }
        Check.That(laterOffers == 25, "every later CreateOffer produced an offer");
        Check.That(laterOffersLedByPin < laterOffers, "later offers do not force the pin into slot 0");

        // No pin at all is the pre-existing behaviour, unchanged.
        var q = new BoonEngine(Pool(), new Random(11), 45f);
        q.CreateOffer();
        Check.That(q.CurrentOffer.Count == 3, "an unpinned first offer still has three options");
        Check.That(q.CurrentOffer.Select(d => d.Id).Distinct().Count() == 3, "unpinned offer options are distinct");

        // A pin naming something outside the pool is ignored rather than shrinking the offer,
        // and it does not keep trying on later offers either.
        var r = new BoonEngine(Pool(), new Random(13), 45f) { FirstOfferPin = "nonsense" };
        r.CreateOffer();
        Check.That(r.CurrentOffer.Count == 3, "an unresolvable pin still yields three options");
        Check.That(r.CurrentOffer.All(d => d.Id != "nonsense"), "an unresolvable pin adds nothing");

        // A pin naming an already-held passive is excluded by the usual held-passive filter.
        var s = new BoonEngine(Pool(), new Random(17), 45f) { FirstOfferPin = "fleet" };
        s.RestoreHeld(new[] { new KeyValuePair<string, float>("fleet", 0f) });
        s.CreateOffer();
        Check.That(s.CurrentOffer.Count == 3, "a pin on a held passive still yields three options");
        Check.That(s.CurrentOffer.All(d => d.Id != "fleet"), "a held passive is never offered, pinned or not");

        MinBossesTests();
    }

    /// <summary>
    /// MinBosses: a boon that is worthless until the run has got somewhere is not offered before
    /// then.
    ///
    /// Resistances forced this. "Resistant to frost" in the Meadows is a wasted pick — the player
    /// spends one of three options on something that does nothing for hours. The challenge pool has
    /// had MaxTier for exactly this reason since alpha11; the boon pool had no equivalent.
    /// </summary>
    static void MinBossesTests()
    {
        Check.That(new BoonDefinition().MinBosses == 0, "MinBosses defaults to 0 — offered from the start");

        var pool = new List<BoonDefinition>
        {
            new BoonDefinition { Id="fleet", Display="Fleet-footed", IsPassive=true },
            new BoonDefinition { Id="sharp", Display="Sharpened",    IsPassive=true },
            new BoonDefinition { Id="mule",  Display="Packmule",     IsPassive=true },
            new BoonDefinition { Id="frost", Display="Coldblooded",  IsPassive=true, MinBosses = 2 },
        };

        // Before the gate opens, the boon simply is not among the options.
        var early = new BoonEngine(pool, new Random(101), 45f) { DefeatedBosses = 0 };
        for (int i = 0; i < 20; i++)
        {
            early.CreateOffer();
            Check.That(early.CurrentOffer.All(d => d.Id != "frost"), "a gated boon is never offered too early");
            early.ClearOffer();
        }

        // Exactly at the threshold it becomes available.
        var ready = new BoonEngine(pool, new Random(102), 45f) { DefeatedBosses = 2 };
        bool seen = false;
        for (int i = 0; i < 20 && !seen; i++)
        {
            ready.CreateOffer();
            seen = ready.CurrentOffer.Any(d => d.Id == "frost");
            ready.ClearOffer();
        }
        Check.That(seen, "a gated boon is offered once its threshold is met");

        // The gate must never shrink an offer below three while ungated options remain — a run
        // should not be handed two choices because a third was filtered out.
        var gatedPool = pool.Concat(new[]
        {
            new BoonDefinition { Id="poison", Display="Irongut",     IsPassive=true, MinBosses = 1 },
            new BoonDefinition { Id="fire",   Display="Fire-blooded", IsPassive=true, MinBosses = 3 },
        }).ToList();

        var narrow = new BoonEngine(gatedPool, new Random(103), 45f) { DefeatedBosses = 0 };
        narrow.CreateOffer();
        Check.That(narrow.CurrentOffer.Count == 3, "the gate does not shrink an offer that has enough ungated options");
        Check.That(narrow.CurrentOffer.All(d => d.MinBosses == 0), "only ungated boons are offered at zero bosses");

        // Raising the count mid-run opens the gate without anything else changing.
        narrow.ClearOffer();
        narrow.DefeatedBosses = 3;
        bool sawGated = false;
        for (int i = 0; i < 30 && !sawGated; i++)
        {
            narrow.CreateOffer();
            sawGated = narrow.CurrentOffer.Any(d => d.MinBosses > 0);
            narrow.ClearOffer();
        }
        Check.That(sawGated, "raising the boss count opens the gate mid-run");
    }
}
