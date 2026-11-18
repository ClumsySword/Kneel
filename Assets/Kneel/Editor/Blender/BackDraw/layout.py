import sys, math; sys.path.insert(0,'/home/claude/anim')
from kscene import *
import numpy as np
# --- layout parameters (Blender world, rest pose; character faces -Y, left = +X) ---
MOUTH = Vector((-0.17, 0.0, 1.50))   # x,z of the scabbard mouth; y found from the back surface
TILT = 32.0                          # degrees from vertical, tip toward the left hip
GAP = 0.012                          # scabbard clearance off the back/cape
SHIELD_OUT = float(sys.argv[1]) if len(sys.argv)>1 else 0.09

def build(sc):
    P=np.array([tuple(p) for p in sc.body_mesh_world()])
    def back_y(x,z,r=0.05):
        m=(np.abs(P[:,0]-x)<r)&(np.abs(P[:,2]-z)<r)
        return P[m,1].max() if m.any() else 0.0
    d=Vector((math.sin(math.radians(TILT)),0,-math.cos(math.radians(TILT))))
    # sample the back along the line; scabbard half-thickness 0.04 + gap above the highest point within its width
    ys=[]
    for s in np.linspace(-0.05,1.15,25):
        p=MOUTH+d*s; ys.append(max(back_y(p.x,p.z,0.07),0.0))
    # fit a straight line y(s) that stays above every sample (lift each end to clear)
    S=np.linspace(-0.05,1.15,25); Y=np.array(ys)+0.04+GAP
    # choose slope from endpoints' upper envelope
    a=np.polyfit(S,Y,1); off=(Y-(a[0]*S+a[1])).max(); a[1]+=off
    y0=a[1]; slope=a[0]
    Z=Vector((d.x, slope, d.z)).normalized()          # into the scabbard (sword +Y in Unity)
    Yl=Vector((0,1,0)); Yl=(Yl-Z*Yl.dot(Z)).normalized() # thin axis: out of the back
    X=Yl.cross(Z)
    R=Matrix((X,Yl,Z)).transposed()
    origin=Vector((MOUTH.x,y0,MOUTH.z))-Z*0.05          # grip centre: guard (≈5 cm) above the mouth
    old=sc.sock_world['Sheath']; scale=old.to_scale()[0]
    new=Matrix.Translation(origin)@R.to_4x4()@Matrix.Scale(scale,4)
    return new, ys, (y0,slope)

if __name__=='__main__':
    sc=Scene(); colour_items(sc); sc.place()
    new,ys,line=build(sc)
    old=sc.sock_world['Sheath']; scab_old=sc.sock_world['Scabbard']
    sc.set_mount('Sheath','Spine_03',new)
    sc.set_mount('Scabbard','Spine_03',new@old.inverted()@scab_old)
    sb=sc.sock_world['ShieldBack']; out=(sb.to_3x3()@Vector((0,-1,0))).normalized()
    sc.set_mount('ShieldBack','Spine_03',Matrix.Translation(out*SHIELD_OUT)@sb)
    sc.place()
    P=np.array([tuple(p) for p in sc.body_mesh_world()])
    def pen(ob):
        V=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices]); worst=0
        for v in V[::2]:
            m=(np.abs(P[:,0]-v[0])<0.03)&(np.abs(P[:,2]-v[2])<0.03)
            if m.any(): worst=max(worst,P[m,1].max()-v[1])
        return worst
    print("line y0,slope",[round(x,3) for x in line])
    print("scabbard back-penetration %.3f, sword %.3f"%(pen(sc.items['scabbard']),pen(sc.items['sword'])))
    # shield vs scabbard: shield verts within scabbard footprint that are inside (less y than scabbard top)
    Sc=np.array([tuple(sc.items['scabbard'].matrix_world@v.co) for v in sc.items['scabbard'].data.vertices])
    Sh=np.array([tuple(sc.items['shield'].matrix_world@v.co) for v in sc.items['shield'].data.vertices])
    worst=0;cnt=0
    loc=np.array([tuple(v.co) for v in sc.items['shield'].data.vertices]); Sh=Sh[loc[:,1]<-0.055]
    for v in Sh:
        m=(np.abs(Sc[:,0]-v[0])<0.03)&(np.abs(Sc[:,2]-v[2])<0.03)
        if m.any():
            d=Sc[m,1].max()-v[1]
            if d>0: cnt+=1; worst=max(worst,d)
    print("shield BOARD verts under scabbard top: %d, worst %.3f"%(cnt,worst))
    hilt=np.array([tuple(sc.items['sword'].matrix_world@v.co) for v in sc.items['sword'].data.vertices])
    top=hilt[hilt[:,2].argmax()]; print("pommel at",top.round(3))
    near=np.linalg.norm(P-top,axis=1).min(); print("pommel to body min dist %.3f"%near)
    import json; json.dump({'sheath':[list(r) for r in new],'shield_out':SHIELD_OUT},open('/home/claude/anim/layout.json','w'))
    render_views('/home/claude/anim/layout',['back','back34','side_r','game'],scale=2.0)
