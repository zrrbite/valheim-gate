# Valheim game catalog (read from the Mac install, game 1.0.16 data, 2026-10-06)

Read-only extraction from `valheim.app/Contents/Resources/Data`: localization CSVs (TextAssets in `resources.assets`), every SoftRef bundle decoded with its type trees (MonoBehaviour fields, prefab roots, asset paths), the main scene (`Scenes/main.unity`: ZoneSystem, RandEventSystem, ObjectDB, DreamTexts), and enum values from `assembly_valheim.dll`. English names come from the game's own `localization` table; the full key->English table is in `loc-en.tsv` next to this file.

Conventions: **prefab** is the ZNetScene/ZoneSystem name (what `ZNetScene.GetPrefab`/`ZoneSystem` use). **token** is the `$key` the game shows through `Localization`. Folders are the Unity asset folders, a strong hint of what a thing is.

## Contents

- [1. Localization](#1-localization)
- [2. NPCs and characters](#2-npcs-and-characters)
- [3. Creatures](#3-creatures)
- [4. Crafting stations and pieces](#4-crafting-stations-and-pieces)
- [5. Items](#5-items)
- [6. Locations (ZoneSystem)](#6-locations-zonesystem)
- [7. Status effects and guardian powers](#7-status-effects-and-guardian-powers)
- [8. Raids / random events (`RandEventSystem.m_events` plus the `m_events` of each `LocationList`)](#8-raids--random-events-randeventsystemm_events-plus-the-m_events-of-each-locationlist)
- [9. Dreams (`DreamTexts`) and ravens' tutorial texts](#9-dreams-dreamtexts-and-ravens-tutorial-texts)
- [10. Other story-usable systems](#10-other-story-usable-systems)
- [11. The Deep North (1.0) in detail](#11-the-deep-north-10-in-detail)
- [12. Models: world props and character folders (from the bundle manifest)](#12-models-world-props-and-character-folders-from-the-bundle-manifest)

### Counts

| category | count |
|---|---|
| Localization keys (English) | 6058 |
| NPC components | 30 |
| Raven guide points | 104 |
| Creature/character prefabs | 162 |
| $enemy_* tokens | 121 |
| Spawn table entries | 103 |
| Functional pieces (special classes) | 215 |
| Piece prefabs | 689 |
| Notable items | 355 |
| Trophies | 72 |
| Item prefabs | 1519 |
| Location entries | 216 |
| Dungeon room themes | 9 |
| Runestone texts placed | 245 |
| Status effect assets | 97 |
| Guardian powers (GP_*) | 7 |
| Raid/events | 31 |
| Dreams | 39 |
| Hugin/Munin tutorial texts | 140 |
| Prop/character model folders | 186 |


## 1. Localization

The English column of Valheim's `localization` TextAsset (5,619 keys) plus side tables (`localization_warriortitles`, `_captions`, `_extra`, platform ones). `localization_deepnorth`, `_witch`, `_ashlands`, `_celebrationupdate` and `_combatupdate` ship as empty shells: their strings were merged into the main table. Full dump: `loc-en.tsv` (key, English, source table).

| prefix | keys | example |
|---|---|---|
| item | 1959 | item_quality = Quality |
| piece | 886 | piece_use = Use |
| npc | 350 | npc_haldor_greeting01 = Humph! Another scrap of flesh from the Valkyries... |
| settings | 243 | settings_language = Language |
| menu | 240 | menu_startgame = Start Game |
| se | 223 | se_burning_start = You are burning! |
| fallenwarrior | 202 | fallenwarrior_title1_m = {name} the Adventurer |
| tutorial | 165 | tutorial_start_topic = Welcome to Valheim |
| lore | 144 | lore_gdking = BURN THEIR YOUNG |
| enemy | 138 | enemy_troll = Troll |
| msg | 135 | msg_worldsaved = World saved |
| caption | 132 | caption_alerted = alerted |
| hud | 127 | hud_showmap = Show map |
| ach | 110 | ach_arrived = I Have Arrived! |
| fallen | 80 | fallen_viking_randomstart_1 = Let us see what you are capable of! |
| customization | 66 | customization_beard01 = Majestic |
| inventory | 61 | inventory_splitstack = Split stack |
| skill | 56 | skill_swords = Swords |
| stat | 55 | stat_MaxBuildingHeight = Highest building |
| event | 54 | event_wolves_start = You are being hunted... |
| ps | 49 | ps_error_crossplayprivilege = You've enabled the 'PlayStation™Network Only' setting in th… |
| switch | 45 | switch_eula_important_first_text = (Valheim – Nintendo Switch 2) / IMPORTANT: PLEASE READ THE… |
| dream | 39 | dream_random01 = You dream of a river running uphill, of green shoots turnin… |
| language | 37 | language_english = English |
| button | 33 | button_mouse0 = Mouse-1 |
| prop | 30 | prop_ancienttree = Ancient Tree |
| shadowperson | 30 | shadowperson_randomtalk_01 = His blade was dipped in poison, I stood no chance... |
| xbox | 28 | xbox_error_crossplayprivilege = Your platform privilege settings prevent you from playing o… |
| loadscreen | 25 | loadscreen_tip01 = Don't forget to upgrade your crafting stations, weapons and… |
| deadspeak | 22 | deadspeak_eikthyr = So you were my death? You look so small and soft… Tell Oden… |
| tag | 20 | tag_misc = Misc. |
| radial | 19 | radial_back = Back |
| report | 19 | report_user_failed_header = Report failed! |
| location | 16 | location_enter = Enter |
| error | 15 | error_incompatibleversion = Incompatible version |
| guardianstone | 15 | guardianstone_eikthyr_desc = His antlers are branches of iron / They crack the rocks and… |
| biome | 13 | biome_forest = Forest |
| animal | 13 | animal_fish = Fish |
| codeofconduct | 12 | codeofconduct_header = Player Conduct & Bans - Play Responsibly |
| ship | 11 | ship_holdfast = Hold fast |

## 2. NPCs and characters

### 2a. NPC components found in prefabs

Name/English come from the prefab's `Character` when it has one (the Dvergr/FallenWarrior/ShadowPerson `NpcTalk.m_name` is a dev leftover, literally `Haldor`/`Dvergr`). `odin` is the cloaked figure that appears near the player and vanishes (class `Odin`, despawns at 20 m or after 60 s); `Valkyrie` carries a new character to the start; `Valkyrie_End` (EndCredits) is the post-Kall valkyrie at the start temple, "Journey to Valhalla", shown when the world key `StoneCircle` is set.

| GameObject | class | token | English | inside (root) | folder | note |
|---|---|---|---|---|---|---|
| piece_barber | Barber | $piece_barber | Barber Station | DevDressingRoom | Locations/Misc |  |
| piece_barber | Barber | $piece_barber | Barber Station | DevHouse5 | Locations/Misc |  |
| piece_barber | Barber | $piece_barber | Barber Station |  | Pieces |  |
| valkyrie2 | EndCredits | $npc_valkyrie_end | Valkyrie | Valkyrie_End | Valkyrie |  |
| Dverger | NpcTalk | $enemy_dvergr | Dvergr Rogue |  | Dverger | 51 talk lines |
| DvergerAshlands | NpcTalk | $enemy_dvergr | Dvergr Rogue |  | Dverger | 60 talk lines |
| DvergerDeepNorth | NpcTalk | $enemy_dvergr_deepnorth | Imprisoned Dvergr |  | Dverger | 19 talk lines |
| DvergerMage | NpcTalk | $enemy_dvergr_mage | Dvergr Mage |  | Dverger | 49 talk lines |
| DvergerMageFire | NpcTalk | $enemy_dvergr_mage | Dvergr Mage |  | Dverger | 49 talk lines |
| DvergerMageIce | NpcTalk | $enemy_dvergr_mage | Dvergr Mage |  | Dverger | 49 talk lines |
| DvergerMageSupport | NpcTalk | $enemy_dvergr_mage | Dvergr Mage |  | Dverger | 49 talk lines |
| DvergerTest | NpcTalk | $enemy_dverger | $enemy_dverger |  | Dverger | 18 talk lines |
| FallenWarrior | NpcTalk | $enemy_fallenwarrior | Fallen Warrior |  | FallenWarrior | 80 talk lines |
| FallenWarrior (1) | NpcTalk | $enemy_fallenwarrior | Fallen Warrior | memorialsite_offering | Props/MemorialStones | 80 talk lines |
| ShadowPerson | NpcTalk | $enemy_shadowperson | Shadow |  | FallenWarrior | 30 talk lines |
| odin | Odin |  |  |  | Odin |  |
| Halstein | Petable | $npc_halstein | Halstein | Vendor_BlackForest | Locations/BlackForest |  |
| Halstein | Petable | $npc_halstein | Halstein |  | Lox |  |
| HildirsLox | Petable | $npc_smultron | $npc_smultron |  | Lox |  |
| HildirsLox | Petable | $npc_blabar | Blåbär | Hildir_camp | Locations/Meadows |  |
| Hugin | Raven | $npc_hugin | Hugin |  | Raven | hint bird |
| Hugin | Raven | $npc_hugin | Hugin | Ravens | Raven | hint bird |
| Munin | Raven |  | Munin |  | Raven | hint bird |
| Munin | Raven | $npc_munin | Munin | Ravens | Raven | hint bird |
| BogWitch | Trader |  |  | BogWitch_Camp | Props/BogWitchHut | 20 wares |
| BogWitch | Trader |  |  |  | BogWitch | 20 wares |
| Haldor | Trader | $npc_haldor | Haldor | Vendor_BlackForest | Locations/BlackForest | 11 wares |
| Haldor | Trader | $npc_haldor | Haldor |  | TraderHaldor | 11 wares |
| Hildir | Trader | $npc_hildir | Hildir | Hildir_camp | Locations/Meadows | 38 wares |
| Hildir | Trader | $npc_hildir | Hildir |  | Hildir | 38 wares |

### 2b. NPC name tokens (`$npc_*` names)

| token | English |
|---|---|
| npc_blabar | Blåbär |
| npc_bogwitch | The Bog Witch |
| npc_giveitem | Give item |
| npc_haldor | Haldor |
| npc_hallon | Hallon |
| npc_halstein | Halstein |
| npc_hildir | Hildir |
| npc_hugin | Hugin |
| npc_munin | Munin |
| npc_valkyrie_end | Valkyrie |
| npc_valkyrie_end_interact | Journey to Valhalla |

### 2c. NPC dialogue sets (`$npc_*` lines, grouped)

| group | lines | sample (English) |
|---|---|---|
| deadspeak_bonemass | 1 | Thank you, warrior. We who lived on in this shape were denied the peace of death for too long. We go now... |
| deadspeak_eikthyr | 1 | So you were my death? You look so small and soft… Tell Oden he may have broken this form but the wilderness w… |
| deadspeak_elder | 1 | Little thing of blood and bone, I should have snapped you like a twig! Now I wither and die, let the great tr… |
| deadspeak_fader | 1 | I followed a false destiny, and it gave me nothing but ruin. Forgive me, my love... |
| deadspeak_fish | 12 | I'm a perch, not a bass! |
| deadspeak_fish_shared | 2 | Don't worry. Be happy. |
| deadspeak_hive | 1 | You think you beat us? Think again... We are still, and forever will be the hive - and the hive is unbeatable. |
| deadspeak_moder | 1 | Sheathe your pride, creature of Oden! My long reign is at an end but those who come after me are greater stil… |
| deadspeak_queen | 1 | Tell Oden that my kin will bring forth another... A monarch never dies. |
| deadspeak_yagluth | 1 | Do you come to gloat, little thing? Do you know how many times I have been killed, broken, banished, burned a… |
| fallen_viking_randomend_ | 20 | You are worthy. |
| fallen_viking_randomstart* | 20 | Let us see what you are capable of! |
| fallen_viking_randomtaunt* | 40 | Is that the best you can do? |
| fallenwarrior_title* | 202 | {name} the Wolf |
| npc_blabar | 1 | Blåbär |
| npc_bogwitch | 1 | The Bog Witch |
| npc_bogwitch_buy* | 3 | Lhm! |
| npc_bogwitch_goodbye* | 3 | Gnk. |
| npc_bogwitch_greet* | 3 | Gk! |
| npc_bogwitch_sell* | 3 | Mm, lpr... |
| npc_bogwitch_start* | 3 | Mg, lhm? |
| npc_bogwitch_talk* | 3 | Hm... |
| npc_dvergr_ashlands_aggravated* | 5 | You really shouldn't have done that! |
| npc_dvergr_ashlands_goodbye* | 5 | Take care now. |
| npc_dvergr_ashlands_greet* | 5 | Who goes there? |
| npc_dvergr_ashlands_private_area_alarm* | 5 | Hey! |
| npc_dvergr_ashlands_talk* | 40 | I think I'm going to sweat my beard off. |
| npc_dvergr_deepnorth_goodbye* | 7 | Don't worry about me, I'll be fine without you. |
| npc_dvergr_deepnorth_greet* | 7 | Who goes there? |
| npc_dvergrmage_aggravated* | 11 | I'll show you what magic can do! |
| npc_dvergrmage_goodbye* | 5 | Farewell. |
| npc_dvergrmage_greet* | 5 | You're taller than I remember. |
| npc_dvergrmage_private_area_alarm* | 8 | Hey! Be careful with that. |
| npc_dvergrmage_talk* | 20 | I hope this mine isn't as infested as the last. |
| npc_dvergrrogue_aggravated* | 11 | I'll make you bleed! |
| npc_dvergrrogue_goodbye* | 6 | Until next time. |
| npc_dvergrrogue_greet* | 6 | Who goes there? |
| npc_dvergrrogue_private_area_alarm* | 8 | I wouldn't do that if I were you. |
| npc_dvergrrogue_talk* | 20 | You never know what you'll find out there in the mist. |
| npc_giveitem | 1 | Give item |
| npc_giveitem_no | 12 | Thanks, but I don't need that. |
| npc_haldor | 1 | Haldor |
| npc_haldor_buy* | 9 | These are fine items, not the sort of things you can just pick up. |
| npc_haldor_goodbye* | 6 | Try not to wander too far. I want your things when you get killed. |
| npc_haldor_greet* | 7 | Humph! Another scrap of flesh from the Valkyries... |
| npc_haldor_sell* | 8 | Good deal. |
| npc_haldor_smalltalk* | 5 | We're all lost here. The Gods turned their eyes from Valheim a long time ago. |
| npc_haldor_start* | 5 | Take a look at my wares. |
| npc_haldor_talk* | 7 | I'm short for a dwarf. |
| npc_haldor_trade* | 3 | I'm interested in trade, not chatter. Do you want to buy something or not? |
| npc_hallon | 1 | Hallon |
| npc_halstein | 1 | Halstein |
| npc_hildir | 1 | Hildir |
| npc_hildir_buy* | 5 | Ooh, that'll look great on you! |
| npc_hildir_chest1_recieved | 1 | Yay, you found it! Now check out my wares! |
| npc_hildir_chest2_recieved | 1 | That one must have been a challenge, but you won't regret it! |
| npc_hildir_chest3_recieved | 1 | I didn't think you would manage that one. Colour me impressed! |
| npc_hildir_goodbye* | 4 | See you again soon, I hope! |
| npc_hildir_greet* | 5 | Welcome to my shop. It's a tad bit more elegant than my brother's. |
| npc_hildir_random_alreadyhavechest | 5 | You've already given me that. |
| npc_hildir_random_anylockedchest | 9 | Thanks for the help, I really appreciate it. There's still more of my stuff out there though, if you have tim… |
| npc_hildir_random_lockedchest | 6 | Almost all of my wares, stolen! What use is a shop if it doesn't even have anything to sell? |
| npc_hildir_random_map | 5 | Most of my stuff was stolen from me. I've marked out where it was on the map, in case you'd like to help me l… |
| npc_hildir_sell* | 5 | Sure, I'll take it. |
| npc_hildir_start* | 6 | Take a look, why don't you? |
| npc_hildir_talk* | 10 | It's okay, you can pet the lox if you want. |
| npc_hugin | 1 | Hugin |
| npc_munin | 1 | Munin |
| npc_munin_ashlands_bellshard | 1 | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passin… |
| npc_munin_ashlands_general* | 5 | Kraa! Poor pickings here. Empty skulls scoured clean by the flames. Barely a trace of memory left… |
| npc_munin_deepnorth_general* | 4 | This is a place of transformation. Certain creatures find a new purpose here, becoming something more than th… |
| npc_munin_deepnorth_morkborg | 1 | Kraa! Strange folk dwell here, created from the very evil itself... |
| npc_munin_deepnorth_ship | 1 | Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way fo… |
| npc_munin_general* | 10 | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees th… |
| npc_valkyrie_end* | 2 | Valkyrie |
| shadowperson_randomtalk* | 30 | His blade was dipped in poison, I stood no chance... |

### 2d. Trader wares (Haldor, Hildir, Bog Witch)

**Haldor** (`Haldor`, Assets/Characters/TraderHaldor/Haldor.prefab)

| prefab | English | stack | price | requires key |
|---|---|---|---|---|
| HelmetYule | Yule Hat | 1 | 100 |  |
| HelmetDverger | Dverger Circlet | 1 | 620 |  |
| BeltStrength | Megingjord | 1 | 950 |  |
| YmirRemains | Ymir Flesh | 1 | 120 | defeated_gdking |
| FishingRod | Fishing Rod | 1 | 350 |  |
| FishingBait | Fishing Bait | 20 | 10 |  |
| Thunderstone | Thunder Stone | 1 | 50 | defeated_gdking |
| ChickenEgg | Egg | 1 | 1500 | defeated_goblinking |
| BarrelRings | Barrel Hoops | 3 | 100 |  |
|  |  | 1 | 1000 | defeated_dragon |
|  |  | 1 | 2000 | defeated_queen |

**Hildir** (`Hildir`, Assets/Characters/Hildir/Hildir.prefab)

| prefab | English | stack | price | requires key |
|---|---|---|---|---|
| ArmorDress2 | Brown Dress with Shawl | 1 | 450 | Hildir2 |
| ArmorDress3 | Brown Dress with Beads | 1 | 550 | Hildir3 |
| ArmorDress5 | Blue Dress with Shawl | 1 | 450 | Hildir2 |
| ArmorDress6 | Blue Dress with Beads | 1 | 550 | Hildir3 |
| ArmorDress8 | Yellow Dress with Shawl | 1 | 450 | Hildir2 |
| ArmorDress9 | Yellow Dress with Beads | 1 | 550 | Hildir3 |
| ArmorDress10 | Simple Undyed Dress | 1 | 250 |  |
| ArmorTunic2 | Blue Tunic with Cape | 1 | 450 | Hildir2 |
| ArmorTunic3 | Blue Tunic with Beads | 1 | 550 | Hildir3 |
| ArmorTunic5 | Red Tunic with Cape | 1 | 450 | Hildir2 |
| ArmorTunic6 | Red Tunic with Beads | 1 | 550 | Hildir3 |
| ArmorTunic8 | Yellow Tunic with Cape | 1 | 450 | Hildir2 |
| ArmorTunic9 | Yellow Tunic with Beads | 1 | 550 | Hildir3 |
| ArmorTunic10 | Simple Undyed Tunic | 1 | 250 |  |
| ArmorDress1 | Plain Brown Dress | 1 | 350 | Hildir1 |
| ArmorDress4 | Plain Blue Dress | 1 | 350 | Hildir1 |
| ArmorDress7 | Plain Yellow Dress | 1 | 350 | Hildir1 |
| ArmorTunic1 | Plain Blue Tunic | 1 | 350 | Hildir1 |
| ArmorTunic4 | Plain Red Tunic | 1 | 350 | Hildir1 |
| ArmorTunic7 | Plain Yellow Tunic | 1 | 350 | Hildir1 |
| ArmorHarvester1 | Harvest Tunic | 1 | 550 | Hildir1 |
| ArmorHarvester2 | Harvest Dress | 1 | 550 | Hildir1 |
| HelmetHat1 | Blue Tied Headscarf | 1 | 200 | Hildir1 |
| HelmetHat2 | Green Twisted Headscarf | 1 | 250 | Hildir2 |
| HelmetHat3 | Brown Fur Cap | 1 | 200 | Hildir1 |
| HelmetHat4 | Extravagant Green Cap | 1 | 250 | Hildir2 |
| HelmetHat5 | Simple Red Cap | 1 | 150 |  |
| HelmetHat6 | Yellow Tied Headscarf | 1 | 250 | Hildir2 |
| HelmetHat7 | Red Twisted Headscarf | 1 | 300 | Hildir3 |
| HelmetHat8 | Grey Fur Cap | 1 | 300 | Hildir3 |
| HelmetHat9 | Extravagant Orange Cap | 1 | 300 | Hildir3 |
| HelmetHat10 | Simple Purple Cap | 1 | 150 |  |
| HelmetStrawHat | Straw Hat | 1 | 300 | Hildir1 |
| HelmetSweatBand | Headband | 1 | 175 |  |
| FireworksRocket_White | Basic Fireworks | 1 | 50 | Hildir3 |
| Sparkler | Sparkler | 1 | 150 |  |
| Ironpit | Iron Pit | 1 | 75 |  |
| BarberKit | Barber Kit | 1 | 600 |  |

**BogWitch** (`BogWitch`, Assets/Characters/BogWitch/BogWitch.prefab)

| prefab | English | stack | price | requires key |
|---|---|---|---|---|
| CandleWick | Candle Wick | 50 | 100 |  |
| ScytheHandle | Scythe Handle | 1 | 200 | defeated_dragon |
| MeadTrollPheromones | Love Potion | 5 | 110 |  |
| MushroomBzerker | Toadstool | 1 | 85 | defeated_dragon |
| FragrantBundle | Fragrant Bundle | 5 | 140 | defeated_dragon |
| FreshSeaweed | Fresh Seaweed | 5 | 75 |  |
| CuredSquirrelHamstring | Cured Squirrel Hamstring | 5 | 80 |  |
| PowderedDragonEgg | Powdered Dragon Eggshells | 5 | 120 |  |
| PungentPebbles | Pungent Pebbles | 5 | 125 |  |
| VineGreenSeeds | Ivy Seeds | 3 | 65 |  |
| Feaster | Serving Tray | 1 | 140 |  |
| SpiceForests | Woodland Herb Blend | 5 | 120 | defeated_gdking |
| SpiceOceans | Seafarer's Herbs | 5 | 130 | defeated_serpent |
| SpiceMountains | Mountain Peak Pepper Powder | 5 | 140 | defeated_dragon |
| SpicePlains | Grasslands Herbalist Harvest | 5 | 160 | defeated_goblinking |
| SpiceMistlands | Herbs of the Hidden Hills | 5 | 180 | defeated_queen |
| SpiceAshlands | Fiery Spice Powder | 5 | 200 | defeated_fader |
| SpiceDeepNorth | Seasoning of the Gourd | 5 | 220 | defeated_frozenking_p3 |
| BlobVial | Corked Vial | 5 | 150 | defeated_gdking |
| HelmetRootCrown | Crown of Roots | 1 | 3000 | defeated_writhan |

### 2e. Hugin / Munin guide points (raven hints placed in the world)

| key | raven | topic | text (start) | placed in | folder |
|---|---|---|---|---|---|
| batteringram | Hugin | A powerful siege engine | The enemy fortresses are built from sturdy stuff. You'll need machines like this one if you're to break through their w… | BatteringRam | Cart |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | CharredTowerRuins1_dvergr | Locations/Ashlands |
| npc_munin_ashlands_general01 | Munin |  | Kraa! Poor pickings here. Empty skulls scoured clean by the flames. Barely a trace of memory left… | MorgenHole1 | Locations/Ashlands |
| npc_munin_ashlands_general02 | Munin |  | Well met little swordling! Look around you this is the Waste of the Emerald Flame… This is the end of all pride the fir… | CharredRuins1 | Locations/Ashlands |
| npc_munin_ashlands_general03 | Munin |  | Kraa! I bring a message from the Allfather. Honor is forged in flame. Strike hard upon the anvil spare not the steel! | LeviathanLava | Locations/Ashlands |
| npc_munin_ashlands_general04 | Munin |  | Kraahaha! A fellow carrion bird! Look at you picking over these poor dead remains… | VoltureNest | Locations/Ashlands |
| crypt | Hugin | Treasures lie below | Delves and dungeons can be found across the tenth world. They are monuments of the past, and most often filled with the… | Crypt2 | Locations/BlackForest |
| crypt | Hugin | Treasures lie below | Delves and dungeons can be found across the tenth world. They are monuments of the past, and most often filled with the… | Crypt4 | Locations/BlackForest |
| crypt | Hugin | Treasures lie below | Delves and dungeons can be found across the tenth world. They are monuments of the past, and most often filled with the… | Crypt3 | Locations/BlackForest |
| hildirdungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. | Hildir_crypt | Locations/BlackForest |
| eternalpyre | Hugin |  | Be wary! The being imprisoned beyond this gate is mighty, more so than any other to be found in all of Valheim. You wou… | DN_Bossroom | Locations/DeepNorth |
| generalDN | Munin |  | Watch these lands for memorials in the honour of the warriors who came before you. They may bestow a boon upon you, sho… | ShipSetting02 | Locations/DeepNorth |
| generalDN1 | Munin |  | This is a place of transformation. Certain creatures find a new purpose here, becoming something more than they once we… | DN_gammeltrollFrac02 | Locations/DeepNorth |
| generalDN2 | Munin |  | Kraa! Those who first ventured here did not know about the underground thieves. If they had known, they would certainly… | TheHole01 | Locations/DeepNorth |
| morkborg | Munin |  | Kraa! Strange folk dwell here, created from the very evil itself... | MorkBorg | Locations/DeepNorth |
| shipDN | Munin |  | Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way for them to… | FrozenShip02_DN | Locations/DeepNorth |
| shipDN | Munin |  | Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way for them to… | FrozenShip01_DN | Locations/DeepNorth |
| Eikthyr | Hugin | Calling forth the beast | You have found the summoning place of one of the Forsaken. Make the correct offering at their altar and they will come.… | Eikthyrnir | Locations/Meadows |
| temple1 | Hugin | Welcome to the tenth world, warrior | I am Hugin, sent here to guide you in your travels. / / The megaliths surrounding you are the Sacrificial Stones. They… | StartTemple | Locations/Meadows |
| temple2 | Hugin | This stone is a Vegvisir | These magical stones were scattered throughout the lands by Oden as signposts pointing toward the ritual grounds of the… | StartTemple | Locations/Meadows |
| temple4 | Hugin | Oden is pleased | You have been granted the power of Eikthyr. Use it in times of need. / / Your next target dwells in the black forest. G… | StartTemple | Locations/Meadows |
| bathtub | Hugin |  | Jump in, the water's nice and warm! | DevBedchamber | Locations/Misc |
| magetable1 | Hugin |  | Trying your hand at scrying the runes are you? I'm impressed, sorcerer! / / Charms and conjurations will take their tol… | DevHouse5 | Locations/Misc |
| magetable1 | Hugin |  | Trying your hand at scrying the runes are you? I'm impressed, sorcerer! / / Charms and conjurations will take their tol… | DevHouse4 | Locations/Misc |
| magetable1 | Hugin |  | Trying your hand at scrying the runes are you? I'm impressed, sorcerer! / / Charms and conjurations will take their tol… | DevMageRoom | Locations/Misc |
| maptable | Hugin | Record your exploration | With this table you can record and share your explorations of the tenth world. Use the toolbox to record your progress… | DevForge | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouse1 | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouse4 | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevBedchamber | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouse3 | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouse5 | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouseStart | Locations/Misc |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | DevHouse2 | Locations/Misc |
| smelter | Hugin | You have built a smelter | Deposit your raw ore in this furnace and it will melt away all impurities, leaving you with a bar of refined metal to w… | DevForge | Locations/Misc |
| smelter | Hugin | You have built a smelter | Deposit your raw ore in this furnace and it will melt away all impurities, leaving you with a bar of refined metal to w… | DevHouseStart | Locations/Misc |
| tissueref1 | Hugin |  | Warrior, what kind of contraption is this?! It looks dangerous... / / There is a grinding funnel up here, I wonder what… | DevHouse5 | Locations/Misc |
| tissueref1 | Hugin |  | Warrior, what kind of contraption is this?! It looks dangerous... / / There is a grinding funnel up here, I wonder what… | DevMageRoom | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevHouse4 | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevHouse5 | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevHouse1 | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevForge | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevKitchen | Locations/Misc |
| workbench | Hugin | A new tool | Now you'll be able to craft ceramic tiles, which have excellent heat insulation. / You'll need them if you're to travel… | DevHouse5 | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevGarden | Locations/Misc |
| workbench | Hugin | A new tool | Now you'll be able to craft ceramic tiles, which have excellent heat insulation. / You'll need them if you're to travel… | DevForge | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevHouseStart | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevHouse3 | Locations/Misc |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | DevMageRoom | Locations/Misc |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_Excavation2 | Locations/Mistlands |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_GuardTower3_new | Locations/Mistlands |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_GuardTower2_new | Locations/Mistlands |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_Excavation1 | Locations/Mistlands |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_Lighthouse1_new | Locations/Mistlands |
| dvergr1 | Munin |  | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in… | Mistlands_GuardTower1_new | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_GuardTower2_new | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_Excavation1 | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_Lighthouse1_new | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_GuardTower1_new | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_Excavation2 | Locations/Mistlands |
| dvergr2 | Hugin |  | Kra-kraaa! Stubborn fools! The Dvergr will tolerate strangers and vagrants but they are quick to anger should you upset… | Mistlands_GuardTower3_new | Locations/Mistlands |
| dvergrhalls1 | Munin |  | These are the Dvergrhomes, built long ago in a gilded age... Their splendour rivalled the Golden Hall itself! / / Regre… | Mistlands_DvergrTownEntrance1 | Locations/Mistlands |
| dvergrhalls1 | Munin |  | These are the Dvergrhomes, built long ago in a gilded age... Their splendour rivalled the Golden Hall itself! / / Regre… | Mistlands_DvergrTownEntrance2 | Locations/Mistlands |
| dvergrhalls2 | Hugin |  | As I am sure my lesser brother tells you, the halls beyond are very impressive... / / Just try not to get lost in the w… | Mistlands_DvergrTownEntrance2 | Locations/Mistlands |
| dvergrhalls2 | Hugin |  | As I am sure my lesser brother tells you, the halls beyond are very impressive... / / Just try not to get lost in the w… | Mistlands_DvergrTownEntrance1 | Locations/Mistlands |
| giantremains1 | Munin |  | Shadows of an ancient age. The Jotunn once ruled the tenth world, until their time ran out and they were ousted by some… | Mistlands_Giant2 | Locations/Mistlands |
| giantremains1 | Munin |  | Shadows of an ancient age. The Jotunn once ruled the tenth world, until their time ran out and they were ousted by some… | Mistlands_Giant1 | Locations/Mistlands |
| giantremains2 | Hugin |  | Kraa! Pick their bones and break their domes! | Mistlands_Giant1 | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_Harbour1 | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_Lighthouse1_new | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_Excavation2 | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_Excavation1 | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_GuardTower3_new | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_GuardTower2_new | Locations/Mistlands |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | Mistlands_GuardTower1_new | Locations/Mistlands |
| elitedungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. | Hildir_cave | Locations/Mountains |
| upgradestation | Munin | Hear ye, wanderer! | Finding this place of ancient power is no easy feat, and those who have done so stand to gain a great deal! A word of w… | AncientUpgradeStation | Locations/Mountains |
| bathtub | Hugin |  | Jump in, the water's nice and warm! | piece_bathtub | Pieces |
| eternalpyre | Hugin |  | It is not just a dragon's breath that burns bright, but its very essence as well. One can harness it, with enough skill… | piece_EternalPyre | Pieces |
| faderember1 | Hugin |  | It is not just a dragon's breath that burns bright, but its very essence as well. One can harness it, with enough skill… | piece_FaderEmbers | Pieces |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | guard_stone | Pieces |
| magetable1 | Hugin |  | Trying your hand at scrying the runes are you? I'm impressed, sorcerer! / / Charms and conjurations will take their tol… | piece_magetable | Pieces |
| maptable | Hugin | Record your exploration | With this table you can record and share your explorations of the tenth world. Use the toolbox to record your progress… | piece_cartographytable | Pieces |
| portal | Hugin |  | Portals are great for fast travel between different parts of the world. / / Of course, you need to build one on the oth… | portal_stone | Pieces |
| portal | Hugin |  | Portals are great for fast travel between different parts of the world. / / Of course, you need to build one on the oth… | portal_wood | Pieces |
| shieldgenerator | Hugin | Means of protection | Kraa! This should keep away whatever the sky might throw at you. | piece_shieldgenerator | Pieces |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | bed | Pieces |
| sleepingspot | Hugin | A headrest for the weary! | Sleep the night away in your bed and awaken feeling refreshed and full of energy. / / Another improvement to your home… | ashwood_bed | Pieces |
| smelter | Hugin | You have built a smelter | Deposit your raw ore in this furnace and it will melt away all impurities, leaving you with a bar of refined metal to w… | smelter | Pieces |
| tissueref1 | Hugin |  | Warrior, what kind of contraption is this?! It looks dangerous... / / There is a grinding funnel up here, I wonder what… | eitrrefinery | Pieces |
| upgradestation | Munin | Hear ye, wanderer! | Finding this place of ancient power is no easy feat, and those who have done so stand to gain a great deal! A word of w… | UpgradeStation | Pieces |
| wispattractor1 | Hugin |  | A strange edifice indeed. I wonder what it will attract. Let us just perch here for a while and see what happens... / /… | piece_wisplure | Pieces |
| workbench | Hugin | A new tool | Now you'll be able to craft ceramic tiles, which have excellent heat insulation. / You'll need them if you're to travel… | artisan_ext1 | Pieces |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | piece_workbench | Pieces |
| workbench | Hugin | You have built a workbench | A workbench allows you to craft <color=yellow>complex items</color> as well as giving you access to lots of more <color… | piece_magetable | Pieces |
| guardstone | Hugin | You have built a ward | The ward emits a strong aura which prevents other vikings from building things. It also locks all doors within its infl… | dverger_guardstone | Props/Dvergr |
| shipDN | Munin |  | Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way for them to… | frozenship | Props/FrozenShips |
| yggdrasilroot | Hugin |  | Kraa! Although severed from the trunk, I can see that magic still lingers in the roots of the old ash. / / Needless to… | YggdrasilRoot | Props/Mistlands |
| temple4 | Hugin | Oden is pleased | You have been granted the power of Eikthyr. Use it in times of need. / / Your next target dwells in the black forest. G… | StartPlatform | Props/StartTemple |
| temple4 | Hugin |  | You have been granted the power of Eikthyr. Use it in times of need. / / Your next target dwells in the black forest. G… | BossStone_Eikthyr | Props/StartTemple |
|  | Hugin |  |  | GuidePoint | Raven |
| npc_munin_ashlands_general05 | Munin |  | Hmmm... How do you yet live warrior? Perhaps you have found your element here in the furnace? Curious... | CharredRuins25 | Rooms/ashlands |
| generalDN4 | Munin |  | Long before the Allfather summoned you, others were tasked to guard this world. Even though many years have gone by sin… | northvillage_fence2 | Rooms/northVillage |
| end_hugin | Hugin | Journey to Valhalla | Well done, warrior! / At long last, you have slain all Forsaken and proven yourself worthy. The gates of Valhalla are n… | Valkyrie_End | Valkyrie |
| end_munin | Munin | And so the saga comes to an end... | You have fought well, warrior. / Thanks to you, the Forsaken are no more, and this world can once again be reunited wit… | Valkyrie_End | Valkyrie |

## 3. Creatures

Every prefab root with a `Character`/`Humanoid` component in the main prefab bundle. Faction is `Character.Faction`. Boss = `m_boss`; event = `m_bossEvent` (music/raid id); key = `m_defeatSetGlobalKey`. DN = Deep North (faction DeepNorth, in the Deep North spawn list, or a Deep North asset folder).

| prefab | token | English | HP | faction | boss / event / key | tame | DN | folder | drops |
|---|---|---|---|---|---|---|---|---|---|
| Abomination | $enemy_abomination | Abomination | 800.0 | Undead |  |  |  | Abomination | TrophyAbomination x1 (50%); Root x5; Guck x3-5 |
| Asksvin | $enemy_asksvin | Asksvin | 800.0 | Demon |  | yes |  | Asksvin | TrophyAsksvin x1 (10%); AskBladder x1; AskHide x2-3; AsksvinMeat x2-3 |
| Asksvin_hatchling | $enemy_asksvin_hatchling | Asksvin Hatchling | 400.0 | Undead |  |  |  | Asksvin | AskBladder x1 (20%); AskHide x1 (20%); AsksvinMeat x1 (20%) |
| Barka | $enemy_barka | Barka | 2200.0 | DeepNorth |  |  | DN | Barka | TrophyBarka x1 (10%); BarkaBranch x1 |
| Bat | $enemy_bat | Bat | 10.0 | MountainMonsters | key=KilledBat |  |  | Bat | LeatherScraps x1 (50%) |
| Bat_Swamp | $enemy_bat | Bat | 10.0 | Undead | key=KilledBat |  |  | Bat | LeatherScraps x1 (50%) |
| Bjorn | $enemy_bjorn | Bear | 500.0 | ForestMonsters |  |  |  | Bjorn | BjornPaw x1; BjornMeat x2-3; BjornHide x4-5; TrophyBjorn x1 (10%) |
| Bjorn_sleeping | $enemy_bjorn | Bear | 500.0 | ForestMonsters |  |  |  | Bjorn | BjornPaw x1; BjornMeat x2-3; BjornHide x4-5; TrophyBjorn x1 (10%) |
| Bjorn_spiritcaller | $spiritcaller_bjorn |  | 1500.0 | Players |  | yes |  | Bjorn |  |
| Unbjorn | $enemy_unbjorn | Vile | 1200.0 | PlainsMonsters |  |  |  | Bjorn | TrophyBjornUndead x1 (10%); BjornMeat x2-3; RottenMeat x1-2 (80%); UndeadBjornRibcage x1-3; BjornHide x1-2 |
| Blob | $enemy_blob | Blob | 50.0 | Undead |  |  |  | Blob | TrophyBlob x1 (10%); Ooze x1-2 |
| BlobAspect | $enemy_blob | Blob | 50.0 | Undead |  |  |  | Blob | TrophyBlob x1 (10%); Ooze x1-2 |
| BlobElite | $enemy_blobelite | Oozer | 150.0 | Undead |  |  |  | Blob | Ooze x2-3; IronScrap x1 (33%); TrophyBlob x1 (10%); Blob x2 |
| BlobFrost | $enemy_blobfrost | Frost Blob | 50.0 | MountainMonsters |  |  |  | Blob | Crystal x1-2; TrophyBlob_Frost x1 (10%) |
| BlobLava | $enemy_bloblava | Lava Blob | 300.0 | Demon |  |  |  | Blob | ProustitePowder x1-2; SulfurStone x1-2; TrophyBlob_Lava x1 (10%) |
| BlobMork | $enemy_blobmork | Shapeless Pulp | 150.0 | DeepNorth |  |  | DN | Blob | TrophyBlob_Morkhalla x1 (10%); BlobMorkMini x1-2; OozeMork x1 (50%) |
| BlobMorkMini | $enemy_blobmorkmini | Tiny Pulp | 50.0 | DeepNorth |  |  | DN | Blob | OozeMork x1 (25%) |
| BlobTar | $enemy_blobtar | Growth | 100.0 | Undead |  |  |  | Blob | TrophyGrowth x1 (10%); Tar x1 |
| Boar | $enemy_boar | Boar | 10.0 | ForestMonsters |  | yes |  | Boar | RawMeat x1; LeatherScraps x1; TrophyBoar x1 (15%) |
| Boar_piggy | $enemy_boarpiggy | Piggy | 10.0 | ForestMonsters |  |  |  | Boar |  |
| Boar_spiritcaller | $spiritcaller_boar |  | 1000.0 | Players |  | yes |  | Boar |  |
| Bonemass | $enemy_bonemass | Bonemass | 5000.0 | Boss | BOSS ev=boss_bonemass key=defeated_bonemass |  |  | Bonemass | TrophyBonemass x1; Wishbone x1 |
| BonemawSerpent | $enemy_bonemawserpent | Bonemaw | 1100.0 | Demon |  |  |  | BonemawSerpent | TrophyBonemawSerpent x1 (33%); BoneMawSerpentMeat x6-8; BonemawSerpentTooth x8-10 |
| Chicken | $enemy_chicken | Chicken | 10.0 | ForestMonsters |  |  |  | Chicken | ChickenMeat x1 (25%); Feathers x1-2 (50%) |
| Hen | $enemy_hen | Hen | 10.0 | ForestMonsters |  | yes |  | Chicken | ChickenMeat x1; Feathers x1-3 |
| Deathsquito | $enemy_deathsquito | Deathsquito | 10.0 | PlainsMonsters |  |  |  | Deathsquito | Needle x1; TrophyDeathsquito x1 (5%) |
| Deer | $enemy_deer | Deer | 10.0 | ForestMonsters |  |  |  | Deer | DeerMeat x2; DeerHide x1-3; TrophyDeer x1 (50%) |
| Deer_White | $enemy_deerwhite |  | 30.0 | ForestMonsters |  |  |  | Deer | DeerMeat x2; TrophyDeerWhite x1 |
| Dragon | $enemy_dragon | Moder | 7500.0 | Boss | BOSS ev=boss_moder key=defeated_dragon |  |  | Dragon | TrophyDragonQueen x1; DragonTear x10 |
| Draugr | $enemy_draugr | Draugr | 100.0 | Undead |  |  |  | Draugr | Entrails x1; TrophyDraugr x1 (10%) |
| Draugr_Elite | $enemy_draugrelite | Draugr Elite | 200.0 | Undead |  |  |  | Draugr | Entrails x2-3; TrophyDraugrElite x1 (10%) |
| Draugr_Elite_sleeping | $enemy_draugrelite | Draugr Elite | 200.0 | Undead |  |  |  | Draugr | Entrails x2-3; TrophyDraugrElite x1 (10%) |
| Draugr_Ranged | $enemy_draugr | Draugr | 100.0 | Undead |  |  |  | Draugr | Entrails x1; TrophyDraugr x1 (10%) |
| Draugr_Ranged_sleeping | $enemy_draugr | Draugr | 100.0 | Undead |  |  |  | Draugr | Entrails x1; TrophyDraugr x1 (10%) |
| Draugr_sleeping | $enemy_draugr | Draugr | 100.0 | Undead |  |  |  | Draugr | Entrails x1; TrophyDraugr x1 (10%) |
| Dverger | $enemy_dvergr | Dvergr Rogue | 350.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerAshlands | $enemy_dvergr | Dvergr Rogue | 1000.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerDeepNorth | $enemy_dvergr_deepnorth | Imprisoned Dvergr | 1500.0 | Dverger |  |  | DN | Dverger | Coins x10-20; TrophyDvergr x1 (5%); AncientGemstoneBlack x1 (10%); AncientGemstoneGreen x1 (10%); AncientGemstoneOrange x1 (10%); AncientGemstonePurple x1 (10%) |
| DvergerMage | $enemy_dvergr_mage | Dvergr Mage | 350.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerMageFire | $enemy_dvergr_mage | Dvergr Mage | 350.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerMageIce | $enemy_dvergr_mage | Dvergr Mage | 350.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerMageSupport | $enemy_dvergr_mage | Dvergr Mage | 350.0 | Dverger |  |  |  | Dverger | Softtissue x1-2 (25%); BlackMarble x1-2 (50%); Coins x2-15; TrophyDvergr x1 (5%) |
| DvergerTest | $enemy_dverger |  | 100.0 | Dverger |  |  |  | Dverger | Coins x20-40 (25%); BlackMetalScrap x1-2; Pukeberries x1-2; TrophyGoblinShaman x1 (10%) |
| Mistile | $enemy_mistile | Mistile | 1.0 | Dverger |  |  |  | Dverger |  |
| Eikthyr | $enemy_eikthyr | Eikthyr | 500.0 | Boss | BOSS ev=boss_eikthyr key=defeated_eikthyr |  |  | Eikthyr | TrophyEikthyr x1; HardAntler x3 |
| Elaking | $enemy_elaking | Elaking | 350.0 | DeepNorth |  |  | DN | Elaking | ElakingHairBundle x1-2; TrophyElaking x1 (10%); MoldKeys x1 (20%) |
| ElakingLantern | $enemy_elaking | Elaking | 350.0 | DeepNorth |  |  | DN | Elaking | ElakingHairBundle x1-2; TrophyElaking x1 (10%) |
| ElakingMole | $enemy_elakingmole | Eyeless One | 1400.0 | DeepNorth | key=elakingmole_defeated |  | DN | ElakingMole | TrophyMole x1 (10%); MoleClaws x1-2; MoldKeys x1 (50%) |
| Fader | $enemy_fader | Fader | 25000.0 | Boss | BOSS ev=boss_fader key=defeated_fader |  |  | Fader | TrophyFader x1; FaderDrop x5 |
| FallenValkyrie | $enemy_fallenvalkyrie | Fallen Valkyrie | 1500.0 | Demon |  |  |  | FallenValkyrie | CelestialFeather x2-4; TrophyFallenValkyrie x1 (5%) |
| FallenWarrior | $enemy_fallenwarrior | Fallen Warrior | 750.0 | Undead |  |  | DN | FallenWarrior | OrbFrostFire x1 (50%); OrbThunderBlood x1 (50%) |
| ShadowPerson | $enemy_shadowperson | Shadow | 750.0 | DeepNorth |  |  | DN | FallenWarrior |  |
| Fenring | $enemy_fenring | Fenring | 300.0 | MountainMonsters |  |  |  | Fenring | WolfFang x1-2; TrophyFenring x1 (10%) |
| Fenring_Cultist | $enemy_fenringcultist | Cultist | 200.0 | MountainMonsters |  |  |  | Fenring | JuteRed x1-3; TrophyCultist x1 (10%) |
| Fenring_Cultist_Hildir | $enemy_fenringcultist_hildir | <color=orange>Geirrhafa</color> | 3700.0 | MountainMonsters | key=BossHildir2 |  |  | Fenring | chest_hildir2 x1; TrophyCultist_Hildir x1 |
| Fenring_Cultist_Hildir_nochest | $enemy_fenringcultist_hildir | <color=orange>Geirrhafa</color> | 1850.0 | MountainMonsters | key=BossHildir2 |  |  | Fenring | TrophyCultist_Hildir x1 |
| FrostWisp | $enemy_frostwisp |  | 100.0 | TrainingDummy |  |  |  | Frostwisp | LeatherScraps x1 (50%) |
| FrozenKing | $enemy_frozenking | Kall Fimbulbringer | 10000.0 | Boss | BOSS ev=boss_frozenking key=defeated_frozenking |  | DN | FrozenKing |  |
| FrozenKing_p2 | $enemy_frozenking | Kall Fimbulbringer | 7000.0 | Boss | BOSS ev=boss_frozenking |  | DN | FrozenKing |  |
| FrozenKing_p3 | $enemy_frozenking_p3 | Kall Fimbulbringer | 30000.0 | Boss | BOSS ev=boss_frozenking key=defeated_frozenking_p3 |  | DN | FrozenKing | FrozenKingDrop x1; CrownJewel x1 |
| Tendril_back | Root |  | 1000.0 | Boss |  |  | DN | FrozenKing |  |
| Aspect_Bonemass | $enemy_aspect_bonemass | Aspect of the Writhing Dead | 1600.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Eikthyr | $enemy_aspect_eikthyr | Aspect of the Lightning Stag | 3000.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Elder | $enemy_aspect_gdking | Aspect of the Living Forest | 1600.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Fader | $enemy_aspect_fader | Aspect of the Emerald Flame | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Moder | $enemy_aspect_dragon | Aspect of the Dragon Mother | 1500.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_SeekerQueen | $enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Yagluth | $enemy_aspect_goblinking | Aspect of the Twisted Soul | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Tendril | Tendril |  | 80.0 | Boss |  |  | DN | FrozenKing/attacks |  |
| Frysling | $enemy_frysling | Frysling | 100.0 | TrainingDummy | key=killed_frysling |  | DN | Frysling | FrostCore x1 |
| Ghost | $enemy_ghost | Ghost | 60.0 | Undead |  |  |  | Ghost | Ectoplasm x1-5; TrophyGhost x1 (10%) |
| Ghost_Void | $enemy_ghost_void | The Void | 600.0 | Undead |  |  |  | Ghost | KnifeVoid x1 |
| Ghost_old | $enemy_ghost_void | The Void | 60.0 | Undead |  |  |  | Ghost | Ectoplasm x1-2 (50%) |
| Ghost_sleeping | $enemy_ghost | Ghost | 60.0 | Undead |  |  |  | Ghost | Ectoplasm x1-5; TrophyGhost x1 (10%) |
| Gjall | $enemy_gjall | Gjall | 1500.0 | MistlandsMonsters |  |  |  | Gjall | Bilebag x1; TrophyGjall x1 (30%) |
| Goblin | $enemy_goblin | Fuling | 175.0 | PlainsMonsters |  |  |  | Goblin | Coins x5-10 (25%); BlackMetalScrap x1-2; TrophyGoblin x1 (10%) |
| GoblinArcher | $enemy_goblin | Fuling | 175.0 | PlainsMonsters |  |  |  | Goblin | Coins x5-10 (25%); BlackMetalScrap x1-2; TrophyGoblin x1 (10%) |
| GoblinDeepNorth | $enemy_goblin_deepnorth | Captive Fuling | 250.0 | PlainsMonsters |  |  | DN | Goblin | Coins x20-40 (25%); AncientCoin x1-2; Lingonberry x1-10 (20%) |
| Goblin_Gem | $enemy_gemgoblin | Riktig Fuling | 1000.0 | ForestMonsters |  |  |  | Goblin | GemstoneBlue x2-4; GemstoneGreen x2-4; GemstoneRed x2-4 |
| GoblinBrute | $enemy_goblinbrute | Fuling Berserker | 800.0 | PlainsMonsters |  |  |  | GoblinBrute | Coins x5-20; BlackMetalScrap x3-5; GoblinTotem x1 (10%); TrophyGoblinBrute x1 (5%) |
| GoblinBruteBros | $enemy_goblinbrute_hildircombined | <color=orange>Zil & Thungr</color> | 4200.0 | PlainsMonsters | key=BossHildir3 |  |  | GoblinBruteBros | GoblinShaman_Hildir x1; TrophyGoblinBruteBrosBrute x1 |
| GoblinBruteBros_nochest | $enemy_goblinbrute_hildircombined | <color=orange>Zil & Thungr</color> | 2100.0 | PlainsMonsters | key=BossHildir3 |  |  | GoblinBruteBros | GoblinShaman_Hildir_nochest x1; TrophyGoblinBruteBrosBrute x1 |
| GoblinBrute_Hildir | $enemy_goblinbrute_hildir | <color=orange>Thungr</color> | 800.0 | PlainsMonsters |  |  |  | GoblinBruteBros | chest_hildir3 x1 |
| GoblinShaman_Hildir | $enemy_goblin_hildir | <color=orange>Zil</color> | 2400.0 | PlainsMonsters |  |  |  | GoblinBruteBros | chest_hildir3 x1; TrophyGoblinBruteBrosShaman x1 |
| GoblinShaman_Hildir_nochest | $enemy_goblin_hildir | <color=orange>Zil</color> | 1200.0 | PlainsMonsters |  |  |  | GoblinBruteBros | TrophyGoblinBruteBrosShaman x1 |
| GoblinKing | $enemy_goblinking | Yagluth | 10000.0 | Boss | BOSS ev=boss_goblinking key=defeated_goblinking |  |  | GoblinKing | TrophyGoblinKing x1; YagluthDrop x3 |
| GoblinShaman | $enemy_goblinshaman | Fuling Shaman | 100.0 | PlainsMonsters |  |  |  | GoblinShaman | Coins x20-40 (25%); BlackMetalScrap x1-2; Pukeberries x1-2; TrophyGoblinShaman x1 (10%) |
| Greydwarf | $enemy_greydwarf | Greydwarf | 40.0 | ForestMonsters |  |  |  | GreyDwarf | GreydwarfEye x1 (50%); Stone x1; Wood x1; Resin x1; TrophyGreydwarf x1 (5%) |
| Greydwarf_Elite | $enemy_greydwarfbrute | Greydwarf Brute | 150.0 | ForestMonsters |  |  |  | GreyDwarf | GreydwarfEye x2 (50%); Stone x2; Wood x3-5; Dandelion x1; AncientSeed x1 (33%); TrophyGreydwarfBrute x1 (10%) |
| Greydwarf_Frozen | $enemy_greydwarf | Greydwarf | 100.0 | DeepNorth |  |  | DN | GreyDwarf | GreydwarfEye x1 (50%); Wood x1; Resin x1; TrophyGreydwarf x1 (5%); Snowball x1; Ice x1-2 |
| Greydwarf_Shaman | $enemy_greydwarfshaman | Greydwarf Shaman | 60.0 | ForestMonsters |  |  |  | GreyDwarf | GreydwarfEye x1 (50%); Wood x1; Resin x1-2; TrophyGreydwarfShaman x1 (10%); Pukeberries x1-2 |
| Greydwarf_Shaman_Frozen | $enemy_greydwarfshaman | Greydwarf Shaman | 120.0 | DeepNorth |  |  | DN | GreyDwarf | GreydwarfEye x1 (50%); Wood x1; Resin x1-2; TrophyGreydwarfShaman x1 (10%); Pukeberries x1-2; Ice x1-2 |
| Greyling | $enemy_greyling | Greyling | 20.0 | ForestMonsters |  |  |  | GreyDwarf | Resin x1 |
| Aspect_TentaRoot | $enemy_root | Root | 20.0 | Boss |  |  |  | Greydwarf_king |  |
| TentaRoot | $enemy_root | Root | 20.0 | Boss |  |  |  | Greydwarf_king |  |
| gd_king | $enemy_gdking | The Elder | 2500.0 | Boss | BOSS ev=boss_gdking key=defeated_gdking |  |  | Greydwarf_king | TrophyTheElder x1; CryptKey x1 |
| TentaRoot_wild | $enemy_root | Root | 30.0 | ForestMonsters |  |  |  | Greydwarf_king/TentaRoots |  |
| Hare | $enemy_hare | Hare | 10.0 | AnimalsVeg |  |  |  | Hare | HareMeat x1; ScaleHide x1-3; TrophyHare x1 (5%) |
| Hatchling | $enemy_drake | Drake | 100.0 | MountainMonsters |  |  |  | Hatchling | TrophyHatchling x1 (10%); FreezeGland x1-2 |
| Hive | $enemy_hive |  | 10000.0 | Boss | BOSS ev=boss_hive key=defeated_hive |  |  | Hive | TrophySeekerQueen x1; QueenDrop x1 |
| staff_greenroots_tentaroot | $enemy_summonedroot | Summoned Root | 250.0 | Players |  |  |  | Items/weapons/_res/staffs |  |
| JotunWarrior | $enemy_jotun_warrior | Krigen | 1300.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | MoldArmormediumChest x1 (3%); MoldArmorMediumHelmet x1 (3%); MoldArmorMediumLegs x1 (3%); MemorialCoal x1 (20%); TrophyJotunWarrior x1 (10%); Leatherstraps x1-3 |
| JotunWarriorDualWield | $enemy_jotun_warrior | Krigen | 1300.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | MoldArmormediumChest x1 (3%); MoldArmorMediumHelmet x1 (3%); MoldArmorMediumLegs x1 (3%); MemorialCoal x1 (20%); TrophyJotunWarrior x1 (10%); Leatherstraps x1-3 |
| JotunWitch | $enemy_jotun_witch | Hexen | 800.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | NornThread x1-3; TrophyJotunWitch x1 (10%); BloodGoldKey x1 (10%); MoldArmorMageChest x1 (5%); MoldArmorMageHelmet x1 (5%); MoldArmorMageLegs x1 (5%) |
| BogWitchKvastur | $enemy_kvastur | Kvastur | 700.0 | Dverger |  |  |  | Kvastur | Wood x1; Resin x1; TrophyKvastur x1 |
| Leech | $enemy_leech | Leech | 60.0 | Undead |  |  |  | Leech | TrophyLeech x1 (10%); Bloodbag x1 |
| Leech_cave | $enemy_leech | Leech | 60.0 | Undead |  |  |  | Leech | TrophyLeech x1 (10%); Bloodbag x1 |
| Lox | $enemy_lox | Lox | 1000.0 | PlainsMonsters |  | yes |  | Lox | LoxMeat x4-6; TrophyLox x1 (10%); LoxPelt x2-3 |
| Lox_Calf | $enemy_loxcalf | Lox Calf | 100.0 | PlainsMonsters |  |  |  | Lox | LoxMeat x1 |
| Morgen | $enemy_morgen | Morgen | 1600.0 | Demon |  |  |  | Morgen | MorgenSinew x1-2; MorgenHeart x1 (80%); TrophyMorgen x1 (5%) |
| Morgen_NonSleeping | $enemy_morgen | Morgen | 1600.0 | Demon |  |  |  | Morgen | MorgenSinew x1-2; MorgenHeart x1 (80%); TrophyMorgen x1 (5%) |
| Neck | $enemy_neck | Neck | 5.0 | ForestMonsters |  |  |  | Neck | NeckTail x1 (70%); TrophyNeck x1 (5%) |
| piece_TrainingDummy | $piece_trainingdummy | T.W.I.G. | 2500.0 | TrainingDummy |  |  |  | Pieces |  |
| Player | Human |  | 100.0 | Players |  |  |  | Player |  |
| Seeker | $enemy_seeker | Seeker | 200.0 | MistlandsMonsters |  |  |  | Seeker | BugMeat x1-2; Carapace x1-2; TrophySeeker x1 (5%) |
| SeekerBrood | $enemy_babyseeker | Seeker Brood | 20.0 | MistlandsMonsters |  |  |  | Seeker | RoyalJelly x1 (50%) |
| SeekerBrute | $enemy_seekerbrute | Seeker Soldier | 1500.0 | MistlandsMonsters |  |  |  | SeekerBrute | BugMeat x1-2; Carapace x2-4; TrophySeekerBrute x1 (5%); Mandible x1-2 |
| SeekerQueen | $enemy_seekerqueen | The Queen | 12500.0 | Boss | BOSS ev=boss_queen key=defeated_queen |  |  | SeekerQueen | TrophySeekerQueen x1; QueenDrop x5 |
| Serpent | $enemy_serpent | Serpent | 400.0 | SeaMonsters | key=defeated_serpent |  |  | Serpent | TrophySerpent x1 (33%); SerpentMeat x6-8; SerpentScale x8-10 |
| Skeleton | $enemy_skeleton | Skeleton | 40.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_DeepNorth | $enemy_skeleton | Skeleton | 100.0 | DeepNorth |  |  | DN | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1; Ice x1 |
| Skeleton_Friendly | $enemy_skeleton_summoned | Skelett | 400.0 | Players |  | yes |  | Skeleton |  |
| Skeleton_Hildir | $enemy_skeletonfire | <color=orange>Brenna</color> | 1200.0 | Undead | key=BossHildir1 |  |  | Skeleton | chest_hildir1 x1; TrophySkeletonHildir x1 |
| Skeleton_Hildir_nochest | $enemy_skeletonfire | <color=orange>Brenna</color> | 600.0 | Undead | key=BossHildir1 |  |  | Skeleton | TrophySkeletonHildir x1 |
| Skeleton_Meadows | $enemy_skeleton | Skeleton | 30.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_Meadows_noarcher | $enemy_skeleton | Skeleton | 30.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_Mountains | $enemy_skeleton | Skeleton | 75.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_Mountains_noarcher | $enemy_skeleton | Skeleton | 75.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_NoArcher | $enemy_skeleton | Skeleton | 40.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_Poison | $enemy_skeletonpoison | Rancid Remains | 100.0 | Undead |  |  |  | Skeleton | TrophySkeletonPoison x1 (10%); BoneFragments x3 |
| Skeleton_Swamps | $enemy_skeleton | Skeleton | 60.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_Swamps_noarcher | $enemy_skeleton | Skeleton | 60.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| Skeleton_aspect | $enemy_skeleton | Skeleton | 40.0 | Undead |  |  |  | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1 |
| StoneGolem | $enemy_stonegolem | Stone Golem | 800.0 | ForestMonsters |  |  |  | StoneGolem | TrophySGolem x1 (5%); Stone x5-10; Crystal x8-12 |
| Surtling | $enemy_surtling | Surtling | 20.0 | Demon | key=killed_surtling |  |  | Surtling | Coal x4-5; SurtlingCore x1 (50%); TrophySurtling x1 (5%) |
| Charred_Archer | $enemy_charred_archer | Charred Marksman | 200.0 | Demon |  |  |  | TheCharred | CharredBone x1-3; TrophyCharredArcher x1 (5%) |
| Charred_Archer_Fader | $enemy_charred_melee_Fader | Summoned Charred Warrior | 50.0 | Demon |  |  |  | TheCharred |  |
| Charred_Mage | $enemy_charred_mage | Charred Warlock | 600.0 | Demon |  |  |  | TheCharred | CharredBone x1-3; TrophyCharredMage x1 (5%) |
| Charred_Melee | $enemy_charred_melee | Charred Warrior | 600.0 | Demon |  |  |  | TheCharred | CharredBone x1-3; TrophyCharredMelee x1 (5%) |
| Charred_Melee_Dyrnwyn | $enemy_charred_melee_Dyrnwyn | <color=orange>Lord Reto</color> | 2500.0 | Demon |  |  |  | TheCharred | DyrnwynHiltFragment x1 |
| Charred_Melee_Fader | $enemy_charred_melee_Fader | Summoned Charred Warrior | 50.0 | Demon |  |  |  | TheCharred |  |
| Charred_Twitcher | $enemy_charred_twitcher | Charred Twitcher | 220.0 | Demon |  |  |  | TheCharred | CharredBone x1-2 |
| Charred_Twitcher_Summoned | $enemy_charred_twitcher_summoned | Summoned Twitcher | 50.0 | Demon |  |  |  | TheCharred | CharredBone x1-2 |
| TheHive | $enemy_thehive |  | 5000.0 | MistlandsMonsters | BOSS |  |  | TheHive | TrophyHatchling x1 (10%); FreezeGland x1-2 |
| Tick | $enemy_tick | Tick | 50.0 | MistlandsMonsters |  |  |  | Tick | GiantBloodSack x1; TrophyTick x1 (5%) |
| TrainingDummy | TrainingDummy |  | 40000.0 | Undead |  |  |  | TrainingDummy | Entrails x1 (50%); TrophyDraugr x1 (10%) |
| Troll | $enemy_troll | Troll | 600.0 | ForestMonsters | key=KilledTroll |  |  | Troll | Coins x20-30; TrophyFrostTroll x1 (50%); TrollHide x5 |
| TrollFrost | $enemy_trollfrost | Gammeltroll | 3000.0 | DeepNorth |  |  | DN | Troll |  |
| Troll_Summoned | $enemy_summonedtroll | Summoned Troll | 2000.0 | PlayerSpawned | key=KilledTroll |  |  | Troll |  |
| Troll_sleeping | $enemy_troll | Troll | 600.0 | ForestMonsters | key=KilledTroll |  |  | Troll | Coins x20-30; TrophyFrostTroll x1 (50%); TrollHide x5 |
| Ulv | $enemy_ulv | Ulv | 50.0 | MountainMonsters |  |  |  | Ulv | WolfFang x1-2 (50%); TrophyUlv x1 (10%) |
| Volture | $enemy_volture | Volture | 200.0 | Demon |  |  |  | Volture | TrophyVolture x1 (10%); VoltureMeat x1; Feathers x2-3 (50%); VoltureEgg x1-2 (50%) |
| Wolf | $enemy_wolf | Wolf | 80.0 | MountainMonsters |  | yes |  | Wolf | TrophyWolf x1 (10%); WolfMeat x1; WolfPelt x1-2; WolfFang x1 (40%) |
| Wolf_cub | $enemy_wolfcub | Wolf Cub | 10.0 | MountainMonsters |  |  |  | Wolf |  |
| Wolf_spiritcaller | $spiritcaller_wolf |  | 800.0 | Players |  | yes |  | Wolf |  |
| Wraith | $enemy_wraith | Wraith | 100.0 | Undead |  |  |  | Wraith | TrophyWraith x1 (5%); Chain x1 |
| Writhan | $enemy_writhan | Writhan | 400.0 | Undead | key=defeated_writhan |  | DN | Writhan | TrophyWrithan x1 (10%); WrithanRoots x1-2 |
| Moose | $enemy_moose | Moose | 1000.0 | DeepNorth |  | yes | DN | moose | MooseMeat x4-6; TrophyMoose x1 (10%); MooseHide x2-3; MooseSinew x2-3 |
| Moose_calf | $enemy_moosecalf | Moose Calf | 1000.0 | DeepNorth |  |  | DN | moose |  |
| Moose_spiritcaller | $spiritcaller_moose |  | 1100.0 | Players |  | yes | DN | moose |  |
| Seal | $enemy_seal | Seal | 400.0 | DeepNorth |  |  | DN | seal | SealHide x2-3; TrophySeal x1 (10%); SealBlubber x2-3 |
| Seal_Pup | $enemy_seal_baby | Baby Seal | 200.0 | DeepNorth |  |  | DN | seal | SealBlubber x1 (10%) |

### 3b. `$enemy_*` tokens with English (including ones not tied to a prefab above)

| token | English | note |
|---|---|---|
| enemy_abomination | Abomination |  |
| enemy_asksvin | Asksvin |  |
| enemy_asksvin_hatchling | Asksvin Hatchling |  |
| enemy_aspect_bonemass | Aspect of the Writhing Dead |  |
| enemy_aspect_dragon | Aspect of the Dragon Mother |  |
| enemy_aspect_eikthyr | Aspect of the Lightning Stag |  |
| enemy_aspect_fader | Aspect of the Emerald Flame |  |
| enemy_aspect_gdking | Aspect of the Living Forest |  |
| enemy_aspect_goblinking | Aspect of the Twisted Soul |  |
| enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch |  |
| enemy_babyseeker | Seeker Brood |  |
| enemy_barka | Barka |  |
| enemy_bat | Bat |  |
| enemy_bigblob | Hexahedric Pulp | not on a root Character |
| enemy_bjorn | Bear |  |
| enemy_blob | Blob |  |
| enemy_blobelite | Oozer |  |
| enemy_blobfrost | Frost Blob |  |
| enemy_bloblava | Lava Blob |  |
| enemy_blobmork | Shapeless Pulp |  |
| enemy_blobmorkmini | Tiny Pulp |  |
| enemy_blobtar | Growth |  |
| enemy_boar | Boar |  |
| enemy_boarpiggy | Piggy |  |
| enemy_bonemass | Bonemass |  |
| enemy_bonemawserpent | Bonemaw |  |
| enemy_charred | Charred | not on a root Character |
| enemy_charred_archer | Charred Marksman |  |
| enemy_charred_grunt | Charred Grunt | not on a root Character |
| enemy_charred_mage | Charred Warlock |  |
| enemy_charred_melee | Charred Warrior |  |
| enemy_charred_melee_Dyrnwyn | <color=orange>Lord Reto</color> |  |
| enemy_charred_melee_Fader | Summoned Charred Warrior |  |
| enemy_charred_twitcher | Charred Twitcher |  |
| enemy_charred_twitcher_summoned | Summoned Twitcher |  |
| enemy_charredspawnercross | Effigy of Malice | not on a root Character |
| enemy_charredtwitcherspawner | Monument of Torment | not on a root Character |
| enemy_chicken | Chicken |  |
| enemy_deathsquito | Deathsquito |  |
| enemy_deer | Deer |  |
| enemy_dragon | Moder |  |
| enemy_drake | Drake |  |
| enemy_draugr | Draugr |  |
| enemy_draugrelite | Draugr Elite |  |
| enemy_draugrspawner | Body Pile | not on a root Character |
| enemy_dvergr | Dvergr Rogue |  |
| enemy_dvergr_deepnorth | Imprisoned Dvergr |  |
| enemy_dvergr_mage | Dvergr Mage |  |
| enemy_dvergrs | Dvergr | not on a root Character |
| enemy_eikthyr | Eikthyr |  |
| enemy_elaking | Elaking |  |
| enemy_elakingmole | Eyeless One |  |
| enemy_fader | Fader |  |
| enemy_fader_codename | The Emerald Flame | not on a root Character |
| enemy_fallenvalkyrie | Fallen Valkyrie |  |
| enemy_fallenwarrior | Fallen Warrior |  |
| enemy_fenring | Fenring |  |
| enemy_fenringcultist | Cultist |  |
| enemy_fenringcultist_hildir | <color=orange>Geirrhafa</color> |  |
| enemy_frozenking | Kall Fimbulbringer |  |
| enemy_frozenking_p3 | Kall Fimbulbringer |  |
| enemy_frysling | Frysling |  |
| enemy_gdking | The Elder |  |
| enemy_gemgoblin | Riktig Fuling |  |
| enemy_ghost | Ghost |  |
| enemy_ghost_void | The Void |  |
| enemy_gjall | Gjall |  |
| enemy_goblin | Fuling |  |
| enemy_goblin_deepnorth | Captive Fuling |  |
| enemy_goblin_hildir | <color=orange>Zil</color> |  |
| enemy_goblinbrute | Fuling Berserker |  |
| enemy_goblinbrute_hildir | <color=orange>Thungr</color> |  |
| enemy_goblinbrute_hildircombined | <color=orange>Zil & Thungr</color> |  |
| enemy_goblinking | Yagluth |  |
| enemy_goblinshaman | Fuling Shaman |  |
| enemy_greydwarf | Greydwarf |  |
| enemy_greydwarfbrute | Greydwarf Brute |  |
| enemy_greydwarfshaman | Greydwarf Shaman |  |
| enemy_greydwarfspawner | Greydwarf Nest | not on a root Character |
| enemy_greyling | Greyling |  |
| enemy_hare | Hare |  |
| enemy_hen | Hen |  |
| enemy_holespawner | Wardrobe Shaft | not on a root Character |
| enemy_jotun_warrior | Krigen |  |
| enemy_jotun_witch | Hexen |  |
| enemy_kvastur | Kvastur |  |
| enemy_leech | Leech |  |
| enemy_lox | Lox |  |
| enemy_loxcalf | Lox Calf |  |
| enemy_mistile | Mistile |  |
| enemy_moose | Moose |  |
| enemy_moosecalf | Moose Calf |  |
| enemy_morgen | Morgen |  |
| enemy_neck | Neck |  |
| enemy_root | Root |  |
| enemy_seal | Seal |  |
| enemy_seal_baby | Baby Seal |  |
| enemy_seeker | Seeker |  |
| enemy_seekerbrute | Seeker Soldier |  |
| enemy_seekerqueen | The Queen |  |
| enemy_serpent | Serpent |  |
| enemy_shadowperson | Shadow |  |
| enemy_skeleton | Skeleton |  |
| enemy_skeleton_summoned | Skelett |  |
| enemy_skeletonfire | <color=orange>Brenna</color> |  |
| enemy_skeletonpoison | Rancid Remains |  |
| enemy_skeletonspawner | Evil Bone Pile | not on a root Character |
| enemy_stonegolem | Stone Golem |  |
| enemy_summonedroot | Summoned Root |  |
| enemy_summonedtroll | Summoned Troll |  |
| enemy_surtling | Surtling |  |
| enemy_tick | Tick |  |
| enemy_troll | Troll |  |
| enemy_trollfrost | Gammeltroll |  |
| enemy_ulv | Ulv |  |
| enemy_unbjorn | Vile |  |
| enemy_volture | Volture |  |
| enemy_wolf | Wolf |  |
| enemy_wolfcub | Wolf Cub |  |
| enemy_wraith | Wraith |  |
| enemy_writhan | Writhan |  |

### 3c. Natural spawn tables (`SpawnSystemList`)

| list | spawner | prefab | biome | requires key | weather | time | levels | state |
|---|---|---|---|---|---|---|---|---|
| mistlands | Seeker | Seeker | Mistlands |  |  | day | 1-2 |  |
| mistlands | Dverger | Dverger | Mistlands |  |  | day | 1-3 |  |
| mistlands | Seeker | Seeker | Mistlands |  |  | night | 1-3 |  |
| mistlands | Gjall | Gjall | Mistlands |  |  | day | 0-0 |  |
| mistlands | Gjall | Gjall | Mistlands |  |  | night | 1-3 |  |
| mistlands | Hare | Hare | Mistlands |  |  | any | 1-3 |  |
| mistlands | Seeker Brute | SeekerBrute | Mistlands |  |  | any | 1-3 |  |
| mistlands | Seeker defeated queen other biomes | Seeker | Meadows, Swamp, Mountain, BlackForest, Plains | defeated_queen |  | night | 1-1 |  |
| mistlands | SeekerBrood defeated queen other biomes | SeekerBrood | Mistlands | defeated_queen |  | night | 1-1 |  |
| mistlands | Tick defeated queen other biomes | Tick | Mistlands | defeated_queen |  | night | 1-3 |  |
| base | deer | Deer | Meadows, BlackForest |  |  | any | 1-3 |  |
| base | Boar | Boar | Meadows |  |  | any | 1-3 |  |
| base | Neck lakes | Neck | Meadows |  |  | any | 1-3 |  |
| base | Neck IN RAIN | Neck | Meadows |  | Rain,ThunderStorm,LightRain | any | 1-3 |  |
| base | Seagull | Seagal | Meadows, BlackForest, Plains, Ocean |  |  | any | 1-1 |  |
| base | Fish1 | Fish1 | Meadows, BlackForest, Plains |  |  | any | 1-5 |  |
| base | Fish2 | Fish2 | Meadows, BlackForest, Plains |  |  | any | 1-5 |  |
| base | Fish3 | Fish3 | Ocean |  |  | any | 1-5 |  |
| base | Fish5 | Fish5 | BlackForest |  |  | any | 1-5 |  |
| base | Fish6 | Fish6 | Swamp |  |  | any | 1-5 |  |
| base | Fish7 | Fish7 | Plains |  |  | any | 1-5 |  |
| base | Fish8 | Fish8 | Ocean |  |  | any | 1-5 |  |
| base | Fish9 | Fish9 | Mistlands |  |  | any | 1-5 |  |
| base | Fish12 | Fish12 | Ocean, Mistlands |  |  | any | 1-5 |  |
| base | Fish10 | Fish10 | DeepNorth |  |  | any | 1-5 |  |
| base | Fish11 | Fish11 | Ashlands |  |  | any | 1-5 |  |
| base | greydwarf DAY | Greydwarf | BlackForest |  |  | day | 1-3 |  |
| base | Bjorn DAY | Bjorn | BlackForest |  |  | day | 1-2 |  |
| base | Bjorn NIGHT | Bjorn | BlackForest |  |  | night | 1-3 |  |
| base | greydwarf After boss | Greydwarf | Meadows | defeated_eikthyr |  | night | 1-1 |  |
| base | greydwarf Night | Greydwarf | BlackForest |  |  | night | 1-3 |  |
| base | greydwarf ELITE | Greydwarf_Elite | BlackForest |  |  | night | 1-3 |  |
| base | Fenring | Fenring | Mountain |  |  | night | 1-1 |  |
| base | Greydwarf Elite | Greydwarf_Elite | Meadows | defeated_gdking |  | night | 1-1 |  |
| base | Greydwarf Shaman | Greydwarf_Shaman | Meadows | defeated_gdking |  | night | 1-1 |  |
| base | Skeleton | Skeleton | Meadows, Swamp, Mountain, BlackForest, Plains | defeated_bonemass |  | night | 1-1 |  |
| base | Draugr | Draugr | Meadows, Mountain, BlackForest, Plains | defeated_gdking | Misty | night | 1-1 |  |
| base | Goblin | Goblin | Meadows, Mountain, BlackForest | defeated_goblinking |  | night | 1-1 |  |
| base | Greydwarf Shaman | Greydwarf_Shaman | BlackForest |  |  | night | 1-3 |  |
| base | Greydwarf | Greydwarf | Meadows | defeated_eikthyr |  | night | 1-1 |  |
| base | Greyling | Greyling | Meadows |  |  | any | 1-1 |  |
| base | ODIN | odin | Meadows, Swamp, BlackForest, Plains | defeated_gdking |  | night | 1-1 |  |
| base | Troll | Troll | BlackForest |  |  | any | 1-3 |  |
| base | Unbjorn | Unbjorn | Plains |  |  | night | 1-1 |  |
| base | Marsh draugr | Draugr | Swamp |  |  | any | 1-3 |  |
| base | Skeleton | Skeleton_Swamps | Swamp |  |  | any | 1-3 |  |
| base | Draugr Elite | Draugr_Elite | Swamp |  |  | night | 1-1 |  |
| base | Marsh surtling | Surtling | Swamp |  |  | any | 1-1 | disabled |
| base | Blob | Blob | Swamp |  |  | any | 1-1 |  |
| base | Leech | Leech | Swamp |  |  | any | 1-3 |  |
| base | BlobElite | BlobElite | Swamp |  |  | night | 1-1 |  |
| base | Goblin | Goblin | Plains |  |  | night | 1-3 |  |
| base | Goblin | Goblin | Plains |  |  | day | 1-3 |  |
| base | GoblinBrute | GoblinBrute | Plains |  |  | any | 1-3 |  |
| base | Lox | Lox | Plains |  |  | any | 1-1 |  |
| base | Deathsquito | Deathsquito | Plains |  |  | any | 1-1 |  |
| base | Wraith | Wraith | Swamp |  |  | night | 1-1 |  |
| base | StoneGolem | StoneGolem | Mountain |  |  | any | 1-1 |  |
| base | Hatchling | Hatchling | Mountain |  |  | any | 1-1 |  |
| base | Wolf | Wolf | Mountain |  |  | night | 1-3 |  |
| base | Wolf | Wolf | Mountain |  |  | day | 1-1 |  |
| base | FireFlies | FireFlies | BlackForest |  |  | night | 1-1 |  |
| base | Surtling | Surtling | Ashlands |  |  | any | 1-1 | disabled |
| base | Serpent | Serpent | Ocean |  |  | night | 1-1 |  |
| base | Serpent | Serpent | Ocean |  | ThunderStorm,Rain | any | 1-1 |  |
| base | Abomination | Abomination | Swamp |  |  | any | 1-0 |  |
| base | Writhan | Writhan | Swamp |  |  | any | 1-3 |  |
| ashlands | Meteor | projectile_ashlandmeteor | Ashlands |  |  | any | 1-1 | disabled |
| ashlands | CinderSky CinderRain | CinderSky | Ashlands |  | Ashlands_CinderRain | any | 1-1 |  |
| ashlands | CinderSky Ashrain | CinderSky | Ashlands |  | Ashlands_ashrain | any | 1-1 |  |
| ashlands | CinderSky Storm | CinderStorm | Ashlands |  | Ashlands_storm | any | 1-1 |  |
| ashlands | Bonemaw Serpent | BonemawSerpent | Ashlands |  |  | any | 1-1 |  |
| ashlands | Fallen Valkyrie | FallenValkyrie | Ashlands |  |  | any | 1-1 |  |
| ashlands | Morgen [Non-sleeper] | Morgen_NonSleeping | Ashlands |  |  | any | 1-1 |  |
| ashlands | Asksvin [DAY] | Asksvin | Ashlands |  |  | day | 1-2 |  |
| ashlands | Asksvin [NIGHT] | Asksvin | Ashlands |  |  | night | 1-3 |  |
| ashlands | Volture | Volture | Ashlands |  |  | any | 1-1 |  |
| ashlands | Charred Twitcher [DAY] | Charred_Twitcher | Ashlands |  |  | day | 1-2 |  |
| ashlands | Charred Twitcher [NIGHT] | Charred_Twitcher | Ashlands |  |  | night | 1-3 |  |
| ashlands | Charred Archer | Charred_Archer | Ashlands |  |  | any | 1-2 |  |
| ashlands | Charred Melee | Charred_Melee | Ashlands |  |  | any | 1-3 |  |
| ashlands | Lava Blob | BlobLava | Ashlands |  |  | any | 1-1 |  |
| ashlands | Lava Rock | LavaRock | Ashlands |  |  | any | 1-1 |  |
| ashlands | Dverger | DvergerAshlands | Ashlands |  |  | day | 1-3 |  |
| ashlands | Charred Melee [Other biomes when Fader is defeated] | Charred_Melee | Meadows, Swamp, Mountain, BlackForest, Plains | defeated_fader |  | night | 1-1 |  |
| ashlands | Charred Archer [Other biomes when Fader is defeated] | Charred_Archer | Meadows, Swamp, Mountain, BlackForest, Plains | defeated_fader |  | night | 1-1 |  |
| DeepNorth | Seal | Seal | DeepNorth |  |  | any | 1-3 |  |
| DeepNorth | Shadow People | ShadowPerson | DeepNorth |  |  | night | 1-3 |  |
| DeepNorth | Frozen GD | Greydwarf_Frozen | DeepNorth |  |  | any | 1-3 |  |
| DeepNorth | Frozen GD shaman | Greydwarf_Shaman_Frozen | DeepNorth |  |  | any | 1-3 |  |
| DeepNorth | Frozen Skeleton | Skeleton_DeepNorth | DeepNorth |  |  | any | 1-3 |  |
| DeepNorth | Seal pup | Seal_Pup | DeepNorth |  |  | any | 1-1 |  |
| DeepNorth | Elakingar NIGHT | Elaking | DeepNorth |  |  | night | 1-2 |  |
| DeepNorth | Elakingar Lantern NIGHT | ElakingLantern | DeepNorth |  |  | night | 1-2 |  |
| DeepNorth | Jotun Melee Patrol | JotunWarrior | DeepNorth | jotun_killed |  | day | 1-1 |  |
| DeepNorth | Jotun Witch Patrol | JotunWitch | DeepNorth | jotun_killed |  | day | 1-1 |  |
| DeepNorth | Giant Troll | Spawner_TrollFrost | DeepNorth |  |  | any | 1-1 |  |
| DeepNorth | Barka | Barka | DeepNorth |  |  | any | 1-1 |  |
| DeepNorth | Älg | Moose | DeepNorth |  |  | any | 1-3 |  |
| DeepNorth | Fimbulvinter - Jotun Warriors | JotunWarrior | All |  |  | any | 1-1 |  |
| DeepNorth | Fimbulvinter - Jotun Witches | JotunWitch | All |  |  | any | 1-1 |  |
| DeepNorth | Fimbulvinter - Elakingar | Elaking | All |  |  | any | 1-1 |  |
| DeepNorth | Fimbulvinter - Meteors | projectile_FimbulvinterMeteor | All |  |  | any | 1-1 |  |

## 4. Crafting stations and pieces

### 4a. Notable functional pieces (by component class)

| prefab | class | token | English | build category | built with | description | detail |
|---|---|---|---|---|---|---|---|
| ArmorStand | ArmorStand | $piece_armorstand | Armour Stand | Furniture | Hammer | Some clothes are just too nice to fold away. Why not put them on display instead? |  |
| ArmorStand_Female | ArmorStand | $piece_armorstand | Armour Stand | Furniture |  |  |  |
| ArmorStand_Male | ArmorStand | $piece_armorstand | Armour Stand | Furniture |  |  |  |
| piece_barber | Barber | $piece_barber | Barber Station | Furniture | Hammer | Helps you stay up to date with the latest viking fashion. |  |
| ashwood_bed | Bed | $piece_ashwood_bed | Ashwood Bed | Furniture | Hammer | Rest easy on this finely crafted bed, with asksvin hides to insulate you from heat and co… |  |
| bed | Bed | $piece_bed | Bed | Furniture | Hammer | When night falls, you'll want somewhere to sleep. |  |
| piece_bed02 | Bed | $piece_bed02 | Dragon Bed | Furniture | Hammer | Draped in furs, this bed is sure to give you a good night's sleep. |  |
| piece_beehive | Beehive | $piece_beehive | Beehive | Crafting | Hammer | When they're happy, the bees will produce tasty honey. |  |
| piece_birdnest | Beehive | $piece_birdnest | Birds' Nest | Crafting | Hammer | Healthy and happy birds might shed a feather or two. |  |
| BossStone_Bonemass | BossStone |  |  |  |  |  |  |
| BossStone_DragonQueen | BossStone |  |  |  |  |  |  |
| BossStone_Eikthyr | BossStone |  |  |  |  |  |  |
| BossStone_Fader | BossStone |  |  |  |  |  |  |
| BossStone_TheElder | BossStone |  |  |  |  |  |  |
| BossStone_TheQueen | BossStone |  |  |  |  |  |  |
| BossStone_Yagluth | BossStone |  |  |  |  |  |  |
| StartPlatform | BossStone |  |  |  |  |  |  |
| Catapult | Catapult | $tool_catapult | Catapult | Misc | Hammer | If you can't go through it, perhaps you can go over it... |  |
| CargoCrate | Container | $ship_cargo | Cargo |  |  |  | 2x2 |
| Cart | Container | $msg_cart_storage | Storage | Misc | Hammer | Convenient when you need to move a lot of resources around. | 6x3 |
| Chest | Container | Container |  |  |  |  | 5x3 |
| Karve | Container | Storage |  | Misc | Hammer | A small and sleek ship, ready to set sail. | 2x2 |
| Morkhalla_ChestAncient | Container | $piece_morkhallachestancient | Ancient Chest | Furniture |  | The sturdy black metal that holds this chest together lets you put almost anything inside. | 8x4 |
| Player_tombstone | Container | $piece_tombstone_container | Grave |  |  |  | 8x4 |
| Sled | Container | $msg_cart_storage | Storage | Misc |  |  | 4x2 |
| Trailership | Container | Storage |  | Misc |  |  | 6x3 |
| TreasureChest_ashland_stone | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_blackforest | Container | $piece_chestwood | Chest | Furniture |  |  | 5x2 |
| TreasureChest_charredfortress | Container | $piece_charredchest | Charred Chest | Furniture |  |  | 4x2 |
| TreasureChest_deepnorth_village | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_dvergr_loose_stone | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_dvergrtower | Container | $piece_dvergrchest | Dvergr Treasure Chest | Furniture |  |  | 4x2 |
| TreasureChest_dvergrtown | Container | $piece_dvergrchest | Dvergr Treasure Chest | Furniture |  |  | 4x2 |
| TreasureChest_fCrypt | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_forestcrypt | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_forestcrypt_hildir | Container | $piece_dvergrchest | Dvergr Treasure Chest | Furniture |  |  | 4x2 |
| TreasureChest_heath | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_heath_hildir | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_meadows | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_meadows_01 | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_meadows_02 | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_meadows_buried | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_meadows_combat | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_memorial_buried | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_morkhalla | Container | $piece_jotunchest | Jotun's Chest | Furniture |  |  | 4x2 |
| TreasureChest_mountaincave | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_mountaincave_hildir | Container | $piece_dvergrchest | Dvergr Treasure Chest | Furniture |  |  | 4x2 |
| TreasureChest_mountains | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_plains_stone | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_plainsfortress_hildir | Container | $piece_dvergrchest | Dvergr Treasure Chest | Furniture |  |  | 4x2 |
| TreasureChest_sunkencrypt | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_swamp | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| TreasureChest_trollcave | Container | $piece_chestwood | Chest | Furniture |  |  | 4x2 |
| VikingShip | Container | Storage |  | Misc | Hammer | A mighty viking ship for sailing to distant shores. | 6x3 |
| VikingShip_Ashlands | Container | Storage |  | Misc | Hammer | A massive ship, sturdy enough to sail on even the most dangerous seas. | 8x4 |
| incinerator | Container | $piece_incinerator | Obliterator | Crafting | Hammer | Reduce, reuse, obliterate. | 7x3 |
| loot_chest_stone | Container | Chest |  | Furniture |  |  | 4x2 |
| loot_chest_wood | Container | Chest |  | Furniture |  |  | 5x2 |
| loot_deepNorth_Granary | Container | $piece_chestbarrel | Barrel | Furniture |  | A barrel is good for storing lots of things, including food and drink. | 6x2 |
| loot_deepNorth_TimberHall | Container | $piece_chestbarrel | Barrel | Furniture |  | A barrel is good for storing lots of things, including food and drink. | 6x2 |
| piece_chest | Container | $piece_chest | Reinforced Chest | Furniture | Hammer | A sturdier chest, with room for more storage. | 6x4 |
| piece_chest_barrel | Container | $piece_chestbarrel | Barrel | Furniture | Hammer | A barrel is good for storing lots of things, including food and drink. | 6x2 |
| piece_chest_blackmetal | Container | $piece_chestblackmetal | Black Metal Chest | Furniture | Hammer | The sturdy black metal that holds this chest together lets you put almost anything inside. | 8x4 |
| piece_chest_grausten | Container | $piece_chestgrausten | Grausten Chest | DeepNorth tab (Hammer) / Feasts (Feaster) | Hammer | Stone and metal are sure to keep your belongings safe. The charred skulls help too. | 8x5 |
| piece_chest_private | Container | $piece_chestprivate | Personal Chest | Furniture | Hammer | Some things you would rather lock away and keep for yourself. | 3x2 |
| piece_chest_warderobe | Container | $piece_chestwarderobe | Wardrobe | DeepNorth tab (Hammer) / Feasts (Feaster) | Hammer | An elegant place for storage. | 5x10 |
| piece_chest_wood | Container | $piece_chestwood | Chest | Furniture | Hammer | A good place to store any items you don't need to carry with you. | 5x2 |
| piece_gift1 | Container | $piece_yuleklapp | Yuleklapp | Furniture | Hammer | A small gift for someone you like. | 1x1 |
| piece_gift2 | Container | $piece_yuleklapp | Yuleklapp | Furniture | Hammer | A gift for someone who was nice this year. | 2x1 |
| piece_gift3 | Container | $piece_yuleklapp | Yuleklapp | Furniture | Hammer | A large gift for someone you appreciate a lot. | 3x1 |
| piece_pot1 | Container | $piece_pot_medium_green | Medium Green Pot | Furniture | Hammer | A piece of ceramic, deep enough to fit a thing or two inside. | 1x2 |
| piece_pot1_cracked | Container | $piece_pot_medium_green | Medium Green Pot | Furniture |  |  | 1x2 |
| piece_pot1_red | Container | $piece_pot_medium_red | Medium Red Pot | Furniture |  |  | 1x2 |
| piece_pot2 | Container | $piece_pot_large_green | Large Green Pot | Furniture | Hammer | A large statement piece of a pot! You could almost fit an entire viking in there. | 1x3 |
| piece_pot2_cracked | Container | $piece_pot_large_green | Large Green Pot | Furniture |  |  | 1x3 |
| piece_pot2_red | Container | $piece_pot_large_red | Large Red Pot | Furniture |  |  | 1x3 |
| piece_pot3 | Container | $piece_pot_small_green | Small Green Pot | Furniture | Hammer | A small piece of earthenware, with room for something inside. | 1x1 |
| piece_pot3_cracked | Container | $piece_pot_small_green | Small Green Pot | Furniture |  |  | 1x1 |
| piece_pot3_red | Container | $piece_pot_small_red | Small Red Pot | Furniture |  |  | 1x1 |
| shipwreck_karve_chest | Container | $piece_chestwood | Chest |  |  |  | 5x2 |
| shipwreck_vikingship_chest | Container | $piece_chestwood | Chest |  |  |  | 5x2 |
| stonechest | Container | Stone box |  |  |  |  | 6x3 |
| piece_FrostFoundry | CookingStation | $piece_frostfoundry | Frost Foundry | Crafting | Hammer | This foundry can be used to harden casts into proper items. | 30 recipes, e.g. SwordGoldUncooked->SwordGold, AtgeirGoldUncooked->AtgeirGold, BattleaxeGoldUncooked->BattleaxeGold |
| piece_cookingstation | CookingStation | $piece_cookingstation | Cooking Station | Crafting | Hammer | Perfect for grilling raw meat. | 8 recipes, e.g. RawMeat->CookedMeat, NeckTail->NeckTailGrilled, FishRaw->FishCooked |
| piece_cookingstation_iron | CookingStation | $piece_cookingstation_iron | Iron Cooking Station | Crafting | Hammer | Sturdy, with room to cook meat from larger creatures. | 15 recipes, e.g. SerpentMeat->SerpentMeatCooked, LoxMeat->CookedLoxMeat, FishRaw->FishCooked |
| piece_oven | CookingStation | $piece_oven | Stone Oven | Crafting | Hammer | For all your baking needs. | 14 recipes, e.g. LoxPieUncooked->LoxPie, BreadDough->Bread, FishAndBreadUncooked->FishAndBread |
| UpgradeStation | CraftingStation | $piece_forge | Forge |  |  |  |  |
| UpgradeStation | CraftingStation | $piece_upgradestation | Forge of Potential |  |  |  |  |
| blackforge | CraftingStation | $piece_blackforge | Black Forge | Crafting | Hammer |  |  |
| forge | CraftingStation | $piece_forge | Forge | Crafting | Hammer |  |  |
| piece_MeadCauldron | CraftingStation | $piece_meadcauldron | Mead Ketill | Crafting | Hammer |  |  |
| piece_artisanstation | CraftingStation | $piece_artisanstation | Artisan Table | Crafting | Hammer |  |  |
| piece_cauldron | CraftingStation | $piece_cauldron | Cauldron | Crafting | Hammer |  |  |
| piece_magetable | CraftingStation | $piece_magetable | Galdr Table | Crafting | Hammer |  |  |
| piece_preptable | CraftingStation | $piece_preptable | Food Preparation Table | Crafting | Hammer |  |  |
| piece_stonecutter | CraftingStation | $piece_stonecutter | Stonecutter | Crafting | Hammer |  |  |
| piece_workbench | CraftingStation | $piece_workbench | Workbench | Crafting | Hammer |  |  |
| FeastAshlands | Feast | $item_feastashlands | Ashlands Gourmet Bowl | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | It's hard to tell whether the steam coming off of this dish is because it's freshly cooke… |  |
| FeastBlackforest | Feast | $item_feastblackforest | Black Forest Buffet Platter | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | You won't be able to resist this platter of delights from the Black Forest! Venison sirlo… |  |
| FeastDeepNorth | Feast | $item_feastdeepnorth | Northern Morning Fare | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | Warming and filling, this meal will sustain you even during the coldest of days. Porridge… |  |
| FeastMeadows | Feast | $item_feastmeadows | Whole Roasted Meadow Boar | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | A boar that has been roasted to perfection, glazed and served atop a bed of greens, with… |  |
| FeastMistlands | Feast | $item_feastmistlands | Mushrooms Galore á la Mistlands | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | The time has come for mushroom enthusiasts to rejoice! Try different kinds of mushrooms,… |  |
| FeastMountains | Feast | $item_feastmountains | Hearty Mountain Logger's Stew | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | Gather around this steaming pot full of deliciousness and warm yourselves up again after… |  |
| FeastOceans | Feast | $item_feastoceans | Sailor's Bounty | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | Fish, fish, and more fish! And also serpent meat, cut to look like fish! Explore the flav… |  |
| FeastPlains | Feast | $item_feastplains | Plains Pie Picnic | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | There's nothing plain about this feast! Enjoy pies and loaves fresh from the oven, both s… |  |
| FeastSwamps | Feast | $item_feastswamps | Swamp Dweller's Delight | DeepNorth tab (Hammer) / Feasts (Feaster) | Feaster | Who knew that leeches were edible? With the correct preparation (lots of cooking and lots… |  |
| fermenter | Fermenter | $piece_fermenter | Fermenter | Crafting | Hammer | Mead needs to sit and ferment for a while, for all its intended properties to emerge. | 20 recipes, e.g. MeadBaseHealthMinor->MeadHealthMinor, MeadBaseHealthMedium->MeadHealthMedium, MeadBaseStaminaMinor->MeadStaminaMinor |
| BogWitch_Fire_Pit | Fireplace | $piece_fire | Fire | Misc |  |  |  |
| Candle_resin | Fireplace | $piece_candle | Resin Candle | Furniture | Hammer | A small but incredibly cosy lightsource. |  |
| CastleKit_groundtorch_unlit | Fireplace | $piece_groundtorchwood | Standing Wood Torch |  |  |  |  |
| Morkhalla_firepit | Fireplace | $piece_fire | Fire | Misc |  |  |  |
| bonfire | Fireplace | $piece_fire | Fire | Misc | Hammer | For when a regular campfire just isn't impressive enough! |  |
| fire_pit | Fireplace | $piece_fire | Fire | Misc | Hammer | Useful for cooking, warmth, or just resting for a bit. |  |
| fire_pit_haldor | Fireplace | $piece_fire | Fire | Misc |  |  |  |
| fire_pit_hildir | Fireplace | $piece_fire | Fire | Misc |  |  |  |
| fire_pit_iron | Fireplace | $piece_fire | Fire | Misc | Hammer | If you don't want to place a campfire directly onto the ground, this is a classy alternat… |  |
| hearth | Fireplace | $piece_fire | Fire | Misc | Hammer | A central part of any homestead. |  |
| piece_brazierceiling01 | Fireplace | $piece_fire | Fire | Furniture | Hammer | If your fire hangs from the ceiling, you can't accidentally stumble into it. |  |
| piece_brazierfloor01 | Fireplace | $piece_fire | Fire | Furniture | Hammer | A classy way to light up the room. |  |
| piece_brazierfloor02 | Fireplace | $piece_fire | Fire | Furniture | Hammer | It's hard to tell whether this flame burns incredibly hot or incredibly cold. |  |
| piece_groundtorch | Fireplace | $piece_groundtorch | Standing Iron Torch | Furniture | Hammer | A sturdy iron torch to light up your surroundings. |  |
| piece_groundtorch_blue | Fireplace | $piece_groundtorchblue | Standing Blue-burning Iron Torch | Furniture | Hammer | A sturdy iron torch, emitting an eerie blue light. |  |
| piece_groundtorch_green | Fireplace | $piece_groundtorchgreen | Standing Green-burning Iron Torch | Furniture | Hammer | A sturdy iron torch, burning with a green flame. |  |
| piece_groundtorch_wood | Fireplace | $piece_groundtorchwood | Standing Wood Torch | Furniture | Hammer | A torch to stick in the ground, for when you need your hands free. |  |
| piece_jackoturnip | Fireplace | $piece_jackoturnip | Jack-o-turnip | Furniture | Hammer | The face carved into this vegetable...is it spooky or friendly? |  |
| piece_snowlantern | Fireplace | $piece_snowlantern | Snow Lantern | DeepNorth tab (Hammer) / Feasts (Feaster) | Hammer | Adds a cosy touch to a wintry landscape. |  |
| piece_walltorch | Fireplace | $piece_sconce | Sconce | Furniture | Hammer | Why stick a torch in the ground when you can hang it on your wall? |  |
| incinerator | Incinerator | $piece_incinerator | Obliterator | Crafting | Hammer | Reduce, reuse, obliterate. | 5 input kinds -> Coal; 42 input kinds -> Coal; 14 input kinds -> Coal |
| BossStone_Bonemass | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_DragonQueen | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_Eikthyr | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_Fader | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_TheElder | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_TheQueen | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| BossStone_Yagluth | ItemStand | $guardianstone_hook_name | Trophy Hook |  |  |  |  |
| Placeable_HardRock | ItemStand | $item_hardrock | Mysterious Rock | Misc | Hammer | It's just a rock... |  |
| StartPlatform | ItemStand | $stonecircle_hook_name | Chiselled Platform |  |  |  |  |
| dragoneggcup | ItemStand | $prop_eggcup | Offering Bowl |  |  |  |  |
| fader_bellholder | ItemStand | $faderlocation_bellholder | Bell Holder |  |  |  |  |
| goblinking_totemholder | ItemStand | $prop_eggcup | Offering Bowl |  |  |  |  |
| itemstand | ItemStand | $piece_itemstand | Item Stand | Furniture | Hammer | Mount items on vertical surfaces with this item stand. |  |
| itemstandh | ItemStand | $piece_itemstand | Item Stand | Furniture | Hammer | Place items on horizontal surfaces with this item stand. |  |
| Karve | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | A small and sleek ship, ready to set sail. |  |
| Raft | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | It may not look like much, but a raft will get you farther than swimming! |  |
| Trailership | Ladder | Ladder |  | Misc |  |  |  |
| Trailership | Ladder | Ladder |  | Misc |  |  |  |
| VikingShip | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | A mighty viking ship for sailing to distant shores. |  |
| VikingShip | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | A mighty viking ship for sailing to distant shores. |  |
| VikingShip_Ashlands | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | A massive ship, sturdy enough to sail on even the most dangerous seas. |  |
| VikingShip_Ashlands | Ladder | $piece_ship_ladder | Ladder | Misc | Hammer | A massive ship, sturdy enough to sail on even the most dangerous seas. |  |
| piece_cartographytable | MapTable | $piece_cartographytable | Cartography Table | Misc | Hammer | Mark your discoveries on this map and share them with your friends. |  |
| dverger_guardstone | PrivateArea | $piece_guardstone | Ward | Misc |  | Emits a magic seal on the nearby surroundings which prevents other players from construct… |  |
| guard_stone | PrivateArea | $piece_guardstone | Ward | Misc | Hammer | Emits a magic seal on the nearby surroundings which prevents other players from construct… |  |
| guard_stone_test | PrivateArea | Guard stone |  | Misc |  |  |  |
| Eitr | Radiator |  |  |  |  |  |  |
| eitrrefinery | Radiator | $piece_eitrrefinery | Eitr Refinery | Crafting | Hammer | This machine will turn soft tissue into the magical substance eitr. The process is quite… |  |
| eitrrefinery | Radiator | $piece_eitrrefinery | Eitr Refinery | Crafting | Hammer | This machine will turn soft tissue into the magical substance eitr. The process is quite… |  |
| piece_sapcollector | SapCollector | $piece_sapcollector | Sap Extractor | Crafting | Hammer | Extract sap from mysterious branches. |  |
| charred_shieldgenerator | ShieldGenerator | $piece_shieldgenerator | Shield Generator | Misc |  |  | fuel , radius 30.0 |
| piece_shieldgenerator | ShieldGenerator | $piece_shieldgenerator | Shield Generator | Misc | Hammer | Creates a shield to protect against weather and incoming projectiles. Fuelled by bones of… | fuel BoneFragments/CharredBone, radius 30.0 |
| Karve | Ship | $ship_karve | Karve | Misc | Hammer | A small and sleek ship, ready to set sail. |  |
| Raft | Ship | $ship_raft | Raft | Misc | Hammer | It may not look like much, but a raft will get you farther than swimming! |  |
| Trailership | Ship | Longship |  | Misc |  |  |  |
| VikingShip | Ship | $ship_longship | Longship | Misc | Hammer | A mighty viking ship for sailing to distant shores. |  |
| VikingShip_Ashlands | Ship | $ship_longship_ashlands | Drakkar | Misc | Hammer | A massive ship, sturdy enough to sail on even the most dangerous seas. |  |
| BatteringRam | SiegeMachine | $tool_batteringram | Battering Ram | Misc | Hammer | This is a force to be reckoned with. Fuel it up and bring it to your enemies' stronghold… |  |
| sign | Sign | $piece_sign | Sign | Furniture | Hammer | Disorder is a survivalist's worst enemy. Defend yourself against it by putting up signs. |  |
| BatteringRam | Smelter | $tool_batteringram | Battering Ram | Misc | Hammer | This is a force to be reckoned with. Fuel it up and bring it to your enemies' stronghold… | 4 recipes, e.g. Wood->None, FineWood->None, RoundLog->None |
| blastfurnace | Smelter | $piece_blastfurnace | Blast Furnace | Crafting | Hammer | Some metals need higher temperatures to melt. | 4 recipes, e.g. FlametalOreNew->FlametalNew, BlackMetalScrap->BlackMetal, FlametalOreNew->FlametalNew |
| charcoal_kiln | Smelter | $piece_charcoalkiln | Charcoal Kiln | Crafting | Hammer | Useful for turning any kind of wood into coal. | 3 recipes, e.g. Wood->Coal, FineWood->Coal, RoundLog->Coal |
| eitrrefinery | Smelter | $piece_eitrrefinery | Eitr Refinery | Crafting | Hammer | This machine will turn soft tissue into the magical substance eitr. The process is quite… | 1 recipes, e.g. Softtissue->Eitr |
| piece_FrostKiln | Smelter | $piece_frostkiln | Frigid Kiln | Crafting | Hammer | A strange and chilling process happens within this kiln, to produce a most potent fuel... | 1 recipes, e.g. None->FrozenFuel |
| piece_bathtub | Smelter | $piece_bathtub | Hot Tub | Furniture | Hammer | Respectable vikings bathe as often as once per week! | 0 recipes, e.g. |
| piece_spinningwheel | Smelter | $piece_spinningwheel | Spinning Wheel | Crafting | Hammer | This automated machine makes spinning fibres into thread a remarkably quick process. | 1 recipes, e.g. Flax->LinenThread |
| smelter | Smelter | $piece_smelter | Smelter | Crafting | Hammer | Fuel and ore go in, beautiful metal ingots come out. | 7 recipes, e.g. CopperOre->Copper, IronOre->Iron, IronScrap->Iron |
| windmill | Smelter | $piece_windmill | Windmill | Crafting | Hammer | Turns grain into flour. Works best on windy days! | 3 recipes, e.g. Barley->BarleyFlour, OatSeeds->Oat, Oat->OatFlour |
| artisan_ext1 | StationExtension | $piece_artisan_ext1 | Artisan Press | Crafting | Hammer |  | extends piece_artisanstation |
| blackforge_ext1 | StationExtension | $piece_blackforge_ext1 | Black Forge Cooler | Crafting | Hammer |  | extends blackforge |
| blackforge_ext2_vise | StationExtension | $piece_blackforge_ext2 | Vice | Crafting | Hammer |  | extends blackforge |
| blackforge_ext3_metalcutter | StationExtension | $piece_blackforge_ext3 | Metal Cutter | Crafting | Hammer |  | extends blackforge |
| blackforge_ext4_gemcutter | StationExtension | $piece_blackforge_ext4 | Gem Cutter | Crafting | Hammer |  | extends blackforge |
| blackforge_ext5_apron | StationExtension | $piece_blackforge_ext5 | Smith's Aprons | Crafting | Hammer |  | extends blackforge |
| cauldron_ext1_spice | StationExtension | $piece_cauldron_ext1_spice | Spice Rack | Crafting | Hammer |  | extends piece_cauldron |
| cauldron_ext3_butchertable | StationExtension | $piece_cauldron_ext3_butchertable | Butcher's Table | Crafting | Hammer |  | extends piece_cauldron |
| cauldron_ext4_pots | StationExtension | $piece_cauldron_ext4_pans | Pots and Pans | Crafting | Hammer |  | extends piece_cauldron |
| cauldron_ext5_mortarandpestle | StationExtension | $piece_cauldron_ext5_mortarandpestle | Mortar and Pestle | Crafting | Hammer |  | extends piece_cauldron |
| cauldron_ext6_rollingpins | StationExtension | $piece_cauldron_ext6_rollingpins | Rolling Pins and Cutting Boards | Crafting | Hammer |  | extends piece_cauldron |
| cauldron_ext7_smoker | StationExtension | $piece_cauldron_ext7_smoker | Smoker | Crafting | Hammer |  | extends piece_cauldron |
| forge_ext1 | StationExtension | $piece_forge_ext1 | Forge Bellows | Crafting | Hammer |  | extends forge |
| forge_ext2 | StationExtension | $piece_forge_ext2 | Anvils | Crafting | Hammer |  | extends forge |
| forge_ext3 | StationExtension | $piece_forge_ext3 | Grinding Wheel | Crafting | Hammer |  | extends forge |
| forge_ext4 | StationExtension | $piece_forge_ext4 | Smith's Anvil | Crafting | Hammer |  | extends forge |
| forge_ext5 | StationExtension | $piece_forge_ext5 | Forge Cooler | Crafting | Hammer |  | extends forge |
| forge_ext6 | StationExtension | $piece_forge_ext6 | Forge Tool Rack | Crafting | Hammer |  | extends forge |
| piece_magetable_ext | StationExtension | $piece_magetable_ext | Rune Table | Crafting | Hammer |  | extends piece_magetable |
| piece_magetable_ext2 | StationExtension | $piece_magetable_ext2 | Unfading Candles | Crafting | Hammer |  | extends piece_magetable |
| piece_magetable_ext3 | StationExtension | $piece_magetable_ext3 | Feathery Wreath | Crafting | Hammer |  | extends piece_magetable |
| piece_magetable_ext4 | StationExtension | $piece_magetable_ext4 | Standing Loom | Crafting | Hammer |  | extends piece_magetable |
| piece_workbench_ext1 | StationExtension | $piece_workbench_ext1 | Chopping Block | Crafting | Hammer |  | extends piece_workbench |
| piece_workbench_ext2 | StationExtension | $piece_workbench_ext2 | Tanning Rack | Crafting | Hammer |  | extends piece_workbench |
| piece_workbench_ext3 | StationExtension | $piece_workbench_ext3 | Adze | Crafting | Hammer |  | extends piece_workbench |
| piece_workbench_ext4 | StationExtension | $piece_workbench_ext4 | Tool Shelf | Crafting | Hammer |  | extends piece_workbench |
| portal | TeleportWorld | $piece_portal | Portal | Misc |  |  |  |
| portal_stone | TeleportWorld | $piece_portal_stone | Portal – Stone | Misc | Hammer | The powerful energy source lets you pass through even with the most valuable of items. |  |
| portal_wood | TeleportWorld | $piece_portal | Portal | Misc | Hammer | Connects to another portal with equal or no tag. |  |
| fuling_trap | Trap | $piece_trap | Trap | Misc |  |  |  |
| piece_trap_troll | Trap | $piece_trap | Trap | Misc | Hammer | This trap will clamp down on whatever steps on it. Careful! |  |
| fuling_turret | Turret | $piece_turret | Ballista | Misc |  | Defensive structure that shoots missiles at anything that gets in its way. | ammo TurretBolt/TurretBoltWood |
| piece_Charred_Balista | Turret | $piece_charredballista | Skugg |  |  |  | ammo TurretBoltBone |
| piece_turret | Turret | $piece_turret | Ballista | Misc | Hammer | Defensive structure that shoots missiles at anything that gets in its way. | ammo TurretBolt/TurretBoltWood/TurretBoltFlametal/TurretBoltBloodgold |
| BatteringRam | Vagon | $tool_batteringram | Battering Ram | Misc | Hammer | This is a force to be reckoned with. Fuel it up and bring it to your enemies' stronghold… |  |
| Cart | Vagon | $tool_cart | Cart | Misc | Hammer | Convenient when you need to move a lot of resources around. |  |
| Catapult | Vagon | $tool_catapult | Catapult | Misc | Hammer | If you can't go through it, perhaps you can go over it... |  |
| Sled | Vagon | $tool_sled |  | Misc |  |  |  |
| windmill | Windmill | $piece_windmill | Windmill | Crafting | Hammer | Turns grain into flour. Works best on windy days! |  |

### 4b. All pieces (prefab roots with `Piece`)

`built with` = which tool's PieceTable lists it (Hammer, Hoe, Cultivator, Feaster/ScytheHandle ...); blank = world-only (props, dungeon walls, spawned).

| prefab | token | English | category | station | built with | cost | description |
|---|---|---|---|---|---|---|---|
| piece_remove_feaster | $hud_remove | Remove | 100 |  | Feaster |  | <color=yellow>Once a feast has been eaten of, it won't return any resources if… |
| piece_repair | $piece_repair | Repair | 100 |  | Hammer |  |  |
| Ashlands_Arch1 | $piece_stonewall1x1 | Stone Wall 1x1 | Building(Stonecutter) | piece_stonecutter |  | Stone x3 |  |
| Ashlands_Arch2 | $piece_stonewall1x1 | Stone Wall 1x1 | Building(Stonecutter) | piece_stonecutter |  | Stone x3 |  |
| Ashlands_WallBlock_1x2x2 | $piece_stonewall1x1 | Stone Wall 1x1 | Building(Stonecutter) | piece_stonecutter |  | Stone x3 |  |
| Ashlands_WallBlock_base | $piece_stonewall1x1 | Stone Wall 1x1 | Building(Stonecutter) | piece_stonecutter |  | Stone x3 |  |
| Piece_flametal_beam | $piece_flametal_beam | Flametal Beam | Building(Stonecutter) | blackforge | Hammer | FlametalNew x2 | Pure flamin' metal. |
| Piece_flametal_pillar | $piece_flametal_pillar | Flametal Pillar | Building(Stonecutter) | blackforge | Hammer | FlametalNew x2 | Pure flamin' metal. |
| Piece_grausten_floor_1x1 | $piece_grausten_floor1x1 | Grausten Floor 1x1 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x2 | These thin floor tiles are deceptively sturdy. |
| Piece_grausten_floor_2x2 | $piece_grausten_floor2x2 | Grausten Floor 2x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x4 | These thin floor tiles are deceptively sturdy. |
| Piece_grausten_floor_4x4 | $piece_grausten_floor4x4 | Grausten Floor 4x4 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x8 | These thin floor tiles are deceptively sturdy. |
| Piece_grausten_pillar_arch | $piece_grausten_archmedium | Grausten Medium Arch | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x4 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillar_arch_small | $piece_grausten_archsmall | Grausten Small Arch | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x2 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbase_medium | $piece_grausten_pillarmedium | Grausten Medium Pillar | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x3 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbase_small | $piece_grausten_pillarsmall | Grausten Small Pillar | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x1 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbase_tapered | $piece_grausten_pillartapered | Grausten Tapered Pillar | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbase_tapered_inverted | $piece_grausten_pillartaperedinverted | Grausten Tapered Pillar (Inverted) | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbeam_medium | $piece_grausten_beammedium | Grausten Medium Beam | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x3 | Strong enough to withstand a land of fire. |
| Piece_grausten_pillarbeam_small | $piece_grausten_beamsmall | Grausten Small Beam | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x1 | Strong enough to withstand a land of fire. |
| Piece_grausten_stone_ladder | $piece_grausten_stoneladder | Grausten Steep Stairs | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | Good for getting to higher levels. |
| Piece_grausten_wall_1x2 | $piece_grausten_wall1x2 | Grausten Wall 1x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x4 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_wall_2x2 | $piece_grausten_wall2x2 | Grausten Wall 2x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x6 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_wall_4x2 | $piece_grausten_wall4x2 | Grausten Wall 4x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x12 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_wall_arch | $piece_grausten_wallarch | Grausten Wall Arch | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x2 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_wall_arch_inverted | $piece_grausten_wallarchinv | Grausten Wall Arch (Inverted) | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x2 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_window_2x2 | $piece_grausten_window2x2 | Grausten Window 2x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x4 | Stone walls inspired by a long-gone civilisation. |
| Piece_grausten_window_4x2 | $piece_grausten_window4x2 | Grausten Window 4x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x10 | Stone walls inspired by a long-gone civilisation. |
| blackmarble_1x1 | $piece_blackmarble1x1 | Black Marble 1x1x1 | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x2 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_2x1x1 | $piece_blackmarble2x1x1 | Black Marble 2x1x1 | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x4 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_2x2_enforced | $piece_blackmarble2x2_enforced | Enforced Black Marble | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1, CopperScrap x1 |  |
| blackmarble_2x2x1 | $piece_blackmarble2x2x1 | Black Marble 2x2x1 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x6 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_2x2x2 | $piece_blackmarble2x2x2 | Black Marble 2x2x2 | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x8 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_arch | $piece_blackmarble_arch | Black Marble Arch | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x5 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_base_1 | $piece_blackmarble_base1 | Black Marble Plinth | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x5 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_base_2 | $piece_blackmarble_base2 | Black Marble Wide Plinth | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_basecorner | $piece_blackmarble_basecorner | Black Marble Plinth Corner | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x6 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_column_1 | $piece_blackmarble_column_1 | Black Marble Column Small | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x2 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_column_2 | $piece_blackmarble_column_2 | Black Marble Column Wide | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x4 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_column_3 | $piece_blackmarble_column_3 | Black Marble Column Tall | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_creep_4x1x1 | $piece_blackmarble2x2x1 | Black Marble 2x2x1 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_creep_4x2x1 | $piece_blackmarble2x2x1 | Black Marble 2x2x1 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_creep_slope_inverted_1x1x2 | $piece_blackmarble_out1 | Black Marble Cornice | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_creep_slope_inverted_2x2x1 | $piece_blackmarble_out1 | Black Marble Cornice | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_creep_stair | $piece_blackmarble_stair | Black Marble Stair | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 | Can be used for going both up and down. How convenient! |
| blackmarble_floor | $piece_blackmarble_floor | Black Marble Floor | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x4 | Dark floors prevent stains. |
| blackmarble_floor_large | $piece_blackmarble_floor4x4 | Black Marble Floor 4x4 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x8 |  |
| blackmarble_floor_triangle | $piece_blackmarble_floor_triangle | Black Marble Floor Triangle | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x3 | Dark floors prevent stains. |
| blackmarble_head01 | $piece_blackmarble_bronze_head1 | Bronze Head 1 | Building(Stonecutter) | piece_stonecutter |  | CopperScrap x2 |  |
| blackmarble_head02 | $piece_blackmarble_bronze_head2 | Bronze Head 2 | Building(Stonecutter) | piece_stonecutter |  | CopperScrap x2 |  |
| blackmarble_head_big01 | $piece_blackmarble_head_big1 | Black Marble Large Head 1 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_head_big02 | $piece_blackmarble_head_big2 | Black Marble Large Head 2 | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_out_1 | $piece_blackmarble_out1 | Black Marble Cornice | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x5 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_out_2 | $piece_blackmarble_out2 | Black Marble Cornice Wide | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_outcorner | $piece_blackmarble_outcorner | Black Marble Cornice Corner | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x6 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| blackmarble_slope_1x2 | $piece_blackmarble_slope1x2 | Black Marble Slope | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_slope_inverted_1x2 | $piece_blackmarble_inverted_slope1x2 | Black Marble Slope (Inverted) | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x1 |  |
| blackmarble_stair | $piece_blackmarble_stair | Black Marble Stair | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x8 | Can be used for going both up and down. How convenient! |
| blackmarble_stair_corner | $piece_blackmarble_stair_corner_right | Black Marble Stair Right | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_stair_corner_left | $piece_blackmarble_stair_corner_left | Black Marble Stair Left | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tile_floor_1x1 | $piece_blackmarble_tile_floor_1x1 | Black Marble Floor Tile Small | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tile_floor_2x2 | $piece_blackmarble_tile_floor_2x2 | Black Marble Floor Tile Large | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tile_wall_1x1 | $piece_blackmarble_tile_wall_1x1 | Black Marble Wall Tile Small | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tile_wall_2x2 | $piece_blackmarble_tile_wall_2x2 | Black Marble Wall Tile Large | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tile_wall_2x4 | $piece_blackmarble_tile_wall_2x4 | Black Marble Wall Tile Tall | Building(Stonecutter) | piece_stonecutter |  | BlackMarble x2 |  |
| blackmarble_tip | $piece_blackmarble_tip | Black Marble Quarter Spire | Building(Stonecutter) | piece_stonecutter | Hammer | BlackMarble x2 | A black marble piece inspired by dwarven architecture. The only limit is your o… |
| crystal_wall_1x1 | $piece_crystalwall1x1 | Crystal Wall 1x1 | Building(Stonecutter) | piece_workbench | Hammer | Crystal x2 | Beautiful and fragile. |
| flametal_gate | $piece_flametalgate | Flametal Gate | Building(Stonecutter) | blackforge | Hammer | FlametalNew x16 | An impressive way to welcome your friends, or to ward off your foes. |
| iron_floor_1x1 | $piece_ironfloorSmall | Cage Floor 1x1 | Building(Stonecutter) | forge |  | Iron x1 | Finely crafted iron bars, sure to be rattle-proof! |
| iron_floor_1x1_v2 | $piece_ironfloorSmall | Cage Floor 1x1 | Building(Stonecutter) | forge | Hammer | Iron x1 | Finely crafted iron bars, sure to be rattle-proof! |
| iron_floor_2x2 | $piece_ironfloor | Cage Floor 2x2 | Building(Stonecutter) | forge | Hammer | Iron x2 | Finely crafted iron bars, sure to be rattle-proof! |
| iron_grate | $piece_irongate | Iron Gate | Building(Stonecutter) | forge | Hammer | Iron x4 | A very important gate. |
| iron_wall_1x1 | $piece_ironwallSmall | Cage Wall 1x1 | Building(Stonecutter) | forge | Hammer | Iron x1 | Finely crafted iron bars, sure to be rattle-proof! |
| iron_wall_2x2 | $piece_ironwall | Cage Wall 2x2 | Building(Stonecutter) | forge | Hammer | Iron x2 | Finely crafted iron bars, sure to be rattle-proof! |
| piece_dvergr_metal_wall_2x2 | $piece_dvergr_metal_wall | Dvergr Metal Wall | Building(Stonecutter) | blackforge | Hammer | Copper x2 | Dvergr design is all the rage these days. |
| piece_grausten_roof_45 | $piece_grausten_roof45 | Grausten Roof | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_roof_45_arch | $piece_grausten_roof45_arch | Grausten Arched Roof | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_roof_45_arch_corner | $piece_grausten_roof45_archcorner | Grausten Arched Roof Corner | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_roof_45_arch_corner2 | $piece_grausten_roof45_archcorner2 | Grausten Arched Roof Corner | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_roof_45_corner | $piece_grausten_roof45_corner | Grausten Roof Corner | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_roof_45_corner2 | $piece_grausten_roof45_corner2 | Grausten Roof Corner | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x5 | A roof design fit for the mightiest of halls. |
| piece_grausten_stonestair | $piece_grausten_stair | Grausten Stairs | Building(Stonecutter) | piece_stonecutter | Hammer | Grausten x8 | Just take one step after the other. |
| piece_icecube | $piece_icecube | Ice Block | Building(Stonecutter) | piece_workbench | Hammer | Ice x2 | A solid chunk of ice. |
| stone_arch | $piece_stonearch | Stone Arch | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x4 | Stable as a rock. |
| stone_floor | $piece_stonefloor4x4 | Stone Floor 4x4 | Building(Stonecutter) | piece_stonecutter |  | Stone x8 |  |
| stone_floor_2x2 | $piece_stonefloor2x2 | Stone Floor 2x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x6 | Best to put a rug on it, or it might get cold! |
| stone_pillar | $piece_stonepillar | Stone Pillar | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x5 | Stable as a rock. |
| stone_stair | $piece_stonestair | Stone Stair | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x8 | Useful for getting around in a large fortress. |
| stone_wall_1x1 | $piece_stonewall1x1 | Stone Wall 1x1 | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x3 | A roughly hewn block of stone. |
| stone_wall_2x1 | $piece_stonewall2x1 | Stone Wall 2x1 | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x4 | A roughly hewn block of stone. |
| stone_wall_4x2 | $piece_stonewall4x2 | Stone Wall 4x2 | Building(Stonecutter) | piece_stonecutter | Hammer | Stone x6 | A roughly hewn block of stone. |
| OLD_wood_roof | Wood roof |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| OLD_wood_roof_icorner | Wood roof icorner |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| OLD_wood_roof_ocorner | Wood roof ocorner |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| OLD_wood_roof_top | Wood roof ridge |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| OLD_wood_wall_roof | Wood wall roof |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| ashwood_arch_big | $piece_ashwoodarch_big | Ashwood Arch | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_arch_bottom | $piece_ashwoodarch_bottom | Ashwood Bottom Arch | Building(Workbench) | piece_workbench |  | Blackwood x2 |  |
| ashwood_arch_top | $piece_ashwoodarch_top | Ashwood Top Arch | Building(Workbench) | piece_workbench |  | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_beam_1m | $piece_ashwood_beam_1m | Ashwood Beam 1 m | Building(Workbench) | piece_workbench | Hammer | Blackwood x1 | These supports are always warm to the touch. |
| ashwood_beam_2m | $piece_ashwood_beam_2m | Ashwood Beam 2 m | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_deco_floor | $piece_ashwood_floor_deco | Ashwood Decorative Floor | Building(Workbench) | piece_workbench | Hammer | Blackwood x4 | Patterns work great on the floor as well. |
| ashwood_decowall_2x2 | $piece_ashwood_decowall | Ashwood Decorative Wall | Building(Workbench) | piece_workbench | Hammer | Blackwood x4 | You could watch this pattern for hours... |
| ashwood_decowall_divider | $piece_ashwood_decowall_divider | Ashwood Divider | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Adds a nice detail to any room. |
| ashwood_decowall_tree | $piece_ashwood_decowall_tree | Ashwood Decorative Window | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Are you looking out the window or at the window? Nobody needs to know. |
| ashwood_door | $piece_ashwood_door | Ashwood Door | Building(Workbench) | piece_workbench | Hammer | FlametalNew x1, Blackwood x5 | The detailed decorations might make you stop and look instead of actually going… |
| ashwood_floor_1x1 | $piece_ashwood_floor_1x1 | Ashwood Floor 1x1 | Building(Workbench) | piece_workbench | Hammer | Blackwood x1 | Ashwood floorboards, laid diagonally. |
| ashwood_floor_2x2 | $piece_ashwood_floor_2x2 | Ashwood Floor 2x2 | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Ashwood floorboards, laid diagonally. |
| ashwood_halfwall_1x2 | $piece_ashwood_halfwall | Ashwood Half Wall | Building(Workbench) | piece_workbench | Hammer | Blackwood x1 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_pole_1m | $piece_ashwood_pole_1m | Ashwood Pole 1 m | Building(Workbench) | piece_workbench | Hammer | Blackwood x1 | These supports are always warm to the touch. |
| ashwood_pole_2m | $piece_ashwood_pole_2m | Ashwood Pole 2 m | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_quarterwall_1x1 | $piece_ashwood_quarterwall | Ashwood Quarter Wall | Building(Workbench) | piece_workbench | Hammer | Blackwood x1 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_stair | $piece_ashwoodstair | Ashwood Stair | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These stairs are sturdier than they might appear. |
| ashwood_wall_2x2 | $piece_ashwood_wall | Ashwood Wall | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_arch | $piece_ashwood_archedwall | Ashwood Arched Wall | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_beam_26 | $piece_ashwoodbeam26 | Ashwood Beam 26° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_beam_26_alt | $piece_ashwoodbeam26 | Ashwood Beam 26° | Building(Workbench) | piece_workbench |  | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_beam_45 | $piece_ashwoodbeam45 | Ashwood Beam 45° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_beam_45_alt | $piece_ashwoodbeam45 | Ashwood Beam 45° | Building(Workbench) | piece_workbench |  | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_26 | $piece_ashwoodcross26 | Ashwood Roof Cross 26° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_26_alt | $piece_ashwoodcross26 | Ashwood Roof Cross 26° | Building(Workbench) | piece_workbench |  | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_45 | $piece_ashwoodcross45 | Ashwood Roof Cross 45° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_45_alt | $piece_ashwoodcross45 | Ashwood Roof Cross 45° | Building(Workbench) | piece_workbench |  | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_roof_26 | $piece_ashwoodwallroof_26 | Ashwood Wall 26° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_roof_26_upsidedown | $piece_ashwoodwallroof_26_upsidedown | Ashwood Wall 26° (Inverted) | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_roof_45 | $piece_ashwoodwallroof_45 | Ashwood Wall 45° | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_roof_45_upsidedown | $piece_ashwoodwallroof_45_upsidedown | Ashwood Wall 45° (Inverted) | Building(Workbench) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| darkwood_arch | $piece_darkwoodarch | Darkwood Arch | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_beam | $piece_darkwoodbeam | Darkwood Beam 2 m | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_beam4x4 | $piece_darkwoodbeam4 | Darkwood Beam 4 m | Building(Workbench) | piece_workbench | Hammer | Wood x4, Tar x1 | Intricate designs run along this support structure. |
| darkwood_beam_26 | $piece_darkwoodbeam_26 | Darkwood Beam 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_beam_45 | $piece_darkwoodbeam_45 | Darkwood Beam 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_decowall | $piece_darkwooddecowall | Carved Darkwood Divider | Building(Workbench) | piece_workbench | Hammer | FineWood x2, Tar x1 | An elegant design for decorating your home, and for keeping prying eyes away. |
| darkwood_gate | $piece_darkwoodgate | Darkwood Gate | Building(Workbench) | forge | Hammer | Wood x16, Iron x4, Tar x2 | A fancy and sturdy gate. To use by itself or as part of a pair. |
| darkwood_pole | $piece_darkwoodpole | Darkwood Pole 2m | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_pole4 | $piece_darkwoodpole4 | Darkwood Pole 4m | Building(Workbench) | piece_workbench | Hammer | Wood x4, Tar x1 | Intricate designs run along this support structure. |
| darkwood_roof | $piece_darkwoodroof26 | Shingle Roof 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This shallow roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_45 | $piece_darkwoodroof45 | Shingle Roof 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This steeper roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_icorner | $piece_darkwoodrooficorner | Shingle Roof Inner Corner 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This shallow roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_icorner_45 | $piece_darkwoodrooficorner45 | Shingle Roof Inner Corner 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This steeper roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_ocorner | $piece_darkwoodroofocorner | Shingle Roof Outer Corner 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This shallow roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_ocorner_45 | $piece_darkwoodroofocorner45 | Shingle Roof Outer Corner 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This steeper roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_top | $piece_darkwoodrooftop | Shingle Roof Ridge 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This shallow roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_top_45 | $piece_darkwoodrooftop45 | Shingle Roof Ridge 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2, Tar x1 | This steeper roof is sure to keep the rain out, and look good doing it. |
| dvergrprops_wood_floor | $piece_woodfloor2x2 | Wood Floor 2x2 | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| dvergrprops_wood_stair | $piece_woodstair | Wood Stairs | Building(Workbench) | piece_workbench |  | YggdrasilWood x4 |  |
| iron_wall_1x1_rusty | $piece_ironwallSmall | Cage Wall 1x1 | Building(Workbench) | forge |  | Iron x1 |  |
| metalbar_1x2 | $piece_blackmarble1x1 | Black Marble 1x1x1 | Building(Workbench) | piece_stonecutter |  | BlackMarble x1 |  |
| piece_drawbridge | $piece_drawbridge_dn | Timberwood Drawbridge | Building(Workbench) | piece_workbench | Hammer | FlametalNew x2, Gold x2, Frostwood x24 | Both a bridge and a gate, how clever! |
| piece_drawbridge_log | $piece_drawbridge_log | Rustic Drawbridge | Building(Workbench) | piece_workbench | Hammer | Silver x2, TrophyWolf x2, RoundLog x10 | Convenient for crossing chasms, or for keeping enemies out. |
| piece_dvergr_pole | $piece_dvergr_pole | Dvergr Pole | Building(Workbench) | blackforge |  | YggdrasilWood x4, Copper x2 |  |
| piece_dvergr_spiralstair | $piece_dvergr_spiralstair | Dvergr Spiral Staircase Left | Building(Workbench) | blackforge | Hammer | YggdrasilWood x5, Copper x2 | Up and around it goes! |
| piece_dvergr_spiralstair_right | $piece_dvergr_spiralstair_right | Dvergr Spiral Staircase Right | Building(Workbench) | blackforge | Hammer | YggdrasilWood x5, Copper x2 | Up and around it goes! |
| piece_dvergr_wood_door | $piece_dvergr_door | Dvergr Door | Building(Workbench) | blackforge |  | YggdrasilWood x8, Copper x8 |  |
| piece_dvergr_wood_wall | $piece_dvergr_woodwall | Dvergr Wall | Building(Workbench) | blackforge |  | YggdrasilWood x20, Copper x10 |  |
| piece_hexagonal_door | $piece_hexagonalgate | Hexagonal Gate | Building(Workbench) | blackforge | Hammer | YggdrasilWood x8, Copper x8 | This dwarven design is excellent for keeping unwanted guests out. |
| siege_wall_1x1 | Temp Siege Wall |  | Building(Workbench) | piece_stonecutter |  | Stone x3 |  |
| stave_gate | $piece_stavegate | Timberwood Gate | Building(Workbench) | forge | Hammer | Frostwood x24, Gold x4 | Not quite as glorious as the gates of Valhalla, but awfully close! |
| turf_roof | Wood roof |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| turf_roof_top | Wood roof ridge |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| turf_roof_wall | Wood wall roof |  | Building(Workbench) | piece_workbench |  | Wood x2 |  |
| wood_beam | $piece_woodbeam2 | Wood Beam 2 m | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_beam_1 | $piece_woodbeam1 | Wood Beam 1 m | Building(Workbench) | piece_workbench | Hammer | Wood x1 | A sturdy wooden support. |
| wood_beam_26 | $piece_woodbeam26 | Wood Beam 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_beam_45 | $piece_woodbeam45 | Wood Beam 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_door | $piece_wooddoor | Wood Door | Building(Workbench) | piece_workbench | Hammer | Wood x4 | A simple door for opening and closing. |
| wood_floor | $piece_woodfloor2x2 | Wood Floor 2x2 | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A solid bit of wooden flooring. |
| wood_floor_1x1 | $piece_woodfloor1x1 | Wood Floor 1x1 | Building(Workbench) | piece_workbench | Hammer | Wood x1 | A solid bit of wooden flooring. |
| wood_gate | $piece_woodgate | Wood Gate | Building(Workbench) | piece_workbench | Hammer | Wood x12 | A simple wooden gate. To use by itself or as part of a pair. |
| wood_ledge | $piece_woodledge | Wood Ledge | Building(Workbench) | piece_workbench |  | Wood x1 |  |
| wood_log_26 | $piece_woodlog26 | Log Beam 26° | Building(Workbench) | piece_workbench | Hammer | RoundLog x2 | Support your constructions in this rustic style. |
| wood_log_45 | $piece_woodlog45 | Log Beam 45° | Building(Workbench) | piece_workbench | Hammer | RoundLog x2 | Support your constructions in this rustic style. |
| wood_log_67 | $piece_woodlog67 | Log Beam 67° | Building(Workbench) | piece_workbench | Hammer | RoundLog x2 | Support your constructions in this rustic style. |
| wood_pole | $piece_woodpole | Wood Pole 1 m | Building(Workbench) | piece_workbench | Hammer | Wood x1 | A sturdy wooden support. |
| wood_pole2 | $piece_woodpole2 | Wood Pole 2 m | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_pole_log | $piece_logpole2 | Log Pole 2 m | Building(Workbench) | piece_workbench | Hammer | RoundLog x1 | Support your constructions in this rustic style. |
| wood_pole_log_4 | $piece_logpole4 | Log Pole 4 m | Building(Workbench) | piece_workbench | Hammer | RoundLog x2 | Support your constructions in this rustic style. |
| wood_pole_log_4_worn | $piece_logpole4 | Log Pole 4 m | Building(Workbench) | piece_workbench |  | RoundLog x2 | Support your constructions in this rustic style. |
| wood_roof | $piece_woodroof26 | Thatch Roof 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a shallow angle. |
| wood_roof_45 | $piece_woodroof45 | Thatch Roof 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, with a steeper angle. |
| wood_roof_icorner | $piece_woodrooficorner | Thatch Roof Inner Corner 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a shallow angle. |
| wood_roof_icorner_45 | $piece_woodrooficorner45 | Thatch Roof Inner Corner 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, with a steeper angle. |
| wood_roof_ocorner | $piece_woodroofocorner | Thatch Roof Outer Corner 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a shallow angle. |
| wood_roof_ocorner_45 | $piece_woodroofocorner45 | Thatch Roof Outer Corner 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, with a steeper angle. |
| wood_roof_top | $piece_woodrooftop | Thatch Roof Ridge 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a shallow angle. |
| wood_roof_top_45 | $piece_woodrooftop45 | Thatch Roof Ridge 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, with a steeper angle. |
| wood_stair | $piece_woodstair | Wood Stairs | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Good for getting off the ground. |
| wood_stepladder | $piece_woodstepladder | Wood Ladder | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Convenient for climbing higher. |
| wood_wall_half | $piece_woodwallhalf | Wood Wall Half | Building(Workbench) | piece_workbench | Hammer | Wood x1 | Walls are an important part of any house! |
| wood_wall_log | $piece_logbeam2 | Log Beam 2 m | Building(Workbench) | piece_workbench | Hammer | RoundLog x1 | Support your constructions in this rustic style. |
| wood_wall_log_4x0.5 | $piece_logbeam4 | Log Beam 4 m | Building(Workbench) | piece_workbench | Hammer | RoundLog x2 | Support your constructions in this rustic style. |
| wood_wall_quarter | $piece_woodwallquarter | Wood Wall 1x1 | Building(Workbench) | piece_workbench | Hammer | Wood x1 | Walls are an important part of any house! |
| wood_wall_roof | $piece_woodwallroof | Wood Wall 26° | Building(Workbench) | piece_workbench |  | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_45 | $piece_woodwallroof45 | Wood Wall 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_45_upsidedown | $piece_woodwallroof45_upsidedown | Wood Wall 45° (Inverted) | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_a | $piece_woodwallroof | Wood Wall 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_top | $piece_woodwallrooftop | Wood Roof Cross 26° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_wall_roof_top_45 | $piece_woodwallrooftop45 | Wood Roof Cross 45° | Building(Workbench) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_wall_roof_upsidedown | $piece_woodwallroof_upsidedown | Wood Wall 26° (Inverted) | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_window | $piece_woodwindowshutter | Wood Shutter | Building(Workbench) | piece_workbench | Hammer | Wood x4, BronzeNails x2 | Open or closed, keep it the way you prefer or switch things up whenever you fee… |
| woodiron_beam | $piece_woodironbeam | Wood Iron Beam | Building(Workbench) | forge | Hammer | Wood x2, Iron x1 | The strongest support you've ever built. |
| woodiron_beam_26 | $piece_woodironbeam_26 | Wood Iron Beam 26° | Building(Workbench) | forge | Hammer | Wood x2, Iron x1 | The strongest support you've ever built. |
| woodiron_beam_45 | $piece_woodironbeam_45 | Wood Iron Beam 45° | Building(Workbench) | forge | Hammer | Wood x2, Iron x1 | The strongest support you've ever built. |
| woodiron_beam_67 | $piece_ironwoodbeam67 | Wood Iron Beam 67° | Building(Workbench) | forge | Hammer | Wood x2, Iron x1 | The strongest support you've ever built. |
| woodiron_pole | $piece_woodironpole | Wood Iron Pole | Building(Workbench) | forge | Hammer | Wood x2, Iron x1 | The strongest support you've ever built. |
| woodwall | $piece_woodwall | Wood Wall | Building(Workbench) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| artisan_ext1 | $piece_artisan_ext1 | Artisan Press | Crafting | piece_artisanstation | Hammer | BlackMarble x5, Bronze x5, QueenDrop x1 |  |
| blackforge | $piece_blackforge | Black Forge | Crafting | piece_workbench | Hammer | BlackMarble x10, YggdrasilWood x10, BlackCore x5 |  |
| blackforge_ext1 | $piece_blackforge_ext1 | Black Forge Cooler | Crafting | blackforge | Hammer | Iron x5, Copper x5, BlackMarble x4 |  |
| blackforge_ext2_vise | $piece_blackforge_ext2 | Vice | Crafting | blackforge | Hammer | Iron x5, Copper x8, MechanicalSpring x2 |  |
| blackforge_ext3_metalcutter | $piece_blackforge_ext3 | Metal Cutter | Crafting | blackforge | Hammer | BlackMarble x5, FlametalNew x5, Blackwood x5, CharredBone x4 |  |
| blackforge_ext4_gemcutter | $piece_blackforge_ext4 | Gem Cutter | Crafting | blackforge | Hammer | FlametalNew x5, Blackwood x8, MorgenSinew x2, GemstoneRed x1 |  |
| blackforge_ext5_apron | $piece_blackforge_ext5 | Smith's Aprons | Crafting | blackforge | Hammer | Gold x5, Frostwood x8, MooseHide x2 |  |
| blastfurnace | $piece_blastfurnace | Blast Furnace | Crafting | piece_artisanstation | Hammer | Stone x20, SurtlingCore x5, Iron x10, FineWood x20 | Some metals need higher temperatures to melt. |
| cauldron_ext1_spice | $piece_cauldron_ext1_spice | Spice Rack | Crafting | piece_workbench | Hammer | Dandelion x3, Carrot x2, Mushroom x5, Thistle x3, Turnip x3 |  |
| cauldron_ext3_butchertable | $piece_cauldron_ext3_butchertable | Butcher's Table | Crafting | piece_workbench | Hammer | ElderBark x2, RoundLog x4, FineWood x4, Silver x2 |  |
| cauldron_ext4_pots | $piece_cauldron_ext4_pans | Pots and Pans | Crafting | piece_workbench | Hammer | Iron x5, Copper x5, BlackMetal x5, FineWood x10 |  |
| cauldron_ext5_mortarandpestle | $piece_cauldron_ext5_mortarandpestle | Mortar and Pestle | Crafting | piece_workbench | Hammer | BlackMarble x8, FineWood x6, RoundLog x4 |  |
| cauldron_ext6_rollingpins | $piece_cauldron_ext6_rollingpins | Rolling Pins and Cutting Boards | Crafting | piece_workbench | Hammer | Blackwood x8, FineWood x6, FlametalNew x4 |  |
| cauldron_ext7_smoker | $piece_cauldron_ext7_smoker | Smoker | Crafting | piece_workbench | Hammer | Gold x5, Frostwood x6 |  |
| charcoal_kiln | $piece_charcoalkiln | Charcoal Kiln | Crafting | piece_workbench | Hammer | Stone x20, SurtlingCore x5 | Useful for turning any kind of wood into coal. |
| eitrrefinery | $piece_eitrrefinery | Eitr Refinery | Crafting | piece_workbench | Hammer | BlackMarble x20, BlackMetal x5, YggdrasilWood x10, BlackCore x5, Sap x3 | This machine will turn soft tissue into the magical substance eitr. The process… |
| fermenter | $piece_fermenter | Fermenter | Crafting | forge | Hammer | FineWood x30, Bronze x5, Resin x10 | Mead needs to sit and ferment for a while, for all its intended properties to e… |
| forge | $piece_forge | Forge | Crafting | piece_workbench | Hammer | Stone x4, Coal x4, Wood x10, Copper x6 |  |
| forge_ext1 | $piece_forge_ext1 | Forge Bellows | Crafting | piece_workbench | Hammer | Wood x5, DeerHide x5, Chain x4 |  |
| forge_ext2 | $piece_forge_ext2 | Anvils | Crafting | piece_workbench | Hammer | Wood x5, Bronze x2 |  |
| forge_ext3 | $piece_forge_ext3 | Grinding Wheel | Crafting | piece_workbench | Hammer | Wood x25, SharpeningStone x1 |  |
| forge_ext4 | $piece_forge_ext4 | Smith's Anvil | Crafting | piece_workbench | Hammer | Iron x20, Wood x5 |  |
| forge_ext5 | $piece_forge_ext5 | Forge Cooler | Crafting | piece_workbench | Hammer | FineWood x25, Copper x10 |  |
| forge_ext6 | $piece_forge_ext6 | Forge Tool Rack | Crafting | piece_workbench | Hammer | Iron x15, Wood x10 |  |
| incinerator | $piece_incinerator | Obliterator | Crafting | forge | Hammer | Iron x8, Copper x4, Thunderstone x1 | Reduce, reuse, obliterate. |
| piece_EternalPyre | $piece_faderember | Eternal Pyre | Crafting | piece_stonecutter | Hammer | Stone x10, FaderDrop x1 | His rage and regret are at war with each other, and their struggle will fuel th… |
| piece_FaderEmbers | $piece_faderember | Eternal Pyre | Crafting | piece_stonecutter |  | Stone x10, FaderDrop x1 | His rage and regret are at war with each other, and their struggle will fuel th… |
| piece_FrostFoundry | $piece_frostfoundry | Frost Foundry | Crafting | piece_workbench | Hammer | Iron x15, Stone x20, FrostCore x10 | This foundry can be used to harden casts into proper items. |
| piece_FrostKiln | $piece_frostkiln | Frigid Kiln | Crafting | piece_workbench | Hammer | Stone x20, FrostCore x10, Ice x5 | A strange and chilling process happens within this kiln, to produce a most pote… |
| piece_MeadCauldron | $piece_meadcauldron | Mead Ketill | Crafting | forge | Hammer | Tin x4, Copper x6, LeatherScraps x2 |  |
| piece_artisanstation | $piece_artisanstation | Artisan Table | Crafting |  | Hammer | Wood x10, DragonTear x2 |  |
| piece_beehive | $piece_beehive | Beehive | Crafting | piece_workbench | Hammer | Wood x10, QueenBee x1 | When they're happy, the bees will produce tasty honey. |
| piece_birdnest | $piece_birdnest | Birds' Nest | Crafting | piece_workbench | Hammer | WrithanRoots x2, Acorn x3, Thistle x5 | Healthy and happy birds might shed a feather or two. |
| piece_cauldron | $piece_cauldron | Cauldron | Crafting | forge | Hammer | Tin x10 |  |
| piece_cookingstation | $piece_cookingstation | Cooking Station | Crafting |  | Hammer | Wood x2 | Perfect for grilling raw meat. |
| piece_cookingstation_iron | $piece_cookingstation_iron | Iron Cooking Station | Crafting | forge | Hammer | Iron x3, Chain x3 | Sturdy, with room to cook meat from larger creatures. |
| piece_magetable | $piece_magetable | Galdr Table | Crafting | piece_workbench | Hammer | YggdrasilWood x20, BlackMetal x10, BlackCore x5, Eitr x5 |  |
| piece_magetable_ext | $piece_magetable_ext | Rune Table | Crafting | piece_magetable | Hammer | BlackMarble x10, YggdrasilWood x5, Eitr x10 |  |
| piece_magetable_ext2 | $piece_magetable_ext2 | Unfading Candles | Crafting | piece_magetable | Hammer | BlackMarble x10, TrophySkeleton x3, Eitr x10, Resin x15 |  |
| piece_magetable_ext3 | $piece_magetable_ext3 | Feathery Wreath | Crafting | piece_magetable | Hammer | CelestialFeather x8, TrophyAsksvin x1, Eitr x10, Blackwood x3 |  |
| piece_magetable_ext4 | $piece_magetable_ext4 | Standing Loom | Crafting | piece_magetable | Hammer | Frostwood x5, NornThread x10 |  |
| piece_oven | $piece_oven | Stone Oven | Crafting | piece_artisanstation | Hammer | Iron x15, Stone x20, SurtlingCore x4 | For all your baking needs. |
| piece_preptable | $piece_preptable | Food Preparation Table | Crafting | piece_workbench | Hammer | Iron x5, FineWood x20, LeatherScraps x15 |  |
| piece_sapcollector | $piece_sapcollector | Sap Extractor | Crafting | piece_workbench | Hammer | YggdrasilWood x10, BlackMetal x5, DvergrNeedle x1 | Extract sap from mysterious branches. |
| piece_spinningwheel | $piece_spinningwheel | Spinning Wheel | Crafting | piece_artisanstation | Hammer | FineWood x20, IronNails x10, LeatherScraps x5 | This automated machine makes spinning fibres into thread a remarkably quick pro… |
| piece_stonecutter | $piece_stonecutter | Stonecutter | Crafting | piece_workbench | Hammer | Wood x10, Iron x2, Stone x4 |  |
| piece_wisplure | $piece_wisplure | Wisp Fountain | Crafting | piece_stonecutter | Hammer | Stone x10, YagluthDrop x1 | Attracts wisps. They mostly come at night... mostly. |
| piece_workbench | $piece_workbench | Workbench | Crafting |  | Hammer | Wood x10 |  |
| piece_workbench_ext1 | $piece_workbench_ext1 | Chopping Block | Crafting | piece_workbench | Hammer | Wood x10, Flint x10 |  |
| piece_workbench_ext2 | $piece_workbench_ext2 | Tanning Rack | Crafting | piece_workbench | Hammer | Wood x10, Flint x15, LeatherScraps x20, DeerHide x5 |  |
| piece_workbench_ext3 | $piece_workbench_ext3 | Adze | Crafting | forge | Hammer | FineWood x10, Bronze x3 |  |
| piece_workbench_ext4 | $piece_workbench_ext4 | Tool Shelf | Crafting | forge | Hammer | Iron x4, FineWood x10, Obsidian x4 |  |
| smelter | $piece_smelter | Smelter | Crafting | piece_workbench | Hammer | Stone x20, SurtlingCore x5 | Fuel and ore go in, beautiful metal ingots come out. |
| windmill | $piece_windmill | Windmill | Crafting | piece_artisanstation | Hammer | Stone x20, Wood x30, IronNails x30 | Turns grain into flour. Works best on windy days! |
| FeastAshlands | $item_feastashlands | Ashlands Gourmet Bowl | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastAshlands_Material x1 | It's hard to tell whether the steam coming off of this dish is because it's fre… |
| FeastBlackforest | $item_feastblackforest | Black Forest Buffet Platter | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastBlackforest_Material x1 | You won't be able to resist this platter of delights from the Black Forest! Ven… |
| FeastDeepNorth | $item_feastdeepnorth | Northern Morning Fare | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastDeepNorth_Material x1 | Warming and filling, this meal will sustain you even during the coldest of days… |
| FeastMeadows | $item_feastmeadows | Whole Roasted Meadow Boar | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastMeadows_Material x1 | A boar that has been roasted to perfection, glazed and served atop a bed of gre… |
| FeastMistlands | $item_feastmistlands | Mushrooms Galore á la Mistlands | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastMistlands_Material x1 | The time has come for mushroom enthusiasts to rejoice! Try different kinds of m… |
| FeastMountains | $item_feastmountains | Hearty Mountain Logger's Stew | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastMountains_Material x1 | Gather around this steaming pot full of deliciousness and warm yourselves up ag… |
| FeastOceans | $item_feastoceans | Sailor's Bounty | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastOceans_Material x1 | Fish, fish, and more fish! And also serpent meat, cut to look like fish! Explor… |
| FeastPlains | $item_feastplains | Plains Pie Picnic | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastPlains_Material x1 | There's nothing plain about this feast! Enjoy pies and loaves fresh from the ov… |
| FeastSwamps | $item_feastswamps | Swamp Dweller's Delight | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastSwamps_Material x1 | Who knew that leeches were edible? With the correct preparation (lots of cookin… |
| ashwood_wall_beam_67 | $piece_ashwoodbeam67 | Ashwood Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_67 | $piece_ashwoodwallrooftop67 | Ashwood Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_roof_67_a | $piece_ashwoodwallroof67 | Ashwood Wall 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_roof_67_upsidedown | $piece_ashwoodwallroof67upsidedown | Ashwood Wall 67° (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| darkwood_beam_67 | $piece_darkwoodbeam67 |  | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_roof_67 | $piece_darkwoodroof67 | Shingle Roof 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_icorner_67 | $piece_darkwoodrooficorner67 | Shingle Roof Inner Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_ocorner_67 | $piece_darkwoodroofocorner67 | Shingle Roof Outer Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_top_67 | $piece_darkwoodrooftop67 | Shingle Roof Ridge 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| piece_bench_runed | $piece_bench_runed | Carved Bench | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x6, MooseHide x1 | A rustic yet comfortable place to sit. |
| piece_chair_runed | $piece_chair_runed | Carved Chair | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4, MooseSinew x1 | The shape of this chair might be simple, but its decorations are not. |
| piece_chest_grausten | $piece_chestgrausten | Grausten Chest | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Grausten x10, TrophyCharredMelee x5, FlametalNew x2 | Stone and metal are sure to keep your belongings safe. The charred skulls help… |
| piece_chest_warderobe | $piece_chestwarderobe | Wardrobe | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x10, Tar x2, BlackMetal x6 | An elegant place for storage. |
| piece_snowlantern | $piece_snowlantern | Snow Lantern | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Snowball x8 | Adds a cosy touch to a wintry landscape. |
| piece_table_runed | $piece_table_runed | Long Carved Table | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x20, Tar x2, IronNails x20 | The story carved into this table is excellent to read during long feasts. |
| piece_table_runed_small | $piece_table_runed_small | Square Carved Table | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x6, Tar x1, IronNails x6 | A table for more intimate gatherings. |
| rug_seal | $piece_rug_seal | Sealskin Rug | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | SealHide x4 | This rug is so soft, soft like innocence. |
| scale_halfwall_1x2 | $piece_scale_halfwall | Scalewood Half Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x1 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_quarterwall_1x1 | $piece_scale_quarterwall | Scalewood Quarter Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x1 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_2x2 | $piece_scale_wall | Scalewood Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26 | $piece_scale_26 | Scalewood Wall 26° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_flipped | $piece_scale_26_flipped | Scalewood Wall 26° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_upsidedown | $piece_scale_26_upsidedown | Scalewood Wall 26° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_upsidedown_flipped | $piece_scale_26_upsidedown_flipped | Scalewood Wall 26° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45 | $piece_scale_45 | Scalewood Wall 45° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_flipped | $piece_scale_45_flipped | Scalewood Wall 45° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_upsidedown | $piece_scale_45_upsidedown | Scalewood Wall 45° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_upsidedown_flipped | $piece_scale_45_upsidedown_flipped | Scalewood Wall 45° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67 | $piece_scale_67 | Scalewood Wall 67° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_flipped | $piece_scale_67_flipped | Scalewood Wall 67° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_upsidedown | $piece_scale_67_upsidedown | Scalewood Wall 67° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_upsidedown_flipped | $piece_scale_67_upsidedown_flipped | Scalewood Wall 67° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| stave_beam_26 | $piece_stavebeam26 | Timber Beam 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_2m | $piece_stavebeam2 | Timber Beam 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_45 | $piece_stavebeam45 | Timber Beam 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_4m | $piece_stavebeam4 | Timber Beam 4m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_67 | $piece_stavebeam67 | Timber Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_deco_beam_26 | $piece_stavedecobeam26 | Decorated Timber Beam 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_2m | $piece_stave_deco_beam_2m | Decorated Timber Beam 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_45 | $piece_stavedecobeam45 | Decorated Timber Beam 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_67 | $piece_stavedecobeam67 | Decorated Timber Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_pole_2m | $piece_stave_deco_pole_2m | Decorated Timber Pole 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_wall_2x2 | $piece_deco_stave_wall | Lathed Timber Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | This wall won't do much to keep out the cold, but it's very pretty to look at. |
| stave_pole_2m | $piece_stave_pole_2m | Timber Pole 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_pole_4m | $piece_stave_pole_4m | Timber Pole 4m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_2x2 | $piece_stave_wall | Timber Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | Heavy timber walls are good for keeping out the cold. |
| stave_wall_cross_26 | $piece_stavecross26 | Timber Roof Cross 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_cross_45 | $piece_stavecross45 | Timber Roof Cross 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_cross_67 | $piece_stavewallrooftop67 | Timber Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| wood_beam_67 | $piece_woodbeam67 | Wood Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_roof_67 | $piece_woodroof67 | Thatch Roof 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_icorner_67 | $piece_woodrooficorner67 | Thatch Roof Inner Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_ocorner_67 | $piece_woodroofocorner67 | Thatch Roof Outer Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_top_67 | $piece_woodrooftop67 | Thatch Roof Ridge 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_wall_roof_67_a | $piece_woodwallroof67 | Wood Wall 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_67_upsidedown | $piece_woodwallroof67upsidedown | Wood Wall 67° (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_top_67 | $piece_woodwallrooftop67 | Wood Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| BakedPoteitr | $item_bakedpoteitr | Baked Poteitr | Food (Feaster) |  | Feaster | BakedPoteitr x1 | Neither boiled nor mashed nor in a stew. Still delicious though! |
| BlackSoup | $item_blacksoup | Black Soup | Food (Feaster) |  | Feaster | BlackSoup x1 | A perfect balance of sweetness and acidity. |
| BloodPudding | $item_bloodpudding | Blood Pudding | Food (Feaster) |  | Feaster | BloodPudding x1 | It's bloody tasty. |
| Blueberries | $item_blueberries | Blueberries | Food (Feaster) |  | Feaster | Blueberries x1 | Tiny but tasty. |
| BoarJerky | $item_boarjerky | Boar Jerky | Food (Feaster) |  | Feaster | BoarJerky x1 | Lean and salty. |
| Bread | $item_bread | Bread | Food (Feaster) |  | Feaster | Bread x1 | A tasty loaf of bread. |
| Carrot | $item_carrot | Carrot | Food (Feaster) |  | Feaster | Carrot x1 | An orange treat. |
| CarrotSoup | $item_carrotsoup | Carrot Soup | Food (Feaster) |  | Feaster | CarrotSoup x1 | A warm tasty soup made of mostly carrots. |
| Cloudberry | $item_cloudberries | Cloudberries | Food (Feaster) |  | Feaster | Cloudberry x1 | The gold of the forest. |
| CookedAsksvinMeat | $item_asksvin_meat_cooked | Cooked Asksvin Tail | Food (Feaster) |  | Feaster | CookedAsksvinMeat x1 | This meat has a potent and mature flavour, but is very tasty when grilled right. |
| CookedBjornMeat | $item_bjorn_meat_cooked | Cooked Bear Meat | Food (Feaster) |  | Feaster | CookedBjornMeat x1 | Tastes like victory. |
| CookedBoneMawSerpentMeat | $item_bonemawmeat_cooked | Cooked Bonemaw Meat | Food (Feaster) |  | Feaster | CookedBoneMawSerpentMeat x1 | The boiling sea did nothing to this meat, but grilling it over the fire has giv… |
| CookedBugMeat | $item_bug_meat_cooked | Cooked Seeker Meat | Food (Feaster) |  | Feaster | CookedBugMeat x1 | Succulent white meat. A true delicacy. |
| CookedChickenMeat | $item_chicken_meat_cooked | Cooked Chicken Meat | Food (Feaster) |  | Feaster | CookedChickenMeat x1 | It tastes like chicken. |
| CookedDeerMeat | $item_deer_meat_cooked | Cooked Deer Meat | Food (Feaster) |  | Feaster | CookedDeerMeat x1 | All that running paid off. |
| CookedEgg | $item_egg_cooked | Cooked Egg | Food (Feaster) |  | Feaster | CookedEgg x1 | Sunny side up! |
| CookedHareMeat | $item_hare_meat_cooked | Cooked Hare Meat | Food (Feaster) |  | Feaster | CookedHareMeat x1 | Stringy but flavorful. |
| CookedLoxMeat | $item_loxmeat_cooked | Cooked Lox Meat | Food (Feaster) |  | Feaster | CookedLoxMeat x1 | A great hunk of tender meat, food fit for Valhalla! |
| CookedMeat | $item_boar_meat_cooked | Cooked Boar Meat | Food (Feaster) |  | Feaster | CookedMeat x1 | An earthly taste. |
| CookedMooseMeat | $item_moose_meat_cooked | Cooked Moose Meat | Food (Feaster) |  | Feaster | CookedMooseMeat x1 | This meat is lean yet full of flavour. |
| CookedSealBlubber | $item_blubber_cooked | Cooked Seal Blubber | Food (Feaster) |  | Feaster | CookedSealBlubber x1 | A chewy meat, with an aftertaste of remorse. |
| CookedVoltureMeat | $item_volture_meat_cooked | Cooked Volture Meat | Food (Feaster) |  | Feaster | CookedVoltureMeat x1 | A chewy and somewhat dry meat. Some seasoning would probably make it taste bett… |
| CookedWolfMeat | $item_wolf_meat_cooked | Cooked Wolf Meat | Food (Feaster) |  | Feaster | CookedWolfMeat x1 | A wild taste. |
| DeerStew | $item_deerstew | Deer Stew | Food (Feaster) |  | Feaster | DeerStew x1 | Fall-apart tender. |
| Eyescream | $item_eyescream | Eyescream | Food (Feaster) |  | Feaster | Eyescream x1 | Crispy cool and creamy. |
| Fiddleheadfern | $item_fiddleheadfern | Fiddlehead | Food (Feaster) |  | Feaster | Fiddleheadfern x1 | Veggies with a twist! |
| FierySvinstew | $item_fierysvinstew | Fiery Svinstew | Food (Feaster) |  | Feaster | FierySvinstew x1 | This musty stew is a necessity on every adventurer's menu. |
| FishAndBread | $item_fishandbread | Fish 'n' Bread | Food (Feaster) |  | Feaster | FishAndBread x1 | Bounty from both land and sea. |
| FishCooked | $item_fish_cooked | Cooked Fish | Food (Feaster) |  | Feaster | FishCooked x1 | A tasty side of smoked fish. |
| FishSoup | $item_fishsoup | Fish Soup | Food (Feaster) |  | Feaster | FishSoup x1 | Swimming with flavour! |
| FishWraps | $item_fishwraps | Fish Wraps | Food (Feaster) |  | Feaster | FishWraps x1 | Bread and fish, what more to wish? |
| GlowWorm | $item_glowworm | Luminous Larva | Food (Feaster) |  |  | GlowWorm x1 | Slimy, yet satisfying. |
| Honey | $item_honey | Honey | Food (Feaster) |  | Feaster | Honey x1 | Sweet and tasty. |
| HoneyGlazedChicken | $item_honeyglazedchicken | Honey Glazed Chicken | Food (Feaster) |  | Feaster | HoneyGlazedChicken x1 | Grilled to perfection. Makes both eyes and mouths water. |
| Kale | $item_kale | Kale | Food (Feaster) |  | Feaster | Kale x1 | A versatile leafy green. |
| KaleChips | $item_kalechips | Kale Chips | Food (Feaster) |  | Feaster | KaleChips x1 | Crispy greens! |
| Lingonberry | $item_lingonberries | Lingonberries | Food (Feaster) |  | Feaster | Lingonberry x1 |  |
| Lingondricka | $item_lingondricka | Lingonberry Juice | Food (Feaster) |  | Feaster | Lingondricka x1 | Pairs well with most foods. |
| LoxPie | $item_loxpie | Lox Meat Pie | Food (Feaster) |  | Feaster | LoxPie x1 | Break the crust to release a cloud of fragrant steam. Delicious! |
| MagicallyStuffedShroom | $item_magicallystuffedmushroom | Stuffed Mushroom | Food (Feaster) |  | Feaster | MagicallyStuffedShroom x1 | Bursting with magical flavour. |
| MarinatedGreens | $item_marinatedgreens | Marinated Greens | Food (Feaster) |  | Feaster | MarinatedGreens x1 | It's spicy, it's chewy, it's sweet… This mad dish tickles your tongue as well a… |
| MashedMeat | $item_mashedmeat | Mashed Meat | Food (Feaster) |  | Feaster | MashedMeat x1 | Leftover meat can actually be pretty tasty if you just mash it right! |
| MeatPlatter | $item_meatplatter | Meat Platter | Food (Feaster) |  | Feaster | MeatPlatter x1 | Battle fuel. |
| MeatballsMashedPoteitr | $item_meatballsmashedpoteitr | Meatballs and Poteitr | Food (Feaster) |  | Feaster | MeatballsMashedPoteitr x1 | It doesn't get more iconic than this! |
| MinceMeatSauce | $item_mincemeatsauce | Minced Meat Sauce | Food (Feaster) |  | Feaster | MinceMeatSauce x1 | Chunks of goodness in a thick gravy. |
| MisthareSupreme | $item_mistharesupreme | Misthare Supreme | Food (Feaster) |  | Feaster | MisthareSupreme x1 | One of life's Great Pleasures. |
| MooseKebab | $item_moosekebab | Meat In Bread | Food (Feaster) |  | Feaster | MooseKebab x1 | A convenient meal, often favoured by travelling merchants. |
| Mushroom | $item_mushroomcommon | Mushroom | Food (Feaster) |  | Feaster | Mushroom x1 | Bounty of the forest. |
| MushroomBzerker | $item_mushroom_bzerker | Toadstool | Food (Feaster) |  | Feaster | MushroomBzerker x1 | Some say you can eat everything you find in the forest. That is not the case wi… |
| MushroomJotunPuffs | $item_jotunpuffs | Jotun Puffs | Food (Feaster) |  | Feaster | MushroomJotunPuffs x1 | An invigorating mushroom that can be used for cooking. |
| MushroomMagecap | $item_magecap | Magecap | Food (Feaster) |  | Feaster | MushroomMagecap x1 | A mushroom commonly used in a sorcerer's diet. |
| MushroomOmelette | $item_mushroomomelette | Mushroom Omelette | Food (Feaster) |  | Feaster | MushroomOmelette x1 | A delicious omelette with an earthy aftertaste. |
| MushroomSmokePuff | $item_smokepuff | Smoke Puff | Food (Feaster) |  | Feaster | MushroomSmokePuff x1 | Hopefully it tastes better after cooking. |
| MushroomYellow | $item_mushroomyellow | Yellow Mushroom | Food (Feaster) |  | Feaster | MushroomYellow x1 | An energetic glowing mushroom. |
| NeckTailGrilled | $item_necktailgrilled | Grilled Neck Tail | Food (Feaster) |  | Feaster | NeckTailGrilled x1 | This savoury, charcoal-grilled meat has a slight aroma of seaweed and grass. |
| Oat | $item_oat | Oats | Food (Feaster) |  | Feaster | Oat x1 | Tasty grains, to be used as they are or to be ground into flour. |
| OatMilk | $item_oatmilk | Oat Milk | Food (Feaster) |  | Feaster | OatMilk x1 | Tastes like innovation. |
| OatmealLingonberryJam | $item_oatmeallingonberryjam | Oatmeal | Food (Feaster) |  | Feaster | OatmealLingonberryJam x1 | Served with a generous helping of lingonberry jam. |
| Onion | $item_onion | Onion | Food (Feaster) |  | Feaster | Onion x1 | A crunchy and spicy taste. |
| OnionSoup | $item_onionsoup | Onion Soup | Food (Feaster) |  | Feaster | OnionSoup x1 | Deliciously rich. |
| OvenPancake | $item_ovenpancake | Oven Pancake | Food (Feaster) |  | Feaster | OvenPancake x1 | Warm and fluffy. |
| Pancakes | $item_pancakes | Pancakes | Food (Feaster) |  | Feaster | Pancakes x1 | Was there ever a more comforting food? |
| PiquantPie | $item_piquantpie | Piquant Pie | Food (Feaster) |  | Feaster | PiquantPie x1 | It takes some time and effort to make this pie, but the taste is well worth it. |
| Poteitr | $item_poteitr | Poteitr | Food (Feaster) |  | Feaster | Poteitr x1 | The possibilities are practically endless. Who wouldn't want a taste? |
| Pukeberries | $item_pukeberries | Bukeperries | Food (Feaster) |  | Feaster | Pukeberries x1 | Allows the consumer to quickly evacuate any misplaced meal and start anew. |
| PulledBear | $item_pulledbear | Pulled Bear | Food (Feaster) |  | Feaster | PulledBear x1 | Tender meat cooked for hours upon hours, until it practically falls apart. |
| QueensJam | $item_queensjam | Queen's Jam | Food (Feaster) |  | Feaster | QueensJam x1 | That classic tasty blend of raspberries and blueberries. |
| Raspberry | $item_raspberries | Raspberries | Food (Feaster) |  | Feaster | Raspberry x1 | Sweet and delicious. |
| RoastedCrustPie | $item_roastedcrustpie | Roasted Crust Pie | Food (Feaster) |  | Feaster | RoastedCrustPie x1 | This dessert keeps you going all day long. |
| RottenMeat | $item_meat_rotten | Rotten Meat | Food (Feaster) |  | Feaster | RottenMeat x1 | There are maggots crawling in the meat. It smells awful. |
| RoyalJelly | $item_royaljelly | Royal Jelly | Food (Feaster) |  | Feaster | RoyalJelly x1 | Jelly fit for kings and queens. |
| Salad | $item_salad | Salad | Food (Feaster) |  | Feaster | Salad x1 | Fresh, crisp leaves. |
| Sausages | $item_sausages | Sausages | Food (Feaster) |  | Feaster | Sausages x1 | Links of savory, smoked meat. |
| ScorchingMedley | $item_scorchingmedley | Scorching Medley | Food (Feaster) |  | Feaster | ScorchingMedley x1 | A varied diet is important, so why not try this vegetarian option? |
| SealSoup | $item_sealsoup | Seal Meat Soup | Food (Feaster) |  | Feaster | SealSoup x1 | A warm and tasty meal, best enjoyed on a cold day. |
| SeekerAspic | $item_seekeraspic | Seeker Aspic | Food (Feaster) |  | Feaster | SeekerAspic x1 | A quivering jelly with a taste like gentle electricity. |
| SerpentMeatCooked | $item_serpentmeatcooked | Cooked Serpent Meat | Food (Feaster) |  | Feaster | SerpentMeatCooked x1 | A cooked slice of sea serpent. Smells good. |
| SerpentStew | $item_serpentstew | Serpent Stew | Food (Feaster) |  | Feaster | SerpentStew x1 | Smells of honey and serpent... |
| ShocklateSmoothie | $item_shocklatesmoothie | Muckshake | Food (Feaster) |  | Feaster | ShocklateSmoothie x1 | Wakes you up! |
| SizzlingBerryBroth | $item_sizzlingberrybroth | Sizzling Berry Broth | Food (Feaster) |  | Feaster | SizzlingBerryBroth x1 | This soup settles in your stomach with an almost tingly sensation. |
| SmokedFish | $item_smokedfish | Smoked Fish | Food (Feaster) |  | Feaster | SmokedFish x1 | Fish prepared in the most delicious way. |
| SmokedMooseMeat | $item_smokedmoosemeat | Smoked Moose Meat | Food (Feaster) |  | Feaster | SmokedMooseMeat x1 | The smoke only adds to the wild flavour. |
| SparklingShroomshake | $item_sparklingshroomshake | Sparkling Shroomshake | Food (Feaster) |  | Feaster | SparklingShroomshake x1 | Perhaps it's not the best flavour to start the day with, but it will give you t… |
| SpicyMarmalade | $item_spicymarmalade | Spicy Marmalade | Food (Feaster) |  | Feaster | SpicyMarmalade x1 | Sugary honey perfectly balanced with tangy fronds and tart berries. |
| TurnipStew | $item_turnipstew | Turnip Stew | Food (Feaster) |  | Feaster | TurnipStew x1 | Nutritious and restorative. |
| VikingCupcake | $item_vikingcupcake | Frosted Sweetbread | Food (Feaster) |  | Feaster | VikingCupcake x1 | A sweet and tasty treat, to celebrate a feat! |
| Vineberry | $item_vineberry | Vineberry Cluster | Food (Feaster) |  | Feaster | Vineberry x1 | These juicy berries are both sour and sweet. |
| WolfJerky | $item_wolfjerky | Wolf Jerky | Food (Feaster) |  | Feaster | WolfJerky x1 | Chewy and full of flavor. |
| WolfMeatSkewer | $item_wolf_skewer | Wolf Skewer | Food (Feaster) |  | Feaster | WolfMeatSkewer x1 | Dripping with taste. |
| YggdrasilPorridge | $item_yggdrasilporridge | Yggdrasil Porridge | Food (Feaster) |  | Feaster | YggdrasilPorridge x1 | Made with sap from the great tree. Even a mouthful imparts a warm glow to your… |
| ArmorStand | $piece_armorstand | Armour Stand | Furniture | piece_workbench | Hammer | FineWood x8, IronNails x4, LeatherScraps x2 | Some clothes are just too nice to fold away. Why not put them on display instea… |
| ArmorStand_Female | $piece_armorstand | Armour Stand | Furniture | piece_workbench |  | FineWood x8, BronzeNails x2, Tar x4 |  |
| ArmorStand_Male | $piece_armorstand_male |  | Furniture | piece_workbench |  | FineWood x8, BronzeNails x2, Tar x4 |  |
| Candle_resin | $piece_candle | Resin Candle | Furniture | piece_workbench | Hammer | Resin x1, CandleWick x1 | A small but incredibly cosy lightsource. |
| Candle_resin_bogwitch | $piece_candle | Resin Candle | Furniture | piece_workbench |  | Resin x1, CandleWick x1 |  |
| Morkhalla_ChestAncient | $piece_morkhallachestancient | Ancient Chest | Furniture | piece_workbench |  | Wood x10, Tar x2, BlackMetal x6 | The sturdy black metal that holds this chest together lets you put almost anyth… |
| TreasureChest_ashland_stone | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_blackforest | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_charredfortress | $piece_charredchest | Charred Chest | Furniture | piece_workbench |  | CharredBone x10 |  |
| TreasureChest_deepnorth_village | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_dvergr_loose_stone | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_dvergrtower | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_dvergrtown | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_fCrypt | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_forestcrypt | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_forestcrypt_hildir | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_heath | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_heath_hildir | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_meadows | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_meadows_01 | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_meadows_02 | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_meadows_buried | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_meadows_combat | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_memorial_buried | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_morkhalla | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_mountaincave | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_mountaincave_hildir | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_mountains | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_plains_stone | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_plainsfortress_hildir | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_sunkencrypt | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| TreasureChest_swamp | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_trollcave | Chest |  | Furniture | piece_workbench |  | Stone x10 |  |
| ashwood_bed | $piece_ashwood_bed | Ashwood Bed | Furniture | piece_workbench | Hammer | Blackwood x8, LoxPelt x2, AskHide x2 | Rest easy on this finely crafted bed, with asksvin hides to insulate you from h… |
| bed | $piece_bed | Bed | Furniture | piece_workbench | Hammer | Wood x8 | When night falls, you'll want somewhere to sleep. |
| goblin_bed |  |  | Furniture | piece_workbench |  |  |  |
| itemstand | $piece_itemstand | Item Stand | Furniture | piece_workbench | Hammer | FineWood x4, BronzeNails x1 | Mount items on vertical surfaces with this item stand. |
| itemstandh | $piece_itemstand | Item Stand | Furniture | piece_workbench | Hammer | FineWood x4, BronzeNails x1 | Place items on horizontal surfaces with this item stand. |
| jute_carpet | $piece_jute_carpet | Red Jute Carpet | Furniture | piece_workbench | Hammer | JuteRed x4 | This coarse fabric is excellent for keeping the cold at bay. |
| jute_carpet_blue | $piece_juteblue_carpet | Blue Jute Carpet | Furniture | piece_workbench | Hammer | JuteBlue x4 | No matter how misty the air is, this carpet doesn't appear to get damp. |
| loot_chest_stone | Chest |  | Furniture |  |  | Wood x10 |  |
| loot_chest_wood | Chest |  | Furniture |  |  | Wood x10 |  |
| loot_deepNorth_Granary | $piece_chestbarrel | Barrel | Furniture | piece_workbench |  | Wood x10 | A barrel is good for storing lots of things, including food and drink. |
| loot_deepNorth_TimberHall | $piece_chestbarrel | Barrel | Furniture | piece_workbench |  | Wood x10 | A barrel is good for storing lots of things, including food and drink. |
| piece_CelebrationGarland | $piece_celebrationgarland | Flower Garland | Furniture | piece_workbench | Hammer | FineWood x2, Dandelion x1 | Some flowers to brighten up your day! |
| piece_FairylightGarland | $piece_fairylightgarland | Fey Lights | Furniture | piece_workbench | Hammer | FineWood x2, Thistle x1 | Lends a gentle light to your festivities. |
| piece_Lavalantern | $piece_lavalantern | Lava Lantern | Furniture | piece_workbench | Hammer | FlametalNew x1, ProustitePowder x1, SulfurStone x1 | It's...hypnotising to look at... |
| piece_asksvinskeleton | $piece_asksvinskeleton | Asksvin Skeleton | Furniture | piece_workbench | Hammer | BoneFragments x50, AsksvinCarrionNeck x1, AsksvinCarrionPelvic x1, AsksvinCarrionRibcage x1, AsksvinCarrionSkull x1 | Gruesome or decorative? That is up to you. |
| piece_banner01 | $piece_banner01 | Black Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Coal x4 | A black banner, like the depths of Hel. |
| piece_banner02 | $piece_banner02 | Blue Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Blueberries x4 | A blue banner, like the oceans around Midgard. |
| piece_banner03 | $piece_banner03 | White and Red Striped Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Raspberry x4 | A red striped banner, like a sail for a ship. |
| piece_banner04 | $piece_banner04 | Red Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Bloodbag x1 | A red banner, like the blood of Odin's foes. |
| piece_banner05 | $piece_banner05 | Green Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Guck x1 | A green banner, like the lush forests of Alfheim. |
| piece_banner06 | $piece_banner06 | Blue, Red and White Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Blueberries x2, Raspberry x2, Cloudberry x1 | A red and blue striped banner, because sometimes you can't pick a colour. |
| piece_banner07 | $piece_banner07 | White and Blue Striped Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Blueberries x2, Cloudberry x3 | A blue striped banner, like a sail for a ship. |
| piece_banner08 | $piece_banner08 | Yellow Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Dandelion x4, Coal x2 | A yellow banner, like the golden fields of Vanaheim. |
| piece_banner09 | $piece_banner09 | Purple Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Blueberries x2, Raspberry x3 | A purple banner, like strange magics. |
| piece_banner10 | $piece_banner10 | Orange Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Carrot x2, Cloudberry x3 | An orange banner, like the fires of Muspelheim. |
| piece_banner11 | $piece_banner11 | White Banner | Furniture | piece_workbench | Hammer | LeatherScraps x6, FineWood x2, Coal x2, Cloudberry x4 | A white banner, like the snow in Niflheim. |
| piece_barber | $piece_barber | Barber Station | Furniture | piece_workbench | Hammer | FineWood x10, BarberKit x1, BronzeNails x5, TrollHide x5 | Helps you stay up to date with the latest viking fashion. |
| piece_bathtub | $piece_bathtub | Hot Tub | Furniture | piece_workbench | Hammer | Wood x20, Tar x6, Iron x10, Stone x8 | Respectable vikings bathe as often as once per week! |
| piece_bed02 | $piece_bed02 | Dragon Bed | Furniture | piece_workbench | Hammer | FineWood x40, DeerHide x7, WolfPelt x4, Feathers x10, IronNails x15 | Draped in furs, this bed is sure to give you a good night's sleep. |
| piece_bench01 | $piece_bench01 | Wood Bench | Furniture | piece_workbench | Hammer | FineWood x6 | If there is room in your heart, there is room for your butt. |
| piece_blackmarble_bench | $piece_blackmarble_bench | Black Marble Bench | Furniture | piece_stonecutter | Hammer | BlackMarble x6, Copper x3 | Sit with dignity on this bench, finely carved from black marble. |
| piece_blackmarble_table | $piece_blackmarble_table | Black Marble Table | Furniture | piece_stonecutter | Hammer | BlackMarble x6, Copper x3 | Dine like a dwarf at this table! |
| piece_blackmarble_throne | $piece_blackmarble_throne | Black Marble Throne | Furniture | piece_stonecutter | Hammer | BlackMarble x20, ScaleHide x4, DeerHide x2, Copper x5 | A throne carved in authentic dvergr style, yet large enough for a human to sit… |
| piece_blackwood_bench | $piece_bench01 | Wood Bench | Furniture | piece_workbench |  | Blackwood x6 |  |
| piece_blackwood_bench01 | $piece_blackwoodbench01 | Ashwood Bench | Furniture | piece_workbench | Hammer | Blackwood x6 | This bench has an elegant curve to it, to facilitate conversation. |
| piece_bone_throne | $piece_bone_throne | Bone Throne | Furniture | piece_stonecutter | Hammer | CharredBone x15, FlametalNew x4, Grausten x20, Charredskull x3 | This throne is sure to invoke respect in friends and foes alike. |
| piece_brazierceiling01 | $piece_brazierceiling01 | Hanging Brazier | Furniture | forge | Hammer | Bronze x5, Coal x2, Chain x1 | If your fire hangs from the ceiling, you can't accidentally stumble into it. |
| piece_brazierfloor01 | $piece_brazierfloor01 | Standing Brazier | Furniture | forge | Hammer | Bronze x5, Coal x2, WolfClaw x3 | A classy way to light up the room. |
| piece_brazierfloor02 | $piece_brazierfloor02 | Blue Standing Brazier | Furniture | forge | Hammer | Bronze x5, GreydwarfEye x5, WolfClaw x3 | It's hard to tell whether this flame burns incredibly hot or incredibly cold. |
| piece_chair | $piece_stool | Stool | Furniture | piece_workbench | Hammer | FineWood x4 | A place to sit. |
| piece_chair02 | $piece_chair | Wood Chair | Furniture | piece_workbench | Hammer | FineWood x4 | Lean back and relax. |
| piece_chair03 | $piece_darkwoodchair | Darkwood Chair | Furniture | piece_workbench | Hammer | FineWood x4, Tar x1, IronNails x5, DeerHide x1 | This chair is padded with furs, suitable for esteemed guests. |
| piece_chest | $piece_chest | Reinforced Chest | Furniture | piece_workbench | Hammer | FineWood x10, Iron x2 | A sturdier chest, with room for more storage. |
| piece_chest_barrel | $piece_chestbarrel | Barrel | Furniture | piece_workbench | Hammer | Wood x10, BarrelRings x1 | A barrel is good for storing lots of things, including food and drink. |
| piece_chest_blackmetal | $piece_chestblackmetal | Black Metal Chest | Furniture | piece_workbench | Hammer | Wood x10, Tar x2, BlackMetal x6 | The sturdy black metal that holds this chest together lets you put almost anyth… |
| piece_chest_private | $piece_chestprivate | Personal Chest | Furniture | piece_workbench | Hammer | FineWood x10, Iron x8 | Some things you would rather lock away and keep for yourself. |
| piece_chest_wood | $piece_chestwood | Chest | Furniture | piece_workbench | Hammer | Wood x10 | A good place to store any items you don't need to carry with you. |
| piece_cloth_hanging_door | $piece_clothdoor | Red Jute Curtain | Furniture | piece_workbench | Hammer | JuteRed x4, FineWood x1 | Heavy curtains of red jute, excellent for draping. |
| piece_cloth_hanging_door_blue | $piece_hanging_cloth_blue1 | Blue Jute Drapes | Furniture | piece_workbench | Hammer | JuteBlue x4, FineWood x1 | Blue jute makes any building look official and important. |
| piece_cloth_hanging_door_blue2 | $piece_hanging_cloth_blue2 | Blue Jute Curtain | Furniture | piece_workbench | Hammer | JuteBlue x4, FineWood x1 | Blue jute makes any building look official and important. |
| piece_dvergr_lantern | $piece_dvergr_lantern | Dvergr Wall Lantern | Furniture | blackforge | Hammer | Copper x2, Lantern x1, Chain x1 | A finely wrought lantern, crafted by a true artisan. The wall mount is easy eno… |
| piece_dvergr_lantern_pole | $piece_dvergr_lantern_pole | Dvergr Pole Lantern | Furniture | blackforge | Hammer | Copper x3, Lantern x1, Chain x1 | You can build a pole easy enough, but the lantern can only have been made by a… |
| piece_gift1 | $piece_yuleklapp | Yuleklapp | Furniture | piece_workbench | Hammer | FineWood x2, BoneFragments x1 | A small gift for someone you like. |
| piece_gift2 | $piece_yuleklapp | Yuleklapp | Furniture | piece_workbench | Hammer | FineWood x3, Dandelion x1 | A gift for someone who was nice this year. |
| piece_gift3 | $piece_yuleklapp | Yuleklapp | Furniture | piece_workbench | Hammer | FineWood x4, Raspberry x1 | A large gift for someone you appreciate a lot. |
| piece_groundtorch | $piece_groundtorch | Standing Iron Torch | Furniture | forge | Hammer | Iron x2, Resin x2 | A sturdy iron torch to light up your surroundings. |
| piece_groundtorch_blue | $piece_groundtorchblue | Standing Blue-burning Iron Torch | Furniture | forge | Hammer | Iron x2, GreydwarfEye x2 | A sturdy iron torch, emitting an eerie blue light. |
| piece_groundtorch_green | $piece_groundtorchgreen | Standing Green-burning Iron Torch | Furniture | forge | Hammer | Iron x2, Guck x2 | A sturdy iron torch, burning with a green flame. |
| piece_groundtorch_mist | $piece_groundtorchdemister | Wisp Torch | Furniture |  | Hammer | YggdrasilWood x1, Wisp x1 | Bind the wisp to a fixed spot, to ensure the place is always free of mist. |
| piece_groundtorch_wood | $piece_groundtorchwood | Standing Wood Torch | Furniture | piece_workbench | Hammer | Wood x2, Resin x2 | A torch to stick in the ground, for when you need your hands free. |
| piece_jackoturnip | $piece_jackoturnip | Jack-o-turnip | Furniture | piece_workbench | Hammer | Turnip x4, Resin x2 | The face carved into this vegetable...is it spooky or friendly? |
| piece_logbench01 | $piece_benchlog | Sitting Log | Furniture |  | Hammer | RoundLog x2 | It's nothing fancy, but it's better than sitting on the ground. |
| piece_maypole | $piece_maypole | Maypole | Furniture | piece_workbench | Hammer | Wood x10, Dandelion x4, Thistle x4 | On the longest day of the year, let this monument stretch towards the sun! |
| piece_mistletoe | $piece_mistletoe | Mistletoe | Furniture | piece_workbench | Hammer | FineWood x1, JuteRed x1 | You can't help but to feel like there's something romantic about this... |
| piece_moose_throne | $piece_moose_throne | Antler Throne | Furniture | piece_workbench | Hammer | Frostwood x15, TrophyMoose x1, MooseHide x5 | Though rustic and sturdy, this throne is well suited even for the most noble of… |
| piece_pot1 | $piece_pot_medium_green | Medium Green Pot | Furniture |  | Hammer | Pot_Shard_Green x4, CharcoalResin x1 | A piece of ceramic, deep enough to fit a thing or two inside. |
| piece_pot1_cracked | $piece_pot_medium_cracked | Medium Clay Pot | Furniture |  |  | Pot_Shard_Green x4, CharcoalResin x1 |  |
| piece_pot1_red | $piece_pot_medium_red | Medium Red Pot | Furniture |  |  | Pot_Shard_Red x4, CharcoalResin x1 |  |
| piece_pot2 | $piece_pot_large_green | Large Green Pot | Furniture |  | Hammer | Pot_Shard_Green x5, CharcoalResin x1 | A large statement piece of a pot! You could almost fit an entire viking in ther… |
| piece_pot2_cracked | $piece_pot_large_cracked | Large Clay Pot | Furniture |  |  | Pot_Shard_Green x5, CharcoalResin x1 |  |
| piece_pot2_red | $piece_pot_large_red | Large Red Pot | Furniture |  |  | Pot_Shard_Red x5, CharcoalResin x1 |  |
| piece_pot3 | $piece_pot_small_green | Small Green Pot | Furniture |  | Hammer | Pot_Shard_Green x3, CharcoalResin x1 | A small piece of earthenware, with room for something inside. |
| piece_pot3_cracked | $piece_pot_small_cracked | Small Clay Pot | Furniture |  |  | Pot_Shard_Green x3, CharcoalResin x1 |  |
| piece_pot3_red | $piece_pot_small_red | Small Red Pot | Furniture |  |  | Pot_Shard_Red x3, CharcoalResin x1 |  |
| piece_table | $piece_table | Table | Furniture | piece_workbench | Hammer | FineWood x6 | A small but sturdy table. |
| piece_table_oak | $piece_table_oak | Long Heavy Table | Furniture | piece_workbench | Hammer | FineWood x20, Tar x2, IronNails x20 | This table is perfect for a large feast, with room for many different dishes! |
| piece_table_round | $piece_table_round | Round Table | Furniture | piece_workbench | Hammer | FineWood x10, Tar x2, IronNails x20 | At a round table, no seat is more important than any other. |
| piece_throne01 | $piece_throne01 | Raven Throne | Furniture | piece_workbench | Hammer | FineWood x20, IronNails x10 | Perhaps this is what Odin's throne in Valhalla looks like... Either way, it's c… |
| piece_throne02 | $piece_stonethrone | Stone Throne | Furniture | piece_stonecutter | Hammer | Stone x20, DeerHide x2, WolfPelt x2 | A good leader must be firm like stone, yet they also need to know when to be so… |
| piece_walltorch | $piece_sconce | Sconce | Furniture | forge | Hammer | Wood x2, Copper x2, Resin x2 | Why stick a torch in the ground when you can hang it on your wall? |
| piece_xmascrown | $piece_yulecrown | Yule Wreath | Furniture | piece_workbench | Hammer | PineCone x4, JuteRed x1, FineWood x1 | It's hard to think of anything more festive than a wreath! |
| piece_xmasgarland | $piece_yulegarland | Yule Garland | Furniture | piece_workbench | Hammer | FineWood x2, PineCone x1 | Deck the halls! |
| piece_xmastree | $piece_yuletree | Yule Tree | Furniture | piece_workbench | Hammer | Wood x10, FirCone x1 | This fir tree has been given festive decorative lights. |
| rug_Bjorn | $piece_rug_bjorn | Bearskin Rug | Furniture | piece_workbench | Hammer | BjornHide x1, BjornPaw x2, TrophyBjorn x1 | The fur is soft, though the teeth are still sharp. |
| rug_asksvin | $piece_rug_asksvin | Asksvin Rug | Furniture | piece_workbench | Hammer | AskHide x4 | With how lumpy asksvin hide is, it's hard to make anything useful from a single… |
| rug_deer | $piece_rug_deer | Deer Rug | Furniture | piece_workbench | Hammer | DeerHide x4 | Protects your feet from wood splinters. |
| rug_fur | $piece_rug_lox | Lox Rug | Furniture | piece_workbench | Hammer | LoxPelt x4 | It's incredibly fluffy! |
| rug_hare | $piece_rug_hare | Hare Rug | Furniture | piece_workbench | Hammer | ScaleHide x4 | It's a strange texture, but a very interesting piece of decor. |
| rug_moose | $piece_rug_moose | Moose Hide Carpet | Furniture | piece_workbench | Hammer | MooseHide x4 | This carpet is neatly sewn together, to ensure a large, soft surface. |
| rug_straw | $piece_rug_straw | Straw | Furniture | piece_workbench | Hammer | Barley x1, Flax x1 | Your animals will love this. |
| rug_wolf | $piece_rug_wolf | Wolf Rug | Furniture | piece_workbench | Hammer | WolfPelt x4 | Warm and cosy, especially up in the mountains. |
| sign | $piece_sign | Sign | Furniture |  | Hammer | Wood x2, Coal x1 | Disorder is a survivalist's worst enemy. Defend yourself against it by putting… |
| sign_notext | $piece_sign | Sign | Furniture | piece_workbench |  | Wood x2, Coal x1 |  |
| BarleyWine | $item_barleywine | Fire Resistance Barley Wine | Meads (Feaster) |  | Feaster | BarleyWine x1 | Fortifies you against fire. |
| MeadBugRepellent | $item_mead_bugrepellent | Anti-Sting Concoction | Meads (Feaster) |  | Feaster | MeadBugRepellent x1 | The perfect drink for a day in the Plains. |
| MeadBzerker | $item_mead_bzerker | Berserkir Mead | Meads (Feaster) |  | Feaster | MeadBzerker x1 | Something poisonous stirs within, brewed to the point where its potential can f… |
| MeadEitrLingering | $item_mead_eitr_lingering | Lingering Eitr Mead | Meads (Feaster) |  | Feaster | MeadEitrLingering x1 | Increases eitr regeneration. |
| MeadEitrMinor | $item_mead_eitr_minor | Minor Eitr Mead | Meads (Feaster) |  | Feaster | MeadEitrMinor x1 | Restores eitr. |
| MeadFrostResist | $item_mead_frostres | Frost Resistance Mead | Meads (Feaster) |  | Feaster | MeadFrostResist x1 | Protects against the cold. |
| MeadHasty | $item_mead_hasty | Tonic of Ratatosk | Meads (Feaster) |  | Feaster | MeadHasty x1 | The squirrel must be quick on its feet as it runs up and down the trunk of the… |
| MeadHealthLingering | $item_mead_hp_lingering | Lingering Healing Mead | Meads (Feaster) |  | Feaster | MeadHealthLingering x1 | Increases health regeneration. |
| MeadHealthMajor | $item_mead_hp_major | Major Healing Mead | Meads (Feaster) |  | Feaster | MeadHealthMajor x1 | Restores health. |
| MeadHealthMedium | $item_mead_hp_medium | Medium Healing Mead | Meads (Feaster) |  | Feaster | MeadHealthMedium x1 | Restores health. |
| MeadHealthMinor | $item_mead_hp_minor | Minor Healing Mead | Meads (Feaster) |  | Feaster | MeadHealthMinor x1 | Restores health. |
| MeadLightfoot | $item_mead_lightfoot | Lightfoot Mead | Meads (Feaster) |  | Feaster | MeadLightfoot x1 | This bottle is full, yet it feels like it weighs almost nothing at all. |
| MeadPoisonResist | $item_mead_poisonres | Poison Resistance Mead | Meads (Feaster) |  | Feaster | MeadPoisonResist x1 | Fortifies you against poison. |
| MeadStaminaLingering | $item_mead_stamina_lingering | Lingering Stamina Mead | Meads (Feaster) |  | Feaster | MeadStaminaLingering x1 | Increases stamina regeneration. |
| MeadStaminaMedium | $item_mead_stamina_medium | Medium Stamina Mead | Meads (Feaster) |  | Feaster | MeadStaminaMedium x1 | Restores stamina. |
| MeadStaminaMinor | $item_mead_stamina_minor | Minor Stamina Mead | Meads (Feaster) |  | Feaster | MeadStaminaMinor x1 | Restores stamina. |
| MeadStrength | $item_mead_strength | Mead of Troll Endurance | Meads (Feaster) |  | Feaster | MeadStrength x1 | What creature can carry more than a troll? Why, a viking with this drink of cou… |
| MeadSwimmer | $item_mead_swimmer | Draught of Vananidir | Meads (Feaster) |  | Feaster | MeadSwimmer x1 | You feel invigorated just by holding this, as the ocean beckons you to come for… |
| MeadTamer | $item_mead_tamer | Brew of Animal Whispers | Meads (Feaster) |  | Feaster | MeadTamer x1 | It smells like a pigsty. Likely the taste won't be much better. |
| MeadTasty | $item_mead_tasty | Tasty Mead | Meads (Feaster) |  | Feaster | MeadTasty x1 | The nectar of the Gods, divine mead. |
| MeadTrollPheromones | $item_mead_trollpheromones | Love Potion | Meads (Feaster) |  | Feaster | MeadTrollPheromones x1 | An intense musk permeates the air around this bottle. |
| BatteringRam | $tool_batteringram | Battering Ram | Misc | piece_workbench | Hammer | Blackwood x20, FlametalNew x10, SurtlingCore x2 | This is a force to be reckoned with. Fuel it up and bring it to your enemies' s… |
| Beech_Sapling | $prop_beech_sapling | Beech sapling | Misc |  | Cultivator | BeechSeeds x1 | A beech yields good old fashioned wood. |
| Birch_Sapling | $prop_birch_sapling | Birch Sapling | Misc |  | Cultivator | BirchSeeds x1 | A prime source of finewood. |
| BogWitch_Fire_Pit | $piece_firepit | Campfire | Misc |  |  | Stone x5, Wood x2 |  |
| Cart | $tool_cart | Cart | Misc | piece_workbench | Hammer | Wood x20, BronzeNails x10 | Convenient when you need to move a lot of resources around. |
| Catapult | $tool_catapult | Catapult | Misc | piece_workbench | Hammer | Blackwood x20, FlametalNew x10, CharredCogwheel x1 | If you can't go through it, perhaps you can go over it... |
| FirTree_Sapling | $prop_fir_sapling | Fir Sapling | Misc |  | Cultivator | FirCone x1 | A pretty tree, all year round. |
| FirTree_big_Sapling | $prop_fir_big_sapling | Timberwood Sapling | Misc |  | Cultivator | FirConeFrost x1 | Though native to the northern regions, this tree can grow in most climates. |
| Karve | $ship_karve | Karve | Misc | piece_workbench | Hammer | FineWood x30, DeerHide x10, Resin x20, BronzeNails x80 | A small and sleek ship, ready to set sail. |
| Morkhalla_Stonepile | $piece_marblepile | Black Marble Pile | Misc |  |  | BlackMarble x50 | Perfect for Dvergr inspired construction. |
| Morkhalla_firepit | $piece_firepit | Campfire | Misc |  |  | Stone x5, Wood x2 |  |
| Oak_Sapling | $prop_oak_sapling | Oak Sapling | Misc |  | Cultivator | Acorn x1 | With time, this will grow into a large oak. |
| PineTree_Sapling | $prop_pine_sapling | Pine Sapling | Misc |  | Cultivator | PineCone x1 | These tall trees can be chopped down for corewood. |
| Placeable_HardRock | $item_hardrock | Mysterious Rock | Misc |  | Hammer | StoneRock x1, Coal x1 | It's just a rock... |
| Placeable_Stone | $item_stone | Stone | Misc |  | Hoe | Stone x1 | It's a rock. |
| Raft | $ship_raft | Raft | Misc | piece_workbench | Hammer | Wood x20, LeatherScraps x6, Resin x6 | It may not look like much, but a raft will get you farther than swimming! |
| Sled | $tool_sled |  | Misc | piece_workbench |  | Wood x20, BronzeNails x10 |  |
| Trailership | Longship |  | Misc | piece_workbench |  | IronNails x80, DeerHide x10, FineWood x40, ElderBark x40 |  |
| VikingShip | $ship_longship | Longship | Misc | piece_workbench | Hammer | IronNails x100, DeerHide x10, FineWood x40, ElderBark x40 | A mighty viking ship for sailing to distant shores. |
| VikingShip_Ashlands | $ship_longship_ashlands | Drakkar | Misc | piece_workbench | Hammer | IronNails x100, CeramicPlate x30, FineWood x50, YggdrasilWood x25 | A massive ship, sturdy enough to sail on even the most dangerous seas. |
| VineAsh_sapling | $piece_sapling_vineash | Ashvine | Misc |  | Cultivator | VineberrySeeds x1 | Needs something to cling to. |
| VineGreen_sapling | $piece_sapling_vinegreen | Ivy | Misc |  | Cultivator | VineGreenSeeds x1 | Needs something to cling to. |
| bar_ancientmetal_stack | $piece_ancientmetalstack |  | Misc |  |  | Flametal x30 |  |
| bar_blackmetal_stack | $piece_blackmetalbarstack | Black Metal Stack | Misc |  | Hammer | BlackMetal x30 | Sure to be the envy of all fulings! |
| bar_bronze_stack | $piece_bronzebarstack | Bronze Stack | Misc |  | Hammer | Bronze x30 | Beautiful, versatile... Now also stackable! |
| bar_copper_stack | $piece_copperbarstack | Copper Stack | Misc |  | Hammer | Copper x30 | Premium quality copper. Nothing to complain about! |
| bar_flametal_stack | $piece_flametalbarstack | Flametal Stack | Misc |  | Hammer | FlametalNew x30 | The heat is practically radiating from this stack... |
| bar_gold_stack | $piece_bloodgoldbarstack | Bloodgold Stack | Misc |  | Hammer | Gold x30 | This metal does have a more sinister air than others, doesn't it? |
| bar_iron_stack | $piece_ironbarstack | Iron Stack | Misc |  | Hammer | Iron x30 | If you have iron, you have everything. |
| bar_silver_stack | $piece_silverbarstack | Silver Stack | Misc |  | Hammer | Silver x30 | A tidy way of displaying your wealth. |
| bar_tin_stack | $piece_tinbarstack | Tin Stack | Misc |  | Hammer | Tin x30 | You can make a lot of bronze with this... |
| blackmarble_pile | $piece_marblepile | Black Marble Pile | Misc |  | Hammer | BlackMarble x50 | Perfect for Dvergr inspired construction. |
| blackwood_stack | $piece_blackwoodstack | Ashwood Stack | Misc |  | Hammer | Blackwood x50 | No matter what, this stack of wood always feels warm. |
| bone_stack | $piece_bonestack | Bone Stack | Misc |  | Hammer | BoneFragments x50 | You never know when these might come in handy! |
| bonfire | $piece_bonfire | Bonfire | Misc |  | Hammer | SurtlingCore x1, ElderBark x5, FineWood x5, RoundLog x5 | For when a regular campfire just isn't impressive enough! |
| burn | $piece_cultivate | Cultivate | Misc |  |  |  |  |
| charred_shieldgenerator | $piece_shieldgenerator | Shield Generator | Misc | piece_workbench |  | Stone x5, ShieldCore x1, CeramicPlate x5 |  |
| coal_pile | $piece_coalpile | Coal Pile | Misc |  | Hammer | Coal x50 | It never hurts to have a lot of fuel on hand. |
| cultivate | $piece_cultivate | Cultivate | Misc |  |  |  | Make the soil ready for planting. |
| cultivate_v2 | $piece_cultivate | Cultivate | Misc |  | Cultivator |  | Make the soil ready for planting. |
| darkwood_raven | $piece_darkwoodraven | Raven Adornment | Misc | piece_workbench | Hammer | FineWood x10, Tar x1 | The ravens are trusted companions of the Allfather, of course you would want on… |
| darkwood_wolf | $piece_darkwoodwolf | Wolf Adornment | Misc | piece_workbench | Hammer | FineWood x10, Tar x1 | Wolves are pack animals, but this one seems happy enough by itself. |
| dverger_guardstone | $piece_guardstone | Ward | Misc |  |  | FineWood x5, GreydwarfEye x5, SurtlingCore x1 | Emits a magic seal on the nearby surroundings which prevents other players from… |
| fire_pit | $piece_firepit | Campfire | Misc |  | Hammer | Stone x5, Wood x2 | Useful for cooking, warmth, or just resting for a bit. |
| fire_pit_haldor | $piece_firepit | Campfire | Misc |  |  | Stone x5, Wood x2 |  |
| fire_pit_hildir | $piece_firepit | Campfire | Misc |  |  | Stone x5, Wood x2 |  |
| fire_pit_iron | $piece_firepit_iron | Iron Fire Pit | Misc |  | Hammer | Ironpit x1, Wood x1 | If you don't want to place a campfire directly onto the ground, this is a class… |
| flint_pile | $piece_flintpile | Flint Pile | Misc |  | Hammer | Flint x50 | Watch your step! |
| fuling_trap | $piece_trap | Trap | Misc | piece_workbench |  | BlackMetal x5, BronzeNails x10, MechanicalSpring x1 |  |
| fuling_turret | $piece_turret | Ballista | Misc | piece_workbench |  | BlackMetal x10, YggdrasilWood x10, MechanicalSpring x3 | Defensive structure that shoots missiles at anything that gets in its way. |
| grausten_pile | $piece_graustenpile | Grausten Pile | Misc |  | Hammer | Grausten x50 | Grausten, carefully placed into a pile. |
| guard_stone | $piece_guardstone | Ward | Misc |  | Hammer | FineWood x5, GreydwarfEye x5, SurtlingCore x1 | Emits a magic seal on the nearby surroundings which prevents other players from… |
| guard_stone_test | Guard stone |  | Misc | piece_stonecutter |  | Stone x20, SurtlingCore x5 |  |
| hearth | $piece_hearth | Hearth | Misc | piece_stonecutter | Hammer | Stone x15 | A central part of any homestead. |
| mud_road | $piece_levelground | Level Ground | Misc |  |  |  | If the terrain is too lumpy, you can just flatten it. |
| mud_road_v2 | $piece_levelground | Level Ground | Misc |  | Hoe |  | If the terrain is too lumpy, you can just flatten it. |
| obsidian_pile | $piece_obsidianpile | Obsidian Pile | Misc |  |  | Obsidian x50 | A pile as dark as the night itself. |
| path | $piece_path | Pathen | Misc |  |  |  | Mark out the best way forward. |
| path_v2 | $piece_path | Pathen | Misc |  | Hoe |  | Mark out the best way forward. |
| paved_road | $piece_pavedroad | Paved Road | Misc | piece_stonecutter |  | Stone x1 | A sure mark of civilisation. |
| paved_road_v2 | $piece_pavedroad | Paved Road | Misc | piece_stonecutter | Hoe | Stone x1 | A sure mark of civilisation. |
| piece_ArcheryTarget | $piece_archerytarget | Archery Target | Misc | piece_workbench | Hammer | FineWood x4, LeatherScraps x10 | Aim for the center. |
| piece_TrainingDummy | $piece_trainingdummy | T.W.I.G. | Misc | piece_workbench | Hammer | FineWood x5, BronzeNails x10, Ectoplasm x5 | Step close at your own risk! |
| piece_cartographytable | $piece_cartographytable | Cartography Table | Misc | piece_workbench | Hammer | FineWood x10, BoneFragments x10, Bronze x2, LeatherScraps x5, Raspberry x4 | Mark your discoveries on this map and share them with your friends. |
| piece_chest_treasure | $piece_chesttreasure | Treasure Chest | Misc | piece_workbench | Hammer | Coins x99, Ruby x5, SilverNecklace x2, FineWood x8, Silver x2 | What better way to display your wealth than a treasure chest spilling over with… |
| piece_dvergr_sharpstakes | $piece_dvergr_sharpstakes | Dvergr Sharp Stakes | Misc | blackforge | Hammer | YggdrasilWood x5, Iron x2 | The dvergr know how to keep unwanted visitors away. |
| piece_dvergr_stake_wall | $piece_dvergr_stake_wall | Dvergr Stakewall | Misc | blackforge | Hammer | YggdrasilWood x8, Iron x8 | Reinforced with metal, this is sure to be a sturdy defence. |
| piece_hoodedlantern | $piece_hoodedlantern | Hooded Lantern | Misc |  | Hammer | Frostwood x3, GlowWorm x1 | Aimed in just one direction, the light grows more potent. Convenient for sneak… |
| piece_sharpstakes | $piece_sharpstakes | Sharp Stakes | Misc | piece_workbench | Hammer | Wood x6, RoundLog x4 | If someone comes too close, they'll get poked. |
| piece_shieldgenerator | $piece_shieldgenerator | Shield Generator | Misc | piece_workbench | Hammer | Iron x5, Copper x5, ShieldCore x1 | Creates a shield to protect against weather and incoming projectiles. Fuelled b… |
| piece_stakewall_blackwood | $piece_BlackwoodStakewall | Ashwood Stakewall | Misc | piece_workbench | Hammer | Blackwood x6 | Good for keeping intruders out. |
| piece_trap_troll | $piece_trap | Trap | Misc | piece_workbench | Hammer | BlackMetal x5, BronzeNails x10, MechanicalSpring x1 | This trap will clamp down on whatever steps on it. Careful! |
| piece_turret | $piece_turret | Ballista | Misc | piece_workbench | Hammer | BlackMetal x10, YggdrasilWood x10, MechanicalSpring x3 | Defensive structure that shoots missiles at anything that gets in its way. |
| placeable_bigrock_01 | $piece_rock_01 | Ornamental Boulder | Misc |  | Hoe | Stone x25 | Adds a rustic touch to any garden. |
| placeable_bigrock_02 | $piece_rock_02 | Decorative Boulder | Misc |  | Hoe | Stone x10 | That is a nice boulder. |
| portal | $piece_portal | Portal | Misc | piece_workbench |  | GreydwarfEye x6, Stone x4 |  |
| portal_stone | $piece_portal_stone | Portal – Stone | Misc | piece_stonecutter | Hammer | GreydwarfEye x10, Grausten x30, MoltenCore x2 | The powerful energy source lets you pass through even with the most valuable of… |
| portal_wood | $piece_portal | Portal | Misc | piece_workbench | Hammer | GreydwarfEye x10, FineWood x20, SurtlingCore x2 | Connects to another portal with equal or no tag. |
| raise | $piece_raise | Raise Ground | Misc | piece_workbench |  | Stone x4 | Fill holes and create elevated platforms. |
| raise_v2 | $piece_raise | Raise Ground | Misc | piece_workbench | Hoe | Stone x2 | Fill holes and create elevated platforms. |
| replant | $piece_replant | Grass | Misc |  |  |  | Restore the ground to its original vegetation. |
| replant_v2 | $piece_replant | Grass | Misc |  | Cultivator |  | Restore the ground to its original vegetation. |
| sapling_Kale | $piece_sapling_kale | Kale | Misc |  | Cultivator | KaleSeeds x1 | A hardy and nutritious plant. |
| sapling_barley | $piece_sapling_barley | Barley | Misc |  | Cultivator | Barley x1 | A robust grain that can be ground into flour. |
| sapling_carrot | $piece_sapling_carrot | Carrot | Misc |  | Cultivator | CarrotSeeds x1 | Can be eaten as is, or used for more advanced cooking. |
| sapling_flax | $piece_sapling_flax | Flax | Misc |  | Cultivator | Flax x1 | Pretty flowers with fibres that can be spun into thread. |
| sapling_jotunpuffs | $item_jotunpuffs | Jotun Puffs | Misc |  | Cultivator | MushroomJotunPuffs x1 | An invigorating mushroom that can be used for cooking. |
| sapling_magecap | $item_magecap | Magecap | Misc |  | Cultivator | MushroomMagecap x1 | A mushroom commonly used in a sorcerer's diet. |
| sapling_oat | $piece_sapling_oat | Oat Straw | Misc |  | Cultivator | OatSeeds x1 | Plant to grow oats. |
| sapling_onion | $piece_sapling_onion | Onion | Misc |  | Cultivator | OnionSeeds x1 | Useful for many cooking recipes. |
| sapling_poteitr | $piece_sapling_poteitr | Poteitr | Misc |  | Cultivator | PoteitrSeeds x1 | A potent, otherworldly spud. |
| sapling_seedcarrot | $piece_sapling_seedcarrot | Seed-carrot | Misc |  | Cultivator | Carrot x1 | Plant a carrot to get more seeds |
| sapling_seedkale | $piece_sapling_seedkale | Seed Kale | Misc |  | Cultivator | KaleSeeds x1 | Plant kale seeds to get even more seeds. |
| sapling_seedonion | $piece_sapling_seedonion | Seed-onion | Misc |  | Cultivator | Onion x1 | Plant an onion to get more seeds. |
| sapling_seedturnip | $piece_sapling_seedturnip | Seed-turnip | Misc |  | Cultivator | Turnip x1 | Plant a turnip to get more seeds |
| sapling_turnip | $piece_sapling_turnip | Turnip | Misc |  | Cultivator | TurnipSeeds x1 | A hearty vegetable used for cooking. |
| ship_construction | Ship construction |  | Misc | piece_workbench |  | Wood x20, DeerHide x4 |  |
| skull_pile | $piece_skullpile | Pile of Skulls | Misc |  | Hammer | Charredskull x50 | A certain proof of your prowess in combat. |
| snow_decrease | $piece_snowshovel |  | Misc |  |  |  |  |
| snow_decrease_firepit_placed_large | $piece_snowshovel |  | Misc |  |  |  |  |
| snow_decrease_firepit_placed_small | $piece_snowshovel |  | Misc |  |  |  |  |
| snow_fire | Snow Decrease |  | Misc |  |  |  |  |
| snow_fire_big | Snow Decrease |  | Misc |  |  |  |  |
| snow_increase | Snow Increase |  | Misc |  |  |  |  |
| snow_increase_roller | Snow Increase |  | Misc |  |  |  |  |
| snow_increase_tree | Snow Increase |  | Misc |  |  |  |  |
| snow_increase_treesmall | Snow Increase |  | Misc |  |  |  |  |
| snow_shovel | Snow Decrease |  | Misc |  |  |  |  |
| snow_shovel_big | Snow Decrease |  | Misc |  |  |  |  |
| snow_tree | Snow Decrease |  | Misc |  |  |  |  |
| snow_walk | Snow Decrease |  | Misc |  |  |  |  |
| snow_walk_big | Snow Decrease |  | Misc |  |  |  |  |
| snow_walk_huge | Snow Decrease |  | Misc |  |  |  |  |
| snow_walk_medium | Snow Decrease |  | Misc |  |  |  |  |
| stake_wall | $piece_stakewall | Stakewall | Misc | piece_workbench | Hammer | Wood x4 | Good for keeping intruders out. |
| stone_fence | $piece_stonefence | Stone Fence | Misc | piece_workbench | Hammer | Stone x4 | Stones carefully pieced together into a sturdy fence. |
| stone_pile | $piece_stonepile | Stone Pile | Misc |  | Hammer | Stone x50 | It's a big pile of stone. |
| treasure_pile | $piece_treasure_pile | Coin Pile | Misc |  | Hammer | Coins x999 | A small fortune, gleaming and glittering. |
| treasure_stack | $piece_treasure_stack | Coin Stack | Misc |  | Hammer | Coins x99 | It's important to keep count of your coins. |
| wood_core_stack | $piece_woodcorestack | Corewood Stack | Misc |  | Hammer | RoundLog x50 | A big pile of logs, fresh from the forest. |
| wood_dragon1 | $piece_wooddragon | Wood Dragon Adornment | Misc | piece_workbench | Hammer | FineWood x10 | Make your home legendary by decorating it with a dragon. |
| wood_fence | $piece_woodfence | Roundpole Fence | Misc | piece_workbench | Hammer | Wood x1 | A good fence, for livestock and crops alike! |
| wood_fence_gate | $piece_woodfencegate | Roundpole Gate | Misc | piece_workbench | Hammer | Wood x4 | A gate is far more practical than jumping over the fence every time. |
| wood_fine_stack | $piece_woodfinestack | Finewood Stack | Misc |  | Hammer | FineWood x50 | This pile of wood is waiting to be turned into furniture. |
| wood_frost_stack | $piece_woodfroststack | Timberwood Stack | Misc |  | Hammer | Frostwood x50 | It's as if fresh snow just fell upon it... |
| wood_stack | $piece_woodstack | Wood Stack | Misc |  | Hammer | Wood x50 | Put all that hard work on display! |
| wood_yggdrasil_stack | $piece_yggdrasilstack | Yggdrasil Wood Stack | Misc |  | Hammer | YggdrasilWood x50 | Fine branches, thrumming with mysterious energy. |

## 5. Items

### 5a. Notable items (unique, boss drops, trader wares, keys, utility/trinkets, meads, feasts, odd ones)

| prefab | token | English | type | why | description |
|---|---|---|---|---|---|
| FishingBait | $item_fishingbait | Fishing Bait | Ammo | sold by Haldor, fishing | Common dvergr fishing bait. Fishing rod sold separately. |
| FishingBaitAshlands | $item_fishingbait_ashlands | Hot Fishing Bait | Ammo | fishing | Some fish already like it where the waters are warm, and this bait brings the temperature close to boiling. |
| FishingBaitCave | $item_fishingbait_cave | Cold Fishing Bait | Ammo | fishing | This bait doesn't look like much, but it's a treat to fish that live where it's cold and dark. |
| FishingBaitDeepNorth | $item_fishingbait_deepnorth | Frosty Fishing Bait | Ammo | fishing | It's not very nutritious, so the only fish that'll take this bait are the ones that are used to just eating ice. |
| FishingBaitForest | $item_fishingbait_forest | Mossy Fishing Bait | Ammo | fishing | Dead trolls in the forest often attract a fish or two, speed up the process with this bait! |
| FishingBaitMistlands | $item_fishingbait_mistlands | Misty Fishing Bait | Ammo | fishing | A bait to guide the fish to you through shrouded waters. |
| FishingBaitOcean | $item_fishingbait_ocean | Heavy Fishing Bait | Ammo | fishing | This bait sinks deep, deep enough to lure the fish swimming along the very bottom of the sea. |
| FishingBaitPlains | $item_fishingbait_plains | Stingy Fishing Bait | Ammo | fishing | You're not immune to deathsquito bites, and the fish here aren't immune to this snack! |
| FishingBaitSwamp | $item_fishingbait_swamp | Sticky Fishing Bait | Ammo | fishing | It might smell foul, but the fish drawn to this bait are used to so much worse. |
| charred_bow_Fader | Bow |  | Bow | fader |  |
| charred_bow_volley_Fader | Bow |  | Bow | fader |  |
| skeleton_bow_meadows | Bow |  | Bow | mead |  |
| ArmorBerserkerChest | $item_chest_berserker | Patterns of the Bear | Chest | orb | If these intricate designs can protect warriors from distant lands, they can surely protect you too. |
| ArmorBerserkerUndeadChest | $item_chest_berserker_undead | Vilebone Cage | Chest | orb | Crafted from the shattered bones of a tormented bear. |
| ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | orb | A breastplate of hammered bronze. |
| ArmorDress1 | $item_chest_dress1 | Plain Brown Dress | Chest | sold by Hildir | A plain brown dress, worn over a blue underdress. |
| ArmorDress10 | $item_chest_dress10 | Simple Undyed Dress | Chest | sold by Hildir | A simple dress for everyday wear. |
| ArmorDress2 | $item_chest_dress2 | Brown Dress with Shawl | Chest | sold by Hildir | A brown dress, with a matching shawl around the shoulders. Warm and stylish! |
| ArmorDress3 | $item_chest_dress3 | Brown Dress with Beads | Chest | sold by Hildir | A fancy brown dress, with beads and silver fibula brooches. Excellent for a feast! |
| ArmorDress4 | $item_chest_dress4 | Plain Blue Dress | Chest | sold by Hildir | A plain blue dress, worn over a red underdress. |
| ArmorDress5 | $item_chest_dress5 | Blue Dress with Shawl | Chest | sold by Hildir | A blue dress, with a green shawl around the shoulders. Warm and stylish! |
| ArmorDress6 | $item_chest_dress6 | Blue Dress with Beads | Chest | sold by Hildir | A fancy blue dress, with beads and bronze fibula brooches. Excellent for a feast! |
| ArmorDress7 | $item_chest_dress7 | Plain Yellow Dress | Chest | sold by Hildir | A plain yellow dress, worn over a green underdress. |
| ArmorDress8 | $item_chest_dress8 | Yellow Dress with Shawl | Chest | sold by Hildir | A yellow dress, with a purple shawl around the shoulders. Warm and stylish! |
| ArmorDress9 | $item_chest_dress9 | Yellow Dress with Beads | Chest | sold by Hildir | A fancy yellow dress, with beads and silver fibula brooches. Excellent for a feast! |
| ArmorHarvester1 | $item_chest_harvester1 | Harvest Tunic | Chest | sold by Hildir | When working the fields, it's important to dress accordingly. A shorter tunic lets in a cool breeze. |
| ArmorHarvester2 | $item_chest_harvester2 | Harvest Dress | Chest | sold by Hildir | When working the fields, it's important to dress accordingly. This long dress keeps your knees covered while you work. |
| ArmorTunic1 | $item_chest_tunic1 | Plain Blue Tunic | Chest | sold by Hildir | A plain blue tunic, brightly dyed. |
| ArmorTunic10 | $item_chest_tunic10 | Simple Undyed Tunic | Chest | sold by Hildir | A simple tunic for everyday wear. |
| ArmorTunic2 | $item_chest_tunic2 | Blue Tunic with Cape | Chest | sold by Hildir | A blue tunic, worn with a fashionable cape made from fine brown wool. |
| ArmorTunic3 | $item_chest_tunic3 | Blue Tunic with Beads | Chest | sold by Hildir | A blue tunic worn with beads and silver jewelry. Sure to attract attention from other vikings. |
| ArmorTunic4 | $item_chest_tunic4 | Plain Red Tunic | Chest | sold by Hildir | A plain red tunic, brightly dyed. |
| ArmorTunic5 | $item_chest_tunic5 | Red Tunic with Cape | Chest | sold by Hildir | A red tunic, worn with a fashionable cape made from fine green wool. |
| ArmorTunic6 | $item_chest_tunic6 | Red Tunic with Beads | Chest | sold by Hildir | A red tunic worn with beads and bronze jewelry. Sure to attract attention from other vikings. |
| ArmorTunic7 | $item_chest_tunic7 | Plain Yellow Tunic | Chest | sold by Hildir | A plain yellow tunic, brightly dyed. |
| ArmorTunic8 | $item_chest_tunic8 | Yellow Tunic with Cape | Chest | sold by Hildir | A yellow tunic, worn with a fashionable cape made from fine purple wool. |
| ArmorTunic9 | $item_chest_tunic9 | Yellow Tunic with Beads | Chest | sold by Hildir | A yellow tunic worn with beads and silver jewelry. Sure to attract attention from other vikings. |
| DvergerSuitArbalest | Iron plate armor |  | Chest | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitArbalest_Ashlands | Iron plate armor |  | Chest | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitFire | Iron plate armor |  | Chest | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitIce | Iron plate armor |  | Chest | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitSupport | Iron plate armor |  | Chest | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| FW_ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | orb | A breastplate of hammered bronze. |
| SP_ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | orb | A breastplate of hammered bronze. |
| CookedEgg | $item_egg_cooked | Cooked Egg | Consumable | egg | Sunny side up! |
| FeastAshlands | $item_feastashlands | Ashlands Gourmet Bowl | Consumable | feast |  |
| FeastBlackforest | $item_feastblackforest | Black Forest Buffet Platter | Consumable | feast |  |
| FeastDeepNorth | $item_feastdeepnorth | Northern Morning Fare | Consumable | feast |  |
| FeastMeadows | $item_feastmeadows | Whole Roasted Meadow Boar | Consumable | feast, mead |  |
| FeastMistlands | $item_feastmistlands | Mushrooms Galore á la Mistlands | Consumable | feast |  |
| FeastMountains | $item_feastmountains | Hearty Mountain Logger's Stew | Consumable | feast |  |
| FeastOceans | $item_feastoceans | Sailor's Bounty | Consumable | feast |  |
| FeastPlains | $item_feastplains | Plains Pie Picnic | Consumable | feast |  |
| FeastSwamps | $item_feastswamps | Swamp Dweller's Delight | Consumable | feast |  |
| HealthUpgrade_Bonemass | Bonemass heart |  | Consumable | quest item |  |
| HealthUpgrade_GDKing | Elder heart |  | Consumable | quest item |  |
| MeadBugRepellent | $item_mead_bugrepellent | Anti-Sting Concoction | Consumable | mead | The perfect drink for a day in the Plains. |
| MeadBzerker | $item_mead_bzerker | Berserkir Mead | Consumable | mead | Something poisonous stirs within, brewed to the point where its potential can finally be harnessed. But be careful to let the beast out... |
| MeadEitrLingering | $item_mead_eitr_lingering | Lingering Eitr Mead | Consumable | mead | Increases eitr regeneration. |
| MeadEitrMinor | $item_mead_eitr_minor | Minor Eitr Mead | Consumable | mead | Restores eitr. |
| MeadFrostResist | $item_mead_frostres | Frost Resistance Mead | Consumable | mead | Protects against the cold. |
| MeadHasty | $item_mead_hasty | Tonic of Ratatosk | Consumable | mead | The squirrel must be quick on its feet as it runs up and down the trunk of the world tree. Although it no longer visits Valheim, there are still those who reme… |
| MeadHealthLingering | $item_mead_hp_lingering | Lingering Healing Mead | Consumable | mead | Increases health regeneration. |
| MeadHealthMajor | $item_mead_hp_major | Major Healing Mead | Consumable | mead | Restores health. |
| MeadHealthMedium | $item_mead_hp_medium | Medium Healing Mead | Consumable | mead | Restores health. |
| MeadHealthMinor | $item_mead_hp_minor | Minor Healing Mead | Consumable | mead | Restores health. |
| MeadLightfoot | $item_mead_lightfoot | Lightfoot Mead | Consumable | mead | This bottle is full, yet it feels like it weighs almost nothing at all. |
| MeadPoisonResist | $item_mead_poisonres | Poison Resistance Mead | Consumable | mead | Fortifies you against poison. |
| MeadStaminaLingering | $item_mead_stamina_lingering | Lingering Stamina Mead | Consumable | mead | Increases stamina regeneration. |
| MeadStaminaMedium | $item_mead_stamina_medium | Medium Stamina Mead | Consumable | mead | Restores stamina. |
| MeadStaminaMinor | $item_mead_stamina_minor | Minor Stamina Mead | Consumable | mead | Restores stamina. |
| MeadStrength | $item_mead_strength | Mead of Troll Endurance | Consumable | mead | What creature can carry more than a troll? Why, a viking with this drink of course! |
| MeadSwimmer | $item_mead_swimmer | Draught of Vananidir | Consumable | mead | You feel invigorated just by holding this, as the ocean beckons you to come for a swim. |
| MeadTamer | $item_mead_tamer | Brew of Animal Whispers | Consumable | mead | It smells like a pigsty. Likely the taste won't be much better. |
| MeadTasty | $item_mead_tasty | Tasty Mead | Consumable | mead | The nectar of the Gods, divine mead. |
| MeadTrollPheromones | $item_mead_trollpheromones | Love Potion | Consumable | sold by BogWitch, mead | An intense musk permeates the air around this bottle. |
| MushroomBzerker | $item_mushroom_bzerker | Toadstool | Consumable | sold by BogWitch | Some say you can eat everything you find in the forest. That is not the case with this mushroom. |
| StaminaUpgrade_Greydwarf | Stamina Greydwarf |  | Consumable | quest item |  |
| StaminaUpgrade_Troll | Stamina Troll |  | Consumable | quest item |  |
| StaminaUpgrade_Wraith | Stamina Wraith |  | Consumable | quest item |  |
| DvergerHairMale | Iron plate armor |  | Helmet | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairMale_Redbeard | Iron plate armor |  | Helmet | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| HelmetCrownofValheim | $item_helmet_crown_of_valheim | Crown of Valheim | Helmet | crown | A glorious reward for the truly worthy. |
| HelmetDverger | $item_helmet_dverger | Dverger Circlet | Helmet | sold by Haldor, dvergr | A portable perpetual lightsource for the dungeon explorer. |
| HelmetHat1 | $item_helmet_hat1 | Blue Tied Headscarf | Helmet | sold by Hildir | A blue practical headscarf. |
| HelmetHat10 | $item_helmet_hat10 | Simple Purple Cap | Helmet | sold by Hildir | A simple yet fashionable purple cap. |
| HelmetHat2 | $item_helmet_hat2 | Green Twisted Headscarf | Helmet | sold by Hildir | A fancy green headscarf. |
| HelmetHat3 | $item_helmet_hat3 | Brown Fur Cap | Helmet | sold by Hildir | A warm fur cap, made from the finest leather. |
| HelmetHat4 | $item_helmet_hat4 | Extravagant Green Cap | Helmet | sold by Hildir | A warm cap for special occasions. |
| HelmetHat5 | $item_helmet_hat5 | Simple Red Cap | Helmet | sold by Hildir | A simple yet fashionable red cap. |
| HelmetHat6 | $item_helmet_hat6 | Yellow Tied Headscarf | Helmet | sold by Hildir | A practical yellow headscarf. |
| HelmetHat7 | $item_helmet_hat7 | Red Twisted Headscarf | Helmet | sold by Hildir | A fancy red headscarf. |
| HelmetHat8 | $item_helmet_hat8 | Grey Fur Cap | Helmet | sold by Hildir | A warm fur cap, made from the finest wool. |
| HelmetHat9 | $item_helmet_hat9 | Extravagant Orange Cap | Helmet | sold by Hildir | A warm cap for special occasions. |
| HelmetMidsummerCrown | $item_helmet_midsummercrown | Midsummer Crown | Helmet | crown | Celebrate summer with a crown woven from flowers. |
| HelmetRootCrown | $item_helmet_rootcrown | Crown of Roots | Helmet | sold by BogWitch, crown | A painful, yet intricate headpiece. |
| HelmetStrawHat | $item_helmet_strawhat | Straw Hat | Helmet | sold by Hildir | The perfect way to avoid sunstroke. |
| HelmetSweatBand | $item_helmet_sweatband | Headband | Helmet | sold by Hildir | It feels a bit...moist. |
| HelmetYule | $item_helmet_yule | Yule Hat | Helmet | sold by Haldor, yule | A red cap in the style of house gnomes. |
| ArmorBerserkerLegs | $item_legs_berserker | Loincloth of the Bear | Legs | orb | It covers all the important bits, but not much else. |
| ArmorBerserkerUndeadLegs | $item_legs_berserker_undead | Vilebone Drapes | Legs | orb | Thick hides and bone fragments woven into a rugged skirt. |
| ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | orb | Bronze greaves to shield your legs. |
| DvergerHairFemale | Iron plate armor |  | Legs | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairFemale_Redhair | Iron plate armor |  | Legs | dvergr | An iron scale mail, this will turn all but the strongest of blows. |
| FW_ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | orb | Bronze greaves to shield your legs. |
| SP_ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | orb | Bronze greaves to shield your legs. |
| AncientCoin | $item_ancientcoin | Ancient Coin | Material | ancient | A relic of a lost age. Its surface still bears the trace of mysterious symbols. |
| AncientGemstoneBlack | $item_ancientgemstone_black | Draumyx | Material | ancient | A dark, opaque gem with a smooth and polished surface. |
| AncientGemstoneGreen | $item_ancientgemstone_green | Grimvarn | Material | ancient | A striking green gem, the colour reminiscent of deep forests. |
| AncientGemstoneOrange | $item_ancientgemstone_orange | Solryth | Material | ancient | A vibrant orange stone, like a summer sunset. |
| AncientGemstonePurple | $item_ancientgemstone_purple | Veydris | Material | ancient | A rich purple stone, suitable for royalty. |
| BellFragment | $item_bellfragment | Bell Fragment | Material | bell | This ancient fragment appears to be a piece of a broken bell... |
| BlobVial | $item_blobvial | Corked Vial | Material | sold by BogWitch | Thick enough to contain something volatile, yet fragile enough to be shattered. |
| CandleWick | $item_candlewick | Candle Wick | Material | sold by BogWitch | Steep these in something flammable for a long lasting and cosy light source. |
| CrownJewel | $item_crownjewel | Crown Jewel | Material | drop of FrozenKing_p3, crown | A strange power surges within this gem, unlike anything you've felt before. |
| CuredSquirrelHamstring | $item_curedsquirrelhamstring | Cured Squirrel Hamstring | Material | sold by BogWitch | Elastic and strong. This tendon must have come from a quick and agile animal. |
| DvergrKeyFragment | $item_dvergrkeyfragment | Sealbreaker Fragment | Material | key | A fragment of a Dvergr sealbreaker. |
| FaderDrop | $item_fader_drop | Kindled Ribs | Material | drop of Fader, fader | The smouldering remains of a patriarch. |
| FaderEmber | $item_faderember | Embers | Material | fader | Every flying ember is a burning wish to repent. |
| FeastAshlands_Material | $item_feastashlands | Ashlands Gourmet Bowl | Material | feast | It's hard to tell whether the steam coming off of this dish is because it's freshly cooked or because of the asksvin meat in it. Either way, the spiced meat to… |
| FeastBlackforest_Material | $item_feastblackforest | Black Forest Buffet Platter | Material | feast | You won't be able to resist this platter of delights from the Black Forest! Venison sirloin steaks are served together with spiced thistles and carrots, and th… |
| FeastDeepNorth_Material | $item_feastdeepnorth | Northern Morning Fare | Material | feast | Warming and filling, this meal will sustain you even during the coldest of days. Porridge and pancakes pair well with jams and sausages, making it hard not to… |
| FeastMeadows_Material | $item_feastmeadows | Whole Roasted Meadow Boar | Material | feast, mead | A boar that has been roasted to perfection, glazed and served atop a bed of greens, with additional cuts of meat on the side. A feast like this is sure to fill… |
| FeastMistlands_Material | $item_feastmistlands | Mushrooms Galore á la Mistlands | Material | feast | The time has come for mushroom enthusiasts to rejoice! Try different kinds of mushrooms, mushroom marinated seeker meat and mushroom-filled misthare, and don't… |
| FeastMountains_Material | $item_feastmountains | Hearty Mountain Logger's Stew | Material | feast | Gather around this steaming pot full of deliciousness and warm yourselves up again after a day out in the cold. The usually tough and chewey wolf meat has beco… |
| FeastOceans_Material | $item_feastoceans | Sailor's Bounty | Material | feast | Fish, fish, and more fish! And also serpent meat, cut to look like fish! Explore the flavours of the Ocean, along with some grilled greens for those still prac… |
| FeastPlains_Material | $item_feastplains | Plains Pie Picnic | Material | feast | There's nothing plain about this feast! Enjoy pies and loaves fresh from the oven, both sweet and savoury, and experience the culinary equivalent of a hug. |
| FeastSwamps_Material | $item_feastswamps | Swamp Dweller's Delight | Material | feast | Who knew that leeches were edible? With the correct preparation (lots of cooking and lots of seasoning) you will be able to serve them in this feast for the cu… |
| FireworksRocket_White | $item_fireworkrocket_white | Basic Fireworks | Material | sold by Hildir | This rocket's blasting off again! |
| FragrantBundle | $item_fragrantbundle | Fragrant Bundle | Material | sold by BogWitch | These plants carry a strong but pleasant scent. However, it's possible that not all creatures agree... |
| FreezeGland | $item_freezegland | Freeze Gland | Material | drop of TheHive | This mysterious organ keeps a perfect temperature. |
| FreshSeaweed | $item_freshseaweed | Fresh Seaweed | Material | sold by BogWitch | The saltwater scent of this plant makes you think of the wide open ocean. |
| FrozenKingDrop | $item_frozenking_drop | Sacrificial Blood | Material | drop of FrozenKing_p3, frozen king | The last essence of an end once foretold. |
| HardAntler | $item_hardantler | Hard Antler | Material | drop of Eikthyr | A piece of very hard antlers. |
| Ironpit | $item_ironpit | Iron Pit | Material | sold by Hildir | An empty vessel waiting to be filled with firewood and kindling. |
| KeysGoldUncooked | $item_keys_gold_uncooked | Cast: Intricate Key | Material | key | The keys need to be hardened with frost. |
| MeadBaseBugRepellent | $item_meadbasebugrepellent | Mead Base: Anti-Sting | Material | mead | Needs to be fermented. |
| MeadBaseBzerker | $item_meadbasebzerker | Mead base: Berserkir | Material | mead | Needs to be fermented. |
| MeadBaseEitrLingering | $item_meadbaseeitr_lingering | Mead Base: Lingering Eitr | Material | mead | Needs to be fermented. |
| MeadBaseEitrMinor | $item_meadbaseeitr | Mead Base: Minor Eitr | Material | mead | Needs to be fermented. |
| MeadBaseFrostResist | $item_meadbasefrostresist | Mead Base: Frost Resistance | Material | mead | Needs to be fermented. |
| MeadBaseHasty | $item_meadbasehasty | Mead base: Ratatosk | Material | mead | Needs to be fermented. |
| MeadBaseHealthLingering | $item_meadbasehealth_lingering | Mead Base: Lingering Health | Material | mead | Needs to be fermented. |
| MeadBaseHealthMajor | $item_meadbasehealth_major | Mead Base: Major Healing | Material | mead | Needs to be fermented. |
| MeadBaseHealthMedium | $item_meadbasehealth_medium | Mead Base: Medium Healing | Material | mead | Needs to be fermented. |
| MeadBaseHealthMinor | $item_meadbasehealth | Mead Base: Minor Healing | Material | mead | Needs to be fermented. |
| MeadBaseLightFoot | $item_meadbaselightfoot | Mead Base: Lightfoot | Material | mead | Needs to be fermented. |
| MeadBasePoisonResist | $item_meadbasepoisonresist | Mead Base: Poison Resistance | Material | mead | Needs to be fermented. |
| MeadBaseStaminaLingering | $item_meadbasestamina_lingering | Mead Base: Lingering Stamina | Material | mead | Needs to be fermented. |
| MeadBaseStaminaMedium | $item_meadbasestamina_medium | Mead Base: Medium Stamina | Material | mead | Needs to be fermented. |
| MeadBaseStaminaMinor | $item_meadbasestamina | Mead Base: Minor Stamina | Material | mead | Needs to be fermented. |
| MeadBaseStrength | $item_meadbasestrength | Mead Base: Troll Endurance | Material | mead | Needs to be fermented. |
| MeadBaseSwimmer | $item_meadbaseswimmer | Mead Base: Vananidir | Material | mead | Needs to be fermented. |
| MeadBaseTamer | $item_meadbasetamer | Mead Base: Animal Whispers | Material | mead | Needs to be fermented. |
| MeadBaseTasty | $item_meadbasetasty | Mead Base: Tasty | Material | mead | Needs to be fermented. |
| MoldKeys | $item_mold_keys | Mould: Intricate Key | Material | key | Filled with the right material, this mould will create a powerful key. |
| MoldStaffOrbofAhri | $item_mold_stafforbofahri | Mould: Echo Spike | Material | orb, staff | Filled with the right material, this mould will create a powerful magical item. |
| MoldStafffrostorbs | $item_mold_stafffrostorbs | Mould: Northern Vengeance | Material | orb, staff | Filled with the right material, this mould will create a powerful magical item. |
| MoldStaffspiritcaller | $item_mold_staffspiritcaller | Mould: Spirit Caller | Material | staff, spirit | Filled with the right material, this mould will create a powerful magical item. |
| MoldStaffthunderblood | $item_mold_staffthunderblood | Mould: Lightning Strike | Material | staff, thunderstone | Filled with the right material, this mould will create a powerful magical item. |
| OrbFrostFire | $item_orbfrostfire | Frostfire Essence | Material | orb | Somehow both hot and cold to the touch. |
| OrbThunderBlood | $item_orbthunderblood | Thunderblood Essence | Material | orb, thunderstone | Unstable, erratic and...alive? |
| Pot_Shard_Green | $item_pot_shard_green | Pot Shard | Material | shard | A fragment of something brittle. |
| Pot_Shard_Red | $item_pot_shard_red |  | Material | shard | A fragment of something brittle. |
| PowderedDragonEgg | $item_powdereddragonegg | Powdered Dragon Eggshells | Material | sold by BogWitch, egg | Dragon egg is a hard and difficult material to work with, yet here it has been ground to a fine, glittering dust... |
| PungentPebbles | $item_pungentpebbles | Pungent Pebbles | Material | sold by BogWitch | An earthy odour clings to these dried lumps. It's best to not think too hard about what they are. |
| QueenDrop | $item_seekerqueen_drop | Majestic Carapace | Material | drop of Hive | Her majesty's will was hard and unrelenting, but this piece of carapace is perhaps even more so. |
| SpiceAshlands | $item_spiceashlands | Fiery Spice Powder | Material | sold by BogWitch | Whatever spices have been used in this blend, they must come from someplace hot. And as if that wasn't enough, they have also been dried and smoked before bein… |
| SpiceDeepNorth | $item_spicedeepnorth | Seasoning of the Gourd | Material | sold by BogWitch | Using cinnamon bark and ginger root, with a touch of nutmeg, the herbalist has travelled far to create this fine and warming blend of spices. |
| SpiceForests | $item_spiceforests | Woodland Herb Blend | Material | sold by BogWitch | Thyme and marjoram carefully harvested and dried, before being mixed together. This blend will turn even the most basic of ingredients into a delicious feast. |
| SpiceMistlands | $item_spicemistlands | Herbs of the Hidden Hills | Material | sold by BogWitch | A good herbalist knows exactly where to go to find ramsons, but she won't tell you the secrets of her trade! Still, if you have the coin for it you may treat y… |
| SpiceMountains | $item_spicemountains | Mountain Peak Pepper Powder | Material | sold by BogWitch | Only the most seasoned herbalist can find the pepperwood tree and harvest its leaves and bark. After careful preparation it's ready to be added to food, and is… |
| SpiceOceans | $item_spiceoceans | Seafarer's Herbs | Material | sold by BogWitch | The sour tang of sorrel, mixed with something the herbalist would rather not disclose, is well suitable for fish. Any fisherfolk worth their salt would do well… |
| SpicePlains | $item_spiceplains | Grasslands Herbalist Harvest | Material | sold by BogWitch | This blend utilises all aspects of the lovage plant, a rarity in the tenth world. Roots, leaves and seeds have all been gathered, and then mixed together in th… |
| StaffFrostOrbsUncooked | $item_frostorbs_uncooked | Cast: Northern Vengeance | Material | orb, staff | This magical item needs to be hardened with frost. |
| StaffOrbofAhriUncooked | $item_staff_orbofahri_uncooked | Cast: Echo Spike | Material | orb, staff | This magical item needs to be hardened with frost. |
| StaffSpiritCallerUncooked | $item_staff_spiritcaller_uncooked | Cast: Spirit Caller | Material | staff, spirit | This magical item needs to be hardened with frost. |
| StaffThunderbloodUncooked | $item_staff_thunderblood_uncooked | Cast: Lightning Strike | Material | staff, thunderstone | This magical item needs to be hardened with frost. |
| Thunderstone | $item_thunderstone | Thunder Stone | Material | sold by Haldor, thunderstone | It is crackling with energy. |
| VegvisirShard_Bonemass | Yagluth thing |  | Material | shard |  |
| VineGreenSeeds | $item_vinegreenseeds | Ivy Seeds | Material | sold by BogWitch | These unassuming seeds can grow into vines that might overtake entire buildings. |
| YmirRemains | $item_ymirremains | Ymir Flesh | Material | sold by Haldor, ymir | The earthy remains of the giant Ymir. |
| AncientSeed | $item_ancientseed | Ancient Seed | Misc | ancient | Held against your ear, you hear tiny whisperings within... |
| AsksvinEgg | $item_asksvin_egg | Asksvin Egg | Misc | egg | Hard as rock, yet you can sense the presence of something inside. This should be kept warm. |
| BarberKit | $item_barberkit | Barber Kit | Misc | sold by Hildir | A kit fit for the finest of barbers. |
| BarrelRings | $item_barrelrings | Barrel Hoops | Misc | sold by Haldor | These metal rings are perfectly round, suitable for holding a barrel together. Just add wood! |
| Bell | $item_bell | Bell | Misc | bell | For whom does the bell toll? |
| BloodGoldKey | $item_bloodgoldkey | Intricate Key | Misc | key | If there's a key, then surely there must be a lock. |
| ChickenEgg | $item_chicken_egg | Egg | Misc | sold by Haldor, egg | Keep it warm to see what comes out.. but what came first, really? |
| CryptKey | $item_cryptkey | Swamp Key | Misc | drop of gd_king, key | Partly covered in caked mud, it smells foetid. |
| DragonEgg | $item_dragonegg | Dragon Egg | Misc | egg | Far heavier than it looks, with a faint humming sound from within. |
| DragonTear | $item_dragontear | Dragon Tear | Misc | drop of Dragon | The last frozen tear of a dragon, pulsating with mysterious energy. |
| DvergrKey | $item_dvergrkey | Sealbreaker | Misc | key | An object used to break a Dverger seal. |
| GoblinTotem | $item_goblintotem | Fuling Totem | Misc | totem | Channels the ancient power of Yagluth. |
| HildirKey_forestcrypt | $item_hildirkey1 | Hildir's Brass Key | Misc | key | It seems to be missing its owner... |
| HildirKey_mountaincave | $item_hildirkey2 | Hildir's Silver Key | Misc | key | It seems to be missing its owner... |
| HildirKey_plainsfortress | $item_hildirkey3 | Hildir's Bronze Key | Misc | key | It seems to be missing its owner... |
| SaddleAsksvin | $item_saddleasksvin | Asksvin Saddle | Misc | saddle | The back of an asksvin is rather lumpy, so you'll need a saddle if you want to ride one. |
| SaddleLox | $item_saddlelox | Lox Saddle | Misc | saddle | Use on a lox to be able to ride it. |
| SaddleMoose | $item_saddlemoose | Moose Saddle | Misc | saddle | A moose is a noble creature, but with a saddle this fine it might just allow a rider. |
| ScytheHandle | $item_scythehandle | Scythe Handle | Misc | sold by BogWitch | A sturdy base for a tool. |
| VoltureEgg | $item_voltureegg | Volture Egg | Misc | egg | Warm to the touch, and full of protein. |
| YagluthDrop | $item_yagluththing | Torn Spirit | Misc | drop of GoblinKing | The remains of Yagluth, a twisted spirit torn between this world and the next. |
| DvergerArbalest_shoot | $item_crossbow_arbalest | Arbalest | OneHanded | dvergr | A slow but powerful weapon. |
| DvergerArbalest_shootAshlands | $item_crossbow_arbalest | Arbalest | OneHanded | dvergr | A slow but powerful weapon. |
| DvergerArbalest_shootDeepNorth | $item_crossbow_arbalest | Arbalest | OneHanded | dvergr | A slow but powerful weapon. |
| DvergerMistile | Club |  | OneHanded | dvergr | A crude but useful weapon. |
| DvergerStaffBlocker | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffFire_clusterbomb | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffFire_fireball | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffHeal_heal | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffIce_icebolt | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffNova | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| DvergerStaffSupport_buff | Club |  | OneHanded | dvergr, staff | A crude but useful weapon. |
| Dverger_melee | Club |  | OneHanded | dvergr | A crude but useful weapon. |
| Dverger_meleeAshlands | Club |  | OneHanded | dvergr | A crude but useful weapon. |
| Dverger_meleeDeepNorth | Club |  | OneHanded | dvergr | A crude but useful weapon. |
| Elaking_AttackLantern | Torch |  | OneHanded | lantern | It brings light and warmth, drives back the darkness. |
| Fader_Bite | Fader Bite |  | OneHanded | fader |  |
| Fader_Claw_Left | Fader Claw Left |  | OneHanded | fader |  |
| Fader_Claw_Right | Fader Claw Right |  | OneHanded | fader |  |
| Fader_Fissure | Fader Fissure |  | OneHanded | fader |  |
| Fader_Fissure_Intense | Fader Fissure |  | OneHanded | fader |  |
| Fader_Flamebreath | Fader Firebreath |  | OneHanded | fader |  |
| Fader_Jump | Fader Jump |  | OneHanded | fader |  |
| Fader_Jump_Left | Fader Jump |  | OneHanded | fader |  |
| Fader_Jump_Right | Fader Jump |  | OneHanded | fader |  |
| Fader_Meteors | spawn |  | OneHanded | fader |  |
| Fader_Meteors_Intense | spawn |  | OneHanded | fader |  |
| Fader_Roar | Fader Roar |  | OneHanded | fader |  |
| Fader_Roar_Intense | Fader Roar |  | OneHanded | fader |  |
| Fader_Spin | Fader Spin |  | OneHanded | fader |  |
| Fader_Taunt | Fader Taunt |  | OneHanded | fader |  |
| Fader_WallOfFire | Fader Wall of Fire |  | OneHanded | fader |  |
| FrozenKing_ChainFlurry | FrozenKing ChainFlurry |  | OneHanded | frozen king |  |
| FrozenKing_ChainRush | FrozenKing ChainRush |  | OneHanded | frozen king |  |
| FrozenKing_ChainSlam_L | FrozenKing ChainSlam L |  | OneHanded | frozen king |  |
| FrozenKing_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | frozen king |  |
| FrozenKing_ChainSlam_R | FrozenKing ChainSlam R |  | OneHanded | frozen king |  |
| FrozenKing_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | frozen king |  |
| FrozenKing_ChainSweep_L | FrozenKing ChainSweep L |  | OneHanded | frozen king |  |
| FrozenKing_ChainSweep_R | FrozenKing ChainSweep R |  | OneHanded | frozen king |  |
| FrozenKing_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | frozen king |  |
| FrozenKing_DoubleSweep | FrozenKing DoubleSweep |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Bonemass | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Eikthyr | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Elder | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Fader | Fader Roar |  | OneHanded | fader, frozen king |  |
| FrozenKing_P2_Summon_Moder | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Queen | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P2_Summon_Yagluth | Fader Roar |  | OneHanded | frozen king |  |
| FrozenKing_P3_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | frozen king |  |
| FrozenKing_P3_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | frozen king |  |
| FrozenKing_P3_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | frozen king |  |
| FrozenKing_Punch_AOE | FrozenKing Punch AOE |  | OneHanded | frozen king |  |
| FrozenKing_SpikeRain | spawn |  | OneHanded | frozen king |  |
| FrozenKing_tendrilspawn | spawn |  | OneHanded | frozen king |  |
| SpiritWolf_Attack1 | WolfAttack1 |  | OneHanded | spirit |  |
| SpiritWolf_Attack2 | WolfAttack2 |  | OneHanded | spirit |  |
| SpiritWolf_Attack3 | WolfAttack3 |  | OneHanded | spirit |  |
| aspect_Fader_Bite | Fader Bite |  | OneHanded | fader |  |
| aspect_Fader_Claw_Left | Fader Claw Left |  | OneHanded | fader |  |
| aspect_Fader_Claw_Right | Fader Claw Right |  | OneHanded | fader |  |
| aspect_Fader_Fissure | Fader Fissure |  | OneHanded | fader |  |
| aspect_Fader_Flamebreath | Fader Firebreath |  | OneHanded | fader |  |
| aspect_Fader_Spin | Fader Spin |  | OneHanded | fader |  |
| aspect_Fader_WallOfFire | Fader Wall of Fire |  | OneHanded | fader |  |
| gjall_attack_egg | egg drop |  | OneHanded | egg |  |
| skeleton_sword_meadows | Dragur axe |  | OneHanded | mead |  |
| spiritbjorn_bite | bjorn bite |  | OneHanded | spirit |  |
| spiritbjorn_claws | bjorn bite |  | OneHanded | spirit |  |
| spiritbjorn_slam | slap |  | OneHanded | spirit |  |
| spiritbjorn_swipe_combo | bjorn bite |  | OneHanded | spirit |  |
| spiritbjorn_swipe_l | bjorn bite |  | OneHanded | spirit |  |
| spiritbjorn_swipe_r | bjorn bite |  | OneHanded | spirit |  |
| spiritboar_base_attack | boar attack1 |  | OneHanded | spirit |  |
| spiritmoose_hooves | moose horns |  | OneHanded | spirit |  |
| spiritmoose_horns | moose horns |  | OneHanded | spirit |  |
| spiritmoose_horns_sweep | moose horns |  | OneHanded | spirit |  |
| staff_greenroots_tentaroot_attack | Dragur axe |  | OneHanded | staff |  |
| CapeAsh | $item_cape_ash | Ashen Cape | Shoulder | cape | Thin metal threads are woven into this cape to create an intricate pattern, like a destiny woven by the Norns themselves. |
| CapeAsksvin | $item_cape_asksvin | Asksvin Cloak | Shoulder | cape | This thick cape catches the wind, not unlike the sail of a ship. |
| CapeDeepNorth | $item_cape_deepnorth | Moose Hide Cape | Shoulder | cape | A warm cape with fine details of spun gold. |
| CapeDeepNorthMage | $item_cape_deepnorth_mage | Cape of the Caller | Shoulder | cape | A strange magic is woven into this cape, making it both light and warm. |
| CapeDeerHide | $item_cape_deerhide | Deer Hide Cape | Shoulder | cape | Rustic chic. |
| CapeFeather | $item_cape_feather | Feather Cape | Shoulder | cape | Donning this cape makes you feel lighter, almost as if you could fly! |
| CapeLinen | $item_cape_linen | Linen Cape | Shoulder | cape | A simple traveler's cape. |
| CapeLox | $item_cape_lox | Lox Cape | Shoulder | cape | A pelt from one of the great beasts, thick and warm. |
| CapeOdin | $item_cape_odin | Cape of Oden | Shoulder | cape | Oden's finest warriors deserve the finest cloth. |
| CapeTest | CAPE TEST |  | Shoulder | cape |  |
| CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | cape | Trollskin is tough and supple. |
| CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | cape | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| FW_CapeLinen | $item_cape_linen | Linen Cape | Shoulder | cape | A simple traveler's cape. |
| FW_CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | cape | Trollskin is tough and supple. |
| FW_CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | cape | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| SP_CapeLinen | $item_cape_linen | Linen Cape | Shoulder | cape | A simple traveler's cape. |
| SP_CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | cape | Trollskin is tough and supple. |
| SP_CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | cape | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| Feaster | $item_feaster | Serving Tray | Tool | sold by BogWitch, feast | Set the table with whatever food and drink you fancy, and impress your guests with a delicious feast. / / <color=yellow>Once a feast has been eaten of, it won'… |
| Lantern | $item_lantern | Dvergr Lantern | Torch | lantern | A simple torch would just be so old fashioned. |
| Lantern_DN | $item_lanternDN | Salvaged Lantern | Torch | lantern | An ancient relic, dropped and forgotten by someone long gone. |
| Lantern_hooded | $piece_hoodedlantern | Hooded Lantern | Torch | lantern | A simple torch would just be so old fashioned. |
| Sparkler | $item_sparkler | Sparkler | Torch | sold by Hildir | It's a stick that sparkles. Pretty! |
| TrinketBlackDamageHealth | $item_trinketblackdamagedealth | Bracelets of the Brave | Trinket | trinket | Your mind hardens, as do your blows. |
| TrinketBlackStamina | $item_trinketblackdtamina | Evasion Mantle | Trinket | trinket | Dance with death as you dodge your enemies' strikes. |
| TrinketBloodGoldHealth | $item_trinketbloodgoldhealth | Neckstabber | Trinket | trinket | Claw and bone and gold – grant them blood and they shall grant you a boon. |
| TrinketBloodGoldStamina | $item_trinketbloodgoldstamina | Witch Crown | Trinket | trinket | They say that the soul of a witch can grant strange powers to mortals... |
| TrinketBronzeHealth | $item_trinketbronzehealth | Heart of the Forest | Trinket | trinket | It pulsates with fragments of ancient life. |
| TrinketBronzeStamina | $item_trinketbronzestamina | Bronze Pendant | Trinket | trinket | A beautiful pendant, harbouring the endurance of a bear. |
| TrinketCarapaceEitr | $item_trinketcarapaceeitr | Pulsating Earrings | Trinket | trinket | If you listen carefully, you can hear the faint echoes of lost souls... |
| TrinketChitinSwim | $item_trinketchitinswim | Fins of Destiny | Trinket | trinket | Empty your mind as you become shapeless and one with the water. |
| TrinketFlametalEitr | $item_trinketflametaleitr | Jörmundling | Trinket | trinket | Tormented screams resonate from within. |
| TrinketFlametalStaminaHealth | $item_trinketflametalstaminahealth | Brimstone | Trinket | trinket | It's warm, as if filled with a lifesblood of its own. |
| TrinketIronHealth | $item_trinketironhealth | Iron Brooch | Trinket | trinket | A delicate yet defensive accessory. |
| TrinketIronStamina | $item_trinketironstamina | Nimble Anklet | Trinket | trinket | Puts a spring in your step! |
| TrinketScaleStaminaDamage | $item_trinketscalestaminadamage | Resounding Shackle | Trinket | trinket | A razor-sharp ankle chain. Can it truly be comfortable? |
| TrinketSilverDamage | $item_trinketsilverdamage | Wolf Sight | Trinket | trinket | Assume the sharp and furious mind of a wolf. |
| TrinketSilverResist | $item_trinketsilverresist | Crystal Heart | Trinket | trinket | A shard of frozen sorrow. Touching it makes you feel almost numb. |
| FW_StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | staff | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| FW_StaffLightning | $item_staff_lightning | Dundr | TwoHanded | staff | What happens next may shock you. |
| FishingRod | $item_fishingrod | Fishing Rod | TwoHanded | sold by Haldor, fishing | Standard issue dvergr fishing rod. |
| SP_StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | staff | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| SP_StaffLightning | $item_staff_lightning | Dundr | TwoHanded | staff | What happens next may shock you. |
| StaffClusterbomb | $item_staffclusterbomb | Staff of Fracturing | TwoHanded | staff | Only those with patience and focus will be able to harness the true power of this staff. |
| StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | staff | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| StaffGreenRoots | $item_staffgreenroots | Staff of the Wild | TwoHanded | staff | Ancient natural forces lie curled and dormant within this staff, ready to be unleashed. |
| StaffIceShards | $item_stafficeshards | Staff of Frost | TwoHanded | staff, shard | A staff as cold as the three-year winter that will herald the end of times. |
| StaffLightning | $item_staff_lightning | Dundr | TwoHanded | staff | What happens next may shock you. |
| StaffOrbofAhri | $item_staff_orbofahri | Echo Spike | TwoHanded | orb, staff | A chill that goes right through to the bone. |
| StaffRedTroll | $item_staffredtroll | Trollstav | TwoHanded | staff | Summons a raging beast to cause death and destruction. |
| StaffShield | $item_staffshield | Staff of Protection | TwoHanded | staff | For a slight blood offering it will protect the caster in a magical shell. |
| StaffThunderBlood | $item_staff_thunderblood | Lightning Strike | TwoHanded | staff, thunderstone | Simply point, and you shall summon the wrath of the sky. |
| charred_fader_greatsword_feint | Charred Sword |  | TwoHanded | fader |  |
| charred_fader_greatsword_swing | Charred Sword |  | TwoHanded | fader |  |
| charred_fader_greatsword_thrust | Charred Sword |  | TwoHanded | fader |  |
| charred_fader_greatsword_thrustfeint | Charred Sword |  | TwoHanded | fader |  |
| charred_magestaff_fire | Bow |  | TwoHanded | staff |  |
| charred_magestaff_summon | Bow |  | TwoHanded | staff |  |
| StaffFrostOrbs | $item_staff_frostorbs | Northern Vengeance | TwoHandedLeft | orb, staff | A caged snowflake, endless patterns emerging from within... |
| StaffSkeleton | $item_staffskeleton | Dead Raiser | TwoHandedLeft | staff | Sacrifice a bit of blood to raise the dead. Upgrade the skull to spawn multiple skeletons, and increase your blood magic to make them stronger. |
| StaffSpiritCaller | $item_staff_spiritcaller | Spirit Caller | TwoHandedLeft | staff, spirit | Summon otherworldly aid. It only costs a drop of your blood... |
| BeltStrength | $item_beltstrength | Megingjord | Utility | sold by Haldor, utility, belt | Gives the wearer superhuman strength. |
| Demister | $item_demister | Wisplight | Utility | utility, demister | A bound wisp to guide you through the thickest of mists. |
| DvergerArbalest | $item_crossbow_arbalest | Arbalest | Utility | utility, dvergr | A slow but powerful weapon. |
| DvergerStaffFire | Club |  | Utility | utility, dvergr, staff | A crude but useful weapon. |
| DvergerStaffHeal | Club |  | Utility | utility, dvergr, staff | A crude but useful weapon. |
| DvergerStaffIce | Club |  | Utility | utility, dvergr, staff | A crude but useful weapon. |
| DvergerStaffSupport | Club |  | Utility | utility, dvergr, staff | A crude but useful weapon. |
| GoblinBrute_LegBones | Iron plate armor |  | Utility | utility | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinShaman_Staff_Bones | Club |  | Utility | utility, staff | A crude but useful weapon. |
| GoblinShaman_Staff_Feathers | Club |  | Utility | utility, staff | A crude but useful weapon. |
| GoblinShaman_Staff_Hildir | Club |  | Utility | utility, staff | A crude but useful weapon. |
| IceShoes | $item_iceshoes |  | Utility | utility |  |
| IceSkates | $item_iceskates |  | Utility | utility |  |
| Wishbone | $item_wishbone | Wishbone | Utility | drop of Bonemass, utility, wishbone | This ancient bone remembers the location of many forgotten things. |

### 5b. Trophies

| prefab | token | English | description |
|---|---|---|---|
| TrophyAbomination | $item_trophy_abomination | Abomination Trophy | A tangled mess of roots and bark. |
| TrophyAsksvin | $item_trophy_asksvin | Asksvin Trophy | Don't let yourself be fooled by the friendly smile, for it could easily bite your arm off. |
| TrophyBarka | $item_trophy_barka | Barka Trophy | The frozen head of a once-living tree. |
| TrophyBjorn | $item_trophy_bjorn | Bear Trophy | That stare is still frightening... |
| TrophyBjornUndead | $item_trophy_bjorn_undead | Vile Trophy | These eyes are finally vacant for good. |
| TrophyBlob | $item_trophy_blob | Blob Trophy | A smelly lump of sticky matter. |
| TrophyBlob_Frost | $item_trophy_blob_frost | Frost Blob Trophy | It's like a shard of ice, only...slimy? |
| TrophyBlob_Lava | $item_trophy_blob_lava | Lava Blob Trophy | It's probably dead, right? |
| TrophyBlob_Morkhalla | $item_trophy_blob_morkhalla | Pulp Trophy | Remains of unfortunate adventurers, digested and jellified over time. |
| TrophyBoar | $item_trophy_boar | Boar Trophy | This boar head would make for a nice decoration in any house. |
| TrophyBonemass | $item_trophy_bonemass | Bonemass Trophy | Bones and viscous goo, held together by some unseen force. / / Offer it to the Sacrificial Stones. |
| TrophyBonemawSerpent | $item_trophy_bonemaw | Bonemaw Trophy | A skull made up of dense bone, as dangerous as it is protective. |
| TrophyCharredArcher | $item_trophy_charredarcher | Marksman Trophy | These legs could hold infinite power. |
| TrophyCharredMage | $item_trophy_charredmage | Warlock Trophy | A warm glow seems to almost emanate from within. Handle with care. |
| TrophyCharredMelee | $item_trophy_charredmelee | Warrior Trophy | Fractures line this skull, as if it has taken many hits over the years. |
| TrophyCultist | $item_trophy_cultist | Cultist Trophy | My, what big teeth it has... |
| TrophyCultist_Hildir | $item_trophy_cultist_hildir | Geirrhafa Trophy | He's giving you an icy stare. |
| TrophyDeathsquito | $item_trophy_deathsquito | Deathsquito Trophy | You don't like touching this thing even when it's dead. |
| TrophyDeer | $item_trophy_deer | Deer Trophy | A fine specimen, but you'll need to kill more than deer to enter Valhalla. |
| TrophyDeerWhite | $item_trophy_deer_white |  | A fine specimen, but you'll need to kill more than deer to enter Valhalla. |
| TrophyDragonQueen | $item_trophy_dragonqueen | Moder Trophy | The head of a dragon, majestic even in the rigor of death. / / Offer it to the Sacrificial Stones. |
| TrophyDraugr | $item_trophy_draugr | Draugr Trophy | Bind up the mouth if it starts to whisper in the night... |
| TrophyDraugrElite | $item_trophy_draugrelite | Draugr Elite Trophy | The dead stare of the glowing red eyes sends a shiver through your bones. |
| TrophyDraugrFem | $item_trophy_draugr | Draugr Trophy | Bind up the mouth if it starts to whisper in the night... |
| TrophyDvergr | $item_trophy_dvergr | Dvergr Trophy | It's frankly a little troubling that you would consider hanging these on your wall... |
| TrophyEikthyr | $item_trophy_eikthyr | Eikthyr Trophy | This severed head oozes power. / / Offer it to the Sacrificial Stones. |
| TrophyElaking | $item_trophy_elaking | Elaking Trophy | Mean little eyes stare back at you. |
| TrophyFader | $item_trophy_fader | Fader Trophy | The green dragon, corrupted beyond redemption. / / Offer him to the sacrificial stones. |
| TrophyFallenValkyrie | $item_trophy_fallenvalkyrie | Fallen Valkyrie Trophy | Though she is dead, she yearns for the blood to flow. |
| TrophyFenring | $item_trophy_fenring | Fenring Trophy | A strange, elongated paw, its claws razor sharp. |
| TrophyForestTroll | $item_trophy_troll | Troll Trophy | The leathery skin bears the faded tracery of ancient symbols. |
| TrophyFrostTroll | $item_trophy_troll | Troll Trophy | The leathery skin bears the faded tracery of ancient symbols. |
| TrophyGhost | $item_trophy_ghost | Ghost Trophy | Does it still whisper about unfinished business? |
| TrophyGjall | $item_trophy_gjall | Gjall Trophy | Hopefully it won't float away. |
| TrophyGoblin | $item_trophy_goblin | Fuling Trophy | Loose folds of greenish skin gathered in around a pair of dark and hateful eyes. |
| TrophyGoblinBrute | $item_trophy_goblinbrute | Fuling Berserker Trophy | The huge grizzled head is as heavy as a boulder. |
| TrophyGoblinBruteBrosBrute | $item_trophy_brutebro | Thungr Trophy | Not so tough now. |
| TrophyGoblinBruteBrosShaman | $item_trophy_shamanbro | Zil Trophy | In the choice of 'ride or die', he picked the latter. |
| TrophyGoblinKing | $item_trophy_goblinking | Yagluth Trophy | The crownless head of a dead king. / / Offer it to the Sacrificial Stones. |
| TrophyGoblinShaman | $item_trophy_goblinshaman | Fuling Shaman Trophy | It shall cast no more spells against you. |
| TrophyGreydwarf | $item_trophy_greydwarf | Greydwarf Trophy | The mossy, severed head of a Greydwarf. |
| TrophyGreydwarfBrute | $item_trophy_greydwarfbrute | Greydwarf Brute Trophy | It took seven blows to hack this gnarled head from its body. |
| TrophyGreydwarfShaman | $item_trophy_greydwarfshaman | Greydwarf Shaman Trophy | It may try to come back so be sure to prune any new shoots... |
| TrophyGrowth | $item_trophy_growth | Growth Trophy | A black and sticky mess. |
| TrophyHare | $item_trophy_hare | Hare Trophy | These are said to bring luck. But not for their original owner. |
| TrophyHatchling | $item_trophy_hatchling | Drake Trophy | Still cold to the touch. |
| TrophyJotunWarrior | $item_trophy_jotunwarrior | Krigen Trophy | Once a mighty warrior, now nought but a husk remains. |
| TrophyJotunWitch | $item_trophy_jotunwitch | Hexen Trophy | Before her death, her eyes sparked with magic. Now they're empty and void. |
| TrophyKvastur | $enemy_kvastur | Kvastur | A witch's best friend. |
| TrophyLeech | $item_trophy_leech | Leech Trophy | Although slimy, the skin is beautifully patterned in red and black. |
| TrophyLox | $item_trophy_lox | Lox Trophy | A giant beast's head, thatched with thick fur. |
| TrophyMole | $item_trophy_mole | Eyeless One Trophy | Getting slashed by these claws would be very unpleasant. |
| TrophyMoose | $item_trophy_moose | Moose Trophy | The mighty ruler of the northern forests. |
| TrophyMorgen | $item_trophy_morgen | Morgen Trophy | The waking nightmare has met its end. |
| TrophyNeck | $item_trophy_neck | Neck Trophy | The beady eyes and razor sharp teeth belie the ostensibly calm nature of this small lizard. |
| TrophySGolem | $item_trophy_sgolem | Stone Golem Trophy | This crystalline rock formation would make for an impressive floor decoration. |
| TrophySeal | $item_trophy_seal | Seal Trophy | This animal never did any harm, yet it met an untimely end. |
| TrophySeeker | $item_trophy_seeker | Seeker Trophy | Less delicate than they look. The leather of the wings catches the firelight as if remembering flight. |
| TrophySeekerBrute | $item_trophy_seeker_brute | Seeker Soldier Trophy | The head of a fallen champion. |
| TrophySeekerQueen | $item_trophy_seekerqueen | The Queen Trophy | She has seen enough. / / Offer it to the Sacrificial Stones. |
| TrophySerpent | $item_trophy_serpent | Serpent Trophy | The scales have dulled but the eyes are still bright. |
| TrophySkeleton | $item_trophy_skeleton | Skeleton Trophy | The expressionless grin of this skull reminds you of the inevitability of death. |
| TrophySkeletonHildir | $item_trophy_skeleton_hildir | Brenna Trophy | Still burning, somehow. |
| TrophySkeletonPoison | $item_trophy_skeletonpoison | Rancid Remains Trophy | A rank and rotten skull. You're not sure why you kept it... |
| TrophySurtling | $item_trophy_surtling | Surtling Trophy | Wreathed in pale flame, it still smoulders like an ember. |
| TrophyTheElder | $item_trophy_elder | The Elder Trophy | This severed head oozes power. / / Offer it to the Sacrificial Stones. |
| TrophyTick | $item_trophy_tick | Tick Trophy | It's a conversation piece... |
| TrophyUlv | $item_trophy_ulv | Ulv Trophy | A rugged tail from a not so good boy. |
| TrophyVolture | $item_trophy_volture | Volture Trophy | It's like a vulture, but it thrives in volcanic climates. |
| TrophyWolf | $item_trophy_wolf | Wolf Trophy | Frozen in death, the hair matted with blood and a silent howl lodged in its throat. |
| TrophyWraith | $item_trophy_wraith | Wraith Trophy | The shed skin of a wraith, a flowing robe only visible by moonlight. |
| TrophyWrithan | $item_trophy_writhan | Writhan Trophy | Tough dead, it should probably be handled delicately. |

### 5c. All item prefabs

| prefab | token | English | type | folder | description |
|---|---|---|---|---|---|
| ArrowBloodGold | $item_arrow_bloodgold | Bloodgold Arrow | Ammo | weapons | An arrow forged of perhaps the hardest materials in this world... |
| ArrowBronze | $item_arrow_bronze | Bronzehead Arrow | Ammo | weapons | Sharper than flint. A sleek messenger of death. |
| ArrowCarapace | $item_arrow_carapace | Carapace Arrow | Ammo | weapons | Heavy and pointy, this one's gonna hurt. |
| ArrowCharred | $item_arrow_charred | Charred Arrow | Ammo | weapons | This arrow has been whittled into shape from a charred femur, and it's as hard as any metal. |
| ArrowFire | $item_arrow_fire | Fire Arrow | Ammo | weapons | This arrow burns whatever it pierces. |
| ArrowFlint | $item_arrow_flint | Flinthead Arrow | Ammo | weapons | A hide-breaker with a head of flint. |
| ArrowFrost | $item_arrow_frost | Frost Arrow | Ammo | weapons | A shard of piercing ice. |
| ArrowIron | $item_arrow_iron | Ironhead Arrow | Ammo | weapons | Capped with iron and flighted with dark feathers. |
| ArrowNeedle | $item_arrow_needle | Needle Arrow | Ammo | weapons | The final stitch. |
| ArrowObsidian | $item_arrow_obsidian | Obsidian Arrow | Ammo | weapons | A sliver of darkness. |
| ArrowPoison | $item_arrow_poison | Poison Arrow | Ammo | weapons | A bitter sting from afar. |
| ArrowSilver | $item_arrow_silver | Silver Arrow | Ammo | weapons | A needle to calm restless spirits. |
| ArrowWood | $item_arrow_wood | Wood Arrow | Ammo | weapons | An arrow of sharpened wood. |
| BoltBlackmetal | $item_bolt_blackmetal | Black Metal Bolt | Ammo | weapons | A sleek bolt of dark metal. |
| BoltBloodGold | $item_bolt_bloodgold | Bloodgold Bolt | Ammo | weapons | A bolt forged of perhaps the hardest materials in this world... |
| BoltBone | $item_bolt_bone | Bone Bolt | Ammo | weapons | A crude bolt of yellowed bone. |
| BoltCarapace | $item_bolt_carapace | Carapace Bolt | Ammo | weapons | A heavy and solid bolt. |
| BoltCharred | $item_bolt_charred | Charred Bolt | Ammo | weapons | A sturdy bone from a forearm, shaped into a deadly bolt. |
| BoltIron | $item_bolt_iron | Iron Bolt | Ammo | weapons | A sturdy iron missile. |
| FishingBait | $item_fishingbait | Fishing Bait | Ammo | materials | Common dvergr fishing bait. Fishing rod sold separately. |
| FishingBaitAshlands | $item_fishingbait_ashlands | Hot Fishing Bait | Ammo | materials | Some fish already like it where the waters are warm, and this bait brings the temperature close to… |
| FishingBaitCave | $item_fishingbait_cave | Cold Fishing Bait | Ammo | materials | This bait doesn't look like much, but it's a treat to fish that live where it's cold and dark. |
| FishingBaitDeepNorth | $item_fishingbait_deepnorth | Frosty Fishing Bait | Ammo | materials | It's not very nutritious, so the only fish that'll take this bait are the ones that are used to jus… |
| FishingBaitForest | $item_fishingbait_forest | Mossy Fishing Bait | Ammo | materials | Dead trolls in the forest often attract a fish or two, speed up the process with this bait! |
| FishingBaitMistlands | $item_fishingbait_mistlands | Misty Fishing Bait | Ammo | materials | A bait to guide the fish to you through shrouded waters. |
| FishingBaitOcean | $item_fishingbait_ocean | Heavy Fishing Bait | Ammo | materials | This bait sinks deep, deep enough to lure the fish swimming along the very bottom of the sea. |
| FishingBaitPlains | $item_fishingbait_plains | Stingy Fishing Bait | Ammo | materials | You're not immune to deathsquito bites, and the fish here aren't immune to this snack! |
| FishingBaitSwamp | $item_fishingbait_swamp | Sticky Fishing Bait | Ammo | materials | It might smell foul, but the fish drawn to this bait are used to so much worse. |
| draugr_arrow | Ironhead arrow |  | Ammo | weapons |  |
| TurretBolt | $item_turretbolt | Black Metal Missile | AmmoNonEquipable | weapons | These thick missiles can punch through the hide of even the toughest foes. |
| TurretBoltBloodgold | $item_turretbolt_bloodgold | Bloodgold Missile | AmmoNonEquipable | weapons | Your attacker shall stand no chance as this missile finds its target. |
| TurretBoltBone | $item_turretboltbone |  | AmmoNonEquipable | weapons |  |
| TurretBoltFlametal | $item_turretbolt_flametal | Flametal Missile | AmmoNonEquipable | weapons | Forged from one of the hardest metals in all of Valheim, this missile is sure to hold off your enem… |
| TurretBoltWood | $item_turretboltwood | Wooden Missile | AmmoNonEquipable | weapons | Sturdy wooden missiles that can provide a tough defense against foes. |
| Bow | $item_bow | Crude Bow | Bow | weapons | A crude but functional bow. |
| BowAshlands | $item_bow_ashlands | Ash Fang | Bow | weapons | Risen again from the ashes, this bow holds unyielding strength. |
| BowAshlandsBlood | $item_bow_ashlandsblood | Blood Fang | Bow | weapons | Arrows loosed from this bow will tear into flesh with unmatched ferocity. |
| BowAshlandsRoot | $item_bow_ashlandsroot | Root Fang | Bow | weapons | Like the twisting branch was made to seek the sun, this bow was made to seek the slaughter. |
| BowAshlandsStorm | $item_bow_ashlandsstorm | Storm Fang | Bow | weapons | Let your arrows fly as swift as the lightning strikes. |
| BowDraugrFang | $item_bow_draugrfang | Draugr Fang | Bow | weapons | Dark wood strung with glistening sinew. A vicious thing. |
| BowFineWood | $item_bow_finewood | Finewood Bow | Bow | weapons | A simple bow of strong and supple wood. |
| BowGold | $item_bow_gold | Nord Bow | Bow | weapons | This bow shall find its target with a golden precision. |
| BowGold_BloodLightning | $item_bow_gold_bloodlightning | Thunderblood Bow | Bow | weapons | Lightning dances along the string, waiting to be unleashed. |
| BowGold_FrostFire | $item_bow_gold_frostfire | Frostfire Bow | Bow | weapons | Keep your head cool as you draw, and then let your fury loose with your arrows. |
| BowHuntsman | $item_bow_huntsman | Huntsman Bow | Bow | weapons | Finely worked and strung. A huntsman's joy. |
| BowSpineSnap | $item_bow_snipesnap | Spinesnap | Bow | weapons | Using this bow is backbreaking work but so worth it. |
| CrossbowArbalest | $item_crossbow_arbalest | Arbalest | Bow | weapons | A slow but powerful weapon. |
| CrossbowGold | $item_crossbow_gold | Nord Crossbow | Bow | weapons | An incredible force is bound to this weapon, waiting to be unleashed. |
| CrossbowGold_BloodLightning | $item_crossbow_bloodlightning_gold | Thunderblood Crossbow | Bow | weapons | The bow is pulled taut with unreleased power, like the air before a lightning strike. |
| CrossbowGold_FrostFire | $item_crossbow_frostfire_gold | Frostfire Crossbow | Bow | weapons | If your enemies don't freeze as you take aim, they are sure to do so once they are hit. |
| CrossbowRipper | $item_crossbow_ripper | Ripper | Bow | weapons | Rips your foes apart, simple as that. |
| CrossbowRipperBlood | $item_crossbow_ripper_blood | Wound Ripper | Bow | weapons | Ready to rend your enemies to pieces. |
| CrossbowRipperLightning | $item_crossbow_ripper_lightning | Storm Ripper | Bow | weapons | The bolts will tear through your enemies like a particularly nasty gale. |
| CrossbowRipperNature | $item_crossbow_ripper_nature | Root Ripper | Bow | weapons | If the bolt doesn't pin your foe in place, the roots surging up from the ground surely will. |
| FW_BowDraugrFang | $item_bow_draugrfang | Draugr Fang | Bow | Equipment | Dark wood strung with glistening sinew. A vicious thing. |
| GrapplingHook | $item_graplinghook | Grappling Hook | Bow | weapons | For a dramatic entrance, or a swift retreat. |
| SP_BowDraugrFang | $item_bow_draugrfang | Draugr Fang | Bow | Equipment/ShadowPerson | Dark wood strung with glistening sinew. A vicious thing. |
| charred_bow | Bow |  | Bow | Weapons |  |
| charred_bow_Fader | Bow |  | Bow | Weapons |  |
| charred_bow_volley | Bow |  | Bow | Weapons |  |
| charred_bow_volley_Fader | Bow |  | Bow | Weapons |  |
| charred_twitcher_throw | Bow |  | Bow | Weapons |  |
| draugr_bow | Bow |  | Bow | weapons |  |
| skeleton_bow | Bow |  | Bow | weapons |  |
| skeleton_bow2 | Bow |  | Bow | weapons |  |
| skeleton_bow_meadows | Bow |  | Bow | weapons |  |
| skeleton_bow_mountains | Bow |  | Bow | weapons |  |
| skeleton_bow_swamps | Bow |  | Bow | weapons |  |
| ArmorAshlandsMediumChest | $item_chest_medium_ashlands | Breastplate of Ask | Chest | armor | The first man in Midgard knew how to guard his most vital organs, perhaps with a breastplate just l… |
| ArmorBerserkerChest | $item_chest_berserker | Patterns of the Bear | Chest | armor | If these intricate designs can protect warriors from distant lands, they can surely protect you too. |
| ArmorBerserkerUndeadChest | $item_chest_berserker_undead | Vilebone Cage | Chest | armor | Crafted from the shattered bones of a tormented bear. |
| ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | armor | A breastplate of hammered bronze. |
| ArmorCarapaceChest | $item_chest_carapace | Carapace Breastplate | Chest | armor | A breastplate crafted from the burnished carapace of a giant insect. |
| ArmorDeepNorthHeavyChest | $item_chest_heavy_deepnorth | Breastplate of the Protector | Chest | armor | Fur and hide and metal all work in tandem to ward off an enemy's blows. |
| ArmorDeepNorthMageChest | $item_chest_mage_deepnorth | Robes of the Caller | Chest | armor | Gold trimmed robes, fit for only the most powerful of mages. |
| ArmorDeepNorthMediumChest | $item_chest_medium_deepnorth | Chestpiece of the Vanguard | Chest | armor | Expertly crafted leather armour, offering excellent protection without limiting a warrior's movemen… |
| ArmorDress1 | $item_chest_dress1 | Plain Brown Dress | Chest | armor | A plain brown dress, worn over a blue underdress. |
| ArmorDress10 | $item_chest_dress10 | Simple Undyed Dress | Chest | armor | A simple dress for everyday wear. |
| ArmorDress2 | $item_chest_dress2 | Brown Dress with Shawl | Chest | armor | A brown dress, with a matching shawl around the shoulders. Warm and stylish! |
| ArmorDress3 | $item_chest_dress3 | Brown Dress with Beads | Chest | armor | A fancy brown dress, with beads and silver fibula brooches. Excellent for a feast! |
| ArmorDress4 | $item_chest_dress4 | Plain Blue Dress | Chest | armor | A plain blue dress, worn over a red underdress. |
| ArmorDress5 | $item_chest_dress5 | Blue Dress with Shawl | Chest | armor | A blue dress, with a green shawl around the shoulders. Warm and stylish! |
| ArmorDress6 | $item_chest_dress6 | Blue Dress with Beads | Chest | armor | A fancy blue dress, with beads and bronze fibula brooches. Excellent for a feast! |
| ArmorDress7 | $item_chest_dress7 | Plain Yellow Dress | Chest | armor | A plain yellow dress, worn over a green underdress. |
| ArmorDress8 | $item_chest_dress8 | Yellow Dress with Shawl | Chest | armor | A yellow dress, with a purple shawl around the shoulders. Warm and stylish! |
| ArmorDress9 | $item_chest_dress9 | Yellow Dress with Beads | Chest | armor | A fancy yellow dress, with beads and silver fibula brooches. Excellent for a feast! |
| ArmorFenringChest | $item_chest_fenris | Fenris Coat | Chest | armor | The beast could draw deep breaths, so that its howl could be heard far across the land. |
| ArmorFlametalChest | $item_chest_flametal | Flametal Breastplate | Chest | armor | This fusion of mysterious flametal and charred bones serves as a protective layer more resilient th… |
| ArmorHarvester1 | $item_chest_harvester1 | Harvest Tunic | Chest | armor | When working the fields, it's important to dress accordingly. A shorter tunic lets in a cool breeze. |
| ArmorHarvester2 | $item_chest_harvester2 | Harvest Dress | Chest | armor | When working the fields, it's important to dress accordingly. This long dress keeps your knees cove… |
| ArmorIronChest | $item_chest_iron | Iron Scale Mail | Chest | armor | An iron scale mail, this will turn all but the strongest of blows. |
| ArmorLeatherChest | $item_chest_leather | Leather Tunic | Chest | armor | A tunic made from animal hide. |
| ArmorLoxChest | $item_chest_lox | Lox Fur Jacket | Chest | armor | A heavy musk still clings to it. |
| ArmorMageChest | $item_chest_mage | Eitr-weave Robe | Chest | armor | These artfully layered robes have spells and charms sewn into every seam and fold. |
| ArmorMageChest_Ashlands | $item_chest_mage_ashlands | Robes of Embla | Chest | armor | Imbued with the power of the first sorceress, these robes will grant a boon to any who seeks to wie… |
| ArmorPaddedCuirass | $item_chest_pcuirass | Padded Cuirass | Chest | armor | Finely wrought and strong enough to turn even the sharpest blades. |
| ArmorRagsChest | $item_chest_rags | Rag Tunic | Chest | armor | Better than nothing. |
| ArmorRootChest | $item_chest_root | Root Harnesk | Chest | armor | Finely wrought and strong enough to turn even the sharpest blades. |
| ArmorTrollLeatherChest | $item_chest_trollleather | Troll Leather Tunic | Chest | armor | Trolls are hard to skin but their leather is tough and warm. |
| ArmorTunic1 | $item_chest_tunic1 | Plain Blue Tunic | Chest | armor | A plain blue tunic, brightly dyed. |
| ArmorTunic10 | $item_chest_tunic10 | Simple Undyed Tunic | Chest | armor | A simple tunic for everyday wear. |
| ArmorTunic2 | $item_chest_tunic2 | Blue Tunic with Cape | Chest | armor | A blue tunic, worn with a fashionable cape made from fine brown wool. |
| ArmorTunic3 | $item_chest_tunic3 | Blue Tunic with Beads | Chest | armor | A blue tunic worn with beads and silver jewelry. Sure to attract attention from other vikings. |
| ArmorTunic4 | $item_chest_tunic4 | Plain Red Tunic | Chest | armor | A plain red tunic, brightly dyed. |
| ArmorTunic5 | $item_chest_tunic5 | Red Tunic with Cape | Chest | armor | A red tunic, worn with a fashionable cape made from fine green wool. |
| ArmorTunic6 | $item_chest_tunic6 | Red Tunic with Beads | Chest | armor | A red tunic worn with beads and bronze jewelry. Sure to attract attention from other vikings. |
| ArmorTunic7 | $item_chest_tunic7 | Plain Yellow Tunic | Chest | armor | A plain yellow tunic, brightly dyed. |
| ArmorTunic8 | $item_chest_tunic8 | Yellow Tunic with Cape | Chest | armor | A yellow tunic, worn with a fashionable cape made from fine purple wool. |
| ArmorTunic9 | $item_chest_tunic9 | Yellow Tunic with Beads | Chest | armor | A yellow tunic worn with beads and silver jewelry. Sure to attract attention from other vikings. |
| ArmorWolfChest | $item_chest_wolf | Wolf Hide Chestpiece | Chest | armor | A wolfskin jerkin, warm and wild-looking. It protects against the cold. |
| Charred_Breastplate | Iron plate armor |  | Chest | Armor | An iron scale mail, this will turn all but the strongest of blows. |
| Charred_MageCloths | Iron plate armor |  | Chest | Armor | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitArbalest | Iron plate armor |  | Chest | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitArbalest_Ashlands | Iron plate armor |  | Chest | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitFire | Iron plate armor |  | Chest | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitIce | Iron plate armor |  | Chest | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerSuitSupport | Iron plate armor |  | Chest | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| FW_ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | Equipment | A breastplate of hammered bronze. |
| FW_ArmorFenringChest | $item_chest_fenris | Fenris Coat | Chest | Equipment | The beast could draw deep breaths, so that its howl could be heard far across the land. |
| FW_ArmorMageChest | $item_chest_mage | Eitr-weave Robe | Chest | Equipment | These artfully layered robes have spells and charms sewn into every seam and fold. |
| FW_ArmorMageChest_Ashlands | $item_chest_mage_ashlands | Robes of Embla | Chest | Equipment | Imbued with the power of the first sorceress, these robes will grant a boon to any who seeks to wie… |
| FW_ArmorPaddedCuirass | $item_chest_pcuirass | Padded Cuirass | Chest | Equipment | Finely wrought and strong enough to turn even the sharpest blades. |
| FW_ArmorTrollLeatherChest | $item_chest_trollleather | Troll Leather Tunic | Chest | Equipment | Trolls are hard to skin but their leather is tough and warm. |
| GoblinArmband | Iron plate armor |  | Chest | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinBrute_ArmGuard | Iron plate armor |  | Chest | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinHelmet | Iron plate armor |  | Chest | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinLegband | Iron plate armor |  | Chest | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinShoulders | Iron plate armor |  | Chest | misc | An iron scale mail, this will turn all but the strongest of blows. |
| SP_ArmorBronzeChest | $item_chest_bronze | Bronze Plate Tunic | Chest | Equipment/ShadowPerson | A breastplate of hammered bronze. |
| SP_ArmorDress1 | $item_chest_dress1 | Plain Brown Dress | Chest | armor | A plain brown dress, worn over a blue underdress. |
| SP_ArmorFenringChest | $item_chest_fenris | Fenris Coat | Chest | Equipment/ShadowPerson | The beast could draw deep breaths, so that its howl could be heard far across the land. |
| SP_ArmorMageChest | $item_chest_mage | Eitr-weave Robe | Chest | Equipment/ShadowPerson | These artfully layered robes have spells and charms sewn into every seam and fold. |
| SP_ArmorMageChest_Ashlands | $item_chest_mage_ashlands | Robes of Embla | Chest | Equipment/ShadowPerson | Imbued with the power of the first sorceress, these robes will grant a boon to any who seeks to wie… |
| SP_ArmorPaddedCuirass | $item_chest_pcuirass | Padded Cuirass | Chest | Equipment/ShadowPerson | Finely wrought and strong enough to turn even the sharpest blades. |
| SP_ArmorTrollLeatherChest | $item_chest_trollleather | Troll Leather Tunic | Chest | Equipment/ShadowPerson | Trolls are hard to skin but their leather is tough and warm. |
| SP_ArmorTunic5 | $item_chest_tunic5 | Red Tunic with Cape | Chest | armor | A red tunic, worn with a fashionable cape made from fine green wool. |
| StoneGolem_clubs |  |  | Chest | Misc |  |
| StoneGolem_spikes |  |  | Chest | Misc |  |
| BakedPoteitr | $item_bakedpoteitr | Baked Poteitr | Consumable | consumables | Neither boiled nor mashed nor in a stew. Still delicious though! |
| BarleyWine | $item_barleywine | Fire Resistance Barley Wine | Consumable | consumables | Fortifies you against fire. |
| BlackSoup | $item_blacksoup | Black Soup | Consumable | consumables | A perfect balance of sweetness and acidity. |
| BloodPudding | $item_bloodpudding | Blood Pudding | Consumable | consumables | It's bloody tasty. |
| Blueberries | $item_blueberries | Blueberries | Consumable | consumables | Tiny but tasty. |
| BoarJerky | $item_boarjerky | Boar Jerky | Consumable | consumables | Lean and salty. |
| Bread | $item_bread | Bread | Consumable | consumables | A tasty loaf of bread. |
| Carrot | $item_carrot | Carrot | Consumable | consumables | An orange treat. |
| CarrotSoup | $item_carrotsoup | Carrot Soup | Consumable | consumables | A warm tasty soup made of mostly carrots. |
| Cloudberry | $item_cloudberries | Cloudberries | Consumable | consumables | The gold of the forest. |
| CookedAsksvinMeat | $item_asksvin_meat_cooked | Cooked Asksvin Tail | Consumable | consumables | This meat has a potent and mature flavour, but is very tasty when grilled right. |
| CookedBjornMeat | $item_bjorn_meat_cooked | Cooked Bear Meat | Consumable | consumables | Tastes like victory. |
| CookedBoneMawSerpentMeat | $item_bonemawmeat_cooked | Cooked Bonemaw Meat | Consumable | consumables | The boiling sea did nothing to this meat, but grilling it over the fire has given it a delightful c… |
| CookedBugMeat | $item_bug_meat_cooked | Cooked Seeker Meat | Consumable | consumables | Succulent white meat. A true delicacy. |
| CookedChickenMeat | $item_chicken_meat_cooked | Cooked Chicken Meat | Consumable | consumables | It tastes like chicken. |
| CookedDeerMeat | $item_deer_meat_cooked | Cooked Deer Meat | Consumable | consumables | All that running paid off. |
| CookedEgg | $item_egg_cooked | Cooked Egg | Consumable | consumables | Sunny side up! |
| CookedHareMeat | $item_hare_meat_cooked | Cooked Hare Meat | Consumable | consumables | Stringy but flavorful. |
| CookedLoxMeat | $item_loxmeat_cooked | Cooked Lox Meat | Consumable | consumables | A great hunk of tender meat, food fit for Valhalla! |
| CookedMeat | $item_boar_meat_cooked | Cooked Boar Meat | Consumable | consumables | An earthly taste. |
| CookedMooseMeat | $item_moose_meat_cooked | Cooked Moose Meat | Consumable | consumables | This meat is lean yet full of flavour. |
| CookedSealBlubber | $item_blubber_cooked | Cooked Seal Blubber | Consumable | consumables | A chewy meat, with an aftertaste of remorse. |
| CookedVoltureMeat | $item_volture_meat_cooked | Cooked Volture Meat | Consumable | consumables | A chewy and somewhat dry meat. Some seasoning would probably make it taste better, but it'll fill y… |
| CookedWolfMeat | $item_wolf_meat_cooked | Cooked Wolf Meat | Consumable | consumables | A wild taste. |
| DeerStew | $item_deerstew | Deer Stew | Consumable | consumables | Fall-apart tender. |
| Eyescream | $item_eyescream | Eyescream | Consumable | consumables | Crispy cool and creamy. |
| FeastAshlands | $item_feastashlands | Ashlands Gourmet Bowl | Consumable |  |  |
| FeastBlackforest | $item_feastblackforest | Black Forest Buffet Platter | Consumable |  |  |
| FeastDeepNorth | $item_feastdeepnorth | Northern Morning Fare | Consumable |  |  |
| FeastMeadows | $item_feastmeadows | Whole Roasted Meadow Boar | Consumable |  |  |
| FeastMistlands | $item_feastmistlands | Mushrooms Galore á la Mistlands | Consumable |  |  |
| FeastMountains | $item_feastmountains | Hearty Mountain Logger's Stew | Consumable |  |  |
| FeastOceans | $item_feastoceans | Sailor's Bounty | Consumable |  |  |
| FeastPlains | $item_feastplains | Plains Pie Picnic | Consumable |  |  |
| FeastSwamps | $item_feastswamps | Swamp Dweller's Delight | Consumable |  |  |
| Fiddleheadfern | $item_fiddleheadfern | Fiddlehead | Consumable | consumables | Veggies with a twist! |
| FierySvinstew | $item_fierysvinstew | Fiery Svinstew | Consumable | consumables | This musty stew is a necessity on every adventurer's menu. |
| FishAndBread | $item_fishandbread | Fish 'n' Bread | Consumable | consumables | Bounty from both land and sea. |
| FishCooked | $item_fish_cooked | Cooked Fish | Consumable | consumables | A tasty side of smoked fish. |
| FishSoup | $item_fishsoup | Fish Soup | Consumable | consumables | Swimming with flavour! |
| FishWraps | $item_fishwraps | Fish Wraps | Consumable | consumables | Bread and fish, what more to wish? |
| GlowWorm | $item_glowworm | Luminous Larva | Consumable | consumables | Slimy, yet satisfying. |
| HealthUpgrade_Bonemass | Bonemass heart |  | Consumable | Upgrades |  |
| HealthUpgrade_GDKing | Elder heart |  | Consumable | Upgrades |  |
| Honey | $item_honey | Honey | Consumable | materials | Sweet and tasty. |
| HoneyGlazedChicken | $item_honeyglazedchicken | Honey Glazed Chicken | Consumable | consumables | Grilled to perfection. Makes both eyes and mouths water. |
| Kale | $item_kale | Kale | Consumable | consumables | A versatile leafy green. |
| KaleChips | $item_kalechips | Kale Chips | Consumable | consumables | Crispy greens! |
| Lingonberry | $item_lingonberries | Lingonberries | Consumable | consumables | These tart berries can grow even in cold climates. |
| Lingondricka | $item_lingondricka | Lingonberry Juice | Consumable | consumables | Pairs well with most foods. |
| LoxPie | $item_loxpie | Lox Meat Pie | Consumable | consumables | Break the crust to release a cloud of fragrant steam. Delicious! |
| MagicallyStuffedShroom | $item_magicallystuffedmushroom | Stuffed Mushroom | Consumable | consumables | Bursting with magical flavour. |
| MarinatedGreens | $item_marinatedgreens | Marinated Greens | Consumable | consumables | It's spicy, it's chewy, it's sweet… This mad dish tickles your tongue as well as your mind. |
| MashedMeat | $item_mashedmeat | Mashed Meat | Consumable | consumables | Leftover meat can actually be pretty tasty if you just mash it right! |
| MeadBugRepellent | $item_mead_bugrepellent | Anti-Sting Concoction | Consumable | consumables | The perfect drink for a day in the Plains. |
| MeadBzerker | $item_mead_bzerker | Berserkir Mead | Consumable | consumables | Something poisonous stirs within, brewed to the point where its potential can finally be harnessed.… |
| MeadEitrLingering | $item_mead_eitr_lingering | Lingering Eitr Mead | Consumable | consumables | Increases eitr regeneration. |
| MeadEitrMinor | $item_mead_eitr_minor | Minor Eitr Mead | Consumable | consumables | Restores eitr. |
| MeadFrostResist | $item_mead_frostres | Frost Resistance Mead | Consumable | consumables | Protects against the cold. |
| MeadHasty | $item_mead_hasty | Tonic of Ratatosk | Consumable | consumables | The squirrel must be quick on its feet as it runs up and down the trunk of the world tree. Although… |
| MeadHealthLingering | $item_mead_hp_lingering | Lingering Healing Mead | Consumable | consumables | Increases health regeneration. |
| MeadHealthMajor | $item_mead_hp_major | Major Healing Mead | Consumable | consumables | Restores health. |
| MeadHealthMedium | $item_mead_hp_medium | Medium Healing Mead | Consumable | consumables | Restores health. |
| MeadHealthMinor | $item_mead_hp_minor | Minor Healing Mead | Consumable | consumables | Restores health. |
| MeadLightfoot | $item_mead_lightfoot | Lightfoot Mead | Consumable | consumables | This bottle is full, yet it feels like it weighs almost nothing at all. |
| MeadPoisonResist | $item_mead_poisonres | Poison Resistance Mead | Consumable | consumables | Fortifies you against poison. |
| MeadStaminaLingering | $item_mead_stamina_lingering | Lingering Stamina Mead | Consumable | consumables | Increases stamina regeneration. |
| MeadStaminaMedium | $item_mead_stamina_medium | Medium Stamina Mead | Consumable | consumables | Restores stamina. |
| MeadStaminaMinor | $item_mead_stamina_minor | Minor Stamina Mead | Consumable | consumables | Restores stamina. |
| MeadStrength | $item_mead_strength | Mead of Troll Endurance | Consumable | consumables | What creature can carry more than a troll? Why, a viking with this drink of course! |
| MeadSwimmer | $item_mead_swimmer | Draught of Vananidir | Consumable | consumables | You feel invigorated just by holding this, as the ocean beckons you to come for a swim. |
| MeadTamer | $item_mead_tamer | Brew of Animal Whispers | Consumable | consumables | It smells like a pigsty. Likely the taste won't be much better. |
| MeadTasty | $item_mead_tasty | Tasty Mead | Consumable | consumables | The nectar of the Gods, divine mead. |
| MeadTrollPheromones | $item_mead_trollpheromones | Love Potion | Consumable | consumables | An intense musk permeates the air around this bottle. |
| MeatPlatter | $item_meatplatter | Meat Platter | Consumable | consumables | Battle fuel. |
| MeatballsMashedPoteitr | $item_meatballsmashedpoteitr | Meatballs and Poteitr | Consumable | consumables | It doesn't get more iconic than this! |
| MinceMeatSauce | $item_mincemeatsauce | Minced Meat Sauce | Consumable | consumables | Chunks of goodness in a thick gravy. |
| MisthareSupreme | $item_mistharesupreme | Misthare Supreme | Consumable | consumables | One of life's Great Pleasures. |
| MooseKebab | $item_moosekebab | Meat In Bread | Consumable | consumables | A convenient meal, often favoured by travelling merchants. |
| Mushroom | $item_mushroomcommon | Mushroom | Consumable | consumables | Bounty of the forest. |
| MushroomBlue | $item_mushroomblue | Blue Mushroom | Consumable | consumables | Glows with a soft blue hue. |
| MushroomBzerker | $item_mushroom_bzerker | Toadstool | Consumable | consumables | Some say you can eat everything you find in the forest. That is not the case with this mushroom. |
| MushroomJotunPuffs | $item_jotunpuffs | Jotun Puffs | Consumable | consumables | An invigorating mushroom that can be used for cooking. |
| MushroomMagecap | $item_magecap | Magecap | Consumable | consumables | A mushroom commonly used in a sorcerer's diet. |
| MushroomOmelette | $item_mushroomomelette | Mushroom Omelette | Consumable | consumables | A delicious omelette with an earthy aftertaste. |
| MushroomSmokePuff | $item_smokepuff | Smoke Puff | Consumable | consumables | Hopefully it tastes better after cooking. |
| MushroomYellow | $item_mushroomyellow | Yellow Mushroom | Consumable | consumables | An energetic glowing mushroom. |
| NeckTailGrilled | $item_necktailgrilled | Grilled Neck Tail | Consumable | consumables | This savoury, charcoal-grilled meat has a slight aroma of seaweed and grass. |
| Oat | $item_oat | Oats | Consumable | consumables | Tasty grains, to be used as they are or to be ground into flour. |
| OatMilk | $item_oatmilk | Oat Milk | Consumable | consumables | Tastes like innovation. |
| OatmealLingonberryJam | $item_oatmeallingonberryjam | Oatmeal | Consumable | consumables | Served with a generous helping of lingonberry jam. |
| Onion | $item_onion | Onion | Consumable | consumables | A crunchy and spicy taste. |
| OnionSoup | $item_onionsoup | Onion Soup | Consumable | consumables | Deliciously rich. |
| OvenPancake | $item_ovenpancake | Oven Pancake | Consumable | consumables | Warm and fluffy. |
| Pancakes | $item_pancakes | Pancakes | Consumable | consumables | Was there ever a more comforting food? |
| PiquantPie | $item_piquantpie | Piquant Pie | Consumable | consumables | It takes some time and effort to make this pie, but the taste is well worth it. |
| Poteitr | $item_poteitr | Poteitr | Consumable | consumables | The possibilities are practically endless. Who wouldn't want a taste? |
| Pukeberries | $item_pukeberries | Bukeperries | Consumable | consumables | Allows the consumer to quickly evacuate any misplaced meal and start anew. |
| PulledBear | $item_pulledbear | Pulled Bear | Consumable | consumables | Tender meat cooked for hours upon hours, until it practically falls apart. |
| QueensJam | $item_queensjam | Queen's Jam | Consumable | consumables | That classic tasty blend of raspberries and blueberries. |
| Raspberry | $item_raspberries | Raspberries | Consumable | consumables | Sweet and delicious. |
| RoastedCrustPie | $item_roastedcrustpie | Roasted Crust Pie | Consumable | consumables | This dessert keeps you going all day long. |
| RottenMeat | $item_meat_rotten | Rotten Meat | Consumable | consumables | There are maggots crawling in the meat. It smells awful. |
| RoyalJelly | $item_royaljelly | Royal Jelly | Consumable | consumables | Jelly fit for kings and queens. |
| Salad | $item_salad | Salad | Consumable | consumables | Fresh, crisp leaves. |
| Sausages | $item_sausages | Sausages | Consumable | consumables | Links of savory, smoked meat. |
| ScorchingMedley | $item_scorchingmedley | Scorching Medley | Consumable | consumables | A varied diet is important, so why not try this vegetarian option? |
| SealSoup | $item_sealsoup | Seal Meat Soup | Consumable | consumables | A warm and tasty meal, best enjoyed on a cold day. |
| SeekerAspic | $item_seekeraspic | Seeker Aspic | Consumable | consumables | A quivering jelly with a taste like gentle electricity. |
| SerpentMeatCooked | $item_serpentmeatcooked | Cooked Serpent Meat | Consumable | consumables | A cooked slice of sea serpent. Smells good. |
| SerpentStew | $item_serpentstew | Serpent Stew | Consumable | consumables | Smells of honey and serpent... |
| ShocklateSmoothie | $item_shocklatesmoothie | Muckshake | Consumable | consumables | Wakes you up! |
| SizzlingBerryBroth | $item_sizzlingberrybroth | Sizzling Berry Broth | Consumable | consumables | This soup settles in your stomach with an almost tingly sensation. |
| SmokedFish | $item_smokedfish | Smoked Fish | Consumable | consumables | Fish prepared in the most delicious way. |
| SmokedMooseMeat | $item_smokedmoosemeat | Smoked Moose Meat | Consumable | consumables | The smoke only adds to the wild flavour. |
| SparklingShroomshake | $item_sparklingshroomshake | Sparkling Shroomshake | Consumable | consumables | Perhaps it's not the best flavour to start the day with, but it will give you the boost you need. |
| SpicyMarmalade | $item_spicymarmalade | Spicy Marmalade | Consumable | consumables | Sugary honey perfectly balanced with tangy fronds and tart berries. |
| StaminaUpgrade_Greydwarf | Stamina Greydwarf |  | Consumable | Upgrades |  |
| StaminaUpgrade_Troll | Stamina Troll |  | Consumable | Upgrades |  |
| StaminaUpgrade_Wraith | Stamina Wraith |  | Consumable | Upgrades |  |
| TurnipStew | $item_turnipstew | Turnip Stew | Consumable | consumables | Nutritious and restorative. |
| VikingCupcake | $item_vikingcupcake | Frosted Sweetbread | Consumable | consumables | A sweet and tasty treat, to celebrate a feat! |
| Vineberry | $item_vineberry | Vineberry Cluster | Consumable | consumables | These juicy berries are both sour and sweet. |
| WolfJerky | $item_wolfjerky | Wolf Jerky | Consumable | consumables | Chewy and full of flavor. |
| WolfMeatSkewer | $item_wolf_skewer | Wolf Skewer | Consumable | consumables | Dripping with taste. |
| YggdrasilPorridge | $item_yggdrasilporridge | Yggdrasil Porridge | Consumable | consumables | Made with sap from the great tree. Even a mouthful imparts a warm glow to your whole body. |
| Beard1 | $customization_beard01 | Majestic | Customization | customizations/beards |  |
| Beard10 | $customization_beard10 | Top Braid | Customization | customizations/beards |  |
| Beard11 | $customization_beard11 | Facewarmer | Customization | customizations/beards |  |
| Beard12 | $customization_beard12 | Royal | Customization | customizations/beards |  |
| Beard13 | $customization_beard13 | Triplets | Customization | customizations/beards |  |
| Beard14 | $customization_beard14 | Split Braid | Customization | customizations/beards |  |
| Beard15 | $customization_beard15 | Mini Braid | Customization | customizations/beards |  |
| Beard16 | $customization_beard16 | Stonedweller | Customization | customizations/beards |  |
| Beard17 | $customization_beard17 | Neat | Customization | customizations/beards |  |
| Beard18 | $customization_beard18 | Jarl Braids | Customization | customizations/beards |  |
| Beard19 | $customization_beard19 | Bushy | Customization | customizations/beards |  |
| Beard2 | $customization_beard02 | Twin Braids | Customization | customizations/beards |  |
| Beard20 | $customization_beard20 | Spiky | Customization | customizations/beards |  |
| Beard21 | $customization_beard21 | Tidy | Customization | customizations/beards |  |
| Beard22 | $customization_beard22 | Mustache | Customization | customizations/beards |  |
| Beard23 | $customization_beard23 | Crumb Catcher | Customization | customizations/beards |  |
| Beard24 | $customization_beard24 | Waxed | Customization | customizations/beards |  |
| Beard25 | $customization_beard25 | Trimmed | Customization | customizations/beards |  |
| Beard26 | $customization_beard26 | Handlebar | Customization | customizations/beards |  |
| Beard3 | $customization_beard03 | Short | Customization | customizations/beards |  |
| Beard4 | $customization_beard04 | Straight | Customization | customizations/beards |  |
| Beard5 | $customization_beard05 | Single Braid | Customization | customizations/beards |  |
| Beard6 | $customization_beard06 | Loose Braid | Customization | customizations/beards |  |
| Beard7 | $customization_beard07 | Split Shave | Customization | customizations/beards |  |
| Beard8 | $customization_beard08 | Thick | Customization | customizations/beards |  |
| Beard9 | $customization_beard09 | Trobadour | Customization | customizations/beards |  |
| BeardNone | $customization_nobeard | No Beard | Customization | customizations/beards |  |
| Hair1 | $customization_hair01 | Windswept | Customization | customizations/hairs |  |
| Hair10 | $customization_hair10 | Side Swept | Customization | customizations/hairs |  |
| Hair10_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair11 | $customization_hair11 | Long Braid | Customization | customizations/hairs |  |
| Hair11_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair11_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair12 | $customization_hair12 | Matronly | Customization | customizations/hairs |  |
| Hair12_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair13 | $customization_hair13 | Twin Braids | Customization | customizations/hairs |  |
| Hair13_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair13_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair14 | $customization_hair14 | Speed Demon | Customization | customizations/hairs |  |
| Hair14_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair15 | $customization_hair15 | Pulled Back Curls | Customization | customizations/hairs |  |
| Hair15_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair15_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair16 | $customization_hair16 | Gathered Braids | Customization | customizations/hairs |  |
| Hair16_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair17 | $customization_hair17 | Neat Braids | Customization | customizations/hairs |  |
| Hair17_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair18 | $customization_hair18 | Royal Braids | Customization | customizations/hairs |  |
| Hair18_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair19 | $customization_hair19 | Painter Curls | Customization | customizations/hairs |  |
| Hair2 | $customization_hair02 | High Ponytail | Customization | customizations/hairs |  |
| Hair20 | $customization_hair20 | Tidy Curls | Customization | customizations/hairs |  |
| Hair21 | $customization_hair21 | Twin Buns | Customization | customizations/hairs |  |
| Hair21_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair22 | $customization_hair22 | Single Bun | Customization | customizations/hairs |  |
| Hair22_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair23 | $customization_hair23 | Short Curls | Customization | customizations/hairs |  |
| Hair24 | $customization_hair24 | Shaved and Braided | Customization | customizations/hairs |  |
| Hair24_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair24_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair25 | $customization_hair25 | Knot | Customization | customizations/hairs |  |
| Hair26 | $customization_hair26 | Short Locs | Customization | customizations/hairs |  |
| Hair27 | $customization_hair27 | Strength Braids | Customization | customizations/hairs |  |
| Hair27_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair27_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair28 | $customization_hair28 | Merchant's Braid | Customization | customizations/hairs |  |
| Hair28_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair28_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair29 | $customization_hair29 | Tucked Back | Customization | customizations/hairs |  |
| Hair29_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair3 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair30 | $customization_hair30 | Loose Waves | Customization | customizations/hairs |  |
| Hair30_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair30_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair31 | $customization_hair31 | Gathered Locs | Customization | customizations/hairs |  |
| Hair31_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair31_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair32 | $customization_hair32 | Mullet | Customization | customizations/hairs |  |
| Hair32_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair32_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair33 | $customization_hair33 | Vinland Shave | Customization | customizations/hairs |  |
| Hair33_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair34 | $customization_hair34 | Castellan | Customization | customizations/hairs |  |
| Hair34_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair34_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair35 | $customization_hair35 | Champion | Customization | customizations/hairs |  |
| Hair35_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair35_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair36 | $customization_hair36 | Chronicler | Customization | customizations/hairs |  |
| Hair36_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair36_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair37 | $customization_hair37 | Sunbringer | Customization | customizations/hairs |  |
| Hair37_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair37_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair38 | $customization_hair38 | Masculine | Customization | customizations/hairs |  |
| Hair38_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair38_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair3_2 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair3_3 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair4 | $customization_hair04 | Low Ponytail | Customization | customizations/hairs |  |
| Hair4_2 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair4_3 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair5 | $customization_hair05 | Short | Customization | customizations/hairs |  |
| Hair5_2 | $customization_hair03 | Pigtails | Customization | customizations/hairs |  |
| Hair6 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair6_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair6_3 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair7 | $customization_hair07 | Dragonslayer | Customization | customizations/hairs |  |
| Hair7_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair8 | $customization_hair08 | Parted | Customization | customizations/hairs |  |
| Hair8_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| Hair9 | $customization_hair09 | Old One-Eye | Customization | customizations/hairs |  |
| Hair9_2 | $customization_hair06 | Long and Loose | Customization | customizations/hairs |  |
| HairNone | $customization_nohair | No Hair | Customization | customizations/hairs |  |
| Fish1 | $animal_fish1 | Perch | Fish | fishes | A tasty whitemeat fish. |
| Fish10 | $animal_fish10 | Northern Salmon | Fish | fishes | This fish likes the water to be almost freezing cold. |
| Fish11 | $animal_fish11 | Magmafish | Fish | fishes | Some say this fish lays its eggs directly in molten lava! |
| Fish12 | $animal_fish12 | Pufferfish | Fish | fishes | Tasty when cooked right, but the flavour has a bit of a sting. |
| Fish2 | $animal_fish2 | Pike | Fish | fishes | A freshwater fish that needs a lot of seasoning. |
| Fish3 | $animal_fish3 | Tuna | Fish | fishes | Chicken of the sea... |
| Fish4_cave | $animal_fish4 | Tetra | Fish | fishes | Spending its whole life in the dark, it has no need for eyes. |
| Fish5 | $animal_fish5 | Trollfish | Fish | fishes | This fish is a nuisance in the local streams. |
| Fish6 | $animal_fish6 | Giant Herring | Fish | fishes | Fermented, this fish will smell worse than the swamp it came from. |
| Fish7 | $animal_fish7 | Grouper | Fish | fishes | Best served with lots of carbs! |
| Fish8 | $animal_fish8 | Coral Cod | Fish | fishes | It has seen some things... Some very haunting things... |
| Fish9 | $animal_fish9 | Anglerfish | Fish | fishes | The dangling light makes it easier to see that pretty little face! |
| Charred_Helmet | Iron plate armor |  | Helmet | Armor | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairMale | Iron plate armor |  | Helmet | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairMale_Redbeard | Iron plate armor |  | Helmet | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| FW_HelmetBronze | $item_helmet_bronze | Bronze Helmet | Helmet | Equipment | This will help to keep your brains inside your skull. |
| GoblinBrute_Backbones | Iron plate armor |  | Helmet | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinBrute_ExecutionerCap | Iron plate armor |  | Helmet | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinShaman_Headdress_antlers | Club |  | Helmet | misc | A crude but useful weapon. |
| GoblinShaman_Headdress_feathers | Club |  | Helmet | misc | A crude but useful weapon. |
| HelmetAshlandsMediumHood | $item_helmet_medium_ashlands | Hood of Ask | Helmet | helmets | As the first man of Midgard, Ask knew it was important to not draw too much attention to himself. |
| HelmetBerserkerHood | $item_helmet_berserker | Headdress of the Bear | Helmet | helmets | Let the primal rage of a mighty bear consume you. |
| HelmetBerserkerUndead | $item_helmet_berserker_undead | Vilebone Visage | Helmet | helmets | Inspiring fear in foes and courage in kin when worn. |
| HelmetBronze | $item_helmet_bronze | Bronze Helmet | Helmet | helmets | This will help to keep your brains inside your skull. |
| HelmetCarapace | $item_helmet_carapace | Carapace Helmet | Helmet | helmets | People might say you look like a giant ant. But they will only say it once. |
| HelmetCelebration | $item_helmet_celebration | Celebratory Cap | Helmet | helmets | You simply can't help but feel happy when donning this cap! |
| HelmetCrownofValheim | $item_helmet_crown_of_valheim | Crown of Valheim | Helmet | helmets | A glorious reward for the truly worthy. |
| HelmetDNHeavy | $item_helmet_heavy_deepnorth | Helmet of the Protector | Helmet | helmets | Embellished with the wings of victory. |
| HelmetDNMage | $item_helmet_mage_deepnorth | Headdress of the Caller | Helmet | helmets | The spirits of the land come as you beckon. Are they fooled by your disguise? |
| HelmetDNMediumHood | $item_helmet_medium_deepnorth | Hood of the Vanguard | Helmet | helmets | Something to protect your neck from the elements as well as from the sharp teeth of the enemy. |
| HelmetDrake | $item_helmet_drake | Drake Helmet | Helmet | helmets | An elaborate and finely-crafted helm. |
| HelmetDverger | $item_helmet_dverger | Dverger Circlet | Helmet | helmets | A portable perpetual lightsource for the dungeon explorer. |
| HelmetFenring | $item_helmet_fenris | Fenris Hood | Helmet | helmets | The eyes of the beast were wise and knowing, so that it could measure the strength of a warrior in… |
| HelmetFishingHat | $item_helmet_fishinghat | Fishing Hat | Helmet | helmets | This catchy hat may only be reeled in by the most seasoned adventurers. |
| HelmetFlametal | $item_helmet_flametal | Flametal Helmet | Helmet | helmets | While you're wearing this helmet, your enemies will think twice before trying to bite your head off. |
| HelmetHat1 | $item_helmet_hat1 | Blue Tied Headscarf | Helmet | helmets | A blue practical headscarf. |
| HelmetHat10 | $item_helmet_hat10 | Simple Purple Cap | Helmet | helmets | A simple yet fashionable purple cap. |
| HelmetHat2 | $item_helmet_hat2 | Green Twisted Headscarf | Helmet | helmets | A fancy green headscarf. |
| HelmetHat3 | $item_helmet_hat3 | Brown Fur Cap | Helmet | helmets | A warm fur cap, made from the finest leather. |
| HelmetHat4 | $item_helmet_hat4 | Extravagant Green Cap | Helmet | helmets | A warm cap for special occasions. |
| HelmetHat5 | $item_helmet_hat5 | Simple Red Cap | Helmet | helmets | A simple yet fashionable red cap. |
| HelmetHat6 | $item_helmet_hat6 | Yellow Tied Headscarf | Helmet | helmets | A practical yellow headscarf. |
| HelmetHat7 | $item_helmet_hat7 | Red Twisted Headscarf | Helmet | helmets | A fancy red headscarf. |
| HelmetHat8 | $item_helmet_hat8 | Grey Fur Cap | Helmet | helmets | A warm fur cap, made from the finest wool. |
| HelmetHat9 | $item_helmet_hat9 | Extravagant Orange Cap | Helmet | helmets | A warm cap for special occasions. |
| HelmetIron | $item_helmet_iron | Iron Helmet | Helmet | helmets | A helm of polished iron, fit for a hero. |
| HelmetLeather | $item_helmet_leather | Leather Helmet | Helmet | helmets | A hood of toughened leather. |
| HelmetLox | $item_helmet_lox | Lox Fur Hood | Helmet | helmets | See the world like a lox might. |
| HelmetMage | $item_helmet_mage | Eitr-weave Hood | Helmet | helmets | Sorcery shows itself in the eyes, so most mages wear cowls to disguise their occult pursuits. |
| HelmetMage_Ashlands | $item_helmet_mage_ashlands | Hood of Embla | Helmet | helmets | Even the first sorceress valued the mystique of covering one's face. It is rumoured that honouring… |
| HelmetMidsummerCrown | $item_helmet_midsummercrown | Midsummer Crown | Helmet | helmets | Celebrate summer with a crown woven from flowers. |
| HelmetOdin | $item_helmet_odin | Hood of Oden | Helmet | helmets | Oden's finest warriors deserve the finest cloth. |
| HelmetPadded | $item_helmet_padded | Padded Helmet | Helmet | helmets | A snug fit, finely made. |
| HelmetPointyHat | $item_helmet_witchhat | Pointy Hat | Helmet | helmets | This hat is sure to add a bit of magical flair to any outfit. |
| HelmetRoot | $item_helmet_root | Root Mask | Helmet | helmets | Your head fits perfectly inside this knot of roots and bark. |
| HelmetRootCrown | $item_helmet_rootcrown | Crown of Roots | Helmet | helmets | A painful, yet intricate headpiece. |
| HelmetStrawHat | $item_helmet_strawhat | Straw Hat | Helmet | helmets | The perfect way to avoid sunstroke. |
| HelmetSweatBand | $item_helmet_sweatband | Headband | Helmet | helmets | It feels a bit...moist. |
| HelmetTrollLeather | $item_helmet_trollleather | Troll Leather Hood | Helmet | helmets | Trollskin is hard to work but makes exceptional armour. |
| HelmetYule | $item_helmet_yule | Yule Hat | Helmet | helmets | A red cap in the style of house gnomes. |
| JotunHairFemale | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale2 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale3 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale4 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale5 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale6 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale7 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| JotunHairMale8 | Iron plate armor |  | Helmet | gear | An iron scale mail, this will turn all but the strongest of blows. |
| SP_HelmetBronze | $item_helmet_bronze | Bronze Helmet | Helmet | Equipment/ShadowPerson | This will help to keep your brains inside your skull. |
| StoneGolem_hat |  |  | Helmet | Misc |  |
| ArmorAshlandsMediumlegs | $item_legs_medium_ashlands | Trousers of Ask | Legs | armor | The first man to roam Midgard preferred to tread lightly. Wearers of these trousers could learn a t… |
| ArmorBerserkerLegs | $item_legs_berserker | Loincloth of the Bear | Legs | armor | It covers all the important bits, but not much else. |
| ArmorBerserkerUndeadLegs | $item_legs_berserker_undead | Vilebone Drapes | Legs | armor | Thick hides and bone fragments woven into a rugged skirt. |
| ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | armor | Bronze greaves to shield your legs. |
| ArmorCarapaceLegs | $item_legs_carapace | Carapace Greaves | Legs | armor | Leg guards of a rigid carapace. |
| ArmorDeepNorthHeavylegs | $item_legs_heavy_deepnorth | Trousers of the Protector | Legs | armor | Heavy boots and trousers, to keep you warm as you trudge through deep snow. |
| ArmorDeepNorthMagelegs | $item_legs_mage_deepnorth | Trousers of the Caller | Legs | armor | Tight legwraps to keep the cold from touching your skin. |
| ArmorDeepNorthMediumlegs | $item_legs_medium_deepnorth | Trousers of the Vanguard | Legs | armor | Warm trousers suitable for a cold climate. The boots offer excellent grip in the icy terrain. |
| ArmorFenringLegs | $item_legs_fenris | Fenris Leggings | Legs | armor | The legs of the beast were lean and strong, so that it could leap great strides. |
| ArmorFlametalLegs | $item_legs_flametal | Flametal Greaves | Legs | armor | Heavy trousers insulate against the heat, while solid greaves keep your shins safe from low blows. |
| ArmorIronLegs | $item_legs_iron | Iron Greaves | Legs | armor | Iron greaves to protect your legs. |
| ArmorLeatherLegs | $item_legs_leather | Leather Trousers | Legs | armor | They squeak a little when you walk. |
| ArmorLoxLegs | $item_legs_lox | Lox Fur Trousers | Legs | armor | Soft, silent and flexible. |
| ArmorMageLegs | $item_legs_mage | Eitr-weave Trousers | Legs | armor | The trousers worn by mages are always especially tight. Discomfort fuels the focus that is needed f… |
| ArmorMageLegs_Ashlands | $item_legs_mage_ashlands | Trousers of Embla | Legs | armor | Whether or not the first sorceress actually wore trousers exactly like these, we can never know. |
| ArmorPaddedGreaves | $item_legs_pgreaves | Padded Greaves | Legs | armor | Expertly crafted leg protection. |
| ArmorRagsLegs | $item_legs_rags | Rag Trousers | Legs | armor | A simple remedy for nudity. |
| ArmorRootLegs | $item_legs_root | Root Leggings | Legs | armor | A light armour oddly woven together by ancient roots and bark. |
| ArmorTrollLeatherLegs | $item_legs_trollleather | Troll Leather Trousers | Legs | armor | Leggings of tough troll hide. |
| ArmorWolfLegs | $item_legs_wolf | Wolf Hide Trousers | Legs | armor | Shaggy breeches of wolfskin. |
| Charred_HipCloth | Iron plate armor |  | Legs | Armor | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairFemale | Iron plate armor |  | Legs | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| DvergerHairFemale_Redhair | Iron plate armor |  | Legs | Gear | An iron scale mail, this will turn all but the strongest of blows. |
| FW_ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | Equipment | Bronze greaves to shield your legs. |
| FW_ArmorFenringLegs | $item_legs_fenris | Fenris Leggings | Legs | Equipment | The legs of the beast were lean and strong, so that it could leap great strides. |
| FW_ArmorMageLegs | $item_legs_mage | Eitr-weave Trousers | Legs | Equipment | The trousers worn by mages are always especially tight. Discomfort fuels the focus that is needed f… |
| FW_ArmorMageLegs_Ashlands | $item_legs_mage_ashlands | Trousers of Embla | Legs | Equipment | Whether or not the first sorceress actually wore trousers exactly like these, we can never know. |
| FW_ArmorPaddedGreaves | $item_legs_pgreaves | Padded Greaves | Legs | Equipment | Expertly crafted leg protection. |
| FW_ArmorTrollLeatherLegs | $item_legs_trollleather | Troll Leather Trousers | Legs | Equipment | Leggings of tough troll hide. |
| GoblinBrute_HipCloth | Iron plate armor |  | Legs | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinLoin | Iron plate armor |  | Legs | misc | An iron scale mail, this will turn all but the strongest of blows. |
| SP_ArmorBronzeLegs | $item_legs_bronze | Bronze Plate Leggings | Legs | Equipment/ShadowPerson | Bronze greaves to shield your legs. |
| SP_ArmorFenringLegs | $item_legs_fenris | Fenris Leggings | Legs | Equipment/ShadowPerson | The legs of the beast were lean and strong, so that it could leap great strides. |
| SP_ArmorLeatherLegs | $item_legs_leather | Leather Trousers | Legs | armor | They squeak a little when you walk. |
| SP_ArmorMageLegs | $item_legs_mage | Eitr-weave Trousers | Legs | Equipment/ShadowPerson | The trousers worn by mages are always especially tight. Discomfort fuels the focus that is needed f… |
| SP_ArmorMageLegs_Ashlands | $item_legs_mage_ashlands | Trousers of Embla | Legs | Equipment/ShadowPerson | Whether or not the first sorceress actually wore trousers exactly like these, we can never know. |
| SP_ArmorPaddedGreaves | $item_legs_pgreaves | Padded Greaves | Legs | Equipment/ShadowPerson | Expertly crafted leg protection. |
| SP_ArmorTrollLeatherLegs | $item_legs_trollleather | Troll Leather Trousers | Legs | Equipment/ShadowPerson | Leggings of tough troll hide. |
| Acorn | $item_oakseeds | Acorns | Material | materials | Plant them to grow an oak tree. |
| Amber | $item_amber | Amber | Material | valuables | <color=yellow>Valuable</color> |
| AmberPearl | $item_amberpearl | Amber Pearl | Material | valuables | <color=yellow>Valuable</color> |
| AncientCoin | $item_ancientcoin | Ancient Coin | Material | valuables | A relic of a lost age. Its surface still bears the trace of mysterious symbols. |
| AncientGemstoneBlack | $item_ancientgemstone_black | Draumyx | Material | valuables | A dark, opaque gem with a smooth and polished surface. |
| AncientGemstoneGreen | $item_ancientgemstone_green | Grimvarn | Material | valuables | A striking green gem, the colour reminiscent of deep forests. |
| AncientGemstoneOrange | $item_ancientgemstone_orange | Solryth | Material | valuables | A vibrant orange stone, like a summer sunset. |
| AncientGemstonePurple | $item_ancientgemstone_purple | Veydris | Material | valuables | A rich purple stone, suitable for royalty. |
| ArmorGoldHeavyChestUncooked | $item_chest_heavy_gold_uncooked | Cast: Breastplate of the Protector | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldHeavyHelmetUncooked | $item_helmet_heavy_gold_uncooked | Cast: Helmet of the Protector | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldHeavyLegsUncooked | $item_legs_heavy_gold_uncooked | Cast: Trousers of the Protector | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMageChestUncooked | $item_chest_mage_gold_uncooked | Cast: Robes of the Caller | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMageHelmetUncooked | $item_helmet_mage_gold_uncooked | Cast: Headdress of the Caller | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMageLegsUncooked | $item_legs_mage_gold_uncooked | Cast: Trousers of the Caller | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMediumChestUncooked | $item_chest_medium_gold_uncooked | Cast: Chestpiece of the Vanguard | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMediumHelmetUncooked | $item_helmet_medium_gold_uncooked | Cast: Hood of the Vanguard | Material | consumables | This armour needs to be hardened with frost. |
| ArmorGoldMediumLegsUncooked | $item_legs_medium_gold_uncooked | Cast: Trousers of the Vanguard | Material | consumables | This armour needs to be hardened with frost. |
| AskBladder | $item_askbladder | Asksvin Bladder | Material | materials | An acidic smell still lingers. Prominently. |
| AskHide | $item_askhide | Asksvin Hide | Material | materials | This sturdy leather is thick, and still warm to the touch. |
| AsksvinCarrionNeck | $item_asksvincarrionneck | Asksvin Neck | Material | materials | A neck in its final stage of life. |
| AsksvinCarrionPelvic | $item_asksvincarrionpelvic | Asksvin Pelvis | Material | materials | The pelvic bone of a four legged creature. |
| AsksvinCarrionRibcage | $item_asksvincarrionribcage | Asksvin Ribcage | Material | materials | These ribs have already been stripped clean of any meat. |
| AsksvinCarrionSkull | $item_asksvincarrionskull | Asksvin Skull | Material | materials | A thick skull, with room for a surprisingly large brain. |
| AsksvinMeat | $item_asksvin_meat | Asksvin Tail | Material | consumables | Smells a bit smokey, even when raw. |
| AtgeirGoldUncooked | $item_atgeir_gold_uncooked | Cast: Nord Atgeir | Material | weapons | This weapon needs to be hardened with frost. |
| AxeGoldUncooked | $item_axe_gold_uncooked | Cast: Nord Axe | Material | weapons | This weapon needs to be hardened with frost. |
| AxeHead1 | $item_axehead1 | Curious Axe Head | Material | materials | The metal glints oddly in the sunlight. Somehow, you know that it can never be complete on its own.… |
| AxeHead2 | $item_axehead2 | Mysterious Axe Head | Material | materials | What battles has this weapon borne witness to? You cannot see its past, only shape its future... |
| BakedPoteitrUncooked | $item_bakedpoteitr_uncooked | Unbaked Poteitr | Material | consumables | Ready for the oven. |
| BarkaBranch | $item_barkabranch | Frozen Branch | Material | materials | This piece of wood was once animated and alive. Still a strange, magical air clings to it. |
| Barley | $item_barley | Barley | Material | materials | A bundle of barley. |
| BarleyWineBase | $item_barleywinebase | Barley Wine Base: Fire Resistance | Material | consumables | Needs to be fermented. |
| BattleaxeGoldUncooked | $item_battleaxe_gold_uncooked | Cast: Nord Greataxe | Material | weapons | This weapon needs to be hardened with frost. |
| BeechSeeds | $item_beechseeds | Beech Seeds | Material | materials | Plant them to grow a beech tree. |
| BellFragment | $item_bellfragment | Bell Fragment | Material | misc | This ancient fragment appears to be a piece of a broken bell... |
| Bilebag | $item_bilebag | Bilebag | Material | materials | Caustic bile drawn from the corpse of a gjall. |
| BirchSeeds | $item_birchseeds | Birch Seeds | Material | materials | Plant them to grow a birch tree. |
| BjornHide | $item_bjornhide | Bear Hide | Material | materials | A thick and furry hide, coarse yet incredibly soft. |
| BjornMeat | $item_bjorn_meat | Bear Meat | Material | consumables | This fatty meat was once meant to sustain a body through hibernation. |
| BjornPaw | $item_bjornpaw | Bear Paw | Material | materials | Sharp claws suitable for picking berries – or for dismembering. |
| BlackCore | $item_blackcore | Black Core | Material | consumables | Filled to the brim with ancient power. |
| BlackMarble | $item_blackmarble | Black Marble | Material | materials | A block of solid stone, seamed with shifting colors. |
| BlackMetal | $item_blackmetal | Black Metal | Material | materials | A heavy bar of dark metal. |
| BlackMetalScrap | $item_blackmetalscrap | Black Metal Scrap | Material | materials | A twisted hunk of dark metal. |
| Blackwood | $item_blackwood | Ashwood | Material | materials | Wood hardened by fire and ash. |
| BlobVial | $item_blobvial | Corked Vial | Material | misc | Thick enough to contain something volatile, yet fragile enough to be shattered. |
| Bloodbag | $item_bloodbag | Bloodbag | Material | materials | The contents of a leech. Ick! |
| BombSiege | $item_catapult_ammo | Explosive Payload | Material | weapons | Best used with a catapult. Handle with care. |
| BoneFragments | $item_bonefragments | Bone Fragments | Material | materials | A pile of shattered bones. |
| BoneMawSerpentMeat | $item_bonemawmeat | Bonemaw Meat | Material | consumables | A tasty, white fish meat. Very good for your bones! |
| BonemawSerpentScale | $item_bonemawscale | Bonemaw Scale | Material | materials | It looks just like bone. Best not to question the anatomy of this creature too much... |
| BonemawSerpentTooth | $item_bonemawtooth | Bonemaw Tooth | Material | materials | This has caused the death of many a brave sailor. |
| BowGoldUncooked | $item_bow_gold_uncooked | Cast: Nord Bow | Material | weapons | This weapon needs to be hardened with frost. |
| BreadDough | $item_breaddough | Bread Dough | Material | materials | Ready for the oven. |
| Bronze | $item_bronze | Bronze | Material | materials | A strong alloy of copper and tin. |
| BronzeNails | $item_bronzenails | Bronze Nails | Material | materials | Used in construction of ships and furniture. |
| BronzeScrap | $item_bronzescrap | Scrap Bronze | Material | materials | It's old and oxidized but can be smelted and used again. |
| BugMeat | $item_bug_meat | Seeker Meat | Material | consumables | When you crack open their shells, the meat within is tender and succulent. |
| CandleWick | $item_candlewick | Candle Wick | Material | materials | Steep these in something flammable for a long lasting and cosy light source. |
| Carapace | $item_carapace | Carapace | Material | materials | A plate of chitinous armour. |
| CarrotSeeds | $item_carrotseeds | Carrot Seeds | Material | materials | Plant these if you like carrots... |
| Catapult_Ammo_BloodGold | $item_catapult_bloodgold_ammo | Bloodgold Payload | Material |  | Hit 'em hard and hit 'em bloody. |
| Catapult_ammo | $item_catapult_training_ammo | Grausten Payload | Material |  | Best used with a catapult. Make sure nothing fragile is in the way. / |
| CelestialFeather | $item_celestialfeather | Celestial Feather | Material | materials | The only remnant of the fallen valkyrie's former self. |
| CeramicPlate | $item_ceramicplate | Ceramic Plate | Material | materials | No matter how hot this gets, the other side of it remains strangely cool. |
| Chain | $item_chain | Chain | Material | materials | A link of iron chain. |
| CharcoalResin | $item_charcoalresin | Charcoal Resin | Material | materials | The resin from a tree that was set ablaze a long time ago. It's still ready to burn some more. |
| CharredBone | $item_charredbone | Charred Bone | Material | materials | Followed by the distinct smell of burnt meat. |
| CharredCogwheel | $item_charredcogwheel | Charred Cogwheel | Material | materials | This could be used for some clever machinery... |
| Charredskull | $item_charredskull | Charred Skull | Material | materials | The blackened skull of a long-dead warrior. It's unlikely that a proper burial would grant them any… |
| ChickenMeat | $item_chicken_meat | Chicken Meat | Material | consumables | All chickens bear the ancestral curse of being delicious. |
| Chitin | $item_chitin | Chitin | Material | materials | A shard of crustacean shell. |
| Coal | $item_coal | Coal | Material | materials | A lump of coal. |
| Coins | $item_coins | Coins | Material | valuables | <color=yellow>Valuable</color> |
| Copper | $item_copper | Copper | Material | materials | A bar of pure copper ready to be worked. |
| CopperOre | $item_copperore | Copper Ore | Material | materials | Unrefined copper. Needs to be refined in a smelter. |
| CopperScrap | $item_copperscrap | Copper Scrap | Material | materials | One person's scrap is another person's treasure. |
| CrossbowGoldUncooked | $item_crossbow_gold_uncooked | Cast: Nord Crossbow | Material | weapons | This weapon needs to be hardened with frost. |
| CrownJewel | $item_crownjewel | Crown Jewel | Material | materials | A strange power surges within this gem, unlike anything you've felt before. |
| Crystal | $item_crystal | Crystal | Material | materials | A shard of crystal from deep within the earth. |
| CuredSquirrelHamstring | $item_curedsquirrelhamstring | Cured Squirrel Hamstring | Material | materials | Elastic and strong. This tendon must have come from a quick and agile animal. |
| Dandelion | $item_dandelion | Dandelion | Material | materials | Some call it a weed, but it's pretty nonetheless. |
| DeerHide | $item_deerhide | Deer Hide | Material | materials | A cleaned hide from a deer. |
| DeerMeat | $item_deer_meat | Deer Meat | Material | consumables |  |
| DvergrKeyFragment | $item_dvergrkeyfragment | Sealbreaker Fragment | Material | misc | A fragment of a Dvergr sealbreaker. |
| DvergrNeedle | $item_dvergrneedle | Dvergr Extractor | Material | materials | Looks like a perfect piece for piercing something... |
| DyrnwynBladeFragment | $item_Dyrnwyn_blade | Dyrnwyn Blade Fragment | Material | misc | Parts of an old blade. If all the pieces were reassembled it could likely be made whole. |
| DyrnwynHiltFragment | $item_Dyrnwyn_hilt | Dyrnwyn Hilt Fragment | Material | misc | The hilt of a long forgotten sword. Perhaps it could be reforged if one had all the pieces... |
| DyrnwynTipFragment | $item_Dyrnwyn_tip | Dyrnwyn Tip Fragment | Material | misc | This shard of metal looks like the tip of a sword. One might be able to reforge the blade if more p… |
| Ectoplasm | $item_ectoplasm | Ectoplasm | Material | materials | A restless essence of a once living thing... |
| Eitr | $item_eitr | Refined Eitr | Material | materials | This is the stuff of life, the poison that consumes itself. The Dvergr refine it to use in their es… |
| ElakingHairBundle | $item_elakinghairbundle | Elaking Hair Bundle | Material | materials | The fur is dense, coarse, and surprisingly clean. |
| ElderBark | $item_elderbark | Ancient Bark | Material | materials | An ancient and sturdy material. |
| Entrails | $item_entrails | Entrails | Material | materials | A slimy length of something's insides. |
| FaderDrop | $item_fader_drop | Kindled Ribs | Material | misc | The smouldering remains of a patriarch. |
| FaderEmber | $item_faderember | Embers | Material | materials | Every flying ember is a burning wish to repent. |
| FeastAshlands_Material | $item_feastashlands | Ashlands Gourmet Bowl | Material | materials | It's hard to tell whether the steam coming off of this dish is because it's freshly cooked or becau… |
| FeastBlackforest_Material | $item_feastblackforest | Black Forest Buffet Platter | Material | materials | You won't be able to resist this platter of delights from the Black Forest! Venison sirloin steaks… |
| FeastDeepNorth_Material | $item_feastdeepnorth | Northern Morning Fare | Material | materials | Warming and filling, this meal will sustain you even during the coldest of days. Porridge and panca… |
| FeastMeadows_Material | $item_feastmeadows | Whole Roasted Meadow Boar | Material | materials | A boar that has been roasted to perfection, glazed and served atop a bed of greens, with additional… |
| FeastMistlands_Material | $item_feastmistlands | Mushrooms Galore á la Mistlands | Material | materials | The time has come for mushroom enthusiasts to rejoice! Try different kinds of mushrooms, mushroom m… |
| FeastMountains_Material | $item_feastmountains | Hearty Mountain Logger's Stew | Material | materials | Gather around this steaming pot full of deliciousness and warm yourselves up again after a day out… |
| FeastOceans_Material | $item_feastoceans | Sailor's Bounty | Material | materials | Fish, fish, and more fish! And also serpent meat, cut to look like fish! Explore the flavours of th… |
| FeastPlains_Material | $item_feastplains | Plains Pie Picnic | Material | materials | There's nothing plain about this feast! Enjoy pies and loaves fresh from the oven, both sweet and s… |
| FeastSwamps_Material | $item_feastswamps | Swamp Dweller's Delight | Material | materials | Who knew that leeches were edible? With the correct preparation (lots of cooking and lots of season… |
| Feathers | $item_feathers | Feathers | Material | materials | A small pile of feathers. |
| FineWood | $item_finewood | Finewood | Material | materials | High quality wood for fine carpentry. |
| FirCone | $item_fircone | Fir Cone | Material | materials | Plant it to grow a fir tree. |
| FirConeFrost | $item_fircone_big | Timberwood Cone | Material | materials | Plant it to grow a timberwood tree. |
| FireworksRocket_Blue | $item_fireworkrocket_blue | Blue Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_Cyan | $item_fireworkrocket_cyan | Cyan Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_Green | $item_fireworkrocket_green | Green Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_Purple | $item_fireworkrocket_purple | Purple Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_Red | $item_fireworkrocket_red | Red Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_White | $item_fireworkrocket_white | Basic Fireworks | Material | materials | This rocket's blasting off again! |
| FireworksRocket_Yellow | $item_fireworkrocket_yellow | Yellow Fireworks | Material | materials | This rocket's blasting off again! |
| FishAndBreadUncooked | $item_fishandbreaduncooked | Uncooked Fish 'n' Bread | Material | materials | Ready for the oven. |
| FishAnglerRaw | $item_fish_raw | Raw Fish | Material | consumables | A good catch. |
| FishRaw | $item_fish_raw | Raw Fish | Material | consumables | A good catch. |
| FistGoldUncooked | $item_fistweapon_gold_uncooked | Cast: Nord Knucklechains | Material | weapons | This weapon needs to be hardened with frost. |
| Flametal | $item_flametal_old | Ancient Metal | Material | materials | A withered metal from ancient times. No one knows what it was once used for. |
| FlametalNew | $item_flametal | Flametal | Material | materials | According to legend, this metal was used by the gods themselves to craft powerful weapons. |
| FlametalOre | $item_flametalore_old | Glowing Metal Ore | Material | materials | A withered metal from ancient times. No one knows what it was once used for. |
| FlametalOreNew | $item_flametalore | Flametal Ore | Material | materials | Warm to the touch with glowing veins of strange metal. Needs to be refined in a blast furnace. |
| Flax | $item_flax | Flax | Material | materials | Unspun fibers from a flax plant. |
| Flint | $item_flint | Flint | Material | materials | Can be shaped into sharp blades. |
| FragrantBundle | $item_fragrantbundle | Fragrant Bundle | Material | materials | These plants carry a strong but pleasant scent. However, it's possible that not all creatures agree… |
| FreezeGland | $item_freezegland | Freeze Gland | Material | materials | This mysterious organ keeps a perfect temperature. |
| FreshSeaweed | $item_freshseaweed | Fresh Seaweed | Material | materials | The saltwater scent of this plant makes you think of the wide open ocean. |
| FrostCore | $item_frostcore | Frostcore | Material | consumables | Terribly cold to the touch, filled with frozen energy. |
| Frostwood | $item_frostwood | Timberwood | Material | materials | A sturdy kind of wood, excellent for mighty halls. |
| FrozenFuel | $item_frozenfuel | Liquid Frost | Material | materials | Magic has infused this ice, turning it into something else entirely. |
| FrozenKingDrop | $item_frozenking_drop | Sacrificial Blood | Material | misc | The last essence of an end once foretold. |
| GemstoneBlue | $item_gemstone_blue | Iolite | Material | materials | Light is reflected sharply off of this gem, or does it come from within the stone itself? |
| GemstoneGreen | $item_gemstone_green | Jade | Material | materials | This gem pulses with energy, almost as if it were a living thing. |
| GemstoneRed | $item_gemstone_red | Bloodstone | Material | materials | You wonder how many deals have been made to appease this gem. How many palms have bled onto it in e… |
| GenericMoldUncooked | $item_smallparts_gold_uncooked |  | Material | consumables |  |
| GiantBloodSack | $item_bloodclot | Blood Clot | Material | materials | Be careful not to puncture this while you carry it... |
| Gold | $item_gold | Bloodgold | Material | materials | Precious metal, infused with the essence of a living thing. |
| GoldOre | $item_goldore | Petrified Tissue | Material | materials | A hard and sturdy material with plenty of potential. Needs to be refined in a Blast Furnace. |
| Grausten | $item_grausten | Grausten | Material | materials | Porous yet sturdy. |
| GreydwarfEye | $item_greydwarfeye | Greydwarf Eye | Material | materials | The milky eyeball of a Greydwarf. |
| Guck | $item_guck | Guck | Material | materials | It smells like fermented fish. |
| HardAntler | $item_hardantler | Hard Antler | Material | materials | A piece of very hard antlers. |
| HareMeat | $item_hare_meat | Hare Meat | Material | consumables | The meat of a hare is scant but toothsome. |
| HoneyGlazedChickenUncooked | $item_honeyglazedchickenuncooked | Uncooked Honey Glazed Chicken | Material | materials | Ready for the oven. |
| Hook | $item_hook | Hook | Material | materials | A finely wrought item, with a gripping potential. |
| Ice | $item_ice | Ice | Material | materials | So cold... |
| Iron | $item_iron | Iron | Material | materials | A bar of pure iron ready to be worked. |
| IronNails | $item_ironnails | Iron Nails | Material | materials | Needed for advanced construction projects. |
| IronOre | $item_ironore | Iron Ore | Material | materials | Unrefined iron. Needs to be refined in a smelter. |
| IronScrap | $item_ironscrap | Scrap Iron | Material | materials | It's old and rusty but can be smelted and used again. |
| Ironpit | $item_ironpit | Iron Pit | Material | materials | An empty vessel waiting to be filled with firewood and kindling. |
| JuteBlue | $item_juteblue | Blue Jute | Material | materials | Made from natural fibers and dvergr hair. |
| JuteRed | $item_jutered | Red Jute | Material | materials | A sturdy, rough fabric. |
| KaleChipsUncooked | $item_kalechips_uncooked | Raw Kale Chips | Material | consumables | Ready for the oven. |
| KaleSeeds | $item_kaleseeds | Kale Seeds | Material | materials | Plant to grow kale. |
| KeysGoldUncooked | $item_keys_gold_uncooked | Cast: Intricate Key | Material | consumables | The keys need to be hardened with frost. |
| KnifeGoldUncooked | $item_knife_gold_uncooked | Cast: Nord Dagger | Material | weapons | This weapon needs to be hardened with frost. |
| Larva | $item_larva |  | Material | consumables |  |
| LastBossGate_RuneTile | $item_runetile |  | Material | DeepNorth/LastBossGate |  |
| LeatherScraps | $item_leatherscraps | Leather Scraps | Material | materials | A small pile of leather scraps. |
| Leatherstraps | $item_leatherstraps | Leather Straps | Material | materials | A sturdy yet flexible material. |
| LinenThread | $item_linenthread | Linen Thread | Material | materials | A fine linen thread made out of a strong flax filament. |
| LoxMeat | $item_loxmeat | Lox Meat | Material | consumables | A raw slab of marbled meat. |
| LoxPelt | $item_loxpelt | Lox Pelt | Material | materials | A heavy pelt of thick, musty fur. |
| LoxPieUncooked | $item_loxpie_uncooked | Unbaked Lox Pie | Material | consumables | Ready for the oven. |
| MaceGoldUncooked | $item_mace_gold_uncooked | Cast: Nord Mace | Material | weapons | This weapon needs to be hardened with frost. |
| MagicallyStuffedShroomUncooked | $item_magicallystuffedmushroomuncooked | Uncooked Stuffed Mushroom | Material | consumables | Ready for the oven. |
| Mandible | $item_mandible | Mandible | Material | materials | The hand of man could hardly design a more perfect weapon. |
| MeadBaseBugRepellent | $item_meadbasebugrepellent | Mead Base: Anti-Sting | Material | consumables | Needs to be fermented. |
| MeadBaseBzerker | $item_meadbasebzerker | Mead base: Berserkir | Material | consumables | Needs to be fermented. |
| MeadBaseEitrLingering | $item_meadbaseeitr_lingering | Mead Base: Lingering Eitr | Material | consumables | Needs to be fermented. |
| MeadBaseEitrMinor | $item_meadbaseeitr | Mead Base: Minor Eitr | Material | consumables | Needs to be fermented. |
| MeadBaseFrostResist | $item_meadbasefrostresist | Mead Base: Frost Resistance | Material | consumables | Needs to be fermented. |
| MeadBaseHasty | $item_meadbasehasty | Mead base: Ratatosk | Material | consumables | Needs to be fermented. |
| MeadBaseHealthLingering | $item_meadbasehealth_lingering | Mead Base: Lingering Health | Material | consumables | Needs to be fermented. |
| MeadBaseHealthMajor | $item_meadbasehealth_major | Mead Base: Major Healing | Material | consumables | Needs to be fermented. |
| MeadBaseHealthMedium | $item_meadbasehealth_medium | Mead Base: Medium Healing | Material | consumables | Needs to be fermented. |
| MeadBaseHealthMinor | $item_meadbasehealth | Mead Base: Minor Healing | Material | consumables | Needs to be fermented. |
| MeadBaseLightFoot | $item_meadbaselightfoot | Mead Base: Lightfoot | Material | consumables | Needs to be fermented. |
| MeadBasePoisonResist | $item_meadbasepoisonresist | Mead Base: Poison Resistance | Material | consumables | Needs to be fermented. |
| MeadBaseStaminaLingering | $item_meadbasestamina_lingering | Mead Base: Lingering Stamina | Material | consumables | Needs to be fermented. |
| MeadBaseStaminaMedium | $item_meadbasestamina_medium | Mead Base: Medium Stamina | Material | consumables | Needs to be fermented. |
| MeadBaseStaminaMinor | $item_meadbasestamina | Mead Base: Minor Stamina | Material | consumables | Needs to be fermented. |
| MeadBaseStrength | $item_meadbasestrength | Mead Base: Troll Endurance | Material | consumables | Needs to be fermented. |
| MeadBaseSwimmer | $item_meadbaseswimmer | Mead Base: Vananidir | Material | consumables | Needs to be fermented. |
| MeadBaseTamer | $item_meadbasetamer | Mead Base: Animal Whispers | Material | consumables | Needs to be fermented. |
| MeadBaseTasty | $item_meadbasetasty | Mead Base: Tasty | Material | consumables | Needs to be fermented. |
| MeatPlatterUncooked | $item_meatplatteruncooked | Uncooked Meat Platter | Material | materials | Ready for the oven. |
| MechanicalSpring | $item_mechanicalspring | Mechanical Spring | Material | materials | A mysterious contraption built by the Dvergr. Used to build traps. |
| MemorialCoal | $item_memorialcoal | Memorial Coal | Material | materials | Somewhere deep within the hot, hazy glow, you can almost see an old memory play out... |
| MisthareSupremeUncooked | $item_mistharesupremeuncooked | Uncooked Misthare Supreme | Material | materials | Ready for the oven. |
| MoldArmorGoldChest | $item_mold_armor_gold_chest | Mould: Breastplate of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorGoldHelmet | $item_mold_armor_gold_helmet | Mould: Helmet of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorGoldLegs | $item_mold_armor_gold_legs | Mould: Trousers of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageChest | $item_mold_armor_mage_chest | Mould: Robes of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageHelmet | $item_mold_armor_mage_helmet | Mould: Headdress of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageLegs | $item_mold_armor_mage_legs | Mould: Trousers of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMediumHelmet | $item_mold_armor_medium_helmet | Mould: Hood of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMediumLegs | $item_mold_armor_medium_legs | Mould: Trousers of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmormediumChest | $item_mold_armor_medium_chest | Mould: Chestpiece of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldAtgeir | $item_mold_atgeir | Mould: Nord Atgeir | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldAxe | $item_mold_axe | Mould: Nord Axe | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldAxe2H | $item_mold_axe2h | Mould: Nord Greataxe | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldBow | $item_mold_bow | Mould: Nord Bow | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldCrossbow | $item_mold_crossbow | Mould: Nord Crossbow | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldFistweapon | $item_mold_fistweapon | Mould: Nord Knucklechains | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldKeys | $item_mold_keys | Mould: Intricate Key | Material | consumables | Filled with the right material, this mould will create a powerful key. |
| MoldKnife | $item_mold_knife | Mould: Nord Dagger | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldMace | $item_mold_mace | Mould: Nord Mace | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldMace2H | $item_mold_mace2h | Mould: Nord Sledge | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldShieldBuckler | $item_mold_shield_buckler | Mould: Nord Buckler | Material | consumables | Filled with the right material, this mould will create a hardy shield. |
| MoldShieldRound | $item_mold_shield_round | Mould: Nord Shield | Material | consumables | Filled with the right material, this mould will create a hardy shield. |
| MoldShieldTower | $item_mold_shield_tower | Mould: Nord Greatshield | Material | consumables | Filled with the right material, this mould will create a hardy shield. |
| MoldSmallParts | $item_mold_smallparts |  | Material | consumables |  |
| MoldSpear | $item_mold_spear | Mould: Nord Spear | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldStaffOrbofAhri | $item_mold_stafforbofahri | Mould: Echo Spike | Material | consumables | Filled with the right material, this mould will create a powerful magical item. |
| MoldStafffrostorbs | $item_mold_stafffrostorbs | Mould: Northern Vengeance | Material | consumables | Filled with the right material, this mould will create a powerful magical item. |
| MoldStaffspiritcaller | $item_mold_staffspiritcaller | Mould: Spirit Caller | Material | consumables | Filled with the right material, this mould will create a powerful magical item. |
| MoldStaffthunderblood | $item_mold_staffthunderblood | Mould: Lightning Strike | Material | consumables | Filled with the right material, this mould will create a powerful magical item. |
| MoldSword | $item_mold_sword | Mould: Nord Sword | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoldSword2H | $item_mold_sword2h | Mould: Nord Greatsword | Material | consumables | Filled with the right material, this mould will create a most formidable weapon. |
| MoleClaws | $item_moleclaws | Long Claws | Material | materials | A lethal weapon, if one can hold them without getting cut. |
| MoltenCore | $item_moltencore | Molten Core | Material | consumables | Potent energy swirls within, ready to be unleashed. |
| MooseHide | $item_moosehide | Moose Hide | Material | materials | This fur is perfectly adapted to northern climates. |
| MooseMeat | $item_moose_meat | Moose Meat | Material | consumables | This meat is sure to provide a hearty meal once cooked. |
| MooseSinew | $item_moosesinew | Moose Sinew | Material | materials | Tough and hardy, this is sure to come in handy. |
| MorgenHeart | $item_morgenheart | Morgen Heart | Material | materials | It's hard to believe it has ever beaten. |
| MorgenSinew | $item_morgensinew | Morgen Sinew | Material | materials | Chewy. |
| NeckTail | $item_necktail | Neck Tail | Material | materials | Inedible when raw, but proves to be quite a tasty snack if cooked. |
| Needle | $item_needle | Needle | Material | materials | The pointy end of a Deathsquito. |
| NornThread | $item_nornthread | Nornathread | Material | materials | Don't let the delicate strands fool you. These threads are spun from the power of the world tree it… |
| OatSeeds | $item_oatseeds | Oat Seeds | Material | materials | Grind them to oats in the mill, or plant to grow more seeds. |
| Obsidian | $item_obsidian | Obsidian | Material | materials | Dark volcanic glass. |
| OnionSeeds | $item_onionseeds | Onion Seeds | Material | materials | Plant to grow a healthy onion. |
| Ooze | $item_ooze | Ooze | Material | materials | Rotten and putrid-smelling. Why do you want this? |
| OozeMork | $item_ooze_mork | Dead Pulp | Material | materials | It's best not to think about what this consists of. |
| OrbFrostFire | $item_orbfrostfire | Frostfire Essence | Material | materials | Somehow both hot and cold to the touch. |
| OrbThunderBlood | $item_orbthunderblood | Thunderblood Essence | Material | materials | Unstable, erratic and...alive? |
| OvenPancakeUncooked | $item_ovenpancake_uncooked | Oven Pancake Batter | Material | consumables | Ready for the oven. |
| PineCone | $item_pinecone | Pine Cone | Material | materials | Plant it to grow a pine tree. |
| PiquantPieUncooked | $item_piquantpie_uncooked | Uncooked Piquant Pie | Material | consumables | Ready for the oven. |
| Pot_Shard_Green | $item_pot_shard_green | Pot Shard | Material | materials | A fragment of something brittle. |
| Pot_Shard_Red | $item_pot_shard_red |  | Material | materials | A fragment of something brittle. |
| PoteitrSeeds | $item_poteitrseeds | Seed Poteitr | Material | materials | Plant to grow poteitr. |
| PowderedDragonEgg | $item_powdereddragonegg | Powdered Dragon Eggshells | Material | materials | Dragon egg is a hard and difficult material to work with, yet here it has been ground to a fine, gl… |
| ProustitePowder | $item_proustitepowder | Proustite Powder | Material | materials | This unstable powder packs great potential. |
| PungentPebbles | $item_pungentpebbles | Pungent Pebbles | Material | materials | An earthy odour clings to these dried lumps. It's best to not think too hard about what they are. |
| QueenBee | $item_queenbee | Queen Bee | Material | materials | The queen of the bees! |
| QueenDrop | $item_seekerqueen_drop | Majestic Carapace | Material | misc | Her majesty's will was hard and unrelenting, but this piece of carapace is perhaps even more so. |
| RawMeat | $item_boar_meat | Boar Meat | Material | consumables |  |
| Resin | $item_resin | Resin | Material | materials | Sticky tree resin which insulates well. If put to the flame it burns slow and steady. |
| RoastedCrustPieUncooked | $item_roastedcrustpie_uncooked | Uncooked Roasted Crust Pie | Material | consumables | Ready for the oven. |
| Root | $item_root | Root | Material | materials | An old root from an ancient tree stump. It feels both flexible and durable at the same time. |
| RoundLog | $item_roundlog | Corewood | Material | materials | Perfect for building log cabins. |
| Ruby | $item_ruby | Ruby | Material | valuables | <color=yellow>Valuable</color> |
| Sap | $item_sap | Sap | Material | materials | Sacred blood from the Great Tree. |
| ScaleHide | $item_scalehide | Scale Hide | Material | materials | A pelt of glittering scales. |
| SealBlubber | $item_blubber | Seal Blubber | Material | consumables | The insulating fat of a creature adapted to the northern waters. |
| SealHide | $item_sealhide | Seal Pelt | Material | materials | The thick fur helps the animal stay both warm and dry. |
| SerpentMeat | $item_serpentmeat | Serpent Meat | Material | consumables | A slice of sea serpent. Smells fishy. |
| SerpentScale | $item_serpentscale | Serpent Scale | Material | materials | The shiny metal-like scale from a sea serpent. |
| SharpeningStone | $item_sharpeningstone | Sharpening Stone | Material | materials | A whetstone wheel ready to spin. |
| ShieldBucklerGoldUncooked | $item_shield_buckler_gold_uncooked | Cast: Nord Buckler | Material | consumables | This shield needs to be hardened with frost. |
| ShieldCore | $item_shieldcore | Shield Core | Material | consumables | A protective force within is ready to be unleashed. |
| ShieldRoundGoldUncooked | $item_shield_round_gold_uncooked | Cast: Nord Shield | Material | consumables | This shield needs to be hardened with frost. |
| ShieldTowerGoldUncooked | $item_shield_tower_gold_uncooked | Cast: Nord Greatshield | Material | consumables | This shield needs to be hardened with frost. |
| Silver | $item_silver | Silver | Material | materials | A bar of pure silver ready to be worked. |
| SilverNecklace | $item_silvernecklace | Silver Necklace | Material | valuables | <color=yellow>Valuable</color> |
| SilverOre | $item_silverore | Silver Ore | Material | materials | Unrefined silver. Needs to be refined in a smelter. |
| SledgeGoldUncooked | $item_sledge_gold_uncooked | Cast: Nord Sledge | Material | weapons | This weapon needs to be hardened with frost. |
| SmallPartsGoldUncooked | $item_smallparts_gold_uncooked |  | Material | consumables |  |
| Softtissue | $item_softtissue | Soft Tissue | Material | consumables | It still fizzes softly with ancient memories. |
| SpearGoldUncooked | $item_spear_gold_uncooked | Cast: Nord Spear | Material | weapons | This weapon needs to be hardened with frost. |
| SpiceAshlands | $item_spiceashlands | Fiery Spice Powder | Material | materials | Whatever spices have been used in this blend, they must come from someplace hot. And as if that was… |
| SpiceDeepNorth | $item_spicedeepnorth | Seasoning of the Gourd | Material | materials | Using cinnamon bark and ginger root, with a touch of nutmeg, the herbalist has travelled far to cre… |
| SpiceForests | $item_spiceforests | Woodland Herb Blend | Material | materials | Thyme and marjoram carefully harvested and dried, before being mixed together. This blend will turn… |
| SpiceMistlands | $item_spicemistlands | Herbs of the Hidden Hills | Material | materials | A good herbalist knows exactly where to go to find ramsons, but she won't tell you the secrets of h… |
| SpiceMountains | $item_spicemountains | Mountain Peak Pepper Powder | Material | materials | Only the most seasoned herbalist can find the pepperwood tree and harvest its leaves and bark. Afte… |
| SpiceOceans | $item_spiceoceans | Seafarer's Herbs | Material | materials | The sour tang of sorrel, mixed with something the herbalist would rather not disclose, is well suit… |
| SpicePlains | $item_spiceplains | Grasslands Herbalist Harvest | Material | materials | This blend utilises all aspects of the lovage plant, a rarity in the tenth world. Roots, leaves and… |
| StaffFrostOrbsUncooked | $item_frostorbs_uncooked | Cast: Northern Vengeance | Material | weapons | This magical item needs to be hardened with frost. |
| StaffOrbofAhriUncooked | $item_staff_orbofahri_uncooked | Cast: Echo Spike | Material | weapons | This magical item needs to be hardened with frost. |
| StaffSpiritCallerUncooked | $item_staff_spiritcaller_uncooked | Cast: Spirit Caller | Material | weapons | This magical item needs to be hardened with frost. |
| StaffThunderbloodUncooked | $item_staff_thunderblood_uncooked | Cast: Lightning Strike | Material | weapons | This magical item needs to be hardened with frost. |
| Stone | $item_stone | Stone | Material | materials | It's a rock. |
| StoneRock | $item_stonerock | Rock | Material | materials | It's a stone. |
| SulfurStone | $item_sulfurstone | Sulfur | Material | materials | Smells like rotten eggs. |
| SurtlingCore | $item_surtlingcore | Surtling Core | Material | materials | It throbs with inner heat. |
| SwordGoldUncooked | $item_sword_gold_uncooked | Cast: Nord Sword | Material | weapons | This weapon needs to be hardened with frost. |
| THSwordGoldUncooked | $item_sword2h_gold_uncooked | Cast: Nord Greatsword | Material | weapons | This weapon needs to be hardened with frost. |
| Tar | $item_tar | Tar | Material | materials | A sticky lump of tar. |
| Thistle | $item_thistle | Thistle | Material | materials | Beautiful but prickly. |
| Thunderstone | $item_thunderstone | Thunder Stone | Material | materials | It is crackling with energy. |
| Tin | $item_tin | Tin | Material | materials | A bar of pure tin ready to be worked. |
| TinOre | $item_tinore | Tin Ore | Material | materials | Unrefined tin. Needs to be refined in a smelter. |
| TrollHide | $item_trollhide | Troll Hide | Material | materials | A thick and sturdy hide. This is why trolls are so hard to kill. |
| Turnip | $item_turnip | Turnip | Material | materials | A dense vegetable, rich with nutrition. |
| TurnipSeeds | $item_turnipseeds | Turnip Seeds | Material | materials | Plant to grow a healthy turnip. |
| UndeadBjornRibcage | $item_UndeadBjornRibcage | Vile Ribcage | Material | materials | A relic of savage strength, perfect for forging armour that bears the wild’s fury. |
| Upgrader0Armor | $item_upgrader_tier0 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader0Weapon | $item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader1Armor | $item_upgrader_tier1 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader1Weapon | $item_upgrader_tier1 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader2Armor | $item_upgrader_tier2 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader2Weapon | $item_upgrader_tier2 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader3Armor | $item_upgrader_tier3 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader3Weapon | $item_upgrader_tier3 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader4Armor | $item_upgrader_tier4 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader4Weapon | $item_upgrader_tier4 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader5Armor | $item_upgrader_tier5 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader5Weapon | $item_upgrader_tier5 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader6Armor | $item_upgrader_tier6 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader6Weapon | $item_upgrader_tier6 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader7Armor | $item_upgrader_tier7 $item_upgrader_armor $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| Upgrader7Weapon | $item_upgrader_tier7 $item_upgrader_weapon $item_upgrader_name |  | Material | Upgrades | This mysterious idol is brimming with the potential of the gods. It could grant a powerful boon, or… |
| VegvisirShard_Bonemass | Yagluth thing |  | Material | misc |  |
| VikingCupcakeUncooked | $item_vikingcupcake_uncooked | Unbaked Sweetbread | Material | consumables | Ready for the oven. |
| VineGreenSeeds | $item_vinegreenseeds | Ivy Seeds | Material | Vines_Green | These unassuming seeds can grow into vines that might overtake entire buildings. |
| VineberrySeeds | $item_vineberryseeds | Vineberry Seeds | Material | Vines_Ashlands | Looks like they could crumble at any moment. |
| Voidplasm | $item_ectoplasm | Ectoplasm | Material | materials | A restless essence of a once living thing... |
| VoltureMeat | $item_volture_meat | Volture Meat | Material | consumables | Given that this bird feasts on all kinds of things, you'd better cook its meat before you even thin… |
| Wisp | $item_wisp | Wisp | Material | materials | It keeps whispering jibberish... |
| WitheredBone | $item_witheredbone | Withered Bone | Material | misc | A giant bone, knotted like old wood. |
| WolfClaw | $item_wolfclaw | Fenris Claw | Material | materials | It is hard and sharp like iron. |
| WolfFang | $item_wolffang | Wolf Fang | Material | materials | Still sharp. |
| WolfHairBundle | $item_wolfhairbundle | Fenris Hair | Material | materials | A bundle of thick, rough hair. It has a strong smell. |
| WolfMeat | $item_wolf_meat | Wolf Meat | Material | consumables |  |
| WolfPelt | $item_wolfpelt | Wolf Pelt | Material | materials | A pelt of shaggy fur. |
| Wood | $item_wood | Wood | Material | materials | Good, strong wood to build with. |
| WrithanRoots | $item_writhanroots | Writhan Roots | Material | materials | The gnarled body parts of a strange, offputting creature. |
| YggdrasilWood | $item_yggdrasilwood | Yggdrasil Wood | Material | materials | Godflesh, wood from the Great Tree. |
| YmirRemains | $item_ymirremains | Ymir Flesh | Material | materials | The earthy remains of the giant Ymir. |
| AncientSeed | $item_ancientseed | Ancient Seed | Misc | materials | Held against your ear, you hear tiny whisperings within... |
| AsksvinEgg | $item_asksvin_egg | Asksvin Egg | Misc | consumables | Hard as rock, yet you can sense the presence of something inside. This should be kept warm. |
| BarberKit | $item_barberkit | Barber Kit | Misc | materials | A kit fit for the finest of barbers. |
| BarleyFlour | $item_barleyflour | Barley Flour | Misc | materials | Great for baking bread. |
| BarrelRings | $item_barrelrings | Barrel Hoops | Misc | materials | These metal rings are perfectly round, suitable for holding a barrel together. Just add wood! |
| Bell | $item_bell | Bell | Misc | misc | For whom does the bell toll? |
| BloodGoldKey | $item_bloodgoldkey | Intricate Key | Misc | misc | If there's a key, then surely there must be a lock. |
| ChickenEgg | $item_chicken_egg | Egg | Misc | consumables | Keep it warm to see what comes out.. but what came first, really? |
| CryptKey | $item_cryptkey | Swamp Key | Misc | misc | Partly covered in caked mud, it smells foetid. |
| DragonEgg | $item_dragonegg | Dragon Egg | Misc | misc | Far heavier than it looks, with a faint humming sound from within. |
| DragonTear | $item_dragontear | Dragon Tear | Misc | materials | The last frozen tear of a dragon, pulsating with mysterious energy. |
| DvergrKey | $item_dvergrkey | Sealbreaker | Misc | misc | An object used to break a Dverger seal. |
| GoblinTotem | $item_goblintotem | Fuling Totem | Misc | misc | Channels the ancient power of Yagluth. |
| HatefulBlood | $item_hatefulblood | Malicious Blood | Misc | materials | There's something unsettling about this clotted, frozen mass. |
| HildirKey_forestcrypt | $item_hildirkey1 | Hildir's Brass Key | Misc | misc | It seems to be missing its owner... |
| HildirKey_mountaincave | $item_hildirkey2 | Hildir's Silver Key | Misc | misc | It seems to be missing its owner... |
| HildirKey_plainsfortress | $item_hildirkey3 | Hildir's Bronze Key | Misc | misc | It seems to be missing its owner... |
| OatFlour | $item_oatflour | Oat Flour | Misc | materials | Finely ground oats, with plenty of potential. |
| SaddleAsksvin | $item_saddleasksvin | Asksvin Saddle | Misc | tools | The back of an asksvin is rather lumpy, so you'll need a saddle if you want to ride one. |
| SaddleLox | $item_saddlelox | Lox Saddle | Misc | tools | Use on a lox to be able to ride it. |
| SaddleMoose | $item_saddlemoose | Moose Saddle | Misc | tools | A moose is a noble creature, but with a saddle this fine it might just allow a rider. |
| ScytheHandle | $item_scythehandle | Scythe Handle | Misc | materials | A sturdy base for a tool. |
| VoltureEgg | $item_voltureegg | Volture Egg | Misc | consumables | Warm to the touch, and full of protein. |
| YagluthDrop | $item_yagluththing | Torn Spirit | Misc | misc | The remains of Yagluth, a twisted spirit torn between this world and the next. |
| chest_hildir1 | $item_chest_hildir1 | Hildir's Brass Chest | Misc | HildirWagon | Property of Hildir, please return if found. |
| chest_hildir2 | $item_chest_hildir2 | Hildir's Silver Chest | Misc | HildirWagon | Property of Hildir, please return if found. |
| chest_hildir3 | $item_chest_hildir3 | Hildir's Bronze Chest | Misc | HildirWagon | Property of Hildir, please return if found. |
| Abomination_attack1 | Swing attack |  | OneHanded | Misc |  |
| Abomination_attack2 | Slam attack |  | OneHanded | Misc |  |
| Abomination_attack3 | Stub to the ground |  | OneHanded | Misc |  |
| Asksvin_Bite | lox bite |  | OneHanded | attacks |  |
| Asksvin_Headbutt | Dragon claw left |  | OneHanded | attacks |  |
| Asksvin_Pounce | lox bite |  | OneHanded | attacks |  |
| Asksvin_Turnaround | lox bite |  | OneHanded | attacks |  |
| Axe1h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| Axe2h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| AxeBlackMetal | $item_axe_blackmetal | Black Metal Axe | OneHanded | weapons | A perfectly balanced axe forged from dark metal with an emerald sheen. |
| AxeBronze | $item_axe_bronze | Bronze Axe | OneHanded | weapons | A bright and burnished blade, curved like a smile. |
| AxeFlint | $item_axe_flint | Flint Axe | OneHanded | weapons | Sharper than stone. |
| AxeGold | $item_axe_gold | Nord Axe | OneHanded | weapons | A finely detailed axe, for finely cutting down your enemies. |
| AxeGold_BloodLightning | $item_axe_gold_bloodlightning | Thunderblood Axe | OneHanded | weapons | As this axe cuts through wood and blood alike, a resounding thunderous crack shall be heard through… |
| AxeGold_FrostFire | $item_axe_gold_frostfire | Frostfire Axe | OneHanded | weapons | Burn it all to the ground, or freeze it in an eternal moment of destruction... |
| AxeIron | $item_axe_iron | Iron Axe | OneHanded | weapons | Sharp and strong, a woodcutter's friend. |
| AxeJotunBane | $item_axe_jotunbane | Jotun Bane | OneHanded | weapons | Not even the giants of old could weather the poisonous bite of this weapon. |
| AxeStone | $item_axe_stone | Stone Axe | OneHanded | weapons | A crude axe for tree-felling. |
| AxeWood | $item_axe_wood | Wooden Axe | OneHanded | weapons | If this axe could chop wood, wouldn't that be strange? |
| Barka_Backslam | Barka Backslam |  | OneHanded | attacks |  |
| Barka_HeavySwing | Dragon claw left |  | OneHanded | attacks |  |
| Barka_SlamDrive | Dragon claw left |  | OneHanded | attacks |  |
| Barka_WhipFlurry | Dragon claw left |  | OneHanded | attacks |  |
| Barka_WhipSlam | Dragon claw left |  | OneHanded | attacks |  |
| BogWitchKvastur_attack | jaws |  | OneHanded |  |  |
| BombBile | $item_bilebomb | Bile Bomb | OneHanded | weapons | Handle with care. |
| BombBlob_Frost | $item_bombblob_frost | Blob Bomb: Frost | OneHanded | weapons | If you hold it for too long your hand might just freeze in place. |
| BombBlob_Lava | $item_bombblob_lava | Blob Bomb: Lava | OneHanded | weapons | The vial is hot in your hand, promising fiery destruction. |
| BombBlob_Morkhalla | $item_bombblob_morkhalla | Blob Bomb: Pulp | OneHanded | weapons | A dungeon is where the blob is. |
| BombBlob_Poison | $item_bombblob_poison | Blob Bomb: Poison | OneHanded | weapons | Should this vial break, caution is strongly advised. |
| BombBlob_PoisonElite | $item_bombblob_poisonelite | Blob Bomb: Elite Poison | OneHanded | weapons | Breathing in the fumes is not recommended. |
| BombBlob_Tar | $item_bombblob_tar | Blob Bomb: Tar | OneHanded | weapons | The sticky substance within pulses, as if threatening you. |
| BombDynamite | $item_bomb_dynamite | Ember Charge | OneHanded | weapons | Trapped embers, ready to burst. Caution is advised. |
| BombLava | $item_lavabomb | Basalt Bomb | OneHanded | weapons | With enough heat, it solidifies upon explosion. |
| BombOoze | $item_oozebomb | Ooze Bomb | OneHanded | weapons | The stench is unbearable... |
| BombSmoke | $item_smokebomb | Smoke Bomb | OneHanded | weapons | Everyone knows you can't breathe in the smoke. |
| BonemawSerpent_bite | Serpent bite |  | OneHanded | attacks |  |
| BonemawSerpent_breath | Fallen Valkyrie Poison Breath |  | OneHanded | attacks |  |
| BonemawSerpent_ram | Serpent bite |  | OneHanded | attacks |  |
| BonemawSerpent_spit | bonemaw spit |  | OneHanded | attacks |  |
| BonemawSerpent_taunt | Serpent Taunt |  | OneHanded | attacks |  |
| Club | $item_club | Club | OneHanded | weapons | A crude but useful weapon. |
| Deathsquito_sting | Wraith melee |  | OneHanded | attacks |  |
| DvergerArbalest_shoot | $item_crossbow_arbalest | Arbalest | OneHanded | Attacks | A slow but powerful weapon. |
| DvergerArbalest_shootAshlands | $item_crossbow_arbalest | Arbalest | OneHanded | Attacks | A slow but powerful weapon. |
| DvergerArbalest_shootDeepNorth | $item_crossbow_arbalest | Arbalest | OneHanded | Attacks | A slow but powerful weapon. |
| DvergerMistile | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffBlocker | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffFire_clusterbomb | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffFire_fireball | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffHeal_heal | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffIce_icebolt | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffNova | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| DvergerStaffSupport_buff | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| Dverger_melee | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| Dverger_meleeAshlands | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| Dverger_meleeDeepNorth | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| Eikthyr_antler | StagAttack1 |  | OneHanded | attacks |  |
| Eikthyr_charge | StagAttack2 |  | OneHanded | attacks |  |
| Eikthyr_flegs_OLD | StagAttack1 |  | OneHanded | attacks |  |
| Eikthyr_stomp | slap |  | OneHanded | attacks |  |
| ElakingMole_AttackClaw | jaws |  | OneHanded | attacks |  |
| ElakingMole_AttackClaw2 | jaws |  | OneHanded | attacks |  |
| ElakingMole_AttackSandcloud | StagAttack2 |  | OneHanded | attacks |  |
| Elaking_AttackClaw | jaws |  | OneHanded | attacks |  |
| Elaking_AttackLantern | Torch |  | OneHanded | misc | It brings light and warmth, drives back the darkness. |
| FW_AxeBronze | $item_axe_bronze | Bronze Axe | OneHanded | Equipment | A bright and burnished blade, curved like a smile. |
| FW_KnifeSilver | $item_knife_silver | Silver Knife | OneHanded | Equipment | A savage piece of pain. |
| FW_SwordBlackmetal | $item_sword_blackmetal | Black Metal Sword | OneHanded | Equipment | A thing of death and beauty. It catches the light with a greenish glow. |
| Fader_Bite | Fader Bite |  | OneHanded | attacks |  |
| Fader_Claw_Left | Fader Claw Left |  | OneHanded | attacks |  |
| Fader_Claw_Right | Fader Claw Right |  | OneHanded | attacks |  |
| Fader_Fissure | Fader Fissure |  | OneHanded | attacks |  |
| Fader_Fissure_Intense | Fader Fissure |  | OneHanded | attacks |  |
| Fader_Flamebreath | Fader Firebreath |  | OneHanded | attacks |  |
| Fader_Jump | Fader Jump |  | OneHanded | attacks |  |
| Fader_Jump_Left | Fader Jump |  | OneHanded | attacks |  |
| Fader_Jump_Right | Fader Jump |  | OneHanded | attacks |  |
| Fader_Meteors | spawn |  | OneHanded | attacks |  |
| Fader_Meteors_Intense | spawn |  | OneHanded | attacks |  |
| Fader_Roar | Fader Roar |  | OneHanded | attacks |  |
| Fader_Roar_Intense | Fader Roar |  | OneHanded | attacks |  |
| Fader_Spin | Fader Spin |  | OneHanded | attacks |  |
| Fader_Taunt | Fader Taunt |  | OneHanded | attacks |  |
| Fader_WallOfFire | Fader Wall of Fire |  | OneHanded | attacks |  |
| Fenring_attack_IceNova | Club |  | OneHanded | attacks | A crude but useful weapon. |
| Fenring_attack_claw | claw |  | OneHanded | attacks |  |
| Fenring_attack_fireclaw | claw |  | OneHanded | attacks |  |
| Fenring_attack_fireclaw_double | claw |  | OneHanded | attacks |  |
| Fenring_attack_flames | Fenring cultist flames |  | OneHanded | attacks |  |
| Fenring_attack_frost | Fenring cultist frost |  | OneHanded | attacks |  |
| Fenring_attack_iceclaw | claw |  | OneHanded | attacks |  |
| Fenring_attack_iceclaw_double | claw |  | OneHanded | attacks |  |
| Fenring_attack_jump | claw |  | OneHanded | attacks |  |
| Fenring_taunt | scream |  | OneHanded | attacks |  |
| FrozenKing_ChainFlurry | FrozenKing ChainFlurry |  | OneHanded | attacks |  |
| FrozenKing_ChainRush | FrozenKing ChainRush |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_L | FrozenKing ChainSlam L |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_R | FrozenKing ChainSlam R |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | attacks |  |
| FrozenKing_ChainSweep_L | FrozenKing ChainSweep L |  | OneHanded | attacks |  |
| FrozenKing_ChainSweep_R | FrozenKing ChainSweep R |  | OneHanded | attacks |  |
| FrozenKing_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | attacks |  |
| FrozenKing_DoubleSweep | FrozenKing DoubleSweep |  | OneHanded | attacks |  |
| FrozenKing_P2_Summon_Bonemass | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Eikthyr | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Elder | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Fader | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Moder | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Queen | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Yagluth | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P3_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | attacks |  |
| FrozenKing_P3_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | attacks |  |
| FrozenKing_P3_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | attacks |  |
| FrozenKing_Punch_AOE | FrozenKing Punch AOE |  | OneHanded | attacks |  |
| FrozenKing_SpikeRain | spawn |  | OneHanded | attacks |  |
| FrozenKing_tendrilspawn | spawn |  | OneHanded | attacks |  |
| Ghost_attack | jaws |  | OneHanded | misc |  |
| GoblinBruteBros_Attack | Brute sword |  | OneHanded | attacks |  |
| GoblinBruteBros_RageAttack | Brute sword |  | OneHanded | attacks |  |
| GoblinBrute_Attack | Brute sword |  | OneHanded | attacks |  |
| GoblinBrute_RageAttack | Brute sword |  | OneHanded | attacks |  |
| GoblinBrute_Taunt | Brute taunt |  | OneHanded | attacks |  |
| GoblinClub | Club |  | OneHanded | misc | A crude but useful weapon. |
| GoblinClubDeepNorth | Club |  | OneHanded | misc | A crude but useful weapon. |
| GoblinKing_Beam | dragon breath |  | OneHanded | attacks |  |
| GoblinKing_Meteors | spawn |  | OneHanded | attacks |  |
| GoblinKing_Nova | slap |  | OneHanded | attacks |  |
| GoblinKing_Taunt | scream |  | OneHanded | attacks |  |
| GoblinShaman_attack_fireball | fireballattack |  | OneHanded | attacks |  |
| GoblinShaman_attack_fireball_hildir | fireballattack |  | OneHanded | attacks |  |
| GoblinShaman_attack_poke | Club |  | OneHanded | attacks | A crude but useful weapon. |
| GoblinShaman_attack_protect | heal |  | OneHanded | attacks |  |
| GoblinShaman_attack_protect_hildir | heal |  | OneHanded | attacks |  |
| GoblinSpear | Flint spear |  | OneHanded | misc |  |
| GoblinSpearDeepNorth | Flint spear |  | OneHanded | misc |  |
| GoblinSword | Bronze sword |  | OneHanded | misc | Blood-drinker. A thirsty friend. |
| GoblinSwordDeepNorth | Bronze sword |  | OneHanded | misc | Blood-drinker. A thirsty friend. |
| GoblinTorch | Torch |  | OneHanded | misc | It brings light and warmth, drives back the darkness. |
| GoblinTorchDeepNorth | Torch |  | OneHanded | misc | It brings light and warmth, drives back the darkness. |
| Greydwarf_attack | jaws |  | OneHanded | misc |  |
| Greydwarf_attack_frozen | jaws |  | OneHanded | misc |  |
| Greydwarf_elite_attack | jaws |  | OneHanded | misc |  |
| Greydwarf_shaman_attack | shaman attack |  | OneHanded | misc |  |
| Greydwarf_shaman_attack_frozen | shaman attack |  | OneHanded | misc |  |
| Greydwarf_shaman_heal | heal |  | OneHanded | misc |  |
| Greydwarf_shaman_heal_frozen | heal |  | OneHanded | misc |  |
| Greydwarf_throw | throw stone |  | OneHanded | misc |  |
| Greydwarf_throw_frozen | throw stone |  | OneHanded | misc |  |
| Greyling_attack | jaws |  | OneHanded | misc |  |
| JotunWarriorSword2h | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| JotunWitch_attack_lightningbolt | fireballattack |  | OneHanded | attacks |  |
| JotunWitch_attack_magicblast | StagAttack2 |  | OneHanded | attacks |  |
| KnifeBlackMetal | $item_knife_blackmetal | Black Metal Knife | OneHanded | weapons | A darkling blade. Strong and sharp. |
| KnifeButcher | $item_knife_butcher | Butcher Knife | OneHanded | weapons | A butcher's knife designed specifically for slaughtering tamed animals. |
| KnifeChitin | $item_knife_chitin | Abyssal Razor | OneHanded | weapons | A knife from the deep. |
| KnifeCopper | $item_knife_copper | Copper Knife | OneHanded | weapons | A glittering copper knife. |
| KnifeFlint | $item_knife_flint | Flint Knife | OneHanded | weapons | Sharpened flint. A reliable tool. |
| KnifeGold | $item_knife_gold | Nord Dagger | OneHanded | weapons | A flash of gold is the last thing your foes will ever see. |
| KnifeGold_BloodLightning | $item_knife_gold_bloodlightning | Thunderblood Dagger | OneHanded | weapons | The blade is already bloodied, yet it sparks in want of more. |
| KnifeGold_FrostFire | $item_knife_gold_frostfire | Frostfire Dagger | OneHanded | weapons | A cut from this blade stings like ice, then burns like the flame. |
| KnifeSilver | $item_knife_silver | Silver Knife | OneHanded | weapons | A savage piece of pain. |
| KnifeVoid | $item_knife_void | Voidcaller | OneHanded | weapons | Who shall answer the call of the Void? |
| KnifeWood | $item_knife_wood | Wooden Knife | OneHanded | weapons | Some people would say this knife is better for butter. |
| Leech_BiteAttack | jaws |  | OneHanded | attacks |  |
| MaceBronze | $item_mace_bronze | Bronze Mace | OneHanded | weapons | A headache on a stick. |
| MaceEldner | $item_mace_eldner | Flametal Mace | OneHanded | weapons | Dense yet spiked flametal, perfect for bashing enemy faces in. |
| MaceEldnerBlood | $item_mace_eldner_blood | Bloodgeon | OneHanded | weapons | The blood all but soaks into this mace, and it always yearns for more. |
| MaceEldnerLightning | $item_mace_eldner_lightning | Storm Star | OneHanded | weapons | Particularly effective on cloudy mornings. |
| MaceEldnerNature | $item_mace_eldner_nature | Klossen | OneHanded | weapons | If the force of your blow isn't enough to knock your enemies to the ground, perhaps the primal root… |
| MaceGold | $item_mace_gold | Nord Mace | OneHanded | weapons | Hit hard and fast, and leave your foe no time to recover. |
| MaceGold_BloodLightning | $item_mace_gold_bloodlightning | Thunderblood Mace | OneHanded | weapons | A weapon to rival perhaps even that of the thunder god himself... |
| MaceGold_FrostFire | $item_mace_gold_frostfire | Frostfire Mace | OneHanded | weapons | Are those sparks that fly as this weapon finds its impact, or are they shards of ice? |
| MaceIron | $item_mace_iron | Iron Mace | OneHanded | weapons | A fist-sized lump of iron on a wooden shaft. |
| MaceNeedle | $item_mace_needle | Porcupine | OneHanded | weapons | A deadly weapon, bristling with fiendish spikes. |
| MaceSilver | $item_mace_silver | Frostner | OneHanded | weapons | The dead fear silver. Remind them why. |
| MaceWood | $item_mace_wood | Wooden Mace | OneHanded | weapons | One has to wonder why someone made this instead of a simple club. |
| Mistile_kamikaze | Mistile Kamikaze |  | OneHanded | Attacks |  |
| Morgen_bite | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_bodyslam | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_roll_left | Morgen Roll Left |  | OneHanded | attacks |  |
| Morgen_roll_right | Morgen Roll Right |  | OneHanded | attacks |  |
| Morgen_swipe_1 | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_swipe_2 | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_swipe_3 | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_swipe_4 | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_swipe_5 | Dragon claw left |  | OneHanded | attacks |  |
| Morgen_swipe_6 | Dragon claw left |  | OneHanded | attacks |  |
| Neck_BiteAttack | jaws |  | OneHanded | attacks |  |
| PlayerUnarmed | Unarmed |  | OneHanded | weapons |  |
| SP_AxeBronze | $item_axe_bronze | Bronze Axe | OneHanded | Equipment/ShadowPerson | A bright and burnished blade, curved like a smile. |
| SP_KnifeSilver | $item_knife_silver | Silver Knife | OneHanded | Equipment/ShadowPerson | A savage piece of pain. |
| SP_SwordBlackmetal | $item_sword_blackmetal | Black Metal Sword | OneHanded | Equipment/ShadowPerson | A thing of death and beauty. It catches the light with a greenish glow. |
| SeekerBrute_Taunt | Brute taunt |  | OneHanded | attacks |  |
| SeekerBrute_bite | Dragon claw left |  | OneHanded | attacks |  |
| SeekerBrute_groundslam | slap |  | OneHanded | attacks |  |
| SeekerBrute_ram | Dragon claw left |  | OneHanded | attacks |  |
| SeekerQueen_Bite | slap |  | OneHanded | attacks |  |
| SeekerQueen_Call | Brute taunt |  | OneHanded | attacks |  |
| SeekerQueen_PierceAOE | slap |  | OneHanded | attacks |  |
| SeekerQueen_Rush | slap |  | OneHanded | attacks |  |
| SeekerQueen_Slap | slap |  | OneHanded | attacks |  |
| SeekerQueen_Spit | dragon breath |  | OneHanded | attacks |  |
| SeekerQueen_Teleport | Brute taunt |  | OneHanded | attacks |  |
| Serpent_attack | Serpent bite |  | OneHanded | misc |  |
| Serpent_taunt | Serpent Taunt |  | OneHanded | misc |  |
| Snowball | $item_snowball | Snowball | OneHanded | weapons | Looks like a perfect thing to throw... |
| SnowballBig | Big $item_snowball |  | OneHanded | weapons | Looks like a perfect thing to throw... |
| SpearBronze | $item_spear_bronze | Bronze Spear | OneHanded | weapons | A sturdy spear with a head of burnished bronze. |
| SpearCarapace | $item_spear_carapace | Carapace Spear | OneHanded | weapons | Sharpened to jagged perfection, this spear is sure to be deadly. |
| SpearChitin | $item_spear_chitin | Abyssal Harpoon | OneHanded | weapons | The ocean's wrath. |
| SpearElderbark | $item_spear_ancientbark | Ancient Bark Spear | OneHanded | weapons | Despite its gnarled look, this spear is strong and perfectly balanced. |
| SpearFlint | $item_spear_flint | Flint Spear | OneHanded | weapons | If your eye marks a thing for death, let your arm send the messenger. |
| SpearGold | $item_spear_gold | Nord Spear | OneHanded | weapons | A golden opportunity to strike. |
| SpearGold_BloodLightning | $item_spear_gold_bloodlightning | Thunderblood Spear | OneHanded | weapons | May it strike like lightning, quick and fierce. |
| SpearGold_FrostFire | $item_spear_gold_frostfire | Frostfire Spear | OneHanded | weapons | Dipped in frozen flames, this spear spells certain doom. |
| SpearSplitner | $item_spear_splitner | Splitnir | OneHanded | weapons | Split your enemies' hearts in two. |
| SpearSplitner_Blood | $item_spear_splitner_blood | Splitnir the Bleeding | OneHanded | weapons | A small sacrifice must be made for every battle... |
| SpearSplitner_Lightning | $item_spear_splitner_lightning | Splitnir the Storming | OneHanded | weapons | Let the crack of thunder split the air. |
| SpearSplitner_Nature | $item_spear_splitner_nature | Splitnir the Primal | OneHanded | weapons | Nature's forces burst through the ground wherever this spear strikes. |
| SpearWolfFang | $item_spear_wolffang | Fang Spear | OneHanded | weapons | Even in death, the wolf's tooth aches for flesh. |
| SpearWood | $item_spear_wood | Wooden Spear | OneHanded | weapons | This is just as sharp as a real spear, if a real spear was made out of wood. |
| SpiritWolf_Attack1 | WolfAttack1 |  | OneHanded | misc |  |
| SpiritWolf_Attack2 | WolfAttack2 |  | OneHanded | misc |  |
| SpiritWolf_Attack3 | WolfAttack3 |  | OneHanded | misc |  |
| Sword2h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| SwordBlackmetal | $item_sword_blackmetal | Black Metal Sword | OneHanded | weapons | A thing of death and beauty. It catches the light with a greenish glow. |
| SwordBronze | $item_sword_bronze | Bronze Sword | OneHanded | weapons | Blood-drinker. A thirsty friend. |
| SwordCheat | Cheat sword |  | OneHanded | weapons |  |
| SwordDyrnwyn | $item_sword_dyrnwyn | Dyrnwyn | OneHanded | weapons | The sword that was broken is whole once more. Its flames burn hot, fuelled by the memories of ancie… |
| SwordGold | $item_sword_gold | Nord Sword | OneHanded | weapons | Even in the faintest sunlight, this weapon glimmers. |
| SwordGold_BloodLightning | $item_sword_gold_bloodlightning | Thunderblood Sword | OneHanded | weapons | As the blood runs along the blade, it awakens the storm within. |
| SwordGold_FrostFire | $item_sword_gold_frostfire | Frostfire Sword | OneHanded | weapons | Flames dance along this blade, but are they hot or cold? |
| SwordIron | $item_sword_iron | Iron Sword | OneHanded | weapons | The straight line between life and death runs along the edge of this blade. |
| SwordIronFire | $item_sword_fire | Dyrnwyn | OneHanded | weapons | Unsheathed, it sizzles and spits with deathless fire, an impossible blade. |
| SwordMistwalker | $item_sword_mistwalker | Mistwalker | OneHanded | weapons | The faint glow seems to slice through the mist. |
| SwordNiedhogg | $item_sword_niedhogg | Nidhögg | OneHanded | weapons | Named after the evil dragon that dwells by the roots of the world tree, this sword heralds doom for… |
| SwordNiedhoggBlood | $item_sword_niedhogg_blood | Nidhögg the Bleeding | OneHanded | weapons | If you bleed, your foes are sure to do so as well. |
| SwordNiedhoggLightning | $item_sword_niedhogg_lightning | Nidhögg the Thundering | OneHanded | weapons | The power of lightning dances along the blade of this sword, a promise of the pain to come. |
| SwordNiedhoggNature | $item_sword_niedhogg_nature | Nidhögg the Primal | OneHanded | weapons | Tangle your foes in roots, like the namesake of this blade. |
| SwordSilver | $item_sword_silver | Silver Sword | OneHanded | weapons | Purest of metals, nothing unclean can abide its touch. |
| SwordWood | $item_sword_wood | Wooden Sword | OneHanded | weapons | Mostly harmless, unless you're very persistent. |
| Tankard | $item_tankard | Tankard | OneHanded | misc | Skål! |
| TankardAnniversary | $item_tankard_anniversary | Horn of Celebration | OneHanded | misc | One year since we arrived... Skål! |
| TankardOdin | $item_tankard_odin | Mead Horn of Oden | OneHanded | misc | Oden's finest warriors deserve the finest drinks. |
| Tankard_dvergr | $item_dvergrtankard | Dvergr Tankard | OneHanded | misc | It can hold a lot of mead! |
| TrainingDummy_attack | dummy attack |  | OneHanded | Attacks |  |
| TrainingDummy_attack2 | dummy attack |  | OneHanded | Attacks |  |
| TrainingDummy_attack3 | dummy attack |  | OneHanded | Attacks |  |
| TrainingDummy_throw | dummy throw stone |  | OneHanded | Attacks |  |
| Ulv_attack1_bite | Bite Attack |  | OneHanded | Attacks |  |
| Ulv_attack2_slash | Slash Attack |  | OneHanded | Attacks |  |
| Wolf_Attack1 | WolfAttack1 |  | OneHanded | misc |  |
| Wolf_Attack2 | WolfAttack2 |  | OneHanded | misc |  |
| Wolf_Attack3 | WolfAttack3 |  | OneHanded | misc |  |
| aspect_Eikthyr_antler | StagAttack1 |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Eikthyr_charge | StagAttack2 |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Eikthyr_stomp | slap |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Fader_Bite | Fader Bite |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Claw_Left | Fader Claw Left |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Claw_Right | Fader Claw Right |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Fissure | Fader Fissure |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Flamebreath | Fader Firebreath |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Spin | Fader Spin |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_WallOfFire | Fader Wall of Fire |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_GoblinKing_Beam | dragon breath |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_GoblinKing_Nova | slap |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_GoblinKing_Taunt | scream |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_SeekerQueen_Bite | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_PierceAOE | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Rush | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Slap | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Spit | dragon breath |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_bonemass_attack_aoe | heal |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_bonemass_attack_punch | slap |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_bonemass_attack_throw | slime throw |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_dragon_bite | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_claw_left | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_claw_right | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_coldbreath | dragon breath |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_spit_shotgun | cold ball |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_taunt | scream |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_gd_king_rootspawn | spawn |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_scream | scream |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_shoot | shaman attack |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_stomp | jaws |  | OneHanded | BossAspects/Attacks/Elder |  |
| babyseeker_attack | Dragon claw left |  | OneHanded | attacks |  |
| bat_melee | Bat melee |  | OneHanded | Attacks |  |
| bjorn_bite | bjorn bite |  | OneHanded | attacks |  |
| bjorn_claws | bjorn bite |  | OneHanded | attacks |  |
| bjorn_slam | slap |  | OneHanded | attacks |  |
| bjorn_swipe_combo | bjorn bite |  | OneHanded | attacks |  |
| bjorn_swipe_l | bjorn bite |  | OneHanded | attacks |  |
| bjorn_swipe_r | bjorn bite |  | OneHanded | attacks |  |
| blobLava_attack_aoe |  |  | OneHanded | misc |  |
| blob_attack_aoe | fart |  | OneHanded | misc |  |
| blob_frost_attack_aoe | fart |  | OneHanded | misc |  |
| blobelite_attack_aoe | fart |  | OneHanded | misc |  |
| blobmork_attack_aoe | fart |  | OneHanded | misc |  |
| blobtar_attack | fireballattack |  | OneHanded | misc |  |
| boar_base_attack | boar attack1 |  | OneHanded | attacks |  |
| bonemass_attack_aoe | heal |  | OneHanded | misc |  |
| bonemass_attack_punch | slap |  | OneHanded | misc |  |
| bonemass_attack_spawn | heal |  | OneHanded | misc |  |
| bonemass_attack_throw | slime throw |  | OneHanded | misc |  |
| dragon_bite | Dragon claw left |  | OneHanded | attacks |  |
| dragon_claw_left | Dragon claw left |  | OneHanded | attacks |  |
| dragon_claw_right | Dragon claw left |  | OneHanded | attacks |  |
| dragon_coldbreath | dragon breath |  | OneHanded | attacks |  |
| dragon_coldbreath_OLD | dragon breath |  | OneHanded | attacks |  |
| dragon_spit_shotgun | cold ball |  | OneHanded | attacks |  |
| dragon_taunt | scream |  | OneHanded | attacks |  |
| draugr_axe | Dragur axe |  | OneHanded | weapons |  |
| draugr_sword | Dragur axe |  | OneHanded | weapons |  |
| fallenvalkyrie_claws | Fallen Valkyrie Claws |  | OneHanded | attacks |  |
| fallenvalkyrie_poisonbreath | Fallen Valkyrie Poison Breath |  | OneHanded | attacks |  |
| fallenvalkyrie_screech | Fallen Valkyrie Claws |  | OneHanded | attacks |  |
| fallenvalkyrie_spin | Fallen Valkyrie Aoe Spin |  | OneHanded | attacks |  |
| fallenvalkyrie_spit | cold ball |  | OneHanded | attacks |  |
| fallenvalkyrie_swoopattack | Fallen Valkyrie swooping |  | OneHanded | attacks |  |
| fallenvalkyrie_taunt | Fallen Valkyrie Claws |  | OneHanded | attacks |  |
| fallenvalkyrie_wingspin | Fallen Valkyrie Wingspin |  | OneHanded | attacks |  |
| frysling_snowball_attack | fireballattack |  | OneHanded | attacks |  |
| gd_king_punch | jaws |  | OneHanded | misc |  |
| gd_king_rootspawn | spawn |  | OneHanded | misc |  |
| gd_king_scream | scream |  | OneHanded | misc |  |
| gd_king_shoot | shaman attack |  | OneHanded | misc |  |
| gd_king_stomp | jaws |  | OneHanded | misc |  |
| gjall_attack_egg | egg drop |  | OneHanded | attacks |  |
| gjall_attack_shake | gjall shake |  | OneHanded | attacks |  |
| gjall_attack_spit | gjall spit |  | OneHanded | attacks |  |
| gjall_attack_taunt | gjall taunt |  | OneHanded | attacks |  |
| hatchling_spit_cold | cold ball |  | OneHanded | attacks |  |
| hive_attack_aoe | heal |  | OneHanded | Attacks |  |
| hive_attack_punch | slap |  | OneHanded | Attacks |  |
| hive_attack_ranged | dragon breath |  | OneHanded | Attacks |  |
| hive_attack_throw | slime throw |  | OneHanded | Attacks |  |
| imp_fireball_attack | fireballattack |  | OneHanded | misc |  |
| lox_bite | lox bite |  | OneHanded | attacks |  |
| lox_stomp | slap |  | OneHanded | attacks |  |
| moose_hooves | moose horns |  | OneHanded | attacks |  |
| moose_horns | moose horns |  | OneHanded | attacks |  |
| moose_horns_sweep | moose horns |  | OneHanded | attacks |  |
| seeker_claw_left | Dragon claw left |  | OneHanded | attacks |  |
| seeker_claw_right | Dragon claw left |  | OneHanded | attacks |  |
| seeker_groundslam | Dragon claw left |  | OneHanded | attacks |  |
| seeker_groundslam_flying | Dragon claw left |  | OneHanded | attacks |  |
| seeker_land | land |  | OneHanded | attacks |  |
| seeker_pincers | Dragon claw left |  | OneHanded | attacks |  |
| seeker_takeoff | takeoff |  | OneHanded | attacks |  |
| skeleton_hildir_firenova | Fire Skeleton Sword |  | OneHanded | weapons |  |
| skeleton_mace | Dragur axe |  | OneHanded | weapons |  |
| skeleton_mace_DeepNorth | Dragur axe |  | OneHanded | weapons |  |
| skeleton_sword | Dragur axe |  | OneHanded | weapons |  |
| skeleton_sword2 | Dragur axe |  | OneHanded | weapons |  |
| skeleton_sword_hildir | Fire Skeleton Sword |  | OneHanded | weapons |  |
| skeleton_sword_meadows | Dragur axe |  | OneHanded | weapons |  |
| skeleton_sword_mountains | Dragur axe |  | OneHanded | weapons |  |
| skeleton_sword_swamps | Dragur axe |  | OneHanded | weapons |  |
| spiritbjorn_bite | bjorn bite |  | OneHanded | attacks |  |
| spiritbjorn_claws | bjorn bite |  | OneHanded | attacks |  |
| spiritbjorn_slam | slap |  | OneHanded | attacks |  |
| spiritbjorn_swipe_combo | bjorn bite |  | OneHanded | attacks |  |
| spiritbjorn_swipe_l | bjorn bite |  | OneHanded | attacks |  |
| spiritbjorn_swipe_r | bjorn bite |  | OneHanded | attacks |  |
| spiritboar_base_attack | boar attack1 |  | OneHanded | attacks |  |
| spiritmoose_hooves | moose horns |  | OneHanded | attacks |  |
| spiritmoose_horns | moose horns |  | OneHanded | attacks |  |
| spiritmoose_horns_sweep | moose horns |  | OneHanded | attacks |  |
| staff_greenroots_tentaroot_attack | Dragur axe |  | OneHanded | weapons/_res/staffs |  |
| stonegolem_attack1_spike | Spike attack |  | OneHanded | Misc |  |
| stonegolem_attack2_left_groundslam | One hand ground slam |  | OneHanded | Misc |  |
| stonegolem_attack3_spikesweep | Spike sweep |  | OneHanded | Misc |  |
| stonegolem_attack_doublesmash | slap |  | OneHanded | Misc |  |
| stonegolem_attack_sonicboom_NOTUSED | slap |  | OneHanded | Misc |  |
| tendril_attack | Dragur axe |  | OneHanded | attacks |  |
| tentaroot_attack | Dragur axe |  | OneHanded | misc |  |
| tick_attack | boar attack1 |  | OneHanded | attacks |  |
| tick_attack_attach | boar attack1 |  | OneHanded | attacks |  |
| troll_groundslam | slap |  | OneHanded | misc |  |
| troll_log_swing_h | LOG |  | OneHanded | misc |  |
| troll_log_swing_v | LOG |  | OneHanded | misc |  |
| troll_punch | slap |  | OneHanded | misc |  |
| troll_summoned_groundslam | slap |  | OneHanded | misc |  |
| troll_summoned_log_swing_h | LOG |  | OneHanded | misc |  |
| troll_summoned_log_swing_v | LOG |  | OneHanded | misc |  |
| troll_summoned_punch | slap |  | OneHanded | misc |  |
| troll_summoned_throw | fireballattack |  | OneHanded | misc |  |
| troll_throw | fireballattack |  | OneHanded | misc |  |
| trollsnow_groundslam | slap |  | OneHanded | misc |  |
| trollsnow_groundslam_r | slap |  | OneHanded | misc |  |
| trollsnow_punch | slap |  | OneHanded | misc |  |
| trollsnow_punch_r | slap |  | OneHanded | misc |  |
| trollsnow_throw | fireballattack |  | OneHanded | misc |  |
| unbjorn_bite | bjorn bite |  | OneHanded | attacks |  |
| unbjorn_claws | bjorn bite |  | OneHanded | attacks |  |
| unbjorn_slam | slap |  | OneHanded | attacks |  |
| unbjorn_swipe_combo | bjorn bite |  | OneHanded | attacks |  |
| unbjorn_swipe_l | bjorn bite |  | OneHanded | attacks |  |
| unbjorn_swipe_r | bjorn bite |  | OneHanded | attacks |  |
| volture_talons | Volture Talons |  | OneHanded | attacks |  |
| wraith_melee | Wraith melee |  | OneHanded | misc |  |
| writhan_bite | writhan bite |  | OneHanded | attacks |  |
| writhan_explode_aoe |  |  | OneHanded | attacks |  |
| Axe1h_JotunWarrior 1 | Club |  | Shield | model/weapons | A crude but useful weapon. |
| FW_ShieldBlackmetalTower | $item_shield_blackmetal_tower | Black Metal Tower Shield | Shield | Equipment | A tower shield of gleaming dark metal. |
| SP_ShieldBlackmetalTower | $item_shield_blackmetal_tower | Black Metal Tower Shield | Shield | Equipment/ShadowPerson | A tower shield of gleaming dark metal. |
| ShieldBanded | $item_shield_banded | Banded Shield | Shield | shields | Banded with hoops of iron, a true warrior's companion. |
| ShieldBlackmetal | $item_shield_blackmetal | Black Metal Shield | Shield | shields | Fashioned from the strongest metal, able to turn even the deadliest blades. |
| ShieldBlackmetalTower | $item_shield_blackmetal_tower | Black Metal Tower Shield | Shield | shields | A tower shield of gleaming dark metal. |
| ShieldBoneTower | $item_shield_bonetower | Bone Tower Shield | Shield | shields | The bones of dead warriors make for a good protection. |
| ShieldBronzeBuckler | $item_shield_bronzebuckler | Bronze Buckler | Shield | shields | A shield of burnished bronze, good to turn a blade or two. |
| ShieldCarapace | $item_shield_carapace | Carapace Shield | Shield | shields | The almost unbreakable carapace of your enemies makes an excellent shield. |
| ShieldCarapaceBuckler | $item_shield_carapacebuckler | Carapace Buckler | Shield | shields | The skull of a seeker is solid but not heavy, which makes it perfect for a small and agile shield. |
| ShieldFlametal | $item_shield_flametal | Flametal Shield | Shield | shields | The shield is and always will be a viking's most important weapon. |
| ShieldFlametalTower | $item_shield_flametal_tower | Flametal Tower Shield | Shield | shields | The best defence is a great defence. |
| ShieldGold | $item_shield_gold | Nord Shield | Shield | shields | Stay safe, and do so in style. |
| ShieldGoldBuckler | $item_shield_goldbuckler | Nord Buckler | Shield | shields | An effective method of protection, with sufficient skill. |
| ShieldGoldTower | $item_shield_gold_tower | Nord Greatshield | Shield | shields | Your foes can't hit you if they can't see you! |
| ShieldIronBuckler | $item_shield_ironbuckler | Iron Buckler | Shield | shields | Its lightness and curved center makes it excellent for deflecting attacks. |
| ShieldIronSquare | $item_shield_iron_square | Iron Shield | Shield | shields | An iron sword-breaker, tile of the battle-wall. |
| ShieldIronTower | $item_shield_iron_tower | Iron Tower Shield | Shield | shields | A tall shield of strong iron. |
| ShieldKnight | $item_shield_knight | Knight shield UNUSED | Shield | shields | A wooden shield reinforced with iron. UNUSED |
| ShieldRoots | $item_shield_roots | Shield of Roots | Shield | shields | Malleable roots have been twisted into a surprisingly sturdy shield. |
| ShieldSerpentscale | $item_shield_serpentscale | Serpent Scale Shield | Shield | shields | A sturdy shield of overlapping scales. |
| ShieldSilver | $item_shield_silver | Silver Shield | Shield | shields | A shield of radiant silver. |
| ShieldWood | $item_shield_wood | Wood Shield | Shield | shields | A simple wooden shield. |
| ShieldWoodTower | $item_shield_woodtower | Wood Tower Shield | Shield | shields | A rough but heavy wooden shield. |
| CapeAsh | $item_cape_ash | Ashen Cape | Shoulder | armor | Thin metal threads are woven into this cape to create an intricate pattern, like a destiny woven by… |
| CapeAsksvin | $item_cape_asksvin | Asksvin Cloak | Shoulder | armor | This thick cape catches the wind, not unlike the sail of a ship. |
| CapeDeepNorth | $item_cape_deepnorth | Moose Hide Cape | Shoulder | armor | A warm cape with fine details of spun gold. |
| CapeDeepNorthMage | $item_cape_deepnorth_mage | Cape of the Caller | Shoulder | armor | A strange magic is woven into this cape, making it both light and warm. |
| CapeDeerHide | $item_cape_deerhide | Deer Hide Cape | Shoulder | armor | Rustic chic. |
| CapeFeather | $item_cape_feather | Feather Cape | Shoulder | armor | Donning this cape makes you feel lighter, almost as if you could fly! |
| CapeLinen | $item_cape_linen | Linen Cape | Shoulder | armor | A simple traveler's cape. |
| CapeLox | $item_cape_lox | Lox Cape | Shoulder | armor | A pelt from one of the great beasts, thick and warm. |
| CapeOdin | $item_cape_odin | Cape of Oden | Shoulder | armor | Oden's finest warriors deserve the finest cloth. |
| CapeTest | CAPE TEST |  | Shoulder | armor |  |
| CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | armor | Trollskin is tough and supple. |
| CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | armor | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| FW_CapeLinen | $item_cape_linen | Linen Cape | Shoulder | Equipment | A simple traveler's cape. |
| FW_CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | Equipment | Trollskin is tough and supple. |
| FW_CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | Equipment | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| GoblinBrute_ShoulderGuard | Iron plate armor |  | Shoulder | misc | An iron scale mail, this will turn all but the strongest of blows. |
| SP_CapeLinen | $item_cape_linen | Linen Cape | Shoulder | Equipment/ShadowPerson | A simple traveler's cape. |
| SP_CapeTrollHide | $item_cape_trollhide | Troll Hide Cape | Shoulder | Equipment/ShadowPerson | Trollskin is tough and supple. |
| SP_CapeWolf | $item_cape_wolf | Wolf Fur Cape | Shoulder | Equipment/ShadowPerson | Wolves are natural survivors. This one was just unlucky. Now its pelt will warm you in the snow. |
| Cultivator | $item_cultivator | Cultivator | Tool | tools | A farming tool for tilling soil. |
| Feaster | $item_feaster | Serving Tray | Tool | tools | Set the table with whatever food and drink you fancy, and impress your guests with a delicious feas… |
| Hammer | $item_hammer | Hammer | Tool | tools | With this to your hand, you can raise high halls and mighty fortifications. |
| Hoe | $item_hoe | Hoe | Tool | tools | A farmer's tool for working the earth. |
| Lantern | $item_lantern | Dvergr Lantern | Torch | weapons | A simple torch would just be so old fashioned. |
| Lantern_DN | $item_lanternDN | Salvaged Lantern | Torch | weapons | An ancient relic, dropped and forgotten by someone long gone. |
| Lantern_hooded | $piece_hoodedlantern | Hooded Lantern | Torch | weapons | A simple torch would just be so old fashioned. |
| Sparkler | $item_sparkler | Sparkler | Torch | weapons | It's a stick that sparkles. Pretty! |
| Torch | $item_torch | Torch | Torch | weapons | It brings light and warmth, drives back the darkness. |
| TorchMist | $item_torchmist |  | Torch | weapons | It brings light and warmth, drives back the darkness. |
| TrinketBlackDamageHealth | $item_trinketblackdamagedealth | Bracelets of the Brave | Trinket | Trinkets | Your mind hardens, as do your blows. |
| TrinketBlackStamina | $item_trinketblackdtamina | Evasion Mantle | Trinket | Trinkets | Dance with death as you dodge your enemies' strikes. |
| TrinketBloodGoldHealth | $item_trinketbloodgoldhealth | Neckstabber | Trinket | Trinkets | Claw and bone and gold – grant them blood and they shall grant you a boon. |
| TrinketBloodGoldStamina | $item_trinketbloodgoldstamina | Witch Crown | Trinket | Trinkets | They say that the soul of a witch can grant strange powers to mortals... |
| TrinketBronzeHealth | $item_trinketbronzehealth | Heart of the Forest | Trinket | Trinkets | It pulsates with fragments of ancient life. |
| TrinketBronzeStamina | $item_trinketbronzestamina | Bronze Pendant | Trinket | Trinkets | A beautiful pendant, harbouring the endurance of a bear. |
| TrinketCarapaceEitr | $item_trinketcarapaceeitr | Pulsating Earrings | Trinket | Trinkets | If you listen carefully, you can hear the faint echoes of lost souls... |
| TrinketChitinSwim | $item_trinketchitinswim | Fins of Destiny | Trinket | Trinkets | Empty your mind as you become shapeless and one with the water. |
| TrinketFlametalEitr | $item_trinketflametaleitr | Jörmundling | Trinket | Trinkets | Tormented screams resonate from within. |
| TrinketFlametalStaminaHealth | $item_trinketflametalstaminahealth | Brimstone | Trinket | Trinkets | It's warm, as if filled with a lifesblood of its own. |
| TrinketIronHealth | $item_trinketironhealth | Iron Brooch | Trinket | Trinkets | A delicate yet defensive accessory. |
| TrinketIronStamina | $item_trinketironstamina | Nimble Anklet | Trinket | Trinkets | Puts a spring in your step! |
| TrinketScaleStaminaDamage | $item_trinketscalestaminadamage | Resounding Shackle | Trinket | Trinkets | A razor-sharp ankle chain. Can it truly be comfortable? |
| TrinketSilverDamage | $item_trinketsilverdamage | Wolf Sight | Trinket | Trinkets | Assume the sharp and furious mind of a wolf. |
| TrinketSilverResist | $item_trinketsilverresist | Crystal Heart | Trinket | Trinkets | A shard of frozen sorrow. Touching it makes you feel almost numb. |
| TrophyAbomination | $item_trophy_abomination | Abomination Trophy | Trophy | trophies | A tangled mess of roots and bark. |
| TrophyAsksvin | $item_trophy_asksvin | Asksvin Trophy | Trophy | trophies | Don't let yourself be fooled by the friendly smile, for it could easily bite your arm off. |
| TrophyBarka | $item_trophy_barka | Barka Trophy | Trophy | trophies | The frozen head of a once-living tree. |
| TrophyBjorn | $item_trophy_bjorn | Bear Trophy | Trophy | trophies | That stare is still frightening... |
| TrophyBjornUndead | $item_trophy_bjorn_undead | Vile Trophy | Trophy | trophies | These eyes are finally vacant for good. |
| TrophyBlob | $item_trophy_blob | Blob Trophy | Trophy | trophies | A smelly lump of sticky matter. |
| TrophyBlob_Frost | $item_trophy_blob_frost | Frost Blob Trophy | Trophy | trophies | It's like a shard of ice, only...slimy? |
| TrophyBlob_Lava | $item_trophy_blob_lava | Lava Blob Trophy | Trophy | trophies | It's probably dead, right? |
| TrophyBlob_Morkhalla | $item_trophy_blob_morkhalla | Pulp Trophy | Trophy | trophies | Remains of unfortunate adventurers, digested and jellified over time. |
| TrophyBoar | $item_trophy_boar | Boar Trophy | Trophy | trophies | This boar head would make for a nice decoration in any house. |
| TrophyBonemass | $item_trophy_bonemass | Bonemass Trophy | Trophy | trophies | Bones and viscous goo, held together by some unseen force. / / Offer it to the Sacrificial Stones. |
| TrophyBonemawSerpent | $item_trophy_bonemaw | Bonemaw Trophy | Trophy | trophies | A skull made up of dense bone, as dangerous as it is protective. |
| TrophyCharredArcher | $item_trophy_charredarcher | Marksman Trophy | Trophy | trophies | These legs could hold infinite power. |
| TrophyCharredMage | $item_trophy_charredmage | Warlock Trophy | Trophy | trophies | A warm glow seems to almost emanate from within. Handle with care. |
| TrophyCharredMelee | $item_trophy_charredmelee | Warrior Trophy | Trophy | trophies | Fractures line this skull, as if it has taken many hits over the years. |
| TrophyCultist | $item_trophy_cultist | Cultist Trophy | Trophy | trophies | My, what big teeth it has... |
| TrophyCultist_Hildir | $item_trophy_cultist_hildir | Geirrhafa Trophy | Trophy | trophies | He's giving you an icy stare. |
| TrophyDeathsquito | $item_trophy_deathsquito | Deathsquito Trophy | Trophy | trophies | You don't like touching this thing even when it's dead. |
| TrophyDeer | $item_trophy_deer | Deer Trophy | Trophy | trophies | A fine specimen, but you'll need to kill more than deer to enter Valhalla. |
| TrophyDeerWhite | $item_trophy_deer_white |  | Trophy | trophies | A fine specimen, but you'll need to kill more than deer to enter Valhalla. |
| TrophyDragonQueen | $item_trophy_dragonqueen | Moder Trophy | Trophy | trophies | The head of a dragon, majestic even in the rigor of death. / / Offer it to the Sacrificial Stones. |
| TrophyDraugr | $item_trophy_draugr | Draugr Trophy | Trophy | trophies | Bind up the mouth if it starts to whisper in the night... |
| TrophyDraugrElite | $item_trophy_draugrelite | Draugr Elite Trophy | Trophy | trophies | The dead stare of the glowing red eyes sends a shiver through your bones. |
| TrophyDraugrFem | $item_trophy_draugr | Draugr Trophy | Trophy | trophies | Bind up the mouth if it starts to whisper in the night... |
| TrophyDvergr | $item_trophy_dvergr | Dvergr Trophy | Trophy | trophies | It's frankly a little troubling that you would consider hanging these on your wall... |
| TrophyEikthyr | $item_trophy_eikthyr | Eikthyr Trophy | Trophy | trophies | This severed head oozes power. / / Offer it to the Sacrificial Stones. |
| TrophyElaking | $item_trophy_elaking | Elaking Trophy | Trophy | trophies | Mean little eyes stare back at you. |
| TrophyFader | $item_trophy_fader | Fader Trophy | Trophy | trophies | The green dragon, corrupted beyond redemption. / / Offer him to the sacrificial stones. |
| TrophyFallenValkyrie | $item_trophy_fallenvalkyrie | Fallen Valkyrie Trophy | Trophy | trophies | Though she is dead, she yearns for the blood to flow. |
| TrophyFenring | $item_trophy_fenring | Fenring Trophy | Trophy | trophies | A strange, elongated paw, its claws razor sharp. |
| TrophyForestTroll | $item_trophy_troll | Troll Trophy | Trophy | trophies | The leathery skin bears the faded tracery of ancient symbols. |
| TrophyFrostTroll | $item_trophy_troll | Troll Trophy | Trophy | trophies | The leathery skin bears the faded tracery of ancient symbols. |
| TrophyGhost | $item_trophy_ghost | Ghost Trophy | Trophy | trophies | Does it still whisper about unfinished business? |
| TrophyGjall | $item_trophy_gjall | Gjall Trophy | Trophy | trophies | Hopefully it won't float away. |
| TrophyGoblin | $item_trophy_goblin | Fuling Trophy | Trophy | trophies | Loose folds of greenish skin gathered in around a pair of dark and hateful eyes. |
| TrophyGoblinBrute | $item_trophy_goblinbrute | Fuling Berserker Trophy | Trophy | trophies | The huge grizzled head is as heavy as a boulder. |
| TrophyGoblinBruteBrosBrute | $item_trophy_brutebro | Thungr Trophy | Trophy | trophies | Not so tough now. |
| TrophyGoblinBruteBrosShaman | $item_trophy_shamanbro | Zil Trophy | Trophy | trophies | In the choice of 'ride or die', he picked the latter. |
| TrophyGoblinKing | $item_trophy_goblinking | Yagluth Trophy | Trophy | trophies | The crownless head of a dead king. / / Offer it to the Sacrificial Stones. |
| TrophyGoblinShaman | $item_trophy_goblinshaman | Fuling Shaman Trophy | Trophy | trophies | It shall cast no more spells against you. |
| TrophyGreydwarf | $item_trophy_greydwarf | Greydwarf Trophy | Trophy | trophies | The mossy, severed head of a Greydwarf. |
| TrophyGreydwarfBrute | $item_trophy_greydwarfbrute | Greydwarf Brute Trophy | Trophy | trophies | It took seven blows to hack this gnarled head from its body. |
| TrophyGreydwarfShaman | $item_trophy_greydwarfshaman | Greydwarf Shaman Trophy | Trophy | trophies | It may try to come back so be sure to prune any new shoots... |
| TrophyGrowth | $item_trophy_growth | Growth Trophy | Trophy | trophies | A black and sticky mess. |
| TrophyHare | $item_trophy_hare | Hare Trophy | Trophy | trophies | These are said to bring luck. But not for their original owner. |
| TrophyHatchling | $item_trophy_hatchling | Drake Trophy | Trophy | trophies | Still cold to the touch. |
| TrophyJotunWarrior | $item_trophy_jotunwarrior | Krigen Trophy | Trophy | trophies | Once a mighty warrior, now nought but a husk remains. |
| TrophyJotunWitch | $item_trophy_jotunwitch | Hexen Trophy | Trophy | trophies | Before her death, her eyes sparked with magic. Now they're empty and void. |
| TrophyKvastur | $enemy_kvastur | Kvastur | Trophy |  | A witch's best friend. |
| TrophyLeech | $item_trophy_leech | Leech Trophy | Trophy | trophies | Although slimy, the skin is beautifully patterned in red and black. |
| TrophyLox | $item_trophy_lox | Lox Trophy | Trophy | trophies | A giant beast's head, thatched with thick fur. |
| TrophyMole | $item_trophy_mole | Eyeless One Trophy | Trophy | trophies | Getting slashed by these claws would be very unpleasant. |
| TrophyMoose | $item_trophy_moose | Moose Trophy | Trophy | trophies | The mighty ruler of the northern forests. |
| TrophyMorgen | $item_trophy_morgen | Morgen Trophy | Trophy | trophies | The waking nightmare has met its end. |
| TrophyNeck | $item_trophy_neck | Neck Trophy | Trophy | trophies | The beady eyes and razor sharp teeth belie the ostensibly calm nature of this small lizard. |
| TrophySGolem | $item_trophy_sgolem | Stone Golem Trophy | Trophy | trophies | This crystalline rock formation would make for an impressive floor decoration. |
| TrophySeal | $item_trophy_seal | Seal Trophy | Trophy | trophies | This animal never did any harm, yet it met an untimely end. |
| TrophySeeker | $item_trophy_seeker | Seeker Trophy | Trophy | trophies | Less delicate than they look. The leather of the wings catches the firelight as if remembering flig… |
| TrophySeekerBrute | $item_trophy_seeker_brute | Seeker Soldier Trophy | Trophy | trophies | The head of a fallen champion. |
| TrophySeekerQueen | $item_trophy_seekerqueen | The Queen Trophy | Trophy | trophies | She has seen enough. / / Offer it to the Sacrificial Stones. |
| TrophySerpent | $item_trophy_serpent | Serpent Trophy | Trophy | trophies | The scales have dulled but the eyes are still bright. |
| TrophySkeleton | $item_trophy_skeleton | Skeleton Trophy | Trophy | trophies | The expressionless grin of this skull reminds you of the inevitability of death. |
| TrophySkeletonHildir | $item_trophy_skeleton_hildir | Brenna Trophy | Trophy | trophies | Still burning, somehow. |
| TrophySkeletonPoison | $item_trophy_skeletonpoison | Rancid Remains Trophy | Trophy | trophies | A rank and rotten skull. You're not sure why you kept it... |
| TrophySurtling | $item_trophy_surtling | Surtling Trophy | Trophy | trophies | Wreathed in pale flame, it still smoulders like an ember. |
| TrophyTheElder | $item_trophy_elder | The Elder Trophy | Trophy | trophies | This severed head oozes power. / / Offer it to the Sacrificial Stones. |
| TrophyTick | $item_trophy_tick | Tick Trophy | Trophy | trophies | It's a conversation piece... |
| TrophyUlv | $item_trophy_ulv | Ulv Trophy | Trophy | trophies | A rugged tail from a not so good boy. |
| TrophyVolture | $item_trophy_volture | Volture Trophy | Trophy | trophies | It's like a vulture, but it thrives in volcanic climates. |
| TrophyWolf | $item_trophy_wolf | Wolf Trophy | Trophy | trophies | Frozen in death, the hair matted with blood and a silent howl lodged in its throat. |
| TrophyWraith | $item_trophy_wraith | Wraith Trophy | Trophy | trophies | The shed skin of a wraith, a flowing robe only visible by moonlight. |
| TrophyWrithan | $item_trophy_writhan | Writhan Trophy | Trophy | trophies | Tough dead, it should probably be handled delicately. |
| AtgeirBlackmetal | $item_atgeir_blackmetal | Black Metal Atgeir | TwoHanded | weapons | A vicious hewing-axe of almost unbreakable black metal. |
| AtgeirBronze | $item_atgeir_bronze | Bronze Atgeir | TwoHanded | weapons | A true warrior's tool. |
| AtgeirGold | $item_atgeir_gold | Nord Atgeir | TwoHanded | weapons | The edge of this weapon is as deadly as it is shiny. |
| AtgeirGold_BloodLightning | $item_atgeir_gold_bloodlightning | Thunderblood Atgeir | TwoHanded | weapons | The heavens shall sound their praise as you make your enemies bleed. |
| AtgeirGold_FrostFire | $item_atgeir_gold_frostfire | Frostfire Atgeir | TwoHanded | weapons | Let the flames begin to devour, while the ice claims whatever remains. |
| AtgeirHimminAfl | $item_atgeir_himminafl | Himminafl | TwoHanded | weapons | It might not be a hammer, but Thor himself would still approve of this weapon. |
| AtgeirIron | $item_atgeir_iron | Iron Atgeir | TwoHanded | weapons | Blood-drinker, skull-cracker, death-bringer. |
| AtgeirWood | $item_atgeir_wood | Wooden Atgeir | TwoHanded | weapons | It might not hurt a lot, but it has reach! |
| AxeBerzerkr | $item_axe_berzerkr | Berserkir Axes | TwoHanded | weapons | Let your rage take over and face the slaughter. |
| AxeBerzerkrBlood | $item_axe_berzerkr_blood | Bleeding Berserkir Axes | TwoHanded | weapons | The closer you are to death, the harder you are sure to hit. |
| AxeBerzerkrLightning | $item_axe_berzerkr_lightning | Thundering Berserkir Axes | TwoHanded | weapons | Carnage spreads around you when you wield these axes, such that Thor himself would be proud. |
| AxeBerzerkrNature | $item_axe_berzerkr_nature | Primal Berserkir Axes | TwoHanded | weapons | Your most primal instincts take over, and the nature around you reaches out to aid you. |
| AxeEarly | $item_axe_early | Early Axes | TwoHanded | weapons | Mighty weapons from long ago, from a time when the world was still young and incomplete... |
| Battleaxe | $item_battleaxe | Battleaxe | TwoHanded | weapons | Skull-splitter, a warrior's joy. |
| BattleaxeBlackmetal | $item_battleaxe_blackmetal | Black Metal Battleaxe | TwoHanded | weapons | Green shall be the last thing your foes see before you cleave them in half. |
| BattleaxeCrystal | $item_battleaxe_crystal | Crystal Battleaxe | TwoHanded | weapons | It's see-through and tears through. |
| BattleaxeGold | $item_axe2h_gold | Nord Greataxe | TwoHanded | weapons | A mighty axe fit for a mighty warrior. |
| BattleaxeGold_BloodLightning | $item_axe2h_gold_bloodlightning | Thunderblood Greataxe | TwoHanded | weapons | As you cleave your foes in two, their blood shall sing like a thunderstorm. |
| BattleaxeGold_FrostFire | $item_axe2h_gold_frostfire | Frostfire Greataxe | TwoHanded | weapons | Strike your foes with a frozen inferno! |
| BattleaxeSkullSplittur | $item_battleaxe_skullsplittur | Skull Splittur | TwoHanded | weapons | Will find skulls to split even in the thickest of mists. |
| BattleaxeWood | $item_battleaxe_wood | Wooden Battleaxe | TwoHanded | weapons | Intimidating, but not as intimidating as the real thing. |
| Elaking_AttackJump | Charred Sword |  | TwoHanded | attacks |  |
| FW_BattleaxeCrystal | $item_battleaxe_crystal | Crystal Battleaxe | TwoHanded | Equipment | It's see-through and tears through. |
| FW_KnifeSkollAndHati | $item_knife_skollandhati | Skoll and Hati | TwoHanded | Equipment | Stab once for those who've betrayed you, and twice for those you hate. |
| FW_StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | Equipment | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| FW_StaffLightning | $item_staff_lightning | Dundr | TwoHanded | Equipment | What happens next may shock you. |
| FishingRod | $item_fishingrod | Fishing Rod | TwoHanded | tools | Standard issue dvergr fishing rod. |
| FistBjornClaw | $item_fistweapon_bjorn | Paws of the Bear | TwoHanded | weapons | Made for tearing and rending. |
| FistBjornUndeadClaw | $item_fistweapon_bjorn_undead | Vilebone Maulclaws | TwoHanded | weapons | These claws will rend flesh and bone alike. |
| FistFenrirClaw | $item_fistweapon_fenris | Flesh Rippers | TwoHanded | weapons | If claws work for wolves, why not for a viking? |
| FistGold | $item_fistweapon_gold | Nord Knucklechains | TwoHanded | weapons | Wrap your fists in the hardest of metals, to ensure your foes feel the strength behind your blows. |
| FistGold_BloodLightning | $item_fistweapon_gold_bloodlightning | Thunderblood Knucklechains | TwoHanded | weapons | A fury comes over you as you fight with these weapons, your blood roaring like thunder in your ears… |
| FistGold_FrostFire | $item_fistweapon_frostfire_gold | Frostfire Knucklechains | TwoHanded | weapons | A slight risk of frostbite is inevitable. |
| JotunWarrior1HAxe_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_dodger | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_slashdw | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_sword | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge2 | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge_down | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge_up | Charred Sword |  | TwoHanded | attacks |  |
| KnifeSkollAndHati | $item_knife_skollandhati | Skoll and Hati | TwoHanded | weapons | Stab once for those who've betrayed you, and twice for those you hate. |
| PickaxeAntler | $item_pickaxe_antler | Antler Pickaxe | TwoHanded | weapons | This tool is hard enough to crack even the most stubborn rocks. |
| PickaxeBlackMetal | $item_pickaxe_blackmetal | Black Metal Pickaxe | TwoHanded | weapons | A good strong pick of glistening dark metal. |
| PickaxeBronze | $item_pickaxe_bronze | Bronze Pickaxe | TwoHanded | weapons | A good bronze pick. Can break very hard rocks. |
| PickaxeIron | $item_pickaxe_iron | Iron Pickaxe | TwoHanded | weapons | A sturdy tool of hardened iron. |
| PickaxeStone | $item_pickaxe_stone | Stone Pickaxe | TwoHanded | weapons | A rock-breaker made of stone. |
| SP_BattleaxeCrystal | $item_battleaxe_crystal | Crystal Battleaxe | TwoHanded | Equipment/ShadowPerson | It's see-through and tears through. |
| SP_KnifeSkollAndHati | $item_knife_skollandhati | Skoll and Hati | TwoHanded | Equipment/ShadowPerson | Stab once for those who've betrayed you, and twice for those you hate. |
| SP_StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | Equipment/ShadowPerson | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| SP_StaffLightning | $item_staff_lightning | Dundr | TwoHanded | Equipment/ShadowPerson | What happens next may shock you. |
| Scythe | $item_scythe | Scythe | TwoHanded | tools | The right tool makes the task at hand so much easier. |
| Shovel | $item_snowshovel | Snow Shovel | TwoHanded | tools | Useful for clearing away the deepest of snow. |
| SledgeCheat | Cheat sledge |  | TwoHanded | weapons |  |
| SledgeDemolisher | $item_sledge_demolisher | Demolisher | TwoHanded | weapons | This mighty sledge yearns to wreak havoc. |
| SledgeGold | $item_sledge_gold | Nord Sledge | TwoHanded | weapons | With this weapon, your blows will be heavy as that of a troll. |
| SledgeGold_BloodLightning | $item_mace2h_gold_bloodlightning | Thunderblood Sledge | TwoHanded | weapons | As this weapon strikes true, the blow shall echo throughout the world... |
| SledgeGold_FrostFire | $item_mace2h_gold_frostfire | Frostfire Sledge | TwoHanded | weapons | Is it so cold that it's burning, or so hot that it's freezing? |
| SledgeIron | $item_sledge_iron | Iron Sledge | TwoHanded | weapons | A mighty hammer, worthy of a champion. |
| SledgeStagbreaker | $item_stagbreaker | Stagbreaker | TwoHanded | weapons | A weapon worthy of the Gods! If you get hit with this, you'll know it... |
| SledgeWood | $item_sledge_wood | Wooden Sledge | TwoHanded | weapons | This should probably hurt more than it does. |
| StaffClusterbomb | $item_staffclusterbomb | Staff of Fracturing | TwoHanded | weapons | Only those with patience and focus will be able to harness the true power of this staff. |
| StaffFireball | $item_stafffireball | Staff of Embers | TwoHanded | weapons | The sweltering heat of Muspelheim seems almost pathetic when compared to what this staff can do... |
| StaffGreenRoots | $item_staffgreenroots | Staff of the Wild | TwoHanded | weapons | Ancient natural forces lie curled and dormant within this staff, ready to be unleashed. |
| StaffIceShards | $item_stafficeshards | Staff of Frost | TwoHanded | weapons | A staff as cold as the three-year winter that will herald the end of times. |
| StaffLightning | $item_staff_lightning | Dundr | TwoHanded | weapons | What happens next may shock you. |
| StaffOrbofAhri | $item_staff_orbofahri | Echo Spike | TwoHanded | weapons | A chill that goes right through to the bone. |
| StaffRedTroll | $item_staffredtroll | Trollstav | TwoHanded | weapons | Summons a raging beast to cause death and destruction. |
| StaffShield | $item_staffshield | Staff of Protection | TwoHanded | weapons | For a slight blood offering it will protect the caster in a magical shell. |
| StaffThunderBlood | $item_staff_thunderblood | Lightning Strike | TwoHanded | weapons | Simply point, and you shall summon the wrath of the sky. |
| THSwordGold | $item_sword2h_gold | Nord Greatsword | TwoHanded | weapons | A striking weapon, both visually and lethally. |
| THSwordGold_BloodLightning | $item_sword2h_gold_bloodlightning | Thunderblood Greatsword | TwoHanded | weapons | Anyone wielding this weapon is sure to be very frightening indeed. |
| THSwordGold_FrostFire | $item_sword2h_gold_frostfire | Frostfire Greatsword | TwoHanded | weapons | The choice between a fiery end and a frozen one is simple: Both at the same time. |
| THSwordKrom | $item_sword_krom | Krom | TwoHanded | weapons | As deadly as it is shiny, and it's very shiny. |
| THSwordSlayer | $item_sword_slayer | Slayer | TwoHanded | weapons | This mighty blade thirsts for the blood of foes. |
| THSwordSlayerBlood | $item_sword_slayer_blood | Brutal Slayer | TwoHanded | weapons | If it bleeds, you can kill it. |
| THSwordSlayerLightning | $item_sword_slayer_lightning | Scourging Slayer | TwoHanded | weapons | The lightning bound into this blade is erratic, ever searching for something to strike. |
| THSwordSlayerNature | $item_sword_slayer_nature | Primal Slayer | TwoHanded | weapons | The blade works in tandem with the primal forces of the world, seeking death and slaughter. |
| THSwordWood | $item_sword_wood_2h | Wooden Greatsword | TwoHanded | weapons | Would you be careful with that, please? |
| charred_dyrnwyn_greatsword_feint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_dyrnwyn_greatsword_swing | Charred Sword |  | TwoHanded | Weapons |  |
| charred_dyrnwyn_greatsword_thrust | Charred Sword |  | TwoHanded | Weapons |  |
| charred_dyrnwyn_greatsword_thrustfeint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_fader_greatsword_feint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_fader_greatsword_swing | Charred Sword |  | TwoHanded | Weapons |  |
| charred_fader_greatsword_thrust | Charred Sword |  | TwoHanded | Weapons |  |
| charred_fader_greatsword_thrustfeint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_greatsword_feint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_greatsword_swing | Charred Sword |  | TwoHanded | Weapons |  |
| charred_greatsword_thrust | Charred Sword |  | TwoHanded | Weapons |  |
| charred_greatsword_thrustfeint | Charred Sword |  | TwoHanded | Weapons |  |
| charred_magestaff_fire | Bow |  | TwoHanded | Weapons |  |
| charred_magestaff_summon | Bow |  | TwoHanded | Weapons |  |
| charred_twitcher_scratch_l | Charred Sword |  | TwoHanded | Weapons |  |
| charred_twitcher_scratch_r | Charred Sword |  | TwoHanded | Weapons |  |
| StaffFrostOrbs | $item_staff_frostorbs | Northern Vengeance | TwoHandedLeft | weapons | A caged snowflake, endless patterns emerging from within... |
| StaffSkeleton | $item_staffskeleton | Dead Raiser | TwoHandedLeft | weapons | Sacrifice a bit of blood to raise the dead. Upgrade the skull to spawn multiple skeletons, and incr… |
| StaffSpiritCaller | $item_staff_spiritcaller | Spirit Caller | TwoHandedLeft | weapons | Summon otherworldly aid. It only costs a drop of your blood... |
| BeltStrength | $item_beltstrength | Megingjord | Utility | utility | Gives the wearer superhuman strength. |
| Demister | $item_demister | Wisplight | Utility | utility | A bound wisp to guide you through the thickest of mists. |
| DvergerArbalest | $item_crossbow_arbalest | Arbalest | Utility | Gear | A slow but powerful weapon. |
| DvergerStaffFire | Club |  | Utility | Gear | A crude but useful weapon. |
| DvergerStaffHeal | Club |  | Utility | Gear | A crude but useful weapon. |
| DvergerStaffIce | Club |  | Utility | Gear | A crude but useful weapon. |
| DvergerStaffSupport | Club |  | Utility | Gear | A crude but useful weapon. |
| GoblinBrute_LegBones | Iron plate armor |  | Utility | misc | An iron scale mail, this will turn all but the strongest of blows. |
| GoblinShaman_Staff_Bones | Club |  | Utility | misc | A crude but useful weapon. |
| GoblinShaman_Staff_Feathers | Club |  | Utility | misc | A crude but useful weapon. |
| GoblinShaman_Staff_Hildir | Club |  | Utility |  | A crude but useful weapon. |
| IceShoes | $item_iceshoes |  | Utility | utility |  |
| IceSkates | $item_iceskates |  | Utility | utility |  |
| Wishbone | $item_wishbone | Wishbone | Utility | utility | This ancient bone remembers the location of many forgotten things. |

## 6. Locations (ZoneSystem)

All entries of `ZoneSystem.m_locations` (main scene) and the six `LocationList` prefabs. `list` is where the entry lives. `prefab` is resolved from the SoftRef asset id through the bundle manifest. `contents` summarises what the location prefab holds: who stands there, what it spawns, altars, vegvisirs (pointing at which location), runestone texts, dungeon entrances.

| prefab | entry name | list | biome | qty | group | flags | folder | contents |
|---|---|---|---|---|---|---|---|---|
| Castle |  | main |  | 200 |  | DISABLED |  |  |
| Fort1 |  | main |  | 0 |  | DISABLED |  |  |
| GoblinCamp1 |  | main | Plains | 200 |  | DISABLED |  |  |
| Greydwarf_camp2 |  | main | BlackForest | 50 |  | DISABLED |  |  |
| Greydwarf_camp3 |  | main | BlackForest | 100 |  | DISABLED |  |  |
| Hugintest |  | main | Meadows | 0 |  | DISABLED |  |  |
| MountainCave01 |  | main | Mountain | 500 |  | DISABLED |  |  |
| Pillar1 |  | main | BlackForest | 0 |  | DISABLED |  |  |
| Pillar2 |  | main | BlackForest | 0 |  | DISABLED |  |  |
| StoneHouse1 |  | main | BlackForest | 0 |  | DISABLED |  |  |
| StoneHouse2 |  | main | BlackForest | 0 |  | DISABLED |  |  |
| StoneHouse5 |  | main | BlackForest | 0 |  | DISABLED |  |  |
| StoneTower2 |  | main | Plains | 100 |  | DISABLED |  |  |
| StoneTower4 |  | main | Plains | 100 |  | DISABLED |  |  |
| SunkenCrypt1 |  | main | Swamp | 0 |  | DISABLED |  |  |
| SunkenCrypt2 |  | main | Swamp | 0 |  | DISABLED |  |  |
| SunkenCrypt3 |  | main | Swamp | 0 |  | DISABLED |  |  |
| TrollCave |  | main | BlackForest | 100 |  | DISABLED |  |  |
| xmastree |  | main |  | 0 |  | DISABLED |  |  |
| BigRockClearing | BigRockClearing | main | BlackForest | 10 |  | unique | BlackForest |  |
| Crypt2 | Crypt2 | main | BlackForest | 200 |  |  | BlackForest | enter: Burial Chambers; spawns: Skeleton; raven: Hugin: Treasures lie below |
| Crypt3 | Crypt3 | main | BlackForest | 200 |  |  | BlackForest | enter: Burial Chambers; spawns: Skeleton; raven: Hugin: Treasures lie below |
| Crypt4 | Crypt4 | main | BlackForest | 200 |  |  | BlackForest | enter: Burial Chambers; spawns: Skeleton; raven: Hugin: Treasures lie below |
| GDKing | GDKing | main | BlackForest | 4 |  | prioritized | BlackForest | altar: Ancient Bowl: offer AncientSeed x3 -> gd_king; runestone: $lore_gdking |
| Greydwarf_camp1 | Greydwarf_camp1 | main | BlackForest | 300 |  |  | BlackForest | spawns: Greydwarf, Greydwarf_Elite, Greydwarf_Shaman |
| Ruin1 | Ruin1 | main | BlackForest | 200 |  |  | BlackForest | spawns: Greydwarf, Greydwarf_Shaman; chests: Chest |
| Ruin2 | Ruin2 | main | BlackForest | 200 |  |  | BlackForest | vegvisir->GDKing (The Elder); spawns: Greydwarf, Greydwarf_Elite; chests: Chest |
| Runestone_BlackForest | Runestone_BlackForest | main | BlackForest | 50 | Runestones |  | BlackForest | runestone: $lore_blackforest_random01, $lore_blackforest_random02, $lore_blackforest_random03, $lore_blackforest_random04 … |
| Runestone_Greydwarfs | Runestone_Greydwarfs | main | BlackForest | 25 | Runestones |  | BlackForest | spawns: Greydwarf; runestone: $lore_greydwarfs, $lore_greydwarfs_label |
| StoneTowerRuins03 | StoneTowerRuins03 | main | BlackForest | 80 | Stonetowerruins |  | BlackForest | vegvisir->GDKing (The Elder); spawns: Greydwarf, Greydwarf_Elite, Skeleton; chests: Chest |
| StoneTowerRuins07 | StoneTowerRuins07 | main | BlackForest | 80 | Stonetowerruins |  | BlackForest | spawns: Skeleton; chests: Chest |
| StoneTowerRuins08 | StoneTowerRuins08 | main | BlackForest | 80 | Stonetowerruins |  | BlackForest | spawns: Skeleton; chests: Chest |
| StoneTowerRuins09 | StoneTowerRuins09 | main | BlackForest | 80 | Stonetowerruins |  | BlackForest | spawns: Skeleton; chests: Chest |
| StoneTowerRuins10 | StoneTowerRuins10 | main | BlackForest | 80 | Stonetowerruins |  | BlackForest | spawns: Skeleton; chests: Chest |
| TrollCave02 | TrollCave02 | main | BlackForest | 200 |  |  | BlackForest | enter: Troll Cave; spawns: Troll; chests: Chest |
| Vendor_BlackForest | Vendor_BlackForest | main | BlackForest | 10 |  | unique prioritized map icon | BlackForest | npc: Haldor, Halstein (Halstein) |
| GoblinCamp2 | GoblinCamp2 | main | Plains | 200 |  |  | Heath |  |
| GoblinKing | GoblinKing | main | Plains | 4 |  | prioritized | Heath | altar: Mystical Altar: offer GoblinTotem x3 -> GoblinKing; runestone: $lore_goblinking |
| Ruin3 | Ruin3 | main | Plains | 50 | Goblintower |  | Heath | spawns: Goblin; chests: Chest |
| Runestone_Plains | Runestone_Plains | main | Plains | 100 | Runestones |  | Heath | runestone: $lore_plains_random01, $lore_plains_random02, $lore_plains_random03, $lore_plains_random04 … |
| StoneHenge1 | StoneHenge1 | main | Plains | 5 | Stonehenge |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: GoblinBrute; chests: Chest |
| StoneHenge2 | StoneHenge2 | main | Plains | 5 | Stonehenge |  | Heath | spawns: GoblinBrute; chests: Chest |
| StoneHenge3 | StoneHenge3 | main | Plains | 5 | Stonehenge |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: GoblinBrute; chests: Chest |
| StoneHenge4 | StoneHenge4 | main | Plains | 5 | Stonehenge |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: GoblinBrute |
| StoneHenge5 | StoneHenge5 | main | Plains | 20 | Stonehenge |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: Goblin |
| StoneHenge6 | StoneHenge6 | main | Plains | 20 | Stonehenge |  | Heath |  |
| StoneTower1 | StoneTower1 | main | Plains | 50 | Goblintower |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: Goblin; chests: Chest |
| StoneTower3 | StoneTower3 | main | Plains | 50 | Goblintower |  | Heath | vegvisir->GoblinKing (Yagluth); spawns: Goblin; chests: Chest |
| CombatRuin01 | CombatRuin01 | main | Meadows | 5 |  |  | Meadows | spawns: Skeleton; chests: Chest |
| Dolmen01 | Dolmen01 | main | Meadows, BlackForest | 100 |  |  | Meadows | spawns: Skeleton_Meadows_noarcher |
| Dolmen02 | Dolmen02 | main | Meadows, BlackForest | 100 |  |  | Meadows | spawns: Skeleton_Meadows_noarcher |
| Dolmen03 | Dolmen03 | main | Meadows, BlackForest | 50 |  |  | Meadows | spawns: Skeleton_Meadows_noarcher |
| Eikthyrnir | Eikthyrnir | main | Meadows | 3 |  | prioritized | Meadows | altar: Mystical Altar: offer TrophyDeer x2 -> Eikthyr; runestone: $lore_eikthyr; raven: Hugin: Calling forth the beast |
| Runestone_Boars | Runestone_Boars | main | Meadows | 50 | Runestones |  | Meadows | spawns: Boar; runestone: $lore_meadows_boartaming, $lore_meadows_boartaming_label |
| Runestone_Meadows | Runestone_Meadows | main | Meadows | 100 | Runestones |  | Meadows | runestone: $lore_meadows_random01, $lore_meadows_random02, $lore_meadows_random03, $lore_meadows_random04 … |
| ShipSetting01 | ShipSetting01 | main | Meadows | 100 |  |  | Meadows | spawns: Skeleton_Meadows_noarcher; chests: Chest |
| StartTemple | StartTemple | main | Meadows | 1 |  | prioritized map icon | Meadows | vegvisir->Eikthyrnir (Eikthyr); runestone: $guardianstone_bonemass_desc, $guardianstone_eikthyr_desc, $guardianstone_moder_desc, $guardianstone_theelder_desc …; raven: Hugin: Oden is pleased; Hugin: This stone is a Vegvisir; Hugin: Welcome to the tenth world, warrior; boss stones |
| WoodFarm1 | WoodFarm1 | main | Meadows | 10 | woodvillage |  | Meadows |  |
| WoodHouse1 | WoodHouse1 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodHouse10 | WoodHouse10 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodHouse11 | WoodHouse11 | main | Meadows | 20 |  |  | Meadows | spawns: Greydwarf; chests: Chest |
| WoodHouse12 | WoodHouse12 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodHouse13 | WoodHouse13 | main | Meadows | 20 |  |  | Meadows | spawns: Greydwarf; chests: Chest |
| WoodHouse2 | WoodHouse2 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodHouse3 | WoodHouse3 | main | Meadows | 20 |  |  | Meadows |  |
| WoodHouse4 | WoodHouse4 | main | Meadows | 20 |  |  | Meadows |  |
| WoodHouse5 | WoodHouse5 | main | Meadows | 20 |  |  | Meadows |  |
| WoodHouse6 | WoodHouse6 | main | Meadows | 20 |  |  | Meadows | spawns: Greydwarf; chests: Chest |
| WoodHouse7 | WoodHouse7 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodHouse8 | WoodHouse8 | main | Meadows | 20 |  |  | Meadows |  |
| WoodHouse9 | WoodHouse9 | main | Meadows | 20 |  |  | Meadows | chests: Chest |
| WoodVillage1 | WoodVillage1 | main | Meadows | 15 | woodvillage |  | Meadows |  |
| DevBedchamber | DevBedchamber | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: bathtub; chests: Yuleklapp |
| DevCombatRange | DevCombatRange | main |  | 5 |  | DISABLED | Misc | chests: Black Metal Chest |
| DevCombatRing | DevCombatRing | main |  | 0 |  | DISABLED | Misc | chests: Black Metal Chest |
| DevDressingRoom | DevDressingRoom | main |  | 0 |  | DISABLED | Misc | chests: Chest, Large Red Pot |
| DevFloor1 | DevFloor1 | main |  | 0 |  | DISABLED | Misc |  |
| DevForge | DevForge | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A new tool; Hugin: Record your exploration; Hugin: You have built a smelter; Hugin: You have built a workbench; chests: Black Metal Chest |
| DevGarden | DevGarden | main |  | 0 |  | DISABLED | Misc | raven: Hugin: You have built a workbench; chests: Reinforced Chest |
| DevGround1 | DevGround1 | main |  | 0 |  | DISABLED | Misc |  |
| DevGround2 | DevGround2 | main |  | 0 |  | DISABLED | Misc |  |
| DevHouse1 | DevHouse1 | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: You have built a workbench; chests: Black Metal Chest, Yuleklapp |
| DevHouse2 | DevHouse2 | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary! |
| DevHouse3 | DevHouse3 | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: You have built a workbench; chests: Black Metal Chest |
| DevHouse4 | DevHouse4 | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: You have built a workbench; Hugin: magetable1; chests: Black Metal Chest |
| DevHouse5 | DevHouse5 | main |  | 0 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: A new tool; Hugin: You have built a workbench; Hugin: magetable1; Hugin: tissueref1; chests: Black Metal Chest |
| DevHouseStart | DevHouseStart | main |  | 5 |  | DISABLED | Misc | raven: Hugin: A headrest for the weary!; Hugin: You have built a smelter; Hugin: You have built a workbench; chests: Chest |
| DevKitchen | DevKitchen | main |  | 0 |  | DISABLED | Misc | raven: Hugin: You have built a workbench; chests: Black Metal Chest, Chest |
| DevMageRoom | DevMageRoom | main |  | 0 |  | DISABLED | Misc | raven: Hugin: You have built a workbench; Hugin: magetable1; Hugin: tissueref1; chests: Grausten Chest |
| DevSoundTest | DevSoundTest | main |  | 0 |  | DISABLED | Misc |  |
| DevWall1 | DevWall1 | main |  | 0 |  | DISABLED | Misc |  |
| DevWall2 | DevWall2 | main |  | 0 |  | DISABLED | Misc |  |
| FireHole | FireHole | main | Swamp | 75 | FireHole |  | Misc | spawns: Surtling |
| ShipWreck01 | ShipWreck01 | main | Swamp, BlackForest, Plains, Ocean | 25 | Shipwreck |  | Misc | chests: Chest |
| ShipWreck02 | ShipWreck02 | main | Swamp, BlackForest, Plains, Ocean | 25 | Shipwreck |  | Misc | chests: Chest |
| ShipWreck03 | ShipWreck03 | main | Swamp, BlackForest, Plains, Ocean | 25 | Shipwreck |  | Misc | chests: Chest |
| ShipWreck04 | ShipWreck04 | main | Swamp, BlackForest, Plains, Ocean | 25 | Shipwreck |  | Misc | chests: Chest |
| StoneCircle | StoneCircle | main | Meadows | 25 |  |  | Misc |  |
| StoneHouse1_heath | StoneHouse1_heath | main | Plains | 0 |  |  | Misc | spawns: Goblin; chests: Chest |
| StoneHouse2_heath | StoneHouse2_heath | main | Plains | 0 |  |  | Misc | spawns: Goblin |
| StoneHouse3 | StoneHouse3 | main | BlackForest | 200 |  |  | Misc | spawns: Greydwarf; chests: Chest |
| StoneHouse4 | StoneHouse4 | main | BlackForest | 200 |  |  | Misc | spawns: Greydwarf |
| StoneHouse5_heath | StoneHouse5_heath | main | Plains | 0 |  |  | Misc | chests: Chest |
| AbandonedLogCabin02 | AbandonedLogCabin02 | main | Mountain | 33 | Abandonedcabin |  | Mountains | spawns: StoneGolem; chests: Chest |
| AbandonedLogCabin03 | AbandonedLogCabin03 | main | Mountain | 33 | Abandonedcabin |  | Mountains | spawns: Skeleton_Mountains, StoneGolem; chests: Chest |
| AbandonedLogCabin04 | AbandonedLogCabin04 | main | Mountain | 50 | Abandonedcabin |  | Mountains | spawns: Skeleton_Mountains, StoneGolem; chests: Chest |
| Dragonqueen | Dragonqueen | main | Mountain | 3 |  | prioritized | Mountains | altar: Sacrificial Altar: items on its item stands -> Dragon; runestone: $lore_dragonqueen |
| DrakeLorestone | DrakeLorestone | main | Mountain | 50 | Runestones |  | Mountains | runestone: $lore_drake, $lore_drake_label |
| DrakeNest01 | DrakeNest01 | main | Mountain | 200 |  |  | Mountains | spawns: Hatchling |
| MountainGrave01 | MountainGrave01 | main | Mountain | 100 |  |  | Mountains |  |
| MountainWell1 | MountainWell1 | main | Mountain | 25 |  |  | Mountains | chests: Chest |
| Runestone_Mountains | Runestone_Mountains | main | Mountain | 100 | Runestones |  | Mountains | runestone: $lore_mountains_fenring, $lore_mountains_random01, $lore_mountains_random02, $lore_mountains_random03 … |
| StoneTowerRuins04 | StoneTowerRuins04 | main | Mountain | 50 | Mountainruin |  | Mountains | vegvisir->Dragonqueen (Moder); spawns: Skeleton_Mountains; chests: Chest |
| StoneTowerRuins05 | StoneTowerRuins05 | main | Mountain | 50 | Mountainruin |  | Mountains | spawns: Skeleton, Skeleton_Mountains; chests: Chest |
| Waymarker01 | Waymarker01 | main | Mountain | 50 |  |  | Mountains |  |
| Waymarker02 | Waymarker02 | main | Mountain | 50 |  |  | Mountains |  |
| Bonemass | Bonemass | main | Swamp | 5 |  | prioritized | Swamp | altar: Boiling Death: offer WitheredBone x10 -> Bonemass; runestone: $lore_bonemass |
| Grave1 | Grave1 | main | Swamp | 200 |  |  | Swamp | spawns: Draugr, Draugr_Elite, Draugr_Ranged, Skeleton_Swamps; chests: Chest |
| InfestedTree01 | InfestedTree01 | main | Swamp | 700 |  |  | Swamp |  |
| Runestone_Draugr | Runestone_Draugr | main | Swamp | 50 | Runestones |  | Swamp | spawns: Draugr; runestone: $lore_draugr, $lore_draugr_label |
| Runestone_Swamps | Runestone_Swamps | main | Swamp | 100 | Runestones |  | Swamp | runestone: $lore_swamp_random01, $lore_swamp_random02, $lore_swamp_random03, $lore_swamp_random04 … |
| SunkenCrypt4 | SunkenCrypt4 | main | Swamp | 175 | SunkenCrypt | prioritized | Swamp | enter: Sunken Crypts; spawns: BlobElite, Draugr; locked by: CryptKey |
| SwampHut1 | SwampHut1 | main | Swamp | 50 | Swamphut |  | Swamp | spawns: Wraith; chests: Chest |
| SwampHut2 | SwampHut2 | main | Swamp | 50 | Swamphut |  | Swamp | spawns: Wraith; chests: Chest |
| SwampHut3 | SwampHut3 | main | Swamp | 50 | Swamphut |  | Swamp | spawns: Wraith; chests: Chest |
| SwampHut4 | SwampHut4 | main | Swamp | 50 | Swamphut |  | Swamp | spawns: Draugr, Draugr_Ranged; chests: Chest |
| SwampHut5 | SwampHut5 | main | Swamp | 25 | Swamphut |  | Swamp | spawns: Wraith; chests: Chest |
| SwampRuin1 | SwampRuin1 | main | Swamp | 30 | SwampRuin |  | Swamp | vegvisir->Bonemass (Bonemass); spawns: Draugr, Draugr_Elite, Draugr_Ranged; chests: Chest |
| SwampRuin2 | SwampRuin2 | main | Swamp | 30 | SwampRuin |  | Swamp | vegvisir->Bonemass (Bonemass); spawns: Draugr, Draugr_Elite, Draugr_Ranged; chests: Chest |
| SwampWell1 | SwampWell1 | main | Swamp | 25 |  |  | Swamp | spawns: Draugr_Elite |
| AshlandRuins | AshlandRuins | Ashlands | Ashlands | 100 |  |  | Ashlands |  |
| CharredFortress | CharredFortress | Ashlands | Ashlands | 20 | FaderBoss | prioritized | Ashlands | vegvisir->FaderLocation (The Emerald Flame); spawns: Charred_Archer, Charred_Mage, Charred_Melee, piece_Charred_Balista; chests: Charred Chest |
| CharredRuins1 | CharredRuins1 | Ashlands | Ashlands | 75 | zigg |  | Ashlands | vegvisir->FaderLocation (The Emerald Flame); raven: Munin: npc_munin_ashlands_general02 |
| CharredRuins2 | CharredRuins2 | Ashlands | Ashlands | 100 |  |  | Ashlands | vegvisir->FaderLocation (The Emerald Flame) |
| CharredRuins3 | CharredRuins3 | Ashlands | Ashlands | 100 |  |  | Ashlands | vegvisir->FaderLocation (The Emerald Flame) |
| CharredRuins4 | CharredRuins4 | Ashlands | Ashlands | 100 |  |  | Ashlands | vegvisir->FaderLocation (The Emerald Flame) |
| CharredStone_Spawner | CharredStone_Spawner | Ashlands | Ashlands | 300 |  |  | Ashlands | spawns: Charred_Melee, Charred_Twitcher |
| CharredTowerRuins1 | CharredTowerRuins1 | Ashlands | Ashlands | 30 | towerruins |  | Ashlands |  |
| CharredTowerRuins1_dvergr | CharredTowerRuins1_dvergr | Ashlands | Ashlands | 30 | towerruins |  | Ashlands | spawns: DvergerAshlands; raven: Hugin: You have built a ward |
| CharredTowerRuins2 | CharredTowerRuins2 | Ashlands | Ashlands | 40 |  |  | Ashlands |  |
| CharredTowerRuins3 | CharredTowerRuins3 | Ashlands | Ashlands | 30 |  |  | Ashlands | spawns: Charred_Melee, Charred_Twitcher |
| DevWallAsh | DevWallAsh | Ashlands | Ashlands | 5 | FaderBoss | prioritized DISABLED | Ashlands |  |
| FaderLocation | FaderLocation | Ashlands | Ashlands | 3 | FaderBoss | prioritized | Ashlands | altar: Altar of The Emerald Flame: offer Bell x3 -> Fader; runestone: $lore_fader |
| FortressRuins | FortressRuins | Ashlands | Ashlands | 100 |  |  | Ashlands |  |
| LeviathanLava | LeviathanLava | Ashlands | Ashlands | 100 |  |  | Ashlands | raven: Munin: npc_munin_ashlands_general03 |
| MorgenHole1 | MorgenHole1 | Ashlands | Ashlands | 40 | MorgenHole |  | Ashlands | enter: Putrid Hole; vegvisir->PlaceofMystery1 (Mysterious Location); spawns: Morgen; raven: Munin: npc_munin_ashlands_general01; chests: Chest |
| MorgenHole2 | MorgenHole2 | Ashlands | Ashlands | 40 | MorgenHole |  | Ashlands | enter: Putrid Hole; vegvisir->PlaceofMystery1 (Mysterious Location); spawns: Morgen; chests: Chest |
| MorgenHole3 | MorgenHole3 | Ashlands | Ashlands | 40 | MorgenHole |  | Ashlands | enter: Putrid Hole; vegvisir->PlaceofMystery1 (Mysterious Location); spawns: Morgen; chests: Chest |
| PlaceofMystery1 | PlaceofMystery1 | Ashlands | Ashlands | 1 | PlaceofMystery | unique prioritized | Ashlands | vegvisir->PlaceofMystery2 (Mysterious Location); spawns: Charred_Archer, Charred_Mage, Charred_Melee, Charred_Twitcher |
| PlaceofMystery2 | PlaceofMystery2 | Ashlands | Ashlands | 1 | PlaceofMystery | unique prioritized | Ashlands | vegvisir->PlaceofMystery3 (Mysterious Location); spawns: Charred_Archer, Charred_Mage, Charred_Melee, Charred_Twitcher |
| PlaceofMystery3 | PlaceofMystery3 | Ashlands | Ashlands | 1 | PlaceofMystery | unique prioritized | Ashlands | enter: Tomb of Lord Reto; spawns: Charred_Archer, Charred_Mage, Charred_Melee, Charred_Melee_Dyrnwyn, Charred_Twitcher |
| Runestone_Ashlands | Runestone_Ashlands | Ashlands | Ashlands | 70 | Runestones |  | Ashlands | runestone: $lore_ashlands_random01, $lore_ashlands_random02, $lore_ashlands_random03, $lore_ashlands_random04 … |
| SulfurArch | SulfurArch | Ashlands | Ashlands | 100 |  |  | Ashlands |  |
| VoltureNest | VoltureNest | Ashlands | Ashlands | 350 |  |  | Ashlands | spawns: Volture; raven: Munin: npc_munin_ashlands_general04 |
| BogWitch_Camp | BogWitch_Camp | Ashlands | Swamp | 10 |  | unique prioritized map icon | BogWitchHut | npc: BogWitch; spawns: BogWitchKvastur |
| Runestone_DeepNorth | Runestone_DeepNorth | DeepNorth | DeepNorth | 70 | Runestones |  | Ashlands | runestone: $lore_deepnorth_hervor01, $lore_deepnorth_hervor02, $lore_deepnorth_hervor03, $lore_deepnorth_hervor04 … |
| BearCave | BearCave | DeepNorth | BlackForest | 50 |  |  | BlackForest | enter: Bear Cave; spawns: Bjorn_sleeping |
| HalfBurried_ForestCrypt | HalfBurried_ForestCrypt | DeepNorth | BlackForest | 100 |  | DISABLED | BlackForest | spawns: Skeleton; chests: Chest |
| DN_Bossroom | bosslocation | DeepNorth | DeepNorth | 3 | dn_boss | prioritized map icon | DeepNorth | enter: The Prison; altar: Strange Bowl: offer HatefulBlood x3 -> FrozenKing; Strange Bowl: offer HatefulBlood x3 -> vfx_LastBossGate_destroyed [sets LastBossGate_Open]; runestone: $lore_frozenking; raven: Hugin: eternalpyre |
| DN_gammeltrollFrac01 | DN_gammeltrollFrac01 | DeepNorth | DeepNorth | 30 |  |  | DeepNorth |  |
| DN_gammeltrollFrac02 | DN_gammeltrollFrac02 | DeepNorth | DeepNorth | 30 |  |  | DeepNorth | raven: Munin: generalDN1 |
| DN_hut01 | hut01 | DeepNorth | DeepNorth | 40 | northvillage |  | DeepNorth | chests: Chest |
| FimbulLocation01 | FimbulLocation01 | DeepNorth |  | 100 |  | DISABLED | DeepNorth | spawns: JotunWarrior, JotunWarriorDualWield, JotunWitch |
| FrozenShip01_DN | FrozenShip01 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth | raven: Munin: shipDN |
| FrozenShip02_DN | FrozenShip02 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth | raven: Munin: shipDN |
| FrozenShip03_DN | FrozenShip03 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth |  |
| HotSpring1 | hotspring | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| HotSpring2 | hotspring2 | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| HotSpring3 | hotspring3 | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| IcePond1 | icepond | DeepNorth | DeepNorth | 40 | icepond |  | DeepNorth |  |
| LumberCamp | Lumbercamp | DeepNorth | DeepNorth | 50 | thehole |  | DeepNorth | chests: Chest |
| MorkBorg | morkborg | DeepNorth | DeepNorth | 40 | morkborg |  | DeepNorth | enter: Mörkhalla; spawns: BlobMork; raven: Munin: morkborg; chests: Ancient Chest, Jotun's Chest; locked by: BloodGoldKey |
| NorthMemorialPlace | NorthMemorialPlace | DeepNorth | DeepNorth | 15 | memorialplace |  | DeepNorth | altar: Ancient Altar: offer MemorialCoal x3 -> memorialsite_offering; vegvisir->DN_Bossroom (Aesir Passage); runestone: $lore_deepnorth_memorial1, $lore_deepnorth_memorial2, $lore_deepnorth_memorial3, $lore_deepnorth_memorial_description …; chests: Chest |
| NorthVillage | NorthVillage | DeepNorth | DeepNorth | 135 |  |  | DeepNorth |  |
| ShipSetting02 | shipsetting | DeepNorth | DeepNorth | 100 | shipsetting |  | DeepNorth | raven: Munin: generalDN; chests: Chest |
| ShipSetting03 | shipsetting | DeepNorth | DeepNorth | 50 | shipsetting |  | DeepNorth | chests: Chest |
| ShipWreck01_DN | Shipwreck_DN | DeepNorth | DeepNorth | 170 |  |  | DeepNorth | chests: Chest |
| ShipWreck02_DN | Shipwreck02_DN | DeepNorth | DeepNorth | 120 |  |  | DeepNorth | chests: Chest |
| TheDarkestHole | darkesthole | DeepNorth | DeepNorth | 1 | thehole | unique prioritized DISABLED | DeepNorth | enter: Winding tunnels; spawns: Elaking, ElakingLantern, Ghost_old |
| TheHole01 | thehole01 | DeepNorth | DeepNorth | 40 |  |  | DeepNorth | enter: Winding tunnels; spawns: ShadowPerson; raven: Munin: generalDN2; chests: Barrel, Wardrobe |
| Hildir_crypt | Hildir_crypt | Hildir | BlackForest | 3 |  | prioritized map icon | BlackForest | enter: Smouldering Tomb; spawns: Skeleton; raven: Hugin: Watch your step, warrior. |
| Hildir_camp | Hildir_camp | Hildir | Meadows | 10 |  | unique prioritized map icon | Meadows | npc: Hildir, HildirsLox (1) (Hallon), HildirsLox (Blåbär); vegvisir->Hildir_cave (Howling Cavern), Hildir_crypt (Smouldering Tomb), Hildir_plainsfortress (Sealed Tower) |
| Hildir_cave | Hildir_cave | Hildir | Mountain | 3 |  | prioritized map icon | Mountains | enter: Howling Cavern; raven: Hugin: Watch your step, warrior. |
| Hildir_plainsfortress | Hildir_plainsfortress | Hildir | Plains | 3 |  | prioritized | Plains |  |
| Mistlands_DvergrBossEntrance1 | Mistlands_DvergrBossEntrance1 | Mistlands | Mistlands | 5 | DvergrBoss | prioritized | Mistlands | enter: Infested Citadel; spawns: Seeker; runestone: $lore_queen; locked by: DvergrKey |
| Mistlands_DvergrTownEntrance1 | Mistlands_DvergrTownEntrance1 | Mistlands | Mistlands | 120 | DvergrDungeon | prioritized | Mistlands | enter: Infested Mine; spawns: Seeker; raven: Hugin: dvergrhalls2; Munin: dvergrhalls1 |
| Mistlands_DvergrTownEntrance2 | Mistlands_DvergrTownEntrance2 | Mistlands | Mistlands | 120 | DvergrDungeon | prioritized | Mistlands | enter: Infested Mine; spawns: Seeker; raven: Hugin: dvergrhalls2; Munin: dvergrhalls1 |
| Mistlands_Excavation1 | Mistlands_Excavation1 | Mistlands | Mistlands | 40 | Excavation |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_Excavation2 | Mistlands_Excavation2 | Mistlands | Mistlands | 40 | Excavation |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_Excavation3 | Mistlands_Excavation3 | Mistlands | Mistlands | 40 | Excavation |  | Mistlands | spawns: Seeker |
| Mistlands_Giant1 | Mistlands_Giant1 | Mistlands | Mistlands | 250 | Giant |  | Mistlands | spawns: Tick; raven: Hugin: giantremains2; Munin: giantremains1 |
| Mistlands_Giant2 | Mistlands_Giant2 | Mistlands | Mistlands | 85 | Giant |  | Mistlands | raven: Munin: giantremains1 |
| Mistlands_GuardTower1_new | Mistlands_GuardTower1_new | Mistlands | Mistlands | 75 | Dvergr |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_GuardTower1_ruined_new | Mistlands_GuardTower1_ruined_new | Mistlands | Mistlands | 80 | Dvergr |  | Mistlands | spawns: Seeker; chests: Dvergr Treasure Chest |
| Mistlands_GuardTower1_ruined_new2 | Mistlands_GuardTower1_ruined_new2 | Mistlands | Mistlands | 20 | Dvergr |  | Mistlands | spawns: Seeker; chests: Dvergr Treasure Chest |
| Mistlands_GuardTower2_new | Mistlands_GuardTower2_new | Mistlands | Mistlands | 75 | Dvergr |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_GuardTower3_new | Mistlands_GuardTower3_new | Mistlands | Mistlands | 50 | Dvergr |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_GuardTower3_ruined_new | Mistlands_GuardTower3_ruined_new | Mistlands | Mistlands | 50 | Dvergr |  | Mistlands | spawns: Seeker; chests: Dvergr Treasure Chest |
| Mistlands_Harbour1 | Mistlands_Harbour1 | Mistlands | Mistlands | 100 | Harbour |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward |
| Mistlands_Lighthouse1_new | Mistlands_Lighthouse1_new | Mistlands | Mistlands | 100 | Dvergr |  | Mistlands | spawns: Dverger, DvergerMage; raven: Hugin: You have built a ward; Hugin: dvergr2; Munin: dvergr1 |
| Mistlands_RoadPost1 | Mistlands_RoadPost1 | Mistlands | Mistlands | 500 |  |  | Mistlands | spawns: Dverger, DvergerMage |
| Mistlands_RockSpire1 | Mistlands_RockSpire1 | Mistlands | Mistlands | 200 |  |  | Mistlands | spawns: Seeker; chests: Dvergr Treasure Chest |
| Mistlands_Statue1 | Mistlands_Statue1 | Mistlands | Mistlands | 200 |  |  | Mistlands |  |
| Mistlands_Statue2 | Mistlands_Statue2 | Mistlands | Mistlands | 200 |  |  | Mistlands |  |
| Mistlands_StatueGroup1 | Mistlands_StatueGroup1 | Mistlands | Mistlands | 200 |  |  | Mistlands |  |
| Mistlands_Swords1 | Mistlands_Swords1 | Mistlands | Mistlands | 33 | GiantArmor |  | Mistlands |  |
| Mistlands_Swords2 | Mistlands_Swords2 | Mistlands | Mistlands | 33 | GiantArmor |  | Mistlands |  |
| Mistlands_Swords3 | Mistlands_Swords3 | Mistlands | Mistlands | 33 | GiantArmor |  | Mistlands |  |
| Mistlands_Viaduct1 | Mistlands_Viaduct1 | Mistlands | Mistlands | 100 | Harbour |  | Mistlands |  |
| Mistlands_Viaduct2 | Mistlands_Viaduct2 | Mistlands | Mistlands | 150 | Harbour |  | Mistlands |  |
| Runestone_Mistlands | Runestone_Mistlands | Mistlands | Mistlands | 50 | Runestones |  | Mistlands | runestone: $lore_mistlands_random01, $lore_mistlands_random02, $lore_mistlands_random03, $lore_mistlands_random04 … |
| AncientUpgradeStation | AncientUpgradeStation | MountainCaves | Mountain | 10 | AncientUpgradeStation | unique prioritized map icon | Mountains | runestone: $lore_upgradestation_description, $lore_upgradestation_label; raven: Munin: Hear ye, wanderer! |
| MountainCave02 | MountainCave02 | MountainCaves | Mountain | 120 | mountaincaves |  | Mountains | enter: Frost Caves |
| TarPit1 | TarPit1 | cp1 | Plains | 100 | tarpit |  | Plains | spawns: BlobTar |
| TarPit2 | TarPit2 | cp1 | Plains | 16 | tarpit |  | Plains | spawns: BlobTar |
| TarPit3 | TarPit3 | cp1 | Plains | 100 | tarpit |  | Plains | spawns: BlobTar |

Location prefabs shipped in bundles but not placed by any ZoneSystem list (dev, test, event or spawned-by-code): `GoblinCamp2_1`, `GoblinHut01`, `GoblinHut02`, `GoblinHut03`, `StoneTowerRuins05_leet`, `StoneTowerRuins07_sunk`, `StoneTowerRuins08_sunk`, `StoneTowerRuins09_sunk`, `StoneTowerRuins10_sunk`, `SwampHut1_1`, `SwampHut2_1`, `SwampHut3_1`, `TarPit1_1`, `TarPit2_1`, `TarPit3_1`, `WoodVillage2`

Location display tokens: `location_bearcave` = Bear Cave; `location_darkesthole` = The Hole; `location_dnbossroom` = The First Prison; `location_dnbossroomnew` = The Prison; `location_dvergrboss` = Infested Citadel; `location_dvergrtown` = Infested Mine; `location_enter` = Enter; `location_exit` = Exit; `location_forestcave` = Troll Cave; `location_forestcrypt` = Burial Chambers; `location_mausoleum` = Tomb of Lord Reto; `location_morgenhole` = Putrid Hole; `location_morkhalla` = Mörkhalla; `location_mountaincave` = Frost Caves; `location_sunkencrypt` = Sunken Crypts; `location_thehole` = Winding tunnels

### 6b. Dungeon room themes (rooms used by DungeonGenerator interiors)

| room theme (folder) | creatures spawned | npcs | chests | door keys | runestones | ravens |
|---|---|---|---|---|---|---|
| ashlands | Charred_Twitcher, Morgen |  | Charred Chest |  |  | Munin: npc_munin_ashlands_general05 |
| cave | Bat, Fenring_Cultist, Fenring_Cultist_Hildir, Fish4_cave, StoneGolem, Ulv |  | Chest |  | $lore_caveman01, $lore_caveman02 |  |
| halfBurriedCrypt | Skeleton |  | Chest |  |  |  |
| hole | Elaking, ElakingLantern, ElakingMole |  |  |  | $lore_windingtunnels |  |
| mistlands | Seeker, SeekerBrood, SeekerBrute, Tick |  | Dvergr Treasure Chest |  |  |  |
| morkhalla | BlobMork, DvergerDeepNorth, GoblinDeepNorth, JotunWarrior, JotunWarriorDualWield, JotunWitch |  | Ancient Chest, Jotun's Chest |  |  |  |
| northVillage | ShadowPerson |  | Barrel, Chest |  |  | Munin: generalDN4 |
| rooms | Blob, Boar, Draugr, Draugr_Elite, Draugr_Ranged, Ghost, Goblin, GoblinArcher, GoblinBrute, GoblinShaman, Skeleton, Skeleton_Hildir, Skeleton_Poison |  | Chest |  |  |  |
| tower | Goblin, GoblinArcher, GoblinBruteBros, GoblinShaman |  | Chest |  |  |  |

### 6c. Runestones and lore texts

| location / prefab | stone | token | English (start) |
|---|---|---|---|
| AncientUpgradeStation | RuneStone_UpgradeStation | $lore_upgradestation_description | Ingrid and Torgunn decided to honour the gods in this place. They smiled upon Ingrid, granting her a great boon. Torgunn on the other hand, was made… |
| Bonemass | RuneStone_Bonemass | $lore_bonemass | COOK THEIR REMAINS |
| BossStone_Bonemass | BossStone_Bonemass | $guardianstone_bonemass_desc | Wanderer, look to your feet / That tread upon our tomb / One thousand bones without their meat / Will drag you to your doom |
| BossStone_DragonQueen | BossStone_DragonQueen | $guardianstone_moder_desc | Black wings across the moon and sun / Down from the mountain our mother comes / Her weeping tears will fall like rain / Her voice will call us home a… |
| BossStone_Eikthyr | BossStone_Eikthyr | $guardianstone_eikthyr_desc | His antlers are branches of iron / They crack the rocks and bring down mountains / His hooves are the sound of thunder / His voice a howling gale |
| BossStone_Fader | BossStone_Fader | $guardianstone_fader_desc | A father noble and proud / He soared through skies of fire / Then madness lowered its shroud / And warped his heart's desire |
| BossStone_TheElder | BossStone_TheElder | $guardianstone_theelder_desc | First of the Forest, King-in-the-Wood / Lord over those who dwell at his feet / His roots will grow where cities once stood / Their blood his wine, t… |
| BossStone_TheQueen | BossStone_TheQueen | $guardianstone_thequeen_desc | Born in armour / Mother of many / Queen without crown / Ruler beneath |
| BossStone_Yagluth | BossStone_Yagluth | $guardianstone_yagluth_desc | Long ages past, he wore a crown / Beneath a blood-red sky / Now naught is left of all he was / But his spirit cannot die |
| DN_Bossroom | RuneTablet_FrozenKing | $lore_frozenking | HALT THE INVASION |
| Dragonqueen | RuneStone_DragonQueen | $lore_dragonqueen | SACRIFICE HER SPAWN |
| DrakeLorestone | RuneStone_Drake | $lore_drake | Let you who read me be aware of the Frost wyrms, one of the most ancient kins sprung from Ymir's body. / / The most common form of the wyrm are the D… |
| Eikthyrnir | RuneTablet_Eikthyr | $lore_eikthyr | HUNT HIS KIN |
| FaderLocation | RuneTablet_Fader | $lore_fader | RING HIS PRAISE |
| GDKing | RuneStone_GDKing | $lore_gdking | BURN THEIR YOUNG |
| GoblinKing | RuneTablet_GoblinKing | $lore_goblinking | AND HIS DYING SOUL WAS SPLIT AND SHARED AMONG ALL HIS KIN |
| Mistlands_DvergrBossEntrance1 | RuneStone_Mistlands_bosshint | $lore_queen | We sealed the door and scattered the key. Leave her be. |
| NorthMemorialPlace | RuneStone_Memorial1 | $lore_deepnorth_memorial1 | The mighty ones who came before us rest here. / Tread warily, as their slumber may yet be disturbed. |
| NorthMemorialPlace | RuneStone_Memorial1 | $lore_deepnorth_memorial2 | Death comes for all, in the end, even the mightiest of warriors. / One day you may be counted amongst them. |
| NorthMemorialPlace | RuneStone_Memorial1 | $lore_deepnorth_memorial3 | Let us honour our ancestors, who carved this world that we now live in. / And may they deem us worthy. |
| NorthMemorialPlace | RuneStone_Memorial1 | $lore_deepnorth_memorial_description | Ancestral Memorial |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random01 | Here lie the Jotunn, most ancient of all Oden's kin and fiercest of all his adversaries. In life they were bringers of ruin but now, in death, they n… |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random02 | Where the air is thick with magic / And the earth is quick with life, / The mist breeds wonders. |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random03 | Trust nothing in the mist. I passed through here in a group of seven people but every time we stopped to count, we counted eight. We do not know who… |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random04 | The Dvergr are the descendants of the great smiths of old, delvers in the deep earth, seekers of hidden treasure. / / Here in Valheim they mine the b… |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random05 | The raven showed me how to make a staff that set my beard on fire. I will meddle no more with such things. / / Weary of mist and magic, Ulf carved th… |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random06 | Heed the words of Ulf and do not take the hats of the short ones from their heads. They have no sense of a jest and the only cure for insulting them… |
| RuneStone_Ashlands | RuneStone_Ashlands | $lore_mistlands_random07 | Wanderers in fog, / Where do you go? / Not knowing what you were, / Nor seeing where you go. |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random01 | Beware the deep trees, beware the true dark. When the night comes, keep close to your fire. |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random02 | Beneath the ground, the roots of the forest twine together on a great loom. Pluck one thread and the whole weave will move. Chop down one tree and al… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random03 | I was Harald, a man from the coast. I remember nothing more of my life in Midgard except that I was a warrior. In my dreams I see the faces of those… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random04 | Rest awhile and remember Ulf, who carved this stone with his own hand but could think of nothing to say. |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random05 | Beware the Old One. The ravens say that in ages past he was a shoot of great Yggdrasil itself and a force of wisdom in the days when men and trees we… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random06 | We who were carried here by the Valkyrie are not the first men in this land. I have seen with my own eyes the halls they made beneath the ground and… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random07 | Eight were the creatures banished to this world by mighty Oden in the first days of his kingship. Eight Gods and monsters too proud to bear his yoke.… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random08 | The trolls of Midgard may be fading from your memory, as indeed they are from all the race of man. Few and forlorn, they crouch in damp caves and gna… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random09 | Raised by the Old Man of the Forest from seeds of sin, the greydwarfs clothe themselves in the human forms they once knew, but there is no longer any… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random10 | Which among you was a murderer, a kin-slayer, a renegade? Who turned a blade in their brother's back? Who tore babes from their mothers' arms, set fi… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random11 | Look to the sky where mighty Yggdrasil reaches out, called by the Forsaken Ones to join itself once more with this wayward world. It is a glorious si… |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random12 | Astrid will not look at me twice. I leave this stone to honour great Freya. O goddess, grant me a beard like Bjorn's that I might win her heart! |
| RuneStone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random13 | I have been reborn many times in this world. I have died again and again only to awaken in my own house. Yet still I wonder how many more times I wil… |
| RuneStone_Boars | RuneStone_Boars | $lore_meadows_boartaming | This land is hard and wild but we who are brought here are harder still. Take comfort, traveller, in the gifts before you, the good wood and stone, t… |
| RuneStone_Bonemass | RuneStone_Bonemass | $lore_bonemass | COOK THEIR REMAINS |
| RuneStone_CaveMan | RuneStone_CaveMan | $lore_caveman01 | The depths of the cave promised me riches. / Down turns out, is easier than up. / The lake helped best it could, but it could only do so much. / It t… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor01 | The Deep North. The final obstacle before I can prove myself worthy. / / The trials have been many, and so have the deaths. I have lost friends dear… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor02 | It seems peaceful enough. So far, the wildlife doesn’t appear to care much for my presence here. / / I should be able to gather some valuable resourc… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor03 | I have to be on my guard. Few places have been peaceful since I first arrived here, and it would be foolish to believe these lands to be different. /… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor04 | So far I’ve been left alone. Despite that, I can’t shake the feeling that something is watching me. |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor05 | I found an old building, but it was deserted. It’s hard to say for how long, but I don’t believe I’ll find any friends here. |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor06 | I don’t like to think about what could have happened, had my axe not been as sharp nor my senses as keen. After all, there is no one left to come to… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor07 | It’s strange. I can’t help but feel like there should be people here, just waiting to welcome me. / / I think the winters looked like this when I was… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor08 | I have been haunted by dreams lately, in which I am a raven. On great, black wings I soar across this world that I’ve come to know, but it is not the… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor09 | I know now where I need to go. The Ancestors have pointed me in the direction of the one who rules these lands, the one they tried to keep in chains… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random01 | At first this seemed a good home for Ulf, who liked the cold and the snow. Then came the mean little creatures, intent on dragging me below, and I wa… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random02 | If what the raven says is true, there is only one more forsaken I must slay. But what comes after? Will my son truly be returned to me, or I to him?… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random03 | These lands seemed so familiar, awakening memories buried deep within our minds. We should have known they were not all that they seemed. |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random04 | Heed the words of Kata, who once thought the trolls of the forest were mighty foes. Their kin in these lands are tall as the tallest of towers, and h… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random05 | When we first arrived here, this place was rich and prosperous. But the winters are colder and longer now, and we fear they might soon take over alto… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random06 | In the farthest north / Where magic dances in the skies / Offer to call forth / What just below the surface lies / / Let the coals burn bright / Ance… |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random07 | Beware of false trees! Chiming icicles and creaking branches might not just be moving in the wind... |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random08 | Signe raised this stone in memory of Eigil, who fell in battle. He had yet to learn that not everything here is as frozen as it first seems. |
| RuneStone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random09 | Buried beneath the snow, deep within the north, / bonds harden like frozen earth / as truth begins to thaw. |
| RuneStone_DragonQueen | RuneStone_DragonQueen | $lore_dragonqueen | SACRIFICE HER SPAWN |
| RuneStone_Drake | RuneStone_Drake | $lore_drake | Let you who read me be aware of the Frost wyrms, one of the most ancient kins sprung from Ymir's body. / / The most common form of the wyrm are the D… |
| RuneStone_Draugr | RuneStone_Draugr | $lore_draugr | Long ages ago, the world of Valheim was home to a race of proud and noble people. They built great towers which touched the clouds and delved deep in… |
| RuneStone_GDKing | RuneStone_GDKing | $lore_gdking | BURN THEIR YOUNG |
| RuneStone_Greydwarfs | RuneStone_Greydwarfs | $lore_greydwarfs | Let all who read me beware of the Greydwarfs, the skulkers in darkness, the soulless ones. They are born from rot and rainfall, they spring like mush… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random01 | Where the grass grows underfoot / / And the sky is blue overhead / / There will always be a hearth and a home / |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random02 | Long ages past, when the Allfather Oden united the worlds, he threw down the Vanir, the giants and those creatures older than any others. The greates… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random03 | Give thanks to Frey for the rain and sun / / For the shoots that break the earth's skin / / And the fruits of the vine / / Give thanks to Oden for th… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random04 | Hold, traveller, and bear witness to my warning. We are many who have come before you, carried here by Oden's will to do his work. The path ahead is… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random05 | Heed these words of Ulf, a poor settler in a strange land. You will find here good stone and wood, all you need to build a house. You will need to cr… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random06 | I was Astrid, a shieldmaiden of the forest. I know nothing of my life before I came here but my arm remembers the sword and my eyes see the course of… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random07 | Blue-eyed shufflers in muck, the neck are small lizards native to Valheim. Surly and mean-spirited, they will attack on sight and must be destroyed l… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random08 | Sisters and brothers of the shield-wall, lift up your hearts! You are the greatest warriors, chosen by the Allfather himself for your courage and val… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random09 | On this spot a tree fell on my head and I cursed the Gods. So I leave this stone in praise of them, that they might forgive my reckless words. |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random10 | Pause, traveler. You are well come to the last and most lawless of the Ten Worlds. In Valheim, the air is pure, the water deep and clear and the fore… |
| RuneStone_Meadows | RuneStone_Meadows | $lore_meadows_random11 | A full belly / / A full sail / / The weight of a spear to your hand / / And a song on your lips / / Let all the mountains crumble / / And the seas bo… |
| RuneStone_Memorial1 | RuneStone_Memorial1 | $lore_deepnorth_memorial1 | The mighty ones who came before us rest here. / Tread warily, as their slumber may yet be disturbed. |
| RuneStone_Memorial1 | RuneStone_Memorial1 | $lore_deepnorth_memorial2 | Death comes for all, in the end, even the mightiest of warriors. / One day you may be counted amongst them. |
| RuneStone_Memorial1 | RuneStone_Memorial1 | $lore_deepnorth_memorial3 | Let us honour our ancestors, who carved this world that we now live in. / And may they deem us worthy. |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random01 | Here lie the Jotunn, most ancient of all Oden's kin and fiercest of all his adversaries. In life they were bringers of ruin but now, in death, they n… |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random02 | Where the air is thick with magic / And the earth is quick with life, / The mist breeds wonders. |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random03 | Trust nothing in the mist. I passed through here in a group of seven people but every time we stopped to count, we counted eight. We do not know who… |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random04 | The Dvergr are the descendants of the great smiths of old, delvers in the deep earth, seekers of hidden treasure. / / Here in Valheim they mine the b… |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random05 | The raven showed me how to make a staff that set my beard on fire. I will meddle no more with such things. / / Weary of mist and magic, Ulf carved th… |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random06 | Heed the words of Ulf and do not take the hats of the short ones from their heads. They have no sense of a jest and the only cure for insulting them… |
| RuneStone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random07 | Wanderers in fog, / Where do you go? / Not knowing what you were, / Nor seeing where you go. |
| RuneStone_Mistlands_bosshint | RuneStone_Mistlands_bosshint | $loretext_mistlands_bosshint |  |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_fenring | Watch for him in moonlight / / Haunter of the night. / / Soft of foot / / Sharp of tooth / / Slow to stalk / / Quick to bite. |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random01 | Few of us found our way to these mountains. We were twenty and now we are two. Agda is dying, a Draugr arrow lies near her heart. I will bid her fare… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random02 | This marks the spot where the great drake was first seen by me, Ulf, in the third summer of my life in Valheim. She stopped here to leave a pile of d… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random03 | Halt and listen, traveller. On the highest peaks of Valheim, the air is thin and fragile. From here you can sometimes catch sounds from other places,… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random04 | Where this stands I once saw the great drake flying above me and I hid in a bush until she passed. Ulf the Brave carved this stone. |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random05 | Let those who read me know not to tarry on these slopes, far from their hearth and the safety of the greenwood. The beasts of the mountains are fell… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random06 | Great cities do not rise of themselves / / Harden your heart, settler in a strange land / / Build from the ground upwards |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random07 | This stone was placed by me, Astrid, in my seventh year in Valheim. At this spot, the Allfather spoke to me. I awoke from a deep sleep to find his wo… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random08 | When first I awoke in Valheim, I pleaded with Oden to show himself to me. Where was he? Why had he abandoned me? / / For many moons I braved storms a… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random09 | Upon this spot, Otho and Bjorn fought a mighty duel to decide who has the finest beard. Now I, Bjorn, must carve this stone to say that the beard of… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random10 | In Midgard of old, the armies of man pushed back the drakes from the mountains just as the Vanir threw down their mother and cast her into Valheim. B… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random11 | In these mountains I cannot throw a spear without hitting something that wants to kill me. But from the heights I have seen sunny plains where life w… |
| RuneStone_Mountains | RuneStone_Mountains | $lore_mountains_random12 | There are friends in Valheim but you must seek them amongst your enemies. Old friends, fire-kin, moon-singers. For centuries they have hunted alongsi… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random01 | Where no rain falls / / And no crops will grow / / Still the ground can give up treasures. |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random02 | This place was too hot for Ulf, a man used to brushing snow from his beard. He carved this stone and moved on. |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random03 | Still your mind, traveller. Oden speaks to those who listen. Here in this barren land, one of the Forsaken Ones dwells. He is an ancient sorcerer twi… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random04 | In a land far from here I once saw a star come unfastened from the great curtain of night and fall into the sand. It glowed red when I approached but… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random05 | Good friend, lay your hand on this stone and remember Harald, who carved it. In Midgard I lost my life on the battlefield but in Valheim it was resto… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random06 | In my dream, Oden came to me as an old man leaning on a stick, a wide-brimmed traveller’s hat on his head. He told me to trust the ravens who carry h… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random07 | Beneath the ground are the halls of men and women long since gone, ancient tribes who lived in Valheim even before the Allfather turned his eye upon… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random08 | From the mountaintop, this plains looked peaceful and pleasant. But I have found it worse than anywhere else. I always hear the buzzing of those thri… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random09 | What joy it is to roam abroad / / With the wind in your hair / / And a blue sky before you. / / / And what joy it is / / To stand tall against your f… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random10 | On these plains, only the greatest survive to read my words and heed my advice. So heed me now. / / Great Oden is not a loving father, kind and gentl… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random11 | Here on the plains you will find the dwellings of the Fuling, that ancient race who once built towers and cities to rival those of men until Oden pun… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random12 | The lox are mighty creatures, great earth-shakers who roamed the plains of Valheim long before the Forsaken were banished to this realm. They are qui… |
| RuneStone_Plains | RuneStone_Plains | $lore_plains_random13 | Know, traveler, that while you cannot die in the world of Valheim, yet you can cease to be reborn. Many are those who have come before you to work th… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random01 | Heed the words of poor Ulf and do not build your house beside the murky waters. Bad dreams and a soggy bed are all you will find. I leave this stone… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random02 | Linger not, traveller. The air is pestilent and the water poison. The Draugr walk here and the thing I will not name stirs below the surface, a bitte… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random03 | In this gloomy region you may yet find something which shines. War-flesh, warrior's gold, bread of the forge... Bright iron is here for those who wil… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random04 | You who pass, remember me. I am a man whose home was once in the mountains of Midgard, carried here when I thought to earn my rest, to find a life af… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random05 | Beware the surtlings, embers of a great fire long ago stamped to ashes. They are drawn to the swamp in numbers but their lights can be seen from afar… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random06 | In centuries past, the Draugr walked these lands just as you do now. Pity them, caught between the living and the dead in a shadow of the world they… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random07 | Below the mist and murk / / Bone speaks to bone / / Remembering flesh. |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random08 | For long ages, Oden's eye was turned from Valheim. Yet while the Gods ignored it, other creatures crept or fell through cracks into the forgotten wor… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random09 | At this place I killed seven of the draugr and ended their long years of fighting and misery. Now who will end mine? |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random10 | I am Gudrun, no man's wife, no father's daughter. Only my name remains to me. But sometimes when I wake I feel the weight of a babe at my breast and… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random11 | Fear not that the Gods have abandoned you. Valheim drifts from the world tree and the Vanir cannot come in arms to this world but still they watch fr… |
| RuneStone_Swamps | RuneStone_Swamps | $lore_swamp_random12 | There is no death that does not bring new life. As worms feed on the battlefield, so did the great corpses of Oden's enemies bring new life to Valhei… |
| RuneStone_UpgradeStation | RuneStone_UpgradeStation | $lore_upgradestation_description | Ingrid and Torgunn decided to honour the gods in this place. They smiled upon Ingrid, granting her a great boon. Torgunn on the other hand, was made… |
| RuneStone_Windingtunnels | RuneStone_Windingtunnels | $lore_windingtunnels | The glowing creatures are an excellent source of light in the dark. / Perhaps a wooden cage might serve to keep them in place. |
| RuneTablet_Eikthyr | RuneTablet_Eikthyr | $lore_eikthyr | HUNT HIS KIN |
| RuneTablet_Fader | RuneTablet_Fader | $lore_fader | RING HIS PRAISE |
| RuneTablet_FrozenKing | RuneTablet_FrozenKing | $lore_frozenking | HALT THE INVASION |
| RuneTablet_GoblinKing | RuneTablet_GoblinKing | $lore_goblinking | AND HIS DYING SOUL WAS SPLIT AND SHARED AMONG ALL HIS KIN |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random01 | Know, child unborn, that this was once the greatest kingdom in all Valheim. Here lived the Sons and Daughters of the King of the Emerald Flame. Benea… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random02 | In the forests of the far North, our scouts came across a sleeping boy, sitting naked in the deep snow with his back to a tree. They wrapped him in f… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random03 | It was known throughout the land that the two would often sit together as if in talk, the king coiled about the sleeping child with his face close, s… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random04 | When the King of the Emerald Flame flew away to visit the wizard king of the Fulings, his own thegns rose against him and torched the palace with the… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random05 | When the king saw the charred remains of his great hall, he swore to show his foes a fire far greater than the little spark they had kindled. With hi… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random06 | The stones tell of a great tragedy in this land, many centuries ago. It is hard to believe the creature who stalks this ruined city was once a noble… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random07 | I have not been happy for more than a day in any place since I came here but this place is the worst of all. Most things are on fire and the fishing… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random08 | The pure fire of a grand drake has the power to give life and quicken the dead earth. Once, they blessed the land and seas with their cleansing flame… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random09 | If the tales are true, the gods long ago abandoned this world. The King of the Emerald Flame was the closest to a god we knew but our prayers to him… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random10 | It is folly for the free folk to say they need no gods. Nobody needs gods and the gods need no one. They owe nothing to men. / / But still the wise w… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random11 | War is a fire / And men are fuel. / / When the heart-blaze kindles / Their bodies are dry wood. |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random12 | I am Astrid of the Long Arm. I came here after much hardship to fight the beast and break the curse that keeps me here. The raven has returned a memo… |
| Runestone_Ashlands | RuneStone_Ashlands | $lore_ashlands_random13 | I have watched many warriors throw themselves against the beast again and again, losing a little of their will each time until their beds lie empty.… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random01 | Beware the deep trees, beware the true dark. When the night comes, keep close to your fire. |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random02 | Beneath the ground, the roots of the forest twine together on a great loom. Pluck one thread and the whole weave will move. Chop down one tree and al… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random03 | I was Harald, a man from the coast. I remember nothing more of my life in Midgard except that I was a warrior. In my dreams I see the faces of those… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random04 | Rest awhile and remember Ulf, who carved this stone with his own hand but could think of nothing to say. |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random05 | Beware the Old One. The ravens say that in ages past he was a shoot of great Yggdrasil itself and a force of wisdom in the days when men and trees we… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random06 | We who were carried here by the Valkyrie are not the first men in this land. I have seen with my own eyes the halls they made beneath the ground and… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random07 | Eight were the creatures banished to this world by mighty Oden in the first days of his kingship. Eight Gods and monsters too proud to bear his yoke.… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random08 | The trolls of Midgard may be fading from your memory, as indeed they are from all the race of man. Few and forlorn, they crouch in damp caves and gna… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random09 | Raised by the Old Man of the Forest from seeds of sin, the greydwarfs clothe themselves in the human forms they once knew, but there is no longer any… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random10 | Which among you was a murderer, a kin-slayer, a renegade? Who turned a blade in their brother's back? Who tore babes from their mothers' arms, set fi… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random11 | Look to the sky where mighty Yggdrasil reaches out, called by the Forsaken Ones to join itself once more with this wayward world. It is a glorious si… |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random12 | Astrid will not look at me twice. I leave this stone to honour great Freya. O goddess, grant me a beard like Bjorn's that I might win her heart! |
| Runestone_BlackForest | RuneStone_BlackForest | $lore_blackforest_random13 | I have been reborn many times in this world. I have died again and again only to awaken in my own house. Yet still I wonder how many more times I wil… |
| Runestone_Boars | RuneStone_Boars | $lore_meadows_boartaming | This land is hard and wild but we who are brought here are harder still. Take comfort, traveller, in the gifts before you, the good wood and stone, t… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor01 | The Deep North. The final obstacle before I can prove myself worthy. / / The trials have been many, and so have the deaths. I have lost friends dear… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor02 | It seems peaceful enough. So far, the wildlife doesn’t appear to care much for my presence here. / / I should be able to gather some valuable resourc… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor03 | I have to be on my guard. Few places have been peaceful since I first arrived here, and it would be foolish to believe these lands to be different. /… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor04 | So far I’ve been left alone. Despite that, I can’t shake the feeling that something is watching me. |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor05 | I found an old building, but it was deserted. It’s hard to say for how long, but I don’t believe I’ll find any friends here. |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor06 | I don’t like to think about what could have happened, had my axe not been as sharp nor my senses as keen. After all, there is no one left to come to… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor07 | It’s strange. I can’t help but feel like there should be people here, just waiting to welcome me. / / I think the winters looked like this when I was… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor08 | I have been haunted by dreams lately, in which I am a raven. On great, black wings I soar across this world that I’ve come to know, but it is not the… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_hervor09 | I know now where I need to go. The Ancestors have pointed me in the direction of the one who rules these lands, the one they tried to keep in chains… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random01 | At first this seemed a good home for Ulf, who liked the cold and the snow. Then came the mean little creatures, intent on dragging me below, and I wa… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random02 | If what the raven says is true, there is only one more forsaken I must slay. But what comes after? Will my son truly be returned to me, or I to him?… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random03 | These lands seemed so familiar, awakening memories buried deep within our minds. We should have known they were not all that they seemed. |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random04 | Heed the words of Kata, who once thought the trolls of the forest were mighty foes. Their kin in these lands are tall as the tallest of towers, and h… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random05 | When we first arrived here, this place was rich and prosperous. But the winters are colder and longer now, and we fear they might soon take over alto… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random06 | In the farthest north / Where magic dances in the skies / Offer to call forth / What just below the surface lies / / Let the coals burn bright / Ance… |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random07 | Beware of false trees! Chiming icicles and creaking branches might not just be moving in the wind... |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random08 | Signe raised this stone in memory of Eigil, who fell in battle. He had yet to learn that not everything here is as frozen as it first seems. |
| Runestone_DeepNorth | RuneStone_DeepNorth | $lore_deepnorth_random09 | Buried beneath the snow, deep within the north, / bonds harden like frozen earth / as truth begins to thaw. |
| Runestone_Draugr | RuneStone_Draugr | $lore_draugr | Long ages ago, the world of Valheim was home to a race of proud and noble people. They built great towers which touched the clouds and delved deep in… |
| Runestone_Greydwarfs | RuneStone_Greydwarfs | $lore_greydwarfs | Let all who read me beware of the Greydwarfs, the skulkers in darkness, the soulless ones. They are born from rot and rainfall, they spring like mush… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random01 | Where the grass grows underfoot / / And the sky is blue overhead / / There will always be a hearth and a home / |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random02 | Long ages past, when the Allfather Oden united the worlds, he threw down the Vanir, the giants and those creatures older than any others. The greates… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random03 | Give thanks to Frey for the rain and sun / / For the shoots that break the earth's skin / / And the fruits of the vine / / Give thanks to Oden for th… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random04 | Hold, traveller, and bear witness to my warning. We are many who have come before you, carried here by Oden's will to do his work. The path ahead is… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random05 | Heed these words of Ulf, a poor settler in a strange land. You will find here good stone and wood, all you need to build a house. You will need to cr… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random06 | I was Astrid, a shieldmaiden of the forest. I know nothing of my life before I came here but my arm remembers the sword and my eyes see the course of… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random07 | Blue-eyed shufflers in muck, the neck are small lizards native to Valheim. Surly and mean-spirited, they will attack on sight and must be destroyed l… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random08 | Sisters and brothers of the shield-wall, lift up your hearts! You are the greatest warriors, chosen by the Allfather himself for your courage and val… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random09 | On this spot a tree fell on my head and I cursed the Gods. So I leave this stone in praise of them, that they might forgive my reckless words. |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random10 | Pause, traveler. You are well come to the last and most lawless of the Ten Worlds. In Valheim, the air is pure, the water deep and clear and the fore… |
| Runestone_Meadows | RuneStone_Meadows | $lore_meadows_random11 | A full belly / / A full sail / / The weight of a spear to your hand / / And a song on your lips / / Let all the mountains crumble / / And the seas bo… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random01 | Here lie the Jotunn, most ancient of all Oden's kin and fiercest of all his adversaries. In life they were bringers of ruin but now, in death, they n… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random02 | Where the air is thick with magic / And the earth is quick with life, / The mist breeds wonders. |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random03 | Trust nothing in the mist. I passed through here in a group of seven people but every time we stopped to count, we counted eight. We do not know who… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random04 | The Dvergr are the descendants of the great smiths of old, delvers in the deep earth, seekers of hidden treasure. / / Here in Valheim they mine the b… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random05 | The raven showed me how to make a staff that set my beard on fire. I will meddle no more with such things. / / Weary of mist and magic, Ulf carved th… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random06 | Heed the words of Ulf and do not take the hats of the short ones from their heads. They have no sense of a jest and the only cure for insulting them… |
| Runestone_Mistlands | RuneStone_Mistlands | $lore_mistlands_random07 | Wanderers in fog, / Where do you go? / Not knowing what you were, / Nor seeing where you go. |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_fenring | Watch for him in moonlight / / Haunter of the night. / / Soft of foot / / Sharp of tooth / / Slow to stalk / / Quick to bite. |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random01 | Few of us found our way to these mountains. We were twenty and now we are two. Agda is dying, a Draugr arrow lies near her heart. I will bid her fare… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random02 | This marks the spot where the great drake was first seen by me, Ulf, in the third summer of my life in Valheim. She stopped here to leave a pile of d… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random03 | Halt and listen, traveller. On the highest peaks of Valheim, the air is thin and fragile. From here you can sometimes catch sounds from other places,… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random04 | Where this stands I once saw the great drake flying above me and I hid in a bush until she passed. Ulf the Brave carved this stone. |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random05 | Let those who read me know not to tarry on these slopes, far from their hearth and the safety of the greenwood. The beasts of the mountains are fell… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random06 | Great cities do not rise of themselves / / Harden your heart, settler in a strange land / / Build from the ground upwards |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random07 | This stone was placed by me, Astrid, in my seventh year in Valheim. At this spot, the Allfather spoke to me. I awoke from a deep sleep to find his wo… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random08 | When first I awoke in Valheim, I pleaded with Oden to show himself to me. Where was he? Why had he abandoned me? / / For many moons I braved storms a… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random09 | Upon this spot, Otho and Bjorn fought a mighty duel to decide who has the finest beard. Now I, Bjorn, must carve this stone to say that the beard of… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random10 | In Midgard of old, the armies of man pushed back the drakes from the mountains just as the Vanir threw down their mother and cast her into Valheim. B… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random11 | In these mountains I cannot throw a spear without hitting something that wants to kill me. But from the heights I have seen sunny plains where life w… |
| Runestone_Mountains | RuneStone_Mountains | $lore_mountains_random12 | There are friends in Valheim but you must seek them amongst your enemies. Old friends, fire-kin, moon-singers. For centuries they have hunted alongsi… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random01 | Where no rain falls / / And no crops will grow / / Still the ground can give up treasures. |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random02 | This place was too hot for Ulf, a man used to brushing snow from his beard. He carved this stone and moved on. |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random03 | Still your mind, traveller. Oden speaks to those who listen. Here in this barren land, one of the Forsaken Ones dwells. He is an ancient sorcerer twi… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random04 | In a land far from here I once saw a star come unfastened from the great curtain of night and fall into the sand. It glowed red when I approached but… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random05 | Good friend, lay your hand on this stone and remember Harald, who carved it. In Midgard I lost my life on the battlefield but in Valheim it was resto… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random06 | In my dream, Oden came to me as an old man leaning on a stick, a wide-brimmed traveller’s hat on his head. He told me to trust the ravens who carry h… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random07 | Beneath the ground are the halls of men and women long since gone, ancient tribes who lived in Valheim even before the Allfather turned his eye upon… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random08 | From the mountaintop, this plains looked peaceful and pleasant. But I have found it worse than anywhere else. I always hear the buzzing of those thri… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random09 | What joy it is to roam abroad / / With the wind in your hair / / And a blue sky before you. / / / And what joy it is / / To stand tall against your f… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random10 | On these plains, only the greatest survive to read my words and heed my advice. So heed me now. / / Great Oden is not a loving father, kind and gentl… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random11 | Here on the plains you will find the dwellings of the Fuling, that ancient race who once built towers and cities to rival those of men until Oden pun… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random12 | The lox are mighty creatures, great earth-shakers who roamed the plains of Valheim long before the Forsaken were banished to this realm. They are qui… |
| Runestone_Plains | RuneStone_Plains | $lore_plains_random13 | Know, traveler, that while you cannot die in the world of Valheim, yet you can cease to be reborn. Many are those who have come before you to work th… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random01 | Heed the words of poor Ulf and do not build your house beside the murky waters. Bad dreams and a soggy bed are all you will find. I leave this stone… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random02 | Linger not, traveller. The air is pestilent and the water poison. The Draugr walk here and the thing I will not name stirs below the surface, a bitte… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random03 | In this gloomy region you may yet find something which shines. War-flesh, warrior's gold, bread of the forge... Bright iron is here for those who wil… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random04 | You who pass, remember me. I am a man whose home was once in the mountains of Midgard, carried here when I thought to earn my rest, to find a life af… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random05 | Beware the surtlings, embers of a great fire long ago stamped to ashes. They are drawn to the swamp in numbers but their lights can be seen from afar… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random06 | In centuries past, the Draugr walked these lands just as you do now. Pity them, caught between the living and the dead in a shadow of the world they… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random07 | Below the mist and murk / / Bone speaks to bone / / Remembering flesh. |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random08 | For long ages, Oden's eye was turned from Valheim. Yet while the Gods ignored it, other creatures crept or fell through cracks into the forgotten wor… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random09 | At this place I killed seven of the draugr and ended their long years of fighting and misery. Now who will end mine? |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random10 | I am Gudrun, no man's wife, no father's daughter. Only my name remains to me. But sometimes when I wake I feel the weight of a babe at my breast and… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random11 | Fear not that the Gods have abandoned you. Valheim drifts from the world tree and the Vanir cannot come in arms to this world but still they watch fr… |
| Runestone_Swamps | RuneStone_Swamps | $lore_swamp_random12 | There is no death that does not bring new life. As worms feed on the battlefield, so did the great corpses of Oden's enemies bring new life to Valhei… |
| StartTemple | BossStone_Bonemass | $guardianstone_bonemass_desc | Wanderer, look to your feet / That tread upon our tomb / One thousand bones without their meat / Will drag you to your doom |
| StartTemple | BossStone_DragonQueen | $guardianstone_moder_desc | Black wings across the moon and sun / Down from the mountain our mother comes / Her weeping tears will fall like rain / Her voice will call us home a… |
| StartTemple | BossStone_Eikthyr | $guardianstone_eikthyr_desc | His antlers are branches of iron / They crack the rocks and bring down mountains / His hooves are the sound of thunder / His voice a howling gale |
| StartTemple | BossStone_TheElder | $guardianstone_theelder_desc | First of the Forest, King-in-the-Wood / Lord over those who dwell at his feet / His roots will grow where cities once stood / Their blood his wine, t… |
| StartTemple | BossStone_Yagluth | $guardianstone_yagluth_desc | Long ages past, he wore a crown / Beneath a blood-red sky / Now naught is left of all he was / But his spirit cannot die |
| cave_dome_bottom_lake | RuneStone_CaveMan | $lore_caveman01 | The depths of the cave promised me riches. / Down turns out, is easier than up. / The lake helped best it could, but it could only do so much. / It t… |
| cave_dome_middle | RuneStone_CaveMan (1) | $lore_caveman02 | This cliff I climbed in in hopes of reaching the top. / Regretfully the cave was too deep. / ... / Back here again considering the jump. / Alas, the… |
| hole_corridors04_alt5 | RuneStone_Windingtunnels | $lore_windingtunnels | The glowing creatures are an excellent source of light in the dark. / Perhaps a wooden cage might serve to keep them in place. |
| hole_corridors04_alt6 | RuneStone_Windingtunnels | $lore_windingtunnels | The glowing creatures are an excellent source of light in the dark. / Perhaps a wooden cage might serve to keep them in place. |

All `lore_*` keys (some are only in the table, not placed):

| token | English (start) |
|---|---|
| lore_ashlands_random01 | Know, child unborn, that this was once the greatest kingdom in all Valheim. Here lived the Sons and Daughters of the King of the Emerald Flame. Beneath his wing, we knew… |
| lore_ashlands_random02 | In the forests of the far North, our scouts came across a sleeping boy, sitting naked in the deep snow with his back to a tree. They wrapped him in furs and brought him… |
| lore_ashlands_random03 | It was known throughout the land that the two would often sit together as if in talk, the king coiled about the sleeping child with his face close, straining to catch an… |
| lore_ashlands_random04 | When the King of the Emerald Flame flew away to visit the wizard king of the Fulings, his own thegns rose against him and torched the palace with the Winter Child inside… |
| lore_ashlands_random05 | When the king saw the charred remains of his great hall, he swore to show his foes a fire far greater than the little spark they had kindled. With his breath, he razed o… |
| lore_ashlands_random06 | The stones tell of a great tragedy in this land, many centuries ago. It is hard to believe the creature who stalks this ruined city was once a noble beast and a wise kin… |
| lore_ashlands_random07 | I have not been happy for more than a day in any place since I came here but this place is the worst of all. Most things are on fire and the fishing is not good. These a… |
| lore_ashlands_random08 | The pure fire of a grand drake has the power to give life and quicken the dead earth. Once, they blessed the land and seas with their cleansing flame, bringing new life… |
| lore_ashlands_random09 | If the tales are true, the gods long ago abandoned this world. The King of the Emerald Flame was the closest to a god we knew but our prayers to him have turned to ashes… |
| lore_ashlands_random10 | It is folly for the free folk to say they need no gods. Nobody needs gods and the gods need no one. They owe nothing to men. / / But still the wise will seek the powerfu… |
| lore_ashlands_random11 | War is a fire / And men are fuel. / / When the heart-blaze kindles / Their bodies are dry wood. |
| lore_ashlands_random12 | I am Astrid of the Long Arm. I came here after much hardship to fight the beast and break the curse that keeps me here. The raven has returned a memory to me and it will… |
| lore_ashlands_random13 | I have watched many warriors throw themselves against the beast again and again, losing a little of their will each time until their beds lie empty. If this is my fate,… |
| lore_blackforest_random01 | Beware the deep trees, beware the true dark. When the night comes, keep close to your fire. |
| lore_blackforest_random02 | Beneath the ground, the roots of the forest twine together on a great loom. Pluck one thread and the whole weave will move. Chop down one tree and all the wood will know… |
| lore_blackforest_random03 | I was Harald, a man from the coast. I remember nothing more of my life in Midgard except that I was a warrior. In my dreams I see the faces of those I killed. I leave th… |
| lore_blackforest_random04 | Rest awhile and remember Ulf, who carved this stone with his own hand but could think of nothing to say. |
| lore_blackforest_random05 | Beware the Old One. The ravens say that in ages past he was a shoot of great Yggdrasil itself and a force of wisdom in the days when men and trees were friends. Now he s… |
| lore_blackforest_random06 | We who were carried here by the Valkyrie are not the first men in this land. I have seen with my own eyes the halls they made beneath the ground and the ruins of their t… |
| lore_blackforest_random07 | Eight were the creatures banished to this world by mighty Oden in the first days of his kingship. Eight Gods and monsters too proud to bear his yoke. I have heard the tr… |
| lore_blackforest_random08 | The trolls of Midgard may be fading from your memory, as indeed they are from all the race of man. Few and forlorn, they crouch in damp caves and gnaw on the bones of th… |
| lore_blackforest_random09 | Raised by the Old Man of the Forest from seeds of sin, the greydwarfs clothe themselves in the human forms they once knew, but there is no longer any warmth in their hea… |
| lore_blackforest_random10 | Which among you was a murderer, a kin-slayer, a renegade? Who turned a blade in their brother's back? Who tore babes from their mothers' arms, set fire to the houses of… |
| lore_blackforest_random11 | Look to the sky where mighty Yggdrasil reaches out, called by the Forsaken Ones to join itself once more with this wayward world. It is a glorious sight but a dreadful o… |
| lore_blackforest_random12 | Astrid will not look at me twice. I leave this stone to honour great Freya. O goddess, grant me a beard like Bjorn's that I might win her heart! |
| lore_blackforest_random13 | I have been reborn many times in this world. I have died again and again only to awaken in my own house. Yet still I wonder how many more times I will return like this.… |
| lore_bonemass | COOK THEIR REMAINS |
| lore_caveman01 | The depths of the cave promised me riches. / Down turns out, is easier than up. / The lake helped best it could, but it could only do so much. / It took me strength to n… |
| lore_caveman02 | This cliff I climbed in in hopes of reaching the top. / Regretfully the cave was too deep. / ... / Back here again considering the jump. / Alas, the coward I am is climb… |
| lore_cavepainting_label | Ancient Cave Markings |
| lore_deepnorth_hervor01 | The Deep North. The final obstacle before I can prove myself worthy. / / The trials have been many, and so have the deaths. I have lost friends dear to me. / / But at la… |
| lore_deepnorth_hervor02 | It seems peaceful enough. So far, the wildlife doesn’t appear to care much for my presence here. / / I should be able to gather some valuable resources from them if I ne… |
| lore_deepnorth_hervor03 | I have to be on my guard. Few places have been peaceful since I first arrived here, and it would be foolish to believe these lands to be different. / / No matter how wel… |
| lore_deepnorth_hervor04 | So far I’ve been left alone. Despite that, I can’t shake the feeling that something is watching me. |
| lore_deepnorth_hervor05 | I found an old building, but it was deserted. It’s hard to say for how long, but I don’t believe I’ll find any friends here. |
| lore_deepnorth_hervor06 | I don’t like to think about what could have happened, had my axe not been as sharp nor my senses as keen. After all, there is no one left to come to my aid. / / I may ha… |
| lore_deepnorth_hervor07 | It’s strange. I can’t help but feel like there should be people here, just waiting to welcome me. / / I think the winters looked like this when I was a child, and any mo… |
| lore_deepnorth_hervor08 | I have been haunted by dreams lately, in which I am a raven. On great, black wings I soar across this world that I’ve come to know, but it is not the same. / / The lands… |
| lore_deepnorth_hervor09 | I know now where I need to go. The Ancestors have pointed me in the direction of the one who rules these lands, the one they tried to keep in chains but whose power now… |
| lore_deepnorth_memorial1 | The mighty ones who came before us rest here. / Tread warily, as their slumber may yet be disturbed. |
| lore_deepnorth_memorial2 | Death comes for all, in the end, even the mightiest of warriors. / One day you may be counted amongst them. |
| lore_deepnorth_memorial3 | Let us honour our ancestors, who carved this world that we now live in. / And may they deem us worthy. |
| lore_deepnorth_memorial_description | Ancestral Memorial |
| lore_deepnorth_memorial_label | Lore: Ancestral Memorial |
| lore_deepnorth_random01 | At first this seemed a good home for Ulf, who liked the cold and the snow. Then came the mean little creatures, intent on dragging me below, and I was forced to reconsid… |
| lore_deepnorth_random02 | If what the raven says is true, there is only one more forsaken I must slay. But what comes after? Will my son truly be returned to me, or I to him? / / Astrid raised th… |
| lore_deepnorth_random03 | These lands seemed so familiar, awakening memories buried deep within our minds. We should have known they were not all that they seemed. |
| lore_deepnorth_random04 | Heed the words of Kata, who once thought the trolls of the forest were mighty foes. Their kin in these lands are tall as the tallest of towers, and harder than the harde… |
| lore_deepnorth_random05 | When we first arrived here, this place was rich and prosperous. But the winters are colder and longer now, and we fear they might soon take over altogether. / / Yet, her… |
| lore_deepnorth_random06 | In the farthest north / Where magic dances in the skies / Offer to call forth / What just below the surface lies / / Let the coals burn bright / Ancestral wisdom waits f… |
| lore_deepnorth_random07 | Beware of false trees! Chiming icicles and creaking branches might not just be moving in the wind... |
| lore_deepnorth_random08 | Signe raised this stone in memory of Eigil, who fell in battle. He had yet to learn that not everything here is as frozen as it first seems. |
| lore_deepnorth_random09 | Buried beneath the snow, deep within the north, / bonds harden like frozen earth / as truth begins to thaw. |
| lore_dragonqueen | SACRIFICE HER SPAWN |
| lore_drake | Let you who read me be aware of the Frost wyrms, one of the most ancient kins sprung from Ymir's body. / / The most common form of the wyrm are the Drakes, the small mal… |
| lore_drake_label | Lore: Drake |
| lore_draugr | Long ages ago, the world of Valheim was home to a race of proud and noble people. They built great towers which touched the clouds and delved deep into the earth for pre… |
| lore_draugr_label | Lore: Draugr |
| lore_dvergr_label | Munin: Dvergr |
| lore_dvergr_text | Take care, warrior. You've happened upon an outpost of the forlorn Dvergr clans, long since separated from their kin in Nidavellir. Trapped here they still toil, expecti… |
| lore_dvergrhalls_label | Munin: Dvergr homes |
| lore_dvergrhalls_text | These are the Dvergrhomes, built long ago in a gilded age... Their splendour rivalled the Golden Hall itself! / / Regrettably, nothing can last forever. The halls below… |
| lore_eikthyr | HUNT HIS KIN |
| lore_fader | RING HIS PRAISE |
| lore_frozenking | HALT THE INVASION |
| lore_gdking | BURN THEIR YOUNG |
| lore_giants_label | Munin: Giant remains |
| lore_giants_text | Shadows of an ancient age. The Jotunn once ruled the tenth world, until their time ran out and they were ousted by some other power. |
| lore_goblinking | AND HIS DYING SOUL WAS SPLIT AND SHARED AMONG ALL HIS KIN |
| lore_greydwarfs | Let all who read me beware of the Greydwarfs, the skulkers in darkness, the soulless ones. They are born from rot and rainfall, they spring like mushrooms from the smoki… |
| lore_greydwarfs_label | Lore: Greydwarfs |
| lore_intro | Long ago, the Allfather Oden united the worlds. He threw down his foes and cast them into the tenth world, then split the boughs which held their prison to the World-Tre… |
| lore_intro_OLD | To prove you are worthy of entering Valhalla you have been sent to Valheim, the tenth Norse world. Only by defeating the mighty beasts of these lands will you win the fa… |
| lore_meadows_boartaming | This land is hard and wild but we who are brought here are harder still. Take comfort, traveller, in the gifts before you, the good wood and stone, the fruits and flower… |
| lore_meadows_boartaming_label | Lore: Boars |
| lore_meadows_random01 | Where the grass grows underfoot / / And the sky is blue overhead / / There will always be a hearth and a home / |
| lore_meadows_random02 | Long ages past, when the Allfather Oden united the worlds, he threw down the Vanir, the giants and those creatures older than any others. The greatest of them could not… |
| lore_meadows_random03 | Give thanks to Frey for the rain and sun / / For the shoots that break the earth's skin / / And the fruits of the vine / / Give thanks to Oden for the flesh and bone / /… |
| lore_meadows_random04 | Hold, traveller, and bear witness to my warning. We are many who have come before you, carried here by Oden's will to do his work. The path ahead is hard and the dangers… |
| lore_meadows_random05 | Heed these words of Ulf, a poor settler in a strange land. You will find here good stone and wood, all you need to build a house. You will need to craft a roof to keep o… |
| lore_meadows_random06 | I was Astrid, a shieldmaiden of the forest. I know nothing of my life before I came here but my arm remembers the sword and my eyes see the course of the arrow. Now the… |
| lore_meadows_random07 | Blue-eyed shufflers in muck, the neck are small lizards native to Valheim. Surly and mean-spirited, they will attack on sight and must be destroyed like vermin. They sta… |
| lore_meadows_random08 | Sisters and brothers of the shield-wall, lift up your hearts! You are the greatest warriors, chosen by the Allfather himself for your courage and valor. Now death has br… |
| lore_meadows_random09 | On this spot a tree fell on my head and I cursed the Gods. So I leave this stone in praise of them, that they might forgive my reckless words. |
| lore_meadows_random10 | Pause, traveler. You are well come to the last and most lawless of the Ten Worlds. In Valheim, the air is pure, the water deep and clear and the forests overflowing with… |
| lore_meadows_random11 | A full belly / / A full sail / / The weight of a spear to your hand / / And a song on your lips / / Let all the mountains crumble / / And the seas boil to salt / / One d… |
| lore_mistlands_random01 | Here lie the Jotunn, most ancient of all Oden's kin and fiercest of all his adversaries. In life they were bringers of ruin but now, in death, they nurture new growth. /… |
| lore_mistlands_random02 | Where the air is thick with magic / And the earth is quick with life, / The mist breeds wonders. |
| lore_mistlands_random03 | Trust nothing in the mist. I passed through here in a group of seven people but every time we stopped to count, we counted eight. We do not know who the eighth was, but… |
| lore_mistlands_random04 | The Dvergr are the descendants of the great smiths of old, delvers in the deep earth, seekers of hidden treasure. / / Here in Valheim they mine the bones of the Jotunn a… |
| lore_mistlands_random05 | The raven showed me how to make a staff that set my beard on fire. I will meddle no more with such things. / / Weary of mist and magic, Ulf carved this stone. Now I go t… |
| lore_mistlands_random06 | Heed the words of Ulf and do not take the hats of the short ones from their heads. They have no sense of a jest and the only cure for insulting them is to kill them. Als… |
| lore_mistlands_random07 | Wanderers in fog, / Where do you go? / Not knowing what you were, / Nor seeing where you go. |
| lore_mountains_fenring | Watch for him in moonlight / / Haunter of the night. / / Soft of foot / / Sharp of tooth / / Slow to stalk / / Quick to bite. |
| lore_mountains_fenring_label | Lore: Fenring |
| lore_mountains_random01 | Few of us found our way to these mountains. We were twenty and now we are two. Agda is dying, a Draugr arrow lies near her heart. I will bid her farewell beside this sto… |
| lore_mountains_random02 | This marks the spot where the great drake was first seen by me, Ulf, in the third summer of my life in Valheim. She stopped here to leave a pile of dung holding the bone… |
| lore_mountains_random03 | Halt and listen, traveller. On the highest peaks of Valheim, the air is thin and fragile. From here you can sometimes catch sounds from other places, the ring of battle… |
| lore_mountains_random04 | Where this stands I once saw the great drake flying above me and I hid in a bush until she passed. Ulf the Brave carved this stone. |
| lore_mountains_random05 | Let those who read me know not to tarry on these slopes, far from their hearth and the safety of the greenwood. The beasts of the mountains are fell and fierce, hungry f… |
| lore_mountains_random06 | Great cities do not rise of themselves / / Harden your heart, settler in a strange land / / Build from the ground upwards |
| lore_mountains_random07 | This stone was placed by me, Astrid, in my seventh year in Valheim. At this spot, the Allfather spoke to me. I awoke from a deep sleep to find his words scattered around… |
| lore_mountains_random08 | When first I awoke in Valheim, I pleaded with Oden to show himself to me. Where was he? Why had he abandoned me? / / For many moons I braved storms and fierce beasts in… |
| lore_mountains_random09 | Upon this spot, Otho and Bjorn fought a mighty duel to decide who has the finest beard. Now I, Bjorn, must carve this stone to say that the beard of Otho is as bright an… |
| lore_mountains_random10 | In Midgard of old, the armies of man pushed back the drakes from the mountains just as the Vanir threw down their mother and cast her into Valheim. But here they have re… |
| lore_mountains_random11 | In these mountains I cannot throw a spear without hitting something that wants to kill me. But from the heights I have seen sunny plains where life will be easier. I go… |
| lore_mountains_random12 | There are friends in Valheim but you must seek them amongst your enemies. Old friends, fire-kin, moon-singers. For centuries they have hunted alongside you, now you must… |
| lore_munin_ashlands_label | Ashlands awaits... |
| lore_munin_ashlands_text | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and the King st… |
| lore_munin_label | Munin: Introduction |
| lore_munin_text | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. Keep his wa… |
| lore_plains_random01 | Where no rain falls / / And no crops will grow / / Still the ground can give up treasures. |
| lore_plains_random02 | This place was too hot for Ulf, a man used to brushing snow from his beard. He carved this stone and moved on. |
| lore_plains_random03 | Still your mind, traveller. Oden speaks to those who listen. Here in this barren land, one of the Forsaken Ones dwells. He is an ancient sorcerer twisted by bitterness,… |
| lore_plains_random04 | In a land far from here I once saw a star come unfastened from the great curtain of night and fall into the sand. It glowed red when I approached but by morning was cool… |
| lore_plains_random05 | Good friend, lay your hand on this stone and remember Harald, who carved it. In Midgard I lost my life on the battlefield but in Valheim it was restored to me. Yet still… |
| lore_plains_random06 | In my dream, Oden came to me as an old man leaning on a stick, a wide-brimmed traveller’s hat on his head. He told me to trust the ravens who carry his words under their… |
| lore_plains_random07 | Beneath the ground are the halls of men and women long since gone, ancient tribes who lived in Valheim even before the Allfather turned his eye upon this place. Delve de… |
| lore_plains_random08 | From the mountaintop, this plains looked peaceful and pleasant. But I have found it worse than anywhere else. I always hear the buzzing of those thrice-damned insects. M… |
| lore_plains_random09 | What joy it is to roam abroad / / With the wind in your hair / / And a blue sky before you. / / / And what joy it is / / To stand tall against your foes / / And speak pl… |
| lore_plains_random10 | On these plains, only the greatest survive to read my words and heed my advice. So heed me now. / / Great Oden is not a loving father, kind and gentle, speaking honeyed… |
| lore_plains_random11 | Here on the plains you will find the dwellings of the Fuling, that ancient race who once built towers and cities to rival those of men until Oden punished them for their… |
| lore_plains_random12 | The lox are mighty creatures, great earth-shakers who roamed the plains of Valheim long before the Forsaken were banished to this realm. They are quick to anger and will… |
| lore_plains_random13 | Know, traveler, that while you cannot die in the world of Valheim, yet you can cease to be reborn. Many are those who have come before you to work the will of Oden, only… |
| lore_queen | We sealed the door and scattered the key. Leave her be. |
| lore_surtlings | Let you who read me know of the Surtlings, that you might not fall prey to their wickedness. / / Long ago the great demon Surtr was brought down and destroyed by the Fir… |
| lore_surtlings_label | Lore: Surtlings |
| lore_swamp_random01 | Heed the words of poor Ulf and do not build your house beside the murky waters. Bad dreams and a soggy bed are all you will find. I leave this stone as a warning and go… |
| lore_swamp_random02 | Linger not, traveller. The air is pestilent and the water poison. The Draugr walk here and the thing I will not name stirs below the surface, a bitter mass of bone and s… |
| lore_swamp_random03 | In this gloomy region you may yet find something which shines. War-flesh, warrior's gold, bread of the forge... Bright iron is here for those who will take it! |
| lore_swamp_random04 | You who pass, remember me. I am a man whose home was once in the mountains of Midgard, carried here when I thought to earn my rest, to find a life after life in Valheim.… |
| lore_swamp_random05 | Beware the surtlings, embers of a great fire long ago stamped to ashes. They are drawn to the swamp in numbers but their lights can be seen from afar. Keep to the high g… |
| lore_swamp_random06 | In centuries past, the Draugr walked these lands just as you do now. Pity them, caught between the living and the dead in a shadow of the world they once knew. To destro… |
| lore_swamp_random07 | Below the mist and murk / / Bone speaks to bone / / Remembering flesh. |
| lore_swamp_random08 | For long ages, Oden's eye was turned from Valheim. Yet while the Gods ignored it, other creatures crept or fell through cracks into the forgotten world. Trolls, goblins… |
| lore_swamp_random09 | At this place I killed seven of the draugr and ended their long years of fighting and misery. Now who will end mine? |
| lore_swamp_random10 | I am Gudrun, no man's wife, no father's daughter. Only my name remains to me. But sometimes when I wake I feel the weight of a babe at my breast and I cry. Great Freya t… |
| lore_swamp_random11 | Fear not that the Gods have abandoned you. Valheim drifts from the world tree and the Vanir cannot come in arms to this world but still they watch from afar. Keep them e… |
| lore_swamp_random12 | There is no death that does not bring new life. As worms feed on the battlefield, so did the great corpses of Oden's enemies bring new life to Valheim. Lesser creatures… |
| lore_upgradestation_description | Ingrid and Torgunn decided to honour the gods in this place. They smiled upon Ingrid, granting her a great boon. Torgunn on the other hand, was made all too aware of the… |
| lore_upgradestation_label | Lore: Forge of Potential |
| lore_windingtunnels | The glowing creatures are an excellent source of light in the dark. / Perhaps a wooden cage might serve to keep them in place. |
| lore_wraith | Rest, wanderer, and consider the wraiths. / / We know that when a warrior dies, their soul cracks open and seeps into the earth around them, thereby to nourish new life… |
| lore_wraith_label | Lore: Wraiths |

## 7. Status effects and guardian powers

| asset | class | token | English | tooltip | ttl s | cooldown s |
|---|---|---|---|---|---|---|
| AdrenalineRush | SE_Stats | $se_adrenalinerush 1 |  |  | 0.0 | 0.0 |
| AdrenalineRush2 | SE_Stats | $se_adrenalinerush 2 |  |  | 0.0 | 0.0 |
| AdrenalineRush3 | SE_Stats | $se_adrenalinerush 3 |  |  | 0.0 | 0.0 |
| AdrenalineRush4 | SE_Stats | $se_adrenalinerush 4 |  |  | 0.0 | 0.0 |
| BeltStrength | SE_Stats | $item_beltstrength | Megingjord | Increase max carry weight. | 0.0 | 0.0 |
| Burning | SE_Burning | $se_burning_name | Burning | You are on fire! | 5.0 | 0.0 |
| CampFire | StatusEffect | $se_fire_name | Fire | Warm from a cozy fire. | 0.0 | 0.0 |
| Cold | SE_Stats | $se_cold_name | Cold | Lower health and stamina regeneration. | 0.0 | 0.0 |
| CorpseRun | SE_Stats | $se_corpserun_name | Corpse run | You can run longer and take significantly less damage from physical attacks. | 50.0 | 0.0 |
| Crowned | SE_Crowned | $se_crowned_name | Royalty | The creatures of Valheim will not harm the one who has mastered them. | 0.0 | 0.0 |
| Demister | SE_Demister | $item_demister | Wisplight | A bound wisp to guide you through the thickest of mists. | 0.0 | 0.0 |
| Encumbered | StatusEffect | $se_encumbered_name | Encumbered | You are encumbered and cannot run. | 0.0 | 0.0 |
| Freezing | SE_Stats | $se_freezing_name | Freezing | You are freezing. No health regeneration and significantly lowered stamina regeneration. | 0.0 | 0.0 |
| Frost | SE_Frost | $se_frost_name | Frost | You suffer from hypothermia. | 0.0 | 0.0 |
| GoblinShaman_shield | SE_Shield | GD Heal |  |  | 40.0 | 0.0 |
| GP_Bonemass | SE_Stats | $se_bonemass_name | Bonemass | The mass takes the brunt of your blows. The effort required to raise your shield lessens, and you can withstand harder hits. | 300.0 | 1200.0 |
| GP_Eikthyr | SE_Stats | $se_eikthyr_name | Eikthyr | The grace of the mighty stag flows through you. Your ability to move around is improved, be it on land or in water. | 300.0 | 1200.0 |
| GP_Fader | SE_Stats | $se_fader_name | Fader | The Emerald Flame never relents. You shall stand your ground, even when it is aflame, and you shall use all the advantages the battle may o… | 300.0 | 1200.0 |
| GP_Moder | SE_Stats | $se_moder_name | Moder |  | 300.0 | 1200.0 |
| GP_Queen | SE_Stats | $se_queen_name | The Queen | Her Majesty blesses you. In her shadow both stealth and magic may flourish, and no poison shall harm you. | 300.0 | 1200.0 |
| GP_TheElder | SE_Stats | $se_theelder_name | The Elder | The power of the forest invigorates you. You shall gather its bounties faster, just as you shall regain your own vitality should you lose i… | 300.0 | 1200.0 |
| GP_Yagluth | SE_Stats | $se_yagluth_name | Yagluth | The fallen king lends you his might. A king must have the power to vanquish his foes, just as he must be able to support his people. | 300.0 | 1200.0 |
| GrapplingHook | SE_Stats | GrapplingHook |  |  | 0.0 | 0.0 |
| Harpooned | SE_Harpooned | $se_harpooned_name | Harpooned | You have been harpooned. | 0.0 | 0.0 |
| Immobilized | SE_Stats | $se_immobilized | Immobilized | You are stuck | 5.0 | 0.0 |
| ImmobilizedAshlands | SE_Stats | $se_immobilized | Immobilized | You are stuck | 10.0 | 0.0 |
| ImmobilizedLong | SE_Stats | $se_immobilized | Immobilized | You are stuck | 60.0 | 0.0 |
| Lightning | StatusEffect | $se_lightning_name | Lightning | Lightning damage. | 3.0 | 0.0 |
| Poison | SE_Poison | $se_poison_name | Poison | Poison damage. | 0.0 | 0.0 |
| Potion_barleywine | SE_Stats | $item_barleywine | Fire Resistance Barley Wine | You take less damage from burning. | 600.0 | 0.0 |
| Potion_BugRepellent | SE_Stats | $item_mead_bugrepellent | Anti-Sting Concoction | The fresh scent of this potion will keep away certain unwanted companions. | 600.0 | 0.0 |
| Potion_bzerker | SE_Stats | $item_mead_bzerker | Berserkir Mead | For a short while, you fight with the ferocity of a mad and frenzied creature. | 20.0 | 120.0 |
| Potion_eitr_lingering | SE_Stats | $item_mead_eitr_lingering | Lingering Eitr Mead | Eitr regeneration over time. | 300.0 | 0.0 |
| Potion_eitr_minor | SE_Stats | $item_mead_eitr_minor | Minor Eitr Mead | Eitr over time. | 120.0 | 0.0 |
| Potion_frostresist | SE_Stats | $item_mead_frostres | Frost Resistance Mead | You are protected against the cold. | 600.0 | 0.0 |
| Potion_hasty | SE_Stats | $item_mead_hasty | Tonic of Ratatosk | Magic fills you, letting you move quicker than ever before. | 600.0 | 0.0 |
| Potion_health_lingering | SE_Stats | $item_mead_hp_lingering | Lingering Healing Mead | Health regeneration over time. | 300.0 | 0.0 |
| Potion_health_major | SE_Stats | $item_mead_hp_major | Major Healing Mead | Health over time. | 120.0 | 0.0 |
| Potion_health_medium | SE_Stats | $item_mead_hp_medium | Medium Healing Mead | Health over time. | 120.0 | 0.0 |
| Potion_health_minor | SE_Stats | $item_mead_hp_minor | Minor Healing Mead | Health over time. | 120.0 | 0.0 |
| Potion_LightFoot | SE_Stats | $item_mead_lightfoot | Lightfoot Mead | You feel lighter, almost like you could jump all the way to the branches of Yggdrasil itself. | 600.0 | 0.0 |
| Potion_poisonresist | SE_Stats | $item_mead_poisonres | Poison Resistance Mead | You take less damage from poison. | 600.0 | 0.0 |
| Potion_stamina_lingering | SE_Stats | $item_mead_stamina_lingering | Lingering Stamina Mead | Stamina regeneration over time. | 300.0 | 0.0 |
| Potion_stamina_medium | SE_Stats | $item_mead_stamina_medium | Medium Stamina Mead | Regenerate stamina fast. | 120.0 | 0.0 |
| Potion_stamina_minor | SE_Stats | $item_mead_stamina_minor | Minor Stamina Mead | Regenerate stamina fast. | 120.0 | 0.0 |
| Potion_strength | SE_Stats | $item_mead_strength | Mead of Troll Endurance | Incredible strength courses through you, but only for a time... | 300.0 | 120.0 |
| Potion_swimmer | SE_Stats | $item_mead_swimmer | Draught of Vananidir | Bring the power of the sea-god with you as you brave the waves. | 300.0 | 0.0 |
| Potion_tamer | SE_Stats | $item_mead_tamer | Brew of Animal Whispers | Animals will be less reluctant to accept you into their flock. | 600.0 | 0.0 |
| Potion_tasty | SE_Stats | $item_mead_tasty | Tasty Mead | Lower health regeneration, but increased stamina regeneration. | 10.0 | 0.0 |
| Potion_TrollPheromones | SE_Stats | $item_mead_trollpheromones | Love Potion | All is fair in love and war. | 300.0 | 0.0 |
| Puke | SE_Puke | $se_puke_name | Feeling sick | You don't feel so well and can't hold your food down. | 15.0 | 0.0 |
| Rested | SE_Rested | $se_rested_name | Rested | You feel rested. Health and stamina regeneration are higher. | 1.0 | 0.0 |
| Resting | SE_Cozy | $se_resting_name | Resting | You are currently resting. Health and stamina regeneration are significantly higher. | 0.0 | 0.0 |
| SE_Dvergr_buff | SE_Stats | $dvergr_buff | Dvergr power | You are infused with a strange Dvergr power which increases your strength. | 20.0 | 0.0 |
| SE_Dvergr_heal | SE_Stats | Dvergr Heal |  |  | 4.0 | 0.0 |
| SE_Greydwarf_shaman_frozen_heal | SE_Stats | GD Heal |  |  | 4.0 | 0.0 |
| SE_Greydwarf_shaman_heal | SE_Stats | GD Heal |  |  | 4.0 | 0.0 |
| SetEffect_AshlandsMediumArmor | SE_Stats | $se_ashlandsmediumarmorseteffect_name | Ask's Endurance | A lighter armour lets you move more freely, every move requiring less energy. | 0.0 | 0.0 |
| SetEffect_BerserkerArmor | SE_Stats | $se_berserkereffect_name | Berserk | Increases damage and regeneration, but weakens you against physical damage. | 0.0 | 0.0 |
| SetEffect_BerserkerUndeadArmor | SE_Stats | $se_berserker_undead_effect_name | Vilebone Wrath | Increases damage and regeneration, but weakens you against physical damage. | 0.0 | 0.0 |
| SetEffect_DeepNorthMediumArmor | SE_Stats | $se_deepnorthmediumarmorseteffect_name | Vanguard | You feel stronger and faster, ready to strike at your foes! | 0.0 | 0.0 |
| SetEffect_FenringArmor | SE_Stats | $se_fenringseteffect_name | Fenris blessing | The Fenris armour makes you quick on your feet so you can pass through fire, and your fists feel the power of the beast. | 0.0 | 0.0 |
| SetEffect_FishingHat | SE_Stats | $item_helmet_fishinghat | Fishing Hat | Vikings want you, fish fear you. | 0.0 | 0.0 |
| SetEffect_HarvesterArmor | SE_Stats | $se_harvesterseteffect_name | Harvester | Wearing the right clothes makes the chores easier. | 0.0 | 0.0 |
| SetEffect_LoxArmor | SE_Stats | $se_loxseteffect_name | Boon of the Lox | The fur allows you to blend in with the wilderness and move about unseen. | 0.0 | 0.0 |
| SetEffect_MageArmor | SE_Stats | $se_mageseteffect_name | Eitr-infused | You are one with the eitr. (increased regen. + elemental magic skill) | 0.0 | 0.0 |
| SetEffect_RootArmor | SE_Stats | $se_rootseteffect_name | Improved archery | The ancient roots help you focus your bow skill. | 0.0 | 0.0 |
| SetEffect_TrollArmor | SE_Stats | $se_trollseteffect_name | Sneaky | Makes you more sneaky. | 0.0 | 0.0 |
| SetEffect_WolfArmor | SE_Stats | $se_frostres_name | Frost resistance | You are protected against the cold. | 0.0 | 0.0 |
| Shelter | StatusEffect | $se_shelter_name | Shelter | You are sheltered from the weather. | 0.0 | 0.0 |
| Slimed | SE_Stats | $se_slimed_name | Slimed |  | 1.0 | 0.0 |
| SlowFall | SE_Stats | $se_slowfall_name | Feather fall | What is gravity but an effect of entropy? | 0.0 | 0.0 |
| Smoked | SE_Smoke | $se_smoked_name | Smoked | Breathing smoke is unhealthy. | 0.0 | 0.0 |
| SoftDeath | StatusEffect | $se_softdeath_name | No skill drain | If you die you won't lose any skill points. | 600.0 | 0.0 |
| Spirit | SE_Burning | $se_spirit_name | Spirit | Extra damage against the undead. | 3.0 | 0.0 |
| Staff_FrostOrbs | SE_React | $se_frostorbs | Vengeance Sphere | Should your enemy hurt you, it shall reflect back upon them at once. | 120.0 | 0.0 |
| Staff_shield | SE_Shield | $se_shield | Magic barrier | A magical shield that absorbs damage. | 60.0 | 0.0 |
| Tared | SE_Stats | $se_tared_name | Tarred | The sticky tar is slowing you down. | 10.0 | 0.0 |
| TrinketBlackDamageHealth | SE_Stats | $se_trinketblackdamagedealth | Bracelets of the Brave | Increased mace damage, and a quick health burst regeneration. | 60.0 | 0.0 |
| TrinketBlackStamina | SE_Stats | $se_trinketblackdtamina | Evasion Mantle | Increased dodge skill and reduced block stamina cost. | 120.0 | 0.0 |
| TrinketBloodGoldHealth | SE_Stats | $se_trinketbloodgoldhealth | Neckstabber | Health regenerates faster, running and attacking costs less stamina. You also get an instant boost to health and armour. | 30.0 | 0.0 |
| TrinketBloodGoldStamina | SE_Stats | $se_trinketbloodgoldstamina | Witch Crown | Health, stamina and eitr regenerate faster, and you get an instant boost to stamina and eitr. | 30.0 | 0.0 |
| TrinketBronzeHealth | SE_Stats | $se_trinketbronzehealth | Heart of the Forest | Increased health regeneration. | 60.0 | 0.0 |
| TrinketBronzeStamina | SE_Stats | $se_trinketbronzestamina | Bronze Pendant | Increased stamina regeneration. | 60.0 | 0.0 |
| TrinketCarapaceEitr | SE_Stats | $se_trinketcarapaceeitr | Pulsating Earrings | Increased eitr regeneration. | 60.0 | 0.0 |
| TrinketChitinSwim | SE_Stats | $se_trinketchitinswim | Fins of Destiny | Reduced swim stamina cost and increased swim speed. | 120.0 | 0.0 |
| TrinketFlametalEitr | SE_Stats | $se_trinketflametaleitr | Jörmundling | Increased elemental and blood magic skill, and a quick burst of eitr regeneration. | 60.0 | 0.0 |
| TrinketFlametalStaminaHealth | SE_Stats | $se_trinketflametalstaminahealth | Brimstone | A quick burst of both health and stamina regeneration. | 1.0 | 0.0 |
| TrinketIronHealth | SE_Stats | $se_trinketironhealth | Iron Brooch | Increased armour and a quick burst of health regeneration. | 30.0 | 0.0 |
| TrinketIronStamina | SE_Stats | $se_trinketironstamina | Nimble Anklet | Increased run speed and a quick burst of stamina regeneration. | 30.0 | 0.0 |
| TrinketScaleStaminaDamage | SE_Stats | $se_trinketscalestaminadamage | Resounding Shackle | Increased slash damage and a quick burst of stamina regeneration. | 60.0 | 0.0 |
| TrinketSilverDamage | SE_Stats | $se_trinketsilverdamage | Wolf Sight | Increased bow skill, spear skill, and increased pierce damage. | 30.0 | 0.0 |
| TrinketSilverResist | SE_Stats | $se_trinketsilverresist | Crystal Heart | Increased resistance against blunt, slash and pierce damage. | 50.0 | 0.0 |
| Warm | SE_Stats | $se_warm_name | Warm | You are warm. | 0.0 | 0.0 |
| Wet | SE_Wet | $se_wet_name | Wet | Lowers your health and stamina regeneration. | 120.0 | 0.0 |
| WindRun | SE_Stats | $se_windrun_name | Wind Run | Increases run speed and greatly reduced stamina use when running with the wind. Let the wind guide you to victory! | 0.0 | 0.0 |
| Wishbone | SE_Finder | $se_wishbone_name | Wishbone | Helps you find hidden things. Move in the direction the pings get more intense. | 0.0 | 0.0 |

Guardian powers: `GP_Bonemass` Bonemass; `GP_Eikthyr` Eikthyr; `GP_Fader` Fader; `GP_Moder` Moder; `GP_Queen` The Queen; `GP_TheElder` The Elder; `GP_Yagluth` Yagluth

`$se_*` keys not used as an asset's name (many are tooltips/descriptions of the above, or effects defined inside items):

| token | English |
|---|---|
| se_adrenaline | Adrenaline gain |
| se_adrenaline_upfront | Adrenaline gain |
| se_adrenalinerush | Adrenaline Rush |
| se_attackstamina | Attack stamina usage |
| se_blockstamina | Block stamina cost |
| se_blockstaminaflat | Block stamina use |
| se_blockstaminaflat_minus | Block stamina return |
| se_cold_repeat | You are cold |
| se_coldres_name | Cold resistance |
| se_cozy_name | Cozy |
| se_dodgestamina | Dodge stamina usage |
| se_eitr | Eitr |
| se_eitr_upfront | Eitr gain |
| se_eitrregen | Eitr regen |
| se_encumbered_repeat | You are carrying too much |
| se_freezing_repeat | You are freezing! |
| se_health | Health |
| se_health_upfront | Health gain |
| se_healthpotionmedium_name | Health Potion |
| se_healthpotionminor_name | Minor Health Potion |
| se_healthregen | Health regen |
| se_healthupgrade_name | Health upgrade |
| se_jumpheight | Jump height |
| se_jumplength | Jump length |
| se_jumpstamina | Jump stamina usage |
| se_lightfooteffect_name | Lightfoot |
| se_max_carryweight | Max carry weight |
| se_mead_name | Mead |
| se_noisemod | Noise |
| se_poisonres_name | Poison resistance |
| se_rested_comfort | Comfort |
| se_runstamina | Run stamina usage |
| se_shield_damage | Damage absorption (based on skill): |
| se_shield_ttl | Time: |
| se_sneakmod | Sneak |
| se_sneakstamina | Sneak stamina usage |
| se_stagger | Stagger resistance |
| se_stamina | Stamina |
| se_stamina_upfront | Stamina gain |
| se_staminapotion_name | Stamina potion |
| se_staminaregen | Stamina regen |
| se_staminaupgrade_name | Stamina upgrade |
| se_swimstamina | Swim stamina usage |
| se_ttl | Effect duration |
| se_wet_repeat | You are wet |

## 8. Raids / random events (`RandEventSystem.m_events` plus the `m_events` of each `LocationList`)

| event | list | state | biome | dur s | world keys req | world keys not | player keys | player keys not | start msg | end msg | spawns |
|---|---|---|---|---|---|---|---|---|---|---|---|
| army_eikthyr | main |  | Meadows, BlackForest | 90.0 |  | defeated_eikthyr |  | GP_Eikthyr | Eikthyr rallies the creatures of the forest | The creatures are calming down | Boar, Neck |
| army_goblin | main |  | Meadows, BlackForest, Plains | 120.0 | defeated_dragon | defeated_goblinking | $se_moder_name | GP_Yagluth | The horde is attacking! | The horde is retreating | Goblin, GoblinBrute, GoblinShaman |
| army_theelder | main |  | Meadows, Swamp, BlackForest, Plains | 120.0 | defeated_eikthyr | defeated_gdking | GP_TheElder | GP_TheElder | The forest is moving... | The forest rests again | Greydwarf, Greydwarf_Elite, Greydwarf_Shaman, Greyling |
| wolves | main |  | Mountain, Plains | 120.0 | defeated_bonemass |  | GP_Bonemass |  | You are being hunted... | The hunt is over | Wolf |
| skeletons | main |  | Meadows, Swamp, Mountain, BlackForest, Plains, Mistlands | 120.0 | defeated_bonemass |  | GP_Bonemass |  | A skeleton surprise! | The skeletons are tired of fighting | Skeleton, Skeleton_Poison |
| army_bonemass | main |  | Meadows, Swamp, Mountain, BlackForest, Plains | 150.0 | defeated_gdking | defeated_bonemass | GP_TheElder | GP_Bonemass | A foul smell from the swamp... | The smell is gone | Draugr, Skeleton |
| army_moder | main |  | Meadows, Swamp, Mountain, BlackForest, Plains | 150.0 | defeated_bonemass | defeated_dragon | $se_bonemass_name | GP_Moder | A cold wind blows from the mountains | The cold wind is gone | Hatchling |
| blobs | main |  | Meadows, Swamp, BlackForest, Plains | 120.0 | defeated_bonemass |  | GP_Bonemass |  | A foul smell from the swamp... | The smell is gone | Blob, BlobElite |
| foresttrolls | main |  | Meadows, Swamp, BlackForest, Plains | 80.0 | KilledTroll,defeated_gdking |  | KilledTroll,defeated_gdking |  | The ground is shaking | The shaking begins to fade | Troll |
| surtlings | main |  | Meadows, Swamp, BlackForest, Plains | 120.0 | killed_surtling,defeated_bonemass |  | killed_surtling,defeated_bonemass |  | There's a smell of sulfur in the air... | The smell is fading | Surtling |
| boss_eikthyr | main |  | Meadows, Swamp, BlackForest, Plains | 0.0 |  |  |  |  |  |  |  |
| boss_bonemass | main |  | Meadows, Swamp, BlackForest, Plains | 0.0 |  |  |  |  |  |  |  |
| boss_moder | main |  | All | 0.0 |  |  |  |  |  |  |  |
| boss_gdking | main |  | All | 0.0 |  |  |  |  |  |  |  |
| boss_goblinking | main |  | All | 0.0 |  |  |  |  |  |  |  |
| ghosts | main |  | Meadows, Swamp, Mountain, BlackForest, Plains | 150.0 | defeated_bonemass |  | GP_Bonemass | GP_Moder | You feel a chill down your spine... | They have been banished, for now... | Ghost, Wraith |
| fimbulvinter | main |  | All | 0.0 |  |  |  |  |  |  |  |
| boss_frozenking | main |  | All | 0.0 |  |  |  |  |  |  |  |
| army_elakingar | DeepNorth |  | DeepNorth | 90.0 | elakingmole_defeated | defeated_frozenking_p3 |  |  | They emerge from below... | They return to their burrows... | Elaking, ElakingLantern, ElakingMole |
| army_jotuns | DeepNorth |  | Meadows, Swamp, Mountain, BlackForest, Plains, DeepNorth, Mistlands | 90.0 | jotun_killed | defeated_frozenking_p3 |  |  | The Jotun have found you | The Jotun withdraw | Elaking, JotunWarrior |
| gemgoblin | Ashlands | disabled | BlackForest, Plains, Ashlands, DeepNorth, Mistlands | 90.0 | defeated_fader | defeated_queen |  | GP_Queen | Get 'em! | You got 'em | Goblin_Gem |
| boss_fader | Ashlands |  | All | 0.0 |  |  |  |  |  |  |  |
| army_charred | Ashlands |  | BlackForest, Plains, Ashlands, DeepNorth, Mistlands | 90.0 | defeated_queen | defeated_fader |  |  | The undead army marches | The army retreats | Charred_Archer, Charred_Melee, Charred_Twitcher |
| army_charredspawners | Ashlands |  | BlackForest, Plains, Ashlands, DeepNorth, Mistlands | 90.0 | defeated_queen | defeated_fader |  |  | The dead have been summoned | The dead lie still once more | Charred_Twitcher, Spawner_CharredStone_event |
| boss_queen | Mistlands |  | All | 0.0 |  |  |  |  |  |  |  |
| army_gjall | Mistlands |  | Mistlands | 90.0 | defeated_goblinking | defeated_queen | $se_yagluth_name | GP_Queen | What's up, Gjall?! | Good bye Gjall | Gjall, Tick |
| army_seekers | Mistlands |  | BlackForest, Plains, Ashlands, DeepNorth, Mistlands | 90.0 | defeated_goblinking | defeated_queen |  | GP_Queen | They sought you out | The search is over | Seeker, SeekerBrood, SeekerBrute |
| bats | MountainCaves |  | All | 120.0 | KilledBat,defeated_bonemass |  | KilledBat,defeated_bonemass |  | You stirred the cauldron | The cauldron calms | Bat |
| hildirboss1 | Hildir |  | All | 90.0 | hildir1 |  | BossHildir1 |  | She's hot on your tail! | She got burnt | Skeleton, Skeleton_Hildir_nochest, Skeleton_Meadows, Skeleton_Poison |
| hildirboss2 | Hildir |  | All | 90.0 | hildir2 |  | BossHildir2 |  | You get the chills... | You can chill out | Fenring, Fenring_Cultist, Fenring_Cultist_Hildir_nochest |
| hildirboss3 | Hildir |  | All | 90.0 | hildir3 |  | BossHildir3 |  | They were bros, man | You broke the code...again | Goblin, GoblinBrute, GoblinBruteBros_nochest |

Weather per list (`LocationList.m_environments`, `m_biomeEnvironments`):

- **DeepNorth**: Twilight_Snow, Twilight_Clear, Twilight_SnowStorm, JotunInvasion_swamp, JotunInvasion_meadows, JotunInvasion_mountain, JotunInvasion_blackforest, JotunInvasion_plains, JotunInvasion_mistlands, Morkhalla, TheHollow, DN_Bossroom — Deep north: Twilight_SnowStorm, Twilight_Snow, Twilight_Clear (music deepnorth)
- **Ashlands**: Ashlands_ashrain, Ashlands_ashrain_clear, Ashlands_storm, Ashlands_meteorshower, Ashlands_misty, Ashlands_CinderRain, Ashlands_SeaStorm, Fader — Ashlands: Ashlands_ashrain, Ashlands_misty, Ashlands_CinderRain, Ashlands_storm (music ashlands); Ashlands Ocean: Ashlands_SeaStorm (music None)
- **Mistlands**: Mistlands_clear, Mistlands_rain, Mistlands_thunder, InfectedMine, Queen — Mistlands: Mistlands_clear, Mistlands_rain, Mistlands_thunder (music mistlands)
- **MountainCaves**: Caves, CavesHildir
- **Hildir**: CryptHildir

Boss/event ids also seen on creatures and event zones: boss_bonemass, boss_eikthyr, boss_fader, boss_frozenking, boss_gdking, boss_goblinking, boss_hive, boss_moder, boss_queen

## 9. Dreams (`DreamTexts`) and ravens' tutorial texts

| token | chance | needs keys | blocked by keys | English |
|---|---|---|---|---|
| $dream_random01 | 0.5 |  |  | You dream of a river running uphill, of green shoots turning downward into the earth… |
| $dream_random02 | 0.5 |  |  | Once again, you run at the head of your warriors, the weight of your father's axe in your hand. / / You wake with the war-cry on your lips… |
| $dream_random03 | 0.5 |  |  | In your dream, you sit beside a fire in a great hall, surrounded by the chatter of familiar voices. / / Their faces blur like smoke and their names slip your mind, but the warmth of their memory lingers… |
| $dream_random04 | 0.5 |  |  | You stand at the prow of a leaping ship, the salt spray before you and the joyful shriek of gulls above. / / Folded within a dream, you remember what it was like to be alive in the land of your birth. |
| $dream_random05 | 0.5 |  |  | You dream of a great tree reaching out through the night. One half of its branches crackle with flames, the others are green with leaves. |
| $dream_eikthyr01 | 0.1 |  | defeated_eikthyr | You dream of running through a meadow, the sky alight with pale fire. There is a thunder of hoofbeats behind you but when you turn, nothing is there. / / You awaken with your heart pounding in your chest. |
| $dream_elder01 | 0.1 | defeated_eikthyr | defeated_gdking | In your dream, the forest rises before you, dragging the trees upward like a cloak, its dark mass hiding the stars. At the mountain's peak, vast antlers frame the moon... |
| $dream_bonemass01 | 0.1 | defeated_gdking,defeated_eikthyr | defeated_bonemass | You dream of a hundred ghosts crowding thickly around your bedside, seeking warmth and life until something thumps deep below the ground and they jump like crumbs on a drumskin and are gone. |
| $dream_moder01 | 0.1 | defeated_bonemass,defeated_gdking,defeated_eikthyr | defeated_moder | You dream that you are flying over mountaintops, all of Valheim spread out below you. / / As you wheel and dive in the cold air, a great shape soars up past you to block the sun. In the darkness, it speaks. "Seek me." |
| $dream_yagluth01 | 0.1 | defeated_moder,defeated_bonemass,defeated_gdking,defeated_eikthyr | defeated_goblinking | In a chamber hung with golden drapes, you kneel before the throne of a veiled king. "Sleep is but a mask", he tells you, lifting the veil slowly. / / You wake screaming. |
| $dream_random06 | 0.5 |  |  | Amidst the crash of arms, on the dark and glimmering plain of sleep, a face swells snarling before you. Your shield arm hangs limp, your spear is broken. You welcome the cold blade when it comes. / / From a dream of death, you awaken to death itself. |
| $dream_random07 | 0.5 |  |  | You dream you are lying on your back in a meadow, gazing upward at the clouds. Your name is nothing, your mind is free of thought. But there is a warm hand in yours. / / In the dream, you are laughing. But when you awaken, you find your face damp with tears. |
| $dream_random08 | 0.5 |  |  | On a boat carved from dark wood, beneath ragged sails, you lie with your arms folded across your chest. Blurred faces, like thumbprints on the darkness, croon familiar songs as they push you out to float on a sea as black and flat as glass. |
| $dream_random09 | 0.5 |  |  | You lie on the battlefield, dreaming eyes turned upward to a sky veiled by smoke. The calls of your warriors grow fainter and your eyes close for a second time. Great talons slide beneath you and you feel yourself rising, lifted from your body like a babe from its crib… |
| $dream_random10 | 0.5 |  |  | You fall into the deep well of sleep and dream only of darkness. |
| $dream_random11 | 0.5 |  |  | You dream of a bright hall filled with gracious warriors and fair maidens. The air hums with song, the boards groan under the weight of steaming dishes, the mead flows like water. / / You awake slowly with the laughter still ringing in your ears… |
| $dream_random12 | 0.5 |  |  | You sleep in fits and fretful dreams, the weight of the nightmare heavy on your chest. When morning comes, you greet it with relief. |
| $dream_random13 | 0.5 |  |  | Sleep is a river and dreams are live fish. You wake in the morning with your net empty. |
| $dream_random14 | 0.5 |  |  | You dream you are hunting with your companions, running high over green hills and down through mist-haunted valleys. Ahead of you, your prey stumbles and you leap forward, sinking your teeth into warm flesh. / / When you wake, the taste of metal lingers in your mouth. |
| $dream_random15 | 0.5 |  |  | You dream you are walking in a snowy wood when you come upon a naked child, sitting against a tree with his eyes closed but his chest moving to breathe. As you kneel beside him, you know he has been sleeping here for many centuries, waiting for you. When you touch his shoulder, you both awaken. |
| $dream_random16 | 0.5 |  |  | You dream of a mighty bear, sleeping deep below the earth in the winter of the world. It turns in its sleep, folds upon folds of flesh and fur. It has no head, no limbs. A vast mass of bear flesh, mercifully quiet. |
| $dream_random17 | 0.5 |  |  | You fall asleep planning your next day's labor and in your dreams you complete it, hewing wood, foraging for food and hunting after swift deer. You return home exhausted but happy, only to awaken and find the day is still ahead of you... |
| $dream_mistlands01 | 0.1 | defeated_goblinking |  | In your dream, you walk through a hall of smiling warriors and gracious maidens. You join with their mirth until you realise that you are naked and the small bronze shield you are carrying is not enough to cover your shame. / / You greet the morning with gratitude. |
| $dream_mistlands02 | 0.1 | defeated_goblinking |  | Dark-eyed Loki approaches you in your dream and gifts you a ring for each hand. He tells you that as long as they stay on your fingers, you will never hear an insulting word again. / / You wake with your fingers in your ears. |
| $dream_mistlands03 | 0.1 | defeated_goblinking |  | You climb a winding staircase, curled tightly within a tall tower, until you arrive at the top and look out over an endless forest. / / The wind blows green waves across the tree-tops and beneath the surface, dark shapes stir... |
| $dream_ashlands01 | 0.1 | defeated_queen |  | You awaken within a dream of dark skies and boiling clouds. Night on the mountaintop, a rough cloak pulled tight around you. Thunder tears the sky and a rain of hot embers pours through the rift, destroying your dream body in an instant… |
| $dream_ashlands02 | 0.1 | defeated_queen |  | In your sleep, you crouch in the middle of a vast and gloomy cavern while a huge beast prowls in circles around you, dragging the darkness behind it. Emerald flames flicker at its edges and the growl of its throat lingers long into the waking day… |
| $dream_ashlands03 | 0.1 | defeated_queen |  | You dream of eating roast hog at a feast. It is a simple and delicious meal. If only all dreams could be like this. |
| $dream_ashlands04 | 0.1 | defeated_queen |  | Nine maidens in robes of white dance barefoot at midnight. Nine wolves hunt a white hart in the greenwood. Nine bells ring out on the mountainside and you awake. |
| $dream_deepnorth01 | 0.1 | defeated_fader |  | In your dream, you stand naked in the middle of a pool of water so clear it cannot be seen, only felt as a swaying girdle around your hips. Three figures stand at the water's edge, watching you in silence. When the lead figure touches the water's surface with her toe, the ripple dissolves the dream and you wake. |
| $dream_deepnorth02 | 0.1 |  |  | You slide down the tunnel of a fretful dream into an absence so complete that no words will cling to it… |
| $dream_deepnorth03 | 0.1 | defeated_fader |  | You dream of a hall in winter, glittering with light and laughter in the midst of a snowy night. As you approach, the lights twist into tall flames and the laughter into screams. You run towards the hall but it sinks hissing into the snow as you approach. |
| $dream_deepnorth04 | 0.1 |  |  | You run down an endless hill, your heart thumping for joy within your chest. Two dark shapes fly alongside you and the beat of their wings lingers into your waking hours. |
| $dream_deepnorth05 | 0.1 |  | defeated_frozenking | You see a child's face, laughing and crying at the same time. The eyes are clear and blue behind their tears, the mouth is a tumble of tiny teeth. In the dream, he tells you something, but the words are gone when you wake. |
| $dream_deepnorth06 | 0.1 |  |  | In your dream there is a door without pin or bolt. You feel it would open with a push but your body is frozen in a swirl of cold wind. Behind the door are the voices of the ones you love but cannot name. You strain forward into empty wakefulness. |
| $dream_deepnorth07 | 0.1 |  |  | The ghosts of all those who have died at your hand come to you in your sleep and linger at the edges of your dreams. They want nothing, need nothing, say nothing. |
| $dream_deepnorth08 | 0.1 |  | defeated_frozenking | You sink through layers of sleep to a vast chamber where a dark shape hunches beneath a snarl of heavy chains. As it shifts to face you, the blue orbs of its eyes pierce the dream and you wake, drenched in sweat. |
| $dream_deepnorth09 | 0.1 | defeated_frozenking |  | You remember nothing of this night's dream but its rainbow mood lifts you into the day with a smile on your lips. |
| $dream_deepnorth10 | 0.1 | defeated_frozenking |  | Odin visits you in your dream and offers you a horn filled to the brim with a frothing draught. You drink deep and greet the new day feeling refreshed and alive. |

| tutorial id | raven | topic | text (start) |
|---|---|---|---|
| start | Hugin | Welcome to Valheim | Start tips: try to interact with things in the environment. Some items like stones, branches and pieces of flint can be collected and used for crafting. / / To… |
| death | Hugin | You suffered a mortal blow! | Each time you are struck down, you will <color=yellow">forget a small part of your abilities and drop your belongings</color> at the site of the accident. / /… |
| inventory | Hugin | Take stock of your inventory | Most items must be crafted. However, due to your recent departure from Midgard, you will have to recall the true shape of objects. Just pick things up and it w… |
| hunger | Hugin | You need sustenance | Consuming food is of <color=yellow>utmost importance</color> to a viking warrior, even in the afterlife. Having a full belly both fortifies your health and imp… |
| food | Hugin | A tasty morsel! | You have found a snack. Consume it to improve your health and stamina. / / Be aware that before long you will grow hungry again, so try to always have at least… |
| hammer | Hugin | You have crafted a hammer | With this tool you will raise mighty halls and towering fortifications. / / Start by building a <color=yellow>workbench</color>. This in turn will enable you t… |
| hoe | Hugin | You have crafted a hoe | This tool is used for landscaping. You could say it is the perfect complement to the hammer. / / Use it to clear the ground and manipulate the terrain. It is e… |
| cold | Hugin | Be wary of the weather | When the temperature drops at night, or if you are wet, you will suffer from being cold. This reduces your stamina regeneration. / / Seeking shelter by an open… |
| encumbered | Hugin | You need to lighten your load! | If you carry too much luggage you will become encumbered, slowing you down and preventing you from regaining your stamina. |
| pickaxe | Hugin | You have crafted a pickaxe | The black forest is rich in minerals. There you can find copper in the ground and tin lining the ocean shore. / / Now go forth and strike the earth! |
| ore | Hugin | You have found some ore | Raw ore needs to be refined in the <color=yellow>smelter</color> before you can work it at the <color=yellow>forge</color>. / / To build a smelter you will nee… |
| boss_trophy | Hugin | Congratulations, warrior! | Return to the Sacrificial Stones with your Forsaken trophy and offer it as a sacrifice to make the Gods smile upon you. |
| wishbone | Hugin | Bonemass left you a parting gift | It seems one of the many bones this living ossuary hid in his belly was a Wishbone. / / This bone contains powerful magic which guides you to things hidden in… |
| blackforest | Hugin | Turn back! This is a dangerous place | You have wandered into the Black Forest. This place can be very dangerous for those unprepared for it. Prove your worth by slaying Eikthyr. |
| randomevent | Hugin | You have been invaded | Monsters will lay siege to your camp from time to time. Strength of arms does not guarantee you victory in these situations. Build a strong defense to weather… |
| shield | Hugin | You have crafted a shield | A shield allows you to block incoming damage. / If your timing is perfect, the enemy may also be <color=yellow>parried</color>. / Be careful though, if you blo… |
| defeated_goblinking | Munin |  | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. K… |
| eitr | Hugin |  | The mists permeate all that live and grow in these lands, and now it is a part of you as well. / / With eitr you will be able to cast all sorts of powerful spe… |
| haldor | Hugin | Well met, adventurer… | Rumour has it that some traders have found themselves into the tenth world. If you find yourself in need of special commodities you might want to seek them out… |
| hildir | Hugin | Why, hello there. | I've heard that a trader was seen in an area not unlike this one. She is an odd character indeed, but may have very special goods for sale. / / However, rumour… |
| hildir_dungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. |
| defeated_queen | Munin |  | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and t… |
| ashlands | Hugin | Keep a cool head! | You have discovered the Ashlands, the very hottest parts of the tenth world. / Here, water boils without a cauldron, and anything flammable can be set alight w… |
| ashlandsocean | Hugin | Ahoy! | It seems you have ventured to the boiling waters of the south. As you surely noted, this can be a deadly pursuit. / To traverse these waters you will need to b… |
| bellfragment | Munin | A fragment of the past | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passing but now he cannot bear the sound nor the reminde… |
| trinket | Hugin | Adrenaline surges through you! | Kra-kraaa! Warrior, is that a gleaming trinket I see in your grasp? Don it with pride, and let its power course through you as you strike down your foes. Once… |
| mold | Hugin | You have found a mould! | Moulds can yield fascinating results! But it is not enough to merely fill them with materials – they also need to be hardened with the help of extreme temperat… |
| sacrificialblood | Hugin | It is time... | Well done, warrior! You have found the sacrificial blood, the final key to restoring this realm. Now the All-Father has one last task for you – prove yourself… |
| start | Hugin | Welcome to Valheim | Start tips: try to interact with things in the environment. Some items like stones, branches and pieces of flint can be collected and used for crafting. / / To… |
| death | Hugin | You suffered a mortal blow! | Each time you are struck down, you will <color=yellow">forget a small part of your abilities and drop your belongings</color> at the site of the accident. / /… |
| inventory | Hugin | Take stock of your inventory | Most items must be crafted. However, due to your recent departure from Midgard, you will have to recall the true shape of objects. Just pick things up and it w… |
| hunger | Hugin | You need sustenance | Consuming food is of <color=yellow>utmost importance</color> to a viking warrior, even in the afterlife. Having a full belly both fortifies your health and imp… |
| food | Hugin | A tasty morsel! | You have found a snack. Consume it to improve your health and stamina. / / Be aware that before long you will grow hungry again, so try to always have at least… |
| hammer | Hugin | You have crafted a hammer | With this tool you will raise mighty halls and towering fortifications. / / Start by building a <color=yellow>workbench</color>. This in turn will enable you t… |
| hoe | Hugin | You have crafted a hoe | This tool is used for landscaping. You could say it is the perfect complement to the hammer. / / Use it to clear the ground and manipulate the terrain. It is e… |
| cold | Hugin | Be wary of the weather | When the temperature drops at night, or if you are wet, you will suffer from being cold. This reduces your stamina regeneration. / / Seeking shelter by an open… |
| encumbered | Hugin | You need to lighten your load! | If you carry too much luggage you will become encumbered, slowing you down and preventing you from regaining your stamina. |
| pickaxe | Hugin | You have crafted a pickaxe | The black forest is rich in minerals. There you can find copper in the ground and tin lining the ocean shore. / / Now go forth and strike the earth! |
| ore | Hugin | You have found some ore | Raw ore needs to be refined in the <color=yellow>smelter</color> before you can work it at the <color=yellow>forge</color>. / / To build a smelter you will nee… |
| boss_trophy | Hugin | Congratulations, warrior! | Return to the Sacrificial Stones with your Forsaken trophy and offer it as a sacrifice to make the Gods smile upon you. |
| wishbone | Hugin | Bonemass left you a parting gift | It seems one of the many bones this living ossuary hid in his belly was a Wishbone. / / This bone contains powerful magic which guides you to things hidden in… |
| blackforest | Hugin | Turn back! This is a dangerous place | You have wandered into the Black Forest. This place can be very dangerous for those unprepared for it. Prove your worth by slaying Eikthyr. |
| randomevent | Hugin | You have been invaded | Monsters will lay siege to your camp from time to time. Strength of arms does not guarantee you victory in these situations. Build a strong defense to weather… |
| shield | Hugin | You have crafted a shield | A shield allows you to block incoming damage. / If your timing is perfect, the enemy may also be <color=yellow>parried</color>. / Be careful though, if you blo… |
| defeated_goblinking | Munin |  | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. K… |
| eitr | Hugin |  | The mists permeate all that live and grow in these lands, and now it is a part of you as well. / / With eitr you will be able to cast all sorts of powerful spe… |
| haldor | Hugin | Well met, adventurer… | Rumour has it that some traders have found themselves into the tenth world. If you find yourself in need of special commodities you might want to seek them out… |
| hildir | Hugin | Why, hello there. | I've heard that a trader was seen in an area not unlike this one. She is an odd character indeed, but may have very special goods for sale. / / However, rumour… |
| hildir_dungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. |
| defeated_queen | Munin |  | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and t… |
| ashlands | Hugin | Keep a cool head! | You have discovered the Ashlands, the very hottest parts of the tenth world. / Here, water boils without a cauldron, and anything flammable can be set alight w… |
| ashlandsocean | Hugin | Ahoy! | It seems you have ventured to the boiling waters of the south. As you surely noted, this can be a deadly pursuit. / To traverse these waters you will need to b… |
| bellfragment | Munin | A fragment of the past | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passing but now he cannot bear the sound nor the reminde… |
| trinket | Hugin | Adrenaline surges through you! | Kra-kraaa! Warrior, is that a gleaming trinket I see in your grasp? Don it with pride, and let its power course through you as you strike down your foes. Once… |
| mold | Hugin | You have found a mould! | Moulds can yield fascinating results! But it is not enough to merely fill them with materials – they also need to be hardened with the help of extreme temperat… |
| sacrificialblood | Hugin | It is time... | Well done, warrior! You have found the sacrificial blood, the final key to restoring this realm. Now the All-Father has one last task for you – prove yourself… |
| start | Hugin | Welcome to Valheim | Start tips: try to interact with things in the environment. Some items like stones, branches and pieces of flint can be collected and used for crafting. / / To… |
| death | Hugin | You suffered a mortal blow! | Each time you are struck down, you will <color=yellow">forget a small part of your abilities and drop your belongings</color> at the site of the accident. / /… |
| inventory | Hugin | Take stock of your inventory | Most items must be crafted. However, due to your recent departure from Midgard, you will have to recall the true shape of objects. Just pick things up and it w… |
| hunger | Hugin | You need sustenance | Consuming food is of <color=yellow>utmost importance</color> to a viking warrior, even in the afterlife. Having a full belly both fortifies your health and imp… |
| food | Hugin | A tasty morsel! | You have found a snack. Consume it to improve your health and stamina. / / Be aware that before long you will grow hungry again, so try to always have at least… |
| hammer | Hugin | You have crafted a hammer | With this tool you will raise mighty halls and towering fortifications. / / Start by building a <color=yellow>workbench</color>. This in turn will enable you t… |
| hoe | Hugin | You have crafted a hoe | This tool is used for landscaping. You could say it is the perfect complement to the hammer. / / Use it to clear the ground and manipulate the terrain. It is e… |
| cold | Hugin | Be wary of the weather | When the temperature drops at night, or if you are wet, you will suffer from being cold. This reduces your stamina regeneration. / / Seeking shelter by an open… |
| encumbered | Hugin | You need to lighten your load! | If you carry too much luggage you will become encumbered, slowing you down and preventing you from regaining your stamina. |
| pickaxe | Hugin | You have crafted a pickaxe | The black forest is rich in minerals. There you can find copper in the ground and tin lining the ocean shore. / / Now go forth and strike the earth! |
| ore | Hugin | You have found some ore | Raw ore needs to be refined in the <color=yellow>smelter</color> before you can work it at the <color=yellow>forge</color>. / / To build a smelter you will nee… |
| boss_trophy | Hugin | Congratulations, warrior! | Return to the Sacrificial Stones with your Forsaken trophy and offer it as a sacrifice to make the Gods smile upon you. |
| wishbone | Hugin | Bonemass left you a parting gift | It seems one of the many bones this living ossuary hid in his belly was a Wishbone. / / This bone contains powerful magic which guides you to things hidden in… |
| blackforest | Hugin | Turn back! This is a dangerous place | You have wandered into the Black Forest. This place can be very dangerous for those unprepared for it. Prove your worth by slaying Eikthyr. |
| randomevent | Hugin | You have been invaded | Monsters will lay siege to your camp from time to time. Strength of arms does not guarantee you victory in these situations. Build a strong defense to weather… |
| shield | Hugin | You have crafted a shield | A shield allows you to block incoming damage. / If your timing is perfect, the enemy may also be <color=yellow>parried</color>. / Be careful though, if you blo… |
| defeated_goblinking | Munin |  | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. K… |
| eitr | Hugin |  | The mists permeate all that live and grow in these lands, and now it is a part of you as well. / / With eitr you will be able to cast all sorts of powerful spe… |
| haldor | Hugin | Well met, adventurer… | Rumour has it that some traders have found themselves into the tenth world. If you find yourself in need of special commodities you might want to seek them out… |
| hildir | Hugin | Why, hello there. | I've heard that a trader was seen in an area not unlike this one. She is an odd character indeed, but may have very special goods for sale. / / However, rumour… |
| hildir_dungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. |
| defeated_queen | Munin |  | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and t… |
| ashlands | Hugin | Keep a cool head! | You have discovered the Ashlands, the very hottest parts of the tenth world. / Here, water boils without a cauldron, and anything flammable can be set alight w… |
| ashlandsocean | Hugin | Ahoy! | It seems you have ventured to the boiling waters of the south. As you surely noted, this can be a deadly pursuit. / To traverse these waters you will need to b… |
| bellfragment | Munin | A fragment of the past | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passing but now he cannot bear the sound nor the reminde… |
| trinket | Hugin | Adrenaline surges through you! | Kra-kraaa! Warrior, is that a gleaming trinket I see in your grasp? Don it with pride, and let its power course through you as you strike down your foes. Once… |
| mold | Hugin | You have found a mould! | Moulds can yield fascinating results! But it is not enough to merely fill them with materials – they also need to be hardened with the help of extreme temperat… |
| sacrificialblood | Hugin | It is time... | Well done, warrior! You have found the sacrificial blood, the final key to restoring this realm. Now the All-Father has one last task for you – prove yourself… |
| start | Hugin | Welcome to Valheim | Start tips: try to interact with things in the environment. Some items like stones, branches and pieces of flint can be collected and used for crafting. / / To… |
| death | Hugin | You suffered a mortal blow! | Each time you are struck down, you will <color=yellow">forget a small part of your abilities and drop your belongings</color> at the site of the accident. / /… |
| inventory | Hugin | Take stock of your inventory | Most items must be crafted. However, due to your recent departure from Midgard, you will have to recall the true shape of objects. Just pick things up and it w… |
| hunger | Hugin | You need sustenance | Consuming food is of <color=yellow>utmost importance</color> to a viking warrior, even in the afterlife. Having a full belly both fortifies your health and imp… |
| food | Hugin | A tasty morsel! | You have found a snack. Consume it to improve your health and stamina. / / Be aware that before long you will grow hungry again, so try to always have at least… |
| hammer | Hugin | You have crafted a hammer | With this tool you will raise mighty halls and towering fortifications. / / Start by building a <color=yellow>workbench</color>. This in turn will enable you t… |
| hoe | Hugin | You have crafted a hoe | This tool is used for landscaping. You could say it is the perfect complement to the hammer. / / Use it to clear the ground and manipulate the terrain. It is e… |
| cold | Hugin | Be wary of the weather | When the temperature drops at night, or if you are wet, you will suffer from being cold. This reduces your stamina regeneration. / / Seeking shelter by an open… |
| encumbered | Hugin | You need to lighten your load! | If you carry too much luggage you will become encumbered, slowing you down and preventing you from regaining your stamina. |
| pickaxe | Hugin | You have crafted a pickaxe | The black forest is rich in minerals. There you can find copper in the ground and tin lining the ocean shore. / / Now go forth and strike the earth! |
| ore | Hugin | You have found some ore | Raw ore needs to be refined in the <color=yellow>smelter</color> before you can work it at the <color=yellow>forge</color>. / / To build a smelter you will nee… |
| boss_trophy | Hugin | Congratulations, warrior! | Return to the Sacrificial Stones with your Forsaken trophy and offer it as a sacrifice to make the Gods smile upon you. |
| wishbone | Hugin | Bonemass left you a parting gift | It seems one of the many bones this living ossuary hid in his belly was a Wishbone. / / This bone contains powerful magic which guides you to things hidden in… |
| blackforest | Hugin | Turn back! This is a dangerous place | You have wandered into the Black Forest. This place can be very dangerous for those unprepared for it. Prove your worth by slaying Eikthyr. |
| randomevent | Hugin | You have been invaded | Monsters will lay siege to your camp from time to time. Strength of arms does not guarantee you victory in these situations. Build a strong defense to weather… |
| shield | Hugin | You have crafted a shield | A shield allows you to block incoming damage. / If your timing is perfect, the enemy may also be <color=yellow>parried</color>. / Be careful though, if you blo… |
| defeated_goblinking | Munin |  | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. K… |
| eitr | Munin |  | It seems you have had a sip from an Yggdrasil branch, and I bet you found it to your liking. / Those fun colored lights you may see spinning around you might n… |
| haldor | Hugin | Well met, adventurer… | Rumour has it that some traders have found themselves into the tenth world. If you find yourself in need of special commodities you might want to seek them out… |
| hildir | Hugin | Why, hello there. | I've heard that a trader was seen in an area not unlike this one. She is an odd character indeed, but may have very special goods for sale. / / However, rumour… |
| hildir_dungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. |
| defeated_queen | Munin |  | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and t… |
| ashlands | Hugin | Keep a cool head! | You have discovered the Ashlands, the very hottest parts of the tenth world. / Here, water boils without a cauldron, and anything flammable can be set alight w… |
| ashlandsocean | Hugin | Ahoy! | It seems you have ventured to the boiling waters of the south. As you surely noted, this can be a deadly pursuit. / To traverse these waters you will need to b… |
| bellfragment | Munin | A fragment of the past | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passing but now he cannot bear the sound nor the reminde… |
| trinket | Hugin | Adrenaline surges through you! | Kra-kraaa! Warrior, is that a gleaming trinket I see in your grasp? Don it with pride, and let its power course through you as you strike down your foes. Once… |
| mold | Hugin | You have found a mould! | Moulds can yield fascinating results! But it is not enough to merely fill them with materials – they also need to be hardened with the help of extreme temperat… |
| sacrificialblood | Hugin | It is time... | Well done, warrior! You have found the sacrificial blood, the final key to restoring this realm. Now the All-Father has one last task for you – prove yourself… |
| start | Hugin | Welcome to Valheim | Start tips: try to interact with things in the environment. Some items like stones, branches and pieces of flint can be collected and used for crafting. / / To… |
| death | Hugin | You suffered a mortal blow! | Each time you are struck down, you will <color=yellow">forget a small part of your abilities and drop your belongings</color> at the site of the accident. / /… |
| inventory | Hugin | Take stock of your inventory | Most items must be crafted. However, due to your recent departure from Midgard, you will have to recall the true shape of objects. Just pick things up and it w… |
| hunger | Hugin | You need sustenance | Consuming food is of <color=yellow>utmost importance</color> to a viking warrior, even in the afterlife. Having a full belly both fortifies your health and imp… |
| food | Hugin | A tasty morsel! | You have found a snack. Consume it to improve your health and stamina. / / Be aware that before long you will grow hungry again, so try to always have at least… |
| hammer | Hugin | You have crafted a hammer | With this tool you will raise mighty halls and towering fortifications. / / Start by building a <color=yellow>workbench</color>. This in turn will enable you t… |
| hoe | Hugin | You have crafted a hoe | This tool is used for landscaping. You could say it is the perfect complement to the hammer. / / Use it to clear the ground and manipulate the terrain. It is e… |
| cold | Hugin | Be wary of the weather | When the temperature drops at night, or if you are wet, you will suffer from being cold. This reduces your stamina regeneration. / / Seeking shelter by an open… |
| encumbered | Hugin | You need to lighten your load! | If you carry too much luggage you will become encumbered, slowing you down and preventing you from regaining your stamina. |
| pickaxe | Hugin | You have crafted a pickaxe | The black forest is rich in minerals. There you can find copper in the ground and tin lining the ocean shore. / / Now go forth and strike the earth! |
| ore | Hugin | You have found some ore | Raw ore needs to be refined in the <color=yellow>smelter</color> before you can work it at the <color=yellow>forge</color>. / / To build a smelter you will nee… |
| boss_trophy | Hugin | Congratulations, warrior! | Return to the Sacrificial Stones with your Forsaken trophy and offer it as a sacrifice to make the Gods smile upon you. |
| wishbone | Hugin | Bonemass left you a parting gift | It seems one of the many bones this living ossuary hid in his belly was a Wishbone. / / This bone contains powerful magic which guides you to things hidden in… |
| blackforest | Hugin | Turn back! This is a dangerous place | You have wandered into the Black Forest. This place can be very dangerous for those unprepared for it. Prove your worth by slaying Eikthyr. |
| randomevent | Hugin | You have been invaded | Monsters will lay siege to your camp from time to time. Strength of arms does not guarantee you victory in these situations. Build a strong defense to weather… |
| shield | Hugin | You have crafted a shield | A shield allows you to block incoming damage. / If your timing is perfect, the enemy may also be <color=yellow>parried</color>. / Be careful though, if you blo… |
| defeated_goblinking | Munin |  | Kraa! Well met, wanderer… I am Munin, brother to Hugin. I bring greetings from the Allfather. His eye sees through mine, I carry his words beneath my tongue. K… |
| eitr | Hugin |  | The mists permeate all that live and grow in these lands, and now it is a part of you as well. / / With eitr you will be able to cast all sorts of powerful spe… |
| haldor | Hugin | Well met, adventurer… | Rumour has it that some traders have found themselves into the tenth world. If you find yourself in need of special commodities you might want to seek them out… |
| hildir | Hugin | Why, hello there. | I've heard that a trader was seen in an area not unlike this one. She is an odd character indeed, but may have very special goods for sale. / / However, rumour… |
| hildir_dungeon | Hugin | Watch your step, warrior. | I have a feeling that this place could be more challenging than one might expect. |
| defeated_queen | Munin |  | Well fought warrior! My brother and I will feast well on this offering! Yet still there is work for your arm… In the Ashlands dark clouds blister the sky and t… |
| ashlands | Hugin | Keep a cool head! | You have discovered the Ashlands, the very hottest parts of the tenth world. / Here, water boils without a cauldron, and anything flammable can be set alight w… |
| ashlandsocean | Hugin | Ahoy! | It seems you have ventured to the boiling waters of the south. As you surely noted, this can be a deadly pursuit. / To traverse these waters you will need to b… |
| bellfragment | Munin | A fragment of the past | Kraa! A sad memory lingers here... A faint echo in the air... Once these rang for joy to celebrate his passing but now he cannot bear the sound nor the reminde… |
| trinket | Hugin | Adrenaline surges through you! | Kra-kraaa! Warrior, is that a gleaming trinket I see in your grasp? Don it with pride, and let its power course through you as you strike down your foes. Once… |
| mold | Hugin | You have found a mould! | Moulds can yield fascinating results! But it is not enough to merely fill them with materials – they also need to be hardened with the help of extreme temperat… |
| sacrificialblood | Hugin | It is time... | Well done, warrior! You have found the sacrificial blood, the final key to restoring this realm. Now the All-Father has one last task for you – prove yourself… |

## 10. Other story-usable systems

**ItemSets** (dev kits, `ItemSets.m_sets`): `Meadows`, `BlackForest`, `Swamps`, `Mountains`, `Plains`, `PlainsBoss`, `Mistlands`, `Jonathan`, `1`, `MistlandsMage`, `Fisherman`, `MistlandsWarrior`, `Ashlands`, `AshlandsFood`, `AshlandsMage`, `AshlandsMedium`, `AshlandsWarrior`, `AshlandsWeapons`, `Base`, `Builder`, `Harvester`, `DeepNorthWarrior`, `DeepNorthMage`, `DeepNorthMedium`

**ItemSets** (dev kits, `ItemSets.m_sets`): `Meadows`, `BlackForest`, `Swamps`, `Mountains`, `Plains`, `PlainsBoss`, `Mistlands`, `Jonathan`, `1`, `MistlandsMage`, `Fisherman`, `MistlandsWarrior`, `Ashlands`, `AshlandsFood`, `AshlandsMage`, `AshlandsMedium`, `AshlandsWarrior`, `AshlandsWeapons`, `Base`, `Builder`, `Harvester`, `DeepNorthWarrior`, `DeepNorthMage`, `DeepNorthMedium`

**Fallen Warriors' names** (`WarriorNames` on `memorialsite_offering`): 97 Norse male names (Adalstein, Agmundr, Agnar, Aki, Alfin, Alfrik …), 71 female names (Alfhildr, Alof, Asdis, Asgerdr, Ashildr, Aslaug …), titled with one of 202 suffixes, e.g. {name} the Adventurer; {name} the Baleful; {name} the Bear; {name} the Beaten. Their fight lines: `fallen_viking_randomstart_*` (20), `_randomtaunt_*` (40), `_randomend_*` (20, e.g. "You are worthy.").

**Prefabs whose token has no English** (unfinished, hidden or dev content; the game would show the raw token): Bjorn_spiritcaller (`$spiritcaller_bjorn`, creature); Boar_spiritcaller (`$spiritcaller_boar`, creature); Deer_White (`$enemy_deerwhite`, creature); DvergerTest (`$enemy_dverger`, creature); FrostWisp (`$enemy_frostwisp`, creature); Hive (`$enemy_hive`, creature); Moose_spiritcaller (`$spiritcaller_moose`, creature); TheHive (`$enemy_thehive`, creature); Wolf_spiritcaller (`$spiritcaller_wolf`, creature); ArmorStand_Male (`$piece_armorstand_male`, piece); Sled (`$tool_sled`, piece); bar_ancientmetal_stack (`$piece_ancientmetalstack`, piece); darkwood_beam_67 (`$piece_darkwoodbeam67`, piece); snow_decrease (`$piece_snowshovel`, piece); snow_decrease_firepit_placed_large (`$piece_snowshovel`, piece); snow_decrease_firepit_placed_small (`$piece_snowshovel`, piece); GenericMoldUncooked (`$item_smallparts_gold_uncooked`, item); IceShoes (`$item_iceshoes`, item); IceSkates (`$item_iceskates`, item); Larva (`$item_larva`, item); LastBossGate_RuneTile (`$item_runetile`, item); MoldSmallParts (`$item_mold_smallparts`, item); Pot_Shard_Red (`$item_pot_shard_red`, item); SmallPartsGoldUncooked (`$item_smallparts_gold_uncooked`, item); TorchMist (`$item_torchmist`, item); TrophyDeerWhite (`$item_trophy_deer_white`, item); TurretBoltBone (`$item_turretboltbone`, item); Upgrader0Armor (`$item_upgrader_tier0 $item_upgrader_armor $item_upgrader_name`, item); Upgrader0Weapon (`$item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader1Armor (`$item_upgrader_tier1 $item_upgrader_armor $item_upgrader_name`, item); Upgrader1Weapon (`$item_upgrader_tier1 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader2Armor (`$item_upgrader_tier2 $item_upgrader_armor $item_upgrader_name`, item); Upgrader2Weapon (`$item_upgrader_tier2 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader3Armor (`$item_upgrader_tier3 $item_upgrader_armor $item_upgrader_name`, item); Upgrader3Weapon (`$item_upgrader_tier3 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader4Armor (`$item_upgrader_tier4 $item_upgrader_armor $item_upgrader_name`, item); Upgrader4Weapon (`$item_upgrader_tier4 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader5Armor (`$item_upgrader_tier5 $item_upgrader_armor $item_upgrader_name`, item); Upgrader5Weapon (`$item_upgrader_tier5 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader6Armor (`$item_upgrader_tier6 $item_upgrader_armor $item_upgrader_name`, item); Upgrader6Weapon (`$item_upgrader_tier6 $item_upgrader_weapon $item_upgrader_name`, item); Upgrader7Armor (`$item_upgrader_tier7 $item_upgrader_armor $item_upgrader_name`, item); Upgrader7Weapon (`$item_upgrader_tier7 $item_upgrader_weapon $item_upgrader_name`, item)

**Achievements**: 53 (`Achievement` assets); the all-bosses one lists `$enemy_frozenking_p3` as the final kill.

## 11. The Deep North (1.0) in detail

### 11a. Kall Fimbulbringer, the Frozen King: forms and arena

| prefab | token | English | HP | faction | boss | event | defeat key | drops |
|---|---|---|---|---|---|---|---|---|
| Aspect_Bonemass | $enemy_aspect_bonemass | Aspect of the Writhing Dead | 1600.0 | Boss |  |  |  |  |
| Aspect_Eikthyr | $enemy_aspect_eikthyr | Aspect of the Lightning Stag | 3000.0 | Boss |  |  |  |  |
| Aspect_Elder | $enemy_aspect_gdking | Aspect of the Living Forest | 1600.0 | Boss |  |  |  |  |
| Aspect_Fader | $enemy_aspect_fader | Aspect of the Emerald Flame | 1700.0 | Boss |  |  |  |  |
| Aspect_Moder | $enemy_aspect_dragon | Aspect of the Dragon Mother | 1500.0 | Boss |  |  |  |  |
| Aspect_SeekerQueen | $enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch | 1700.0 | Boss |  |  |  |  |
| Aspect_TentaRoot | $enemy_root | Root | 20.0 | Boss |  |  |  |  |
| Aspect_Yagluth | $enemy_aspect_goblinking | Aspect of the Twisted Soul | 1700.0 | Boss |  |  |  |  |
| BlobAspect | $enemy_blob | Blob | 50.0 | Undead |  |  |  | TrophyBlob x1 (10%); Ooze x1-2 |
| FrozenKing | $enemy_frozenking | Kall Fimbulbringer | 10000.0 | Boss | boss | boss_frozenking | defeated_frozenking |  |
| FrozenKing_p2 | $enemy_frozenking | Kall Fimbulbringer | 7000.0 | Boss | boss | boss_frozenking |  |  |
| FrozenKing_p3 | $enemy_frozenking_p3 | Kall Fimbulbringer | 30000.0 | Boss | boss | boss_frozenking | defeated_frozenking_p3 | FrozenKingDrop x1; CrownJewel x1 |
| Skeleton_aspect | $enemy_skeleton | Skeleton | 40.0 | Undead |  |  |  | TrophySkeleton x1 (10%); BoneFragments x1 |

- Altar `offeraltar_FrozenKing` in `DN_Bossroom`: "Strange Bowl" — offer **HatefulBlood x3** to summon **vfx_LastBossGate_destroyed**; sets world key `LastBossGate_Open`.
- Altar `offeraltar_FrozenKing` in `offeraltar_FrozenKing`: "Strange Bowl" — offer **HatefulBlood x3** to summon **vfx_LastBossGate_destroyed**; sets world key `LastBossGate_Open`.
- Altar `offeraltar_FrozenKing_bossroom` in `DN_Bossroom`: "Strange Bowl" — offer **HatefulBlood x3** to summon **FrozenKing**; sets world key ``.
- Altar `offeraltar_FrozenKing_bossroom` in `offeraltar_FrozenKing_bossroom`: "Strange Bowl" — offer **HatefulBlood x3** to summon **FrozenKing**; sets world key ``.

**The ending chain, as wired in the prefabs.** Break a `BlackIce_Core` (it drops **Malicious Blood**, `HatefulBlood`, and stops the Jotun invasion) -> offer 3 at the outer `offeraltar_FrozenKing` of `DN_Bossroom` ("The First Prison"/"The Prison"; its runestone reads `$lore_frozenking` "HALT THE INVASION"), which plays `vfx_LastBossGate_destroyed` and sets world key `LastBossGate_Open` -> offer 3 more at the inner `offeraltar_FrozenKing_bossroom` to summon `FrozenKing` (10,000 HP; key `defeated_frozenking`) -> `FrozenKing_p2` (7,000 HP) calls the seven **Aspects** of the earlier Forsaken -> `FrozenKing_p3` (30,000 HP; key `defeated_frozenking_p3`) drops **Sacrificial Blood** (`FrozenKingDrop`, "The last essence of an end once foretold.") and the **Crown Jewel** -> place the Sacrificial Blood on the **Chiselled Platform** (`StartPlatform` in the start temple; message "You are worthy") which sets world key `StoneCircle` -> `Valkyrie_End` appears ("Journey to Valhalla", runs the end credits) with Hugin ("Journey to Valhalla") and Munin ("And so the saga comes to an end...") guide points beside it. Achievement `$ach_boss8frozenking`: "Defeat the Shackled One."

| token | English |
|---|---|
| ach_boss8frozenking | Kall Fimbulbringer |
| ach_boss8frozenking_desc | Defeat the Shackled One. |
| enemy_aspect_bonemass | Aspect of the Writhing Dead |
| enemy_aspect_dragon | Aspect of the Dragon Mother |
| enemy_aspect_eikthyr | Aspect of the Lightning Stag |
| enemy_aspect_fader | Aspect of the Emerald Flame |
| enemy_aspect_gdking | Aspect of the Living Forest |
| enemy_aspect_goblinking | Aspect of the Twisted Soul |
| enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch |
| enemy_boss_frozenking_alertmessage | His hatred corrupts all! |
| enemy_boss_frozenking_deathmessage | Peace settles over the world |
| enemy_frozenking | Kall Fimbulbringer |
| enemy_frozenking_p3 | Kall Fimbulbringer |
| fimbulvinterorb | Orb of Fimbulvinter |
| fimbulvinterorb_destroyed | The Jotun Retreat |
| fimbulvinterorb_start | The Jotun Advance |
| item_crownjewel | Crown Jewel |
| item_crownjewel_description | A strange power surges within this gem, unlike anything you've felt before. |
| item_frozenking_drop | Sacrificial Blood |
| item_frozenking_drop_description | The last essence of an end once foretold. |
| item_hatefulblood | Malicious Blood |
| location_dnbossroom | The First Prison |
| location_dnbossroomnew | The Prison |
| lore_frozenking | HALT THE INVASION |
| npc_valkyrie_end | Valkyrie |
| npc_valkyrie_end_interact | Journey to Valhalla |
| piece_offerbowl_frozenking | Strange Bowl |
| stonecircle_hook_name | Chiselled Platform |
| stonecircle_itemplaced | You are worthy |
| tutorial_end_hugin_label | Hugin: To Valhalla |
| tutorial_end_hugin_text | Well done, warrior! / At long last, you have slain all Forsaken and proven yourself worthy. The gates of Valhalla are now open to you – all you need to do is to let the valkyrie carry you there. / Bu… |
| tutorial_end_hugin_topic | Journey to Valhalla |
| tutorial_end_munin_label | Munin: Forsaken Defeated |
| tutorial_end_munin_text | You have fought well, warrior. / Thanks to you, the Forsaken are no more, and this world can once again be reunited with the rest of Yggdrasil. But that is a story for another time! For now, you ough… |
| tutorial_end_munin_topic | And so the saga comes to an end... |

### 11b. Deep North creatures

| prefab | token | English | HP | faction | boss / event / key | tame | DN | folder | drops |
|---|---|---|---|---|---|---|---|---|---|
| Barka | $enemy_barka | Barka | 2200.0 | DeepNorth |  |  | DN | Barka | TrophyBarka x1 (10%); BarkaBranch x1 |
| BlobMork | $enemy_blobmork | Shapeless Pulp | 150.0 | DeepNorth |  |  | DN | Blob | TrophyBlob_Morkhalla x1 (10%); BlobMorkMini x1-2; OozeMork x1 (50%) |
| BlobMorkMini | $enemy_blobmorkmini | Tiny Pulp | 50.0 | DeepNorth |  |  | DN | Blob | OozeMork x1 (25%) |
| DvergerDeepNorth | $enemy_dvergr_deepnorth | Imprisoned Dvergr | 1500.0 | Dverger |  |  | DN | Dverger | Coins x10-20; TrophyDvergr x1 (5%); AncientGemstoneBlack x1 (10%); AncientGemstoneGreen x1 (10%); AncientGemstoneOrange x1 (10%); AncientGemstonePurple x1 (10%) |
| Elaking | $enemy_elaking | Elaking | 350.0 | DeepNorth |  |  | DN | Elaking | ElakingHairBundle x1-2; TrophyElaking x1 (10%); MoldKeys x1 (20%) |
| ElakingLantern | $enemy_elaking | Elaking | 350.0 | DeepNorth |  |  | DN | Elaking | ElakingHairBundle x1-2; TrophyElaking x1 (10%) |
| ElakingMole | $enemy_elakingmole | Eyeless One | 1400.0 | DeepNorth | key=elakingmole_defeated |  | DN | ElakingMole | TrophyMole x1 (10%); MoleClaws x1-2; MoldKeys x1 (50%) |
| FallenWarrior | $enemy_fallenwarrior | Fallen Warrior | 750.0 | Undead |  |  | DN | FallenWarrior | OrbFrostFire x1 (50%); OrbThunderBlood x1 (50%) |
| ShadowPerson | $enemy_shadowperson | Shadow | 750.0 | DeepNorth |  |  | DN | FallenWarrior |  |
| FrozenKing | $enemy_frozenking | Kall Fimbulbringer | 10000.0 | Boss | BOSS ev=boss_frozenking key=defeated_frozenking |  | DN | FrozenKing |  |
| FrozenKing_p2 | $enemy_frozenking | Kall Fimbulbringer | 7000.0 | Boss | BOSS ev=boss_frozenking |  | DN | FrozenKing |  |
| FrozenKing_p3 | $enemy_frozenking_p3 | Kall Fimbulbringer | 30000.0 | Boss | BOSS ev=boss_frozenking key=defeated_frozenking_p3 |  | DN | FrozenKing | FrozenKingDrop x1; CrownJewel x1 |
| Tendril_back | Root |  | 1000.0 | Boss |  |  | DN | FrozenKing |  |
| Aspect_Bonemass | $enemy_aspect_bonemass | Aspect of the Writhing Dead | 1600.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Eikthyr | $enemy_aspect_eikthyr | Aspect of the Lightning Stag | 3000.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Elder | $enemy_aspect_gdking | Aspect of the Living Forest | 1600.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Fader | $enemy_aspect_fader | Aspect of the Emerald Flame | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Moder | $enemy_aspect_dragon | Aspect of the Dragon Mother | 1500.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_SeekerQueen | $enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Aspect_Yagluth | $enemy_aspect_goblinking | Aspect of the Twisted Soul | 1700.0 | Boss |  |  | DN | FrozenKing/BossAspects |  |
| Tendril | Tendril |  | 80.0 | Boss |  |  | DN | FrozenKing/attacks |  |
| Frysling | $enemy_frysling | Frysling | 100.0 | TrainingDummy | key=killed_frysling |  | DN | Frysling | FrostCore x1 |
| GoblinDeepNorth | $enemy_goblin_deepnorth | Captive Fuling | 250.0 | PlainsMonsters |  |  | DN | Goblin | Coins x20-40 (25%); AncientCoin x1-2; Lingonberry x1-10 (20%) |
| Greydwarf_Frozen | $enemy_greydwarf | Greydwarf | 100.0 | DeepNorth |  |  | DN | GreyDwarf | GreydwarfEye x1 (50%); Wood x1; Resin x1; TrophyGreydwarf x1 (5%); Snowball x1; Ice x1-2 |
| Greydwarf_Shaman_Frozen | $enemy_greydwarfshaman | Greydwarf Shaman | 120.0 | DeepNorth |  |  | DN | GreyDwarf | GreydwarfEye x1 (50%); Wood x1; Resin x1-2; TrophyGreydwarfShaman x1 (10%); Pukeberries x1-2; Ice x1-2 |
| JotunWarrior | $enemy_jotun_warrior | Krigen | 1300.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | MoldArmormediumChest x1 (3%); MoldArmorMediumHelmet x1 (3%); MoldArmorMediumLegs x1 (3%); MemorialCoal x1 (20%); TrophyJotunWarrior x1 (10%); Leatherstraps x1-3 |
| JotunWarriorDualWield | $enemy_jotun_warrior | Krigen | 1300.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | MoldArmormediumChest x1 (3%); MoldArmorMediumHelmet x1 (3%); MoldArmorMediumLegs x1 (3%); MemorialCoal x1 (20%); TrophyJotunWarrior x1 (10%); Leatherstraps x1-3 |
| JotunWitch | $enemy_jotun_witch | Hexen | 800.0 | DeepNorth | key=jotun_killed |  | DN | Jotnar | NornThread x1-3; TrophyJotunWitch x1 (10%); BloodGoldKey x1 (10%); MoldArmorMageChest x1 (5%); MoldArmorMageHelmet x1 (5%); MoldArmorMageLegs x1 (5%) |
| Skeleton_DeepNorth | $enemy_skeleton | Skeleton | 100.0 | DeepNorth |  |  | DN | Skeleton | TrophySkeleton x1 (10%); BoneFragments x1; Ice x1 |
| TrollFrost | $enemy_trollfrost | Gammeltroll | 3000.0 | DeepNorth |  |  | DN | Troll |  |
| Writhan | $enemy_writhan | Writhan | 400.0 | Undead | key=defeated_writhan |  | DN | Writhan | TrophyWrithan x1 (10%); WrithanRoots x1-2 |
| Moose | $enemy_moose | Moose | 1000.0 | DeepNorth |  | yes | DN | moose | MooseMeat x4-6; TrophyMoose x1 (10%); MooseHide x2-3; MooseSinew x2-3 |
| Moose_calf | $enemy_moosecalf | Moose Calf | 1000.0 | DeepNorth |  |  | DN | moose |  |
| Moose_spiritcaller | $spiritcaller_moose |  | 1100.0 | Players |  | yes | DN | moose |  |
| Seal | $enemy_seal | Seal | 400.0 | DeepNorth |  |  | DN | seal | SealHide x2-3; TrophySeal x1 (10%); SealBlubber x2-3 |
| Seal_Pup | $enemy_seal_baby | Baby Seal | 200.0 | DeepNorth |  |  | DN | seal | SealBlubber x1 (10%) |

### 11c. Deep North spawn table

| spawner | prefab | English | biome | requires key | time | levels | state |
|---|---|---|---|---|---|---|---|
| Seal | Seal | Seal | DeepNorth |  | any | 1-3 |  |
| Shadow People | ShadowPerson | Shadow | DeepNorth |  | night | 1-3 |  |
| Frozen GD | Greydwarf_Frozen | Greydwarf | DeepNorth |  | any | 1-3 |  |
| Frozen GD shaman | Greydwarf_Shaman_Frozen | Greydwarf Shaman | DeepNorth |  | any | 1-3 |  |
| Frozen Skeleton | Skeleton_DeepNorth | Skeleton | DeepNorth |  | any | 1-3 |  |
| Seal pup | Seal_Pup | Baby Seal | DeepNorth |  | any | 1-1 |  |
| Elakingar NIGHT | Elaking | Elaking | DeepNorth |  | night | 1-2 |  |
| Elakingar Lantern NIGHT | ElakingLantern | Elaking | DeepNorth |  | night | 1-2 |  |
| Jotun Melee Patrol | JotunWarrior | Krigen | DeepNorth | jotun_killed | day | 1-1 |  |
| Jotun Witch Patrol | JotunWitch | Hexen | DeepNorth | jotun_killed | day | 1-1 |  |
| Giant Troll | Spawner_TrollFrost |  | DeepNorth |  | any | 1-1 |  |
| Barka | Barka | Barka | DeepNorth |  | any | 1-1 |  |
| Älg | Moose | Moose | DeepNorth |  | any | 1-3 |  |
| Fimbulvinter - Jotun Warriors | JotunWarrior | Krigen | All |  | any | 1-1 |  |
| Fimbulvinter - Jotun Witches | JotunWitch | Hexen | All |  | any | 1-1 |  |
| Fimbulvinter - Elakingar | Elaking | Elaking | All |  | any | 1-1 |  |
| Fimbulvinter - Meteors | projectile_FimbulvinterMeteor |  | All |  | any | 1-1 |  |

### 11d. Deep North locations

| prefab | entry name | list | biome | qty | group | flags | folder | contents |
|---|---|---|---|---|---|---|---|---|
| Runestone_DeepNorth | Runestone_DeepNorth | DeepNorth | DeepNorth | 70 | Runestones |  | Ashlands | runestone: $lore_deepnorth_hervor01, $lore_deepnorth_hervor02, $lore_deepnorth_hervor03, $lore_deepnorth_hervor04 … |
| BearCave | BearCave | DeepNorth | BlackForest | 50 |  |  | BlackForest | enter: Bear Cave; spawns: Bjorn_sleeping |
| HalfBurried_ForestCrypt | HalfBurried_ForestCrypt | DeepNorth | BlackForest | 100 |  | DISABLED | BlackForest | spawns: Skeleton; chests: Chest |
| DN_Bossroom | bosslocation | DeepNorth | DeepNorth | 3 | dn_boss | prioritized map icon | DeepNorth | enter: The Prison; altar: Strange Bowl: offer HatefulBlood x3 -> FrozenKing; Strange Bowl: offer HatefulBlood x3 -> vfx_LastBossGate_destroyed [sets LastBossGate_Open]; runestone: $lore_frozenking; raven: Hugin: eternalpyre |
| DN_gammeltrollFrac01 | DN_gammeltrollFrac01 | DeepNorth | DeepNorth | 30 |  |  | DeepNorth |  |
| DN_gammeltrollFrac02 | DN_gammeltrollFrac02 | DeepNorth | DeepNorth | 30 |  |  | DeepNorth | raven: Munin: generalDN1 |
| DN_hut01 | hut01 | DeepNorth | DeepNorth | 40 | northvillage |  | DeepNorth | chests: Chest |
| FimbulLocation01 | FimbulLocation01 | DeepNorth |  | 100 |  | DISABLED | DeepNorth | spawns: JotunWarrior, JotunWarriorDualWield, JotunWitch |
| FrozenShip01_DN | FrozenShip01 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth | raven: Munin: shipDN |
| FrozenShip02_DN | FrozenShip02 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth | raven: Munin: shipDN |
| FrozenShip03_DN | FrozenShip03 | DeepNorth | DeepNorth | 50 | FrozenShip |  | DeepNorth |  |
| HotSpring1 | hotspring | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| HotSpring2 | hotspring2 | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| HotSpring3 | hotspring3 | DeepNorth | DeepNorth | 50 | hotspring | DISABLED | DeepNorth |  |
| IcePond1 | icepond | DeepNorth | DeepNorth | 40 | icepond |  | DeepNorth |  |
| LumberCamp | Lumbercamp | DeepNorth | DeepNorth | 50 | thehole |  | DeepNorth | chests: Chest |
| MorkBorg | morkborg | DeepNorth | DeepNorth | 40 | morkborg |  | DeepNorth | enter: Mörkhalla; spawns: BlobMork; raven: Munin: morkborg; chests: Ancient Chest, Jotun's Chest; locked by: BloodGoldKey |
| NorthMemorialPlace | NorthMemorialPlace | DeepNorth | DeepNorth | 15 | memorialplace |  | DeepNorth | altar: Ancient Altar: offer MemorialCoal x3 -> memorialsite_offering; vegvisir->DN_Bossroom (Aesir Passage); runestone: $lore_deepnorth_memorial1, $lore_deepnorth_memorial2, $lore_deepnorth_memorial3, $lore_deepnorth_memorial_description …; chests: Chest |
| NorthVillage | NorthVillage | DeepNorth | DeepNorth | 135 |  |  | DeepNorth |  |
| ShipSetting02 | shipsetting | DeepNorth | DeepNorth | 100 | shipsetting |  | DeepNorth | raven: Munin: generalDN; chests: Chest |
| ShipSetting03 | shipsetting | DeepNorth | DeepNorth | 50 | shipsetting |  | DeepNorth | chests: Chest |
| ShipWreck01_DN | Shipwreck_DN | DeepNorth | DeepNorth | 170 |  |  | DeepNorth | chests: Chest |
| ShipWreck02_DN | Shipwreck02_DN | DeepNorth | DeepNorth | 120 |  |  | DeepNorth | chests: Chest |
| TheDarkestHole | darkesthole | DeepNorth | DeepNorth | 1 | thehole | unique prioritized DISABLED | DeepNorth | enter: Winding tunnels; spawns: Elaking, ElakingLantern, Ghost_old |
| TheHole01 | thehole01 | DeepNorth | DeepNorth | 40 |  |  | DeepNorth | enter: Winding tunnels; spawns: ShadowPerson; raven: Munin: generalDN2; chests: Barrel, Wardrobe |

Deep North dungeon interiors (room themes):

| room theme (folder) | creatures spawned | npcs | chests | door keys | runestones | ravens |
|---|---|---|---|---|---|---|
| hole | Elaking, ElakingLantern, ElakingMole |  |  |  | $lore_windingtunnels |  |
| morkhalla | BlobMork, DvergerDeepNorth, GoblinDeepNorth, JotunWarrior, JotunWarriorDualWield, JotunWitch |  | Ancient Chest, Jotun's Chest |  |  |  |
| northVillage | ShadowPerson |  | Barrel, Chest |  |  | Munin: generalDN4 |

### 11e. Deep North NPCs, ghosts and voices

The Imprisoned Dvergr (`DvergerDeepNorth`) and Captive Fuling (`GoblinDeepNorth`) are spawned inside Mörkhalla rooms (`morkhalla` theme, by `CreatureSpawner`), next to Jotun guards; Fallen Warriors stand at `memorialsite_offering` (spawned by the Ancestral Memorial altar, `NorthMemorialPlace`); Shadows walk the North Village and The Hole at night.

| GameObject | class | token | English | inside (root) | folder | note |
|---|---|---|---|---|---|---|
| DvergerDeepNorth | NpcTalk | $enemy_dvergr_deepnorth | Imprisoned Dvergr |  | Dverger | 19 talk lines |
| FallenWarrior | NpcTalk | $enemy_fallenwarrior | Fallen Warrior |  | FallenWarrior | 80 talk lines |
| FallenWarrior (1) | NpcTalk | $enemy_fallenwarrior | Fallen Warrior | memorialsite_offering | Props/MemorialStones | 80 talk lines |
| ShadowPerson | NpcTalk | $enemy_shadowperson | Shadow |  | FallenWarrior | 30 talk lines |

| token | English |
|---|---|
| enemy_dvergr_deepnorth | Imprisoned Dvergr |
| enemy_fallenwarrior | Fallen Warrior |
| enemy_ghost_void | The Void |
| enemy_goblin_deepnorth | Captive Fuling |
| enemy_shadowperson | Shadow |
| fallen_viking_randomstart_1 | Let us see what you are capable of! |
| fallen_viking_randomstart_2 | Show me what you can do! |
| fallen_viking_randomstart_3 | Prove yourself! |
| fallen_viking_randomtaunt_1 | Is that the best you can do? |
| fallen_viking_randomtaunt_2 | Come on! Fight me then! |
| fallen_viking_randomtaunt_3 | You can do better than that! |
| fallenwarrior_title1_m | {name} the Adventurer |
| fallenwarrior_title2_m | {name} the Baleful |
| fallenwarrior_title3_m | {name} the Bear |
| npc_dvergr_deepnorth_random_goodbye1 | Don't worry about me, I'll be fine without you. |
| npc_dvergr_deepnorth_random_goodbye2 | I suppose I'll find my own way out. |
| npc_dvergr_deepnorth_random_goodbye3 | Slay all the giantkind you can! |
| npc_dvergr_deepnorth_random_goodbye4 | Until we meet again, then. |
| npc_dvergr_deepnorth_random_goodbye5 | What are you looking at? |
| npc_dvergr_deepnorth_random_goodbye6 | I'm so far from home... |
| npc_dvergr_deepnorth_random_goodbye7 | Are you leaving? |
| npc_dvergr_deepnorth_random_greet1 | Who goes there? |
| npc_dvergr_deepnorth_random_greet2 | Are you friend or foe? |
| npc_dvergr_deepnorth_random_greet3 | Hey! You there! |
| npc_dvergr_deepnorth_random_greet4 | Help me get out of here! |
| npc_dvergr_deepnorth_random_greet5 | A hand, perhaps? |
| npc_dvergr_deepnorth_random_greet6 | Over here! |
| npc_dvergr_deepnorth_random_greet7 | I'll help you if you help me. |
| npc_munin_deepnorth_general01 | This is a place of transformation. Certain creatures find a new purpose here, becoming something more than they once were. Are you one of these creatures, I wonder? |
| npc_munin_deepnorth_general02 | Kraa! Those who first ventured here did not know about the underground thieves. If they had known, they would certainly have chosen better places to build their homesteads. |
| npc_munin_deepnorth_general03 | Watch these lands for memorials in the honour of the warriors who came before you. They may bestow a boon upon you, should you prove yourself worthy in their eyes. |
| npc_munin_deepnorth_general04 | Long before the Allfather summoned you, others were tasked to guard this world. Even though many years have gone by since their passing, they still can't seem to find peace. Be kind, and do not disturb them. |
| npc_munin_deepnorth_morkborg | Kraa! Strange folk dwell here, created from the very evil itself... |
| npc_munin_deepnorth_ship | Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way for them to make the return journey... |
| shadowperson_randomtalk_01 | His blade was dipped in poison, I stood no chance... |
| shadowperson_randomtalk_02 | Pain, sharp and excruciating. And then nothing. |
| shadowperson_randomtalk_03 | Fire doesn't warm me any longer, nor does it offer any true light. |
| shadowperson_randomtalk_04 | I don't recall what it was like to not be numb and cold. |
| shadowperson_randomtalk_05 | Her knife found my heart just as I slit her throat. We died next to each other. |

### 11f. Deep North pieces (`Piece` category DeepNorth, or Deep North asset folder)

| prefab | token | English | category | station | built with | cost | description |
|---|---|---|---|---|---|---|---|
| CookedMooseMeat | $item_moose_meat_cooked | Cooked Moose Meat | Food (Feaster) |  | Feaster | CookedMooseMeat x1 | This meat is lean yet full of flavour. |
| CookedSealBlubber | $item_blubber_cooked | Cooked Seal Blubber | Food (Feaster) |  | Feaster | CookedSealBlubber x1 | A chewy meat, with an aftertaste of remorse. |
| FeastDeepNorth | $item_feastdeepnorth | Northern Morning Fare | DeepNorth tab (Hammer) / Feasts (Feaster) |  | Feaster | FeastDeepNorth_Material x1 | Warming and filling, this meal will sustain you even during the coldest of days… |
| FirTree_big_Sapling | $prop_fir_big_sapling | Timberwood Sapling | Misc |  | Cultivator | FirConeFrost x1 | Though native to the northern regions, this tree can grow in most climates. |
| MooseKebab | $item_moosekebab | Meat In Bread | Food (Feaster) |  | Feaster | MooseKebab x1 | A convenient meal, often favoured by travelling merchants. |
| Morkhalla_ChestAncient | $piece_morkhallachestancient | Ancient Chest | Furniture | piece_workbench |  | Wood x10, Tar x2, BlackMetal x6 | The sturdy black metal that holds this chest together lets you put almost anyth… |
| Morkhalla_Stonepile | $piece_marblepile | Black Marble Pile | Misc |  |  | BlackMarble x50 | Perfect for Dvergr inspired construction. |
| Morkhalla_firepit | $piece_firepit | Campfire | Misc |  |  | Stone x5, Wood x2 |  |
| SealSoup | $item_sealsoup | Seal Meat Soup | Food (Feaster) |  | Feaster | SealSoup x1 | A warm and tasty meal, best enjoyed on a cold day. |
| SmokedMooseMeat | $item_smokedmoosemeat | Smoked Moose Meat | Food (Feaster) |  | Feaster | SmokedMooseMeat x1 | The smoke only adds to the wild flavour. |
| TreasureChest_deepnorth_village | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| TreasureChest_morkhalla | Chest |  | Furniture | piece_workbench |  | Wood x10 |  |
| ashwood_wall_beam_67 | $piece_ashwoodbeam67 | Ashwood Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_cross_67 | $piece_ashwoodwallrooftop67 | Ashwood Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | These supports are always warm to the touch. |
| ashwood_wall_roof_67_a | $piece_ashwoodwallroof67 | Ashwood Wall 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| ashwood_wall_roof_67_upsidedown | $piece_ashwoodwallroof67upsidedown | Ashwood Wall 67° (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Blackwood x2 | Both sides of this wall have their own charm. The choice is yours! |
| darkwood_beam_67 | $piece_darkwoodbeam67 |  | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | Intricate designs run along this support structure. |
| darkwood_roof_67 | $piece_darkwoodroof67 | Shingle Roof 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_icorner_67 | $piece_darkwoodrooficorner67 | Shingle Roof Inner Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_ocorner_67 | $piece_darkwoodroofocorner67 | Shingle Roof Outer Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| darkwood_roof_top_67 | $piece_darkwoodrooftop67 | Shingle Roof Ridge 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2, Tar x1 | This very steep roof is sure to keep the rain out, and look good doing it. |
| loot_deepNorth_Granary | $piece_chestbarrel | Barrel | Furniture | piece_workbench |  | Wood x10 | A barrel is good for storing lots of things, including food and drink. |
| loot_deepNorth_TimberHall | $piece_chestbarrel | Barrel | Furniture | piece_workbench |  | Wood x10 | A barrel is good for storing lots of things, including food and drink. |
| piece_bench_runed | $piece_bench_runed | Carved Bench | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x6, MooseHide x1 | A rustic yet comfortable place to sit. |
| piece_chair_runed | $piece_chair_runed | Carved Chair | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4, MooseSinew x1 | The shape of this chair might be simple, but its decorations are not. |
| piece_chest_grausten | $piece_chestgrausten | Grausten Chest | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Grausten x10, TrophyCharredMelee x5, FlametalNew x2 | Stone and metal are sure to keep your belongings safe. The charred skulls help… |
| piece_chest_warderobe | $piece_chestwarderobe | Wardrobe | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x10, Tar x2, BlackMetal x6 | An elegant place for storage. |
| piece_moose_throne | $piece_moose_throne | Antler Throne | Furniture | piece_workbench | Hammer | Frostwood x15, TrophyMoose x1, MooseHide x5 | Though rustic and sturdy, this throne is well suited even for the most noble of… |
| piece_snowlantern | $piece_snowlantern | Snow Lantern | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Snowball x8 | Adds a cosy touch to a wintry landscape. |
| piece_table_runed | $piece_table_runed | Long Carved Table | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x20, Tar x2, IronNails x20 | The story carved into this table is excellent to read during long feasts. |
| piece_table_runed_small | $piece_table_runed_small | Square Carved Table | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x6, Tar x1, IronNails x6 | A table for more intimate gatherings. |
| rug_moose | $piece_rug_moose | Moose Hide Carpet | Furniture | piece_workbench | Hammer | MooseHide x4 | This carpet is neatly sewn together, to ensure a large, soft surface. |
| rug_seal | $piece_rug_seal | Sealskin Rug | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | SealHide x4 | This rug is so soft, soft like innocence. |
| scale_halfwall_1x2 | $piece_scale_halfwall | Scalewood Half Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x1 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_quarterwall_1x1 | $piece_scale_quarterwall | Scalewood Quarter Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x1 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_2x2 | $piece_scale_wall | Scalewood Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26 | $piece_scale_26 | Scalewood Wall 26° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_flipped | $piece_scale_26_flipped | Scalewood Wall 26° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_upsidedown | $piece_scale_26_upsidedown | Scalewood Wall 26° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_26_upsidedown_flipped | $piece_scale_26_upsidedown_flipped | Scalewood Wall 26° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45 | $piece_scale_45 | Scalewood Wall 45° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_flipped | $piece_scale_45_flipped | Scalewood Wall 45° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_upsidedown | $piece_scale_45_upsidedown | Scalewood Wall 45° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_45_upsidedown_flipped | $piece_scale_45_upsidedown_flipped | Scalewood Wall 45° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67 | $piece_scale_67 | Scalewood Wall 67° Left | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_flipped | $piece_scale_67_flipped | Scalewood Wall 67° Right | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_upsidedown | $piece_scale_67_upsidedown | Scalewood Wall 67° Right (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| scale_wall_roof_67_upsidedown_flipped | $piece_scale_67_upsidedown_flipped | Scalewood Wall 67° Left (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | The overlapping wood of these walls provide excellent protection against the el… |
| stave_beam_26 | $piece_stavebeam26 | Timber Beam 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_2m | $piece_stavebeam2 | Timber Beam 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_45 | $piece_stavebeam45 | Timber Beam 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_4m | $piece_stavebeam4 | Timber Beam 4m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | These thick chunks of wood are sure to support your constructions. |
| stave_beam_67 | $piece_stavebeam67 | Timber Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_deco_beam_26 | $piece_stavedecobeam26 | Decorated Timber Beam 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_2m | $piece_stave_deco_beam_2m | Decorated Timber Beam 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_45 | $piece_stavedecobeam45 | Decorated Timber Beam 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_beam_67 | $piece_stavedecobeam67 | Decorated Timber Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_pole_2m | $piece_stave_deco_pole_2m | Decorated Timber Pole 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These supports add a tasteful and decorative touch. |
| stave_deco_wall_2x2 | $piece_deco_stave_wall | Lathed Timber Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | This wall won't do much to keep out the cold, but it's very pretty to look at. |
| stave_pole_2m | $piece_stave_pole_2m | Timber Pole 2m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_pole_4m | $piece_stave_pole_4m | Timber Pole 4m | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_2x2 | $piece_stave_wall | Timber Wall | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x4 | Heavy timber walls are good for keeping out the cold. |
| stave_wall_cross_26 | $piece_stavecross26 | Timber Roof Cross 26° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_cross_45 | $piece_stavecross45 | Timber Roof Cross 45° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| stave_wall_cross_67 | $piece_stavewallrooftop67 | Timber Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Frostwood x2 | These thick chunks of wood are sure to support your constructions. |
| wood_beam_67 | $piece_woodbeam67 | Wood Beam 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |
| wood_roof_67 | $piece_woodroof67 | Thatch Roof 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_icorner_67 | $piece_woodrooficorner67 | Thatch Roof Inner Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_ocorner_67 | $piece_woodroofocorner67 | Thatch Roof Outer Corner 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_roof_top_67 | $piece_woodrooftop67 | Thatch Roof Ridge 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A simple roof to keep the rain out, built at a very steep angle. |
| wood_wall_roof_67_a | $piece_woodwallroof67 | Wood Wall 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_67_upsidedown | $piece_woodwallroof67upsidedown | Wood Wall 67° (Inverted) | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | Walls are an important part of any house! |
| wood_wall_roof_top_67 | $piece_woodwallrooftop67 | Wood Roof Cross 67° | DeepNorth tab (Hammer) / Feasts (Feaster) | piece_workbench | Hammer | Wood x2 | A sturdy wooden support. |

The Hammer's PieceTable has a sixth tab whose label is the raw string `DEEPNORTH` (category 5); in the Feaster's table the same category number is labelled Feasts.

### 11g. Deep North items (dropped by Deep North creatures, or in a Deep North asset folder)

| prefab | token | English | type | folder | description |
|---|---|---|---|---|---|
| FishingBaitDeepNorth | $item_fishingbait_deepnorth | Frosty Fishing Bait | Ammo | materials | It's not very nutritious, so the only fish that'll take this bait are the ones that are used to jus… |
| ArmorDeepNorthHeavyChest | $item_chest_heavy_deepnorth | Breastplate of the Protector | Chest | armor | Fur and hide and metal all work in tandem to ward off an enemy's blows. |
| ArmorDeepNorthMageChest | $item_chest_mage_deepnorth | Robes of the Caller | Chest | armor | Gold trimmed robes, fit for only the most powerful of mages. |
| ArmorDeepNorthMediumChest | $item_chest_medium_deepnorth | Chestpiece of the Vanguard | Chest | armor | Expertly crafted leather armour, offering excellent protection without limiting a warrior's movemen… |
| CookedMooseMeat | $item_moose_meat_cooked | Cooked Moose Meat | Consumable | consumables | This meat is lean yet full of flavour. |
| CookedSealBlubber | $item_blubber_cooked | Cooked Seal Blubber | Consumable | consumables | A chewy meat, with an aftertaste of remorse. |
| FeastDeepNorth | $item_feastdeepnorth | Northern Morning Fare | Consumable |  |  |
| Lingonberry | $item_lingonberries | Lingonberries | Consumable | consumables | These tart berries can grow even in cold climates. |
| MooseKebab | $item_moosekebab | Meat In Bread | Consumable | consumables | A convenient meal, often favoured by travelling merchants. |
| MushroomJotunPuffs | $item_jotunpuffs | Jotun Puffs | Consumable | consumables | An invigorating mushroom that can be used for cooking. |
| Pukeberries | $item_pukeberries | Bukeperries | Consumable | consumables | Allows the consumer to quickly evacuate any misplaced meal and start anew. |
| SealSoup | $item_sealsoup | Seal Meat Soup | Consumable | consumables | A warm and tasty meal, best enjoyed on a cold day. |
| SmokedMooseMeat | $item_smokedmoosemeat | Smoked Moose Meat | Consumable | consumables | The smoke only adds to the wild flavour. |
| HelmetDNHeavy | $item_helmet_heavy_deepnorth | Helmet of the Protector | Helmet | helmets | Embellished with the wings of victory. |
| HelmetDNMage | $item_helmet_mage_deepnorth | Headdress of the Caller | Helmet | helmets | The spirits of the land come as you beckon. Are they fooled by your disguise? |
| HelmetDNMediumHood | $item_helmet_medium_deepnorth | Hood of the Vanguard | Helmet | helmets | Something to protect your neck from the elements as well as from the sharp teeth of the enemy. |
| ArmorDeepNorthHeavylegs | $item_legs_heavy_deepnorth | Trousers of the Protector | Legs | armor | Heavy boots and trousers, to keep you warm as you trudge through deep snow. |
| ArmorDeepNorthMagelegs | $item_legs_mage_deepnorth | Trousers of the Caller | Legs | armor | Tight legwraps to keep the cold from touching your skin. |
| ArmorDeepNorthMediumlegs | $item_legs_medium_deepnorth | Trousers of the Vanguard | Legs | armor | Warm trousers suitable for a cold climate. The boots offer excellent grip in the icy terrain. |
| AncientCoin | $item_ancientcoin | Ancient Coin | Material | valuables | A relic of a lost age. Its surface still bears the trace of mysterious symbols. |
| AncientGemstoneBlack | $item_ancientgemstone_black | Draumyx | Material | valuables | A dark, opaque gem with a smooth and polished surface. |
| AncientGemstoneGreen | $item_ancientgemstone_green | Grimvarn | Material | valuables | A striking green gem, the colour reminiscent of deep forests. |
| AncientGemstoneOrange | $item_ancientgemstone_orange | Solryth | Material | valuables | A vibrant orange stone, like a summer sunset. |
| AncientGemstonePurple | $item_ancientgemstone_purple | Veydris | Material | valuables | A rich purple stone, suitable for royalty. |
| BarkaBranch | $item_barkabranch | Frozen Branch | Material | materials | This piece of wood was once animated and alive. Still a strange, magical air clings to it. |
| BoneFragments | $item_bonefragments | Bone Fragments | Material | materials | A pile of shattered bones. |
| Coins | $item_coins | Coins | Material | valuables | <color=yellow>Valuable</color> |
| CrownJewel | $item_crownjewel | Crown Jewel | Material | materials | A strange power surges within this gem, unlike anything you've felt before. |
| ElakingHairBundle | $item_elakinghairbundle | Elaking Hair Bundle | Material | materials | The fur is dense, coarse, and surprisingly clean. |
| FeastDeepNorth_Material | $item_feastdeepnorth | Northern Morning Fare | Material | materials | Warming and filling, this meal will sustain you even during the coldest of days. Porridge and panca… |
| FrostCore | $item_frostcore | Frostcore | Material | consumables | Terribly cold to the touch, filled with frozen energy. |
| FrozenFuel | $item_frozenfuel | Liquid Frost | Material | materials | Magic has infused this ice, turning it into something else entirely. |
| FrozenKingDrop | $item_frozenking_drop | Sacrificial Blood | Material | misc | The last essence of an end once foretold. |
| GreydwarfEye | $item_greydwarfeye | Greydwarf Eye | Material | materials | The milky eyeball of a Greydwarf. |
| Ice | $item_ice | Ice | Material | materials | So cold... |
| LastBossGate_RuneTile | $item_runetile |  | Material | DeepNorth/LastBossGate |  |
| Leatherstraps | $item_leatherstraps | Leather Straps | Material | materials | A sturdy yet flexible material. |
| MemorialCoal | $item_memorialcoal | Memorial Coal | Material | materials | Somewhere deep within the hot, hazy glow, you can almost see an old memory play out... |
| MoldArmorGoldChest | $item_mold_armor_gold_chest | Mould: Breastplate of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorGoldHelmet | $item_mold_armor_gold_helmet | Mould: Helmet of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorGoldLegs | $item_mold_armor_gold_legs | Mould: Trousers of the Protector | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageChest | $item_mold_armor_mage_chest | Mould: Robes of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageHelmet | $item_mold_armor_mage_helmet | Mould: Headdress of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMageLegs | $item_mold_armor_mage_legs | Mould: Trousers of the Caller | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMediumHelmet | $item_mold_armor_medium_helmet | Mould: Hood of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmorMediumLegs | $item_mold_armor_medium_legs | Mould: Trousers of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldArmormediumChest | $item_mold_armor_medium_chest | Mould: Chestpiece of the Vanguard | Material | consumables | Filled with the right material, this mould will create a spectacular piece of armour. |
| MoldKeys | $item_mold_keys | Mould: Intricate Key | Material | consumables | Filled with the right material, this mould will create a powerful key. |
| MoleClaws | $item_moleclaws | Long Claws | Material | materials | A lethal weapon, if one can hold them without getting cut. |
| MooseHide | $item_moosehide | Moose Hide | Material | materials | This fur is perfectly adapted to northern climates. |
| MooseMeat | $item_moose_meat | Moose Meat | Material | consumables | This meat is sure to provide a hearty meal once cooked. |
| MooseSinew | $item_moosesinew | Moose Sinew | Material | materials | Tough and hardy, this is sure to come in handy. |
| NornThread | $item_nornthread | Nornathread | Material | materials | Don't let the delicate strands fool you. These threads are spun from the power of the world tree it… |
| OozeMork | $item_ooze_mork | Dead Pulp | Material | materials | It's best not to think about what this consists of. |
| OrbFrostFire | $item_orbfrostfire | Frostfire Essence | Material | materials | Somehow both hot and cold to the touch. |
| OrbThunderBlood | $item_orbthunderblood | Thunderblood Essence | Material | materials | Unstable, erratic and...alive? |
| Resin | $item_resin | Resin | Material | materials | Sticky tree resin which insulates well. If put to the flame it burns slow and steady. |
| SealBlubber | $item_blubber | Seal Blubber | Material | consumables | The insulating fat of a creature adapted to the northern waters. |
| SealHide | $item_sealhide | Seal Pelt | Material | materials | The thick fur helps the animal stay both warm and dry. |
| SpiceDeepNorth | $item_spicedeepnorth | Seasoning of the Gourd | Material | materials | Using cinnamon bark and ginger root, with a touch of nutmeg, the herbalist has travelled far to cre… |
| Wood | $item_wood | Wood | Material | materials | Good, strong wood to build with. |
| WrithanRoots | $item_writhanroots | Writhan Roots | Material | materials | The gnarled body parts of a strange, offputting creature. |
| BloodGoldKey | $item_bloodgoldkey | Intricate Key | Misc | misc | If there's a key, then surely there must be a lock. |
| SaddleMoose | $item_saddlemoose | Moose Saddle | Misc | tools | A moose is a noble creature, but with a saddle this fine it might just allow a rider. |
| Axe1h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| Axe2h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| AxeJotunBane | $item_axe_jotunbane | Jotun Bane | OneHanded | weapons | Not even the giants of old could weather the poisonous bite of this weapon. |
| Barka_Backslam | Barka Backslam |  | OneHanded | attacks |  |
| Barka_HeavySwing | Dragon claw left |  | OneHanded | attacks |  |
| Barka_SlamDrive | Dragon claw left |  | OneHanded | attacks |  |
| Barka_WhipFlurry | Dragon claw left |  | OneHanded | attacks |  |
| Barka_WhipSlam | Dragon claw left |  | OneHanded | attacks |  |
| BombBlob_Morkhalla | $item_bombblob_morkhalla | Blob Bomb: Pulp | OneHanded | weapons | A dungeon is where the blob is. |
| DvergerArbalest_shootDeepNorth | $item_crossbow_arbalest | Arbalest | OneHanded | Attacks | A slow but powerful weapon. |
| Dverger_meleeDeepNorth | Club |  | OneHanded | Attacks | A crude but useful weapon. |
| ElakingMole_AttackClaw | jaws |  | OneHanded | attacks |  |
| ElakingMole_AttackClaw2 | jaws |  | OneHanded | attacks |  |
| ElakingMole_AttackSandcloud | StagAttack2 |  | OneHanded | attacks |  |
| Elaking_AttackClaw | jaws |  | OneHanded | attacks |  |
| Elaking_AttackLantern | Torch |  | OneHanded | misc | It brings light and warmth, drives back the darkness. |
| FrozenKing_ChainFlurry | FrozenKing ChainFlurry |  | OneHanded | attacks |  |
| FrozenKing_ChainRush | FrozenKing ChainRush |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_L | FrozenKing ChainSlam L |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_R | FrozenKing ChainSlam R |  | OneHanded | attacks |  |
| FrozenKing_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | attacks |  |
| FrozenKing_ChainSweep_L | FrozenKing ChainSweep L |  | OneHanded | attacks |  |
| FrozenKing_ChainSweep_R | FrozenKing ChainSweep R |  | OneHanded | attacks |  |
| FrozenKing_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | attacks |  |
| FrozenKing_DoubleSweep | FrozenKing DoubleSweep |  | OneHanded | attacks |  |
| FrozenKing_P2_Summon_Bonemass | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Eikthyr | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Elder | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Fader | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Moder | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Queen | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P2_Summon_Yagluth | Fader Roar |  | OneHanded | attacks/Phase 2 |  |
| FrozenKing_P3_ChainSlam_L_double | FrozenKing ChainSlam L double |  | OneHanded | attacks |  |
| FrozenKing_P3_ChainSlam_R_double | FrozenKing ChainSlam R double |  | OneHanded | attacks |  |
| FrozenKing_P3_ChainWhirl | FrozenKing ChainWhirl |  | OneHanded | attacks |  |
| FrozenKing_Punch_AOE | FrozenKing Punch AOE |  | OneHanded | attacks |  |
| FrozenKing_SpikeRain | spawn |  | OneHanded | attacks |  |
| FrozenKing_tendrilspawn | spawn |  | OneHanded | attacks |  |
| GoblinClubDeepNorth | Club |  | OneHanded | misc | A crude but useful weapon. |
| GoblinSpearDeepNorth | Flint spear |  | OneHanded | misc |  |
| GoblinSwordDeepNorth | Bronze sword |  | OneHanded | misc | Blood-drinker. A thirsty friend. |
| GoblinTorchDeepNorth | Torch |  | OneHanded | misc | It brings light and warmth, drives back the darkness. |
| Greydwarf_attack_frozen | jaws |  | OneHanded | misc |  |
| Greydwarf_shaman_attack_frozen | shaman attack |  | OneHanded | misc |  |
| Greydwarf_shaman_heal_frozen | heal |  | OneHanded | misc |  |
| Greydwarf_throw_frozen | throw stone |  | OneHanded | misc |  |
| JotunWarriorSword2h | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| JotunWitch_attack_lightningbolt | fireballattack |  | OneHanded | attacks |  |
| JotunWitch_attack_magicblast | StagAttack2 |  | OneHanded | attacks |  |
| Snowball | $item_snowball | Snowball | OneHanded | weapons | Looks like a perfect thing to throw... |
| Sword2h_JotunWarrior | Club |  | OneHanded | model/weapons | A crude but useful weapon. |
| aspect_Eikthyr_antler | StagAttack1 |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Eikthyr_charge | StagAttack2 |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Eikthyr_stomp | slap |  | OneHanded | BossAspects/Attacks/Eikthyr |  |
| aspect_Fader_Bite | Fader Bite |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Claw_Left | Fader Claw Left |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Claw_Right | Fader Claw Right |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Fissure | Fader Fissure |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Flamebreath | Fader Firebreath |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_Spin | Fader Spin |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_Fader_WallOfFire | Fader Wall of Fire |  | OneHanded | BossAspects/Attacks/Fader |  |
| aspect_GoblinKing_Beam | dragon breath |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_GoblinKing_Nova | slap |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_GoblinKing_Taunt | scream |  | OneHanded | BossAspects/Attacks/Yagluth |  |
| aspect_SeekerQueen_Bite | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_PierceAOE | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Rush | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Slap | slap |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_SeekerQueen_Spit | dragon breath |  | OneHanded | BossAspects/Attacks/Queen |  |
| aspect_bonemass_attack_aoe | heal |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_bonemass_attack_punch | slap |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_bonemass_attack_throw | slime throw |  | OneHanded | BossAspects/Attacks/Bonemass |  |
| aspect_dragon_bite | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_claw_left | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_claw_right | Dragon claw left |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_coldbreath | dragon breath |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_spit_shotgun | cold ball |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_dragon_taunt | scream |  | OneHanded | BossAspects/Attacks/Moder |  |
| aspect_gd_king_rootspawn | spawn |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_scream | scream |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_shoot | shaman attack |  | OneHanded | BossAspects/Attacks/Elder |  |
| aspect_gd_king_stomp | jaws |  | OneHanded | BossAspects/Attacks/Elder |  |
| blobmork_attack_aoe | fart |  | OneHanded | misc |  |
| frysling_snowball_attack | fireballattack |  | OneHanded | attacks |  |
| moose_hooves | moose horns |  | OneHanded | attacks |  |
| moose_horns | moose horns |  | OneHanded | attacks |  |
| moose_horns_sweep | moose horns |  | OneHanded | attacks |  |
| skeleton_mace_DeepNorth | Dragur axe |  | OneHanded | weapons |  |
| spiritmoose_hooves | moose horns |  | OneHanded | attacks |  |
| spiritmoose_horns | moose horns |  | OneHanded | attacks |  |
| spiritmoose_horns_sweep | moose horns |  | OneHanded | attacks |  |
| tendril_attack | Dragur axe |  | OneHanded | attacks |  |
| writhan_bite | writhan bite |  | OneHanded | attacks |  |
| writhan_explode_aoe |  |  | OneHanded | attacks |  |
| Axe1h_JotunWarrior 1 | Club |  | Shield | model/weapons | A crude but useful weapon. |
| CapeDeepNorth | $item_cape_deepnorth | Moose Hide Cape | Shoulder | armor | A warm cape with fine details of spun gold. |
| CapeDeepNorthMage | $item_cape_deepnorth_mage | Cape of the Caller | Shoulder | armor | A strange magic is woven into this cape, making it both light and warm. |
| TrophyBarka | $item_trophy_barka | Barka Trophy | Trophy | trophies | The frozen head of a once-living tree. |
| TrophyBlob_Morkhalla | $item_trophy_blob_morkhalla | Pulp Trophy | Trophy | trophies | Remains of unfortunate adventurers, digested and jellified over time. |
| TrophyDvergr | $item_trophy_dvergr | Dvergr Trophy | Trophy | trophies | It's frankly a little troubling that you would consider hanging these on your wall... |
| TrophyElaking | $item_trophy_elaking | Elaking Trophy | Trophy | trophies | Mean little eyes stare back at you. |
| TrophyGreydwarf | $item_trophy_greydwarf | Greydwarf Trophy | Trophy | trophies | The mossy, severed head of a Greydwarf. |
| TrophyGreydwarfShaman | $item_trophy_greydwarfshaman | Greydwarf Shaman Trophy | Trophy | trophies | It may try to come back so be sure to prune any new shoots... |
| TrophyJotunWarrior | $item_trophy_jotunwarrior | Krigen Trophy | Trophy | trophies | Once a mighty warrior, now nought but a husk remains. |
| TrophyJotunWitch | $item_trophy_jotunwitch | Hexen Trophy | Trophy | trophies | Before her death, her eyes sparked with magic. Now they're empty and void. |
| TrophyMole | $item_trophy_mole | Eyeless One Trophy | Trophy | trophies | Getting slashed by these claws would be very unpleasant. |
| TrophyMoose | $item_trophy_moose | Moose Trophy | Trophy | trophies | The mighty ruler of the northern forests. |
| TrophySeal | $item_trophy_seal | Seal Trophy | Trophy | trophies | This animal never did any harm, yet it met an untimely end. |
| TrophySkeleton | $item_trophy_skeleton | Skeleton Trophy | Trophy | trophies | The expressionless grin of this skull reminds you of the inevitability of death. |
| TrophyWrithan | $item_trophy_writhan | Writhan Trophy | Trophy | trophies | Tough dead, it should probably be handled delicately. |
| Elaking_AttackJump | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_dodger | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior1HAxe_attack_slashdw | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HAxe_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior2HSword_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_charge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_cleave | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_slash | Charred Sword |  | TwoHanded | attacks |  |
| JotunWarrior_attack_sword | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge2 | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge_down | Charred Sword |  | TwoHanded | attacks |  |
| JotunWitch_attack_dodge_up | Charred Sword |  | TwoHanded | attacks |  |

Left out: `FW_*` and `SP_*` items (copies of player gear worn by Fallen Warriors and Shadows) and `JotunHair*` (Jotun visual attachments).

### 11h. Deep North raids

| event | biome | world keys req | player keys | start msg | end msg | spawns |
|---|---|---|---|---|---|---|
| fimbulvinter | All |  |  |  |  |  |
| boss_frozenking | All |  |  |  |  |  |
| army_elakingar | DeepNorth | elakingmole_defeated |  | They emerge from below... | They return to their burrows... | Elaking, ElakingLantern, ElakingMole |
| army_jotuns | Meadows, Swamp, Mountain, BlackForest, Plains, DeepNorth, Mistlands | jotun_killed |  | The Jotun have found you | The Jotun withdraw | Elaking, JotunWarrior |

`fimbulvinter` has no spawns of its own: the four `Fimbulvinter - *` spawners in the Deep North spawn list (Jotun warriors, witches, Elakingar, meteors; biome All) do the work, tied to the Orb of Fimbulvinter (`$fimbulvinterorb`). The trigger is physical: breaking the orb at a Mörkhalla end room (`morkhalla_endcap01/02`, `TriggerPersistentEventOnDestroy` -> persistent event `jotun_invasion`, centre text "The Jotun Advance") starts it, and destroying a `BlackIce_Core` stops it ("The Jotun Retreat"); `FimbulLocation01` is a disabled location of the same set. Deep North weather: Twilight_Snow, Twilight_Clear, Twilight_SnowStorm, plus JotunInvasion_<biome> variants for the invasion, and Morkhalla, TheHollow, DN_Bossroom interiors.

### 11i. Deep North dreams (full text)

- **dream_deepnorth01** (needs defeated_fader; not after -; chance 0.1): In your dream, you stand naked in the middle of a pool of water so clear it cannot be seen, only felt as a swaying girdle around your hips. Three figures stand at the water's edge, watching you in silence. When the lead figure touches the water's surface with her toe, the ripple dissolves the dream and you wake.
- **dream_deepnorth02** (needs -; not after -; chance 0.1): You slide down the tunnel of a fretful dream into an absence so complete that no words will cling to it…
- **dream_deepnorth03** (needs defeated_fader; not after -; chance 0.1): You dream of a hall in winter, glittering with light and laughter in the midst of a snowy night. As you approach, the lights twist into tall flames and the laughter into screams. You run towards the hall but it sinks hissing into the snow as you approach.
- **dream_deepnorth04** (needs -; not after -; chance 0.1): You run down an endless hill, your heart thumping for joy within your chest. Two dark shapes fly alongside you and the beat of their wings lingers into your waking hours.
- **dream_deepnorth05** (needs -; not after defeated_frozenking; chance 0.1): You see a child's face, laughing and crying at the same time. The eyes are clear and blue behind their tears, the mouth is a tumble of tiny teeth. In the dream, he tells you something, but the words are gone when you wake.
- **dream_deepnorth06** (needs -; not after -; chance 0.1): In your dream there is a door without pin or bolt. You feel it would open with a push but your body is frozen in a swirl of cold wind. Behind the door are the voices of the ones you love but cannot name. You strain forward into empty wakefulness.
- **dream_deepnorth07** (needs -; not after -; chance 0.1): The ghosts of all those who have died at your hand come to you in your sleep and linger at the edges of your dreams. They want nothing, need nothing, say nothing.
- **dream_deepnorth08** (needs -; not after defeated_frozenking; chance 0.1): You sink through layers of sleep to a vast chamber where a dark shape hunches beneath a snarl of heavy chains. As it shifts to face you, the blue orbs of its eyes pierce the dream and you wake, drenched in sweat.
- **dream_deepnorth09** (needs defeated_frozenking; not after -; chance 0.1): You remember nothing of this night's dream but its rainbow mood lifts you into the day with a smile on your lips.
- **dream_deepnorth10** (needs defeated_frozenking; not after -; chance 0.1): Odin visits you in your dream and offers you a horn filled to the brim with a frothing draught. You drink deep and greet the new day feeling refreshed and alive.

### 11j. Deep North lore (full text)

- **lore_deepnorth_hervor01**: The Deep North. The final obstacle before I can prove myself worthy. / / The trials have been many, and so have the deaths. I have lost friends dear to me. / / But at last I am here.
- **lore_deepnorth_hervor02**: It seems peaceful enough. So far, the wildlife doesn’t appear to care much for my presence here. / / I should be able to gather some valuable resources from them if I need to. It’s my survival or theirs, after all.
- **lore_deepnorth_hervor03**: I have to be on my guard. Few places have been peaceful since I first arrived here, and it would be foolish to believe these lands to be different. / / No matter how well I fight, I might still be overcome by the inevitable cold of the place. / / I’d best find some shelter.
- **lore_deepnorth_hervor04**: So far I’ve been left alone. Despite that, I can’t shake the feeling that something is watching me.
- **lore_deepnorth_hervor05**: I found an old building, but it was deserted. It’s hard to say for how long, but I don’t believe I’ll find any friends here.
- **lore_deepnorth_hervor06**: I don’t like to think about what could have happened, had my axe not been as sharp nor my senses as keen. After all, there is no one left to come to my aid. / / I may have lost my companions. But it is for their sake that I must continue. The ruler of these lands must die.
- **lore_deepnorth_hervor07**: It’s strange. I can’t help but feel like there should be people here, just waiting to welcome me. / / I think the winters looked like this when I was a child, and any moment my father should be welcoming me home. But he isn’t here. No one is.
- **lore_deepnorth_hervor08**: I have been haunted by dreams lately, in which I am a raven. On great, black wings I soar across this world that I’ve come to know, but it is not the same. / / The landscape has gone from green and fertile to cold and barren. Perhaps it is just this frozen north that has been getting to me. Yet...what if it’s not?
- **lore_deepnorth_hervor09**: I know now where I need to go. The Ancestors have pointed me in the direction of the one who rules these lands, the one they tried to keep in chains but whose power now shackles the very world. / / If I can just slay this foe, this hateful being, I will have proven myself once and for all.
- **lore_deepnorth_memorial1**: The mighty ones who came before us rest here. / Tread warily, as their slumber may yet be disturbed.
- **lore_deepnorth_memorial2**: Death comes for all, in the end, even the mightiest of warriors. / One day you may be counted amongst them.
- **lore_deepnorth_memorial3**: Let us honour our ancestors, who carved this world that we now live in. / And may they deem us worthy.
- **lore_deepnorth_memorial_description**: Ancestral Memorial
- **lore_deepnorth_memorial_label**: Lore: Ancestral Memorial
- **lore_deepnorth_random01**: At first this seemed a good home for Ulf, who liked the cold and the snow. Then came the mean little creatures, intent on dragging me below, and I was forced to reconsider.
- **lore_deepnorth_random02**: If what the raven says is true, there is only one more forsaken I must slay. But what comes after? Will my son truly be returned to me, or I to him? / / Astrid raised this stone for her son, should he follow in her footsteps before she gets to see him again.
- **lore_deepnorth_random03**: These lands seemed so familiar, awakening memories buried deep within our minds. We should have known they were not all that they seemed.
- **lore_deepnorth_random04**: Heed the words of Kata, who once thought the trolls of the forest were mighty foes. Their kin in these lands are tall as the tallest of towers, and harder than the hardest of stones, but those who manage to slay them stand to reap a glimmering reward.
- **lore_deepnorth_random05**: When we first arrived here, this place was rich and prosperous. But the winters are colder and longer now, and we fear they might soon take over altogether. / / Yet, here we have built our homes and here we have grown our roots, so here it is that we shall stay.
- **lore_deepnorth_random06**: In the farthest north / Where magic dances in the skies / Offer to call forth / What just below the surface lies / / Let the coals burn bright / Ancestral wisdom waits for those / Who should stand to fight / Their elders' mighty blades and bows
- **lore_deepnorth_random07**: Beware of false trees! Chiming icicles and creaking branches might not just be moving in the wind...
- **lore_deepnorth_random08**: Signe raised this stone in memory of Eigil, who fell in battle. He had yet to learn that not everything here is as frozen as it first seems.
- **lore_deepnorth_random09**: Buried beneath the snow, deep within the north, / bonds harden like frozen earth / as truth begins to thaw.
- **npc_munin_deepnorth_general01**: This is a place of transformation. Certain creatures find a new purpose here, becoming something more than they once were. Are you one of these creatures, I wonder?
- **npc_munin_deepnorth_general02**: Kraa! Those who first ventured here did not know about the underground thieves. If they had known, they would certainly have chosen better places to build their homesteads.
- **npc_munin_deepnorth_general03**: Watch these lands for memorials in the honour of the warriors who came before you. They may bestow a boon upon you, should you prove yourself worthy in their eyes.
- **npc_munin_deepnorth_general04**: Long before the Allfather summoned you, others were tasked to guard this world. Even though many years have gone by since their passing, they still can't seem to find peace. Be kind, and do not disturb them.
- **npc_munin_deepnorth_morkborg**: Kraa! Strange folk dwell here, created from the very evil itself...
- **npc_munin_deepnorth_ship**: Long ago, others attempted to sail to these lands. It appears they made it here, but that there was no way for them to make the return journey...

## 12. Models: world props and character folders (from the bundle manifest)

Every prefab path under `Assets/world/Props/` and `Assets/Characters/` in `manifest_extended`, grouped by folder (fx/sfx/attack sub-prefabs left out). These are spawnable by prefab name only if they are also in ZNetScene; most props with a `Piece`/`Destructible`/`WearNTear` are.

| folder | prefabs | names |
|---|---|---|
| Characters/Abomination | 1 | Abomination |
| Characters/Abomination/Misc | 3 | Abomination_attack1, Abomination_attack2, Abomination_attack3 |
| Characters/Asksvin | 2 | Asksvin, Asksvin_hatchling |
| Characters/Barka | 1 | Barka |
| Characters/Bat | 2 | Bat, Bat_Swamp |
| Characters/Bjorn | 7 | Bjorn, Bjorn_ragdoll, Bjorn_sleeping, Bjorn_spiritcaller, Spawner_Bjorn_sleeping, Unbjorn, Unbjorn_ragdoll |
| Characters/Blob | 15 | Blob, BlobAspect, BlobElite, BlobFrost, BlobLava, BlobLava_explosion, BlobMork, BlobMorkMini, BlobTar, Spawner_Blob, Spawner_BlobElite, Spawner_BlobTar, Spawner_BlobTar_respawn_30, digg_blobLavaExplosion, vfx_BigBlob_destroyed |
| Characters/Blob/misc | 8 | blobLava_attack_aoe, blob_aoe, blob_attack_aoe, blob_frost_attack_aoe, blobelite_attack_aoe, blobmork_attack_aoe, blobtar_attack, blobtar_projectile_tarball |
| Characters/Boar | 4 | Boar, Boar_piggy, Boar_spiritcaller, Spawner_Boar |
| Characters/BogWitch | 1 | BogWitch |
| Characters/Bonemass | 1 | Bonemass |
| Characters/Bonemass/misc | 7 | bonemass_aoe, bonemass_attack_aoe, bonemass_attack_punch, bonemass_attack_spawn, bonemass_attack_throw, bonemass_spawn, bonemass_throw_projectile |
| Characters/BonemawSerpent | 1 | BonemawSerpent |
| Characters/Chicken | 4 | Chicken, Hen, Spawner_Chicken, Spawner_Hen |
| Characters/Deathsquito | 1 | Deathsquito |
| Characters/Deer | 2 | Deer, Deer_White |
| Characters/Deer/misc | 1 | DeerGodExplosion |
| Characters/Dragon | 1 | Dragon |
| Characters/Draugr | 14 | Draugr, Draugr_Elite, Draugr_Elite_sleeping, Draugr_Ranged, Draugr_Ranged_sleeping, Draugr_sleeping, Spawner_Draugr, Spawner_DraugrPile, Spawner_Draugr_Elite, Spawner_Draugr_Noise, Spawner_Draugr_Ranged, Spawner_Draugr_Ranged_Noise, Spawner_Draugr_respawn_30, Spawner_Kvastur |
| Characters/Dverger | 14 | Dverger, DvergerAshlands, DvergerDeepNorth, DvergerMage, DvergerMageFire, DvergerMageIce, DvergerMageSupport, DvergerTest, Mistile, Spawner_DvergerArbalest, Spawner_DvergerAshlands, Spawner_DvergerDeepNorth, Spawner_DvergerMage, Spawner_DvergerRandom |
| Characters/Dverger/Fx | 13 | Dverger_ragdoll, fx_DvergerMage_Fire_hit, fx_DvergerMage_Fire_start, fx_DvergerMage_Ice_hit, fx_DvergerMage_MistileSpawn, fx_DvergerMage_Mistile_attack, fx_DvergerMage_Mistile_die, fx_DvergerMage_Nova_ring, fx_DvergerMage_Nova_start, fx_DvergerMage_Support_hit, fx_DvergerMage_Support_start, fx_Dverger_death, fx_Dverger_hit |
| Characters/Dverger/Gear | 14 | DvergerArbalest, DvergerHairFemale, DvergerHairFemale_Redhair, DvergerHairMale, DvergerHairMale_Redbeard, DvergerStaffFire, DvergerStaffHeal, DvergerStaffIce, DvergerStaffSupport, DvergerSuitArbalest, DvergerSuitArbalest_Ashlands, DvergerSuitFire, DvergerSuitIce, DvergerSuitSupport |
| Characters/Eikthyr | 1 | Eikthyr |
| Characters/Elaking | 6 | Elaking, ElakingLantern, Spawner_Hole, Spawner_Hole_double, vfx_HoleSpawner_destruction, vfx_HoleSpawner_double_destruction |
| Characters/ElakingMole | 2 | ElakingMole, Spawner_ElakingMole_Wakeup |
| Characters/Fader | 1 | Fader |
| Characters/FallenValkyrie | 2 | FallenValkyrie, Spawner_FallenValkyrie |
| Characters/FallenWarrior | 2 | FallenWarrior, ShadowPerson |
| Characters/Fenring | 8 | Fenring, Fenring_Cultist, Fenring_Cultist_Hildir, Fenring_Cultist_Hildir_nochest, Spawner_Cultist, Spawner_Cultist_Hildir, Spawner_Cultist_Hildir_bossroom, Spawner_Fenring |
| Characters/Frostwisp | 2 | FrostWisp, FrostWisp_Storm |
| Characters/FrozenKing | 4 | FrozenKing, FrozenKing_p2, FrozenKing_p3, Tendril_back |
| Characters/FrozenKing/BossAspects | 8 | Aspect_Bonemass, Aspect_Eikthyr, Aspect_Elder, Aspect_Fader, Aspect_Moder, Aspect_SeekerQueen, Aspect_Yagluth, aspect_aoe_explosion |
| Characters/Frysling | 3 | Frysling, Spawner_Frysling, Spawner_Frysling_respawn_30 |
| Characters/Ghost | 7 | Ghost, Ghost_Void, Ghost_old, Ghost_sleeping, Spawner_Ghost, Spawner_Ghost_Void, Spawner_Ghost_sleeping |
| Characters/Ghost/misc | 1 | Ghost_attack |
| Characters/Gjall | 1 | Gjall |
| Characters/Goblin | 8 | Goblin, GoblinArcher, GoblinDeepNorth, Goblin_Gem, Spawner_Goblin, Spawner_GoblinArcher, Spawner_GoblinDeepNorth, Spawner_ShadowPerson |
| Characters/Goblin/misc | 16 | Elaking_AttackLantern, GoblinArmband, GoblinClub, GoblinClubDeepNorth, GoblinHelmet, GoblinLegband, GoblinLoin, GoblinShoulders, GoblinSpear, GoblinSpearDeepNorth, GoblinSpearDeepNorth_projectile, GoblinSpear_projectile, GoblinSword, GoblinSwordDeepNorth, GoblinTorch, GoblinTorchDeepNorth |
| Characters/GoblinBrute | 3 | GoblinBrute, Spawner_GoblinBrute, Spawner_GoblinBrute_Hildir |
| Characters/GoblinBrute/misc | 6 | GoblinBrute_ArmGuard, GoblinBrute_Backbones, GoblinBrute_ExecutionerCap, GoblinBrute_HipCloth, GoblinBrute_LegBones, GoblinBrute_ShoulderGuard |
| Characters/GoblinBruteBros | 6 | GoblinBruteBros, GoblinBruteBros_nochest, GoblinBrute_Hildir, GoblinShaman_Hildir, GoblinShaman_Hildir_nochest, GoblinShaman_Staff_Hildir |
| Characters/GoblinKing | 1 | GoblinKing |
| Characters/GoblinShaman | 2 | GoblinShaman, Spawner_GoblinShaman |
| Characters/GoblinShaman/misc | 4 | GoblinShaman_Headdress_antlers, GoblinShaman_Headdress_feathers, GoblinShaman_Staff_Bones, GoblinShaman_Staff_Feathers |
| Characters/GreyDwarf | 13 | Greydwarf, Greydwarf_Elite, Greydwarf_Frozen, Greydwarf_Shaman, Greydwarf_Shaman_Frozen, Greyling, Spawner_Greydwarf, Spawner_Greydwarf_Elite, Spawner_Greydwarf_Shaman, Spawner_Greydwarf_Surprise, Spawner_Location_Elite, Spawner_Location_Greydwarf, Spawner_Location_Shaman |
| Characters/GreyDwarf/misc | 16 | Greydwarf_attack, Greydwarf_attack_frozen, Greydwarf_elite_attack, Greydwarf_shaman_attack, Greydwarf_shaman_attack_frozen, Greydwarf_shaman_heal, Greydwarf_shaman_heal_frozen, Greydwarf_throw, Greydwarf_throw_frozen, Greydwarf_throw_projectile, Greydwarf_throw_projectile_frozen, Greyling_attack, shaman_attack_aoe, shaman_attack_aoe_frozen, shaman_heal_aoe, shaman_heal_aoe_frozen |
| Characters/Greydwarf_king | 3 | Aspect_TentaRoot, TentaRoot, gd_king |
| Characters/Greydwarf_king/TentaRoots | 1 | TentaRoot_wild |
| Characters/Greydwarf_king/misc | 8 | gd_king_punch, gd_king_rootspawn, gd_king_scream, gd_king_shoot, gd_king_stomp, gdking_root_projectile, spawn_roots, tentaroot_attack |
| Characters/Hare | 1 | Hare |
| Characters/Hare/Fx | 2 | Hare_ragdoll, fx_hare_death |
| Characters/Hatchling | 2 | Hatchling, Spawner_Hatchling |
| Characters/Hildir | 1 | Hildir |
| Characters/Hive | 1 | Hive |
| Characters/Jotnar | 6 | JotunWarrior, JotunWarriorDualWield, JotunWitch, Spawner_JotunDualWield, Spawner_JotunWarrior, Spawner_JotunWitch |
| Characters/Jotnar/gear | 9 | JotunHairFemale, JotunHairMale, JotunHairMale2, JotunHairMale3, JotunHairMale4, JotunHairMale5, JotunHairMale6, JotunHairMale7, JotunHairMale8 |
| Characters/Kvastur | 4 | BogWitchKvastur, BogWitchKvastur_attack, Spawner_BogWitchKvastur_respawn_30, TrophyKvastur |
| Characters/LavaRock | 2 | LavaRock, projectile_lavaRock |
| Characters/Leech | 3 | Leech, Leech_cave, Spawner_Leech_cave |
| Characters/Leviathan | 2 | Leviathan, LeviathanLava |
| Characters/Lox | 4 | Halstein, HildirsLox, Lox, Lox_Calf |
| Characters/Meteor | 3 | projectile_FimbulvinterMeteor, projectile_ashlandmeteor, projectile_ashlandmeteor2 |
| Characters/Morgen | 4 | Morgen, Morgen_NonSleeping, Spawner_Morgen, Spawner_Morgen_wakeup |
| Characters/Neck | 1 | Neck |
| Characters/Odin | 2 | odin, vfx_odin_despawn |
| Characters/Player | 2 | Player, Player_tombstone |
| Characters/Raven | 4 | GuidePoint, Hugin, Munin, Ravens |
| Characters/Seeker | 4 | Seeker, SeekerBrood, Spawner_Seeker, Spawner_Seeker_respawn_240 |
| Characters/SeekerBrute | 3 | SeekerBrute, Spawner_SeekerBrute, Spawner_SeekerBrute_respawn_240 |
| Characters/SeekerQueen | 1 | SeekerQueen |
| Characters/SeekerQueen/spawnhole | 3 | TriggerSpawner_Brood, TriggerSpawner_Seeker, fx_seeker_spawn |
| Characters/Serpent | 1 | Serpent |
| Characters/Serpent/misc | 2 | Serpent_attack, Serpent_taunt |
| Characters/Skeleton | 29 | BonePileSpawner, BonePileSpawner_swamp, Skeleton, Skeleton_DeepNorth, Skeleton_Friendly, Skeleton_Hildir, Skeleton_Hildir_nochest, Skeleton_Meadows, Skeleton_Meadows_noarcher, Skeleton_Mountains, Skeleton_Mountains_noarcher, Skeleton_NoArcher, Skeleton_Poison, Skeleton_Swamps, Skeleton_Swamps_noarcher, Skeleton_aspect, Spawner_Skeleton, Spawner_Skeleton_Meadows … |
| Characters/StoneGolem | 2 | Spawner_StoneGolem, StoneGolem |
| Characters/StoneGolem/Misc | 8 | StoneGolem_clubs, StoneGolem_hat, StoneGolem_spikes, stonegolem_attack1_spike, stonegolem_attack2_left_groundslam, stonegolem_attack3_spikesweep, stonegolem_attack_doublesmash, stonegolem_attack_sonicboom_NOTUSED |
| Characters/Surtling | 3 | Spawner_imp, Spawner_imp_respawn, Surtling |
| Characters/Surtling/misc | 2 | Imp_fireball_projectile, imp_fireball_attack |
| Characters/TheCharred | 26 | Charred_Archer, Charred_Archer_Fader, Charred_Mage, Charred_Melee, Charred_Melee_Dyrnwyn, Charred_Melee_Fader, Charred_Twitcher, Charred_Twitcher_Summoned, GraveStone_Broken_CharredTwitcherNest, GraveStone_Broken_World, GraveStone_CharredFaderLocation, GraveStone_CharredTwitcherNest, GraveStone_Elite_Broken_CharredTwitcherNest, GraveStone_Elite_CharredTwitcherNest, Spawner_Charred, Spawner_CharredCross, Spawner_CharredStone, Spawner_CharredStone_Elite … |
| Characters/TheCharred/Armor | 4 | Charred_Breastplate, Charred_Helmet, Charred_HipCloth, Charred_MageCloths |
| Characters/TheCharred/effects | 7 | Charred_Melee_Ragdoll, fx_charred_chestglow, fx_charred_death, fx_charred_eyeglow, fx_charred_firestaff_chargeup, fx_charred_hit, fx_charred_summoned_death |
| Characters/TheHive | 1 | TheHive |
| Characters/Tick | 4 | Spawner_Tick, Spawner_Tick_stared, Spawner_Tick_stared_respawn_240, Tick |
| Characters/Tick/Fx | 2 | fx_TickBloodHit, fx_tick_death |
| Characters/Tick/SFX | 6 | sfx_tick_alerted, sfx_tick_attack_drain, sfx_tick_attack_jump, sfx_tick_attack_land, sfx_tick_hurt, sfx_tick_idle |
| Characters/TraderHaldor | 2 | ForceField, Haldor |
| Characters/TrainingDummy | 1 | TrainingDummy |
| Characters/Traps | 2 | fuling_trap, fuling_turret |
| Characters/Troll | 7 | Spawner_Troll, Spawner_TrollFrost, Troll, TrollFrost, TrollFrost_Dead, Troll_Summoned, Troll_sleeping |
| Characters/Troll/misc | 21 | troll_groundslam, troll_groundslam_aoe, troll_log_swing_h, troll_log_swing_v, troll_punch, troll_summoned_groundslam, troll_summoned_groundslam_aoe, troll_summoned_log_swing_h, troll_summoned_log_swing_v, troll_summoned_punch, troll_summoned_throw, troll_summoned_throw_projectile, troll_throw, troll_throw_projectile, trollsnow_groundslam, trollsnow_groundslam_aoe, trollsnow_groundslam_r, trollsnow_punch … |
| Characters/Ulv | 2 | Spawner_Ulv, Ulv |
| Characters/Ulv/Fx | 3 | Ulv_Ragdoll, sfx_ulv_death, vfx_ulv_death |
| Characters/Valkyrie | 1 | Valkyrie_End |
| Characters/Volture | 3 | Spawner_Volture, Volture, volture_strawpile |
| Characters/Wolf | 3 | Wolf, Wolf_cub, Wolf_spiritcaller |
| Characters/Wolf/misc | 6 | SpiritWolf_Attack1, SpiritWolf_Attack2, SpiritWolf_Attack3, Wolf_Attack1, Wolf_Attack2, Wolf_Attack3 |
| Characters/Wraith | 3 | Spawner_Bat, Spawner_Wraith, Wraith |
| Characters/Wraith/misc | 1 | wraith_melee |
| Characters/Writhan | 2 | Spawner_Writhan, Writhan |
| Characters/animals/birds | 3 | AshCrow, Crow, Seagal |
| Characters/animals/fishes | 14 | Fish1, Fish10, Fish11, Fish12, Fish2, Fish3, Fish4_cave, Fish5, Fish6, Fish7, Fish8, Fish9, Spawner_Fish4, vfx_water_surface_fish |
| Characters/character_effects | 13 | fx_backstab, fx_creature_tamed, fx_crit, fx_slide, sfx_creature_consume, vfx_BloodDeath, vfx_BloodHit, vfx_auto_pickup, vfx_blocked, vfx_creature_soothed, vfx_perfectblock, vfx_tar_surface, vfx_water_surface |
| Characters/character_effects/footsteps | 31 | fx_footstep_ash_jog, fx_footstep_ash_land, fx_footstep_ash_run, fx_footstep_ash_walk, fx_footstep_climb, fx_footstep_dverger_run, fx_footstep_grass_jog, fx_footstep_grass_land, fx_footstep_grass_run, fx_footstep_ground_climb, fx_footstep_ice_land, fx_footstep_ice_run, fx_footstep_ice_walk, fx_footstep_jog, fx_footstep_mud_jog, fx_footstep_mud_run, fx_footstep_run, fx_footstep_snow_deep_run … |
| Characters/character_effects/land | 3 | fx_land, fx_land_tar, fx_land_water |
| Characters/moose | 3 | Moose, Moose_calf, Moose_spiritcaller |
| Characters/seal | 4 | Seal, Seal_Pup, seal_pup_ragdoll, seal_ragdoll |
| world/Props | 27 | Ashlands_rock1, HugeStone1, RockDolmen_1, RockDolmen_2, RockDolmen_3, Rock_3, Rock_3_deepnorth, Rock_3_deepnorth_frac, Rock_3_frac, Rock_3_static, Rock_4, Rock_4_deepnorth, Rock_4_plains, Rock_7, Rock_7_deepnorth, Rock_7_meadows, Rock_destructible, SpawnPlatform … |
| world/Props/Ashlands | 35 | AshlandsBranch1, AshlandsBranch2, AshlandsBranch3, AshlandsBush1, AshlandsBush2, AshlandsTree1, AshlandsTree3, AshlandsTree4, AshlandsTree5, AshlandsTree6, AshlandsTree6_big, AshlandsTreeLog1, AshlandsTreeLog2, AshlandsTreeLogHalf1, AshlandsTreeLogHalf2, AshlandsTreeStump1, AshlandsTreeStump2, AshlandsTreeStump3 … |
| world/Props/BeeHive | 1 | Beehive |
| world/Props/Beech | 7 | Beech1, Beech_Sapling, Beech_Stub, Beech_small1, Beech_small2, beech_log, beech_log_half |
| world/Props/Birch | 8 | Birch1, Birch1_aut, Birch2, Birch2_aut, BirchStub, Birch_Sapling, Birch_log, Birch_log_half |
| world/Props/BogWitchHut | 17 | BogWitch_Amulet, BogWitch_Cauldron, BogWitch_Fire_Pit, BogWitch_Hut, BogWitch_Ladder, BogWitch_Leech, BogWitch_Stump1, BogWitch_Stump2, BogWitch_Table, BogWitch_Talisman1, BogWitch_Talisman2, BogWitch_Talisman3, BogWitch_Thistles, bogwitch_barrel, rug_bogwitch_deer, rug_bogwitch_fur, rug_bogwitch_wolf |
| world/Props/Bones | 2 | Skull1, Skull2 |
| world/Props/Bush01 | 6 | BlueberryBush, Bush01, Bush01_deepnorth, Bush01_heath, Bush02_en, RaspberryBush |
| world/Props/CastleBuildingKit | 23 | CastleKit_braided_box01, CastleKit_decal_dirt, CastleKit_decal_straw, CastleKit_groundtorch, CastleKit_groundtorch_blue, CastleKit_groundtorch_green, CastleKit_groundtorch_unlit, CastleKit_pot03, Charredfortress_LOD, StoneKit_ext_wall_2x2, StoneKit_int_floor_2x2, SunkenKit_int_arch, SunkenKit_int_floor_2x2, SunkenKit_int_floor_4x4, SunkenKit_int_stair, SunkenKit_int_towerwall_LOD, SunkenKit_int_wall_1x2, SunkenKit_int_wall_1x4 … |
| world/Props/Caverocks | 42 | CastleKit_brazier, CastleKit_decal_clawmarks, CastleKit_decal_fenrir_blood, CastleKit_metal_groundtorch_unlit, Ice_floor, Ice_floor_fractured, MountainKit_brazier, MountainKit_brazier_blue, MountainKit_int_floor, MountainKit_int_floor_2x2, MountainKit_int_wall_2x4, MountainKit_int_wall_4x2, MountainKit_int_wall_4x4, MountainKit_wood_gate, caverock_cornerwall, caverock_curvedrock, caverock_curvedwallbig, caverock_curvedwallbig_extra … |
| world/Props/CharredBanners | 7 | CharredBanner1, CharredBanner2, CharredBanner3, vfx_CharredBanner1_destroyed, vfx_CharredBanner2_destroyed, vfx_CharredBanner3_destroyed, vfx_charredbanner_destroyed |
| world/Props/Chests | 2 | crypt_skeleton_chest, stonechest |
| world/Props/CloudberryBush | 1 | CloudberryBush |
| world/Props/CryptKit | 7 | fi_vil_cath_decor_swords_cross, fi_vil_shield05_a, root07, root08, root11, root12, waterflow |
| world/Props/DeepNorth | 27 | BlackIce_Core, BlackIce_Core_combined, BlackIce_Core_outer, BlackIce_Start, FimbulvinterOrb, FimbulvinterOrb_start, LastBossGate, LastBossGate_Chain, LastBossGate_Chain2, LastBossGate_Floorstone, LastBossGate_InternalGate, LastBossGate_Pillar, LastBossGate_Pillarbase, LastBossGate_Rotator, LastBossGate_RuneTile, Stone1_huge, caverock_curvedrock, caverock_floorsmall … |
| world/Props/DeepNorthEnv | 10 | FirTree_big, FirTree_big_Sapling, FrozenGD, FrozenSkeleton_Pose1, FrozenSkeleton_Pose2, LingonberryBush, SnowFirTree, SnowFirTree 2, SnowFirTree_small, deepnorth_lantern_standing |
| world/Props/DeepNorth_TimberHall | 40 | PropFeastDeepNorth, prop_FeastAshlands, prop_FeastMeadows, prop_Tankard, prop_TrophyDraugrElite, prop_TrophyGoblinBrute, prop_TrophyGoblinShaman, prop_TrophyGreydwarf, prop_TrophyGreydwarfBrute, prop_TrophySeekerBrute, prop_ashwood_bed, prop_bed02, prop_bonfire, prop_cauldron_ext1_spice, prop_cauldron_ext3_butchertable, prop_cauldron_ext5_mortarandpestle, prop_cauldron_ext6_rollingpins, prop_chest_warderobe … |
| world/Props/DirtWalls | 4 | dirtfloor, dirtfloorflat, dirtwall, mudfloor |
| world/Props/DrakeNest | 3 | IceSpike01, IceSpike02, NestRock |
| world/Props/Dvergr | 90 | CreepProp_FloorCover01, CreepProp_drops, CreepProp_egg_hanging01, CreepProp_egg_hanging02, CreepProp_entrance1, CreepProp_entrance2, CreepProp_hanging01, CreepProp_pillar01, CreepProp_pillarhalf01, CreepProp_pillarhalf02, CreepProp_wall01, Hanging_RoyalJelly, Hildir_plainsfortress_wood_beam, Hildir_plainsfortress_wood_floor, SeekerEgg, SeekerEgg_alwayshatch, blackmarble_creep_4x1x1, blackmarble_creep_4x2x1 … |
| world/Props/EvilHeart | 2 | EvilHeart_Forest, EvilHeart_Swamp |
| world/Props/FirTree | 15 | FirTree, FirTree_Big_log, FirTree_Big_plantable_Stub, FirTree_Sapling, FirTree_Snow_Stub, FirTree_Snow_log, FirTree_Snow_log_half, FirTree_Stub, FirTree_big_log_half, FirTree_log, FirTree_log_half, FirTree_oldLog, FirTree_oldLog_deepnorth, FirTree_small, FirTree_small_dead |
| world/Props/Flametal | 2 | FlametalRockstand, FlametalRockstand_frac |
| world/Props/FrozenShips | 10 | Ice_ship_1, Ice_ship_2, Ice_ship_3, Ice_ship_4, Ice_ship_5, Ice_ship_6, Ice_ship_7, frozenship, frozenship02, frozenship03 |
| world/Props/GreyDwarfSpawner | 2 | Greydwarf_Root, Spawner_GreydwarfNest |
| world/Props/GuckSack | 2 | GuckSack, GuckSack_small |
| world/Props/HeathRockPillar | 2 | HeathRockPillar, HeathRockPillar_frac |
| world/Props/HildirWagon | 33 | chest_hildir1, chest_hildir1_incamp, chest_hildir2, chest_hildir2_incamp, chest_hildir3, chest_hildir3_incamp, fire_pit_haldor, fire_pit_hildir, fx_HildirChest_Unlock, hildir_barrel, hildir_carpet, hildir_clothesrack1, hildir_clothesrack2, hildir_clothesrack3, hildir_divan, hildir_divan1, hildir_fabricsroll1, hildir_fabricsroll2 … |
| world/Props/Ice | 3 | ice1, ice_rock1, ice_rock1_frac |
| world/Props/IceShelf | 10 | IceShelf_01, IceShelf_02, IceShelf_03, IceShelf_04, IceShelf_05, IceShelf_06, IceShelf_07, IceShelf_08, IceShelf_09, IceShelf_10 |
| world/Props/Ice_FimbulWinter | 8 | BlackIceShard_01, BlackIceShard_02, IceShard_01, IceShard_02, IceShard_03, IceShard_04, IceShard_05, IceShard_06 |
| world/Props/MemorialStones | 4 | MemorialStone_Large, MemorialStone_Medium, MemorialStone_Small, memorialsite_offering |
| world/Props/MineRock | 7 | MineRock_Copper, MineRock_Iron, MineRock_Meteorite, MineRock_Obsidian, MineRock_Stone, MineRock_Tin, Rock_destructible_test |
| world/Props/Mistlands | 36 | MistArea, MistArea_edge, MistArea_small, YggdrasilRoot, ancient_skull, cliff_ashlands1, cliff_ashlands1_frac, cliff_ashlands2, cliff_ashlands2_frac, cliff_ashlands3_Arch_1, cliff_mistlands1, cliff_mistlands1_creep, cliff_mistlands1_creep_frac, cliff_mistlands1_frac, cliff_mistlands2, cliff_mistlands2_frac, flying_core, giant_arm … |
| world/Props/Morkhalla | 175 | BlobMorkBig, Morkborg_gate, Morkhalla_Banner1, Morkhalla_Banner2, Morkhalla_Bedroll1, Morkhalla_Bedroll2, Morkhalla_Bench, Morkhalla_Block, Morkhalla_BoundingWall_20_broken, Morkhalla_Chain, Morkhalla_ChainLink, Morkhalla_ChestAncient, Morkhalla_Drawbridge, Morkhalla_Eye1, Morkhalla_Eye2, Morkhalla_Eye3, Morkhalla_Eye4, Morkhalla_Eye5_gemstone … |
| world/Props/MountainGrave | 1 | MountainGraveStone01 |
| world/Props/MudPile | 6 | mudpile, mudpile2, mudpile2_frac, mudpile_beacon, mudpile_frac, mudpile_old |
| world/Props/PineTree | 13 | PineTree_Sapling, PineTree_Snow_log, PineTree_Snow_log_XL, PineTree_Snow_log_XL_half, PineTree_Snow_log_half, PineTree_Snow_log_half_frost_troll, PineTree_log, PineTree_log_half, Pinetree_01, Pinetree_01_Stub, Pinetree_Snow, Pinetree_Snow_Stub, Pinetree_Snow_dead |
| world/Props/PineTreeOLD | 3 | PineTree, PineTree_logOLD, PineTree_log_halfOLD |
| world/Props/Rocks | 53 | Ashlands_rock2, BigRock, IcePond_rock, IcePond_rock_frac, IceShore, IceShoreShard, IceShore_1, IceShore_frac, IceWall, ShimmeringSand_rock, ShimmeringSand_rock_frac, TrollFrost_Frac, TrollFrost_Frac_arm, TrollFrost_Frac_legs, goldvein, goldvein_frac, highstone, highstone_2 … |
| world/Props/RuneStones | 39 | RuneStone_Ashlands, RuneStone_BlackForest, RuneStone_Boars, RuneStone_Bonemass, RuneStone_CaveMan, RuneStone_Cavepainting1, RuneStone_Cavepainting2, RuneStone_Cavepainting3, RuneStone_Cavepainting4, RuneStone_DeepNorth, RuneStone_DragonQueen, RuneStone_Drake, RuneStone_Draugr, RuneStone_GDKing, RuneStone_Greydwarfs, RuneStone_Meadows, RuneStone_Memorial1, RuneStone_Mistlands … |
| world/Props/SacredPillar | 1 | SacredPillar |
| world/Props/ShipwreckKarve | 6 | shipwreck_karve_bottomboards, shipwreck_karve_bow, shipwreck_karve_chest, shipwreck_karve_dragonhead, shipwreck_karve_stern, shipwreck_karve_sternpost |
| world/Props/ShipwreckVikingship | 5 | shipwreck_vikingship_chest, shipwreck_vikingship_front, shipwreck_vikingship_frontpiece, shipwreck_vikingship_mast1, shipwreck_vikingship_rear |
| world/Props/Shoots | 7 | ShootStump, YggaShoot1, YggaShoot2, YggaShoot3, YggaShoot_small1, yggashoot_log, yggashoot_log_half |
| world/Props/Shrub02 | 2 | shrub_2, shrub_2_heath |
| world/Props/StartTemple | 8 | BossStone_Bonemass, BossStone_DragonQueen, BossStone_Eikthyr, BossStone_Fader, BossStone_TheElder, BossStone_TheQueen, BossStone_Yagluth, StartPlatform |
| world/Props/Statues | 5 | StatueCorgi, StatueDeer, StatueEvil, StatueHare, StatueSeed |
| world/Props/Statues_Thor_Freya | 6 | StatueFreya, StatueFreya_broken_left, StatueFreya_broken_right, StatueThor, StatueThor_broken_bottom, StatueThor_broken_top |
| world/Props/StumpHut | 6 | BigBranch, StumpHole, StumpHole_destroyed, StumpHut, StumpHut_frac, StumpLog |
| world/Props/SwampTree | 7 | HugeRoot1, SwampTree1, SwampTree1_Stub, SwampTree1_log, SwampTree2, SwampTree2_darkland, SwampTree2_log |
| world/Props/Tar | 2 | TarLiquid, WaterLiquid |
| world/Props/TheHole | 16 | HoleRock_curved1, HoleRock_curved2, HoleRock_curved2wHole, HoleRock_floor1, HoleRock_opening1, HoleRock_opening_znet, HoleRock_root1, HoleRock_root1_destruction, HoleRock_rootBush1, HoleRock_rootFloor1, HoleRock_rootWall1, HoleRock_rootWall1_destruction, HoleRock_small1, HoleRock_small1_hole, elaking_trashpile, elaking_trashpile_destruction |
| world/Props/TraderChest | 1 | TraderChest_static |
| world/Props/TraderRune | 1 | TraderRune |
| world/Props/TraderTent | 2 | TraderLamp, TraderTent |
| world/Props/Vines | 1 | vines |
| world/Props/Vines_Ashlands | 3 | VineAsh, VineAsh_sapling, VineberrySeeds |
| world/Props/Vines_Green | 3 | VineGreen, VineGreenSeeds, VineGreen_sapling |
| world/Props/Waymarkers | 2 | marker01, marker02 |
| world/Props/Waystone | 2 | Waystone, sfx_runestone_activate |
| world/Props/WolfStatue | 1 | WolfStatue |
| world/Props/YagluthLocation | 7 | RockFinger, RockFingerBroken, RockFingerBroken_frac, RockFinger_frac, RockThumb, RockThumb_frac, YagluthAltarBase |
| world/Props/barrell | 3 | barrell, barrell_static, vfx_barrle_destroyed |
| world/Props/ground_clutter | 22 | clutter_shrub_large, grasscross_heath_green, instanced_ashlands_grass_long, instanced_ashlands_grass_short, instanced_forest_groundcover, instanced_forest_groundcover_brown, instanced_forest_groundcover_snow, instanced_heathflowers, instanced_heathgrass, instanced_meadows_grass, instanced_meadows_grass_short, instanced_mistlands_grass_short, instanced_mistlands_rockplant, instanced_ormbunke, instanced_shrub, instanced_small_rock1, instanced_swamp_grass, instanced_swamp_ormbunke … |
| world/Props/marble | 18 | Charred_altar_bellfragment, blackmarble_2x2_enforced, blackmarble_altar_crystal, blackmarble_altar_crystal_broken, blackmarble_column_3, blackmarble_floor_large, blackmarble_floor_triangle, blackmarble_head01, blackmarble_head02, blackmarble_head_big01, blackmarble_head_big02, blackmarble_post01, blackmarble_slope_1x2, blackmarble_slope_inverted_1x2, blackmarble_stair_corner, blackmarble_stair_corner_left, blackmarble_tip, metalbar_1x2 |
| world/Props/mushrooms | 1 | GlowingMushroom |
| world/Props/oak | 5 | Oak1, OakStub, Oak_Sapling, Oak_log, Oak_log_half |
| world/Props/offeraltar | 14 | altar, dragoneggcup, fader_bellholder, goblinking_totemholder, offeraltar_FrozenKing, offeraltar_FrozenKing_bossroom, offeraltar_bonemass, offeraltar_deer, offeraltar_dragon, offeraltar_fader, offeraltar_gdking, offeraltar_goblinking, offeraltar_memorialsite, offeraltar_queen |
| world/Props/rock_a | 1 | rock_a |
| world/Props/rockformation1 | 1 | rockformation1 |
| world/Props/small_flyer | 1 | tolroko_flyer |
| world/Props/stone | 2 | Stone1_huge, Stone1_interior |
| world/Props/stonepillar | 7 | StonePillar, StonePillarTall, StonePillarTall_mountain, StonePillar_mountain, Stoneblock, StoneblockSmall, stoneblock_fracture |
| world/Props/stoneslab | 1 | StoneSlab |
| world/Props/stonewall | 4 | stonewall, stonewall_1, stonewall_2, stonewall_3 |
| world/Props/stubbe | 3 | stubbe, stubbe_deepnorth, stubbe_spawner |
| world/Props/vagon | 3 | trader_wagon, trader_wagon_destructable, vfx_wagon_destroyed |
| world/Props/vegetation | 2 | acacitree, ormbunke_green_medium |
| world/Props/webs | 3 | horizontal_web, tunnel_web, vertical_web |

