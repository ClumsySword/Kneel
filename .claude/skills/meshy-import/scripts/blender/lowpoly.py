# Meshy high-poly -> game-ready low-poly with a baked tangent-space normal map.
# Runs inside Blender (MCP execute_blender_code):
#   import sys; sys.path.insert(0, r"<skill>/scripts/blender"); import importlib, lowpoly; importlib.reload(lowpoly)
import bpy, time, mathutils, numpy as np
from mathutils.bvhtree import BVHTree

# Reproduces Kneel's existing FBX exports exactly (fbxcmp reports 0 structural diffs against
# Player_EmberWarden.fbx). Unity's importer then gives the same transforms, bindposes and avatar.
EXPORT = dict(use_selection=True, object_types={'ARMATURE', 'MESH'}, global_scale=1.0, apply_unit_scale=True,
              apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=False,
              use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False,
              primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
              use_armature_deform_only=False, bake_anim=False, path_mode='AUTO', embed_textures=False,
              use_custom_props=False)


def import_fbx(path):
    """Import an FBX and return the objects it added."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def world_bbox(ob):
    ws = [ob.matrix_world @ mathutils.Vector(c) for c in ob.bound_box]
    return [round(min(v[i] for v in ws), 4) for i in range(3)], [round(max(v[i] for v in ws), 4) for i in range(3)]


def _select_only(*obs):
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    for o in obs:
        o.hide_set(False)
        o.select_set(True)
    vl.objects.active = obs[0]


def cleanup_weights(ob, max_influences=4):
    """Drop tiny weights, keep the 4 strongest per vertex (Unity's maxBonesPerVertex) and renormalize.
    These operators only act on the view layer's active, selected object; temp_override is not enough."""
    _select_only(ob)
    bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.001, keep_single=True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=max_influences)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def place_like(hp, ref, name, ground=True):
    """New object `name` holding hp's mesh, expressed in ref's local space and parented like ref.
    ground=True drops hp's lowest point to world z=0 first (Meshy exports sit at arbitrary heights).
    Only meaningful when hp is the same model in the same pose as ref; check fit_report afterwards."""
    T = mathutils.Matrix.Identity(4)
    if ground:
        lo = min((hp.matrix_world @ v.co).z for v in hp.data.vertices)
        T = mathutils.Matrix.Translation((0, 0, -lo))
    me = hp.data.copy()
    me.name = name
    me.transform(ref.matrix_world.inverted() @ T @ hp.matrix_world)
    me.materials.clear()
    for m in ref.data.materials:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    ref.users_collection[0].objects.link(ob)
    ob.parent = ref.parent
    ob.parent_type = ref.parent_type
    ob.matrix_parent_inverse = ref.matrix_parent_inverse.copy()
    ob.matrix_basis = ref.matrix_basis.copy()
    return ob


def fit_report(ob, ref, every=7):
    """World bboxes of both, and how far ob's vertices sit from ref's surface (mm), at the current pose.
    A good fit for a re-generated version of the same model: bboxes within ~1 cm, median a few mm."""
    co = surface_distances(ob, ref, every)
    return dict(bbox=world_bbox(ob), ref_bbox=world_bbox(ref),
                median_mm=round(float(np.median(co)) * 1000, 2), p99_mm=round(float(np.percentile(co, 99)) * 1000, 1))


def transfer_weights(dst, src, rig):
    """Copy skin weights from the existing rigged mesh src onto dst (nearest-face interpolation, in rest pose),
    then clean them to 4 influences and add the armature modifier."""
    rig.data.pose_position = 'REST'
    for vg in src.vertex_groups:
        if vg.name not in dst.vertex_groups:
            dst.vertex_groups.new(name=vg.name)
    dt = dst.modifiers.new('WeightXfer', 'DATA_TRANSFER')
    dt.object = src
    dt.use_object_transform = True
    dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}
    dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'
    dt.layers_vgroup_select_dst = 'NAME'
    with bpy.context.temp_override(object=dst, active_object=dst):
        bpy.ops.object.modifier_apply(modifier=dt.name)
    cleanup_weights(dst)
    am = dst.modifiers.new(rig.name, 'ARMATURE')
    am.object = rig


def make_lowpoly(hp, tris, name=None, rig=None):
    """Collapse-decimate a copy of hp to ~tris triangles. Keeps UVs (so hp's textures still line up) and
    vertex groups. Clears custom split normals: plain smooth normals are what the bake's tangent space and
    Unity's MikkTSpace tangents agree on. With rig, weights are re-limited to 4 and an armature modifier added."""
    name = name or '%s_LP%dk' % (hp.name, round(tris / 1000))
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name])
    if rig:
        rig.data.pose_position = 'REST'
    me = hp.data.copy()
    me.name = name
    ob = bpy.data.objects.new(name, me)
    hp.users_collection[0].objects.link(ob)
    ob.parent = hp.parent
    ob.parent_type = hp.parent_type
    ob.matrix_parent_inverse = hp.matrix_parent_inverse.copy()
    ob.matrix_basis = hp.matrix_basis.copy()
    for vg in hp.vertex_groups:
        ob.vertex_groups.new(name=vg.name)
    dm = ob.modifiers.new('Dec', 'DECIMATE')
    dm.decimate_type = 'COLLAPSE'
    dm.ratio = min(1.0, tris / max(1, tri_count(hp)))
    dm.use_collapse_triangulate = True
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier='Dec')
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for p in ob.data.polygons:
        p.use_smooth = True
    if rig:
        cleanup_weights(ob)
        am = ob.modifiers.new(rig.name, 'ARMATURE')
        am.object = rig
    return ob


