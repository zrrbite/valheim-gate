using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The barrow-keeper: Act II's speaker, one of the dead of the burial chambers. The first
    /// speaker on <see cref="SagaSpeaker"/>.
    /// </summary>
    /// <remarks>
    /// WHERE, NOT WHEN. The shade wants dark, Thjalfi wants rain, the thane wants day; the keeper
    /// wants a place - the door of the burial chamber nearest the player. The dead below ground do
    /// not keep the sky's hours, so he has no gate but the steps.
    ///
    /// WHAT HE IS FOR. The act asks where the light goes. The couriers show it being carried; he
    /// shows the light that was KEPT BACK - every green fire in the chamber walls is a light the
    /// dead buried rather than let the little ones carry it off. The cores the player digs out to
    /// build a smelter are those lights, and he asks for one back. In return he teaches the
    /// Stormsworn helm, the storm's piece the forest buried with the rest.
    ///
    /// Unnamed, like the shade and the thane. Thjalfi has a name because somebody wrote it down.
    /// </remarks>
    internal sealed class BarrowKeeper : SagaSpeaker
    {
        public enum Phase { Speak, Pay, Idle, After }

        public const string KeeperName = "The barrow-keeper";

        /// <summary>The location the keeper stands at - the one <c>bf-tomb</c> discovers.</summary>
        public const string ChamberLocation = "Crypt2";

        /// <summary>
        /// One rescued light, by display name - a saga clone's shared name is plain text, not a
        /// token. Checked against ObjectDB at run start (RunService.ValidateQuestPrices).
        /// </summary>
        public static readonly (string token, int amount, string label)[] Price =
        {
            (SagaItems.RescuedLightName, 1, "light"),
        };

        public const string AskLine =
            "You have been inside. I know, because the walls are dimmer.\n\n" +
            "Those were ours. Every green fire down there is a light somebody buried rather than let the " +
            "little ones carry it off. We could not keep ourselves, so we kept those, and we have kept them " +
            "lit for longer than this forest has had a name.\n\n" +
            "Burn them, if you must. The living need fire; I remember that much. But bring one back — one of " +
            "the lights the little ones are carrying. They run at night. Cut one loose, carry it down to me, " +
            "and I will give you what the storm left buried here.";

        public const string NotYetLine =
            "One light. Not stone, not cores — a light the little ones were carrying. They run at night.";

        public const string PaidLine =
            "There. It will burn longer down here than it would have burned in him.\n\n" +
            "The storm left a piece of itself in this forest, long ago, and we buried it with the rest. " +
            "Bronze, troll hide and coal, at a forge. Strike it and it is yours.";

        public const string IdleLine = "They are still lit. Go on.";

        /// <summary>
        /// After the Elder. Backed by the world: the chamber's green fires still burn after he dies,
        /// and the act's own chapter close says everything he was fed went out with him.
        /// </summary>
        public const string AfterLine =
            "Whatever he was fed went out with him. The forest is darker for it.\n\n" +
            "Not down here. Ours are still burning.";

        private const string GreetSpeak = "You took from the walls. Come here.";
        private const string GreetPay = "One light. Carried down, not dug up.";
        private const string GreetIdle = "Still lit.";
        private const string GreetAfter = "Still lit. Only ours, now.";

        /// <summary>How far from the chamber's centre he may stand: outside its door, not inside its walls.</summary>
        private const float MinDistance = 6f;
        private const float MaxDistance = 14f;

        private const int SpotAttempts = 24;
        private const float Waterline = 31f;
        private const int FlatSamples = 8;
        private const float FlatRadius = 2.5f;
        private const float FlatSpread = 2.5f;

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick; drives replies, greetings and the hover.</summary>
        public Phase Current = Phase.Speak;

        /// <summary>Raised by an interact, read and cleared by <see cref="Tick"/>.</summary>
        private bool _spokenPending;
        private bool _paidPending;
        private bool _afterSaid;

        public BarrowKeeper(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => KeeperName;

        protected override CreatureDressing.Look Look => CreatureDressing.Keeper();

        public override void Reset()
        {
            base.Reset();
            _spokenPending = false;
            _paidPending = false;
            _afterSaid = false;
        }

        /// <summary>Call about once a second. Reports what the player did at him.</summary>
        public void Tick(Player player, Phase phase, bool wanted, out bool spoken, out bool paid)
        {
            Current = phase;
            Stand(player, wanted);

            spoken = _spokenPending;
            paid = _paidPending;
            _spokenPending = false;
            _paidPending = false;
        }

        protected override string Greeting =>
            Current == Phase.Speak ? GreetSpeak
            : Current == Phase.Pay ? GreetPay
            : Current == Phase.After ? GreetAfter
            : GreetIdle;

        public override string HoverText(Player player)
        {
            if (Current == Phase.Pay)
            {
                string have = player != null ? $" ({PriceProgress(player.GetInventory(), Price)})" : string.Empty;
                return KeeperName + have + "\n[<color=yellow><b>$KEY_Use</b></color>] Give him the light";
            }
            return KeeperName + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public override bool OnInteract(Humanoid user, bool alt)
        {
            switch (Current)
            {
                case Phase.Speak:
                    Say(AskLine);
                    _spokenPending = true;
                    return true;

                case Phase.Pay:
                    if (TryPay(user != null ? user.GetInventory() : null, Price))
                    {
                        Say(PaidLine);
                        _paidPending = true;
                    }
                    else
                    {
                        Say(NotYetLine);
                    }
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

        /// <summary>
        /// The chamber nearest the player, then level dry ground a few metres from its centre.
        /// </summary>
        /// <remarks>
        /// Chamber doors sit in hillsides, so a fixed offset would stand him in rock or on a slope:
        /// the thane's flatness ring, on generated terrain, picks the spot. Strict first, then any dry
        /// ground, then the centre plus a fixed step, logged - a keeper in an odd place beats none.
        /// Null until the location system answers, so the host simply tries again next second.
        /// </remarks>
        protected override Vector3? ChooseSpot(Player player)
        {
            var zone = ZoneSystem.instance;
            if (zone == null || player == null) return null;

            ZoneSystem.LocationInstance chamber;
            try
            {
                if (!zone.FindClosestLocation(ChamberLocation, player.transform.position, out chamber)) return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The barrow-keeper found no chamber: " + ex.Message);
                return null;
            }

            Vector3 door = chamber.m_position;
            Vector3 chosen;
            string pass;
            if (TryRing(door, true, out chosen)) pass = "level ground";
            else if (TryRing(door, false, out chosen)) pass = "the only dry ground near the door";
            else
            {
                chosen = door + new Vector3(MaxDistance, 0f, 0f);
                pass = "no dry ground near the door - placed anyway";
            }

            Debug.Log($"[ICanShowYouTheWorld] The barrow-keeper's door: chamber at {door:0.0}, " +
                      $"standing at {chosen:0.0} on {pass}.");
            return chosen;
        }

        private bool TryRing(Vector3 origin, bool strict, out Vector3 chosen)
        {
            chosen = origin;

            var gen = WorldGenerator.instance;
            if (gen == null) return false;

            for (int attempt = 0; attempt < SpotAttempts; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                Vector3 at = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                float h;
                try { h = gen.GetHeight(at.x, at.z); }
                catch { return false; }

                if (h <= Waterline) continue;
                if (strict && !IsFlatEnough(gen, at)) continue;

                chosen = new Vector3(at.x, h, at.z);
                return true;
            }

            return false;
        }

        private static bool IsFlatEnough(WorldGenerator gen, Vector3 at)
        {
            float low = float.MaxValue;
            float high = float.MinValue;

            for (int i = 0; i < FlatSamples; i++)
            {
                float angle = (float)i / FlatSamples * Mathf.PI * 2f;
                Vector3 s = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * FlatRadius;

                float h;
                try { h = gen.GetHeight(s.x, s.z); }
                catch { return false; }

                if (h <= Waterline) return false;
                if (h < low) low = h;
                if (h > high) high = h;
            }

            return high - low <= FlatSpread;
        }
    }
}
