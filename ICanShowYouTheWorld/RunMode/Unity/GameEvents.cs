using System;
using System.Reflection;
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

        /// <summary>Probe result, computed once and cached — the IL of a loaded method cannot change.</summary>
        private static bool? _probedInstalled;

        /// <summary>
        /// True when the Patcher's call to <see cref="CharacterDied"/> is actually present in
        /// Character.OnDeath's IL. This answers "is the hook installed" directly, rather than the
        /// old heuristic of waiting to see whether anything died — which reported a missing hook
        /// after a quiet minute of play and put a false warning on the HUD.
        ///
        /// Scans the method body for a `call` (0x28) whose operand token resolves to CharacterDied.
        /// The injected call site references it as a MemberRef into the mod assembly, so
        /// ResolveMethod may either hand back the very same MethodInfo or throw on the foreign
        /// token; both are handled, and a resolved-but-not-reference-equal match falls back to
        /// comparing the method and declaring-type names.
        ///
        /// Any failure is reported as "not installed" — a wrong "installed" would silently disable
        /// the warning the player needs, whereas a wrong "missing" is merely a visible notice.
        /// </summary>
        public static bool ProbeHookInstalled()
        {
            if (_probedInstalled.HasValue) return _probedInstalled.Value;

            try
            {
                var onDeath = typeof(Character).GetMethod(
                    "OnDeath",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);

                var body = onDeath?.GetMethodBody()?.GetILAsByteArray();
                var target = typeof(GameEvents).GetMethod(nameof(CharacterDied));

                _probedInstalled = body != null && target != null && ContainsCallTo(onDeath.Module, body, target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Hook probe failed: {ex.Message}");
                _probedInstalled = false;
            }

            return _probedInstalled.Value;
        }

        /// <summary>
        /// Walks the IL looking for `call` (0x28) with an operand token resolving to <paramref name="target"/>.
        ///
        /// Naive byte scan, not a real IL decoder: it does not track instruction boundaries, so a
        /// 0x28 byte inside another instruction's operand can be examined too. That is harmless —
        /// such a token almost never resolves, and if it does it will not be CharacterDied.
        /// </summary>
        private static bool ContainsCallTo(Module module, byte[] body, MethodInfo target)
        {
            for (int i = 0; i < body.Length - 4; i++)
            {
                if (body[i] != 0x28) continue; // call

                int token = BitConverter.ToInt32(body, i + 1);

                MethodBase resolved;
                try
                {
                    resolved = module.ResolveMethod(token);
                }
                catch
                {
                    // Not a method token (or a foreign one this module can't resolve) — keep looking.
                    continue;
                }

                if (resolved == null) continue;
                if (resolved == target) return true;

                // Cross-assembly MemberRef: the resolved handle need not be reference-equal to the
                // mod's own MethodInfo, so fall back to identifying it by name.
                if (resolved.Name == target.Name && resolved.DeclaringType?.Name == target.DeclaringType?.Name)
                    return true;
            }

            return false;
        }

        // Called from IL injected at the start of Character.OnDeath().
        public static void CharacterDied(Character c)
        {
            HookInstalled = true;
            try { OnCharacterDied?.Invoke(c); }
            catch (Exception ex) { Debug.LogError($"[ICanShowYouTheWorld] OnCharacterDied handler failed: {ex}"); }
        }
    }
}
