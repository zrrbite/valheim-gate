"""Build game-catalog.md from extracted records + localization. Data only.
usage: python3 -I build.py <catalogdir>
"""
import collections
import glob
import json
import os
import re
import sys

C = sys.argv[1]
W = os.path.join(C, "work")
MAIN_CAB = "CAB-8923bd833c4171316cf3c761b642c1c8"

LOC = json.load(open(os.path.join(W, "loc.json")))["en"]


def L(tok):
    if not tok or not isinstance(tok, str):
        return ""
    t = tok[1:] if tok.startswith("$") else tok
    return LOC.get(t, "")


def clean(s, n=None):
    if s is None:
        return ""
    s = str(s).replace("\r", "").replace("\n", " / ").replace("|", "/")
    s = re.sub(r"\s+", " ", s).strip()
    if n and len(s) > n:
        s = s[:n - 1].rstrip() + "…"
    return s


# ---------- names for PPtr resolution ----------
NAMES = collections.defaultdict(dict)
CONTAINER = collections.defaultdict(dict)
for fn in glob.glob(os.path.join(W, "ex", "*.names.json")):
    d = json.load(open(fn))
    for cab, v in d.items():
        NAMES[cab].update(v["names"])
        CONTAINER[cab].update(v["container"])
for cab, m in json.load(open(os.path.join(W, "compmap_c4210710.json"))).items():
    for k, v in m.items():
        NAMES[cab].setdefault(k, v)


def R(x):
    if not isinstance(x, str):
        return x
    if x.startswith("@#"):
        return "?"
    if x.startswith("@"):
        return x[1:]
    if x.startswith("ext:"):
        _, cab, pid = x.split(":", 2)
        return NAMES.get(cab, {}).get(pid, "?ext")
    return x


# ---------- GUID -> asset path ----------
GUID = {}
for m in ("manifest.ids.tsv", "manifest_extended.ids.tsv"):
    for line in open(os.path.join(W, m)):
        p = line.rstrip("\n").split("\t")
        if len(p) == 3:
            GUID[p[0]] = p[2]


def guid_path(sr):
    try:
        a = sr["m_assetID"]
        return GUID.get("%08x%08x%08x%08x" % (a["v3"], a["v2"], a["v1"], a["v0"]), "")
    except Exception:
        return ""


def base(p):
    return os.path.splitext(os.path.basename(p))[0] if p else ""


# ---------- records ----------
BY = collections.defaultdict(list)
for fn in glob.glob(os.path.join(W, "ex", "*.jsonl")):
    for line in open(fn):
        r = json.loads(line)
        BY[r["cls"]].append(r)


def main_roots(cls):
    """records of cls on a prefab root in the main prefab bundle, one per prefab"""
    out = {}
    for r in BY[cls]:
        if r["b"] == "c4210710" and r.get("go") == r.get("root") and "at" not in r:
            out.setdefault(r["root"], r)
    return out


BIOMES = [(1, "Meadows"), (2, "Swamp"), (4, "Mountain"), (8, "BlackForest"), (16, "Plains"), (32, "Ashlands"),
          (64, "DeepNorth"), (256, "Ocean"), (512, "Mistlands")]


def biome(v):
    if not isinstance(v, int):
        return ""
    if v in (895, 1023) or v & 895 == 895:
        return "All"
    return ", ".join(n for b, n in BIOMES if v & b)


FACTION = "Players AnimalsVeg ForestMonsters Undead Demon MountainMonsters SeaMonsters PlainsMonsters Boss MistlandsMonsters Dverger PlayerSpawned TrainingDummy DeepNorth".split()
ITYPE = {0: "None", 1: "Material", 2: "Consumable", 3: "OneHanded", 4: "Bow", 5: "Shield", 6: "Helmet", 7: "Chest",
         9: "Ammo", 10: "Customization", 11: "Legs", 12: "Hands", 13: "Trophy", 14: "TwoHanded", 15: "Torch",
         16: "Misc", 17: "Shoulder", 18: "Utility", 19: "Tool", 20: "Attach_Atgeir", 21: "Fish", 22: "TwoHandedLeft",
         23: "AmmoNonEquipable", 24: "Trinket"}
PCAT = {0: "Misc", 1: "Crafting", 2: "Building(Workbench)", 3: "Building(Stonecutter)", 4: "Furniture",
        5: "DeepNorth tab (Hammer) / Feasts (Feaster)", 6: "Food (Feaster)", 7: "Meads (Feaster)", 8: "Meads"}

out = []
counts = collections.OrderedDict()


def w(s=""):
    out.append(s)


def table(headers, rows):
    w("| " + " | ".join(headers) + " |")
    w("|" + "|".join("---" for _ in headers) + "|")
    for r in rows:
        w("| " + " | ".join(clean(c) if c is not None else "" for c in r) + " |")
    w()


def folder(p, drop=2):
    if not p:
        return ""
    parts = p.split("/")[drop:-1]
    return "/".join(parts)


# ===================== data assembly =====================
items = main_roots("ItemDrop")
pieces = main_roots("Piece")
chars = {}
for cls in ("Humanoid", "Character", "Player"):
    for k, r in main_roots(cls).items():
        chars.setdefault(k, r)
drops = main_roots("CharacterDrop")
tame = main_roots("Tameable")
stations = main_roots("CraftingStation")

piece_tables = {}
for r in BY["PieceTable"]:
    piece_tables[r["root"]] = [R(x) for x in r["f"].get("m_pieces", [])]
buildable = {}
for tname, plist in piece_tables.items():
    for p in plist:
        buildable.setdefault(p, tname.replace("_", "").replace("PieceTable", ""))

DN_FOLDERS = ("DeepNorth", "FrozenKing", "Jotnar", "ElakingMole", "Morkhalla", "TheHole", "MemorialStones",
              "northVillage", "DN_", "Frozen", "DNFeast", "Moose", "seal", "Barka", "Writhan", "ShadowPerson",
              "FallenWarrior", "Elaking", "Frysling", "Gammeltroll", "TrollFrost", "BlobMork", "BigBlob", "DeepNorth_TimberHall")


def is_dn_path(p):
    return bool(p) and any(k.lower() in p.lower() for k in DN_FOLDERS)


# drops by creature
def drop_list(name, limit=8):
    r = drops.get(name)
    if not r:
        return []
    res = []
    for d in r["f"].get("m_drops", []) or []:
        pn = R(d.get("m_prefab"))
        ch = d.get("m_chance", 1)
        a, b = d.get("m_amountMin", 1), d.get("m_amountMax", 1)
        amt = "%d" % a if a == b else "%d-%d" % (a, b)
        res.append("%s x%s%s" % (pn, amt, "" if ch >= 1 else " (%d%%)" % round(ch * 100)))
    return res[:limit]


