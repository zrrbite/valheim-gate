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
        /// <summary>
        /// Sentinel original meaning "this world had no value for the key before the run", which is
        /// restored by removing the key rather than by writing a number. Rates are never negative,
        /// so it cannot collide with a real one — and unlike NaN it survives a round trip through
        /// JsonUtility, which writes NaN as a bare token no JSON parser will read back.
        /// </summary>
        private const float KeyWasAbsent = -1f;

        /// <summary>
        /// The lowest stored percentage that could have come from the game itself. Valheim's own
        /// world-modifier presets sit between 50 and 300; anything below this is damage from a
        /// pre-alpha17 build of this mod. See <see cref="Sanitize"/>.
        /// </summary>
        private const float MinSaneRatePercent = 5f;

        private readonly Dictionary<GlobalKeys, float> _originalValues = new Dictionary<GlobalKeys, float>();

        /// <summary>
        /// Writes a rate key as the PERCENTAGE the game expects, not as a bare multiplier.
        ///
        /// This is the single most important line in the file. Game.UpdateWorldRates reads each of
        /// these keys through a helper that does <c>rate = parsed / 100</c> (verified in the IL:
        /// <c>Game.&lt;UpdateWorldRates&gt;g__trySetScalarKey</c>, whose "defaultValue 1, multiplier
        /// 100" arguments mean an absent key is 1.0x and a stored 100 is also 1.0x). Writing a bare
        /// 3 for "triple resources" therefore asked for 0.03x, and every empowerment this mode
        /// grants was silently inverted into a crippling penalty: 3x resources became 3% (Mathf.Ceil
        /// floored every drop to one — the field-reported "I chop a tree and get 1 wood"), 1.5x
        /// stamina regen became 1.5% (stamina that never came back), and heat's enemy scaling made
        /// enemies WEAKER as the run got hotter.
        /// </summary>
        private static void SetRate(GlobalKeys key, float multiplier)
        {
            ZoneSystem.instance.SetGlobalKey(key, multiplier * 100f);
        }

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
            SaveOriginal(GlobalKeys.StaminaRate);

            SetRate(GlobalKeys.ResourceRate, cfg.RunResourceRate);
            SetRate(GlobalKeys.SkillGainRate, cfg.RunSkillGainRate);
            SetRate(GlobalKeys.MoveStaminaRate, cfg.RunMoveStaminaRate);
            SetRate(GlobalKeys.StaminaRegenRate, cfg.RunStaminaRegenRate);
            SetRate(GlobalKeys.StaminaRate, cfg.RunStaminaRate);
            RefreshRates();

            Debug.Log("[ICanShowYouTheWorld] Run Mode baseline world modifiers applied " +
                $"(resource={cfg.RunResourceRate}, skill={cfg.RunSkillGainRate}, " +
                $"moveStamina={cfg.RunMoveStaminaRate}, staminaRegen={cfg.RunStaminaRegenRate}, " +
                $"stamina={cfg.RunStaminaRate}).");
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

            SetRate(GlobalKeys.EnemyDamage,
                HeatEffects.EnemyDamageMultiplier(heat, cfg.RunHeatEnemyDamageWeight));
            // (RefreshRates below keeps the live caches in step with the key writes.)
            SetRate(GlobalKeys.EnemyLevelUpRate,
                HeatEffects.EnemyLevelUpMultiplier(heat, cfg.RunHeatEnemyLevelUpWeight));
            RefreshRates();
        }

        /// <summary>
        /// Restores every world-modifier key touched by this instance back to its pre-run value —
        /// and REMOVES any key the world did not have before, rather than inventing a value for it.
        /// Returns false if the world is not loaded, in which case the saved originals are
        /// deliberately kept so the caller can retry once ZoneSystem comes back.
        /// </summary>
        public bool RestoreAll()
        {
            if (_originalValues.Count == 0) return true;
            if (ZoneSystem.instance == null) return false;

            foreach (var kv in _originalValues)
            {
                if (kv.Value < 0f) ZoneSystem.instance.RemoveGlobalKey(kv.Key);
                else ZoneSystem.instance.SetGlobalKey(kv.Key, kv.Value);
            }

            Debug.Log($"[ICanShowYouTheWorld] Run Mode world modifiers restored ({_originalValues.Count} key(s)).");

            _originalValues.Clear();
            RefreshRates();
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
                // Sanitised on the way in as well: a save written by a pre-alpha17 build carries
                // that build's bad originals, and importing them unchecked would restore the
                // damage this run is meant to undo.
                _originalValues[key] = values[i] < 0f ? KeyWasAbsent : Sanitize(key, values[i]);
            }

            Debug.Log($"[ICanShowYouTheWorld] Run Mode world modifier originals imported ({n} key(s)).");
        }


        /// <summary>
        /// Valheim CACHES every world-modifier rate as a static on Game (m_resourceRate,
        /// m_skillGainRate, m_enemyDamageRate, ...), refreshed only by UpdateWorldRates —
        /// normally at world load. SetGlobalKey alone therefore changes what's SAVED but not
        /// what's LIVE: drops, skill gain and heat's enemy scaling all kept reading stale
        /// caches (field-found: a 3x resource run yielding vanilla 1-wood trees). Call this
        /// after every batch of key writes.
        /// </summary>
        private static void RefreshRates()
        {
            try { ZoneSystem.instance?.UpdateWorldRates(); }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ICanShowYouTheWorld] UpdateWorldRates failed: {ex.Message}");
            }
        }

        private void SaveOriginal(GlobalKeys key)
        {
            if (_originalValues.ContainsKey(key)) return;

            _originalValues[key] = ZoneSystem.instance.GetGlobalKey(key, out float v)
                ? Sanitize(key, v)
                : KeyWasAbsent;
        }

        /// <summary>
        /// A stored rate that no world setting could have produced is treated as damage from an
        /// earlier build of this mod, not as the world's own value — so it is restored by REMOVING
        /// the key rather than by writing the nonsense back.
        ///
        /// Builds before alpha17 wrote raw multipliers into keys the game reads as percentages,
        /// and restored an untouched key as "1" — which is 1%, not 1x. A world that has finished a
        /// run under one of those builds is therefore sitting on resourcerate=1: one wood per tree,
        /// forever, in and out of Run Mode. Nothing in Valheim's own options can set a rate that
        /// low (its presets are 50-300), so anything under <see cref="MinSaneRatePercent"/> is
        /// unambiguously ours to clean up.
        /// </summary>
        private static float Sanitize(GlobalKeys key, float rawPercent)
        {
            if (rawPercent >= MinSaneRatePercent) return rawPercent;

            Debug.LogWarning($"[ICanShowYouTheWorld] World modifier '{key}' was {rawPercent:0.##}% — " +
                             "far below anything the game can set, so it is being treated as damage " +
                             "from an earlier build and will be cleared when this run ends.");
            return KeyWasAbsent;
        }
    }
}
