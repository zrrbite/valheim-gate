using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Tracks what a run has LENT against a set of named values, so several lenders can modify the
    /// same value without corrupting each other.
    ///
    /// This exists because the obvious design is wrong, and was wrong in shipped code. Letting each
    /// lender snapshot "the original" for itself works only while there is exactly one of them:
    ///
    ///   Hearty applies      -> snapshots 25, sets 40
    ///   Glass Cannon applies -> snapshots 40 as "original", sets 32.5
    ///   Hearty is lost      -> restores ITS snapshot, 25, wiping Glass Cannon entirely
    ///   Glass Cannon is lost -> restores ITS snapshot, 40 — a value that was never original
    ///
    /// The player ends the run with more base health than they started with, permanently, which is
    /// precisely what "power is loaned" exists to prevent.
    ///
    /// So the original is held ONCE PER FIELD, every lender merely declares a contribution, and the
    /// live value is always recomputed as original + sum(contributions). Order stops mattering,
    /// removal is exact, and unwinding everything is a single assignment back to the original.
    ///
    /// Pure arithmetic and no game types, so the part that was wrong is the part that is tested.
    /// </summary>
    public class LoanLedger
    {
        private readonly Dictionary<string, float> originals = new Dictionary<string, float>();
        private readonly Dictionary<string, Dictionary<string, float>> contributions =
            new Dictionary<string, Dictionary<string, float>>();

        /// <summary>Fields that currently have an original recorded.</summary>
        public IEnumerable<string> Fields => originals.Keys;

        /// <summary>True once <see cref="SetOriginal"/> has recorded this field's pristine value.</summary>
        public bool HasOriginal(string field) => field != null && originals.ContainsKey(field);

        /// <summary>
        /// Records a field's pristine value. Ignored if one is already recorded — re-taking it later
        /// would capture an already-loaned number and bake the loan in permanently, which is the
        /// whole failure this class prevents.
        ///
        /// Use <see cref="Forget"/> when the value genuinely belongs to something else now (a
        /// respawn hands back a fresh player with vanilla fields).
        /// </summary>
        public void SetOriginal(string field, float value)
        {
            if (field == null || originals.ContainsKey(field)) return;
            originals[field] = value;
        }

        /// <summary>The pristine value, or 0 when none was recorded.</summary>
        public float Original(string field) =>
            field != null && originals.TryGetValue(field, out var v) ? v : 0f;

        /// <summary>
        /// Declares (or replaces) one lender's contribution to a field. Replacing rather than adding
        /// is what lets a contribution CHANGE — the per-task health reward grows as the run goes on,
        /// and re-lending each time must not compound.
        /// </summary>
        public void Lend(string field, string lender, float amount)
        {
            if (field == null || lender == null) return;

            if (!contributions.TryGetValue(field, out var byLender))
            {
                byLender = new Dictionary<string, float>();
                contributions[field] = byLender;
            }

            byLender[lender] = amount;
        }

        /// <summary>Withdraws one lender's contribution to one field. Unknown pairs are ignored.</summary>
        public void Repay(string field, string lender)
        {
            if (field == null || lender == null) return;
            if (contributions.TryGetValue(field, out var byLender)) byLender.Remove(lender);
        }

        /// <summary>Withdraws a lender's contribution from EVERY field it lent to.</summary>
        public void RepayAll(string lender)
        {
            if (lender == null) return;
            foreach (var byLender in contributions.Values) byLender.Remove(lender);
        }

        /// <summary>True when any lender is currently contributing to this field.</summary>
        public bool HasContributions(string field) =>
            field != null && contributions.TryGetValue(field, out var byLender) && byLender.Count > 0;

        /// <summary>
        /// What the field should read right now: its pristine value plus every live contribution.
        ///
        /// Always computed from the original, never from the field's current value, which is what
        /// makes this safe to apply as often as needed.
        /// </summary>
        public float Value(string field)
        {
            float value = Original(field);
            if (field != null && contributions.TryGetValue(field, out var byLender))
            {
                foreach (var amount in byLender.Values) value += amount;
            }
            return value;
        }

        /// <summary>Drops a field's original and every contribution to it.</summary>
        public void Forget(string field)
        {
            if (field == null) return;
            originals.Remove(field);
            contributions.Remove(field);
        }

        /// <summary>Drops everything. The run is over; nothing is owed.</summary>
        public void Clear()
        {
            originals.Clear();
            contributions.Clear();
        }

        /// <summary>Fields this lender is currently contributing to — for repaying one boon's loans.</summary>
        public IEnumerable<string> FieldsLentBy(string lender) =>
            lender == null
                ? Enumerable.Empty<string>()
                : contributions.Where(kvp => kvp.Value.ContainsKey(lender)).Select(kvp => kvp.Key).ToList();
    }
}