# spawn lists
spawn_rows = []
for r in BY["SpawnSystemList"]:
    if r["b"] != "c4210710":
        continue
    for s in r["f"].get("m_spawners", []) or []:
        spawn_rows.append((r["root"], s))
DN_SPAWN = {R(s.get("m_prefab")) for lst, s in spawn_rows if "DeepNorth" in lst}

# events
events = []
for r in BY["RandEventSystem"]:
    if r["b"] == "17245031":
        events = [dict(e, _list="main") for e in r["f"].get("m_events", [])]
for r in BY["LocationList"]:
    for e in r["f"].get("m_events", []) or []:
        events.append(dict(e, _list=r["root"].replace("_LocationList_", "")))

# locations from ZoneSystem + location lists
loc_entries = []
for r in BY["ZoneSystem"]:
    if r["b"] == "17245031":
        for e in r["f"].get("m_locations", []):
            loc_entries.append(("main", e))
for r in BY["LocationList"]:
    for e in r["f"].get("m_locations", []):
        loc_entries.append((r["root"].replace("_LocationList_", ""), e))

# location contents
loc_content = collections.defaultdict(lambda: collections.defaultdict(set))
LOC_PREFABS = set()
for _l, _e in loc_entries:
    _p = guid_path(_e.get("m_prefab")) if _e.get("m_prefab") else ""
    LOC_PREFABS.add(base(_p) or _e.get("m_prefabName") or "")
INTEREST = {"Vegvisir", "RuneStone", "OfferingBowl", "Trader", "Teleport", "BossStone", "CreatureSpawner", "SpawnArea",
            "Location", "Container", "NpcTalk", "Raven", "GuidePoint", "ItemStand", "Petable", "CraftingStation",
            "Beacon", "DungeonGenerator", "Door", "Incinerator", "WarriorNames", "TriggerSpawner", "SoftReferencePrefabSpawner",
            "SpawnPrefab", "Fish", "EventZone", "MusicLocation", "LootSpawner", "Bed", "Fireplace", "Pickable", "PickableItem"}
for cls in INTEREST:
    for r in BY[cls]:
        p = r.get("path", "")
        if p.startswith("Assets/world/Rooms/"):
            root = "ROOMS:" + p.split("/")[3]
        elif p.startswith("Assets/world/Locations/") or r.get("root") in LOC_PREFABS:
            root = r["root"]
        else:
            continue
        f = r["f"]
        lc = loc_content[root]
        lc["classes"].add(cls)
        if cls == "Vegvisir":
            for e in f.get("m_locations", []):
                lc["vegvisir"].add("%s (%s)" % (e.get("m_locationName"), L(e.get("m_pinName")) or e.get("m_pinName", "")))
        elif cls == "RuneStone":
            for k in ("m_text", "m_topic", "m_label"):
                if f.get(k):
                    lc["runes"].add(f[k])
            for e in f.get("m_randomTexts", []) or []:
                if e.get("m_text"):
                    lc["runes"].add(e["m_text"])
        elif cls == "OfferingBowl":
            lc["altar"].add("%s: %s -> %s%s" % (L(f.get("m_name")) or f.get("m_name", ""),
                                                        ("offer %s x%s" % (R(f.get("m_bossItem")), f.get("m_bossItems"))) if f.get("m_bossItem") else "items on its item stands",
                                                        R(f.get("m_bossPrefab")),
                                                        (" [sets %s]" % f["m_setGlobalKey"]) if f.get("m_setGlobalKey") else ""))
        elif cls == "Trader":
            lc["npc"].add(r.get("go"))
        elif cls in ("NpcTalk", "Raven", "Petable"):
            lc["npc"].add("%s%s" % (r.get("go"), (" (%s)" % L(f.get("m_name"))) if L(f.get("m_name")) else ""))
        elif cls == "Teleport":
            if f.get("m_enterText"):
                lc["enter"].add(L(f["m_enterText"]) or f["m_enterText"])
        elif cls == "CreatureSpawner":
            lc["spawns"].add(R(f.get("m_creaturePrefab")))
        elif cls == "SpawnArea":
            for e in f.get("m_prefabs", []) or []:
                lc["spawns"].add(R(e.get("m_prefab")))
        elif cls in ("TriggerSpawner",):
            for e in f.get("m_creaturePrefabs", []) or []:
                lc["spawns"].add(R(e))
        elif cls == "GuidePoint":
            t = f.get("m_text", {})
            lc["raven"].add("%s: %s" % ("Munin" if t.get("m_munin") else "Hugin", L(t.get("m_topic")) or t.get("m_key", "")))
        elif cls == "BossStone":
            lc["bossstone"].add(r.get("go"))
        elif cls == "Location":
            lc["loc"].add(json.dumps({k: f.get(k) for k in ("m_hasInterior", "m_noBuild")}))
        elif cls == "Container":
            if f.get("m_name"):
                lc["chests"].add(L(f["m_name"]) or f["m_name"])
        elif cls == "Door":
            if f.get("m_keyItem"):
                lc["keys"].add(R(f["m_keyItem"]))
        elif cls == "ItemStand":
            for e in f.get("m_supportedItems", []) or []:
                lc["itemstand"].add(R(e))
        elif cls == "MusicLocation":
            pass
        elif cls == "DungeonGenerator":
            lc["dungeon"].add(r.get("go"))
        elif cls == "Fish":
            lc["fish"].add(r.get("go"))
        elif cls == "EventZone":
            lc["event"].add(f.get("m_event", ""))
        elif cls in ("Pickable", "PickableItem"):
            it = R(f.get("m_itemPrefab")) if cls == "Pickable" else None
            if it and it not in ("?",):
                lc["pick"].add(it)
        elif cls == "Incinerator":
            lc["special"].add("Obliterator")
        elif cls == "WarriorNames":
            lc["special"].add("FallenWarrior names")

loc_paths = {}
for fn in ("manifest.ids.tsv",):
    for line in open(os.path.join(W, fn)):
        p = line.rstrip("\n").split("\t")
        if len(p) == 3 and p[2].startswith("Assets/world/Locations/"):
            loc_paths[base(p[2])] = p[2]

# status effects
ses = []
for cls, lst in BY.items():
    if cls == "StatusEffect" or cls.startswith("SE_"):
        for r in lst:
            if r["b"] == "c4210710" and r.get("so"):
                ses.append((cls, r))
ses.sort(key=lambda x: x[1]["so"].lower())

# ========================= write =========================
w("# Valheim game catalog (read from the Mac install, game 1.0.16 data, 2026-10-06)")
w()
w("Read-only extraction from `valheim.app/Contents/Resources/Data`: localization CSVs (TextAssets in `resources.assets`), "
  "every SoftRef bundle decoded with its type trees (MonoBehaviour fields, prefab roots, asset paths), the main scene "
  "(`Scenes/main.unity`: ZoneSystem, RandEventSystem, ObjectDB, DreamTexts), and enum values from `assembly_valheim.dll`. "
  "English names come from the game's own `localization` table; the full key->English table is in `loc-en.tsv` next to this file.")
