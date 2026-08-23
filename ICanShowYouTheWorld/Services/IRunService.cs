using System.Collections.Generic;
using ICanShowYouTheWorld.RunMode;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Orchestrates Run Mode: lifecycle (start/abandon/finish), per-frame polling,
    /// resume-after-reload, and scoring. The engines it owns (challenges, boons, heat)
    /// are pure logic; this service is the only piece that touches the live game.
    /// </summary>
    public interface IRunService
    {
        /// <summary>True while a run is in progress.</summary>
        bool IsRunActive { get; }

        /// <summary>Wall-clock seconds elapsed in the current run (0 when inactive).</summary>
        float ElapsedSeconds { get; }

        /// <summary>Current heat (0 when inactive).</summary>
        float Heat { get; }

        /// <summary>
        /// Live score while a run is active; the score of the last finished run otherwise.
        /// </summary>
        float CurrentScore { get; }

        /// <summary>Boss splits recorded this run, formatted as "Eikthyr  12:34".</summary>
        IReadOnlyList<string> Splits { get; }
        HearthRecords Records { get; }

        /// <summary>Challenge engine for the current run; null when inactive.</summary>
        ICanShowYouTheWorld.RunMode.ChallengeEngine Challenges { get; }

        /// <summary>
        /// The act the run is currently in; null when inactive. Derived from the world's defeated
        /// bosses rather than stored, so it cannot disagree with the world.
        /// </summary>
        ICanShowYouTheWorld.RunMode.ActDefinition CurrentAct { get; }

        /// <summary>
        /// The run's stash — things set aside that follow the player between bases and acts; null
        /// when inactive. See <see cref="ICanShowYouTheWorld.RunMode.RunStash"/>.
        /// </summary>
        IReadOnlyList<ICanShowYouTheWorld.RunMode.StashEntry> StashEntries { get; }

        /// <summary>Unspent Homeward charges — one per boss felled, spent with Keypad 9.</summary>
        int HomewardCharges { get; }
        bool HomewardReady { get; }
        float HomewardCooldown { get; }

        /// <summary>
        /// Where Eikthyr's Herald is ("north-east, 180m") while its questline step is in play;
        /// null otherwise.
        /// </summary>
        string QuestBearing { get; }

        /// <summary>Max health lent by completions so far this run.</summary>
        float EarnedHealth { get; }

        /// <summary>Moves every unequipped material into the stash; returns how many items moved.</summary>
        int DepositMaterials();

        /// <summary>Takes everything of one stashed kind back, by index into <see cref="StashEntries"/>.</summary>
        void WithdrawStash(int index);

        /// <summary>Boon engine for the current run; null when inactive.</summary>
        ICanShowYouTheWorld.RunMode.BoonEngine Boons { get; }

        /// <summary>
        /// Whether the injected Character.OnDeath hook can be relied upon. True once the hook
        /// has actually fired, and optimistically true for a 60s grace window from the start
        /// of a run — the hook can only prove itself once something dies.
        ///
        /// The challenge pool is built optimistically, so kill challenges are dealt regardless.
        /// If the window closes with the hook still silent, the run raises a HUD notice; the
        /// active set is not re-drawn mid-run.
        /// </summary>
        bool KillHookAvailable { get; }

        /// <summary>Begin a new run. No-op (with a HUD message) if a run cannot be started.</summary>
        void StartRun();

        /// <summary>Give up the current run: restore world modifiers, drop boons, delete saved state.</summary>
        void AbandonRun();

        /// <summary>Reroll the challenge in the given slot, charging the configured heat cost.</summary>
        void RerollChallenge(int slot);

        /// <summary>Per-frame update, driven by CheatController.Update.</summary>
        void Tick(float dt);

        /// <summary>Permanent-record one-liner for the lobby UI.</summary>
        string LobbySummary();
    }
}
