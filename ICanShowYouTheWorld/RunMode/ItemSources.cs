using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>One thing the saga asks the player to bring, and who asks for it.</summary>
    public sealed class AskedItem
    {
        /// <summary>A prefab name or an "$item_" token. "A|B" means either will do.</summary>
        public string Name;
        public string Who;
    }

    /// <summary>
    /// Whether every item the saga asks for can actually be got (2026-10-06).
    ///
    /// The first self-check ever read (the Mac, 2026-10-06) found Haldor asking for TrophyForestTroll: an item
    /// that exists, so the name check passed, and that no troll drops (they drop TrophyFrostTroll; both read
    /// "Troll Trophy"). Act II would have stalled at his ask. The lesson is general: an item that exists is
    /// not an item anyone can get. The game side gathers everything the game drops, crafts, sells, yields and
    /// converts, plus what the saga itself grants, and this decides which asks nothing provides.
    /// </summary>
    public static class ItemSources
    {
        /// <summary>
        /// Items found lying in the world, which no drop table, recipe, shop or conversion lists. Each one is
        /// here because the catalogue shows where it lies: DragonEgg is laid in the mountains' drake nests.
        /// </summary>
        public static readonly string[] WorldFinds = { "DragonEgg" };

        /// <summary>The items in an ask: "A|B" is either, trimmed; nothing is none.</summary>
        public static IEnumerable<string> Alternatives(string name) =>
            (name ?? string.Empty).Split('|').Select(a => a.Trim()).Where(a => a.Length > 0);

        /// <summary>
        /// How many of each wanted item are held, keyed by the ask as written. An ask of "A|B" counts
        /// both, so a step can take either of two items (2026-10-06: Act I's "Cook 5 meat" asked for
        /// "$item_cookedmeat", no item's name, and could never finish; now it takes cooked boar or deer).
        /// </summary>
        /// <param name="held">Each stack as (its item name - the localisation token - and its size).</param>
        public static Dictionary<string, int> CountWanted(IEnumerable<(string name, int stack)> held, IEnumerable<string> wanted)
        {
            var counts = new Dictionary<string, int>();
            var asks = new Dictionary<string, List<string>>();
            foreach (var ask in wanted ?? Enumerable.Empty<string>())
            {
                if (ask == null || counts.ContainsKey(ask)) continue;
                counts[ask] = 0;
                foreach (var name in Alternatives(ask))
                {
                    if (!asks.TryGetValue(name, out var into)) asks[name] = into = new List<string>();
                    into.Add(ask);
                }
            }

            foreach (var (name, stack) in held ?? Enumerable.Empty<(string, int)>())
                if (name != null && asks.TryGetValue(name, out var into))
                    foreach (var ask in into) counts[ask] += stack;

            return counts;
        }

        /// <summary>
        /// The asks nothing provides: one per item, naming everyone who asks for it. <paramref name="resolve"/>
        /// maps a name (a prefab, or a token naming several) to prefab names; an ask is fine when any of its
        /// alternatives resolves to something produced or found in the world.
        /// </summary>
        public static List<AskedItem> Unobtainable(IEnumerable<AskedItem> asked, ISet<string> produced,
                                                   Func<string, IEnumerable<string>> resolve)
        {
            var lost = new List<AskedItem>();

            foreach (var ask in asked ?? Enumerable.Empty<AskedItem>())
            {
                var alternatives = Alternatives(ask?.Name).ToList();
                if (alternatives.Count == 0) continue;

                bool got = alternatives.Any(a => (resolve(a) ?? Enumerable.Empty<string>())
                    .Any(p => produced.Contains(p) || WorldFinds.Contains(p)));
                if (got) continue;

                var known = lost.FirstOrDefault(l => l.Name == ask.Name);
                if (known == null) lost.Add(new AskedItem { Name = ask.Name, Who = ask.Who });
                else if (!known.Who.Split(new[] { ", " }, StringSplitOptions.None).Contains(ask.Who))
                    known.Who += ", " + ask.Who;
            }

            return lost;
        }
    }
}
