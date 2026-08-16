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
    }
}
