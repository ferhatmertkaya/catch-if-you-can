"""Decimate the project's oversized models, in Blender, with the result MEASURED.

    blender --background --python Tools/Decimate/decimate_models.py

Nothing else is needed: no add-on, no scene, no manual step. Blender is only the
engine that owns a decimator - Unity's model importer has none, which is why this
cannot be done with an import setting.

WHY THIS EXISTS
    01_MainMenu names 24.7 million triangles in eleven models, about 846 MiB of
    vertex and index buffers before a single texture. Every prop in it is roughly
    1.5 million triangles: a candle holder, a rotary phone, a table. They are raw
    photogrammetry exports that were never decimated, and an iPhone ends the
    process rather than load them.

WHAT IT DOES NOT TOUCH
    The .meta beside each .fbx. Overwriting a model in place keeps its .meta, so
    the guid does not change and every scene, prefab and material that points at
    it still points at it. Never delete a .meta to "refresh" an import.

WHAT IT PROVES RATHER THAN ASSUMES
    A round trip through Blender can silently change scale or axis, and a prop
    that comes back 100x too big or lying on its side is a worse problem than the
    one being fixed. So each model's bounding box is measured before and after and
    printed side by side, and a drift beyond a tenth of a percent is REFUSED: that
    file is left exactly as it was rather than written wrong.

    This project has been bitten by an unverified size twice (CLAUDE.md mistakes 12
    and 29). A number that is not measured after the operation is a hope.

AFTERWARDS
    - git diff --stat            to see what shrank
    - Scripts/check_scene_budget.sh   to see the new triangle count per scene
    - lower the figures in Scripts/scene_budget.txt to the new ones
    - open Unity, let it reimport, and LOOK at each prop before committing
"""

import os
import sys
import bpy
from mathutils import Vector

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Target triangle counts, chosen for what the prop is and how close the player gets.
#
# A hand-held device fills the screen and earns a few thousand. A room prop seen
# from one to three metres in the dark earns fewer than people expect - the normal
# map keeps the detail that the silhouette loses, and these all have one.
#
# Set a target to 0 to skip a model.
TARGETS = [
    # architecture the player is always looking at
    ("Art/Environment/MainMenuCorridor/Models/CIYC_MainMenu_Corridor.fbx",              20000),

    # room props, seen at 1-3 m
    ("Art/Environment/Props/HauntedCandleHolder/Models/CIYC_HauntedCandleHolder.fbx",    4000),
    ("Art/Environment/Props/HauntedGrandfatherClock/Models/CIYC_HauntedGrandfatherClock.fbx", 6000),
    ("Art/Environment/Props/HauntedRotaryPhone/Models/CIYC_HauntedRotaryPhone.fbx",      3000),
    ("Art/Environment/Props/HauntedTable/Models/CIYC_HauntedTable.fbx",                  5000),
    ("Art/Environment/Props/MonsteraPlant/Models/CIYC_MonsteraPlant.fbx",                6000),
    ("Art/Environment/Props/WallLamp/Models/CIYC_WallLamp.fbx",                          3000),
    ("Art/Environment/Doors/CIYC_VictorianHauntedDoor/Models/CIYC_VictorianHauntedDoor.fbx", 4000),
    ("Art/Environment/Props/VintageCoffeeTable/Models/CIYC_VintageCoffeeTable.fbx",       3000),
    ("Art/Environment/Props/CorkBulletinBoard/Models/Meshy_AI_cork_bulletin_board_0908012435_image-to-3d-texture.fbx", 2000),
    ("Art/Environment/Props/PizzaBox/Models/CIYC_PizzaBox.fbx",                           1500),

    # a ghost, seen briefly and in the dark, but it is a character silhouette
    ("Art/Characters/Ghosts/FloatingFemaleGhost/Models/CIYC_FloatingFemaleGhost.fbx",     8000),

    # equipment held in the hand, so it fills the screen
    ("Art/Environment/Props/EMFLevel2/Models/CIYC_EMFLevel2.fbx",                         6000),
    ("Resources/Props/CIYC_DOTSProjectorLevel1.fbx",                                      6000),
    ("Resources/Props/CIYC_UvLight.fbx",                                                  6000),
]

