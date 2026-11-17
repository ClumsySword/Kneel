import sys; sys.path.insert(0,'/home/claude/anim')
from kscene import *
sc=Scene()
L=json.load(open('/home/claude/anim/layout.json'))
new_B=Matrix(L['sheath']); sb_B=sc.sock_world['ShieldBack']
out=(sb_B.to_3x3()@Vector((0,-1,0))).normalized()
shield_B=Matrix.Translation(out*L['shield_out']+Vector((0,0,-L.get('shield_down',0.0))))@sb_B
def U(M): 
    m=CI@M@C; return [m[r][c] for c in range(4) for r in range(4)]  # column-major
json.dump({'sheathSocketMesh':U(new_B),'shieldBackSocketMesh':U(shield_B),'parentBone':'Spine_03'},open('/home/claude/anim/back_layout_unity.json','w'))
print("ok")
