using System;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Raids as story beats: the game's own random events, forced by name at a place and moment
    /// the saga chooses.
    ///
    /// A forced event is the complete vanilla package — the red event bar with its message, the
    /// event music, the spawn waves for the event's duration, the end message — for one public
    /// call, RandEventSystem.SetRandomEventByName. Forcing skips the event's own requirements
    /// (global keys, known items), which is the point: "Eikthyr rallies the creatures of the
    /// forest" is a raid the game holds back until Eikthyr is dead, and the saga wants it the
    /// moment his Herald falls, because the bible says its fall will be heard.
    ///
    /// Event NAMES are asset data. They are read from the live event list at run start and
    /// logged ("Raid registry"), and a beat whose event the game does not have is skipped with
    /// one log line rather than guessed at.
    ///
    /// One raid at a time, by the game's own design; a beat that lands during another event
    /// replaces it. A run ending mid-raid resets the event, so the lobby is quiet.
    /// </summary>
    internal sealed class SagaRaids
    {
        /// <summary>Vanilla event names, as commonly documented. Verified against the live list before use.</summary>
        public const string EikthyrRally = "army_eikthyr";   // boars and necks: "Eikthyr rallies the creatures of the forest"
        public const string ForestMoving = "army_theelder";  // greydwarves: "The forest is moving..."

        private string _ours;
        private bool _logged;

        /// <summary>Logs every event the game has, once per run start. Diagnostics only.</summary>
        public void LogRegistry()
        {
            if (_logged) return;
            try
            {
                var sys = RandEventSystem.instance;
                if (sys == null || sys.m_events == null) return;

                _logged = true;
                foreach (var e in sys.m_events)
                {
                    if (e == null) continue;
                    Debug.Log($"[ICanShowYouTheWorld] Raid registry: '{e.m_name}' biome={e.m_biome} duration={e.m_duration:0}s " +
                              $"random={e.m_random} nearBaseOnly={e.m_nearBaseOnly} " +
                              $"requires=[{string.Join(",", (e.m_requiredGlobalKeys ?? new System.Collections.Generic.List<string>()).ToArray())}] " +
                              $"start=\"{e.m_startMessage}\"");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Raid registry could not be read: " + ex.Message);
            }
        }

        public bool Has(string name)
        {
            try
            {
                var sys = RandEventSystem.instance;
                return sys != null && sys.m_events != null && sys.m_events.Any(e => e != null && e.m_name == name);
            }
            catch { return false; }
        }

        /// <summary>Forces the named event at a position. False, with a log line, when the game lacks it.</summary>
        public bool Trigger(string name, Vector3 at)
        {
            try
            {
                var sys = RandEventSystem.instance;
                if (sys == null) return false;

                if (!Has(name))
                {
                    Debug.LogError($"[ICanShowYouTheWorld] Raid '{name}' is not in this build's event list — beat skipped. See 'Raid registry'.");
                    return false;
                }

                sys.SetRandomEventByName(name, at);
                _ours = name;
                Debug.Log($"[ICanShowYouTheWorld] Raid forced: {name} at {at}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Raid '{name}' could not be forced: {ex.Message}");
                return false;
            }
        }

        /// <summary>Ends a raid the saga started, if it is still the active one. Run end.</summary>
        public void Reset()
        {
            _logged = false;
            if (_ours == null) return;

            try
            {
                var sys = RandEventSystem.instance;
                var active = sys != null ? sys.GetCurrentRandomEvent() : null;
                if (active != null && active.m_name == _ours) sys.ResetRandomEvent();
            }
            catch { }

            _ours = null;
        }
    }
}
