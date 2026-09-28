using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Turns boon gain/loss/activation into calls against the mod's existing cheat surfaces.
    /// RunService owns the lifecycle (which boon fired, when a run ends) and wires its three
    /// seams — ApplyBoonEffect, UnapplyBoonEffect, UnapplyAllBoonEffects — to this class's
    /// methods; this class only knows how to turn a boon id into game effects.
    ///
    /// Two cheat-state worlds coexist in this codebase: the legacy <see cref="CheatCommands"/>
    /// statics (driven every frame by <c>CheatCommands.PeriodicManager</c>) and the newer
    /// per-interface services (whose own periodic driver, <c>BuffService.HandlePeriodic</c>,
    /// has zero call sites). wind/ember ride the LEGACY toggles because that's the one actually
    /// ticking; brother touches neither, spawning against the game's own APIs.
    ///
    /// The pet-buff boon this replaced went through <c>IPetService.BuffAllPets</c>, which is a
    /// poor fit for a loaned boon on three counts: it fires once at pick time (a creature tamed
    /// later is never buffed), it recomputes its "baseline" from already-buffed weapons so
    /// repeat calls compound, and it writes absolute damage into <c>m_shared</c> — the per-prefab
    /// block — exactly the permanence fleet/sharp were rewritten to avoid.
    ///
    /// fleet/sharp never touch either cheat's shared/global counters (SpeedUp/SpeedDown collapse
    /// walk into run and stomp jump; the damage-counter mechanism writes an ABSOLUTE, per-prefab
    /// value into the weapon's shared damage block). Instead sharp snapshots the item state on
    /// Apply and restores that exact snapshot on Unapply, and fleet lends against the player's speed
    /// fields through the ledger below (since 2026-09-28, so the Marching Song can share them) —
    /// "power is loaned", never permanent.
    ///
    /// mule/hearty/enduring are the same bargain in its simplest form: one plain float field on
    /// Player each (carry weight, base HP, base stamina), snapshotted and restored. They go
    /// through the shared <see cref="FieldBoost"/> table rather than three copies of fleet's code,
    /// and unlike fleet they refuse to apply twice to one player — the respawn re-apply path can
    /// legitimately reach them a second time, and stacking there would also snapshot the boosted
    /// value as the "original".
    /// </summary>
    public class BoonEffects
    {
        private const float SharpDamageMultiplier = 1.2f;
        private const float FleetSpeedIncrements = 2f;
        private const float WindOnSeconds = 10f;
        private const float EmberOnSeconds = 30f;

        /// <summary>
        /// How long Unseen lasts. Short on purpose: ghost mode is a total answer to every melee in
        /// the game, so its value has to be "get out of this", not "win this".
        /// </summary>
        private const float UnseenOnSeconds = 20f;

        private const float MuleCarryWeightBonus = 100f;   // vanilla Player.m_maxCarryWeight is 300

        // Vanilla Player.m_baseHP is 25, so the originally specified +25 was a flat doubling of
        // base HP — and it compounds with food and with Enduring. Trimmed to +15 (a 60% lift)
        // pending a play-test.
        private const float HeartyBaseHpBonus = 15f;
        private const float EnduringBaseStaminaBonus = 25f;

        // Glass Cannon. Vanilla Player.m_baseHP is 25, so -7.5 is the stated 30%. A flat number
        // rather than a multiplier because the field is shared with Hearty and with food, and two
        // boons multiplying the same base in an order nobody controls is how compounding bugs start.
        private const float GlassCannonBaseHpPenalty = 7.5f;
        private const float GlassCannonDamageMultiplier = 1.4f;
        private const float RecklessDamageMultiplier = 1.5f;

        /// <summary>
        /// How hard Forge-fed hits per point of heat. At the default heat weights a run reaching
        /// the Plains sits near 45 heat, so this tops out around +45% — comparable to Sharpened
        /// but earned by having run hot rather than by being picked.
        /// </summary>
        private const float ForgeFedPerHeat = 0.01f;

        /// <summary>Cap on Forge-fed, so a pathological heat number cannot produce a silly multiplier.</summary>
        private const float ForgeFedMaxMultiplier = 2f;

        /// <summary>Health and stamina returned per kill by the on-kill boons.</summary>
        private const float BloodthirstHealPerKill = 5f;
        private const float RelentlessStaminaPerKill = 15f;

        // Packbrother. Two at a time keeps it a bodyguard rather than an army that trivialises
        // the heat curve, and the level tracks boss progress so a companion summoned in the
        // Plains isn't the same meadows wolf that dies to one Fuling.
        private const string CompanionPrefab = "Wolf";
        private const string BonePrefab = "Skeleton";

        // --- The ways' actives (classes, phase 2) ---
        //
        // The descriptions in RunService.DefaultBoons state the radii, the duration and "half again",
        // so those numbers are the card's as much as this class's; change one, change both.

        /// <summary>Blood Rage's gain: "half again the damage", on the same product Sharpened rides.</summary>
        private const float RageMultiplier = 1.5f;

        /// <summary>Blood Rage's window: "fifteen seconds". Short, because the cost only lasts as long.</summary>
        private const float RageSeconds = 15f;

        /// <summary>Rend reaches what a sweep of a blade reaches — about a spear's length and a step.</summary>
        private const float RendRadius = 5f;

        /// <summary>Rend's cut, before boss scaling: about one early sword swing, spent on everyone at once.</summary>
        private const float RendSlash = 20f;

        /// <summary>Rend's bleed, before boss scaling. Poison, because RPC_Damage hands it to the game's
        /// own SE_Poison as a damage-over-time — the bleed costs no status effect of ours.</summary>
        private const float RendPoison = 15f;

        /// <summary>Warcry's reach: "eight metres", a shout rather than a swing.</summary>
        private const float WarcryRadius = 8f;

        /// <summary>Thor's Wrath's blast: "six metres" around where it lands.</summary>
        private const float WrathRadius = 6f;

        /// <summary>Wrath's lightning, before boss scaling. Above the Stormward's 26 per discharge
        /// because it is a sixty-second cooldown the player aims, not a block every few seconds.</summary>
        private const float WrathLightning = 40f;

        /// <summary>A little over the default 1: lightning already counts toward stagger in the IL
        /// (GetTotalStaggerDamage), and this is a spell, not a Warcry.</summary>
        private const float WrathStaggerMultiplier = 1.5f;

        /// <summary>How far the aim ray looks: bow range, roughly — further and the strike lands
        /// somewhere the player cannot see well enough to have meant it.</summary>
        private const float WrathAimRange = 40f;

        /// <summary>Where the strike lands when the aim ray finds nothing: a few steps ahead.</summary>
        private const float WrathFallbackDistance = 8f;

        /// <summary>Eitr for one Wrath, in the band a Mistlands staff charges per cast.</summary>
        private const float WrathEitr = 25f;

        /// <summary>Stamina for one Wrath when the player has no eitr — which is every act before the
        /// Mistlands, since eitr only exists while eitr food is eaten. About a power attack.</summary>
        private const float WrathStamina = 30f;

        /// <summary>How long the borrowed flash is allowed to stand before it is taken down. See
        /// SpawnWrathFlash for why its lifetime is ours rather than the prefab's.</summary>
        private const float WrathFlashSeconds = 4f;

        /// <summary>
        /// Rend and Wrath grow a quarter per boss felled, capped at triple — the companion-level idea
        /// (CompanionLevel) in damage: a Meadows swing would be a rounding error on a Plains Fuling,
        /// and an uncapped scale is how a late-game cooldown becomes a screen wipe.
        /// </summary>
        private const float ClassDamagePerBoss = 0.25f;
        private const float ClassDamageMaxScale = 3f;

        // --- The four ways of 2026-09-28: Húskarl, Skald, Sæfari, Smiðr ---
        //
        // Same rule again: DefaultBoons' descriptions and the passives' cards state these numbers
        // ("+20 max health", "twenty seconds", "within twenty metres"), so a change here is a change
        // to the card.

        /// <summary>Hirdman's health: "+20", a little over Hearty's 15 because it is the way's only
        /// passive stat, and the Húskarl is the one who is meant to be standing at the end.</summary>
        private const float HirdmanBaseHpBonus = 20f;

        /// <summary>Craftsman's carry: "100 more weight", Packmule's number.</summary>
        private const float CraftsmanCarryWeightBonus = 100f;

        /// <summary>Shield Bash's reach: a shield's length and a step. Shorter than Rend's five,
        /// because it only reaches forward.</summary>
        private const float BashRadius = 4f;

        /// <summary>
        /// How far off the look direction a foe may stand and still be "in front": the cosine of
        /// sixty degrees, so a 120-degree wedge. Wide enough that a player who is not aiming
        /// carefully still hits what they are facing; narrow enough that the thing behind them does
        /// not get staggered by a shield pointed the other way.
        /// </summary>
        private const float BashFrontDot = 0.5f;

        /// <summary>Shield Wall's window: "twenty seconds".</summary>
        private const float BulwarkSeconds = 20f;

        /// <summary>Last Stand's window: "six seconds nothing can end".</summary>
        private const float LastStandSeconds = 6f;

        /// <summary>Last Stand's close: "half your health back".</summary>
        private const float LastStandHealFraction = 0.5f;

        /// <summary>Marching Song's window: "twenty seconds".</summary>
        private const float MarchSeconds = 20f;

        /// <summary>
        /// Marching Song's step, as a fraction of the player's own speed before any loan. A
        /// fraction rather than Fleet-footed's flat increments, because the song is a window and
        /// "a quarter faster" is the thing a player can feel in twenty seconds.
        /// </summary>
        private const float MarchSpeedFraction = 0.25f;

        /// <summary>Marching Song's breath: +4 stamina a second on the regen field Tireless lends.</summary>
        private const float MarchStaminaRegen = 4f;

        /// <summary>War Song's window and edge: "twenty seconds", +15% on the weapon product.</summary>
        private const float WarsongSeconds = 20f;
        private const float WarsongMultiplier = 1.15f;

        /// <summary>Saga of Bragi's heal: "some of your health back", which is 30%.</summary>
        private const float BragiHealFraction = 0.3f;

        /// <summary>Tide-borne's window and breath: "thirty seconds", +8 stamina a second - more than
        /// swimming or rowing can spend.</summary>
        private const float TideSeconds = 30f;
        private const float TideStaminaRegen = 8f;

        /// <summary>Fair Wind's window: "for a minute".</summary>
        private const float FairWindSeconds = 60f;

        /// <summary>How near a ship must be to feel Fair Wind: a stone's throw from the shore.</summary>
        private const float FairWindShipRange = 30f;

        /// <summary>Sea Legs' window: "five minutes".</summary>
        private const float SeaLegsSeconds = 300f;

        /// <summary>Field Forge's window: "ninety seconds".</summary>
        private const float FieldForgeSeconds = 90f;

        /// <summary>
        /// Where the bench and the forge stand: this far ahead of the player, and this far either
        /// side of that point. Four metres between centres leaves about a metre and a half of clear
        /// ground between the two pieces, which is room to walk between them.
        /// </summary>
        private const float FieldForgeAhead = 3f;
        private const float FieldForgeSide = 2f;

        internal const string FieldBenchPrefab = "piece_workbench";
        internal const string FieldForgePrefab = "forge";

        /// <summary>Master's Minute: "one minute".</summary>
        private const float MastersMinuteSeconds = 60f;

        /// <summary>Reinforce's window and reach: "ten minutes", "within twenty metres".</summary>
        private const float ReinforceSeconds = 600f;
        private const float ReinforceRadius = 20f;

        // --- The general boons of 2026-09-27 ---
        //
        // Same rule as the ways' block above: DefaultBoons' descriptions state these numbers, so a
        // change here is a change to the card.

        /// <summary>Stoker's gain: "+20%", Sharpened's number, because the heat is the price Sharpened
        /// does not ask. The heat half lives in RunService.AddHeat, next to Slow Burn's.</summary>
        private const float StokerDamageMultiplier = 1.2f;

        /// <summary>Mending Hands' reach: "twenty metres", the radius the GM repair command has always
        /// used — a longhouse and its fence, not the village.</summary>
        private const float MendRadius = 20f;

        /// <summary>Farsight's reach: "four hundred metres", four times the game's own walking reveal
        /// (Minimap.m_exploreRadius is 100). A look from a hilltop, not the whole map.</summary>
        private const float FarsightRadius = 400f;

        /// <summary>
        /// How many summoned companions may stand at once, across ALL summoning boons.
        ///
        /// One shared cap rather than one each: the retinue is the thing being limited, and two
        /// boons with two caps would quietly become a retinue of four.
        /// </summary>
        private const int MaxCompanions = 4;
        private static readonly string[] CompanionNames =
            { "Freki", "Geri", "Hati", "Skoll", "Vigi", "Garm" };

        private readonly Func<IReadOnlyList<HeldBoon>> _heldBoons;
        private readonly Func<IEnumerable<string>> _undefeatedBossLocations;
        private readonly Func<int> _defeatedBossCount;

        /// <summary>
        /// Raises a skill for the rest of the run — RunService.LoanSkill, which snapshots the
        /// pre-run level, only ever raises, and hands it back when the run ends. Skill boons go
        /// through the host rather than writing levels here because the snapshot has to live with
        /// the run's other loans, be persisted with them, and be given back on the same paths.
        /// </summary>
        private readonly Action<Skills.SkillType, float> _loanSkill;

        /// <summary>
        /// Companions summoned this run, oldest first, identified by ZDOID rather than by
        /// GameObject: a companion whose zone unloads has no live object but still has a ZDO,
        /// and only the ZDOID survives that round trip.
        /// </summary>
        private readonly List<ZDOID> _companions = new List<ZDOID>();
        private int _companionNameIndex;

        private struct PendingOff
        {
            public string Key;
            public float Remaining;
            public Action Off;
        }

        // Timed ON/OFF scheduling for the active boons (wind's 10s heal window, ember's 30s burn
        // window). Keyed so a lose-then-regain cycle can't have a stale timer cut a fresh window
        // short (RemovePending is called both when (re)scheduling and when forcing off).
        private readonly List<PendingOff> _pending = new List<PendingOff>();

        // Toggle safety: the legacy statics are TOGGLES, not setters. Only flip one back off if
        // this class is the one that flipped it on — never stomp a state the player set for
        // themselves outside Run Mode.
        private bool _aoeRenewalOnByUs;
        private bool _cloakOnByUs;

        /// <summary>Unseen turned ghost mode on and owes it an off. See ForceGhostOff.</summary>
        private bool _ghostOnByUs;

        /// <summary>
        /// The flames drawn on the player while Emberskin is on: the Burning status effect's own
        /// start effects, attached to the player and destroyed when the cloak ends. The look
        /// without the status — applying SE_Burning itself would burn the player (which is what
        /// "I've tried it before" found). Owner, 2026-09-12: "Emberskin could trigger a fire
        /// condition on player."
        /// </summary>
        private GameObject[] _emberFlames;

        /// <summary>
        /// One plain float field on Player that a boon can lend against — carry weight, base health,
        /// the stamina numbers. All are read live by the game every frame (GetMaxCarryWeight reads
        /// m_maxCarryWeight, GetTotalFoodValue reads m_baseHP/m_baseStamina, both verified against
        /// the IL), so raising the field IS the effect. All are plain instance fields on Player, not
        /// shared or prefab state, which is what makes a loan honest: it is per-character and dies
        /// with the run.
        ///
        /// The pristine value is NOT held here. It lives once per FIELD in <see cref="_loans"/>,
        /// because more than one boon can lend against the same field — Hearty and Glass Cannon both
        /// move base health, and the per-completion reward makes three. Snapshotting per boon, which
        /// is what this did before alpha35, let the second lender record the first one's boosted
        /// value as "original" and leave the player permanently altered after the run. See
        /// <see cref="LoanLedger"/>.
        /// </summary>
        private sealed class FieldLoan
        {
            public string BoonId;
            public string Field;
            public float Amount;
        }

        /// <summary>Which Player field each <see cref="FieldLoan.Field"/> key reads and writes.</summary>
        private static readonly Dictionary<string, (Func<Player, float> Get, Action<Player, float> Set)> FieldAccess =
            new Dictionary<string, (Func<Player, float>, Action<Player, float>)>
            {
                ["MaxCarryWeight"]    = (p => p.m_maxCarryWeight,    (p, v) => p.m_maxCarryWeight = v),
                ["BaseHp"]            = (p => p.m_baseHP,            (p, v) => p.m_baseHP = v),
                ["BaseStamina"]       = (p => p.m_baseStamina,       (p, v) => p.m_baseStamina = v),
                ["StaminaRegen"]      = (p => p.m_staminaRegen,      (p, v) => p.m_staminaRegen = v),
                ["StaminaRegenDelay"] = (p => p.m_staminaRegenDelay, (p, v) => p.m_staminaRegenDelay = v),
                ["DodgeStamina"]      = (p => p.m_dodgeStaminaUsage, (p, v) => p.m_dodgeStaminaUsage = v),

                // Speed joined the ledger with the Marching Song (2026-09-28). Fleet-footed used to
                // snapshot these two and put the snapshot back, which is exactly the shape that
                // cannot share a field: a song sung while Fleet-footed was held would be wiped by
                // Fleet-footed's restore, or would restore Fleet-footed's boost as "original" and
                // leave it on the character for good. Two lenders on one field is what this table
                // is for.
                ["RunSpeed"]          = (p => p.m_runSpeed,          (p, v) => p.m_runSpeed = v),
                ["WalkSpeed"]         = (p => p.m_walkSpeed,         (p, v) => p.m_walkSpeed = v),
            };

        /// <summary>
        /// What each boon lends. Several rows may share a boon id (Tireless lends four fields) and
        /// several may share a FIELD (Hearty and Glass Cannon both move base health) — the ledger
        /// handles both, which the previous design could not.
        /// </summary>
        private static readonly List<FieldLoan> FieldLoans = new List<FieldLoan>
        {
            new FieldLoan { BoonId = "mule",   Field = "MaxCarryWeight", Amount = MuleCarryWeightBonus },
            new FieldLoan { BoonId = "hearty", Field = "BaseHp",         Amount = HeartyBaseHpBonus },

            // Tireless (alpha34): the merge of what used to be Enduring, Vigorous, Cat's Breath and
            // Acrobat. Five stamina boons competed for slots against a problem the run's baseline
            // already solves; Marathoner's run-drain cut was dropped outright rather than folded in,
            // since baseline move stamina x0.5 IS that boon.
            new FieldLoan { BoonId = "tireless", Field = "BaseStamina",       Amount = EnduringBaseStaminaBonus },
            new FieldLoan { BoonId = "tireless", Field = "StaminaRegen",      Amount = 3f },    // vanilla ~6/s -> ~9/s
            new FieldLoan { BoonId = "tireless", Field = "StaminaRegenDelay", Amount = -0.5f }, // vanilla ~1s -> 0.5s
            new FieldLoan { BoonId = "tireless", Field = "DodgeStamina",      Amount = -5f },   // vanilla ~10 -> ~5

            // Glass Cannon's cost, on the same field Hearty raises — the pair the ledger exists for.
            new FieldLoan { BoonId = "glasscannon", Field = "BaseHp", Amount = -GlassCannonBaseHpPenalty },

            // The ways' passives that carry a stat as well as skills (2026-09-28). Their skill half
            // is in SkillBoons; Apply runs both for these ids.
            new FieldLoan { BoonId = "hirdman",   Field = "BaseHp",         Amount = HirdmanBaseHpBonus },
            new FieldLoan { BoonId = "craftsman", Field = "MaxCarryWeight", Amount = CraftsmanCarryWeightBonus },
        };

        /// <summary>
        /// The run's outstanding loans against Player fields, and the player they were taken from.
        /// A respawn hands back a Player with vanilla fields, so a new player means the ledger is
        /// forgotten and rebuilt rather than restored into someone else's numbers.
        /// </summary>
        private readonly LoanLedger _loans = new LoanLedger();
        private Player _loanOwner;

        /// <summary>
        /// The lender id used by the per-completion health reward. Not a boon — it is the run itself
        /// lending — but it shares the mechanism, and sharing it is the point: three claimants on
        /// base health now compose instead of corrupting each other.
        /// </summary>
        internal const string TaskRewardLender = "taskreward";

        // sharp: keyed by the SHARED damage block (ItemDrop.ItemData.m_shared), not the ItemData
        // instance. m_shared is per-PREFAB, not per-instance — a fresh ItemData handed out after
        // respawn still points at the same SharedData object the pre-death item used. Keying by
        // instance would treat that as "new gear", re-snapshot an already-1.2x'd value, and stack
        // to 1.44x, with Unapply later stomping the prefab's true original with whichever
        // snapshot happened to restore last. Keying by the shared block itself makes "already
        // boosted" a property of the block, not the transient instance pointing at it.
        private readonly Dictionary<ItemDrop.ItemData.SharedData, HitData.DamageTypes> _sharpSnapshots =
            new Dictionary<ItemDrop.ItemData.SharedData, HitData.DamageTypes>();

        private struct PugilistSnapshot
        {
            public float Primary, Secondary;                 // m_attackStamina
            public float PrimaryDraw, SecondaryDraw;         // m_drawStaminaDrain (bows)
            public float PrimaryReload, SecondaryReload;     // m_reloadStaminaDrain (crossbows)
        }

        /// <summary>Ranged keeps SOME cost — "bows should only drain a little" — so archery
        /// stays a resource decision without being a chore. Melee/tools are fully free.</summary>
        private const float RangedStaminaFraction = 0.25f;

        private readonly Dictionary<ItemDrop.ItemData.SharedData, PugilistSnapshot> _pugilistSnapshots =
            new Dictionary<ItemDrop.ItemData.SharedData, PugilistSnapshot>();

        /// <summary>Set by a failed Activate() with a boon-specific reason; null means "not ready" is generic enough.</summary>
        public string LastActivationMessage { get; private set; }

        /// <summary>
        /// How many times Windfall may be spent in a run. Deliberately a constant rather than
        /// config: the boon's DESCRIPTION states the number, and the description is built from this
        /// same constant — a config value could not reach it, and a number the card and the code
        /// disagreed about would be worse than one that cannot be tuned without a rebuild.
        /// </summary>
        public const int WindfallCharges = 3;

        /// <summary>
        /// Hands the player an item stack — RunService.GrantItem, which resolves the prefab, adds
        /// what fits, drops the rest at their feet, and logs either way. Windfall goes through the
        /// host for the same reason the skill boons do: the awkward parts (a full inventory, a
        /// prefab that won't resolve) are already solved there and solved once.
        /// </summary>
        private readonly Action<string, int> _grantItem;

        /// <summary>
        /// The lightning effect prefab the Stormward and Thor's bow already resolve — SagaItems owns
        /// the candidate list and the log line saying which name won, so Wrath borrows the answer
        /// rather than keeping a second list that could disagree with the first. May return null
        /// (no scene yet, or no candidate resolved); Wrath then strikes without a flash.
        /// </summary>
        private readonly Func<GameObject> _lightningFx;

        /// <summary>
        /// Switches the world's free-building key on or off — RunService, through WorldModifiers,
        /// which records the key's pre-run state with the run's other world-key originals and
        /// persists it in the save. Answers false when it would not act: the world already builds
        /// for free on its own (the key is the world's, not ours to switch), or there is no world.
        ///
        /// Master's Minute goes through the host for the reason the rate boons do: a global key is
        /// saved with the WORLD, so its original has to live where RestoreAll can reach it after a
        /// crash, not in a field of this class that dies with the process.
        /// </summary>
        private readonly Func<bool, bool> _setFreeBuild;

        public BoonEffects(Func<IReadOnlyList<HeldBoon>> heldBoons, Func<IEnumerable<string>> undefeatedBossLocations,
            Func<int> defeatedBossCount = null, Action<Skills.SkillType, float> loanSkill = null,
            Action<string, int> grantItem = null, Func<GameObject> lightningFx = null,
            Func<bool, bool> setFreeBuild = null, Func<int, string> stepBowElement = null,
            Action resetBowElement = null)
        {
            _heldBoons = heldBoons ?? (() => Array.Empty<HeldBoon>());
            _undefeatedBossLocations = undefeatedBossLocations ?? (() => Enumerable.Empty<string>());
            _defeatedBossCount = defeatedBossCount ?? (() => 0);
            _loanSkill = loanSkill ?? ((_, __) => { });
            _grantItem = grantItem ?? ((_, __) => { });
            _lightningFx = lightningFx ?? (() => null);
            _setFreeBuild = setFreeBuild ?? (_ => false);
            _stepBowElement = stepBowElement ?? (_ => null);
            _resetBowElement = resetBowElement ?? (() => { });
        }

        /// <summary>
        /// Elemental Arrows: moves Thor's bow one element along the cycle (+1 forward, -1 back) and
        /// answers the element's name, or null when it could not. Through the host, for the reason
        /// the lightning effect is: the bow's numbers live in SagaItems, and the run persists the
        /// choice - this class only knows that a key was pressed.
        /// </summary>
        private readonly Func<int, string> _stepBowElement;

        /// <summary>Thor's bow back to lightning, what it ships as. Idempotent.</summary>
        private readonly Action _resetBowElement;

        // --- Public surface (RunService's boon seams) ---

        public void Apply(string boonId)
        {
            switch (boonId)
            {
                case "fleet":
                    ApplyFleet();
                    break;

                case "sharp":
                    ApplySharp();
                    break;

                case "woodsman":
                case "hunter":
                case "warrior":
                case "miner":
                case "wayfarer":
                case "steady":
                case "poet":
                case "seafarer":
                    ApplySkillBoon(boonId);
                    break;

                case "hirdman":
                case "craftsman":
                    // Two halves each: skills through the host's loan (SkillBoons), and one Player
                    // field through the ledger (FieldLoans). Both are re-run safely on respawn.
                    ApplySkillBoon(boonId);
                    ApplyFieldBoost(boonId);
                    break;

                case "irongut":
                case "coldblood":
                case "fireblood":
                case "thickskin":
                case "hardshell":
                case "reckless":
                    ApplyDamageModifier(boonId);
                    break;

                case "stoker":
                    // The gain half only. The cost — heat rising faster — is the host's, applied
                    // where Slow Burn's discount is (RunService.AddHeat), because heat is not a
                    // thing this class can write.
                    ApplyWeaponMultiplier(StokerDamageMultiplier, "stoker");
                    break;

                case "kindling":
                    // Nothing to apply. It doubles what each FUTURE completion lends
                    // (RunService.GrantCompletionHealth), so the loan it enlarges is the task-health
                    // reward's own and is repaid with it. Listed so a reader looking for its effect
                    // finds this note rather than concluding it was forgotten.
                    break;

                case "glasscannon":
                    // Two halves: the cost is a field boost (see the table), the gain rides
                    // Sharpened's snapshot so the two can never both claim the same weapon.
                    ApplyFieldBoost(boonId);
                    ApplyWeaponMultiplier(GlassCannonDamageMultiplier, "glasscannon");
                    break;

                case "shepherd":
                    // Rides the legacy PetBuff statics rather than IPetService: the legacy world is
                    // the one that is actually ticked, and — more to the point — it is the one with
                    // a ResetPetBuffs. Power is loaned, so an effect with no way back is not an
                    // option.
                    // Through the legacy god-mode bracket, like every other effect that rides
                    // the GM statics. Unbracketed, RequireGodMode refused in every fair run —
                    // the boon was a SILENT NO-OP that printed a GM warning, which is how it was
                    // finally caught: "I was choosing a boon and then it showed a message from
                    // my gm mod."
                    //
                    // quiet: true, because the GM readouts are addressed to somebody who typed a
                    // command. A saga player who picked a boon and had no animals yet was told
                    // "No baseline, nothing buffed" - a diagnostic reading as a broken quest
                    // (owner: "the shephard quest says 'no baseline', which is confusing for a
                    // player"). This class says nothing to the player by design; the boon grant is
                    // announced where boons are announced, and the blessing finding an empty pen
                    // is not news - RefreshShepherd blesses whatever is tamed later.
                    try { WithLegacyGodModeBracket(() => PetBuff.BuffAllPets(false, quiet: true)); }
                    catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Shepherd: {e.Message}"); }
                    break;

                case "forgefed":
                    // Nothing to apply on gain — its multiplier is a function of live heat, and the
                    // host re-applies it on every heat change (see RefreshForgeFed).
                    break;

                // bloodthirst/relentless have no effect on gain either: they act on the kill hook,
                // which the host routes to OnKill.

                case "tracker":
                    // Nothing to apply. Hunter's Eye is a panel RunWindow draws while the boon is
                    // held (see HoldsTracker) — pure observation, so there is no state to set here
                    // and nothing to unwind when it is lost. Listed anyway so a reader looking for
                    // its effect finds this note instead of concluding it was forgotten.
                    break;

                case "way":
                    // One charge on the pick; the run's boss kills grant the rest (see
                    // RunService.RechargeWaystone). Targets the NEWEST held entry rather than the
                    // first match, which costs nothing and keeps this correct if a duplicate ever
                    // reaches the held list again.
                    var held = FindNewestHeld("way");
                    if (held != null) held.Charges++;
                    break;

                case "windfall":
                    // Charges given at the pick, and unlike Waystone nothing ever refills them. The
                    // boon is a fixed number of windfalls you choose the moments for, not a tap.
                    //
                    // Was one charge, by ruling, until it was played (owner: "it would be nice with
                    // a boon x charges of replenish stacks"). Three keeps the "choose the moment"
                    // decision that made it interesting — a tap would remove it — while making the
                    // pick worth a slot against a permanent passive.
                    var windfall = FindNewestHeld("windfall");
                    if (windfall != null) windfall.Charges += WindfallCharges;
                    break;

                case "study":
                case "bounty":
                    // Nothing to apply here. Both ride a world-modifier key — SkillGainRate and
                    // ResourceRate — which only the host can write, and must re-write after its own
                    // baseline pass: see RunService.RefreshRateBoons. Listed so a reader looking for
                    // their effect finds this note rather than concluding it was forgotten.
                    break;

                // wind/ember have no effect on gain — only on activation (Keypad4/5).

                default:
                    // mule/hearty/enduring, which are one Player field each; anything else
                    // (wind/ember, an id from a newer save) finds no match and does nothing.
                    ApplyFieldBoost(boonId);
                    break;
            }
        }

        public void Unapply(string boonId)
        {
            switch (boonId)
            {
                case "fleet":
                    UnapplyFleet();
                    break;

                case "sharp":
                    UnapplySharp();
                    break;

                case "wind":
                    // Losing the boon (e.g. death) mid-window must not leave the AoE heal running.
                    ForceAoeRenewalOff();
                    break;

                case "ember":
                    ForceCloakOff();
                    break;

                case "unseen":
                    // Losing the boon mid-window (a death, most likely) must not leave the player
                    // permanently unseen - the flag is ours and nothing else would turn it back.
                    ForceGhostOff();
                    break;

                case "irongut":
                case "coldblood":
                case "fireblood":
                case "thickskin":
                case "hardshell":
                case "reckless":
                    UnapplyDamageModifier(boonId);
                    break;

                case "stoker":
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "glasscannon":
                    UnapplyFieldBoost(boonId);
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "rage":
                    // Losing Blood Rage mid-window (a death, most likely) takes the whole window
                    // with it: the pending off, the damage and the cost. Nothing on gain matched
                    // this — rage is an ACTIVE — so this is the only unwind besides the timer.
                    EndRage();
                    break;

                // The four ways' timed actives (2026-09-28). A way's boon is never taken by death,
                // so these are reached from the dev Revoke and from UnapplyAll's held-boon loop -
                // and each End is idempotent, because UnapplyAll's finally calls them again.
                case "bulwark":       EndBulwark(); break;
                case "laststand":     EndLastStand(heal: false); break;
                case "march":         EndMarch(); break;
                case "warsong":       EndWarsong(); break;
                case "tide":          EndTide(); break;
                case "fairwind":      EndFairWind(); break;
                case "sealegs":       EndSeaLegs(); break;
                case "fieldforge":    TakeDownFieldForge(); break;
                case "mastersminute": EndMastersMinute(); break;
                case "reinforce":     EndReinforce(); break;

                // The bow outlives the boon and ships as lightning; losing the switch (the dev
                // class cycle, or the run's end through UnapplyAll) puts it back there.
                case "elemental":     _resetBowElement(); break;

                case "shepherd":
                    try { WithLegacyGodModeBracket(() => PetBuff.ResetPetBuffs(quiet: true)); }
                    catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Shepherd reset: {e.Message}"); }
                    break;

                case "forgefed":
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "brother":
                    // Losing the boon takes its summons with it — a death that costs you
                    // Packbrother must not leave the pack fighting on. The held-list check is
                    // belt and braces: an offer no longer lists a boon the player already holds,
                    // so a second Packbrother should not arise, and BoonEngine removes the entry
                    // before raising Lost — anything still held here would be a genuine duplicate.
                    var stillHeld = _heldBoons();
                    // All companions leave only when NO summoning boon remains — losing the
                    // wolves must not evict the skeletons or the menagerie beast.
                    if (stillHeld == null || !stillHeld.Any(h =>
                            h.Def.Id == "brother" || h.Def.Id == "bonecaller" || h.Def.Id == "menagerie"))
                        DespawnAllCompanions();
                    break;

                // way: charges are just data — nothing to unwind.

                default:
                    // mule/hearty/enduring put their snapshotted Player field back; way and
                    // any unknown id fall through here and do nothing.
                    UnapplyFieldBoost(boonId);
                    break;
            }
        }

        /// <param name="reverse">
        /// Only Elemental Arrows reads it: its two keys cycle the bow's element forward and back.
        /// Every other active has one key and ignores it.
        /// </param>
        public bool Activate(string boonId, bool reverse = false)
        {
            LastActivationMessage = null;
            switch (boonId)
            {
                case "elemental": return ActivateElemental(reverse ? -1 : 1);
                case "wind": return ActivateWind();
                case "ember": return ActivateEmber();
                case "way": return ActivateWay();
                case "brother": return ActivateBrother();
                case "bonecaller": return ActivateBonecaller();
                case "menagerie": return ActivateMenagerie();
                case "windfall": return ActivateWindfall();
                case "shaman": return ActivateShamanHeal();
                case "unseen": return ActivateUnseen();
                case "rage": return ActivateRage();
                case "rend": return ActivateRend();
                case "warcry": return ActivateWarcry();
                case "wrath": return ActivateWrath();
                case "mend": return ActivateMend();
                case "farsight": return ActivateFarsight();
                case "bash": return ActivateBash();
                case "bulwark": return ActivateBulwark();
                case "laststand": return ActivateLastStand();
                case "march": return ActivateMarch();
                case "warsong": return ActivateWarsong();
                case "bragi": return ActivateBragi();
                case "tide": return ActivateTide();
                case "fairwind": return ActivateFairWind();
                case "sealegs": return ActivateSeaLegs();
                case "fieldforge": return ActivateFieldForge();
                case "mastersminute": return ActivateMastersMinute();
                case "reinforce": return ActivateReinforce();
                default: return false;
            }
        }

        /// <summary>
        /// Fires every pending timed effect immediately and unwinds every currently-held boon.
        /// Called on run finish/abandon/failed-resume — a cheat toggle or a snapshot boost must
        /// never survive past the run that granted it. Each per-boon unapply is isolated so one
        /// throwing boon can't stop the rest from being cleaned up; the toggle safety clears run
        /// in a finally so they fire even if something above throws.
        /// </summary>
        public void UnapplyAll()
        {
            try
            {
                // Last Stand FIRST, and without its heal: its pending off is the one timer whose Off
                // gives something (half your health back), and a run that is being abandoned or
                // failed must not hand the player a heal on the way out. EndLastStand removes its
                // own pending entry, so the flush below never reaches it.
                SafeInvoke(() => EndLastStand(heal: false));

                foreach (var pending in _pending.ToList()) SafeInvoke(pending.Off);
                _pending.Clear();

                var held = _heldBoons();
                if (held != null)
                {
                    foreach (var h in held.ToList())
                    {
                        string id = h.Def.Id;
                        SafeInvoke(() => Unapply(id));
                    }
                }
            }
            finally
            {
                ForceAoeRenewalOff();
                ForceCloakOff();
                // Same reasoning, and the worst of the three to get wrong: a run that ended with
                // the window open must not leave the player walking through a world that cannot
                // see them.
                ForceGhostOff();
                // Blood Rage: the pending flush above normally ends it, and the weapon/modifier
                // sweeps below would catch its halves anyway. Explicit so the rage window is
                // provably closed on a path where an earlier step threw.
                SafeInvoke(EndRage);
                // A borrowed lightning flash still standing when the run ends goes with it.
                SafeInvoke(TakeDownWrathFlashes);
                // And Thor's bow goes back to its own lightning: it outlives the run, and the run's
                // fire or frost is not the bow's to keep.
                SafeInvoke(_resetBowElement);
                // The four ways' windows, each provably closed on a path where an earlier step
                // threw - the same reasoning as Blood Rage above. The two that touch something other
                // than the player come first: the world's wind and the world's free-building key are
                // not the character's, and a run must not leave either behind.
                SafeInvoke(() => EndLastStand(heal: false));
                SafeInvoke(EndFairWind);
                SafeInvoke(EndMastersMinute);
                SafeInvoke(EndReinforce);
                SafeInvoke(TakeDownFieldForge);
                SafeInvoke(EndSeaLegs);
                SafeInvoke(EndBulwark);
                SafeInvoke(EndWarsong);
                SafeInvoke(EndMarch);
                SafeInvoke(EndTide);
                // Pugilist is run baseline rather than a held boon, so the held-boon loop above
                // never reaches it — unwind it here so weapon stamina costs always come back.
                SafeInvoke(UnapplyPugilist);
                // Belt and braces for the summons: the loop above already unwinds "brother" when
                // it is held, but a run can end with companions alive and the boon already lost.
                SafeInvoke(DespawnAllCompanions);
                // Same reasoning for weapon damage: it is written into a PER-PREFAB shared block,
                // so anything left boosted here outlives the run and the character. The per-boon
                // unwinds above should have cleared it; this makes sure.
                SafeInvoke(UnapplyWeaponMultipliers);
                SafeInvoke(UnapplyAllDamageModifiers);
                // And every borrowed Player field, including the per-completion health reward,
                // which is not a boon and so is not reached by the held-boon loop above.
                SafeInvoke(RepayAllFieldLoans);
            }
        }

        /// <summary>Advances timed ON/OFF windows. Call once per frame while a run is active.</summary>
        public void Tick(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var entry = _pending[i];
                entry.Remaining -= dt;
                if (entry.Remaining <= 0f)
                {
                    _pending.RemoveAt(i);
                    SafeInvoke(entry.Off);
                }
                else
                {
                    _pending[i] = entry;
                }
            }

            // The windows that must be HELD rather than set once: something in the game keeps
            // undoing them every frame. See each method for what.
            SafeInvoke(HoldLastStand);
            SafeInvoke(HoldSeaLegs);
            SafeInvoke(SteerFairWind);
        }

        // --- fleet ---

        /// <summary>
        /// Fleet-footed: run and walk speed up by the config's increment, twice.
        ///
        /// Through the field ledger since 2026-09-28, not a snapshot of its own: the Marching Song
        /// lends against the same two fields, and a snapshot-and-restore cannot share a field with
        /// anyone (see FieldAccess). The ledger replaces rather than adds on a second Apply, so the
        /// respawn re-apply cannot stack it either. Jump was snapshotted before but never changed,
        /// so there is nothing of it to carry over.
        /// </summary>
        private void ApplyFleet()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            SyncLoanOwner(player);

            float increment = Resolve<IConfiguration>()?.SpeedIncrement ?? 0.5f;
            float boost = increment * FleetSpeedIncrements;
            LendField(player, "RunSpeed", "fleet", boost);
            LendField(player, "WalkSpeed", "fleet", boost);
        }

        private void UnapplyFleet() => RepayLender("fleet");

        // --- mule / hearty / enduring (single-field passives) ---

        /// <summary>
        /// Records this boon's loans against the player's fields and writes the new values.
        ///
        /// Re-applying against the SAME player is harmless: <see cref="LoanLedger.Lend"/> replaces
        /// rather than adds, and the value is recomputed from the pristine original either way. That
        /// matters because RunService's respawn detector re-applies every held passive whenever the
        /// Player reference changes.
        /// </summary>
        private void ApplyFieldBoost(string boonId)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            SyncLoanOwner(player);

            // EVERY row with this id, not just the first: a boon may lend more than one field, and
            // Tireless lends four. A first-match lookup would silently apply a quarter of it.
            foreach (var loan in FieldLoans.Where(l => l.BoonId == boonId))
            {
                LendField(player, loan.Field, boonId, loan.Amount);
            }
        }

        /// <summary>Withdraws every loan this boon made and rewrites the affected fields.</summary>
        private void UnapplyFieldBoost(string boonId) => RepayLender(boonId);

        /// <summary>
        /// Points the ledger at the current player, forgetting it wholesale if that is a DIFFERENT
        /// player than the loans were taken from.
        ///
        /// Death hands back a Player with vanilla fields, so the old originals describe someone who
        /// no longer exists — keeping them would mean restoring one character's numbers onto
        /// another's, handing out or confiscating a permanent bonus.
        /// </summary>
        private void SyncLoanOwner(Player player)
        {
            if (ReferenceEquals(_loanOwner, player)) return;

            _loans.Clear();
            _loanOwner = player;
        }

        /// <summary>Records one loan and writes the field's recomputed value.</summary>
        private void LendField(Player player, string field, string lender, float amount)
        {
            if (!FieldAccess.TryGetValue(field, out var access)) return;

            _loans.SetOriginal(field, access.Get(player));   // first lender only; ignored thereafter
            _loans.Lend(field, lender, amount);
            access.Set(player, _loans.Value(field));
        }

        /// <summary>
        /// Lends a FRACTION of the field's pristine value - the Marching Song's "a quarter faster".
        /// Measured from the ledger's original, never from the live value, so a song sung while
        /// Fleet-footed is held is a quarter of the player's own pace, not a quarter of the boosted
        /// one, and the result does not depend on which was taken first.
        /// </summary>
        private void LendFieldFraction(Player player, string field, string lender, float fraction)
        {
            if (!FieldAccess.TryGetValue(field, out var access)) return;

            _loans.SetOriginal(field, access.Get(player));   // first lender only; ignored thereafter
            LendField(player, field, lender, _loans.Original(field) * fraction);
        }

        /// <summary>
        /// Withdraws every loan a lender made, rewriting each affected field from its original plus
        /// whatever OTHER lenders still have outstanding — which is the whole reason the ledger
        /// exists. Repaying Hearty must not take Glass Cannon's contribution with it.
        /// </summary>
        private void RepayLender(string lender)
        {
            var fields = _loans.FieldsLentBy(lender).ToList();
            if (fields.Count == 0) return;

            var owner = _loanOwner;
            foreach (var field in fields) _loans.Repay(field, lender);

            // Unity's == (not ReferenceEquals): a destroyed player must read as null, since writing
            // fields on it is pointless and a fresh one already has vanilla values.
            if (owner == null) return;

            foreach (var field in fields)
            {
                if (FieldAccess.TryGetValue(field, out var access)) access.Set(owner, _loans.Value(field));
            }
        }

        /// <summary>
        /// Hands every borrowed field back at once. The run is over; nothing is owed.
        ///
        /// Assigning the pristine original directly rather than repaying lender by lender: at this
        /// point the correct end state is known exactly, and it cannot be got wrong by a lender the
        /// loop happens to miss.
        /// </summary>
        internal void RepayAllFieldLoans()
        {
            var owner = _loanOwner;
            _loanOwner = null;

            if (owner != null)
            {
                foreach (var field in _loans.Fields.ToList())
                {
                    if (FieldAccess.TryGetValue(field, out var access)) access.Set(owner, _loans.Original(field));
                }
            }

            _loans.Clear();
        }

        /// <summary>
        /// Sets the run's accumulated per-completion health reward. Called by the host with the
        /// running total, not an increment — re-lending replaces, so this cannot compound however
        /// often it is called.
        /// </summary>
        public void SetTaskHealthReward(float total)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            SyncLoanOwner(player);

            if (total <= 0f) RepayLender(TaskRewardLender);
            else LendField(player, "BaseHp", TaskRewardLender, total);
        }

        // --- sharp ---

        /// <summary>
        /// Every live weapon-damage multiplier, by boon id. THREE boons multiply weapon damage now
        /// — Sharpened, Glass Cannon and Forge-fed — and they must compose, so each registers a
        /// factor here and the actual damage is always recomputed from the pristine snapshot as
        /// the PRODUCT of them all.
        ///
        /// The alternative, each boon scaling whatever it found, is the compounding bug the sharp
        /// snapshot was written to avoid: two boons applying in an order nobody controls, and the
        /// first Unapply stomping the prefab's true original with a partly-boosted value.
        /// </summary>
        private readonly Dictionary<string, float> _weaponMultipliers = new Dictionary<string, float>();

        private void ApplySharp() => ApplyWeaponMultiplier(SharpDamageMultiplier, "sharp");

        private void UnapplySharp() => RemoveWeaponMultiplier("sharp");

        /// <summary>Registers (or updates) one boon's weapon-damage factor and re-applies the product.</summary>
        private void ApplyWeaponMultiplier(float multiplier, string boonId = "glasscannon")
        {
            _weaponMultipliers[boonId] = multiplier;
            RefreshWeaponDamage();
        }

        private void RemoveWeaponMultiplier(string boonId)
        {
            if (!_weaponMultipliers.Remove(boonId)) return;

            if (_weaponMultipliers.Count == 0) UnapplyWeaponMultipliers();
            else RefreshWeaponDamage();
        }

        /// <summary>
        /// Rewrites every equipped weapon's damage as its ORIGINAL times the product of the live
        /// multipliers.
        ///
        /// Always from the original, never from the current value — which is what makes this safe to
        /// call as often as we like, and is what lets Forge-fed change with heat rather than
        /// ratcheting upward.
        ///
        /// Also run on the poll tick, so a weapon crafted or equipped after the boon was taken is
        /// covered. Sharpened did not do that before alpha34: it applied once at pick time, and a
        /// sword forged afterwards quietly missed out.
        /// </summary>
        internal void RefreshWeaponDamage()
        {
            var inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null || _weaponMultipliers.Count == 0) return;

            float product = 1f;
            foreach (var m in _weaponMultipliers.Values) product *= m;

            foreach (var item in inventory.GetEquippedItems())
            {
                if (item == null || !item.IsWeapon()) continue;

                var shared = item.m_shared;
                if (shared == null) continue;

                // Snapshot on first sight only. Keyed by the SHARED block rather than the ItemData
                // instance for the reason documented on _sharpSnapshots: m_shared is per-prefab, and
                // a fresh instance after respawn points at the same already-boosted block.
                if (!_sharpSnapshots.ContainsKey(shared))
                    _sharpSnapshots[shared] = DamageHelpers.Copy(shared.m_damages);
            }

            // Then write every block we have touched — the ones in hand, and the ones no longer in
            // hand. Before Blood Rage no factor
            // ever dropped mid-run except by losing a boon, so a weapon put away kept whatever the
            // product was when it was last held and nobody could tell. A fifteen-second factor makes
            // that visible: sheathe the axe mid-rage, draw it after, and the first swings before the
            // next poll would still land at x1.5. One product for every block we have touched keeps
            // "original times the live multipliers" true of all of them, not just the equipped ones.
            foreach (var kvp in _sharpSnapshots)
            {
                if (kvp.Key == null) continue;
                kvp.Key.m_damages = DamageHelpers.Scaled(kvp.Value, product);
            }
        }

        // --- Damage modifiers: resistances, and Reckless's cost ---

        /// <summary>
        /// The player's damage-modifier struct as it was before any boon touched it, and which
        /// boons are currently modifying it.
        ///
        /// One snapshot rather than one per boon: <see cref="Character.m_damageModifiers"/> is a
        /// single struct, so two boons each restoring "their" version would put back whichever ran
        /// last and silently discard the other. Instead the pristine copy is kept once and the live
        /// value is always recomputed from it.
        /// </summary>
        private HitData.DamageModifiers _damageModOriginal;
        private Player _damageModOwner;
        private readonly HashSet<string> _damageModBoons = new HashSet<string>();

        private void ApplyDamageModifier(string boonId)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // A respawn hands back a Player with vanilla modifiers, so a new player means a new
            // snapshot — the same reasoning the field boosts use.
            if (!ReferenceEquals(_damageModOwner, player))
            {
                _damageModOwner = player;
                _damageModOriginal = player.m_damageModifiers;
            }

            _damageModBoons.Add(boonId);
            RefreshDamageModifiers();
        }

        private void UnapplyDamageModifier(string boonId)
        {
            if (!_damageModBoons.Remove(boonId)) return;

            if (_damageModBoons.Count == 0) UnapplyAllDamageModifiers();
            else RefreshDamageModifiers();
        }

        /// <summary>
        /// Rewrites the player's modifiers as the ORIGINAL plus whatever the held boons say — never
        /// as an edit of the current value, so this is safe to run repeatedly and a removed boon
        /// leaves nothing behind.
        /// </summary>
        private void RefreshDamageModifiers()
        {
            var player = _damageModOwner;
            if (player == null) return;

            var mods = _damageModOriginal;

            // Every resistance RAISES to Resistant rather than assigning it (2026-09-28). Two claims
            // on one slot arrived with the ways - Shield Wall beside Thick-skinned, Sea Legs beside
            // Coldblooded - and an assignment would let whichever ran last decide; worse, it would
            // write Resistant over an original that was already better. The better of the two is
            // the only answer that means what both cards say.
            if (_damageModBoons.Contains("irongut")) RaiseToResistant(ref mods.m_poison);
            // Sea Legs holds frost at Resistant for its window, and that is the whole of how it
            // keeps the cold off: Player.UpdateEnvStatusEffects never applies Cold or Freezing to a
            // player whose frost modifier is Resistant (verified in the 1.0.16 IL).
            if (_damageModBoons.Contains("coldblood") || _damageModBoons.Contains("sealegs")) RaiseToResistant(ref mods.m_frost);
            if (_damageModBoons.Contains("fireblood")) RaiseToResistant(ref mods.m_fire);

            // The physical three. Thick-skinned and Hardshell (2026-09-27) each claim one; Shield
            // Wall (the Húskarl's rung 2) claims all three for its twenty seconds.
            bool wall = _damageModBoons.Contains("bulwark");
            bool thickskin = _damageModBoons.Contains("thickskin") || wall;
            bool hardshell = _damageModBoons.Contains("hardshell") || wall;
            bool slashResisted = wall;
            if (thickskin) RaiseToResistant(ref mods.m_blunt);
            if (hardshell) RaiseToResistant(ref mods.m_pierce);
            if (slashResisted) RaiseToResistant(ref mods.m_slash);

            // Reckless's cost. "Weak" is the game's own one-step-worse modifier, which is roughly
            // the stated 25% and, more importantly, is a value Valheim already balances around
            // rather than a number invented here.
            //
            // Blood Rage pays the same price, for its fifteen seconds only. It is not a separate
            // modifier because there is no separate armour to put it on: one struct, one snapshot,
            // and both claims collapse to the same Weak — holding Reckless and raging at once costs
            // no more than either, which is the honest reading of "one step worse".
            //
            // Thick-skinned and Hardshell were the first boons to claim the same slots. Written as
            // "last one wins", holding Reckless would silently delete a resistance the player also
            // picked, and which of the two the player kept would depend on nothing they could see.
            // One step better and one step worse is no step at all, so a resisted type goes to
            // Normal under the cost rather than to Weak: both picks still mean what their cards say.
            if (_damageModBoons.Contains("reckless") || _damageModBoons.Contains("rage"))
            {
                mods.m_blunt = thickskin ? HitData.DamageModifier.Normal : HitData.DamageModifier.Weak;
                mods.m_slash = slashResisted ? HitData.DamageModifier.Normal : HitData.DamageModifier.Weak;
                mods.m_pierce = hardshell ? HitData.DamageModifier.Normal : HitData.DamageModifier.Weak;
            }

            player.m_damageModifiers = mods;
        }

        /// <summary>
        /// Sets a slot to Resistant unless it is already at least as good. The enum's order is not
        /// its strength (Normal, Resistant, Weak, Immune, Ignore, VeryResistant, VeryWeak,
        /// SlightlyResistant, SlightlyWeak - verified in the IL), so "better" is spelled out here.
        /// </summary>
        private static void RaiseToResistant(ref HitData.DamageModifier slot)
        {
            if (Protection(slot) < Protection(HitData.DamageModifier.Resistant))
                slot = HitData.DamageModifier.Resistant;
        }

        /// <summary>How much a modifier protects, weakest first. Ignore counts as total: nothing lands.</summary>
        private static int Protection(HitData.DamageModifier m)
        {
            switch (m)
            {
                case HitData.DamageModifier.VeryWeak: return 0;
                case HitData.DamageModifier.Weak: return 1;
                case HitData.DamageModifier.SlightlyWeak: return 2;
                case HitData.DamageModifier.Normal: return 3;
                case HitData.DamageModifier.SlightlyResistant: return 4;
                case HitData.DamageModifier.Resistant: return 5;
                case HitData.DamageModifier.VeryResistant: return 6;
                case HitData.DamageModifier.Immune: return 7;
                case HitData.DamageModifier.Ignore: return 8;
                default: return 3;
            }
        }

        /// <summary>Puts the pristine modifiers back and forgets every claim on them.</summary>
        private void UnapplyAllDamageModifiers()
        {
            var owner = _damageModOwner;
            _damageModBoons.Clear();
            _damageModOwner = null;

            // Unity's ==: a destroyed player reads as null, and a fresh one already has vanilla
            // modifiers, so there is nothing to put back.
            if (owner == null) return;

            owner.m_damageModifiers = _damageModOriginal;
        }

        // --- On-kill boons ---

        /// <summary>
        /// Called by the host for every non-player, non-tamed death while a run is active.
        ///
        /// The Character death hook already exists for the questline's kill steps, so these boons
        /// cost nothing structurally — which is most of why they were the cheapest new category to
        /// add. They are also the only boons in the pool that reward AGGRESSION rather than raising
        /// a number, which is what the pool was short of.
        /// </summary>
        public void OnKill()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var held = _heldBoons();
            if (held == null) return;

            foreach (var h in held)
            {
                switch (h.Def.Id)
                {
                    case "bloodthirst":
                        // Heal, never overheal: Player.Heal clamps to max health itself, so this
                        // cannot be used to bank health above the cap.
                        try { player.Heal(BloodthirstHealPerKill); }
                        catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Bloodthirst failed: {e.Message}"); }
                        break;

                    case "relentless":
                        try { player.AddStamina(RelentlessStaminaPerKill); }
                        catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Relentless failed: {e.Message}"); }
                        break;
                }
            }
        }

        // --- Forge-fed ---

        /// <summary>
        /// Re-scales weapon damage for the run's current heat. Called by the host on every heat
        /// change, and a no-op unless Forge-fed is held.
        ///
        /// This is the one boon whose strength MOVES, and it is only safe because the weapon
        /// mechanism recomputes from a pristine snapshot: registering a new factor and refreshing
        /// cannot ratchet, however many times heat changes. Capped so a pathological heat number
        /// cannot produce a silly multiplier.
        /// </summary>
        private float _hearthlightAt;

        /// <summary>
        /// Hearthlight: a slow mending pulse around the player, reaching tamed animals too.
        ///
        /// Implemented directly rather than through the legacy AoE-renewal statics — those gate
        /// on god mode and tick through PeriodicManager, and the Shepherd just demonstrated what
        /// riding an ungated legacy toggle from a boon costs. A heal is small enough to own.
        /// </summary>
        public void RefreshHearthlight(bool held)
        {
            if (!held || Time.time < _hearthlightAt) return;
            _hearthlightAt = Time.time + 5f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            try
            {
                player.Heal(4f);

                var list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 15f, list);
                foreach (var c in list)
                {
                    if (c == null || c.IsPlayer() || !c.IsTamed()) continue;
                    c.Heal(8f);
                }
            }
            catch { /* a missed pulse is a missed pulse */ }
        }

        /// <summary>
        /// Re-applies the shepherd's blessing to anything tamed SINCE it was granted.
        ///
        /// A passive applied once would only ever bless the animals you already had, and the whole
        /// point of Act I's hearth is that the pen grows. Cheap enough to run on the same slow
        /// tick that refreshes Forge-fed.
        /// </summary>
        public void RefreshShepherd(bool held)
        {
            if (!held) return;

            try { WithLegacyGodModeBracket(() => PetBuff.BuffAllPets(false, quiet: true)); }
            catch { /* a missed refresh is cosmetic; the next one catches it */ }
        }

        public void RefreshForgeFed(float heat)
        {
            var held = _heldBoons();
            if (held == null || !held.Any(h => h.Def.Id == "forgefed")) return;

            float multiplier = Mathf.Clamp(1f + Mathf.Max(0f, heat) * ForgeFedPerHeat, 1f, ForgeFedMaxMultiplier);
            ApplyWeaponMultiplier(multiplier, "forgefed");
        }

        /// <summary>Puts every weapon back and forgets every multiplier. The full unwind.</summary>
        private void UnapplyWeaponMultipliers()
        {
            foreach (var kvp in _sharpSnapshots)
            {
                var shared = kvp.Key;
                if (shared == null) continue; // guard: no longer reachable, nothing to restore
                shared.m_damages = kvp.Value;
            }
            _sharpSnapshots.Clear();
            _weaponMultipliers.Clear();
        }

        // --- pugilist ---

        /// <summary>Fully free: everything swung by hand — melee weapons AND tools. Ranged is
        /// handled separately at a reduced (not zero) cost; see RangedStaminaFraction.</summary>
        private static bool IsStaminaFreeSkill(Skills.SkillType skill)
        {
            return skill != Skills.SkillType.Bows
                && skill != Skills.SkillType.Crossbows;
        }

        /// <summary>
        /// Baseline empowerment, not a boon: applied for the whole run and re-run on the poll
        /// tick so freshly crafted or newly equipped gear is covered too (already-snapshotted
        /// shared blocks are skipped, so re-running is free and cannot stack).
        /// </summary>
        internal void ApplyPugilist()
        {
            var inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null) return;

            foreach (var item in inventory.GetEquippedItems())
            {
                if (item == null || !item.IsWeapon()) continue;

                var shared = item.m_shared;
                // Keyed by SharedData (per-prefab), like sharp: a fresh ItemData for the same
                // weapon after a respawn must not re-snapshot an already-zeroed cost.
                if (shared == null || _pugilistSnapshots.ContainsKey(shared)) continue;

                _pugilistSnapshots[shared] = new PugilistSnapshot
                {
                    Primary = shared.m_attack.m_attackStamina,
                    Secondary = shared.m_secondaryAttack.m_attackStamina,
                    PrimaryDraw = shared.m_attack.m_drawStaminaDrain,
                    SecondaryDraw = shared.m_secondaryAttack.m_drawStaminaDrain,
                    PrimaryReload = shared.m_attack.m_reloadStaminaDrain,
                    SecondaryReload = shared.m_secondaryAttack.m_reloadStaminaDrain
                };

                bool ranged = !IsStaminaFreeSkill(shared.m_skillType);
                float f = ranged ? RangedStaminaFraction : 0f;
                shared.m_attack.m_attackStamina *= f;
                shared.m_secondaryAttack.m_attackStamina *= f;
                shared.m_attack.m_drawStaminaDrain *= f;
                shared.m_secondaryAttack.m_drawStaminaDrain *= f;
                shared.m_attack.m_reloadStaminaDrain *= f;
                shared.m_secondaryAttack.m_reloadStaminaDrain *= f;
            }
        }

        internal void UnapplyPugilist()
        {
            foreach (var kvp in _pugilistSnapshots)
            {
                var shared = kvp.Key;
                if (shared == null) continue;
                shared.m_attack.m_attackStamina = kvp.Value.Primary;
                shared.m_secondaryAttack.m_attackStamina = kvp.Value.Secondary;
                shared.m_attack.m_drawStaminaDrain = kvp.Value.PrimaryDraw;
                shared.m_secondaryAttack.m_drawStaminaDrain = kvp.Value.SecondaryDraw;
                shared.m_attack.m_reloadStaminaDrain = kvp.Value.PrimaryReload;
                shared.m_secondaryAttack.m_reloadStaminaDrain = kvp.Value.SecondaryReload;
            }
            _pugilistSnapshots.Clear();
        }

        // --- actives ---

        private bool ActivateWind()
        {
            var held = FindHeld("wind");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (CheatCommands.AOERenewalActive)
            {
                // Already on (ours or the player's) — nothing to (re)activate, don't burn the cooldown.
                LastActivationMessage = "AoE Renewal is already active.";
                return false;
            }

            // Flag set BEFORE the call, not after: ToggleAoeRenewal can flip the live static and
            // THEN throw (e.g. CheatVisualizer failing) — setting the flag first means a later
            // step throwing (scheduling, cooldown) can never skip it and strand an on-but-
            // unflagged effect. A throw from the toggle call itself rolls the flag back, since in
            // that case we can't tell whether the live flag actually flipped.
            _aoeRenewalOnByUs = true;
            try
            {
                WithLegacyGodModeBracket(CheatCommands.ToggleAoeRenewal);
            }
            catch
            {
                _aoeRenewalOnByUs = false;
                throw;
            }

            RemovePending("wind");
            SchedulePending("wind", WindOnSeconds, ForceAoeRenewalOff);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private bool ActivateEmber()
        {
            var held = FindHeld("ember");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (CheatCommands.CloakActive)
            {
                LastActivationMessage = "Cloak of Flames is already active.";
                return false;
            }

            // See ActivateWind for why this is set before the call, not after.
            _cloakOnByUs = true;
            try
            {
                WithLegacyGodModeBracket(CheatCommands.ToggleCloakOfFlames);
            }
            catch
            {
                _cloakOnByUs = false;
                throw;
            }

            RemovePending("ember");
            SchedulePending("ember", EmberOnSeconds, ForceCloakOff);
            LightEmberFlames();

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void LightEmberFlames()
        {
            SnuffEmberFlames();
            try
            {
                var player = Player.m_localPlayer;
                var odb = ObjectDB.instance;
                if (player == null || odb == null) return;

                var burning = odb.GetStatusEffect("Burning".GetStableHashCode());
                if (burning == null || burning.m_startEffects == null)
                {
                    Debug.Log("[ICanShowYouTheWorld] Emberskin: no Burning status effect to borrow flames from.");
                    return;
                }

                _emberFlames = burning.m_startEffects.Create(
                    player.transform.position, player.transform.rotation, player.transform, 1f, -1, ZDOID.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Emberskin flames failed: " + ex.Message);
            }
        }

        private void SnuffEmberFlames()
        {
            if (_emberFlames == null) return;
            foreach (var go in _emberFlames)
            {
                try { if (go != null) UnityEngine.Object.Destroy(go); } catch { }
            }
            _emberFlames = null;
        }

        /// <summary>
        /// The AoE heal the GM mod has cast for years, handed to a saga as an active.
        ///
        /// Rides CheatCommands.CastHealAOE rather than reimplementing it: the legacy statics are the
        /// pipeline that is actually ticked, and this one already resolves the prefab, places it and
        /// reports a missing one. It is gated on the legacy god-mode flag, which a run forces off, so
        /// it needs the same bracket Second Wind and Emberskin use - unbracketed it would refuse in
        /// every fair run while printing a GM warning, which is exactly how Shepherd was caught.
        ///
        /// A BURST, where Second Wind is a window: Second Wind turns AoE Renewal on for ten seconds
        /// and this is one cast that lands and is gone. That is the whole difference between them,
        /// and it is why both are worth a slot.
        /// </summary>
        private bool ActivateShamanHeal()
        {
            var held = FindHeld("shaman");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (Player.m_localPlayer == null)
            {
                LastActivationMessage = "Nothing here to mend.";
                return false;
            }

            WithLegacyGodModeBracket(CheatCommands.CastHealAOE);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Mending Hands: every damaged player-built piece within <see cref="MendRadius"/> goes back
        /// to full health.
        ///
        /// The GM mod's "repair all things" (CheatCommands.RepairStructuresAoE) is the ancestor, and
        /// the radius is its radius — but its loop is not reused, for three reasons that all matter
        /// to a boon and none to a cheat. It walks FindObjectsOfType&lt;Piece&gt;, a scene search per
        /// press; it fires RPC_Repair at every piece whether damaged or not and counts them all, so
        /// it can never say "nothing needed it"; and it would mend a dungeon's walls or a
        /// Fuling village as readily as the player's own house. WearNTear.Repair() — the game's own
        /// hammer path — answers all three: it returns false for anything already whole, and the
        /// Piece's creator says whether a player built it.
        ///
        /// A press that mends nothing refuses rather than firing, the same policy as Rend: a
        /// three-minute cooldown spent on a sound house is a punishment for checking.
        ///
        /// Not a loan, and nothing to unwind: a repaired wall is what the hammer would have made of
        /// it with a trip to the workbench. The boon saves the walk, not the wood's worth.
        /// </summary>
        private bool ActivateMend()
        {
            var held = FindHeld("mend");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;

            int mended = 0;
            try
            {
                Vector3 center = player.transform.position;
                float r2 = MendRadius * MendRadius;

                // A copy: Repair raises an RPC, and a piece that is destroyed mid-walk rearranges
                // the game's list under us (OnDestroy swaps the last entry into its slot).
                foreach (var wnt in WearNTear.GetAllInstances().ToList())
                {
                    if (wnt == null) continue;
                    if ((wnt.transform.position - center).sqrMagnitude > r2) continue;

                    var piece = wnt.GetComponent<Piece>();
                    if (piece == null || !piece.IsPlacedByPlayer()) continue;

                    if (wnt.Repair()) mended++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Mending Hands failed: " + ex.Message);
                if (mended == 0) return false;
            }

            if (mended == 0)
            {
                LastActivationMessage = "Nothing here needs mending.";
                return false;
            }

            LastActivationMessage = mended == 1 ? "One piece made whole." : $"{mended} pieces made whole.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Minimap.Explore(Vector3, float) — the method the game's own walking reveal calls every
        /// few seconds with m_exploreRadius. Private, so found by reflection, and by its full
        /// signature: there is a private Explore(int, int) beside it, and a lookup by name alone
        /// would be ambiguous. Resolved once; null if a game update renames it.
        /// </summary>
        private static readonly System.Reflection.MethodInfo MinimapExplore =
            typeof(Minimap).GetMethod("Explore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(Vector3), typeof(float) }, null);

        /// <summary>
        /// Elemental Arrows: Thor's bow moves to its next element - lightning, fire, frost, and round.
        /// </summary>
        /// <remarks>
        /// A switch, not a spell: no cooldown, no charges, and it works with the bow in the pack as
        /// well as in the hand, so the player can set it before the fight rather than during it.
        /// What it changes is the KIND of damage, never the amount (see SagaItems.ApplyBowElement),
        /// which is why it needs no cooldown to stay honest. A loan like everything else a run
        /// grants: the run's end and the boon's loss both put the bow back to lightning.
        /// </remarks>
        private bool ActivateElemental(int step)
        {
            var held = FindHeld("elemental");
            if (held == null) return false;

            string element = _stepBowElement(step);
            if (string.IsNullOrEmpty(element))
            {
                LastActivationMessage = "Thor\u2019s bow does not answer.";
                return false;
            }

            LastActivationMessage = $"Thor\u2019s bow: {element}.";
            return true;
        }

        /// <summary>
        /// Farsight: the map within <see cref="FarsightRadius"/> of the player is revealed.
        ///
        /// Through the game's own reveal rather than by writing the fog texture here, so the
        /// explored bits and the texture cannot disagree and the result saves with the character
        /// exactly as walking there would have.
        ///
        /// The one boon whose effect is not a loan, and deliberately: what the player has seen they
        /// have seen. The map a run ends with is a map they could have walked, and taking fog back
        /// would mean remembering which pixels were ours — a record of a thing that is not power.
        /// </summary>
        private bool ActivateFarsight()
        {
            var held = FindHeld("farsight");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            if (player == null || map == null) return false;

            if (MinimapExplore == null)
            {
                // Said once in the log and to the player, rather than spending the cooldown on
                // nothing: the reason is a game update, and the player should hear it did nothing.
                Debug.LogWarning("[ICanShowYouTheWorld] Farsight: Minimap.Explore(Vector3, float) not found.");
                LastActivationMessage = "The land will not show itself.";
                return false;
            }

            try
            {
                MinimapExplore.Invoke(map, new object[] { player.transform.position, FarsightRadius });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Farsight failed: " + ex.Message);
                return false;
            }

            LastActivationMessage = "The land around you is known.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Ghost mode for a window, then off again. The mod's own ToggleGhostMode, bracketed and on
        /// a timer.
        ///
        /// The flag is set BEFORE the call for the reason ActivateWind spells out: the toggle can
        /// flip the live static and then throw, and a later step throwing must never be able to
        /// strand an on-but-unflagged effect. Here that would mean a player invisible for the rest
        /// of the run with nothing left that knows to undo it.
        /// </summary>
        private bool ActivateUnseen()
        {
            var held = FindHeld("unseen");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (CheatCommands.GhostMode)
            {
                LastActivationMessage = "You are already unseen.";
                return false;
            }

            _ghostOnByUs = true;
            try
            {
                WithLegacyGodModeBracket(CheatCommands.ToggleGhostMode);
            }
            catch
            {
                _ghostOnByUs = false;
                throw;
            }

            RemovePending("unseen");
            SchedulePending("unseen", UnseenOnSeconds, ForceGhostOff);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        // --- The ways' actives: Blood Rage, Rend, Warcry, Thor's Wrath ---

        /// <summary>
        /// Blood Rage: the Emberskin shape — switch on, schedule the off — carried by the weapon
        /// multiplier product and the damage-modifier snapshot, both of which already know how to
        /// give back exactly what they took.
        ///
        /// Discrete rather than per-frame on purpose. The boon design turned down "damage rises as
        /// health falls" because it would be a number moving every frame against the player's own
        /// health; fifteen seconds you chose to start is a decision, and a readable one.
        ///
        /// Recasting is refused while the window is open, like Emberskin and Unseen: a recast that
        /// merely restarted the timer would waste the cooldown for nothing the player could see.
        /// </summary>
        private bool ActivateRage()
        {
            var held = FindHeld("rage");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_weaponMultipliers.ContainsKey("rage"))
            {
                LastActivationMessage = "The rage is already on you.";
                return false;
            }

            if (Player.m_localPlayer == null) return false;

            try
            {
                // The cost first, then the gain: if the second half throws, EndRage below unwinds
                // whichever half landed, and the player is never left with the damage but not the
                // price.
                ApplyDamageModifier("rage");
                ApplyWeaponMultiplier(RageMultiplier, "rage");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Blood Rage failed: " + ex.Message);
                SafeInvoke(EndRage);
                return false;
            }

            RemovePending("rage");
            SchedulePending("rage", RageSeconds, EndRage);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Closes the rage window: the pending timer, the weapon factor, and the cost. Idempotent —
        /// each half is a no-op when it has nothing registered — because it is reached from the
        /// timer, from Unapply, from the pending flush and from UnapplyAll's finally, and on a bad
        /// day from more than one of them.
        /// </summary>
        private void EndRage()
        {
            RemovePending("rage");
            try { RemoveWeaponMultiplier("rage"); }
            finally { UnapplyDamageModifier("rage"); }
        }

        /// <summary>
        /// Rend: one sweep around the player, a cut and a bleed on everything hostile in reach.
        ///
        /// Built as a HitData and delivered through Character.Damage, never by writing health: the
        /// attacker is set, so the hit goes through RPC_Damage's own path — resistances, the
        /// "attacked by a player" bookkeeping, aggravation, the enemy-hit stat. The poison half is
        /// what the game turns into SE_Poison by itself (RPC_Damage zeroes it off the hit and calls
        /// AddPoisonDamage), so the bleed is the game's damage-over-time, not a status of ours.
        ///
        /// A sweep that finds nobody refuses rather than firing: a twenty-second cooldown spent on
        /// empty air is a punishment for pressing the key early, and it tells the player nothing.
        /// </summary>
        private bool ActivateRend()
        {
            var held = FindHeld("rend");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                var foes = HostilesNear(player.transform.position, RendRadius, player, skipBosses: false);
                if (foes.Count == 0)
                {
                    LastActivationMessage = "Nothing within reach.";
                    return false;
                }

                float scale = ClassDamageScale();
                Vector3 from = player.transform.position;
                foreach (var c in foes)
                {
                    var hit = new HitData();
                    hit.m_damage.m_slash = RendSlash * scale;
                    hit.m_damage.m_poison = RendPoison * scale;
                    AimHit(hit, c, from, player);
                    DamageOne(c, hit, "Rend");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Rend failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Warcry: CheatCommands.StaggerAoE's shape, aimed. The direction is away from the player —
        /// RPC_Stagger turns the target to face AGAINST the force, so every staggered foe ends up
        /// looking at the one who shouted, which is the picture a warcry should leave.
        ///
        /// Bosses are skipped, as the card says ("Not the gods"), and for a reason the card does not:
        /// RPC_Stagger does nothing but set an animator trigger, and a boss's animator may have no
        /// "stagger" state to go to. The game staggers bosses through accumulated stagger damage,
        /// which knows about them; this path does not.
        /// </summary>
        private bool ActivateWarcry()
        {
            var held = FindHeld("warcry");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                var foes = HostilesNear(player.transform.position, WarcryRadius, player, skipBosses: true);
                if (foes.Count == 0)
                {
                    LastActivationMessage = "Nothing within earshot to cow.";
                    return false;
                }

                Vector3 from = player.transform.position;
                foreach (var c in foes)
                {
                    try { c.Stagger(AwayFrom(from, c)); }
                    catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Warcry: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Warcry failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Thor's Wrath: lightning where the player is looking. The mod's first lightning HitData —
        /// everything else that strikes with lightning (the Stormward, the bow) borrows a weapon's
        /// attack and lets the game build the hit.
        ///
        /// The order is: find the point, find the foes, pay, strike, flash. Foes before cost so a
        /// strike at empty ground refuses without charging anything (the same rule Rend follows);
        /// cost before damage so a player who cannot pay gets nothing for free.
        ///
        /// The price is eitr when the player has enough of it and stamina otherwise. Eitr has no
        /// base value in the IL — it exists only while eitr food is eaten — so an eitr-only spell
        /// would be dead for every act before the Mistlands; stamina keeps it castable from the
        /// first rung, and eitr takes over once the player has some to give.
        /// </summary>
        private bool ActivateWrath()
        {
            var held = FindHeld("wrath");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                Vector3 point = WrathAimPoint(player);

                var foes = HostilesNear(point, WrathRadius, player, skipBosses: false);
                if (foes.Count == 0)
                {
                    LastActivationMessage = "Nothing there for the lightning.";
                    return false;
                }

                if (player.HaveEitr(WrathEitr)) player.UseEitr(WrathEitr);
                else if (player.HaveStamina(WrathStamina)) player.UseStamina(WrathStamina);
                else
                {
                    LastActivationMessage = "You have not the strength to call it down.";
                    return false;
                }

                float scale = ClassDamageScale();
                foreach (var c in foes)
                {
                    var hit = new HitData();
                    hit.m_damage.m_lightning = WrathLightning * scale;
                    hit.m_staggerMultiplier = WrathStaggerMultiplier;
                    AimHit(hit, c, point, player);
                    DamageOne(c, hit, "Thor's Wrath");
                }

                SpawnWrathFlash(point);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Thor's Wrath failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Where the player is looking, out to <see cref="WrathAimRange"/>: the nearest solid thing on
        /// the camera's forward ray that is not the player.
        ///
        /// GameCamera rather than the GM mod's Camera.main.ScreenPointToRay(Input.mousePosition):
        /// in play the cursor is locked to the centre anyway, but the GM helpers are written for a
        /// free cursor and a menu, and the camera's own forward is the crosshair without asking.
        /// RaycastAll, sorted, because the camera sits behind the player in third person and the
        /// first thing its ray meets is very often the player's own collider.
        /// </summary>
        private static Vector3 WrathAimPoint(Player player)
        {
            Vector3 fallback = player.transform.position + player.transform.forward * WrathFallbackDistance;

            var cam = GameCamera.instance;
            if (cam == null) return fallback;

            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain",
                "vehicle", "character", "character_net", "character_ghost", "character_noenv", "hitbox");

            var origin = cam.transform.position;
            var hits = Physics.RaycastAll(origin, cam.transform.forward, WrathAimRange, mask,
                QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return fallback;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var ch = h.collider.GetComponentInParent<Character>();
                if (ch != null && ReferenceEquals(ch, player)) continue;
                return h.point;
            }
            return fallback;
        }

        /// <summary>
        /// Everything within <paramref name="radius"/> of <paramref name="centre"/> that a class
        /// active may hurt: not a player, not tamed (which spares the player's own companions and
        /// the saga's tamed speakers), not already dead, and — for Warcry — not a boss.
        /// </summary>
        private static List<Character> HostilesNear(Vector3 centre, float radius, Player player, bool skipBosses)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(centre, radius, list);

            var foes = new List<Character>();
            foreach (var c in list)
            {
                if (c == null || ReferenceEquals(c, player)) continue;
                if (c.IsPlayer() || c.IsTamed() || c.IsDead()) continue;
                if (skipBosses && c.IsBoss()) continue;
                foes.Add(c);
            }
            return foes;
        }

        /// <summary>The hit's geometry and author: at the target's centre, pushing away from the
        /// source, and the player's — so kills, aggravation and stats follow the normal path.</summary>
        private static void AimHit(HitData hit, Character target, Vector3 source, Player player)
        {
            hit.m_point = target.GetCenterPoint();
            hit.m_dir = AwayFrom(source, target);
            hit.SetAttacker(player);
        }

        /// <summary>Flat direction from a point to a character; forward when they stand on it.</summary>
        private static Vector3 AwayFrom(Vector3 source, Character target)
        {
            Vector3 d = target.transform.position - source;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : target.transform.forward;
        }

        /// <summary>One target's hit, isolated so a single bad target cannot rob the rest of theirs.</summary>
        private static void DamageOne(Character c, HitData hit, string what)
        {
            try { c.Damage(hit); }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] {what}: hit failed: {ex.Message}"); }
        }

        private float ClassDamageScale() =>
            Mathf.Min(ClassDamageMaxScale, 1f + ClassDamagePerBoss * Mathf.Max(0, _defeatedBossCount()));

        /// <summary>Flashes standing now, so a run ending mid-flash can take them down.</summary>
        private readonly List<GameObject> _wrathFlashes = new List<GameObject>();
        private bool _wrathFlashLogged;

        /// <summary>
        /// The Stormward's lightning, borrowed as a picture only.
        ///
        /// The candidate list SagaItems resolves includes "lightningAOE", and an Aoe on that prefab
        /// would do damage of its own when spawned: bare, with no Setup call, it has no owner — so
        /// nothing exempts the player, nothing exempts their wolves, and nobody is credited. Every
        /// Aoe on the instance is therefore disabled (which drops it from the game's updater list in
        /// OnDisable, before any fixed update can run it) and destroyed, and what remains is effects.
        ///
        /// With the Aoe gone, so is whatever TTL it was carrying, so the flash's lifetime is ours: a
        /// pending entry takes it down after <see cref="WrathFlashSeconds"/>. Through ZNetScene when
        /// it has a network view, because a bare Object.Destroy leaves the ZDO behind and ZNetScene
        /// recreates objects for ZDOs that have none — from the PREFAB, Aoe and all. It is also made
        /// non-persistent at once, so it can never be saved into the world.
        /// </summary>
        private void SpawnWrathFlash(Vector3 point)
        {
            GameObject fx = null;
            try { fx = _lightningFx(); }
            catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Thor's Wrath: lightning lookup failed: " + ex.Message); }

            bool hadAoe = fx != null && fx.GetComponentsInChildren<Aoe>(true).Length > 0;
            if (!_wrathFlashLogged)
            {
                _wrathFlashLogged = true;
                Debug.Log(fx == null
                    ? "[ICanShowYouTheWorld] Thor's Wrath: no lightning prefab resolved; it strikes without a flash."
                    : $"[ICanShowYouTheWorld] Thor's Wrath flash: '{fx.name}'" +
                      (hadAoe ? " (carries an Aoe; stripped from each instance)." : "."));
            }
            if (fx == null) return;

            GameObject inst = null;
            try
            {
                inst = UnityEngine.Object.Instantiate(fx, point, Quaternion.identity);
                if (inst == null) return;

                foreach (var aoe in inst.GetComponentsInChildren<Aoe>(true))
                {
                    aoe.enabled = false;
                    UnityEngine.Object.Destroy(aoe);
                }

                var view = inst.GetComponent<ZNetView>();
                var zdo = (view != null && view.IsValid()) ? view.GetZDO() : null;
                if (zdo != null) zdo.Persistent = false;

                _wrathFlashes.Add(inst);
                var flash = inst;
                SchedulePending("wrath-flash-" + inst.GetInstanceID(), WrathFlashSeconds, () => TakeDownWrathFlash(flash));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Thor's Wrath flash failed: " + ex.Message);
                if (inst != null) TakeDownWrathFlash(inst);
            }
        }

        private void TakeDownWrathFlash(GameObject go)
        {
            _wrathFlashes.Remove(go);

            // Unity's ==: the effect may have destroyed itself already, and then there is nothing
            // left to take down.
            if (go == null) return;

            try
            {
                var view = go.GetComponent<ZNetView>();
                if (view != null && view.GetZDO() != null && ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
                else UnityEngine.Object.Destroy(go);
            }
            catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Thor's Wrath flash cleanup: " + ex.Message); }
        }

        private void TakeDownWrathFlashes()
        {
            foreach (var go in _wrathFlashes.ToList()) TakeDownWrathFlash(go);
            _wrathFlashes.Clear();
        }

        // --- The four ways of 2026-09-28 ---
        //
        // Every one is Blood Rage's shape or Rend's. The timed ones switch something on, schedule
        // its off, refuse a recast while the window is open (a recast that only restarted the timer
        // would spend the cooldown on nothing the player could see), and have an End that is
        // idempotent, because it is reached from the timer, from Unapply, from the pending flush and
        // from UnapplyAll's finally. The instant ones refuse on an empty target, as Rend does, so a
        // press at nothing spends no cooldown.

        /// <summary>True while a timed window keyed <paramref name="key"/> has its off still to come.</summary>
        private bool IsWindowOpen(string key)
        {
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].Key == key) return true;
            return false;
        }

        // --- Húskarl ---

        /// <summary>
        /// Shield Bash: Warcry's stagger, narrowed to what stands in front of the player - the
        /// flat look direction, a 120-degree wedge, four metres. Bosses are skipped for Warcry's
        /// reason: RPC_Stagger only sets an animator trigger, and a boss's animator may have no
        /// stagger state for it.
        /// </summary>
        private bool ActivateBash()
        {
            var held = FindHeld("bash");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                // GetLookDir is the eye's forward, pitch and all; flattened, because a player
                // looking at the ground in front of a Greydwarf is still facing it.
                Vector3 look = player.GetLookDir();
                look.y = 0f;
                if (look.sqrMagnitude < 0.0001f) look = player.transform.forward;
                look.Normalize();

                Vector3 from = player.transform.position;
                var foes = HostilesNear(from, BashRadius, player, skipBosses: true)
                    .Where(c =>
                    {
                        Vector3 d = c.transform.position - from;
                        d.y = 0f;
                        // Standing ON the player counts as in front: there is no side to be on.
                        return d.sqrMagnitude < 0.0001f || Vector3.Dot(d.normalized, look) > BashFrontDot;
                    })
                    .ToList();

                if (foes.Count == 0)
                {
                    LastActivationMessage = "Nothing in front of the shield.";
                    return false;
                }

                foreach (var c in foes)
                {
                    try { c.Stagger(AwayFrom(from, c)); }
                    catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Shield Bash: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Shield Bash failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Shield Wall: Resistant to blunt, slash and pierce for twenty seconds. Blood Rage's cost
        /// run the other way - a claim on the one damage-modifier struct, recomputed from the
        /// pristine snapshot, so it composes with Thick-skinned, Hardshell and Reckless instead of
        /// overwriting them (see RefreshDamageModifiers).
        /// </summary>
        private bool ActivateBulwark()
        {
            var held = FindHeld("bulwark");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_damageModBoons.Contains("bulwark"))
            {
                LastActivationMessage = "The wall is already up.";
                return false;
            }

            if (Player.m_localPlayer == null) return false;

            try { ApplyDamageModifier("bulwark"); }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Shield Wall failed: " + ex.Message);
                SafeInvoke(EndBulwark);
                return false;
            }
            if (!_damageModBoons.Contains("bulwark")) return false;

            RemovePending("bulwark");
            SchedulePending("bulwark", BulwarkSeconds, EndBulwark);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndBulwark()
        {
            RemovePending("bulwark");
            UnapplyDamageModifier("bulwark");
        }

        /// <summary>Last Stand is up, on this player, and owes them god mode off (unless they had it).</summary>
        private bool _lastStandOn;
        private Player _lastStandPlayer;
        private bool _lastStandHadGod;

        /// <summary>
        /// Last Stand: six seconds in which the player cannot drop below one hit point, then half
        /// their health back.
        ///
        /// The game's own god mode is exactly that clamp and nothing more: Character.ApplyDamage
        /// takes the hit in full and, only if the result is zero or less and InGodMode() is true,
        /// sets health to 1 (verified in the 1.0.16 IL - the only other reader is the "cheated" flag
        /// on a victim's ZDO, which touches achievement counters and nothing the saga reads). So
        /// this sets Player.SetGodMode directly, and NOT CheatCommands.SetGodMode: the GM mod's
        /// periodic regen (GodRegenTick) is gated on CheatCommands.GodMode, the static, which stays
        /// false - the clamp without the regen, which is what "nothing can end" means and all it
        /// means. The heal is the stand's reward, paid when it ends.
        ///
        /// Two things undo the flag while the window is open and are answered by HoldLastStand:
        /// the legacy god-mode bracket's close (see WithLegacyGodModeBracket), and anything else
        /// that writes Player.SetGodMode(false) mid-window.
        /// </summary>
        private bool ActivateLastStand()
        {
            var held = FindHeld("laststand");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_lastStandOn)
            {
                LastActivationMessage = "You are already standing.";
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) return false;

            // Latched before the write, as Unseen's flag is: a throw after the write must not leave
            // a god-mode player with nothing that knows to undo it.
            _lastStandHadGod = player.InGodMode();
            _lastStandPlayer = player;
            _lastStandOn = true;
            try
            {
                player.SetGodMode(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Last Stand failed: " + ex.Message);
                SafeInvoke(() => EndLastStand(heal: false));
                return false;
            }

            RemovePending("laststand");
            SchedulePending("laststand", LastStandSeconds, () => EndLastStand(heal: true));

            LastActivationMessage = "For six seconds, nothing can end you.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>Per frame while the stand is up: puts the flag back if anything took it.</summary>
        private void HoldLastStand()
        {
            if (!_lastStandOn) return;

            var player = _lastStandPlayer;
            // Unity's ==: the player this was cast on is gone (a logout mid-window). Nothing to
            // hold, and the next player spawns with god mode off of its own accord.
            if (player == null)
            {
                _lastStandOn = false;
                _lastStandPlayer = null;
                RemovePending("laststand");
                return;
            }

            if (!player.InGodMode()) player.SetGodMode(true);
        }

        /// <summary>
        /// Closes the stand: god mode back to what it was (on only if the player had it before, or
        /// the GM mod's static says so now), and - from the timer only - the heal.
        /// </summary>
        private void EndLastStand(bool heal)
        {
            RemovePending("laststand");
            if (!_lastStandOn) return;
            _lastStandOn = false;

            var player = _lastStandPlayer;
            _lastStandPlayer = null;
            if (player == null) return;

            player.SetGodMode(_lastStandHadGod || CheatCommands.GodMode);

            if (heal && !player.IsDead())
                player.Heal(player.GetMaxHealth() * LastStandHealFraction);
        }

        // --- Skald ---

        /// <summary>
        /// Marching Song: a quarter faster and +4 stamina a second for twenty seconds. All three
        /// halves are LOANS in the field ledger under the lender "march", so Fleet-footed (lender
        /// "fleet", the same two speed fields) and Tireless (the same regen field) compose with it,
        /// and one RepayLender takes the song back without touching either.
        /// </summary>
        private bool ActivateMarch()
        {
            var held = FindHeld("march");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (IsWindowOpen("march"))
            {
                LastActivationMessage = "The song is already on your lips.";
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                SyncLoanOwner(player);
                LendFieldFraction(player, "RunSpeed", "march", MarchSpeedFraction);
                LendFieldFraction(player, "WalkSpeed", "march", MarchSpeedFraction);
                LendField(player, "StaminaRegen", "march", MarchStaminaRegen);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Marching Song failed: " + ex.Message);
                SafeInvoke(EndMarch);
                return false;
            }

            SchedulePending("march", MarchSeconds, EndMarch);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndMarch()
        {
            RemovePending("march");
            RepayLender("march");
        }

        /// <summary>
        /// War Song: +15% on the weapon product for twenty seconds, Blood Rage without the price.
        ///
        /// The player only. The legacy pet blessing Shepherd rides (PetBuff.BuffAllPets) is not a
        /// "harder blow" at all - it sets tamed max health to 5000, matches their speed to the
        /// player's and rewrites their weapons from a group baseline - and its only undo,
        /// ResetPetBuffs, would strip a held Shepherd's blessing along with the song's. A timed
        /// version is not cheap, so the card promises the player's blows and nothing else.
        /// </summary>
        private bool ActivateWarsong()
        {
            var held = FindHeld("warsong");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_weaponMultipliers.ContainsKey("warsong"))
            {
                LastActivationMessage = "The war song is already sung.";
                return false;
            }

            if (Player.m_localPlayer == null) return false;

            try { ApplyWeaponMultiplier(WarsongMultiplier, "warsong"); }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] War Song failed: " + ex.Message);
                SafeInvoke(EndWarsong);
                return false;
            }

            RemovePending("warsong");
            SchedulePending("warsong", WarsongSeconds, EndWarsong);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndWarsong()
        {
            RemovePending("warsong");
            RemoveWeaponMultiplier("warsong");
        }

        /// <summary>
        /// Saga of Bragi: the game's own Rested, and 30% of max health.
        ///
        /// Not a loan, and deliberately: Rested is the character's own status, earned the way a
        /// fire and a bench earn it, and it runs out on its own clock. SEMan.AddStatusEffect(int,
        /// resetTime: true) looks the effect up in ObjectDB itself and, when Rested is already on,
        /// only resets its time. Its length is the game's (SE_Rested.UpdateTTL): 300 s plus 60 s per
        /// comfort level above 1, from the comfort the player stands in - five minutes in the open,
        /// more by a hearth - and a reset never shortens what is left. "Rested where you stand"
        /// is exactly that.
        /// </summary>
        private bool ActivateBragi()
        {
            var held = FindHeld("bragi");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            var seman = player == null ? null : player.GetSEMan();
            if (seman == null || player.IsDead()) return false;

            try
            {
                seman.AddStatusEffect(SEMan.s_statusEffectRested, resetTime: true);

                // AddStatusEffect answers null both for "reset an existing one" and for "could not",
                // so the check is whether Rested is on now.
                if (!seman.HaveStatusEffect(SEMan.s_statusEffectRested))
                {
                    Debug.LogWarning("[ICanShowYouTheWorld] Saga of Bragi: the Rested status effect did not take.");
                    LastActivationMessage = "The saga will not come.";
                    return false;
                }

                player.Heal(player.GetMaxHealth() * BragiHealFraction);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Saga of Bragi failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        // --- Sæfari ---

        /// <summary>
        /// Tide-borne: +8 stamina a second for thirty seconds, a field loan under "tide". Swimming
        /// and rowing drain less than that, so for the window the water cannot tire you - and it
        /// is the same field Tireless and the Marching Song lend against, so all three compose.
        /// </summary>
        private bool ActivateTide()
        {
            var held = FindHeld("tide");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (IsWindowOpen("tide"))
            {
                LastActivationMessage = "The tide is already with you.";
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                SyncLoanOwner(player);
                LendField(player, "StaminaRegen", "tide", TideStaminaRegen);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Tide-borne failed: " + ex.Message);
                SafeInvoke(EndTide);
                return false;
            }

            SchedulePending("tide", TideSeconds, EndTide);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndTide()
        {
            RemovePending("tide");
            RepayLender("tide");
        }

        /// <summary>Fair Wind is blowing, on this EnvMan, for this ship, and owes the old wind back.</summary>
        private bool _fairWindOn;
        private EnvMan _fairWindEnv;
        private Ship _fairWindShip;
        private bool _windWasDebug;
        private float _windWasAngle;
        private float _windWasIntensity;

        /// <summary>
        /// Fair Wind: for a minute the wind blows the way the nearest ship is heading, at full
        /// strength.
        ///
        /// Through the game's own wind override, the one the "wind" console command drives: when
        /// EnvMan.m_debugWind is set, UpdateWind aims the target wind at (sin a, 0, cos a) for
        /// a = m_debugWindAngle in degrees, at m_debugWindIntensity (all three public fields,
        /// verified in the 1.0.16 IL). The ship's heading is re-read every frame (SteerFairWind), so
        /// the wind follows the helm - it eases toward each new heading over the game's own five
        /// second transition, as Moder's power does, rather than snapping.
        ///
        /// The three fields are snapshotted and put back exactly, so a player who had set the wind
        /// themselves with the console gets their own wind back, not a cleared one. The wind is
        /// LOCAL: EnvMan computes it per client, and the sail is pushed by whoever owns the ship -
        /// which is the player at the helm, the case this is for.
        /// </summary>
        private bool ActivateFairWind()
        {
            var held = FindHeld("fairwind");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_fairWindOn)
            {
                LastActivationMessage = "The wind is already yours.";
                return false;
            }

            var player = Player.m_localPlayer;
            var env = EnvMan.instance;
            if (player == null || env == null) return false;

            try
            {
                var ship = NearestShip(player.transform.position, FairWindShipRange);
                if (ship == null)
                {
                    LastActivationMessage = "No ship near enough to feel it.";
                    return false;
                }

                _windWasDebug = env.m_debugWind;
                _windWasAngle = env.m_debugWindAngle;
                _windWasIntensity = env.m_debugWindIntensity;
                _fairWindEnv = env;
                _fairWindShip = ship;
                _fairWindOn = true;

                env.m_debugWindAngle = HeadingDegrees(ship.transform.forward);
                env.m_debugWindIntensity = 1f;
                env.m_debugWind = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Fair Wind failed: " + ex.Message);
                SafeInvoke(EndFairWind);
                return false;
            }

            RemovePending("fairwind");
            SchedulePending("fairwind", FairWindSeconds, EndFairWind);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Per frame while the wind is ours: keeps it at the ship's back. If the ship is gone
        /// (sunk, unloaded) the wind follows where the player looks for the rest of the minute -
        /// the minute was paid for, and a wind that dropped dead mid-crossing would be the worse
        /// surprise.
        /// </summary>
        private void SteerFairWind()
        {
            if (!_fairWindOn) return;

            var env = _fairWindEnv;
            if (env == null)
            {
                // The world this was cast in has gone (a logout). A fresh EnvMan starts with the
                // override off, so there is nothing to put back - only the bookkeeping.
                _fairWindOn = false;
                _fairWindEnv = null;
                _fairWindShip = null;
                RemovePending("fairwind");
                return;
            }

            Vector3 heading;
            if (_fairWindShip != null) heading = _fairWindShip.transform.forward;
            else
            {
                var player = Player.m_localPlayer;
                if (player == null) return;
                heading = player.GetLookDir();
            }

            env.m_debugWindAngle = HeadingDegrees(heading);
        }

        private void EndFairWind()
        {
            RemovePending("fairwind");
            if (!_fairWindOn) return;
            _fairWindOn = false;

            var env = _fairWindEnv;
            _fairWindEnv = null;
            _fairWindShip = null;
            if (env == null) return;

            env.m_debugWind = _windWasDebug;
            env.m_debugWindAngle = _windWasAngle;
            env.m_debugWindIntensity = _windWasIntensity;
        }

        /// <summary>The compass angle UpdateWind reads back as (sin a, 0, cos a).</summary>
        private static float HeadingDegrees(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The ship the player is aboard, else the nearest one within range. Ship keeps a static
        /// list only of ships with the local player aboard (GetLocalShip), so a ship at the jetty
        /// is found by a scene search - acceptable at a five-minute cooldown.
        /// </summary>
        private static Ship NearestShip(Vector3 at, float range)
        {
            var aboard = Ship.GetLocalShip();
            if (aboard != null && Vector3.Distance(aboard.transform.position, at) <= range) return aboard;

            Ship best = null;
            float bestDistance = range;
            foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
            {
                if (ship == null) continue;
                float d = Vector3.Distance(ship.transform.position, at);
                if (d > bestDistance) continue;
                best = ship;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>Sea Legs is up: cold and wet are shed every frame until it ends.</summary>
        private bool _seaLegsOn;

        /// <summary>
        /// Sea Legs: five minutes in which neither cold nor wet can reach you.
        ///
        /// Cold and Freezing are kept off by the game itself: Player.UpdateEnvStatusEffects never
        /// applies either while the player's frost modifier is Resistant or better (verified in
        /// the 1.0.16 IL), so the window holds frost at Resistant through the damage-modifier set.
        /// Wet has no such gate - rain adds it every physics step to anyone not under a roof, and
        /// swimming adds it in UpdateWater - so it is removed, quietly, every frame (HoldSeaLegs),
        /// and Cold/Freezing with it in case either was on when the song started.
        /// </summary>
        private bool ActivateSeaLegs()
        {
            var held = FindHeld("sealegs");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_seaLegsOn)
            {
                LastActivationMessage = "Your sea legs are already under you.";
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                ApplyDamageModifier("sealegs");
                if (!_damageModBoons.Contains("sealegs")) return false;

                _seaLegsOn = true;
                ShedColdAndWet(player);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Sea Legs failed: " + ex.Message);
                SafeInvoke(EndSeaLegs);
                return false;
            }

            RemovePending("sealegs");
            SchedulePending("sealegs", SeaLegsSeconds, EndSeaLegs);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void HoldSeaLegs()
        {
            if (!_seaLegsOn) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            // A respawn mid-window hands back a player with vanilla modifiers; the claim is still
            // held, so re-applying against the new player snapshots them and puts frost back up.
            if (!ReferenceEquals(_damageModOwner, player)) ApplyDamageModifier("sealegs");

            ShedColdAndWet(player);
        }

        private static void ShedColdAndWet(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            // quiet: true blanks the stop message, so the shedding is not announced every frame.
            if (seman.HaveStatusEffect(SEMan.s_statusEffectWet)) seman.RemoveStatusEffect(SEMan.s_statusEffectWet, quiet: true);
            if (seman.HaveStatusEffect(SEMan.s_statusEffectCold)) seman.RemoveStatusEffect(SEMan.s_statusEffectCold, quiet: true);
            if (seman.HaveStatusEffect(SEMan.s_statusEffectFreezing)) seman.RemoveStatusEffect(SEMan.s_statusEffectFreezing, quiet: true);
        }

        private void EndSeaLegs()
        {
            RemovePending("sealegs");
            if (!_seaLegsOn) return;
            _seaLegsOn = false;

            UnapplyDamageModifier("sealegs");
        }

        // --- Smiðr ---

        /// <summary>The bench and forge standing now, by ZDOID - see DestroyByZdo for why not by object.</summary>
        private readonly List<ZDOID> _fieldForge = new List<ZDOID>();
        private bool _fieldForgeLogged;

        /// <summary>
        /// Field Forge: a workbench and a forge at the player's feet for ninety seconds.
        ///
        /// Raised the way the companions are - Instantiate, non-persistent ZDO, owned, tracked by
        /// ZDOID - so they can never be saved into the world, and are taken down through the ZDO
        /// even from an unloaded zone. Four things are changed on each INSTANCE (never the prefab),
        /// each for a reason the IL gave:
        ///
        ///  - CraftingStation.m_craftRequireRoof and m_craftRequireFire go false. Both default TRUE
        ///    in the class and CheckUsable refuses a station without a roof over it ("needs a
        ///    roof") - a bench raised on open ground would otherwise be one you cannot use. Set
        ///    before the station's Start, which is when it decides whether to poll for fire.
        ///  - Piece.m_canBeRemoved goes false, so the hammer cannot take them down, and
        ///    Piece.m_resources is emptied, so a Greydwarf that breaks one drops nothing: a raised
        ///    bench taken apart would otherwise refund a real bench's wood, and the forge its copper.
        ///
        /// NOT claimed as the player's (no SetCreator): the built-piece scan counts pieces whose
        /// creator is the player, and a station that stood for ninety seconds must not complete a
        /// "build a workbench" step. Standing near a station does teach it (CraftingStation.
        /// UpdateKnownStationsInRange) - that is how its recipes become craftable at all, and like
        /// Farsight's map it is knowledge, not power: the player has seen a forge.
        /// </summary>
        private bool ActivateFieldForge()
        {
            var held = FindHeld("fieldforge");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_fieldForge.Count > 0)
            {
                LastActivationMessage = "The bench and forge are already standing.";
                return false;
            }

            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null) return false;

            if (!player.IsOnGround() || player.IsSwimming() || player.GetStandingOnShip() != null || player.IsAttachedToShip())
            {
                LastActivationMessage = "Solid ground first.";
                return false;
            }

            var bench = scene.GetPrefab(FieldBenchPrefab);
            var forge = scene.GetPrefab(FieldForgePrefab);
            if (!_fieldForgeLogged)
            {
                // Asset names are data: said once, whichever way it went.
                _fieldForgeLogged = true;
                Debug.Log($"[ICanShowYouTheWorld] Field Forge: '{FieldBenchPrefab}' " + (bench != null ? "resolved" : "NOT FOUND") +
                          $", '{FieldForgePrefab}' " + (forge != null ? "resolved" : "NOT FOUND") + ".");
            }
            if (bench == null && forge == null)
            {
                LastActivationMessage = $"Missing prefabs: {FieldBenchPrefab}, {FieldForgePrefab}";
                return false;
            }

            try
            {
                Vector3 ahead = player.transform.forward;
                ahead.y = 0f;
                if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
                ahead.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, ahead);
                Vector3 centre = player.transform.position + ahead * FieldForgeAhead;
                // Facing the player, so both are ready to use from where they were called.
                Quaternion facing = Quaternion.LookRotation(-ahead);

                if (bench != null) RaiseStation(bench, centre - side * FieldForgeSide, facing, player);
                if (forge != null) RaiseStation(forge, centre + side * FieldForgeSide, facing, player);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Field Forge failed: " + ex.Message);
            }

            if (_fieldForge.Count == 0)
            {
                LastActivationMessage = "The ground would not take them.";
                return false;
            }

            RemovePending("fieldforge");
            SchedulePending("fieldforge", FieldForgeSeconds, TakeDownFieldForge);

            LastActivationMessage = bench != null && forge != null
                ? "A bench and a forge, for ninety seconds."
                : bench != null ? "A bench, for ninety seconds." : "A forge, for ninety seconds.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void RaiseStation(GameObject prefab, Vector3 at, Quaternion facing, Player player)
        {
            // Thane's margin overload: a short ray from just above, which answers false over a cliff
            // edge rather than guessing. On a miss, the player's own footing is the next best thing.
            Vector3 pos = at;
            pos.y = player.transform.position.y;
            try
            {
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(at, out float ground, 5))
                    pos.y = ground;
            }
            catch { }

            var inst = UnityEngine.Object.Instantiate(prefab, pos, facing);
            if (inst == null) return;

            var view = inst.GetComponent<ZNetView>();
            var zdo = (view != null && view.IsValid()) ? view.GetZDO() : null;
            if (zdo == null)
            {
                // Untrackable without a ZDO, and so impossible to take down on time - never leave one.
                UnityEngine.Object.Destroy(inst);
                return;
            }

            zdo.Persistent = false;
            if (!view.IsOwner()) view.ClaimOwnership();

            var piece = inst.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_canBeRemoved = false;
                piece.m_resources = new Piece.Requirement[0];
            }

            foreach (var station in inst.GetComponentsInChildren<CraftingStation>(true))
            {
                station.m_craftRequireRoof = false;
                station.m_craftRequireFire = false;
            }

            _fieldForge.Add(zdo.m_uid);
        }

        private void TakeDownFieldForge()
        {
            RemovePending("fieldforge");
            foreach (var id in _fieldForge.ToList())
            {
                try { DestroyByZdo(id); }
                catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Field Forge cleanup: " + ex.Message); }
            }
            _fieldForge.Clear();
        }

        /// <summary>Master's Minute switched the world's free-building key on and owes it an off.</summary>
        private bool _mastersMinuteOn;

        /// <summary>
        /// Master's Minute: a minute in which building costs nothing.
        ///
        /// Through the world key GlobalKeys.NoBuildCost, NOT Player.m_noPlacementCost. The brief
        /// named the player flag (the GM "nocost"), and the 1.0.16 IL says it is far more than
        /// building: it is private, it shows every piece in the game whether learned or not
        /// (PieceTable.UpdateAvailable), makes every RECIPE craftable with no station and no
        /// materials (Player.GetAvailableRecipes, InventoryGui), skips the roof and fire checks on
        /// every station, and toggles with a "No placement cost" line on screen. A minute of that
        /// is a Flametal sword in Act I.
        ///
        /// The world key is only the building half. Player.HaveRequirements answers true for a
        /// piece while the key is set but still demands the piece be KNOWN and its station in
        /// range; placement skips ConsumeResources; crafting is untouched (that is the separate
        /// NoCraftCost key). And while it is set, Piece.DropResources refunds nothing, so nothing
        /// can be built free and taken down for its bill inside the minute.
        ///
        /// The key is saved with the world, so its original lives with the run's other world-key
        /// originals (WorldModifiers) and is persisted with the run: a crash inside the minute is
        /// put right on resume (RunService releases it) and at run end (RestoreAll).
        /// </summary>
        private bool ActivateMastersMinute()
        {
            var held = FindHeld("mastersminute");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_mastersMinuteOn)
            {
                LastActivationMessage = "The minute is already running.";
                return false;
            }

            if (ZoneSystem.instance == null || Player.m_localPlayer == null) return false;

            bool switched;
            try { switched = _setFreeBuild(true); }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Master's Minute failed: " + ex.Message);
                return false;
            }

            if (!switched)
            {
                // The world builds free already, by its own setting. Nothing to give, and the key
                // is the world's - switching it off after a minute would take the world's own away.
                LastActivationMessage = "Building here already costs nothing.";
                return false;
            }

            _mastersMinuteOn = true;
            RemovePending("mastersminute");
            SchedulePending("mastersminute", MastersMinuteSeconds, EndMastersMinute);

            LastActivationMessage = "For one minute, building costs nothing.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndMastersMinute()
        {
            RemovePending("mastersminute");
            if (!_mastersMinuteOn) return;
            _mastersMinuteOn = false;

            _setFreeBuild(false);
        }

        /// <summary>A piece Reinforce touched, and the two flags it had before.</summary>
        private struct Reinforced
        {
            public WearNTear Piece;
            public bool RoofWear;
            public bool SupportWear;
        }

        private readonly List<Reinforced> _reinforced = new List<Reinforced>();

        /// <summary>
        /// Reinforce: for ten minutes, every player-built piece within twenty metres takes neither
        /// weather wear nor support wear.
        ///
        /// Mind the names. WearNTear.m_noRoofWear and m_noSupportWear read as "no wear", and mean
        /// the opposite: both default TRUE, and UpdateWear only applies rain damage when
        /// m_noRoofWear is true and only runs the support check (100 damage to an unsupported
        /// piece) when m_noSupportWear is true (verified in the 1.0.16 IL). So shoring a piece up
        /// is setting both FALSE - the brief's "set them true" would have been a no-op on a normal
        /// wall and would have added wear to a stone one.
        ///
        /// The flags are per-instance fields, never saved: each touched piece's own values are kept
        /// and put back. A piece destroyed or unloaded meanwhile is skipped (Unity's ==), and comes
        /// back from its prefab anyway. One consequence worth knowing: with the support check off,
        /// a piece whose support is taken away stands until the ten minutes are up, then falls.
        /// </summary>
        private bool ActivateReinforce()
        {
            var held = FindHeld("reinforce");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (_reinforced.Count > 0)
            {
                LastActivationMessage = "Your walls are already shored up.";
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                Vector3 centre = player.transform.position;
                float r2 = ReinforceRadius * ReinforceRadius;

                // A copy, for Mending Hands' reason: the game's list is rearranged when a piece is
                // destroyed.
                foreach (var wnt in WearNTear.GetAllInstances().ToList())
                {
                    if (wnt == null) continue;
                    if ((wnt.transform.position - centre).sqrMagnitude > r2) continue;

                    var piece = wnt.GetComponent<Piece>();
                    if (piece == null || !piece.IsPlacedByPlayer()) continue;

                    _reinforced.Add(new Reinforced { Piece = wnt, RoofWear = wnt.m_noRoofWear, SupportWear = wnt.m_noSupportWear });
                    wnt.m_noRoofWear = false;
                    wnt.m_noSupportWear = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Reinforce failed: " + ex.Message);
                SafeInvoke(EndReinforce);
                return false;
            }

            if (_reinforced.Count == 0)
            {
                LastActivationMessage = "Nothing of yours to shore up.";
                return false;
            }

            RemovePending("reinforce");
            SchedulePending("reinforce", ReinforceSeconds, EndReinforce);

            LastActivationMessage = _reinforced.Count == 1
                ? "One piece shored up for ten minutes."
                : $"{_reinforced.Count} pieces shored up for ten minutes.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        private void EndReinforce()
        {
            RemovePending("reinforce");

            foreach (var r in _reinforced)
            {
                // Unity's ==, deliberately: here the question IS "is it destroyed", and a destroyed
                // piece has no flags left to restore.
                if (r.Piece == null) continue;
                try
                {
                    r.Piece.m_noRoofWear = r.RoofWear;
                    r.Piece.m_noSupportWear = r.SupportWear;
                }
                catch { }
            }
            _reinforced.Clear();
        }

        private bool ActivateWay()
        {
            var held = FindHeldWithCharge("way");
            if (held == null)
            {
                LastActivationMessage = "No Waystone charge available.";
                return false;
            }

            var zone = ZoneSystem.instance;
            var player = Player.m_localPlayer;
            if (zone == null || player == null) return false;

            Vector3 playerPos = player.transform.position;
            Vector3? altar = null;

            // The FIRST undefeated boss, not the nearest one. _undefeatedBossLocations yields in
            // the saga's own progression order, and taking the closest instead sent a player with
            // Bonemass still alive to Moder — "I hadn't killed bonemass but my teleport to
            // boss-boon teleported me to mother". Distance is not the question the boon is
            // answering: "the next boss altar" is, and next has an order.
            foreach (var locName in _undefeatedBossLocations())
            {
                if (!zone.FindClosestLocation(locName, playerPos, out ZoneSystem.LocationInstance loc)) continue;

                altar = loc.m_position;
                break;
            }

            if (altar == null)
            {
                LastActivationMessage = "No undefeated altar found.";
                return false;
            }

            var teleport = Resolve<ITeleportService>();
            if (teleport == null) return false;

            // Distant on purpose: an altar is always across the map, and the game's distant wait is
            // what lets its zones load before the player is put down in them.
            teleport.TeleportTo(LandingNear(altar.Value, playerPos), distant: true);
            held.Charges--;
            return true;
        }

        /// <summary>How far outside an altar to put the player down.</summary>
        private const float WaystoneStandoff = 28f;

        /// <summary>
        /// A place to arrive at an altar: outside it, on dry land, facing in.
        ///
        /// Landing ON the altar was the bug — "e.g. bonemass you teleport inside his skull and
        /// can't get out". Boss altars are BUILDINGS, and the centre point the location system
        /// reports is inside the geometry: Bonemass's is the middle of a closed skull, and Moder's
        /// and Yagluth's are raised platforms. Two metres of clearance is nowhere near enough to
        /// escape any of them.
        ///
        /// Height comes from WorldGenerator rather than a raycast: the destination zone is by
        /// definition not loaded yet — that is the whole point of teleporting there — so there is
        /// no terrain collider to hit. The noise function answers anywhere.
        ///
        /// Directions are tried starting from the side the player is coming FROM, which is the
        /// approach they would have walked, then around the circle; the first comfortably dry one
        /// wins. If none is — Bonemass's mire sits barely above the waterline, so this is a real
        /// case rather than a theoretical one — the HIGHEST of the eight is used instead, and only
        /// a total failure falls back to the altar itself. Standing in a skull beats a boon that
        /// spends its charge and does nothing.
        /// </summary>
        private static Vector3 LandingNear(Vector3 altar, Vector3 from)
        {
            var world = WorldGenerator.instance;

            Vector3 approach = from - altar;
            approach.y = 0f;
            if (approach.sqrMagnitude < 1f) approach = Vector3.forward;
            approach.Normalize();

            const int Spokes = 8;

            Vector3 highest = altar + Vector3.up * 2f;
            float highestGround = float.NegativeInfinity;

            for (int i = 0; i < Spokes; i++)
            {
                // Alternating half-turns out from the approach bearing: 0, +45, -45, +90…
                float degrees = ((i + 1) / 2) * (360f / Spokes) * (i % 2 == 0 ? 1f : -1f);
                Vector3 candidate = altar + Quaternion.Euler(0f, degrees, 0f) * approach * WaystoneStandoff;

                if (world == null) return candidate + Vector3.up * 2f;

                float ground;
                try { ground = world.GetHeight(candidate.x, candidate.z); }
                catch { return candidate + Vector3.up * 2f; }

                candidate.y = ground + 1f;

                if (ground > BiomeCompass.WaterLevel + 1.5f) return candidate;

                if (ground > highestGround)
                {
                    highestGround = ground;
                    highest = candidate;
                }
            }

            return highestGround > BiomeCompass.WaterLevel ? highest : altar + Vector3.up * 2f;
        }

        /// <summary>
        /// Doubles every stack the player is carrying, once per run.
        ///
        /// "Stackable" is the filter, and it is doing real work: m_maxStackSize is a compiled field,
        /// so this needs no asset names at all, and every weapon, tool and piece of armour in the
        /// game has a max stack of 1 and is therefore skipped automatically. What remains is
        /// materials, arrows, food and trophies — which is what "resources" means in play.
        ///
        /// The SNAPSHOT is the load-bearing part. GetAllItems fills a caller-owned list, and
        /// iterating the live inventory instead would keep meeting the stacks it had just added and
        /// double them again, forever. Taking the list first and adding afterwards is what makes
        /// this terminate.
        ///
        /// Amounts are read before ANY grant, for the same reason: a stack that merges with one this
        /// method already created would otherwise be re-measured at its new, larger size.
        ///
        /// Overflow is not lost — _grantItem drops what will not fit at the player's feet.
        /// </summary>
        private bool ActivateWindfall()
        {
            var held = FindHeldWithCharge("windfall");
            if (held == null)
            {
                LastActivationMessage = "Windfall is spent.";
                return false;
            }

            var player = Player.m_localPlayer;
            var inventory = player == null ? null : player.GetInventory();
            if (inventory == null) return false;

            // Resolved to (prefab name, count) pairs BEFORE anything is granted — see the note
            // above. ToList() here is the snapshot: whether GetAllItems hands back the inventory's
            // own list or a copy, this projection is independent of both.
            var toGrant = inventory.GetAllItems()
                .Where(i => i != null && i.m_shared != null && i.m_shared.m_maxStackSize > 1 && i.m_stack > 0)
                .Select(i => new { Name = i.m_dropPrefab == null ? null : i.m_dropPrefab.name, Count = i.m_stack })
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .ToList();

            if (toGrant.Count == 0)
            {
                LastActivationMessage = "Nothing stackable to double.";
                return false;
            }

            foreach (var entry in toGrant) _grantItem(entry.Name, entry.Count);

            held.Charges--;
            LastActivationMessage = held.Charges > 0
                ? $"Windfall: {toGrant.Count} stacks doubled. {held.Charges} left."
                : $"Windfall: {toGrant.Count} stacks doubled. That was the last of it.";
            return true;
        }

        // --- Skill boons ---

        /// <summary>
        /// What each skill boon lifts, and to what. Levels are absolute floors rather than
        /// additions, because that is what the loan mechanism can honestly give back: it
        /// snapshots once and raises, so "set to 50" survives a respawn (which knocks skills down)
        /// without ever compounding.
        ///
        /// Picking one twice is possible — only PASSIVES are excluded from re-offer, and these
        /// are passives, so in practice each is offered at most once per run. The tiers exist so
        /// that a second grant on the same skill from another source still moves it.
        /// </summary>
        private static readonly Dictionary<string, (Skills.SkillType skill, float level)[]> SkillBoons =
            new Dictionary<string, (Skills.SkillType, float)[]>
            {
                ["woodsman"] = new[] { (Skills.SkillType.WoodCutting, 60f) },
                ["hunter"]   = new[] { (Skills.SkillType.Bows, 50f) },
                ["warrior"]  = new[]
                {
                    (Skills.SkillType.Axes, 50f),
                    (Skills.SkillType.Swords, 50f),
                    (Skills.SkillType.Clubs, 50f),
                },

                // General again (2026-09-27), refilling the pool the ways emptied. Each is a skill
                // the run leans on without teaching: ore from the Black Forest on, the distances
                // every act adds, and the parry the bosses are balanced around.
                ["miner"]    = new[] { (Skills.SkillType.Pickaxes, 50f) },
                ["wayfarer"] = new[]
                {
                    (Skills.SkillType.Run, 50f),
                    (Skills.SkillType.Jump, 50f),
                    (Skills.SkillType.Swim, 50f),
                },
                ["steady"]   = new[] { (Skills.SkillType.Blocking, 50f) },

                // The four ways' passives (2026-09-28). Overlaps with the general skill boons are
                // harmless - a loan only ever raises, and never lends a skill twice - so a Skald who
                // also drew Wayfarer holds two cards that say the same thing, and the higher level
                // wins where they differ (the Sæfari's Swim 60 over Wayfarer's 50).
                ["hirdman"]  = new[]
                {
                    (Skills.SkillType.Blocking, 50f),
                    (Skills.SkillType.Spears, 50f),
                },
                ["poet"]     = new[]
                {
                    (Skills.SkillType.Run, 50f),
                    (Skills.SkillType.Jump, 50f),
                    (Skills.SkillType.Swim, 50f),
                },
                // No Fishing, deliberately: the hearth track has a "Fishing skill 10" step, and a
                // loan would complete it the moment the way was taken up.
                ["seafarer"] = new[]
                {
                    (Skills.SkillType.Swim, 60f),
                    (Skills.SkillType.Spears, 50f),
                },
                ["craftsman"] = new[]
                {
                    (Skills.SkillType.WoodCutting, 50f),
                    (Skills.SkillType.Pickaxes, 50f),
                },
            };

        private void ApplySkillBoon(string boonId)
        {
            if (!SkillBoons.TryGetValue(boonId, out var grants)) return;

            foreach (var (skill, level) in grants) _loanSkill(skill, level);
        }

        // --- Packbrother (summoned companions) ---

        /// <summary>
        /// Summons a tamed wolf that follows the player. Like every other boon the power is
        /// loaned, and here that guarantee is made by the SAVE rather than by cleanup code: the
        /// companion's ZDO is marked non-persistent, so it is never written into the world file
        /// no matter how the run (or the process) ends. Cleanup on top of that is what keeps it
        /// from outliving the run within a single session.
        /// </summary>
        private ZDOID _menagerie = ZDOID.None;

        /// <summary>
        /// The roster is VERIFIED at runtime (unresolvable names skipped), and was read from the
        /// world in the first place — the tameable probe printed exactly this list. Chicken stays
        /// in: Odin lending you a chicken is content.
        /// </summary>
        private static readonly string[] MenagerieRoster = { "Boar", "Wolf", "Lox", "Hen", "Chicken", "Asksvin" };

        /// <summary>
        /// Odin lends a beast — ANY beast. Casting again trades the old one back and rolls fresh,
        /// which is the whole game of it: the reroll is the player's choice to make, at the cost
        /// of whatever they had (owner: "you can always just respawn it to try for a different
        /// one"). One menagerie beast at a time; it shares the retinue cap with the wolves and
        /// the skeletons like everything summoned.
        /// </summary>
        private bool ActivateMenagerie()
        {
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null) return false;

            var available = MenagerieRoster.Where(n => scene.GetPrefab(n) != null).ToList();
            if (available.Count == 0)
            {
                LastActivationMessage = "The Allfather has nothing to lend.";
                return false;
            }

            if (_menagerie != ZDOID.None)
            {
                DespawnCompanion(_menagerie);
                _menagerie = ZDOID.None;
            }

            string pick = available[UnityEngine.Random.Range(0, available.Count)];
            if (!SummonOne(player, scene.GetPrefab(pick), named: false, 0)) return false;

            _menagerie = _companions[_companions.Count - 1];
            LastActivationMessage = $"The Allfather lends you a {pick}. Cast again to trade it back.";
            return true;
        }

        private bool ActivateBrother() => Summon(CompanionPrefab, 1, named: true);

        /// <summary>
        /// Raises skeletons that stay raised — Tameable's own Tame(), so they follow, fight, and
        /// are cleaned up at run end like any other companion.
        ///
        /// Two at a time, because one skeleton is a curiosity and a pair is a shield wall.
        /// </summary>
        private bool ActivateBonecaller() => Summon(BonePrefab, 2, named: false);

        /// <summary>
        /// Spawns <paramref name="count"/> tamed followers of a prefab, within the shared retinue
        /// cap.
        ///
        /// Non-persistent, exactly as Packbrother's wolves have always been: summoned company must
        /// not outlive the session and accumulate in someone's world. Power is loaned.
        /// </summary>
        private bool Summon(string prefabName, int count, bool named)
        {
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null) return false;

            var prefab = scene.GetPrefab(prefabName);
            if (prefab == null)
            {
                LastActivationMessage = $"Missing prefab: {prefabName}";
                return false;
            }

            bool any = false;
            for (int i = 0; i < count; i++)
                any |= SummonOne(player, prefab, named, i);

            return any;
        }

        private bool SummonOne(Player player, GameObject prefab, bool named, int index)
        {
            // Oldest out first, so the summon always succeeds rather than refusing at the cap.
            PruneDeadCompanions();
            while (_companions.Count >= MaxCompanions) DespawnCompanion(_companions[0]);

            Vector3 pos = player.transform.position
                        + player.transform.forward * 2f
                        + player.transform.right * (index == 0 ? 0f : index % 2 == 0 ? 1.5f : -1.5f);
            var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (inst == null) return false;

            var ch = inst.GetComponent<Character>();
            var view = inst.GetComponent<ZNetView>();
            var zdo = (view != null && view.IsValid()) ? view.GetZDO() : null;
            if (ch == null || zdo == null)
            {
                // Never leave a half-built companion in the world: without a ZDO it is untrackable
                // and could not be cleaned up at run end.
                UnityEngine.Object.Destroy(inst);
                return false;
            }

            zdo.Persistent = false;

            ch.SetTamed(true);
            ch.SetLevel(CompanionLevel());

            // SetTamed is the whole of it: Tameable.Tame() is private, and this is the same path
            // Packbrother's wolves have used since alpha1.

            if (named) ch.m_name = CompanionNames[_companionNameIndex++ % CompanionNames.Length];

            var ai = inst.GetComponent<MonsterAI>();
            if (ai != null) ai.SetFollowTarget(player.gameObject);

            _companions.Add(zdo.m_uid);
            LastActivationMessage = named ? $"{ch.m_name} answers the call." : "The bones remember.";
            return true;
        }

        /// <summary>
        /// One star per boss felled, capped at two — a meadows wolf is a real bodyguard at the
        /// start and still worth summoning in the Plains, without ever eclipsing the player.
        /// </summary>
        private int CompanionLevel() => Mathf.Clamp(1 + _defeatedBossCount(), 1, 3);

        private void DespawnAllCompanions()
        {
            _menagerie = ZDOID.None;

            foreach (var id in _companions.ToList()) DespawnCompanion(id);
            _companions.Clear();
        }

        /// <summary>
        /// Removes one companion and forgets it. Destroying the loaded object is not enough on its
        /// own: a companion whose zone has unloaded has no object but keeps its ZDO, and would be
        /// re-instantiated on the player's return, so the ZDO goes too.
        ///
        /// Both paths must claim ownership first. <c>ZDOMan.DestroyZDO</c> returns immediately
        /// unless the calling peer owns the ZDO, so on a hosted world an unloaded companion whose
        /// ownership had migrated to another player would otherwise be dropped from tracking while
        /// still fighting — dismissed on paper only. Claiming is the same local write
        /// <c>ZNetView.ClaimOwnership</c> performs, spelled out here because there is no view.
        /// </summary>
        private void DespawnCompanion(ZDOID id)
        {
            _companions.Remove(id);
            DestroyByZdo(id);
        }

        /// <summary>
        /// Takes a networked object out of the world by its ZDOID, loaded or not - the companions'
        /// dismissal, shared since 2026-09-28 with the Field Forge's bench and forge, which have the
        /// same problem: a station whose zone unloaded keeps its ZDO and would come back from the
        /// PREFAB, a real bench, on the player's return.
        /// </summary>
        private static void DestroyByZdo(ZDOID id)
        {
            if (id == ZDOID.None) return;

            var scene = ZNetScene.instance;
            var go = scene == null ? null : scene.FindInstance(id);
            var view = go == null ? null : go.GetComponent<ZNetView>();

            if (view != null && view.IsValid())
            {
                if (!view.IsOwner()) view.ClaimOwnership();
                view.Destroy();
                return;
            }

            var man = ZDOMan.instance;
            var zdo = man?.GetZDO(id);
            if (zdo == null) return;

            if (!zdo.IsOwner()) zdo.SetOwner(ZDOMan.GetSessionID());
            man.DestroyZDO(zdo);
        }

        /// <summary>Drops companions that died on their own, so kills free up a summon slot.</summary>
        private void PruneDeadCompanions()
        {
            var man = ZDOMan.instance;
            if (man == null) return;

            for (int i = _companions.Count - 1; i >= 0; i--)
                if (man.GetZDO(_companions[i]) == null) _companions.RemoveAt(i);
        }

        // --- toggle-safety helpers ---

        private void ForceAoeRenewalOff()
        {
            RemovePending("wind");
            if (!_aoeRenewalOnByUs) return;
            _aoeRenewalOnByUs = false;

            if (CheatCommands.AOERenewalActive) WithLegacyGodModeBracket(CheatCommands.ToggleAoeRenewal);

            // See ForceCloakOff: the ring's lifetime must not depend on a flag staying in sync.
            CheatVisualizer.KillConformHeal();
        }

        private void ForceGhostOff()
        {
            RemovePending("unseen");
            if (!_ghostOnByUs) return;
            _ghostOnByUs = false;

            if (CheatCommands.GhostMode) WithLegacyGodModeBracket(CheatCommands.ToggleGhostMode);
        }

        private void ForceCloakOff()
        {
            RemovePending("ember");
            SnuffEmberFlames();
            if (!_cloakOnByUs) return;
            _cloakOnByUs = false;

            if (CheatCommands.CloakActive) WithLegacyGodModeBracket(CheatCommands.ToggleCloakOfFlames);

            // The toggle owns the ring, but a desynced flag would strand it drawing (and
            // raycasting) forever — a field session produced a 28MB log that way. Killing it
            // explicitly makes the ring's lifetime the boon's, not the flag's.
            CheatVisualizer.KillPbaoeRing();
        }

        /// <summary>
        /// AoE Renewal / Cloak of Flames gate on the LEGACY CheatCommands.GodMode flag, which
        /// RunService forces off for the whole run. The flag is bracketed on just long enough
        /// for the one synchronous toggle call it gates, and restored via CheatCommands.SetGodMode
        /// (the side-effect-free setter) — no frame is ever rendered with it set.
        ///
        /// Last Stand holds the player's OWN god-mode flag while it is up, and the bracket's close -
        /// CheatCommands.SetGodMode(false) - writes Player.SetGodMode(false) straight through it.
        /// Second Wind and Emberskin are general and bracketed, so pressing either mid-window would
        /// end the stand early with nothing on screen to say so.
        /// The hold is put back the instant the bracket closes, before any damage can be taken.
        /// </summary>
        private void WithLegacyGodModeBracket(Action action)
        {
            try { WithGodModeBracket(CheatCommands.SetGodMode, () => CheatCommands.GodMode, action); }
            finally { HoldLastStand(); }
        }

        /// <summary>Same bracket, for the modern-service god-mode flag that gates IPetService.BuffAllPets.</summary>
        private static void WithServiceGodModeBracket(Action action)
        {
            var combat = Resolve<ICombatService>();
            if (combat == null)
            {
                action?.Invoke();
                return;
            }
            WithGodModeBracket(combat.SetGodMode, () => combat.GodMode, action);
        }

        /// <summary>
        /// weTurnedOn is latched BEFORE calling setGodMode(true) — CombatService/CheatCommands
        /// both assign their flag before touching the player, so if that call throws partway,
        /// the flag can already be true; latching first guarantees the finally still tries to
        /// put it back regardless of where the throw happens.
        /// </summary>
        private static void WithGodModeBracket(Action<bool> setGodMode, Func<bool> getGodMode, Action action)
        {
            if (action == null) return;

            bool weTurnedOn = false;
            try
            {
                if (!getGodMode())
                {
                    weTurnedOn = true;
                    setGodMode(true);
                }
                action();
            }
            finally
            {
                if (weTurnedOn) setGodMode(false);
            }
        }

        private void SchedulePending(string key, float seconds, Action off)
        {
            _pending.Add(new PendingOff { Key = key, Remaining = seconds, Off = off });
        }

        private void RemovePending(string key)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Key == key) _pending.RemoveAt(i);
            }
        }

        private HeldBoon FindHeld(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Def.Id == boonId) return list[i];
            }
            return null;
        }

        /// <summary>Most-recently-picked entry for an id that can be held more than once (any active).</summary>
        private HeldBoon FindNewestHeld(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Def.Id == boonId) return list[i];
            }
            return null;
        }

        private HeldBoon FindHeldWithCharge(string boonId)
        {
            var list = _heldBoons();
            if (list == null) return null;

            foreach (var h in list)
            {
                if (h.Def.Id == boonId && h.Charges > 0) return h;
            }
            return null;
        }

        /// <summary>Never throws — ServiceContainer.Instance.TryGet returns null rather than raising when unregistered.</summary>
        private static T Resolve<T>() where T : class => ServiceContainer.Instance.TryGet<T>();

        private static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[ICanShowYouTheWorld] BoonEffects action failed: {ex}"); }
        }
    }
}
