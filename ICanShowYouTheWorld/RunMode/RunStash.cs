using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// One kind of thing in the stash. Quality and variant are part of the IDENTITY, not extra
    /// data: a level-3 bronze axe and a level-1 one are different objects and must never merge into
    /// a single stack, or depositing two would hand back two of whichever was written last.
    /// </summary>
    public class StashEntry
    {
        public string Prefab;
        public int Count;
        public int Quality;
        public int Variant;

        public bool SameKind(string prefab, int quality, int variant) =>
            Prefab == prefab && Quality == quality && Variant == variant;
    }

    /// <summary>
    /// The run's stash: things put aside that follow the player between bases and acts.
    ///
    /// It exists because the saga asks you to abandon a house every act — "we have to leave our
    /// house behind to go to Act x, so we don't have to carry everything". A chest cannot follow
    /// you, so the stash is not a chest: it is run state, reachable wherever the run window opens.
    ///
    /// Deliberately NOT an inventory. There is no grid, no weight, no slots — just quantities of
    /// kinds of thing, because everything that makes an inventory interesting (space pressure,
    /// what to carry) is the decision the stash exists to remove for STORED goods and preserve for
    /// carried ones.
    ///
    /// Durability is not stored. A withdrawn tool comes back at full durability, which is a small
    /// gift rather than a loss, and the alternative is persisting per-instance state for every
    /// stacked item to avoid one edge case nobody would notice.
    /// </summary>
    public class RunStash
    {
        /// <summary>
        /// Distinct kinds the stash will hold. A cap exists because the stash is written into the
        /// run's save file on every autosave, and an unbounded one would grow that file without
        /// limit. High enough that no honest player meets it.
        /// </summary>
        public const int MaxEntries = 128;

        private readonly List<StashEntry> entries = new List<StashEntry>();

        public IReadOnlyList<StashEntry> Entries => entries;

        /// <summary>Total items held, across every kind. For the UI's summary line.</summary>
        public int TotalCount => entries.Sum(e => e.Count);

        public bool IsFull => entries.Count >= MaxEntries;

        /// <summary>
        /// Adds a stack, merging into a matching kind when there is one.
        ///
        /// Returns how many were actually taken: everything, or nothing when the deposit would need
        /// a new kind and the stash is full. Deliberately all-or-nothing per stack rather than
        /// partial — a deposit that silently swallowed half a stack and left the rest would be
        /// worse than one that plainly refuses.
        /// </summary>
        public int Deposit(string prefab, int count, int quality, int variant)
        {
            if (string.IsNullOrEmpty(prefab) || count <= 0) return 0;

            var existing = entries.FirstOrDefault(e => e.SameKind(prefab, quality, variant));
            if (existing != null)
            {
                existing.Count += count;
                return count;
            }

            if (IsFull) return 0;

            entries.Add(new StashEntry { Prefab = prefab, Count = count, Quality = quality, Variant = variant });
            return count;
        }

        /// <summary>
        /// Takes up to <paramref name="count"/> of one kind, addressed by index, and returns how
        /// many came out. An emptied kind is removed so the list does not fill with zeroes.
        ///
        /// Index-addressed because the UI has a row per entry and quality/variant make prefab alone
        /// an ambiguous key — two rows can share a prefab.
        /// </summary>
        public int Withdraw(int index, int count)
        {
            if (index < 0 || index >= entries.Count || count <= 0) return 0;

            var entry = entries[index];
            int taken = Math.Min(count, entry.Count);
            entry.Count -= taken;

            if (entry.Count <= 0) entries.RemoveAt(index);

            return taken;
        }

        /// <summary>Everything of one kind, by index.</summary>
        public int WithdrawAll(int index) =>
            index < 0 || index >= entries.Count ? 0 : Withdraw(index, entries[index].Count);

        public void Clear() => entries.Clear();

        /// <summary>
        /// Rebuilds from saved parallel lists, tolerating any malformed combination the way the
        /// rest of Run Mode's persistence does: a short or absent list, a null prefab, a
        /// non-positive count. A hand-edited save must never stop a run resuming — it just loses
        /// the entries that made no sense.
        ///
        /// Entries are merged on the way in, so a save that somehow recorded the same kind twice
        /// collapses rather than presenting two rows that behave as one.
        /// </summary>
        public void Restore(IList<string> prefabs, IList<int> counts, IList<int> qualities, IList<int> variants)
        {
            entries.Clear();
            if (prefabs == null) return;

            for (int i = 0; i < prefabs.Count; i++)
            {
                string prefab = prefabs[i];
                int count = counts != null && i < counts.Count ? counts[i] : 0;
                int quality = qualities != null && i < qualities.Count ? qualities[i] : 1;
                int variant = variants != null && i < variants.Count ? variants[i] : 0;

                if (string.IsNullOrEmpty(prefab) || count <= 0) continue;

                Deposit(prefab, count, quality, variant);
            }
        }
    }
}