w()
w("Conventions: **prefab** is the ZNetScene/ZoneSystem name (what `ZNetScene.GetPrefab`/`ZoneSystem` use). "
  "**token** is the `$key` the game shows through `Localization`. Folders are the Unity asset folders, a strong hint of what a thing is.")
w()
w("## Contents")
w()
toc_at = len(out)
w()

# ---- 1. localization ----
w("## 1. Localization")
w()
w("The English column of Valheim's `localization` TextAsset (5,619 keys) plus side tables (`localization_warriortitles`, `_captions`, `_extra`, platform ones). "
  "`localization_deepnorth`, `_witch`, `_ashlands`, `_celebrationupdate` and `_combatupdate` ship as empty shells: their strings were merged into the main table. "
  "Full dump: `loc-en.tsv` (key, English, source table).")
w()
pref = collections.Counter(k.split("_")[0] for k in LOC)
table(["prefix", "keys", "example"], [(p, n, "%s = %s" % (next(k for k in LOC if k.split('_')[0] == p), clean(LOC[next(k for k in LOC if k.split('_')[0] == p)], 60)))
                                      for p, n in pref.most_common(40)])
counts["Localization keys (English)"] = len(LOC)

# ---- 2. NPCs ----
w("## 2. NPCs and characters")
w()
npc_rows = []
seen = set()
for cls in ("Trader", "Raven", "NpcTalk", "Valkyrie", "Odin", "Petable", "EndCredits", "Barber"):
    for r in BY[cls]:
        key = (r.get("go"), r.get("root"), cls)
        if key in seen:
            continue
        seen.add(key)
        f = r["f"]
        nm = f.get("m_name") or f.get("m_hoverName", "")
        nm = nm.split("\\n")[0] if isinstance(nm, str) else nm
        note = ""
        if cls == "Trader":
            note = "%d wares" % len(f.get("m_items", []) or [])
        if cls == "NpcTalk":
            n = sum(len(v) for k, v in f.items() if isinstance(v, list))
            note = "%d talk lines" % n
        if cls == "Raven":
            note = "hint bird"
        ch = chars.get(r.get("go").split(" (")[0])
        if ch and ch["f"].get("m_name"):
            nm = ch["f"]["m_name"]
        if (cls, r.get("go").split(" (")[0], r.get("root")) in seen:
            continue
        seen.add((cls, r.get("go").split(" (")[0], r.get("root")))
        npc_rows.append((r.get("go"), cls, nm if isinstance(nm, str) and nm.startswith("$") else "", L(nm) or (nm if isinstance(nm, str) else ""),
                         r.get("root") if r.get("root") != r.get("go") else "", folder(r.get("path")), note))
npc_rows.sort(key=lambda x: (x[1], x[0]))
w("### 2a. NPC components found in prefabs")
w()
w("Name/English come from the prefab's `Character` when it has one (the Dvergr/FallenWarrior/ShadowPerson `NpcTalk.m_name` is a dev leftover, literally `Haldor`/`Dvergr`). "
  "`odin` is the cloaked figure that appears near the player and vanishes (class `Odin`, despawns at 20 m or after 60 s); `Valkyrie` carries a new character to the start; "
  "`Valkyrie_End` (EndCredits) is the post-Kall valkyrie at the start temple, \"Journey to Valhalla\", shown when the world key `StoneCircle` is set.")
w()
table(["GameObject", "class", "token", "English", "inside (root)", "folder", "note"], npc_rows)
counts["NPC components"] = len(npc_rows)

w("### 2b. NPC name tokens (`$npc_*` names)")
w()
names_rows = [(k, LOC[k]) for k in sorted(LOC) if k.startswith("npc_") and not re.search(r"\d+$", k) and len(LOC[k]) < 40]
table(["token", "English"], names_rows)

w("### 2c. NPC dialogue sets (`$npc_*` lines, grouped)")
w()
grp = collections.defaultdict(list)
for k in sorted(LOC):
    if k.startswith(("npc_", "shadowperson_", "fallen_viking_", "fallenwarrior_", "deadspeak_")):
        g = re.sub(r"\d+$", "", k)
        g = re.sub(r"_(random_)?(talk|greet|goodbye|aggravated|private_area_alarm|privatearea|buy|sell|trade|greeting|smalltalk|start|end|general|randomstart|randomtaunt|randomtalk|title)\w*$", r"_\2*", g)
        grp[g].append(k)
rows = []
for g, ks in sorted(grp.items()):
    sample = next((LOC[k] for k in ks if LOC[k]), "")
    rows.append((g, len(ks), clean(sample, 110)))
table(["group", "lines", "sample (English)"], rows)

w("### 2d. Trader wares (Haldor, Hildir, Bog Witch)")
w()
for r in BY["Trader"]:
    if r["b"] != "c4210710":
        continue
    w("**%s** (`%s`, %s)" % (L(r["f"].get("m_name")) or r["go"], r["go"], r.get("path")))
    w()
    rows = []
    for it in r["f"].get("m_items", []):
        pn = R(it.get("m_prefab"))
        tok = items.get(pn, {}).get("f", {}).get("m_itemData", {}).get("m_shared", {}).get("m_name", "")
        rows.append((pn, L(tok), it.get("m_stack"), it.get("m_price"), it.get("m_requiredGlobalKey", "")))
    table(["prefab", "English", "stack", "price", "requires key"], rows)

w("### 2e. Hugin / Munin guide points (raven hints placed in the world)")
w()
rows = []
for r in BY["GuidePoint"]:
    t = r["f"].get("m_text", {})
    rows.append((t.get("m_key", ""), "Munin" if t.get("m_munin") else "Hugin", L(t.get("m_topic")), clean(L(t.get("m_text")), 120), r.get("root"), folder(r.get("path"))))
rows = sorted(set(rows), key=lambda x: (x[5], x[0]))
table(["key", "raven", "topic", "text (start)", "placed in", "folder"], rows)
counts["Raven guide points"] = len(rows)

# ---- 3. creatures ----
w("## 3. Creatures")
w()
w("Every prefab root with a `Character`/`Humanoid` component in the main prefab bundle. Faction is `Character.Faction`. "
  "Boss = `m_boss`; event = `m_bossEvent` (music/raid id); key = `m_defeatSetGlobalKey`. DN = Deep North (faction DeepNorth, in the Deep North spawn list, or a Deep North asset folder).")
