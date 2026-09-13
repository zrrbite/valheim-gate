using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The saga's dreams: what the player sees on waking, while a run is live.
    ///
    /// Valheim shows a line of text when you wake, drawn from a list on the sleep screen's
    /// SleepText component — each entry with a chance and a set of global keys that must be true
    /// or false. Nothing in the game explains the system; most players see two or three dreams in
    /// a hundred hours. It is exactly the surface the story bible wants: told where the player is
    /// looking, in one line, by the world.
    ///
    /// While a run is live the list is the SAGA'S. The vanilla entries are set aside and put back
    /// when the run ends, so that the first night of a saga dreams the saga's first dream rather
    /// than rolling against five vanilla lines (owner, 2026-09-13: "Would be fun to dream on the
    /// first night"). Chance 1.0 for the act's first dream; the rest roll so sleep stays quiet
    /// sometimes, which is what makes a dream a dream.
    ///
    /// Keyed on the same defeat keys the acts are — a dream belongs to an act, and the world says
    /// which act it is. The sleep screen is a scene object, found by a search that includes
    /// inactive objects because it is inactive whenever the player is awake.
    /// </summary>
    internal sealed class SagaDreams
    {
        /// <summary>Text, chance, keys that must be true, keys that must be false.</summary>
        private static readonly (string text, float chance, string[] trueKeys, string[] falseKeys)[] Dreams =
        {
            // Act I — before Eikthyr.
            ("You dream of the herd. Every antler carries a spark, and every spark is counted by something in the trees.",
             1.0f, new string[0], new[] { "defeated_eikthyr" }),
            ("You dream you are a deer, and the dark is full of small hands.",
             0.6f, new string[0], new[] { "defeated_eikthyr" }),
            // Act II — the Elder.
            ("You dream of roots, and of light moving down them like sap, slow, toward something old.",
             0.8f, new[] { "defeated_eikthyr" }, new[] { "defeated_gdking" }),
            // Act III — Bonemass.
            ("You dream of the marsh. Nothing in it is asleep. Nothing in it has ever let go.",
             0.8f, new[] { "defeated_gdking" }, new[] { "defeated_bonemass" }),
            // Act IV — Moder.
            ("You dream of eggs under snow, and of a warmth that has not woken yet.",
             0.8f, new[] { "defeated_bonemass" }, new[] { "defeated_dragon" }),
            // Act V — Yagluth.
            ("You dream of a harvest. The fields are gold and the stores are full and every door is open, and no one is there.",
             0.8f, new[] { "defeated_dragon" }, new[] { "defeated_goblinking" }),
            // After.
            ("You dream of a lantern. Somebody borrowed the light in it, and means to give it back.",
             0.5f, new[] { "defeated_goblinking" }, new string[0]),
        };

        private SleepText _owner;
        private List<DreamTexts.DreamText> _vanilla;
        private bool _reported;

        /// <summary>Puts the saga's dreams in place. Cheap when already done; re-finds the screen after a scene change.</summary>
        public void Ensure()
        {
            try
            {
                if (_owner != null && _owner.m_dreamTexts != null && _owner.m_dreamTexts.m_texts != null &&
                    _owner.m_dreamTexts.m_texts.Count == Dreams.Length) return;

                var sleep = Resources.FindObjectsOfTypeAll<SleepText>().FirstOrDefault(s => s != null && s.m_dreamTexts != null);
                if (sleep == null || sleep.m_dreamTexts.m_texts == null)
                {
                    if (!_reported)
                    {
                        _reported = true;
                        Debug.Log("[ICanShowYouTheWorld] Saga dreams: no sleep screen found yet; will retry.");
                    }
                    return;
                }

                // A new owner (scene reload) means the old vanilla list is gone with it.
                if (!ReferenceEquals(sleep, _owner)) _vanilla = null;

                _owner = sleep;
                if (_vanilla == null) _vanilla = new List<DreamTexts.DreamText>(sleep.m_dreamTexts.m_texts);

                sleep.m_dreamTexts.m_texts.Clear();
                foreach (var d in Dreams)
                {
                    sleep.m_dreamTexts.m_texts.Add(new DreamTexts.DreamText
                    {
                        m_text = d.text,
                        m_chanceToDream = d.chance,
                        m_trueKeys = new List<string>(d.trueKeys),
                        m_falseKeys = new List<string>(d.falseKeys),
                    });
                }

                Debug.Log($"[ICanShowYouTheWorld] Saga dreams in place: {Dreams.Length} (vanilla set aside: {_vanilla.Count}).");
            }
            catch (Exception ex)
            {
                if (!_reported)
                {
                    _reported = true;
                    Debug.LogWarning("[ICanShowYouTheWorld] Saga dreams could not be placed: " + ex.Message);
                }
            }
        }

        /// <summary>Gives the game its own dreams back. Run end.</summary>
        public void Remove()
        {
            try
            {
                if (_owner != null && _owner.m_dreamTexts != null && _owner.m_dreamTexts.m_texts != null && _vanilla != null)
                {
                    _owner.m_dreamTexts.m_texts.Clear();
                    _owner.m_dreamTexts.m_texts.AddRange(_vanilla);
                }
            }
            catch { }

            _owner = null;
            _vanilla = null;
            _reported = false;
        }
    }
}
