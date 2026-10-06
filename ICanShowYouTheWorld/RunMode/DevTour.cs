using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// MACBOOK-TEMP (2026-10-06): the dev "go to" key, to be removed once the owner tests at a full
    /// keyboard again. The MacBook has no mouse ("I dont have a mouse either so i cant fight or do
    /// anything complex"), so walking 2 km to a speaker is out; one key hops between the places the
    /// saga cares about instead: the speakers, the act's altar, the three traders.
    ///
    /// Pure: which places exist, and where they are, is the game side's business (RunService).
    /// </summary>
    public static class DevTour
    {
        public struct Stop
        {
            public readonly string Name;

            /// <summary>The act the place belongs to, 0-based like the run's act index.</summary>
            public readonly int Act;

            public Stop(string name, int act)
            {
                Name = name;
                Act = act;
            }
        }

        /// <summary>The current act's places first, then the rest in act order; within an act, as given.</summary>
        public static List<Stop> Order(IEnumerable<Stop> stops, int currentAct) =>
            stops.Select((stop, i) => (stop, i))
                 .OrderBy(x => x.stop.Act == currentAct ? 0 : 1)
                 .ThenBy(x => x.stop.Act)
                 .ThenBy(x => x.i)
                 .Select(x => x.stop)
                 .ToList();

        /// <summary>
        /// Where the next press goes: the place after <paramref name="last"/>, or before it going back,
        /// wrapping at both ends. With no last place, or one no longer listed, the tour starts again.
        /// -1 when there is nowhere to go.
        /// </summary>
        public static int Next(IList<string> names, string last, bool back)
        {
            if (names == null || names.Count == 0) return -1;

            int at = last == null ? -1 : names.IndexOf(last);
            if (at < 0) return back ? names.Count - 1 : 0;

            return ((at + (back ? -1 : 1)) % names.Count + names.Count) % names.Count;
        }
    }
}
