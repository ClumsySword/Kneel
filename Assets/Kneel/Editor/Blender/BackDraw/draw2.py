# Back draw on the Ember Warden, keyed in arm space (wrist, elbow pole, hand-relative-to-forearm), checked per frame,
# baked for Unity. Usage: python3 draw2.py [render] ['{json overrides}']
import sys, json, math; sys.path.insert(0, '/home/claude/anim')
from kscene import *
from karm import *

RENDER = 'render' in sys.argv
OVR = next((json.loads(a) for a in sys.argv[1:] if a.startswith('{')), {})
FPS = 30

sc = Scene(); colour_items(sc); load_layout(sc)
IDLE = game_pose(sc, 'idle'); sc.place()
ARMED = game_pose(sc, 'idle', 'armed')
set_pose(sc, IDLE)
RA, LA = Arm(sc, 'R'), Arm(sc, 'L')
UB = upper_bones(sc)
FING_R = {n for n in UB if n.endswith('_1') and ('Finger' in n or 'Thumb' in n)}
FING_L = {n for n in UB if not n.endswith('_1') and ('Finger' in n or 'Thumb' in n)}
_, off_sw = sc.mounts['SwordHand']
REST_HR = sc.bone_rest_world('Hand_R'); REST_HL = sc.bone_rest_world('Hand_L')