w()
rows = []
for name, r in sorted(chars.items(), key=lambda kv: (folder(kv[1].get("path")), kv[0])):
    f = r["f"]
    tok = f.get("m_name", "")
    fac = f.get("m_faction")
    fac = FACTION[fac] if isinstance(fac, int) and fac < len(FACTION) else fac
    boss = ""
    if f.get("m_boss"):
        boss = "BOSS"
    if f.get("m_bossEvent"):
        boss += " ev=%s" % f["m_bossEvent"]
    if f.get("m_defeatSetGlobalKey"):
        boss += " key=%s" % f["m_defeatSetGlobalKey"]
    dn = "DN" if (fac == "DeepNorth" or name in DN_SPAWN or is_dn_path(r.get("path"))) else ""
    rows.append((name, tok, L(tok), f.get("m_health"), fac, boss.strip(), "yes" if name in tame else "", dn,
                 folder(r.get("path")), "; ".join(drop_list(name, 6))))
table(["prefab", "token", "English", "HP", "faction", "boss / event / key", "tame", "DN", "folder", "drops"], rows)
counts["Creature/character prefabs"] = len(rows)
creature_rows = rows

w("### 3b. `$enemy_*` tokens with English (including ones not tied to a prefab above)")
w()
tied = {r[1][1:] for r in rows if r[1]}
rows2 = [(k, LOC[k], "" if k in tied else "not on a root Character") for k in sorted(LOC) if k.startswith("enemy_") and LOC[k] and not k.endswith(("message", "_lore"))]
table(["token", "English", "note"], rows2)
counts["$enemy_* tokens"] = len(rows2)

w("### 3c. Natural spawn tables (`SpawnSystemList`)")
w()
rows = []
for lst, s in spawn_rows:
    rows.append((lst.replace("_SpawnList_", ""), s.get("m_name"), R(s.get("m_prefab")), biome(s.get("m_biome")),
                 s.get("m_requiredGlobalKey", ""), s.get("m_requiredEnvironments", "") and ",".join(s.get("m_requiredEnvironments")),
                 ("night" if s.get("m_spawnAtNight") and not s.get("m_spawnAtDay") else "day" if s.get("m_spawnAtDay") and not s.get("m_spawnAtNight") else "any"),
                 "%s-%s" % (s.get("m_minLevel"), s.get("m_maxLevel")), "" if s.get("m_enabled", 1) else "disabled"))
table(["list", "spawner", "prefab", "biome", "requires key", "weather", "time", "levels", "state"], rows)
counts["Spawn table entries"] = len(rows)

# ---- 4. stations & pieces ----
w("## 4. Crafting stations and pieces")
w()
w("### 4a. Notable functional pieces (by component class)")
w()
rows = []
SPECIAL = ["CraftingStation", "StationExtension", "Incinerator", "ShieldGenerator", "Turret", "Catapult", "SiegeMachine",
           "Fermenter", "Smelter", "CookingStation", "MapTable", "Beehive", "SapCollector", "Windmill", "TeleportWorld",
           "Bed", "Ship", "Vagon", "ArmorStand", "ItemStand", "Barber", "Feast", "Fireplace", "PrivateArea", "Ladder",
           "Container", "Sign", "Trap", "Radiator", "WayStone", "BossStone"]
for cls in SPECIAL:
    for r in BY[cls]:
        if r["b"] != "c4210710":
            continue
        root = r["root"]
        f = r["f"]
        pr = pieces.get(root, {}).get("f", {})
        tok = f.get("m_name") or pr.get("m_name") or ""
        if not isinstance(tok, str):
            tok = ""
        detail = ""
        if cls == "Incinerator":
            detail = "; ".join("%d input kinds -> %s" % (len(c.get("m_requirements", [])), R(c.get("m_result"))) for c in f.get("m_conversions", []))
        elif cls in ("Fermenter", "Smelter", "CookingStation"):
            conv = f.get("m_conversion", [])
            detail = "%d recipes, e.g. %s" % (len(conv), ", ".join("%s->%s" % (R(c.get("m_from")), R(c.get("m_to"))) for c in conv[:3]))
        elif cls == "ShieldGenerator":
            detail = "fuel %s, radius %s" % ("/".join(R(x) for x in f.get("m_fuelItems", [])), f.get("m_maxShieldRadius"))
        elif cls == "Turret":
            detail = "ammo %s" % "/".join(R(a.get("m_ammo")) for a in f.get("m_allowedAmmo", []))
        elif cls == "StationExtension":
            detail = "extends %s" % R(f.get("m_craftingStation"))
        elif cls == "Container":
            detail = "%sx%s" % (f.get("m_width"), f.get("m_height"))
        rows.append((root, cls, tok, L(tok), PCAT.get(pr.get("m_category"), ""), buildable.get(root, ""), clean(L(pr.get("m_description")), 90), detail))
rows.sort(key=lambda x: (x[1], x[0]))
table(["prefab", "class", "token", "English", "build category", "built with", "description", "detail"], rows)
counts["Functional pieces (special classes)"] = len(rows)

w("### 4b. All pieces (prefab roots with `Piece`)")
w()
w("`built with` = which tool's PieceTable lists it (Hammer, Hoe, Cultivator, Feaster/ScytheHandle ...); blank = world-only (props, dungeon walls, spawned).")
w()
rows = []
for name, r in sorted(pieces.items(), key=lambda kv: (PCAT.get(kv[1]["f"].get("m_category"), ""), kv[0])):
    f = r["f"]
    res = ", ".join("%s x%s" % (R(x.get("m_resItem")), x.get("m_amount")) for x in (f.get("m_resources") or [])[:5])
    rows.append((name, f.get("m_name", ""), L(f.get("m_name")), PCAT.get(f.get("m_category"), f.get("m_category")),
                 R(f.get("m_craftingStation")) or "", buildable.get(name, ""), res, clean(L(f.get("m_description")), 80)))
table(["prefab", "token", "English", "category", "station", "built with", "cost", "description"], rows)
counts["Piece prefabs"] = len(rows)

# ---- 5. items ----
w("## 5. Items")
w()
def shared(r):
    return r["f"].get("m_itemData", {}).get("m_shared", {})

boss_drop = {}
for name, r in chars.items():
    if r["f"].get("m_boss") or "BOSS" in str(r["f"].get("m_faction")):
        for d in drops.get(name, {}).get("f", {}).get("m_drops", []) or []:
            boss_drop.setdefault(R(d.get("m_prefab")), name)
trader_items = {}
for r in BY["Trader"]:
    if r["b"] == "c4210710":
        for it in r["f"].get("m_items", []):
            trader_items.setdefault(R(it.get("m_prefab")), r["go"])

