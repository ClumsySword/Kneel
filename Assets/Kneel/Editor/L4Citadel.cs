using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Phase 3 (refinement): the citadel at the end of the level, the final boss's building (owner, 2026-10-19:
    // "Completely the castle at the end. It should like a final boss building."). It stands on the citadel bastion
    // (L4Plan M06, T+1) behind the throne doors, and replaces the lone keep spire S13.
    //
    // South to north: the throne hall, a cathedral-sized nave from the Church pieces with transepts and twin west
    // towers, its windows burning; then the great keep, a 70 m tower with four turrets and the spire, the level's
    // landmark; corner towers with spires and royal banners at the bastion's corners, joined by a curtain; beacon
    // fires on the towers and smoke leaning north. Exterior only (L5 is the inside): no colliders, nothing walkable.
    public static class L4Citadel
    {
        public const string GroupName = "Citadel";
        private const float Base = L4Plan.T1;

        private static Material Stone => L4Build.Mat("L4_Knights_Stone");
        private static Material Dark => L4Build.Mat("L4_Knights_StoneDark");
        private static Material Soot => L4Build.Mat("L4_Knights_Soot");
        private static Material Cloth => BannerRed;
        private static Material Glow => L4Build.Flat("L4_WindowGlow", "#1A0802", 0.1f, "#E0480E", 1.6f);

        // The master's colours (refinement, owner 2026-10-19: "more red banners... for contrast"), then "more crimson
        // like the color we had on the soldiers. The red is too bright": the Vanguard's dried-blood crimson (L1Look,
        // hue 0.99), a shade lighter so it still reads at dusk.
        public static Material BannerRed => L4Build.Flat("L4_BannerRed", "#511A1E", 0.1f);

        // The colossi's stone, blackened (owner: "make the statues look more menacing (stone color and evil
        // indicator)"), and the red that burns in their eyes and runes.
        private static Material Basalt => L4Build.Flat("L4_Basalt", "#2C2627", 0.42f);
        public static Material EvilGlow => L4Build.Flat("L4_EvilGlow", "#2A0402", 0.2f, "#FF1206", 3.2f);

        [MenuItem("Kneel/L4/Build/Citadel (replaces that group)")]
        public static void BuildMenu()
        {
            var root = L4Level.Root;
            if (root == null)
            {
                Debug.LogWarning("Open L4_BurningGate first.");
                return;
            }

            Debug.Log(Build(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        public static string Build(Transform root)
        {
            var top = root.Find(L4Level.ThresholdName);
            var old = top.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(top, GroupName).transform;
            int fires = 0;
            Hall(g, ref fires);
            Keep(g, ref fires);
            Corners(g, ref fires);
            Curtain(g);
            Colossi(g, ref fires);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is ParticleSystemRenderer))
                {
                    if (r.sharedMaterial == EvilGlow)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }

                    r.gameObject.layer = L3Build.Layer(L3Build.Unlayered);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            return GroupName + ": throne hall, keep, 4 corner towers, curtain, " + fires + " fires";
        }

        // Stacks pack pieces from groundY, each on the last; returns the top.
        private static float Stack(Transform parent, Vector2 at, float groundY, float scale, Material mat, params string[] keys)
        {
            float y = groundY;
            foreach (var key in keys)
            {
                var go = L3Build.Seat(parent, key, Vector3.zero, scale, at, y, mat);
                y = L3Build.LocalBounds(go, parent).max.y;
            }

            return y;
        }

        private static void Banner(Transform parent, Vector3 at, float yaw, float scale)
        {
            var pole = L3Build.Part(parent, "K:SM_Bld_Castle_Flagpole_01", at, new Vector3(0f, yaw, 0f), Vector3.one * scale, Soot);
            pole.name = "Flagpole";
            var flag = L3Build.Part(parent, "K:SM_Bld_Castle_Flag_01", at + Vector3.up * 2.55f * scale, new Vector3(0f, yaw, 0f), Vector3.one * scale, Cloth);
            flag.name = "Flag";
        }

        // A flag on the very tip of a spire (owner: "Make sure the flags are on the top of the castle towers").
        private static void TipFlag(Transform parent, GameObject spire, float yaw, float scale)
        {
            var b = L3Build.LocalBounds(spire, parent);
            Banner(parent, new Vector3(b.center.x, b.max.y - 0.35f * scale, b.center.z), yaw, scale);
        }

        // A long hanging banner on a face (its top edge at 'top', hanging toward -Y), facing 'yaw'.
        public static void HangBanner(Transform parent, Vector3 top, float yaw, float scale, int variant = 0)
        {
            string key = variant % 3 == 0 ? "K:SM_Prop_Banner_01" : variant % 3 == 1 ? "K:SM_Prop_Banner_02" : "K:SM_Prop_Banner_03";
            var banner = L3Build.Part(parent, key, top, new Vector3(0f, yaw, 0f), Vector3.one * scale, BannerRed);
            banner.name = "Banner";
        }

        // Fire in the master's colour: the stock fire, its flames and light turned blood red.
        public static Transform Hellfire(Transform parent, string name, Vector3 at, float size, float light, float range)
        {
            var head = L2Fire.Fire(parent, name, at, size, light, range, true);
            var ramp = new Gradient();
            ramp.SetKeys(
                new[] { new GradientColorKey(L1Build.Hex("#FFB08A"), 0f), new GradientColorKey(L1Build.Hex("#FF3A1A"), 0.3f), new GradientColorKey(L1Build.Hex("#C0100A"), 0.7f), new GradientColorKey(L1Build.Hex("#3A0402"), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            foreach (var ps in head.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name == "Smoke")
                {
                    continue;
                }

                var colour = ps.colorOverLifetime;
                colour.enabled = true;
                colour.color = new ParticleSystem.MinMaxGradient(ramp);
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            }

            foreach (var l in head.GetComponentsInChildren<Light>(true))
            {
                l.color = L1Build.Hex("#FF2410");
            }

            return head;
        }

        private static void Beacon(Transform parent, Vector3 at, float size, ref int fires)
        {
            L2Fire.Fire(parent, "Beacon", at, size, 3.5f * size, 9f * size, true);
            fires++;
        }

        // A tall burning window: a generated pane of firelight set into the wall, a pack window frame round it.
        private static void Window(Transform parent, Vector3 at, float yaw, float width, float height)
        {
            var pane = L3Build.MeshChild(parent, "WindowGlow", L4LookDev.FlatQuad(), at, Glow);
            pane.transform.localRotation = Quaternion.Euler(-90f, yaw, 0f);
            pane.transform.localScale = new Vector3(width, 1f, height);
            var frame = L3Build.Part(parent, "K:SM_Bld_Church_Window_01", at - Vector3.up * height * 0.5f, new Vector3(0f, yaw + 180f, 0f), new Vector3(width / 0.8f * 1.25f, height / 1.5f * 1.1f, 1.6f), Stone);
            frame.name = "WindowFrame";
        }

        // ---------------------------------------------------------------- The throne hall

        private static void Hall(Transform g, ref int fires)
        {
            var hall = L3Build.Child(g, "ThroneHall").transform;
            const float s = 4f;
            // The nave: the pack's lancet-windowed church bay at four times its size, 26 m wide and 35 m long,
            // walls 16 m, the ridge 26 m up.
            L3Build.Seat(hall, "K:SM_Bld_Church_Room_01", Vector3.zero, s, new Vector2(54f, 291f), Base, Dark);
            // Firelight in the lancets. The pack bay's three lancets a side (measured at this scale: glass 1.5 m wide,
            // 4.2 m tall, 8.6 m out from the nave's axis) each get a burning pane on the glass, behind the frame.
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float dz in new[] { -7f, 0f, 7f })
                {
                    var pane = L3Build.MeshChild(hall, "LancetGlow", L4LookDev.FlatQuad(), new Vector3(54f + side * 8.62f, Base + 8.3f, 291f + dz), Glow);
                    pane.transform.localRotation = Quaternion.Euler(-90f, side > 0 ? -90f : 90f, 0f);
                    pane.transform.localScale = new Vector3(1.4f, 1f, 4.1f);
                }
            }

            // Transepts at the crossing, and the apse closing the nave towards the keep.
            L3Build.Seat(hall, "K:SM_Bld_Church_Extension_01", new Vector3(0f, 90f, 0f), s, new Vector2(54f + 19.6f, 296f), Base, Dark);
            L3Build.Seat(hall, "K:SM_Bld_Church_Extension_01", new Vector3(0f, -90f, 0f), s, new Vector2(54f - 19.6f, 296f), Base, Dark);
            L3Build.Seat(hall, "K:SM_Bld_Church_Room_02", Vector3.zero, 2.6f, new Vector2(54f, 317f), Base, Dark);
            // The twin west towers at the nave's front corners, behind the throne doors' own towers.
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector2(54f + side * 16f, 276f);
                // Three plain shaft blocks, the open belfry, then a dark spire (no crosses: this is no church now).
                float top = Stack(hall, at, Base, 3.4f, Dark, "K:SM_Bld_Church_TowerBase_01", "K:SM_Bld_Church_TowerBase_01", "K:SM_Bld_Church_TowerBase_01", "K:SM_Bld_Church_Tower_03");
                var spire = L3Build.Seat(hall, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(3.6f, 3.2f, 3.6f), at, top - 0.4f, Soot);
                TipFlag(hall, spire, side > 0 ? 0f : 180f, 2.6f);
                // Two long banners down each tower's south face.
                foreach (float dx in new[] { -1.6f, 1.6f })
                {
                    HangBanner(hall, new Vector3(at.x + dx, Base + 21f, at.y - 5.25f), 0f, 3f, dx > 0 ? 1 : 0);
                }
            }

            // The master's banners down the hall's gable front, either side of its great door.
            foreach (float dx in new[] { -8.5f, -4.2f, 4.2f, 8.5f })
            {
                HangBanner(hall, new Vector3(54f + dx, Base + 15.5f, 273.2f), 0f, Mathf.Abs(dx) > 6f ? 3.4f : 4f, (int)(dx + 10f));
            }

            // Braziers either side of the hall's door, behind the throne doors.
            foreach (float side in new[] { -1f, 1f })
            {
                Beacon(hall, new Vector3(54f + side * 7.5f, Base + 0.2f, 271.5f), 1.2f, ref fires);
            }
        }

        // ---------------------------------------------------------------- The great keep

        private static void Keep(Transform g, ref int fires)
        {
            var keep = L3Build.Child(g, "GreatKeep").transform;
            var at = new Vector2(54f, 337f);
            float top = Stack(keep, at, Base, 4.2f, Dark, "K:SM_Bld_Castle_Tower_Base_02", "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_03", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_04", "K:SM_Bld_Castle_Tower_01");
            top = Stack(keep, at, top, 4.2f, Dark, "K:SM_Bld_Castle_Tower_Top_01");
            var keepSpire = L3Build.Seat(keep, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(4f, 3.2f, 4f), at, top - 1.5f, Soot);
            Beacon(keep, new Vector3(at.x + 4.5f, top + 0.2f, at.y - 4.5f), 1.6f, ref fires);
            TipFlag(keep, keepSpire, 180f, 3.4f);
            // Banners the height of a house down the keep's south face, between the burning slits.
            foreach (float dx in new[] { -4.3f, 4.3f })
            {
                HangBanner(keep, new Vector3(at.x + dx, Base + 46f, at.y - 6.3f), 0f, 5f, dx > 0 ? 2 : 0);
            }

            // Four turrets clasping the keep's corners, each with its own spire.
            foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                var c = at + new Vector2(dx * 6.6f, dz * 6.6f);
                float t = Stack(keep, c, Base, 2.6f, Stone, "K:SM_Bld_Castle_Tower_Round_01", "K:SM_Bld_Castle_Tower_Round_02", "K:SM_Bld_Castle_Tower_Round_01", "K:SM_Bld_Castle_Tower_Round_02", "K:SM_Bld_Castle_Tower_Round_01", "K:SM_Bld_Castle_Tower_Round_Top_01");
                var turretSpire = L3Build.Seat(keep, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(1.9f, 2.2f, 1.9f), c, t - 0.8f, Soot);
                TipFlag(keep, turretSpire, dx > 0 ? 0f : 180f, 1.7f);
            }

            // Burning slits up the keep's south face, seen from the court.
            for (int i = 0; i < 4; i++)
            {
                Window(keep, new Vector3(at.x, Base + 16f + i * 12.5f, at.y - 6.1f), 0f, 1.2f, 3.6f);
            }
        }

        // ---------------------------------------------------------------- Towers and curtain

        private static readonly Vector2[] CornerAt = { new Vector2(28f, 257f), new Vector2(80f, 257f), new Vector2(28f, 350f), new Vector2(80f, 350f), new Vector2(28f, 304f), new Vector2(80f, 304f) };

        private static void Corners(Transform g, ref int fires)
        {
            var towers = L3Build.Child(g, "Towers").transform;
            for (int i = 0; i < CornerAt.Length; i++)
            {
                var at = CornerAt[i];
                bool mid = i >= 4;
                float top = Stack(towers, at, Base - 0.5f, mid ? 3f : 3.4f, Stone, "K:SM_Bld_Castle_Tower_Base_01", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_04", mid ? "K:SM_Bld_Castle_Tower_01" : "K:SM_Bld_Castle_Tower_03", "K:SM_Bld_Castle_Tower_Top_01");
                if (!mid)
                {
                    var spire = L3Build.Seat(towers, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(3.2f, 2.4f, 3.2f), at, top - 1.2f, Soot);
                    TipFlag(towers, spire, at.x < 54f ? 0f : 180f, 2.4f);
                    HangBanner(towers, new Vector3(at.x, top - 3f, at.y - (mid ? 4.6f : 5.2f)), 0f, 3.2f, i);
                }
                else
                {
                    Beacon(towers, new Vector3(at.x, top + 0.2f, at.y), 1.4f, ref fires);
                }
            }
        }

        // Two colossal statues either side of the Kneeling Court, on the bastion's shoulders, looking down the grand
        // stair: the master's champion with his sword raised over the kneeling (the pack's knight statue at 4.2x,
        // 16 m, on a 5 m plinth). Refinement (owner: "more menacing (stone color and evil indicator)"): black
        // basalt, eyes burning red, red runes cut into the plinths, braziers of red fire throwing red light up them.
        private static void Colossi(Transform g, ref int fires)
        {
            var group = L3Build.Child(g, "Colossi").transform;
            foreach (float x in new[] { 34.5f, 73.5f })
            {
                float yaw = x < 54f ? 160f : 200f;
                var plinth = L3Build.Seat(group, "K:SM_Prop_Plinth_02", new Vector3(0f, yaw - 180f, 0f), new Vector3(2.4f, 2.1f, 2.4f), new Vector2(x, 263f), Base, Basalt);
                var pb = L3Build.LocalBounds(plinth, group);
                // The plinth is turned with the statue, so its faces come from its own frame, not the group's box.
                var pl = L3Build.LocalBounds(plinth, plinth.transform);
                var psize = Vector3.Scale(pl.size, plinth.transform.localScale);
                float halfD = psize.z * 0.5f, halfW = psize.x * 0.5f;
                var statue = L3Build.Seat(group, "K:SM_Prop_Statue_01", new Vector3(0f, yaw, 0f), 4.2f, new Vector2(x, 263f), pb.max.y - 0.2f, Basalt);
                statue.name = "Colossus";

                // Eyes: two slits of red light in the helm (the statue's face is at about 2.55 m, model +Z forward).
                var eyes = new L3Mesh();
                foreach (float ex in new[] { 0.015f, 0.125f })
                {
                    L4Shapes.Box(eyes, 0, new Vector3(ex, 2.56f, 0.215f), Quaternion.Euler(0f, 0f, ex < 0.07f ? -12f : 12f), new Vector3(0.06f, 0.018f, 0.04f), new Vector2(0.5f, 0.5f));
                }

                var eyeGo = L3Build.MeshChild(statue.transform, "Eyes", eyes.ToMesh("L4_ColossusEyes"), Vector3.zero, EvilGlow);
                eyeGo.transform.localRotation = Quaternion.identity;
                eyeGo.transform.localScale = Vector3.one;
                var face = statue.transform.TransformPoint(new Vector3(0.07f, 2.56f, 0.6f));
                var eyeLight = L3Build.Child(group, "EyeLight", group.InverseTransformPoint(face)).AddComponent<Light>();
                eyeLight.type = LightType.Point;
                eyeLight.color = L1Build.Hex("#FF1A08");
                eyeLight.intensity = 4f;
                eyeLight.range = 5f;
                eyeLight.shadows = LightShadows.None;

                // The plinth burns from inside: a seam of red light under its cornice all round, and the master's
                // glyphs cut into its face over a band at its foot (owner, 2026-10-19: "I want them back" after a
                // pass that swapped them for a crack).
                var front = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var across = Vector3.Cross(Vector3.up, front);
                var runes = new L3Mesh();
                var rrng = new System.Random((int)x);
                var centre = new Vector3(x, 0f, 263f);
                float seam = pb.max.y - 0.55f;
                foreach (var dir in new[] { front, -front, across, -across })
                {
                    bool facing = Mathf.Abs(Vector3.Dot(dir, front)) > 0.5f;
                    float reach = facing ? halfD : halfW;
                    float span = facing ? psize.x : psize.z;
                    var c = centre + dir * (reach + 0.015f) + Vector3.up * seam;
                    L4Shapes.Box(runes, 0, group.InverseTransformPoint(c), Quaternion.LookRotation(dir), new Vector3(span * 0.96f, 0.05f, 0.04f), new Vector2(0.5f, 0.5f));
                }

                var faceCentre = centre + front * (halfD + 0.02f);
                for (int i = 0; i < 7; i++)
                {
                    float off = (i - 3f) * Mathf.Min(0.75f, psize.x * 0.12f);
                    float y0 = Base + 1.2f + (float)rrng.NextDouble() * 0.4f, len = 0.9f + (float)rrng.NextDouble() * 1.4f;
                    var c = faceCentre + across * off + Vector3.up * (y0 + len * 0.5f);
                    L4Shapes.Box(runes, 0, group.InverseTransformPoint(c), Quaternion.LookRotation(front) * Quaternion.Euler(0f, 0f, ((float)rrng.NextDouble() - 0.5f) * 30f), new Vector3(0.09f, len, 0.06f), new Vector2(0.5f, 0.5f));
                    if (rrng.NextDouble() < 0.6)
                    {
                        var tick = faceCentre + across * (off + 0.18f) + Vector3.up * (y0 + len * (0.3f + (float)rrng.NextDouble() * 0.4f));
                        L4Shapes.Box(runes, 0, group.InverseTransformPoint(tick), Quaternion.LookRotation(front) * Quaternion.Euler(0f, 0f, 55f), new Vector3(0.07f, 0.4f, 0.06f), new Vector2(0.5f, 0.5f));
                    }
                }

                L4Shapes.Box(runes, 0, group.InverseTransformPoint(faceCentre + Vector3.up * (Base + 0.6f)), Quaternion.LookRotation(front), new Vector3(psize.x * 0.85f, 0.08f, 0.06f), new Vector2(0.5f, 0.5f));

                L3Build.MeshChild(group, "Runes", runes.ToMesh("L4_ColossusRunes_" + (x < 54f ? "W" : "E")), Vector3.zero, EvilGlow);

                // Red fire either side of the plinth, and a red light washing up the statue from its foot.
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = new Vector3(x, Base + 0.1f, 263f) + front * (pb.extents.z + 1.6f) + across * side * 2.6f;
                    // The fire (and its light) in the brazier's basket: from the piece's own box, not a guess.
                    var bowl = L3Build.Seat(group, "K:SM_Prop_Brazier_01", Vector3.zero, 1.6f, new Vector2(at.x, at.z), Base, Soot);
                    var bb = L3Build.LocalBounds(bowl, group);
                    Hellfire(group, "Hellfire", new Vector3(bb.center.x, bb.max.y - 0.4f, bb.center.z), 1.1f, 5f, 12f);
                    fires++;
                }

                var up = L3Build.Child(group, "Uplight", new Vector3(x, Base + 1f, 263f) + front * (pb.extents.z + 3.5f)).AddComponent<Light>();
                up.type = LightType.Spot;
                up.transform.rotation = Quaternion.LookRotation((new Vector3(x, Base + 14f, 263f) - up.transform.position).normalized);
                up.spotAngle = 50f;
                up.color = L1Build.Hex("#FF2A12");
                up.intensity = 240f;
                up.range = 26f;
                up.shadows = LightShadows.None;
            }
        }

        // The bastion's own curtain on its outer edges, pack walls at twice their size, between the towers.
        private static void Curtain(Transform g)
        {
            var walls = L3Build.Child(g, "Curtain").transform;
            void Run(Vector2 a, Vector2 b)
            {
                float length = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.RoundToInt(length / 10.8f));
                var dir = (b - a) / length;
                int yaw = Mathf.Abs(dir.x) > 0.5f ? 0 : 90;
                for (int i = 0; i < n; i++)
                {
                    var c = a + dir * (length * (i + 0.5f) / n);
                    var size = yaw == 0 ? new Vector3(length / n + 0.2f, 7.6f, 2.2f) : new Vector3(2.2f, 7.6f, length / n + 0.2f);
                    L3Build.Fit(walls, "K:SM_Bld_Castle_Wall_01", yaw, size, c, Base - 0.3f, Stone);
                }
            }

            Run(new Vector2(28f, 261f), new Vector2(28f, 300f));
            Run(new Vector2(28f, 308f), new Vector2(28f, 346f));
            Run(new Vector2(80f, 261f), new Vector2(80f, 300f));
            Run(new Vector2(80f, 308f), new Vector2(80f, 346f));
            Run(new Vector2(32f, 350f), new Vector2(76f, 350f));
        }
    }
}
