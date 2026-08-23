using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// HearthRecords: the homestead's answer to boss splits.
///
/// Splits measure a run against the clock. These measure it against itself, and every one latches
/// its MAXIMUM — losing the fish to a death or tearing the house down must not take the record
/// with it, the same rule every host-reported measure follows.
/// </summary>
static class HearthRecordsTests
{
    public static void Run()
    {
        var r = new HearthRecords();

        Check.That(!r.Achieved.Any(), "a fresh run has no records to show");
        Check.That(r.All.Count == 6, "but the table's shape is fixed from the first second");

        // Latching: a smaller fish does not replace a bigger one.
        r.Report(HearthRecords.HeaviestFish, 4f, "Pike");
        r.Report(HearthRecords.HeaviestFish, 1.5f, "Perch");
        Check.That(r.ValueOf(HearthRecords.HeaviestFish) == 4f, "a smaller fish does not beat a bigger one");
        Check.That(r.Get(HearthRecords.HeaviestFish).Detail == "Pike", "and the name stays with the weight that set it");

        r.Report(HearthRecords.HeaviestFish, 8f, "Tuna");
        Check.That(r.Get(HearthRecords.HeaviestFish).Text == "Tuna, 8.0", "a bigger fish takes both the weight and the name");

        // Counts render without decimals; weights with one.
        r.Report(HearthRecords.Comfort, 7f);
        Check.That(r.Get(HearthRecords.Comfort).Text == "7", "a count has no decimal places");

        // Dropping to zero must not erase what was reached.
        r.Report(HearthRecords.Comfort, 0f);
        Check.That(r.ValueOf(HearthRecords.Comfort) == 7f, "tearing the house down does not un-earn the comfort record");

        Check.That(r.Achieved.Count() == 2, "only records that happened are shown");
        Check.That(r.ValueOf("nonsense") == 0f, "an unknown record reads as zero rather than throwing");
        r.Report("nonsense", 99f);
        Check.That(r.ValueOf("nonsense") == 0f, "and cannot be created by reporting into it");

        // Personal bests: beating counts, matching does not.
        r.MarkPersonalBests(new Dictionary<string, float>
        {
            { HearthRecords.HeaviestFish, 9f },
            { HearthRecords.Comfort, 7f },
        });
        Check.That(!r.Get(HearthRecords.HeaviestFish).IsPersonalBest, "a smaller fish than your best is not a best");
        Check.That(!r.Get(HearthRecords.Comfort).IsPersonalBest, "matching your best is not beating it");

        // A first run has no stored bests at all, so everything it does is a best.
        var fresh = new HearthRecords();
        fresh.Report(HearthRecords.LargestPen, 3f);
        fresh.MarkPersonalBests(new Dictionary<string, float>());
        Check.That(fresh.Get(HearthRecords.LargestPen).IsPersonalBest, "a first run sets records by definition");
        Check.That(!fresh.Get(HearthRecords.Comfort).IsPersonalBest, "but a record never set is not one");

        // Restore ignores ids it does not know, so a record removed later cannot break a save.
        var restored = new HearthRecords();
        restored.Restore(
            new List<string> { HearthRecords.NightsSlept, "retired-record" },
            new List<float> { 6f, 12f },
            new List<string> { null, "x" });
        Check.That(restored.ValueOf(HearthRecords.NightsSlept) == 6f, "a saved record comes back");
        Check.That(restored.Achieved.Count() == 1, "and an unknown saved id is ignored rather than fatal");
    }
}
