using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// MACBOOK-TEMP (2026-10-06): the dev "go to" key. The owner tests on a MacBook with no mouse, so
/// travel and fighting are out; one key hops between the places the saga cares about.
/// </summary>
static class DevTourTests
{
    public static void Run()
    {
        var stops = new[]
        {
            new DevTour.Stop("the shade", 0), new DevTour.Stop("the thane", 0),
            new DevTour.Stop("the barrow-keeper", 1), new DevTour.Stop("Haldor's camp", 1),
            new DevTour.Stop("the drowned one", 2), new DevTour.Stop("the Bog Witch", 2),
            new DevTour.Stop("Bonemass's altar", 2),
        };

        var inActThree = DevTour.Order(stops, currentAct: 2).Select(s => s.Name).ToArray();
        Check.That(inActThree.SequenceEqual(new[]
                   {
                       "the drowned one", "the Bog Witch", "Bonemass's altar",
                       "the shade", "the thane", "the barrow-keeper", "Haldor's camp",
                   }),
                   "the current act's places come first, then the rest in act order, each act as given");

        var names = inActThree;
        Check.That(DevTour.Next(names, last: null, back: false) == 0 && DevTour.Next(names, last: null, back: true) == names.Length - 1,
                   "the first press goes to the first place; the first press back, to the last");
        Check.That(DevTour.Next(names, "the Bog Witch", back: false) == 2 && DevTour.Next(names, "the Bog Witch", back: true) == 0,
                   "after a place comes the next one, and back goes to the one before");
        Check.That(DevTour.Next(names, "Haldor's camp", back: false) == 0 && DevTour.Next(names, "the drowned one", back: true) == names.Length - 1,
                   "the tour wraps at both ends");
        Check.That(DevTour.Next(names, "a place that is gone", back: false) == 0,
                   "a place no longer on the list starts the tour again");
        Check.That(DevTour.Next(new string[0], null, back: false) == -1,
                   "nowhere to go is -1, not an error");
    }
}
