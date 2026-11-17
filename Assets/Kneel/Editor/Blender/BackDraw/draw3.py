# Back draw / sheathe v3 on the Ember Warden: one continuous arm-space key track per arm (idle -> guard), the shield
# taken overhand by its top rim over the left shoulder, lifted over it, tossed up and caught in the grip; smoothed,
# checked per frame (joints, capsules, angular speed), baked for Unity. python3 draw3.py [render] [draw|sheath]
import sys, json, math; sys.path.insert(0, '/home/claude/anim')
from kscene import *
from karm import *

RENDER = 'render' in sys.argv
FPS = 30
sc = Scene(); colour_items(sc); load_layout(sc)
IDLE = game_pose(sc, 'idle'); sc.place()
ARMED = game_pose(sc, 'idle', 'armed')
set_pose(sc, IDLE)
RA, LA = Arm(sc, 'R'), Arm(sc, 'L')
UB = upper_bones(sc)
FING_R = {n for n in UB if n.endswith('_1') and ('Finger' in n or 'Thumb' in n)}
FING_L = {n for n in UB if not n.endswith('_1') and ('Finger' in n or 'Thumb' in n)}
_, off_sw = sc.mounts['SwordHand']; _, off_sh = sc.mounts['ShieldHand']
REST_HR = sc.bone_rest_world('Hand_R'); REST_HL = sc.bone_rest_world('Hand_L')
SR = RA.shoulder_pos(); SL = LA.shoulder_pos()
cap0 = Capsules(sc)
FLEX_MAX = 118.0

P = dict(end=33,
         # right (sword)
         grab_R=10, pull_R=14, clear_R=17, up_R=20, over_R=24, pull=0.15,
         clear_off=(0.10, 0.25, 0.46), clear_dir=(1.0, 0.0, -0.1),
         up_off=(-0.04, 0.20, 0.48), up_dir=(-0.05, 1.0, 0.15),
         over_off=(-0.12, -0.18, 0.32), over_dir=(-0.05, 0.35, 1.0),
         # left (shield)
         outL_L=2.5, reach_L=5.5, grab_L=9, lift_L=12, overL_L=17, front_L=21, catch_L=25, armedL_L=30,
         reachL_off=(0.14, 0.12, 0.36), outL_off=(0.34, 0.06, -0.10), lift_t=(0.0, 0.04, 0.10),
         overL_off=(0.24, -0.04, 0.36), frontL_off=(0.12, -0.36, 0.06), catch_lift=(0.0, 0.02, 0.10), toss_arc=0.08)
for a in sys.argv[1:]:
    if a.startswith('{'): P.update(json.loads(a))