def notable(name, s, path):
    n = name.lower()
    t = s.get("m_itemType")
    why = []
    if name in boss_drop:
        why.append("drop of " + boss_drop[name])
    if name in trader_items:
        why.append("sold by " + trader_items[name])
    if s.get("m_questItem"):
        why.append("quest item")
    if t in (18, 24, 13):
        why.append(ITYPE[t].lower())
    for kw, lab in (("key", "key"), ("wishbone", "wishbone"), ("demister", "demister"), ("fishingrod", "fishing"), ("fishingbait", "fishing"),
                    ("feast", "feast"), ("mead", "mead"), ("dverger", "dvergr"), ("megingjord", "belt"), ("beltstrength", "belt"),
                    ("chest", ""), ("fader", "fader"), ("frozenking", "frozen king"), ("orb", "orb"), ("bell", "bell"),
                    ("egg", "egg"), ("saddle", "saddle"), ("cape", "cape"), ("staff", "staff"), ("thunder", "thunderstone"),
                    ("yule", "yule"), ("ymir", "ymir"), ("seasonal", "seasonal"), ("lantern", "lantern"), ("map", "map"),
                    ("memory", "memory"), ("tablet", "tablet"), ("shard", "shard"), ("soul", "soul"), ("spirit", "spirit"),
                    ("totem", "totem"), ("crown", "crown"), ("ancient", "ancient")):
        if kw in n and lab and lab not in why:
            why.append(lab)
    return why

rows_n = []
rows_all = []
for name, r in sorted(items.items(), key=lambda kv: (ITYPE.get(shared(kv[1]).get("m_itemType"), ""), kv[0])):
    s = shared(r)
    tok = s.get("m_name", "")
    row = (name, tok, L(tok), ITYPE.get(s.get("m_itemType"), s.get("m_itemType")), folder(r.get("path"), 3), clean(L(s.get("m_description")), 100))
    rows_all.append(row)
    why = notable(name, s, r.get("path"))
    if why and s.get("m_itemType") != 13:
        rows_n.append(row[:4] + (", ".join(why), clean(L(s.get("m_description")), 160)))
w("### 5a. Notable items (unique, boss drops, trader wares, keys, utility/trinkets, meads, feasts, odd ones)")
w()
table(["prefab", "token", "English", "type", "why", "description"], rows_n)
counts["Notable items"] = len(rows_n)

w("### 5b. Trophies")
w()
rows = [(n, s_tok, L(s_tok), clean(L(shared(items[n]).get("m_description")), 140)) for n in sorted(items)
        for s_tok in [shared(items[n]).get("m_name", "")] if shared(items[n]).get("m_itemType") == 13]
table(["prefab", "token", "English", "description"], rows)
counts["Trophies"] = len(rows)

w("### 5c. All item prefabs")
w()
table(["prefab", "token", "English", "type", "folder", "description"], rows_all)
counts["Item prefabs"] = len(rows_all)

# ---- 6. locations ----
w("## 6. Locations (ZoneSystem)")
w()
w("All entries of `ZoneSystem.m_locations` (main scene) and the six `LocationList` prefabs. `list` is where the entry lives. "
  "`prefab` is resolved from the SoftRef asset id through the bundle manifest. `contents` summarises what the location prefab holds: "
  "who stands there, what it spawns, altars, vegvisirs (pointing at which location), runestone texts, dungeon entrances.")
w()
rows = []
seen_loc = set()
for lst, e in loc_entries:
    pth = guid_path(e.get("m_prefab")) if e.get("m_prefab") else ""
    pn = base(pth) or e.get("m_prefabName") or ""
    lc = loc_content.get(pn, {})
    bits = []
    if lc.get("enter"):
        bits.append("enter: " + "/".join(sorted(lc["enter"])))
    if lc.get("npc"):
        bits.append("npc: " + ", ".join(sorted(lc["npc"])))
    if lc.get("altar"):
        bits.append("altar: " + "; ".join(sorted(lc["altar"])))
    if lc.get("vegvisir"):
        bits.append("vegvisir->" + ", ".join(sorted(lc["vegvisir"])))
    if lc.get("spawns"):
        sp = sorted(x for x in lc["spawns"] if x and x != "?")
        bits.append("spawns: " + ", ".join(sp[:8]) + (" …" if len(sp) > 8 else ""))
    if lc.get("runes"):
        rn = sorted(lc["runes"])
        bits.append("runestone: " + ", ".join(rn[:4]) + (" …" if len(rn) > 4 else ""))
    if lc.get("raven"):
        bits.append("raven: " + "; ".join(sorted(lc["raven"])))
    if lc.get("bossstone"):
        bits.append("boss stones")
    if lc.get("event"):
        bits.append("event zone: " + ",".join(sorted(lc["event"])))
    if lc.get("chests"):
        bits.append("chests: " + ", ".join(sorted(lc["chests"])))
    if lc.get("keys"):
        bits.append("locked by: " + ", ".join(sorted(lc["keys"])))
    if lc.get("special"):
        bits.append(", ".join(sorted(lc["special"])))
    if lc.get("fish"):
        bits.append("fish: " + ",".join(sorted(lc["fish"]))[:60])
    flags = []
    if e.get("m_unique"):
        flags.append("unique")
    if e.get("m_prioritized"):
        flags.append("prioritized")
    if e.get("m_iconAlways") or e.get("m_iconPlaced"):
        flags.append("map icon")
    if not e.get("m_enable", 1):
        flags.append("DISABLED")
    rows.append((pn, e.get("m_name", ""), lst, biome(e.get("m_biome")), e.get("m_quantity"), e.get("m_group", ""), " ".join(flags),
                 folder(pth or loc_paths.get(pn, ""), 3), clean("; ".join(bits), 400)))
    seen_loc.add(pn)
rows.sort(key=lambda x: (x[2] != "main", x[2], x[7], x[0]))
table(["prefab", "entry name", "list", "biome", "qty", "group", "flags", "folder", "contents"], rows)
counts["Location entries"] = len(rows)
loc_rows = rows

orph = sorted(set(loc_paths) - seen_loc)
if orph:
    w("Location prefabs shipped in bundles but not placed by any ZoneSystem list (dev, test, event or spawned-by-code): " + ", ".join("`%s`" % o for o in orph))
    w()
w("Location display tokens: " + "; ".join("`%s` = %s" % (k, LOC[k]) for k in sorted(LOC) if k.startswith("location_") and LOC[k]))
w()

w("### 6b. Dungeon room themes (rooms used by DungeonGenerator interiors)")
w()
rows = []
for k in sorted(x for x in loc_content if x.startswith("ROOMS:")):
    lc = loc_content[k]
    sp = sorted(x for x in lc.get("spawns", []) if x and x != "?")
    rows.append((k[6:], ", ".join(sp[:14]), ", ".join(sorted(lc.get("npc", []))), ", ".join(sorted(lc.get("chests", []))),
                 ", ".join(sorted(lc.get("keys", []))), ", ".join(sorted(lc.get("runes", []))[:5]), ", ".join(sorted(lc.get("raven", []))[:4])))