def surface_distances(a, b, every=7):
    """Distance (m) from every n-th vertex of a to b's surface, both evaluated (modifiers/pose applied)."""
    dg = bpy.context.evaluated_depsgraph_get()

    def world(ob):
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

    ac, _ = world(a)
    bc, bp = world(b)
    bvh = BVHTree.FromPolygons([tuple(map(float, c)) for c in bc], bp)
    return np.array([bvh.find_nearest(tuple(map(float, c)))[3] for c in ac[::every]])


def deviation(hp, lp, every=7):
    """How far the high-poly surface lies from the low-poly one (mm). Its max sets the bake cage."""
    d = surface_distances(hp, lp, every)
    return dict(median_mm=round(float(np.median(d)) * 1000, 2), p99_mm=round(float(np.percentile(d, 99)) * 1000, 1),
                max_mm=round(float(d.max()) * 1000, 1))


def bake_source_material(normal_png):
    """A material carrying Meshy's own normal map, so its fine detail ends up in the bake too."""
    m = bpy.data.materials.get('BakeSrc') or bpy.data.materials.new('BakeSrc')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    if normal_png:
        tx = nt.nodes.new('ShaderNodeTexImage')
        tx.image = bpy.data.images.load(normal_png, check_existing=True)
        tx.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tx.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    return m


def bake_normal(hp, lp, out_png, src_normal_png=None, size=2048, cage=None, max_ray=None, rig=None):
    """Selected-to-active tangent-space normal bake of hp onto lp, saved as an 8-bit PNG.
    OpenGL convention (green up), which is what Unity and Meshy both use, so no channel flip.
    cage/max_ray default to the measured hp->lp deviation with headroom (at least 1 cm / 3 cm).
    Both objects' materials are swapped for the bake and restored afterwards."""
    if rig:
        rig.data.pose_position = 'REST'
    if cage is None:
        dev = deviation(hp, lp)
        cage = max(0.01, dev['max_mm'] / 1000 * 1.5)
    max_ray = max_ray or cage * 3
    sc = bpy.context.scene
    prev_engine = sc.render.engine
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 8
    sc.cycles.use_denoising = False
    hp_mats, lp_mats = list(hp.data.materials), list(lp.data.materials)
    src = bake_source_material(src_normal_png)
    img_name = 'N_' + lp.name
    if img_name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[img_name])
    img = bpy.data.images.new(img_name, size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color'
    dst = bpy.data.materials.new('BakeDst_' + lp.name)
    dst.use_nodes = True
    t = dst.node_tree.nodes.new('ShaderNodeTexImage')
    t.image = img
    dst.node_tree.nodes.active = t
    try:
        for i in range(max(1, len(hp_mats))):
            if i < len(hp.data.materials): hp.data.materials[i] = src
            else: hp.data.materials.append(src)
        for i in range(max(1, len(lp_mats))):
            if i < len(lp.data.materials): lp.data.materials[i] = dst
            else: lp.data.materials.append(dst)
        for o in (hp, lp):
            o.hide_render = False
        _select_only(lp, hp)
        bpy.context.view_layer.objects.active = lp
        t0 = time.time()
        bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_selected_to_active=True, cage_extrusion=cage,
                            max_ray_distance=max_ray, margin=16, margin_type='EXTEND', use_clear=True)
        img.filepath_raw = out_png
        img.file_format = 'PNG'
        img.save()
        return dict(seconds=round(time.time() - t0, 1), cage_m=round(cage, 4), max_ray_m=round(max_ray, 4), png=out_png)
    finally:
        for ob, mats in ((hp, hp_mats), (lp, lp_mats)):
            ob.data.materials.clear()
            for m in mats:
                ob.data.materials.append(m)
        sc.render.engine = prev_engine


def export_fbx(lp, path, as_name, rig=None):
    """Export lp (plus rig, if any) with Kneel's settings. lp and its mesh are exported as `as_name`, so
    Unity keeps the same object path and material slot as the asset being replaced. Anything else using
    that name is renamed for the duration. The rig is exported in its rest pose."""
    taken = [d for d in (bpy.data.objects.get(as_name), bpy.data.meshes.get(as_name)) if d and d not in (lp, lp.data)]
    old = (lp.name, lp.data.name)
    for d in taken:
        d.name = as_name + '__tmp'
    try:
        lp.name = as_name
        lp.data.name = as_name
        if rig:
            rig.data.pose_position = 'POSE'
            for pb in rig.pose.bones:
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = (1, 0, 0, 0)
                pb.location = (0, 0, 0)
                pb.scale = (1, 1, 1)
        _select_only(*([rig, lp] if rig else [lp]))
        settings = dict(EXPORT, object_types={'ARMATURE', 'MESH'} if rig else {'MESH'})
        bpy.ops.export_scene.fbx(filepath=path, **settings)
    finally:
        lp.name, lp.data.name = old
        for d in taken:
            d.name = as_name
    return path
