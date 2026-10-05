using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    internal enum SelfCheckVerdict { Missing, Fallback, Ok }

    internal sealed class SelfCheckLine
    {
        public SelfCheckVerdict Verdict;
        public string What;
        public string Detail;
    }

    /// <summary>
    /// Every name the saga GUESSES, checked against the running game at run start and logged as one
    /// block: a line per guess, marked OK, FALLBACK or MISSING.
    /// </summary>
    /// <remarks>
    /// Most of what the reviews of Acts II-VII found had one root: asset names the compiled assembly
    /// cannot see - a crypt, a stone ring, a dvergr site, a body, a mesh, a trader, Hildir's chests,
    /// flametal. Each was logged where it was used, scattered across Player.log and often only when
    /// the act that needs it is reached, hours in. This puts every one of them in front of the
    /// first launch, under one grep: <c>grep "Saga self-check" Player.log</c>.
    ///
    /// FALLBACK is not a failure - every guess with a way around it says what happens instead - but
    /// it is the line to correct. MISSING means a step that cannot be finished.
    /// </remarks>
    internal sealed class SagaSelfCheck
    {
        public const string Tag = "Saga self-check";

        private const int ShownCandidates = 3;

        private readonly List<SelfCheckLine> _lines = new List<SelfCheckLine>();

        public IReadOnlyList<SelfCheckLine> Lines => _lines;

        public void Ok(string what, string detail) => Add(SelfCheckVerdict.Ok, what, detail);
        public void Fallback(string what, string detail) => Add(SelfCheckVerdict.Fallback, what, detail);
        public void Missing(string what, string detail) => Add(SelfCheckVerdict.Missing, what, detail);

        public int Count(SelfCheckVerdict verdict) => _lines.Count(l => l.Verdict == verdict);

        /// <summary>
        /// The first candidate that exists, recorded as OK when it is the guess itself and FALLBACK
        /// when it is a later one. None at all is FALLBACK when <paramref name="stalls"/> is false
        /// (there is a way around it, and <paramref name="whenNone"/> says what it is) and MISSING
        /// when it is true (<paramref name="whenNone"/> says what breaks). Returns null for none.
        /// </summary>
        public string Pick(string what, IList<string> candidates, Func<string, bool> exists, string whenNone, bool stalls)
        {
            var names = (candidates ?? new string[0]).Where(n => !string.IsNullOrEmpty(n)).ToList();

            for (int i = 0; i < names.Count; i++)
            {
                bool found;
                try { found = exists != null && exists(names[i]); }
                catch { found = false; }
                if (!found) continue;

                if (i == 0) Ok(what, names[i]);
                else Fallback(what, $"no '{names[0]}' - using '{names[i]}'");
                return names[i];
            }

            string tried = names.Count == 0 ? "nothing to try" : "none of " + Shorten(names);
            Add(stalls ? SelfCheckVerdict.Missing : SelfCheckVerdict.Fallback, what, $"{tried} - {whenNone}");
            return null;
        }

        /// <summary>
        /// For a list where ANY name will do (a speaker goes to the nearest of several stone rings):
        /// OK naming how many were found, else as <see cref="Pick"/> does for none. Returns the count.
        /// </summary>
        public int AnyOf(string what, IList<string> candidates, Func<string, bool> exists, string whenNone, bool stalls)
        {
            var names = (candidates ?? new string[0]).Where(n => !string.IsNullOrEmpty(n)).ToList();
            var found = names.Where(n => { try { return exists != null && exists(n); } catch { return false; } }).ToList();

            if (found.Count > 0)
                Ok(what, $"{found.Count} of {names.Count}: {Shorten(found)}");
            else
                Add(stalls ? SelfCheckVerdict.Missing : SelfCheckVerdict.Fallback, what,
                    (names.Count == 0 ? "nothing to try" : "none of " + Shorten(names)) + " - " + whenNone);
            return found.Count;
        }

        /// <summary>For a list where EVERY name must exist: OK with the total, or MISSING naming the bad ones.</summary>
        public void AllOf(string what, int total, IList<string> missing, string consequence)
        {
            if (missing == null || missing.Count == 0) Ok(what, $"all {total} resolve");
            else Missing(what, $"{string.Join(", ", missing.ToArray())} - {consequence}");
        }

        /// <summary>A header counting each verdict, then the worst lines first. Every line starts with <see cref="Tag"/>.</summary>
        public IEnumerable<string> Format(string build)
        {
            yield return $"{Tag}: {Count(SelfCheckVerdict.Ok)} OK, {Count(SelfCheckVerdict.Fallback)} FALLBACK, " +
                         $"{Count(SelfCheckVerdict.Missing)} MISSING (build {build})";

            // Stable: OrderBy keeps insertion order within a verdict.
            foreach (var line in _lines.OrderBy(l => (int)l.Verdict))
                yield return $"{Tag} | {Word(line.Verdict),-8} | {line.What}: {line.Detail}";
        }

        private void Add(SelfCheckVerdict verdict, string what, string detail) =>
            _lines.Add(new SelfCheckLine { Verdict = verdict, What = what ?? "?", Detail = detail ?? string.Empty });

        private static string Word(SelfCheckVerdict verdict) =>
            verdict == SelfCheckVerdict.Ok ? "OK" : verdict == SelfCheckVerdict.Fallback ? "FALLBACK" : "MISSING";

        private static string Shorten(IList<string> names) =>
            names.Count <= ShownCandidates + 1
                ? string.Join(", ", names.ToArray())
                : string.Join(", ", names.Take(ShownCandidates).ToArray()) + $" and {names.Count - ShownCandidates} more";
    }
}