table(["room theme (folder)", "creatures spawned", "npcs", "chests", "door keys", "runestones", "ravens"], rows)
counts["Dungeon room themes"] = len(rows)
rows_rooms = rows
w("### 6c. Runestones and lore texts")
w()
rows = []
for r in BY["RuneStone"]:
    f = r["f"]
    toks = []
    for k in ("m_text",):
        if f.get(k):
            toks.append(f[k])
    for e in f.get("m_randomTexts", []) or []:
        if e.get("m_text"):
            toks.append(e["m_text"])
    for t in toks:
        rows.append((r.get("root"), r.get("go"), t, clean(L(t), 150)))
rows = sorted(set(rows))
table(["location / prefab", "stone", "token", "English (start)"], rows)
counts["Runestone texts placed"] = len(rows)
w("All `lore_*` keys (some are only in the table, not placed):")
w()
table(["token", "English (start)"], [(k, clean(LOC[k], 170)) for k in sorted(LOC) if k.startswith("lore_")])

# ---- 7. status effects ----
w("## 7. Status effects and guardian powers")
w()
rows = []
for cls, r in ses:
    f = r["f"]
    rows.append((r["so"], cls, f.get("m_name", ""), L(f.get("m_name")), clean(L(f.get("m_tooltip")), 140), f.get("m_ttl"), f.get("m_cooldown")))
table(["asset", "class", "token", "English", "tooltip", "ttl s", "cooldown s"], rows)
counts["Status effect assets"] = len(rows)
gp = [r for r in rows if r[0].startswith("GP_")]
counts["Guardian powers (GP_*)"] = len(gp)
w("Guardian powers: " + "; ".join("`%s` %s" % (r[0], r[3]) for r in gp))
w()
tied = {r[2][1:] for r in rows if r[2]}
extra = [(k, clean(LOC[k], 120)) for k in sorted(LOC) if k.startswith("se_") and k not in tied and not k.endswith(("tooltip", "description", "_desc", "start", "stop"))]
w("`$se_*` keys not used as an asset's name (many are tooltips/descriptions of the above, or effects defined inside items):")
w()
table(["token", "English"], extra)

# ---- 8. raids ----
w("## 8. Raids / random events (`RandEventSystem.m_events` plus the `m_events` of each `LocationList`)")
w()
rows = []
for e in events:
    spawns = sorted({R(s.get("m_prefab")) for s in e.get("m_spawn", []) or []})
    req = ",".join(e.get("m_requiredGlobalKeys", []) or [])
    nreq = ",".join(e.get("m_notRequiredGlobalKeys", []) or [])
    pk = ",".join((e.get("m_altRequiredPlayerKeysAny", []) or []) + (e.get("m_altRequiredPlayerKeysAll", []) or []))
    npk = ",".join(e.get("m_altNotRequiredPlayerKeys", []) or [])
    rows.append((e.get("m_name"), e.get("_list"), "" if e.get("m_enabled") else "disabled", biome(e.get("m_biome")), e.get("m_duration"),
                 req, nreq, pk, npk, L(e.get("m_startMessage")), L(e.get("m_endMessage")), ", ".join(spawns)))
table(["event", "list", "state", "biome", "dur s", "world keys req", "world keys not", "player keys", "player keys not", "start msg", "end msg", "spawns"], rows)
counts["Raid/events"] = len(rows)
w("Weather per list (`LocationList.m_environments`, `m_biomeEnvironments`):")
w()
for r in BY["LocationList"]:
    f = r["f"]
    envs = [e.get("m_name") for e in f.get("m_environments", []) or []]
    if envs:
        be = "; ".join("%s: %s (music %s)" % (b.get("m_name"), ", ".join(x.get("m_environment") for x in b.get("m_environments", [])), b.get("m_musicDay")) for b in f.get("m_biomeEnvironments", []) or [])
        w("- **%s**: %s%s" % (r["root"].replace("_LocationList_", ""), ", ".join(envs), (" — " + be) if be else ""))
w()
w("Boss/event ids also seen on creatures and event zones: " + ", ".join(sorted({r["f"].get("m_event") for r in BY["EventZone"] if r["f"].get("m_event")} | {c["f"].get("m_bossEvent") for c in chars.values() if c["f"].get("m_bossEvent")})))
w()

# ---- 9. dreams ----
w("## 9. Dreams (`DreamTexts`) and ravens' tutorial texts")
w()
rows = []
for r in BY["DreamTexts"]:
    for t in r["f"].get("m_texts", []):
        rows.append((t.get("m_text"), t.get("m_chanceToDream"), ",".join(t.get("m_trueKeys", []) or []), ",".join(t.get("m_falseKeys", []) or []), clean(L(t.get("m_text")), 400)))
table(["token", "chance", "needs keys", "blocked by keys", "English"], rows)
counts["Dreams"] = len(rows)
rows = []
for r in BY["Tutorial"]:
    for t in r["f"].get("m_texts", []):
        rows.append((t.get("m_name"), "Munin" if t.get("m_isMunin") else "Hugin", L(t.get("m_topic")), clean(L(t.get("m_text")), 160)))
table(["tutorial id", "raven", "topic", "text (start)"], rows)
counts["Hugin/Munin tutorial texts"] = len(rows)

# ---- 10. misc systems ----
w("## 10. Other story-usable systems")
w()
for r in BY["ItemSets"]:
    if r["b"] == "d59cfac":
        w("**ItemSets** (dev kits, `ItemSets.m_sets`): " + ", ".join("`%s`" % s.get("m_name") for s in r["f"].get("m_sets", [])))
        w()
for r in BY["WarriorNames"]:
    f = r["f"]
    w("**Fallen Warriors' names** (`WarriorNames` on `%s`): %d Norse male names (%s …), %d female names (%s …), titled with one of %d suffixes, e.g. %s. "
      "Their fight lines: `fallen_viking_randomstart_*` (20), `_randomtaunt_*` (40), `_randomend_*` (20, e.g. \"%s\")." % (
      r["root"], len(f.get("m_maleNames", [])), ", ".join(f.get("m_maleNames", [])[:6]), len(f.get("m_femaleNames", [])),
      ", ".join(f.get("m_femaleNames", [])[:6]), len(f.get("m_suffixes", [])), "; ".join(L(x) for x in f.get("m_suffixes", [])[:8:2]),
      L("fallen_viking_randomend_1")))
    w()
    break
_untr = []
for _n, _r in sorted(chars.items()):
    if _r["f"].get("m_name", "").startswith("$") and not L(_r["f"]["m_name"]):
        _untr.append("%s (`%s`, creature)" % (_n, _r["f"]["m_name"]))
