using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Whether every item the saga asks for can actually be got (2026-10-06). The first self-check ever read
/// found Haldor asking for TrophyForestTroll, an item that exists, whose name the name check accepted, and
/// that no troll drops. The game side gathers what the game drops, crafts, sells and converts; this decides.
/// </summary>
static class ItemSourcesTests
{
    public static void Run()
    {
        var produced = new HashSet<string> { "TrophyFrostTroll", "Wood", "Iron", "Saga_ThorsBow" };

        // A token can mean several prefabs: both troll trophies are "$item_trophy_troll".
        IEnumerable<string> Resolve(string name) =>
            name == "$item_trophy_troll" ? new[] { "TrophyForestTroll", "TrophyFrostTroll" }
            : name == "$item_wood" ? new[] { "Wood" }
            : name == "$item_nothing" ? new string[0]
            : new[] { name };

        var lost = ItemSources.Unobtainable(new[]
        {
            new AskedItem { Name = "TrophyForestTroll", Who = "Haldor" },
            new AskedItem { Name = "$item_trophy_troll", Who = "a test step" },
            new AskedItem { Name = "$item_wood", Who = "the thane" },
            new AskedItem { Name = "Saga_Stormward|Saga_ThorsBow", Who = "a pool task" },
            new AskedItem { Name = "DragonEgg", Who = "the frozen one" },
            new AskedItem { Name = "Silver", Who = "the forge" },
            new AskedItem { Name = "Silver", Who = "the harvester" },
            new AskedItem { Name = "$item_nothing", Who = "a typo" },
        }, produced, Resolve);

        Check.That(lost.Any(l => l.Name == "TrophyForestTroll" && l.Who == "Haldor"),
                   "a prefab nothing produces is lost, even though the item exists (the troll-trophy case)");
        Check.That(!lost.Any(l => l.Name == "$item_trophy_troll"),
                   "a token is fine when ANY prefab it names can be got");
        Check.That(!lost.Any(l => l.Name == "$item_wood"), "a produced item is fine");
        Check.That(!lost.Any(l => l.Name.StartsWith("Saga_Stormward")),
                   "\"A|B\" is fine when either can be got");
        Check.That(!lost.Any(l => l.Name == "DragonEgg"),
                   "a world find (laid in drake nests, in no drop table) is fine");
        Check.That(lost.Count(l => l.Name == "Silver") == 1 && lost.First(l => l.Name == "Silver").Who == "the forge, the harvester",
                   "one line per item, naming everyone who asks for it");
        Check.That(lost.Any(l => l.Name == "$item_nothing"),
                   "a name that resolves to nothing is lost too");
        Check.That(lost.Count == 3, "exactly the three lost asks: the Forest trophy, silver, the typo");

        Check.That(ItemSources.Unobtainable(new AskedItem[0], produced, Resolve).Count == 0 &&
                   ItemSources.Unobtainable(new[] { new AskedItem { Name = "", Who = "x" } }, produced, Resolve).Count == 0,
                   "nothing asked, or an empty ask, loses nothing");
    }
}
