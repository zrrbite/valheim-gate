using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The lantern-keeper: Act VI's speaker, a dvergr at a dvergr site in the Mistlands. The fifth
    /// speaker on <see cref="SagaSpeaker"/>, and the first who is ALIVE - not one of the put-away.
    /// </summary>
    /// <remarks>
    /// THE SAGA'S PAYOFF, SAID BY THE RIGHT PERSON. The dvergr borrow light and give it back; it is
    /// the only answer to the shortage that has ever worked. He sees the lights the player has carried
    /// since the meadows and says what they are: carried honestly, before anyone taught it.
    ///
    /// His ask - set one free, at night - accepts EITHER a rescued light (Acts I-II) or a wisp caught
    /// in the mist. Not every player still holds a rescued light by Act VI (the bow and the shield
    /// spend them), and an ask that cannot be paid stalls an act. Light rises only in the dark, so by
    /// day he says to come back after sundown.
    /// </remarks>
    internal sealed class LanternKeeper : SagaSpeaker
    {
        public enum Phase { Speak, Free, Idle, After }

        public const string KeeperName = "The lantern-keeper";

        /// <summary>Dvergr sites, tried in turn - names guessed, confirmed by the location registry.</summary>
        public static readonly string[] SiteLocations =
        {
            "Mistlands_DvergrTownEntrance1", "Mistlands_DvergrTownEntrance2",
            "Mistlands_Excavation1", "Mistlands_Excavation2", "Mistlands_Excavation3",
            "Mistlands_Harbour1", "Mistlands_Lighthouse1",
            "Mistlands_Viaduct1", "Mistlands_Viaduct2",
            "Mistlands_GuardTower1_new", "Mistlands_GuardTower2_new", "Mistlands_GuardTower3_new",
        };

        /// <summary>A light either way: one carried since the meadows, or one caught in the mist.</summary>
        public const string WispToken = "$item_wisp";

        public const string AskLine =
            "Living. Hm. We don't get many of those up here.\n\n" +
            "We borrow light. That's the whole of it — we catch it, carry it, let it see for us a while, and " +
            "give it back. Everyone else in this world grabbed. The forest ate it, the marsh kept it, the cold " +
            "froze it, the ones on the plains stacked it in barns. We borrow.\n\n" +
            "And you — what's that you're carrying? Let me see. Since the meadows? You've been carrying them " +
            "honestly since the meadows, and nobody taught you.\n\n" +
            "Then you'll understand what I ask. Bring one here after dark and let it go. Yours from the meadows, " +
            "or one you catch up here at the roots. Give one back, and I'll show you how we carry the rest.";

        public const string DaylightLine =
            "Not in daylight. Light only rises in the dark — you know that better than anyone. After sundown.";

        public const string NoLightLine =
            "You'll need one to let go of. One of yours from the meadows, or a wisp from the roots — they come " +
            "up at night.";

        public const string FreedLine =
            "There. Watch it go. That's how it's done — it was never ours.\n\n" +
            "Here's how we carry the rest. Five wisps and some silver, at a galdr table, the way we build our " +
            "lanterns. It'll push the mist back, and it'll give you light to see by. Borrowed, mind.";

        public const string IdleLine = "Carry it well. Give it back when you're done.";

        public const string AfterLine =
            "She's down? Then the lanterns can go back under the mist without a fight.\n\n" +
            "Borrowed light, all of it. Always was. You knew that before I did.";

        private const string GreetSpeak = "Living. Hm. Come here.";
        private const string GreetFree = "One light. After dark.";
        private const string GreetIdle = "Carry it well.";
        private const string GreetAfter = "Lanterns are lit again.";

        private const float MinDistance = 6f;
        private const float MaxDistance = 16f;
        private const float Waterline = 31f;
        private const int SpotAttempts = 24;

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick.</summary>
        public Phase Current = Phase.Speak;

        /// <summary>Set by the host every tick.</summary>
        public bool Night;

        private bool _spokenPending;
        private bool _freedPending;
        private bool _afterSaid;

        public LanternKeeper(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => KeeperName;

        protected override string BodyPrefab => "Dverger";

        protected override CreatureDressing.Look Look => CreatureDressing.LanternKeeper();

        public override void Reset()
        {
            base.Reset();
            _spokenPending = false;
            _freedPending = false;
            _afterSaid = false;
        }

        /// <summary>Call about once a second. <paramref name="freedAt"/> is where the freed light should rise.</summary>
        public void Tick(Player player, Phase phase, bool night, bool wanted, out bool spoken, out Vector3? freedAt)
        {
            Current = phase;
            Night = night;
            Stand(player, wanted);

            spoken = _spokenPending;
            freedAt = _freedPending ? (Position() ?? Spot()) : null;
            _spokenPending = false;
            _freedPending = false;
        }

        protected override string Greeting =>
            Current == Phase.Speak ? GreetSpeak
            : Current == Phase.Free ? GreetFree
            : Current == Phase.After ? GreetAfter
            : GreetIdle;

        public override string HoverText(Player player)
        {
            if (Current == Phase.Free)
            {
                var inv = player != null ? player.GetInventory() : null;
                int lights = inv == null ? 0 : inv.CountItems(SagaItems.RescuedLightName) + inv.CountItems(WispToken);
                string state = !Night ? " (after dark)" : lights > 0 ? " (you carry a light)" : " (no light to give)";
                return KeeperName + state + "\n[<color=yellow><b>$KEY_Use</b></color>] Let a light go";
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

                case Phase.Free:
                {
                    if (!Night) { Say(DaylightLine); return true; }
                    var inv = user != null ? user.GetInventory() : null;
                    if (inv == null) return false;

                    // A rescued light first: it is the one this whole saga has been about.
                    string token = inv.CountItems(SagaItems.RescuedLightName) > 0 ? SagaItems.RescuedLightName
                                 : inv.CountItems(WispToken) > 0 ? WispToken
                                 : null;
                    if (token == null) { Say(NoLightLine); return true; }

                    inv.RemoveItem(token, 1);
                    Say(FreedLine);
                    _freedPending = true;
                    return true;
                }

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
        /// The nearest dvergr site among the guessed names; failing all of them, dry ground near the
        /// player - he can never be unfindable.
        /// </summary>
        protected override Vector3? ChooseSpot(Player player)
        {
            var zone = ZoneSystem.instance;
            if (zone == null || player == null) return null;

            Vector3? site = null;
            string which = null;
            float best = float.MaxValue;
            foreach (var name in SiteLocations)
            {
                try
                {
                    if (!zone.FindClosestLocation(name, player.transform.position, out var loc)) continue;
                    float d = Vector3.Distance(player.transform.position, loc.m_position);
                    if (d < best) { best = d; site = loc.m_position; which = name; }
                }
                catch { }
            }

            if (site == null)
            {
                var land = BiomeCompass.LandNear(player.transform.position, 30f, 60f, _rng);
                if (land == null) return null;
                Debug.LogWarning("[ICanShowYouTheWorld] The lantern-keeper found no dvergr site by name; " +
                                 $"standing on dry ground near the player at {land.Value:0.0}. See the location registry.");
                return land;
            }

            Vector3 chosen = site.Value + new Vector3(MaxDistance, 0f, 0f);
            string pass = "no footing by the site - placed anyway";
            var gen = WorldGenerator.instance;
            if (gen != null)
            {
                for (int attempt = 0; attempt < SpotAttempts; attempt++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                    float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                    Vector3 at = site.Value + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    float h;
                    try { h = gen.GetHeight(at.x, at.z); }
                    catch { break; }
                    if (h <= Waterline) continue;
                    chosen = new Vector3(at.x, h, at.z);
                    pass = "ground by the site";
                    break;
                }
            }

            Debug.Log($"[ICanShowYouTheWorld] The lantern-keeper's site: {which} at {site.Value:0.0}, standing at {chosen:0.0} on {pass}.");
            return chosen;
        }
    }
}
