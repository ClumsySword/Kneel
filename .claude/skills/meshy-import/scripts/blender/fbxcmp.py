# Compare two FBX files' scene structure: global settings, node transforms,
# hierarchy, skin clusters (TransformLink) and bind pose matrices.
from io_scene_fbx import parse_fbx


def _find(e, name):
    for c in e.elems:
        if c.id == name:
            return c


def _props70(e):
    p = _find(e, b'Properties70')
    out = {}
    if p is None:
        return out
    for c in p.elems:
        out[c.props[0].decode()] = c.props[4:]
    return out


def _name(c):
    return c.props[1].split(b'\x00')[0].decode()


def summarize(path):
    root, ver = parse_fbx.parse(path)
    gs = _props70(_find(root, b'GlobalSettings'))
    objs = _find(root, b'Objects')
    conns = _find(root, b'Connections')
    uid = {}
    models = {}
    clusters = {}
    bindpose = {}
    geom = {}
    for c in objs.elems:
        uid[c.props[0]] = c
        if c.id == b'Model':
            pr = _props70(c)
            models[_name(c)] = dict(type=c.props[2].decode(), **{k: list(pr[k]) for k in pr if k.startswith('Lcl') or k in ('PreRotation', 'PostRotation', 'RotationOrder', 'InheritType', 'RotationActive', 'GeometricTranslation', 'GeometricRotation', 'GeometricScaling')})
        elif c.id == b'Geometry':
            v = _find(c, b'Vertices')
            pv = _find(c, b'PolygonVertexIndex')
            geom[_name(c)] = dict(verts=len(v.props[0]) // 3, polyidx=len(pv.props[0]), layers=[e.id.decode() for e in c.elems if e.id.startswith(b'Layer')])
        elif c.id == b'Pose':
            for pn in c.elems:
                if pn.id == b'PoseNode':
                    bindpose[_find(pn, b'Node').props[0]] = list(_find(pn, b'Matrix').props[0])
    parent = {}
    for cc in conns.elems:
        if cc.props[0] == b'OO':
            a, b = cc.props[1], cc.props[2]
            if a in uid and uid[a].id == b'Model':
                parent[_name(uid[a])] = _name(uid[b]) if b in uid else '<root>'
            if a in uid and uid[a].id == b'Model' and b in uid and uid[b].id == b'Deformer':
                cl = uid[b]
                clusters[_name(uid[a])] = dict(T=list(_find(cl, b'Transform').props[0]), TL=list(_find(cl, b'TransformLink').props[0]), n=len(_find(cl, b'Indexes').props[0]) if _find(cl, b'Indexes') is not None else 0)
    bp = {_name(uid[k]): v for k, v in bindpose.items() if k in uid}
    return dict(ver=ver, gs={k: list(v) for k, v in gs.items()}, models=models, parent=parent, clusters=clusters, bindpose=bp, geom=geom)


def _maxdiff(a, b):
    if len(a) != len(b):
        return float('inf')
    d = 0.0
    for x, y in zip(a, b):
        if isinstance(x, (int, float)) and isinstance(y, (int, float)):
            d = max(d, abs(x - y))
        elif x != y:
            return float('inf')
    return d


def compare(a_path, b_path, tol=1e-4, skip_geom=False):
    A, B = summarize(a_path), summarize(b_path)
    report = []
    if A['ver'] != B['ver']:
        report.append('version %s vs %s' % (A['ver'], B['ver']))
    for k in sorted(set(A['gs']) | set(B['gs'])):
        if k in ('TimeSpanStop', 'TimeSpanStart'):
            continue
        if A['gs'].get(k) != B['gs'].get(k):
            report.append('gs %s: %s vs %s' % (k, A['gs'].get(k), B['gs'].get(k)))
    for k in sorted(set(A['models']) | set(B['models'])):
        ma, mb = A['models'].get(k), B['models'].get(k)
        if ma is None or mb is None:
            report.append('model %s only in %s' % (k, 'A' if mb is None else 'B'))
            continue
        for p in sorted(set(ma) | set(mb)):
            va, vb = ma.get(p), mb.get(p)
            if p == 'type':
                if va != vb:
                    report.append('model %s type %s vs %s' % (k, va, vb))
                continue
            # absent Lcl means default; treat missing as zero/one
            dflt = [1.0, 1.0, 1.0] if p == 'Lcl Scaling' else [0.0, 0.0, 0.0] if p.startswith('Lcl') or p.startswith('Geometric') else None
            va = va if va is not None else dflt
            vb = vb if vb is not None else dflt
            if va is None or vb is None or _maxdiff(va, vb) > (tol * 100 if 'Rotation' in p else tol):
                report.append('model %s %s: %s vs %s' % (k, p, va, vb))
        if A['parent'].get(k) != B['parent'].get(k):
            report.append('parent %s: %s vs %s' % (k, A['parent'].get(k), B['parent'].get(k)))
    for k in sorted(set(A['clusters']) | set(B['clusters'])):
        ca, cb = A['clusters'].get(k), B['clusters'].get(k)
        if ca is None or cb is None:
            report.append('cluster %s only in %s' % (k, 'A' if cb is None else 'B'))
            continue
        for p in ('T', 'TL'):
            if _maxdiff(ca[p], cb[p]) > tol:
                report.append('cluster %s %s maxdiff %.6g' % (k, p, _maxdiff(ca[p], cb[p])))
    for k in sorted(set(A['bindpose']) | set(B['bindpose'])):
        pa, pb = A['bindpose'].get(k), B['bindpose'].get(k)
        if pa is None or pb is None:
            report.append('bindpose %s only in %s' % (k, 'A' if pb is None else 'B'))
        elif _maxdiff(pa, pb) > tol:
            report.append('bindpose %s maxdiff %.6g' % (k, _maxdiff(pa, pb)))
    if not skip_geom and A['geom'] != B['geom']:
        report.append('geom %s vs %s' % (A['geom'], B['geom']))
    return report, A, B