def smooth(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ease(f, a, b): return smooth((f - a) / max(1e-6, b - a))
def rel_angle(a, b): return quat_angle(a.rotation_difference(b))

def vcr(vals, F, f):
    if f <= F[0]: return vals[0].copy()
    if f >= F[-1]: return vals[-1].copy()
    i = max(j for j in range(len(F)) if F[j] <= f)
    u = (f - F[i]) / (F[i + 1] - F[i]); h = F[i + 1] - F[i]
    p1, p2 = vals[i], vals[i + 1]
    m1 = (p2 - vals[i - 1]) / (F[i + 1] - F[i - 1]) * h if i > 0 else p2 * 0
    m2 = (vals[i + 2] - p1) / (F[i + 2] - F[i]) * h if i + 2 < len(vals) else p2 * 0
    u2 = u * u; u3 = u2 * u
    return (2 * u3 - 3 * u2 + 1) * p1 + (u3 - 2 * u2 + u) * m1 + (-2 * u3 + 3 * u2) * p2 + (u3 - u2) * m2

def qcr(qs, F, f):
    al = []; prev = None
    for q in qs:
        q = q.copy()
        if prev is not None and prev.dot(q) < 0: q.negate()
        al.append(q); prev = q
    v = vcr([Vector((q.w, q.x, q.y, q.z)) for q in al], F, f)
    return Quaternion((v[0], v[1], v[2], v[3])).normalized()

class ArmTrack:
    def __init__(self, keys): self.k = sorted(keys, key=lambda k: k[0]); self.F = [k[0] for k in self.k]
    def at(self, f):
        return (vcr([k[1] for k in self.k], self.F, f), vcr([k[2] for k in self.k], self.F, f), qcr([k[3] for k in self.k], self.F, f))

def arm_state(arm, wrist, pole, hand_q):
    arm.solve(wrist, hand_q, pole)
    return wrist, pole, arm.last_Rf0.inverted() @ hand_q

def fk_state(arm, pose):
    set_pose(sc, pose)
    w = sc.bone_world(arm.hand).to_translation(); q = arm.world_delta(arm.hand)
    E = sc.bone_world(arm.fore).to_translation(); S = sc.bone_world(arm.up).to_translation()
    r = arm_state(arm, w, E + (E - (S + w) * 0.5).normalized() * 0.3, q)
    set_pose(sc, IDLE); return r

def hand_from_item(item_world, off, rest):
    H = item_world @ off.inverted()
    return H.to_translation(), (H.to_3x3() @ rest.to_3x3().inverted()).to_quaternion()

def hand_matrix(wrist, q, rest):
    return Matrix.Translation(wrist) @ q.to_matrix().to_4x4() @ Matrix.Translation(-rest.to_translation()) @ rest

def frame_from(pos, z_dir, y_dir, scale):
    z = Vector(z_dir).normalized(); y = (Vector(y_dir) - z * Vector(y_dir).dot(z)).normalized(); x = y.cross(z)
    return Matrix.Translation(pos) @ Matrix((x, y, z)).transposed().to_4x4() @ Matrix.Scale(scale, 4)

POLES = [Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
         for az in range(0, 360, 30) for el in (-45, -15, 15, 45, 75)]

def strain(j):
    return (j['wrist_bend'] + 0.6 * j['wrist_twist'] + 0.4 * j['fore_twist'] + 0.25 * max(0, j['upper_twist'] - 90)
            + 6 * max(0, j['flex'] - FLEX_MAX) + 3 * max(0, 15 - j['flex']))

# ======================================================================= right arm (sword) — the arc that worked
S1 = sc.mount_world('Sheath'); z_in = (S1.to_3x3() @ Vector((0, 0, 1))).normalized(); scale_sw = S1.to_scale()[0]
S_pull = Matrix.Translation(-z_in * P['pull']) @ S1

def solve_sword_key(grip, blade_dir, prev):
    bd = Vector(blade_dir).normalized(); ref = Vector((0, 1, 0)) if abs(bd.y) < 0.9 else Vector((1, 0, 0))
    base = frame_from(grip, bd, ref, scale_sw); best = None
    for roll in range(0, 360, 15):
        S = base @ Matrix.Rotation(math.radians(roll), 4, 'Z')
        w, q = hand_from_item(S, off_sw, REST_HR)
        if (w - SR).length > RA.a + RA.b - 0.01: continue
        for d in POLES:
            pole = SR + d * 0.5
            set_pose(sc, IDLE); RA.solve(w, q, pole); j = RA.joints()
            c = strain(j) + 60 * (pole - prev[1]).length + 30 * rel_angle(RA.last_Rf0.inverted() @ q, prev[2]) / 90
            sc.holder['sword'] = 'SwordHand'; sc.place()
            pts = [sc.items['sword'].matrix_world @ v.co for v in sc.items['sword'].data.vertices][::4]
            c += 600 * cap0.depth_points(pts, skip=('farm_R', 'uarm_R'))[0] + 300 * cap0.arm_hits('R')[0]
            if best is None or c < best[0]: best = (c, (w, pole, RA.last_Rf0.inverted() @ q), j)
    set_pose(sc, IDLE); return best[1]

def best_pole_for(arm, w, q, prev_pole, extra=None):
    best = None
    for d in POLES:
        pole = arm.shoulder_pos() + d * 0.5
        set_pose(sc, IDLE); arm.solve(w, q, pole); j = arm.joints()
        c = strain(j) + 300 * cap0.arm_hits(arm.side)[0] + (40 * (pole - prev_pole).length if prev_pole is not None else 0)
        if extra: c += extra()
        if best is None or c < best[0]: best = (c, (w, pole, arm.last_Rf0.inverted() @ q), j)
    set_pose(sc, IDLE); return best

kR_idle = fk_state(RA, IDLE); kR_armed = fk_state(RA, ARMED)
wg, qg = hand_from_item(S1, off_sw, REST_HR)
kR_grab = best_pole_for(RA, wg, qg, SR + Vector((-0.45, 0.25, 0.0)))[1]
wp, qp = hand_from_item(S_pull, off_sw, REST_HR)
kR_pull = best_pole_for(RA, wp, qp, kR_grab[1])[1]
kR_clear = solve_sword_key(SR + Vector(P['clear_off']), P['clear_dir'], kR_pull)
kR_up = solve_sword_key(SR + Vector(P['up_off']), P['up_dir'], kR_clear)
kR_over = solve_sword_key(SR + Vector(P['over_off']), P['over_dir'], kR_up)
kR_side = (kR_grab[0] + Vector((-0.16, -0.06, -0.12)), kR_grab[1], kR_grab[2])
trackR = ArmTrack([(0, *kR_idle), (P['grab_R'] - 4, *kR_side), (P['grab_R'], *kR_grab), (P['pull_R'], *kR_pull),
                   (P['clear_R'], *kR_clear), (P['up_R'], *kR_up), (P['over_R'], *kR_over), (P['end'] - 4, *kR_armed), (P['end'], *kR_armed)])

# ======================================================================= left arm (shield)
SH0 = sc.mount_world('ShieldBack')
U_sh = lambda M: (M.to_3x3() @ Vector((0, 0, 1))).normalized()           # shield up
N_sh = lambda M: (M.to_3x3() @ Vector((0, -1, 0))).normalized()          # shield face (out of the back when stowed)
shV = [v.co.copy() for v in sc.items['shield'].data.vertices]
top_local = max(v.z for v in shV)
rim_local = [v for v in shV if v.z > top_local - 0.035]
rim_pt_local = min(rim_local, key=lambda v: (SH0 @ v - SL).length)       # the rim point nearest the left shoulder
# the hand's palm, from how it holds the grip in guard: palm faces the shield socket
fdir0 = (sc.bone_rest_world('Finger_01').to_translation() + sc.bone_rest_world('IndexFinger_01').to_translation()) * 0.5 - REST_HL.to_translation()
grip_vec0 = sc.sock_world['ShieldHand'].to_translation() - REST_HL.to_translation()
n0 = (grip_vec0 - fdir0.normalized() * grip_vec0.dot(fdir0.normalized())).normalized()   # palm normal at rest
f0 = fdir0.normalized()
palm0 = fdir0 * 0.55 + n0 * 0.03                                         # palm centre from the wrist, at rest

def hold_on_rim(M_shield, phi_deg, tilt_deg):
    """Hand pose holding the rim overhand: palm down on it (along -shield up), fingers across it at angle phi."""
    U = U_sh(M_shield); Nf = N_sh(M_shield)
    side = U.cross(Nf).normalized()
    fd = (Nf * math.cos(math.radians(phi_deg)) + side * math.sin(math.radians(phi_deg))).normalized()
    n = (-U)
    n = (Quaternion(fd, math.radians(tilt_deg)) @ n)
    R_rest = Matrix((f0, n0, f0.cross(n0))).transposed()
    R_new = Matrix((fd, (n - fd * n.dot(fd)).normalized(), fd.cross((n - fd * n.dot(fd)).normalized()))).transposed()
    q = (R_new @ R_rest.transposed()).to_quaternion()
    P_rim = M_shield @ rim_pt_local + U * 0.025
    wrist = P_rim - q @ palm0
    return wrist, q

def shield_pts(M): return [M @ shV[i] for i in range(0, len(shV), 4)]

# grab: search the rim hold (phi, tilt) and pole for the least strain; the hold then rides rigidly in shield space
best = None
for phi in range(0, 360, 20):
    for tilt in (-30, -15, 0, 15, 30):
        w, q = hold_on_rim(SH0, phi, tilt)
        if (w - SL).length > LA.a + LA.b - 0.01: continue
        b = best_pole_for(LA, w, q, SL + Vector((0.45, 0.20, 0.25)))
        if best is None or b[0] < best[0]: best = (b[0], phi, tilt, b)
print("rim hold: phi %d tilt %d cost %.0f" % (best[1], best[2], best[0]), {k: round(v) if isinstance(v, float) else v for k, v in best[3][2].items()})
kL_grab = best[3][1]
H_hold = hand_matrix(kL_grab[0], hold_on_rim(SH0, best[1], best[2])[1], REST_HL)
hand_in_shield = SH0.inverted() @ H_hold

def key_from_shield(M, prev):
    H = M @ hand_in_shield
    w = H.to_translation(); q = (H.to_3x3() @ REST_HL.to_3x3().inverted()).to_quaternion()
    b = best_pole_for(LA, w, q, prev[1], extra=lambda: 900 * cap0.depth_points(shield_pts(M), skip=('farm_L',))[0])
    return b[1], b[2]

def search_carry(target_hand_pos, prev, face_pref=None, label=''):
    """Hand at a position, shield hanging from the rim hold: search the shield's turn for strain + clearance."""
    best = None
    for yaw in range(0, 360, 20):
        for pitch in (-40, -20, 0, 20, 40):
            for roll in (-30, 0, 30):
                R = Euler((math.radians(pitch), math.radians(roll), math.radians(yaw))).to_matrix().to_4x4()
                Mr = R @ SH0.to_3x3().normalized().to_4x4()          # candidate shield turn
                Mr = Mr @ Matrix.Scale(SH0.to_scale()[0], 4)
                H = Mr @ hand_in_shield
                M = Matrix.Translation(target_hand_pos - H.to_translation()) @ Mr
                w = (M @ hand_in_shield).to_translation(); q = ((M @ hand_in_shield).to_3x3() @ REST_HL.to_3x3().inverted()).to_quaternion()
                if (w - SL).length > LA.a + LA.b - 0.01: continue
                set_pose(sc, IDLE); LA.solve(w, q, prev[1]); j0 = LA.joints()
                if j0['wrist_bend'] > 80: continue
                depth = cap0.depth_points(shield_pts(M), skip=('farm_L',))[0]
                if depth > 0.06: continue
                b = best_pole_for(LA, w, q, prev[1], extra=lambda: 900 * cap0.depth_points(shield_pts(M), skip=('farm_L',))[0])
                c = b[0] + 25 * rel_angle(b[1][2], prev[2]) / 90
                if face_pref is not None: c += 40 * (1 - N_sh(M).dot(Vector(face_pref).normalized()))
                if best is None or c < best[0]: best = (c, M, b)
    print(label, "cost %.0f" % best[0], {k: round(v) if isinstance(v, float) else v for k, v in best[2][2].items()})
    return best[1], best[2][1]

kL_idle = fk_state(LA, IDLE); kL_armed = fk_state(LA, ARMED)
SH_lift = Matrix.Translation(Vector(P['lift_t'])) @ SH0
kL_lift, jl = key_from_shield(SH_lift, kL_grab)
SH_over, kL_over = search_carry(SL + Vector(P['overL_off']), kL_lift, label='over')
SH_front, kL_front = search_carry(SL + Vector(P['frontL_off']), kL_over, face_pref=(0.2, -1, 0), label='front')
# catch: straight into the grip, the shield in its guard turn a little above the guard pose
set_pose(sc, ARMED); sc.place(); SH_ARMED = sc.mount_world('ShieldHand').copy(); set_pose(sc, IDLE)
SH_catch = Matrix.Translation(Vector(P['catch_lift'])) @ SH_ARMED
wc, qc = hand_from_item(SH_catch, off_sh, REST_HL)
kL_catch = best_pole_for(LA, wc, qc, kL_armed[1])[1]
kL_reach = (SL + Vector(P['reachL_off']), kL_grab[1], kL_grab[2])
kL_out = (SL + Vector(P['outL_off']), SL + Vector((0.45, 0.15, -0.30)), kL_idle[2].slerp(kL_grab[2], 0.4))
trackL = ArmTrack([(0, *kL_idle), (P['outL_L'], *kL_out), (P['reach_L'], *kL_reach), (P['grab_L'], *kL_grab), (P['lift_L'], *kL_lift),
                   (P['overL_L'], *kL_over), (P['front_L'], *kL_front), (P['catch_L'], *kL_catch), (P['armedL_L'], *kL_armed), (P['end'], *kL_armed)])

# ======================================================================= pose every frame (arms in arm space), smooth, items
ARM_BONES = list(SIDE['R']) + list(SIDE['L'])
def raw_pose(f):
    set_pose(sc, IDLE)
    for n in UB - set(ARM_BONES) - FING_R - FING_L:   # head/neck: idle -> armed across the clip
        sc.rig.pose.bones[n].rotation_quaternion = IDLE[n][1].slerp(ARMED[n][1], ease(f, 6, P['end']))
    for n in FING_R: sc.rig.pose.bones[n].rotation_quaternion = IDLE[n][1].slerp(ARMED[n][1], ease(f, P['grab_R'] - 3, P['grab_R']))
    for n in FING_L:
        k = ease(f, P['grab_L'] - 3, P['grab_L']) * (1 - ease(f, P['front_L'], P['front_L'] + 1.5)) + ease(f, P['catch_L'] - 1.5, P['catch_L'])
        sc.rig.pose.bones[n].rotation_quaternion = IDLE[n][1].slerp(ARMED[n][1], min(1.0, k))
    bpy.context.view_layer.update()
    w, pole, rel = trackR.at(f)
    shrug = smooth((w.z - SR.z - 0.20) / 0.30) * 15.0
    RA.solve(w, None, pole, shrug=Quaternion(Vector((0, 1, 0)), math.radians(shrug)) @ RA.world_delta('Clavicle_R'), rel=rel)
    w, pole, rel = trackL.at(f)
    shrugL = smooth((w.z - SL.z - 0.20) / 0.30) * 12.0
    LA.solve(w, None, pole, shrug=Quaternion(Vector((0, -1, 0)), math.radians(shrugL)) @ LA.world_delta('Clavicle_L'), rel=rel)
    return {n: sc.rig.pose.bones[n].rotation_quaternion.copy() for n in UB}

def smooth_seq(seq, passes=2):
    names = seq[0].keys()
    out = [dict(s) for s in seq]
    for _ in range(passes):
        nxt = [dict(s) for s in out]
        for i in range(1, len(out) - 1):
            for n in names:
                a, b, c = out[i - 1][n].copy(), out[i][n].copy(), out[i + 1][n].copy()
                if a.dot(b) < 0: a.negate()
                if c.dot(b) < 0: c.negate()
                v = (Vector((a.w, a.x, a.y, a.z)) + 2 * Vector((b.w, b.x, b.y, b.z)) + Vector((c.w, c.x, c.y, c.z))) * 0.25
                nxt[i][n] = Quaternion((v[0], v[1], v[2], v[3])).normalized()
        out = nxt
    return out

def apply(locals_):
    set_pose(sc, IDLE)
    for n, q in locals_.items(): sc.rig.pose.bones[n].rotation_quaternion = q
    bpy.context.view_layer.update()

POSES_J = json.load(open('/mnt/user-data/uploads/Kneel-Game/Temp/kael_poses.json'))
BIND = {n: umat(m) for n, m in POSES_J['bind'].items()}
def unity_frame():
    out = {}
    for n, Bm in BIND.items():
        M = CI @ (sc.bone_world(n) @ sc.bone_rest_world(n).inverted()) @ C @ Bm
        t = M.to_translation(); q = M.to_quaternion()
        out[n] = [round(t.x, 6), round(t.y, 6), round(t.z, 6), round(q.x, 7), round(q.y, 7), round(q.z, 7), round(q.w, 7)]
    return out

def socket_local_U(M_sock, M_item):
    O = CI @ (M_sock.inverted() @ M_item) @ C
    t = O.to_translation(); q = O.to_quaternion()
    return [t.x, t.y, t.z, q.x, q.y, q.z, q.w]

def build(name, fmap, nframes):
    raws = [raw_pose(fmap(g)) for g in range(nframes + 1)]
    sm = smooth_seq(raws, 2)
    # shield pose per frame (needs the smoothed hands): held on the rim / tossed / in the grip
    rel_pts = {}
    def hand_world(g): apply(sm[g]); return sc.bone_world('Hand_L').copy(), sc.mount_world('ShieldHand').copy()
    gs = list(range(nframes + 1))
    F = [fmap(g) for g in gs]
    g_release = None
    shield_W = []
    for g in gs:
        f = F[g]
        if f < P['grab_L']:
            shield_W.append(None)
        elif f <= P['front_L']:
            Hh, _ = hand_world(g); shield_W.append(Hh @ hand_in_shield.inverted())
        elif f < P['catch_L']:
            shield_W.append('toss')
        else:
            _, Hs = hand_world(g); shield_W.append(Hs)
    # the toss: from the release pose up in a small arc and into the grip at the catch
    def at_f(ff):  # the pose at the boundary frames (nearest g)
        g = min(gs, key=lambda g: abs(F[g] - ff)); return shield_W[g]
    held = [g for g in gs if isinstance(shield_W[g], Matrix) and P['grab_L'] <= F[g] <= P['front_L']]
    grip = [g for g in gs if isinstance(shield_W[g], Matrix) and F[g] >= P['catch_L']]
    gr = max(held, key=lambda g: F[g]); gc = min(grip, key=lambda g: F[g])
    Mr = shield_W[gr]; Mc = shield_W[gc]
    fr0, fc0 = F[gr], F[gc]
    for g in gs:
        if shield_W[g] == 'toss':
            u = (F[g] - fr0) / (fc0 - fr0)
            k = smooth(u)
            p = Mr.to_translation().lerp(Mc.to_translation(), k) + Vector((0, 0, P['toss_arc'] * 4 * u * (1 - u)))
            q = Mr.to_quaternion().slerp(Mc.to_quaternion(), k)
            shield_W[g] = Matrix.Translation(p) @ q.to_matrix().to_4x4() @ Matrix.Scale(SH0.to_scale()[0], 4)
    frames, offsets, rep = [], [], []
    prev = None
    for g in gs:
        f = F[g]; apply(sm[g])
        sc.holder['sword'] = 'SwordHand' if f >= P['grab_R'] else 'Sheath'
        sc.holder['shield'] = 'ShieldBack'; sc.place()
        if shield_W[g] is not None: sc.items['shield'].matrix_world = shield_W[g]
        bpy.context.view_layer.update()
        offsets.append(socket_local_U(sc.mount_world('ShieldHand'), sc.items['shield'].matrix_world) if shield_W[g] is not None else None)
        sw = [sc.items['sword'].matrix_world @ v.co for v in sc.items['sword'].data.vertices][::2]
        shp = [sc.items['shield'].matrix_world @ v.co for v in sc.items['shield'].data.vertices][::3]
        cur = {n: sc.bone_world(n).to_quaternion() for n in ARM_BONES}
        speed = max(rel_angle(cur[n], prev[n]) for n in ARM_BONES) if prev else 0
        prev = cur
        rep.append(dict(g=g, f=f, R=RA.joints(), L=LA.joints(), sword=cap.depth_points(sw, skip=('farm_R',)),
                        shield=cap.depth_points(shp, skip=('farm_L',)), armR=cap.arm_hits('R'), armL=cap.arm_hits('L'),
                        swsh=item_into_item(sc.items['sword'], sc.items['shield'], 3) if f >= P['grab_R'] else (0, 0), speed=speed))
        frames.append(unity_frame())
        if RENDER and (g % 2 == 0 or g == nframes):
            render_views(f'/home/claude/anim/frames/{name[0]}_{g:02d}', ['back34', 'front', 'side_r', 'game'], scale=2.3, res=330)
    print("=== " + name)
    for r in rep:
        J = lambda d: f"fl{d['flex']:.0f} wb{d['wrist_bend']:.0f} wt{d['wrist_twist']:.0f} ft{d['fore_twist']:.0f}"
        print(f"{name[0]}{r['g']:02d} R[{J(r['R'])}] L[{J(r['L'])}] sword {r['sword'][0]*100:.0f} shield {r['shield'][0]*100:.0f}{r['shield'][1]} armR {r['armR'][0]*100:.0f} armL {r['armL'][0]*100:.0f} sw-sh {r['swsh'][0]*100:.0f} speed {r['speed']:.0f}")
    return frames, offsets

cap = Capsules(sc)
which = [a for a in sys.argv[1:] if a in ('draw', 'sheath')] or ['draw', 'sheath']
D = P['end']; SHE = 36
def warp(u): return 0.6 * u + 0.4 * smooth(u)          # eased ends, same length
def unwarp(y):
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if warp(mid) < y: lo = mid
        else: hi = mid
    return (lo + hi) / 2
if 'draw' in which:
    fr, off = build('draw', lambda g: D * warp(g / D), D)
    t_of = lambda ef: unwarp(ef / D) * D / FPS
    json.dump({'fps': FPS, 'frames': fr, 'shield': off, 'events': {'swordGrab': t_of(P['grab_R']), 'shieldGrab': t_of(P['grab_L']), 'shieldCatch': t_of(P['catch_L'])}},
              open('/home/claude/anim/draw_unity.json', 'w'))
if 'sheath' in which:
    g2f = lambda g: D * (1 - warp(g / SHE)); f2t = lambda f: unwarp(1 - f / D) * SHE / FPS
    fr, off = build('sheath', g2f, SHE)
    json.dump({'fps': FPS, 'frames': fr, 'shield': off, 'events': {'swordRelease': f2t(P['grab_R']), 'shieldRelease': f2t(P['grab_L']), 'shieldToss': f2t(P['catch_L'])}},
              open('/home/claude/anim/sheath_unity.json', 'w'))
