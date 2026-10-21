using System.Text;
using Kneel.EditorTools;
using UnityEngine;

namespace Kneel.Lighting.EditorTools
{
    // The end of L1: the battlefield path runs out at a leaning road sign and a broken village fence,
    // and the rooftops of the Hollow Village stand in the northern fog against a low red glow.
    // Everything tall sits beyond the path (north), never on the camera side.
    public static class L1ExitDressing
    {
        private struct Piece
        {
            public string Key;
            public Vector3 Position;
            public Vector3 Euler;
            public float Scale;
            public bool Solid;

            public Piece(string key, float x, float z, float yaw, float scale = 1f, bool solid = true, float tiltX = 0f, float tiltZ = 0f)
            {
                Key = key;
                Position = new Vector3(x, 0f, z);
                Euler = new Vector3(tiltX, yaw, tiltZ);
                Scale = scale;
                Solid = solid;
            }
        }

        // Roadside, along the last stretch of path (walkable x 0.5..7.5 up to z 377.5).
        private static readonly Piece[] Roadside =
        {
            new Piece("A/Props/SM_Prop_RoadSign_01", 8.4f, 359.5f, -30f, 1f, true, 0f, 7f),
            new Piece("K/Props/SM_Prop_Gravestone_01", -0.6f, 363.5f, 80f, 1f, true, 8f, 0f),
            new Piece("K/Props/SM_Prop_Gravestone_02", -0.9f, 366f, 100f, 0.9f, true, -6f, 4f),
            new Piece("A/Buildings/SM_Bld_Fence_01", -0.4f, 369.5f, 90f),
            new Piece("A/Buildings/SM_Bld_Fence_02", -0.2f, 373.5f, 96f, 1f, true, 0f, 12f),
            new Piece("A/Buildings/SM_Bld_Fence_01", 8.5f, 368f, 88f, 1f, true, 0f, -9f),
            new Piece("A/Buildings/SM_Bld_FencePost_01", 8.6f, 372.5f, 0f, 1f, true, 10f, 0f),
            new Piece("K/Props/SM_Prop_Lampost_01", -1f, 377f, 20f, 1f, true, 0f, -8f),
            new Piece("K/Props/SM_Prop_CartHay_01", -6f, 382f, 140f, 1f, false, 0f, 9f),
        };

        // The Hollow Village, beyond the end of the ground (on the far plane), deep in the northern fog.
        // With the camera looking west, "far" is up-screen (west): the houses sit north-west of the road's end,
        // so they recede into the fog instead of looming beside the camera.
        private static readonly Piece[] Village =
        {
            new Piece("A/Buildings/SM_Bld_Village_01", -12f, 398f, 20f, 1f, false),
            new Piece("A/Buildings/SM_Bld_Village_03", -24f, 404f, -12f, 1f, false),
            new Piece("A/Buildings/SM_Bld_Village_05", -3f, 410f, 196f, 1f, false),
            new Piece("A/Buildings/SM_Bld_Village_04", -34f, 414f, 64f, 1f, false),
            new Piece("A/Buildings/SM_Bld_Village_07", -16f, 420f, 5f, 1f, false),
        };

        public static string Build(Transform root)
        {
            var report = new StringBuilder();
            var dressing = L1Build.Group(root.Find("SetDressing"), "ExitRoad", true);
            var background = L1Build.Group(root.Find("Background"), "HollowVillage", true);
            int placed = 0, skipped = 0;

            foreach (var piece in Roadside)
            {
                if (Place(dressing, piece, L1Build.PropStatic, report))
                {
                    placed++;
                }
                else
                {
                    skipped++;
                }
            }

            foreach (var piece in Village)
            {
                if (Place(background, piece, L1Build.EnvironmentStatic, report))
                {
                    placed++;
                }
                else
                {
                    skipped++;
                }
            }

            return $"Exit: {placed} pieces placed, {skipped} skipped. {report}";
        }

        private static bool Place(Transform parent, Piece piece, UnityEditor.StaticEditorFlags flags, StringBuilder report)
        {
            var go = L1Build.Spawn(piece.Key, parent, Vector3.zero, piece.Euler, piece.Scale);
            go.transform.position = Ground(piece.Position);

            // Never taller than the camera-to-player sight line allows here.
            float height = Height(go) - go.transform.position.y;
            float cap = L1Layout.MaxPropHeight(new Vector2(piece.Position.x, piece.Position.z));
            if (height > cap)
            {
                report.Append($"{piece.Key} too tall at {piece.Position} ({height:F1} > {cap:F1}); ");
                Object.DestroyImmediate(go);
                return false;
            }

            if (!piece.Solid)
            {
                L1Build.StripColliders(go);
            }

            L1Build.SetLayer(go, "Obstacles");
            L1Build.SetStatic(go, flags);
            return true;
        }

        private static float Height(GameObject go)
        {
            float top = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, r.bounds.max.y);
            }

            return top;
        }

        private static Vector3 Ground(Vector3 p)
        {
            int ground = 1 << LayerMask.NameToLayer("Ground");
            return Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ground) ? hit.point : p;
        }
    }
}
