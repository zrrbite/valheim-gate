using System.Collections.Generic;
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
        /// </summary>
        public void RestoreAll()
        {
            if (ZoneSystem.instance == null) return;

            foreach (var kv in _originalValues)
            {
                ZoneSystem.instance.SetGlobalKey(kv.Key, kv.Value);
            }

            Debug.Log($"[ICanShowYouTheWorld] Run Mode world modifiers restored ({_originalValues.Count} key(s)).");

            _originalValues.Clear();
        }

        private void SaveOriginal(GlobalKeys key)
        {
            if (_originalValues.ContainsKey(key)) return;

            float value = ZoneSystem.instance.GetGlobalKey(key, out float v) ? v : 1f;
            _originalValues[key] = value;
        }
    }
}