for _n, _r in sorted(pieces.items()):
    if _r["f"].get("m_name", "").startswith("$") and not L(_r["f"]["m_name"]):
        _untr.append("%s (`%s`, piece)" % (_n, _r["f"]["m_name"]))
for _n, _r in sorted(items.items()):
    _t = shared(_r).get("m_name", "")
    if _t.startswith("$") and not L(_t):
        _untr.append("%s (`%s`, item)" % (_n, _t))
w("**Prefabs whose token has no English** (unfinished, hidden or dev content; the game would show the raw token): " + "; ".join(_untr))
w()
w("**Achievements**: %d (`Achievement` assets); the all-bosses one lists `$enemy_frozenking_p3` as the final kill." % len(BY["Achievement"]))
w()

# ---- 11. Deep North ----
w("## 11. The Deep North (1.0) in detail")
w()
w("### 11a. Kall Fimbulbringer, the Frozen King: forms and arena")
w()
rows = []
for name, r in sorted(chars.items()):
    if "FrozenKing" in name or "Aspect" in name or "aspect" in r.get("path", ""):
        f = r["f"]
        rows.append((name, f.get("m_name", ""), L(f.get("m_name")), f.get("m_health"), FACTION[f["m_faction"]] if isinstance(f.get("m_faction"), int) and f["m_faction"] < len(FACTION) else "",
                     "boss" if f.get("m_boss") else "", f.get("m_bossEvent", ""), f.get("m_defeatSetGlobalKey", ""), "; ".join(drop_list(name))))
table(["prefab", "token", "English", "HP", "faction", "boss", "event", "defeat key", "drops"], rows)
for r in BY["OfferingBowl"]:
    if "FrozenKing" in r.get("go", "") or "FrozenKing" in r.get("root", "") or "DN_" in r.get("root", ""):
        f = r["f"]
        w("- Altar `%s` in `%s`: \"%s\" — offer **%s x%s** to summon **%s**; sets world key `%s`." % (
            r["go"], r["root"], L(f.get("m_name")), R(f.get("m_bossItem")), f.get("m_bossItems"), R(f.get("m_bossPrefab")), f.get("m_setGlobalKey", "")))
w()
w("**The ending chain, as wired in the prefabs.** Break a `BlackIce_Core` (it drops **Malicious Blood**, `HatefulBlood`, and stops the Jotun invasion) -> "
  "offer 3 at the outer `offeraltar_FrozenKing` of `DN_Bossroom` (\"The First Prison\"/\"The Prison\"; its runestone reads `$lore_frozenking` \"HALT THE INVASION\"), "
  "which plays `vfx_LastBossGate_destroyed` and sets world key `LastBossGate_Open` -> offer 3 more at the inner `offeraltar_FrozenKing_bossroom` to summon `FrozenKing` "
  "(10,000 HP; key `defeated_frozenking`) -> `FrozenKing_p2` (7,000 HP) calls the seven **Aspects** of the earlier Forsaken -> `FrozenKing_p3` (30,000 HP; key `defeated_frozenking_p3`) "
  "drops **Sacrificial Blood** (`FrozenKingDrop`, \"The last essence of an end once foretold.\") and the **Crown Jewel** -> place the Sacrificial Blood on the "
  "**Chiselled Platform** (`StartPlatform` in the start temple; message \"You are worthy\") which sets world key `StoneCircle` -> `Valkyrie_End` appears "
  "(\"Journey to Valhalla\", runs the end credits) with Hugin (\"Journey to Valhalla\") and Munin (\"And so the saga comes to an end...\") guide points beside it. "
  "Achievement `$ach_boss8frozenking`: \"Defeat the Shackled One.\"")
w()
dn_tokens = [k for k in sorted(LOC) if re.search(r"frozenking|fimbul|aspect_|lastboss|stonecircle|tutorial_end_|valkyrie_end|hatefulblood|crownjewel|dnbossroom", k)]
table(["token", "English"], [(k, clean(LOC[k], 200)) for k in dn_tokens])

w("### 11b. Deep North creatures")
w()
table(["prefab", "token", "English", "HP", "faction", "boss / event / key", "tame", "DN", "folder", "drops"], [r for r in creature_rows if r[7] == "DN"])
w("### 11c. Deep North spawn table")
w()
table(["spawner", "prefab", "English", "biome", "requires key", "time", "levels", "state"],
      [(s.get("m_name"), R(s.get("m_prefab")), L(chars.get(R(s.get("m_prefab")), {}).get("f", {}).get("m_name")), biome(s.get("m_biome")),
        s.get("m_requiredGlobalKey", ""), ("night" if s.get("m_spawnAtNight") and not s.get("m_spawnAtDay") else "day" if s.get("m_spawnAtDay") and not s.get("m_spawnAtNight") else "any"),
        "%s-%s" % (s.get("m_minLevel"), s.get("m_maxLevel")), "" if s.get("m_enabled", 1) else "disabled") for lst, s in spawn_rows if "DeepNorth" in lst])
w("### 11d. Deep North locations")
w()
table(["prefab", "entry name", "list", "biome", "qty", "group", "flags", "folder", "contents"],
      [r for r in loc_rows if "DeepNorth" in r[2] or "DeepNorth" in r[7] or "DeepNorth" in r[3]])
w("Deep North dungeon interiors (room themes):")
w()
table(["room theme (folder)", "creatures spawned", "npcs", "chests", "door keys", "runestones", "ravens"],
      [r for r in rows_rooms if r[0] in ("hole", "morkhalla", "northVillage")])
w("### 11e. Deep North NPCs, ghosts and voices")
w()
w("The Imprisoned Dvergr (`DvergerDeepNorth`) and Captive Fuling (`GoblinDeepNorth`) are spawned inside Mörkhalla rooms (`morkhalla` theme, by `CreatureSpawner`), next to Jotun guards; "
  "Fallen Warriors stand at `memorialsite_offering` (spawned by the Ancestral Memorial altar, `NorthMemorialPlace`); Shadows walk the North Village and The Hole at night.")
w()
table(["GameObject", "class", "token", "English", "inside (root)", "folder", "note"],
      [r for r in npc_rows if is_dn_path(r[5]) or "DeepNorth" in r[0] or r[0] in ("ShadowPerson", "FallenWarrior")])
