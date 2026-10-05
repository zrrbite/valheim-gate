using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The harvester: Act V's speaker, one of Yagluth's own people, at a stone ring on the plains. The
    /// fourth speaker on <see cref="SagaSpeaker"/>.
    /// </summary>
    /// <remarks>
    /// THE ACT IN ONE PERSON. The plains' answer to the shortage was to industrialise it: take the
    /// light from the herd, the forest and the fields at scale, and store it. He did that, and never
    /// once sat down. He asks to see someone EAT from the field - a full table with something the
    /// plains grew - and gives the Stormsworn mantle, the set's last piece.
    ///
    /// Act V is where the saga ends by default, so his last line after Yagluth is written to stand
    /// as the ending: the field can be eaten from now.
    /// </remarks>
    internal sealed class Harvester : SagaSpeaker
    {
        public enum Phase { Speak, Ask, Idle, After }

        public const string HarvesterName = "The harvester";

        /// <summary>Stone rings on the plains, tried in turn - names guessed, confirmed by the location registry.</summary>
        public static readonly string[] RingLocations =
        {
            "StoneHenge1", "StoneHenge2", "StoneHenge3", "StoneHenge4", "StoneHenge5", "StoneHenge6",
        };

        public const string AskLine =
            "You have been doing what we did. I can tell by how you walk — like somebody counting.\n\n" +
            "We took it from the herd, and from the forest, and from the fields. Then we did it properly: we " +
            "cut the whole country into straight lines and took it all, and stored it, and took more. We never " +
            "once sat down. There was always another field.\n\n" +
            "The little ones still keep our quota. Nobody told them it ended. I am telling you.\n\n" +
            "I want to see one thing before I go back into the stones. Eat. Fill yourself with what the field " +
            "grew — bread from the barley, a pie from the lox — and come and stand where I can see it.";

        public const string NotYetLine =
            "No. Full, and something from the field in it. Bread, or the lox pie, or the wrapped fish. " +
            "I have watched people carry food my whole life. I want to watch somebody eat it.";

        public const string FedLine =
            "There. That is all it was ever for.\n\n" +
            "We wove a mantle against the fire, once, for the ones who worked the fields at midday. Lox pelt, " +
            "needles and silver, at a table for the fine work. Take it. Wear the whole of the storm's set into " +
            "what is left — it was made for that.";

        public const string IdleLine = "Go and eat. That is the whole of the advice.";

        /// <summary>After Yagluth: written to stand as the saga's ending when he is the last god.</summary>
        public const string AfterLine =
            "His hand closed on nothing. Ours always did, in the end.\n\n" +
            "The field is still there. You can eat from it now. Nobody is counting.";

        private const string GreetSpeak = "Counting, are you? Come here.";
        private const string GreetAsk = "Eat. Then come and stand where I can see it.";
        private const string GreetIdle = "Nobody is counting.";
        private const string GreetAfter = "You can eat from it now.";

        private const float MinDistance = 6f;
        private const float MaxDistance = 14f;
        private const float Waterline = 31f;
        private const int SpotAttempts = 24;

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick.</summary>
        public Phase Current = Phase.Speak;

        /// <summary>Set by the host every tick: the player has a full table with something from the field.</summary>
        public bool Fed;

        private bool _spokenPending;
        private bool _fedPending;
        private bool _afterSaid;

        public Harvester(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => HarvesterName;

        protected override CreatureDressing.Look Look => CreatureDressing.Harvester();

        public override void Reset()
        {
            base.Reset();
            _spokenPending = false;
            _fedPending = false;
            _afterSaid = false;
        }

        /// <summary>Call about once a second.</summary>
        public void Tick(Player player, Phase phase, bool fed, bool wanted, out bool spoken, out bool fedNow)
        {
            Current = phase;
            Fed = fed;
            Stand(player, wanted);

            spoken = _spokenPending;
            fedNow = _fedPending;
            _spokenPending = false;
            _fedPending = false;
        }

        protected override string Greeting =>
            Current == Phase.Speak ? GreetSpeak
            : Current == Phase.Ask ? GreetAsk
            : Current == Phase.After ? GreetAfter
            : GreetIdle;

        public override string HoverText(Player player)
        {
            if (Current == Phase.Ask)
                return HarvesterName + (Fed ? " (a full table)" : " (eat from the field first)") +
                       "\n[<color=yellow><b>$KEY_Use</b></color>] Let him see";
            return HarvesterName + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public override bool OnInteract(Humanoid user, bool alt)
        {
            switch (Current)
            {
                case Phase.Speak:
                    Say(AskLine);
                    _spokenPending = true;
                    return true;
                case Phase.Ask:
                    if (Fed)
                    {
                        Say(FedLine);
                        _fedPending = true;
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

        /// <summary>The nearest plains stone ring, then level ground beside it.</summary>
        protected override Vector3? ChooseSpot(Player player)
        {
            var zone = ZoneSystem.instance;
            if (zone == null || player == null) return null;

            Vector3? ring = null;
            string which = null;
            float best = float.MaxValue;
            foreach (var name in RingLocations)
            {
                try
                {
                    if (!zone.FindClosestLocation(name, player.transform.position, out var loc)) continue;
                    float d = Vector3.Distance(player.transform.position, loc.m_position);
                    if (d < best) { best = d; ring = loc.m_position; which = name; }
                }
                catch { }
            }
            if (ring == null)
            {
                // No stone ring by any guessed name: stand on Plains ground near the player rather
                // than never at all - his two steps and the mantle would stall (review, 2026-10-05).
                var gen0 = WorldGenerator.instance;
                for (int i = 0; i < 6; i++)
                {
                    var land = BiomeCompass.LandNear(player.transform.position, 20f, 45f, _rng);
                    if (land != null && (gen0 == null || gen0.GetBiome(land.Value) == Heightmap.Biome.Plains))
                    {
                        Debug.LogWarning($"[ICanShowYouTheWorld] The harvester found no stone ring by name; standing on the plains at {land.Value:0.0}.");
                        return land;
                    }
                }
                return null;   // not on the plains yet - asked again next second
            }

            Vector3 chosen = ring.Value + new Vector3(MaxDistance, 0f, 0f);
            string pass = "no level ground by the stones - placed anyway";
            var gen = WorldGenerator.instance;
            if (gen != null)
            {
                for (int attempt = 0; attempt < SpotAttempts; attempt++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                    float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                    Vector3 at = ring.Value + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    float h;
                    try { h = gen.GetHeight(at.x, at.z); }
                    catch { break; }
                    if (h <= Waterline) continue;
                    chosen = new Vector3(at.x, h, at.z);
                    pass = "the grass by the stones";
                    break;
                }
            }

            Debug.Log($"[ICanShowYouTheWorld] The harvester's ring: {which} at {ring.Value:0.0}, standing at {chosen:0.0} on {pass}.");
            return chosen;
        }
    }
}
