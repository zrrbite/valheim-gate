using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The Húskarl's Guard (2026-10-08). SEMan.OnDamaged runs for EVERY hit in Character.RPC_Damage, before
    /// Humanoid.BlockAttack, and BlockAttack calls ModifyTimedBlockBonus only on a parry attempt. It calls
    /// ModifyBlockStaminaUsage once per block, and a second time on a successful parry when the shield has no
    /// m_perfectBlockStaminaRegen. So a hit arms the effect with the frame it landed in, and a block is the arm
    /// still live in that same frame: RPC_Damage runs the whole sequence within one frame. Two things must not
    /// heal. The second call of a parry: the first call disarms, so a parry heals once. And a hit that was never
    /// blocked (from behind, no blocker, not blockable) leaves the arm behind, for the inventory tooltip's
    /// Player.GetEquipmentModifierPlusSE call to find later: the frame check drops it. m_blockStaminaUseModifier = -0.5
    /// is the half-stamina refund, through the game's own SE_Stats arithmetic.
    /// </summary>
    internal sealed class GuardEffect : SE_Stats
    {
        /// <summary>Raised once per block: true for a parry, false for a plain block. Set by BoonEffects.</summary>
        public static Action<bool> OnGuard;

        /// <summary>The frame of the last hit; -1 when disarmed. Time.frameCount is never negative.</summary>
        private int _armedFrame = -1;
        private bool _parry;

        private bool ArmedNow => _armedFrame == UnityEngine.Time.frameCount;

        public override void OnDamaged(HitData hit, Character attacker)
        {
            base.OnDamaged(hit, attacker);
            _armedFrame = UnityEngine.Time.frameCount;
            _parry = false;
        }

        public override void ModifyTimedBlockBonus(ref float timedBlockBonus)
        {
            base.ModifyTimedBlockBonus(ref timedBlockBonus);
            if (ArmedNow) _parry = true;
        }

        public override void ModifyBlockStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            base.ModifyBlockStaminaUsage(baseStaminaUse, ref staminaUse);
            bool live = ArmedNow;
            _armedFrame = -1;   // disarmed either way: a parry's second call, or a tooltip's, finds nothing
            if (!live) return;
            try { OnGuard?.Invoke(_parry); }
            catch { /* a missed heal is a missed heal */ }
        }
    }
}
