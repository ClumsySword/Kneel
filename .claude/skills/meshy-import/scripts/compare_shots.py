"""Score and lay out in-game captures of mesh variants against a reference capture.

  python -I compare_shots.py --dir Temp/meshy_shots --prefix near --ref full --variants 30k 15k 10k \
      --box 800 370 1340 790 --scale 1 --out compare_near.png [--noise full2]

Expects <dir>/<prefix>_<tag>.png for the reference and every variant, all from the same Play session
(pose frozen, camera still). Prints how many pixels differ by more than 12/255 from the reference inside
the box. --noise names a second capture of the reference to show the run's noise floor; anything near it
is indistinguishable. Writes a labelled sheet (3 per row) of the crops, scaled by --scale.
"""
import argparse, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ap = argparse.ArgumentParser()
ap.add_argument('--dir', required=True)
ap.add_argument('--prefix', required=True)
ap.add_argument('--ref', required=True)
ap.add_argument('--variants', nargs='+', required=True)
ap.add_argument('--box', nargs=4, type=int, required=True, metavar=('X0', 'Y0', 'X1', 'Y1'))
ap.add_argument('--scale', type=int, default=1)
ap.add_argument('--noise')
ap.add_argument('--out', required=True)
ap.add_argument('--labels', nargs='*', help='display labels, same order as ref + variants')
a = ap.parse_args()

path = lambda t: os.path.join(a.dir, '%s_%s.png' % (a.prefix, t))
x0, y0, x1, y1 = a.box
load = lambda t: np.asarray(Image.open(path(t)).convert('RGB'), dtype=np.int16)[y0:y1, x0:x1]
diff = lambda p, q: int((np.abs(p - q).max(2) > 12).sum())

ref = load(a.ref)
if a.noise:
    print('noise floor (%s vs %s): %d px' % (a.ref, a.noise, diff(ref, load(a.noise))))
for t in a.variants:
    print('%s: %d px differ from %s' % (t, diff(ref, load(t)), a.ref))

tags = [a.ref] + a.variants
labels = a.labels or tags
ims = [Image.open(path(t)).convert('RGB').crop(a.box) for t in tags]
ims = [im.resize((im.width * a.scale, im.height * a.scale), Image.LANCZOS) for im in ims]
try:
    font = ImageFont.truetype('arial.ttf', 22)
except OSError:
    font = ImageFont.load_default()
w, h = ims[0].size
cols, pad, head = min(3, len(ims)), 10, 36
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * (w + pad) - pad, rows * (h + head + pad) - pad), (18, 18, 18))
draw = ImageDraw.Draw(sheet)
for i, (im, label) in enumerate(zip(ims, labels)):
    x, y = (i % cols) * (w + pad), (i // cols) * (h + head + pad)
    sheet.paste(im, (x, y + head))
    draw.text((x + 8, y + 6), label, fill=(235, 235, 235), font=font)
sheet.save(a.out)
print('sheet', a.out, sheet.size)
