using Kneel.Core;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools.Transitions
{
    // What each side of a seam shows of the other (Kneel > Transitions > Glimpses). Close up: the other level's own
    // pieces, copied where its start (or end) actually stands, mapped through the seam so the road runs on. Far off:
    // the other level's landmarks as flat silhouettes (Kneel/Silhouette) in the direction of travel. Everything lands
    // in "_Transition/Glimpse", in named groups each builder rebuilds whole (a level with a glimpse both ways keeps
    // both: neither builder clears the other's); nothing else in the scene changes. Build a level's
    // seam (SeamBuilders) before the glimpses that copy it: they copy it as it stands.
    public static class SeamGlimpses
    {
        private const string L1 = SeamBuilders.L1;
        private const string L2 = SeamBuilders.L2;
        private const string L3 = SeamBuilders.L3;
        private const string L4 = SeamBuilders.L4;
        private const string L5 = SeamBuilders.L5;

        // Seam frames: where the road crosses from one level into the next, and which way it runs, on each side.
        // L2 -> L3: L2's road, 20 m past its exit (the far silhouettes' frame); the mouth of L3's sunken lane, which the
        // near copy stands on L2's crest (S2Crest), past the top of the road.
        private static readonly Vector3 S2From = new Vector3(8.8f, 2.5f, 166f);
        private static readonly Vector3 S2To = new Vector3(64f, 0f, -0.5f);
        private static readonly Vector3 S2Crest = new Vector3(8.8f, 6.45f, 184f);
        // L3 -> L4: the causeway's north end; the south end of L4's walled farm track (its field gate).
        private static readonly Vector3 S3From = new Vector3(186f, 0f, 401f);
        private static readonly Vector3 S3To = new Vector3(60f, 0f, -121f);

        // ---------------------------------------------------------------- Seam 1

        // L1's road runs on north into the west end of L2's Village 1 as it now stands (SeamBuilders.L2Arrival): the
        // road (L2Answer's) between the burning tall house on the left and the square's houses ahead, its trees and
        // dead. The copy starts where L1's own road ends (L2 x -116, L1 z 392).
        [MenuItem("Kneel/Transitions/Glimpses/L1 (toward L2)")]
        public static void L1Glimpse()
        {
            SeamKit.Open(L1);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", true);
            Matrix4x4 map = SeamCopy.Map(SeamBuilders.Seam1L2, 90f, SeamBuilders.Seam1L1, 0f);

            Transform near = SeamKit.Group(g, "Near_L2Village1", true);
            var box = new Bounds(new Vector3(-104f, 4f, -58f), new Vector3(36f, 14f, 40f));
            int n = SeamCopy.Copy(L2, new[]
            {
                "L2_HollowVillage/SetDressing/Buildings", "L2_HollowVillage/SetDressing/Trees", "L2_HollowVillage/SetDressing/TreesExtra",
                "L2_HollowVillage/SetDressing/Dead", "L2_HollowVillage/SetDressing/Skeletons", "L2_HollowVillage/SetDressing/LivedIn",
                "L2_HollowVillage/SetDressing/SouthYards", "L2_HollowVillage/Background/Village", "L2_HollowVillage/Background/DeadTrees",
                "_Transition/Glimpse/Verge",
            }, box, map, near, null, false, t => t.position.x < -116.5f);

            // Far: the rest of the village and the collapsed church as silhouettes.
            Transform far = SeamKit.Group(g, "Far_L2Silhouettes", true);
            Material sil = SeamCopy.SilhouetteMaterial("Silhouette_L1_TowardL2", new Color(0.045f, 0.045f, 0.055f), 0.22f, new Color(0.11f, 0.12f, 0.15f), 1.5f, -1.5f);
            var farBox = new Bounds(new Vector3(-20f, 6f, -10f), new Vector3(140f, 30f, 110f));
            int f = SeamCopy.Copy(L2, new[] { "L2_HollowVillage/SetDressing/Buildings", "L2_HollowVillage/Landmarks/Church" }, farBox, map, far, sil, false,
                t => box.Contains(new Vector3(t.position.x, 4f, t.position.z)));
            SeamCopy.Combine(far, "L2Silhouettes");
            SeamKit.Save();
            Debug.Log($"L1 glimpse: {n} near pieces, {f} silhouettes");
        }

        // L2's start answers L1's end: the road he walked out of L1 comes in from the west, past the same leaning road
        // sign and broken fence, the burning house he saw at the road's end now on his left (L2's night palette).
        [MenuItem("Kneel/Transitions/Glimpses/L2 (back toward L1)")]
        public static void L2Answer()
        {
            SeamKit.Open(L2);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            Material night = SeamBuilders.FindMaterial("L2_Adventure_Night");
            Material charred = SeamBuilders.FindMaterial("L2_Knights_Charred");
            // The road itself is painted into L2's ground (SeamBuilders.RoadsL2, both of L2's roads in one pass).
            SeamKit.Group(g, "Road", true);
            string pieces = SeamRoads.Paint(SeamBuilders.RoadsL2, 23);

            Transform verge = SeamKit.Group(g, "Verge", true);
            const string A = "Assets/SyntyStudios/PolygonAdventure/Prefabs/";
            GameObject[] props =
            {
                SeamKit.Place(A + "Props/SM_Prop_RoadSign_01.prefab", verge, SeamKit.Ground(new Vector3(-106.2f, 0f, -60.9f)), new Vector3(0f, 196f, 7f)),
                SeamKit.Place(A + "Buildings/SM_Bld_Fence_02.prefab", verge, SeamKit.Ground(new Vector3(-113.6f, 0f, -61f)), new Vector3(0f, 182f, 12f)),
                SeamKit.Place(A + "Buildings/SM_Bld_Fence_01.prefab", verge, SeamKit.Ground(new Vector3(-118.8f, 0f, -61.1f)), new Vector3(0f, 176f, -6f)),
                SeamKit.Place(A + "Buildings/SM_Bld_FencePost_01.prefab", verge, SeamKit.Ground(new Vector3(-110.9f, 0f, -61.2f)), new Vector3(10f, 30f, 0f)),
                SeamKit.Place(A + "Buildings/SM_Bld_Fence_02.prefab", verge, SeamKit.Ground(new Vector3(-121.5f, 0f, -55.3f)), new Vector3(0f, 4f, -10f)),
                SeamKit.Place(A + "Buildings/SM_Bld_FencePost_01.prefab", verge, SeamKit.Ground(new Vector3(-117.2f, 0f, -55.4f)), new Vector3(-8f, 64f, 0f)),
            };
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] != null)
                {
                    SeamKit.SetMaterial(props[i], i == 0 ? night : (charred != null ? charred : night));
                    SeamKit.SetLayer(props[i], "Obstacles");
                }
            }

            SeamKit.Save();
            Debug.Log("L2 answer: " + pieces);
        }

        // ---------------------------------------------------------------- Seam 2

        // L2's road climbs to the crest north of the fields; on it begins L3's sunken lane, its low stone walls, its
        // floor and the fields either side (L3's own pieces, at the crest's height), and beyond, L3's windmill on its
        // mound and L4's stronghold on the horizon as silhouettes.
        [MenuItem("Kneel/Transitions/Glimpses/L2 (toward L3)")]
        public static void L2Glimpse()
        {
            SeamKit.Open(L2);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            Matrix4x4 map = SeamCopy.Map(S2To, 0f, S2Crest, 0f);

            Transform near = SeamKit.Group(g, "Near_L3Lane", true);
            var box = new Bounds(new Vector3(64f, 2f, 30f), new Vector3(60f, 14f, 61f));
            int n = SeamCopy.Copy(L3, new[] { "L3_Root/Boundaries/S", "L3_Root/Boundaries/A0", "L3_Root/Floors/S", "L3_Root/Floors/A0", "L3_Root/Dressing", "L3_Root/Props/A0", "L3_Root/Props/S" }, box, map, near, null, true);
            Transform gate = SeamKit.Group(g, "Near_L3MouthGate", true);
            SeamCopy.Copy(L3, new[] { "_Transition/MouthGate" }, new Bounds(), map, gate);
            foreach (Transform leaf in gate)
            {
                bool west = leaf.position.x < S2Crest.x;
                leaf.RotateAround(map.MultiplyPoint3x4(new Vector3(west ? 62.05f : 65.95f, 0f, -0.59f)), Vector3.up, west ? -100f : 100f);
            }

            // Far: L3's windmill on its mound and, beyond, L4's stronghold on the ridge (L3 decision 12).
            Transform far = SeamKit.Group(g, "Far_Silhouettes", true);
            Material sil = SeamCopy.SilhouetteMaterial("Silhouette_L2_TowardL3", new Color(0.03f, 0.035f, 0.05f), 0.2f, new Color(0.06f, 0.08f, 0.11f), 2f, -2f);
            int f = SeamCopy.Copy(L3, new[] { "L3_Root/Props/E5/Windmill" }, new Bounds(), SeamCopy.Map(new Vector3(174f, 0f, 346f), 0f, S2From + new Vector3(-26f, -2.5f, 105f), -18f, 1.25f), far, sil);
            f += SeamCopy.Copy(L4, new[] { "L4_Root/L4L5_Threshold/Citadel" }, new Bounds(), SeamCopy.Map(new Vector3(56f, 0f, 300f), 0f, S2From + new Vector3(18f, -6f, 260f), 0f, 0.7f), far, sil);
            SeamCopy.Combine(far, "FarSilhouettes");
            SeamKit.Save();
            Debug.Log($"L2 glimpse: {n} near pieces, {f} silhouettes");
        }

        // L3's start answers L2's end. L3 had no ground south of its lane's closing wall, so its own lane, banks and
        // backdrop fields are mirrored south of the mouth: the sunken lane runs on toward L2, and in it stand L2's last
        // fence posts and field rows, where L2's road ends.
        [MenuItem("Kneel/Transitions/Glimpses/L3 (back toward L2)")]
        public static void L3Answer()
        {
            SeamKit.Open(L3);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            // The lane carried on south of its mouth: its banks, shifted one whole lane length (34 m) so no copy lies
            // over the original; and its floor, run on 8 m in its own material and layout mapping, falling to the field
            // (the floor is one ramp mesh, which a box can't trim, so it isn't copied).
            Transform lane = SeamKit.Group(g, "LaneSouth", true);
            int l = SeamCopy.Copy(L3, new[] { "L3_Root/Boundaries/S" }, new Bounds(), Matrix4x4.Translate(new Vector3(0f, 0f, -34f)), lane, null, false,
                t => t.name.StartsWith("L3_StoneWall_Low") || (t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().bounds.min.z > 30f));
            LaneFloorSouth(lane);
            Transform ground = SeamKit.Group(g, "FieldsSouth", true);
            int f = SeamCopy.Copy(L3, new[] { "L3_Root/Floors/_Backdrop" }, new Bounds(), Matrix4x4.Translate(new Vector3(0f, 0f, -8f)) * Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.Translate(new Vector3(0f, 0f, 8f)), ground);
            Transform l2 = SeamKit.Group(g, "Near_L2RoadEnd", true);
            Matrix4x4 map = SeamCopy.Map(S2From, 0f, S2To, 0f);
            var box = new Bounds(new Vector3(8.8f, 3f, 156f), new Vector3(16f, 8f, 14f));
            int n = SeamCopy.Copy(L2, new[] { "L2_HollowVillage/SetDressing/EdgeWalls", "L2_HollowVillage/SetDressing/Farmland" }, box, map, l2, null, true);
            SeamKit.Save();
            Debug.Log($"L3 answer: {l} lane pieces, {f} ground, {n} L2 pieces");
        }

        // L3's lane floor (F01, one ramp mesh in L3_Ground whose layout UVs are planar in x and z) carried on 8 m south
        // of the mouth, falling SeamBuilders.MouthDrop to the field, in the same material and the same UV mapping, so
        // its ground runs on unbroken.
        private static void LaneFloorSouth(Transform parent)
        {
            GameObject floor = SeamKit.Find("L3_Root/Floors/S/F01 Sunken lane (ramp)");
            if (floor == null)
            {
                return;
            }

            // Fit u(x) and v(z) from the floor's own vertices.
            Mesh src = floor.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] fv = src.vertices;
            Vector2[] fuv = src.uv;
            int lo = 0, hi = 0, left = 0, rightI = 0;
            for (int i = 0; i < fv.Length; i++)
            {
                Vector3 w = floor.transform.TransformPoint(fv[i]);
                if (w.z < floor.transform.TransformPoint(fv[lo]).z) lo = i;
                if (w.z > floor.transform.TransformPoint(fv[hi]).z) hi = i;
                if (w.x < floor.transform.TransformPoint(fv[left]).x) left = i;
                if (w.x > floor.transform.TransformPoint(fv[rightI]).x) rightI = i;
            }

            Vector3 wl = floor.transform.TransformPoint(fv[left]), wr = floor.transform.TransformPoint(fv[rightI]);
            Vector3 wlo = floor.transform.TransformPoint(fv[lo]), whi = floor.transform.TransformPoint(fv[hi]);
            float du = (fuv[rightI].x - fuv[left].x) / (wr.x - wl.x), dv = (fuv[hi].y - fuv[lo].y) / (whi.z - wlo.z);
            float u0 = fuv[left].x - du * wl.x, v0 = fuv[lo].y - dv * wlo.z;

            const int nx = 8, nz = 16;
            float x0 = 60f, x1 = 68f, z0 = -8f, z1 = 0.02f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int j = 0; j <= nz; j++)
            {
                float z = Mathf.Lerp(z0, z1, j / (float)nz);
                float y = -SeamBuilders.MouthDrop * Mathf.Clamp01(-z / 8f);
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)nx);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(u0 + du * x, v0 + dv * z));
                }
            }

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + nx + 1;
                    tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            var mesh = new Mesh { name = "L3_LaneFloorSouth" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = SeamBuilders.MeshFolder + "/L3_LaneFloorSouth.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("LaneFloorSouth");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = floor.GetComponent<Renderer>().sharedMaterial;
            go.layer = floor.layer;
        }

        // ---------------------------------------------------------------- Seam 3

        // The causeway runs straight into the south end of L4's walled farm track, through its field gate, standing
        // open, the hay wagon drawn up inside along the west wall; far beyond, the stronghold on its ridge.
        [MenuItem("Kneel/Transitions/Glimpses/L3 (toward L4)")]
        public static void L3Glimpse()
        {
            SeamKit.Open(L3);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            Matrix4x4 map = SeamCopy.Map(S3To, 0f, S3From, 0f);

            Transform near = SeamKit.Group(g, "Near_L4FarmTrack", true);
            var box = new Bounds(new Vector3(60f, 2f, -108f), new Vector3(22f, 8f, 26f));
            int n = SeamCopy.Copy(L4, new[] { "L4_Root/Farmland" }, box, map, near, null, true);

            // The gate's leaves, swung open (L4 keeps them shut, as built, and opens them for the arrival).
            Transform gate = SeamKit.Group(g, "Near_L4FieldGate", true);
            int leaves = SeamCopy.Copy(L4, new[] { "_Transition/FieldGate" }, new Bounds(), map, gate);
            foreach (Transform leaf in gate)
            {
                bool west = leaf.position.x < S3From.x;
                Vector3 hinge = map.MultiplyPoint3x4(new Vector3(west ? 58.05f : 61.95f, 0f, -120.5f));
                leaf.RotateAround(hinge, Vector3.up, west ? -100f : 100f);
            }

            // Far: the stronghold itself, on the ridge, which L3 never had in view (decision 12: it IS L4's fortress).
            Transform far = SeamKit.Group(g, "Far_Stronghold", true);
            Material sil = SeamCopy.SilhouetteMaterial("Silhouette_L3_TowardL4", new Color(0.07f, 0.065f, 0.07f), 0.3f, new Color(0.3f, 0.27f, 0.24f), 4f, -4f);
            int f = SeamCopy.Copy(L4, new[] { "L4_Root/L4L5_Threshold/Citadel" }, new Bounds(), SeamCopy.Map(S3To, 0f, S3From, 0f, 0.55f), far, sil);
            SeamCopy.Combine(far, "Stronghold");

            // L3's ground ended 7 m past the causeway: its backdrop fields mirrored north carry the land on to the fog,
            // and a low ridge, a shade lighter than the walls on it, carries the stronghold.
            Transform land = SeamKit.Group(g, "FieldsNorth", true);
            SeamCopy.Copy(L3, new[] { "L3_Root/Floors/_Backdrop" }, new Bounds(), Matrix4x4.Translate(new Vector3(0f, 0f, 408f)) * Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.Translate(new Vector3(0f, 0f, -408f)), land);
            Material ridge = SeamCopy.SilhouetteMaterial("Silhouette_L3_Ridge", new Color(0.11f, 0.1f, 0.1f), 0.55f, new Color(0.32f, 0.29f, 0.26f), 6f, -6f);
            SeamCopy.Ridge(SeamKit.Group(g, "Far_Ridge", true), "Ridge", new Vector3(186f, -2f, 585f), 0f, 520f, 90f, 4f, 13f, 41, ridge);
            SeamKit.Save();
            Debug.Log($"L3 glimpse: {n} near pieces, {leaves} gate leaves, {f} silhouettes");
        }

        // L4's start answers L3's end: outside the field gate, the causeway's last stretch, its kerbs and low walls,
        // and the windmill far behind.
        [MenuItem("Kneel/Transitions/Glimpses/L4 (back toward L3)")]
        public static void L4Answer()
        {
            SeamKit.Open(L4);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            Matrix4x4 map = SeamCopy.Map(S3From, 0f, S3To, 0f);
            Transform near = SeamKit.Group(g, "Near_L3Causeway", true);
            var box = new Bounds(new Vector3(186f, 1f, 386f), new Vector3(10f, 6f, 30f));
            int n = SeamCopy.Copy(L3, new[] { "L3_Root/Boundaries/CW", "L3_Root/Floors/CW" }, box, map, near, null, true);
            Transform far = SeamKit.Group(g, "Far_Windmill", true);
            Material sil = SeamCopy.SilhouetteMaterial("Silhouette_L4_TowardL3", new Color(0.08f, 0.07f, 0.07f), 0.3f, new Color(0.3f, 0.26f, 0.23f), 3f, -3f);
            int f = SeamCopy.Copy(L3, new[] { "L3_Root/Props/E5/Windmill" }, new Bounds(), map, far, sil);
            SeamCopy.Combine(far, "Windmill");
            SeamKit.Save();
            Debug.Log($"L4 answer: {n} near pieces, {f} silhouettes");
        }

        // ---------------------------------------------------------------- Seam 4

        // Through L4's throne-hall doors: the first 30 m of L5's nave, from L5 itself (L5 continues L4's coordinates,
        // 5 m lower), its sconces, candelabra and chandeliers lit, the carpet running from the doors up the nave. Held a
        // little darker than L5 itself (GlimpseTint), so the hall reads as dim against L4's noon, never as black.
        [MenuItem("Kneel/Transitions/Glimpses/L4 (toward L5)")]
        public static void L4Glimpse()
        {
            SeamKit.Open(L4);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", false);
            Transform near = SeamKit.Group(g, "Near_L5Nave", true);
            var box = new Bounds(new Vector3(54f, 6f, 285f), new Vector3(20f, 14f, 32f));
            int n = SeamCopy.Copy(L5, new[] { "L5_Root/Art/Floors", "L5_Root/Art/Walls", "L5_Root/Art/Nave", "L5_Root/Art/Carpets", "L5_Root/Art/Figures", "L5_Root/Art/Atmosphere", "L5_Root/Art/OuterDoors/Sconce", "L5_Root/Nave" }, box, Matrix4x4.Translate(new Vector3(0f, 5f + GlimpseLift, 0f)), near, null, true,
                t => OutsideDoors(t));
            near.gameObject.AddComponent<GlimpseTint>().brightness = GlimpseBrightness;
            int lights = 0;
            foreach (Light l in near.GetComponentsInChildren<Light>(true))
            {
                // L4's noon exposure lifts everything: the hall's own lights at a fraction of L5's, so it reads dim.
                l.shadows = LightShadows.None;
                l.intensity *= GlimpseLightScale;
                lights++;
            }

            SeamKit.Save();
            Debug.Log($"L4 glimpse: {n} nave pieces, {lights} lights");
        }

        // The nave's copy sits 3 cm above L5's own heights: L4's bastion floor slab (Floors/B/M06) lies at exactly the
        // nave floor's height (5 m) under the throne hall, and the two flickered.
        public const float GlimpseLift = 0.03f;

        public static float GlimpseBrightness = 0.75f;
        public static float GlimpseLightScale = 0.55f;

        // L5 pieces wholly outside its outer doors (z 269.2), by their renderers (floors and carpets sit at the origin).
        private static bool OutsideDoors(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.max.z < 269.2f : t.position.z < 269.2f;
        }

        // L5's start answers L4's end: the Kneeling Court's two door braziers and its kneelers, outside the outer doors.
        [MenuItem("Kneel/Transitions/Glimpses/L5 (back toward L4)")]
        public static void L5Answer()
        {
            SeamKit.Open(L5);
            Transform g = SeamKit.Group(SeamKit.Root(), "Glimpse", true);
            int n = SeamCopy.Copy(L4, new[] { "L4_Root/L4L5_Threshold/Props/B", "L4_Root/L4L5_Threshold/Structures/B/ThroneDoors/Brazier" }, new Bounds(), Matrix4x4.Translate(new Vector3(0f, -5f, 0f)), g);
            SeamKit.Save();
            Debug.Log($"L5 answer: {n} court pieces");
        }
    }
}
