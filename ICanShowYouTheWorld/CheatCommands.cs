using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;
using System.Reflection;
using System.Linq;

namespace ICanShowYouTheWorld
{
    // Boss configuration and reveal logic
    public static class BossData
    {
        public static readonly (string prefab, string name)[] All = {
            ("Eikthyrnir","Eikthyr"),("GDKing","The Elder"),("Bonemass","Bonemass"),
            ("Dragonqueen","Moder"),("GoblinKing","Yagluth"),("FaderLocation","Fader")
        };
        public static void Reveal((string prefab, string name) b)
        {
            Game.instance.DiscoverClosestLocation(
                b.prefab,
                Player.m_localPlayer.transform.position,
                b.name,
                (int)Minimap.PinType.Boss);
        }
    }

    public static class LocationCheats
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try to extract a prefab/location name from a ZoneLocation, regardless of backing type.
        static string GetLocationName(object zoneLocation)
        {
            if (zoneLocation == null) return null;
            var t = zoneLocation.GetType();

            // 1) Most versions have a direct string
            var fName = t.GetField("m_prefabName", BF);
            if (fName != null)
                return fName.GetValue(zoneLocation) as string;

            // 2) Some versions wrap the data in m_location
            var fLoc = t.GetField("m_location", BF);
            if (fLoc != null)
            {
                var inner = fLoc.GetValue(zoneLocation);
                var innerName = inner?.GetType().GetField("m_prefabName", BF)?.GetValue(inner) as string;
                if (!string.IsNullOrEmpty(innerName)) return innerName;
            }

            // 3) Fall back to m_prefab (GameObject in old builds, SoftReference in new builds)
            var fPrefab = t.GetField("m_prefab", BF);
            if (fPrefab != null)
            {
                var val = fPrefab.GetValue(zoneLocation);
                if (val is GameObject go) return go.name;

                if (val != null)
                {
                    // SoftReference cases: try common fields/properties
                    var pName = val.GetType().GetField("m_name", BF)?.GetValue(val) as string
                             ?? val.GetType().GetProperty("Name", BF)?.GetValue(val, null) as string;

                    // Some soft refs expose an underlying UnityEngine.Object in "m_asset"
                    var fAsset = val.GetType().GetField("m_asset", BF);
                    if (fAsset != null)
                    {
                        var asset = fAsset.GetValue(val) as UnityEngine.Object;
                        if (asset != null) return asset.name;
                    }

                    if (!string.IsNullOrEmpty(pName)) return pName;
                }
            }

            return null;
        }

        // Reveal the closest instance of a RANDOM match for a filter (e.g., "MorgenHole")
        public static void RevealClosestRandom(string filter,
                                               Minimap.PinType pin = Minimap.PinType.Boss,
                                               string labelOverride = null)
        {
            var names = LocationCheats.ListLocationNames(filter); // uses your soft-ref safe scanner
            if (names == null || names.Count == 0)
            {
                CheatCommands.Show($"No locations found for \"{filter}\"");
                return;
            }

            int idx = UnityEngine.Random.Range(0, names.Count);
            string picked = names[idx];

            bool ok = LocationCheats.RevealClosest(picked, pin, labelOverride ?? picked);
            CheatCommands.Show(ok ? $"Revealed nearest {picked}" : $"Failed to reveal {picked}");
        }

