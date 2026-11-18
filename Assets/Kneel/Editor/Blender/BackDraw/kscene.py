# Builds the Ember Warden in Blender with the real items on their Unity sockets, for layout and animation checks.
import bpy, json, math
from mathutils import Matrix, Vector, Quaternion, Euler

EXPORT = '/mnt/user-data/uploads/Kneel-Game/Temp/kael_export.json'
FBX = '/home/claude/anim/warden.fbx'
C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # Unity mesh space -> Blender
CI = C.inverted()

def umat(l):
    return Matrix([[l[c * 4 + r] for c in range(4)] for r in range(4)])

def to_b(m_unity):
    return C @ m_unity @ CI

class Scene:
    def __init__(self, fbx=FBX):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=fbx)
        self.rig = bpy.data.objects['EmberWarden_Rig']
        self.body = bpy.data.objects['EmberWarden']
        self.J = json.load(open(EXPORT))
        for pb in self.rig.pose.bones:
            pb.rotation_mode = 'QUATERNION'
        # sockets: world matrix at rest (Blender) and their bone
        self.sock_world = {k: to_b(umat(v['m'])) for k, v in self.J['sockets'].items()}
        self.items = {}
        for name, d in self.J['items'].items():
            v = d['v']; t = d['t']
            verts = [tuple(C.to_3x3() @ Vector((v[i], v[i + 1], v[i + 2]))) for i in range(0, len(v), 3)]
            faces = [(t[i], t[i + 2], t[i + 1]) for i in range(0, len(t), 3)]  # mirror flips winding back
            me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
            ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
            ob.matrix_parent_inverse = Matrix.Identity(4)
            self.items[name] = ob
        # where each item lives: (socket matrix at rest in world, bone name)
        self.mounts = {}
        self.set_mount('SwordHand', 'Hand_R', self.sock_world['SwordHand'])
        self.set_mount('ShieldHand', 'Hand_L', self.sock_world['ShieldHand'])
        self.set_mount('ShieldBack', 'Spine_03', self.sock_world['ShieldBack'])
        self.set_mount('Scabbard', 'Hips', self.sock_world['Scabbard'])
        self.set_mount('Sheath', 'Hips', self.sock_world['Sheath'])
        self.holder = {'sword': 'Sheath', 'shield': 'ShieldBack', 'scabbard': 'Scabbard'}

    def bone_rest_world(self, bone):
        return self.rig.matrix_world @ self.rig.data.bones[bone].matrix_local

    def bone_world(self, bone):
        return self.rig.matrix_world @ self.rig.pose.bones[bone].matrix

    def set_mount(self, name, bone, world_at_rest):
        self.mounts[name] = (bone, self.bone_rest_world(bone).inverted() @ world_at_rest)

    def mount_world(self, name):
        bone, off = self.mounts[name]
        return self.bone_world(bone) @ off

    def place(self):
        bpy.context.view_layer.update()
        for item, mount in self.holder.items():
            self.items[item].matrix_world = self.mount_world(mount)
        bpy.context.view_layer.update()

    def body_mesh_world(self):
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.body.evaluated_get(dg)
        m = ev.to_mesh()
        P = [self.body.matrix_world @ v.co for v in m.vertices]
        ev.to_mesh_clear()
        return P

    def reset_pose(self):
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)

# ---------- rendering ----------
def setup_render(res=520):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'OBJECT'
    sc.render.resolution_x = res; sc.render.resolution_y = res
    if 'cam' not in bpy.data.objects:
        cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'
        co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
    return bpy.data.objects['cam']

def look(co, eye, target, scale):
    co.location = eye
    d = (Vector(target) - Vector(eye)).normalized()
    co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    co.data.ortho_scale = scale

VIEWS = {  # Blender: character faces -Y, left is +X
    'back': ((0, 4, 1.25), (0, 0, 1.25)),
    'back34': ((-2.6, 3.0, 2.0), (0, 0, 1.2)),
    'side_r': ((-4, 0, 1.25), (0, 0, 1.25)),
    'front': ((0, -4, 1.25), (0, 0, 1.25)),
    'game': ((0.0, 3.2, 5.2), (0, 0, 1.0)),
    'top': ((0, 0, 5), (0, 0, 1.0)),
}

