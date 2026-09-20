using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// An item of the saga's own, made by cloning one of the game's item prefabs at runtime and
    /// changing its shared data — name, description, damage. No shipped assets: the model and
    /// icon are the source item's.
    /// </summary>
    internal sealed class SagaItemDefinition
    {
        /// <summary>Prefab of the vanilla item to clone, e.g. "BowFineWood".</summary>
        public string SourcePrefab;

        /// <summary>
        /// Tried in turn if <see cref="SourcePrefab"/> does not resolve to an item, first match wins.
        /// The same discipline the effect lists use: asset names are data the assembly cannot see,
        /// and a saga item whose source moved in a game update is an item that silently stops
        /// existing — along with any recipe that asks for it.
        /// </summary>
        public string[] SourceFallbacks;

        /// <summary>Name of the clone. This is what saves, drops and recipes refer to — never change it once shipped.</summary>
        public string PrefabName;

        /// <summary>What the game shows. Plain text, not a "$" token: the localiser leaves it alone.</summary>
        public string DisplayName;

        public string Description;

        /// <summary>Applied to the clone's OWN shared data once, after the copy.</summary>
        public Action<ItemDrop.ItemData.SharedData> Tune;

        /// <summary>
        /// Applied after <see cref="Tune"/>, for the part of an item that needs the scene's prefab
        /// table - an effect list, a spawned object. Separate because <see cref="Tune"/> is a pure
        /// function of numbers and is the one the tests can read.
        /// </summary>
        public Action<SagaItems, ItemDrop.ItemData.SharedData> TuneWithScene;
    }

    /// <summary>
    /// The saga's own items, and the two things they need to stay real: a place in the game's
    /// registries, and their special behaviour.
    ///
    /// A cloned item is a GameObject the game never shipped, so nothing knows it unless it is
    /// put in ObjectDB (what recipes and inventories resolve names through) and ZNetScene (what
    /// dropped items in the world resolve through). Both registries are REBUILT on every world
    /// load, so the check runs every frame and re-registers whenever either instance has changed.
    /// Per frame, not per second, because the moment that matters is narrow: the player's
    /// inventory is loaded a few frames after ObjectDB is, and a Thor's bow in a pack that loads
    /// before the name resolves is silently dropped from the save. The check itself is two
    /// reference comparisons.
    ///
    /// That is also why this is NOT run-only, unlike the recipe that makes the item: the bow
    /// outlives the run it was strung in and must keep loading afterwards. The one thing this
    /// cannot fix is the mod being absent — then the item is an unknown name and is lost from
    /// any pack or world it was in. Documented in the act plans; accepted for a personal mod.
    ///
    /// The clones live under a holder object that is inactive and survives scene loads, so they
    /// are never "in" the world and their ZNetViews never register a ZDO — the same trick the
    /// game plays with its own prefab lists. Instantiating under an inactive parent is what
    /// keeps Awake from running.
    ///
    /// Lightning on impact is done here too, because it is the ITEM's behaviour. The game plays
    /// a projectile's hit effects from the ARROW, not the bow — so a bow cannot carry an effect of
    /// its own in data. Instead, while Thor's bow is the weapon in hand, every arrow the player has
    /// in flight is given a spawn-on-hit of the lightning effect the game already has. The
    /// projectile's owner and weapon are private fields, read by reflection; nothing else is.
    /// </summary>
    internal sealed class SagaItems
    {
        public const string ThorsBowPrefab = "Saga_ThorsBow";
        public const string ThorsBowName = "Thor’s bow";

        public const string StormwardPrefab = "Saga_Stormward";
        public const string StormwardName = "Stormward";

        /// <summary>
        /// The shield's block, and the reason it is worth an act's last craft step. Blackmetal-tier
        /// on a wooden frame, because what makes it is not the frame: it is what the hide came off
        /// and what was bound into it.
        /// </summary>
        private const float StormwardBlock = 60f;
        private const float StormwardBlockPerLevel = 8f;

        /// <summary>
        /// The discharge: how many blocks the shield stores before it gives the storm back, and how
        /// long it will hold them.
        /// </summary>
        /// <remarks>
        /// Two, and five seconds, which together say something the shield could not say with one
        /// number: it answers a FIGHT, not a tap. Block twice inside five seconds and it goes off;
        /// take one hit from a passing boar and the charge is simply gone, because
        /// <c>Humanoid.UpdateBlock</c> resets the count to zero (not down by one) once the decay time
        /// passes with no block.
        ///
        /// Both are one-line dials. One charge makes it fire on every single block, which is louder
        /// and was very nearly the choice ("something crazy"); three makes it a reward for standing
        /// in a swarm. Five seconds is the honest limiter here, not the count.
        /// </remarks>
        private const int StormwardBlockCharges = 2;
        private const float StormwardChargeDecaySeconds = 5f;

        /// <summary>
        /// What the discharge does. Radius in metres, and the lightning is the shield's own damage
        /// so it scales with quality like any weapon's.
        /// </summary>
        /// <remarks>
        /// Five metres is wider than Thor's bow's three, deliberately: the bow is aimed and this is
        /// not - it goes off where you are standing, when something has just hit you, and its job is
        /// to clear the ring of things pressed against the shield rather than to kill one chosen
        /// target. Hence the force and the stagger too: being thrown off you IS the effect, and the
        /// damage is a bonus. The force is the item's own m_attackForce, set rather than inherited,
        /// with the attack's multiplier left at one.
        ///
        /// The blunt is small and exists so the numbers on the item card are not a lie: the shield
        /// does have a physical hit, and it is the boss of the shield coming forward.
        /// </remarks>
        private const float StormwardDischargeRadius = 5f;
        private const float StormwardLightning = 26f;
        private const float StormwardLightningPerLevel = 5f;
        private const float StormwardBlunt = 12f;
        private const float StormwardBluntPerLevel = 2f;
        private const float StormwardDischargeForce = 80f;

        /// <summary>
        /// What the shield is worth before it needs a bench, and what one discharge takes out of it.
        /// </summary>
        /// <remarks>
        /// The discharge used to be entirely free, which the owner spotted: "since the shield should
        /// trigger an aoe on attack maybe the durability should be low to start? Must be repaired
        /// often?" Right that it should cost something, and the cost is put on the DISCHARGE rather
        /// than on the shield's ceiling, because those two punish different play.
        ///
        /// A low ceiling taxes BLOCKING - every ordinary parry against a greyling brings the repair
        /// trip nearer, and the shield ends up worst at the thing shields are for. Wear per discharge
        /// taxes the POWER, which is the part that is free and automatic: the storm eats the shield.
        /// It also reads correctly without a word of explanation, because the durability bar drops
        /// visibly at the moment the lightning goes off.
        ///
        /// Both are set rather than inherited, for the usual reason - the source prefab is an
        /// Ashlands tower shield and its ceiling is a late-game number that would have made the cost
        /// invisible. 300 against 12 a discharge is about twenty-five storms from full, plus the
        /// ordinary drain of blocking, so a heavy night sends you home. Which is where this saga
        /// wants you anyway: the hearth is not decoration, and Homeward exists.
        /// </remarks>
        private const float StormwardDurability = 300f;
        private const float StormwardDurabilityPerLevel = 60f;
        public const float StormwardDischargeWear = 12f;
        private const float StormwardDischargeStagger = 4f;

        public const string RescuedLightPrefab = "Saga_RescuedLight";
        public const string RescuedLightName = "Rescued light";

        /// <summary>
        /// How many rescued lights Thor's bow asks for. Repeated in the step's Hint, which lies if
        /// this changes alone.
        ///
        /// Three, and the reason it is safe to put a rescued light on the CHAIN is the Gatherer: it
        /// frees Clamp(lightsLost, 2, 6) of them when it dies, so the worse the race goes the more it
        /// is carrying — a forfeited race (8 lost) frees six. Losing the race cannot lock the bow
        /// away; it only moves where the lights have to be taken from, which is the story anyway.
        /// </summary>
        public const int ThorsBowLightCost = 3;

        /// <summary>
        /// The shield's own light cost. Three again, deliberately: the act's two saga items ask the
        /// same price, so the choice between them is never about which is cheaper. A player who
        /// raced badly can afford ONE of them, which is a decision rather than a shortage.
        /// </summary>
        public const int StormwardLightCost = 3;

        // ------------------------------------------------------------------ the Stormsworn set
        //
        // One piece per act, Acts II to V, each answering the thing that act kills people with.
        // Requested as "a quest line to collect storm armor and limit to one per act", and the
        // shape is the answer to the one real question in it: what are the pieces FOR?
        //
        // Five pieces of +armour would be a grind with a story on top, and a one-per-act limit
        // would only pace the grind. So each piece instead resists its OWN act's named threat, the
        // way the Stormward resists Eikthyr's lightning and the Breaker's weight. The set then is
        // not a stat total but a record of what the run survived, and it is worth wearing into a
        // later act precisely because of where it was made.
        //
        // The Stormward is the first piece of it, retroactively - lightning and blunt, Act I. What
        // follows covers poison, frost and fire, and Valheim adds resistances across equipped items
        // by itself, so the full kit IS the set bonus. No m_setStatusEffect: that wants a
        // StatusEffect asset this build cannot verify, and an invisible set bonus is worse than an
        // honest one made of parts.
        //
        // Deliberately no rescued lights in these recipes. Lights come from the deer hunt and the
        // couriers - Acts I and II only (PollLights) - so a light cost in Act IV would be a step
        // nobody could finish, and a CollectItem step that cannot be finished is a stalled act.
        public const string StormHelmPrefab   = "Saga_StormHelm";
        public const string StormChestPrefab  = "Saga_StormChest";
        public const string StormLegsPrefab   = "Saga_StormLegs";
        public const string StormCapePrefab   = "Saga_StormCape";

        public const string StormHelmName  = "Stormsworn helm";
        public const string StormChestName = "Stormsworn cuirass";
        public const string StormLegsName  = "Stormsworn greaves";
        public const string StormCapeName  = "Stormsworn mantle";

        /// <summary>
        /// One resistance and a tier-appropriate armour value, for a Stormsworn piece.
        /// </summary>
        /// <remarks>
        /// Resistant, never VeryResistant or Immune: the saga does not hand out a fight that cannot
        /// hurt you, and four pieces of Resistant already add up to something the player will feel.
        /// The armour numbers sit a little above the tier the piece is cloned from, which is what
        /// makes it worth the detour without making the act's own smithing pointless.
        /// </remarks>
        private static Action<ItemDrop.ItemData.SharedData> StormPiece(
            float armor, float armorPerLevel, HitData.DamageType resist)
        {
            return shared =>
            {
                shared.m_armor = armor;
                shared.m_armorPerLevel = armorPerLevel;

                shared.m_damageModifiers = new List<HitData.DamageModPair>
                {
                    new HitData.DamageModPair
                    {
                        m_type = resist,
                        m_modifier = HitData.DamageModifier.Resistant,
                    },
                };
            };
        }

        /// <summary>
        /// The bow's damage, SET rather than added to whatever the source prefab happened to carry.
        /// </summary>
        /// <remarks>
        /// Set, because inherited was a quiet defect: the pierce came from whichever prefab the
        /// fallback chain resolved, so the saga's signature weapon had a different power depending on
        /// which bow existed in the build - and the number nobody chose was the one the player read.
        ///
        /// The numbers have moved three times and the last move was DOWN, which is the interesting
        /// one. 20/+4 lightning first, then 26/+5 ("we COULD increase the dmg just a bit"), then
        /// 58 pierce + 32 lightning once pierce stopped being inherited - and then the bow learned to
        /// fork, and 90 damage to everything within four metres was plainly too much (owner: "since
        /// the bow is AOE now, it seems OP").
        ///
        /// 44 + 22 is 66 on a direct hit, against the Huntsman's 52 that mq-herald hands over - so it
        /// is still the better bow in the hand, and far better against a group, which is what it is
        /// FOR. The radius came down with it; see <see cref="ThorsBowAoeRadius"/> for why that
        /// mattered more than the damage did.
        ///
        /// Both are one-line dials and neither is sacred. If it is still too strong, cut the pierce:
        /// the lightning is the part that makes it Thor's.
        /// </remarks>
        private const float ThorsBowPierce = 44f;
        private const float ThorsBowPiercePerLevel = 5f;
        private const float ThorsBowLightning = 22f;
        private const float ThorsBowLightningPerLevel = 4f;

        /// <summary>
        /// How far the lightning reaches around an arrow's impact, in metres.
        /// </summary>
        /// <remarks>
        /// Three. It was four, and four plus the damage above was too much. BOTH dials were turned,
        /// because the radius is what decides how many things a shot kills and that is where the
        /// strength actually was.
        ///
        /// <c>Projectile.m_aoe</c> applies the arrow's full damage to everything inside it - there is
        /// no distance falloff on that path, only on <c>Aoe</c> components, which the strike code
        /// deliberately does not use (it would have had no owner to exempt). So the radius is not a
        /// nicety: it is a multiplier on the whole weapon.
        /// </remarks>
        private const float ThorsBowAoeRadius = 3f;

        /// <summary>Candidate lightning effects; the first that resolves is used, and the log says which.</summary>
        /// <remarks>
        /// A long list because these are asset names this assembly cannot verify, and the cost of a
        /// miss is a weapon that does its damage invisibly - which is exactly how the shield was first
        /// reported: "the shield didnt do lightning on impact, just acted as a normal shield."
        /// </remarks>
        private static readonly string[] LightningPrefabs =
        {
            "fx_eikthyr_stomp", "fx_Eikthyr_stomp", "vfx_eikthyr_stomp",
            "lightningAOE", "fx_lightning", "vfx_lightning", "vfx_lightning_hit", "fx_lightning_hit",
        };

        public static readonly SagaItemDefinition[] All =
        {
            new SagaItemDefinition
            {
                // The Huntsman rather than the Finewood bow, for the LOOK: the saga's named things
                // should not be the plainest model in their class (owner: "The storm shield/thor bow
                // models are a bit simple"). Nothing about the weapon is inherited any more - every
                // number below is set - so the source prefab is now purely which mesh it wears, and
                // the fallbacks exist because a prefab name is asset data this build cannot verify.
                // The log line "Saga item created: ... from X" says which one won.
                SourcePrefab = "BowHuntsman",
                SourceFallbacks = new[] { "BowDraugrFang", "BowFineWood", "Bow" },
                PrefabName = ThorsBowPrefab,
                DisplayName = ThorsBowName,
                Description = "Strung from the herd’s hide, sealed with the forest’s resin, taught by " +
                              "a hunter who never loosed. The storm in it is Eikthyr’s own, turned.",
                Tune = shared =>
                {
                    shared.m_damages.m_pierce = ThorsBowPierce;
                    shared.m_damagesPerLevel.m_pierce = ThorsBowPiercePerLevel;

                    shared.m_damages.m_lightning = ThorsBowLightning;
                    shared.m_damagesPerLevel.m_lightning = ThorsBowLightningPerLevel;

                    // Whatever else the source bow did, it does not do here. DraugrFang carries
                    // poison and is the first fallback, and a bow named for the storm that also
                    // poisons things is the source prefab leaking through the item.
                    shared.m_damages.m_poison = 0f;
                    shared.m_damages.m_fire = 0f;
                    shared.m_damages.m_frost = 0f;
                    shared.m_damages.m_spirit = 0f;
                    shared.m_damagesPerLevel.m_poison = 0f;
                    shared.m_damagesPerLevel.m_fire = 0f;
                    shared.m_damagesPerLevel.m_frost = 0f;
                    shared.m_damagesPerLevel.m_spirit = 0f;
                },
            },

            // Act I's last craft, and the answer to the god at the end of it.
            //
            // Thor's bow turns Eikthyr's storm outward; this turns it aside. The pair is the point
            // (owner: "another craft quest before we take on this boss? A shield maybe. A very
            // powerful shield") - and a shield with a NAMED weakness to answer is a different object
            // from a shield with a bigger number. Lightning is what Eikthyr does.
            //
            // Blunt resistance comes from the troll: the hide in the recipe is the Breaker's, which
            // is what gives the losable mini-boss a consequence you can hold. Miss the troll and the
            // shield is what it costs you - expensive, visible, and survivable, because this is the
            // LAST step on the craft track and an unfinished craft track is the price of rushing
            // rather than a stalled act.
            new SagaItemDefinition
            {
                // A TOWER shield, and the biggest one the game has (owner: "we need the model to be
                // the biggest we have"). Every stat is set below, so the source prefab is purely
                // which mesh it wears - and for a thing that now answers being hit with a five-metre
                // storm, the silhouette should be the largest object a player can hold.
                //
                // Ordered biggest first, and the chain is long because a prefab name is asset data
                // this assembly cannot verify; the log line "Saga item created: ... from X" says
                // which one actually won, and that is the line to read if it looks wrong.
                SourcePrefab = "ShieldFlametalTower",
                SourceFallbacks = new[]
                {
                    "ShieldBlackmetalTower", "ShieldIronTower", "ShieldCarapace", "ShieldWoodTower",
                    "ShieldSerpentscale", "ShieldBanded", "ShieldWood",
                },
                PrefabName = StormwardPrefab,
                DisplayName = StormwardName,
                Description = "Troll hide over a meadow frame, with three rescued lights bound under " +
                              "the boss. The storm goes around it — and when it has gone around " +
                              "twice, it comes back out. The herd paid for the light; the forest paid " +
                              "for the hide.",
                Tune = shared =>
                {
                    shared.m_blockPower = StormwardBlock;
                    shared.m_blockPowerPerLevel = StormwardBlockPerLevel;
                    shared.m_deflectionForce = 40f;
                    shared.m_deflectionForcePerLevel = 5f;

                    // Set, not inherited: an Ashlands tower shield's ceiling is a late-game number
                    // and would have made the discharge's wear invisible. See StormwardDurability.
                    shared.m_useDurability = true;
                    shared.m_maxDurability = StormwardDurability;
                    shared.m_durabilityPerLevel = StormwardDurabilityPerLevel;
                    shared.m_useDurabilityDrain = 1f;

                    // Parry window worth using, which is the difference between a wall and a tool.
                    // Set explicitly because a tower shield's own value is 1 - no parry at all - and
                    // this one is a tower shield now.
                    shared.m_timedBlockBonus = 2.5f;

                    // THE DISCHARGE. The shield answers being hit with lightning (owner: "can we do
                    // something crzy with it? lighting and aoe when someone hits it?").
                    //
                    // This is the game's OWN machinery, not a bolt-on: Humanoid.BlockAttack counts a
                    // successful block into m_blockCharges when m_buildBlockCharges is set, and at
                    // m_maxBlockCharges it calls m_shared.m_attack.StartWithoutAnimation and resets
                    // the count. Valheim reserved this field for exactly this behaviour, so the
                    // discharge arrives with the player as its attacker, which is the whole reason
                    // to use it: friendly fire, tames, skill factors and the player's own immunity
                    // all follow the normal hit path. A hand-rolled Aoe would have had no owner -
                    // the same trap Thor's bow walked around by using Projectile.m_aoe.
                    //
                    // The attack itself needs the scene, so it is built in TuneWithScene below.
                    shared.m_buildBlockCharges = true;
                    shared.m_maxBlockCharges = StormwardBlockCharges;
                    shared.m_blockChargeDecayTime = StormwardChargeDecaySeconds;

                    // The discharge's damage IS the shield's damage: DoAreaAttack reads
                    // m_weapon.GetDamage(), which is this, per-level scaling included. So these
                    // numbers are also what the item card shows, and the card is not lying.
                    shared.m_damages.m_lightning = StormwardLightning;
                    shared.m_damagesPerLevel.m_lightning = StormwardLightningPerLevel;
                    shared.m_damages.m_blunt = StormwardBlunt;
                    shared.m_damagesPerLevel.m_blunt = StormwardBluntPerLevel;

                    // Whatever the source shield carried, it does not carry here. Flametal is the
                    // first choice and it burns things.
                    shared.m_damages.m_fire = 0f;
                    shared.m_damages.m_frost = 0f;
                    shared.m_damages.m_poison = 0f;
                    shared.m_damages.m_spirit = 0f;
                    shared.m_damagesPerLevel.m_fire = 0f;
                    shared.m_damagesPerLevel.m_frost = 0f;
                    shared.m_damagesPerLevel.m_poison = 0f;
                    shared.m_damagesPerLevel.m_spirit = 0f;

                    // The named answer. VeryResistant rather than Immune: the saga does not hand out
                    // a fight that cannot hurt you, and Eikthyr still has hooves.
                    shared.m_damageModifiers = new List<HitData.DamageModPair>
                    {
                        new HitData.DamageModPair
                        {
                            m_type = HitData.DamageType.Lightning,
                            m_modifier = HitData.DamageModifier.VeryResistant,
                        },
                        new HitData.DamageModPair
                        {
                            m_type = HitData.DamageType.Blunt,
                            m_modifier = HitData.DamageModifier.Resistant,
                        },
                    };
                },
                TuneWithScene = (items, shared) => items.GiveStormwardItsStorm(shared),
            },

            // The light the player takes back off the forest, made into something they can hold.
            //
            // It exists so the bow can COST one (owner: "Thors bow is a bit simple to craft. Can we
            // make some of the light we collect a part of the recipe?"). Until now a rescued light
            // was a number on a scoreboard; a recipe needs an ItemDrop, so the number had to become
            // an object.
            //
            // Cloned from the Mistlands' Wisp, which is exactly this thing already: a caught light,
            // ItemType.Material, with a glow of its own and an icon that reads at a glance. The
            // fallbacks are only there because the source is asset data — a game update that moved
            // it would otherwise take the bow's recipe down with it, silently.
            // --- The Stormsworn, Acts II to V. See the constants above for why each answers what it
            //     answers, and why none of them asks for light.

            new SagaItemDefinition
            {
                // Act II, the Black Forest. Blunt, because everything here swings: troll, brute, and
                // the Elder's own roots. The Stormward already resists blunt, and the two stacking
                // is the point - you arrive in troll country wearing two things made of trolls.
                SourcePrefab = "HelmetBronze",
                SourceFallbacks = new[] { "HelmetIron", "HelmetTrollLeather", "HelmetLeather" },
                PrefabName = StormHelmPrefab,
                DisplayName = StormHelmName,
                Description = "Bronze over troll leather, beaten out at the forge the forest made you " +
                              "build. Nothing in here goes around what it wants.",
                Tune = StormPiece(14f, 2f, HitData.DamageType.Blunt),
            },

            new SagaItemDefinition
            {
                // Act III, the Swamp. Poison, which in the fen is not an attack but a climate -
                // leeches, blobs, and a god made of the stuff.
                SourcePrefab = "ArmorIronChest",
                SourceFallbacks = new[] { "ArmorBronzeChest", "ArmorTrollLeatherChest", "ArmorLeatherChest" },
                PrefabName = StormChestPrefab,
                DisplayName = StormChestName,
                Description = "Iron pulled out of standing water, where men who came before you left " +
                              "it. What killed them is still in the air. It will not get through this.",
                Tune = StormPiece(24f, 3f, HitData.DamageType.Poison),
            },

            new SagaItemDefinition
            {
                // Act IV, the Mountain. Frost, the one biome where the WEATHER is the enemy and the
                // drakes are merely agreeing with it.
                SourcePrefab = "ArmorWolfLegs",
                SourceFallbacks = new[] { "ArmorIronLegs", "ArmorBronzeLegs", "ArmorLeatherLegs" },
                PrefabName = StormLegsPrefab,
                DisplayName = StormLegsName,
                Description = "Silver and wolf, worked where the cold keeps everything it takes. You " +
                              "will stop shivering long before the mountain stops trying.",
                Tune = StormPiece(30f, 4f, HitData.DamageType.Frost),
            },

            new SagaItemDefinition
            {
                // Act V, the Plains. Fire - Yagluth's hand comes down burning, and the growths burn
                // too. A mantle rather than a fourth plate: the cape slot is the one the vanilla
                // tiers leave open, so the set finishes without asking the player to give up
                // anything they already chose.
                SourcePrefab = "CapeLox",
                SourceFallbacks = new[] { "CapeWolf", "CapeTrollHide", "CapeDeerHide" },
                PrefabName = StormCapePrefab,
                DisplayName = StormCapeName,
                Description = "Lox hide, cured in a country that burns. The last thing the storm gave " +
                              "you, and the first thing it asked for nothing in return.",
                Tune = StormPiece(12f, 2f, HitData.DamageType.Fire),
            },

            new SagaItemDefinition
            {
                SourcePrefab = "Wisp",
                SourceFallbacks = new[] { "GreydwarfEye", "SurtlingCore" },
                PrefabName = RescuedLightPrefab,
                DisplayName = RescuedLightName,
                Description = "A light the forest had already taken, carried back in the hand. It is " +
                              "warm, and it is not yours — it belongs to a herd that will not get it " +
                              "back. Spend it on something worth the theft.",
                Tune = shared =>
                {
                    // Stack, so a good night's racing is one slot and not eleven. Weightless on
                    // purpose: a light is not cargo, and the carry-weight validator would otherwise
                    // have an opinion about a recipe that asks for three.
                    shared.m_maxStackSize = 50;
                    shared.m_weight = 0f;

                    // Must survive a portal: the bench is at home and the hunt is not, and an
                    // ingredient that cannot be carried through a portal is a walk, not a cost.
                    shared.m_teleportable = true;
                },
            },
        };

        private const float StrikePollSeconds = 0.05f;

        private GameObject _holder;
        private readonly Dictionary<string, GameObject> _clones = new Dictionary<string, GameObject>();
        private readonly HashSet<string> _reported = new HashSet<string>();

        private ObjectDB _registeredDb;
        private ZNetScene _registeredScene;

        private GameObject _lightning;
        private bool _lightningResolved;
        private float _strikeTimer;
        private readonly HashSet<int> _tunedProjectiles = new HashSet<int>();

        private static readonly FieldInfo ProjectileOwner = typeof(Projectile).GetField("m_owner", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProjectileWeapon = typeof(Projectile).GetField("m_weapon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DbByHash = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DbByData = typeof(ObjectDB).GetField("m_itemByData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SceneNamed = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Every frame. Cheap when nothing changed.</summary>
        public void Ensure()
        {
            try
            {
                var odb = ObjectDB.instance;
                var scene = ZNetScene.instance;

                bool dbChanged = odb != null && !ReferenceEquals(odb, _registeredDb);
                bool sceneChanged = scene != null && !ReferenceEquals(scene, _registeredScene);
                if (!dbChanged && !sceneChanged) return;

                // Clones need the source prefabs, which come from the same registries.
                if (odb != null && odb.m_items != null) EnsureClones(odb, scene);

                if (dbChanged && _clones.Count > 0) RegisterWithDb(odb);
                if (sceneChanged && _clones.Count > 0) RegisterWithScene(scene);

                // The altar's prefab, once the clones exist to be its result.
                if (sceneChanged && _clones.Count > 0) TeachAltarPrefab(scene);
            }
            catch (Exception ex)
            {
                ReportOnce("ensure", "[ICanShowYouTheWorld] Saga items could not be registered: " + ex.Message);
            }
        }

        /// <summary>Every frame while the mod is loaded. Gives Thor's bow its lightning.</summary>
        public void TickStrikes(float dt)
        {
            _strikeTimer += dt;
            if (_strikeTimer < StrikePollSeconds) return;
            _strikeTimer = 0f;

            try
            {
                var player = Player.m_localPlayer;
                if (player == null) return;

                var weapon = player.GetCurrentWeapon();
                if (weapon == null || weapon.m_shared == null || weapon.m_shared.m_name != ThorsBowName) return;

                var lightning = Lightning();
                if (lightning == null || ProjectileOwner == null || ProjectileWeapon == null) return;

                var projectiles = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
                foreach (var p in projectiles)
                {
                    if (p == null) continue;

                    int id = p.GetInstanceID();
                    if (_tunedProjectiles.Contains(id)) continue;

                    var owner = ProjectileOwner.GetValue(p) as Character;
                    if (!ReferenceEquals(owner, player)) continue;

                    var fired = ProjectileWeapon.GetValue(p) as ItemDrop.ItemData;
                    if (fired == null || fired.m_shared == null || fired.m_shared.m_name != ThorsBowName) continue;

                    p.m_spawnOnHit = lightning;
                    p.m_spawnOnHitChance = 1f;

                    // And it strikes AROUND the arrow, not just through it (owner: "I really feel
                    // the Thors bow should have an Aoe component since it does lightning!"). Quite
                    // right: a bolt that stops at one deer is not lightning, it is a nail.
                    //
                    // Projectile.m_aoe is the game's OWN area path, which is why it is used instead
                    // of spawning an Aoe component of our own. The projectile already knows its
                    // owner, so hitOwner/noDamageFriendly mean what they say and the player cannot
                    // be caught in their own storm - whereas a bare Aoe spawned by m_spawnOnHit is
                    // never given an owner at all, and would have had nothing to exempt.
                    //
                    // The bow's damage is not divided between the targets: m_aoe re-uses the hit,
                    // which is generous, and deliberately so - this is the reward for a whole craft
                    // track plus three lights won off the forest.
                    p.m_aoe = ThorsBowAoeRadius;
                    p.m_aoeMaxHitOnce = true;
                    p.m_hitOwner = false;
                    p.m_noDamageFriendly = true;

                    _tunedProjectiles.Add(id);
                }

                // Instance ids are never reused within a session, but the set should not grow
                // for the life of the process either.
                if (_tunedProjectiles.Count > 512) _tunedProjectiles.Clear();
            }
            catch (Exception ex)
            {
                ReportOnce("strikes", "[ICanShowYouTheWorld] Thor's bow could not arm its arrows: " + ex.Message);
            }
        }

        /// <summary>Every source name a definition will accept, in order.</summary>
        private static IEnumerable<string> Candidates(SagaItemDefinition def)
        {
            if (!string.IsNullOrEmpty(def.SourcePrefab)) yield return def.SourcePrefab;
            if (def.SourceFallbacks == null) yield break;

            foreach (var name in def.SourceFallbacks)
                if (!string.IsNullOrEmpty(name)) yield return name;
        }

        /// <summary>
        /// First candidate that resolves to something carrying an ItemDrop. The ItemDrop test is
        /// what makes the fallback worth having: a name can resolve to a prefab that is not an item
        /// at all, and stopping at the first mere existence would pick that and fail one step later.
        /// </summary>
        private static GameObject ResolveSource(SagaItemDefinition def, ObjectDB odb, ZNetScene scene,
            out string resolvedName)
        {
            resolvedName = null;

            foreach (var name in Candidates(def))
            {
                var candidate = odb.GetItemPrefab(name);
                if (candidate == null && scene != null) candidate = scene.GetPrefab(name);
                if (candidate == null) continue;
                if (candidate.GetComponent<ItemDrop>() == null) continue;

                resolvedName = name;
                return candidate;
            }

            return null;
        }

        private void EnsureClones(ObjectDB odb, ZNetScene scene)
        {
            if (_holder == null)
            {
                _holder = new GameObject("saga_items");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }

            foreach (var def in All)
            {
                GameObject existing;
                if (_clones.TryGetValue(def.PrefabName, out existing) && existing != null) continue;

                string sourceName;
                var source = ResolveSource(def, odb, scene, out sourceName);
                if (source == null)
                {
                    ReportOnce(def.PrefabName + "-source",
                        $"[ICanShowYouTheWorld] Saga item '{def.PrefabName}': no source prefab resolved from " +
                        $"{string.Join(", ", Candidates(def))} — item NOT created.");
                    continue;
                }

                var clone = UnityEngine.Object.Instantiate(source, _holder.transform);
                clone.name = def.PrefabName;

                var drop = clone.GetComponent<ItemDrop>();
                var sourceDrop = source.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                {
                    UnityEngine.Object.Destroy(clone);
                    ReportOnce(def.PrefabName + "-drop",
                        $"[ICanShowYouTheWorld] Saga item '{def.PrefabName}': '{sourceName}' is not an item — item NOT created.");
                    continue;
                }

                // Instantiate copies serialised classes by value, so the shared data SHOULD be the
                // clone's own. Checked rather than assumed: if it were the original's, tuning it
                // would retune every Finewood bow in the world.
                if (sourceDrop != null && ReferenceEquals(drop.m_itemData.m_shared, sourceDrop.m_itemData.m_shared))
                {
                    var copy = typeof(ItemDrop.ItemData.SharedData)
                        .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(drop.m_itemData.m_shared, null) as ItemDrop.ItemData.SharedData;
                    if (copy == null)
                    {
                        UnityEngine.Object.Destroy(clone);
                        ReportOnce(def.PrefabName + "-shared",
                            $"[ICanShowYouTheWorld] Saga item '{def.PrefabName}': could not give it its own shared data — item NOT created.");
                        continue;
                    }
                    drop.m_itemData.m_shared = copy;
                }

                var shared = drop.m_itemData.m_shared;
                shared.m_name = def.DisplayName;
                if (!string.IsNullOrEmpty(def.Description)) shared.m_description = def.Description;
                try { def.Tune?.Invoke(shared); }
                catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Saga item '{def.PrefabName}' tuning failed: {ex.Message}"); }

                try { def.TuneWithScene?.Invoke(this, shared); }
                catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Saga item '{def.PrefabName}' scene tuning failed: {ex.Message}"); }

                // What a pack writes into the save is the drop prefab's NAME; it must be ours.
                drop.m_itemData.m_dropPrefab = clone;

                _clones[def.PrefabName] = clone;
                Debug.Log($"[ICanShowYouTheWorld] Saga item created: {def.PrefabName} from {sourceName} (\"{def.DisplayName}\").");
            }
        }

        private void RegisterWithDb(ObjectDB odb)
        {
            var byHash = DbByHash?.GetValue(odb) as Dictionary<int, GameObject>;
            var byData = DbByData?.GetValue(odb) as Dictionary<ItemDrop.ItemData.SharedData, GameObject>;

            foreach (var clone in _clones.Values)
            {
                if (clone == null) continue;
                if (!odb.m_items.Contains(clone)) odb.m_items.Add(clone);

                int hash = clone.name.GetStableHashCode();
                if (byHash != null) byHash[hash] = clone;

                var drop = clone.GetComponent<ItemDrop>();
                if (byData != null && drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                    byData[drop.m_itemData.m_shared] = clone;
            }

            if (byHash == null)
                ReportOnce("db-hash", "[ICanShowYouTheWorld] Saga items: ObjectDB's hash table is not reachable — names may not resolve.");

            _registeredDb = odb;
            Debug.Log($"[ICanShowYouTheWorld] Saga items registered with ObjectDB: {string.Join(", ", _clones.Keys.ToArray())}.");
        }

        private void RegisterWithScene(ZNetScene scene)
        {
            var named = SceneNamed?.GetValue(scene) as Dictionary<int, GameObject>;

            foreach (var clone in _clones.Values)
            {
                if (clone == null) continue;
                if (scene.m_prefabs != null && !scene.m_prefabs.Contains(clone)) scene.m_prefabs.Add(clone);
                if (named != null) named[clone.name.GetStableHashCode()] = clone;
            }

            if (named == null)
                ReportOnce("scene-named", "[ICanShowYouTheWorld] Saga items: ZNetScene's prefab table is not reachable — a dropped saga item may not load.");

            _registeredScene = scene;
        }

        /// <summary>
        /// Builds the Stormward's discharge - the area attack the game fires for us when the shield
        /// has taken enough hits - and gives it the lightning to be seen by.
        /// </summary>
        /// <remarks>
        /// An <c>Attack</c> is a plain serialisable class with a public constructor, so one can be
        /// made here rather than stolen off a prefab. Checked in the IL before relying on it, along
        /// with the two facts the whole design rests on:
        ///
        /// 1. <c>DoAreaAttack</c> skips the attacker's own GameObject twice - once on the collider
        ///    and once on the resolved hit object - so the player cannot be caught in their own
        ///    storm. With <c>m_hitFriendly</c> false, neither can their tames.
        /// 2. It sets the player as the attacker via <c>HitData.SetAttacker</c> and reads the damage
        ///    from <c>m_weapon.GetDamage()</c>, which is the shield's own.
        ///
        /// The one genuine hazard is that the three layer masks <c>DoAreaAttack</c> reads are private
        /// STATICS, populated the first time any attack goes through <c>Attack.Start</c>. In practice
        /// the player has swung something long before they own this shield - but "in practice" is how
        /// a first discharge that hits nothing gets shipped, so <see cref="EnsureAttackMasks"/>
        /// fills them itself if they are still zero.
        /// </remarks>
        internal void GiveStormwardItsStorm(ItemDrop.ItemData.SharedData shared)
        {
            EnsureAttackMasks();

            var fx = Lightning();

            var discharge = new Attack
            {
                m_attackType = Attack.AttackType.Area,

                // No animation: the game calls StartWithoutAnimation for this, which goes straight to
                // OnAttackTrigger. Nothing here plays a swing, and nothing needs to - the player is
                // mid-block, and a shield that recoils by itself is the right read anyway.
                m_attackAnimation = "",
                m_attackChainLevels = 1,

                // Centred on the player rather than thrown forward: whatever just hit the shield is
                // in contact with it, and so is whatever else has crowded in behind.
                m_attackRange = 0f,
                m_attackOffset = 0f,
                m_attackHeight = 1.2f,
                m_attackRayWidth = StormwardDischargeRadius,

                // One sphere. The extra character sweeps exist for long weapons reaching up and down
                // a target, and here they would only re-test the same ring at two more heights.
                m_attackRayWidthCharExtra = 0f,
                m_attackHeightChar1 = 0f,
                m_attackHeightChar2 = 0f,

                // It clears the ring, so it must not stop at the first thing in it.
                m_multiHit = true,
                m_lowerDamagePerHit = false,

                // Not the terrain and not your own animals. The storm is aimed at what is pressing
                // on the shield, and a discharge that dug a hole in the ground every few blocks
                // would rearrange the player's own doorway.
                m_hitTerrain = false,
                m_hitFriendly = false,

                m_damageMultiplier = 1f,

                // Multiplier ONE, and the force set on the item instead. DoAreaAttack computes the
                // knockback as the item's m_attackForce times this, and a shield's own attack force
                // is whatever the source prefab happened to carry - which for a shield is very
                // possibly zero, since vanilla shields never attack. A multiplier on zero is zero,
                // so the knockback would silently not exist; a multiplier on a big inherited number
                // would fire things into orbit. Same defect the bow had when its pierce was
                // inherited, and the same fix: set the value, do not scale an unknown.
                m_forceMultiplier = 1f,
                m_staggerMultiplier = StormwardDischargeStagger,

                // Free. It is paid for by the stamina the block itself cost, and by having had to be
                // hit twice to earn it.
                m_attackStamina = 0f,
                m_attackEitr = 0f,
                m_attackHealth = 0f,

                // It is thunder. Things hear it.
                m_attackHitNoise = 40f,

                // No skill from it: BlockAttack already raises Blocking for the hit that caused this,
                // and paying twice for one block would make the shield train itself.
                m_raiseSkillAmount = 0f,
            };

            if (fx != null)
            {
                discharge.m_hitEffect = EffectListOf(fx);
                discharge.m_triggerEffect = EffectListOf(fx);
            }
            _stormFlashDone = fx != null;

            shared.m_attack = discharge;

            // The knockback the discharge scales by. See m_forceMultiplier above for why this is set
            // here rather than inferred from the source shield.
            shared.m_attackForce = StormwardDischargeForce;

            // m_blockChargeEffects is DELIBERATELY left alone. Putting the same lightning on it was
            // the first instinct - feedback while the shield charges - and it is a lie: the first
            // block would flash exactly like the second, so the player could not tell a stored hit
            // from a discharge. A readout that cannot be distinguished from the thing it reports is
            // worse than no readout, which this mode has paid to learn twice now (the dev clock, the
            // stale dev-key banner). The discharge's own flash is the only lightning, and it means
            // one thing.

            Debug.Log($"[ICanShowYouTheWorld] Stormward discharge: {StormwardDischargeRadius}m, " +
                      $"{StormwardLightning} lightning, every {StormwardBlockCharges} blocks" +
                      (fx == null ? " (no lightning effect resolved)." : $", flash '{fx.name}'."));
        }

        /// <summary>
        /// Gives the Stormward's discharge its flash, once there is a scene to find one in.
        /// </summary>
        /// <remarks>
        /// The item is cloned as early as the registries allow - deliberately, since a saga item in a
        /// pack must resolve before the pack loads - and at that moment ZNetScene.instance can still
        /// be null, which is what happened: <c>Ensure</c> creates clones as soon as the ObjectDB has
        /// items, without waiting for the scene. So <c>Lightning()</c> returned null, the effect lists
        /// were baked empty, and the discharge fired its damage with nothing to see. The log said so
        /// in as many words - "(no lightning effect resolved)" - and nobody read it until the shield
        /// was reported as behaving like an ordinary shield.
        ///
        /// Retried from the tick rather than fixed by delaying the clone, because the early clone is
        /// load-bearing and the flash is not. An effect list is a field on a live object; it can be
        /// filled in at any time before the first block.
        /// </remarks>
        private void EnsureStormFlash()
        {
            if (_stormFlashDone) return;

            if (!_clones.TryGetValue(StormwardPrefab, out var clone) || clone == null) return;

            var shared = clone.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (shared?.m_attack == null) return;

            var fx = Lightning();
            if (fx == null) return;

            shared.m_attack.m_hitEffect = EffectListOf(fx);
            shared.m_attack.m_triggerEffect = EffectListOf(fx);
            _stormFlashDone = true;

            Debug.Log($"[ICanShowYouTheWorld] Stormward discharge flash: '{fx.name}' " +
                      "(resolved after the item was made).");
        }

        private bool _stormFlashDone;

        private static EffectList EffectListOf(GameObject prefab) =>
            new EffectList
            {
                m_effectPrefabs = new[]
                {
                    new EffectList.EffectData { m_prefab = prefab, m_enabled = true, m_variant = -1 },
                },
            };

        /// <summary>
        /// Makes sure <c>Attack</c>'s three layer masks are populated, since an area attack reads
        /// them and they are zero until some attack has been through <c>Attack.Start</c>.
        /// </summary>
        /// <remarks>
        /// The layer name lists are copied verbatim out of <c>Attack.Start</c>'s IL. They are private
        /// statics, hence the reflection; if a game update renames a layer this writes the same wrong
        /// answer the game would, which is the right kind of wrong.
        /// </remarks>
        private static void EnsureAttackMasks()
        {
            if (_masksChecked) return;
            _masksChecked = true;

            try
            {
                SetMaskIfZero("m_attackMask", new[]
                {
                    "Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "character",
                    "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle",
                });
                SetMaskIfZero("m_attackMaskTerrain", new[]
                {
                    "Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain",
                    "character", "character_net", "character_ghost", "hitbox", "character_noenv",
                    "vehicle",
                });
                SetMaskIfZero("m_attackMaskCharacters", new[]
                {
                    "character", "character_net", "character_ghost", "hitbox", "character_noenv",
                    "vehicle",
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Attack layer masks could not be primed: " + ex.Message);
            }
        }

        private static void SetMaskIfZero(string field, string[] layers)
        {
            var f = typeof(Attack).GetField(field, BindingFlags.Static | BindingFlags.NonPublic);
            if (f == null) return;
            if (!(f.GetValue(null) is int current) || current != 0) return;

            f.SetValue(null, LayerMask.GetMask(layers));
            Debug.Log($"[ICanShowYouTheWorld] Primed Attack.{field} (the game had not built it yet).");
        }

        private static bool _masksChecked;

        // ------------------------------------------------------------------ the storm-altar
        //
        // Valheim's Obliterator - the lever-and-lightning machine that turns what you feed it into
        // coal - carries a PUBLIC conversion table: Incinerator.m_conversions, a list of
        // IncineratorConversion { m_requirements, m_result, m_resultAmount, m_priority }. Put in
        // exactly these things, take out exactly that thing. An EverQuest combine, shipped in the
        // base game and used by nothing but coal (owner: "if the user oblitarates 4 specific things,
        // for instance, replace with a new object? Sort of like in Everquest, old school mmo").
        //
        // So the saga's lightning items can be MADE OF LIGHTNING, in the machine that makes it,
        // rather than tapped together at a bench. The lever animation, the strike and the
        // m_lightingAOEs flash all come free because it is the game's own path.
        //
        // The Obliterator is found by its COMPONENT and never by name: a prefab name is asset data
        // this assembly cannot verify, and the one time this codebase guessed one it spent an
        // evening on it. Whatever it is called, it is the thing with an Incinerator on it, and the
        // log says what that turned out to be.
        //
        // For the record, a running 1.0.15 printed it: the prefab is plain lowercase
        // 'incinerator'. Written down because it is useful for console spawning and for reading old
        // logs - NOT used in code, because the component search cannot go stale and a hardcoded
        // name can.

        /// <summary>How many of each thing the altar asks for. Deliberately the bench recipe's cost.</summary>
        /// <remarks>
        /// The same price by a different road, not a cheaper one. Until the Obliterator is something
        /// Act I can reach, the bench has to stay open or the shield is unmakeable - so the altar is
        /// an alternative, and an alternative that undercut the bench would simply retire it.
        ///
        /// When the altar becomes the point (see the story bible: it is to be Act I's third act-long
        /// thread and the forge for every storm item after) this table is where the divergence goes:
        /// things only the lightning can bind, which the bench then does not list at all.
        /// </remarks>
        private static readonly (string item, int amount)[] StormwardCombine =
        {
            ("Wood", 20), ("Resin", 20), ("TrollHide", 10), ("DeerHide", 10),
        };

        private readonly HashSet<int> _alteredAltars = new HashSet<int>();
        private float _altarTimer;
        private bool _altarFound;

        /// <summary>
        /// Teaches every Obliterator in the world to give back the Stormward.
        /// </summary>
        /// <remarks>
        /// Both the prefab and the live instances, because neither alone is enough: patching only
        /// the prefab misses an Obliterator that was already standing when the mod loaded, and
        /// patching only instances loses the change the moment the player builds a new one.
        /// Idempotent by result - a conversion whose result is already ours is left alone - so this
        /// can run as often as it likes.
        /// </remarks>
        public void TickStormAltar(float dt)
        {
            _altarTimer += dt;
            if (_altarTimer < AltarPollSeconds) return;
            _altarTimer = 0f;

            try
            {
                if (!_clones.TryGetValue(StormwardPrefab, out var shieldClone) || shieldClone == null) return;

                var result = shieldClone.GetComponent<ItemDrop>();
                if (result == null) return;

                EnsureStormFlash();

                // The PREFAB pass, retried until it has actually landed.
                //
                // It used to run once, from Ensure, gated on the scene reference changing - and that
                // is a one-shot with no second chance. If the rescued light's clone was not ready on
                // the frame it fired, the re-cost silently never happened, the piece kept the price
                // the game shipped (a Thunder Stone from a trader two biomes away), and the only
                // retry path was a live instance of a piece nobody could build. A chicken-and-egg
                // failure that would have looked exactly like "the anvil is not in my hammer".
                if (!_altarTaught) TeachAltarPrefab(ZNetScene.instance);

                foreach (var inc in UnityEngine.Object.FindObjectsByType<Incinerator>(FindObjectsSortMode.None))
                {
                    if (inc == null) continue;
                    if (!_alteredAltars.Add(inc.GetInstanceID())) continue;
                    TeachAltar(inc, result);
                }
            }
            catch (Exception ex)
            {
                ReportOnce("altar", "[ICanShowYouTheWorld] The storm-altar could not be taught: " + ex.Message);
            }
        }

        /// <summary>The prefab pass, so an Obliterator built later is born knowing it.</summary>
        /// <summary>
        /// The Storm-Anvil's prefab, found by its component, or null.
        /// </summary>
        /// <remarks>
        /// Exists so a tester can plant one without anybody having to know what the thing is called.
        /// The name is asset data - the one fact this whole thread was missing - and finding the
        /// object by the component it carries answers the question without needing it.
        /// </remarks>
        public GameObject StormAnvilPrefab()
        {
            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return null;

            foreach (var prefab in scene.m_prefabs)
                if (prefab != null && prefab.GetComponent<Incinerator>() != null) return prefab;

            return null;
        }

        private void TeachAltarPrefab(ZNetScene scene)
        {
            if (scene == null || scene.m_prefabs == null) return;
            if (!_clones.TryGetValue(StormwardPrefab, out var shieldClone) || shieldClone == null) return;

            var result = shieldClone.GetComponent<ItemDrop>();
            if (result == null) return;

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var inc = prefab.GetComponent<Incinerator>();
                if (inc == null) continue;

                if (!_altarFound)
                {
                    _altarFound = true;
                    Debug.Log($"[ICanShowYouTheWorld] The storm-altar is '{prefab.name}' " +
                              $"(found by its Incinerator, not by name).");
                }

                EnsureAnvilInHammer(prefab);
                TeachAltar(inc, result);

                // Only now is the prefab pass done with. Both halves have to have landed: a
                // conversion the lever can find, and a price the Meadows can pay.
                _altarTaught = _altarConversionDone && _anvilRecosted;
            }
        }

        /// <summary>True once the prefab carries both the combine and the saga's price.</summary>
        private bool _altarTaught;
        private bool _altarConversionDone;

        /// <summary>
        /// What the saga calls the Obliterator.
        /// </summary>
        /// <remarks>
        /// "Obliterator" is a machine's name in a world that has no machines, and the saga is about to
        /// make this thing a fixture (owner: "we could make the obliterator a focal topic of act1 that
        /// you then use throughout the game to craft lighting items"). It needed a name in the same
        /// family as the rest - Stormward, Stormsworn - and an ANVIL is where the blow lands, which is
        /// precisely what this is: you put a thing down and the sky hits it.
        ///
        /// Only Piece.m_name is ours to set. The lever's own hover text is a localisation token
        /// ($piece_incinerator) and will still say Obliterator, which is a seam worth knowing about
        /// rather than a thing to go fighting.
        /// </remarks>
        private const string AnvilName = "Storm-Anvil";

        private const string AnvilDescription =
            "The sky does the work. Put down what you have taken back, pull the lever, and see " +
            "whether it comes up as something or as ash. Be exact: it knows a few shapes, and " +
            "everything it does not recognise it burns.";

        /// <summary>
        /// What the Storm-Anvil costs to raise, in Act I materials only.
        /// </summary>
        /// <remarks>
        /// The whole reason this exists (owner: "we need to give the player one, through a quest or
        /// otherwise. Recipe that requires items from act 1"). Vanilla gates the Obliterator behind a
        /// Thunder Stone bought from Haldor, who lives in the Black Forest - so as shipped it is
        /// unreachable in the Meadows, and the saga's first act cannot be built on a thing its player
        /// cannot have. The fishing rod had the same problem and the same answer.
        ///
        /// Stone, because an altar is stone. And ONE rescued light, which is the part that matters:
        /// the Storm-Anvil is a machine that breaks light, and to raise it you give one up. That is
        /// the act's own question - what you do with what you took - asked with the player's hand on
        /// it, and it costs one third of what the shield wants rather than anything that could stall
        /// a chain.
        ///
        /// It doubles as the unlock. Valheim shows a piece once every material is a KNOWN one, so the
        /// anvil appears in the hammer exactly when the hunt has started paying out - which is the
        /// moment the shade hands over its kept light.
        /// </remarks>
        private static readonly (string item, int amount)[] AnvilCost =
        {
            ("Stone", 20), ("Wood", 10), ("Resin", 10),
        };

        private const int AnvilLightCost = 1;

        /// <summary>
        /// Gives the Obliterator the saga's name, the saga's price, and a place in the hammer.
        /// </summary>
        private void DressTheAnvil(Incinerator inc)
        {
            var piece = inc.GetComponent<Piece>();
            if (piece == null) return;

            if (piece.m_name != AnvilName)
            {
                piece.m_name = AnvilName;
                piece.m_description = AnvilDescription;
            }

            RecostAnvil(piece);
        }

        private void RecostAnvil(Piece piece)
        {
            if (_anvilRecosted) return;

            var reqs = new List<Piece.Requirement>();
            foreach (var (item, amount) in AnvilCost)
            {
                var drop = ItemPrefab(item);
                if (drop == null)
                {
                    ReportOnce("anvil-" + item,
                        $"[ICanShowYouTheWorld] The Storm-Anvil wants '{item}' and the game has no such " +
                        "item — its price is left as the game shipped it.");
                    return;
                }
                reqs.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_recover = true });
            }

            var light = _clones.TryGetValue(RescuedLightPrefab, out var lightClone) && lightClone != null
                ? lightClone.GetComponent<ItemDrop>()
                : null;
            if (light == null) return;   // Not yet cloned; the next pass will have it.

            // NOT recoverable. Tearing the altar down does not give the light back - it went into the
            // thing, which is the only way that cost means anything.
            reqs.Add(new Piece.Requirement { m_resItem = light, m_amount = AnvilLightCost, m_recover = false });

            piece.m_resources = reqs.ToArray();

            // A workbench, which the Meadows have. Whatever it asked for before was a later act's
            // station by definition, since what it asked FOR was later-act materials.
            var bench = ZNetScene.instance?.GetPrefab("piece_workbench")?.GetComponent<CraftingStation>();
            if (bench != null) piece.m_craftingStation = bench;

            _anvilRecosted = true;
            Debug.Log($"[ICanShowYouTheWorld] Storm-Anvil re-costed: " +
                      string.Join(", ", AnvilCost.Select(r => $"{r.amount} {r.item}")) +
                      $", {AnvilLightCost} {RescuedLightName}.");
        }

        private bool _anvilRecosted;

        /// <summary>
        /// Makes sure the hammer can actually build it.
        /// </summary>
        /// <remarks>
        /// Belt and braces, and the braces are the point: re-costing the piece only helps if the
        /// piece is in the hammer's table to begin with, and whether vanilla puts it there or adds it
        /// when the Thunder Stone is acquired is asset behaviour this assembly cannot read. Adding it
        /// when absent answers the question without needing to know.
        /// </remarks>
        private void EnsureAnvilInHammer(GameObject anvilPrefab)
        {
            if (_anvilInHammer || anvilPrefab == null) return;

            var hammer = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
            var table = hammer?.m_itemData?.m_shared?.m_buildPieces;
            if (table?.m_pieces == null) return;

            _anvilInHammer = true;

            if (table.m_pieces.Contains(anvilPrefab)) return;

            table.m_pieces.Add(anvilPrefab);
            Debug.Log("[ICanShowYouTheWorld] The Storm-Anvil was not in the hammer's table; added.");
        }

        private bool _anvilInHammer;

        private void TeachAltar(Incinerator inc, ItemDrop result)
        {
            DressTheAnvil(inc);

            if (inc.m_conversions == null)
                inc.m_conversions = new List<Incinerator.IncineratorConversion>();

            // Already knows it. Checked by RESULT rather than by a flag, so a reload, a rebuild and
            // a second pass over the same object all come to the same answer.
            foreach (var existing in inc.m_conversions)
                if (existing != null && ReferenceEquals(existing.m_result, result))
                {
                    _altarConversionDone = true;
                    return;
                }

            var reqs = new List<Incinerator.Requirement>();
            foreach (var (item, amount) in StormwardCombine)
            {
                var drop = ItemPrefab(item);
                if (drop == null)
                {
                    ReportOnce("altar-" + item,
                        $"[ICanShowYouTheWorld] The storm-altar wants '{item}' and the game has no such item — " +
                        "the Stormward combine is NOT registered.");
                    return;
                }
                reqs.Add(new Incinerator.Requirement { m_resItem = drop, m_amount = amount });
            }

            var lightDrop = _clones.TryGetValue(RescuedLightPrefab, out var lightClone) && lightClone != null
                ? lightClone.GetComponent<ItemDrop>()
                : null;
            if (lightDrop != null)
                reqs.Add(new Incinerator.Requirement { m_resItem = lightDrop, m_amount = StormwardLightCost });

            inc.m_conversions.Add(new Incinerator.IncineratorConversion
            {
                m_requirements = reqs,
                m_result = result,
                m_resultAmount = 1,

                // Above the coal. The default conversion is what happens to anything with no rule of
                // its own, and a shield's worth of troll hide going to coal because the table was
                // read in the wrong order is the one outcome here that cannot be undone.
                m_priority = 100,
                m_requireOnlyOneIngredient = false,
            });

            _altarConversionDone = true;

            Debug.Log($"[ICanShowYouTheWorld] Storm-altar combine registered: {reqs.Count} things in, " +
                      $"{StormwardName} out.");
        }

        private ItemDrop ItemPrefab(string name)
        {
            var odb = ObjectDB.instance;
            var go = odb != null ? odb.GetItemPrefab(name) : null;
            return go != null ? go.GetComponent<ItemDrop>() : null;
        }

        private const float AltarPollSeconds = 3f;

        private GameObject Lightning()
        {
            if (_lightningResolved && _lightning != null) return _lightning;

            var scene = ZNetScene.instance;
            if (scene == null) return null;

            _lightningResolved = true;
            _lightning = LightningPrefabs.Select(scene.GetPrefab).FirstOrDefault(p => p != null);
            if (_lightning == null)
                ReportOnce("lightning", "[ICanShowYouTheWorld] No lightning effect prefab resolved; Thor's bow strikes without a flash.");
            else
                Debug.Log($"[ICanShowYouTheWorld] Thor's bow lightning effect: {_lightning.name}.");

            return _lightning;
        }

        private void ReportOnce(string key, string message)
        {
            if (!_reported.Add(key)) return;
            Debug.LogError(message);
        }
    }
}
