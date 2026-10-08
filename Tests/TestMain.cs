using System;
using System.Collections.Generic;

static class Check
{
    public static int Failures;
    public static void That(bool condition, string label)
    {
        if (condition) { Console.WriteLine($"  ok: {label}"); }
        else { Failures++; Console.WriteLine($"FAIL: {label}"); }
    }
}

static class TestMain
{
    // Every test class registers a Run() call here.
    static int Main()
    {
        Console.WriteLine("RunMode tests");
        SelfTest();
        HeatModelTests.Run();
        ChallengeEngineTests.Run();
        BoonEngineTests.Run();
        BoonEngineTests.GrantTests();
        BoonEngineTests.ClassTests();
        ClassLadderTests.Run();
        NameManifestTests.Run();
        ActDefinitionTests.Run();
        HearthRecordsTests.Run();
        StepPredicateTests.Run();
        StepOpeningTests.Run();
        SagaRecipeCardTests.Run();
        ActDefinitionTests.SeatingTests();
        ActDefinitionTests.CarryCapTests();
        RunStashTests.Run();
        LoanLedgerTests.Run();
        TooltipPlacementTests.Run();
        SagaPageTests.Run();
        ReviewFollowUpTests.Run();
        ShipFittingsTests.Run();
        KeyLayoutTests.Run();
        ItemSourcesTests.Run();
        AfterwordTests.Run();
        DevTourTests.Run();
        DevSeaTests.Run();
        SeaDangerTests.Run();
        LandSpotTests.Run();
        TameStarsTests.Run();
        WayRulesTests.Run();
        RunCoinsTests.Run();
        Console.WriteLine(Check.Failures == 0 ? "ALL PASS" : $"{Check.Failures} FAILURES");
        return Check.Failures == 0 ? 0 : 1;
    }

    static void SelfTest() => Check.That(1 + 1 == 2, "harness self-test");
}
