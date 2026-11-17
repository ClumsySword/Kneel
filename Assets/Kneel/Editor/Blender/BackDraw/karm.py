# Arm solver and checks for the Ember Warden (Blender world: faces -Y, left = +X, up = +Z).
import bpy, math
from mathutils import Matrix, Vector, Quaternion
from mathutils.bvhtree import BVHTree
import numpy as np

SIDE = {'R': ('Clavicle_R', 'Shoulder_R', 'Elbow_R', 'Hand_R'), 'L': ('Clavicle_L', 'Shoulder_L', 'Elbow_L', 'Hand_L')}

def frame_mat(a, b):
    """Rotation with x=a, y=b (orthonormalised), z=a×b."""
    x = a.normalized(); y = (b - x * b.dot(x)).normalized(); z = x.cross(y)
    return Matrix((x, y, z)).transposed()

def swing_twist(q, axis):
    """Split q into swing*twist with twist about `axis`."""
    axis = axis.normalized()
    p = Vector((q.x, q.y, q.z)).dot(axis) * axis
    t = Quaternion((q.w, p.x, p.y, p.z))
    if t.magnitude < 1e-9: t = Quaternion()
    t.normalize(); s = q @ t.inverted()
    return s, t

def quat_angle(q):
    q = q.normalized(); return math.degrees(2 * math.acos(min(1.0, abs(q.w))))

class Arm:
    def __init__(self, sc, side):
        self.sc = sc; self.side = side
        self.clav, self.up, self.fore, self.hand = SIDE[side]
        R = lambda n: sc.bone_rest_world(n)
        self.S0 = R(self.up).to_translation(); self.E0 = R(self.fore).to_translation(); self.W0 = R(self.hand).to_translation()
        self.a = (self.E0 - self.S0).length; self.b = (self.W0 - self.E0).length
        self.u0 = (self.E0 - self.S0).normalized(); self.v0 = (self.W0 - self.E0).normalized()
        # natural flexion: the hand moves forward (-Y) as the elbow bends from the rest A-pose
        fwd = Vector((0, -1, 0))
        self.f0 = (fwd - self.u0 * fwd.dot(self.u0)).normalized()
        self.h0 = self.u0.cross(self.f0)          # elbow hinge axis at rest

    # world-delta helpers -----------------------------------------------------
    def set_world_delta(self, bone, D):
        """Pose `bone` so its world matrix = D (rotation, about its own head) applied to its rest, head kept where the parent puts it."""
        sc = self.sc; A = sc.rig.matrix_world; pb = sc.rig.pose.bones[bone]
        bpy.context.view_layer.update()
        head = (A @ pb.matrix).to_translation()
        rest = sc.bone_rest_world(bone)
        M = Matrix.Translation(head) @ D.to_matrix().to_4x4() @ Matrix.Translation(-rest.to_translation()) @ rest
        pb.matrix = A.inverted() @ M
        bpy.context.view_layer.update()

    def world_delta(self, bone):
        W = self.sc.bone_world(bone); R = self.sc.bone_rest_world(bone)
        return (W.to_3x3() @ R.to_3x3().inverted()).to_quaternion()

    def shoulder_pos(self):
        return self.sc.bone_world(self.up).to_translation()

    # the solve -----------------------------------------------------------------
    def solve(self, wrist, hand_rot, pole, twist_share=0.6, shrug=None, rel=None):
        """Two-bone IK: wrist position (world), hand world-delta rotation, elbow pole point (world)."""
        if shrug is not None:
            self.set_world_delta(self.clav, shrug)
        S = self.shoulder_pos()
        d = wrist - S; L = d.length
        L = max(abs(self.a - self.b) + 1e-3, min(self.a + self.b - 1e-4, L))
        dn = d.normalized()
        # elbow on the circle, toward the pole
        x = (self.a ** 2 - self.b ** 2 + L ** 2) / (2 * L); r = math.sqrt(max(0.0, self.a ** 2 - x ** 2))
        pdir = (pole - S); pdir = (pdir - dn * pdir.dot(dn)).normalized()
        E = S + dn * x + pdir * r
        W = S + dn * L
        u = (E - S).normalized(); v = (W - E).normalized()
        f = (v - u * v.dot(u))
        if f.length < 1e-4: f = pdir
        f.normalize()
        Ru = (frame_mat(u, f) @ frame_mat(self.u0, self.f0).transposed()).to_quaternion()
        h = u.cross(f)
        Rf0 = (frame_mat(v, h) @ frame_mat(self.v0, self.h0).transposed()).to_quaternion()
        self.last_Rf0 = Rf0
        if rel is not None:
            hand_rot = Rf0 @ rel
        # split the hand's twist about the forearm between forearm and wrist
        rel = Rf0.inverted() @ hand_rot
        _, tw = swing_twist(rel, self.v0)
        ang = 2 * math.atan2(Vector((tw.x, tw.y, tw.z)).dot(self.v0), tw.w)
        if ang > math.pi: ang -= 2 * math.pi
        if ang < -math.pi: ang += 2 * math.pi
        Rf = Quaternion(v, ang * twist_share) @ Rf0
        self.set_world_delta(self.up, Ru)
        self.set_world_delta(self.fore, Rf)
        self.set_world_delta(self.hand, hand_rot)
        return E

    # joint quality -----------------------------------------------------------------
    def joints(self):
        """Elbow flexion (deg), elbow bends the natural way, forearm twist (deg), wrist bend (deg), wrist twist (deg)."""
        S = self.sc.bone_world(self.up).to_translation(); E = self.sc.bone_world(self.fore).to_translation(); W = self.sc.bone_world(self.hand).to_translation()
        u = (E - S).normalized(); v = (W - E).normalized()
        flex = math.degrees(math.acos(max(-1, min(1, u.dot(v)))))
        Du = self.world_delta(self.up); Df = self.world_delta(self.fore); Dh = self.world_delta(self.hand)
        f_now = Du @ self.f0  # where the upper arm's natural flex direction points now
        natural = flex < 8 or (v - u * v.dot(u)).normalized().dot(f_now) > 0.5
        Dc = self.world_delta(self.clav)
        _, tu = swing_twist(Dc.inverted() @ Du, self.u0)
        rel_f = Du.inverted() @ Df; _, tf = swing_twist(rel_f, self.v0)
        rel_h = Df.inverted() @ Dh; sh, th = swing_twist(rel_h, self.v0)
        return dict(flex=flex, natural=natural, upper_twist=quat_angle(tu), fore_twist=quat_angle(tf), wrist_bend=quat_angle(sh), wrist_twist=quat_angle(th))

