using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The one line a step gets when it becomes the step in play.
///
/// Small enough to look obviously correct and still worth pinning, because its whole job is a
/// distinction the class cannot observe: "this just became current" versus "this was already
/// current when I arrived". Getting that backwards does not throw, does not log, and shows up as
/// the saga greeting a resumed session with a line about something the player did an hour ago —
/// exactly the class of failure this mode keeps paying play sessions to find.
/// </summary>
static class StepOpeningTests
{
    static ChallengeDefinition Step(string id, string opening = null, string hint = null) =>
        new ChallengeDefinition { Id = id, Opening = opening, Hint = hint, Display = id };

    public static void Run()
    {
        // The first observation is history, not news.
        var fresh = new StepOpenings();
        var owed = fresh.Observe(new[] { Step("a", "line A"), Step("b", "line B") });
        Check.That(owed.Count == 0, "the first observation says nothing, however much it finds");

        // What changes afterwards is news.
        owed = fresh.Observe(new[] { Step("a", "line A"), Step("c", "line C") });
        Check.That(owed.Count == 1 && owed[0].Opening == "line C",
            "a step that becomes current afterwards says its line");

        // And says it once.
        owed = fresh.Observe(new[] { Step("a", "line A"), Step("c", "line C") });
        Check.That(owed.Count == 0, "and does not repeat it while it stays current");

        // A step that leaves and comes back is not new — it has been seen.
        owed = fresh.Observe(new[] { Step("a", "line A") });
        Check.That(owed.Count == 0, "a step leaving says nothing");
        owed = fresh.Observe(new[] { Step("a", "line A"), Step("c", "line C") });
        Check.That(owed.Count == 0, "and a step that comes back does not say its line twice");

        // A HINT IS NOT SPOKEN. It was, for one day, because hints exist for failures seen in play
        // and the Run window is not where a player mid-build is looking. That crowded the screen
        // badly: three tracks advance in parallel, so several steps open at once and each was queuing
        // two lines of large centre text.
        //
        // The split is the honest one. An opening is prose, heard once when a thing becomes true; a
        // hint is a shopping list, meant to be RE-read, and it is already on the HUD's step row and in
        // the BOOK. It is only safe to stop saying it because the HEARD page now keeps everything that
        // was said - before that page existed, not saying something was the same as losing it.
        var hinted = new StepOpenings();
        hinted.Observe(new[] { Step("seed") });
        owed = hinted.Observe(new[] { Step("seed"), Step("hint-only", null, "build it on the fire") });
        Check.That(owed.Count == 0, "a step whose only line is a hint stays quiet");

        // Both: the statement leads, the instruction follows. LineFor answers for the first only;
        // the pause before the second is the host's business, since it needs a clock.
        var both = Step("both", "The storm is his.", "At an improved bench.");
        Check.That(StepOpenings.LineFor(both) == "The storm is his.",
            "a step with both leads with the opening, never the instruction");
        Check.That(StepOpenings.HasSomethingToSay(both), "and it has something to say");

        Check.That(!StepOpenings.HasSomethingToSay(Step("mute")), "a step with neither says nothing");
        Check.That(!StepOpenings.HasSomethingToSay(null), "and a null definition is quiet, not fatal");
        Check.That(StepOpenings.LineFor(null) == null, "LineFor is quiet on null too");
        Check.That(StepOpenings.LineFor(Step("mute")) == null, "and on a step with no lines at all");

        // Steps with nothing to say are still recorded, so they cannot be new later.
        var quiet = new StepOpenings();
        quiet.Observe(new[] { Step("seed") });
        quiet.Observe(new[] { Step("seed"), Step("silent") });
        owed = quiet.Observe(new[] { Step("seed"), Step("silent", "a line it did not have before") });
        Check.That(owed.Count == 0, "a step already seen without a line does not gain one retroactively");

        // A resume is the case this exists for: a brand new instance seeing a step that has been
        // current for an hour must treat it as history.
        var resumed = new StepOpenings();
        Check.That(resumed.Observe(new[] { Step("mid-run", "line nobody should hear again") }).Count == 0,
            "a resume does not replay the line of the step it resumes on");

        // Null and empty are ordinary, not exceptional.
        var empty = new StepOpenings();
        Check.That(empty.Observe(null).Count == 0, "a null list is quiet rather than fatal");
        Check.That(empty.Observe(new ChallengeDefinition[0]).Count == 0, "an empty list is quiet too");
        Check.That(empty.Observe(new[] { Step("after-nothing", "line") }).Count == 1,
            "and the baseline still counts as taken, so the next step speaks");

        // A definition with no id cannot be tracked, so it is skipped rather than crediting itself
        // to the empty-string key and silencing the next one.
        var anonymous = new StepOpenings();
        anonymous.Observe(new[] { Step("seed") });
        owed = anonymous.Observe(new[] { Step("seed"), new ChallengeDefinition { Opening = "no id" }, Step("real", "real line") });
        Check.That(owed.Count == 1 && owed[0].Opening == "real line",
            "a definition with no id is skipped without swallowing the next one");
    }
}
