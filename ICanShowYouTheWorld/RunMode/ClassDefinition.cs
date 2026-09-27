namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// A way: the way of one of the fallen, taken up at their grave. One per run.
    ///
    /// Holds boon IDS, not effects. A way's passive and abilities are ordinary boons carrying a
    /// <see cref="BoonDefinition.ClassId"/>, granted through <see cref="BoonEngine.Grant"/>, so
    /// everything downstream — apply, repay, save, restore, the ability bar — is the one path
    /// boons already have. This type is only the syllabus.
    /// </summary>
    public class ClassDefinition
    {
        public string Id;
        public string Display;

        /// <summary>The fallen one whose way this is — the name on the grave.</summary>
        public string Title;

        public string Description;

        /// <summary>Granted at the choice, alongside rung 0.</summary>
        public string[] PassiveBoonIds;

        /// <summary>
        /// <c>Rungs[i]</c> is the boon ids granted at rung i. Three rungs, each paired by index
        /// with <see cref="ClassLadder.Thresholds"/>.
        /// </summary>
        public string[][] Rungs;

        /// <summary>
        /// Items handed over ONCE, at the moment the way is taken up at the graves, as
        /// (prefab, count). Empty for a way that gives nothing to hold.
        /// </summary>
        /// <remarks>
        /// Not a boon, and deliberately not on the ladder: a thing in the pack is saved with the
        /// pack, so it needs no reapply on resume and no repayment at run end, and it can be lost
        /// the way any item can. The host grants these from the thane's card only - never from the
        /// dev class cycle, which would otherwise fill the pack with a pair per press.
        /// </remarks>
        public (string prefab, int count)[] GrantItems = new (string, int)[0];
    }
}