def smooth(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ease(f, a, b): return smooth((f - a) / max(1e-6, b - a))

def qcr(qs, F, f):
    """Catmull-Rom through quaternions (aligned), normalised."""
    al = []; prev = None
    for q in qs:
        q = q.copy()
        if prev is not None and prev.dot(q) < 0: q.negate()
        al.append(q); prev = q
    vs = [Vector((q.w, q.x, q.y, q.z)) for q in al]
    v = vcr(vs, F, f)
    return Quaternion((v[0], v[1], v[2], v[3])).normalized()

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

class ArmTrack:
    def __init__(self, keys):  # keys: (frame, wrist, pole, rel)
        self.k = sorted(keys, key=lambda k: k[0]); self.F = [k[0] for k in self.k]
    def at(self, f):
        return (vcr([k[1] for k in self.k], self.F, f), vcr([k[2] for k in self.k], self.F, f), qcr([k[3] for k in self.k], self.F, f))

def arm_state(arm, wrist, pole, hand_world_q):
    """(wrist, pole, rel) for a hand pose: solve once and read the hand relative to the twist-free forearm."""
    arm.solve(wrist, hand_world_q, pole)
    return wrist, pole, arm.last_Rf0.inverted() @ hand_world_q

def fk_state(arm, pose, pole_hint=None):
    set_pose(sc, pose)
    w = sc.bone_world(arm.hand).to_translation()
    q = arm.world_delta(arm.hand)
    E = sc.bone_world(arm.fore).to_translation(); S = sc.bone_world(arm.up).to_translation()
    pole = E + (E - (S + w) * 0.5).normalized() * 0.3   # the clip's own elbow
    r = arm_state(arm, w, pole, q)
    set_pose(sc, IDLE)
    return r

def hand_from_item(item_world, off, rest):
    H = item_world @ off.inverted()
    return H.to_translation(), (H.to_3x3() @ rest.to_3x3().inverted()).to_quaternion()

# ------------------------------------------------------------------------------------------------ keys
P = dict(grab_R=9, pull_R=13, clear_R=16, up_R=19, over_R=23, armed_R=28, end=33, pull=0.15,
         clear_off=(0.10, 0.25, 0.46), clear_dir=(1.0, 0.0, -0.1),
         up_off=(-0.04, 0.20, 0.48), up_dir=(-0.05, 1.0, 0.15),
         over_off=(-0.12, -0.18, 0.32), over_dir=(-0.05, 0.35, 1.0),
         grab_L=7, out_L=11, side_L=15, front_L=21, armed_L=27,
         out_t=(0.10, 0.06, 0.0), out_yaw=-35.0, side_t=(0.40, -0.13, -0.16), side_yaw=-90.0,
         front_lift=(0.0, 0.10, 0.20), hold_like_grip=0.5)
P.update(OVR)
SR = RA.shoulder_pos(); SL = LA.shoulder_pos()
S1 = sc.mount_world('Sheath'); z_in = (S1.to_3x3() @ Vector((0, 0, 1))).normalized()
S_pull = Matrix.Translation(-z_in * P['pull']) @ S1
v0R = RA.v0

pole_grab = SR + Vector((-0.45, 0.25, 0.0))
kR_grab = arm_state(RA, *hand_from_item(S1, off_sw, REST_HR)[:1], pole_grab, hand_from_item(S1, off_sw, REST_HR)[1]); set_pose(sc, IDLE)
kR_pull = arm_state(RA, hand_from_item(S_pull, off_sw, REST_HR)[0], SR + Vector((-0.45, 0.20, 0.10)), hand_from_item(S_pull, off_sw, REST_HR)[1]); set_pose(sc, IDLE)
rel_pull = kR_pull[2]
cap0 = Capsules(sc)

def solve_key(grip, blade_dir, prev, roll_step=15, sphere=None, extra_clear=True):
    """Best arm-space state holding the sword at `grip` with its blade along `blade_dir` (roll about the blade free)."""
    bd = Vector(blade_dir).normalized()
    ref = Vector((0, 1, 0)) if abs(bd.y) < 0.9 else Vector((1, 0, 0))
    base = frame_from(grip, bd, ref, scale_sw)
    best = None
    for roll in range(0, 360, roll_step):
        S = base @ Matrix.Rotation(math.radians(roll), 4, 'Z')
        w, q = hand_from_item(S, off_sw, REST_HR)
        if (w - SR).length > RA.a + RA.b - 0.01: continue
        for az in range(0, 360, 30):
            for el in (-45, -15, 15, 45, 75):
                d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
                pole = SR + d * 0.5
                set_pose(sc, IDLE); RA.solve(w, q, pole); j = RA.joints()
                E = sc.bone_world('Elbow_R').to_translation()
                cost = j['wrist_bend'] + 0.6 * j['wrist_twist'] + 0.4 * j['fore_twist'] + 0.25 * max(0, j['upper_twist'] - 90)
                if prev is not None:
                    cost += 60 * (pole - prev[1]).length + 30 * (rel_angle(RA.last_Rf0.inverted() @ q, prev[2]) / 90.0)
                if extra_clear:
                    sc.holder['sword'] = 'SwordHand'; sc.place()
                    pts = [sc.items['sword'].matrix_world @ v.co for v in sc.items['sword'].data.vertices][::4]
                    cost += 600 * cap0.depth_points(pts, skip=('farm_R', 'uarm_R'))[0] + 300 * cap0.arm_hits('R')[0]
                if best is None or cost < best[0]:
                    best = (cost, (w, pole, RA.last_Rf0.inverted() @ q), j, roll)
    set_pose(sc, IDLE)
    print("key cost %.0f roll %d" % (best[0], best[3]), {k: round(v) if isinstance(v, float) else v for k, v in best[2].items()})
    return best[1]

def rel_angle(a, b): return quat_angle(a.rotation_difference(b))

def frame_from(pos, z_dir, y_dir, scale):
    z = Vector(z_dir).normalized(); y = (Vector(y_dir) - z * Vector(y_dir).dot(z)).normalized(); x = y.cross(z)
    return Matrix.Translation(pos) @ Matrix((x, y, z)).transposed().to_4x4() @ Matrix.Scale(scale, 4)
scale_sw = S1.to_scale()[0]

kR_clear = solve_key(SR + Vector(P['clear_off']), P['clear_dir'], kR_pull)
kR_up = solve_key(SR + Vector(P['up_off']), P['up_dir'], kR_clear)
kR_over = solve_key(SR + Vector(P['over_off']), P['over_dir'], kR_up)
kR_armed = fk_state(RA, ARMED, SR + Vector((-0.35, -0.10, -0.35)))
kR_idle = fk_state(RA, IDLE, SR + Vector((-0.45, 0.10, -0.25)))
kR_side = (kR_grab[0] + Vector((-0.16, -0.06, -0.12)), SR + Vector((-0.45, 0.15, -0.05)), kR_grab[2])
trackR = ArmTrack([(0, *kR_idle), (P['grab_R'] - 4, *kR_side), (P['grab_R'], *kR_grab), (P['pull_R'], *kR_pull),
                   (P['clear_R'], *kR_clear), (P['up_R'], *kR_up), (P['over_R'], *kR_over), (P['armed_R'], *kR_armed)])

# left: hold the shield's outer edge (searched for the least strained wrist), then arm-space keys; the shield rides the hand
SH0 = sc.mount_world('ShieldBack')
def rot_about(M, pivot, axis, deg):
    return Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(deg), 4, Vector(axis)) @ Matrix.Translation(-pivot) @ M