def render_views(path_prefix, views, scale=1.6, res=520, center=None):
    from PIL import Image
    co = setup_render(res)
    ims = []
    for v in views:
        eye, tgt = VIEWS[v]
        if center is not None:
            eye = Vector(eye) + Vector(center) - Vector(tgt); tgt = center
        if v == 'top':
            co.location = eye; co.rotation_euler = (0, 0, math.pi); co.data.ortho_scale = scale
        else:
            look(co, eye, tgt, scale)
        f = f'{path_prefix}_{v}.png'
        bpy.context.scene.render.filepath = f
        bpy.ops.render.render(write_still=True)
        ims.append(Image.open(f).convert('RGB'))
    S = Image.new('RGB', (res * len(ims), res))
    for i, im in enumerate(ims): S.paste(im, (i * res, 0))
    S.save(path_prefix + '.png')
    return path_prefix + '.png'

def colour_items(sc):
    cols = {'sword': (0.85, 0.85, 0.9, 1), 'shield': (0.75, 0.5, 0.2, 1), 'scabbard': (0.35, 0.2, 0.1, 1)}
    for k, ob in sc.items.items(): ob.color = cols[k]
    sc.body.color = (0.45, 0.47, 0.52, 1)

POSES = '/mnt/user-data/uploads/Kneel-Game/Temp/kael_poses.json'
_poses = None
def unity_pose(sc, name):
    """Pose the rig like Unity's sample `name` (world delta from bind, per bone, parents first)."""
    global _poses
    if _poses is None: _poses = json.load(open(POSES))
    P = _poses[name]; B = _poses['bind']
    A = sc.rig.matrix_world; AI = A.inverted()
    def walk(b):
        if b.name in P:
            D = C @ umat(P[b.name]) @ umat(B[b.name]).inverted() @ CI
            sc.rig.pose.bones[b.name].matrix = AI @ D @ A @ b.matrix_local
            bpy.context.view_layer.update()
        for ch in b.children: walk(ch)
    for b in sc.rig.data.bones:
        if b.parent is None: walk(b)

def capture_pose(sc):
    return {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in sc.rig.pose.bones}

def set_pose(sc, pose, bones=None):
    for n, (l, q) in pose.items():
        if bones is None or n in bones:
            pb = sc.rig.pose.bones[n]; pb.location = l; pb.rotation_quaternion = q
    bpy.context.view_layer.update()

UPPER = None
def upper_bones(sc):
    """Bones the UpperBody mask drives: both arm chains (clavicle down, fingers) and neck/head."""
    out = set()
    for root in ('Clavicle_L', 'Clavicle_R', 'Neck'):
        stack = [sc.rig.data.bones[root]]
        while stack:
            b = stack.pop(); out.add(b.name); stack += list(b.children)
    return out

def game_pose(sc, body='idle', arms=None):
    """The in-game composite: `body` everywhere, `arms` (parent-relative) on the masked bones."""
    sc.reset_pose(); unity_pose(sc, body); base = capture_pose(sc)
    if arms:
        sc.reset_pose(); unity_pose(sc, arms); a = capture_pose(sc)
        ub = upper_bones(sc)
        for n in ub: base[n] = (base[n][0], a[n][1])
    sc.reset_pose(); set_pose(sc, base)
    return base

def load_layout(sc, path='/home/claude/anim/layout.json'):
    L = json.load(open(path))
    new = Matrix(L['sheath']); old = sc.sock_world['Sheath']
    sc.set_mount('Sheath', 'Spine_03', new)
    sc.set_mount('Scabbard', 'Spine_03', new @ old.inverted() @ sc.sock_world['Scabbard'])
    sb = sc.sock_world['ShieldBack']; out = (sb.to_3x3() @ Vector((0, -1, 0))).normalized()
    sc.set_mount('ShieldBack', 'Spine_03', Matrix.Translation(out * L['shield_out'] + Vector((0, 0, -L.get('shield_down', 0.0)))) @ sb)
