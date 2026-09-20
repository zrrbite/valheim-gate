using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Puts the crafting list in alphabetical order, by asking Valheim to do it.
    /// </summary>
    /// <remarks>
    /// The owner's complaint was the real one: "the later you get in the game the more important it
    /// is, because the default list of recipes is NOT ALPHA SORTED." By the Plains a workbench lists
    /// dozens of recipes in an order nobody can predict, and finding one is a scan rather than a look.
    ///
    /// The plan was a search box, which meant surgery on <c>InventoryGui</c> — hiding recipe elements
    /// and re-stacking the survivors by index on every rebuild, plus an IMGUI text field competing
    /// with the game's own keyboard handling over a panel the player is using. Reading the IL first
    /// turned out to be worth more than any of it.
    ///
    /// <b>Valheim already sorts that list, and has a setting for it.</b>
    /// <c>InventoryGui.UpdateRecipeList</c> does:
    ///
    /// <code>
    /// Player.m_localPlayer.TryGetUniqueKeyValue("sortcraft", out string v)
    ///   → Enum.TryParse&lt;InventoryGui.SortMethod&gt;(v, true, out method)
    ///     → switch (method) { Original | Name | Type | Weight | Count }
    /// </code>
    ///
    /// With no key set the method is <c>Original</c>, which is the arbitrary order being complained
    /// about. Writing <c>"Name"</c> into that key makes the GAME sort its own list, with the game's
    /// own comparator, in the game's own <c>UpdateRecipeList</c>. Nothing is reflected into, nothing
    /// is re-laid-out, and there is no code of ours between the player and the panel.
    ///
    /// It is a console setting, not a UI one — the only other writer of that key in the whole
    /// assembly is <c>Terminal.InitTerminal</c> — so the feature exists and is simply undiscoverable.
    ///
    /// Two pieces of care. The key belongs to the CHARACTER, so this re-checks whenever the player
    /// instance changes rather than once per session. And it only writes when the key is absent or
    /// explicitly <c>Original</c>: somebody who chose <c>Type</c> or <c>Weight</c> on purpose has
    /// said what they want, and a mod that overrules that is a mod arguing with its user.
    /// </remarks>
    internal sealed class CraftingSort
    {
        private const string Key = "sortcraft";
        private const string Wanted = "Name";

        /// <summary>The order that means "whatever order the game happened to build them in".</summary>
        private const string Unsorted = "Original";

        private Player _seen;

        /// <summary>Forgets which player was handled, so a fresh character is checked again.</summary>
        public void Reset() => _seen = null;

        /// <summary>
        /// Every frame, run or no run — this is a quality-of-life fix with nothing to do with a
        /// saga, and the acts where it matters most are the late ones. Two reference comparisons
        /// once the work is done.
        /// </summary>
        public void Ensure(IConfiguration cfg)
        {
            if (cfg != null && !cfg.RunSortCraftingByName) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            // ReferenceEquals, not ==: a destroyed player compares equal to null through Unity's
            // operator while still being the reference we last handled, and the key is per-character
            // so a new one genuinely needs looking at.
            if (ReferenceEquals(player, _seen)) return;
            _seen = player;

            try
            {
                string current;
                bool has = player.TryGetUniqueKeyValue(Key, out current);

                if (has && !string.Equals(current, Unsorted, StringComparison.OrdinalIgnoreCase))
                {
                    // A deliberate choice, including a previous run of this. Leave it alone.
                    return;
                }

                player.AddUniqueKeyValue(Key, Wanted);
                Debug.Log($"[ICanShowYouTheWorld] Crafting list sorted by name (Valheim's own '{Key}' " +
                          $"setting, was {(has ? current : "unset")}).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Could not set the crafting sort order: " + ex.Message);
            }
        }
    }
}