c0 = SH0.to_translation()
# shield keys: lifted off the back and swung open; upright beside the left flank facing out
SH_out = rot_about(Matrix.Translation(Vector(P['out_t'])) @ SH0, c0 + Vector(P['out_t']), (0, 0, 1), P['out_yaw'])
SH_side = rot_about(Matrix.Translation(Vector(P['side_t'])) @ SH0, c0 + Vector(P['side_t']), (0, 0, 1), P['side_yaw'])
set_pose(sc, ARMED); sc.place(); SH_ARMED = sc.mount_world('ShieldHand').copy(); set_pose(sc, IDLE)
SH_front = Matrix.Translation(Vector(P['front_lift'])) @ SH_ARMED @ Matrix.Scale(SH0.to_scale()[0] / SH_ARMED.to_scale()[0], 4)  # guard turn, raised so the held edge is in reach

def hold_candidates():
    sh = sc.items['shield']; V = [SH0 @ v.co for v in sh.data.vertices]
    zc = sum(v.z for v in V) / len(V)
    edge = max((v for v in V if abs(v.z - (zc - 0.04)) < 0.05), key=lambda v: v.x)
    fdir0 = (sc.bone_rest_world('Finger_01').to_translation() - REST_HL.to_translation()); reach = fdir0.length * 0.55
    out = []
    for yaw in range(0, 360, 30):
        for pitch in (-45, -15, 15, 45):
            for roll in range(0, 360, 45):
                q = Euler((math.radians(pitch), math.radians(roll), math.radians(yaw))).to_quaternion()
                fd = (q @ fdir0).normalized()
                if fd.x > -0.2: continue  # fingers wrap in over the edge
                wrist = edge - fd * reach + Vector((0.025, 0, 0))
                H = Matrix.Translation(wrist) @ q.to_matrix().to_4x4() @ Matrix.Translation(-REST_HL.to_translation()) @ REST_HL
                out.append(SH0.inverted() @ H)
    return out

def best_pole(H, prev_pole=None):
    w = H.to_translation(); q = (H.to_3x3() @ REST_HL.to_3x3().inverted()).to_quaternion()
    if (w - SL).length > LA.a + LA.b - 0.01: return None
    best = None
    for az in range(0, 360, 30):
        for el in (-45, -15, 15, 45):
            d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
            pole = SL + d * 0.5
            set_pose(sc, IDLE); LA.solve(w, q, pole); j = LA.joints()
            c = j['wrist_bend'] + 0.6 * j['wrist_twist'] + 0.4 * j['fore_twist'] + 300 * cap0.arm_hits('L')[0]
            if prev_pole is not None: c += 40 * (pole - prev_pole).length
            if best is None or c < best[0]: best = (c, pole, LA.last_Rf0.inverted() @ q, j)
    return best

_, off_sh = sc.mounts['ShieldHand']
grip_hs = (SH0 @ Matrix.Scale(1, 4)).inverted() @ (SH0 @ off_sh.inverted())   # hand in shield space when gripping
kL_armed_state = fk_state(LA, ARMED)
cands = hold_candidates(); results = []
for hs in cands:
    tot = P['hold_like_grip'] * quat_angle(hs.to_quaternion().rotation_difference(grip_hs.to_quaternion()))
    states = []; prev = None; ok = True
    for M in (SH0, SH_out, SH_side, SH_front):
        H = M @ hs; b = best_pole(H, prev)
        if b is None: ok = False; break
        tot += b[0]; prev = b[1]; states.append((H.to_translation(), b[1], b[2], b[3]))
    if ok:
        tot += 80 * (states[-1][1] - kL_armed_state[1]).length + 0.8 * quat_angle(states[-1][2].rotation_difference(kL_armed_state[2]))
        results.append((tot, hs, states))
