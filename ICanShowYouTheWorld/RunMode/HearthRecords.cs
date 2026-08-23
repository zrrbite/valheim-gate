using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The homestead's answer to boss splits.
    ///
    /// Splits measure a run against the clock, which is the speed-runner's question. These measure
    /// it against ITSELF — the biggest haul landed, the most comfortable the house ever got, the
    /// largest the pen ever grew. Nothing here affects score, heat or progression; they exist
    /// because "how big was the fish" is a thing people want to know and tell each other about.
    ///
    /// Every record LATCHES ITS MAXIMUM, so losing the fish to a death or tearing the house down
    /// does not take the record with it. That is the same rule the host-reported measures follow,
    /// and for the same reason: the host reports what it can currently see, and what it can see
    /// comes and goes.
    ///
    /// Pure — no Unity, no game types — so the whole thing is unit-testable.
    /// </summary>
    public class HearthRecords
    {
        /// <summary>One superlative: its best value this run, and optionally what achieved it.</summary>
        public class Record
        {
            public string Id;

            /// <summary>Player-facing row name, e.g. "Best haul".</summary>
            public string Label;

            public float Value;

            /// <summary>What set it — a fish's name, say. Empty for records that are just a number.</summary>
            public string Detail = string.Empty;

            /// <summary>Decimal places for <see cref="Value"/>. Counts want none.</summary>
            public int Decimals;

            /// <summary>True when this run's value beats the character's all-time best.</summary>
            public bool IsPersonalBest;

            /// <summary>The value and its detail, as one phrase: "Pike, 4" or just "7".</summary>
            public string Text
            {
                get
                {
                    string number = Value.ToString("F" + Decimals, CultureInfo.InvariantCulture);
                    return string.IsNullOrEmpty(Detail) ? number : $"{Detail}, {number}";
                }
            }
        }

        /// <summary>
        /// The records a saga keeps, in display order.
        ///
        /// Declared rather than discovered so the panel has a stable shape from the first second of
        /// a run — a list that grew as things happened would reorder itself under the player's eyes.
        /// </summary>
        /// <summary>
        /// Kept as id "fish" so stored personal bests survive, but it is a HAUL now, not a weight.
        /// Every fish in this game weighs 0.5, so "heaviest" was a record that could never move.
        /// </summary>
        public const string BestHaul = "fish";
        public const string Comfort = "comfort";
        public const string LargestPen = "pen";
        public const string Trophies = "trophies";
        public const string NightsSlept = "nights";
        public const string Foraged = "foraged";

        private readonly List<Record> records = new List<Record>
        {
            new Record { Id = BestHaul,     Label = "Best haul",     Decimals = 0 },
            new Record { Id = Comfort,      Label = "Comfort",       Decimals = 0 },
            new Record { Id = LargestPen,   Label = "Largest pen",   Decimals = 0 },
            new Record { Id = Trophies,     Label = "Trophies hung", Decimals = 0 },
            new Record { Id = NightsSlept,  Label = "Nights slept",  Decimals = 0 },
            new Record { Id = Foraged,      Label = "Foraged",       Decimals = 0 },
        };

        /// <summary>Every record, in display order, including ones still at zero.</summary>
        public IReadOnlyList<Record> All => records;

        /// <summary>The records worth showing: anything that has actually happened.</summary>
        public IEnumerable<Record> Achieved => records.Where(r => r.Value > 0f);

        /// <summary>
        /// Offers a value. Kept only if it beats what is already there.
        ///
        /// <paramref name="detail"/> travels WITH the value rather than being latched separately:
        /// a record naming one thing beside a number set by another would be worse than showing
        /// nothing.
        /// </summary>
        public void Report(string id, float value, string detail = null)
        {
            var record = records.FirstOrDefault(r => r.Id == id);
            if (record == null || value <= record.Value) return;

            record.Value = value;
            record.Detail = detail ?? string.Empty;
        }

        /// <summary>Reads one record's current value, or zero if it has none.</summary>
        public float ValueOf(string id) => records.FirstOrDefault(r => r.Id == id)?.Value ?? 0f;

        /// <summary>Reads one record, or null.</summary>
        public Record Get(string id) => records.FirstOrDefault(r => r.Id == id);

        /// <summary>
        /// Marks which records beat the character's all-time bests, given a lookup of those bests.
        ///
        /// Separate from <see cref="Report"/> because the bests live in the character's permanent
        /// data, which is game-coupled: this class stays pure and is simply told the numbers.
        /// A record equal to the best is NOT a personal best — matching is not beating.
        /// </summary>
        public void MarkPersonalBests(IDictionary<string, float> bests)
        {
            foreach (var record in records)
            {
                float best;
                record.IsPersonalBest = record.Value > 0f &&
                                        (bests == null || !bests.TryGetValue(record.Id, out best) || record.Value > best);
            }
        }

        /// <summary>Restores a saved run's records. Ids not in the table are ignored.</summary>
        public void Restore(IList<string> ids, IList<float> values, IList<string> details)
        {
            if (ids == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                var record = records.FirstOrDefault(r => r.Id == ids[i]);
                if (record == null) continue;

                record.Value = values != null && i < values.Count ? values[i] : 0f;
                record.Detail = details != null && i < details.Count ? details[i] ?? string.Empty : string.Empty;
            }
        }
    }
}
