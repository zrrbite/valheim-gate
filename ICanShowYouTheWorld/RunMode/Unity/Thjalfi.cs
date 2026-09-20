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
        /// <remarks>
        /// The tokens are SHARED NAMES, not prefab names, and that distinction cost a play-test:
        /// <c>Inventory.CountItems</c> compares <c>m_shared.m_name</c>, which for a vanilla item is a
        /// localisation token like <c>$item_stone</c>. Asking for "Stone" and "Saga_RescuedLight"
        /// matched nothing, so he refused a pack holding fifty stone and a light ("I have 50 stone and
        /// 1 rescued light in my inv, but Tjalfi doesnt accept it").
        ///
        /// The saga's own items are the exception and go in by DISPLAY name: a clone's shared name is
        /// deliberately plain text rather than a "$" token, so that the localiser leaves it alone.
        /// Hence one of each here, which is exactly the sort of inconsistency a validator should be
        /// watching - and now does. See RunService.ValidateQuestPrices.
        /// </remarks>
        public static readonly (string token, int amount, string label)[] Price =
        {
            ("$item_stone", 20, "stone"),
            (SagaItems.RescuedLightName, 1, "light"),
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

        /// <summary>
        /// How far out he waits. Far enough to be a walk, near enough to be found.
        /// </summary>
        /// <remarks>
        /// These bound the SHORELINE search below, and the fallback ring if no coast is in range.
        /// MaxSearch is much further than MaxDistance on purpose: a homestead well inland may have no
        /// water for two hundred metres, and a long walk to the sea is a better answer than giving up
        /// and putting him in a field.
        /// </remarks>
        private const float MinDistance = 45f;
        private const float MaxDistance = 90f;
        private const float MaxSearch = 320f;

        /// <summary>The walk length the search aims for when several shores qualify.</summary>
        private const float PreferredDistance = 95f;

        /// <summary>He is only CREATED once the player is near his spot, so he is never culled at birth.</summary>
        private const float SpawnRange = 70f;

        /// <summary>How close the player must come for him to speak first.</summary>
        private const float GreetRange = 10f;

        private readonly System.Random _rng;

        private Vector3? _spot;

        /// <summary>Which way the water lies from his spot, so he can stand looking out at it.</summary>
        private Vector3 _seaward = Vector3.forward;

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
        /// <param name="wet">
        /// Whether the sky is awake. He is only here in rain or a storm (owner: "he's only visible
        /// when its raining/turbulent weather") - which is the same shape as the shade's night gate
        /// and makes the pair symmetrical: one wants dark, the other wants weather. It also earns
        /// itself, since what he tends is a machine the sky powers.
        /// </param>
        public void Tick(Player player, Phase phase, bool wanted, bool wet, out bool spoken, out bool paid)
        {
            spoken = false;
            paid = false;

            if (!wanted || !wet || player == null)
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
        public string Bearing(Player player, bool wet)
        {
            if (player == null) return null;

            // Said before any direction, for the same reason the shade's line is: a bearing to
            // somebody who is not there yet reads as a bug rather than as a condition.
            if (!wet) return "He walks only when the sky is awake. Wait for rain.";

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
                SagaTranscript.Record(Name, text);

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

            // The MARGIN overload, for the reason written out in AltarSpot: GetSolidHeight(Vector3)
            // raycasts from a thousand metres up and takes the FIRST collider, so at a cliff edge it
            // returns the CLIFFTOP rather than the ground the spot was chosen for - and it reports a
            // miss by handing back the y it was given, so there is no failure to see. That is how one
            // session put him at y=51.9 on a spot the shore search had measured at 49.3, from which he
            // fell about nineteen metres to the beach.
            //
            // Starting a few metres above the intended height cannot reach over a cliff, and it
            // answers false instead of guessing. On false, keep the generated height: it is what the
            // search chose and it is better than a number from the wrong geometry.
            Vector3 pos = spot;
            try
            {
                float ground;
                if (ZoneSystem.instance.GetSolidHeight(pos, out ground, FootingRayMargin))
                    pos.y = ground + 0.3f;
            }
            catch { }

            // Looking out at the water. Standing with his back to the sea would throw away most of
            // the reason for putting him on a shore at all.
            Quaternion facing = _seaward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(new Vector3(_seaward.x, 0f, _seaward.z).normalized)
                : Quaternion.identity;

            var inst = UnityEngine.Object.Instantiate(prefab, pos, facing);
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
        /// Picks where he waits: the nearest SHORELINE around home, facing the water.
        /// </summary>
        /// <remarks>
        /// The coast is the right place for him and it took no new machinery, only the right height
        /// function. <c>WorldGenerator.GetHeight</c> computes GENERATED terrain height for any point
        /// in the world without the zone being loaded, where <c>ZoneSystem.GetSolidHeight</c> raycasts
        /// real colliders and therefore only answers for the two or three zones around the player.
        /// So the shore can be found three hundred metres away, before the player has ever been there.
        ///
        /// The search walks rays outward from home and looks for the step where the ground crosses the
        /// waterline; the last land sample before that crossing IS the beach. Candidates are scored by
        /// how close their walk is to <see cref="PreferredDistance"/> rather than by being nearest, so
        /// a homestead ten metres from a pond does not get a ten metre pilgrimage.
        ///
        /// He is then pulled back from the crossing by <see cref="ShoreInset"/> so he stands ON the
        /// beach rather than in the surf, and the seaward direction is kept so he can face the water.
        /// Standing with his back to the sea would throw away most of the reason to put him there.
        ///
        /// Falls back to the old dry-land ring when there is no water within MaxSearch - an inland
        /// homestead in the middle of a large continent - because a placed quest-giver in a field
        /// beats an unplaced one anywhere.
        /// </remarks>
        private Vector3 EnsureSpot(Player player)
        {
            if (_spot != null) return _spot.Value;

            Vector3 origin = Home(player) ?? player.transform.position;

            if (TryFindShore(origin, out Vector3 shore, out Vector3 seaward))
            {
                _spot = shore;
                _seaward = seaward;
                Debug.Log($"[ICanShowYouTheWorld] Thjalfi's shore: {shore:0.0}, " +
                          $"{Vector3.Distance(origin, shore):0}m from home.");
                return _spot.Value;
            }

            _spot = DryRing(origin);
            _seaward = (_spot.Value - origin).normalized;
            Debug.Log("[ICanShowYouTheWorld] Thjalfi found no coast in range; he waits inland instead.");
            return _spot.Value;
        }

        /// <summary>
        /// Walks rays out from home and returns the best beach found, with the direction of the water.
        /// </summary>
        private static bool TryFindShore(Vector3 origin, out Vector3 shore, out Vector3 seaward)
        {
            // Strict first, relaxed only if that finds nothing. A placed quest-giver on a bad beach
            // beats an unplaced one, but he should have to earn the bad beach.
            if (TryFindShore(origin, true, out shore, out seaward)) return true;

            // Worth a line in the log: a relaxed placement is the one that can put him somewhere
            // odd, and knowing which pass placed him is the difference between a bug report and a
            // shrug at a world with a rocky coastline.
            if (TryFindShore(origin, false, out shore, out seaward))
            {
                Debug.Log("[ICanShowYouTheWorld] No open, level shore within range; " +
                          "Thjalfi takes the best crossing he can find.");
                return true;
            }

            return false;
        }

        private static bool TryFindShore(Vector3 origin, bool strict, out Vector3 shore, out Vector3 seaward)
        {
            shore = origin;
            seaward = Vector3.forward;

            var gen = WorldGenerator.instance;
            if (gen == null) return false;

            bool found = false;
            float bestScore = float.MaxValue;

            for (int i = 0; i < ShoreRays; i++)
            {
                float angle = (float)i / ShoreRays * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                Vector3 lastLand = origin;
                bool haveLand = false;

                for (float d = MinDistance; d <= MaxSearch; d += ShoreStep)
                {
                    Vector3 at = origin + dir * d;

                    float h;
                    try { h = gen.GetHeight(at.x, at.z); }
                    catch { return false; }   // A generator that throws will throw for every ray.

                    if (h > Waterline)
                    {
                        lastLand = at;
                        haveLand = true;
                        continue;
                    }

                    // Crossed into water. The last land sample is the beach - if there was one, and
                    // if it is far enough out to be a walk rather than the end of the garden.
                    if (!haveLand) break;

                    float distance = Vector3.Distance(origin, lastLand);
                    if (distance < MinDistance) break;

                    Vector3 stand = lastLand - dir * ShoreInset;

                    // A BEACH IS NEAR SEA LEVEL. This was the hole in the two tests below: a cliff
                    // edge dropping straight into open water passes both of them - the sea beyond
                    // keeps being sea, and a clifftop is perfectly flat - so the search happily
                    // reported a shore at y=49 in one play session. He was then placed on it, twenty
                    // metres above the water, and fell.
                    //
                    // Height is the test the crossing cannot give you: the ray walk finds where the
                    // GROUND crosses the waterline, and on a cliff that is the bottom of a drop the
                    // last land sample knows nothing about.
                    float standHeight;
                    try { standHeight = gen.GetHeight(stand.x, stand.z); }
                    catch { break; }

                    if (strict && standHeight > Waterline + BeachHeadroom) break;

                    // A height crossing the waterline is not enough to make a beach. A crevasse
                    // between two cliffs crosses it, and so does a puddle - and the owner found him
                    // standing in one ("sometimes I found him in a crevasse because there was
                    // water"). Two cheap samples tell a coast from a crack: the water beyond must
                    // keep going, and the ground he stands on must be flat.
                    //
                    // Both are skipped on the relaxed pass, which exists so that a genuinely rugged
                    // coastline still gets him a spot rather than exiling him to the inland ring.
                    if (strict && !(OpenWaterBeyond(gen, lastLand, dir) && IsFlatEnough(gen, stand)))
                        break;

                    float score = Mathf.Abs(distance - PreferredDistance);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        shore = stand;
                        seaward = dir;
                        found = true;
                    }
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// True when the water past <paramref name="edge"/> keeps being water.
        /// </summary>
        /// <remarks>
        /// The test that tells a coast from a crevasse. An inlet, a ravine with a stream in it or a
        /// pond all cross the waterline exactly once going out, which is the whole of what the ray
        /// walk was checking - and then there is land again twenty metres on. Open sea does not do
        /// that. Sampling eight steps out is enough to separate the two and costs eight height
        /// lookups on a search that already does hundreds.
        /// </remarks>
        private static bool OpenWaterBeyond(WorldGenerator gen, Vector3 edge, Vector3 dir)
        {
            for (int i = 1; i <= OpenWaterSteps; i++)
            {
                Vector3 at = edge + dir * (ShoreStep * i);

                float h;
                try { h = gen.GetHeight(at.x, at.z); }
                catch { return false; }

                if (h > Waterline) return false;
            }

            return true;
        }

        /// <summary>
        /// True when the ground around a candidate is level enough to stand a man and an altar on.
        /// </summary>
        /// <remarks>
        /// Sampled as a ring rather than a cross, because a ravine floor is flat along its length and
        /// a cross aligned with it reports a meadow. The spread across the whole ring is what matters.
        /// </remarks>
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

                if (h < low) low = h;
                if (h > high) high = h;
            }

            return high - low <= FlatSpread;
        }

        /// <summary>
        /// Where the Storm-Anvil should stand: beside him, on land, at his own level.
        /// </summary>
        /// <remarks>
        /// This used to be "his position plus 2.5 metres along world X", which is a direction with no
        /// relationship to anything. Beside a shore it is a coin flip whether that is the beach or the
        /// sea, and the grounding call underneath it made the failure invisible twice over:
        ///
        ///   <c>ZoneSystem.GetSolidHeight(Vector3)</c> raycasts from a THOUSAND metres up and takes
        ///   the first collider it meets, so a point 2.5m into a cliff face grounds on the clifftop
        ///   above rather than the floor he is standing on - and when the ray hits nothing it returns
        ///   the y it was handed, so there is no failure to detect. Both readings look like success.
        ///
        /// So: fan landward from the water, take the first candidate that grounds on real geometry
        /// near HIS height and above the waterline, and use the margin overload, which starts a few
        /// metres above the point and answers false when it finds nothing.
        ///
        /// The last resort is his own feet. An altar clipping a ghost is a blemish; an altar in the
        /// sea or on a clifftop is a quest the player cannot finish.
        /// </remarks>
        public Vector3 AltarSpot()
        {
            Vector3 feet = Position() ?? _spot ?? Vector3.zero;
            if (feet == Vector3.zero) return feet;

            Vector3 landward = new Vector3(-_seaward.x, 0f, -_seaward.z);
            landward = landward.sqrMagnitude > 0.001f ? landward.normalized : Vector3.forward;

            foreach (float deg in AltarFan)
            {
                Vector3 dir = Quaternion.AngleAxis(deg, Vector3.up) * landward;

                for (float r = AltarNear; r <= AltarFar; r += AltarStep)
                {
                    Vector3 grounded;
                    if (TryGround(feet + dir * r, feet.y, out grounded))
                    {
                        Debug.Log($"[ICanShowYouTheWorld] Storm-Anvil spot {grounded:0.0}, " +
                                  $"{r:0.0}m from Thjalfi at {deg:0}\u00b0 landward.");
                        return grounded;
                    }
                }
            }

            Debug.LogWarning("[ICanShowYouTheWorld] No clear ground beside Thjalfi; " +
                             "the Storm-Anvil goes at his feet.");
            return feet;
        }

        /// <summary>Grounds a point on real geometry near a reference height, or reports failure.</summary>
        private static bool TryGround(Vector3 at, float reference, out Vector3 grounded)
        {
            grounded = at;

            var zones = ZoneSystem.instance;
            if (zones == null) return false;

            float h;
            // The MARGIN overload on purpose - see AltarSpot. It starts heightMargin above the point
            // instead of a kilometre, so it cannot grab a ledge overhead, and it returns a bool.
            try { if (!zones.GetSolidHeight(at, out h, AltarRayMargin)) return false; }
            catch { return false; }

            if (h <= Waterline) return false;                          // not in the surf
            if (Mathf.Abs(h - reference) > AltarMaxStep) return false;  // not on a shelf above or below him

            grounded = new Vector3(at.x, h, at.z);
            return true;
        }

        /// <summary>The old behaviour, kept as the fallback: a ring around home, on dry land.</summary>
        private Vector3 DryRing(Vector3 origin)
        {
            Vector3 candidate = origin;

            for (int attempt = 0; attempt < DryLandAttempts; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
                candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                var gen = WorldGenerator.instance;
                if (gen == null) break;

                try { if (gen.GetHeight(candidate.x, candidate.z) > Waterline) break; }
                catch { break; }
            }

            return candidate;
        }

        private const int ShoreRays = 24;
        private const float ShoreStep = 4f;

        /// <summary>How far back from the water's edge he stands, in metres.</summary>
        private const float ShoreInset = 3f;

        /// <summary>How far out the water must stay water, in <see cref="ShoreStep"/> steps.</summary>
        private const int OpenWaterSteps = 8;

        private const int FlatSamples = 8;
        private const float FlatRadius = 4f;

        /// <summary>Metres of height spread tolerated across the flatness ring - about 25 degrees.</summary>
        private const float FlatSpread = 3.5f;

        /// <summary>
        /// Angles to try for the altar, in order, measured from landward. Deliberately stops short of
        /// 180: the last thing to try is still not straight out to sea.
        /// </summary>
        private static readonly float[] AltarFan = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        private const float AltarNear = 3f;
        private const float AltarFar = 6f;
        private const float AltarStep = 1.5f;

        /// <summary>How far above the candidate the grounding ray starts. Small, so it stays local.</summary>
        private const int AltarRayMargin = 5;

        /// <summary>How far from Thjalfi's own feet the altar's ground may be, in metres.</summary>
        private const float AltarMaxStep = 2f;

        private const int DryLandAttempts = 12;

        /// <summary>Sea level in Valheim's world space, with a margin so he is not ankle-deep.</summary>
        private const float Waterline = 31f;

        /// <summary>
        /// How far above the waterline a candidate may still count as a beach, in metres.
        /// </summary>
        /// <remarks>
        /// Generous enough for a sloping strand and a dune, tight enough to refuse a cliff. The case
        /// this exists for measured 18 metres above the water, so anything under about ten separates
        /// them comfortably; a shore that misses out on a legitimate steep bank falls through to the
        /// relaxed pass, which does not apply this at all.
        /// </remarks>
        private const float BeachHeadroom = 8f;

        /// <summary>How far above his chosen spot the footing ray starts. Small, so it stays local.</summary>
        private const int FootingRayMargin = 5;

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
