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
    }
}
