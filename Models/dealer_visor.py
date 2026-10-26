"""
Board Vikings Cards: the dealer's visor, built from code so it can be edited by hand.

Run it in Blender (Scripting tab > Open > Run Script), or headless:
    blender --background --python Models/dealer_visor.py

It makes, in meters (Blender is Z up, the visor faces -Y, the way Blender's front view shows a character):
    Visor_Band   white fabric band round the forehead: tall at the front, a thin strap at the back
    Visor_Brim   curved green plastic brim, at the FRONT ONLY: longest in the middle, tapering to nothing at the sides
    Visor_Fold   the plastic rising above the band, leaning back, with piping (Visor_FoldTrim) on its top edge
    Visor_Trim   white edge piping along the brim's rim
    Head_reference (not exported)  a head to judge the fit against; the visor's origin is the middle of the head

Then it saves Models/dealer_visor.blend and exports Assets/dealer_visor.glb (the game's model) and a preview picture.
Edit the numbers below, or edit the objects by hand and use File > Export > glTF (Selected: the three Visor_ objects).
"""
import math
import os

import bpy

# ---- the numbers (meters, degrees) ----------------------------------------------------------------
BAND_RADIUS = 0.118      # inner radius of the band (the head is about this wide)
BAND_Y = 0.055           # band height above the middle of the head
BAND_THICK = 0.007
BAND_TALL = 0.042        # band height at the front
BAND_THIN = 0.012        # band height at the back (the elastic strap)
BRIM_REACH = 0.078       # how far the brim sticks out in the middle
BRIM_HALF_ANGLE = 58.0   # the brim spans this many degrees either side of the front
BRIM_DROOP = 0.004       # the brim edge hangs this far below the band, plus more towards the middle
BRIM_DROOP_MID = 0.10
SEGMENTS = 36

# Next to this script when it runs from a file; the mod's folder when it is pasted or sent into a running Blender.
if "__file__" in globals():
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
else:
    ROOT = r"C:\Modding\Vahleim\Mod\BoardVikingsCards"

# ---- helpers ---------------------------------------------------------------------------------------
def to_blender(x, y, z):
    """The game's axes (x right, y up, z forward) to Blender's (x right, y back, z up)."""
    return (x, -z, y)


def reach(deg):
    t = max(0.0, min(1.0, 1.0 - abs(deg) / BRIM_HALF_ANGLE))
    return BRIM_REACH * math.sqrt(math.sin(t * math.pi / 2.0))


def brim_edge(deg):
    a = math.radians(deg)
    r = reach(deg)
    d = (math.sin(a), 0.0, math.cos(a))
    droop = -BRIM_DROOP - BRIM_DROOP_MID * r
    return (d[0] * (BAND_RADIUS + r), droop, d[2] * (BAND_RADIUS + r))


def make_material(name, color, alpha=1.0, roughness=0.6):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        try:
            mat.blend_method = "BLEND"          # Blender up to 4.1
        except Exception:
            pass
        try:
            mat.surface_render_method = "BLENDED"  # Blender 4.2 and newer
        except Exception:
            pass
    mat.diffuse_color = (*color, alpha)
    return mat


