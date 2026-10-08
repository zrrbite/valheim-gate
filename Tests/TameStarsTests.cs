using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The star rule (2026-10-08, class balance): a summon starts with none, gains one per god, two at most; the
/// Hunter's Shepherd adds one more within the same two. It replaced Shepherd's 5000 health (the GM mod's pet buff).
/// </summary>
static class TameStarsTests
{
    public static void Run()
    {
        Check.That(TameStars.SummonLevel(0) == 1 && TameStars.SummonLevel(1) == 2 && TameStars.SummonLevel(2) == 3,
                   "a summon: no star in the Meadows, one per god (level 1, 2, 3)");
        Check.That(TameStars.SummonLevel(5) == 3 && TameStars.SummonLevel(-1) == 1,
                   "two stars at most, whatever the count; nothing below none");
        Check.That(TameStars.WithShepherd(1) == 2 && TameStars.WithShepherd(2) == 3 && TameStars.WithShepherd(3) == 3,
                   "Shepherd's star is one more, inside the same two");
        Check.That(TameStars.WithShepherd(TameStars.SummonLevel(0)) == 2 && TameStars.WithShepherd(TameStars.SummonLevel(1)) == 3,
                   "so a Hunter's wolves: one star in the Meadows, two from Act II");
        Check.That(TameStars.IsLegacyBlessing(5000f) && TameStars.IsLegacyBlessing(5200f) && !TameStars.IsLegacyBlessing(240f),
                   "the old Shepherd's 5000 health is recognisable, so a tame still carrying it can be repaired");
    }
}
