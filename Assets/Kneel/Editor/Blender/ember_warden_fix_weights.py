# Cleans the Ember Warden's skin weights and exports the FBX. Run headless:
#   blender -b --python ember_warden_fix_weights.py -- <in.fbx> <out.fbx>
# Applied to Player_EmberWarden.fbx on 2026-11-17, all rules in one pass over the original export (the FBX in the
# repo is clean). Run it ONCE on a fresh export, never on an already-fixed FBX: rule 4 moves a share of the remaining
# body weight onto the head each time, so a second pass would shift the hood further. Rerun it on any new export
# from the rigged .blend until the weights are repainted there. The rules:
#  1. No vertex mixes cape bones with arm bones; whichever side has more weight keeps it. The back of the left upper
#     arm was a third on Cape_4_0/1, so it jiggled with the cloth sim.
#  2. Head and neck vertices drop the upper-arm bones (Shoulder_L/R); the hood was on them, so raising an arm squeezed
#     the head.
#  3. Cape-weighted vertices not connected to the cape body (through cape-weighted vertices) take the weights of the
#     nearest vertex with no cape weight. A patch at the back of the left elbow was 97% cape, so it stayed behind with
#     the cloth and the arm looked split in two.
#  5. (Runs before 4.) The pauldrons' undersides follow the arms: cape-weighted vertices nearer clear arm skin, along
#     the surface, than the cape's cloth and clear body skin take the nearest arm skin's weights. The left pauldron's
#     underside rode the cape's top corner and tore into spikes under it whenever the arm moved.
#  4. The head above the neck follows the head: from the neck bone to 5 cm above the head bone, within 22 cm of the
#     neck-to-head line, weight shifts smoothly onto Head. The hood and face were 20-40% on the clavicles, neck and
#     chest up to the brow, so shrugs and arm raises warped them (hood drift through a draw: 12.5 cm before, 5.9 after).
# Export: no leaf bones, no animation, apply_scale_options='FBX_SCALE_ALL' (metres in the file, objects at scale 1,
# as the original). Checked against the original in Unity: same 71 transforms, bone rest poses within 0.03 deg,
# bind poses within 2e-5, identical vertex order, positions and UVs.
import bpy, sys
from mathutils import kdtree

src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
me = mesh.data
names = {g.index: g.name for g in mesh.vertex_groups}

def kind(n):
    if n.startswith("Cape"): return "cape"
    if n in ("Shoulder_L", "Shoulder_R"): return "upper"
    if n in ("Elbow_L", "Elbow_R", "Hand_L", "Hand_R"): return "lower"
    if n in ("Head", "Neck"): return "head"
    return "other"

def weights(v):
    return {g.group: g.weight for g in v.groups}

def total(w, *kinds):
    return sum(x for g, x in w.items() if kind(names[g]) in kinds)

def drop(v, *kinds):
    for g, _ in list(weights(v).items()):
        if kind(names[g]) in kinds:
            mesh.vertex_groups[g].remove([v.index])

def normalise(v):
    w = weights(v); t = sum(w.values())
    if t > 0:
        for g, x in w.items():
            mesh.vertex_groups[g].add([v.index], x / t, 'REPLACE')

cape_arm = head_arm = 0
for v in me.vertices:
    w = weights(v)
    cape, arm, head, upper = total(w, "cape"), total(w, "upper", "lower"), total(w, "head"), total(w, "upper")
    changed = False
    if cape > 0 and arm > 0:
        drop(v, "cape") if arm >= cape else drop(v, "upper", "lower")
        cape_arm += 1; changed = True
    w = weights(v); head, upper = total(w, "head"), total(w, "upper")
    if head > 0 and upper > 0 and head >= upper:
        drop(v, "upper"); head_arm += 1; changed = True
    if changed:
        normalise(v)

# Stray cape islands: cape-weighted vertices not connected (through cape-weighted vertices) to the cape body.
cape = [total(weights(v), "cape") > 0 for v in me.vertices]
parent = list(range(len(me.vertices)))
def find(x):
    while parent[x] != x:
        parent[x] = parent[parent[x]]; x = parent[x]
    return x
for e in me.edges:
    a, b = e.vertices
    if cape[a] and cape[b]:
        ra, rb = find(a), find(b)
        if ra != rb: parent[rb] = ra
sizes = {}
for i, c in enumerate(cape):
    if c: sizes[find(i)] = sizes.get(find(i), 0) + 1
main = max(sizes, key=sizes.get)
clean = [v.index for v in me.vertices if not cape[v.index]]
tree = kdtree.KDTree(len(clean))
for n, i in enumerate(clean): tree.insert(me.vertices[i].co, n)
tree.balance()
stray = [i for i, c in enumerate(cape) if c and find(i) != main]
for i in stray:
    _, n, _ = tree.find(me.vertices[i].co)
    source = weights(me.vertices[clean[n]])
    for g in list(weights(me.vertices[i]).keys()):
        mesh.vertex_groups[g].remove([i])
    for g, x in source.items():
        mesh.vertex_groups[g].add([i], x, 'REPLACE')