results.sort(key=lambda r: r[0])
tot, hand_in_shield_inv, statesL = results[0]
hand_in_shield = hand_in_shield_inv  # hand pose in shield space
print("left hold total cost %.0f over %d candidates" % (tot, len(results)), [{k: round(v) if isinstance(v, float) else v for k, v in s[3].items()} for s in statesL])
set_pose(sc, IDLE)
kL_grab, kL_out, kL_side, kL_front = [(s[0], s[1], s[2]) for s in statesL]
kL_idle = fk_state(LA, IDLE)
kL_reach = (kL_grab[0] + Vector((0.10, -0.06, 0.02)), kL_grab[1], kL_grab[2])
kL_armed = fk_state(LA, ARMED)
trackL = ArmTrack([(0, *kL_idle), (P['grab_L'] - 3, *kL_reach), (P['grab_L'], *kL_grab), (P['out_L'], *kL_out),
                   (P['side_L'], *kL_side), (P['front_L'], *kL_front)])

# ------------------------------------------------------------------------------------------------ per frame
def blend_bones(fkA, fkB, s, bones):
    for n in bones:
        sc.rig.pose.bones[n].rotation_quaternion = fkA[n][1].slerp(fkB[n][1], s)
    bpy.context.view_layer.update()

def apply_arm(arm, track, f, w_ik, shrug_deg=0.0):
    names = SIDE[arm.side]
    fkd = {n: arm.world_delta(n) for n in names}
    wrist, pole, rel = track.at(f)
    shrug = None
    if shrug_deg:
        ax = Vector((0, 1, 0)) if arm.side == 'R' else Vector((0, -1, 0))
        shrug = Quaternion(ax, math.radians(shrug_deg)) @ fkd[names[0]]
    arm.solve(wrist, None, pole, shrug=shrug, rel=rel)
    if w_ik < 0.999:
        ikd = {n: arm.world_delta(n) for n in names}
        for n in names: arm.set_world_delta(n, fkd[n].slerp(ikd[n], w_ik))
    return wrist

shield_hs_seat = None
def pose_at(f):
    set_pose(sc, IDLE)
    left_arm = {n for n in UB if n in SIDE['L']}
    blend_bones(IDLE, ARMED, ease(f, P['armed_R'] - 4, P['end']), UB - FING_R - FING_L - left_arm)
    blend_bones(IDLE, ARMED, ease(f, P['front_L'] - 3, P['armed_L']), left_arm)
    blend_bones(IDLE, ARMED, ease(f, P['grab_R'] - 3, P['grab_R']), FING_R)
    blend_bones(IDLE, ARMED, ease(f, P['grab_L'] - 3, P['grab_L']), FING_L)
    wR = 1 - ease(f, P['armed_R'], P['end'])
    wrist = trackR.at(f)[0]
    shrug = smooth((wrist.z - SR.z - 0.20) / 0.30) * 15.0
    apply_arm(RA, trackR, f, wR, shrug)
    apply_arm(LA, trackL, f, 1 - ease(f, P['front_L'], P['armed_L'] + 1))
    sc.holder['sword'] = 'SwordHand' if f >= P['grab_R'] else 'Sheath'
    sc.holder['shield'] = 'ShieldBack'
    sc.place()
    if f >= P['grab_L']:
        H = sc.bone_world('Hand_L')
        carried = H @ hand_in_shield.inverted()  # rigid on the edge hold
        if f > P['front_L']:
            # PlayerEquipment-style settle into the grip: offset in the hand socket eases to identity
            hs = sc.mount_world('ShieldHand')
            o0 = hs_side.inverted() @ shield_side
            k = ease(f, P['front_L'], P['armed_L'])
            o = Matrix.Translation(o0.to_translation().lerp(Vector(), k)) @ o0.to_quaternion().slerp(Quaternion(), k).to_matrix().to_4x4()
            sc.items['shield'].matrix_world = hs @ o @ Matrix.Scale(hs.to_scale()[0] ** -1 * SH0.to_scale()[0], 4) if False else hs @ o
        else:
            sc.items['shield'].matrix_world = carried
    bpy.context.view_layer.update()

# the hand socket and shield at the start of the settle
pose_at(P['front_L']); hs_side = sc.mount_world('ShieldHand'); shield_side = sc.items['shield'].matrix_world.copy()

