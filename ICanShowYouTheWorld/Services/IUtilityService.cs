using System;
using UnityEngine;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Categories for utility commands.
    /// </summary>
    public enum UtilityCategory
    {
        Combat,      // Ghost mode, panic jump
        Repair,      // Repair structures, replenish items
        Crafting,    // Fuel, smelt, set crafter
        Exploration, // Reveal locations, explore map
        Skills,      // Increase skills, toggle powers
        Misc         // Other utilities
    }

    /// <summary>
    /// Represents a single utility command.
    /// </summary>
    public class Utility
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public UtilityCategory Category { get; set; }
        public Action Action { get; set; }
        public Color CategoryColor { get; set; }

        public string CategoryTag => $"[{Category}]";
    }

    /// <summary>
    /// Service for managing utility commands (cycling, execution, categorization).
    /// </summary>
    public interface IUtilityService
    {
        // ===== Current Selection =====

        /// <summary>
        /// Currently selected utility.
        /// </summary>
        Utility CurrentUtility { get; }

        /// <summary>
        /// Index of current utility (0-based).
        /// </summary>
        int CurrentIndex { get; }

        /// <summary>
        /// Total number of utilities available.
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// Current utility name.
        /// </summary>
        string CurrentUtilityName { get; }

        // ===== Context Window (for UI display) =====

        /// <summary>
        /// Get utility at offset from current (e.g., -2, -1, 0, +1, +2).
        /// Returns null if offset is out of bounds.
        /// </summary>
        /// <param name="offset">Offset from current index.</param>
        Utility GetUtilityAtOffset(int offset);

        /// <summary>
        /// Get utility name at offset from current.
        /// Returns empty string if out of bounds.
        /// </summary>
        string GetUtilityNameAtOffset(int offset);

        // ===== Cycling =====

        /// <summary>
        /// Cycle to next utility (wraps around).
        /// </summary>
        void CycleNext();

        /// <summary>
        /// Cycle to previous utility (wraps around).
        /// </summary>
        void CyclePrevious();

        /// <summary>
        /// Jump to specific utility by index.
        /// </summary>
        /// <param name="index">Target index (0-based).</param>
        void JumpToIndex(int index);

        /// <summary>
        /// Jump to first utility in a specific category.
        /// </summary>
        /// <param name="category">Target category.</param>
        void JumpToCategory(UtilityCategory category);

        // ===== Execution =====

        /// <summary>
        /// Execute the currently selected utility.
        /// </summary>
        void ExecuteCurrent();

        // ===== Category Info =====

        /// <summary>
        /// Get color for a specific category.
        /// </summary>
        Color GetCategoryColor(UtilityCategory category);

        /// <summary>
        /// Get count of utilities in a specific category.
        /// </summary>
        int GetCategoryCount(UtilityCategory category);
    }
}
