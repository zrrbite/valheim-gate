using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// "Eat from the field": a full table with at least one food the plains grew. Pure, so the
    /// harvester's ask and the steading's feast agree on what a meal from the field IS - and so the
    /// harness can say so.
    /// </summary>
    /// <remarks>
    /// Prefab names, as <c>Player.Food.m_name</c> carries them. Barley bread and lox pie are made
    /// from what only the plains grow and graze; fish wraps need barley flour too.
    /// </remarks>
    public static class PlainsMeal
    {
        public static readonly string[] PlainsFoods = { "Bread", "LoxPie", "FishWraps" };

        public static bool IsPlainsFood(string prefab) =>
            !string.IsNullOrEmpty(prefab) && PlainsFoods.Contains(prefab);

        /// <summary>Three foods eaten, at least one of them from the field.</summary>
        public static bool FullPlainsTable(IEnumerable<string> foodPrefabs)
        {
            var foods = (foodPrefabs ?? Enumerable.Empty<string>()).Where(f => !string.IsNullOrEmpty(f)).ToList();
            return foods.Count >= 3 && foods.Any(IsPlainsFood);
        }
    }
}
