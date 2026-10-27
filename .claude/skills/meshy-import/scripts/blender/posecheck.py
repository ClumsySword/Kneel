# Pose the rig hard and measure how far each high-poly vertex lands from the
# deformed reference mesh's surface. Wrongly mapped weights show up as outliers.
import bpy, math, numpy as np
from mathutils.bvhtree import BVHTree

POSES = {
    'combat': {
        'Shoulder_L': (0, 0, 55), 'Shoulder_R': (0, 0, -55), 'Elbow_L': (70, 0, 0), 'Elbow_R': (70, 0, 0),
        'Hand_R': (30, 0, 20), 'Spine_02': (0, 25, 0), 'Head': (0, 30, 10),
        'UpperLeg_L': (-60, 0, 0), 'LowerLeg_L': (90, 0, 0), 'UpperLeg_R': (25, 0, 10), 'LowerLeg_R': (40, 0, 0),
        'Cape_0_1': (35, 0, 0), 'Cape_2_1': (45, 0, 0), 'Cape_4_1': (35, 0, 0), 'Cape_2_3': (30, 0, 0),
        **{f'{f}_0{i}_1': (0, 0, -60) for f in ('Finger', 'IndexFinger') for i in (1, 2, 3)},
        **{f'{f}_0{i}': (0, 0, 60) for f in ('Finger', 'IndexFinger') for i in (1, 2, 3)},
        'Thumb_02_1': (0, 0, -40), 'Thumb_02': (0, 0, 40),
    },
    'overhead': {
        'Shoulder_L': (0, 0, -70), 'Shoulder_R': (0, 0, 70), 'Clavicle_L': (0, 0, -20), 'Clavicle_R': (0, 0, 20),
        'Elbow_L': (20, 0, 0), 'Elbow_R': (20, 0, 0), 'Spine_01': (25, 0, 0), 'Spine_03': (0, -30, 0),
        'UpperLeg_L': (0, 0, 35), 'UpperLeg_R': (0, 0, -35), 'Ankle_L': (30, 0, 0), 'Ankle_R': (-30, 0, 0),
        'Cape_1_0': (-40, 0, 0), 'Cape_3_0': (-40, 0, 0), 'Cape_2_2': (-50, 0, 0),
    },
}


def world_verts(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    co = np.zeros(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    mw = np.array(ob.matrix_world)
    co = co @ mw[:3, :3].T + mw[:3, 3]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    return co, polys


def dominant_bone(ob):
    names = [g.name for g in ob.vertex_groups]
    dom = []
    for v in ob.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        dom.append(names[best.group] if best else '-')
    return np.array(dom)


def run(hp_name, ref_name, rig_name='EmberWarden_Rig', thresh=0.01):
    hp, ref, rig = bpy.data.objects[hp_name], bpy.data.objects[ref_name], bpy.data.objects[rig_name]
    rig.data.pose_position = 'POSE'
    dom = dominant_bone(hp)
    out = {}
    for pname, pose in POSES.items():
        for pb in rig.pose.bones:
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
        for b, r in pose.items():
            rig.pose.bones[b].rotation_euler = tuple(math.radians(a) for a in r)
        bpy.context.view_layer.update()
        rc, rp = world_verts(ref)
        bvh = BVHTree.FromPolygons([tuple(map(float, c)) for c in rc], rp)
        hc, _ = world_verts(hp)
        d = np.array([bvh.find_nearest(tuple(map(float, c)))[3] for c in hc])
        bad = d > thresh
        regions = {}
        for b in np.unique(dom[bad]):
            sel = bad & (dom == b)
            regions[b] = (int(sel.sum()), round(float(d[sel].max()) * 1000, 1))
        out[pname] = dict(median_mm=round(float(np.median(d)) * 1000, 3), p99_mm=round(float(np.percentile(d, 99)) * 1000, 2),
                          max_mm=round(float(d.max()) * 1000, 1), n_bad=int(bad.sum()), regions=regions)
        out[pname]['_d'] = d
    return out


def reset(rig_name='EmberWarden_Rig'):
    rig = bpy.data.objects[rig_name]
    for pb in rig.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()