# The pauldrons' undersides follow the arms. Where a pauldron's lower rim meets the cape's top corner, its underside
# was on Cape_4_0 / Cape_0_0, the upper chest and the neck, so it rode the back and the cloth while the pauldron went
# with the arm, and tore into spikes. Measured along the surface, a cape-weighted vertex nearer clear arm skin than both
# the cape's cloth and clear body skin is part of the pauldron: it takes the weights of that nearest arm-skin vertex.
# The cloth is cape vertices at least ARM_CLEARANCE from the arms' lines; the spike tips were closer than that.
import heapq
ARM_CLEARANCE = 0.18
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
to_mesh = mesh.matrix_world.inverted() @ rig.matrix_world
def bone_head(n):
    return to_mesh @ rig.data.bones[n].head_local
def seg_dist(p, a, b):
    ab = b - a
    k = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * k)).length
arm_segments = []
for side in ("_L", "_R"):
    s, e, h = bone_head("Shoulder" + side), bone_head("Elbow" + side), bone_head("Hand" + side)
    arm_segments += [(s, e), (e, h)]
def arm_line_dist(p):
    return min(seg_dist(p, a, b) for a, b in arm_segments)

n_verts = len(me.vertices)
cape_w = [total(weights(v), "cape") for v in me.vertices]
arm_w = [total(weights(v), "upper", "lower") for v in me.vertices]
neighbours = [[] for _ in range(n_verts)]
for e in me.edges:
    a, b = e.vertices
    d = (me.vertices[a].co - me.vertices[b].co).length
    neighbours[a].append((b, d)); neighbours[b].append((a, d))

def surface_distance(sources):
    dist = [float("inf")] * n_verts
    origin = [-1] * n_verts
    heap = []
    for i in sources:
        dist[i] = 0.0; origin[i] = i; heap.append((0.0, i))
    heapq.heapify(heap)
    while heap:
        d, u = heapq.heappop(heap)
        if d > dist[u]: continue
        for x, w in neighbours[u]:
            if d + w < dist[x]:
                dist[x] = d + w; origin[x] = origin[u]
                heapq.heappush(heap, (dist[x], x))
    return dist, origin

arm_dist, arm_origin = surface_distance([i for i in range(n_verts) if arm_w[i] >= 0.5 and cape_w[i] == 0])
cloth_dist, _ = surface_distance([i for i in range(n_verts) if cape_w[i] >= 0.9 and arm_line_dist(me.vertices[i].co) > ARM_CLEARANCE])
body_dist, _ = surface_distance([i for i in range(n_verts) if cape_w[i] == 0 and arm_w[i] < 0.05])
underside = [i for i in range(n_verts) if cape_w[i] > 0 and arm_dist[i] < cloth_dist[i] and arm_dist[i] < body_dist[i] and arm_origin[i] >= 0]
sources = {i: weights(me.vertices[arm_origin[i]]) for i in underside}
for i, source in sources.items():
    for g in list(weights(me.vertices[i]).keys()):
        mesh.vertex_groups[g].remove([i])
    for g, x in source.items():
        mesh.vertex_groups[g].add([i], x, 'REPLACE')

# The head above the neck follows the head. From the neck bone up to just above the head bone, within HOOD_RADIUS of
# the neck-to-head line, weight shifts onto Head (fully Head from HEAD_TOP above the head bone up). The hood and face
# were 20-40% on the clavicles, neck and chest right up to the brow, so shrugs and arm raises warped them.
HOOD_RADIUS = 0.22
HEAD_TOP = 0.05
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
to_mesh = mesh.matrix_world.inverted() @ rig.matrix_world
neck = to_mesh @ rig.data.bones["Neck"].head_local
head = to_mesh @ rig.data.bones["Head"].head_local
axis = (head - neck).normalized()
full = (head - neck).length + HEAD_TOP
head_group = mesh.vertex_groups["Head"].index
head_family = {"Head", "Eyes"}
head_shifted = 0
for v in me.vertices:
    rel = v.co - neck
    up = rel.dot(axis)
    if up <= 0 or (rel - axis * up).length > HOOD_RADIUS:
        continue
    t = min(1.0, up / full)
    t = t * t * (3 - 2 * t)
    w = weights(v)
    body = sum(x for g, x in w.items() if names[g] not in head_family)
    if body <= 0:
        continue
    for g, x in w.items():
        if names[g] not in head_family:
            mesh.vertex_groups[g].add([v.index], x * (1 - t), 'REPLACE')
    mesh.vertex_groups[head_group].add([v.index], w.get(head_group, 0) + body * t, 'REPLACE')
    for g, x in weights(v).items():
        if x <= 1e-4:
            mesh.vertex_groups[g].remove([v.index])
    # Rule 2 again: with more head weight than upper arm now, the upper arm comes off here too.
    w = weights(v)
    if total(w, "upper") > 0 and total(w, "head") >= total(w, "upper"):
        drop(v, "upper")
    normalise(v)
    head_shifted += 1

print(f"FIX cape/arm separated {cape_arm}, upper arm off head {head_arm}, cape islands {sorted(sizes.values(), reverse=True)}, stray fixed {len(stray)}, pauldron undersides moved to the arm {len(underside)}, head vertices shifted onto Head {head_shifted}")

bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=False, path_mode='AUTO',
                         apply_scale_options='FBX_SCALE_ALL')
print("EXPORTED", dst)
