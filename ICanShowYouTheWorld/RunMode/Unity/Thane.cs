using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The interact half of the thane. On a CHILD object with its own trigger collider, for the
    /// reason <see cref="ThjalfiTalk"/> is: the game resolves hover text and interacts with
    /// GetComponentInParent from whatever collider the crosshair hit, and a Character is itself
    /// Hoverable on the root, so a component there loses the prompt to the creature's own name.
    /// </summary>
    internal sealed class ThaneTalk : MonoBehaviour, Interactable, Hoverable
    {
        /// <summary>Set by the owner every tick; drives the reply.</summary>
        public Thane.Phase Phase;

        /// <summary>
        /// Set by the owner every tick: the held way has nothing further to teach, at any boss
        /// count. Idle then answers with the last line instead of "come back".
        /// </summary>
        public bool Exhausted;

        /// <summary>
        /// Set by the owner every tick: a way is held, so the alt-use lays it down. Not the same as
        /// "Phase is not Choose" today, but said separately so the respec never hangs on how the
        /// phases happen to be drawn.
        /// </summary>
        public bool WayHeld;

        /// <summary>Raised by an interact, read and cleared by the owner's tick.</summary>
        public bool SpokenPending;
        public bool TaughtPending;
        public bool RespecPending;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            try
            {
                // SPOKEN on every phase, not only Choose: speaking to him is what completes his step,
                // and a way already held when the step is live (a dev cycle, an old save) must not
                // leave the HEARTH chain waiting on a phase it can never reach.
                SpokenPending = true;

                // The alt-use, and only while a way is held. Player.Update computes alt as AltPlace
                // (Shift) or JoyAltPlace - or JoyAltKeys on a gamepad's non-classic layout - held
                // while Use goes down: the same chord Tameable's "rename" and Sadle's "remove" ride.
                // Checked before the phase, so a rung that is due does not swallow the respec into a
                // lesson; with no way held, alt is simply speaking.
                if (alt && WayHeld)
                {
                    Thane.Say(Thane.RespecLine);
                    RespecPending = true;
                    return true;
                }

                switch (Phase)
                {
                    case Thane.Phase.Choose:
                        Thane.Say(Thane.AskLine);
                        return true;

                    case Thane.Phase.Teach:
                        Thane.Say(Thane.TeachLine);
                        TaughtPending = true;
                        return true;

                    default:
                        Thane.Say(Exhausted ? Thane.AfterLine : Thane.NotYetLine);
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The thane could not answer: " + ex.Message);
                return false;
            }
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // Localised here, not by the HUD, exactly as ThjalfiTalk does: Localization.Localize is what
        // turns "$KEY_Use" into the bound key, and returned raw the prompt reads "$KEY_Use".
        public string GetHoverText()
        {
            string text = Thane.Name + "\n[<color=yellow><b>$KEY_Use</b></color>] Speak";

            // The alt chord spelled as Tameable spells its "rename": AltKeys on a gamepad's
            // non-classic layout, AltPlace everywhere else - the two branches Player.Update reads.
            if (WayHeld)
            {
                string alt = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace";
                text += $"\n[<color=yellow><b>{alt} + $KEY_Use</b></color>] Lay the way down (+{Thane.RespecHeat:0} heat)";
            }

            try { return Localization.instance != null ? Localization.instance.Localize(text) : text; }
            catch { return text; }
        }

        public string GetHoverName() => Thane.Name;

        public float GetHoverOffset() => 0f;
    }

    /// <summary>
    /// The thane: Act I's third speaker, who keeps the graves of a hird that came before you and
    /// teaches their WAYS - the classes.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="Thjalfi"/> - one of the put-away, the Ghost prefab, tamed,
    /// non-persistent, created only when the player is near his spot - and deliberately a copy
    /// rather than a shared base. The two share the spawn and the greeting, and differ in
    /// everything that took play-testing to get right: Thjalfi's spot is a shoreline search with a
    /// seaward facing and an altar placed beside him, the shade's is tracked by ZDO id. A base lifted
    /// out of three actors that agree on half their plumbing would be a refactor of working code
    /// for a feature that needed none of it.
    ///
    /// WHY BY DAY. The shade wants dark, Thjalfi wants weather, and he wants to see which stone is
    /// whose. The three gates make a set, and a player who has met all three has had the Meadows'
    /// sky explained to them without a word of it being said.
    ///
    /// WHY NO NAME. What he keeps is other people's names: Eydís, Sigrún, Ulfr and four more. Thjalfi is the one
    /// somebody wrote down; this one only writes others down.
    ///
    /// He keeps names, not light. He never mentions the shortage and cannot answer it, so nothing he
    /// says touches the act's question - he is the saga's one door to a way, and nothing else.
    /// </remarks>
    internal sealed class Thane
    {
        public enum Phase { Choose, Teach, Idle }

        public const string Name = "The thane";

        /// <summary>The Ghost prefab, as the shade and Thjalfi use - he is one of the put-away too.</summary>
        internal const string Prefab = "Ghost";

        public const string AskLine =
            "I buried them. Seven of a hird that came before you, and I am what is left of the eighth — kept " +
            "here to count the stones, since nobody came for any of us.\n\n" +
            "Eydís, who hunted, and whom the beasts followed. Sigrún, who mended, and called up what the ground " +
            "held. Ulfr, who never learned how to stop. Halvard, who stood where he was put and did not move. " +
            "Ormr, who sang the rest of us onward. Ragna, who was never once afraid of water. Dvalinn, who built " +
            "the hall they all died in.\n\n" +
            "None of them needs the name any more. Take one up and I will teach you what they knew — a little " +
            "at a time, as the gods fall. Or take none, and go. The stones are kept either way.";

        public const string TeachLine =
            "There. That was theirs, and now it is yours. Do not thank me. I only kept it.";

        public const string NotYetLine =
            "Nothing more yet. What they knew came to them slowly, and it comes to you the same way. When a " +
            "god falls, the rest will find you where you stand.";

        public const string AfterLine =
            "That is all of it. All I kept, anyway. The stones stay kept.";

        /// <summary>Said when a held way is laid down at his graves (the alt-use). Owner's text, verbatim.</summary>
        public const string RespecLine =
            "You can put a name down. It costs — the world hears a name change hands, and it turns to look.\n\n" +
            "Which stone, then? The others are still kept.";

        /// <summary>
        /// What laying a way down costs. Heat, because the world notices a change of name; and heat is
        /// the saga's own price, so a respec needs no new item flow, no new save field and no new
        /// shop - just the one number every other risk in the run is already paid in. Three, so it is
        /// felt without being a punishment for having tried a way on. Here rather than in the host
        /// because his hover text says the price.
        /// </summary>
        public const float RespecHeat = 3f;

        // One line each, in a bubble over his head as you come near - the way the trader greets and
        // Thjalfi does. The rune panel is for what he has to SAY; this is for him being there.
        private const string GreetChoose = "Seven stones. I know whose. Come and I will tell you.";
        private const string GreetTeach = "They left more than names. Come — there is a thing to learn.";
        private const string GreetIdle = "Their stones are kept. Go on.";

        /// <summary>
        /// Said once, right after the pick. Keyed by <see cref="ClassDefinition.Id"/>; null for an id
        /// he has no line for, which the caller treats as "say nothing" rather than a generic line.
        /// </summary>
        public static string ChosenLine(string classId)
        {
            switch (classId)
            {
                case "hunter":
                    return "Eydís, then. She walked with a wolf at heel and nothing saw her go. The wolf comes " +
                           "when you call now. The rest of her comes as the gods fall.";
                case "volva":
                    return "Sigrún, then. What she touched, mended; what she called, came up out of the ground. " +
                           "The dead rise at your word from the first. Then the warmth you can pour out, and last, the sky.";
                case "berserker":
                    return "Ulfr, then. He struck all round him and never once from behind a shield. The sweep " +
                           "is yours now. The rest of him comes when a god has fallen — and you may not want it. Take his axes.";
                case "huskarl":
                    return "Halvard, then. He stood where he was put, and what came at him met the shield first. The " +
                           "bash is yours now; the wall and the last stand come as the gods fall. Take his shield and spear.";
                case "skald":
                    return "Ormr, then. He sang, and the rest of us kept walking when we should have dropped. The " +
                           "marching song is yours now. The others come as the gods fall. Take his flask.";
                case "saefari":
                    return "Ragna, then. She was never once afraid of water, and it never once took her. The tide is " +
                           "yours now; the wind and the sea-legs come as the gods fall. Take her harpoon.";
                case "smidr":
                    return "Dvalinn, then. He built the hall they all died in, and it is still standing. The field " +
                           "forge is yours now; the master’s minute and the reinforcing come as the gods fall. Take his tools.";
                default:
                    return null;
            }
        }

        /// <summary>The BOOK's line for the choice: past tense, the chronicle's voice.</summary>
        public static string BookLine(string classId)
        {
            switch (classId)
            {
                case "hunter":
                    return "At the graves the thane named seven, and you took up the way of Eydís, who hunted.";
                case "volva":
                    return "At the graves the thane named seven, and you took up the way of Sigrún, who mended.";
                case "berserker":
                    return "At the graves the thane named seven, and you took up the way of Ulfr, who never stopped.";
                case "huskarl":
                    return "At the graves the thane named seven, and you took up the way of Halvard, who stood.";
                case "skald":
                    return "At the graves the thane named seven, and you took up the way of Ormr, who sang.";
                case "saefari":
                    return "At the graves the thane named seven, and you took up the way of Ragna, who feared no water.";
                case "smidr":
                    return "At the graves the thane named seven, and you took up the way of Dvalinn, who built.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// How far from the claimed bed he stands. A walk, not the end of the garden - and nearer
        /// than Thjalfi's shore can be, because he is the one you come BACK to, once per god.
        /// </summary>
        private const float MinDistance = 60f;
        private const float MaxDistance = 120f;

        /// <summary>He is only CREATED once the player is near his spot, so he is never culled at birth.</summary>
        private const float SpawnRange = 70f;

        /// <summary>How close the player must come for him to speak first.</summary>
        private const float GreetRange = 10f;

        /// <summary>Random candidates tried per pass when choosing his spot.</summary>
        private const int SpotAttempts = 24;

        /// <summary>Sea level in world space with a margin, as Thjalfi uses: not ankle-deep.</summary>
        private const float Waterline = 31f;

        private const int FlatSamples = 8;
        private const float FlatRadius = 4f;

        /// <summary>Metres of height spread tolerated across the flatness ring - Thjalfi's figure.</summary>
        private const float FlatSpread = 3.5f;

        /// <summary>How far above his chosen spot the footing ray starts. Small, so it stays local.</summary>
        private const int FootingRayMargin = 5;

        private readonly System.Random _rng;

        private Vector3? _spot;

        /// <summary>Which way home lies from his spot, so he stands facing the player's door.</summary>
        private Vector3 _homeward = Vector3.forward;

        private ThaneTalk _talk;
        private GameObject _body;
        private Phase? _greetedFor;

        public Thane(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        /// <summary>Forgets the spot and dismisses him. Run start.</summary>
        public void Reset()
        {
            Dismiss();
            _spot = null;
        }

        public bool Standing => _body != null;

        public Vector3? Position() => _body != null ? _body.transform.position : (Vector3?)null;

        /// <summary>
        /// The graves he stands at, once chosen; null before the first day he was wanted. For the
        /// map pin, which wants the PLACE rather than the body - he is only standing by day, and
        /// the pin is there to be read at night too.
        /// </summary>
        public Vector3? Spot() => _spot;

        /// <summary>
        /// Call about once a second. Keeps him standing while he is wanted and it is day, and reports
        /// what the player did at him.
        /// </summary>
        /// <param name="exhausted">The held way has nothing further to teach; Idle says the last line.</param>
        /// <param name="day">
        /// He stands by daylight only - see the class remarks. The gate applies to every phase, not
        /// just the first meeting as Thjalfi's rain does: nothing about teaching is urgent enough to
        /// be worth breaking the one thing that makes him him, and the next morning is minutes away.
        /// </param>
        /// <param name="wayHeld">A way is held, so the alt-use lays it down (<paramref name="respec"/>).</param>
        public void Tick(Player player, Phase phase, bool exhausted, bool wayHeld, bool wanted, bool day,
                         out bool spoken, out bool taught, out bool respec)
        {
            spoken = false;
            taught = false;
            respec = false;

            if (!wanted || !day || player == null)
            {
                if (Standing) Dismiss();
                return;
            }

            if (!Standing) TrySpawn(player);
            if (_talk == null) return;

            _talk.Phase = phase;
            _talk.Exhausted = exhausted;
            _talk.WayHeld = wayHeld;
            Greet(player, phase);

            if (_talk.SpokenPending)
            {
                _talk.SpokenPending = false;
                spoken = true;
            }
            if (_talk.TaughtPending)
            {
                _talk.TaughtPending = false;
                taught = true;
            }
            if (_talk.RespecPending)
            {
                _talk.RespecPending = false;
                respec = true;
            }
        }

        /// <summary>
        /// A coarse bearing to him, or null when there is nothing to say.
        /// </summary>
        /// <param name="teaching">A rung is due: the "more to teach" wording rather than "someone waits".</param>
        public string Bearing(Player player, bool day, bool teaching)
        {
            if (player == null) return null;

            // Said before any direction, as the shade's and Thjalfi's are: a bearing to somebody who
            // is not there yet reads as a bug rather than as a condition.
            if (!day) return "He counts the stones by daylight. Wait for morning.";

            Vector3? position = Position() ?? _spot;
            if (position == null) return null;

            Vector3 delta = position.Value - player.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 8f) return "He is here. Speak to him.";

            string compass = BiomeCompass.Compass(delta);
            string metres = $"{Mathf.Round(distance / 10f) * 10f:0}";

            return teaching
                ? $"The one at the graves has more to teach — {compass}, {metres}m"
                : $"Someone waits {compass}, {metres}m";
        }

        /// <summary>The game's rune panel, the one lore stones use - as Thjalfi and the shade speak.</summary>
        public static void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                SagaTranscript.Record(Name, text);

                var viewer = TextViewer.instance;
                if (viewer != null) viewer.ShowText(TextViewer.Style.Rune, Name, text, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The thane's line could not be shown: " + ex.Message);
            }
        }

        private void Greet(Player player, Phase phase)
        {
            if (_body == null || _greetedFor == phase) return;

            if (Vector3.Distance(player.transform.position, _body.transform.position) > GreetRange) return;

            // Not while the rune panel is up: a bubble raised under it is gone before the panel is.
            try { if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return; } catch { }

            _greetedFor = phase;
            string line = phase == Phase.Choose ? GreetChoose : phase == Phase.Teach ? GreetTeach : GreetIdle;

            try
            {
                var chat = Chat.instance;
                if (chat != null)
                    chat.SetNpcText(_body, Vector3.up * 2.2f, 30f, 12f, Name, line, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The thane could not greet: " + ex.Message);
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
                Debug.LogError($"[ICanShowYouTheWorld] Cannot spawn the thane: no '{Prefab}' prefab.");
                return;
            }

            // The MARGIN overload, for the reason Thjalfi's TrySpawn writes out: the plain
            // GetSolidHeight(Vector3) raycasts from a kilometre up, takes the first collider and
            // reports a miss by returning the y it was given. Starting a few metres above the
            // generated height cannot reach over a cliff, and answers false instead of guessing.
            Vector3 pos = spot;
            try
            {
                float ground;
                if (ZoneSystem.instance.GetSolidHeight(pos, out ground, FootingRayMargin))
                    pos.y = ground + 0.3f;
            }
            catch { }

            // Facing the player's door. He is kept here waiting for somebody to come.
            Quaternion facing = _homeward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(new Vector3(_homeward.x, 0f, _homeward.z).normalized)
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

            // Tamed so he never fights and nothing fights him; parked so he stays put. A teacher who
            // wanders off is a way the player can never take up.
            try { ch.SetTamed(true); } catch { }
            // And immune, as every later speaker is: tamed means an enemy to every monster, and a
            // greyling or a passing troll would otherwise kill a quest-giver mid-errand (review, 2026-10-05).
            SagaSpeaker.MakeImmune(ch);
            ch.m_name = Name;
            try
            {
                var ai = inst.GetComponent<BaseAI>();
                if (ai != null) ai.SetPatrolPoint();
            }
            catch { }

            // Two frames after spawn, never now: LevelEffects re-sets the look on its own Start, and
            // a look applied before that is overwritten. See CreatureDressing.
            CreatureDressing.ApplyWhenSettled(inst, CreatureDressing.Thane());

            var talkObject = new GameObject("saga_thane_talk");
            talkObject.transform.SetParent(inst.transform, false);
            talkObject.transform.localPosition = Vector3.up * 1.2f;
            talkObject.layer = inst.layer;
            var trigger = talkObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            _talk = talkObject.AddComponent<ThaneTalk>();

            _body = inst;
            _greetedFor = null;

            Debug.Log($"[ICanShowYouTheWorld] The thane stands at {pos:0.0}.");
        }

        /// <summary>
        /// Picks where he stands, ONCE per run: a ring 60-120 m around the claimed bed, on dry,
        /// level land.
        /// </summary>
        /// <remarks>
        /// Chosen from GENERATED terrain (<c>WorldGenerator.GetHeight</c>), which answers for any
        /// point in the world without its zone being loaded - the reason Thjalfi's shore search can
        /// look three hundred metres out. Real colliders only answer near the player, and the spot
        /// has to exist before the player has walked there, because the bearing points at it.
        ///
        /// Strict first (above the waterline AND level, Thjalfi's flatness ring), then land alone,
        /// then - a homestead on a sliver of an island - the first candidate anyway, logged. A placed
        /// teacher in an odd spot beats an unplaced one, but he should have to earn the odd spot.
        ///
        /// Kept in <c>_spot</c> until <see cref="Reset"/>: he is the one you come back to after each
        /// god, and a grave that moved between visits would be a stranger thing than any line he says.
        /// </remarks>
        private Vector3 EnsureSpot(Player player)
        {
            if (_spot != null) return _spot.Value;

            Vector3 origin = Home() ?? player.transform.position;
            Vector3 chosen;
            string pass;

            if (TryRing(origin, true, out chosen)) pass = "level ground";
            else if (TryRing(origin, false, out chosen)) pass = "the only dry ground he could find";
            else
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                chosen = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * MinDistance;
                pass = "no dry ground in range - placed anyway";
            }

            _spot = chosen;
            Vector3 toHome = origin - chosen;
            toHome.y = 0f;
            _homeward = toHome.sqrMagnitude > 0.001f ? toHome.normalized : Vector3.forward;

            Debug.Log($"[ICanShowYouTheWorld] The thane's graves: {chosen:0.0}, " +
                      $"{Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(chosen.x, 0f, chosen.z)):0}m " +
                      $"from {(Home() != null ? "the claimed bed" : "the player (no bed claimed)")}, on {pass}.");
            return _spot.Value;
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
                catch { return false; }   // A generator that throws will throw for every candidate.

                if (h <= Waterline) continue;
                if (strict && !IsFlatEnough(gen, at)) continue;

                chosen = new Vector3(at.x, h, at.z);
                return true;
            }

            return false;
        }

        /// <summary>Thjalfi's flatness ring: the spread of generated height around a candidate.</summary>
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

                if (h <= Waterline) return false;   // a pond at his elbow is not dry ground
                if (h < low) low = h;
                if (h > high) high = h;
            }

            return high - low <= FlatSpread;
        }

        /// <summary>The claimed bed, when there is one - the same read as the shade's and Thjalfi's.</summary>
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

        /// <summary>Sends him away. His spot is kept: the graves do not move.</summary>
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