def make_object(name, verts, faces, material, location=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([to_blender(*v) for v in verts], [], faces)
    mesh.update()
    mesh.validate()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(material)
    for poly in mesh.polygons:
        poly.use_smooth = True
    return obj


def solidify(obj, thickness):
    mod = obj.modifiers.new("Thickness", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 0.0
    return mod


# ---- clean slate -----------------------------------------------------------------------------------
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

fabric = make_material("Visor_Fabric", (0.95, 0.93, 0.86), 1.0, 0.85)
plastic = make_material("Visor_Plastic", (0.08, 0.70, 0.30), 0.62, 0.15)
piping = make_material("Visor_Piping", (0.97, 0.96, 0.92), 1.0, 0.7)

# ---- band: a ring whose height changes with the angle ----------------------------------------------
verts, faces = [], []
for i in range(SEGMENTS + 1):
    a = i / SEGMENTS * math.pi * 2.0
    front = (math.cos(a) + 1.0) / 2.0                      # 1 at the front, 0 at the back
    height = BAND_THIN + (BAND_TALL - BAND_THIN) * front * front
    d = (math.sin(a), math.cos(a))
    outer, inner = BAND_RADIUS + BAND_THICK, BAND_RADIUS
    for r in (outer, inner):
        verts.append((d[0] * r, height / 2.0, d[1] * r))   # top
        verts.append((d[0] * r, -height / 2.0, d[1] * r))  # bottom
for i in range(SEGMENTS):
    b = i * 4
    faces += [
        (b + 0, b + 1, b + 5, b + 4),   # outer wall
        (b + 2, b + 6, b + 7, b + 3),   # inner wall
        (b + 0, b + 4, b + 6, b + 2),   # top
        (b + 1, b + 3, b + 7, b + 5),   # bottom
    ]
band = make_object("Visor_Band", verts, faces, fabric, location=(0, 0, 0))
band.location = to_blender(0.0, BAND_Y, 0.0)

# ---- brim: a curved sheet on the front of the band --------------------------------------------------
BRIM_SEGMENTS = 28
verts, faces = [], []
for i in range(BRIM_SEGMENTS + 1):
    deg = -BRIM_HALF_ANGLE + (2 * BRIM_HALF_ANGLE) * i / BRIM_SEGMENTS
    a = math.radians(deg)
    verts.append((math.sin(a) * BAND_RADIUS, 0.0, math.cos(a) * BAND_RADIUS))   # where it meets the band
    verts.append(brim_edge(deg))                                                  # the free rim
for i in range(BRIM_SEGMENTS):
    b = i * 2
    faces.append((b, b + 1, b + 3, b + 2))
brim = make_object("Visor_Brim", verts, faces, plastic)
brim.location = to_blender(0.0, BAND_Y - 0.018, 0.0)
solidify(brim, 0.003)

# ---- trim: piping along the rim ----------------------------------------------------------------------
verts, faces = [], []
for i in range(BRIM_SEGMENTS + 1):
    deg = -BRIM_HALF_ANGLE + (2 * BRIM_HALF_ANGLE) * i / BRIM_SEGMENTS
    e = brim_edge(deg)
    inward = (e[0], 0.0, e[2])
    n = math.hypot(inward[0], inward[2]) or 1.0
    inward = (-inward[0] / n * 0.007, 0.0, -inward[2] / n * 0.007)
    verts.append(e)
    verts.append((e[0] + inward[0], e[1] + 0.003, e[2] + inward[2]))
for i in range(BRIM_SEGMENTS):
    b = i * 2
    faces.append((b, b + 1, b + 3, b + 2))
trim = make_object("Visor_Trim", verts, faces, piping)
trim.location = to_blender(0.0, BAND_Y - 0.018, 0.0)
solidify(trim, 0.004)

# ---- fold: the plastic rises behind the band and leans back over the forehead, like a baseball cap's bent peak ------
FOLD_RISE = 0.034        # how far the plastic stands above the band's top edge, in the middle
FOLD_LEAN = 0.022        # how far its top edge leans in towards the head
band_top = (BAND_TALL / 2.0) + 0.018   # band top edge, measured from the brim's origin
verts, faces, rim = [], [], []
for i in range(BRIM_SEGMENTS + 1):
    deg = -BRIM_HALF_ANGLE + (2 * BRIM_HALF_ANGLE) * i / BRIM_SEGMENTS
    a = math.radians(deg)
    t = max(0.0, 1.0 - abs(deg) / BRIM_HALF_ANGLE)
    k = math.sin(t * math.pi / 2.0) ** 0.7
    d = (math.sin(a), math.cos(a))
    r0 = BAND_RADIUS - 0.002
    rows = [(r0, 0.0), (r0 - 0.002 * k, band_top * 0.55), (r0 - 0.004 * k, band_top),
            (r0 - FOLD_LEAN * k, band_top + FOLD_RISE * k)]
    for r, h in rows:
        verts.append((d[0] * r, h, d[1] * r))
    rim.append(verts[-1])
for i in range(BRIM_SEGMENTS):
    b = i * 4
    for j in range(3):
        faces.append((b + j, b + j + 1, b + j + 5, b + j + 4))
fold = make_object("Visor_Fold", verts, faces, plastic)
fold.location = to_blender(0.0, BAND_Y - 0.018, 0.0)
solidify(fold, 0.003)

# piping along the fold's top edge
verts, faces = [], []
for v in rim:
    verts.append(v)
    verts.append((v[0], v[1] - 0.007, v[2]))
for i in range(BRIM_SEGMENTS):
    b = i * 2
    faces.append((b, b + 1, b + 3, b + 2))
fold_trim = make_object("Visor_FoldTrim", verts, faces, piping)
fold_trim.location = to_blender(0.0, BAND_Y - 0.018, 0.0)
solidify(fold_trim, 0.004)

# ---- a head to judge the fit against (not exported) ---------------------------------------------------
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.105, location=(0, 0.0, 0.0), segments=32, ring_count=16)
head = bpy.context.active_object
head.name = "Head_reference"
head.scale = (1.0, 1.12, 1.18)
head.display_type = "WIRE"
head.hide_render = False

# ---- save, export, preview ----------------------------------------------------------------------------
visor_parts = [band, brim, trim, fold, fold_trim]
for o in bpy.data.objects:
    o.select_set(False)
for o in visor_parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = band

os.makedirs(os.path.join(ROOT, "Models"), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Models", "dealer_visor.blend"))

glb = os.path.join(ROOT, "Assets", "dealer_visor.glb")
bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_apply=True,
                          export_yup=True, export_materials="EXPORT")
print("exported", glb)

try:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 640, 480
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.38, -0.50, 0.14)
    cam.rotation_euler = (math.radians(79), 0, math.radians(37))
    scene.camera = cam
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.filepath = os.path.join(ROOT, "Models", "dealer_visor_preview.png")
    bpy.ops.render.render(write_still=True)
    print("preview", scene.render.filepath)
except Exception as exc:  # the preview is a convenience; the model is what matters
    print("preview skipped:", exc)
