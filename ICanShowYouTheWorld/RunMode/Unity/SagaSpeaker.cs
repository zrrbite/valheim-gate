using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The interact half of any <see cref="SagaSpeaker"/>. On a CHILD object with its own trigger
    /// collider, for the reason every speaker's is: the game resolves hover text and interacts with
    /// GetComponentInParent from whatever collider the crosshair hit, and a Character is itself
    /// Hoverable on the root, so a component there loses the prompt to the creature's own name.
    /// </summary>
    internal sealed class SagaSpeakerTalk : MonoBehaviour, Interactable, Hoverable
    {
        public SagaSpeaker Owner;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || Owner == null) return false;

            try { return Owner.OnInteract(user, alt); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] {Owner.Name} could not answer: {ex.Message}");
                return false;
            }
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // Localised here, not by the HUD: Localization.Localize is what turns "$KEY_Use" into the
        // bound key, and returned raw the prompt reads "$KEY_Use".
        public string GetHoverText()
        {
            string text = Owner != null ? Owner.HoverText(Player.m_localPlayer) : string.Empty;
            try { return Localization.instance != null ? Localization.instance.Localize(text) : text; }
            catch { return text; }
        }

        public string GetHoverName() => Owner != null ? Owner.Name : string.Empty;

        public float GetHoverOffset() => 0f;
    }

    /// <summary>
    /// What every saga speaker shares: one of the put-away in the Ghost's body, created only when
    /// the player is near their spot, tamed and parked, dressed after it settles, greeting once per
    /// line, speaking through the rune panel, and taking a price from the pack.
    /// </summary>
    /// <remarks>
    /// Lifted out of the thane, Thjalfi and the shade, which each carry their own copy - and which
    /// are deliberately NOT moved onto this yet (2026-10-05): they are verified in play, and a base
    /// should prove itself on new speakers before working ones are rewritten onto it. The
    /// barrow-keeper is the first.
    ///
    /// A subclass says WHO (name, look), WHERE (its spot), and WHAT (greeting, hover, interact).
    /// Everything about bodies, colliders and ZDOs stays here.
    /// </remarks>
    internal abstract class SagaSpeaker
    {
        /// <summary>
        /// The body. The Ghost by default - most speakers are of the put-away. The drowned one wears
        /// a Draugr, because in the swamp the dead are not ghosts but bodies that would not lie down.
        /// </summary>
        protected virtual string BodyPrefab => "Ghost";

        /// <summary>
        /// Tried in order when <see cref="BodyPrefab"/> does not resolve - a body name is asset data
        /// this assembly cannot verify. The Ghost is always the last resort, so a speaker whose
        /// guessed body is wrong still stands (in the wrong clothes) rather than never appearing.
        /// </summary>
        protected virtual string[] BodyFallbacks => new string[0];

        /// <summary>Every body this speaker will wear, in the order tried: its own, its fallbacks, then the Ghost.</summary>
        internal IList<string> BodyCandidates =>
            new[] { BodyPrefab }.Concat(BodyFallbacks ?? new string[0]).Concat(new[] { "Ghost" }).Distinct().ToList();

        /// <summary>Only CREATED once the player is near the spot, so never culled at birth.</summary>
        protected virtual float SpawnRange => 70f;

        /// <summary>How close the player must come for the speaker to greet first.</summary>
        protected virtual float GreetRange => 10f;

        /// <summary>How far above the chosen spot the footing ray starts. Small, so it stays local.</summary>
        private const int FootingRayMargin = 5;

        public abstract string Name { get; }

        /// <summary>The palette. Every speaker must read as a different person.</summary>
        protected abstract CreatureDressing.Look Look { get; }

        /// <summary>Where they stand, or null while that cannot be known yet. Called each tick until found.</summary>
        protected abstract Vector3? ChooseSpot(Player player);

        /// <summary>The bubble line for the current state; greeted once per distinct line.</summary>
        protected abstract string Greeting { get; }

        public abstract string HoverText(Player player);

        /// <summary>The player pressed Use (or alt-Use). Return true when handled.</summary>
        public abstract bool OnInteract(Humanoid user, bool alt);

        private Vector3? _spot;
        private GameObject _body;
        private string _greeted;

        public bool Standing => _body != null;

        public Vector3? Position() => _body != null ? _body.transform.position : (Vector3?)null;

        /// <summary>The place, once chosen - for the map pin, which wants it even when nobody stands there.</summary>
        public Vector3? Spot() => _spot;

        /// <summary>Forgets the spot and dismisses them. Run start and run end.</summary>
        public virtual void Reset()
        {
            Dismiss();
            _spot = null;
        }

        /// <summary>
        /// Keeps them standing while wanted and the player is in range; dismisses them otherwise.
        /// Call about once a second.
        /// </summary>
        protected void Stand(Player player, bool wanted)
        {
            if (!wanted || player == null)
            {
                if (Standing) Dismiss();
                return;
            }

            if (_spot == null) _spot = ChooseSpot(player);
            if (_spot == null) return;

            if (!Standing) TrySpawn(player, _spot.Value);
            if (Standing) Greet(player);
        }

        /// <summary>The game's rune panel, the one lore stones use.</summary>
        public void Say(string text)
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
                Debug.LogWarning($"[ICanShowYouTheWorld] {Name}'s line could not be shown: {ex.Message}");
            }
        }

        /// <summary>A coarse bearing, or null before the spot is known.</summary>
        /// <param name="waiting">The sentence's start, e.g. "Someone waits at the burial chambers".</param>
        public string Bearing(Player player, string waiting)
        {
            if (player == null) return null;

            Vector3? position = Position() ?? _spot;
            if (position == null) return null;

            Vector3 delta = position.Value - player.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 8f) return $"{Name} is here. Speak to them.";

            return $"{waiting} — {BiomeCompass.Compass(delta)}, {Mathf.Round(distance / 10f) * 10f:0}m";
        }

        /// <summary>Takes the price from the inventory if all of it is there. False, and nothing taken, otherwise.</summary>
        protected static bool TryPay(Inventory inv, (string token, int amount, string label)[] price)
        {
            if (inv == null || price == null) return false;
            if (!price.All(p => inv.CountItems(p.token) >= p.amount)) return false;

            foreach (var p in price) inv.RemoveItem(p.token, p.amount);
            return true;
        }

        /// <summary>"0/1 light" - what the hover shows while a price is owed.</summary>
        protected static string PriceProgress(Inventory inv, (string token, int amount, string label)[] price)
        {
            if (inv == null || price == null) return string.Empty;
            return string.Join(", ", price
                .Select(p => $"{Mathf.Min(inv.CountItems(p.token), p.amount)}/{p.amount} {p.label}")
                .ToArray());
        }

        private void Greet(Player player)
        {
            string line = Greeting;
            if (_body == null || string.IsNullOrEmpty(line) || _greeted == line) return;

            if (Vector3.Distance(player.transform.position, _body.transform.position) > GreetRange) return;

            // Not while the rune panel is up: a bubble raised under it is gone before the panel is.
            try { if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return; } catch { }

            _greeted = line;

            try
            {
                var chat = Chat.instance;
                if (chat != null) chat.SetNpcText(_body, Vector3.up * 2.2f, 30f, 12f, Name, line, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] {Name} could not greet: {ex.Message}");
            }
        }

        private void TrySpawn(Player player, Vector3 spot)
        {
            if (Vector3.Distance(player.transform.position, spot) > SpawnRange) return;

            var scene = ZNetScene.instance;
            GameObject prefab = null;
            string bodyUsed = null;
            if (scene != null)
            {
                foreach (var candidate in BodyCandidates)
                {
                    prefab = scene.GetPrefab(candidate);
                    if (prefab != null) { bodyUsed = candidate; break; }
                }
            }
            if (prefab == null)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Cannot spawn {Name}: no '{BodyPrefab}' prefab, nor any fallback.");
                return;
            }
            if (bodyUsed != BodyPrefab)
                Debug.LogWarning($"[ICanShowYouTheWorld] {Name}: no '{BodyPrefab}' body - wearing '{bodyUsed}'.");

            // The MARGIN overload: the plain GetSolidHeight(Vector3) raycasts from a kilometre up and
            // takes the first collider. A few metres above the generated height cannot reach over a
            // cliff, and answers false instead of guessing.
            Vector3 pos = spot;
            try
            {
                float ground;
                if (ZoneSystem.instance.GetSolidHeight(pos, out ground, FootingRayMargin))
                    pos.y = ground + 0.3f;
            }
            catch { }

            // Facing whoever came: they are here to be found.
            Vector3 toPlayer = player.transform.position - pos;
            toPlayer.y = 0f;
            Quaternion facing = toPlayer.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toPlayer.normalized)
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

            // Tamed so they never fight the player; parked so they stay put.
            try { ch.SetTamed(true); } catch { }

            // But the game counts a tamed creature as an ENEMY of every monster (review, 2026-10-05):
            // at a sunken crypt the drowned one was hunted by draugr, died, dropped loot and was made
            // again every second. So a speaker is IMMUNE to every real damage type while it speaks.
            // (m_nonPlayer, tried first, is a damage TYPE nothing deals - not "damage from
            // non-players" - and changed nothing; the second review caught it.) The drowned one
            // lifts this for his last beat (Unshield).
            _originalModifiers = MakeImmune(ch);
            ch.m_name = Name;
            try
            {
                var ai = inst.GetComponent<BaseAI>();
                if (ai != null) ai.SetPatrolPoint();
            }
            catch { }

            // Two frames after spawn, never now: LevelEffects re-sets the look on its own Start, and
            // copies whatever it finds into a STATIC per-prefab cache. See CreatureDressing.
            CreatureDressing.ApplyWhenSettled(inst, Look);

            var talkObject = new GameObject("saga_speaker_talk");
            talkObject.transform.SetParent(inst.transform, false);
            talkObject.transform.localPosition = Vector3.up * 1.2f;
            talkObject.layer = inst.layer;
            var trigger = talkObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            talkObject.AddComponent<SagaSpeakerTalk>().Owner = this;

            _body = inst;
            _greeted = null;

            try { OnSpawned(ch); }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] {Name}'s spawn hook failed: {ex.Message}"); }

            Debug.Log($"[ICanShowYouTheWorld] {Name} stands at {pos:0.0}.");
        }

        /// <summary>After a body is made, tamed and dressed. The drowned one untames himself here when it is time.</summary>
        protected virtual void OnSpawned(Character body) { }

        private HitData.DamageModifiers _originalModifiers;

        /// <summary>
        /// Makes a speaker's body immune to every real damage type, and returns the modifiers it had.
        /// Static so Act I's three (the shade, Thjalfi, the thane), which predate this base class,
        /// share it: they are tamed too, and so hunted by every monster that passes.
        /// </summary>
        internal static HitData.DamageModifiers MakeImmune(Character ch)
        {
            var original = default(HitData.DamageModifiers);
            if (ch == null) return original;
            try
            {
                original = ch.m_damageModifiers;
                var mods = ch.m_damageModifiers;
                var immune = HitData.DamageModifier.Immune;
                mods.m_blunt = immune; mods.m_slash = immune; mods.m_pierce = immune;
                mods.m_chop = immune; mods.m_pickaxe = immune; mods.m_fire = immune;
                mods.m_frost = immune; mods.m_lightning = immune; mods.m_poison = immune;
                mods.m_spirit = immune;
                ch.m_damageModifiers = mods;
            }
            catch { }
            return original;
        }

        /// <summary>Gives the body back the damage modifiers it was made with - killable again.</summary>
        protected void Unshield(Character body)
        {
            if (body == null) return;
            try { body.m_damageModifiers = _originalModifiers; } catch { }
        }

        /// <summary>The standing body's Character, or null.</summary>
        protected Character BodyCharacter => _body != null ? _body.GetComponent<Character>() : null;

        /// <summary>
        /// True when <paramref name="c"/> IS this speaker's current body - by ZDO id, never by species,
        /// so another creature of the same prefab can never stand in for them.
        /// </summary>
        public bool IsBody(Character c)
        {
            if (c == null || _body == null) return false;
            try
            {
                var mine = _body.GetComponent<ZNetView>();
                var theirs = c.GetComponent<ZNetView>();
                if (mine == null || theirs == null || !mine.IsValid() || !theirs.IsValid()) return ReferenceEquals(c.gameObject, _body);
                return mine.GetZDO().m_uid == theirs.GetZDO().m_uid;
            }
            catch { return ReferenceEquals(c.gameObject, _body); }
        }

        /// <summary>Sends them away. The spot is kept until <see cref="Reset"/>.</summary>
        public void Dismiss()
        {
            try
            {
                if (_body != null) UnityEngine.Object.Destroy(_body);
            }
            catch { }

            _body = null;
            _greeted = null;
        }
    }
}