table(["token", "English"], [(k, clean(LOC[k], 300)) for k in sorted(LOC) if re.search(r"munin_deepnorth|dvergr_deepnorth|^shadowperson_randomtalk_0[1-5]$|fallen_viking_random(start|taunt)_[1-3]$|fallenwarrior_title[1-3]_m$|enemy_(goblin|dvergr)_deepnorth|enemy_shadowperson|enemy_fallenwarrior|ghost_void", k)])
w("### 11f. Deep North pieces (`Piece` category DeepNorth, or Deep North asset folder)")
w()
table(["prefab", "token", "English", "category", "station", "built with", "cost", "description"],
      [(name, r["f"].get("m_name", ""), L(r["f"].get("m_name")), PCAT.get(r["f"].get("m_category")), R(r["f"].get("m_craftingStation")) or "",
        buildable.get(name, ""), ", ".join("%s x%s" % (R(x.get("m_resItem")), x.get("m_amount")) for x in (r["f"].get("m_resources") or [])[:5]),
        clean(L(r["f"].get("m_description")), 80))
       for name, r in sorted(pieces.items()) if (r["f"].get("m_category") == 5 and buildable.get(name) == "Hammer") or is_dn_path(r.get("path"))])
w("The Hammer's PieceTable has a sixth tab whose label is the raw string `DEEPNORTH` (category 5); in the Feaster's table the same category number is labelled Feasts.")
w()
w("### 11g. Deep North items (dropped by Deep North creatures, or in a Deep North asset folder)")
w()
dn_drop = set()
for r in creature_rows:
    if r[7] == "DN":
        for d in drops.get(r[0], {}).get("f", {}).get("m_drops", []) or []:
            dn_drop.add(R(d.get("m_prefab")))
table(["prefab", "token", "English", "type", "folder", "description"],
      [row for row in rows_all if not row[0].startswith(("FW_", "SP_", "JotunHair")) and (row[0] in dn_drop or is_dn_path(items[row[0]].get("path"))) or re.search(r"frozen|jotun|elaking|moose|seal|fimbul|writhan|barka|morkhalla|deepnorth|ancestral|memorial", row[0].lower() + row[1].lower()) and not row[0].startswith(("FW_", "SP_", "JotunHair"))])
w("Left out: `FW_*` and `SP_*` items (copies of player gear worn by Fallen Warriors and Shadows) and `JotunHair*` (Jotun visual attachments).")
w()
w("### 11h. Deep North raids")
w()
dnrows = []
for e in events:
    if e.get("_list") == "DeepNorth" or re.search(r"elak|jotun|fimbul|frozen", e.get("m_name", "")):
        spawns = sorted({R(s.get("m_prefab")) for s in e.get("m_spawn", []) or []})
        dnrows.append((e.get("m_name"), biome(e.get("m_biome")), ",".join(e.get("m_requiredGlobalKeys", []) or []),
                       ",".join((e.get("m_altRequiredPlayerKeysAny", []) or []) + (e.get("m_altRequiredPlayerKeysAll", []) or [])),
                       L(e.get("m_startMessage")), L(e.get("m_endMessage")), ", ".join(spawns)))
table(["event", "biome", "world keys req", "player keys", "start msg", "end msg", "spawns"], dnrows)
w("`fimbulvinter` has no spawns of its own: the four `Fimbulvinter - *` spawners in the Deep North spawn list (Jotun warriors, witches, Elakingar, meteors; biome All) do the work, "
  "tied to the Orb of Fimbulvinter (`$fimbulvinterorb`). The trigger is physical: breaking the orb at a Mörkhalla end room (`morkhalla_endcap01/02`, `TriggerPersistentEventOnDestroy` -> persistent event `jotun_invasion`, centre text \"The Jotun Advance\") starts it, and destroying a `BlackIce_Core` stops it (\"The Jotun Retreat\"); `FimbulLocation01` is a disabled location of the same set. "
  "Deep North weather: Twilight_Snow, Twilight_Clear, Twilight_SnowStorm, plus JotunInvasion_<biome> variants for the invasion, and Morkhalla, TheHollow, DN_Bossroom interiors.")
w()
w("### 11i. Deep North dreams (full text)")
w()
for k in sorted(LOC):
    if k.startswith("dream_deepnorth"):
        dk = next(((t.get("m_trueKeys"), t.get("m_falseKeys"), t.get("m_chanceToDream")) for r in BY["DreamTexts"] for t in r["f"]["m_texts"] if t.get("m_text") == "$" + k), None)
        w("- **%s**%s: %s" % (k, (" (needs %s; not after %s; chance %s)" % (",".join(dk[0] or []) or "-", ",".join(dk[1] or []) or "-", dk[2])) if dk else " (not in DreamTexts)", clean(LOC[k])))
w()
w("### 11j. Deep North lore (full text)")
w()
for k in sorted(LOC):
    if k.startswith("lore_deepnorth") or k.startswith("npc_munin_deepnorth"):
        w("- **%s**: %s" % (k, clean(LOC[k])))
w()

# ---- 12. models / props by folder ----
w("## 12. Models: world props and character folders (from the bundle manifest)")
w()
w("Every prefab path under `Assets/world/Props/` and `Assets/Characters/` in `manifest_extended`, grouped by folder (fx/sfx/attack sub-prefabs left out). "
  "These are spawnable by prefab name only if they are also in ZNetScene; most props with a `Piece`/`Destructible`/`WearNTear` are.")
w()
grp = collections.defaultdict(list)
for line in open(os.path.join(W, "manifest_extended.ids.tsv")):
    pp = line.rstrip("\n").split("\t")
    if len(pp) != 3 or not pp[2].endswith(".prefab"):
        continue
    path = pp[2]
    if not path.startswith(("Assets/world/Props/", "Assets/Characters/")):
        continue
    if re.search(r"/(fx|sfx|vfx|attacks?|Attacks|BossAspects/Attacks|Weapons|weapons|audio|ragdoll|Equipment)/", path):
        continue
    parts = path.split("/")
    grp["/".join(parts[1:4]) if len(parts) > 4 else "/".join(parts[1:3])].append(base(path))
rows = []
for k in sorted(grp):
    v = sorted(set(grp[k]))
    rows.append((k, len(v), ", ".join(v[:18]) + (" …" if len(v) > 18 else "")))
table(["folder", "prefabs", "names"], rows)
counts["Prop/character model folders"] = len(rows)

# ---- toc + counts ----
heads = [l for l in out if l.startswith("## ") and not l.startswith("## Contents")]
toc = ["- [%s](#%s)" % (h[3:], re.sub(r"[^a-z0-9 _-]", "", h[3:].lower()).replace(" ", "-")) for h in heads]
cnt = ["", "### Counts", "", "| category | count |", "|---|---|"] + ["| %s | %d |" % (k, v) for k, v in counts.items()] + [""]
out[toc_at:toc_at] = toc + cnt
open(os.path.join(C, "game-catalog.md"), "w").write("\n".join(out) + "\n")
json.dump(counts, open(os.path.join(W, "counts.json"), "w"), indent=1)
with open(os.path.join(C, "loc-en.tsv"), "w") as f:
    f.write("key\tenglish\n")
    for k in sorted(LOC):
        f.write("%s\t%s\n" % (k, clean(LOC[k]).replace("\t", " ")))
print(json.dumps(counts, indent=1))
