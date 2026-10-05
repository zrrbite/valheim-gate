using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Haldor, given a voice for Act II of a run and returned to himself afterwards. Not a speaker:
    /// the game already places him, animates him and gives him a talk system. This only changes
    /// what he says and adds one thing he will accept.
    /// </summary>
    /// <remarks>
    /// WHAT HE KNOWS. His camp sits on the couriers' road. They pass every night, laden, and never
    /// stop - splinters carry light and do not take goods, which is why a trader in a starving
    /// forest is never robbed. The one thing he fears is a troll: in the saga, the only thing that
    /// BREAKS light instead of carrying it. Bring him a troll's head and he tells you where the
    /// couriers go, which is what puts the Elder's altar on the map.
    ///
    /// HOW, from the IL of Trader.UseItem (1.0.16). <c>m_useItems</c> is a list of
    /// <c>TraderUseItem { m_prefab, m_setsGlobalKey, m_dialog, m_removesItem }</c> matched on the
    /// item's shared name. On a match he says <c>m_dialog</c>, sets the key, and removes one item. If
    /// the key is already set he says <c>m_randomUseItemAlreadyRecieved</c> instead. The key is the
    /// signal the host polls - and since global keys are SAVED WITH THE WORLD, it names the run
    /// (<see cref="SagaNames.HaldorKey"/>) and is cleared at run start and end.
    ///
    /// Only <c>m_randomTalk</c> is replaced. Greet, goodbye, buy and sell stay vanilla: the story
    /// needs his idle talk, not a stranger at the counter.
    /// </remarks>
    internal sealed class HaldorVoice
    {
        /// <summary>The troll's trophy, by prefab. Its drop chance is logged at run start.</summary>
        public const string TrophyPrefab = "TrophyForestTroll";

        /// <summary>How near Haldor must be before the voice attaches. He is created with his camp's zone.</summary>
        private const float AttachRange = 80f;

        public static readonly string[] Talk =
        {
            "They come past every night. Little ones, glowing like lanterns. Never once stop to trade.",
            "Funny thing. They carry more than they could ever eat, and they never look at my wares.",
            "I keep my lamps low after dark. Not for the little ones. For the big ones.",
            "Want to know where the light goes? I've watched them for years. It'll cost you a troll's head.",
        };

        /// <summary>
        /// Said through his own accept line. "I've marked it" is backed: the altar step becomes
        /// current the moment the key is seen, and the pin follows on the same poll.
        /// </summary>
        public const string RevealLine =
            "Ha! That one won't be knocking my camp over. Fair's fair, then. Every night they go the same way — " +
            "deeper, to where the trees are oldest. There's a ring of stone in there, and something under it that's " +
            "never once come up to collect. I've marked it on your map.";

        private Trader _trader;
        private List<string> _savedTalk;
        private Trader.TraderUseItem _added;
        private bool _loggedName;

        public bool Attached => _trader != null;

        /// <summary>Call about once a second.</summary>
        /// <param name="wanted">Act II of a live run.</param>
        /// <param name="key">The run's key; see <see cref="SagaNames.HaldorKey"/>.</param>
        public void Tick(Player player, bool wanted, string key)
        {
            // A destroyed Trader compares equal to null, which is exactly "he unloaded": nothing of
            // his is left to restore, so the references simply go.
            if (_trader == null && (_savedTalk != null || _added != null))
            {
                _savedTalk = null;
                _added = null;
            }

            if (!wanted || player == null)
            {
                Detach();
                return;
            }

            if (_trader != null) return;

            var trader = FindHaldor(player.transform.position);
            if (trader != null) Attach(trader, key);
        }

        private Trader FindHaldor(Vector3 near)
        {
            Trader best = null;
            float bestDistance = AttachRange;

            foreach (var t in UnityEngine.Object.FindObjectsOfType<Trader>())
            {
                if (t == null || string.IsNullOrEmpty(t.m_name)) continue;

                if (!_loggedName)
                {
                    Debug.Log($"[ICanShowYouTheWorld] Trader in range: m_name '{t.m_name}' at {t.transform.position:0}.");
                }

                if (t.m_name.IndexOf("haldor", StringComparison.OrdinalIgnoreCase) < 0) continue;

                float d = Vector3.Distance(near, t.transform.position);
                if (d <= bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }

            return best;
        }

        private void Attach(Trader trader, string key)
        {
            try
            {
                var trophy = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(TrophyPrefab) : null;
                var drop = trophy != null ? trophy.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] Haldor's voice: no '{TrophyPrefab}' item - his ask cannot work.");
                    return;
                }

                _savedTalk = trader.m_randomTalk != null ? new List<string>(trader.m_randomTalk) : new List<string>();
                trader.m_randomTalk = new List<string>(Talk);

                if (trader.m_useItems == null) trader.m_useItems = new List<Trader.TraderUseItem>();
                _added = new Trader.TraderUseItem
                {
                    m_prefab = drop,
                    m_setsGlobalKey = key,
                    m_dialog = RevealLine,
                    m_removesItem = true,
                };
                trader.m_useItems.Add(_added);

                _trader = trader;
                _loggedName = true;
                Debug.Log($"[ICanShowYouTheWorld] Haldor's voice attached ('{trader.m_name}', key '{key}').");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Haldor's voice could not attach: " + ex.Message);
            }
        }

        /// <summary>Gives Haldor his own talk back and takes the troll's head off his list.</summary>
        public void Detach()
        {
            try
            {
                if (_trader != null)
                {
                    if (_savedTalk != null) _trader.m_randomTalk = _savedTalk;
                    if (_added != null && _trader.m_useItems != null) _trader.m_useItems.Remove(_added);
                    Debug.Log("[ICanShowYouTheWorld] Haldor's voice detached; his own talk restored.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Haldor's voice could not detach cleanly: " + ex.Message);
            }

            _trader = null;
            _savedTalk = null;
            _added = null;
        }

        /// <summary>True once the run's key is set: he has taken the head and told.</summary>
        public static bool Told(string key)
        {
            try
            {
                var zone = ZoneSystem.instance;
                return zone != null && !string.IsNullOrEmpty(key) && zone.GetGlobalKey(key);
            }
            catch { return false; }
        }

        /// <summary>Removes every saga Haldor key from the world. Run start and run end.</summary>
        public static void ClearKeys()
        {
            try
            {
                var zone = ZoneSystem.instance;
                if (zone == null) return;

                foreach (var k in zone.GetGlobalKeys().Where(SagaNames.IsHaldorKey).ToList())
                {
                    zone.RemoveGlobalKey(k);
                    Debug.Log($"[ICanShowYouTheWorld] Cleared Haldor's key '{k}' from the world.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Haldor's keys could not be cleared: " + ex.Message);
            }
        }
    }
}
