using System;
namespace ICanShowYouTheWorld
{
    public static class ModVersion
    {
        public const string VERSION = "1.0.15-run.2026-09-20o";

        /// <summary>
        /// Which flavour this DLL is: <c>"gm"</c> (the saga plus the old cheat mod) or
        /// <c>"saga"</c> (the saga alone).
        /// </summary>
        /// <remarks>
        /// BAKED IN at build time, by Scripts/setversion.sh, and deliberately not a config
        /// setting. The owner's requirement was "I dont want anyone to be able to reach the GM mod
        /// but me", and a config flag cannot give that: the recipient owns the config file. Nothing
        /// outside the build can change this.
        ///
        /// A STRING rather than a bool because a string lands in the assembly's metadata, so the
        /// release scripts can verify which flavour they are about to ship with the same
        /// decode-and-grep they already use for VERSION. A const bool is IL and cannot be checked
        /// that way - and "which build is this" is exactly the question that has to be answerable
        /// from the artefact rather than from whoever built it.
        ///
        /// NOT two project configurations, and not #if. The saga-only binary would be the one
        /// nobody plays and therefore the one to break silently, and the legacy CheatCommands
        /// pipeline has to survive in both builds because Run Mode's own effects ride it - so the
        /// conditional surface would have scattered across Cheat.cs, UIManager.cs and
        /// InputManager.cs. One binary, one code path, one constant.
        /// </remarks>
        public const string FLAVOUR = "gm";

        /// <summary>
        /// The flavour again, with a prefix nothing else in the assembly uses, so it can be found
        /// in the built DLL.
        /// </summary>
        /// <remarks>
        /// The release scripts read the flavour back OUT of the artefact rather than taking it on
        /// trust from whoever ran the build, and they do it by decoding the assembly as UTF-16 and
        /// searching - the same trick that reads VERSION, because .NET user strings live in the #US
        /// heap as UTF-16.
        ///
        /// A bare "gm" or "saga" cannot be found that way. The FIELD name is in the #Strings heap
        /// as UTF-8, so there is nothing to anchor a search to, and the two words are far too
        /// common to match on their own - which is exactly how the first attempt at this failed.
        /// This constant exists to be greppable and for no other reason. Concatenating two consts
        /// still folds to a single literal, so it costs one string in the heap.
        /// </remarks>
        public const string FlavourMarker = "ICSYTW_FLAVOUR_" + FLAVOUR;

        /// <summary>
        /// True when the old GM cheat mod is part of this build.
        /// </summary>
        /// <remarks>
        /// What this gates is DOORS, never the machinery: <see cref="CheatCommands"/> stays alive in
        /// every build because Run Mode's boons ride its pipeline (see BoonEffects'
        /// WithLegacyGodModeBracket). A saga-only build does not REGISTER the GM key bindings and
        /// does not draw the GM windows, which is a stronger version of what InputManager.Gate
        /// already does for the duration of a run.
        ///
        /// Worth being honest about what it buys: Valheim ships its own cheats, one launch option
        /// away (-console, then F5, then devcommands). This protects the saga's score from someone
        /// who never asked for GM. It is not a lock.
        /// </remarks>
        public static bool GmEnabled => FLAVOUR == "gm";
    }
}
