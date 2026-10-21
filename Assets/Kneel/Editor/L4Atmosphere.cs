using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Phase 3 (refinement): the burning. The level is called The Burning Gate and the note's setting is a
    // stronghold still smoking three days after it fell, but the blockout showed almost no fire. This adds what the
    // camera sees around the fights, never in them (decision 17: oil burns only from the level's own lights, so no
    // fire here sits on or over a slick, and nothing here is a hazard):
    // - smoke columns rising out of the ravine, from the siege camp, the chapel's belfry and beyond the walls,
    //   leaning north (decision 10: the smoke thickens beyond the outer walls, never over an arena floor);
    // - fires on the outer gatehouse's towers, on the refugee carts, in the ravine, in the siege camp;
    // - embers drifting over the whole route.
    // No colliders; rebuilding replaces only this group.
    public static class L4Atmosphere
    {
        public const string GroupName = "Atmosphere";

        [MenuItem("Kneel/L4/Build/Atmosphere (replaces that group)")]
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

        // The ravine is lava now (L4Lava): things that stood on its floor stand on the crust.
        private static float Ground(float x, float z) => Mathf.Max(L4Surroundings.Height(new Vector2(x, z)), L4Lava.LavaY);

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            int plumes = 0, fires = 0, embers = 0;

            // ---- Smoke columns. Big ones far out give the scale; the ravine ones sit right under the frame's edge.
            var smoke = L3Build.Child(g, "Smoke").transform;
            var columns = new (float x, float z, float scale)[]
            {
                (101f, 147f, 1.3f),     // under the E4 breach ("smoke rising from the ravine")
                (16f, 112f, 1.1f),      // west ravine, below the Lists
                (104f, 84f, 1f),        // east ravine, by the prison lane
                (20f, 200f, 1.2f),      // west ravine, by the cloister
                (100f, 228f, 1f),       // east ravine, by E6
                (150f, 172f, 1.6f),     // the siege camp on the east bank
                (-40f, 150f, 2.4f),     // far west, burning land
                (190f, 260f, 2.6f),     // far east
                (36f, 380f, 2.2f),      // behind the citadel
                (90f, 395f, 2.8f),
                (-30f, 40f, 2f),        // the south-west, seen from the causeway
                (150f, 20f, 2.2f),
            };
            foreach (var (x, z, scale) in columns)
            {
                Column(smoke, new Vector3(x, Ground(x, z), z), scale);
                plumes++;
            }

            // Out of the chapel's split belfry.
            Column(smoke, new Vector3(91.5f, 12f, 197.5f), 0.7f);
            plumes++;

            // ---- Fires.
            var fire = L3Build.Child(g, "Fires").transform;
            void Fire(string name, Vector3 at, float size, float light, bool withSmoke, Vector3 area = default)
            {
                L2Fire.Fire(fire, name, at, size, light, 6f + size * 4f, withSmoke, area);
                fires++;
            }

            // The outer gatehouse's roof is still burning on the passage walls' tops (S01 at (62, 57), walls 7 m).
            // Kept to the flanking towers' tops (x 47 and 80): on the passage walls they burned right under the camera.
            // (The towers stand at x 47 and 73 since the refinement, 18 m tall.)
            Fire("Gatehouse_W", new Vector3(47f, 18.4f, 60f), 1.3f, 3f, true, new Vector3(3f, 0f, 3f));
            Fire("Gatehouse_E", new Vector3(73f, 18.4f, 60f), 1.2f, 3f, true, new Vector3(3f, 0f, 3f));
            // The refugee carts on the causeway and at the lookout.
            L2Fire.Smoulder(fire, "Cart_1", new Vector3(57.8f, 0.4f, 14f), new Vector2(1.8f, 2.6f));
            L2Fire.Smoulder(fire, "Cart_3", new Vector3(40.8f, 0.4f, 52.5f), new Vector2(1.6f, 2.4f));
            fires += 2;
            // The ram in the outer arch, its shed burnt down to embers.
            L2Fire.Smoulder(fire, "Ram", new Vector3(65f, 0.2f, 54f), new Vector2(3f, 5f), 10f);
            fires++;
            // Refuse burning on the ravine floor under each column, and in the siege camp.
            foreach (var (x, z, scale) in columns)
            {
                if (scale < 1.5f)
                {
                    Fire("Ravine", new Vector3(x, Ground(x, z) + 0.1f, z), 1.6f * scale, 4f * scale, false, new Vector3(3f, 0f, 3f) * scale);
                }
            }

            foreach (var (dx, dz) in new[] { (-6f, 3f), (5f, -4f), (2f, 7f) })
            {
                float x = 150f + dx, z = 172f + dz;
                Fire("SiegeCamp", new Vector3(x, Ground(x, z) + 0.1f, z), 1.4f, 3f, false, new Vector3(2f, 0f, 2f));
            }

            // ---- Embers drifting over the route, sparse enough never to hide a tell.
            var ember = L3Build.Child(g, "Embers").transform;
            var fields = new (Vector3 centre, Vector3 size)[]
            {
                (new Vector3(60f, 3f, 40f), new Vector3(30f, 4f, 60f)),
                (new Vector3(54f, 4f, 95f), new Vector3(50f, 6f, 40f)),
                (new Vector3(68f, 5f, 140f), new Vector3(46f, 8f, 40f)),
                (new Vector3(70f, 4f, 180f), new Vector3(50f, 6f, 40f)),
                (new Vector3(56f, 4f, 230f), new Vector3(36f, 6f, 60f)),
            };
            foreach (var (centre, size) in fields)
            {
                Embers(ember, centre, size);
                embers++;
            }

            // Ash falling out of the smoke over the whole route (refinement, owner 2026-10-21: "endgame dark souls...
            // gritty"): grey flakes drifting down and north, thicker out on the farm track and the causeway.
            var ashFall = L3Build.Child(g, "AshFall").transform;
            foreach (var (centre, size) in fields)
            {
                Ash(ashFall, centre + Vector3.up * 5f, size + new Vector3(10f, 0f, 10f));
            }

            Ash(ashFall, new Vector3(60f, 8f, -20f), new Vector3(40f, 6f, 50f));

            return GroupName + ": " + plumes + " smoke columns, " + fires + " fires, " + embers + " ember fields";
        }

        // A tall column of dark smoke that widens and pales as it rises, leaning north on the wind.
        private static void Column(Transform parent, Vector3 at, float scale)
        {
            var head = L3Build.Child(parent, "Column", at).transform;
            var ps = L1Wayfinding.Particles("Smoke", head, L1Build.FxMat("L1_FX_Smoke"), Mathf.CeilToInt(70 * scale), 2.2f * Mathf.Sqrt(scale),
                new Vector2(24f, 32f), new Vector2(1.6f, 2.6f) * scale, new Vector2(5f, 8f) * scale,
                new Color(0.06f, 0.055f, 0.05f, 0.85f), new Color(0.3f, 0.28f, 0.26f, 0f));
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 2.2f * scale;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 3.6f));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.08f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = 3f;
            renderer.sortingFudge = 10f;
        }

        private static void Ash(Transform parent, Vector3 centre, Vector3 size)
        {
            var ps = L1Wayfinding.Particles("Ash", parent, L1Build.FxMat("L1_FX_Smoke"), Mathf.CeilToInt(size.x * size.z * 0.05f), size.x * size.z * 0.006f,
                new Vector2(7f, 11f), new Vector2(0.05f, 0.2f), new Vector2(0.05f, 0.11f), new Color(0.42f, 0.4f, 0.38f, 0.85f), new Color(0.3f, 0.29f, 0.28f, 0f), 0.012f);
            ps.transform.position = centre;
            ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.z, 0.5f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.02f;
        }

        private static void Embers(Transform parent, Vector3 centre, Vector3 size)
        {
            var ps = L1Wayfinding.Particles("Embers", parent, L1Build.FxMat("L1_FX_Ember"), Mathf.CeilToInt(size.x * size.z * 0.03f), size.x * size.z * 0.004f,
                new Vector2(5f, 9f), new Vector2(0.2f, 0.6f), new Vector2(0.04f, 0.09f), new Color(1f, 0.7f, 0.35f, 1f), new Color(1f, 0.3f, 0.08f, 0.7f), -0.03f);
            ps.transform.position = centre;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.z, size.y);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.3f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.02f;
        }
    }
}
