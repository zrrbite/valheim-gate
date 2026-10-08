using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The Berserker's Fury (2026-10-08, class balance): +5% weapon damage per landed hit, to +50% at ten (fifteen
    /// after the Queen). One hit's worth fades per second once a second passes without one; Blood Rage fills it and
    /// holds it. Pure: BoonEffects feeds it the game's EnemyHits counter.
    /// </summary>
    public sealed class FuryMeter
    {
        public const float PerHit = 0.05f;
        public const int BloodiedAt = 5;
        public const float FadeAfter = 1f;

        private float _lastHit = float.NegativeInfinity;
        private float _lastFade;
        private float _holdUntil = float.NegativeInfinity;

        public int Stacks { get; private set; }
        public int Max { get; private set; } = 10;
        public float Bonus => Stacks * PerHit;
        public bool Bloodied => Stacks >= BloodiedAt;

        public void SetMax(int max)
        {
            Max = Math.Max(1, max);
            if (Stacks > Max) Stacks = Max;
        }

        public void Hit(int n, float now)
        {
            if (n <= 0) return;
            Stacks = Math.Min(Max, Stacks + n);
            _lastHit = now;
            _lastFade = now;
        }

        public void Tick(float now)
        {
            if (now < _holdUntil) { Stacks = Max; _lastHit = now; _lastFade = now; return; }
            // A hold that has ended counts as the last hit: fading starts a second after it, not from before it.
            if (_holdUntil > _lastHit) { _lastHit = _holdUntil; _lastFade = _holdUntil; }
            if (Stacks == 0 || now - _lastHit < FadeAfter) return;
            int fades = (int)Math.Floor(now - Math.Max(_lastFade, _lastHit + FadeAfter - 1f));
            if (fades <= 0) return;
            Stacks = Math.Max(0, Stacks - fades);
            _lastFade += fades;
        }

        public void Fill(float now, float holdUntil)
        {
            Stacks = Max;
            _holdUntil = holdUntil;
            _lastHit = now;
            _lastFade = now;
        }

        public void AddHalf(float now) => Hit((Max + 1) / 2, now);

        /// <summary>Blood Rage ends (its timer, an unapply, or the run's end): the hold stops now, and the stacks fade from here.</summary>
        public void Release(float now)
        {
            if (_holdUntil > now) _holdUntil = now;
        }

        public void Reset()
        {
            Stacks = 0;
            _holdUntil = float.NegativeInfinity;
        }
    }
}