cap = Capsules(sc)
POSES_J = json.load(open('/mnt/user-data/uploads/Kneel-Game/Temp/kael_poses.json'))
BIND = {n: umat(m) for n, m in POSES_J['bind'].items()}
def unity_frame():
    out = {}
    for n, Bm in BIND.items():
        D = sc.bone_world(n) @ sc.bone_rest_world(n).inverted()
        M = CI @ D @ C @ Bm
        t = M.to_translation(); q = M.to_quaternion()
        out[n] = [round(t.x, 6), round(t.y, 6), round(t.z, 6), round(q.x, 7), round(q.y, 7), round(q.z, 7), round(q.w, 7)]
    return out

def run(name, fmap, nframes, render_prefix):
    rep = []; frames = []
    for g in range(nframes + 1):
        f = fmap(g)
        pose_at(f)
        sw = [sc.items['sword'].matrix_world @ v.co for v in sc.items['sword'].data.vertices][::2]
        sh = [sc.items['shield'].matrix_world @ v.co for v in sc.items['shield'].data.vertices][::3]
        rep.append(dict(f=g, R=RA.joints(), L=LA.joints(),
                 sword=cap.depth_points(sw, skip=('farm_R',)), shield=cap.depth_points(sh, skip=('farm_L',)),
                 armR=cap.arm_hits('R'), armL=cap.arm_hits('L'),
                 swsh=item_into_item(sc.items['sword'], sc.items['shield'], 3) if f >= P['grab_R'] else (0, 0),
                 tip=(sc.items['sword'].matrix_world @ Vector((0, 0, 1.06)))))
        frames.append(unity_frame())
        if RENDER and (g % 2 == 0 or g == nframes):
            render_views(f'/home/claude/anim/frames/{render_prefix}_{g:02d}', ['back34', 'front', 'side_r', 'game'], scale=2.3, res=330)
    print("=== " + name)
    for r in rep:
        t = r['tip']
        print(f"{name[0]}{r['f']:02d} R[{J(r['R'])}] L[{J(r['L'])}] sword {r['sword'][0]*100:.0f}cm shield {r['shield'][0]*100:.0f}cm armR {r['armR'][0]*100:.0f} armL {r['armL'][0]*100:.0f} sw-sh {r['swsh'][0]*100:.0f}cm")
    return frames

def J(d): return f"fl{d['flex']:.0f} ut{d['upper_twist']:.0f} ft{d['fore_twist']:.0f} wb{d['wrist_bend']:.0f} wt{d['wrist_twist']:.0f}"
D = P['end']; SHE = P.get('sheath_frames', 36)
which = [a for a in sys.argv[1:] if a in ('draw', 'sheath')] or ['draw', 'sheath']
g2f_sheath = lambda g: D * (1 - g / SHE)
f2g = lambda f: (1 - f / D) * SHE / FPS
if 'draw' in which:
    fr = run('draw', lambda g: g, D, 'd')
    json.dump({'fps': FPS, 'frames': fr, 'events': {'swordGrab': P['grab_R'] / FPS, 'shieldGrab': P['grab_L'] / FPS,
               'shieldSeat': [P['front_L'] / FPS, P['armed_L'] / FPS]}}, open('/home/claude/anim/draw_unity.json', 'w'))
if 'sheath' in which:
    fr = run('sheath', g2f_sheath, SHE, 's')
    json.dump({'fps': FPS, 'frames': fr, 'events': {'swordRelease': f2g(P['grab_R']), 'shieldRelease': f2g(P['grab_L']),
               'shieldUnseat': [f2g(P['armed_L']), f2g(P['front_L'])]}}, open('/home/claude/anim/sheath_unity.json', 'w'))
# the shield's offset in its hand socket while held by the edge (Unity socket space), for the sheathe's reverse settle
o_edge_B = off_sh.inverted() @ hand_in_shield.inverted()
o_edge_U = CI @ o_edge_B @ C
t = o_edge_U.to_translation(); q = o_edge_U.to_quaternion()
json.dump({'pos': list(t), 'rot': [q.x, q.y, q.z, q.w], 'scale': list(o_edge_U.to_scale())}, open('/home/claude/anim/shield_edge_offset.json', 'w'))
print("edge offset", t, q, o_edge_U.to_scale())
