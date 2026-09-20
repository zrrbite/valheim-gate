using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Everything the saga has SAID this run, in order, so none of it is lost to the screen.
    /// </summary>
    /// <remarks>
    /// Centre-screen text is transient by design and the saga pushes a great deal of it: quest
    /// openings, hints, race lines with the ledger, forfeits, arrivals, the shade and Thjalfi. Lines
    /// queue and overwrite one another, and the owner's report was the obvious consequ-
    /// "impossible to keep up with the yellow text on screen. sometimes it gets overwritten by other
    /// hints". A story told only in a place that erases itself is a story half told.
    ///
    /// A STATIC sink, which is the whole reason this is cheap. Every centre-screen line in the mode
    /// already passes through RunService.Message, and both quest-givers' long speeches pass through
    /// their own Say - three call sites total, so recording everything costs three lines of wiring
    /// and no new plumbing anywhere else.
    ///
    /// Deliberately in RunMode rather than RunMode/Unity: it touches nothing in the game, so the unit
    /// tests can reach it. That is not a detail - the last quest-giver bug was invisible precisely
    /// because it lived where the tests cannot see.
    /// </remarks>
    public static class SagaTranscript
    {
        /// <summary>One line, with who said it and when in the run.</summary>
        public struct Line
        {
            /// <summary>Who spoke: a character's name, or null for the saga's own voice.</summary>
            public string Speaker;

            public string Text;

            /// <summary>Run-seconds when it was said, for a readable stamp.</summary>
            public float At;
        }

        /// <summary>
        /// How many lines are kept.
        /// </summary>
        /// <remarks>
        /// A long run says a lot, and this is both rendered every frame the page is open and written
        /// into the run's save file. Two hundred is several hours of play at the rate the mode talks,
        /// and the oldest go first - which is the right end to lose, since the BOOK's chronicle keeps
        /// the beats that mattered permanently and this is the raw feed.
        /// </remarks>
        public const int Capacity = 200;

        private static readonly List<Line> Lines = new List<Line>();

        /// <summary>Set by the host each tick, so a line can be stamped without knowing the clock.</summary>
        public static float RunSeconds;

        /// <summary>Newest LAST, the order it was heard in.</summary>
        public static IEnumerable<Line> All => Lines;

        public static int Count => Lines.Count;

        /// <summary>
        /// Records a line. Repeats of the line just said are dropped.
        /// </summary>
        /// <remarks>
        /// The de-duplication earns its place: several pollers re-announce the same notice while a
        /// condition holds (an unfinished hint, a corpse waiting, a hook warning), which on screen is
        /// invisible because the text simply stays put, and in a transcript would be fifty identical
        /// rows burying everything else.
        /// </remarks>
        public static void Record(string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (Lines.Count > 0)
            {
                var last = Lines[Lines.Count - 1];
                if (last.Text == text && last.Speaker == speaker) return;
            }

            Lines.Add(new Line { Speaker = speaker, Text = text, At = RunSeconds });

            if (Lines.Count > Capacity) Lines.RemoveRange(0, Lines.Count - Capacity);
        }

        public static void Clear() => Lines.Clear();

        /// <summary>
        /// Flattens for the run save. Speaker, seconds and text in one string per line.
        /// </summary>
        /// <remarks>
        /// One string per entry rather than parallel lists, because Unity's JsonUtility cannot
        /// serialise nested collections - the same constraint the chronicle works around. The
        /// separator is a unit separator (U+001F) precisely because no line of dialogue will ever
        /// contain one, where a pipe or a colon certainly could.
        /// </remarks>
        public static List<string> Save() =>
            Lines.Select(l => string.Join("",
                l.Speaker ?? string.Empty,
                l.At.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                l.Text ?? string.Empty)).ToList();

        public static void Restore(IEnumerable<string> saved)
        {
            Lines.Clear();
            if (saved == null) return;

            foreach (var row in saved)
            {
                if (string.IsNullOrEmpty(row)) continue;

                var parts = row.Split('');
                if (parts.Length < 3) continue;

                float at;
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out at);

                Lines.Add(new Line
                {
                    Speaker = string.IsNullOrEmpty(parts[0]) ? null : parts[0],
                    At = at,
                    // Re-joined, so a line that somehow held a separator survives rather than truncating.
                    Text = string.Join("", parts.Skip(2).ToArray()),
                });
            }

            if (Lines.Count > Capacity) Lines.RemoveRange(0, Lines.Count - Capacity);
        }

        /// <summary>A run-time stamp as "12:34", for the page.</summary>
        public static string Stamp(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;
            return string.Format("{0}:{1:00}", total / 60, total % 60);
        }
    }
}
