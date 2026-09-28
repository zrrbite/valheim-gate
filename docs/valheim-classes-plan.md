# Valheim Class System — Plan & Notes

> ## Integration notes (2026-09-27, Claude Code, on `feature/run-mode`)
>
> This brief was written without sight of the saga's boon system, story bible, or constraints.
> The IDEA is adopted — a specialisation with a passive and three abilities, learned from a trainer
> — but the MACHINERY below it changes. Everything in this box is what Claude Code is actually
> building; sections marked **Superseded for the saga** further down are kept as reference only.
> The saga's own state page is `docs/superpowers/RESUME.md`; read it before this file. The design
> record for classes is `docs/superpowers/specs/2026-09-27-classes-design.md`.
>
> ### Decisions taken with Martin (2026-09-27)
>
> | Question | Decision |
> |---|---|
> | Where a class lives | **Per run**, chosen DURING the saga from a trainer NPC — not in the lobby, not per character. One trainer for all classes in v1; later, each class's trainer stands somewhere else in the world. |
> | How abilities unlock | **Saga progression**, not a custom skill: rung 1 at the choice, rung 2 *available* once 1 boss is down (Eikthyr), rung 3 at 3 bosses (Bonemass). Available → **learned** by returning to the trainer. All thresholds are data. |
> | Boon migration | **Everything class-defining moves** out of the general boon pool into the classes (see table). |
> | Roster | **Hunter, Völva, Berserker** first (2026-09-27); **Húskarl, Skald, Sæfari, Smiðr** the next day, once the first three had been played. All seven are in. |
> | Trainer | **A new NPC, "the thane of the graves"** — one of the put-away who remembers what the dead were in life. Stands by DAY (the shade wants dark, Thjalfi wants rain, the thane wants daylight). |
> | When | After the bed step in Act I (`mq-bed`). |
> | Step or encounter | A HEARTH-track questline step that completes on **speaking**; choosing a class is optional. |
>
> ### The one architectural idea: class passives and abilities ARE boons
>
> The saga already has a `BoonEngine` (pure, unit-tested) with a pool, an offer wheel, held boons,
> cooldowns, charges, persistence, per-run loans that are repaid at run end, and a death penalty
> that takes the newest boon. `BoonDefinition` gains one field, `ClassId` (null = general). Then:
>
> - the offer wheel never offers a class boon;
> - death never takes a class boon (the thane's teaching is not Odin's loan);
> - the thane grants class boons through the existing `BoonEngine.Grant`, the same door the
>   "sleep through the night" step uses to hand out Tireless;
> - a pure `ClassDefinition` + `ClassLadder` table (in `RunMode/`, compiled by `Tests/run_tests.sh`)
>   says which boon ids are due for a class at a boss count.
>
> That inherits, for free: apply/unapply in `BoonEffects`, persistence in `heldBoonIds`, passive
> reapply on respawn and resume, unwind at run end, the ability bar, cooldown display and the
> `BoonKeys` help labels. The class layer is a small table plus one NPC, not a second effect system.
>
> ### v1 class table
>
> Ability slots share keys, since a run holds one class: **[7] Keypad7, [0] Keypad0, [Ins] Insert.**
>
> | Class | The fallen one | Passive (at choice) | Rung 1 (at choice) [7] | Rung 2 (1 boss) [0] | Rung 3 (3 bosses) [Ins] |
> |---|---|---|---|---|---|
> | Hunter | Eydís | `hunter` Bows to 50, `shepherd` (tames stronger) | `brother` Packbrother (wolf) | `menagerie` (any beast) | `unseen` (invisible 20 s) |
> | Völva | Sigrún | `hearthlight` (mending warmth, heals you and yours) | `shaman` Mending (heal burst) | `bonecaller` (two skeletons) | `wrath` Thor's Wrath — lightning AoE at the aim point (**new**) |
> | Berserker | Ulfr | `warrior` Axes/Swords/Clubs to 50 | `rend` — 360° sweep, slash + bleed (**new**) | `rage` Blood Rage — +50 % damage for 15 s, takes more (**new**) | `warcry` — stagger everything within 8 m, not bosses (**new**) |
>
> Moved out of the general pool, ids unchanged so saves restore: brother, menagerie, shepherd,
> hearthlight, unseen, hunter, bonecaller, shaman, warrior. `wind` (Second Wind) stays general.
> General pool after the move: 21. Refilling it is a follow-up.
>
> ### What is NOT adopted from this brief, and why
>
> - **Separate `ValheimClasses.dll`, a generic Cecil `Hook()` helper, ~12 IL hooks (§1, §2).**
>   The saga has exactly two hooks (`FejdStartup.Start`, `Character.OnDeath`) and polls everything
>   else from `RunService.Tick`. Class abilities are key-polled actives like every other boon. New
>   IL hooks are a later, separate decision — the Patcher change forces a full reinstall on every
>   machine.
> - **Custom `Skills.SkillType`, `RaiseSkill` prefix, XP feeders, boss-based level cap (§8, §8.1).**
>   The saga runs skill gain at ×3 and LOANS skills (snapshotted and repaid at run end), so a custom
>   skill would fight both. Saga progression (boss count) paces the kit — the brief itself notes
>   level 50 ≈ Bonemass. Favoured skills become skill loans (the existing `hunter`/`warrior` boons).
> - **`Player.m_customData["class"]`, ZDO sync, `ZRoutedRpc` buffs to other players.** The saga is
>   single-player (the run owns the world's global keys). Class is run state; nothing is networked.
> - **Class-gated VANILLA recipes and equipment (§5).** Needs prefix hooks with early return. Not in
>   v1. Class-exclusive items will be SAGA items (runtime prefab clones, `SagaItems.cs`) whose
>   recipe registers only for the class — no hook needed.
> - **Skald and Sæfari crew auras.** No other players in a run. Parked.
> - **The thane at a burned longhouse, "Odin wants a hird rebuilt" (§12).** Reframed inside the
>   story bible: Valheim is where the put-away are put; the graves are theirs; a class is taking up
>   the way of one of the fallen. Odin's audit of the light stays the premise; classes never answer
>   the shortage. The thane is not a god, which is why he may speak.
> - **Berserker "damage rises as HP drops".** Rejected earlier in the boon design: continuous
>   re-application of weapon damage is a known landmine. Blood Rage is an on-demand timed active.
> - **Völva eitr-regen passive (`SE_Stats.m_eitrRegenMultiplier`).** Verified in the game IL: eitr
>   has no base value and only regenerates when food gives eitr, so the passive would be inert until
>   the Mistlands. Dropped from v1; runtime `SE_Stats` creation is recorded as an option (it works:
>   `SEMan.AddStatusEffect(StatusEffect)` clones an instance without an `ObjectDB` lookup).
> - **`vc` console commands, `server.json`, `verified-names.md` (§10).** The saga has `runDevMode`
>   dev keys, a JSON config, and a run-start validator that checks asset names against the live
>   registries. Dev keys for classes: `mod + Keypad*` cycles class, `mod + Keypad/` learns all due.
> - **Multiclass, class halls, bounties, legendary drops, trials, raids, home biome (§9, §9.1,
>   §9.2).** Backlog, unchanged.
>
> ### Constraints the saga imposes on anything added here
>
> - **No AssetBundle pipeline** (standing decision 2026-09-20): reuse shipping prefabs. Named
>   creatures are recoloured through `CreatureDressing` only.
> - **Power is loaned**: every class effect must leave no trace when the run ends.
> - **No line the world does not back**: every hint and NPC line describes something that happens.
> - **Read the IL before building**: `libraries/assembly_valheim.dll`, never memory.
> - **Story in three voices** (epigraph / character / chapter) — see the story bible.


Context: Martin is patching Valheim's `assembly_valheim.dll` with Mono.Cecil (no BepInEx/Harmony). Goal: add an RPG-style class system. This doc captures the design + starting code from a chat session; it's meant to be handed to Claude Code as the brief.

**Caveat:** all Valheim field/method names below are from memory of the decompiled assembly. Verify each one against the game's current version in dnSpy/ILSpy before relying on it.

---

## 1. Architecture

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

Keep the Cecil patch thin. Inject a single call from the game assembly into a separate mod assembly and put all real logic there.

```
assembly_valheim.dll   (patched: one call site per hook)
        |
        v
ValheimClasses.dll     (all class logic, lives in Managed/)
```

Rationale: re-patching after a game update is one small Cecil script re-run, not a big IL diff.

### Hooks needed

| Hook | Purpose |
|---|---|
| `Player.Awake` (end) | Apply local player's class (status effect, keybinds) |
| `Player.Load` or ZDO read in `Player.Update` | Other players' class (visuals/effects on remote clients) |
| `ObjectDB.CopyOtherDB` / `ZNetScene.Awake` | Register custom `StatusEffect`s (must run after `ObjectDB.Awake`; **not** in `Player.Awake`, or it's missing on main-menu → world-load path) |
| `Character.RPC_Damage` / `HitData` | On-hit effects, conditional damage (e.g. Berserker low-HP scaling) |
| `Player.Update` | Active ability keybinds |
| `Humanoid.EquipItem` / `Player.CanEquip` | Gear restrictions per class |
| `Skills.Awake` | Register new `SkillDef`s if adding custom skills |

### Where the systems live

- **Class storage (local):** `Player.m_customData` — `Dictionary<string,string>`, saved with the character. Key: `"class"`.
- **Class storage (networked):** `p.m_nview.GetZDO().Set("vc_class", cls)` so remote clients can read it.
- **Passives:** `SE_Stats` (a `StatusEffect` subclass) supports modifiers for damage, speed, HP/stamina/eitr regen, carry weight, damage-type resistances. Permanent when `m_ttl = 0`.
- **Skills:** `Skills.SkillType` is an enum → new values can be injected with Cecil; register `SkillDef`s in `Skills.Awake`.
- **Actives:** hook `Player.Update` for a keybind, or reuse the Forsaken Power pattern (`Player.UseGuardianPower`) — Forsaken Powers are essentially class abilities already.

### Gotchas

- **Inlining:** small getters may be inlined by the JIT; patch the caller instead or mark with `[MethodImpl(MethodImplOptions.NoInlining)]`.
- **Idempotent patching:** add a marker (e.g. dummy `[ClassModPatched]` attribute on `Player`) so the patcher can detect an already-patched DLL and skip.
- **Multiplayer:** anything touching damage/HP must run on the server too (dedicated server ships the same assembly). Unpatched clients will desync. Class ID must be in the ZDO.
- **`SE_Stats` limits:** no "on hit" triggers → conditional/on-hit logic goes in `RPC_Damage` hooks.

---

## 2. Cecil injection stub (patcher)

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

```csharp
using Mono.Cecil;
using Mono.Cecil.Cil;

var res = new DefaultAssemblyResolver();
res.AddSearchDirectory(managed); // .../Valheim/valheim_Data/Managed

var game = AssemblyDefinition.ReadAssembly(
    Path.Combine(managed, "assembly_valheim.dll"),
    new ReaderParameters { AssemblyResolver = res, ReadWrite = true });
var mod = AssemblyDefinition.ReadAssembly(
    Path.Combine(managed, "ValheimClasses.dll"));

// static void OnPlayerAwake(Player p)
var hook = mod.MainModule.GetType("ValheimClasses.Entry")
    .Methods.First(m => m.Name == "OnPlayerAwake");
var awake = game.MainModule.GetType("Player")
    .Methods.First(m => m.Name == "Awake");

var il = awake.Body.GetILProcessor();
// Patch every ret so early returns are covered too.
foreach (var ret in awake.Body.Instructions
             .Where(i => i.OpCode == OpCodes.Ret).ToList())
{
    il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
    il.InsertBefore(ret, il.Create(OpCodes.Call,
        game.MainModule.ImportReference(hook)));
}

game.Write(); // in-place; back up the original first
```

TODO for Claude Code:
- Generalise into a `Hook(typeName, methodName, hookMethod, position)` helper (Prefix / Postfix / every-ret).
- Add the marker-attribute check.
- Make backup + restore (`assembly_valheim.dll.orig`).
- Add the other hooks from the table in §1.

---

## 3. SE_Stats-based class definitions (mod assembly)

```csharp
static SE_Stats Make(string name, Action<SE_Stats> cfg)
{
    var se = ScriptableObject.CreateInstance<SE_Stats>();
    se.name = name; se.m_name = name;
    se.m_ttl = 0f;      // permanent
    se.m_icon = null;   // set a sprite to show it in the HUD
    cfg(se);
    ObjectDB.instance.m_StatusEffects.Add(se);
    return se;
}

// Example: Berserker
Make("SE_Berserker", s => {
    s.m_damageModifier = 1.15f;          // outgoing damage
    s.m_speedModifier = 0.05f;
    s.m_staminaRegenMultiplier = 1.1f;
    s.m_mods.Add(new HitData.DamageModPair {
        m_type = HitData.DamageType.Blunt,
        m_modifier = HitData.DamageModifier.Weak }); // trade-off
});
```

Apply in `Entry.OnPlayerAwake(Player p)`:

```csharp
string cls = p.m_customData.TryGetValue("class", out var c) ? c : "none";
if (cls != "none")
    p.GetSEMan().AddStatusEffect(("SE_" + cls).GetStableHashCode(), true);
p.m_nview.GetZDO().Set("vc_class", cls); // for other clients
```

Registration (`Make(...)` calls) must run from an `ObjectDB`-ready hook, not from `OnPlayerAwake`.

TODO for Claude Code:
- Class picker: simplest is a console command via `Terminal.ConsoleCommand("setclass", ...)`; a small IMGUI panel on first spawn is the nicer option.
- Verify `SE_Stats` field names (`m_damageModifier`, `m_speedModifier`, `m_healthRegenMultiplier`, `m_staminaRegenMultiplier`, `m_eitrRegenMultiplier`, `m_addMaxCarryWeight`, `m_mods`, `m_percentigeDamageModifiers`, `m_modifyAttackSkill`).

---

## 4. Class roster (design)

| Class | Fantasy | Mechanics (passive / active / trade-off) |
|---|---|---|
| **Berserker** | Fenris-armor feel | +15% dmg, dmg scales up as HP drops; can't block; weak to blunt |
| **Húskarl** | Shield-wall tank | wider parry window, cheaper block stamina; slower sprint |
| **Völva** | Seiðr caster | +eitr regen, small base eitr from the start (magic not locked behind Mistlands); reduced max HP |
| **Veiðimaðr (Hunter)** | Ranger | faster bow draw, faster sneak, highlight animals/tracks, +creature drops |
| **Skald** | Support | aura: extends Rested + stamina regen buff for nearby players; weak solo, strong in a crew |
| **Sæfari (Seafarer)** | Ocean specialist | swim stamina, faster upwind sailing, fishing boost, cold resist on water |
| **Smiðr (Builder)** | Base builder | cheaper build costs, higher structural integrity, cheaper repairs, extra comfort in own builds |

Design notes:
- Skald and Sæfari are the most interesting: they buff activities that exist but nobody specialises in (co-op support, the ocean biome).
- Every class should have a real trade-off so "none" isn't strictly worse than picking one, and so classes matter in group play.
- Passives that `SE_Stats` can express: Berserker (base), Húskarl (partially — parry window needs a `Humanoid.BlockAttack` hook), Völva (eitr regen yes, base eitr needs `Player.GetTotalFoodValue` or `Player.SetMaxEitr` hook), Sæfari (partial), Smiðr (needs `Player.HaveRequirements` / `Piece` cost hooks).

---

## 5. Class-gated item system

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

Goal: items/recipes/build pieces depend on class — e.g. a Skald only sees Skald-usable recipes at the workbench and cannot equip other classes' exclusive gear.

### Hooks

| Hook | Purpose |
|---|---|
| `Player.GetAvailableRecipes(ref List<Recipe>)` | Filter recipe list before `InventoryGui.UpdateRecipeList` renders it → disallowed items never appear in the crafting UI |
| `Player.HaveRequirements(Recipe, bool discover, int qualityLevel)` | Return `false` for disallowed items so console/other code paths can't craft them either |
| `Humanoid.EquipItem(ItemDrop.ItemData, bool triggerEquipEffects)` | Return `false` + `MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "...")` for gear the class can't use. Pickup/trade still allowed. |
| `PieceTable.UpdateAvailable` / `Player.UpdateAvailablePiecesList` | Class-only build pieces (e.g. Smiðr structures) |
| `ItemDrop.ItemData.GetTooltip` | Append "Class: Skald" line so restrictions are visible in inventory |
| on class change | Iterate `p.GetInventory().GetAllItems()`, `UnequipItem` anything now illegal |

### Data: don't put restrictions on the item

`ItemDrop.ItemData.SharedData` is populated from Unity prefabs, so a Cecil-added `m_classMask` field would just default to 0 for every item. Keep a table in the mod instead:

```csharp
[Flags] enum ClassMask { None=0, Berserker=1, Huskarl=2, Volva=4, Hunter=8, Skald=16, Saefari=32, Smidr=64, All=127 }

// prefab name (ItemDrop.name / Recipe.m_item.name, e.g. "SwordBronze") -> allowed classes
static Dictionary<string, ClassMask> ItemRules; // loaded from JSON in the mod folder

static bool Allowed(string prefab, ClassMask cls) =>
    !ItemRules.TryGetValue(prefab, out var m) || (m & cls) != 0;
```

- Unlisted items → usable by everyone (tools, food, materials, base gear stay class-agnostic).
- Match on prefab name, not `m_shared.m_name` (localisation token), for stability.
- Store the rules in JSON so balancing doesn't need a rebuild.

### Design

- Most gear stays shared; each class has an exclusive branch:
  - Skald: horns/lyres (equipping triggers the aura)
  - Húskarl: tower shields, heavy armor tiers
  - Völva: staves available earlier than Mistlands
  - Hunter: bow tiers, tracking items
  - Sæfari: harpoon, sailing gear
  - Smiðr: exclusive build pieces
- Optional **soft tier**: off-class gear usable but with an `SE_Stats` penalty instead of a hard block — less frustrating in co-op. Implement as a second table `SoftRules` applied in `EquipItem` (apply/remove a penalty SE).
- Loot is never blocked from pickup, so items flow to the right class via trading.

TODO for Claude Code:
- Verify signatures of `GetAvailableRecipes`, `HaveRequirements`, `EquipItem`, `UpdateAvailablePiecesList`, `GetTooltip` in current assembly.
- `EquipItem` hook is a **prefix** (needs early-return support in the Cecil `Hook(...)` helper: inject `ldarg.0; ldarg.1; call bool Check(...); brtrue skip; ldc.i4.0; ret; skip:`).
- Write `items.json` with an initial rule set for the exclusive branches above.

---

## 6. Class item sets

Existing custom items from Martin's other session: **Thor's Bow** and a **2h shield that prevents using weapons**. These slot in as Hunter and Húskarl signature items.

Each class: one signature (endgame) item, 2–3 support items, and a quest chain that unlocks the signature recipe. Tiered to biome progression so every class has something to chase per tier.

| Class | Signature | Support items | Tier |
|---|---|---|---|
| **Berserker** | *Blóðøx* — 2h axe; heal on kill; +attack speed below 50% HP | Wolf-pelt chest (+dmg when HP < 50%); Rage horn (consumable: 30s frenzy, no block); Blood-iron gauntlets | Mountain / Plains |
| **Húskarl** | *Bulwark* — 2h tower shield, **no weapon slot**, huge block, shield-bash active (stagger AoE) | Hird round shield (Bronze); Oath-spear (1h, pairs with round shield); Chieftain's helm (+block stamina) | Bronze → Silver |
| **Völva** | *Staff of Seiðr* — eitr staff at Swamp tier (Yggdrasil wood + Guck + Ancient bark) | Bone wand (Meadows, tiny eitr attack); Runecloak (+eitr regen); Ancestor's mask (see spirits/ghosts glow) | Meadows → Swamp → Mistlands |
| **Hunter** | *Thor's Bow* — lightning arrows (chain to nearby target) | Tracker's hood (sneak + highlight animals); Bone arrows (Meadows); Trapper's knife (+creature drops); Finewood longbow (Black Forest) | Meadows → Mountain |
| **Skald** | *Gjallarhorn* — activates the crew aura (stamina regen + Rested extension for nearby players) | Lyre (play at campfire: extends Rested for everyone); Bard's cloak (+carry weight, +move speed); Mead of Bragi (recipe: group heal-over-time) | Black Forest → Plains |
| **Sæfari** | *Harpoon of Rán* — upgraded harpoon, can reel serpents, no stamina drain while pulling | Sealskin coat (cold + wet immunity); Tideglass (shows wind direction + serpent proximity); Fishing spear | Black Forest → Ocean/Mistlands |
| **Smiðr** | *Masterhammer* — build costs −25%, longer placement reach, instant repair | Dvergr apron (+comfort in own builds); Anvil (exclusive station); exclusive build pieces (reinforced walls, long-span roofs) | Bronze → Mistlands |

### Creating items without asset bundles

Pure Cecil, no BepInEx → no easy AssetBundle loader. Start with **prefab clones**:

```csharp
var src = ObjectDB.instance.GetItemPrefab("Bow");
var go = UnityEngine.Object.Instantiate(src, hiddenParent); // inactive holder object
go.name = "ThorsBow";
var drop = go.GetComponent<ItemDrop>();
drop.m_itemData.m_shared = Clone(drop.m_itemData.m_shared); // deep-copy SharedData
drop.m_itemData.m_shared.m_name = "$item_thorsbow";
drop.m_itemData.m_shared.m_damages.m_lightning = 40f;
ObjectDB.instance.m_items.Add(go);
ZNetScene.instance.m_prefabs.Add(go); // + m_namedPrefabs by hash
// Recipe:
var r = ScriptableObject.CreateInstance<Recipe>();
r.name = "Recipe_ThorsBow"; r.m_item = drop; r.m_amount = 1;
r.m_craftingStation = ...; r.m_minStationLevel = 2;
r.m_resources = new[] { new Piece.Requirement { m_resItem = fineWood, m_amount = 10 }, ... };
ObjectDB.instance.m_recipes.Add(r);
```

- Must run after `ObjectDB` and `ZNetScene` are ready, on **both** client and server (ZNetScene needs the prefab hash on all peers or items vanish on sync).
- Localisation: `Localization.instance.AddWord("item_thorsbow", "Thor's Bow")` (verify method name).
- Stats-only variants work fine with clones. Custom models later → Unity AssetBundle + `AssetBundle.LoadFromFile` in the mod DLL.

### Signature item behaviours needing hooks (not expressible via SharedData)

- Bulwark "no weapons": `Humanoid.EquipItem` prefix — if equipping a weapon while Bulwark equipped (or vice versa), refuse. Shield-bash active in `Player.Update`.
- Blóðøx heal-on-kill: `Character.OnDeath` / `Character.Damage` hook, check `HitData.GetAttacker()` weapon.
- Thor's Bow chain lightning: `Projectile.OnHit` hook or `HitData` post-process in `Character.RPC_Damage`.
- Gjallarhorn / Lyre auras: timer in `Player.Update`, apply SE to `Player.GetAllPlayers()` within radius.
- Tideglass / Tracker's hood highlights: `Player.Update` + `Character.GetAllCharacters()` filtered by type, draw via existing `Hud` or a simple overlay.

---

## 7. Class quests (unlock signature items)

**Decision (Martin):** reuse the existing quest-NPC flow from the other session — two items already come from quest NPCs. No token items. Each signature item is handed out by an NPC, with a **different ingredient requirement per item/class**.

So the pieces are:

- NPC offers the class's signature item only if `player class == item class` (reuse `Allowed(prefab, cls)` from §5 in the NPC's dialogue/offer check).
- Ingredient requirements per item are the "quest": e.g. Bulwark = Iron ×20 + Fenring trophy + Wolf pelt ×10; Thor's Bow = Fine wood ×20 + Drake trophy + Silver ×10 + Thunder stone. Store as `Piece.Requirement[]` per item in the same JSON as `items.json` (`"requirements"` block) so balancing is data-only.
- Optional later: quest-chain steps (kill counts etc.) below can gate *when the NPC offers* the item, tracked in `m_customData` — but not needed for v1.

```csharp
// m_customData keys (only if step-based quests are added later)
"class"          -> "huskarl"
"quest.huskarl"  -> "2/5"
```

Ideas for later step-based chains (not v1):
| Class | Quest chain |
|---|---|
| Berserker | Kill 20 enemies with a 2h weapon → kill a Fenring → deliver trophy at Moder's altar |
| Húskarl | Hold the ground: kill 15 enemies without leaving a 10 m radius of an altar → block 100 hits |
| Völva | Read 3 runestones → kill Bonemass → craft 5 meads |
| Hunter | Hunt named beasts: 2★ wolf, drake, lox → 50 headshots (`HitData.m_headshot`? verify) |
| Skald | Visit every boss altar (vegvisir/locations) → have 3 players Rested at your campfire |
| Sæfari | Kill a serpent → touch the shore of every biome by boat → fish 3 species |
| Smiðr | Craft every crafting station tier → build a 200-piece structure → repair 500 pieces |

### Hooks for quest progress

| Event | Hook |
|---|---|
| Kills | `Character.OnDeath` / `Character.Damage` (check attacker is local player, weapon type) |
| Blocks | `Humanoid.BlockAttack` |
| Runestones | `RuneStone.Interact` |
| Locations/altars | `Player.Update` distance check against `ZoneSystem.instance` boss locations, or `Vegvisir.Interact` |
| Crafting | `Player.OnInventoryChanged` / `InventoryGui.DoCrafting` |
| Building/repair | `Player.PlacePiece` / `Player.Repair` |
| Fishing | `Fish.OnHooked`/`FishingFloat` (verify) |
| Boats/biomes | `Player.Update` when `p.GetStandingOnShip() != null`, check `Heightmap.FindBiome` |

Quest UI: reuse `MessageHud.instance.ShowMessage(TopLeft, "Húskarl: 3/5 enemies held")` for progress; a full quest log panel is optional later.

---

## 8. Class skills

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

Valheim's skill system: `Skills.SkillType` enum, `Skills.m_skills` (list of `SkillDef`: `m_skill`, `m_icon`, `m_description`, `m_increseStep`), XP via `Skills.RaiseSkill(SkillType, float factor)`, effect via `Player.GetSkillFactor(SkillType)` (0–1). Skills are saved per character; custom enum values persist as ints, so keep the values stable (use 700+ to avoid vanilla collisions; verify current max).

Three layers per class:

1. **Favoured skills** — vanilla skills that level faster for the class (hook `Skills.RaiseSkill` prefix: multiply `factor` by e.g. 1.5 for favoured, 0.75 for off-class). Cheap, no new content.
2. **One custom skill** — new enum value + `SkillDef` registered in `Skills.Awake`; XP raised from the class's hooks; factor read in the class's passive/active code.
3. **Level perks** — thresholds on the custom skill (25/50/75/100) unlock the active's upgrades. Checked in `Player.Update` / relevant hooks, no extra state.

| Class | Favoured vanilla | Custom skill (raised by) | Perks at 25 / 50 / 75 / 100 |
|---|---|---|---|
| **Berserker** | Axes, Clubs, Run | *Frenzy* (damage dealt while < 50% HP) | Rage horn cooldown −25% / lifesteal 5% / stagger immunity in frenzy / frenzy no longer disables block |
| **Húskarl** | Blocking, Spears, Swords | *Shield-wall* (successful blocks/parries) | Shield-bash unlocked / parry window +50% / nearby allies get block bonus / Bulwark reflects 20% |
| **Völva** | Elemental Magic, Blood Magic, Sneak | *Seiðr* (eitr spent) | Base eitr +25 / staff cost −20% / summon 1 extra skeleton / spells chain |
| **Hunter** | Bows, Crossbows, Sneak, Knives | *Tracking* (creature kills, headshots) | Animals highlighted at 30 m / draw speed +20% / trophies double / Thor's Bow chain to 3 targets |
| **Skald** | Run, Jump, Swim (support mobility) | *Saga* (time with 2+ players in aura) | Aura radius +5 m / Rested bonus +1 lvl / group HoT on Gjallarhorn / aura also gives +10% dmg |
| **Sæfari** | Swim, Fishing, Spears | *Seamanship* (distance sailed, serpents killed, fish caught) | Ship speed +10% / no cold on water / serpent meat buff x2 / can ram (ship deals dmg) |
| **Smiðr** | Wood Cutting, Pickaxes, Unarmed | *Craftsmanship* (pieces placed/repaired, items crafted) | Build cost −10% / +integrity / Masterhammer instant repair AoE / exclusive Tier-4 pieces |

### Adding a custom SkillType with Cecil

```csharp
var skillType = game.MainModule.GetType("Skills").NestedTypes.First(t => t.Name == "SkillType");
var f = new FieldDefinition("Frenzy",
    FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault,
    skillType) { Constant = 701 };
skillType.Fields.Add(f);
```

The mod DLL can then just cast: `(Skills.SkillType)701`. Registration in the `Skills.Awake` postfix:

```csharp
skills.m_skills.Add(new Skills.SkillDef {
    m_skill = (Skills.SkillType)701,
    m_icon = someSprite,           // required or the skills UI throws
    m_description = "Damage dealt while enraged.",
    m_increseStep = 1f
});
```

- `Skills.GetSkillDef` and the skills window iterate `m_skills`; unknown enum values with no `SkillDef` return null → NRE, so register before any `RaiseSkill` call.
- Skill names in the UI come from `"$skill_" + type.ToString().ToLower()` → add localisation words `skill_frenzy` etc.
- The `RaiseSkill` prefix (favoured multipliers) needs the current class → read from `m_customData` once and cache on class change.

### 8.0 Unlock ladder (per class, table-driven)

Every class shares the same level thresholds; what unlocks at each is per-class JSON (`level -> [unlock ids]`).

| Level | Unlock |
|---|---|
| 1 | Class chosen: passive SE, favoured skills, item branch visible at workbench |
| 10 | Ability 1 (§13) — class is passive-only until then |
| 25 | Perk 1 · first exclusive support item craftable |
| 40 | Class hall core piece (§9.2) |
| 50 | Ability 2 · Perk 2 · class trial · NPC offers the signature item (ingredients still required) |
| 60 | Second support item · cosmetic (cape tint / title) |
| 75 | Ability 3 · Perk 3 · hard-tier bounties |
| 90 | Legendary variant can drop |
| 100 | Perk 4 · multiclass · "Master" title |

Design rules:
- Nothing stat-critical before 25 — a fresh character must be viable.
- With the boss-based cap (§8.1), level 50 lands ~Bonemass and 100 at the final boss, so the ladder paces itself to world progression.

Implementation:
- `RaiseSkill` postfix: compare `floor(level)` before/after; on crossing a threshold, set `m_customData["unlock.<cls>.<id>"] = "1"` and `MessageHud.ShowMessage(Center, "$class_skald: Gjallarhorn unlocked")`.
- `HasUnlock(cls, id)` helper queried by: item rules (§5 — a gated item also needs its unlock), NPC offers (§7), build pieces (§9.2), active keybind, perk checks.
- Unlock ids in JSON so the ladder is rebalanced without a rebuild; loading a character re-derives unlocks from current level (handles cap/respec changes).

### 8.1 XP model

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

Valheim has no character XP, only skills. Two options considered:

- **A. Skill-only (native):** the custom skill *is* the class level. XP from class-relevant actions. No new save data; skill window, death penalty, No-Skill-Drain all work unchanged. Downside: some classes level slowly/farmably (Skald "time in aura", Smiðr "pieces placed").
- **B. Class XP pool:** separate `m_customData["xp"]` with level thresholds. Full pacing control, but a second track to save, sync, explain and display.

**Decision: A with feeders.** Single track (the custom skill), but non-combat sources push skill XP via `Skills.RaiseSkill(customSkill, amount)`:

| Source | Skill XP (factor) |
|---|---|
| Organic class action (kill in frenzy, block, sail, place piece) | 1.0 per event (vanilla-style) |
| Bounty completed | 10 |
| Quest / NPC item obtained | 25 |
| First kill of a new creature type (Hunter), first biome coast (Sæfari), first station tier (Smiðr) | 5 |
| Mentoring (near skill-100 same-class player) | ×1.25 on organic |

`RaiseSkill` already handles the curve, the "skill up" popup and death loss, so nothing else is needed.

**Tuning knobs (JSON):**
- XP factor per source (table above)
- Favoured-skill multipliers: primary ×1.5, secondary ×1.25, off-class ×0.75
- Death loss for custom skills (vanilla 5%; consider 10% per the death-twist idea in §9)
- **Level cap by progression:** custom skill cap = `highestBossKilled × 20` (Eikthyr 20 … Fader/Queen 100) — stops hitting 100 in the Meadows. Implement in the `RaiseSkill` prefix (clamp) reading `ZoneSystem.GetGlobalKey("defeated_*")`.
- Multiclass unlock threshold (default 100, casual 75)

---

## 9. Ideas backlog (not scheduled)

Roughly cheapest first.

| Idea | What | How | Notes |
|---|---|---|---|
| **Class trainer NPC** | Pick / respec class via the existing quest NPC instead of a console command | Add "choose class" and "change class" dialogue options; respec costs a boss trophy | Removes the need for a class-picker UI |
| **Visible class** | Other players can see your class at a glance | Tint cape/trim material per class, or prefix a rune to the nameplate via `Player.GetPlayerName` / `EnemyHud` | Reads instantly in co-op |
| **Crew bonuses** | Composition buffs when 2+ different classes are nearby | Same aura loop as Skald: e.g. Húskarl+Skald = "Hird" (block bonus), Berserker+Hunter = "Raiding party" (+speed), Völva+Smiðr = "Wardens" (+base integrity) | Encourages mixed groups |
| **Class-flavoured Forsaken Powers** | Each class gets a twist on a boss power | Small hooks in the `SE_*` guardian-power status effects: Hunter's Eikthyr silences footsteps, Sæfari's Moder calms waves, Völva's Bonemass adds eitr | Reuses existing content |
| **Home biome** | Each class has one biome where it's strongest (not gated, just buffed) | `Heightmap.FindBiome(pos)` check in `Player.Update`, apply a small SE: Sæfari→Ocean, Völva→Swamp, Hunter→Black Forest, Berserker→Mountain, Smiðr→any zone with own build pieces | Gives each class a reason to lead in "their" biome |
| **Death penalty twist** | Class skills lose more on death than vanilla, but the signature item stays with you | `Skills.OnDeath` hook (bigger loss for custom skill), `Player.OnDeath`/tombstone hook to keep signature item in inventory | Identity without grind |
| **Prestige / multiclass** | At custom skill 100, allow a secondary class at 50% strength | Second `m_customData` key `"class2"`, apply its SE with halved modifiers | Long-tail goal for a persistent server |
| **Class Hugin tips** | Raven gives class-specific hints on first spawn | Hook `Raven`/tutorial list to append class entries | Free onboarding |

### 9.1 Multiclassing (promoted — Martin wants this)

- **Unlock:** primary class's custom skill reaches 100 (or 75 on a casual server — make it a config value). Chosen via the trainer NPC; costs the secondary's signature-item ingredients or a boss trophy.
- **Secondary grants:**
  - its `SE_Stats` passives at 50% (build the SE with halved modifiers, registered as `SE_<class>_Half`)
  - favoured-skill XP ×1.25 (primary stays ×1.5)
  - access to its item branch — `Allowed()` checks `primaryMask | secondaryMask`
  - its custom skill, levelling normally, but perks only up to 50
- **Secondary does not grant:** its active ability (keep one active per player; secondary is passives + items), its home-biome bonus, its crew-bonus role.
- **Data:** `m_customData["class2"]`, ZDO `"vc_class2"`.
- **Named combos** (table-driven, same code as crew bonuses): small extra bonus + title shown on nameplate.

| Combo | Title | Bonus |
|---|---|---|
| Berserker + Húskarl | Jarl | Can block during frenzy |
| Völva + Skald | Gothi | Aura also restores eitr |
| Hunter + Sæfari | Whaler | Harpoon + bow crit vs sea creatures |
| Smiðr + Völva | Runesmith | Builds have +10% integrity, ward radius up |
| Berserker + Hunter | Raider | +speed while weapon drawn |
| Húskarl + Skald | Hersir | Crew block bonus doubled |

- **Respec:** swapping the secondary resets its custom skill to 0; the primary is permanent (or a very expensive reset via the NPC).
- **Implementation order:** after item gating and skills are stable, since it's mostly "apply the same systems twice with a multiplier".

### 9.2 World-level ideas

| Idea | What | How | Notes |
|---|---|---|---|
| **Class halls** | Each class can build one special structure (Skald mead hall, Völva seiðr circle, Smiðr great forge, Húskarl shield-wall, Hunter lodge, Sæfari boathouse, Berserker pit) | Exclusive build pieces + one "hall core" piece; top-tier upgrade of the signature item is only possible while standing in the hall (check `Player.GetNearbyPieces` / a `CraftingStation` with class condition) | Gives base-builders class-relevant work. **Worth building.** |
| **Bounties** | Repeatable "kill a 2★ X in biome Y" from the trainer NPC, class-appropriate rewards | NPC dialogue option; store active bounty in `m_customData["bounty"]`; complete via `Character.OnDeath` hook (check level + biome) | Endgame loop reusing NPC + kill hooks. **Worth building.** |
| **Class raids** | Random events weighted by which classes are online (Húskarl → shielded draugr, Völva → wraiths, Hunter → wolf packs) | Extra `RandomEvent` entries in `RandEventSystem` with a class condition (`m_requiredGlobalKeys` won't do it → hook `RandEventSystem.GetPossibleRandomEvents`) | Cheap flavour |
| **Legendary drops** | Rare boss-only version of each signature item with one extra modifier (Blóðøx fires a wave, Thor's Bow chains to 5) | Prefab clone with extra `SharedData` + hook behaviour; add to boss `CharacterDrop` list, chance scaled by custom skill | Long-tail chase |
| **Class trials** | Solo arena location per class, unlocked at custom skill 50; reward cape cosmetic + title | Reuse `Location` prefabs; spawn waves via `SpawnArea`; entry gated by class + skill | Needs some level design |
| **Mentoring** | Skill-100 player near a same-class low-level player gives them +XP | `RaiseSkill` prefix: check nearby players' class/skill | Pulls veterans toward new joiners |
| **Server-wide class saga** | Shared counters per class ("Hunters of this world have slain 1,000 beasts") with milestones unlocking cosmetics for that class | World ZDO / `ZoneSystem.GlobalKeys` counters, synced via RPC; milestones in JSON | Community glue |
| **Class vehicles** | Sæfari rows faster / ship handles better; Hunter can draw bow while riding lox | `Ship.Update` speed modifier by crew class; `Player.Update` riding + attack check | Small hooks |

Priority within 9.2: class halls and bounties first; the rest are seasoning.

---

## 10. Engineering / tooling

> **Superseded for the saga (2026-09-27)** — see the Integration notes at the top. Kept as reference.

Do these early; they pay for themselves quickly.

- **Dev loop:** one script — run patcher → copy `ValheimClasses.dll` + JSON to `Managed/` → launch Valheim with `-console`. Plus a dev console command `vc reload` that re-reads all JSON tables live.
- **Test world:** saved world with one character per class at fixed levels; cheat commands `vc setclass <cls>`, `vc setlevel <skill> <n>`, `vc unlock <id>` to reach any state without grinding.
- **Logging:** everything through `ZLog.Log("[VC] ...")` so it's greppable in `Player.log`; `vc dump` prints class, secondary, skill levels, unlocks, active SEs.
- **Version guard:** patch marker attribute stores `Version.CurrentVersion` at patch time; the mod DLL refuses to load (with a `MessageHud` warning) if the running game version differs → no silent NREs after a Steam update.
- **Server config:** `server.json` holds tuning knobs (caps, XP factors, multipliers, multiclass threshold); server sends it to clients on join via a custom `ZRoutedRpc` so all peers use the same rules.
- **Compat note:** Harmony patches from BepInEx mods generally coexist with Cecil-patched methods, but document it in the README to avoid ghost-chasing later.
- **Docs:** `CLAUDE.md` in the repo pointing at this plan, plus a `verified-names.md` that grows as assembly names are confirmed — the "from memory" caveat at the top of this doc should shrink to zero.

---

## 12. Story — Act 1: Meadows

Premise: Odin wants a *hird* rebuilt, not just the Forsaken dead. The old hird died in the Meadows; their graves are the class lore.

| Beat | What happens | Systems it uses |
|---|---|---|
| 1. The Wanderer | Trainer NPC = the hird's last survivor, an old thane camped at a burned longhouse. First task: bring him something from the world (deer hide / greyling trophy / a comrade's gear piece) | Existing quest NPC |
| 2. Seven graves | Seven runestones across the Meadows, one per fallen hird member, each hinting a class in ~3 lines. Reading all seven = Act 1 exploration quest | `RuneStone.Interact` hook, `m_customData["graves"]` bitmask |
| 3. Choosing a legacy | At the thane, pick whose legacy to carry = class choice, framed as taking up a dead warrior's name. He gives their first item | NPC class picker (§9), NPC-given items (§7) |
| 4. The proving | Class-flavoured Meadows task: Berserker kills 10 boars with an axe, Húskarl blocks 30 hits, Völva reads the graves at night, Hunter kills 5 deer with headshots, Skald gets the thane Rested at his fire, Sæfari launches a raft, Smiðr rebuilds the longhouse hearth. Reward: level-10 active | Unlock ladder (§8.0), quest hooks (§7) |
| 5. Eikthyr | Vanilla fight; the thane walks to the altar and says one class-specific line before it | NPC dialogue table |
| 6. Epilogue hook | The thane mentions a second survivor went into the Black Forest → Act 2's NPC | — |

Tone: sparse, Norse, told through runestones and one NPC — no cutscenes. Three-line rune texts, terse NPC.

Ties to later systems:
- Class halls (§9.2) = rebuilding the hird's longhouse, wing by wing.
- Multiclassing (§9.1) = "carrying two legacies"; the second grave's name is added to yours.
- Each later act's NPC is another survivor; Act 5/6 could end with the hird sworn again at the longhouse before the final boss.

Grave names (placeholders, one per class): Ulfr (Berserker), Halvard (Húskarl), Sigrún (Völva), Eydís (Hunter), Bragi-son Ormr (Skald), Ragna (Sæfari), Dvalinn (Smiðr).

---

## 13. Active abilities (AoE kit)

Martin's ideas (2026-09-27): slow AoE for Hunter (bow), lightning AoE, heal AoE, Rend (Berserker small-dmg AoE), run-speed AoE (Skald), armor song (Skald), pet-buff AoE (Hunter) — and only Hunters can summon pets.

This replaces "one active per class" with an ability kit: **3 abilities per class, unlocked at 10 / 50 / 75** on the custom skill. Skald's abilities are songs; Hunter's are calls.

### Ability table

| Class | Lvl | Ability | Effect | Target |
|---|---|---|---|---|
| **Hunter** | 10 | *Snare Volley* | Next bow shot leaves a 6 m frost zone for 8 s: enemies inside are slowed (SE_Frost-style) | Enemies |
| | 50 | *Call of the Wild* | Summon 2 tamed wolves for 60 s (like Dead Raiser skeletons). **Only Hunters can summon pets** | Self |
| | 75 | *Pack Howl* | Tamed animals within 15 m get +30% dmg, +30% HP regen, +speed for 30 s | Tames |
| **Berserker** | 10 | *Rend* | 360° sweep, 5 m: small physical dmg + bleed (DoT) to all enemies | Enemies |
| | 50 | *Blood Rage* | Frenzy on demand: +dmg, no block, 15 s | Self |
| | 75 | *Warcry* | Stagger all enemies within 8 m, taunt to self | Enemies |
| **Völva** | 10 | *Mending* | Heal AoE: allies within 10 m +40 HP over 6 s (blood magic flavour, costs eitr) | Allies |
| | 50 | *Thor's Wrath* | Lightning AoE: strike at target point, 6 m, lightning dmg + short stagger (costs eitr) | Enemies |
| | 75 | *Ward* | Damage shield on allies within 10 m (reuse Staff of Protection SE) | Allies |
| **Skald** | 10 | *Marching Song* | Run-speed AoE: allies within 15 m +25% move speed, no stamina drain while running, 20 s | Allies |
| | 50 | *Armor Song* | Allies within 15 m +armor / +block power for 20 s | Allies |
| | 75 | *Saga of Bragi* | Group HoT + Rested refresh (from §6 Mead of Bragi idea) | Allies |
| **Húskarl** | 10 | *Shield Bash* | Stagger cone in front | Enemies |
| | 50 | *Shield Wall* | Allies within 6 m get +block, projectiles absorbed | Allies |
| | 75 | *Last Stand* | Can't drop below 1 HP for 5 s, then big heal | Self |
| **Sæfari** | 10 | *Rán's Grip* | Harpoon pull with no stamina drain, 15 s | Self |
| | 50 | *Fair Wind* | Ship within 20 m gets tailwind for 30 s | Ship |
| | 75 | *Sea Legs* | Allies on your ship: +stamina regen, cold immunity | Allies |
| **Smiðr** | 10 | *Quick Repair* | Repair all pieces within 10 m instantly | Pieces |
| | 50 | *Reinforce* | Pieces within 10 m +50% HP for 5 min | Pieces |
| | 75 | *Field Forge* | Temporary crafting station at your feet, 60 s | Self |

### Unlocking abilities: level-gated, trainer-taught

Two states per ability: **available** (skill level reached) → **learned** (taught by a trainer NPC). Perks stay automatic; only abilities need teaching.

- `AbilityDef.taughtBy` = trainer id. v1: all three abilities taught by the thane at the stones (`"thane"`).
- Later: ability 1 stays with the thane (Act 1); abilities 2 and 3 move to **class-specific survivor NPCs hidden in the world** (`"mentor_hunter"` in the Black Forest, `"mentor_saefari"` on a coast, `"mentor_volva"` in the Swamp, …). These are the other hird survivors from §12 — the Act 2+ NPCs. Changing `taughtBy` is a JSON edit; level logic untouched.
- NPC dialogue: "You are ready to learn X" appears when `level >= unlockLevel && !HasUnlock(cls, abilityId)`; learning sets the unlock (§8.0 `HasUnlock`).
- On reaching the level: one `MessageHud` line "Seek out the thane — a new skill awaits" rather than the ability itself; no mid-fight popups.
- Multiclass secondary: ability 1 taught by the thane as usual.

### Generic AoE ability system (table-driven)

One implementation covers almost all of these:

```csharp
class AbilityDef {
    string id; ClassMask cls; int unlockLevel;
    float cooldown, radius, duration, eitrCost, staminaCost;
    TargetKind target;          // Self, Allies, Enemies, Tames, Pieces, Ship
    Shape shape;                // Sphere at self, Cone in front, Point at aim (raycast)
    string applySE;             // status effect name to apply (custom SE_Stats or vanilla)
    HitData damage;             // optional
    string vfxPrefab, sfxPrefab; // reuse vanilla prefabs
}
```

- Keybinds: 3 slots (default `Z`, `X`, `C` — configurable), read in `Player.Update` postfix; cooldowns kept in memory (not saved).
- Targeting: `Sphere` → `Physics.OverlapSphere` filtered by `Character` / `Tameable` / `Piece` / `Ship`; `Point` → raycast from camera. `Allies` = `Player.GetAllPlayers()` in range (+ self); `Enemies` = `Character.GetAllCharacters()` where `!IsPlayer() && !IsTamed()`.
- Damage: build `HitData`, set `m_attacker`, call `character.Damage(hit)`; it routes through `RPC_Damage` so the server owns the result.
- Buffs on **other players**: SEs must be applied on their client — send a custom `ZRoutedRpc` (`"VC_ApplySE", seHash, duration`) to each target's owner. Same for tamed animals (owner client). Pieces/ships: `ZNetView.ClaimOwnership` then modify.
- Slow: apply vanilla `SE_Frost` (or a clone with tuned `m_speedModifier`) via a short-lived `AoEZone` object that ticks every 0.5 s and re-applies.
- Lightning VFX: reuse Thor's-bow / Moder lightning prefab (verify name, e.g. `lightningAOE`); heal VFX: `vfx_Potion_health_medium`.
- Song abilities can be **channelled** instead of instant (Skald keeps singing, aura persists, can't attack) — optional flag `channelled`.

### Pet summoning (Hunter only)

- *Call of the Wild*: `ZNetScene.Instantiate(wolfPrefab)`, then `Tameable.Tame()` + set `m_tameable.m_commandable`, and add a `SE_Summoned` with TTL that destroys the creature on expiry (mirror how Dead Raiser skeletons despawn — verify the SE/component used).
- Enforce "only Hunters summon": summons live only in the ability system, which is class-gated. Optional harder rule: hook `Tameable.Tame` / feeding so non-Hunters can't tame wild animals either (config flag).
- *Pack Howl*: iterate `Character.GetAllCharacters()` where `IsTamed()` in range; apply SE via owner RPC.

### Boons vs class abilities

**Decision (Martin):** the existing boon system from the other session is **not replaced**. Class-specific abilities move out of boons into the class tree; boons stay as the class-agnostic layer.

| | Boons (existing) | Class tree (this spec) |
|---|---|---|
| Who | Any class | Only the class (or secondary for ability 1) |
| Unlocked by | Existing boon mechanism | Custom skill level + trainer NPC |
| Content | General perks/effects usable by everyone | Passives (§3), abilities (§13), perks (§8), item branch (§5) |
| Migration | Remove any boon that is really a class ability (e.g. pet summoning, class AoEs) | Re-add it here at the right level |

- Boons and class abilities may stack; keep an eye on overlap (e.g. a boon that also buffs pets + *Pack Howl*).
- If a boon *should* be class-flavoured but general (e.g. "+10% ranged damage"), leave it a boon — only move things that define a class identity.
- ~~TODO: list current boons, tag each `keep` / `move-to-<class>`.~~ Done — see the migration table in the Integration notes.

### Changes elsewhere

- §8.0 ladder: 10 = ability 1, 50 = ability 2, 75 = ability 3 (perks stay as listed).
- §9.1 multiclass: secondary grants **ability 1 only** (at level 10 of its skill), no further.
- §6: Gjallarhorn now triggers *Saga of Bragi* at 2× radius; Thor's Bow's chain lightning is separate from Völva's *Thor's Wrath*.

---

## 11. Suggested next steps

0. Tooling first (§10): patcher script, dev loop, `vc` console commands, version guard.

1. Decompile current `assembly_valheim.dll`, confirm all names in this doc.
2. Build the patcher (§2) with the generic `Hook(...)` helper + marker + backup.
3. Build `ValheimClasses.dll` with `Entry.OnPlayerAwake`, `Entry.OnObjectDBReady`, and the `Make(...)` registrations for Berserker + Húskarl first.
4. Add `setclass` console command, test single-player.
5. Add ZDO sync + test on a dedicated server with two clients.
6. Then actives (`Player.Update` keybind) and the remaining classes.
7. Item gating (§5): `items.json` + `GetAvailableRecipes` / `EquipItem` hooks; extend the Cecil helper with prefix/early-return support first.
8. Item sets (§6): prefab-clone pipeline + Bulwark and Thor's Bow first (already have designs), then one item per class.
9. Skills (§8): favoured-skill multiplier via `RaiseSkill` prefix (cheap win), then custom `SkillType` injection + `SkillDef`s.
10. Quests (§7): class-check in the existing NPC offer, per-item ingredient lists in JSON. Step-based chains later, if at all.