# ---------------------------------------------------------------------- penetration
class Body:
    """Skinned body BVH with per-region vertex groups for arm-vs-torso and item-vs-body checks."""
    def __init__(self, sc):
        self.sc = sc
        ob = sc.body; gn = {g.index: g.name for g in ob.vertex_groups}
        dom = []
        for v in ob.data.vertices:
            g = max(v.groups, key=lambda g: g.weight) if v.groups else None
            dom.append(gn[g.group] if g else '')
        self.dom = dom
        arm = lambda s: {n for n in dom if n.startswith(('Shoulder_' + s, 'Elbow_' + s, 'Hand_' + s, 'Finger', 'IndexFinger', 'Thumb')) and (n.endswith('_1') == (s == 'R') or n in ('Shoulder_' + s, 'Elbow_' + s, 'Hand_' + s))}
        self.arm_R = np.array([d in ('Elbow_R', 'Hand_R') or (d.endswith('_1')) for d in dom])
        self.arm_L = np.array([d in ('Elbow_L', 'Hand_L') or (d.startswith(('Finger_0', 'IndexFinger_0', 'Thumb_0')) and not d.endswith('_1')) for d in dom])
        torso = {'Hips', 'Spine_01', 'Spine_02', 'Spine_03', 'Neck', 'Head', 'UpperLeg_L', 'UpperLeg_R'}
        self.torso_v = np.array([d in torso for d in dom])
        self.faces = [tuple(p.vertices) for p in ob.data.polygons]
        self.torso_f = [f for f in self.faces if all(self.torso_v[i] for i in f)]

    def update(self):
        self.P = self.sc.body_mesh_world()
        self.all_bvh = BVHTree.FromPolygons(self.P, self.faces)
        self.torso_bvh = BVHTree.FromPolygons(self.P, self.torso_f)

    @staticmethod
    def depth(bvh, pts, step=1):
        """Max penetration (m) and count of points inside a closed-ish surface, by nearest-face normal."""
        worst = 0.0; n = 0
        for p in pts[::step]:
            hit = bvh.find_nearest(p)
            if hit[0] is None: continue
            loc, nrm, _, dist = hit
            if (p - loc).dot(nrm) < 0 and dist < 0.2:
                n += 1; worst = max(worst, dist)
        return worst, n

    def arm_into_torso(self, side):
        mask = self.arm_R if side == 'R' else self.arm_L
        pts = [self.P[i] for i in np.nonzero(mask)[0]]
        return self.depth(self.torso_bvh, pts, 2)

    def item_into_body(self, ob, step=1):
        pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
        return self.depth(self.all_bvh, pts, step)

