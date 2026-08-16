using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Applies Run Mode's empowerment baseline and heat scaling via Valheim's built-in
    /// world-modifier global keys (ZoneSystem.SetGlobalKey), and restores them afterward.
    /// </summary>
    public class WorldModifiers
    {
        private readonly Dictionary<GlobalKeys, float> _originalValues = new Dictionary<GlobalKeys, float>();

        /// <summary>
        /// Applies the run's baseline resource/skill/stamina rates. Saves each key's
        /// pre-run value the first time it is touched.
        /// </summary>
        public void ApplyBaseline(IConfiguration cfg)
        {
            if (ZoneSystem.instance == null) return;

            SaveOriginal(GlobalKeys.ResourceRate);
            SaveOriginal(GlobalKeys.SkillGainRate);
            SaveOriginal(GlobalKeys.MoveStaminaRate);
            SaveOriginal(GlobalKeys.StaminaRegenRate);

            ZoneSystem.instance.SetGlobalKey(GlobalKeys.ResourceRate, cfg.RunResourceRate);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.SkillGainRate, cfg.RunSkillGainRate);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.MoveStaminaRate, cfg.RunMoveStaminaRate);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.StaminaRegenRate, cfg.RunStaminaRegenRate);

            Debug.Log("[ICanShowYouTheWorld] Run Mode baseline world modifiers applied " +
                $"(resource={cfg.RunResourceRate}, skill={cfg.RunSkillGainRate}, " +
                $"moveStamina={cfg.RunMoveStaminaRate}, staminaRegen={cfg.RunStaminaRegenRate}).");
        }

        /// <summary>
        /// Scales enemy damage and level-up chance with the run's current heat.
        /// Called on every heat change; does not log each call.
        /// </summary>
        public void ApplyHeat(float heat, IConfiguration cfg)
        {
            if (ZoneSystem.instance == null) return;

            SaveOriginal(GlobalKeys.EnemyDamage);
            SaveOriginal(GlobalKeys.EnemyLevelUpRate);

            ZoneSystem.instance.SetGlobalKey(GlobalKeys.EnemyDamage,
                HeatEffects.EnemyDamageMultiplier(heat, cfg.RunHeatEnemyDamageWeight));
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.EnemyLevelUpRate,
                HeatEffects.EnemyLevelUpMultiplier(heat, cfg.RunHeatEnemyLevelUpWeight));
        }

        /// <summary>
        /// Restores every world-modifier key touched by this instance back to its
        /// pre-run value (1f, the vanilla default, for any key that had none set).
        /// Returns false if the world is not loaded, in which case the saved originals are
        /// deliberately kept so the caller can retry once ZoneSystem comes back.
        /// </summary>
        public bool RestoreAll()
        {
            if (_originalValues.Count == 0) return true;
            if (ZoneSystem.instance == null) return false;

            foreach (var kv in _originalValues)
            {
                ZoneSystem.instance.SetGlobalKey(kv.Key, kv.Value);
            }

            Debug.Log($"[ICanShowYouTheWorld] Run Mode world modifiers restored ({_originalValues.Count} key(s)).");

            _originalValues.Clear();
            return true;
        }

        /// <summary>Enum values of every key whose pre-run value has been captured.</summary>
        public List<int> ExportOriginalKeys() => _originalValues.Keys.Select(k => (int)k).ToList();

        /// <summary>Pre-run values, in the same order as <see cref="ExportOriginalKeys"/>.</summary>
        public List<float> ExportOriginalValues() => _originalValues.Values.ToList();

        /// <summary>
        /// Seeds the saved-originals dictionary from a previous session WITHOUT reading the
        /// live world. This is what stops a resumed run from mistaking its own inflated rates
        /// for the world's originals and baking them in permanently — Valheim persists valued
        /// global keys with the world save, so a wrong capture here is forever. Keys seeded
        /// this way make later SaveOriginal calls no-ops, exactly as if we had captured them
        /// ourselves before the run started.
        /// </summary>
        public void ImportOriginals(List<int> keys, List<float> values)
        {
            if (keys == null || values == null) return;

            int n = Math.Min(keys.Count, values.Count);
            for (int i = 0; i < n; i++)
            {
                var key = (GlobalKeys)keys[i];
                if (_originalValues.ContainsKey(key)) continue;
                _originalValues[key] = values[i];
            }

            Debug.Log($"[ICanShowYouTheWorld] Run Mode world modifier originals imported ({n} key(s)).");
        }

        private void SaveOriginal(GlobalKeys key)
        {
            if (_originalValues.ContainsKey(key)) return;

            float value = ZoneSystem.instance.GetGlobalKey(key, out float v) ? v : 1f;
            _originalValues[key] = value;
        }
    }
}