        public static List<string> ListLocationNames(string filter = null)
        {
            var zs = ZoneSystem.instance;
            var outList = new List<string>();
            if (!zs) return outList;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var zl in zs.m_locations)
            {
                var name = GetLocationName(zl);
                if (string.IsNullOrEmpty(name)) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (seen.Add(name)) outList.Add(name);
            }
            outList.Sort(StringComparer.OrdinalIgnoreCase);
            return outList;
        }

        public static bool RevealClosest(string partialOrExactName,
                                         Minimap.PinType pin = Minimap.PinType.Boss,
                                         string displayName = null)
        {
            if (Game.instance == null || Player.m_localPlayer == null || ZoneSystem.instance == null)
                return false;

            // Find a matching name first (to avoid passing an unknown id)
            string match = null;
            foreach (var zl in ZoneSystem.instance.m_locations)
            {
                var name = GetLocationName(zl);
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf(partialOrExactName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = name;
                    break;
                }
            }
            if (match == null) return false;

            Game.instance.DiscoverClosestLocation(
                match,
                Player.m_localPlayer.transform.position,
                displayName ?? match,
                (int)pin
            );
            return true;
        }
    }

    public static class CheatCommands
    {
        // States
        public static bool GodMode { get; private set; }
        public static bool RenewalActive { get; private set; }
        public static bool AOERenewalActive { get; private set; }
        public static bool GhostMode { get; private set; }
        public static bool CloakActive { get; private set; }
        public static bool MelodicActive { get; private set; }
        public static bool GiftActive { get; private set; }

        private static int guardianIndex = 0;
        public static int DamageCounter { get; private set; }
        public static string CurrentGuardianName => guardians[guardianIndex];
        private static readonly string[] guardians = { "GP_Eikthyr", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Fader", "GP_Queen" };
        // private static readonly string[] combatPets = { "Wolf", "DvergerMageSupport", "Asksvin" };
        private static readonly string[] housePets = { "Chicken" };
        private static readonly string[] combatPets = { "Skeleton_Friendly" };
        private static readonly string[] petNames = { "Bob", "Ralf", "Liam", "Olivia", "Elijah", "Kebober" };

        // Single base power
        public static float AoePower = 25f;
        private const float MinAoePower = 5f;
        private const float MaxAoePower = 200f;
        private const float AoeDmgScale = 1.5f;

        // --- Player stats snapshot ---
        private static float origBaseHP;
        private static float origBlockStaminaDrain;
        private static float origRunStaminaDrain;
        private static float origStaminaRegen;
        private static float origStaminaRegenDelay;
        private static float origEitrRegen;
        private static float origEitrRegenDelay;
        private static float origMaxCarryWeight;

        public static bool AutoDodgeActive = false;
        private const float DodgeDistance = 3f;    // how far to dodge
        private const float DodgeCooldown = 1f;    // seconds between dodges
        private static float lastDodgeTime = -999f;

        public static bool KnockbackAoEActive = false;
        public static float KnockbackRadius = 10f;
        public static float KnockbackStrength = 300f;

        public static bool BarrierAoEActive = false;
        public static float BarrierRadius = 10f;
        public static float BarrierStrength = 5f;     // how hard to shove them
        public static float BarrierFalloff = 1f;     // 1 = full strength at all ranges; >1 = stronger near center

        public static bool FreezeMonstersActive = false;

        // --- Food snapshot ---
        private static Dictionary<Player.Food, float> origFoodTimes;

        // --- Item snapshot struct + storage ---
        private struct ItemSnapshot
        {
            public float durDrain, maxDur, curDur, armor;
        }
        private static Dictionary<ItemDrop.ItemData, ItemSnapshot> origItemStats;

        // 1.A) The list of available prefabs
        private static readonly string[] SpawnPrefabs = {
                "skip",
                "skip",

                // --- Pets ------------
                "Hen",
                "Asksvin",
                "Boar",
                "Boar_piggy",
                "Wolf",
                "Wolf_cub",

                // --- Spawners -----------
                "Spawner_Charred",
                "Spawner_Charred_Archer",
                "Spawner_Twitcher",
//                "Spawner_CharredStone_Elite",
//                "Spawner_Charred_Mage",
                "Spawner_Volture",

                // --- aoes ------
                "Fader_MeteorSmash_AOE", // invis dmg
                //"Fader_Fissure_AOE",
                //"Fader_Flamebreath_AOE", // "wall of fire"                
                //"FenringIceNova_aoe", // slow?
                //"shieldgenerator_attack",
                //"aoe_nova",

                // --- giant things -----
    //            "giant_arm",
    //            "giant_brain",
    //            "giant_helmet1",
    //            "giant_helmet2",
    //            "giant_ribs",
    //            "giant_skull",
    //            "giant_sword1",
    //            "giant_sword2"
            };
        private static int prefabIndex = 0;
        public static string CurrentPrefab => SpawnPrefabs[prefabIndex];
        public static string PrevPrefab => (SpawnPrefabs != null && SpawnPrefabs.Length > 0)
        ? SpawnPrefabs[(prefabIndex - 1 + SpawnPrefabs.Length) % SpawnPrefabs.Length]
        : "";

        public static string NextPrefab => (SpawnPrefabs != null && SpawnPrefabs.Length > 0)
        ? SpawnPrefabs[(prefabIndex + 1 + SpawnPrefabs.Length) % SpawnPrefabs.Length]
        : "";

        static CheatCommands()
        {
            // register periodic callbacks - todo: can do this better
            PeriodicManager.Register(50, () => { if (BarrierAoEActive) StaggerAoE(); });
            PeriodicManager.Register(50, () => { if (RenewalActive) Invigorate(); });
            PeriodicManager.Register(60, () => { if (AOERenewalActive) AoeRegen(10f); });
            PeriodicManager.Register(150, () => { if (MelodicActive) SlowMonsters(); });
            PeriodicManager.Register(75, () => { if (CloakActive) DamageAoE(5f); }); //todo: config somewhere else

            // Default values
            DamageCounter = 5;
        }

        public static void Discover(string discover = "")
        {
            // List matches in console
            var hits = LocationCheats.ListLocationNames("Ashland");
            foreach (var n in hits) Console.instance.Print("[Loc] " + n);

            // Pin the closest Ashlands ruins
            if (!LocationCheats.RevealClosest("AshlandRuins"))
                Show("AshlandRuins not found");
        }

        // Convenience wrappers:
        public static void RevealClosestAshlandsCave()
        {
            // Putrid Holes are MorgenHole1/2/3 – filter by prefix, pick one at random
            LocationCheats.RevealClosestRandom("MorgenHole", Minimap.PinType.Boss, "Putrid Hole");
        }

        public static void RevealClosestRetoTomb()
        {
            // Exact ID is PlaceofMystery3; passing the full name keeps it deterministic
            bool ok = LocationCheats.RevealClosest("PlaceofMystery3", Minimap.PinType.Boss, "Tomb of Lord Reto");
            Show(ok ? "Revealed nearest Reto Tomb" : "Reto Tomb not found");
        }

        public static void ToggleAutoDodge()
        {
            AutoDodgeActive = !AutoDodgeActive;
            var player = Player.m_localPlayer;
            if (AutoDodgeActive)
                player.m_onDamaged += HandleAutoDodge;
            else
                player.m_onDamaged -= HandleAutoDodge;
            Show($"Auto-Dodge {(AutoDodgeActive ? "ON" : "OFF")}");
        }

        private static void HandleAutoDodge(float damage, Character attacker)
        {
            if (!AutoDodgeActive) return;

            // cooldown
            if (Time.time - lastDodgeTime < DodgeCooldown) return;

            var player = Player.m_localPlayer;
            // direction from attacker to player
            Vector3 dir = (player.transform.position - attacker.transform.position).normalized;

            // pick a perpendicular vector (left or right randomly)
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            if (UnityEngine.Random.value > 0.5f) perp = -perp;

            // target dodge position
            Vector3 target = player.transform.position + perp * DodgeDistance;

            // project to ground so you don't end up in mid-air
            if (Physics.Raycast(target + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                target.y = hit.point.y + 0.1f;

            // teleport the player
            player.TeleportTo(target, player.transform.rotation, distantTeleport: true);

            lastDodgeTime = Time.time;
        }

        public static void ToggleKnockbackAoE()
        {
            KnockbackAoEActive = !KnockbackAoEActive;
            Show($"AoE Knockback {(KnockbackAoEActive ? "ENABLED" : "DISABLED")}");
        }

        // --- guardian gift
        public static void ToggleGuardianGift()
        {
            var p = Player.m_localPlayer;
            if (!RequireGodMode("Guardian's Gift")) return;

            if (!GiftActive)
            {
                // Snapshot player fields we’re about to change
                origBaseHP = p.m_baseHP;
                origBlockStaminaDrain = p.m_blockStaminaDrain;
                origRunStaminaDrain = p.m_runStaminaDrain;
                origStaminaRegen = p.m_staminaRegen;
                origStaminaRegenDelay = p.m_staminaRegenDelay;
                origEitrRegen = p.m_eiterRegen;
                origEitrRegenDelay = p.m_eitrRegenDelay;
                origMaxCarryWeight = p.m_maxCarryWeight;

                // 2) Snapshot food timers
                //origFoodTimes = new Dictionary<Player.Food, float>();
                //foreach (var food in p.GetFoods())
                //{
                //    origFoodTimes[food] = food.m_time;
                //    food.m_time = float.MaxValue;
                //}

                // Snapshot equipped items
                origItemStats = new Dictionary<ItemDrop.ItemData, ItemSnapshot>();
                foreach (var item in p.GetInventory().GetEquippedItems())
                {
                    origItemStats[item] = new ItemSnapshot
                    {
                        durDrain = item.m_shared.m_durabilityDrain,
                        maxDur = item.m_shared.m_maxDurability,
                        curDur = item.m_durability,
                        armor = item.m_shared.m_armor
                    };
                }

                // Buff stats
                p.m_baseHP = p.m_baseHP + 100f;
                p.m_blockStaminaDrain = 0.1f;
                p.m_runStaminaDrain = 0.1f;
                p.m_staminaRegen = 50f;
                p.m_staminaRegenDelay = 0.5f;
                p.m_eiterRegen = 50f;
                p.m_eitrRegenDelay = 0.1f;
                p.m_maxCarryWeight = 99999f;

                // Buff resists
                var m = p.m_damageModifiers;
                m.m_blunt = HitData.DamageModifier.VeryResistant;
                m.m_slash = HitData.DamageModifier.VeryResistant;
                m.m_pierce = HitData.DamageModifier.VeryResistant;
                m.m_fire = HitData.DamageModifier.VeryResistant; //HitData.DamageModifier.Immune;        // fire hurts a lot—just immune it
                m.m_frost = HitData.DamageModifier.VeryResistant;
                m.m_lightning = HitData.DamageModifier.VeryResistant;
                m.m_poison = HitData.DamageModifier.VeryResistant;
                m.m_spirit = HitData.DamageModifier.VeryResistant;
                m.m_chop = HitData.DamageModifier.VeryResistant;
                m.m_pickaxe = HitData.DamageModifier.VeryResistant;

                foreach (var item in p.GetInventory().GetEquippedItems())
                {
                    item.m_shared.m_durabilityDrain = 0.1f;
                    item.m_shared.m_maxDurability = 10000f;
                    item.m_durability = 10000f;
                    item.m_shared.m_armor = item.m_shared.m_armor + 150f;
                }

                GiftActive = true;
                Show("Guardian's Gift: ON");
            }
            else
            {
                // Revert player stats
                p.m_baseHP = origBaseHP;
                p.m_blockStaminaDrain = origBlockStaminaDrain;
                p.m_runStaminaDrain = origRunStaminaDrain;
                p.m_staminaRegen = origStaminaRegen;
                p.m_staminaRegenDelay = origStaminaRegenDelay;
                p.m_eiterRegen = origEitrRegen;
                p.m_eitrRegenDelay = origEitrRegenDelay;
                p.m_maxCarryWeight = origMaxCarryWeight;

                // Revert food timers
                //foreach (var kv in origFoodTimes)
                //    kv.Key.m_time = kv.Value;
                //origFoodTimes = null;

                // TODO: Revert resists
                //
                //

                // Revert items
                foreach (var kv in origItemStats)
                {
                    var item = kv.Key;
                    var snap = kv.Value;

                    // 1) put the old max back
                    item.m_shared.m_maxDurability = snap.maxDur;

                    // 2) restore current durability (now it won't get capped)
                    item.m_durability = snap.curDur;

                    // 3) restore drain and armor
                    item.m_shared.m_durabilityDrain = snap.durDrain;
                    item.m_shared.m_armor = snap.armor;
                }
                origItemStats = null;

                GiftActive = false;
                Show("Guardian's Gift: OFF");
            }
        }

        // ------------------------------------
        // Utility functions to cycle through
        // -------------------------------------
        // 1. Prefer to create an item forril and then replenish the stack to maintain crafter name and not raise suspicion
        // 2. Redo food and pots in chests to have crafter name on it.
        // -------------------------------------
        private static readonly (string Name, Action Action)[] Utilities = {
                // Common
                ("Replenish Stacks",                    ReplenishStacks),
                ("All your craft are belong to me",     SetMeAsCrafterOfFood),
                ("Reapair all things",                  () => RepairStructuresAoE()),
//              ("Enable Devcommands",      () => RunDevCommand("devcommands")),
                ("Fuel",      () =>
                    {
                        CheatCommands.RPCOnInRange<Smelter>("AddFuel", 5f, "Coal", 1);
                        CheatCommands.RPCOnInRange<CookingStation>("AddFuel", 5f, "Wood", 1);
                        CheatCommands.RPCOnInRange<Fireplace>("AddFuel", 5f, "Wood", 1);
                        CheatCommands.RPCOnInRange<Fire>("AddFuel", 5f, "Wood", 1);
                    }),
                ("Toggle Guardian Pwr",     ToggleGuardianPower),
                ("Toggle Ghost Mode",       ToggleGhostMode),
                ("Increase Skills",         IncreaseSkills),
                ("Puke",                    CheatCommands.Vomit),
                
                // Smelt
                //("Smelt Iron",           () => CheatCommands.RPCOnInRange<Smelter>("AddOre", 5f, "IronScrap", 1)),
                //("Smelt Silver",         () => CheatCommands.RPCOnInRange<Smelter>("AddOre", 5f, "SilverOre", 1)),
                //("Smelt BlackIron",      () => CheatCommands.RPCOnInRange<Smelter>("AddOre", 5f, "BlackMetalScrap", 1)),
                ("Smelt Flametal",       () => CheatCommands.RPCOnInRange<Smelter>("AddOre", 5f, "FlametalOreNew", 1)),

                // CookingStation - Just do these from the prep table. Crafter name is applied to the uncooked only, and is stripped since its crafted by "Oven" technically
                //("Cook rabbit",             () => CheatCommands.RPCOnInRange<CookingStation>("AddItem", 5f, "MisthareSupremeUncooked", 1)),
                //("Cook Chicken",            () => CheatCommands.RPCOnInRange<CookingStation>("AddItem", 5f, "HoneyGlazedChickenUncooked", 1)),
                //("Cook PiquantPie",         () => CheatCommands.RPCOnInRange<CookingStation>("AddItem", 5f, "PiquantPieUncooked", 1)),
                //("Cook Meatplatter",        () => CheatCommands.RPCOnInRange<CookingStation>("AddItem", 5f, "MeatPlatterUncooked", 1)),
                //("Cook Crust pie",          () => CheatCommands.RPCOnInRange<CookingStation>("AddItem", 5f, "RoastedCrustPieUncooked", 1)),

                // Fermenter: Just do these from the Mead Kettil
                //("Brew Eitr",              () => CheatCommands.RPCOnInRange<Fermenter>("AddItem", 5f, "MeadBaseEitrMinor", 1)),
                //("Brew LingEitr",          () => CheatCommands.RPCOnInRange<Fermenter>("AddItem", 5f, "MeadBaseEitrLingering", 1)),
                //("Brew Health",            () => CheatCommands.RPCOnInRange<Fermenter>("AddItem", 5f, "MeadBaseHealthMajor", 1)),
                //("Brew Stamina",           () => CheatCommands.RPCOnInRange<Fermenter>("AddItem", 5f, "MeadBaseStaminaMedium", 1)),
                
                // Dangerous
                ("Reveal AshlandCaves",     () => CheatCommands.RevealClosestAshlandsCave()),
               // ("Reveal AshlandTomb",     () => CheatCommands.RevealClosestRetoTomb()), // lord reto
                ("Reveal Bosses",           RevealBosses),
    //          ("Explore Map",             ExploreAll),

                // Other
                // angle is nsew based
               // ("Wind west", () => CheatCommands.RunDevCommand("wind 90 1")),
               // ("Wind north", () => CheatCommands.RunDevCommand("wind 180 1")),
               // ("Wind south", () => CheatCommands.RunDevCommand("wind 0 1"))
                // 
            };

        public static void DumpConsoleRunners()
        {
            var term = Object.FindObjectOfType<Terminal>();
            if (term == null)
            {
                Console.instance.Print("[Debug] Terminal not found!");
                return;
            }

            var type = term.GetType();
            var methods = type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(mi =>
                {
                // must take at least one parameter of type string
                var ps = mi.GetParameters();
                    if (ps.Length == 0 || ps[0].ParameterType != typeof(string))
                        return false;

                // and have a name suggesting they run commands
                var name = mi.Name.ToLowerInvariant();
                    return name.Contains("run") || name.Contains("command") || name.Contains("exec");
                });

            foreach (var mi in methods)
            {
                var sig = mi.Name + "(" +
                          string.Join(", ", mi.GetParameters()
                                               .Select(p => p.ParameterType.Name + " " + p.Name))
                          + ")";
                Console.instance.Print($"▶ {sig}");
            }
        }

        public static void RunDevCommand(string cmd)
        {
            //float yaw = Player.m_localPlayer.transform.eulerAngles.y;

            // wind command’s first arg is the *direction the wind is coming from*,
            // so add 180° to make it a tailwind
            //float bearing = (yaw + 180f) % 360f;

            // 1) find the Terminal instance
            var term = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (term == null)
            {
                Show("[Cheat] Terminal (devconsole) not found!");
                return;
            }

            var mi = typeof(Terminal).GetMethod(
                "TryRunCommand",
                BindingFlags.Instance
              | BindingFlags.Public
              | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(bool), typeof(bool) },
                null
            );
            if (mi == null)
            {
                Show($"[Cheat] TryRunCommand(string,bool,bool) not found!");
                return;
            }

            // 3) invoke it (cmd, runLocally=false, runOnServer=false)
            mi.Invoke(term, new object[] { cmd, false, false });

            // optional feedback
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.TopLeft,
                $"▶️ {cmd}"
            );
        }

        // 2. Track which one is “current”
        private static int utilIndex = 1; //start with ghost mode
        public static string CurrentUtilityName
            => Utilities[utilIndex].Name;

        public static string PrevUtilityName
              => (Utilities != null && Utilities.Length > 0)
            ? Utilities[(utilIndex - 1) % Utilities.Length].Name
            : "";

        public static string NextUtilityName
            => (Utilities != null && Utilities.Length > 0)
            ? Utilities[(utilIndex + 1) % Utilities.Length].Name
            : "";

        // 3. Key‐handler to step to the next one
        public static void CycleUtility()
        {
            utilIndex = (utilIndex + 1) % Utilities.Length;
            Show($"Selected Utility: {CurrentUtilityName}");
        }

        // 4. Key‐handler to execute the selected one
        public static void ExecuteUtility()
        {
            if (!RequireGodMode("Utilities")) return;
            Utilities[utilIndex].Action();
        }

        // 1) Call this to de-aggro every monster in radius
        public static void PacifyAoE(float radius = 20f)
        {
            var center = Player.m_localPlayer.transform.position;
            var list = new List<Character>();
            Character.GetCharactersInRange(center, radius, list);

            int pacified = 0;
            foreach (var c in list)
            {
                // skip players & tamed creatures
                if (c.IsPlayer() || c.IsTamed()) continue;

                // grab their AI
                var ai = c.GetComponent<MonsterAI>();
                if (ai == null) continue;

                // 2) clear the “aggravated” flag
                ai.SetAggravated(false, BaseAI.AggravatedReason.Building);

                // 3) clear any current attack target
                //    (BaseAI stores it in a private field “m_target”)
                var baseAi = (BaseAI)ai;
                var targetField = typeof(BaseAI)
                    .GetField("m_target");
                if (targetField != null)
                    targetField.SetValue(baseAi, null);

                // 4) reset their alert timer so they don’t immediately re-aggro
                var alertedField = typeof(BaseAI)
                    .GetField("m_alerted");
                if (alertedField != null)
                    alertedField.SetValue(baseAi, false);

                pacified++;
            }

            Show($"Pacified {pacified} foes within {radius:0}m");
        }

        // ---

        public static void HandlePeriodic() => PeriodicManager.HandlePeriodic();

        // Permission check
        private static bool RequireGodMode(string ability)
        {
            if (!GodMode)
            {
                Show($"{ability} requires God Mode");
                return false;
            }
            return true;
        }

        // ---------------------------------------------
        // Periodic things - abstract this
        // ---------------------------------------------
        public static void ToggleRenewal()
        {
            RenewalActive = !RenewalActive;
            Show($"Renewal {(RenewalActive ? "ON" : "OFF")}");
        }

        public static void ToggleAoeRenewal()
        {
            if (!RequireGodMode("AoE Renewal")) return;
            AOERenewalActive = !AOERenewalActive;
            CheatVisualizer.ToggleConformHeal(10f); //todo: config. and toggle bool is already here.
            Show($"AoE Renewal {(AOERenewalActive ? "ON" : "OFF")}");
        }

        public static void ToggleCloakOfFlames()
        {
            if (!RequireGodMode("CoF")) return;
            CloakActive = !CloakActive; // Player.m_localPlayer.GetSEMan().AddStatusEffect(new SE_Burning(), resetTime: true, 10, 10);
            CheatVisualizer.TogglePbaoeRing(5f);
            Show($"Cloak of Flames {(CloakActive ? "ON" : "OFF")}");
        }

        public static void ToggleMelodicBinding()
        {
            if (!RequireGodMode("Snare")) return;
            MelodicActive = !MelodicActive;
            Show($"Melodic Binding {MelodicActive}");
        }

        public static void ToggleBarrierAoE()
        {
            BarrierAoEActive = !BarrierAoEActive;
            CheatVisualizer.ToggleBarrierRing(BarrierRadius);
            Show($"Barrier AoE {(BarrierAoEActive ? "ON" : "OFF")} (r={BarrierRadius:0}, s={BarrierStrength:0})");
        }
        // ---------------------------------------------

        public static void ToggleGodMode()
        {
            GodMode = !GodMode;
            ToggleRenewal();
            Player.m_localPlayer.SetGodMode(GodMode);
            Player.m_localPlayer.SetNoPlacementCost(value: GodMode);
            Player.m_localPlayer.m_guardianPowerCooldown = 1f; //todo: this works if we place it in Gift

            // Set all food to 30 mins
            foreach (var food in Player.m_localPlayer.GetFoods())
            {
                food.m_time = 25 * 60;
            }

            Show($"God Mode {(GodMode ? "ON" : "OFF")}");
        }

        public static void Vomit()
        {
            // create the Puke status effect (15s ttl, 2s tick by default)
            var puke = ScriptableObject.CreateInstance<SE_Puke>();
            puke.m_ttl = 10f;       // total duration

            // apply it immediately (reset any existing puke)
            Player.m_localPlayer.GetSEMan().AddStatusEffect(puke, resetTime: true);

            // Reset food
            foreach (var food in Player.m_localPlayer.GetFoods())
            {
                food.m_time = 0;
            }
        }

        public static void ToggleGuardianPower()
        {
            Player.m_localPlayer.SetGuardianPower(guardians[guardianIndex]);
            Show($"Guardian Power: {guardians[guardianIndex]}");
            guardianIndex = (guardianIndex + 1) % guardians.Length;
        }

        public static void IncreaseDamageCounter()
        {
            DamageCounter++;
            Show("Damage Counter: " + DamageCounter);
            ApplySuperWeapon();
        }
        public static void DecreaseDamageCounter()
        {
            DamageCounter--;
            Show("Damage Counter: " + DamageCounter);
            ApplySuperWeapon();
        }

        // 1.B) Cycle to the next prefab in the list
        public static void CyclePrefab()
        {
            prefabIndex = (prefabIndex + 1) % SpawnPrefabs.Length;
            Show($"Selected prefab: {CurrentPrefab}");
        }

        // Spawn whatever is currently selected
        public static void SpawnSelectedPrefab()
        {
            //SpawnPrefab(CurrentPrefab);
            SpawnPrefabAtCursor(CurrentPrefab);
        }

        // Refactored spawn that takes a name
        private static void SpawnPrefab(string name)
        {
            if (!RequireGodMode("Spawn Prefab")) return;
            var prefab = ZNetScene.instance.GetPrefab(name);
            if (prefab == null) { Show($"Missing prefab: {name}"); return; }
            var player = Player.m_localPlayer;
            Vector3 forward = player.transform.forward;
            Vector3 spawnPos = player.transform.position + forward * 5f + Vector3.up;
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 10f))
                spawnPos.y = hit.point.y + 0.5f;
            UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            Show($"Spawned {name} at {spawnPos:0.0}");
        }

        private static void SpawnPrefabAtCursor(string name)
        {
            if (!RequireGodMode("Spawn Prefab")) return;
            var prefab = ZNetScene.instance.GetPrefab(name);
            if (prefab == null)
            {
                Show($"Missing prefab: {name}");
                return;
            }

            Vector3 spawnPos;
            Camera cam = Camera.main;
            if (cam != null)
            {
                // cast a ray from the cursor into the world
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    // spawn right at the hit point
                    spawnPos = hit.point;
                }
                else
                {
                    // fallback: 5m in front
                    var player = Player.m_localPlayer;
                    Vector3 fwd = player.transform.forward;
                    spawnPos = player.transform.position + fwd * 5f + Vector3.up;
                }
            }
            else
            {
                // no camera? fallback to front
                var player = Player.m_localPlayer;
                Vector3 fwd = player.transform.forward;
                spawnPos = player.transform.position + fwd * 5f + Vector3.up;
            }

            // nudge up so it isn't clipping the ground
            spawnPos.y += 0.5f;

            UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            Show($"Spawned {name} at {spawnPos:0.0}");
        }

        public static void RepairStructuresAoE(float radius = 20f)
        {
            var center = Player.m_localPlayer.transform.position;
            int repaired = 0;

            foreach (var piece in Object.FindObjectsOfType<Piece>())
            {
                if (Vector3.Distance(piece.transform.position, center) <= radius)
                {
                    var znv = piece.GetComponent<ZNetView>();
                    znv.InvokeRPC("RPC_Repair");
                    repaired++;
                }
            }

            Show($"Repaired {repaired} structures within {radius:0}m");
        }

        public static void StaggerAoE(float radius = 10f)
        {
            var center = Player.m_localPlayer.transform.position;
            var list = new List<Character>();
            Character.GetCharactersInRange(center, radius, list);

            int count = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;
                c.Stagger(Vector3.forward);
                count++;
            }

            Player.m_localPlayer.Message(
                MessageHud.MessageType.TopLeft,
                $"Staggered {count} foes within {radius:0}m"
            );
        }

        static UInt16 pushed = 0;
        public static bool BarrierUseTeleport = true;
        public static void BarrierAoETick()
        {
            if (!BarrierAoEActive) return;

            var center = Player.m_localPlayer.transform.position;
            var list = new List<Character>();
            Character.GetCharactersInRange(center, BarrierRadius, list);

            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;

                Vector3 delta = c.transform.position - center;
                float dist = delta.magnitude;
                if (dist < 0.01f || dist >= BarrierRadius) continue;

                // compute normalized push direction & falloff
                Vector3 pushDir = delta.normalized;
                float falloff = Mathf.Pow(1f - (dist / BarrierRadius), BarrierFalloff);

                if (BarrierUseTeleport)
                {
                    // teleport mode: snap them back a bit
                    float moveDist = BarrierStrength * 0.1f * falloff;
                    Vector3 newPos = c.transform.position + pushDir * moveDist;
                    c.TeleportTo(newPos, c.transform.rotation, distantTeleport: true);
                    pushed++;
                }
                else
                {
                    // physics mode: overwrite their horizontal velocity
                    var rb = c.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 impulse = pushDir * (BarrierStrength * falloff);
                        rb.velocity = new Vector3(impulse.x, rb.velocity.y, impulse.z);
                        pushed++;
                    }
                    else
                    {
                        // fallback to small teleport if no Rigidbody
                        float moveDist = BarrierStrength * 0.05f * falloff;
                        c.transform.position += pushDir * moveDist;
                        pushed++;
                    }
                }
                Show($"{pushed} mobs pushed.");
                pushed = 0;
            }
        }

        public static void RevealBosses()
        {
            if (!RequireGodMode("Reveal Bosses")) return;
            foreach (var b in BossData.All) BossData.Reveal(b);
            Show("Bosses revealed");
        }

        public static void ExploreAll()
        {
            if (!RequireGodMode("Explore")) return;
            Minimap.instance.ExploreAll();
            Show("Map fully explored");
        }

        public static void SpeedUp() => SetSpeed(Player.m_localPlayer.m_runSpeed + 1);
        public static void SpeedDown() => SetSpeed(Player.m_localPlayer.m_runSpeed - 1);

        public static void ToggleGhostMode()
        {
            if (!RequireGodMode("Ghost")) return;
            GhostMode = !GhostMode;
            Player.m_localPlayer.SetGhostMode(GhostMode);
            Show($"Ghost Mode {GhostMode}");
        }

        public static void SpawnCombatPet()
        {
            if (!RequireGodMode("Spawn Combat Pet")) return;
            var rnd = new System.Random();
            string prefab = combatPets[rnd.Next(combatPets.Length)];
            GameObject p = ZNetScene.instance.GetPrefab(prefab);
            if (p == null) { Show($"Missing prefab: {prefab}"); return; }

            // Get Component
            Vector3 pos = Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f;
            var inst = UnityEngine.Object.Instantiate(p, pos, Quaternion.identity);
            var ch = inst.GetComponent<Character>();

            ch.SetLevel(3);
            ch.GetComponent<Character>().SetMaxHealth(1000);
            ch.GetComponent<Character>().SetHealth(1000);
            ch.GetComponent<MonsterAI>().SetFollowTarget(Player.m_localPlayer.gameObject);
            ch.m_name = petNames[rnd.Next(petNames.Length)];

            //tame it
            Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 20.0f);

            Show($"Spawned pet: {ch.m_name}");
        }

        public static void TameTargeted()
        {
            if (!RequireGodMode("Tame target")) return;
            var cam = Camera.main;
            if (cam == null) return;

            // shoot a ray from mouse cursor (or center of screen)
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                // find the Character on that hit
                var c = hit.collider.GetComponentInParent<Character>();
                if (c != null && !c.IsTamed() && !c.IsPlayer())
                {
                    // tiny blast around just that one
                    Tameable.TameAllInArea(c.transform.position, 0.1f);
                    Show($"Tamed {c.GetHoverName()}");
                    return;
                }
            }
            Show("No valid target to tame");
        }

        // Can also be used to re-tame already tamed, not using tame all in area
        public static void TameAll(bool clearFollow = false)
        {
            if (!RequireGodMode("Tame all")) return;
            int tamed = 0;
            GameObject followTarget = null;
            if(!clearFollow) followTarget = Player.m_localPlayer.gameObject;
            //Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 30.0f);

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 5.0f, list);

            // Set follow
            foreach (Character item in list)
            {
                if (item.IsPlayer() /*|| !item.IsTamed()*/) continue;

                //item.SetLevel(3); //Hmm, its kind of interesting that we could runtime just increase the level of mobs already in the world.
                SetFollowTarget(item, followTarget);
                item.SetTamed(true);
                tamed++;
                //item.GetComponent<Character>().m_name = ... something to symbolise it has been augmented.
            }

            if(clearFollow)
                Show($"{tamed} pets staying");
            else
                Show($"{tamed} pets following");
        }

        public static void BuffTamed(bool incrlevel = false)
        {
            if (!RequireGodMode("Buff tamed")) return;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 30.0f, list);

            // Set follow
            foreach (Character item in list)
            {
                if (item.IsPlayer() || !item.IsTamed()) continue;
                item.SetMaxHealth(1000);
                if(incrlevel)
                    item.SetLevel(2);
            }

            Show("Buff tamed");
        }

        public static void SetFollowTarget(Character c, GameObject target)
        {
            c.GetComponent<MonsterAI>().SetFollowTarget(target);
        }

        public static void BuffAoE()
        {
            if (!RequireGodMode("Buff team")) return;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50.0f, list);

            foreach (Character item in list)
            {
                if (!item.IsPlayer()) continue;
                if (item == Player.m_localPlayer)
                    continue;

                item.m_speed = 9;
                item.m_acceleration = 9;
            }

            Show("AoE Buff!");
        }

        // Can also be used to re-tame
        public static void DebuffAoE()
        {
            if (!RequireGodMode("Debuff")) return;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50.0f, list);

            foreach (Character item in list)
            {
                if (!item.IsMonsterFaction(10)) continue;

                //Lower health to 100. If they're full health, health bar won't show any signs of change
                item.GetComponent<Character>().SetMaxHealth(100);
                item.GetComponent<Character>().SetHealth(100);

                item.GetComponent<Character>().SetLevel(1); // Strip level
                item.GetComponent<Character>().SetWalk(true); // Slow
                item.GetComponent<Character>().m_speed = 1;
                item.GetComponent<Character>().m_runSpeed = 1;
                item.GetComponent<Character>().m_acceleration = 1;

                item.GetComponent<MonsterAI>().SetFollowTarget(Player.m_localPlayer.gameObject);
            }

            Show("AoE Debuff!");
        }

        public static void Taunt()
        {
            if (!RequireGodMode("Taunt")) return;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50.0f, list);

            foreach (Character item in list)
            {
                if (!item.IsMonsterFaction(10)) continue;
                item.GetComponent<MonsterAI>().SetFollowTarget(Player.m_localPlayer.gameObject);
            }

            Show("AoE Taunt!");
        }

        public static void ReplenishStacks()
        {
            if (!RequireGodMode("Replenish")) return;
            foreach (var item in Player.m_localPlayer.GetInventory().GetAllItems())
            {
                if (item.m_shared.m_maxStackSize > 1)
                {
                    item.m_stack = item.m_shared.m_maxStackSize;
                    item.m_shared.m_equipDuration = 1000f;
                }
            }

            Show("Stacks replenished");
        }

        public static void ApplySuperWeapon()
        {
            //            Player.m_localPlayer.UseHealth()
            // Apply current damage counter to equipped weapons
            foreach (var item in Player.m_localPlayer.GetInventory().GetEquippedItems())
            {
                if (!item.IsWeapon()) continue;
                // Get base damage types
                var baseDamages = item.GetDamage();
                int dmg = DamageCounter * 10;

                var updated = new HitData.DamageTypes
                {
                    m_slash = baseDamages.m_slash > 0 ? dmg : 0,
                    m_blunt = baseDamages.m_blunt > 0 ? dmg : 0,
                    m_pierce = baseDamages.m_pierce > 0 ? dmg : 0,
                    m_frost = baseDamages.m_frost > 0 ? dmg : 0,
                    m_lightning = baseDamages.m_lightning > 0 ? dmg : 0,
                    m_poison = baseDamages.m_poison > 0 ? dmg : 0,
                    m_spirit = baseDamages.m_spirit > 0 ? dmg : 0,
                    //harvest
                    m_chop = baseDamages.m_chop > 0 ? dmg : 0,
                    m_pickaxe = baseDamages.m_pickaxe > 0 ? dmg : 0,
                    //generic
                    m_damage = baseDamages.m_damage > 0 ? dmg : 0,

                };
                item.m_shared.m_damages = updated;
            }
            Show($"Applied {DamageCounter} damage counters to weapons");
        }

        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static void SetMeAsCrafterOfFood()
        {
            var p = Player.m_localPlayer;
            if (p == null) return;

            string myName = p.GetPlayerName();

            // Prefer Player.GetPlayerID() (long) to avoid Splatform types.
            long myId = 0;
            var getId = typeof(Player).GetMethod("GetPlayerID", BF, null, System.Type.EmptyTypes, null);
            if (getId != null) myId = (long)getId.Invoke(p, null);

            // Locate optional ItemData.SetCrafter(long,string)
            var setCrafter = typeof(ItemDrop.ItemData)
                .GetMethod("SetCrafter", BF, null, new[] { typeof(long), typeof(string) }, null);

            var items = p.GetInventory().GetAllItems();
            int stamped = 0;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.m_shared == null) continue;
                if (it.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) continue;

                if (setCrafter != null)
                {
                    // Clean path on builds that support it
                    setCrafter.Invoke(it, new object[] { myId, myName });
                }
                else
                {
                    // Minimal fallback: set fields directly
                    it.m_crafterName = myName;

                    var fId = typeof(ItemDrop.ItemData).GetField("m_crafterID", BF);
                    if (fId != null && fId.FieldType == typeof(long))
                        fId.SetValue(it, myId);
                }
                stamped++;
            }

            // UI will catch up when you hover/close/open inventory.
            p.Message(MessageHud.MessageType.TopLeft, $"Crafter set on {stamped} consumables");
            Console.instance?.Print($"[Crafter] Set on {stamped} items (ID={myId}, Name={myName})");
        }

        public static void IncreaseSkills()
        {
            Show($"Raising important skills");

            var p = Player.m_localPlayer;
            p.RaiseSkill(Skills.SkillType.BloodMagic, 2);
            p.RaiseSkill(Skills.SkillType.ElementalMagic, 2);
            p.RaiseSkill(Skills.SkillType.Swim, 2);
            p.RaiseSkill(Skills.SkillType.Run, 2);
            p.RaiseSkill(Skills.SkillType.Sneak, 2);
            p.RaiseSkill(Skills.SkillType.Jump, 2);
        }

        // Old function
        // Guardian's Gift: major buff including Renewal
        public static void GuardianGift()
        {
            GiftActive = true; // can't disable
            if (!RequireGodMode("Guardian's Gift")) return;

            var p = Player.m_localPlayer;
            // Boost stats
            float regen = 100f, fallDmg = 0.1f, noise = 1f;

            // this vs vanilla way?
            p.GetSEMan().ModifyHealthRegen(ref regen);
            p.GetSEMan().ModifyFallDamage(1, ref fallDmg);
            p.GetSEMan().ModifyNoise(1, ref noise);

            p.m_maxCarryWeight = 9999f;
            p.m_baseHP = 75f;
            p.m_baseStamina = 100f;
            //p.m_blockStaminaDrain = 0.1f;

            // stamina
            p.m_runStaminaDrain = 0.1f;
            p.m_staminaRegen = 50f;
            p.m_staminaRegenDelay = 0.5f;
            //eitr
            p.m_eiterRegen = 100f;
            p.m_eitrRegenDelay = 0.1f;

            // Durability TODO move this to other function. not related.

            foreach (var item in p.GetInventory().GetEquippedItems())
            {
                item.m_shared.m_durabilityDrain = 0.1f;
                item.m_shared.m_maxDurability = 10000f;
                item.m_shared.m_weight = 0.1f;
                item.m_durability = 10000f;
                item.m_shared.m_armor = 100;
            }

            // Food - own func?
            List<Player.Food> foods = p.GetFoods();
            foreach (var food in foods)
            {
                food.m_time = 10000f;
                food.m_eitr = 100;
                food.m_health = 100;
                food.m_stamina = 100;
            }

            Show("Guardian's Gift activated");
        }

        public static void Invigorate()
        {
            var p = Player.m_localPlayer;
            p.Heal(p.GetMaxHealth() - p.GetHealth(), true);
            p.AddStamina(p.GetMaxStamina());
            p.AddEitr(p.GetMaxEitr() - p.GetEitr());

            // Remove bad effects
            //
            // TODO: Does this only remove bad ones?
            //List<StatusEffect> effects = p.GetSEMan().GetStatusEffects();
            //foreach (StatusEffect st in effects)
            //{
            //    p.GetSEMan().RemoveStatusEffect(st);
            //}

            // Add beneficial effects
            //          p.GetSEMan().AddStatusEffect(new SE_Rested(), resetTime: true, 10, 10); //this will add lvl1: 8mins
            //          p.GetSEMan().AddStatusEffect(new SE_Shield(), resetTime: true, 10, 10);
        }

        // todo: List of pets in seperate box.
        // todo: Allow cycling through prefabs and displaying that to the user.
        // todo: 
        public static void SpawnPrefabOld()
        {
            if (!RequireGodMode("Spawn Prefab")) return;

            var prefab = ZNetScene.instance.GetPrefab("FenringIceNova_aoe"); // Fader_Fissure_AOE, Fader_Flamebreath_AOE, Fader_MeteorSmash_AOE (crazy invis dmg), FenringIceNova_aoe
            if (prefab == null) { Show("Missing prefab"); return; }

            var player = Player.m_localPlayer;
            // 1) Project a point 5 units in front of the player…
            Vector3 forward = player.transform.forward;
            Vector3 spawnPos = player.transform.position + forward * 5f + Vector3.up;

            // 2) (Optional) Drop it to the ground using a raycast:
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 10f))
                spawnPos.y = hit.point.y + 0.5f;  // adjust so it’s not clipping

            UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            Show($"Spawning prefab at {spawnPos:0.0}");
        }

        public static void IncreaseAoePower()
        {
            AoePower = Mathf.Min(MaxAoePower, AoePower + 5f);
            Show($"AOE Power: {AoePower:0}");
        }
        public static void DecreaseAoePower()
        {
            AoePower = Mathf.Max(MinAoePower, AoePower - 5f);
            Show($"AOE Power: {AoePower:0}");
        }

        // healing pulse
        // New: critical heal threshold and multiplier
        public static float CritThreshold = 0.10f;    // 10%
        public static float CritMultiplier = 2f;      // 2x heal
        public static float HealThreshold = 0.85f;

        public static void AoeRegen(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(
                Player.m_localPlayer.transform.position,
                radius,
                list
            );

            int healedCount = 0;
            int critHealedCount = 0;

            foreach (var c in list)
            {
                if (!c.IsPlayer() && !c.IsTamed()) continue;

                float hpPct = c.GetHealthPercentage(); // 0.0–1.0

                // only heal those below the HealThreshold
                if (hpPct < HealThreshold)
                {
                    // choose normal vs. crit heal
                    float amount = (hpPct <= CritThreshold)
                        ? AoePower * CritMultiplier
                        : AoePower;

                    c.Heal(amount, false);
                    healedCount++;

                    if (hpPct <= CritThreshold)
                        critHealedCount++;
                }
            }

            if (healedCount > 0)
            {
                if (critHealedCount > 0)
                    Show($"AoE Regen: {healedCount} healed (incl. {critHealedCount} critical for {AoePower * CritMultiplier:0} HP)");
                else
                    Show($"AoE Regen: {healedCount} healed for {AoePower:0} HP");
            }
        }

        public static void CastHealAOE()
        {
            if (!RequireGodMode("AoE heal")) return;
            var prefab = ZNetScene.instance.GetPrefab("DvergerStaffHeal_aoe");
            if (prefab == null) { Show("Missing heal prefab"); return; }
            UnityEngine.Object.Instantiate(prefab,
                Player.m_localPlayer.transform.position + Vector3.up,
                Quaternion.identity);
            Show("Heal AOE cast");
        }

        public static void CastDmgAOE()
        {
            GameObject prefab2 = ZNetScene.instance.GetPrefab("aoe_nova");

            if (!prefab2)
            {
                Show("Missing object aoe_nova");
            }
            else
            {
                Vector3 vector = UnityEngine.Random.insideUnitSphere;

                Show("Spawning Nova!");
                GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 4f + Vector3.up + vector, Quaternion.identity);
                ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
            }
        }

        public static void KillAllMonsters()
        {
            if (!RequireGodMode("Kill all monsters")) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 30f, list);
            int killed = 0;
            foreach (var c in list)
                if (!c.IsPlayer() && !c.IsTamed())
                {
                    c.Damage(new HitData { m_damage = { m_damage = 1e10f } });
                    killed++;
                }
            Show($"Monsters killed: {killed}");
        }

        public static void TeleportSolo()
        {
            if (!RequireGodMode("Teleport")) return;
            // use the old minimap-based conversion
            var pos = Utils.ScreenToWorldPoint(Input.mousePosition);
            Player.m_localPlayer.TeleportTo(pos, Player.m_localPlayer.transform.rotation, true);
            Show($"Teleported to {pos}");
        }

        public static void TeleportMass()
        {
            if (!RequireGodMode("Mass Teleport")) return;
            var pos = Utils.ScreenToWorldPoint(Input.mousePosition);
            foreach (var pl in PlayerUtility.GetNearbyPlayers(50f))
                Chat.instance.TeleportPlayer(pl.GetZDOID().UserID, pos, Quaternion.identity, true);
            Show("Mass teleport executed");
        }

        public static void TeleportHome()
        {
            if (!RequireGodMode("Gate")) return;
            var dst = TeleportUtils.GetSpawnPoint();
            Player.m_localPlayer.TeleportTo(dst, Quaternion.identity, true);
            Show("Teleported home");
        }

        public static void TeleportSafe()
        {
            if (!RequireGodMode("Succor")) return;
            var pins = Minimap.m_pins;
            foreach (var pin in pins)
                if (pin.m_name.Equals("safe", StringComparison.OrdinalIgnoreCase))
                {
                    var dst = pin.m_pos + UnityEngine.Random.insideUnitSphere * 5f;
                    Player.m_localPlayer.TeleportTo(dst, Quaternion.identity, true);
                    Show("Teleported to safe spot");
                    return;
                }
            Show("No safe pin found");
        }

        // Helpers
        public static void SlowMonsters()
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 30f, list);
            foreach (var c in list)
                if (c.IsMonsterFaction(10))
                    c.m_runSpeed = c.m_speed = 0f;
        }

        public static void DamageAoE(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(
                Player.m_localPlayer.transform.position,
                radius,
                list
            );
            float dmg = AoePower * AoeDmgScale;
            foreach (var c in list)
            {
                if (c.IsPlayer() || !c.IsMonsterFaction(10)) continue;
                c.Damage(new HitData { m_damage = { m_damage = dmg } });
                c.GetSEMan()?.AddStatusEffect(new SE_Burning(), resetTime: true, 5, 1);
            }
        }

        // call this once when the key is pressed (not every frame)
        public static void DoKnockbackAoE()
        {
            var center = Player.m_localPlayer.transform.position;
            var list = new List<Character>();
            Character.GetCharactersInRange(center, KnockbackRadius, list);

            int count = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;

                // compute direction
                Vector3 dir = (c.transform.position - center).normalized;
                // get the Rigidbody (Character.m_body is protected, so grab via GetComponent)
                var rb = c.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // zero vertical so they fly level
                    dir.y = 0.2f;
                    // apply an impulse
                    rb.AddForce(dir * KnockbackStrength, ForceMode.VelocityChange);
                    count++;
                }
            }

            Show($"Knocked back {count} foes in {KnockbackRadius}m!");
        }

        public static void RPCOnInRange<T>(string rpcSuffix, float radius = 20f, params object[] rpcArgs)
            where T : Component
        {
            // build the full RPC name
            string rpcName = rpcSuffix.StartsWith("RPC_", StringComparison.Ordinal)
                ? rpcSuffix
                : "RPC_" + rpcSuffix;

            Vector3 center = Player.m_localPlayer.transform.position;
            int count = 0;

            // find all T components in the scene
            foreach (var comp in UnityEngine.Object.FindObjectsOfType<T>())
            {
                if (Vector3.Distance(comp.transform.position, center) > radius)
                    continue;

                var znv = comp.GetComponent<ZNetView>();
                if (znv != null && znv.IsValid())
                {
                    // invoke with whatever args you supplied
                    znv.InvokeRPC(rpcName, rpcArgs);
                    count++;
                }
            }

         //   Player.m_localPlayer.Message(
         //       MessageHud.MessageType.TopLeft,
         //       $"Invoked {rpcName} on {count} {typeof(T).Name}(s) within {radius:0}m"
         //   );
        }

        public static void GatherMonsters(float radius = 50f, float forwardOffset = 5f)
        {
            var player = Player.m_localPlayer.transform;
            // Determine the “gather” point:
            Vector3 gatherPos = player.position + player.forward * forwardOffset;

            // Optionally raise it to ground level
            if (Physics.Raycast(gatherPos + Vector3.up * 10f, Vector3.down, out var hit, 20f))
                gatherPos.y = hit.point.y + 0.1f;

            // Find all monsters in radius
            var list = new List<Character>();
            Character.GetCharactersInRange(player.position, radius, list);

            int moved = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed())
                    continue;

                // teleport them to the gather point
                c.TeleportTo(gatherPos, c.transform.rotation, distantTeleport: true);
                // can also use transform.position?
                moved++;
            }

            Show($"Gathered {moved} monsters to ({gatherPos:0.0})");
        }

        public static void ToggleFreezeMonsters()
        {
            FreezeMonstersActive = !FreezeMonstersActive;

            // grab everyone in 50m
            var list = new List<Character>();
            Character.GetCharactersInRange(
                Player.m_localPlayer.transform.position,
                50f,
                list
            );

            int affected = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;

                // 0f = freeze, 1f = normal
                c.m_speed = FreezeMonstersActive ? 0f : 1f;
                affected++;
            }

            Show($"❄️ Monster Freeze {(FreezeMonstersActive ? "ON" : "OFF")} ({affected} {(FreezeMonstersActive ? "frozen" : "unfrozen")})");
        }

        private static void SetSpeed(float speed)
        {
            var p = Player.m_localPlayer;
            p.m_runSpeed = p.m_walkSpeed = speed;

            //p.m_swimSpeed = speed; // This just looks rediculous
            //p.m_acceleration = speed;
            //p.m_crouchSpeed = speed;

            p.m_jumpForce = speed;
            p.m_jumpForceForward = speed;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(p.transform.position, 30.0f, list);

            foreach (Character item in list)
            {
                if (!item.IsMonsterFaction(10) && item != p)
                {
                    item.m_runSpeed = speed;
                    item.m_speed = speed;
                    Show("Increased speed for " + item.GetHoverName() + " to " + item.m_runSpeed);
                }
            }

            Show($"Speed set: {speed}");
        }

        public static void Show(string msg)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, msg);
            Console.instance.Print(msg);
        }
    }
}