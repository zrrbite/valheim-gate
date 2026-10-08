using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Land near the ship whose flyers can come out over the water, by the act it belongs to.</summary>
    public enum SeaCoast { Mountain, Plains, Mistlands, Ashlands }

    public enum SeaCreature { Serpent, Bonemaw, Drake, Deathsquito, Gjall, FallenValkyrie }

    /// <summary>What the voyage clock says this second: nothing, a roll of the dice, or a certain encounter (the horn).</summary>
    public enum SeaTurn { None, Roll, Certain }

    /// <summary>One encounter: what comes, how many, how strong, from where, and what is said.</summary>
    public sealed class SeaEncounter
    {
        public SeaCreature Creature;
        public string Prefab;
        public int Count;
        public int Level;
        public bool Flies;
        public SeaCoast? From;
        public string Message;
    }

    /// <summary>
    /// The sea answers the heat (2026-10-08; docs/superpowers/specs/2026-10-08-sea-danger-design.md). The owner:
    /// "It would be nice to have waters be more treacherous". Heat 0 is vanilla's sea; from there the average gap
    /// between encounters, cooldown included, falls to 2 minutes at full heat.
    /// </summary>
    /// <remarks>
    /// Pure: the game side (SeaWatch) reads the ship, the land and the heat, and asks these rules what happens.
    /// </remarks>
    public static class SeaDanger
    {
        public const int FirstActIndex = 1;      // Act II
        public const int AshlandsActIndex = 6;   // Act VII
        public const float RollSeconds = 10f;
        public const float QuietSeconds = 60f;
        public const float VoyageEndSeconds = 120f;
        public const float CooldownSeconds = 90f;
        public const int MaxLive = 2;
        public const double FlyerShare = 2.0 / 3.0;

        /// <summary>How far up the dial heat has the sea: 0 (calm) to 1 (full).</summary>
        public static float Dial(float heat, float fullHeat)
        {
            if (fullHeat <= 0f || heat <= 0f) return 0f;
            return Math.Min(1f, heat / fullHeat);
        }

        /// <summary>The average minutes between encounters, cooldown included; infinite when the sea is calm.</summary>
        public static double GapMinutes(float h, float peakPerMinute) =>
            h <= 0f || peakPerMinute <= 0f ? double.PositiveInfinity : 1.0 / (peakPerMinute * h);

        /// <summary>
        /// The chance that a 10 s roll brings an encounter. Rolls happen only outside the cooldown, so the rate is
        /// what is left of the gap after it - never more than one per roll-time.
        /// </summary>
        public static double ChancePerRoll(float h, float peakPerMinute)
        {
            double gap = GapMinutes(h, peakPerMinute);
            if (double.IsInfinity(gap)) return 0.0;
            double rollMinutes = RollSeconds / 60.0;
            double wait = Math.Max(gap - CooldownSeconds / 60.0, rollMinutes);
            return 1.0 - Math.Exp(-rollMinutes / wait);
        }

        /// <summary>The game's level: 1 (no star), 2 (one star), 3 (two stars).</summary>
        public static int Level(float h) => h < 1f / 3f ? 1 : h < 2f / 3f ? 2 : 3;

        private static bool HighHeat(float h) => h >= 2f / 3f;

        public static int Count(SeaCreature creature, float h)
        {
            switch (creature)
            {
                case SeaCreature.Drake: return HighHeat(h) ? 2 : 1;
                case SeaCreature.Deathsquito: return HighHeat(h) ? 3 : 2;
                default: return 1;
            }
        }

        public static int ActIndexOf(SeaCoast coast)
        {
            switch (coast)
            {
                case SeaCoast.Mountain: return 3;
                case SeaCoast.Plains: return 4;
                case SeaCoast.Mistlands: return 5;
                default: return AshlandsActIndex;
            }
        }

        public static SeaCreature FlyerOf(SeaCoast coast)
        {
            switch (coast)
            {
                case SeaCoast.Mountain: return SeaCreature.Drake;
                case SeaCoast.Plains: return SeaCreature.Deathsquito;
                case SeaCoast.Mistlands: return SeaCreature.Gjall;
                default: return SeaCreature.FallenValkyrie;
            }
        }

        public static string PrefabOf(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return "BonemawSerpent";
                case SeaCreature.Drake: return "Hatchling";
                case SeaCreature.Deathsquito: return "Deathsquito";
                case SeaCreature.Gjall: return "Gjall";
                case SeaCreature.FallenValkyrie: return "FallenValkyrie";
                default: return "Serpent";
            }
        }

        public static bool Flies(SeaCreature creature) => creature != SeaCreature.Serpent && creature != SeaCreature.Bonemaw;

        public static string MessageOf(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return "The boiling sea gives up a Bonemaw.";
                case SeaCreature.Drake: return "Drakes come down off the mountains.";
                case SeaCreature.Deathsquito: return "Something whines over the water.";
                case SeaCreature.Gjall: return "A Gjall drifts out over the water.";
                case SeaCreature.FallenValkyrie: return "A fallen valkyrie rises over the ash coast.";
                default: return "Something rises off the bow.";
            }
        }

        public static IEnumerable<SeaCreature> All => (SeaCreature[])Enum.GetValues(typeof(SeaCreature));

        public static SeaEncounter Make(SeaCreature creature, SeaCoast? from, float h) => new SeaEncounter
        {
            Creature = creature,
            Prefab = PrefabOf(creature),
            Count = Count(creature, h),
            Level = Level(h),
            Flies = Flies(creature),
            From = from,
            Message = MessageOf(creature),
        };

        /// <summary>
        /// What comes: a reached coast's flyer two times in three, else the sea's own creature - a Bonemaw on the
        /// Ashlands sea once Act VII is reached, otherwise a Serpent. A coast of an act not yet reached is no
        /// candidate (no spoilers). <paramref name="rng"/> returns [0, 1).
        /// </summary>
        public static SeaEncounter Choose(IEnumerable<SeaCoast> coastsNear, bool onAshlandsSea, int actIndex, float h, Func<double> rng)
        {
            var reached = (coastsNear ?? Enumerable.Empty<SeaCoast>())
                .Distinct()
                .Where(c => actIndex >= ActIndexOf(c))
                .OrderBy(c => c)
                .ToList();

            if (reached.Count > 0 && rng() < FlyerShare)
            {
                var coast = reached[Math.Min(reached.Count - 1, (int)(rng() * reached.Count))];
                return Make(FlyerOf(coast), coast, h);
            }

            var sea = onAshlandsSea && actIndex >= AshlandsActIndex ? SeaCreature.Bonemaw : SeaCreature.Serpent;
            return Make(sea, null, h);
        }
    }

    /// <summary>
    /// The voyage clock: when a roll is due. A voyage starts on the first second at sea, is quiet for its first
    /// minute, and ends after two minutes off the sea. After each encounter, a cooldown; at most two at once.
    /// </summary>
    public sealed class SeaVoyage
    {
        private float _startedAt = float.NaN;
        private float _lastAtSea;
        private float _nextRollAt;
        private float _cooldownUntil = float.NegativeInfinity;
        private int _live;
        private bool _horn;

        public bool OnVoyage => !float.IsNaN(_startedAt);
        public int Live => _live;

        /// <summary>Once a second. Says whether to roll now, and keeps the voyage's clock.</summary>
        public SeaTurn Tick(float now, bool atSea)
        {
            if (!atSea)
            {
                if (OnVoyage && now - _lastAtSea >= SeaDanger.VoyageEndSeconds)
                {
                    _startedAt = float.NaN;
                    _horn = false;
                }
                return SeaTurn.None;
            }

            if (!OnVoyage)
            {
                _startedAt = now;
                _nextRollAt = now + SeaDanger.QuietSeconds;
            }
            _lastAtSea = now;

            if (now < _nextRollAt || now < _cooldownUntil || _live >= SeaDanger.MaxLive) return SeaTurn.None;

            _nextRollAt = now + SeaDanger.RollSeconds;
            if (_horn)
            {
                _horn = false;
                return SeaTurn.Certain;
            }
            return SeaTurn.Roll;
        }

        /// <summary>An encounter came: it counts toward the limit, and the cooldown starts.</summary>
        public void Arrived(float now)
        {
            _live++;
            _cooldownUntil = now + SeaDanger.CooldownSeconds;
        }

        /// <summary>An encounter is over (all dead, gone, or far behind).</summary>
        public void Ended()
        {
            if (_live > 0) _live--;
        }

        /// <summary>The Wind-horn: the next roll is certain. Forgotten when the voyage ends.</summary>
        public void HornBlown() => _horn = true;
    }
}
