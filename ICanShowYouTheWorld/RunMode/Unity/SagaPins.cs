using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The saga's people on the map: the thane, Thjalfi and the hunter's shade, each a pin while
    /// the questline wants them and their spot is known.
    /// </summary>
    /// <remarks>
    /// The pin says WHERE; the strip still says WHEN. The gates (the thane by day, Thjalfi in the
    /// rain, the shade after dark) are untouched, so a pin can stand over a place where nobody is
    /// yet - which is the honest reading, since the spot is chosen and kept and the person comes
    /// back to it.
    ///
    /// Never saved (<c>save: false</c>). A saved pin would outlive the run in the character's map
    /// data, where a run that ended has no business leaving a mark, and the spots are re-chosen
    /// each session anyway (they live in memory on the actors). So the pins are re-created from the
    /// actors on every poll that finds them missing, and that is the whole persistence story.
    ///
    /// Why "missing" has to be checked rather than remembered: <c>Minimap.m_pins</c> is STATIC in
    /// the patched assembly and <c>LoadMapData</c> clears it on every world load, so a pin this
    /// class added can vanish underneath it. A pin is trusted only while the list still holds it.
    /// </remarks>
    internal static class SagaPins
    {
        /// <summary>
        /// The plain round marker. Icon0-2 are the fire, the house and the hammer, which read as
        /// things the player built; Icon4 is the portal rune. A dot says "somebody is here" and
        /// nothing else, which is all these pins mean.
        /// </summary>
        private const Minimap.PinType PinType = Minimap.PinType.Icon3;

        private sealed class Record
        {
            public Minimap.PinData Pin;
            public Vector3 Pos;
            public string Name;
        }

        private static readonly Dictionary<string, Record> Shown = new Dictionary<string, Record>();

        /// <summary>Ids whose first appearance has been logged, so a re-added pin is not news.</summary>
        private static readonly HashSet<string> Announced = new HashSet<string>();

        /// <summary>
        /// Puts the pin for <paramref name="id"/> at <paramref name="pos"/>, or leaves it where it
        /// is when it already stands there. Cheap enough for the 1 Hz poll.
        /// </summary>
        public static void Show(string id, Vector3 pos, string name)
        {
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                var map = Minimap.instance;
                if (map == null) return;

                if (Shown.TryGetValue(id, out var have))
                {
                    bool stillThere = have.Pin != null && Minimap.m_pins != null && Minimap.m_pins.Contains(have.Pin);
                    if (stillThere && have.Name == name && (have.Pos - pos).sqrMagnitude < 1f) return;

                    if (stillThere) map.RemovePin(have.Pin);
                    Shown.Remove(id);
                }

                var pin = map.AddPin(pos, PinType, name, save: false, isChecked: false, ownerID: 0L);
                Shown[id] = new Record { Pin = pin, Pos = pos, Name = name };

                if (Announced.Add(id))
                    Debug.Log($"[ICanShowYouTheWorld] Map pin shown: {name} at {pos:0.0}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Map pin '{id}' could not be shown: {ex.Message}");
            }
        }

        /// <summary>Takes the pin for <paramref name="id"/> off the map. Safe when there is none.</summary>
        public static void Hide(string id)
        {
            if (string.IsNullOrEmpty(id) || !Shown.TryGetValue(id, out var have)) return;
            Shown.Remove(id);

            try
            {
                if (have.Pin == null || Minimap.m_pins == null || !Minimap.m_pins.Contains(have.Pin)) return;

                // Through the instance while there is one, because RemovePin also destroys the
                // marker it drew. With no minimap there is no marker, only the static list's entry.
                var map = Minimap.instance;
                if (map != null) map.RemovePin(have.Pin);
                else Minimap.m_pins.Remove(have.Pin);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Map pin '{id}' could not be removed: {ex.Message}");
            }
        }

        /// <summary>
        /// Every saga pin off the map, and the first-shown log re-armed. Run start and run end: the
        /// next run's people choose new spots, and seeing them appear is worth a line again.
        /// </summary>
        public static void HideAll()
        {
            foreach (var id in new List<string>(Shown.Keys)) Hide(id);
            Announced.Clear();
        }
    }
}
