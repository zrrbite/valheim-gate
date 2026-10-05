using System;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The charred one: Act VII's speaker, one of the Ashlands' burned dead who kept their wits,
    /// waiting where the player comes ashore. The sixth speaker on <see cref="SagaSpeaker"/>.
    /// </summary>
    /// <remarks>
    /// THE END OF THE ARC. The meadows' light was stolen, the forest spent it, the marsh kept it, the
    /// cold froze it, the plains stacked it, the dvergr borrowed it. Here it goes to end - and he is
    /// the one who says that burning is the only end that LETS GO. His pyre takes the bow the saga
    /// began with, flametal and three lights, and gives back Last Light: the light spent on purpose.
    ///
    /// Every price has a way through, because an act that cannot be finished is a stalled saga:
    /// lights are rescued lights OR wisps; the bow is Thor's if the player still has it, else any
    /// bow; flametal is resolved from the game's own item names at run start, not guessed.
    /// </remarks>
    internal sealed class CharredOne : SagaSpeaker
    {
        public enum Phase { Speak, Pyre, Idle, After }

        public const string CharredName = "The charred one";

        public const int LightsWanted = 3;
        public const int FlametalWanted = 10;

        public const string AskLine =
            "You came over the water. They all do, at the end.\n\n" +
            "This is where light goes to end. Everything here has burned once already — the trees, the stone, " +
            "me. The ones in the marsh could not let go. The ones on the mountain are still waiting. Burning is " +
            "the only end that lets go. Nothing is kept. Nothing is owed.\n\n" +
            "You carry a bow with the storm in it, and lights you never spent. Build me a fire, here. Put the " +
            "bow in it, with flametal from the ground and three of your lights. Let it burn. What comes out " +
            "will be the last light you ever need.";

        public const string NoFireLine = "No fire. It has to burn here, where I can see it.";

        public const string ShortLine =
            "The bow, ten flametal, three lights. A wisp will do for a light, and any bow will burn if the " +
            "storm's has gone. All of it, into the fire, at once.";

        public const string BurnedLine =
            "There. Watch it go.\n\n" +
            "It is not gone. Nothing that burns properly is gone — it is let go of, which is different. Take " +
            "it. The light is in it now, all of it, spent. That is what it was for.";

        public const string IdleLine = "Let it burn. Go on.";

        public const string AfterLine =
            "So that is where it all went. Here, to the end of it, the way light should.\n\n" +
            "You can go home now. Whatever you carry back, carry it the way the dvergr do.";

        private const string GreetSpeak = "Over the water. They all come, at the end.";
        private const string GreetPyre = "A fire. Then let it burn.";
        private const string GreetIdle = "Let it burn.";
        private const string GreetAfter = "Go home.";

        private readonly System.Random _rng;

        /// <summary>Set by the host every tick.</summary>
        public Phase Current = Phase.Speak;
        public bool FireLit;

        /// <summary>The game's flametal, by shared name - resolved by the host at run start. Null until then.</summary>
        public string FlametalToken;

        private bool _spokenPending;
        private bool _burnedPending;
        private bool _afterSaid;

        public CharredOne(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public override string Name => CharredName;

        protected override string BodyPrefab => "Charred_Melee";
        protected override string[] BodyFallbacks => new[] { "Charred_Archer", "Charred_Twitcher", "Charred_Mage" };

        protected override CreatureDressing.Look Look => CreatureDressing.Charred();

        public override void Reset()
        {
            base.Reset();
            _spokenPending = false;
            _burnedPending = false;
            _afterSaid = false;
        }

        public void Tick(Player player, Phase phase, bool fireLit, bool wanted, out bool spoken, out bool burned)
        {
            Current = phase;
            FireLit = fireLit;
            Stand(player, wanted);

            spoken = _spokenPending;
            burned = _burnedPending;
            _spokenPending = false;
            _burnedPending = false;
        }

        protected override string Greeting =>
            Current == Phase.Speak ? GreetSpeak
            : Current == Phase.Pyre ? GreetPyre
            : Current == Phase.After ? GreetAfter
            : GreetIdle;

        /// <summary>The bow to burn: Thor's if carried, else any bow that is not already Last Light.</summary>
        public static ItemDrop.ItemData BowToBurn(Inventory inv)
        {
            if (inv == null) return null;
            var items = inv.GetAllItems();
            return items.FirstOrDefault(i => i?.m_shared != null && i.m_shared.m_name == SagaItems.ThorsBowName)
                ?? items.FirstOrDefault(i => i?.m_shared != null &&
                                             i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow &&
                                             i.m_shared.m_name != SagaItems.LastLightName);
        }

        public static int LightsCarried(Inventory inv) =>
            inv == null ? 0 : inv.CountItems(SagaItems.RescuedLightName) + inv.CountItems(LanternKeeper.WispToken);

        public override string HoverText(Player player)
        {
            if (Current == Phase.Pyre)
            {
                var inv = player != null ? player.GetInventory() : null;
                string have = inv == null ? string.Empty :
                    $" (bow {(BowToBurn(inv) != null ? 1 : 0)}/1, flametal " +
                    $"{Mathf.Min(FlametalToken == null ? 0 : inv.CountItems(FlametalToken), FlametalWanted)}/{FlametalWanted}, " +
                    $"lights {Mathf.Min(LightsCarried(inv), LightsWanted)}/{LightsWanted}{(FireLit ? "" : ", no fire")})";
                return CharredName + have + "\n[<color=yellow><b>$KEY_Use</b></color>] Let it burn";
            }
            return CharredName + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public override bool OnInteract(Humanoid user, bool alt)
        {
            switch (Current)
            {
                case Phase.Speak:
                    Say(AskLine);
                    _spokenPending = true;
                    return true;

                case Phase.Pyre:
                {
                    if (!FireLit) { Say(NoFireLine); return true; }
                    var player = user as Player;
                    var inv = player != null ? player.GetInventory() : null;
                    var bow = BowToBurn(inv);
                    if (inv == null || bow == null || FlametalToken == null ||
                        inv.CountItems(FlametalToken) < FlametalWanted || LightsCarried(inv) < LightsWanted)
                    {
                        Say(ShortLine);
                        return true;
                    }

                    if (player.IsItemEquiped(bow)) player.UnequipItem(bow, false);
                    inv.RemoveItem(bow);
                    inv.RemoveItem(FlametalToken, FlametalWanted);

                    // Rescued lights first, then wisps: the saga's own lights are the point.
                    int owed = LightsWanted;
                    int rescued = Mathf.Min(owed, inv.CountItems(SagaItems.RescuedLightName));
                    if (rescued > 0) { inv.RemoveItem(SagaItems.RescuedLightName, rescued); owed -= rescued; }
                    if (owed > 0) inv.RemoveItem(LanternKeeper.WispToken, owed);

                    Say(BurnedLine);
                    _burnedPending = true;
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

        /// <summary>Where the player came ashore: dry Ashlands ground near them the first time he is wanted.</summary>
        protected override Vector3? ChooseSpot(Player player)
        {
            if (player == null) return null;
            Vector3 from = player.transform.position;
            var gen = WorldGenerator.instance;

            for (int i = 0; i < 4; i++)
            {
                var land = BiomeCompass.LandNear(from, 12f, 30f, _rng);
                if (land == null) continue;
                if (gen == null || gen.GetBiome(land.Value) == Heightmap.Biome.AshLands)
                {
                    Debug.Log($"[ICanShowYouTheWorld] The charred one waits at the landing: {land.Value:0.0}.");
                    return land;
                }
            }

            var any = BiomeCompass.LandNear(from, 12f, 30f, _rng);
            Debug.Log($"[ICanShowYouTheWorld] The charred one waits near the player (no ash ground found close): {(any ?? from):0.0}.");
            return any ?? from;
        }
    }
}
