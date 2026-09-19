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
        /// The bow's own damage, on top of the Finewood bow it is cut from (32 pierce). Raised from
        /// 20/+4 after the first play (owner: "we COULD increase the dmg just a bit").
        /// </summary>
        private const float ThorsBowLightning = 26f;
        private const float ThorsBowLightningPerLevel = 5f;

        /// <summary>Candidate lightning effects, the Herald's list; the first that resolves is used.</summary>
        private static readonly string[] LightningPrefabs =
            { "fx_eikthyr_stomp", "vfx_lightning", "fx_lightning", "fx_Eikthyr_stomp" };

        public static readonly SagaItemDefinition[] All =
        {
            new SagaItemDefinition
            {
                SourcePrefab = "BowFineWood",
                PrefabName = ThorsBowPrefab,
                DisplayName = ThorsBowName,
                Description = "Strung from the herd’s hide, sealed with the forest’s resin, taught by " +
                              "a hunter who never loosed. The storm in it is Eikthyr’s own, turned.",
                Tune = shared =>
                {
                    shared.m_damages.m_lightning = ThorsBowLightning;
                    shared.m_damagesPerLevel.m_lightning = ThorsBowLightningPerLevel;
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
