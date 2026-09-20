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

        /// <summary>Candidate lightning effects, the Herald's list; the first that resolves is used.</summary>
        private static readonly string[] LightningPrefabs =
            { "fx_eikthyr_stomp", "vfx_lightning", "fx_lightning", "fx_Eikthyr_stomp" };

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
                // Serpentscale first, for the same reason the bow moved off Finewood: a shield
                // this expensive should not be the starter plank with better numbers. Every stat is
                // set below, so the source is the silhouette and nothing else.
                SourcePrefab = "ShieldSerpentscale",
                SourceFallbacks = new[] { "ShieldBronzeBuckler", "ShieldBanded", "ShieldWoodTower", "ShieldWood" },
                PrefabName = StormwardPrefab,
                DisplayName = StormwardName,
                Description = "Troll hide over a meadow frame, with three rescued lights bound under " +
                              "the boss. The storm goes around it. The herd paid for the light; the " +
                              "forest paid for the hide.",
                Tune = shared =>
                {
                    shared.m_blockPower = StormwardBlock;
                    shared.m_blockPowerPerLevel = StormwardBlockPerLevel;
                    shared.m_deflectionForce = 40f;
                    shared.m_deflectionForcePerLevel = 5f;

                    // Parry window worth using, which is the difference between a wall and a tool.
                    shared.m_timedBlockBonus = 2.5f;

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
