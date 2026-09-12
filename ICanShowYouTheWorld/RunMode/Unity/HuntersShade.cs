using System;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The interact half of the shade: what the crosshair says and what an E-press does. Lives on a
    /// CHILD object with its own trigger collider, not on the creature's root.
    ///
    /// That placement is the one non-obvious thing here. The game finds hover text and interacts
    /// with GetComponentInParent from whatever collider the crosshair ray hit. A Character is
    /// itself Hoverable, sits on the root, and was added before anything we add — so a component
    /// on the root loses the hover text to the creature's own (its name, no prompt). A child that
    /// carries its own collider is hit first and searched first, and wins both.
    /// </summary>
    internal sealed class ShadeTalk : MonoBehaviour, Interactable, Hoverable
    {
        /// <summary>Set by the owner every tick; drives the prompt and the reply.</summary>
        public HuntersShade.Phase Phase;

        /// <summary>Raised by an interact, read and cleared by the owner's tick.</summary>
        public bool SpokenPending;
        public bool DeliveredPending;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            try
            {
                switch (Phase)
                {
                    case HuntersShade.Phase.Find:
                        HuntersShade.Say(HuntersShade.AskLine);
                        SpokenPending = true;
                        return true;

                    case HuntersShade.Phase.Deliver:
                    {
                        var inv = user != null ? user.GetInventory() : null;
                        if (inv == null) return false;

                        if (HuntersShade.Price.All(p => inv.CountItems(p.token) >= p.amount))
                        {
                            foreach (var p in HuntersShade.Price) inv.RemoveItem(p.token, p.amount);
                            HuntersShade.Say(HuntersShade.TaughtLine);
                            DeliveredPending = true;
                        }
                        else
                        {
                            HuntersShade.Say(HuntersShade.NotYetLine);
                        }
                        return true;
                    }

                    default:
                        HuntersShade.Say(HuntersShade.AfterLine);
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The shade could not answer: " + ex.Message);
                return false;
            }
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverText()
        {
            switch (Phase)
            {
                case HuntersShade.Phase.Deliver:
                {
                    string have = string.Empty;
                    try
                    {
                        var inv = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
                        if (inv != null)
                            have = "  (" + string.Join(", ",
                                HuntersShade.Price.Select(p => $"{Mathf.Min(inv.CountItems(p.token), p.amount)}/{p.amount} {p.label}").ToArray()) + ")";
                    }
                    catch { }

                    return $"{HuntersShade.Name}\n[<color=yellow><b>$KEY_Use</b></color>] Give " +
                           string.Join(", ", HuntersShade.Price.Select(p => $"{p.amount} {p.label}").ToArray()) + have;
                }
                default:
                    return $"{HuntersShade.Name}\n[<color=yellow><b>$KEY_Use</b></color>] Speak";
            }
        }

        public string GetHoverName() => HuntersShade.Name;

        public float GetHoverOffset() => 0f;
    }

    /// <summary>
    /// Act I's quest-giver: a hunter who died before the bow was strung, standing near the
    /// player's bed after dark. Speak to it, bring it what it lacked, and it teaches the saga's
    /// recipe for a hunter's bow at the workbench.
    ///
    /// Why a shade: the story bible has ravens witness and never help, the Meadows have no living
    /// human, and "everything here is after its life" is the premise — so the one who can teach
    /// is someone who did not finish. It wears the Ghost prefab, the same body the deer's lights
    /// fall back to, with no shipped assets.
    ///
    /// Why night only: the act's one rule is "nothing you seek walks in the light", taught before
    /// it is tested. The shade is dismissed at dawn and returns at dusk, near the same spot.
    ///
    /// Non-persistent, like the Herald and the lights: a shade that survived a reload would stand
    /// there forever with nothing to say. It is re-made whenever its steps are live, the sun is
    /// down, and none is standing — which is also what survives a logout or a wander.
    ///
    /// What it teaches is derived, not stored: the recipe registers while the delivery step is
    /// DONE (see StepPredicates.StepDone), so a resume recomputes it and nothing new is saved.
    /// </summary>
    internal sealed class HuntersShade
    {
        public enum Phase { Find, Deliver, Done }

        public const string Name = "A hunter’s shade";
        public const string Prefab = "Ghost";

        /// <summary>
        /// What the shade asks for: NOT the bow's own makings (those are the recipe's cost) but
        /// the quiver it never filled. Tokens are what CountItems/RemoveItem compare on; the
        /// labels are for the prompt. Both are asset data — checked once at spawn, loudly.
        /// </summary>
        public static readonly (string token, string label, int amount)[] Price =
        {
            ("$item_flint",         "Flint",          10),
            ("$item_leatherscraps", "Leather scraps",  5),
        };

        // The shade's lines. Rune-panel prose: short, and nothing the world does not back.
        public const string AskLine =
            "I hunted this herd before you. The forest took me with the bow unstrung, and a hunter " +
            "who never loosed is a poor thing to be for an age.\n\n" +
            "Bring me what I lacked — ten flint and five scraps of leather, the quiver I never " +
            "filled — and I will show you how the bow is strung. Come after dark. I walk in nothing else.";

        public const string NotYetLine =
            "Not yet. Ten flint, five scraps of leather. I have waited longer than this.";

        public const string TaughtLine =
            "Good. Now mark it: wood for the stave, resin to seal it, and the herd’s own hide for " +
            "the wrap. Your bench knows the shape now.\n\n" +
            "Men will call it Thor’s. Let them — the storm in it is the herd’s, and Eikthyr’s own, " +
            "turned. String it well. What you loose from it, you owe the herd a clean shot.";

        public const string AfterLine =
            "Wood, resin, the herd’s hide. The bench knows it as Thor’s bow. Go and string it.";

        /// <summary>How far from the bed the shade stands.</summary>
        private const float MinDistance = 7f;
        private const float MaxDistance = 11f;

        /// <summary>The shade is only CREATED once the player is near its spot, so it is never culled at birth.</summary>
        private const float SpawnRange = 60f;

        private readonly System.Random _rng;

        private ZDOID _shade = ZDOID.None;
        private Vector3? _spot;
        private ShadeTalk _talk;
        private bool _priceChecked;

        public HuntersShade(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        /// <summary>Forgets the spot and dismisses any standing shade. Run start and end.</summary>
        public void Reset()
        {
            Dismiss();
            _spot = null;
            _priceChecked = false;
        }

        public bool Standing => Position() != null;

        /// <summary>
        /// Call about once a second, in Act I. Keeps the shade standing while it is wanted and the
        /// sun is down, dismisses it otherwise, and reports what the player did at it.
        /// </summary>
        public void Tick(Player player, Phase phase, bool wanted, bool night, out bool spoken, out bool delivered)
        {
            spoken = false;
            delivered = false;

            if (!wanted || !night || player == null)
            {
                if (Standing) Dismiss();
                return;
            }

            if (!Standing) TrySpawn(player);

            if (_talk == null) return;

            _talk.Phase = phase;

            if (_talk.SpokenPending)
            {
                _talk.SpokenPending = false;
                spoken = true;
            }
            if (_talk.DeliveredPending)
            {
                _talk.DeliveredPending = false;
                delivered = true;
            }
        }

        /// <summary>
        /// Where it waits, as a coarse bearing from the player, or a sentence when it will not
        /// show. Null when there is nothing to say.
        /// </summary>
        public string Bearing(Player player, bool night)
        {
            if (player == null) return null;
            if (!night) return "The shade walks only after dark. Wait near your bed.";

            Vector3? position = Position() ?? _spot ?? Home(player);
            if (position == null) return null;

            Vector3 delta = position.Value - player.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 6f) return "The shade is here. Speak to it.";

            return $"The shade waits {BiomeCompass.Compass(delta)}, {Mathf.Round(distance / 10f) * 10f:0}m";
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
                Debug.LogWarning("[ICanShowYouTheWorld] The shade's line could not be shown: " + ex.Message);
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
                Debug.LogError($"[ICanShowYouTheWorld] Cannot spawn the shade: no '{Prefab}' prefab.");
                return;
            }

            CheckPriceOnce();

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

            // Tamed so it never fights and nothing fights it; parked so it stays put. A ghost's
            // AI otherwise drifts, and a quest-giver that wanders off is a quest-giver lost.
            try { ch.SetTamed(true); } catch { }
            ch.m_name = Name;
            // SetPatrolPoint() takes the creature's CURRENT position as its patrol centre; the
            // overload taking a point is not public in this build, and it stands where it spawned.
            try
            {
                var ai = inst.GetComponent<BaseAI>();
                if (ai != null) ai.SetPatrolPoint();
            }
            catch { }

            // Pale and cool-lit, like the Herald: the opposite of the forest's things, and
            // findable at night from a distance, which is the whole point of the light.
            CreatureDressing.Apply(inst, CreatureDressing.Shade());

            // The interact lives on a child with its own trigger — see ShadeTalk for why.
            var talkObject = new GameObject("saga_shade_talk");
            talkObject.transform.SetParent(inst.transform, false);
            talkObject.transform.localPosition = Vector3.up * 1.2f;
            talkObject.layer = inst.layer;
            var trigger = talkObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.4f;
            _talk = talkObject.AddComponent<ShadeTalk>();

            _shade = zdo.m_uid;
        }

        private Vector3 EnsureSpot(Player player)
        {
            if (_spot != null) return _spot.Value;

            Vector3 origin = Home(player) ?? player.transform.position;
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float distance = MinDistance + (float)_rng.NextDouble() * (MaxDistance - MinDistance);
            _spot = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            return _spot.Value;
        }

        /// <summary>The claimed bed, when there is one. The homestead is where a living spark is found.</summary>
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

        private Vector3? Position()
        {
            if (_shade == ZDOID.None) return null;
            try
            {
                var zdo = ZDOMan.instance?.GetZDO(_shade);
                return zdo?.GetPosition();
            }
            catch
            {
                return null;
            }
        }

        private void Dismiss()
        {
            try
            {
                var zdo = _shade == ZDOID.None ? null : ZDOMan.instance?.GetZDO(_shade);
                if (zdo != null) ZDOMan.instance.DestroyZDO(zdo);
            }
            catch { }

            _shade = ZDOID.None;
            _talk = null;
        }

        /// <summary>
        /// The price's item tokens are asset data. Once per run, say so if the database does not
        /// have them — a delivery step that can never be paid would otherwise look like a player
        /// who has not found flint yet.
        /// </summary>
        private void CheckPriceOnce()
        {
            if (_priceChecked) return;
            _priceChecked = true;

            try
            {
                var odb = ObjectDB.instance;
                if (odb == null || odb.m_items == null) return;

                var known = odb.m_items
                    .Where(go => go != null)
                    .Select(go => go.GetComponent<ItemDrop>())
                    .Where(d => d != null && d.m_itemData != null && d.m_itemData.m_shared != null)
                    .Select(d => d.m_itemData.m_shared.m_name)
                    .ToList();

                var missing = Price.Where(p => !known.Contains(p.token)).Select(p => p.token).ToArray();
                if (missing.Length > 0)
                    Debug.LogError("[ICanShowYouTheWorld] The shade asks for item tokens the game does not have — " +
                                   $"its delivery can never be paid: {string.Join(", ", missing)}");
            }
            catch { }
        }
    }
}
