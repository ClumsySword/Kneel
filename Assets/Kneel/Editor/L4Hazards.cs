using System.Collections.Generic;
using Kneel.Hazards;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // The hazards of the L4 manifest (14.4) and the breach edge (W06): their prefabs, the NavMesh areas they mark
    // and the tuning assets they read. Behaviour lives in Kneel.Hazards (Assets/Kneel/Gameplay/Hazards).
    public static partial class L4Assets
    {
        public static readonly string[] NavAreas = { "EdgeBuffer", "PourRing", "OilSlick", "GateSlot", "ScaffoldZone" };
        public static readonly float[] NavAreaCosts = { 4f, 1f, 1f, 1f, 1f };

        public static StrongholdTuning Tuning => Settings<StrongholdTuning>("L4_StrongholdTuning", t =>
        {
            t.playerMask = 1 << L3Build.Layer("Player");
        });

        public static HazardTuning Contact => Settings<HazardTuning>("L4_HazardTuning", t =>
        {
            t.actorMask = (1 << L3Build.Layer("Player")) | (1 << L3Build.Layer("Enemy"));
        });

        private static T Settings<T>(string name, System.Action<T> init) where T : ScriptableObject
        {
            string path = L4Build.SettingsPath + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                L2Build.EnsureFolder(L4Build.SettingsPath);
                asset = ScriptableObject.CreateInstance<T>();
                init(asset);
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        // Adds the L4 NavMesh areas if missing (as L3Build.EnsureProjectSettings does for L3's). Returns what it did.
        public static string EnsureProjectSettings()
        {
            var log = new List<string>();
            var nav = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var areas = nav.FindProperty("areas");
            for (int a = 0; a < NavAreas.Length; a++)
            {
                if (NavMesh.GetAreaFromName(NavAreas[a]) >= 0)
                {
                    continue;
                }

                for (int i = 3; i < areas.arraySize; i++)
                {
                    var slot = areas.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.FindPropertyRelative("name").stringValue))
                    {
                        slot.FindPropertyRelative("name").stringValue = NavAreas[a];
                        slot.FindPropertyRelative("cost").floatValue = NavAreaCosts[a];
                        nav.ApplyModifiedPropertiesWithoutUndo();
                        log.Add("added NavMesh area " + NavAreas[a] + " at " + i);
                        break;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return log.Count == 0 ? "NavMesh areas already in place" : string.Join("; ", log);
        }

        private static Material Oil => OilPalette(false);
        private static Material Charcoal => OilPalette(true);

        // Oil: pitch black with an iridescent edge (teal, violet, amber). Charcoal: the same cells burnt matte.
        private static Material OilPalette(bool spent)
        {
            string name = spent ? "L4_Palette_OilSpent" : "L4_Palette_Oil";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(L4Build.MaterialsPath + "/" + name + ".mat");
            if (existing != null)
            {
                return existing;
            }

            var tones = spent ? new[] { "#151312", "#1F1C1A", "#262220", "#2E2925", "#18100B", "#3A332E", "#100E0D", "#4A423B" }
                              : new[] { "#060505", "#0B1A1C", "#170C1E", "#2A1A06", "#0E0D0C", "#141818", "#060505", "#1B1612" };
            var colors = new Color[tones.Length];
            for (int i = 0; i < tones.Length; i++)
            {
                colors[i] = L1Build.Hex(tones[i]);
            }

            return L4Kit.Palette(name, colors, spent ? 0.05f : 0.82f);
        }

        private static void HazardRows(AddRow Add)
        {
            const string hazards = "14.4 Hazards";
            string hz = L3Build.Hazard;
            Add("H01", "L4_MurderHole", "Hazards", hazards, 6f, 6f, 5.5f, Col.None, hz, "BUILT", "Kneel.Hazards.MurderHole; L1 smoke and ember particles; generated pour column",
                "The pour reaches 1.5 R (3 m). Its mark is painted into the floor where it is placed (L4Paving.Splash), never a decal. Tell: steam from the slot 5.5 m up and hissing drips for 1.5 s; pour: a column of boiling oil for 1 s; then steam off the scalding floor for 2 s. Carries a PourRing NavMesh area. Sequential by default (order 0 of 3); set order and rings per placement, or External for the gate passage.",
                MurderHolePrefab);
            Add("H02", "L4_OilSlick", "Hazards", hazards, 4f, 4f, 1.5f, Col.Custom, hz, "BUILT", "Kneel.Hazards.OilSlick; generated slick (opaque, glossy); L2Fire flames",
                "2 R across. Unlit: a glossy black pool with an oily sheen at its edge, an opaque mesh 1 cm proud of the floor (no transparent decal on floors). Burning: L2's flames over the whole pool with its light, carving the NavMesh. Spent: a charred, matte stain. Carries an OilSlick NavMesh area.",
                OilSlickPrefab);
            Add("H03", "L4_Cresset_Jib", "Hazards", hazards, 1f, 3f, 4f, Col.Custom, hz, "BUILT", "Kneel.Hazards.HangingLight; PolygonKnights beams; generated iron basket and chain; L2Fire",
                "A post with a jib arm reaching 2.4 m toward +Z, an iron fire basket hanging on a chain from its end over the slick. Strike collider on the post (Enemy layer). Falls to 'Landed' at +Z 2.4 m. Link its slick when placed.",
                root => Cresset(root));
            Add("H03b", "L4_Corona", "Hazards", hazards, 3.2f, 3.2f, 9f, Col.Custom, hz, "BUILT", "Kneel.Hazards.HangingLight; generated iron ring, cups and chain; L2Fire candles; PolygonKnights beam",
                "The chapel's corona: an iron ring of candle cups 3 m across hanging 4 m up from a burnt roof beam, its chain run down to a cleat 3 m to the -X side (put the cleat on a column). Strike collider on the cleat.",
                Corona);
            Add("H03c", "L4_TripodBrazier", "Hazards", hazards, 0.8f, 0.8f, 2.2f, Col.Custom, hz, "ADAPTED", "Kneel.Hazards.HangingLight; PolygonKnights SM_Prop_Brazier_01; L2Fire",
                "The pack brazier raised to 2.2 m on its tripod, burning. Struck or rammed, it wobbles and topples toward +Z into the slick beside it.",
                TripodBrazier);
            Add("H04a", "L4_Portcullis_8", "Hazards", hazards, 8f, 2f, 6f, Col.Custom, hz, "BUILT", "Kneel.Hazards.PortcullisCycle; PolygonKnights SM_Bld_Castle_Iron_Gate_01 (3 x 2 panels); generated spikes",
                "The gate passage's cycling portcullis (E6b): 8 m grid, iron spikes along its foot, a 1 R slot that kills. Cycle mode. Dust and chain shudder while it rattles.",
                root => Portcullis(root, 8f, PortcullisCycle.Mode.Cycle));
            Add("H04b", "L4_Portcullis_12", "Hazards", hazards, 12f, 2f, 6f, Col.Custom, hz, "BUILT", "As H04a, 5 x 2 panels",
                "The barbican's outer arch (BA): one-shot demo; up until its DemoTrigger, then down for good.",
                root => Portcullis(root, 12f, PortcullisCycle.Mode.OneShot));
            Add("H04c", "L4_Portcullis_6", "Hazards", hazards, 6f, 2f, 6f, Col.Custom, hz, "BUILT", "As H04a, 2 x 2 panels",
                "The west-lane gate (decision 16): the same gate in Winch mode. Starts down; its winch raises it for good.",
                root => Portcullis(root, 6f, PortcullisCycle.Mode.Winch));
            Add("H05", "L4_Winch", "Hazards", hazards, 1.6f, 1.2f, 1.3f, Col.Box, L3Build.Cover, "BUILT", "Kneel.Hazards.Winch; generated drum; PolygonKnights beams",
                "A timber winch: a drum between two posts, a crank, chain wound on it. Interact (E) within 2.5 m. Link its gate when placed.",
                WinchPrefab);
            Add("H05b", "L4_CapstanBay", "Hazards", hazards, 4f, 4f, 3f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit stone courses; PolygonKnights iron gate (bars); generated capstan; baked Synty gatekeeper",
                "The gatekeeper's barred bay beside the gate passage: a capstan and the gatekeeper working it, seen through bars on its -Z face. The bay's back door (+Z) leads to the forecourt.",
                CapstanBay);
            Add("H06a", "L4_Scaffold", "Hazards", hazards, 6f, 2f, 5f, Col.Custom, hz, "BUILT", "Kneel.Hazards.ScaffoldCollapse, ScaffoldStrut; PolygonKnights beams; L2 planks",
                "A masons' scaffold against a wall (+Z): uprights, ledgers, braces, two plank lifts. Its raking strut stands 2 m beyond the collapse zone on -Z. Struck, it creaks and sags 1.5 s and falls forward (-Z) over its chalk-marked zone (footprint + 1 R): two hits under it, death on it. The debris then blocks and carves.",
                root => Scaffold(root, false));
            Add("H06b", "L4_Scaffold_Demo", "Hazards", hazards, 6f, 2f, 5f, Col.Custom, hz, "BUILT", "As H06a; PolygonAdventure barrels (the casks)",
                "The rampart-stair demo (5:30): the same scaffold over two oil casks inside its chalk marks, played once by its DemoTrigger and never again.",
                root => Scaffold(root, true));
            Add("H07", "L4_ChoirLoft", "Hazards", hazards, 24f, 4f, 6f, Col.Custom, hz, "BUILT", "Kneel.Hazards.ScaffoldCollapse, ScaffoldStrut; L2 planks; PolygonKnights beams; L2Fire candles",
                "The chapel's choir loft at T+1 (+5 m): a timber floor across the apse on posts, a rail with burning lamps, a strut at each end. Either strut brings it down forward (-Z); its lamps light the slick linked to it.",
                ChoirLoft);
            Add("H08a", "L4_KneelerAmbush", "Hazards", hazards, 1f, 1.4f, 1.2f, Col.None, hz, "BUILT", "Kneel.Hazards.KneelerAmbush; L4_Kneeler_Upright (F02)",
                "An upright kneeler (decision 8): the kneeling figure, a hit collider on the Enemy layer, the 3 R wake radius, a SpawnPoint for the enemy it stands in for.",
                root => Kneeler(root, false));
            Add("H08b", "L4_KneelerAmbush_Twin", "Hazards", hazards, 1f, 1.4f, 1.2f, Col.None, hz, "BUILT", "As H08a; generated ember brand",
                "As H08a with the Cursed Twins' ember brand on its chest. Link the pair as partners when placed: striking one wakes both.",
                root => Kneeler(root, true));
            Add("H09", "L4_KillVolume", "Hazards", hazards, 6f, 6f, 2.5f, Col.Custom, hz, "BUILT", "Kneel.Hazards.DitchKill",
                "A resizable kill trigger (pivot at its bottom centre): the ravine, the breach drop.",
                root =>
                {
                    L3Build.StandingBox(root, new Vector3(6f, 2.5f, 6f), true);
                    root.AddComponent<DitchKill>();
                    L3Build.Set(root.GetComponent<DitchKill>(), "contact", Contact);
                });
            Add("H10", "L4_PourTrough", "Hazards", hazards, 6f, 2f, 1.6f, Col.Custom, L3Build.Boundary, "BUILT", "Kneel.Hazards.PourTender; L4Kit stone; L4_Cauldron; baked Synty thrall",
                "Six metres of the pour trough on the north wall-walk, a cauldron, and the chained pour-tender tipping it. One hit kills him and the linked murder holes stop until the level resets.",
                PourTrough);
            Add("H11", "(component on N04)", "Hazards", hazards, 0f, 0f, 0f, Col.None, hz, "BUILT", "Kneel.Hazards.OnScreenGate",
                "A component, not a prefab: it sits on the crossbowman stand-in (N04) and on every ranged attacker the AI will spawn.", null);
            Add("W06", "L4_BreachEdge", "Kit", "14.2 Wall kit", 2f, 4f, 1.4f, Col.Custom, L3Build.Walkable, "BUILT", "L4Kit broken merlons and fallen blocks; Kneel.Hazards.EdgeGuard, DitchKill; L1 smoke and ember particles",
                "A 2 m segment of the breach: merlon stumps, a 0.3 m broken lip with no collider at z 0, a 3 m rubble buffer inward (-Z, loose stones, no colliders), smoke and ember glow rising from below, an EdgeGuard on the lip and a kill volume 1 m beyond it. Carries an EdgeBuffer NavMesh area over the buffer.",
                BreachEdge);
        }

        // ---------------------------------------------------------------- Helpers

        private static GameObject NavAreaVolume(GameObject root, string area, Vector3 size, Vector3 centre)
        {
            // On Ground, so a surface that bakes Ground and Obstacles collects it (an unbaked layer is ignored).
            var go = L3Build.Child(root.transform, "NavArea", centre);
            go.layer = L3Build.Layer(L3Build.Walkable);
            var volume = go.AddComponent<NavMeshModifierVolume>();
            volume.size = size;
            volume.center = Vector3.zero;
            volume.area = L3Build.NavArea(area);
            return go;
        }

        private static GameObject StrikeBox(GameObject root, string name, Vector3 size, Vector3 baseCentre)
        {
            var go = L3Build.Child(root.transform, name, baseCentre);
            L3Build.StandingBox(go, size, true);
            go.layer = L3Build.Layer(L3Build.Hittable);
            return go;
        }

        private static ParticleSystem Steam(Transform parent, string name, Vector3 at, float rate, Vector2 size, Vector2 speed, float spread)
        {
            var ps = L1Wayfinding.Particles(name, parent, L1Build.FxMat("L1_FX_Smoke"), 60, rate, new Vector2(1.2f, 2.2f), speed, size,
                new Color(0.85f, 0.83f, 0.8f, 0.5f), new Color(0.6f, 0.58f, 0.55f, 0.15f), -0.02f);
            ps.transform.localPosition = at;
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = spread;
            return ps;
        }

        // ---------------------------------------------------------------- Builders

        private static void MurderHolePrefab(GameObject root)
        {
            var t = root.transform;
            const float slotY = 5.5f;
            var slot = L3Build.Child(t, "Slot", new Vector3(0f, slotY, 0f));

            var tell = L3Build.Child(t, "Tell");
            Steam(tell.transform, "SlotSteam", new Vector3(0f, slotY - 0.2f, 0f), 18f, new Vector2(0.4f, 0.9f), new Vector2(0.2f, 0.6f), 0.4f);
            var drips = L1Wayfinding.Particles("Drips", tell.transform, L1Build.FxMat("L1_FX_Ember"), 40, 16f, new Vector2(0.5f, 0.6f), new Vector2(0f, 0.2f),
                new Vector2(0.03f, 0.05f), new Color(0.35f, 0.2f, 0.08f, 1f), new Color(0.1f, 0.07f, 0.04f, 1f), 2f);
            drips.transform.localPosition = new Vector3(0f, slotY - 0.3f, 0f);
            var dripShape = drips.shape;
            dripShape.shapeType = ParticleSystemShapeType.Circle;
            dripShape.radius = 0.35f;
            tell.SetActive(false);

            var pour = L3Build.Child(t, "Pour");
            Shapes(pour.transform, "Column", L4Build.Flat("L4_BoilingOil", "#2B1A08", 0.9f, "#3A1E05", 0.6f), m =>
            {
                var profile = new[] { new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.3f, 0.4f), new Vector2(0.24f, slotY - 0.3f), new Vector2(0.3f, slotY), new Vector2(0f, slotY) };
                L4Shapes.Lathe(m, 0, Matrix4x4.identity, profile, 9, L4Kit.Cell(0));
            });
            Steam(pour.transform, "SplashSteam", new Vector3(0f, 0.1f, 0f), 40f, new Vector2(0.8f, 1.6f), new Vector2(0.8f, 1.6f), 1.2f);
            pour.SetActive(false);

            var scald = L3Build.Child(t, "Scald");
            Steam(scald.transform, "FloorSteam", new Vector3(0f, 0.05f, 0f), 30f, new Vector2(0.9f, 1.8f), new Vector2(0.3f, 0.7f), 2.6f);
            scald.SetActive(false);

            NavAreaVolume(root, "PourRing", new Vector3(6f, 2f, 6f), new Vector3(0f, 1f, 0f));
            var hole = root.AddComponent<MurderHole>();
            L3Build.Set(hole, "tuning", Tuning);
            L3Build.Set(hole, "contact", Contact);
            L3Build.Set(hole, "tellVisual", tell);
            L3Build.Set(hole, "pourVisual", pour);
            L3Build.Set(hole, "scaldVisual", scald);
            slot.name = "Slot";
        }

        // An irregular pool outline: a noisy circle of 'radius', inner pitch and an outer sheen band.
        private static void SlickMesh(L3Mesh m, float radius, int seed, bool spent)
        {
            const int n = 28;
            var rng = new System.Random(seed);
            var r = new float[n];
            float phase = (float)rng.NextDouble() * 6f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                r[i] = radius * (0.82f + 0.1f * Mathf.Sin(3f * a + phase) + 0.07f * Mathf.Sin(5f * a + phase * 2f) + ((float)rng.NextDouble() - 0.5f) * 0.08f);
            }

            Vector3 P(int i, float scale, float y) => new Vector3(Mathf.Cos(i * Mathf.PI * 2f / n) * r[i % n] * scale, y, Mathf.Sin(i * Mathf.PI * 2f / n) * r[i % n] * scale);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // Pitch in the middle, a band of sheen (three alternating cells) round the edge.
                L4Shapes.Tri(m, 0, new Vector3(0f, 0.012f, 0f), P(i, 0.86f, 0.012f), P(j, 0.86f, 0.012f), Vector3.up, L4Kit.Cell(spent ? 0 : 0));
                int sheen = spent ? 2 : 1 + (i % 3);
                L4Shapes.Quad(m, 0, P(i, 0.86f, 0.012f), P(j, 0.86f, 0.012f), P(j, 1f, 0.006f), P(i, 1f, 0.006f), Vector3.up, L4Kit.Cell(sheen));
            }
        }

        private static void OilSlickPrefab(GameObject root)
        {
            var t = root.transform;
            L3Build.StandingBox(root, new Vector3(4f, 1.5f, 4f), true);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(3.6f, 1.5f, 3.6f);
            obstacle.center = new Vector3(0f, 0.75f, 0f);
            obstacle.carving = true;
            obstacle.enabled = false;

            var unlit = L3Build.Child(t, "Unlit");
            Shapes(unlit.transform, "Pool", Oil, m => SlickMesh(m, 2f, 301, false));
            var burning = L3Build.Child(t, "Burning");
            Shapes(burning.transform, "Char", Charcoal, m => SlickMesh(m, 2f, 301, true));
            L2Fire.Fire(burning.transform, "Flames", new Vector3(0f, 0.05f, 0f), 1.1f, 2.6f, 7f, true, new Vector3(3.2f, 0f, 3.2f), L2Fire.Shape.Box, 0.8f);
            burning.SetActive(false);
            var spent = L3Build.Child(t, "Spent");
            Shapes(spent.transform, "Char", Charcoal, m => SlickMesh(m, 2f, 301, true));
            spent.SetActive(false);

            NavAreaVolume(root, "OilSlick", new Vector3(4f, 2f, 4f), new Vector3(0f, 1f, 0f));
            var slick = root.AddComponent<OilSlick>();
            L3Build.Set(slick, "tuning", Tuning);
            L3Build.Set(slick, "contact", Contact);
            L3Build.Set(slick, "obstacle", obstacle);
            L3Build.Set(slick, "unlitVisual", unlit);
            L3Build.Set(slick, "burningVisual", burning);
            L3Build.Set(slick, "spentVisual", spent);
        }

        // An iron fire basket: a ring of bars round a bowl of coals, burning, with its light.
        private static GameObject Basket(Transform parent, string name, Vector3 at, float scale)
        {
            var basket = L3Build.Child(parent, name, at);
            Shapes(basket.transform, "Cage", L4Shapes.Props, m =>
            {
                L4Shapes.Lathe(m, 0, Matrix4x4.Scale(Vector3.one * scale), new[] { new Vector2(0f, -0.25f), new Vector2(0.22f, -0.2f), new Vector2(0.3f, -0.05f), new Vector2(0.27f, -0.02f), new Vector2(0f, -0.05f) }, 8, L4Kit.Cell(L4Shapes.IronDark));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    var foot = new Vector3(Mathf.Cos(a) * 0.28f, -0.05f, Mathf.Sin(a) * 0.28f) * scale;
                    var top = new Vector3(Mathf.Cos(a) * 0.4f, 0.32f, Mathf.Sin(a) * 0.4f) * scale;
                    L4Shapes.Shaft(m, 0, foot, top, 0.018f * scale, 0.018f * scale, 4, L4Kit.Cell(L4Shapes.Iron));
                }

                L4Shapes.Lathe(m, 0, Matrix4x4.Scale(Vector3.one * scale), new[] { new Vector2(0f, -0.04f), new Vector2(0.26f, -0.03f), new Vector2(0.18f, 0.06f), new Vector2(0f, 0.09f) }, 7, L4Kit.Cell(L4Shapes.Pitch));
            });
            L2Fire.Fire(basket.transform, "Flames", new Vector3(0f, 0.05f, 0f) * scale, 0.45f * scale, 1.6f, 6f, true);
            return basket;
        }

        private static void Cresset(GameObject root)
        {
            var t = root.transform;
            const float jib = 2.4f, postH = 4f, hangY = 2.6f;
            L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(1.1f, postH / 2.5f, 1.1f), Vector2.zero, 0f, Charred);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(90f, 0f, 0f), new Vector3(0.9f, jib / 2.5f + 0.1f, 0.9f), new Vector2(0f, jib * 0.5f), postH - 0.35f, Charred);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(45f, 0f, 0f), new Vector3(0.7f, 0.5f, 0.7f), new Vector2(0f, 0.45f), postH - 1.3f, Charred);
            var swing = L3Build.Child(t, "Swinging", new Vector3(0f, postH - 0.3f, jib)).transform;
            Shapes(swing, "Chain", L4Shapes.Props, m => L4Shapes.Shaft(m, 0, Vector3.zero, new Vector3(0f, -(postH - 0.3f - hangY), 0f), 0.025f, 0.025f, 4, L4Kit.Cell(L4Shapes.IronDark)));
            Basket(swing, "Basket", new Vector3(0f, -(postH - 0.3f - hangY), 0f), 1f);
            var landed = L3Build.Child(t, "Landed", new Vector3(0f, 0.3f, jib), new Vector3(0f, 0f, 70f));
            var tell = L3Build.Child(t, "Tell");
            var sparks = L1Wayfinding.Particles("Sparks", tell.transform, L1Build.FxMat("L1_FX_Ember"), 40, 30f, new Vector2(0.6f, 1.2f), new Vector2(0.5f, 1.5f), new Vector2(0.04f, 0.08f), new Color(1f, 0.75f, 0.4f, 1f), new Color(1f, 0.3f, 0.1f, 0.8f), 0.6f);
            sparks.transform.localPosition = new Vector3(0f, hangY, jib);
            tell.SetActive(false);
            StrikeBox(root, "Strike", new Vector3(0.6f, postH, 0.6f), Vector3.zero);
            var post = L3Build.Child(t, "PostCollider");
            L3Build.StandingBox(post, new Vector3(0.35f, postH, 0.35f));
            post.layer = L3Build.Layer(L3Build.Cover);
            var light = root.AddComponent<HangingLight>();
            L3Build.Set(light, "tuning", Tuning);
            L3Build.Set(light, "swinging", swing);
            L3Build.Set(light, "landed", landed.transform);
            L3Build.Set(light, "tellVisual", tell);
        }

        private static void Corona(GameObject root)
        {
            var t = root.transform;
            const float ringY = 4f, beamY = 8.6f;
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1.4f, 2.6f, 1.4f), new Vector2(0f, 0f), beamY, Charred);
            var swing = L3Build.Child(t, "Swinging", new Vector3(0f, beamY, 0f)).transform;
            Shapes(swing, "Ring", L4Shapes.Props, m =>
            {
                var c = new Vector3(0f, ringY - beamY, 0f);
                const int n = 12;
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                    var p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 1.5f;
                    var p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 1.5f;
                    L4Shapes.Shaft(m, 0, p0, p1, 0.05f, 0.05f, 5, L4Kit.Cell(L4Shapes.IronDark));
                    L4Shapes.Lathe(m, 0, Matrix4x4.Translate(p0 + Vector3.up * 0.05f), new[] { new Vector2(0f, 0f), new Vector2(0.08f, 0.02f), new Vector2(0.1f, 0.12f), new Vector2(0f, 0.1f) }, 6, L4Kit.Cell(L4Shapes.Iron));
                }

                foreach (float a in new[] { 0f, 2.09f, 4.19f })
                {
                    L4Shapes.Shaft(m, 0, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.5f, c + Vector3.up * 1.6f, 0.02f, 0.02f, 4, L4Kit.Cell(L4Shapes.IronDark));
                }

                L4Shapes.Shaft(m, 0, c + Vector3.up * 1.6f, Vector3.zero, 0.03f, 0.03f, 4, L4Kit.Cell(L4Shapes.IronDark));
            });
            for (int i = 0; i < 12; i += 2)
            {
                float a = i * Mathf.PI * 2f / 12f;
                L2Fire.Candle(swing, "Candle", new Vector3(Mathf.Cos(a) * 1.5f, ringY - beamY + 0.22f, Mathf.Sin(a) * 1.5f), i == 0 ? 1.2f : 0f, 5f);
            }

            // The cleat it is made fast to, on a column 3 m to -X, with the rope up to the beam.
            var cleat = L3Build.Child(t, "Cleat", new Vector3(-3f, 1.4f, 0f));
            Shapes(cleat.transform, "Iron", L4Shapes.Props, m =>
            {
                L4Shapes.Box(m, 0, Vector3.zero, Quaternion.identity, new Vector3(0.08f, 0.35f, 0.12f), L4Kit.Cell(L4Shapes.IronDark));
                L4Shapes.Shaft(m, 0, new Vector3(0f, 0.15f, 0f), new Vector3(2.9f, beamY - 1.45f, 0f), 0.025f, 0.025f, 4, L4Kit.Cell(L4Shapes.Rope));
            });
            StrikeBox(cleat, "Strike", new Vector3(0.6f, 1.2f, 0.6f), new Vector3(0f, -0.6f, 0f));
            // Where 'Swinging' (pivot at the beam) ends up: lowered by the ring's drop, so the ring lies on the floor.
            var landed = L3Build.Child(t, "Landed", new Vector3(0f, beamY - ringY + 0.12f, 0f), new Vector3(8f, 0f, 6f));
            var tell = L3Build.Child(t, "Tell");
            var sparks = L1Wayfinding.Particles("Wax", tell.transform, L1Build.FxMat("L1_FX_Ember"), 40, 24f, new Vector2(0.8f, 1.4f), new Vector2(0.1f, 0.4f), new Vector2(0.03f, 0.06f), new Color(1f, 0.8f, 0.5f, 1f), new Color(1f, 0.4f, 0.1f, 0.6f), 1f);
            sparks.transform.localPosition = new Vector3(0f, ringY, 0f);
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;
            tell.SetActive(false);
            var light = root.AddComponent<HangingLight>();
            L3Build.Set(light, "tuning", Tuning);
            L3Build.Set(light, "swinging", swing);
            L3Build.Set(light, "landed", landed.transform);
            L3Build.Set(light, "tellVisual", tell);
        }

        private static void TripodBrazier(GameObject root)
        {
            var t = root.transform;
            var pivot = L3Build.Child(t, "Swinging").transform;
            L3Build.Seat(pivot, "K:SM_Prop_Brazier_01", Vector3.zero, 1.16f, Vector2.zero, 0f, L4Build.Mat("L4_Iron"));
            L2Fire.Fire(pivot, "Flames", new Vector3(0f, 2.12f, 0f), 0.55f, 2f, 7f, true);
            var landed = L3Build.Child(t, "Landed", new Vector3(0f, 0.15f, 0.2f), new Vector3(82f, 0f, 0f));
            var tell = L3Build.Child(t, "Tell");
            var sparks = L1Wayfinding.Particles("Sparks", tell.transform, L1Build.FxMat("L1_FX_Ember"), 40, 30f, new Vector2(0.6f, 1.2f), new Vector2(0.6f, 1.6f), new Vector2(0.04f, 0.08f), new Color(1f, 0.75f, 0.4f, 1f), new Color(1f, 0.3f, 0.1f, 0.8f), 0.6f);
            sparks.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            tell.SetActive(false);
            StrikeBox(root, "Strike", new Vector3(0.8f, 2.2f, 0.8f), Vector3.zero);
            var body = L3Build.Child(t, "Collider");
            L3Build.StandingBox(body, new Vector3(0.5f, 2.2f, 0.5f));
            body.layer = L3Build.Layer(L3Build.Cover);
            var light = root.AddComponent<HangingLight>();
            L3Build.Set(light, "tuning", Tuning);
            L3Build.Set(light, "swinging", pivot);
            L3Build.Set(light, "landed", landed.transform);
            L3Build.Set(light, "tellVisual", tell);
            L3Build.Set(light, "swingDegrees", 6f);
        }

        private static void Portcullis(GameObject root, float span, PortcullisCycle.Mode mode)
        {
            var t = root.transform;
            const float height = 6f;
            var grid = L3Build.Child(t, "Grid").transform;
            int columns = Mathf.Max(2, Mathf.RoundToInt(span / 2.6f));
            float pw = span / columns;
            var iron = L4Build.Mat("L4_Iron");
            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < 2; r++)
                {
                    L3Build.Fit(grid, "K:SM_Bld_Castle_Iron_Gate_01", 0, new Vector3(pw, height * 0.5f, 0.12f), new Vector2(-span * 0.5f + pw * (c + 0.5f), 0f), r * height * 0.5f, iron);
                }
            }

            Shapes(grid, "Spikes", L4Shapes.Props, m =>
            {
                for (float x = -span * 0.5f + 0.2f; x < span * 0.5f; x += 0.42f)
                {
                    L4Shapes.Shaft(m, 0, new Vector3(x, 0.25f, 0f), new Vector3(x, -0.02f, 0f), 0.05f, 0.005f, 4, L4Kit.Cell(L4Shapes.IronDark));
                }

                L4Shapes.Box(m, 0, new Vector3(0f, height - 0.1f, 0f), Quaternion.identity, new Vector3(span, 0.2f, 0.25f), L4Kit.Cell(L4Shapes.IronDark));
            });

            var blocker = L3Build.Child(grid, "Blocker");
            var blockBox = L3Build.StandingBox(blocker, new Vector3(span, height, 0.4f));
            blocker.layer = L3Build.Layer(L3Build.Boundary);

            var slot = L3Build.Child(t, "Slot");
            var slotBox = L3Build.StandingBox(slot, new Vector3(span, 2.5f, 2f), true);

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(span, 3f, 0.6f);
            obstacle.center = new Vector3(0f, 1.5f, 0f);
            obstacle.carving = true;
            obstacle.enabled = false;

            var rattle = L3Build.Child(t, "Rattle");
            var dust = L1Wayfinding.Particles("Dust", rattle.transform, L1Build.FxMat("L1_FX_Smoke"), 80, 40f, new Vector2(0.8f, 1.4f), new Vector2(0f, 0.3f), new Vector2(0.3f, 0.7f), new Color(0.7f, 0.66f, 0.6f, 0.45f), new Color(0.55f, 0.52f, 0.48f, 0.1f), 0.3f);
            dust.transform.localPosition = new Vector3(0f, height + 0.2f, 0f);
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = new Vector3(span, 0.4f, 0.1f);
            rattle.SetActive(false);

            NavAreaVolume(root, "GateSlot", new Vector3(span, 2f, 2f), new Vector3(0f, 1f, 0f));
            var gate = root.AddComponent<PortcullisCycle>();
            L3Build.Set(gate, "tuning", Tuning);
            L3Build.Set(gate, "contact", Contact);
            L3Build.Set(gate, "mode", mode);
            L3Build.Set(gate, "grid", grid);
            L3Build.Set(gate, "raisedHeight", height);
            L3Build.Set(gate, "slot", slotBox);
            L3Build.Set(gate, "blocker", blockBox);
            L3Build.Set(gate, "obstacle", obstacle);
            L3Build.Set(gate, "rattleVisual", rattle);
            // Saved in its starting pose so the editor shows it as the level will: a winch gate down, the others up.
            grid.localPosition = mode == PortcullisCycle.Mode.Winch ? Vector3.zero : new Vector3(0f, height, 0f);
            if (mode == PortcullisCycle.Mode.OneShot)
            {
                var demo = L3Build.Child(t, "DemoTrigger", new Vector3(0f, 0f, -9f));
                demo.layer = L3Build.Layer(L3Build.Marker);
                L3Build.StandingBox(demo, new Vector3(span + 8f, 3f, 4f), true);
                var trigger = demo.AddComponent<DemoTrigger>();
                L3Build.Set(trigger, "playerMask", (LayerMask)(1 << L3Build.Layer("Player")));
                trigger.SetTargets(gate, null);
            }
        }

        private static GameObject Prompt(Transform parent, Vector3 at)
        {
            var go = new GameObject("Prompt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.fontSize = 3f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.88f, 0.72f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            return go;
        }

        private static void WinchPrefab(GameObject root)
        {
            var t = root.transform;
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(0.8f, 0.5f, 0.8f), new Vector2(x, 0f), 0f, Wood);
            }

            var drum = L3Build.Child(t, "Drum", new Vector3(0f, 0.95f, 0f)).transform;
            Shapes(drum, "Barrel", Wood, m =>
            {
                var frame = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
                L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(0f, -0.6f), new Vector2(0.3f, -0.6f), new Vector2(0.3f, -0.52f), new Vector2(0.22f, -0.5f), new Vector2(0.22f, 0.5f), new Vector2(0.3f, 0.52f), new Vector2(0.3f, 0.6f), new Vector2(0f, 0.6f) }, 10, L4Kit.Cell(L2PolyKit.Side));
            });
            Shapes(drum, "Chain", L4Shapes.Props, m =>
            {
                for (float x = -0.45f; x < 0.46f; x += 0.09f)
                {
                    L4Shapes.Lathe(m, 0, Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one), new[] { new Vector2(0.21f, -0.03f), new Vector2(0.25f, -0.03f), new Vector2(0.25f, 0.03f), new Vector2(0.21f, 0.03f) }, 10, L4Kit.Cell(L4Shapes.IronDark));
                }

                L4Shapes.Shaft(m, 0, new Vector3(0.8f, 0f, 0f), new Vector3(0.8f, 0.5f, -0.2f), 0.04f, 0.04f, 5, L4Kit.Cell(L4Shapes.Iron));
                L4Shapes.Shaft(m, 0, new Vector3(0.8f, 0.5f, -0.2f), new Vector3(1.1f, 0.5f, -0.2f), 0.04f, 0.04f, 5, L4Kit.Cell(L4Shapes.Rope));
            });
            var prompt = Prompt(t, new Vector3(0f, 2.2f, -0.3f));
            var winch = root.AddComponent<Winch>();
            L3Build.Set(winch, "drum", drum);
            L3Build.Set(winch, "prompt", prompt.GetComponent<TMPro.TMP_Text>());
        }

        private static void CapstanBay(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(331);
            L4Kit.Courses(t, new Vector3(-2f, 0f, 2f), Vector3.right, Vector3.forward, 1.4f, 3f, rng, 0f, 0.5f);
            L4Kit.Courses(t, new Vector3(0.6f, 0f, 2f), Vector3.right, Vector3.forward, 1.4f, 3f, rng, 0f, 0.5f);
            L3Build.Seat(t, "K:SM_Bld_Castle_Door_01", new Vector3(0f, 180f, 0f), new Vector3(0.36f, 0.85f, 0.7f), new Vector2(0f, 1.85f), 0f, Soot);
            foreach (float x in new[] { -2f, 1.5f })
            {
                L4Kit.Courses(t, new Vector3(x, 0f, -2f), Vector3.forward, Vector3.left, 4f, 3f, rng, 0f, 0.5f);
            }

            var bars = L3Build.Part(t, "K:SM_Bld_Castle_Iron_Gate_01", Vector3.zero, Vector3.zero, new Vector3(1.4f, 1f, 1f), L4Build.Mat("L4_Iron"));
            var bb = L3Build.LocalBounds(bars, t);
            bars.transform.localPosition += new Vector3(-bb.center.x, -bb.min.y, -1.9f - bb.center.z);
            Shapes(t, "Capstan", Wood, m =>
            {
                L4Shapes.Lathe(m, 0, Matrix4x4.identity, new[] { new Vector2(0f, 0f), new Vector2(0.45f, 0f), new Vector2(0.4f, 0.2f), new Vector2(0.32f, 0.25f), new Vector2(0.32f, 1.05f), new Vector2(0.42f, 1.1f), new Vector2(0.42f, 1.25f), new Vector2(0f, 1.25f) }, 10, L4Kit.Cell(L2PolyKit.Side));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f + 0.3f;
                    L4Shapes.Shaft(m, 0, new Vector3(0f, 1.0f, 0f), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.4f + Vector3.up * 1.0f, 0.06f, 0.05f, 6, L4Kit.Cell(L2PolyKit.Top));
                }
            });
            var keeper = L3Build.MeshChild(t, "Gatekeeper", LabourFigure("Soldier_02"), new Vector3(1.0f, 0f, 0.25f), L1Build.Mat("L1_Regiment_Vanguard"));
            keeper.transform.localRotation = Quaternion.Euler(0f, 210f, 0f);
            foreach (var (x, z, sx, sz) in new[] { (-1.75f, 0f, 0.5f, 4f), (1.75f, 0f, 0.5f, 4f), (0f, 1.75f, 4f, 0.5f), (0f, -1.9f, 4f, 0.2f) })
            {
                var c = L3Build.Child(t, "Wall", new Vector3(x, 0f, z));
                L3Build.StandingBox(c, new Vector3(sx, 3f, sz));
            }
        }

        // A standing Synty figure bent over work: back bowed, arms reaching forward and down (the gatekeeper at his
        // capstan, the pour-tender at his cauldron). Baked once and kept as a mesh asset.
        private static Mesh LabourFigure(string body)
        {
            string path = L4Build.MeshesPath + "/Figures/L4_Labour_" + body + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            var baked = L1Bakers.BakeStanding(KnightsCharacters + "Character_" + body + "_Blue.prefab", "Character_" + body, LabourPose, null, out var rig);
            Object.DestroyImmediate(rig);
            L2Build.EnsureFolder(L4Build.MeshesPath + "/Figures");
            AssetDatabase.CreateAsset(baked, path);
            return baked;
        }

        private static void LabourPose(Transform r, float s)
        {
            Transform F(string name)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }

                throw new System.ArgumentException("No bone " + name);
            }

            void Aim(string bone, string child, Vector3 dir)
            {
                var b = F(bone);
                var c = F(child);
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            Aim("Spine_01", "Spine_02", new Vector3(0f, 1f, 0.45f));
            var l = new Vector3(s, 0f, 0f);
            Aim("Shoulder_L", "Elbow_L", l * 0.2f + new Vector3(0f, -0.5f, 1f));
            Aim("Elbow_L", "Hand_L", new Vector3(0f, -0.2f, 1f));
            Aim("Shoulder_R", "Elbow_R", -l * 0.2f + new Vector3(0f, -0.5f, 1f));
            Aim("Elbow_R", "Hand_R", new Vector3(0f, -0.2f, 1f));
            Aim("UpperLeg_L", "LowerLeg_L", new Vector3(0f, -1f, -0.25f));
            Aim("LowerLeg_L", "Ankle_L", new Vector3(0f, -1f, -0.1f));
            Aim("UpperLeg_R", "LowerLeg_R", new Vector3(0f, -1f, 0.3f));
            Aim("LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, 0.05f));
        }

        private static void Scaffold(GameObject root, bool demo)
        {
            var t = root.transform;
            // Everything that falls hangs off a pivot at the wall-side foot (+Z), so it tips forward (-Z).
            var falling = L3Build.Child(t, "Falling", new Vector3(0f, 0f, 1f)).transform;
            var rng = new System.Random(demo ? 341 : 342);
            foreach (float x in new[] { -2.9f, 0f, 2.9f })
            {
                foreach (float z in new[] { -1.9f, -0.1f })
                {
                    L3Build.Seat(falling, "K:SM_Prop_Beam_01", new Vector3(((float)rng.NextDouble() - 0.5f) * 3f, 0f, ((float)rng.NextDouble() - 0.5f) * 3f), new Vector3(0.75f, 2f, 0.75f), new Vector2(x, z - 1f), 0f, Wood);
                }
            }

            foreach (float y in new[] { 2.1f, 4.1f })
            {
                foreach (float z in new[] { -1.9f, -0.1f })
                {
                    L3Build.Seat(falling, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.6f, 2.45f, 0.6f), new Vector2(0f, z - 1f), y - 0.12f, Wood);
                }

                for (int i = 0; i < 5; i++)
                {
                    var p = Plank(falling, new Vector3(6.2f, 0.06f, 0.36f), new Vector3(0f, y + 0.1f, -1.85f + i * 0.4f), Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 1.5f, 0f), rng.Next(100000));
                    p.transform.localPosition -= new Vector3(0f, 0f, 1f);
                }
            }

            L3Build.Seat(falling, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 52f), new Vector3(0.55f, 2.1f, 0.55f), new Vector2(-1.4f, -1.95f), 0.1f, Wood);
            L3Build.Seat(falling, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, -52f), new Vector3(0.55f, 2.1f, 0.55f), new Vector2(1.4f, -1.95f), 2.1f, Wood);

            // The raking strut, 2 m beyond the collapse zone on -Z, leaning in to the top lift.
            var strutGo = L3Build.Child(t, "Strut", new Vector3(0f, 0f, -5f));
            L3Build.Seat(strutGo.transform, "K:SM_Prop_Beam_01", new Vector3(-48f, 0f, 0f), new Vector3(0.9f, 2.5f, 0.9f), new Vector2(0f, 1.7f), 0f, Wood);
            var strutHit = StrikeBox(strutGo, "Strike", new Vector3(0.7f, 2f, 0.9f), new Vector3(0f, 0f, 0.3f));
            var strut = strutHit.AddComponent<ScaffoldStrut>();

            // The chalk marks of the zone (footprint plus 1 R), opaque and flush with the floor, and old fallen planks.
            var chalk = L4Build.Flat("L4_ChalkKerb", "#9E998F", 0.05f);
            Shapes(t, "ChalkCrosses", chalk, m =>
            {
                foreach (var c in new[] { new Vector2(-4.6f, -3.6f), new Vector2(4.6f, -3.6f), new Vector2(-4.6f, 1.6f), new Vector2(4.6f, 1.6f), new Vector2(0f, -3.6f) })
                {
                    L4Shapes.Box(m, 0, new Vector3(c.x, 0.004f, c.y), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.7f, 0.008f, 0.07f), L4Kit.Cell(0));
                    L4Shapes.Box(m, 0, new Vector3(c.x, 0.004f, c.y), Quaternion.Euler(0f, -45f, 0f), new Vector3(0.7f, 0.008f, 0.07f), L4Kit.Cell(0));
                }
            });
            Plank(t, new Vector3(2.4f, 0.05f, 0.35f), new Vector3(-3.6f, 0.03f, -3.1f), Quaternion.Euler(0f, 24f, 0f), 351, true);
            Plank(t, new Vector3(1.8f, 0.05f, 0.35f), new Vector3(3.2f, 0.03f, -3.4f), Quaternion.Euler(0f, -40f, 0f), 352, true);

            var debris = L3Build.Child(t, "Debris");
            for (int i = 0; i < 9; i++)
            {
                Plank(debris.transform, new Vector3(2f + (float)rng.NextDouble() * 3f, 0.06f, 0.36f), new Vector3(((float)rng.NextDouble() - 0.5f) * 7f, 0.05f + i * 0.04f, -2f - (float)rng.NextDouble() * 2.5f), Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 20f, rng.Next(360), ((float)rng.NextDouble() - 0.5f) * 20f), rng.Next(100000), i % 3 == 0);
            }

            debris.SetActive(false);
            if (demo)
            {
                L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(0f, 20f, 0f), 1f, new Vector2(-0.8f, -2.2f), 0f, AdvSoot);
                L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(0f, -10f, 0f), 1f, new Vector2(0.5f, -2.6f), 0f, AdvSoot);
            }

            var creak = L3Build.Child(t, "Creak");
            var dust = L1Wayfinding.Particles("Dust", creak.transform, L1Build.FxMat("L1_FX_Smoke"), 60, 30f, new Vector2(1f, 1.6f), new Vector2(0f, 0.2f), new Vector2(0.3f, 0.6f), new Color(0.72f, 0.68f, 0.6f, 0.4f), new Color(0.6f, 0.56f, 0.5f, 0.1f), 0.25f);
            dust.transform.localPosition = new Vector3(0f, 4.3f, -1f);
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = new Vector3(6f, 0.1f, 2f);
            creak.SetActive(false);

            var standing = L3Build.Child(t, "Standing", new Vector3(0f, 0f, 0f));
            var standingBox = L3Build.StandingBox(standing, new Vector3(6f, 5f, 2f));
            standing.layer = L3Build.Layer(L3Build.Cover);
            var zone = L3Build.Child(t, "Zone", new Vector3(0f, 0f, -1f));
            var zoneBox = L3Build.StandingBox(zone, new Vector3(10f, 3f, 6f), true);
            var deck = L3Build.Child(t, "Deck", new Vector3(0f, 4.1f, 0f));
            var deckBox = L3Build.StandingBox(deck, new Vector3(6f, 1.4f, 2f), true);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(8f, 1.5f, 5f);
            obstacle.center = new Vector3(0f, 0.75f, -2.5f);
            obstacle.carving = true;
            obstacle.enabled = false;
            NavAreaVolume(root, "ScaffoldZone", new Vector3(10f, 2f, 6f), new Vector3(0f, 1f, -1f));

            var collapse = root.AddComponent<ScaffoldCollapse>();
            L3Build.Set(collapse, "tuning", Tuning);
            L3Build.Set(collapse, "contact", Contact);
            L3Build.Set(collapse, "demo", demo);
            L3Build.Set(collapse, "falling", falling);
            L3Build.Set(collapse, "fallEuler", new Vector3(-78f, 0f, 0f));
            L3Build.Set(collapse, "creakVisual", creak);
            L3Build.Set(collapse, "debris", debris);
            L3Build.Set(collapse, "zone", zoneBox);
            L3Build.Set(collapse, "deck", deckBox);
            L3Build.Set(collapse, "standingCollider", standingBox);
            L3Build.Set(collapse, "obstacle", obstacle);
            strut.Scaffold = collapse;

            if (demo)
            {
                var trigger = L3Build.Child(t, "DemoTrigger", new Vector3(0f, 0f, -12f));
                trigger.layer = L3Build.Layer(L3Build.Marker);
                L3Build.StandingBox(trigger, new Vector3(6f, 3f, 3f), true);
                var demoTrigger = trigger.AddComponent<DemoTrigger>();
                L3Build.Set(demoTrigger, "playerMask", (LayerMask)(1 << L3Build.Layer("Player")));
                demoTrigger.SetTargets(null, collapse);
            }
        }

        private static void ChoirLoft(GameObject root)
        {
            var t = root.transform;
            const float floorY = 5f, width = 24f, depth = 4f;
            var falling = L3Build.Child(t, "Falling", new Vector3(0f, 0f, depth * 0.5f)).transform;
            var rng = new System.Random(361);
            for (float x = -width * 0.5f + 1f; x < width * 0.5f; x += 3.9f)
            {
                foreach (float z in new[] { -depth + 0.3f, -0.3f })
                {
                    L3Build.Seat(falling, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(1f, floorY / 2.5f, 1f), new Vector2(x, z), 0f, Charred);
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Plank(falling, new Vector3(width, 0.07f, 0.4f), new Vector3(0f, floorY, -depth + 0.2f + i * 0.4f), Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 0.4f, 0f), rng.Next(100000), i % 4 == 0);
            }

            for (float x = -width * 0.5f + 0.5f; x < width * 0.5f; x += 1.2f)
            {
                L3Build.Seat(falling, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(0.45f, 0.42f, 0.45f), new Vector2(x, -depth + 0.15f), floorY, Wood);
            }

            Plank(falling, new Vector3(width, 0.06f, 0.2f), new Vector3(0f, floorY + 1.05f, -depth + 0.15f), Quaternion.identity, 362);
            for (float x = -width * 0.5f + 2f; x < width * 0.5f; x += 4f)
            {
                L2Fire.Candle(falling, "Lamp", new Vector3(x, floorY + 1.15f, -depth + 0.15f), x < -6f ? 1.4f : 0f, 6f);
            }

            var rngStrut = new System.Random(363);
            var struts = new List<ScaffoldStrut>();
            foreach (float side in new[] { -1f, 1f })
            {
                var strutGo = L3Build.Child(t, "Strut", new Vector3(side * (width * 0.5f - 1f), 0f, -depth - 2f));
                L3Build.Seat(strutGo.transform, "K:SM_Prop_Beam_01", new Vector3(-40f, 0f, 0f), new Vector3(1f, 2.6f, 1f), new Vector2(0f, 1.8f), 0f, Wood);
                var hit = StrikeBox(strutGo, "Strike", new Vector3(0.8f, 2.2f, 0.9f), new Vector3(0f, 0f, 0.3f));
                struts.Add(hit.AddComponent<ScaffoldStrut>());
            }

            var debris = L3Build.Child(t, "Debris");
            for (int i = 0; i < 14; i++)
            {
                Plank(debris.transform, new Vector3(3f + (float)rngStrut.NextDouble() * 4f, 0.07f, 0.4f), new Vector3(((float)rngStrut.NextDouble() - 0.5f) * width, 0.05f + (i % 4) * 0.05f, -depth - (float)rngStrut.NextDouble() * 2f), Quaternion.Euler(((float)rngStrut.NextDouble() - 0.5f) * 25f, rngStrut.Next(-30, 30), ((float)rngStrut.NextDouble() - 0.5f) * 25f), rngStrut.Next(100000), true);
            }

            debris.SetActive(false);
            var creak = L3Build.Child(t, "Creak");
            var dust = L1Wayfinding.Particles("Dust", creak.transform, L1Build.FxMat("L1_FX_Smoke"), 120, 60f, new Vector2(1f, 1.6f), new Vector2(0f, 0.2f), new Vector2(0.3f, 0.6f), new Color(0.72f, 0.68f, 0.6f, 0.4f), new Color(0.6f, 0.56f, 0.5f, 0.1f), 0.25f);
            dust.transform.localPosition = new Vector3(0f, floorY - 0.1f, -depth * 0.5f);
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = new Vector3(width, 0.1f, depth);
            creak.SetActive(false);

            var floor = L3Build.Child(t, "Floor", new Vector3(0f, floorY - 0.1f, -depth * 0.5f));
            var floorBox = L3Build.StandingBox(floor, new Vector3(width, 0.2f, depth));
            floor.layer = L3Build.Layer(L3Build.Walkable);
            var zone = L3Build.Child(t, "Zone", new Vector3(0f, 0f, -depth * 0.5f - 1f));
            var zoneBox = L3Build.StandingBox(zone, new Vector3(width + 4f, 3f, depth + 4f), true);
            var deck = L3Build.Child(t, "Deck", new Vector3(0f, floorY, -depth * 0.5f));
            var deckBox = L3Build.StandingBox(deck, new Vector3(width, 1.4f, depth), true);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(width, 1.5f, 4f);
            obstacle.center = new Vector3(0f, 0.75f, -depth - 1f);
            obstacle.carving = true;
            obstacle.enabled = false;
            NavAreaVolume(root, "ScaffoldZone", new Vector3(width + 4f, 2f, depth + 4f), new Vector3(0f, 1f, -depth * 0.5f - 1f));

            var collapse = root.AddComponent<ScaffoldCollapse>();
            L3Build.Set(collapse, "tuning", Tuning);
            L3Build.Set(collapse, "contact", Contact);
            L3Build.Set(collapse, "falling", falling);
            L3Build.Set(collapse, "fallEuler", new Vector3(-62f, 0f, 0f));
            L3Build.Set(collapse, "creakVisual", creak);
            L3Build.Set(collapse, "debris", debris);
            L3Build.Set(collapse, "zone", zoneBox);
            L3Build.Set(collapse, "deck", deckBox);
            L3Build.Set(collapse, "standingCollider", floorBox);
            L3Build.Set(collapse, "obstacle", obstacle);
            foreach (var s in struts)
            {
                s.Scaffold = collapse;
            }
        }

        private static void Kneeler(GameObject root, bool twin)
        {
            var t = root.transform;
            Nest(root, Corpses + "L1_Corpse_Vanguard_Soldier_01_Kneeling.prefab", Vector3.zero, 0f);
            if (twin)
            {
                Shapes(t, "Brand", L4Build.Flat("L4_TwinBrand", "#2A0D04", 0.1f, "#FF5A14", 2.6f), m =>
                {
                    L4Shapes.Box(m, 0, new Vector3(0f, 0.95f, 0.17f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.12f, 0.12f, 0.02f), L4Kit.Cell(0));
                    L4Shapes.Box(m, 0, new Vector3(0f, 0.95f, 0.17f), Quaternion.identity, new Vector3(0.04f, 0.22f, 0.02f), L4Kit.Cell(0));
                });
            }

            var hit = L3Build.Child(t, "Hit");
            var capsule = hit.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 1.3f;
            capsule.center = new Vector3(0f, 0.65f, 0f);
            capsule.isTrigger = true;
            hit.layer = L3Build.Layer(L3Build.Hittable);
            var spawn = L3Build.Child(t, "SpawnPoint");
            var ambush = root.AddComponent<KneelerAmbush>();
            L3Build.Set(ambush, "tuning", Tuning);
            L3Build.Set(ambush, "spawnPoint", spawn.transform);
        }

        private static void PourTrough(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(371);
            // The trough: a stone channel along x on the yard side of the walk, pitch in it.
            L4Kit.Courses(t, new Vector3(-3f, 0f, -0.75f), Vector3.right, Vector3.back, 6f, 0.45f, rng, 0f, 0.2f);
            L4Kit.Courses(t, new Vector3(-3f, 0f, -0.15f), Vector3.right, Vector3.back, 6f, 0.45f, rng, 0f, 0.2f);
            Shapes(t, "Pitch", L4Shapes.Props, m => L4Shapes.Box(m, 0, new Vector3(0f, 0.3f, -0.45f), Quaternion.identity, new Vector3(6f, 0.04f, 0.42f), L4Kit.Cell(L4Shapes.Pitch)));
            Nest(root, Find("C08").Path, new Vector3(1.6f, 0f, 0.5f), 0f, 0.8f);
            var alive = L3Build.Child(t, "Alive");
            var tender = L3Build.MeshChild(alive.transform, "PourTender", LabourFigure("Soldier_01"), new Vector3(1.4f, 0f, 1.25f), L1Build.Mat("L1_Regiment_Vanguard"));
            tender.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Shapes(alive.transform, "Chain", L4Shapes.Props, m => L4Shapes.Shaft(m, 0, new Vector3(1.4f, 0.12f, 1.3f), new Vector3(-0.6f, 0.03f, 1.6f), 0.025f, 0.025f, 4, L4Kit.Cell(L4Shapes.IronDark)));
            var dead = L3Build.Child(t, "Dead");
            Nest(dead, Corpses + "L1_Corpse_Vanguard_Soldier_01_OnSide.prefab", new Vector3(1.2f, 0f, 1.2f), 80f);
            dead.SetActive(false);
            var hit = L3Build.Child(t, "Hit", new Vector3(1.4f, 0f, 1.25f));
            var capsule = hit.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 1.7f;
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.isTrigger = true;
            hit.layer = L3Build.Layer(L3Build.Hittable);
            var c = L3Build.Child(t, "TroughCollider", new Vector3(0f, 0f, -0.45f));
            L3Build.StandingBox(c, new Vector3(6f, 0.45f, 0.8f));
            var tenderComponent = root.AddComponent<PourTender>();
            L3Build.Set(tenderComponent, "aliveVisual", alive);
            L3Build.Set(tenderComponent, "deadVisual", dead);
        }

        private static void BreachEdge(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(381);
            // Merlon stumps on the lip, tilted and broken.
            L4Kit.MerlonRun(t, new Vector3(-1f, 0f, 0.15f), Vector3.right, Vector3.forward, 1, 382, 0f, -5f, null, L4Kit.Merlon.Gone, false);
            var stump = L3Build.Child(t, "Stump", new Vector3(0.4f, 0f, -0.1f), new Vector3(4f, 0f, 7f)).transform;
            L4Kit.Courses(stump, new Vector3(-0.3f, 0.85f, 0.25f), Vector3.right, Vector3.forward, 0.7f, 0.45f, rng, 0.5f);
            // The buffer: loose stones and blocks across the 3 m inward of the lip, nothing to trip on (no colliders).
            for (int i = 0; i < 7; i++)
            {
                var at = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.9f, 0f, -0.4f - (float)rng.NextDouble() * 2.5f);
                L4Kit.Fallen(t, at, rng).transform.localScale = Vector3.one * (0.6f + (float)rng.NextDouble() * 0.5f);
            }

            var rocks = new[] { "A:SM_Env_Rock_07", "A:SM_Env_Rock_014", "A:SM_Env_Rock_015", "A:SM_Env_Rock_016" };
            for (int i = 0; i < 6; i++)
            {
                L3Build.Seat(t, rocks[rng.Next(rocks.Length)], new Vector3(0f, rng.Next(360), 0f), 0.3f + (float)rng.NextDouble() * 0.25f, new Vector2(((float)rng.NextDouble() - 0.5f) * 1.9f, -0.3f - (float)rng.NextDouble() * 2.6f), -0.03f, AdvSoot);
            }

            // Smoke and embers rising from the ravine below, and the glow.
            var smoke = L1Wayfinding.Particles("Smoke", t, L1Build.FxMat("L1_FX_Smoke"), 40, 6f, new Vector2(3f, 5f), new Vector2(0.6f, 1.2f), new Vector2(1.2f, 2.4f), new Color(0.4f, 0.37f, 0.34f, 0.35f), new Color(0.55f, 0.52f, 0.48f, 0.05f), -0.05f);
            smoke.transform.localPosition = new Vector3(0f, -2f, 1.5f);
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var embers = L1Wayfinding.Particles("Embers", t, L1Build.FxMat("L1_FX_Ember"), 20, 5f, new Vector2(2f, 3f), new Vector2(0.8f, 1.5f), new Vector2(0.03f, 0.06f), new Color(1f, 0.6f, 0.25f, 1f), new Color(1f, 0.3f, 0.1f, 0.6f), -0.1f);
            embers.transform.localPosition = new Vector3(0f, -1.5f, 1.5f);
            embers.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var glow = L3Build.Child(t, "Glow", new Vector3(0f, -1.5f, 1.8f)).AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = L1Build.Hex("#FF7A2E");
            glow.intensity = 1.2f;
            glow.range = 5f;
            glow.shadows = LightShadows.None;

            var guard = L3Build.Child(t, "EdgeGuard");
            var edge = guard.AddComponent<EdgeGuard>();
            L3Build.Set(edge, "length", 2f);
            var kill = L3Build.Child(t, "Kill", new Vector3(0f, -6f, 2f));
            L3Build.StandingBox(kill, new Vector3(2f, 5f, 2f), true);
            kill.layer = L3Build.Layer(L3Build.Hazard);
            var ditch = kill.AddComponent<DitchKill>();
            L3Build.Set(ditch, "contact", Contact);
            NavAreaVolume(root, "EdgeBuffer", new Vector3(2f, 2f, 3f), new Vector3(0f, 1f, -1.5f));
            // The terrace floor under the buffer is the level's; the breach has no parapet collider on its lip.
        }
    }
}
