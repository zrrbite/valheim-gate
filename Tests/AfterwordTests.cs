using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The charred one stays for his last line after the run (owner, 2026-10-06: "keep him standing for his
/// last line. Someone might find him"). Where he waits is saved on the character, with its world.
/// </summary>
static class AfterwordTests
{
    public static void Run()
    {
        var a = new Afterword { World = "12345:My World | the second", X = 1.5f, Y = -2.25f, Z = 3000.125f };
        var back = Afterword.Decode(a.Encode());
        Check.That(back != null && back.World == a.World && back.X == a.X && back.Y == a.Y && back.Z == a.Z,
                   "where he waits survives saving, whatever the world's name holds");

        Check.That(Afterword.Decode(null) == null && Afterword.Decode("") == null &&
                   Afterword.Decode("garbage") == null && Afterword.Decode("a|b|c|world") == null,
                   "nothing, or something unreadable, is no afterword rather than an error");

        Check.That(Afterword.Stands(a, a.World, runActive: false),
                   "he stands in his own world while no run is live");
        Check.That(!Afterword.Stands(a, "other:world", runActive: false) && !Afterword.Stands(a, a.World, runActive: true) &&
                   !Afterword.Stands(null, a.World, runActive: false),
                   "not in another world, not during a run, and not when there is no afterword");

        Check.That(Afterword.ShouldSave(won: true, faderDown: true, metHim: true),
                   "a run won with Fader down, after meeting him, leaves him standing");
        Check.That(!Afterword.ShouldSave(won: false, faderDown: true, metHim: true) &&
                   !Afterword.ShouldSave(won: true, faderDown: false, metHim: true) &&
                   !Afterword.ShouldSave(won: true, faderDown: true, metHim: false),
                   "not for a run that was lost, one that ended before Fader, or one that never met him");
    }
}
