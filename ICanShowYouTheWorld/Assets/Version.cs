using System;
namespace ICanShowYouTheWorld
{
    public static class ModVersion
    {
        // <game>-run.alpha<N>.<BUILD>
        //
        // N moves for something worth a play-test brief: a mechanic, a content change, a
        // new system. BUILD moves for everything else — a fix, a tuning number, a nudged
        // panel. It exists because forty-two alphas in a day made the alpha number
        // meaningless (owner: "bumping the alpha version itself is a bit too much").
        //
        // What does NOT change: every deployed build gets a UNIQUE version. The popup is
        // the only way to be certain which build is being played, so "small change, leave
        // the version alone" is never the answer — bump the build instead.
        public const string VERSION = "0.221.12-run.alpha42.2";
    }
}
