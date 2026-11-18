# Makes the Ember Warden's LEFT pauldron move as one plate. Run headless on the current (already cleaned) FBX:
#   blender -b --python ember_warden_rigid_pauldron.py -- <in.fbx> <out.fbx>
# Applied to Player_EmberWarden.fbx on 2026-11-17, after ember_warden_fix_weights.py.
#
# Why: the Meshy mesh is one welded surface, and its auto weights blended smoothly across the left pauldron. The
# front of the plate was a Clavicle_L/Shoulder_L mix, the back and top were on Neck/Spine_03, and the upright crest by
# the hood was pulled onto Head by fix_weights' hood rule (its base sits inside HOOD_RADIUS). So front and back of one
# rigid plate followed different bones, and any reach (the sheathe's shield grab) sheared the plate into spikes.
#
# What it does:
#  - The pauldron is the dome/lames (an ellipsoid round the left shoulder) plus the crest (a box beside the hood).
#    Bare upper-arm skin under it (within ARM_CORE of the Shoulder->Elbow line) keeps its weights, so the arm still
#    bends at the shoulder.
#  - Inside, weights depend ONLY on how far out across the shoulder a vertex is (mesh x): the crest is mostly
#    Clavicle_L, the outer dome and lames mostly Shoulder_L. Front, back and top of the plate therefore always share
#    weights, and nothing on it follows the neck, head, spine or cape any more.
#  - A band two edges wide round the border is smoothed, so the plate's rim blends into the mantle and arm.
# Idempotent inside the pauldron (weights come from position, not from the old weights); the border smoothing shifts
# slightly on a re-run, so still prefer running it once per fresh export.
import bpy, sys, math
from mathutils import Vector

src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
me = mesh.data
to_mesh = mesh.matrix_world.inverted() @ rig.matrix_world
def bone_head(n): return to_mesh @ rig.data.bones[n].head_local

# Mesh space: x = character's left, y = up, z = forward.
DOME_CENTRE, DOME_RADII = Vector((0.33, 1.40, 0.03)), Vector((0.19, 0.30, 0.21))
CREST_MIN, CREST_MAX = Vector((0.178, 1.42, -0.20)), Vector((0.27, 1.72, 0.20))
LAME_K, LAME_R, LAME_MIN_X = (0.0, 0.5), 0.24, 0.26
ARM_CORE = 0.12            # bare upper-arm skin is within ~0.11 of the bone line
INNER_X, OUTER_X = 0.20, 0.45
INNER_ARM, OUTER_ARM = 0.15, 0.85   # Shoulder_L share at the crest / at the outer dome; the rest is Clavicle_L
BAND_RINGS, SMOOTH_ITERS = 2, 4

S, E = bone_head("Shoulder_L"), bone_head("Elbow_L")
def arm_core(p):
    ab = E - S; k = (p - S).dot(ab) / ab.length_squared
    return k > 0.1 and (p - (S + ab * min(k, 1.0))).length < ARM_CORE

def in_pauldron(p):
    d = Vector(((p.x - DOME_CENTRE.x) / DOME_RADII.x, (p.y - DOME_CENTRE.y) / DOME_RADII.y, (p.z - DOME_CENTRE.z) / DOME_RADII.z))
    dome = d.length <= 1.0 and not arm_core(p)
    crest = all(CREST_MIN[i] <= p[i] <= CREST_MAX[i] for i in range(3))
    return dome or crest or lames(p)

def lames(p):
    # The lames stepping down the upper arm: a shell round the top of the arm, outside the bare skin, clear of the
    # torso. The dome's ellipsoid misses their lower back rim.
    ab = E - S; k = (p - S).dot(ab) / ab.length_squared
    r = (p - (S + ab * k)).length
    return LAME_K[0] <= k <= LAME_K[1] and ARM_CORE <= r <= LAME_R and p.x >= LAME_MIN_X

def target(p):
    t = max(0.0, min(1.0, (p.x - INNER_X) / (OUTER_X - INNER_X)))
    t = t * t * (3 - 2 * t)
    arm = INNER_ARM + (OUTER_ARM - INNER_ARM) * t
    return {"Shoulder_L": arm, "Clavicle_L": 1 - arm}

groups = {g.name: g for g in mesh.vertex_groups}
gname = {g.index: g.name for g in mesh.vertex_groups}
def get_w(i): return {gname[g.group]: g.weight for g in me.vertices[i].groups if g.weight > 1e-5}
def set_w(i, w):
    for n in list(get_w(i)): groups[n].remove([i])
    top = sorted(w.items(), key=lambda t: -t[1])[:4]
    s = sum(x for _, x in top)
    for n, x in top:
        if x / s > 1e-4: groups[n].add([i], x / s, 'REPLACE')

n = len(me.vertices)
inside = [in_pauldron(v.co) for v in me.vertices]
for i in range(n):
    if inside[i]: set_w(i, target(me.vertices[i].co))

nb = [[] for _ in range(n)]
for e in me.edges:
    a, b = e.vertices; nb[a].append(b); nb[b].append(a)
border = {i for i in range(n) if any(inside[j] != inside[i] for j in nb[i])}
band = set(border)
for _ in range(BAND_RINGS - 1):
    band |= {j for i in band for j in nb[i]}
cur = {i: get_w(i) for i in range(n)}
for _ in range(SMOOTH_ITERS):
    new = {}
    for i in band:
        acc = dict(cur[i])
        for j in nb[i]:
            for g, x in cur[j].items(): acc[g] = acc.get(g, 0) + x
        s = sum(acc.values()); new[i] = {g: x / s for g, x in acc.items()}
    cur.update(new)
for i in band: set_w(i, cur[i])

print(f"PAULDRON rigid {sum(inside)} verts, border band smoothed {len(band)}")
if dst != "--dry":
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={'ARMATURE', 'MESH'},
                             add_leaf_bones=False, bake_anim=False, path_mode='AUTO',
                             apply_scale_options='FBX_SCALE_ALL')
    print("EXPORTED", dst)
