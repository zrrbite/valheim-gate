using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

static class RunStashTests
{
    public static void Run()
    {
        var s = new RunStash();
        Check.That(s.Entries.Count == 0 && s.TotalCount == 0, "a new stash is empty");

        // Deposits of the same kind merge.
        Check.That(s.Deposit("Wood", 50, 1, 0) == 50, "a deposit returns what it took");
        s.Deposit("Wood", 30, 1, 0);
        Check.That(s.Entries.Count == 1 && s.Entries[0].Count == 80, "same kind merges into one entry");

        // Quality and variant are part of the identity. A level-3 axe must never merge with a
        // level-1 one, or depositing both would hand back two of whichever was written last.
        s.Deposit("AxeBronze", 1, 1, 0);
        s.Deposit("AxeBronze", 1, 3, 0);
        Check.That(s.Entries.Count(e => e.Prefab == "AxeBronze") == 2, "different quality is a different kind");
        s.Deposit("Trophy", 1, 1, 0);
        s.Deposit("Trophy", 1, 1, 2);
        Check.That(s.Entries.Count(e => e.Prefab == "Trophy") == 2, "different variant is a different kind");

        // Nonsense in, nothing out.
        Check.That(s.Deposit(null, 5, 1, 0) == 0, "a null prefab deposits nothing");
        Check.That(s.Deposit("", 5, 1, 0) == 0, "a blank prefab deposits nothing");
        Check.That(s.Deposit("Wood", 0, 1, 0) == 0, "a zero count deposits nothing");
        Check.That(s.Deposit("Wood", -5, 1, 0) == 0, "a negative count deposits nothing");
        Check.That(s.Entries[0].Count == 80, "a refused deposit does not disturb the stack");

        // Withdrawal is partial-friendly and removes an emptied kind.
        int woodIndex = s.Entries.ToList().FindIndex(e => e.Prefab == "Wood");
        Check.That(s.Withdraw(woodIndex, 30) == 30, "a withdrawal returns what it gave");
        Check.That(s.Entries[woodIndex].Count == 50, "the remainder stays");
        Check.That(s.Withdraw(woodIndex, 999) == 50, "withdrawing more than held gives only what is there");
        Check.That(s.Entries.All(e => e.Prefab != "Wood"), "an emptied kind is removed");

        Check.That(s.Withdraw(-1, 5) == 0, "a negative index withdraws nothing");
        Check.That(s.Withdraw(9999, 5) == 0, "an out-of-range index withdraws nothing");
        Check.That(s.Withdraw(0, 0) == 0, "a zero count withdraws nothing");

        // The cap is per KIND, and a refused deposit is all-or-nothing: half a stack vanishing
        // would be worse than a deposit that plainly refuses.
        var full = new RunStash();
        for (int i = 0; i < RunStash.MaxEntries; i++) full.Deposit($"Item{i}", 1, 1, 0);
        Check.That(full.IsFull, "the stash reports full at the cap");
        Check.That(full.Deposit("OneMore", 10, 1, 0) == 0, "a new kind is refused when full");
        Check.That(full.Deposit("Item0", 10, 1, 0) == 10, "an EXISTING kind still merges when full");

        // Round trip.
        var source = new RunStash();
        source.Deposit("Wood", 40, 1, 0);
        source.Deposit("AxeBronze", 1, 3, 0);

        var restored = new RunStash();
        restored.Restore(
            source.Entries.Select(e => e.Prefab).ToList(),
            source.Entries.Select(e => e.Count).ToList(),
            source.Entries.Select(e => e.Quality).ToList(),
            source.Entries.Select(e => e.Variant).ToList());

        Check.That(restored.Entries.Count == 2, "a restore rebuilds every kind");
        Check.That(restored.Entries.Any(e => e.Prefab == "AxeBronze" && e.Quality == 3), "quality survives the round trip");
        Check.That(restored.TotalCount == source.TotalCount, "nothing is gained or lost in a round trip");

        // Malformed saves must never stop a resume — they just lose what made no sense.
        var salvaged = new RunStash();
        salvaged.Restore(
            new List<string> { "Wood", null, "", "Stone", "Coal" },
            new List<int> { 10, 5, 5, 0 },              // short, and Stone has a zero count
            new List<int> { 1 },                        // shorter still
            null);                                      // absent entirely
        Check.That(salvaged.Entries.Count == 1 && salvaged.Entries[0].Prefab == "Wood",
            "a malformed save keeps only the entries that make sense");
        Check.That(salvaged.Entries[0].Quality == 1 && salvaged.Entries[0].Variant == 0,
            "missing quality/variant fall back to sane defaults");

        // A save that recorded one kind twice collapses rather than showing two rows that act as one.
        var dupes = new RunStash();
        dupes.Restore(
            new List<string> { "Wood", "Wood" },
            new List<int> { 10, 15 },
            new List<int> { 1, 1 },
            new List<int> { 0, 0 });
        Check.That(dupes.Entries.Count == 1 && dupes.Entries[0].Count == 25, "duplicate saved kinds merge on restore");

        var cleared = new RunStash();
        cleared.Deposit("Wood", 10, 1, 0);
        cleared.Clear();
        Check.That(cleared.Entries.Count == 0, "clearing empties the stash");
    }
}
