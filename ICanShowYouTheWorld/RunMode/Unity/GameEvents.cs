using System;
using UnityEngine;

namespace ICanShowYouTheWorld
{
    /// <summary>
    /// Receives calls injected into game code by the Patcher.
    /// Handlers must never let exceptions escape into game code.
    /// </summary>
    public static class GameEvents
    {
        public static bool HookInstalled { get; private set; }
        public static event Action<Character> OnCharacterDied;

        // Called from IL injected at the start of Character.OnDeath().
        public static void CharacterDied(Character c)
        {
            HookInstalled = true;
            try { OnCharacterDied?.Invoke(c); }
            catch (Exception ex) { Debug.LogError($"[ICanShowYouTheWorld] OnCharacterDied handler failed: {ex}"); }
        }
    }
}
