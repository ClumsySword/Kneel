"""Turn a Meshy texture set into the textures a Kneel URP Lit material uses.

Meshy ships, next to <base>.fbx:
  <base>.png            base colour (sRGB)
  <base>_metallic.png   metallic (greyscale)
  <base>_roughness.png  roughness (greyscale)
  <base>_normal.png     tangent-space normal, OpenGL convention (same as Unity)

Writes into --out:
  T_<Name>_BaseColor.png          copy of the base colour
  T_<Name>_MetallicSmoothness.png RGB = metallic, A = 1 - roughness (URP's Metallic Alpha smoothness source)
  T_<Name>_Normal.png             copy of Meshy's normal map (only when --keep-normal; a low-poly mesh needs
                                  the baked one instead, which lowpoly.bake_normal writes)

Run outside the download folder: python -I pack_textures.py <download_dir>/<base> --name EmberWarden --out <dir>
"""
import argparse, os, shutil
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument('base', help='path prefix of the Meshy files, without .png')
ap.add_argument('--name', required=True)
ap.add_argument('--out', required=True)
ap.add_argument('--keep-normal', action='store_true')
a = ap.parse_args()

os.makedirs(a.out, exist_ok=True)
dst = lambda kind: os.path.join(a.out, 'T_%s_%s.png' % (a.name, kind))

shutil.copyfile(a.base + '.png', dst('BaseColor'))
met = Image.open(a.base + '_metallic.png').convert('L')
rough = Image.open(a.base + '_roughness.png').convert('L')
if rough.size != met.size:
    rough = rough.resize(met.size, Image.LANCZOS)
smooth = rough.point(lambda v: 255 - v)
Image.merge('RGBA', (met, met, met, smooth)).save(dst('MetallicSmoothness'))
if a.keep_normal:
    shutil.copyfile(a.base + '_normal.png', dst('Normal'))
for k in ('BaseColor', 'MetallicSmoothness') + (('Normal',) if a.keep_normal else ()):
    print(dst(k), Image.open(dst(k)).size)