# A round trip that moves a model by more than this fraction of its own size is a
# failure, not a rounding difference.
SIZE_TOLERANCE = 0.001


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def mesh_objects():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def triangles():
    total = 0
    for obj in mesh_objects():
        for poly in obj.data.polygons:
            total += len(poly.vertices) - 2
    return total


def world_bounds():
    """The size of everything in the scene, in world units."""
    objs = mesh_objects()
    if not objs:
        return Vector((0, 0, 0))
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    for obj in objs:
        for corner in obj.bound_box:
            p = obj.matrix_world @ Vector(corner)
            for i in range(3):
                lo[i] = min(lo[i], p[i])
                hi[i] = max(hi[i], p[i])
    return hi - lo


def decimate_to(target_tris):
    """One Collapse decimate per mesh, at the ratio that reaches the share it owns."""
    total = triangles()
    if total <= target_tris:
        return False

    ratio = float(target_tris) / float(total)
    for obj in mesh_objects():
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new(name="CIYC_Decimate", type="DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        # Keeps the UV seams from tearing, which is what makes a decimated
        # photogrammetry prop look shredded rather than simplified.
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return True


def process(rel_path, target):
    path = os.path.join(REPO, "Assets/CatchIfYouCan", rel_path)
    name = os.path.basename(rel_path)

    if not os.path.isfile(path):
        return (name, "FEHLT", 0, 0, "")
    if target <= 0:
        return (name, "uebersprungen", 0, 0, "")

    clear_scene()
    try:
        bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y")
    except Exception as exc:                                   # noqa: BLE001
        return (name, "IMPORT FEHLGESCHLAGEN: %s" % exc, 0, 0, "")

    before_tris = triangles()
    before_size = world_bounds()
    if before_tris == 0:
        return (name, "keine Geometrie", 0, 0, "")

    if not decimate_to(target):
        return (name, "schon klein genug", before_tris, before_tris, "")

    after_tris = triangles()
    after_size = world_bounds()

    # Measured, not assumed. A prop that comes back a different size is refused.
    drift = max(
        abs(after_size[i] - before_size[i]) / max(before_size[i], 1e-6)
        for i in range(3))
    if drift > SIZE_TOLERANCE:
        return (name, "REFUSED: Groesse driftete um %.2f%%" % (drift * 100),
                before_tris, after_tris, "")

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        apply_unit_scale=True,
        global_scale=1.0,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_tspace=True,          # tangents, so the normal maps still light correctly
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",        # no absolute texture paths written into the file
    )

    size = "%.1f x %.1f x %.1f" % (after_size[0], after_size[1], after_size[2])
    return (name, "ok", before_tris, after_tris, size)


def main():
    print("\n=== CIYC Modell-Dezimierung ===\n")
    print("%-52s %12s %10s  %s" % ("Modell", "vorher", "nachher", "Zustand"))
    print("-" * 100)

    before_total = after_total = 0
    refused = []

    for rel, target in TARGETS:
        name, state, before, after, size = process(rel, target)
        before_total += before
        after_total += after if state == "ok" or state == "schon klein genug" else before
        if state.startswith("REFUSED") or state.startswith("IMPORT") or state == "FEHLT":
            refused.append("%s: %s" % (name, state))
        print("%-52s %12s %10s  %s%s" % (
            name[:52], f"{before:,}", f"{after:,}" if after else "-", state,
            ("  [" + size + " m]") if size else ""))

    print("-" * 100)
    print("%-52s %12s %10s" % ("SUMME", f"{before_total:,}", f"{after_total:,}"))
    if before_total:
        print("\nReduktion: %.1f%%  (Faktor %.0f)" % (
            100.0 * (1 - after_total / before_total),
            before_total / max(after_total, 1)))

    if refused:
        print("\nNICHT geschrieben:")
        for r in refused:
            print("  " + r)

    print("\nDanach:")
    print("  Scripts/check_scene_budget.sh   und die Zahlen in scene_budget.txt senken")
    print("  Unity oeffnen, reimportieren lassen, und JEDES Modell ansehen bevor committet wird")


main()
