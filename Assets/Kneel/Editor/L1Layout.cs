using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Single source of truth for the shape of L1 "The Broken Line".
    // Walkable space is a union of circles (arenas, nooks, plazas) and capsules (corridors).
    // Everything that places or validates dressing reads the layout from here.
    public static class L1Layout
    {
        public struct Circle
        {
            public string Name;
            public Vector2 Center;
            public float Radius;
            public bool IsArena;

            public Circle(string name, float x, float z, float radius, bool isArena = false)
            {
                Name = name;
                Center = new Vector2(x, z);
                Radius = radius;
                IsArena = isArena;
            }
        }

        public struct Capsule
        {
            public Vector2 A;
            public Vector2 B;
            public float HalfWidth;

            public Capsule(float ax, float az, float bx, float bz, float halfWidth)
            {
                A = new Vector2(ax, az);
                B = new Vector2(bx, bz);
                HalfWidth = halfWidth;
            }
        }

        public static readonly Circle[] Circles =
        {
            new Circle("Start", 0f, 0f, 6.5f),
            new Circle("BannerNook", 6.5f, 15f, 2.5f),
            new Circle("E1", -3f, 40f, 8f, true),
            new Circle("E2", 5f, 85f, 8f, true),
            new Circle("RamPlaza", 0f, 130f, 13f),
            new Circle("HealPocket", 26f, 130f, 5f),
            new Circle("E3", -3f, 180f, 9f, true),
            new Circle("ShrineNook", -16f, 204f, 3.5f),       // checkpoint shrine, just past E3 (mid-route)
            new Circle("E4", 4f, 232f, 9f, true),
            new Circle("TentNook", -2f, 257f, 4f),
            new Circle("E5", 0f, 290f, 10f, true),
            new Circle("LastStand", 4f, 334f, 7f),          // the rearguard's last stand, before the exit
        };

        public static readonly Capsule[] Capsules =
        {
            new Capsule(0, 3, 3, 18, 5.5f), new Capsule(3, 18, -3, 33, 5.5f),
            new Capsule(-3, 47, -7, 58, 6f), new Capsule(-7, 58, 6, 71, 6f), new Capsule(6, 71, 5, 78, 6f),
            new Capsule(5, 92, -5, 104, 6f), new Capsule(-5, 104, 0, 118, 6f),
            new Capsule(12, 130, 22, 130, 2.5f),
            new Capsule(0, 142, 8, 155, 6f), new Capsule(8, 155, -3, 172, 6f),
            new Capsule(-3, 188, -9, 200, 6f), new Capsule(-9, 200, 2, 215, 6f), new Capsule(2, 215, 4, 224, 6f),
            new Capsule(4, 240, 12, 252, 6f), new Capsule(12, 252, 6, 266, 6f), new Capsule(6, 257, 0, 257, 2.5f),
            new Capsule(6, 266, 0, 275, 6f), new Capsule(0, 275, 0, 281, 6f),
            new Capsule(0, 299, -6, 312, 6f), new Capsule(-6, 312, 4, 327, 6f),
            new Capsule(4, 334, 4, 350, 5f), new Capsule(4, 350, 4, 374, 3.5f),
        };

        // The toppled ram splits the plaza; nothing else may be placed on it.
        public static readonly Rect RamFootprint = new Rect(-3.2f, 122.5f, 6.4f, 15f);

        // Ground mesh / layout texture extents.
        public const float GroundMinX = -52f;
        public const float GroundMinZ = -26f;
        public const float GroundWidth = 114f;
        public const float GroundLength = 412f;

        // Real gameplay camera (Assets/Player/Settings/PlayerCameraSettings.asset; L1 uses its own copy at yaw 270).
        public const float CameraPitch = 65f;
        // Set on Main Camera in L1: looking west, so the level runs left to right on screen.
        public const float CameraYaw = 270f;
        public const float CameraDistance = 8.5f;
        public const float CameraMinDistance = 3.2f;
        public const float CameraMaxDistance = 8.5f;
        public const float CameraTargetHeight = 1f;
        public const float CameraFov = 60f;
        public const float CameraMaxEdgeOffset = 10f;

        // Ground-plane direction the camera looks along (screen up).
        public static Vector2 CameraForward
        {
            get
            {
                Vector3 f = Quaternion.Euler(0f, CameraYaw, 0f) * Vector3.forward;
                return new Vector2(f.x, f.z).normalized;
            }
        }

        public static IEnumerable<Circle> Arenas
        {
            get
            {
                foreach (var circle in Circles)
                {
                    if (circle.IsArena)
                    {
                        yield return circle;
                    }
                }
            }
        }

        // Signed distance to the walkable space: negative inside, positive outside.
        public static float SignedDistance(Vector2 p)
        {
            float distance = float.MaxValue;

            foreach (var circle in Circles)
            {
                distance = Mathf.Min(distance, (p - circle.Center).magnitude - circle.Radius);
            }

            foreach (var capsule in Capsules)
            {
                distance = Mathf.Min(distance, DistanceToSegment(p, capsule.A, capsule.B) - capsule.HalfWidth);
            }

            return distance;
        }

        public static float SignedDistance(Vector3 worldPosition)
        {
            return SignedDistance(new Vector2(worldPosition.x, worldPosition.z));
        }

        // Unit vector pointing away from the walkable space.
        public static Vector2 OutwardNormal(Vector2 p)
        {
            const float e = 0.5f;
            float dx = SignedDistance(p + new Vector2(e, 0f)) - SignedDistance(p - new Vector2(e, 0f));
            float dz = SignedDistance(p + new Vector2(0f, e)) - SignedDistance(p - new Vector2(0f, e));
            return new Vector2(dx, dz).normalized;
        }

        public static bool IsInArena(Vector2 p, float padding)
        {
            foreach (var arena in Arenas)
            {
                if ((p - arena.Center).magnitude < arena.Radius + padding)
                {
                    return true;
                }
            }

            return false;
        }

        // Tallest a piece may be at p without hiding the player from the gameplay camera.
        // The camera looks down at CameraPitch along CameraForward, so anything on the camera side of
        // walkable ground must stay under the camera-to-chest line. Camera-side edges of arenas are capped harder.
        public static float MaxPropHeight(Vector2 p)
        {
            Vector2 forward = CameraForward;
            foreach (var arena in Arenas)
            {
                Vector2 offset = p - arena.Center;
                if (offset.magnitude < arena.Radius + 7f && Vector2.Dot(offset, forward) < -arena.Radius * 0.2f)
                {
                    return 1.2f;
                }
            }

            float slope = Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
            for (float d = 0.5f; d <= 14f; d += 0.5f)
            {
                if (SignedDistance(p + forward * d) < 0f)
                {
                    return CameraTargetHeight + slope * d;
                }
            }

            return float.MaxValue;
        }

        // Where the gameplay camera sits when framing a focus point.
        public static Pose CameraPose(Vector3 feet, float distance, Vector3 edgeOffset = default)
        {
            Quaternion rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            Vector3 look = feet + Vector3.up * CameraTargetHeight + edgeOffset;
            return new Pose(look - rotation * Vector3.forward * distance, rotation);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
