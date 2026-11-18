using UnityEngine;

namespace Kneel.Core
{
    // One key of an authored camera move: a pose (a scene transform, so it can be placed and previewed in the
    // Scene view), the time it is reached, and an optional lens. Moves keep the game's language: top-down-ish,
    // never rolled, the camera's up always world up.
    [System.Serializable]
    public struct CameraKey
    {
        public Transform pose;

        [Tooltip("Seconds from the start of the move.")]
        public float time;

        [Tooltip("Vertical field of view; 0 keeps the gameplay camera's.")]
        public float fieldOfView;
    }

    public static class TransitionCamera
    {
        // The pose along `keys` at time t: positions on a Catmull-Rom curve through the keys, rotations eased
        // between neighbours, with the whole move eased in and out so it never starts or stops with a jolt.
        public static bool Evaluate(CameraKey[] keys, float t, out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            fieldOfView = 0f;
            if (keys == null || keys.Length == 0 || keys[0].pose == null)
            {
                return false;
            }

            if (keys.Length == 1)
            {
                position = keys[0].pose.position;
                rotation = Upright(keys[0].pose.rotation);
                fieldOfView = keys[0].fieldOfView;
                return true;
            }

            float start = keys[0].time;
            float end = keys[keys.Length - 1].time;
            float u = end > start ? Mathf.Clamp01((t - start) / (end - start)) : 1f;
            float eased = Mathf.Lerp(start, end, Mathf.SmoothStep(0f, 1f, u));

            int i = 0;
            while (i < keys.Length - 2 && eased > keys[i + 1].time)
            {
                i++;
            }

            CameraKey a = keys[i];
            CameraKey b = keys[i + 1];
            float span = Mathf.Max(b.time - a.time, 0.0001f);
            float s = Mathf.Clamp01((eased - a.time) / span);

            Vector3 p0 = keys[Mathf.Max(i - 1, 0)].pose.position;
            Vector3 p1 = a.pose.position;
            Vector3 p2 = b.pose.position;
            Vector3 p3 = keys[Mathf.Min(i + 2, keys.Length - 1)].pose.position;
            position = CatmullRom(p0, p1, p2, p3, s);
            rotation = Upright(Quaternion.Slerp(a.pose.rotation, b.pose.rotation, s));

            float fovA = a.fieldOfView;
            float fovB = b.fieldOfView;
            fieldOfView = fovA > 0f || fovB > 0f ? Mathf.Lerp(fovA > 0f ? fovA : fovB, fovB > 0f ? fovB : fovA, s) : 0f;
            return true;
        }

        public static float Duration(CameraKey[] keys) => keys != null && keys.Length > 0 ? keys[keys.Length - 1].time : 0f;

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // No roll, ever.
        private static Quaternion Upright(Quaternion q)
        {
            Vector3 forward = q * Vector3.forward;
            return forward.sqrMagnitude > 0f ? Quaternion.LookRotation(forward, Vector3.up) : q;
        }

        public static void DrawGizmos(CameraKey[] keys, Color color)
        {
#if UNITY_EDITOR
            if (keys == null)
            {
                return;
            }

            Gizmos.color = color;
            Vector3? last = null;
            foreach (CameraKey k in keys)
            {
                if (k.pose == null)
                {
                    continue;
                }

                Matrix4x4 m = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(k.pose.position, Upright(k.pose.rotation), Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, k.fieldOfView > 0f ? k.fieldOfView : 60f, 3f, 0.3f, 16f / 9f);
                Gizmos.matrix = m;
                if (last.HasValue)
                {
                    Gizmos.DrawLine(last.Value, k.pose.position);
                }

                last = k.pose.position;
            }
#endif
        }

        public static void DrawPath(Transform[] path, Vector3 from, Color color)
        {
#if UNITY_EDITOR
            if (path == null)
            {
                return;
            }

            Gizmos.color = color;
            Vector3 last = from;
            foreach (Transform p in path)
            {
                if (p == null)
                {
                    continue;
                }

                Gizmos.DrawLine(last + Vector3.up * 0.1f, p.position + Vector3.up * 0.1f);
                Gizmos.DrawWireSphere(p.position + Vector3.up * 0.1f, 0.25f);
                last = p.position;
            }
#endif
        }
    }
}
