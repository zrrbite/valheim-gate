using System.Linq;
using ICanShowYouTheWorld.RunMode;

static class LoanLedgerTests
{
    public static void Run()
    {
        // The bug this class exists to prevent, reproduced as a test.
        //
        // Two lenders on ONE field. Under the old per-lender-snapshot design, Hearty snapshotted
        // 25 and set 40, Glass Cannon then snapshotted 40 as "original" and set 32.5 — and whichever
        // repaid first restored a number that was never original, leaving the player permanently
        // altered after the run. Here, order cannot matter.
        var hp = new LoanLedger();
        hp.SetOriginal("BaseHp", 25f);

        hp.Lend("BaseHp", "hearty", 15f);
        Check.That(hp.Value("BaseHp") == 40f, "one lender adds to the original");

        hp.Lend("BaseHp", "glasscannon", -7.5f);
        Check.That(hp.Value("BaseHp") == 32.5f, "two lenders compose against the same original");

        hp.Repay("BaseHp", "hearty");
        Check.That(hp.Value("BaseHp") == 17.5f, "repaying one lender leaves the other intact");

        hp.Repay("BaseHp", "glasscannon");
        Check.That(hp.Value("BaseHp") == 25f, "repaying every lender returns the pristine value");

        // The reverse order must give the identical result — that is the whole point.
        var reverse = new LoanLedger();
        reverse.SetOriginal("BaseHp", 25f);
        reverse.Lend("BaseHp", "glasscannon", -7.5f);
        reverse.Lend("BaseHp", "hearty", 15f);
        reverse.Repay("BaseHp", "glasscannon");
        Check.That(reverse.Value("BaseHp") == 40f, "repay order does not change the outcome");
        reverse.Repay("BaseHp", "hearty");
        Check.That(reverse.Value("BaseHp") == 25f, "and the original still comes back exactly");

        // The original is recorded ONCE. Re-taking it after a loan is live would capture the
        // loaned value and bake the loan in permanently.
        var sticky = new LoanLedger();
        sticky.SetOriginal("BaseHp", 25f);
        sticky.Lend("BaseHp", "hearty", 15f);
        sticky.SetOriginal("BaseHp", 40f);          // the mistake
        Check.That(sticky.Original("BaseHp") == 25f, "a second SetOriginal is ignored");
        sticky.Repay("BaseHp", "hearty");
        Check.That(sticky.Value("BaseHp") == 25f, "so the pristine value survives it");

        // Re-lending REPLACES rather than adds, which is what lets a contribution grow: the
        // per-completion health reward re-lends a larger amount every time a task finishes.
        var growing = new LoanLedger();
        growing.SetOriginal("BaseHp", 25f);
        growing.Lend("BaseHp", "tasks", 2f);
        growing.Lend("BaseHp", "tasks", 4f);
        growing.Lend("BaseHp", "tasks", 6f);
        Check.That(growing.Value("BaseHp") == 31f, "re-lending replaces, so a growing loan never compounds");

        // One lender across several fields — Tireless lends four at once.
        var many = new LoanLedger();
        many.SetOriginal("BaseStamina", 50f);
        many.SetOriginal("StaminaRegen", 6f);
        many.Lend("BaseStamina", "tireless", 25f);
        many.Lend("StaminaRegen", "tireless", 3f);
        Check.That(many.Value("BaseStamina") == 75f && many.Value("StaminaRegen") == 9f,
            "one lender can lend to several fields");
        Check.That(many.FieldsLentBy("tireless").OrderBy(f => f).SequenceEqual(new[] { "BaseStamina", "StaminaRegen" }),
            "the fields a lender touched are recoverable");

        many.RepayAll("tireless");
        Check.That(many.Value("BaseStamina") == 50f && many.Value("StaminaRegen") == 6f,
            "RepayAll returns every field the lender touched");
        Check.That(!many.FieldsLentBy("tireless").Any(), "and it lent nothing afterwards");

        // Tolerances, so a malformed call cannot take a run down.
        var edge = new LoanLedger();
        edge.SetOriginal(null, 5f);
        edge.Lend(null, "x", 1f);
        edge.Lend("BaseHp", null, 1f);
        edge.Repay(null, null);
        edge.RepayAll(null);
        Check.That(edge.Value(null) == 0f, "a null field reads as zero rather than throwing");
        Check.That(edge.Value("never-set") == 0f, "an unknown field reads as zero");
        Check.That(!edge.HasOriginal("never-set"), "an unknown field has no original");

        edge.SetOriginal("BaseHp", 25f);
        edge.Repay("BaseHp", "nobody");
        Check.That(edge.Value("BaseHp") == 25f, "repaying an unknown lender changes nothing");

        // Forget drops a field entirely — used when the value belongs to a different player now.
        var forgetful = new LoanLedger();
        forgetful.SetOriginal("BaseHp", 25f);
        forgetful.Lend("BaseHp", "hearty", 15f);
        Check.That(forgetful.HasContributions("BaseHp"), "the field has a live contribution");
        forgetful.Forget("BaseHp");
        Check.That(!forgetful.HasOriginal("BaseHp") && !forgetful.HasContributions("BaseHp"),
            "Forget drops the original and its contributions");
        forgetful.SetOriginal("BaseHp", 100f);
        Check.That(forgetful.Original("BaseHp") == 100f, "and a fresh original can then be recorded");

        var cleared = new LoanLedger();
        cleared.SetOriginal("BaseHp", 25f);
        cleared.Lend("BaseHp", "hearty", 15f);
        cleared.Clear();
        Check.That(!cleared.Fields.Any(), "Clear empties the ledger");
    }
}
