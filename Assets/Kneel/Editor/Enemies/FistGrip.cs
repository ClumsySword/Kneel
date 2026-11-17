using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Fits a closed fist to a round grip (owner, 2026-11-11: "check that they hold the sword correctly"). A fist closed by
    // fixed angles fits one handle thickness only: too tight and the fingers run through the grip, too loose and it
    // floats in the hand. This sets the curl from the handle instead:
    //  - the fingers' flesh is measured from the skinned body (how far the skin sits from each finger's bones);
    //  - for each curl it finds the largest round tunnel the curled fingers and palm leave along the blade;
    //  - it takes the tightest curl whose tunnel still holds the handle plus the flesh, and puts the socket's origin on
    //    the tunnel's centre line. The socket keeps its rotation, so the blade points where it did and reaches hold.
    public static class FistGrip
    {
        private static readonly string[] Fingers = { "index", "mid", "ring", "pinky" };

        public struct Fit
        {
            public float curlScale;
            public float flesh;
            public float handleRadius;
            public float tunnel;
            public Vector3 moved;
            public string Report => string.Format("curl x{0:F2}, flesh {1:F3} m, handle {2:F3} m, tunnel {3:F3} m, socket moved {4:F3} m",
                curlScale, flesh, handleRadius, tunnel, moved.magnitude);
        }

        // The handle's radius around the item's +Y axis, over the stretch the fist covers (`reach` either side of its
        // origin, in metres): the widest point, so nothing of the grip passes through a finger.
        public static float HandleRadius(Transform item, float reach)
        {
            float best = 0f;
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 toItem = item.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                float scale = item.lossyScale.y;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = toItem.MultiplyPoint3x4(vertex) * scale;
                    if (Mathf.Abs(p.y) <= reach)
                        best = Mathf.Max(best, new Vector2(p.x, p.z).magnitude);
                }
            }

            return best;
        }

        // Closes `hand` on `socket`'s +Y axis: the tightest curl (a scale on `curl`) that leaves a tunnel of handle +
        // flesh, the socket moved onto the tunnel's centre. `reach` is how far along the grip the fist spans.
        public static Fit Apply(Animator animator, GameObject model, HumanBodyBones hand, Vector3 curl, Vector2 thumb, Transform socket, float handleRadius)
        {
            Transform handBone = animator.GetBoneTransform(hand);
            EnemyRig.CloseFist(animator, model, hand, curl, thumb);
            float flesh = Flesh(animator.gameObject, handBone);
            Vector3 axis = socket.up;

            var fit = new Fit { handleRadius = handleRadius, flesh = flesh, curlScale = 1f };
            Vector3 centre = socket.position;
            float want = handleRadius + flesh;
            bool found = false;
            // From a hard fist outward: the first curl whose tunnel holds the grip is the tightest that does.
            for (float s = 1.3f; s >= 0.3f; s -= 0.02f)
            {
                EnemyRig.CloseFist(animator, model, hand, curl * s, thumb);
                float tunnel = Tunnel(handBone, axis, socket.position, out Vector3 at);
                if (tunnel >= want)
                {
                    fit.curlScale = s;
                    fit.tunnel = tunnel;
                    centre = at;
                    found = true;
                    break;
                }
            }

            if (found == false)
            {
                EnemyRig.CloseFist(animator, model, hand, curl * 0.3f, thumb);
                fit.curlScale = 0.3f;
                fit.tunnel = Tunnel(handBone, axis, socket.position, out centre);
            }

            // Onto the tunnel's centre line, keeping the socket's place along the grip.
            Vector3 offset = Vector3.ProjectOnPlane(centre - socket.position, axis);
            socket.position += offset;
            fit.moved = offset;
            return fit;
        }

        // Closes `hand` at `scale` x `curl` and centres the socket (rotation kept) on the tunnel the fingers leave.
        // Returns the tunnel's radius; `moved` is how far the socket went.
        public static float At(Animator animator, GameObject model, HumanBodyBones hand, Vector3 curl, Vector2 thumb, Transform socket, float scale, out Vector3 moved)
        {
            Transform handBone = animator.GetBoneTransform(hand);
            EnemyRig.CloseFist(animator, model, hand, curl * scale, thumb);
            float tunnel = Tunnel(handBone, socket.up, socket.position, out Vector3 centre);
            moved = Vector3.ProjectOnPlane(centre - socket.position, socket.up);
            socket.position += moved;
            return tunnel;
        }

        // How far the skin sits from a finger's bones: the median distance of the vertices each finger's middle bone
        // drives most from that bone's segment, over the four fingers.
        private static float Flesh(GameObject root, Transform handBone)
        {
            var distances = new List<float>();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Transform[] bones = skin.bones;
                var baked = new Mesh();
                skin.BakeMesh(baked, false);
                Vector3[] vertices = baked.vertices;
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                foreach (string name in Fingers)
                {
                    Transform first = FindFinger(handBone, name);
                    Transform middle = first != null && first.childCount > 0 ? first.GetChild(0) : null;
                    Transform last = middle != null && middle.childCount > 0 ? middle.GetChild(0) : null;
                    if (last == null)
                        continue;
                    int index = System.Array.IndexOf(bones, middle);
                    if (index < 0)
                        continue;
                    for (int i = 0; i < vertices.Length && i < weights.Length; i++)
                    {
                        if (weights[i].boneIndex0 != index || weights[i].weight0 < 0.6f)
                            continue;
                        Vector3 world = skin.transform.TransformPoint(vertices[i]);
                        distances.Add(SegmentDistance(world, middle.position, last.position));
                    }
                }

                Object.DestroyImmediate(baked);
            }

            if (distances.Count == 0)
                return 0.02f;
            distances.Sort();
            return distances[distances.Count / 2];
        }

        // The largest circle, in the plane across `axis`, that the fingers' segments, the palm and the thumb leave open
        // inside the fist (inside the loop the middle finger closes with the wrist). Returns its radius and centre.
        private static float Tunnel(Transform handBone, Vector3 axis, Vector3 near, out Vector3 centre)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            Vector2 Flat(Vector3 p) => new Vector2(Vector3.Dot(p - near, u), Vector3.Dot(p - near, v));

            var segments = new List<(Vector2, Vector2)>();
            List<Vector2> loop = null;
            foreach (string name in Fingers)
            {
                List<Vector3> chain = Chain(FindFinger(handBone, name));
                if (chain.Count < 2)
                    continue;
                // The palm: from the wrist to the knuckle.
                segments.Add((Flat(handBone.position), Flat(chain[0])));
                for (int i = 0; i + 1 < chain.Count; i++)
                    segments.Add((Flat(chain[i]), Flat(chain[i + 1])));
                if (name == "mid")
                {
                    loop = new List<Vector2> { Flat(handBone.position) };
                    foreach (Vector3 p in chain)
                        loop.Add(Flat(p));
                }
            }

            List<Vector3> thumb = Chain(FindFinger(handBone, "thumb"));
            for (int i = 0; i + 1 < thumb.Count; i++)
                segments.Add((Flat(thumb[i]), Flat(thumb[i + 1])));

            centre = near;
            if (loop == null)
                return 0f;

            // A coarse-to-fine search for the point farthest from every segment, inside the middle finger's loop.
            Vector2 min = loop[0], max = loop[0];
            foreach (Vector2 p in loop)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Vector2 best = (min + max) * 0.5f;
            float bestClear = -1f;
            float step = Mathf.Max(max.x - min.x, max.y - min.y) / 24f;
            Vector2 lo = min, hi = max;
            for (int pass = 0; pass < 4; pass++)
            {
                for (float x = lo.x; x <= hi.x; x += step)
                {
                    for (float y = lo.y; y <= hi.y; y += step)
                    {
                        var p = new Vector2(x, y);
                        if (Inside(loop, p) == false)
                            continue;
                        float clear = float.MaxValue;
                        foreach ((Vector2 a, Vector2 b) in segments)
                            clear = Mathf.Min(clear, SegmentDistance(p, a, b));
                        if (clear > bestClear)
                        {
                            bestClear = clear;
                            best = p;
                        }
                    }
                }

                lo = best - Vector2.one * step * 2f;
                hi = best + Vector2.one * step * 2f;
                step /= 6f;
            }

            centre = near + u * best.x + v * best.y;
            return Mathf.Max(bestClear, 0f);
        }

        // A finger's joints from its first bone, and its tip: the last bone carried on by its own length.
        private static List<Vector3> Chain(Transform first)
        {
            var chain = new List<Vector3>();
            for (Transform bone = first; bone != null; bone = bone.childCount > 0 ? bone.GetChild(0) : null)
                chain.Add(bone.position);
            if (chain.Count >= 2)
            {
                Vector3 a = chain[chain.Count - 2], b = chain[chain.Count - 1];
                chain.Add(b + (b - a) * 0.85f);
            }

            return chain;
        }

        private static bool Inside(List<Vector2> polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }

            return inside;
        }

        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-9f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // As EnemyRig's: the first bone of a finger is a child of the hand whose name holds the finger's.
        private static Transform FindFinger(Transform hand, string finger)
        {
            foreach (Transform child in hand)
            {
                if (child.name.ToLowerInvariant().Contains(finger) && child.name.StartsWith("Socket") == false)
                    return child;
            }

            return null;
        }
    }
}
