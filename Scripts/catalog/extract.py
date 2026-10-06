"""Extract gameplay MonoBehaviours from one unpacked bundle. Data only.
usage: python3 -I extract.py <bundle> <bin> <outdir>
Writes <outdir>/<bundle>.jsonl (one record per MonoBehaviour of a gameplay class) and
<outdir>/<bundle>.names.json (CAB -> {pid: name}) for cross-bundle PPtr resolution.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ufs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
MS = json.load(open(os.path.join(HERE, "..", "work", "monoscripts.json")))
MS_CAB = "CAB-f78add41ea118a2fa5f83cc81e6280d1"

SKIP_CLASSES = set("""
AccessibilitySettings AchievementDetailUnlockCondition AchievementList AchievementUnlockPopup AchievementsGui
AmplifyOcclusionEffect AnimSetTrigger AnimationEffect AnimationObjectToggle AudioMan AudioSettings BackElement
BinaryVibration BuildUi BuildUiFavoriteCategoryCheckButton BuildUiFavoritesDropdown BuildUiPieceButton BuildUiTagButton
CamShaker CameraEffects CaptionItem ChangeLog CharacterAnimEvent CinderSpawner Cinder CinematicsHider CircleProjector
ClosedCaptions ClothQualityManager ClutterSystem ConnectPanel Console DamageText DepthCamera DepthCopy
DisableInPlacementGhost DistantFogEmitter EffectFade ElementInfo EmitterRotation EmoteElement EmoteGroupConfig
EmoteMappings EmptyElement EnemyHud EulaWindow Feedback FootStep FootStepCollider FrameBufferScaler GameCamera GamepadMap
GamepadMapController GamepadMapLabel GamepadMotionSensor GamepadRumble GamepadSettings GameplaySettings Gibber
GlobalBlueNoise GlobalGraphicsConfiguration GraphicsConfiguration GraphicsSettings GraphicsSettingsManager
GraphicsSettingsPreset GraphicsSettingsPresetType GroupElement GuiBar HammerItemElement HeatDistortImageEffect Heightmap
HotbarGroupConfig HotkeyBar HoverText Hud ImpactEffect InstanceRenderer InventoryElement InventoryGrid InventoryGui
ItemElement ItemGroupConfig ItemGroupMappings ItemStyle JoinCode KeyButton KeyHints KeyHintsRadial KeySlider KeyToggle
KeyboardMouseSettings LevelEffects LightFlicker LightLod LineAttach LineConnect LiquidSurface LoadingIndicator LodFadeInOut
LongPressIndicator ManageSavesMenu ManageSavesMenuElement MaterialFader MaterialMan MaterialVariation
MaterialVariationWorld MeleeWeaponTrail Menu MenuScene MenuShipMovement MessageHud MetricsHandler Minimap MistEmitter
Mister ObjectFlicker OpenRadialConfig ParticleDecal ParticleIntensityScaler ParticleMist Pathfinding PersistentEventSystem
PersistentEventSystemDirectionHelper PlatformEnable PlayerClothWindShelter PlayerController PlayerCustomizaton
RadialBase RadialDataInitializer RadialDataSO RadialInventoryInfo RadialOverlapPreventer RadialSettings Ragdoll
RandomAnimation RandomFlyingBird RandomIdle RandomMaterialValues RandomMovement ReflectionUpdate RenderGroupSubscriber
RenderGroupSystem ReportUser ResolutionSwitchDialogTimedRemoval ScrollRectEnsureVisible ServerListGui ServerOptionsGUI
SessionPlayerList SessionPlayerListEntry SetActiveOnAwake Settings SettingsTooltip ShieldDomeImageEffect
ShieldDomeParticleColor SkillsDialog SleepText SlowUpdater Smoke SmokeRenderer SmokeSpawner SnapToGround SplitDialog
StartupMessages StaticPhysics StaticRotation StoreGui TabHandler TerrainComp TerrainLod TerrainModifier TextInput
TextsDialog ThrowElement ThrowGroupConfig ToggleImage TouchRaycastPadding UIGamePad UnifiedPopup UpscaledFrameBuffer
UserProfileDisplay ValheimRadialConfig VariantDialog VortexParticles Water WaterMark WaterTrigger WaterVolume WrapParticles
ZNetView ZSFX ZSyncAnimation ZSyncTransform TimedDestruction CharacterTimedDestruction Tail VisEquipment WearNTearUpdater
LiquidVolume EffectArea Aoe Projectile TerrainOp DropProjectileOverDistance MeteorSmash Floating GlobalWind EnvZone
""".split())

NOISE_SUFFIX = ("Effects", "Effect", "EffectsHit")


def main():
    bundle, binp, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
    bname = os.path.basename(bundle)
    t0 = time.time()
    nodes, files = ufs.open_bundle(bundle, binp)
    out = open(os.path.join(outdir, bname + ".jsonl"), "w")
    names_all = {}
    nrec = 0
    for f in files:
        names = {}
        go_name = {}
        tr_go = {}
        tr_father = {}
        go_tr = {}
        comp_go = {}
        container = {}
        mbs = []
        for o in f.objects:
            cid = f.class_of(o)
            if cid == 1:
                v = f.read(o)
                if not v:
                    continue
                go_name[o[0]] = v.get("m_Name", "")
                comps = v.get("m_Component") or []
                for c in comps:
                    pp = c.get("component") if isinstance(c, dict) else None
                    if pp:
                        comp_go[pp["m_PathID"]] = o[0]
                if comps:
                    first = comps[0].get("component")
                    if first:
                        go_tr[o[0]] = first["m_PathID"]
            elif cid in (4, 224):
                v = f.read(o)
                if not v:
                    continue
                tr_go[o[0]] = v["m_GameObject"]["m_PathID"]
                fa = v.get("m_Father")
                tr_father[o[0]] = fa["m_PathID"] if fa else 0
            elif cid == 142:
                v = f.read(o)
                for item in v.get("m_Container", []) or []:
                    k, val = item.get("first"), item.get("second") or {}
                    a = val.get("asset") or {}
                    if a.get("m_FileID") == 0 and a.get("m_PathID"):
                        container.setdefault(a["m_PathID"], k)
            elif cid == 114:
                mbs.append(o)
            elif cid in (115, 49, 28, 43, 74, 21, 48, 83, 213, 1):
                pass
        for pid, n in go_name.items():
            names[pid] = n

        def root_of(gopid):
            t = go_tr.get(gopid)
            seen = 0
            while t and tr_father.get(t) and seen < 200:
                t = tr_father[t]
                seen += 1
            return tr_go.get(t, gopid)

        def path_of(gopid, depth=6):
            parts = []
            t = go_tr.get(gopid)
            while t and len(parts) < depth:
                g = tr_go.get(t)
                parts.append(go_name.get(g, "?"))
                t = tr_father.get(t)
            return "/".join(reversed(parts))

        def resolve(pp):
            fid, pid = pp.get("m_FileID", 0), pp.get("m_PathID", 0)
            if not pid:
                return None
            if fid == 0:
                if pid in names:
                    return "@" + names[pid]
                if pid in comp_go and comp_go[pid] in go_name:
                    return "@" + go_name[comp_go[pid]]
                return "@#%d" % pid
            ext = f.externals[fid - 1].split("/")[-1] if fid - 1 < len(f.externals) else "?"
            return "ext:%s:%d" % (ext, pid)

        def slim(v, key=""):
            if isinstance(v, dict):
                if set(v.keys()) == {"m_FileID", "m_PathID"}:
                    return resolve(v)
                if "m_effectPrefabs" in v and len(v) == 1:
                    return None
                o2 = {}
                for k, x in v.items():
                    if k.endswith(NOISE_SUFFIX) and isinstance(x, dict):
                        continue
                    if k in ("m_GameObject", "m_Enabled", "m_Script", "m_EditorHideFlags", "m_EditorClassIdentifier",
                             "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset"):
                        continue
                    s = slim(x, k)
                    if s in (None, "", [], {}):
                        continue
                    o2[k] = s
                return o2
            if isinstance(v, list):
                if len(v) > 400:
                    return "<list %d>" % len(v)
                if v and all(isinstance(x, (int, float)) for x in v) and len(v) > 24:
                    return "<nums %d>" % len(v)
                return [slim(x) for x in v]
            if isinstance(v, float):
                return round(v, 3)
            return v

        # ScriptableObject names (m_Name of MBs without GO) for resolution
        decoded = []
        for o in mbs:
            ti = o[3]
            sidx = f.types[ti][1]
            cls = None
            if 0 <= sidx < len(f.scripts):
                fi, lp = f.scripts[sidx]
                ext = f.externals[fi - 1].split("/")[-1] if fi > 0 else f.name
                if ext == MS_CAB:
                    cls = MS.get(str(lp))
            cname = cls[1] if cls else "?"
            asm = cls[2] if cls else "?"
            if asm != "assembly_valheim" or cname in SKIP_CLASSES:
                continue
            v = f.read(o)
            if not v:
                continue
            g = v.get("m_GameObject", {}).get("m_PathID", 0)
            nm = v.get("m_Name", "")
            if not g and nm:
                names[o[0]] = nm
            decoded.append((o, cname, g, v))
        for o, cname, g, v in decoded:
            rec = {"b": bname, "pid": o[0], "cls": cname}
            if g:
                r = root_of(g)
                rec["go"] = go_name.get(g, "?")
                rec["root"] = go_name.get(r, "?")
                if r in container:
                    rec["path"] = container[r]
                if r != g:
                    rec["at"] = path_of(g)
            else:
                rec["so"] = v.get("m_Name", "")
                if o[0] in container:
                    rec["path"] = container[o[0]]
            rec["f"] = slim(v)
            out.write(json.dumps(rec, ensure_ascii=False) + "\n")
            nrec += 1
        roots = {}
        for t, fa in tr_father.items():
            if not fa and t in tr_go:
                g = tr_go[t]
                roots[g] = go_name.get(g, "?")
        names_all[f.name] = {"names": {str(k): v for k, v in names.items() if k in container or v}, "roots": {str(k): v for k, v in roots.items()},
                             "container": {str(k): v for k, v in container.items()}}
    json.dump(names_all, open(os.path.join(outdir, bname + ".names.json"), "w"), ensure_ascii=False)
    print("%s: %d records, %.1fs" % (bname, nrec, time.time() - t0), flush=True)


main()
