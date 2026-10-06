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
                var alternatives = (ask?.Name ?? string.Empty)
                    .Split('|')
                    .Select(a => a.Trim())
                    .Where(a => a.Length > 0)
                    .ToList();
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
