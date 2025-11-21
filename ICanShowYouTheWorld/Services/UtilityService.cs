using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of utility service.
    /// Manages utility command cycling, execution, and categorization.
    /// </summary>
    public class UtilityService : IUtilityService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;
        private readonly List<Utility> _utilities;
        private int _currentIndex;

        // Category colors
        private readonly Dictionary<UtilityCategory, Color> _categoryColors = new Dictionary<UtilityCategory, Color>
        {
            { UtilityCategory.Combat, new Color(1f, 0.3f, 0.3f) },      // Red
            { UtilityCategory.Repair, new Color(0.3f, 0.6f, 1f) },      // Blue
            { UtilityCategory.Crafting, new Color(1f, 0.9f, 0.3f) },    // Yellow
            { UtilityCategory.Exploration, new Color(0.3f, 1f, 0.5f) }, // Green
            { UtilityCategory.Skills, new Color(0.8f, 0.3f, 1f) },      // Purple
            { UtilityCategory.Misc, new Color(0.7f, 0.7f, 0.7f) }       // Gray
        };

        public UtilityService(IGameAPI game, IConfiguration config)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Initialize utilities list with categories
            _utilities = CreateUtilitiesList();
            _currentIndex = 1; // Start at Ghost Mode
        }

        // ===== Current Selection =====

        public Utility CurrentUtility => _utilities[_currentIndex];
        public int CurrentIndex => _currentIndex;
        public int TotalCount => _utilities.Count;
        public string CurrentUtilityName => CurrentUtility.Name;

        // ===== Context Window =====

        public Utility GetUtilityAtOffset(int offset)
        {
            int targetIndex = (_currentIndex + offset + _utilities.Count) % _utilities.Count;
            return targetIndex >= 0 && targetIndex < _utilities.Count ? _utilities[targetIndex] : null;
        }

        public string GetUtilityNameAtOffset(int offset)
        {
            var utility = GetUtilityAtOffset(offset);
            return utility?.Name ?? "";
        }

        // ===== Cycling =====

        public void CycleNext()
        {
            _currentIndex = (_currentIndex + 1) % _utilities.Count;
            _game.ShowMessage($"Selected: {CurrentUtility.CategoryTag} {CurrentUtilityName}", MessageType.TopLeft);
        }

        public void CyclePrevious()
        {
            _currentIndex = (_currentIndex - 1 + _utilities.Count) % _utilities.Count;
            _game.ShowMessage($"Selected: {CurrentUtility.CategoryTag} {CurrentUtilityName}", MessageType.TopLeft);
        }

        public void JumpToIndex(int index)
        {
            if (index >= 0 && index < _utilities.Count)
            {
                _currentIndex = index;
                _game.ShowMessage($"Jumped to: {CurrentUtility.CategoryTag} {CurrentUtilityName}", MessageType.TopLeft);
            }
        }

        public void JumpToCategory(UtilityCategory category)
        {
            int index = _utilities.FindIndex(u => u.Category == category);
            if (index >= 0)
            {
                _currentIndex = index;
                _game.ShowMessage($"Jumped to {category} category", MessageType.TopLeft);
            }
        }

        // ===== Execution =====

        public void ExecuteCurrent()
        {
            try
            {
                CurrentUtility.Action?.Invoke();
                _game.ShowMessage($"Executed: {CurrentUtilityName}", MessageType.TopLeft);
            }
            catch (Exception ex)
            {
                _game.ShowMessage($"Error executing {CurrentUtilityName}: {ex.Message}", MessageType.TopLeft);
                Debug.LogError($"[UtilityService] Error executing {CurrentUtilityName}: {ex}");
            }
        }

        // ===== Category Info =====

        public Color GetCategoryColor(UtilityCategory category)
        {
            return _categoryColors.TryGetValue(category, out var color) ? color : Color.white;
        }

        public int GetCategoryCount(UtilityCategory category)
        {
            return _utilities.Count(u => u.Category == category);
        }

        // ===== Utility List Creation =====

        private List<Utility> CreateUtilitiesList()
        {
            var list = new List<Utility>();

            // === Repair Category ===
            list.Add(new Utility
            {
                Name = "Replenish Stacks",
                Description = "Restore item stack counts to maximum",
                Category = UtilityCategory.Repair,
                CategoryColor = GetCategoryColor(UtilityCategory.Repair),
                Action = () => CheatCommands.ReplenishStacks()
            });

            list.Add(new Utility
            {
                Name = "Repair All Structures",
                Description = "Repair all structures in area",
                Category = UtilityCategory.Repair,
                CategoryColor = GetCategoryColor(UtilityCategory.Repair),
                Action = () => CheatCommands.RepairStructuresAoE()
            });

            // === Crafting Category ===
            list.Add(new Utility
            {
                Name = "Set Crafter Name",
                Description = "Set yourself as crafter on nearby food items",
                Category = UtilityCategory.Crafting,
                CategoryColor = GetCategoryColor(UtilityCategory.Crafting),
                Action = () => CheatCommands.SetMeAsCrafterOfFood()
            });

            list.Add(new Utility
            {
                Name = "Fuel Stations",
                Description = "Add fuel to nearby smelters, fires, and cooking stations",
                Category = UtilityCategory.Crafting,
                CategoryColor = GetCategoryColor(UtilityCategory.Crafting),
                Action = () =>
                {
                    CheatCommands.RPCOnInRange<Smelter>("AddFuel", 5f, "Coal", 1);
                    CheatCommands.RPCOnInRange<CookingStation>("AddFuel", 5f, "Wood", 1);
                    CheatCommands.RPCOnInRange<Fireplace>("AddFuel", 5f, "Wood", 1);
                    CheatCommands.RPCOnInRange<Fire>("AddFuel", 5f, "Wood", 1);
                }
            });

            list.Add(new Utility
            {
                Name = "Smelt Flametal",
                Description = "Add flametal ore to nearby smelter",
                Category = UtilityCategory.Crafting,
                CategoryColor = GetCategoryColor(UtilityCategory.Crafting),
                Action = () => CheatCommands.RPCOnInRange<Smelter>("AddOre", 5f, "FlametalOreNew", 1)
            });

            // === Combat Category ===
            list.Add(new Utility
            {
                Name = "Ghost Mode",
                Description = "Toggle ghost mode (invisible to enemies)",
                Category = UtilityCategory.Combat,
                CategoryColor = GetCategoryColor(UtilityCategory.Combat),
                Action = () => CheatCommands.ToggleGhostMode()
            });

            list.Add(new Utility
            {
                Name = "Panic Jump",
                Description = "Activate super jump buff",
                Category = UtilityCategory.Combat,
                CategoryColor = GetCategoryColor(UtilityCategory.Combat),
                Action = () => CheatCommands.PanicJumpBuff()
            });

            // === Skills Category ===
            list.Add(new Utility
            {
                Name = "Toggle Guardian Power",
                Description = "Toggle forsaken power on/off",
                Category = UtilityCategory.Skills,
                CategoryColor = GetCategoryColor(UtilityCategory.Skills),
                Action = () => CheatCommands.ToggleGuardianPower()
            });

            list.Add(new Utility
            {
                Name = "Increase Skills",
                Description = "Increase all skill levels",
                Category = UtilityCategory.Skills,
                CategoryColor = GetCategoryColor(UtilityCategory.Skills),
                Action = () => CheatCommands.IncreaseSkills()
            });

            // === Exploration Category ===
            list.Add(new Utility
            {
                Name = "Reveal Morgen Hole",
                Description = "Reveal closest Ashlands cave entrance",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => CheatCommands.RevealClosestAshlandsCave()
            });

            list.Add(new Utility
            {
                Name = "Reveal Mystery",
                Description = "Reveal closest mysterious location",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => CheatCommands.RevealClosestMysterious()
            });

            list.Add(new Utility
            {
                Name = "Reveal Silver Veins",
                Description = "Pin nearby silver deposits",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => OreFinder.PinNearbySilver()
            });

            list.Add(new Utility
            {
                Name = "Reveal Dvergr House",
                Description = "Reveal closest Dvergr structure",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => CheatCommands.RevealClosestDvergrHouse()
            });

            list.Add(new Utility
            {
                Name = "Reveal All Bosses",
                Description = "Show all boss locations on map",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => CheatCommands.RevealBosses()
            });

            list.Add(new Utility
            {
                Name = "Explore Entire Map",
                Description = "Reveal the entire world map",
                Category = UtilityCategory.Exploration,
                CategoryColor = GetCategoryColor(UtilityCategory.Exploration),
                Action = () => CheatCommands.ExploreAll()
            });

            // === Misc Category ===
            list.Add(new Utility
            {
                Name = "Vomit",
                Description = "Clear all food buffs",
                Category = UtilityCategory.Misc,
                CategoryColor = GetCategoryColor(UtilityCategory.Misc),
                Action = () => CheatCommands.Vomit()
            });

            return list;
        }
    }
}
