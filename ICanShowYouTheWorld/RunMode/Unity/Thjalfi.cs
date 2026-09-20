using System;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The interact half of Thjalfi. Lives on a CHILD object with its own trigger collider, for the
    /// same reason <see cref="ShadeTalk"/> does: the game resolves hover text and interacts with
    /// GetComponentInParent from whatever collider the crosshair hit, and a Character is itself
    /// Hoverable on the root. A component on the root loses the prompt to the creature's own name.
    /// </summary>
    internal sealed class ThjalfiTalk : MonoBehaviour, Interactable, Hoverable
    {
        /// <summary>Set by the owner every tick; drives the prompt and the reply.</summary>
        public Thjalfi.Phase Phase;

        /// <summary>Raised by an interact, read and cleared by the owner's tick.</summary>
        public bool SpokenPending;
        public bool PaidPending;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            try
            {
                switch (Phase)
                {
                    case Thjalfi.Phase.Find:
                        Thjalfi.Say(Thjalfi.AskLine);
                        SpokenPending = true;
                        return true;

                    case Thjalfi.Phase.Pay:
                    {
                        var inv = user != null ? user.GetInventory() : null;
                        if (inv == null) return false;

                        if (Thjalfi.Price.All(p => inv.CountItems(p.token) >= p.amount))
                        {
                            foreach (var p in Thjalfi.Price) inv.RemoveItem(p.token, p.amount);
                            Thjalfi.Say(Thjalfi.PaidLine);
                            PaidPending = true;
                        }
                        else
                        {
                            Thjalfi.Say(Thjalfi.NotYetLine);
                        }
                        return true;
                    }

                    default:
                        Thjalfi.Say(Thjalfi.AfterLine);
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Thjalfi could not answer: " + ex.Message);
                return false;
            }
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // Localised here, not by the HUD: the game's own Hoverables each run their text through
        // Localization.Localize, and that is what turns "$KEY_Use" into the bound key. Returned
        // raw, the prompt reads "$KEY_Use" on screen.
        public string GetHoverText() => Localize(RawHoverText());

        private static string Localize(string text)
        {
            try { return Localization.instance != null ? Localization.instance.Localize(text) : text; }
            catch { return text; }
        }

        private string RawHoverText()
        {
            if (Phase == Thjalfi.Phase.Pay)
            {
                string have = string.Empty;
                try
                {
                    var inv = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
                    if (inv != null)
                        have = "  (" + string.Join(", ", Thjalfi.Price
                            .Select(p => $"{Mathf.Min(inv.CountItems(p.token), p.amount)}/{p.amount} {p.label}")
                            .ToArray()) + ")";
                }
                catch { }

                return Thjalfi.Name + have + "\n[<color=yellow><b>$KEY_Use</b></color>] Pay him";
            }

            return Thjalfi.Name + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
        }

        public string GetHoverName() => Thjalfi.Name;

        public float GetHoverOffset() => 0f;
    }

    /// <summary>
    /// Thjalfi: the one who waits, and the only living-shaped thing in the Meadows with a NAME.
    /// </summary>
    /// <remarks>
    /// He exists because the Storm-Anvil should be a PLACE (owner: "a quest to talk to an NPC, maybe
    /// another ghost? A norseman, viking, lightning person, odin relative - That, when given what he
    /// asks, spawns the obliterator and we have to go THERE to craft our stuff"). A forge that works
    /// by weather has no business in a shed; it wants to be out under the sky, at the end of a walk,
    /// and somewhere the player chose to return to rather than somewhere they put down.
    ///
    /// WHY THJALFI. Myth hands the saga this character almost finished. Thjalfi broke the bone of
    /// Thor's goat to get at the marrow, and Thor took him as payment for it - so he is a boy whose
    /// whole story is that he BROKE something and was collected for it. This is the world where the
    /// put-away are put, the Storm-Anvil is a machine that breaks things with lightning, and the
    /// saga's frame is Odin auditing a ledger. A debtor who broke something, tending an engine that
    /// breaks things, in a world that is itself a debt: nothing had to be invented.
    ///
    /// He is also not a god, which is what lets him talk. The bible's rule is that gods only speak
    /// when defeated; a servant taken in payment is free to say whatever he likes.
    ///
    /// WHY HE IS NAMED, when the shade is not. The shade is anonymous because it is one of the taken
    /// and the point of it is that nobody came to collect. Thjalfi has a name because somebody DID
    /// come, and wrote it down. That contrast is the two characters' whole relationship, and neither
    /// of them ever mentions the other.
    ///
    /// Standing day and night, unlike the shade, for the same reason: the shade is a thing you catch
    /// after dark, and he is a fixture you walk to.
    /// </remarks>
    internal sealed class Thjalfi
    {
        public enum Phase { Find, Pay, Done }

        public const string Name = "Thjalfi";

        /// <summary>The Ghost prefab, as the shade uses - he is one of the put-away too.</summary>
        private const string Prefab = "Ghost";

        /// <summary>
        /// What he asks for: the altar's body, and its eye.
        /// </summary>
        /// <remarks>
        /// Stone because an altar is stone, and one rescued light because the thing he raises is a
        /// machine that BREAKS light and it should cost one to wake. That was the price of building it
        /// by hand before he existed, and it moves to him unchanged - the beat is the player giving up
        /// a light, not the mechanism that takes it.
        ///
        /// Both are Meadows-available and neither can stall a chain: stone is everywhere, and by the
        /// time this step is live the shade has already handed over a light and the races are paying.
        /// </remarks>
        public static readonly (string token, int amount, string label)[] Price =
        {
            ("Stone", 20, "stone"),
            (SagaItems.RescuedLightPrefab, 1, "light"),
        };

        public const string AskLine =
            "I broke a bone once. That is the whole of it — a goat's hind leg, snapped for the " +
            "marrow, because I was hungry and it was lying there and I did not think anyone counted " +
            "bones.\n\n" +
            "He counted. He took me instead of the goat, and payment does not get to stop being " +
            "payment just because the debt has gone old.\n\n" +
            "So I know exactly what you are. You have been taking light that something else was " +
            "counting. The difference between us is not the taking. It is that you still have " +
            "somewhere to put it.";

        public const string NotYetLine =
            "Twenty stone. And one of their lights, still burning.\n\n" +
            "I am not asking for much and I will not ask twice. Bring it in your hands — what is " +
            "in a chest at your house is not in your hands.";

        public const string PaidLine =
            "Stone for the body. A light for the eye.\n\n" +
            "Stand back.\n\n" +
            "There. It is not a forge — there is no fire in it, and nothing in this world can make " +
            "one worth the name. It only breaks. But put in a shape it knows and what it breaks is " +
            "the shape's own edges, and what stands up out of it is the thing you meant.\n\n" +
            "Put in anything else and you get ash. I have been getting it wrong for a very long time.";

        public const string AfterLine =
            "It is yours. He does not come when it is struck — he never did — but the weather " +
            "still answers to him, and it will do this much on his account.\n\n" +
            "Be exact with it. That is all the advice I have, and it cost me everything.";

        // One line each, in a bubble over his head as you come near: the way the trader greets. The
        // rune panel is for what he has to SAY; this is for him being there.
        private const string GreetFind = "You walk like someone who has taken something. Come here.";
        private const string GreetPay = "Twenty stone and one of their lights. Then I show you what I am for.";
        private const string GreetDone = "It is yours. Be exact with it.";

        /// <summary>How far out he waits. Far enough to be a walk, near enough to be found.</summary>
        private const float MinDistance = 55f;
        private const float MaxDistance = 85f;

        /// <summary>He is only CREATED once the player is near his spot, so he is never culled at birth.</summary>
        private const float SpawnRange = 70f;

        /// <summary>How close the player must come for him to speak first.</summary>
        private const float GreetRange = 10f;

        private readonly System.Random _rng;

        private Vector3? _spot;
        private ThjalfiTalk _talk;
        private GameObject _body;
        private Phase? _greetedFor;

        public Thjalfi(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        /// <summary>Forgets the spot and dismisses him. Run start and end.</summary>
        public void Reset()
        {
            Dismiss();
            _spot = null;
        }

        public bool Standing => _body != null;

        /// <summary>Where he is, for raising the altar at his feet. Null when he is not standing.</summary>
        public Vector3? Position() => _body != null ? _body.transform.position : (Vector3?)null;

        /// <summary>
        /// Call about once a second. Keeps him standing while he is wanted, and reports what the
        /// player did at him.
        /// </summary>
        public void Tick(Player player, Phase phase, bool wanted, out bool spoken, out bool paid)
        {
            spoken = false;
            paid = false;

            if (!wanted || player == null)
            {
                if (Standing) Dismiss();
                return;
            }

            if (!Standing) TrySpawn(player);
            if (_talk == null) return;

            _talk.Phase = phase;
            Greet(player, phase);

            if (_talk.SpokenPending)
            {
                _talk.SpokenPending = false;
                spoken = true;
            }
            if (_talk.PaidPending)
            {
                _talk.PaidPending = false;
                paid = true;
            }
        }

        /// <summary>A coarse bearing to him, or null when there is nothing to say.</summary>
        public string Bearing(Player player)
        {
            if (player == null) return null;

            Vector3? position = Position() ?? _spot;
            if (position == null) return null;

            Vector3 delta = position.Value - player.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 8f) return "He is here. Speak to him.";

            return $"Someone waits {BiomeCompass.Compass(delta)}, {Mathf.Round(distance / 10f) * 10f:0}m";
        }

        /// <summary>The game's rune panel, the one lore stones use. Reads as someone speaking.</summary>
        public static void Say(string text)
        {
            try
            {
                var viewer = TextViewer.instance;
                if (viewer != null) viewer.ShowText(TextViewer.Style.Rune, Name, text, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Thjalfi's line could not be shown: " + ex.Message);
            }
        }

        private void Greet(Player player, Phase phase)
        {
            if (_body == null || _greetedFor == phase) return;

            if (Vector3.Distance(player.transform.position, _body.transform.position) > GreetRange) return;

            // Not while the rune panel is up: the phase changes the instant he is paid, and a bubble
            // raised under the panel is gone before the panel is.
            try { if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return; } catch { }

            _greetedFor = phase;
            string line = phase == Phase.Find ? GreetFind : phase == Phase.Pay ? GreetPay : GreetDone;

            try
            {
                var chat = Chat.instance;
                if (chat != null)
                    chat.SetNpcText(_body, Vector3.up * 2.4f, 30f, 12f, Name, line, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Thjalfi could not greet: " + ex.Message);
            }
        }

        private void TrySpawn(Player player)
        {
            Vector3 spot = EnsureSpot(player);
            if (Vector3.Distance(player.transform.position, spot) > SpawnRange) return;

            var scene = ZNetScene.instance;
            var prefab = scene == null ? null : scene.GetPrefab(Prefab);
            if (prefab == null)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Cannot spawn Thjalfi: no '{Prefab}' prefab.");
                return;
            }

            Vector3 pos = spot;
            try { pos.y = ZoneSystem.instance.GetSolidHeight(pos) + 0.3f; }
            catch { }

            var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (inst == null) return;

            var ch = inst.GetComponent<Character>();
            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (ch == null || zdo == null)
            {
                UnityEngine.Object.Destroy(inst);
                return;
            }

            zdo.Persistent = false;

            // Tamed so he never fights and nothing fights him; parked so he stays put. A quest-giver
            // that wanders off is a quest-giver lost.
            try { ch.SetTamed(true); } catch { }
            ch.m_name = Name;
            try
            {
                var ai = inst.GetComponent<BaseAI>();
                if (ai != null) ai.SetPatrolPoint();
            }
            catch { }

            // Dressed apart from the shade on purpose: warmer and larger, because he is not one of
            // the herd's lights - he is somebody's servant, and he has been here longer than anything
            // else you have met.
            CreatureDressing.ApplyWhenSettled(inst, CreatureDressing.Thjalfi());

            var talkObject = new GameObject("saga_thjalfi_talk");
            talkObject.transform.SetParent(inst.transform, false);
            talkObject.transform.localPosition = Vector3.up * 1.2f;
            talkObject.layer = inst.layer;
            var trigger = talkObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            _talk = talkObject.AddComponent<ThjalfiTalk>();

            _body = inst;
            _greetedFor = null;

            Debug.Log($"[ICanShowYouTheWorld] Thjalfi stands at {pos:0.0}.");
        }

        /// <summary>
        /// Picks where he waits: a ring around home, on DRY LAND.
        /// </summary>
        /// <remarks>
        /// The dry-land check is not a nicety at this range. The shade stands seven to eleven metres
        /// from the bed, where the worst a blind angle can do is put it behind a tree; Thjalfi stands
        /// fifty-five to eighty-five, which on any coastal homestead is far enough to be in the water.
        /// A quest-giver in the sea is a chain that cannot advance and a bearing that points at
        /// nothing, so the angle is re-rolled until the ground is above the waterline.
        ///
        /// Sea level is 30 in Valheim's world space. If every attempt fails - a homestead on an
        /// islet - the last one is used anyway rather than leaving him unplaced: a visible problem
        /// beats an invisible one, and the bearing will at least say where the problem is.
        /// </remarks>
        private Vector3 EnsureSpot(Player player)
        {
            if (_spot != null) return _spot.Value;

            Vector3 origin = Home(player) ?? player.transform.position;
            Vector3 candidate = origin;

            for (int attempt = 0; attempt < DryLandAttempts; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                if (IsDryLand(candidate)) break;
            }

            _spot = candidate;
            return _spot.Value;
        }

        private const int DryLandAttempts = 12;

        /// <summary>Sea level in Valheim's world space, with a margin so he is not ankle-deep.</summary>
        private const float Waterline = 31f;

        private static bool IsDryLand(Vector3 at)
        {
            try
            {
                var zones = ZoneSystem.instance;
                if (zones == null) return true;   // Cannot tell; do not spin.

                return zones.GetSolidHeight(at) > Waterline;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>The claimed bed, when there is one - so his walk is measured from home.</summary>
        private static Vector3? Home(Player player)
        {
            try
            {
                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (profile != null && profile.HaveCustomSpawnPoint()) return profile.GetCustomSpawnPoint();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Sends him away. He is NOT destroyed once the altar stands - see RunService.PollThjalfi:
        /// the whole point of him is that the altar has somebody at it.
        /// </summary>
        public void Dismiss()
        {
            try
            {
                if (_body != null) UnityEngine.Object.Destroy(_body);
            }
            catch { }

            _body = null;
            _talk = null;
            _greetedFor = null;
        }
    }
}
