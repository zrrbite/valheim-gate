using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The god's wind and the Wind-horn on the player: two status effects carrying the game's own
    /// SailingPower attribute - the one Moder's power carries. See <see cref="SagaWind"/> for the rules.
    /// </summary>
    /// <remarks>
    /// Ship.IsWindControllActive asks every player aboard whether they hold SailingPower, and
    /// EnvMan.UpdateWind then turns the wind to the ship's back (1.0.16 IL). So holding the attribute
    /// IS the tailwind, with the game's own sail and visuals - nothing here touches the weather.
    ///
    /// Same shape as the Hunter's hush (BoonEffects): made once, never registered, handed to
    /// SEMan.AddStatusEffect, which clones it. Keyed by the asset name's hash. Status effects are never
    /// saved and death clears them, which suits both: the god's wind is re-laid every second it is
    /// owed, and a horn's two minutes end with the one who blew it.
    /// </remarks>
    internal sealed class SagaWinds
    {
        private const string GodWindName = "ICSYTW_GodsWind";
        private const string HornName = "ICSYTW_WindHorn";

        private static readonly int GodWindHash = GodWindName.GetStableHashCode();
        private static readonly int HornHash = HornName.GetStableHashCode();

        /// <summary>The god's wind lapses this long after the last second it was owed.</summary>
        private const float GodWindLease = 3f;

        /// <summary>Moder's own power, whose icon both winds wear - a guessed name, in the self-check.</summary>
        public const string ModerPower = "GP_Moder";

        private StatusEffect _godWind;
        private StatusEffect _horn;
        private bool _reported;

        /// <summary>Lays the god's wind on (refreshing its short lease) or takes it off.</summary>
        public void SetGodWind(Player player, bool blowing)
        {
            try
            {
                var seman = player != null ? player.GetSEMan() : null;
                if (seman == null) return;
                if (blowing) seman.AddStatusEffect(GodWind(), true);
                else if (seman.HaveStatusEffect(GodWindHash)) seman.RemoveStatusEffect(GodWindHash, true);
            }
            catch (Exception ex) { Report("god's wind", ex); }
        }

        /// <summary>Two minutes of wind at the ship's back.</summary>
        public void BlowHorn(Player player)
        {
            try
            {
                var seman = player != null ? player.GetSEMan() : null;
                if (seman != null) seman.AddStatusEffect(Horn(), true);
            }
            catch (Exception ex) { Report("wind-horn", ex); }
        }

        /// <summary>Both off. At run end.</summary>
        public void Clear(Player player)
        {
            try
            {
                var seman = player != null ? player.GetSEMan() : null;
                if (seman == null) return;
                if (seman.HaveStatusEffect(GodWindHash)) seman.RemoveStatusEffect(GodWindHash, true);
                if (seman.HaveStatusEffect(HornHash)) seman.RemoveStatusEffect(HornHash, true);
            }
            catch { }
        }

        private StatusEffect GodWind() =>
            _godWind != null ? _godWind
            : (_godWind = Make(GodWindName, "The god's wind",
                "The sky wants that god reached. While your prow points at it, the wind comes round.",
                GodWindLease));

        private StatusEffect Horn() =>
            _horn != null ? _horn
            : (_horn = Make(HornName, "Wind-horn", "The wind at your back, whichever way you sail.", SagaWind.HornSeconds));

        private static StatusEffect Make(string id, string display, string tooltip, float ttl)
        {
            var se = ScriptableObject.CreateInstance<StatusEffect>();
            se.name = id;
            se.m_name = display;
            se.m_tooltip = tooltip;
            se.m_ttl = ttl;
            se.m_attributes = StatusEffect.StatusAttribute.SailingPower;
            se.m_icon = ModerIcon();
            return se;
        }

        /// <summary>Moder's power's icon, or none (the wind still blows; only the HUD icon is missing).</summary>
        private static Sprite ModerIcon()
        {
            try
            {
                var odb = ObjectDB.instance;
                var moder = odb != null ? odb.GetStatusEffect(ModerPower.GetStableHashCode()) : null;
                return moder != null ? moder.m_icon : null;
            }
            catch { return null; }
        }

        private void Report(string what, Exception ex)
        {
            if (_reported) return;
            _reported = true;
            Debug.LogWarning($"[ICanShowYouTheWorld] The {what} could not blow: {ex.Message}");
        }
    }
}
