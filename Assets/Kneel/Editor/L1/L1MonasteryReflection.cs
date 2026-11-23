using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The sanctuary's water reflects a whole gothic hall, though the gameplay camera only ever sees the floor and the
    // up-screen walls (the roof and the camera side are cut away so nothing hides Kael). For the bake only, the
    // sanctuary is closed up: its walls mirrored onto the camera side at full height, the end walls made whole, and a
    // ribbed pointed vault laid over it. Its reflection probe renders that once into a cubemap, which the probe then
    // keeps as a custom texture (Generate Lighting leaves custom probes alone). Everything temporary is removed.
    // Needs the monastery placed in the open scene; rerun after changing the sanctuary.
    public static class L1MonasteryReflection
    {
        public const string CubemapPath = L1Monastery.L1Folder + "/Lighting/Textures/L1_Mon_SanctuaryReflection.exr";

        [MenuItem("Kneel/L1/Monastery/Bake Sanctuary Reflection")]
        public static void BakeMenu()
        {
            Debug.Log("[L1 Monastery] " + Bake());
        }

        public static string Bake()
        {
            var root = L1LevelTools.Root;
            var monastery = root != null ? root.transform.Find("Monastery") : null;
            var probe = monastery != null ? monastery.GetComponentInChildren<ReflectionProbe>(true) : null;
            if (probe == null)
            {
                return "Place the monastery first (no sanctuary reflection probe in the open scene).";
            }

            var temp = new GameObject("_ReflectionOnly");
            try
            {
                // The walls and windows mirrored across x = 0 (full height on the camera side, the stepped cross walls
                // made whole by their twins).
                foreach (var part in new[] { "L1_Mon_Sanctuary", "L1_Mon_Windows" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Monastery.PartsFolder + "/" + part + ".prefab");
                    var mirror = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp.transform);
                    PrefabUtility.UnpackPrefabInstance(mirror, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    mirror.transform.localScale = new Vector3(-1f, 1f, 1f);
                }

                var stone = L1Monastery.Mat("Stone");
                var vault = new L3Mesh();
                Vault(vault);
                L3Build.MeshChild(temp.transform, "Vault", vault.ToMesh("Vault"), Vector3.zero, stone);

                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                bool ok = Lightmapping.BakeReflectionProbe(probe, CubemapPath);
                if (!ok)
                {
                    return "Reflection bake failed.";
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            AssetDatabase.ImportAsset(CubemapPath, ImportAssetOptions.ForceUpdate);
            var cube = AssetDatabase.LoadAssetAtPath<Texture>(CubemapPath);

            // Keep it on the prefab itself, so both L1 scenes have it.
            string partPath = L1Monastery.PartsFolder + "/L1_Mon_Lighting.prefab";
            var contents = PrefabUtility.LoadPrefabContents(partPath);
            var partProbe = contents.GetComponentInChildren<ReflectionProbe>(true);
            partProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            partProbe.customBakedTexture = cube;
            PrefabUtility.SaveAsPrefabAsset(contents, partPath);
            PrefabUtility.UnloadPrefabContents(contents);

            // The scene instance follows the prefab (drop the bake's override on it).
            PrefabUtility.RevertObjectOverride(probe, InteractionMode.AutomatedAction);
            return "Sanctuary reflection baked to " + CubemapPath + ".";
        }

        // The round hall's vault: a ribbed dome over the arcade rising from the pillars' capitals to an open oculus
        // (the beam's source), a lower vault over the ambulatory from the outer wall to the capitals, and fan ribs
        // from every pillar, up the dome and across the ambulatory. Webbing dark, ribs light.
        private static void Vault(L3Mesh m)
        {
            var centre = new Vector3(L1Monastery.HallCentre.x, 0f, L1Monastery.HallCentre.y);
            float spring = L1Monastery.PillarHigh;
            float ring = L1Monastery.ArcadeRadius, outer = L1Monastery.HallApothem + 0.2f;
            const float oculus = 2.6f, crown = 23f;
            var web = L4Kit.Cell(L2PolyKit.TopDark);
            var rib = L4Kit.Cell(L2PolyKit.TopLight);
            int sides = L1Monastery.HallSides;

            // Dome profile (radius, height) from the arcade up to the oculus, and the ambulatory's from the wall
            // to the arcade, both seen from below (traversed so the lathe's outward side faces down and in).
            var dome = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                dome.Add(new Vector2(Mathf.Lerp(ring, oculus, t), spring + (crown - spring) * Mathf.Sin(t * Mathf.PI * 0.5f)));
            }

            var amb = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                amb.Add(new Vector2(Mathf.Lerp(outer, ring, t), L1Monastery.HallHigh + 3.2f * Mathf.Sin(t * Mathf.PI)));
            }

            var frame = Matrix4x4.TRS(centre, Quaternion.identity, Vector3.one);
            L4Shapes.Lathe(m, 0, frame, Reverse(dome), sides, web);
            L4Shapes.Lathe(m, 0, frame, Reverse(amb), sides, web);

            for (int k = 0; k < L1Monastery.PillarCount; k++)
            {
                float a = L1Monastery.PillarAngle(k) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // Up the dome to the oculus ring, and fanning: two side ribs to the neighbouring bays' crowns.
                foreach (float fan in new[] { 0f, -0.5f, 0.5f })
                {
                    var side = Quaternion.Euler(0f, -fan * 360f / L1Monastery.PillarCount, 0f);
                    for (int i = 0; i < 12; i++)
                    {
                        Vector2 p0 = dome[i], p1 = dome[i + 1];
                        var d0 = Vector3.Slerp(dir, side * dir, i / 12f);
                        var d1 = Vector3.Slerp(dir, side * dir, (i + 1) / 12f);
                        L4Shapes.Shaft(m, 0, centre + d0 * p0.x + Vector3.up * (p0.y - 0.2f), centre + d1 * p1.x + Vector3.up * (p1.y - 0.2f), 0.16f, 0.16f, 5, rib);
                    }
                }

                // Across the ambulatory to the wall.
                for (int i = 0; i < 8; i++)
                {
                    Vector2 p0 = amb[i], p1 = amb[i + 1];
                    L4Shapes.Shaft(m, 0, centre + dir * p0.x + Vector3.up * (p0.y - 0.2f), centre + dir * p1.x + Vector3.up * (p1.y - 0.2f), 0.14f, 0.14f, 5, rib);
                }
            }

            // The oculus's moulded ring.
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                L4Shapes.Shaft(m, 0, centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * oculus + Vector3.up * crown, centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * oculus + Vector3.up * crown, 0.3f, 0.3f, 6, rib);
            }
        }

        private static Vector2[] Reverse(List<Vector2> profile)
        {
            var copy = new List<Vector2>(profile);
            copy.Reverse();
            return copy.ToArray();
        }
    }
}
