using System;
using System.Collections.Generic;
using System.Reflection;
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
    public partial class BoonEffects
    {
        private const float SharpDamageMultiplier = 1.2f;
        private const float FleetSpeedIncrements = 2f;
        private const float WindOnSeconds = 10f;
        private const float EmberOnSeconds = 30f;

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

        // Blood Rage's window, Rend's reach and Warcry's reach are WayRules.RageSeconds / RendRadius / WarcryRadius
        // (2026-10-08): the god-tempered numbers live there, tested. Blood Rage's gain is the Berserker's Fury, full.

        /// <summary>Rend's cut, before boss scaling: about one early sword swing, spent on everyone at once.</summary>
        private const float RendSlash = 20f;

        /// <summary>Rend's bleed, before boss scaling. Poison, because RPC_Damage hands it to the game's
        /// own SE_Poison as a damage-over-time — the bleed costs no status effect of ours.</summary>
        private const float RendPoison = 15f;

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

        /// <summary>
        /// How far off the look direction a foe may stand and still be "in front": the cosine of
        /// sixty degrees, so a 120-degree wedge. Wide enough that a player who is not aiming
        /// carefully still hits what they are facing; narrow enough that the thing behind them does
        /// not get staggered by a shield pointed the other way.
        /// </summary>
        private const float BashFrontDot = 0.5f;

        /// <summary>Last Stand's close: "half your health back".</summary>
        private const float LastStandHealFraction = 0.5f;

        /// <summary>The Marching Song's breath: stamina regenerates "half again as fast" - a fraction of the player's own
        /// regen, lent against the ledger's original like the song's step.</summary>
        private const float MarchStaminaFraction = 0.5f;

        /// <summary>
        /// Where the bench and the forge stand: this far ahead of the player, and this far either
        /// side of that point. Four metres between centres leaves about a metre and a half of clear
        /// ground between the two pieces, which is room to walk between them.
        /// </summary>
        private const float FieldForgeAhead = 3f;
        private const float FieldForgeSide = 2f;

        internal const string FieldBenchPrefab = "piece_workbench";
        internal const string FieldForgePrefab = "forge";

        // The Field Forge's and the Master's Minute's windows are WayRules.FieldForgeSeconds / MastersMinuteSeconds
        // (ninety seconds and a minute; three and two after Moder and Yagluth).

        /// <summary>The Smiðr's walls (Reinforce, a passive since 2026-10-08): "within twenty metres".</summary>
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

        /// <summary>Packbrother's wolves, oldest first: two at a time, three after Bonemass (WayRules.PackSize).</summary>
        private readonly List<ZDOID> _pack = new List<ZDOID>();

        /// <summary>Bonecaller's skeletons, oldest first: two at a time, three after Bonemass (WayRules.BoneCount).</summary>
        private readonly List<ZDOID> _bones = new List<ZDOID>();
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

                // The default gait. Character.UpdateWalking moves at m_speed * the jog factor; m_walkSpeed is read only
                // while walking (toggle or minor-action slowdown) and m_runSpeed only while sprinting, so a speed that
                // lends those two alone is never felt at the pace a player keeps most of the time. The Marching Song
                // lends all three; Fleet-footed still lends only the first two (same gap, ledgered separately).
                ["JogSpeed"]          = (p => p.m_speed,             (p, v) => p.m_speed = v),
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
        //
        // The ONE owner of a held block's damage (2026-10-08): anything else that writes a held block - Thor's bow's
        // element and its Moder tempering - writes through RebaseWeapon, or the next refresh undoes it.
        private readonly WeaponOriginals<ItemDrop.ItemData.SharedData, HitData.DamageTypes> _weaponOriginals =
            new WeaponOriginals<ItemDrop.ItemData.SharedData, HitData.DamageTypes>(
                shared => DamageHelpers.Copy(shared.m_damages),
                (shared, damages) => shared.m_damages = damages,
                DamageHelpers.Scaled);

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

                case "hunter":
                    // Two halves, like the hirdman's: the loan (Bows and Sneak, in SkillBoons) and
                    // a status effect that quiets the player. Both are re-run on respawn and
                    // resume, and both are idempotent - the loan only ever raises, the effect is
                    // not added twice.
                    ApplySkillBoon(boonId);
                    ApplyHunterHush();
                    break;

                case "woodsman":
                case "warrior":
                case "miner":
                case "wayfarer":
                case "steady":
                case "poet":
                    ApplySkillBoon(boonId);
                    break;

                case "seafarer":
                    // The skill loan, and the water that never tires her: a status effect of ours (2026-10-08).
                    ApplySkillBoon(boonId);
                    ApplyTideBorne();
                    break;

                case "hirdman":
                    // Craftsman's two halves, and a third: Guard (2026-10-08), a status effect of ours.
                    ApplySkillBoon(boonId);
                    ApplyFieldBoost(boonId);
                    ApplyGuard();
                    break;

                case "craftsman":
                    // Two halves: skills through the host's loan (SkillBoons), and one Player
                    // field through the ledger (FieldLoans). Both are re-run safely on respawn.
                    // And a third (2026-10-08): Forge-skin, a status effect of ours. His gear and his walls
                    // are kept by TickSmith, once a second.
                    ApplySkillBoon(boonId);
                    ApplyFieldBoost(boonId);
                    ApplyForgeSkin();
                    break;

                case "irongut":
                case "coldblood":
                case "fireblood":
                case "thickskin":
                case "hardshell":
                    ApplyDamageModifier(boonId);
                    break;

                case "reckless":
                    // Both halves (2026-10-08): the +50% was declared and never applied, so the boon was all cost.
                    ApplyDamageModifier(boonId);
                    ApplyWeaponMultiplier(RecklessDamageMultiplier, "reckless");
                    break;

                case "stoker":
                    // The gain half only. The cost — heat rising faster — is the host's, applied
                    // where Slow Burn's discount is (RunService.AddHeat), because heat is not a
                    // thing this class can write.
                    ApplyWeaponMultiplier(StokerDamageMultiplier, "stoker");
                    break;

                case "stuffed":
                    // Nothing to apply: HoldStuffed hands the meals their time back every frame
                    // while the boon is held. Not a loan — the minutes a meal has already banked
                    // when the boon leaves are the character's, like Farsight's map.
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
                    // One star for every animal on your side (2026-10-08, class balance). It rode the GM
                    // mod's pet buff until then - 5000 health, and a damage copy written through m_shared
                    // that every wild wolf and boar shared. See RefreshShepherd.
                    try { RefreshShepherd(true, force: true); }
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

                case "hunter":
                    // The skill half is the host's to give back (RestoreLoanedSkills); the hush is
                    // ours, and a permanent status effect has nothing else that would ever end it.
                    UnapplyHunterHush();
                    break;

                case "irongut":
                case "coldblood":
                case "fireblood":
                case "thickskin":
                case "hardshell":
                    UnapplyDamageModifier(boonId);
                    break;

                case "reckless":
                    UnapplyDamageModifier(boonId);
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "stoker":
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "warrior":
                    // The skill loan is the host's to give back (RestoreLoanedSkills); this id used to fall to the
                    // default's UnapplyFieldBoost, which is kept. The Fury is ours: its weapon factor and its stacks.
                    UnapplyFieldBoost(boonId);
                    RemoveWeaponMultiplier("fury");
                    _fury.Reset();
                    _lastFuryBonus = -1f;
                    _lastEnemyHits = -1;
                    break;

                case "poet":
                    // What the default did for it (the field loan), and the standing song, which is ours: the
                    // allies' half lapses on its own ttl, but the player's step and blows are loans to repay.
                    UnapplyFieldBoost(boonId);
                    EndSongs();
                    break;

                case "hirdman":
                    // What the default did for it (the field loan), and Guard, which is ours and which nothing else
                    // would ever take off.
                    UnapplyFieldBoost(boonId);
                    UnapplyGuard();
                    break;

                case "craftsman":
                    // What the default did for it (the field loan), Forge-skin, and every wall he had shored up -
                    // their flags are per instance and nothing else would ever put them back.
                    UnapplyFieldBoost(boonId);
                    UnapplyForgeSkin();
                    EndReinforce();
                    break;

                case "seafarer":
                    // What the default did for it (the field loan), and Tide-borne, which is ours and which nothing else
                    // would ever take off.
                    UnapplyFieldBoost(boonId);
                    UnapplyTideBorne();
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
                case "march":
                case "warsong":
                case "bragi":         EndSongs(); break;
                case "stormcaller":   StormUntil = float.NegativeInfinity; break;
                case "sealegs":       EndSeaLegs(); break;
                case "fieldforge":    TakeDownFieldForge(); break;
                case "mastersminute": EndMastersMinute(); break;
                case "watchpost":     TakeDownWatchPosts(); break;

                // The bow outlives the boon and ships as lightning; losing the switch (the dev
                // class cycle, or the run's end through UnapplyAll) puts it back there.
                case "elemental":     _resetBowElement(); break;

                case "shepherd":
                    try { RefreshShepherd(false, force: true); }
                    catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Shepherd reset: {e.Message}"); }
                    break;

                case "forgefed":
                    RemoveWeaponMultiplier(boonId);
                    break;

                case "brother":
                case "bonecaller":
                case "menagerie":
                    // Losing the boon takes its summons with it — a death that costs you
                    // Packbrother must not leave the pack fighting on — and only its own: losing the
                    // wolves must not evict the skeletons or the menagerie beast. Bonecaller and
                    // Menagerie fell through to the default and did nothing until 2026-10-08, so a
                    // Hunter who laid down the way (Packbrother revoked first, Menagerie still held)
                    // kept every wolf and the beast to the run's end.
                    DismissRanks(boonId);
                    // Belt and braces: with NO summoning boon left, nothing summoned stays. The
                    // held-list check is safe because BoonEngine removes the entry before raising
                    // Lost; an offer no longer lists a boon the player already holds, so anything
                    // still held here would be a genuine duplicate.
                    var stillHeld = _heldBoons();
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
                case "march": return SwitchSong(SkaldSong.March);
                case "warsong": return SwitchSong(SkaldSong.War);
                case "bragi": return SwitchSong(SkaldSong.Bragi);
                case "undertow": return ActivateUndertow();
                case "stormcaller": return ActivateStormcaller();
                case "sealegs": return ActivateSeaLegs();
                case "fieldforge": return ActivateFieldForge();
                case "mastersminute": return ActivateMastersMinute();
                case "watchpost": return ActivateWatchPost();
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
                // threw - the same reasoning as Blood Rage above. Master's Minute is the one that
                // touches something other than the player: the world's free-building key is not the
                // character's, and a run must not leave it behind. The storm is a plain reset: it is
                // only a time that SeaWatch reads, so there is nothing in the game to unwind.
                SafeInvoke(() => EndLastStand(heal: false));
                StormUntil = float.NegativeInfinity;
                SafeInvoke(EndMastersMinute);
                SafeInvoke(EndReinforce);
                // The Smiðr's ballista and Forge-skin: a networked object and a status effect of ours, which nothing
                // else would ever take down.
                SafeInvoke(TakeDownWatchPosts);
                SafeInvoke(UnapplyForgeSkin);
                SafeInvoke(TakeDownFieldForge);
                SafeInvoke(EndSeaLegs);
                SafeInvoke(EndBulwark);
                SafeInvoke(EndSongs);
                // The Hunter's hush has no ttl, so a run that ended on a path where the held-boon
                // loop threw would otherwise leave the player quiet for the rest of the session.
                SafeInvoke(UnapplyHunterHush);
                // And Guard, likewise: a permanent status effect of ours that nothing else would ever take off.
                SafeInvoke(UnapplyGuard);
                // And Tide-borne, the Sæfari's: the same kind of thing, the same reasoning.
                SafeInvoke(UnapplyTideBorne);
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
            HoldStuffed(dt);
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
        /// Lends a FRACTION of the field's pristine value - the Marching Song's "+20% speed".
        /// Measured from the ledger's original, never from the live value, so a song sung while
        /// Fleet-footed is held is a fraction of the player's own pace, not of the boosted
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

        /// <summary>True while the live weapon factors multiply past <see cref="WayRules.WeaponCeiling"/>, so the
        /// BOONS page can say a further damage card adds nothing.</summary>
        public bool WeaponCeilingReached { get; private set; }

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
        /// Run whenever a factor is set or dropped: a weapon boon picked or lost, a respawn re-applying a held one,
        /// every Forge-fed heat change, and Fury and the War Song as they rise and fall. NOT on the poll tick: a
        /// weapon first drawn after the last of those carries no factor until the next one.
        /// </summary>
        internal void RefreshWeaponDamage()
        {
            var inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null || _weaponMultipliers.Count == 0) return;

            // Capped (2026-10-08): Sharpened, Glass Cannon, Stoker, Reckless, Forge-fed and Fury held together
            // reached about x4.5. See WayRules.WeaponCeiling.
            float raw = 1f;
            foreach (var m in _weaponMultipliers.Values) raw *= m;
            WeaponCeilingReached = raw > WayRules.WeaponCeiling;
            float product = WayRules.WeaponProduct(_weaponMultipliers.Values);

            foreach (var item in inventory.GetEquippedItems())
            {
                if (item == null || !item.IsWeapon()) continue;

                var shared = item.m_shared;
                if (shared == null) continue;

                // Snapshot on first sight only. Keyed by the SHARED block rather than the ItemData
                // instance for the reason documented on _weaponOriginals: m_shared is per-prefab, and
                // a fresh instance after respawn points at the same already-boosted block.
                _weaponOriginals.Remember(shared);
            }

            // Then write every block we have touched — the ones in hand, and the ones no longer in
            // hand. Before Blood Rage no factor
            // ever dropped mid-run except by losing a boon, so a weapon put away kept whatever the
            // product was when it was last held and nobody could tell. A fifteen-second factor makes
            // that visible: sheathe the axe mid-rage, draw it after, and the first swings before the
            // next poll would still land at x1.5. One product for every block we have touched keeps
            // "original times the live multipliers" true of all of them, not just the equipped ones.
            _weaponOriginals.ApplyAll(product);
        }

        /// <summary>
        /// Writes a weapon's OWN numbers under the weapon boons: <paramref name="change"/> runs on the block's
        /// original, the result becomes the original, and the block is written back as that x the live product.
        /// With no original held - no weapon boon has seen this block - the change is simply written.
        /// </summary>
        /// <remarks>
        /// Thor's bow's element switch and the Hunter's Moder tempering come through here (SagaItems.SetThorsBowElement,
        /// wired by RunService), since 2026-10-08. Written straight into the block, they lasted only until the next
        /// refresh, which put back the original the boons first saw: the HUD said "fire" while the bow dealt lightning,
        /// and a x1.5 caught in the original outlived the tempering.
        /// </remarks>
        internal void RebaseWeapon(ItemDrop.ItemData.SharedData shared, Action change)
        {
            if (change == null) return;
            _weaponOriginals.Rebase(shared, change, WayRules.WeaponProduct(_weaponMultipliers.Values));
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

            // Reckless's cost, and Blood Rage's. "SlightlyWeak" is the game's x1.25 - the cards' "25% more"; it was
            // "Weak" (x1.5) until 2026-10-08, half again what the cards said.
            //
            // Blood Rage pays the same price, for its fifteen seconds only. It is not a separate
            // modifier because there is no separate armour to put it on: one struct, one snapshot,
            // and both claims collapse to the same SlightlyWeak — holding Reckless and raging at once costs
            // no more than either, which is the honest reading of "one step worse".
            //
            // Thick-skinned and Hardshell were the first boons to claim the same slots. Written as
            // "last one wins", holding Reckless would silently delete a resistance the player also
            // picked, and which of the two the player kept would depend on nothing they could see.
            // One step better and one step worse is no step at all, so a resisted type goes to
            // Normal under the cost rather than to SlightlyWeak: both picks still mean what their cards say.
            if (_damageModBoons.Contains("reckless") || _damageModBoons.Contains("rage"))
            {
                mods.m_blunt = thickskin ? HitData.DamageModifier.Normal : HitData.DamageModifier.SlightlyWeak;
                mods.m_slash = slashResisted ? HitData.DamageModifier.Normal : HitData.DamageModifier.SlightlyWeak;
                mods.m_pierce = hardshell ? HitData.DamageModifier.Normal : HitData.DamageModifier.SlightlyWeak;
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
        /// Hearthlight: a mending pulse around the player once a second, reaching tamed animals too.
        ///
        /// 3 a second in the Meadows, +1 for every god felled, to 8, and 12 once the Queen tempers it
        /// (<see cref="WayRules.HearthlightPerSecond"/>) - it was 4 every 5 s, 0.8 a second (class balance, 2026-10-08).
        /// RunService calls this every poll second, so the timer here is the once-a-second gate and a late poll costs
        /// one pulse, not a burst.
        ///
        /// Implemented directly rather than through the legacy AoE-renewal statics — those gate
        /// on god mode and tick through PeriodicManager, and the Shepherd just demonstrated what
        /// riding an ungated legacy toggle from a boon costs. A heal is small enough to own.
        /// </summary>
        public void RefreshHearthlight(bool held)
        {
            if (!held || Time.time < _hearthlightAt) return;
            _hearthlightAt = Time.time + 1f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            // No number pops - it would be one a second. Character.Heal routes to the owner, so tames mend from
            // this client.
            float hp = WayRules.HearthlightPerSecond(_defeatedBossCount());
            try
            {
                player.Heal(hp, false);

                var list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 15f, list);
                foreach (var c in list)
                {
                    if (c == null || c.IsPlayer() || !c.IsTamed() || c.IsDead()) continue;
                    c.Heal(hp, false);
                }
            }
            catch { /* a missed pulse is a missed pulse */ }
        }

        private float _shepherdAt;

        /// <summary>ZDO int on an animal Shepherd starred: its level before the star (0 = not ours).</summary>
        private const string ShepherdMark = "ICSYTW_shepherd";

        /// <summary>How far the shepherd's eye reaches: the pen at home, and the pack around you.</summary>
        private const float ShepherdRadius = 30f;

        /// <summary>
        /// The shepherd's star, kept true every few seconds: every animal on your side within reach gets ONE star
        /// (TameStars.WithShepherd) while it is held, and gives it back when it is not.
        /// </summary>
        /// <remarks>
        /// Since 2026-10-08. It rode the GM mod's pet buff (CheatCommands.BuffAllPets) until then: every tame to
        /// 5000 health, and the strongest tame's weapon x1.2 written into m_shared - which ItemData.Clone does
        /// not copy, so every wild wolf and boar bit harder too. The star is per animal, and the animal carries
        /// a ZDO mark with its old level, so the star comes off after a reload as well: whenever a run sees a
        /// marked animal without the boon. One the old buff left at 5000 (saved into the world - the legacy
        /// reset never undid it) is recomputed from its level on sight, held or not.
        /// The speed match stays from the old buff: it was already per animal, and prefab-anchored.
        /// </remarks>
        public void RefreshShepherd(bool held, bool force = false)
        {
            if (!force && Time.time < _shepherdAt) return;
            _shepherdAt = Time.time + 5f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, ShepherdRadius, list);
            foreach (var c in list)
            {
                if (c == null || c.IsPlayer() || !c.IsTamed()) continue;
                try { ShepherdStar(c, held, player); }
                catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Shepherd on {c.name}: {e.Message}"); }
            }
        }

        /// <summary>One animal's star, given or given back. Health keeps its share of the new maximum.</summary>
        private static void ShepherdStar(Character c, bool held, Player player)
        {
            var view = c.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null) return;
            if (!view.IsOwner()) view.ClaimOwnership();

            int marked = zdo.GetInt(ShepherdMark, 0);

            if (TameStars.IsLegacyBlessing(c.GetMaxHealth())) SetLevelKeepingShare(c, c.GetLevel());

            if (held && marked == 0)
            {
                int was = c.GetLevel();
                zdo.Set(ShepherdMark, was);
                SetLevelKeepingShare(c, TameStars.WithShepherd(was));
                MatchSpeed(c, player, true);
            }
            else if (!held && marked > 0)
            {
                zdo.Set(ShepherdMark, 0);
                SetLevelKeepingShare(c, marked);
                MatchSpeed(c, player, false);
            }
        }

        private static void SetLevelKeepingShare(Character c, int level)
        {
            float share = c.GetMaxHealth() > 0f ? Mathf.Clamp01(c.GetHealth() / c.GetMaxHealth()) : 1f;
            c.SetLevel(level);
            c.SetHealth(Mathf.Max(1f, c.GetMaxHealth() * share));
        }

        /// <summary>
        /// Keeps up with its shepherd: the player's pace, never below the prefab's own (a wolf is never slowed), and
        /// back to the prefab's when the star goes. Prefab-anchored, so repeating it cannot compound.
        /// </summary>
        private static void MatchSpeed(Character c, Player player, bool on)
        {
            var prefab = ZNetScene.instance?.GetPrefab(c.gameObject.name.Replace("(Clone)", ""));
            var orig = prefab != null ? prefab.GetComponent<Character>() : null;
            if (orig == null) return;
            c.m_runSpeed = on ? Mathf.Max(orig.m_runSpeed, player.m_runSpeed) : orig.m_runSpeed;
            c.m_speed = on ? Mathf.Max(orig.m_speed, player.m_walkSpeed) : orig.m_speed;
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
            _weaponOriginals.RestoreAll();
            _weaponMultipliers.Clear();
            WeaponCeilingReached = false;
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

            held.CooldownRemaining = WayRules.MendingCooldown(_defeatedBossCount());
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
            // Short on purpose (WayRules.UnseenSeconds: 20 s, 30 after Yagluth): ghost mode is a total answer to
            // every melee in the game, so its value has to be "get out of this", not "win this".
            SchedulePending("unseen", WayRules.UnseenSeconds(_defeatedBossCount()), ForceGhostOff);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        // --- The ways' actives: Blood Rage, Rend, Warcry, Thor's Wrath ---

        /// <summary>
        /// Blood Rage: the Emberskin shape — switch on, schedule the off. The gain is the Berserker's Fury, filled
        /// and held full until EndRage lets go (WayRules.RageSeconds from the press); the price is the
        /// damage-modifier snapshot, which already knows how to give back exactly what it took. Both end in
        /// EndRage, off the one pending timer, so they cannot run on different clocks: that timer stops while
        /// the player is dead, and a hold timed on Time.time would have run out during the respawn while the
        /// price carried on.
        ///
        /// Discrete rather than per-frame on purpose. The boon design turned down "damage rises as
        /// health falls" because it would be a number moving every frame against the player's own
        /// health; a window you chose to start is a decision, and a readable one.
        ///
        /// Recasting is refused while the window is open, like Emberskin and Unseen: a recast that
        /// merely restarted the timer would waste the cooldown for nothing the player could see.
        /// </summary>
        private bool ActivateRage()
        {
            var held = FindHeld("rage");
            if (held == null || held.CooldownRemaining > 0f) return false;

            if (IsWindowOpen("rage"))
            {
                LastActivationMessage = "The rage is already on you.";
                return false;
            }

            if (Player.m_localPlayer == null) return false;

            float rageSeconds = WayRules.RageSeconds(_defeatedBossCount());
            try
            {
                // The cost first, then the gain: if the second half throws, EndRage below unwinds
                // whichever half landed, and the player is never left with the Fury held but not
                // the price. The hold is open-ended; EndRage's Release is its only end.
                ApplyDamageModifier("rage");
                _fury.Fill(Time.time, float.PositiveInfinity);
                if (Holds("warrior")) RefreshFuryFactor();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Blood Rage failed: " + ex.Message);
                SafeInvoke(EndRage);
                return false;
            }

            RemovePending("rage");
            SchedulePending("rage", rageSeconds, EndRage);

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Closes the rage window: the pending timer, the Fury's hold (the stacks then fade from here), and the
        /// cost. Idempotent — each half is a no-op when it has nothing registered — because it is reached from
        /// the timer, from Unapply, from the pending flush and from UnapplyAll's finally, and on a bad day from
        /// more than one of them.
        /// </summary>
        private void EndRage()
        {
            RemovePending("rage");
            try { _fury.Release(Time.time); }
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
                var foes = HostilesNear(player.transform.position, WayRules.RendRadius(_defeatedBossCount()), player, skipBosses: false);
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
                var foes = HostilesNear(player.transform.position, WayRules.WarcryRadius(_defeatedBossCount()), player, skipBosses: true);
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
                if (Holds("warrior"))
                {
                    _fury.AddHalf(Time.time);
                    RefreshFuryFactor();
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

                var foes = HostilesNear(point, WayRules.WrathRadius(_defeatedBossCount()), player, skipBosses: false);
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
                var foes = HostilesNear(from, WayRules.BashRadius(_defeatedBossCount()), player, skipBosses: true)
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
            SchedulePending("bulwark", WayRules.WallSeconds(_defeatedBossCount()), EndBulwark);

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
            float standSeconds = WayRules.LastStandSeconds(_defeatedBossCount());
            SchedulePending("laststand", standSeconds, () => EndLastStand(heal: true));

            // Six, or ten once Yagluth has fallen (the tempering): the message says the seconds it really is.
            LastActivationMessage = $"For {(standSeconds >= 10f ? "ten" : "six")} seconds, nothing can end you.";
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>Per frame while the stand is up: puts the flag back if anything took it.</summary>
        // --- Stuffed (2026-09-28, owner: "food lasts for hours") ---

        /// <summary>How many times longer a meal lasts. Four turns a 25-minute meal into most of two hours.</summary>
        private const float StuffedFactor = 4f;

        /// <summary>
        /// Player.m_foods is private; each Food's m_time is public. Read by name once, the way the
        /// mod already reads Projectile's owner — the field is data the compiler cannot see, so a
        /// rename in a game update is a null here and a warning in the log, not a crash.
        /// </summary>
        private static readonly FieldInfo FoodsField =
            typeof(Player).GetField("m_foods", BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _stuffedWarned;

        /// <summary>
        /// Gives every eaten meal back (1 - 1/factor) of the time the game is about to take, so
        /// the net burn is a quarter speed. Player.UpdateFood subtracts whole seconds on a one-second
        /// timer; adding fractions every frame and clamping at the meal's full time comes out the
        /// same on average and never overfills.
        /// </summary>
        private void HoldStuffed(float dt)
        {
            if (FindHeld("stuffed") == null) return;

            try
            {
                var player = Player.m_localPlayer;
                if (player == null) return;

                if (FoodsField == null)
                {
                    if (!_stuffedWarned)
                    {
                        _stuffedWarned = true;
                        Debug.LogWarning("[ICanShowYouTheWorld] Stuffed: Player.m_foods not found; meals burn at the usual rate.");
                    }
                    return;
                }

                var foods = FoodsField.GetValue(player) as List<Player.Food>;
                if (foods == null) return;

                float giveBack = dt * (1f - 1f / StuffedFactor);
                foreach (var food in foods)
                {
                    if (food == null || food.m_item == null || food.m_item.m_shared == null) continue;
                    float full = food.m_item.m_shared.m_foodBurnTime;
                    food.m_time = Mathf.Min(full, food.m_time + giveBack);
                }
            }
            catch (Exception ex)
            {
                if (!_stuffedWarned)
                {
                    _stuffedWarned = true;
                    Debug.LogWarning("[ICanShowYouTheWorld] Stuffed failed: " + ex.Message);
                }
            }
        }

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
        // The standing songs (SwitchSong, ApplySongs, TickSongs, EndSongs) live in BoonEffects.Ways.cs.

        // --- Sæfari ---
        // The passive (Tide-borne), Undertow and Stormcaller live in BoonEffects.Ways.cs.

        /// <summary>Sea Legs is up: cold and wet are shed every frame until it ends.</summary>
        private bool _seaLegsOn;

        /// <summary>
        /// Sea Legs: five minutes (ten after Yagluth) in which neither cold nor wet can reach you.
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
            SchedulePending("sealegs", WayRules.SeaLegsSeconds(_defeatedBossCount()), EndSeaLegs);

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
        /// Field Forge: a workbench and a forge at the player's feet for ninety seconds (WayRules.FieldForgeSeconds:
        /// three minutes after Moder).
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
            float forgeSeconds = WayRules.FieldForgeSeconds(_defeatedBossCount());
            SchedulePending("fieldforge", forgeSeconds, TakeDownFieldForge);

            string forgeFor = ", for " + SecondsPhrase(forgeSeconds) + ".";
            LastActivationMessage = bench != null && forge != null
                ? "A bench and a forge" + forgeFor
                : bench != null ? "A bench" + forgeFor : "A forge" + forgeFor;
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
            float minuteSeconds = WayRules.MastersMinuteSeconds(_defeatedBossCount());
            SchedulePending("mastersminute", minuteSeconds, EndMastersMinute);

            LastActivationMessage = "For " + SecondsPhrase(minuteSeconds) + ", building costs nothing.";
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

        /// <summary>A piece the Smiðr's walls touched, and the two flags it had before.</summary>
        private struct Reinforced
        {
            public WearNTear Piece;
            public bool RoofWear;
            public bool SupportWear;
        }

        private readonly List<Reinforced> _reinforced = new List<Reinforced>();

        /// <summary>The same pieces as <see cref="_reinforced"/>, for "already done?" in constant time: a base is thousands of them.</summary>
        private readonly HashSet<WearNTear> _reinforcedSet = new HashSet<WearNTear>();

        /// <summary>
        /// The Smiðr's walls (Reinforce, made a passive 2026-10-08): every player-built piece within twenty metres of
        /// <paramref name="centre"/> takes neither weather wear nor support wear. TickSmith calls it once a second.
        ///
        /// Mind the names. WearNTear.m_noRoofWear and m_noSupportWear read as "no wear", and mean
        /// the opposite: both default TRUE, and UpdateWear only applies rain damage when
        /// m_noRoofWear is true and only runs the support check (100 damage to an unsupported
        /// piece) when m_noSupportWear is true (verified in the 1.0.16 IL). So shoring a piece up
        /// is setting both FALSE - the brief's "set them true" would have been a no-op on a normal
        /// wall and would have added wear to a stone one.
        ///
        /// The flags are per-instance fields, never saved: each touched piece's own values are kept
        /// and put back (EndReinforce). A piece is shored up once; one left more than twenty metres behind is let go and
        /// has its own flags back, so the walls unworn are the ones he is standing near; a piece destroyed or unloaded
        /// meanwhile is forgotten (Unity's ==), and comes back from its prefab anyway. One consequence worth knowing:
        /// with the support check off, a piece whose support is taken away stands until he walks away, then falls.
        /// As a passive it freezes support at its maximum for the pieces near him, so builds past the limits fall when he
        /// leaves, respawns elsewhere, reloads, or the run ends.
        /// </summary>
        private void ReinforceAround(Vector3 centre)
        {
            float r2 = ReinforceRadius * ReinforceRadius;

            // Let go of what is destroyed, or left behind.
            _reinforced.RemoveAll(r =>
            {
                // Unity's ==, deliberately: here the question IS "is it destroyed", and a destroyed
                // piece has no flags left to restore.
                if (r.Piece == null) { _reinforcedSet.Remove(r.Piece); return true; }
                if ((r.Piece.transform.position - centre).sqrMagnitude <= r2) return false;
                try
                {
                    r.Piece.m_noRoofWear = r.RoofWear;
                    r.Piece.m_noSupportWear = r.SupportWear;
                }
                catch { }
                _reinforcedSet.Remove(r.Piece);
                return true;
            });

            // The game's own list, walked in place - this runs once a second, and a base is thousands of pieces. Nothing in
            // the loop destroys or creates one (it reads, and sets two plain fields), so the list cannot move under it;
            // Mending Hands copies its list because repairing can destroy a piece, this does not.
            foreach (var wnt in WearNTear.GetAllInstances())
            {
                if (wnt == null) continue;
                if ((wnt.transform.position - centre).sqrMagnitude > r2) continue;
                if (_reinforcedSet.Contains(wnt)) continue;

                var piece = wnt.GetComponent<Piece>();
                if (piece == null || !piece.IsPlacedByPlayer()) continue;

                _reinforced.Add(new Reinforced { Piece = wnt, RoofWear = wnt.m_noRoofWear, SupportWear = wnt.m_noSupportWear });
                _reinforcedSet.Add(wnt);
                wnt.m_noRoofWear = false;
                wnt.m_noSupportWear = false;
            }
        }

        private void EndReinforce()
        {
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
            _reinforcedSet.Clear();
        }

        /// <summary>A window's length as the message says it: "ninety seconds", "one minute", "three minutes".</summary>
        private static string SecondsPhrase(float seconds)
        {
            int s = (int)Math.Round(seconds);
            if (s == 90) return "ninety seconds";
            if (s == 60) return "one minute";
            if (s >= 120 && s % 60 == 0 && s <= 600)
                return new[] { "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" }[s / 60 - 2] + " minutes";
            return s + " seconds";
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
                // Sneak since 2026-09-28 (owner: "could the hunter make less sound?"). Sneak is
                // the skill UpdateStealth reads while crouched; the noise half is ApplyHunterHush.
                ["hunter"]   = new[]
                {
                    (Skills.SkillType.Bows, 50f),
                    (Skills.SkillType.Sneak, 50f),
                },
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
                // harmless - a loan only ever raises, and never lends a skill twice - and the higher
                // level wins where they differ (the Sæfari's Swim 60 over Wayfarer's 50). A WHOLE
                // overlap is kept off the wheel since 2026-10-08 (BoonDefinition.CoveredBy): Wayfarer
                // for a Skald, Steady Hands for a Húskarl, Miner for a Smiðr.
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

        // --- The Hunter's hush (a runtime status effect) ---

        /// <summary>
        /// Noise, as SE_Stats reads it: <c>noise += baseNoise * m_noiseModifier</c>
        /// (SE_Stats.ModifyNoise, reached from Character.RPC_AddNoise, which every footstep, swing
        /// and shout goes through). So it is an ADDITIVE FRACTION, not a multiplier: -0.4 leaves the
        /// player's noise at 60 percent. The tooltip prints it as "-40%".
        /// </summary>
        private const float HunterNoise = -0.4f;

        /// <summary>
        /// Stealth, same shape: <c>stealth += baseStealth * m_stealthModifier</c> (SE_Stats.ModifyStealth,
        /// read by Player.UpdateStealth and ONLY while crouched - standing, the factor is a flat 1).
        /// The factor is the fraction of a creature's view range at which it can see you
        /// (BaseAI.CanSeeTarget: viewRange * stealthFactor), so LOWER is stealthier and "stealth up
        /// by 0.2" is a negative number: -0.2 means seen from 80 percent as far off.
        /// </summary>
        private const float HunterStealth = -0.2f;

        /// <summary>
        /// The asset name, which is what SEMan keys on: NameHash() is name.GetStableHashCode(), and
        /// HaveStatusEffect/RemoveStatusEffect take the hash. Prefixed so it can never collide with a
        /// vanilla effect's.
        /// </summary>
        private const string HunterHushName = "ICSYTW_HunterHush";

        private static readonly int HunterHushHash = HunterHushName.GetStableHashCode();

        /// <summary>
        /// Made once and kept. SEMan.AddStatusEffect(StatusEffect) never looks the effect up in
        /// ObjectDB - it MemberwiseClones the instance it is handed - so an effect the game has never
        /// heard of works on the local player without being registered. The clone shares this
        /// object's native half, which is why this one is never destroyed. Nothing about it is saved:
        /// Player.Save does not write status effects, and death clears them (RemoveAllStatusEffects),
        /// which is why the respawn's ReapplyPassiveBoonEffects puts it back.
        /// </summary>
        private static SE_Stats _hunterHush;

        private static bool _hunterHushLogged;

        private static SE_Stats HunterHush(Player player)
        {
            if (_hunterHush != null) return _hunterHush;

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = HunterHushName;
            se.m_name = "Hunter's Hush";
            se.m_tooltip = "You move quietly.";
            se.m_ttl = 0f;  // no ttl: IsDone never fires, so it lasts until we take it off
            se.m_noiseModifier = HunterNoise;
            se.m_stealthModifier = HunterStealth;

            // The Sneak skill's own icon, so the HUD shows the effect by the skill it goes with. An
            // effect with no icon would work just as well - GetHUDStatusEffects skips it - but the
            // player would have no way to see the hush is on, or to read its tooltip.
            try
            {
                var skills = player != null ? player.GetSkills() : null;
                var def = skills?.m_skills?.FirstOrDefault(d => d != null && d.m_skill == Skills.SkillType.Sneak);
                se.m_icon = def?.m_icon;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Hunter's hush: no Sneak icon ({ex.Message}); it will be invisible.");
            }

            _hunterHush = se;
            return se;
        }

        private void ApplyHunterHush()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null) return;

                var seman = player.GetSEMan();
                if (seman == null || seman.HaveStatusEffect(HunterHushHash)) return;

                seman.AddStatusEffect(HunterHush(player));

                if (!_hunterHushLogged)
                {
                    _hunterHushLogged = true;
                    Debug.Log($"[ICanShowYouTheWorld] Hunter's hush on: SE '{HunterHushName}', " +
                              $"m_noiseModifier {HunterNoise}, m_stealthModifier {HunterStealth}, " +
                              $"icon {(_hunterHush.m_icon != null ? _hunterHush.m_icon.name : "none")}. " +
                              VanillaStealthEffects());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Hunter's hush failed: {ex.Message}");
            }
        }

        private static void UnapplyHunterHush()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null) return;
                player.GetSEMan()?.RemoveStatusEffect(HunterHushHash, quiet: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Hunter's hush removal failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Every vanilla effect that touches noise or stealth, read out of ObjectDB, so the hush's
        /// numbers can be set beside the game's own. The Trollstrap set bonus is the one to compare
        /// against; it is looked for by name, and the scan catches it anyway if it was renamed.
        /// </summary>
        private static string VanillaStealthEffects()
        {
            try
            {
                var odb = ObjectDB.instance;
                if (odb == null || odb.m_StatusEffects == null) return "No ObjectDB to compare against.";

                var troll = odb.GetStatusEffect("SetEffect_TrollArmor".GetStableHashCode()) as SE_Stats;
                string trollText = troll != null
                    ? $"Trollstrap (SetEffect_TrollArmor): noise {troll.m_noiseModifier}, stealth {troll.m_stealthModifier}."
                    : "SetEffect_TrollArmor not found.";

                var others = odb.m_StatusEffects
                    .OfType<SE_Stats>()
                    .Where(s => s != null && (s.m_noiseModifier != 0f || s.m_stealthModifier != 0f))
                    .Select(s => $"{s.name} (noise {s.m_noiseModifier}, stealth {s.m_stealthModifier})");
                return trollText + " All vanilla: " + string.Join(", ", others) + ".";
            }
            catch (Exception ex)
            {
                return "Vanilla comparison failed: " + ex.Message;
            }
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
        /// Odin lends a beast — any of the lands you have opened (WayRules.MenagerieBeasts). Casting again trades the old one back and rolls fresh,
        /// which is the whole game of it: the reroll is the player's choice to make, at the cost
        /// of whatever they had (owner: "you can always just respawn it to try for a different
        /// one"). One menagerie beast at a time; it shares the retinue cap with the wolves and
        /// the skeletons like everything summoned. Each lend starts the card's 90 s (2026-10-08, the
        /// final review): until then it never cooled, and a reroll was one keypress away.
        /// </summary>
        private bool ActivateMenagerie()
        {
            var held = FindHeld("menagerie");
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null) return false;

            // By biome (2026-10-08): the beasts the gods felled have opened, not all six from the start.
            var opened = WayRules.MenagerieBeasts(_defeatedBossCount());
            var available = MenagerieRoster.Where(n => opened.Contains(n) && scene.GetPrefab(n) != null).ToList();
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
            held.CooldownRemaining = held.Def.CooldownSeconds;
            LastActivationMessage = $"The Allfather lends you a {pick}. Cast again to trade it back.";
            return true;
        }

        /// <summary>
        /// Packbrother: the pack topped up to WayRules.PackSize (2026-10-08, the final review). The wolves at your side
        /// stay and the fallen come back; a full pack refuses and spends nothing, and the card's 240 s starts only when a
        /// wolf came. Until then the call never cooled, and each press sent the oldest wolf home for a fresh one.
        /// </summary>
        private bool ActivateBrother() =>
            CallRanks("brother", _pack, CompanionPrefab, WayRules.PackSize(_defeatedBossCount()), named: true,
                      full: "Your pack is already with you.", several: "Your pack answers the call.");

        /// <summary>
        /// Raises skeletons that stay raised — Tameable's own Tame(), so they follow, fight, and
        /// are cleaned up at run end like any other companion.
        ///
        /// Two at a time, because one skeleton is a curiosity and a pair is a shield wall; three after
        /// Bonemass (<see cref="WayRules.BoneCount"/>). Topped up like the pack since 2026-10-08: each call
        /// raised a whole new pair, bounded only by the retinue cap, and never spent the card's 120 s.
        /// </summary>
        private bool ActivateBonecaller() =>
            CallRanks("bonecaller", _bones, BonePrefab, WayRules.BoneCount(_defeatedBossCount()), named: false,
                      full: "Your dead already stand with you.", several: "The bones remember.");

        /// <summary>
        /// Tops one summoning boon's <paramref name="ranks"/> up to <paramref name="size"/> tamed followers of a
        /// prefab (WayRules.RanksToFill), within the shared retinue cap, and starts the boon's cooldown when at least
        /// one came. A full rank refuses with <paramref name="full"/> and spends no cooldown.
        ///
        /// Non-persistent, exactly as Packbrother's wolves have always been: summoned company must
        /// not outlive the session and accumulate in someone's world. Power is loaned.
        /// </summary>
        private bool CallRanks(string boonId, List<ZDOID> ranks, string prefabName, int size, bool named,
                               string full, string several)
        {
            var held = FindHeld(boonId);
            if (held == null || held.CooldownRemaining > 0f) return false;

            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null) return false;

            var prefab = scene.GetPrefab(prefabName);
            if (prefab == null)
            {
                LastActivationMessage = $"Missing prefab: {prefabName}";
                return false;
            }

            PruneRanks(ranks);
            int missing = WayRules.RanksToFill(size, ranks.Count);
            if (missing == 0)
            {
                LastActivationMessage = full;
                return false;
            }

            int came = 0;
            for (int i = 0; i < missing; i++)
            {
                if (!SummonOne(player, prefab, named, i, keep: ranks)) break;
                ranks.Add(_companions[_companions.Count - 1]);
                came++;
            }
            if (came == 0) return false;

            // One wolf says its own name (SummonOne's line); a whole pack answering is one line, not the last name.
            if (came > 1) LastActivationMessage = several;
            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Forgets the ranks' fallen: an id whose ZDO is gone (dead, or despawned by us), or that the retinue no
        /// longer counts (sent home at the cap).
        /// </summary>
        private void PruneRanks(List<ZDOID> ranks)
        {
            var man = ZDOMan.instance;
            ranks.RemoveAll(id => man == null || man.GetZDO(id) == null || !_companions.Contains(id));
        }

        /// <param name="keep">
        /// The caller's own ranks, never sent home to make room: a top-up that dismissed its own wolf to call a wolf
        /// would call nobody. When the cap is all <paramref name="keep"/>, nothing is summoned.
        /// </param>
        private bool SummonOne(Player player, GameObject prefab, bool named, int index, ICollection<ZDOID> keep = null)
        {
            // Oldest out first, so the summon succeeds rather than refusing at the cap.
            PruneDeadCompanions();
            while (_companions.Count >= MaxCompanions)
            {
                int oldest = keep == null ? 0 : _companions.FindIndex(id => !keep.Contains(id));
                if (oldest < 0) return false;
                DespawnCompanion(_companions[oldest]);
            }

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
            // The Hunter's star at once, not at the next refresh (2026-10-08): she summons mid-fight.
            if (_heldBoons().Any(h => h.Def.Id == "shepherd"))
            {
                try { ShepherdStar(ch, true, player); }
                catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Shepherd on a summon: {e.Message}"); }
            }

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
        private int CompanionLevel() => TameStars.SummonLevel(_defeatedBossCount());

        /// <summary>Sends home one summoning boon's own followers - the pack, the bones or the beast - and forgets them.</summary>
        private void DismissRanks(string boonId)
        {
            if (boonId == "menagerie")
            {
                if (_menagerie != ZDOID.None) DespawnCompanion(_menagerie);
                _menagerie = ZDOID.None;
                return;
            }

            var ranks = boonId == "brother" ? _pack : boonId == "bonecaller" ? _bones : null;
            if (ranks == null) return;
            foreach (var id in ranks) DespawnCompanion(id);
            ranks.Clear();
        }

        private void DespawnAllCompanions()
        {
            _menagerie = ZDOID.None;
            _pack.Clear();
            _bones.Clear();

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
        internal static void DestroyByZdo(ZDOID id)
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
