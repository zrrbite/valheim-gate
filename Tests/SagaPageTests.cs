using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The saga's reward page: the myth cut at the run's last act, the run's own deeds woven in.
/// The rule that matters most is the spoiler rule - a run that ended at Yagluth must never be told
/// what came after.
/// </summary>
static class SagaPageTests
{
    const string Myth =
        "# The Saga of the Living One\n\n" +
        "*A note for the repo's reader, not the player.*\n\n" +
        "---\n\n" +
        "## Before the Beginning\n\nNothing could make its own light.\n\n" +
        "## The Stolen Light\n\n*Something is taking the light.*\n\n### Ashore\n\nYou came ashore.\n\n" +
        "> \"A quoted line.\"\n\n" +
        "## Where the Light Goes\n\nThe forest was fed.\n\n" +
        "## The Golden Ruin\n\nThe harvesters never sat down.\n\n" +
        "## A Light to Carry\n\nThe lantern-keeper waited in the mist.\n\n" +
        "## The Fire Goes Down\n\nThe raven went home.\n";

    static readonly string[] Titles = { "The Stolen Light", "Where the Light Goes", "The Golden Ruin", "A Light to Carry" };

    public static void Run()
    {
        var tale = SagaTale.Split(Myth, Titles);
        Check.That(tale.Prologue.Contains("Nothing could make its own light"), "the prologue is the text before the first act's tale");
        Check.That(!tale.Prologue.Contains("note for the repo"), "the myth's own note is not the player's prologue");
        Check.That(tale.Tales.Count == 4 && tale.Tales[1].Contains("The forest was fed"), "one tale per act title, in act order");
        Check.That(tale.Epilogue.Contains("The raven went home"), "the epilogue is the text after the last act's tale");
        Check.That(tale.Missing.Count == 0, "every act title found");
        Check.That(SagaTale.Split(Myth, new[] { "The Stolen Light", "No Such Act" }).Missing.SequenceEqual(new[] { "No Such Act" }),
                   "a title with no tale is reported, not guessed");

        var deeds = new List<SagaDeed>
        {
            new SagaDeed { ActNumeral = "I", Step = "Craft an axe", Line = "" },
            new SagaDeed { ActNumeral = "II", Step = "Speak with the barrow-keeper", Line = "He wanted a light back." },
            new SagaDeed { ActNumeral = "III", Step = "Defeat The Elder", Line = "" },
        };
        var numerals = new[] { "I", "II", "V", "VI" };

        // A run that ended at act index 2 (The Golden Ruin): no lantern-keeper, no epilogue.
        string ended = SagaPage.Compose(new SagaPageInput
        {
            Character = "Astrid <the>", Way = "Eydís, who hunted", Tale = tale, ActTitles = Titles, ActNumerals = numerals,
            LastActIndex = 2, Deeds = deeds, Date = "5 October 2026", Gods = 5, Time = "4:12:00", Heat = 61f, Score = 123.4f,
        });
        Check.That(ended.Contains("The harvesters never sat down"), "the run's last act is told");
        Check.That(!ended.Contains("lantern-keeper") && !ended.Contains("raven went home"),
                   "nothing past the run's last act - no later tale, no epilogue");
        Check.That(ended.Contains("more of the saga"), "a run that stopped short gets the coda instead");
        Check.That(ended.Contains("Astrid &lt;the&gt;") && !ended.Contains("Astrid <the>"), "the character name is escaped");
        Check.That(ended.Contains("Eydís, who hunted"), "the way taken is on the title page");
        Check.That(ended.IndexOf("He wanted a light back.") > ended.IndexOf("The forest was fed") &&
                   ended.IndexOf("He wanted a light back.") < ended.IndexOf("The harvesters never sat down"),
                   "a deed is told under its own act");
        Check.That(ended.Contains("<blockquote>") && ended.Contains("<h3>Ashore</h3>") && ended.Contains("<em>Something is taking the light.</em>"),
                   "the markdown subset converts: quotes, sub-headings, italics");

        // A run that reached the last act gets the epilogue.
        string whole = SagaPage.Compose(new SagaPageInput
        {
            Character = "Astrid", Tale = tale, ActTitles = Titles, ActNumerals = numerals, LastActIndex = 3, Deeds = deeds,
        });
        Check.That(whole.Contains("The lantern-keeper waited") && whole.Contains("The raven went home"),
                   "the last act and the epilogue for a run that went all the way");

        // An act the myth has no tale for yet (the Deep North) must not keep the epilogue from a run
        // that reached the last act it DOES tell.
        var withUntold = Titles.Concat(new[] { "What the Cold Keeps" }).ToArray();
        string untold = SagaPage.Compose(new SagaPageInput
        {
            Character = "Astrid", Tale = SagaTale.Split(Myth, withUntold), ActTitles = withUntold,
            ActNumerals = numerals.Concat(new[] { "VIII" }).ToArray(), LastActIndex = 3, Deeds = deeds,
        });
        Check.That(untold.Contains("The raven went home"), "a run at the last told act gets the epilogue even with an untold act after it");

        Check.That(SagaPage.MarkdownToHtml("a < b & **c**") == "<p>a &lt; b &amp; <strong>c</strong></p>",
                   "escape first, then the markup");
        Check.That(SagaPage.FileName("Ast/rid:", "2026-10-05 2215") == "Saga of Astrid - 2026-10-05 2215.html",
                   "the file name is sanitised");
    }
}
