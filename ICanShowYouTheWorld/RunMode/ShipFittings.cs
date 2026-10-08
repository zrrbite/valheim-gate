using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    public enum FittingKind { Sail, Hull, FireTar, WindHorn, Ward }

    /// <summary>What a run has fitted. Belongs to the RUN, not to a ship - see <see cref="ShipFittings"/>.</summary>
    public sealed class ShipFittingState
    {
        public int Sail;
        public int Hull;
        public int FireTar;
        public int WindHorn;
        public int Ward;
    }

    /// <summary>What the ward sees of one creature in its radius - read off the game by SeaWatch, judged here.</summary>
    public struct WardTarget
    {
        public bool Player, Tamed, Dead, LightningImmune;
        /// <summary><c>BaseAI.IsEnemy(player, it)</c>: true for nearly every wild creature, so never enough alone.</summary>
        public bool Enemy;
        /// <summary>Its AI is a MonsterAI. AnimalAI (deer, hares) never attacks; its alert is flight.</summary>
        public bool Monster;
        /// <summary>Its AI is alerted - hunting. Taming needs a calm creature, and the sea's own are alerted when sent.</summary>
        public bool Alerted;
    }

    /// <summary>One line on the helm's card: the next tier of one fitting, and its price.</summary>
    public sealed class ShipFittingOffer
    {
        public FittingKind Kind;
        public int Tier;
        public int Price;
        public string Name;
        public string Effect;
    }

    /// <summary>
    /// Ship fittings, bought with gold at any ship's helm (owner, 2026-10-05: "It would be cool if we
    /// could upgrade the boat with gold. Speed, armor, etc").
    /// </summary>
    /// <remarks>
    /// They FOLLOW THE CAPTAIN (owner's pick): the run owns them and every ship it sails wears them -
    /// raft, karve, longship - so moving up to a bigger ship loses nothing. Applied to the ship's live
    /// numbers from its own originals and given back at run end; nothing is written into the world.
    ///
    /// Sail scales the sail's push and the oars (the game paddles on m_backwardForce, both ways).
    /// Hull makes the hull resist every damage type - beaching and capsizing are blunt hits through
    /// the same resistances, so one number covers rocks, shallows and teeth. Fire-tar is the game's
    /// own m_ashlandsReady: the boiling sea stops burning the ship. It is offered only from the act the
    /// Ashlands belong to, because a card naming the boiling sea in Act II would spoil the road there.
    ///
    /// Prices are first numbers, to be tuned in play.
    /// </remarks>
    public static class ShipFittings
    {
        private static readonly int[] SailPrices = { 50, 150, 300 };
        private static readonly int[] HullPrices = { 50, 150, 300 };
        public const int FireTarPrice = 400;
        public const int WindHornPrice = 200;

        private static readonly float[] SailBoosts = { 1f, 1.15f, 1.30f, 1.50f };
        private static readonly float[] HullFactors = { 1f, 0.75f, 0.5f, 0.25f };
        private static readonly int[] WardPrices = { 100, 250, 450 };
        private static readonly float[] WardRadii = { 0f, 20f, 25f, 30f };
        private static readonly float[] WardDamages = { 0f, 20f, 40f, 70f };

        /// <summary>How often the ward strikes, in seconds.</summary>
        public const float WardPulseSeconds = 3f;

        public static int MaxTier(FittingKind kind) =>
            kind == FittingKind.FireTar || kind == FittingKind.WindHorn ? 1 : 3;

        public static int Tier(ShipFittingState state, FittingKind kind)
        {
            if (state == null) return 0;
            switch (kind)
            {
                case FittingKind.Sail: return state.Sail;
                case FittingKind.Hull: return state.Hull;
                case FittingKind.WindHorn: return state.WindHorn;
                case FittingKind.Ward: return state.Ward;
                default: return state.FireTar;
            }
        }

        /// <summary>Sail push and oars, as a multiplier on the ship's own.</summary>
        public static float SailMultiplier(int tier) => SailBoosts[Clamp(tier, SailBoosts.Length - 1)];
        public static float SailMultiplier(ShipFittingState state) => SailMultiplier(state?.Sail ?? 0);

        /// <summary>The share of damage the hull still takes: 1, 0.75, 0.5, 0.25.</summary>
        public static float HullDamageFactor(int tier) => HullFactors[Clamp(tier, HullFactors.Length - 1)];
        public static float HullDamageFactor(ShipFittingState state) => HullDamageFactor(state?.Hull ?? 0);

        /// <summary>
        /// The ward (2026-10-08): while the player is aboard, a lightning pulse every few seconds strikes every hostile
        /// within this radius of the ship - the sea's answer to the sea danger, passive, so it needs no key.
        /// </summary>
        public static float WardRadius(int tier) => WardRadii[Clamp(tier, WardRadii.Length - 1)];
        public static float WardDamage(int tier) => WardDamages[Clamp(tier, WardDamages.Length - 1)];

        /// <summary>
        /// The ward strikes attackers only: an alerted, hostile monster (final review, 2026-10-08). "An enemy" alone
        /// struck every deer by the dock and the boar half tamed; never the player, the tamed, the dead, or what
        /// lightning cannot hurt (every saga speaker is made immune to it).
        /// </summary>
        public static bool WardStrikes(WardTarget t) =>
            !t.Player && !t.Tamed && !t.Dead && !t.LightningImmune && t.Enemy && t.Monster && t.Alerted;

        public static bool AshlandsReady(ShipFittingState state) => state != null && state.FireTar >= 1;

        /// <summary>The horn is a thing you carry and blow (SagaWind), not a number on the ship.</summary>
        public static bool HasWindHorn(ShipFittingState state) => state != null && state.WindHorn >= 1;

        /// <summary>
        /// The next tier of each fitting not yet at its top. Fire-tar only once
        /// <paramref name="fireTarTold"/> - the Ashlands act has begun.
        /// <paramref name="wardTold"/> - the raven has said the sea came for you (the Ward, 2026-10-08).
        /// </summary>
        public static List<ShipFittingOffer> Offers(ShipFittingState state, bool fireTarTold, bool wardTold = false)
        {
            var offers = new List<ShipFittingOffer>();
            state = state ?? new ShipFittingState();

            if (state.Sail < MaxTier(FittingKind.Sail))
            {
                int t = state.Sail + 1;
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.Sail, Tier = t, Price = SailPrices[t - 1],
                    Name = "Sail " + Roman(t),
                    Effect = $"+{Percent(SailMultiplier(t) - 1f)}% to the sail and the oars",
                });
            }

            if (state.Hull < MaxTier(FittingKind.Hull))
            {
                int t = state.Hull + 1;
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.Hull, Tier = t, Price = HullPrices[t - 1],
                    Name = "Hull " + Roman(t),
                    Effect = t == 1 ? "the ship takes a quarter less damage"
                           : t == 2 ? "the ship takes half the damage"
                           : "the ship takes a quarter of the damage",
                });
            }

            // Moder's wind, before Moder: the game's own tailwind for two minutes, blown with the up
            // arrow at sea. Not named for her - a card naming the fourth god in Act II would spoil her.
            if (state.WindHorn < MaxTier(FittingKind.WindHorn))
            {
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.WindHorn, Tier = 1, Price = WindHornPrice,
                    Name = "Wind-horn",
                    Effect = "[\u2191] at sea: the wind at your back for two minutes",
                });
            }

            // The ward: offered once the raven has said the sea came for you, so the player knows what it is for.
            if (wardTold && state.Ward < MaxTier(FittingKind.Ward))
            {
                int t = state.Ward + 1;
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.Ward, Tier = t, Price = WardPrices[t - 1],
                    Name = "Ward " + Roman(t),
                    Effect = $"aboard: lightning strikes attackers within {WardRadius(t):0} m every {WardPulseSeconds:0} s",
                });
            }

            if (fireTarTold && state.FireTar < MaxTier(FittingKind.FireTar))
            {
                offers.Add(new ShipFittingOffer
                {
                    Kind = FittingKind.FireTar, Tier = 1, Price = FireTarPrice,
                    Name = "Fire-tar",
                    Effect = "the boiling sea no longer burns the ship",
                });
            }

            return offers;
        }

        /// <summary>The state with one more tier of <paramref name="kind"/>, capped at its top.</summary>
        public static ShipFittingState Bought(ShipFittingState state, FittingKind kind)
        {
            var s = state ?? new ShipFittingState();
            var next = new ShipFittingState { Sail = s.Sail, Hull = s.Hull, FireTar = s.FireTar, WindHorn = s.WindHorn, Ward = s.Ward };
            switch (kind)
            {
                case FittingKind.Sail: next.Sail = Math.Min(next.Sail + 1, MaxTier(kind)); break;
                case FittingKind.Hull: next.Hull = Math.Min(next.Hull + 1, MaxTier(kind)); break;
                case FittingKind.WindHorn: next.WindHorn = Math.Min(next.WindHorn + 1, MaxTier(kind)); break;
                case FittingKind.Ward: next.Ward = Math.Min(next.Ward + 1, MaxTier(kind)); break;
                default: next.FireTar = Math.Min(next.FireTar + 1, MaxTier(kind)); break;
            }
            return next;
        }

        /// <summary>"Sail II · Hull I · Fire-tar", or empty for a bare ship.</summary>
        public static string Summary(ShipFittingState state)
        {
            if (state == null) return string.Empty;
            var parts = new List<string>();
            if (state.Sail > 0) parts.Add("Sail " + Roman(state.Sail));
            if (state.Hull > 0) parts.Add("Hull " + Roman(state.Hull));
            if (state.Ward > 0) parts.Add("Ward " + Roman(state.Ward));
            if (state.WindHorn > 0) parts.Add("Wind-horn");
            if (state.FireTar > 0) parts.Add("Fire-tar");
            return string.Join(" · ", parts.ToArray());
        }

        private static int Clamp(int tier, int max) => tier < 0 ? 0 : tier > max ? max : tier;

        private static int Percent(float f) => (int)Math.Round(f * 100f);

        private static string Roman(int n) => n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n.ToString();
    }
}
