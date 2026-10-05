using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The alt-use half of a <see cref="TraderVoice"/>: a child trigger on the game's own trader that
    /// answers alt-Use itself and hands everything else to the trader, so her shop is untouched.
    /// </summary>
    /// <remarks>
    /// The game finds hover text and interacts with GetComponentInParent from the collider the
    /// crosshair hit. A trigger on a child, sized to the trader's chest, is found before the trader's
    /// own component - which is why everything that is NOT the alt action is passed straight through.
    /// </remarks>
    internal sealed class TraderAltTalk : MonoBehaviour, Interactable, Hoverable
    {
        public TraderVoice Owner;
        public Trader Trader;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (alt && !hold && Owner != null && Owner.AltAction != null)
            {
                try { return Owner.AltAction(user); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ICanShowYouTheWorld] {Owner.Label}'s alt action failed: {ex.Message}");
                    return false;
                }
            }
            return Trader != null && Trader.Interact(user, hold, alt);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => Trader != null && Trader.UseItem(user, item);

        public string GetHoverText()
        {
            string text = Trader != null ? Trader.GetHoverText() : string.Empty;
            if (Owner != null && !string.IsNullOrEmpty(Owner.AltHover))
            {
                string alt = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace";
                string progress = Owner.AltProgress != null ? Owner.AltProgress(Player.m_localPlayer) : string.Empty;
                text += $"\n[<color=yellow><b>{alt} + $KEY_Use</b></color>] {Owner.AltHover}{progress}";
            }
            try { return Localization.instance != null ? Localization.instance.Localize(text) : text; }
            catch { return text; }
        }

        public string GetHoverName() => Trader != null ? Trader.GetHoverName() : string.Empty;

        public float GetHoverOffset() => Trader != null ? Trader.GetHoverOffset() : 0f;
    }

    /// <summary>
    /// One of the game's own traders, given a saga voice for an act and returned to themselves after.
    /// Haldor (Act II) and the Bog Witch (Act III) are configurations of this.
    /// </summary>
    /// <remarks>
    /// Everything optional, everything restored:
    ///   - TALK: <c>m_randomTalk</c> swapped (greet, goodbye, buy and sell stay vanilla), with an
    ///     after-variant once the voice's errand is answered.
    ///   - A GIVE entry appended to <c>m_useItems</c> while the host says the ask is live - the game's
    ///     own give-item mechanism (Trader.UseItem: match on shared name, say m_dialog, set a global
    ///     key, remove the item). The key is the signal; global keys persist with the world, so the
    ///     host scopes it to the run and clears it.
    ///   - NEAR: whether the player is within <see cref="MetRange"/>, for a "find them" step - a
    ///     trader has no speech of her own to hang one on.
    ///   - An ALT action through <see cref="TraderAltTalk"/>, for work done at her hands.
    /// A destroyed trader compares equal to null, which is exactly "unloaded": a fresh instance from
    /// the prefab is vanilla already, so the references simply go.
    /// </remarks>
    internal sealed class TraderVoice
    {
        private const float AttachRange = 80f;

        public string Label;
        private readonly string _match;
        private string[] _talk;
        private string[] _talkAfter;

        /// <summary>Optional give entry: the item prefab and the line said on acceptance.</summary>
        public string GivePrefab;
        public string GiveDialog;

        /// <summary>Optional: within this many metres counts as met. 0 disables.</summary>
        public float MetRange;

        /// <summary>Optional alt action at the trader's hands, with its hover line and progress.</summary>
        public string AltHover;
        public Func<Humanoid, bool> AltAction;
        public Func<Player, string> AltProgress;

        private Trader _trader;
        private List<string> _savedTalk;
        private Trader.TraderUseItem _added;
        private GameObject _altObject;
        private bool _afterTalk;
        private bool _giveMissing;
        private bool _loggedNames;
        private bool _loggedNoMatch;

        public TraderVoice(string label, string match, string[] talk, string[] talkAfter)
        {
            Label = label;
            _match = match;
            _talk = talk ?? new string[0];
            _talkAfter = talkAfter ?? _talk;
        }

        public bool Attached => _trader != null;

        /// <summary>
        /// Changes what the voice says, for a trader who speaks in more than one act (Hildir: the
        /// cold in Act IV, the harvest in Act V). Applied on the next tick if attached.
        /// </summary>
        public void SetTalk(string[] talk, string[] talkAfter)
        {
            _talk = talk ?? new string[0];
            _talkAfter = talkAfter ?? _talk;
            _afterTalk = !_afterTalk; // forces the next SyncTalk to rewrite
        }

        /// <summary>True when attached and the player stands within <see cref="MetRange"/>.</summary>
        public bool Near { get; private set; }

        /// <summary>The trader's own give entries, for reading a vanilla quest's keys (Hildir's chests).</summary>
        public IReadOnlyList<Trader.TraderUseItem> VanillaGives =>
            _trader != null && _trader.m_useItems != null
                ? _trader.m_useItems.Where(u => !ReferenceEquals(u, _added)).ToList()
                : new List<Trader.TraderUseItem>();

        /// <summary>Call about once a second.</summary>
        /// <param name="wanted">The voice's act is current in a live run.</param>
        /// <param name="answered">The errand is answered: switch to the after-talk.</param>
        /// <param name="giveKey">The run-scoped key the give entry sets; null for no give entry.</param>
        /// <param name="giveLive">The give step is current - the entry exists only then.</param>
        public void Tick(Player player, bool wanted, bool answered, string giveKey, bool giveLive)
        {
            if (_trader == null && (_savedTalk != null || _added != null || _altObject != null))
            {
                _savedTalk = null;
                _added = null;
                _altObject = null;
            }

            Near = false;

            if (!wanted || player == null)
            {
                Detach();
                return;
            }

            if (_trader == null)
            {
                var trader = Find(player.transform.position, giveLive);
                if (trader == null) return;
                Attach(trader);
                if (_trader == null) return;
            }

            SyncTalk(answered);
            SyncGive(giveLive, giveKey);

            if (MetRange > 0f)
                Near = Vector3.Distance(player.transform.position, _trader.transform.position) <= MetRange;
        }

        /// <summary>A bubble over the trader's head, the way their own lines appear.</summary>
        public void SayBubble(string text)
        {
            if (_trader == null || string.IsNullOrEmpty(text)) return;
            try
            {
                SagaTranscript.Record(Label, text);
                var chat = Chat.instance;
                if (chat != null)
                    chat.SetNpcText(_trader.gameObject, Vector3.up * _trader.m_dialogHeight, 20f,
                                    Mathf.Max(_trader.m_hideDialogDelay, 8f), string.Empty, text, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] {Label} could not speak: {ex.Message}");
            }
        }

        private bool IsMatch(Trader t) =>
            (t.m_name != null && t.m_name.IndexOf(_match, StringComparison.OrdinalIgnoreCase) >= 0) ||
            (t.gameObject != null && t.gameObject.name.IndexOf(_match, StringComparison.OrdinalIgnoreCase) >= 0);

        private Trader Find(Vector3 near, bool giveLive)
        {
            Trader best = null;
            float bestDistance = AttachRange;
            int inRange = 0;

            foreach (var t in UnityEngine.Object.FindObjectsOfType<Trader>())
            {
                if (t == null) continue;
                float d = Vector3.Distance(near, t.transform.position);
                if (d > AttachRange) continue;
                inRange++;

                if (!_loggedNames)
                    Debug.Log($"[ICanShowYouTheWorld] Trader in range: m_name '{t.m_name}', object '{t.gameObject.name}' at {t.transform.position:0}.");

                if (!IsMatch(t)) continue;
                if (d <= bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }

            if (inRange > 0) _loggedNames = true;

            if (best == null && inRange > 0 && giveLive && !_loggedNoMatch)
            {
                _loggedNoMatch = true;
                Debug.LogError($"[ICanShowYouTheWorld] {Label}'s ask is live and a trader is in range, but none matched " +
                               $"'{_match}' - the ask cannot complete. See the 'Trader in range' lines.");
            }

            return best;
        }

        private void Attach(Trader trader)
        {
            try
            {
                _savedTalk = trader.m_randomTalk != null ? new List<string>(trader.m_randomTalk) : new List<string>();
                _trader = trader;
                _afterTalk = false;
                _added = null;

                if (AltAction != null) AddAltObject(trader);

                if (trader.m_useItems != null)
                    foreach (var u in trader.m_useItems)
                        Debug.Log($"[ICanShowYouTheWorld] {Label} accepts '{(u?.m_prefab != null ? u.m_prefab.name : "?")}' " +
                                  $"(sets key '{u?.m_setsGlobalKey}', removes {u?.m_removesItem}).");

                Debug.Log($"[ICanShowYouTheWorld] {Label}'s voice attached ('{trader.m_name}', object '{trader.gameObject.name}').");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] {Label}'s voice could not attach: {ex.Message}");
            }
        }

        private void AddAltObject(Trader trader)
        {
            var go = new GameObject("saga_trader_alt");
            go.transform.SetParent(trader.transform, false);
            go.transform.localPosition = Vector3.up * 1.2f;

            var own = trader.GetComponentInChildren<Collider>();
            go.layer = own != null ? own.gameObject.layer : trader.gameObject.layer;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.0f;

            var talk = go.AddComponent<TraderAltTalk>();
            talk.Owner = this;
            talk.Trader = trader;
            _altObject = go;

            Debug.Log($"[ICanShowYouTheWorld] {Label}'s alt-use attached on layer {LayerMask.LayerToName(go.layer)}.");
        }

        private void SyncTalk(bool answered)
        {
            var want = answered ? _talkAfter : _talk;
            if (_afterTalk == answered && _trader.m_randomTalk != null && _trader.m_randomTalk.Count == want.Length &&
                _trader.m_randomTalk.Count > 0 && _trader.m_randomTalk[0] == want[0]) return;

            _trader.m_randomTalk = new List<string>(want);
            _afterTalk = answered;
        }

        private void SyncGive(bool giveLive, string key)
        {
            if (string.IsNullOrEmpty(GivePrefab) || string.IsNullOrEmpty(key)) return;
            if (_trader.m_useItems == null) _trader.m_useItems = new List<Trader.TraderUseItem>();

            if (!giveLive)
            {
                if (_added != null) _trader.m_useItems.Remove(_added);
                _added = null;
                return;
            }

            if (_added != null || _giveMissing) return;

            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(GivePrefab) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                _giveMissing = true;
                Debug.LogError($"[ICanShowYouTheWorld] {Label}'s voice: no '{GivePrefab}' item - the ask cannot work.");
                return;
            }

            _added = new Trader.TraderUseItem
            {
                m_prefab = drop,
                m_setsGlobalKey = key,
                m_dialog = GiveDialog,
                m_removesItem = true,
            };
            _trader.m_useItems.Add(_added);
            Debug.Log($"[ICanShowYouTheWorld] {Label} will take '{GivePrefab}' now (key '{key}').");
        }

        /// <summary>Restores the trader's own talk, removes the give entry and the alt object.</summary>
        public void Detach()
        {
            try
            {
                if (_trader != null)
                {
                    if (_savedTalk != null) _trader.m_randomTalk = _savedTalk;
                    if (_added != null && _trader.m_useItems != null) _trader.m_useItems.Remove(_added);
                    if (_altObject != null) UnityEngine.Object.Destroy(_altObject);
                    Debug.Log($"[ICanShowYouTheWorld] {Label}'s voice detached; their own talk restored.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] {Label}'s voice could not detach cleanly: {ex.Message}");
            }

            _trader = null;
            _savedTalk = null;
            _added = null;
            _altObject = null;
            Near = false;
        }

        /// <summary>True once <paramref name="key"/> is set in the world.</summary>
        public static bool KeySet(string key)
        {
            try
            {
                var zone = ZoneSystem.instance;
                return zone != null && !string.IsNullOrEmpty(key) && zone.GetGlobalKey(key);
            }
            catch { return false; }
        }

        /// <summary>Removes every world key the predicate claims. Run start and run end.</summary>
        public static void ClearKeys(Func<string, bool> ours)
        {
            try
            {
                var zone = ZoneSystem.instance;
                if (zone == null || ours == null) return;

                foreach (var k in zone.GetGlobalKeys().Where(ours).ToList())
                {
                    zone.RemoveGlobalKey(k);
                    Debug.Log($"[ICanShowYouTheWorld] Cleared the saga's key '{k}' from the world.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The saga's trader keys could not be cleared: " + ex.Message);
            }
        }
    }
}
