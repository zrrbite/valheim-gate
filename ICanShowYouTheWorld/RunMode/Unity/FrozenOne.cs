using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The frozen one: Act IV's speaker, at the treeline nearest home. Someone who climbed to watch
    /// the light come back and froze where the cold begins. The third speaker on <see cref="SagaSpeaker"/>.
    /// </summary>
    /// <remarks>
    /// THE ACT IN ONE PERSON. The mountain's answer to the shortage is "it is already gone", and he
    /// refuses it. A FIRE wakes him - the hearth rule turned outward: a light that is only frozen,
    /// not gone, can be reached by warmth. He asks to hold a dragon egg, light that has not woken,
    /// before he will believe; and the egg is warm. He gives the Stormsworn greaves.
    ///
    /// WHERE: the nearest Mountain ground to the claimed bed - the act's epigraph is "Above the
    /// treeline, even light freezes", and he is exactly there. Found once per run through
    /// BiomeCompass, which is expensive and is therefore asked once.
    ///
    /// He is the Ghost in frost-white with a WARM amber core, so he can never be read as the shade
    /// (cold blue).
    /// </remarks>
    internal sealed class FrozenOne : SagaSpeaker
    {
        public enum Phase { Frozen, Waking, Pay, Idle, After }

        public const string FrozenName = "The frozen one";

        /// <summary>How near a burning fire must be to reach him.</summary>
        public const float FireRange = 5f;

        public static readonly (string token, int amount, string label)[] Price =
        {
            ("$item_dragonegg", 1, "dragon egg"),
        };

        public const string IceLine =
            "He does not move. Ice over his eyes, ice in his beard, and under it all, very deep, something " +
            "faintly warm. A fire might reach him.";

        public const string AskLine =
            "Is it back?\n\n" +
            "I came up to see it come back. When the light started going, below, I said it would come back, and " +
            "I would climb to where you can see the whole world at once and watch it. I got this far. This is " +
            "where the cold starts. I did not know it started so low.\n\n" +
            "No — do not tell me it is gone. Gone is a word for things you have seen go. Up there the great one " +
            "lays her eggs: light that has not woken yet. Bring me one. I want to hold one before I believe you.";

        public const string NotYetLine =
            "One of hers. From the peaks, where it is coldest. It is heavy, and it will not go through any door " +
            "you have built — you will have to carry it down.";

        public const string PaidLine =
            "It's warm. It's — warm.\n\n" +
            "Then it isn't gone. Not all of it. Take what the storm made for the cold; I never needed it, " +
            "standing still. Silver, wolf pelt and fang, at a forge built high.";

        public const string IdleLine = "I'll keep the egg warm. Somebody should.";

        /// <summary>After Moder. Backed by the act's own chapter close: what the mountain kept was still warm.</summary>
        public const string AfterLine =
            "Did you feel it go past? Up off the peaks, when she fell. It was warm. I was right.\n\n" +
            "It did not help me. Go on.";

        private const string GreetWaking = "...is it back?";
        private const string GreetPay = "One of hers. Please.";
        private const string GreetIdle = "Still warm.";
        private const string GreetAfter = "I was right.";

        private const float Waterline = 31f;
        private const int FootingAttempts = 16;
        private const float FootingRadius = 10f;
        private const float FlatRadius = 2.5f;
        private const float FlatSpread = 4f;

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick.</summary>
        public Phase Current = Phase.Frozen;

        private bool _wokenPending;
        private bool _paidPending;
        private bool _afterSaid;

        public FrozenOne(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => FrozenName;

        protected override CreatureDressing.Look Look => CreatureDressing.Frozen();

        public override void Reset()
        {
            base.Reset();
            _wokenPending = false;
            _paidPending = false;
            _afterSaid = false;
        }

        /// <summary>Call about once a second.</summary>
        public void Tick(Player player, Phase phase, bool wanted, out bool woken, out bool paid)
        {
            Current = phase;
            Stand(player, wanted);

            woken = _wokenPending;
            paid = _paidPending;
            _wokenPending = false;
            _paidPending = false;
        }

        /// <summary>A burning fire within <see cref="FireRange"/> of <paramref name="at"/>.</summary>
        public static bool FireNear(Vector3 at)
        {
            try
            {
                foreach (var fire in UnityEngine.Object.FindObjectsOfType<Fireplace>())
                {
                    if (fire == null) continue;
                    if (Vector3.Distance(fire.transform.position, at) > FireRange) continue;
                    if (fire.IsBurning()) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>No bubble while he is ice: a frozen man does not call out.</summary>
        protected override string Greeting =>
            Current == Phase.Waking ? GreetWaking
            : Current == Phase.Pay ? GreetPay
            : Current == Phase.Idle ? GreetIdle
            : Current == Phase.After ? GreetAfter
            : null;

        public override string HoverText(Player player)
        {
            if (Current == Phase.Frozen)
                return FrozenName + "\n<i>Frozen solid. A fire might reach him.</i>";
            if (Current == Phase.Pay)
            {
                string have = player != null ? $" ({PriceProgress(player.GetInventory(), Price)})" : string.Empty;
                return FrozenName + have + "\n[<color=yellow><b>$KEY_Use</b></color>] Give him the egg";
            }
            return FrozenName + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public override bool OnInteract(Humanoid user, bool alt)
        {
            switch (Current)
            {
                case Phase.Frozen:
                    Say(IceLine);
                    return true;
                case Phase.Waking:
                    Say(AskLine);
                    _wokenPending = true;
                    return true;
                case Phase.Pay:
                    if (TryPay(user != null ? user.GetInventory() : null, Price))
                    {
                        Say(PaidLine);
                        _paidPending = true;
                    }
                    else Say(NotYetLine);
                    return true;
                case Phase.After:
                    Say(_afterSaid ? IdleLine : AfterLine);
                    _afterSaid = true;
                    return true;
                default:
                    Say(IdleLine);
                    return true;
            }
        }

        /// <summary>The treeline: the nearest Mountain ground to the claimed bed, then footing near it.</summary>
        protected override Vector3? ChooseSpot(Player player)
        {
            if (player == null) return null;

            Vector3 from = Home() ?? player.transform.position;
            Vector3? edge = BiomeCompass.Nearest(from, Heightmap.Biome.Mountain);
            var gen = WorldGenerator.instance;
            if (edge == null || gen == null)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The frozen one found no mountain within range of home.");
                return null;
            }

            Vector3 chosen = new Vector3(edge.Value.x, SafeHeight(gen, edge.Value), edge.Value.z);
            string pass = "the edge itself";

            for (int attempt = 0; attempt < FootingAttempts; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float distance = (float)_rng.NextDouble() * FootingRadius;
                Vector3 at = edge.Value + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                if (gen.GetBiome(at) != Heightmap.Biome.Mountain) continue;
                float h = SafeHeight(gen, at);
                if (h <= Waterline || !IsFlatEnough(gen, at)) continue;

                chosen = new Vector3(at.x, h, at.z);
                pass = "level mountain ground";
                break;
            }

            Debug.Log($"[ICanShowYouTheWorld] The frozen one's treeline: {chosen:0.0}, " +
                      $"{Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(chosen.x, 0f, chosen.z)):0}m from " +
                      $"{(Home() != null ? "the claimed bed" : "the player")}, on {pass}.");
            return chosen;
        }

        private static float SafeHeight(WorldGenerator gen, Vector3 at)
        {
            try { return gen.GetHeight(at.x, at.z); }
            catch { return at.y; }
        }

        private static bool IsFlatEnough(WorldGenerator gen, Vector3 at)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                float h = SafeHeight(gen, at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * FlatRadius);
                if (h < low) low = h;
                if (h > high) high = h;
            }
            return high - low <= FlatSpread;
        }

        private static Vector3? Home()
        {
            try
            {
                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (profile != null && profile.HaveCustomSpawnPoint()) return profile.GetCustomSpawnPoint();
            }
            catch { }
            return null;
        }
    }
}
