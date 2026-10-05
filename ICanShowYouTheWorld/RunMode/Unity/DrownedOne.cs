using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The drowned one: Act III's speaker, a draugr who kept his wits, at the door of a sunken crypt.
    /// The second speaker on <see cref="SagaSpeaker"/>, and the first not in the Ghost's body.
    /// </summary>
    /// <remarks>
    /// THE ACT'S THEME IN ONE PERSON. The swamp's answer to the shortage is never let go, and he is
    /// a man who has held a door since before the iron in it rusted, because holding is all the
    /// marsh lets anyone do. He gives what he wore (the Stormsworn cuirass), and then asks for the
    /// one thing the marsh never does: to be put down. His death - matched by ZDO id, like the
    /// Herald's - lets go of the light he kept, and the host releases it where he falls.
    ///
    /// The swamp does not take him back: the host checks him BEFORE FenWatch, which would otherwise
    /// raise a killed draugr as bone.
    ///
    /// A DRAUGR, not the Ghost. In this act the dead are bodies that would not lie down, and a ghost
    /// would say the opposite of what the act says. Tame while he talks; untamed for the end.
    /// </remarks>
    internal sealed class DrownedOne : SagaSpeaker
    {
        public enum Phase { Speak, Wait, LetGo }

        public const string DrownedName = "The drowned one";

        /// <summary>The sunken crypt's location - the one sw-scrap's iron comes out of. Logged at run start.</summary>
        public const string CryptLocation = "SunkenCrypt4";

        /// <summary>What is left of him when it is time: enough to raise the blade once.</summary>
        private const float LetGoHealthFraction = 0.35f;

        public const string AskLine =
            "You came out of there with iron. I watched you go in.\n\n" +
            "I have held this door since before the iron in it rusted. Not because there is anything left " +
            "behind it worth holding. Because holding is all the marsh lets anyone do. Nothing here is let " +
            "go of. Not the iron, not the water, not us.\n\n" +
            "What I wore is no use to me now. The storm made it, a long time ago, for a man who meant to come " +
            "back out. Take it to a forge — iron and guck and leather — and wear it better than I did.";

        public const string WaitLine =
            "Make the coat first. Then come back. There is one more thing, and it is not a thing I can do myself.";

        public const string LetGoAsk =
            "You wore it out of here. Good.\n\n" +
            "Now the other thing. Put me down. I have tried to let go of this door for longer than you have " +
            "been alive, and my hand will not open. Yours will.\n\n" +
            "I will lift the blade when you come. I always do. Do not let that stop you.";

        private const string GreetSpeak = "Iron. You came out with iron.";
        private const string GreetWait = "The coat first.";
        private const string GreetLetGo = "Now. Before my hand remembers.";

        private const float MinDistance = 4f;
        private const float MaxDistance = 12f;
        private const int SpotAttempts = 24;

        /// <summary>
        /// Lower than the other speakers' 31: sunken crypts stand in water, and he is drowned already.
        /// Ankle-deep is fine for him; under the surface is not.
        /// </summary>
        private const float Waterline = 29.6f;

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick.</summary>
        public Phase Current = Phase.Speak;

        private bool _spokenPending;
        private bool _askedToLetGo;

        public DrownedOne(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => DrownedName;

        protected override string BodyPrefab => "Draugr";

        protected override CreatureDressing.Look Look => CreatureDressing.Drowned();

        public override void Reset()
        {
            base.Reset();
            _spokenPending = false;
            _askedToLetGo = false;
        }

        /// <summary>Call about once a second.</summary>
        public void Tick(Player player, Phase phase, bool wanted, out bool spoken)
        {
            var was = Current;
            Current = phase;
            Stand(player, wanted);

            // Already standing when the coat is made: he becomes what he asked to be, in place.
            if (Standing && phase == Phase.LetGo && was != Phase.LetGo) TurnHostile(BodyCharacter);

            spoken = _spokenPending;
            _spokenPending = false;
        }

        protected override void OnSpawned(Character body)
        {
            if (Current == Phase.LetGo) TurnHostile(body);
        }

        private void TurnHostile(Character body)
        {
            if (body == null) return;
            try
            {
                body.SetTamed(false);
                Unshield(body);   // killable again - by the player, which is the point
                body.SetHealth(body.GetMaxHealth() * LetGoHealthFraction);
                Debug.Log("[ICanShowYouTheWorld] The drowned one lifts his blade, out of habit.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The drowned one could not be let go: " + ex.Message);
            }
        }

        protected override string Greeting =>
            Current == Phase.Speak ? GreetSpeak : Current == Phase.Wait ? GreetWait : GreetLetGo;

        public override string HoverText(Player player)
        {
            if (Current == Phase.LetGo)
                return DrownedName + "\n<i>He is waiting for the blow.</i>\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
            return DrownedName + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public override bool OnInteract(Humanoid user, bool alt)
        {
            switch (Current)
            {
                case Phase.Speak:
                    Say(AskLine);
                    _spokenPending = true;
                    return true;
                case Phase.Wait:
                    Say(WaitLine);
                    return true;
                default:
                    Say(LetGoAsk);
                    _askedToLetGo = true;
                    return true;
            }
        }

        /// <summary>True once he has said, in the rune panel, what the last step is.</summary>
        public bool AskedToLetGo => _askedToLetGo;

        /// <summary>The sunken crypt nearest the player, then footing near its door - shallows allowed.</summary>
        protected override Vector3? ChooseSpot(Player player)
        {
            var zone = ZoneSystem.instance;
            if (zone == null || player == null) return null;

            ZoneSystem.LocationInstance crypt;
            try
            {
                if (!zone.FindClosestLocation(CryptLocation, player.transform.position, out crypt)) return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The drowned one found no crypt: " + ex.Message);
                return null;
            }

            Vector3 door = crypt.m_position;
            Vector3 chosen = door + new Vector3(MaxDistance, 0f, 0f);
            string pass = "no footing near the crypt - placed anyway";

            var gen = WorldGenerator.instance;
            if (gen != null)
            {
                for (int attempt = 0; attempt < SpotAttempts; attempt++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                    float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                    Vector3 at = door + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                    float h;
                    try { h = gen.GetHeight(at.x, at.z); }
                    catch { break; }

                    if (h <= Waterline) continue;
                    chosen = new Vector3(at.x, h, at.z);
                    pass = h < 31f ? "the shallows" : "dry ground";
                    break;
                }
            }

            Debug.Log($"[ICanShowYouTheWorld] The drowned one's door: crypt at {door:0.0}, standing at {chosen:0.0} in {pass}.");
            return chosen;
        }
    }
}
