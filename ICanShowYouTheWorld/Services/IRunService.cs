using System.Collections.Generic;

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

        /// <summary>Challenge engine for the current run; null when inactive.</summary>
        ICanShowYouTheWorld.RunMode.ChallengeEngine Challenges { get; }

        /// <summary>Boon engine for the current run; null when inactive.</summary>
        ICanShowYouTheWorld.RunMode.BoonEngine Boons { get; }

        /// <summary>
        /// Whether the injected Character.OnDeath hook can be relied upon. True once the
        /// hook has actually fired, and optimistically true during a short startup grace
        /// period. When false, kill-based challenges are excluded from the pool.
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