def item_into_item(a, b, step=1):
    """Penetration of a's vertices into b."""
    bm_pts = [b.matrix_world @ v.co for v in b.data.vertices]
    bvh = BVHTree.FromPolygons(bm_pts, [tuple(p.vertices) for p in b.data.polygons])
    return Body.depth(bvh, [a.matrix_world @ v.co for v in a.data.vertices], step)

# ---------------------------------------------------------------------- capsule body proxy
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (p - (a + ab * t)).length

def seg_seg(p1, q1, p2, q2):
    # closest distance between segments (sampled; fine for checks)
    best = 1e9
    for i in range(9):
        a = p1.lerp(q1, i / 8)
        best = min(best, seg_dist(a, p2, q2))
    for i in range(9):
        a = p2.lerp(q2, i / 8)
        best = min(best, seg_dist(a, p1, q1))
    return best

class Capsules:
    SEGS = {  # name: (from bone head, to bone head or 'tail:+z' offset, bones whose skin fits the radius)
        'pelvis': ('Hips', 'Spine_01', ('Hips',)),
        'belly': ('Spine_01', 'Spine_02', ('Spine_01',)),
        'chest': ('Spine_02', 'Spine_03', ('Spine_02',)),
        'upchest': ('Spine_03', 'Neck', ('Spine_03',)),
        'head': ('Neck', 'HEADTOP', ('Head', 'Neck')),
        'uarm_R': ('Shoulder_R', 'Elbow_R', ('Shoulder_R',)), 'farm_R': ('Elbow_R', 'Hand_R', ('Elbow_R',)),
        'uarm_L': ('Shoulder_L', 'Elbow_L', ('Shoulder_L',)), 'farm_L': ('Elbow_L', 'Hand_L', ('Elbow_L',)),
    }
    def __init__(self, sc, pct=50):
        self.sc = sc
        ob = sc.body; gn = {g.index: g.name for g in ob.vertex_groups}
        P = [ob.matrix_world @ v.co for v in ob.data.vertices]  # rest
        dom = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in ob.data.vertices]
        self.r = {}
        for k, (a, b, bones) in self.SEGS.items():
            A, B = self.ends(k, rest=True)
            ds = sorted(seg_dist(P[i], A, B) for i in range(len(P)) if dom[i] in bones)
            self.r[k] = ds[int(len(ds) * pct / 100)] if ds else 0.05
    def ends(self, k, rest=False):
        a, b, _ = self.SEGS[k]
        get = (lambda n: self.sc.bone_rest_world(n)) if rest else (lambda n: self.sc.bone_world(n))
        A = get(a).to_translation()
        if b == 'HEADTOP':
            H = get('Head'); B = H.to_translation() + (H.to_translation() - get('Neck').to_translation()).normalized() * 0.22
        else:
            B = get(b).to_translation()
        return A, B
    def depth_points(self, pts, skip=()):
        """Deepest penetration of points into any capsule (minus skipped)."""
        segs = {k: self.ends(k) for k in self.SEGS if k not in skip}
        worst = 0.0; where = ''
        for p in pts:
            for k, (A, B) in segs.items():
                d = self.r[k] - seg_dist(p, A, B)
                if d > worst: worst, where = d, k
        return worst, where
    def arm_hits(self, side):
        """Arm capsules overlapping torso/head capsules (depth)."""
        worst = 0.0; where = ''
        for arm in ('uarm_' + side, 'farm_' + side):
            A1, B1 = self.ends(arm)
            for k in ('belly', 'chest', 'upchest', 'head', 'pelvis'):
                if arm.startswith('uarm') and k in ('upchest', 'chest'): continue  # the shoulder joint sits in them
                A2, B2 = self.ends(k)
                d = self.r[arm] + self.r[k] - seg_seg(A1, B1, A2, B2)
                if d > worst: worst, where = d, f'{arm}-{k}'
        return worst, where
