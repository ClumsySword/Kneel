using System.Collections.Generic;
using Kneel.Hazards;
using Kneel.Markers;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The structures (14.5), markers (14.6) and enemy stand-ins (14.7) of the L4 manifest. Structures are built
    // from the L4 kit prefabs, nested, so a change to the kit carries through. Origins: a structure's origin is
    // the floor it stands on (T0 for ground buildings, T+1 for the guardroom and the towers) at its centre or
    // at the face named in its notes; up-screen is +Z.
    public static partial class L4Assets
    {
        private const string L3Markers = "Assets/Kneel/Prefabs/L3/Markers/";

        private static GameObject Kit(GameObject root, string id, Vector3 position, float yaw, Vector3? scale = null)
        {
            var go = Nest(root, Find(id).Path, position, yaw);
            if (scale.HasValue)
            {
                go.transform.localScale = scale.Value;
            }

            return go;
        }

        private static void StructureRows(AddRow Add)
        {
            const string st = "14.5 Structures";
            string wall = L3Build.Boundary;
            Add("S01", "L4_OuterGatehouse", "Structures", st, 44f, 28f, 9f, Col.None, wall, "BUILT", "L4 kit (W08b, W07, W05, W01); PolygonKnights SM_Bld_Castle_Wall_01, beams",
                "Origin: the centre of the passage's outer (south) face. The 12 m outer arch over the west 12 m of the 16 m passage under a gate keep to 16 m (machicolations, spired bartizans, banners), braziers at the gate, flanking towers 13 m either side of the arch raised to 18 m with fires on top (refinement: symmetric, the old ram breach gone), 7 m passage walls running 24 m north to the rubble wall with its 2 R gap (where the Husk stands). The roof is burnt off: charred beams across the top. Arch and towers fade when they hide the player.",
                OuterGatehouse);
            Add("S02", "L4_Guardroom", "Structures", st, 17f, 9.4f, 6.3f, Col.None, wall, "BUILT", "L4 kit (W04, W03a); L4Kit gate arches, flags, merlons; PolygonKnights SM_Bld_Castle_Wall_01, Wall_Gate_01; L4 boards; PolygonAdventure barrels, crate; C21, F01b",
                "Shrine A's room over the inner gatehouse, at T+1 (origin on its floor). A stone room: cut wall on the camera side (south); curtain walls east and west, 4.5 m, each with a pack gateway (2.2 m opening; east from the wall-walk, west to the west wall-walk); on the north an arcade of four round arches on square piers, a 0.9 m rail across each bay, open over the courtyard. A flagged stone roof with merlons that fades when it hides the player. Inside: the guards' table and benches (one knocked over), barrels and a crate, the weapon rack, a dead guard.",
                Guardroom);
            Add("S03", "L4_TowerNE", "Structures", st, 10f, 13f, 6.5f, Col.None, wall, "BUILT", "PolygonKnights SM_Bld_Castle_Wall_01, Wall_Gate_01; L4Kit merlons, flags, coursed stair turret, spiral steps",
                "At T+1 (origin on its floor). Doorways south (from the terrace) and west (to the north walk); in the north face a broken doorway into a stair turret behind the wall (an octagon of coursed stone), a 1 R spiral up to the roof (+5 m, the V3b lookout). Walls 5 m, so the doorways clear the player; broken merlons. Fades when it hides the player.",
                root => Tower(root, true));
            Add("S04", "L4_TowerNW", "Structures", st, 10f, 10f, 6.5f, Col.None, wall, "BUILT", "As S03, with the postern (M06a)",
                "At T+1. The postern in its south doorway (barred inside: opens from the tower), doorways east (from the north walk) and north (to the stair down to the inner ward). Fades when it hides the player.",
                root => Tower(root, false));
            Add("S05", "L4_Chapel", "Structures", st, 28f, 32f, 14f, Col.None, wall, "BUILT", "PolygonKnights SM_Bld_Castle_Wall_01 (walls, pilasters, front), Wall_Gate_01, Castle_Tower_Base_01/01/02/Top_01 (x2.05), beams; L4Kit arches, merlons, flags, steps; generated arrow slits, piers, tables (L2 planks); C21; banners",
                "The garrison hall (was the chapel; owner, 2026-10-19). Origin: the centre of the hall floor. Open to the sky. Side walls 7 m with flat pilasters and arrow slits, the west door halfway up the west aisle. Two rows of square stone piers every 4 m under heavy timber beams (6 R hall, 3 R aisles). North: the dais a step up with the high table under the master's banner. South (camera side): a full front with merlons round a castle gate arch, which fades. A square castle tower at the north-east corner. Trestle tables and benches where the pews stood, weapon racks, stores; the crypt grille at the north end of the east aisle.",
                Chapel);
            Add("S06", "L4_Crypt", "Structures", st, 8f, 12f, 3f, Col.None, wall, "BUILT", "L4Kit stone steps and courses; L2Fire candle",
                "Origin: the top of its stair (floor level). Twelve steps down 3 m behind a half-bricked opening, then a low vault 8 x 6 m lit by one candle (the noble's stash, D1).",
                Crypt);
            Add("S07", "L4_InnerGate", "Structures", st, 12f, 16f, 7f, Col.None, wall, "BUILT", "L4 kit (W08a x2, H04a, H01 x2, H05b, W10); PolygonKnights walls, iron gate",
                "Origin: the centre of the passage floor. The 4 R passage between 7 m walls: the south arch with its wrecked portcullis jammed up, the north arch with the cycling portcullis, two murder holes in the vault fired by the gate's down phase, the capstan bay behind bars in the east wall. The vault fades when it hides the player.",
                InnerGate);
            Add("S08", "L4_GrandStair", "Structures", st, 18f, 10f, 5f, Col.None, L3Build.Walkable, "BUILT", "L4 kit (W09b); L4Kit courses (cheeks, balustrade)",
                "Origin: the foot of the stair, centre. The 16 m grand stair from the forecourt to the Kneeling Court (T+1), cheek walls each side and balustrades at the landing's ends.",
                GrandStair);
            Add("S09", "L4_ThroneDoors", "Structures", st, 21.2f, 6f, 17.2f, Col.None, wall, "BUILT", "L4Kit gate arches (three orders), merlons, flags, steps; PolygonKnights SM_Bld_Castle_Door_01 (x2.5), Castle_Tower_01/02/04 and Tower_Top_01 (x1.6), SM_Prop_Banner_01 (x2), SM_Prop_Brazier_01; L2Fire",
                "Origin: the threshold, centre. The throne-hall doors, 8.4 m across (the note's inset), shut in a portal of three recessed round-arched orders between two square towers with royal banners and cressets. Two steps up to a 0.4 m dais; a great brazier on a stone plinth either side (brazier gold at the end). Belongs under the L4L5_Threshold root.",
                ThroneDoors);
            Add("S10", "L4_PrisonCells", "Structures", st, 14f, 8f, 3f, Col.None, wall, "BUILT", "L4Kit courses; PolygonKnights iron gates; L2 planks; F06; TextMeshPro scratching",
                "Origin: the lane's mouth, centre (the lane runs toward -X). A 2 R lane, roofed and unlit, three cells on its north side behind iron gates; the gatewarden in the last, his scratching on its wall.",
                PrisonCells);
            Add("S11", "L4_Gallows", "Structures", st, 10f, 9f, 6f, Col.None, L3Build.Walkable, "BUILT", "PolygonKnights beams; L2 planks; L4Kit steps; F03a x3, F03b",
                "Origin: the platform's centre on the ground. A 10 x 8 m platform at +2 m on posts, steps up its south side, a gallows frame with four nooses: three of the condemned and the tribunal's captain on the last. The crossbowman's perch.",
                Gallows);
            Add("S12", "L4_CausewaySpan", "Structures", st, 9f, 10f, 13f, Col.None, L3Build.Walkable, "BUILT", "L4Kit flags, gate arch (as a bridge arch); L4 kit (W03)",
                "Origin: the deck's centre. Ten metres of the causeway, 8 m wide between 0.9 m parapets, on a stone arch over the dry ravine 12 m below. Repeat it along the approach.",
                CausewaySpan);
            Add("S13", "L4_KeepSpire", "Structures", st, 40f, 30f, 70f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonKnights tower pieces and spire (x3), Church_Room_01 (x2.2)",
                "The level landmark ~40 m beyond the doors: a 70 m keep of stacked tower storeys under a spire, the throne hall's roofs in front of it. Backdrop: no collider.",
                KeepSpire);
            Add("S14", "L4_Cloister", "Structures", st, 14f, 2f, 4.6f, Col.None, wall, "BUILT", "L4Kit gate arches (as arcade bays), fallen blocks",
                "Origin: the arcade's centre. Four bays of a broken arcade, one fallen in. Placed along the cloister walk.",
                Cloister);
        }

        private static void MarkerRows(AddRow Add)
        {
            const string mk = "14.6 Markers";
            string none = L3Build.Unlayered;
            Add("M01", "L4_Shrine", "Markers", mk, 4.3f, 2.7f, 3.9f, Col.None, none, "FOUND", "L3_Shrine (the L1 checkpoint shrine)", "Shrine A and B.", root => Nest(root, L3Markers + "L3_Shrine.prefab", Vector3.zero, 0f));
            Add("M02", "L4_SecretTell", "Markers", mk, 0.3f, 0.05f, 0.3f, Col.None, none, "FOUND", "L3_SecretTell (the scorched handprint)", "One tell for the whole game (decision 7).", root => Nest(root, L3Markers + "L3_SecretTell.prefab", Vector3.zero, 0f));
            Add("M03", "L4_Pickup_HeavyWeapon", "Markers", mk, 2f, 0.4f, 0.3f, Col.None, none, "ADAPTED", "PolygonKnights SM_Wep_Broadsword_01 (x2.1); PickupMarker",
                "The gate captain's heavy blade (decision 6), lying in the sun with a faint glint. Unlocks Heavy Attack (Q) once the player has the verb.", root => Pickup(root, PickupKind.HeavyWeapon, "HeavyWeapon", "K:SM_Wep_Broadsword_01", new Vector3(90f, 0f, 30f), 2.1f));
            Add("M04", "L4_Pickup_Key", "Markers", mk, 0.3f, 0.1f, 0.15f, Col.None, none, "BUILT", "Generated iron key; PickupMarker",
                "The tribunal key (decision 15), on the courtyard Husk's belt; it opens the rampart grille (M06b) through KeyRing.", TribunalKey);
            Add("M05a", "L4_Pickup_Consumable", "Markers", mk, 0.7f, 0.5f, 0.5f, Col.None, none, "FOUND", "L3_Pickup_Consumable", "Healing consumable (D3, D2, D5, D6).", root => Nest(root, L3Markers + "L3_Pickup_Consumable.prefab", Vector3.zero, 0f));
            Add("M05b", "L4_Pickup_Lore", "Markers", mk, 0.8f, 0.5f, 0.2f, Col.None, none, "FOUND", "L3_Pickup_Lore", "Lore fragments (D3, D1, D4, D6; D2's is the scout's account).", root => Nest(root, L3Markers + "L3_Pickup_Lore.prefab", Vector3.zero, 0f));
            Add("M05c", "L4_Pickup_Cordial", "Markers", mk, 0.5f, 0.5f, 0.8f, Col.None, none, "ADAPTED", "PolygonAdventure SM_Item_Potion_04; PickupMarker",
                "The steward's cordial (D1): refills every healing charge once.", root => Pickup(root, PickupKind.Cordial, "StewardsCordial", "A:SM_Item_Potion_04", Vector3.zero, 1.6f));
            Add("M06a", "L4_Postern", "Markers", mk, 3f, 1f, 3f, Col.Custom, L3Build.Boundary, "BUILT", "Kneel.Hazards.OneTimeGate (Barred); PolygonKnights SM_Bld_Castle_Door_01, beam",
                "The shortcut (z): a door that opens only from its barred side (+Z, the tower). From -Z: \"Barred from the other side\". Opens once for good.", root => Gate(root, OneTimeGate.Lock.Barred));
            Add("M06b", "L4_KeyGrille", "Markers", mk, 3f, 0.4f, 3f, Col.Custom, L3Build.Boundary, "BUILT", "Kneel.Hazards.OneTimeGate (Key); PolygonKnights SM_Bld_Castle_Iron_Gate_01",
                "The rampart stair's grille (Y): opens with the tribunal key, from either side; lifts and stays up.", root => Gate(root, OneTimeGate.Lock.Key));
            Add("M07", "L4_VistaTrigger", "Markers", mk, 8f, 6f, 4f, Col.None, none, "FOUND", "L3_VistaTrigger", "V1-V5, V3b, VZ; their framings are set per placement.", root => Nest(root, L3Markers + "L3_VistaTrigger.prefab", Vector3.zero, 0f));
            Add("M08", "L4_ArenaCamVolume", "Markers", mk, 8f, 8f, 6f, Col.None, none, "FOUND", "L3_ArenaCamVolume", "Sized to each arena when placed.", root => Nest(root, L3Markers + "L3_ArenaCamVolume.prefab", Vector3.zero, 0f));
            Add("M09", "L4_EncounterLock", "Markers", mk, 0.5f, 0.5f, 0.5f, Col.None, none, "BUILT", "Kneel.Markers.EncounterMarker", "An encounter's framing and lock (section 7). Data only.",
                root => root.AddComponent<EncounterMarker>());
            Add("M10a", "L4_PlayerSpawn", "Markers", mk, 1f, 1f, 1f, Col.None, none, "FOUND", "L3_PlayerSpawn", "S (60, 0).", root => Nest(root, L3Markers + "L3_PlayerSpawn.prefab", Vector3.zero, 0f));
            Add("M10b", "L4_ExitTrigger", "Markers", mk, 4f, 4f, 3f, Col.None, none, "FOUND", "L3_ExitTrigger", "X, the throne-hall doors.", root => Nest(root, L3Markers + "L3_ExitTrigger.prefab", Vector3.zero, 0f));
            Add("M11", "L4_RoofHider", "Markers", mk, 8f, 8f, 4f, Col.None, L3Build.Marker, "BUILT", "Kneel.Markers.RoofHiderMarker", "A volume listing the roofs that switch to shadows only while the player is inside. Data only.",
                root =>
                {
                    L3Build.StandingBox(root, new Vector3(8f, 4f, 8f), true);
                    root.AddComponent<RoofHiderMarker>();
                });
            Add("M12", "L4_SpawnMarker", "Markers", mk, 1f, 1f, 1f, Col.None, none, "FOUND", "L3_SpawnMarker", "Every enemy in section 6.", root => Nest(root, L3Markers + "L3_SpawnMarker.prefab", Vector3.zero, 0f));
            Add("M13", "L4_DemoTrigger", "Markers", mk, 6f, 4f, 3f, Col.None, L3Build.Marker, "BUILT", "Kneel.Hazards.DemoTrigger", "Plays a one-time demo (portcullis, scaffold) the first time the player walks in; link its target when placed.",
                root =>
                {
                    L3Build.StandingBox(root, new Vector3(6f, 3f, 4f), true);
                    var demo = root.AddComponent<DemoTrigger>();
                    L3Build.Set(demo, "playerMask", (LayerMask)(1 << L3Build.Layer("Player")));
                });
        }

        private static void EnemyRows(AddRow Add, List<Item> list)
        {
            const string en = "14.7 Enemies";
            string hit = L3Build.Hittable;
            list.Add(new Item { Id = "N01", Name = "AshenFootman", Folder = "", Table = en, Size = new Vector3(1f, 2f, 1f), Collider = Col.None, Layer = "Default", Status = "FOUND", Source = "Assets/Enemies/AshenFootman/AshenFootman.prefab", Notes = "The existing enemy, used as it is; shown with its AI switched off in the review.", ExistingPath = "Assets/Enemies/AshenFootman/AshenFootman.prefab" });
            Add("N02", "L4_StandIn_IronHusk", "Enemies", en, 1.2f, 1.2f, 2.2f, Col.Custom, hit, "BUILT", "PolygonKnights Character_Knight_02 baked standing (x1.15), enemy black iron; generated plates",
                "No Husk in the project. An armoured man a head taller than the player, black iron, three heavy plates marked on shoulders and chest (the armour that breaks to heavies). Capsule on the Enemy layer. Carries its SpawnMarker.",
                root => StandIn(root, "Knight_02", "L1_Regiment_Enemy", EnemyType.IronHusk, 1.15f, 0.6f, Pose.Stand, true, false));
            Add("N03", "L4_StandIn_CursedTwin", "Enemies", en, 1f, 1f, 1.9f, Col.Custom, hit, "BUILT", "PolygonKnights Character_Soldier_02 baked standing; generated ember brand",
                "One of the Twins: gate-guard colours, the shared ember brand on the chest. Capsule on the Enemy layer; SpawnMarker.",
                root => StandIn(root, "Soldier_02", "L1_Regiment_Vanguard", EnemyType.CursedTwin, 1f, 0.45f, Pose.Stand, false, true));
            Add("N04", "L4_StandIn_Crossbowman", "Enemies", en, 1f, 1f, 1.9f, Col.Custom, hit, "BUILT", "PolygonKnights Character_Soldier_01 baked aiming; generated crossbow; Kneel.Hazards.OnScreenGate",
                "The Hollow Archer with a crossbow (decision 4), aiming. Carries the OnScreenGate (H11). Capsule; SpawnMarker.",
                root => StandIn(root, "Soldier_01", "L1_Regiment_Enemy", EnemyType.Crossbowman, 1f, 0.45f, Pose.Aim, false, false));
            Add("N05", "L4_StandIn_Fool", "Enemies", en, 1f, 1f, 1.9f, Col.Custom, hit, "BUILT", "PolygonKnights Character_Soldier_02 baked standing; SM_Wep_Broadsword_01 (x1.5)",
                "The Fool with his long blade. Capsule; SpawnMarker.",
                root => StandIn(root, "Soldier_02", "L1_Regiment_Enemy", EnemyType.Fool, 1f, 0.45f, Pose.Stand, false, false, "K:SM_Wep_Broadsword_01"));
            list.Add(new Item { Id = "N06a", Name = "L3_StandIn_Hound", Folder = "", Table = en, Size = new Vector3(0.8f, 0.8f, 1.4f), Collider = Col.None, Layer = hit, Status = "FOUND", Source = "L3 stand-in", Notes = "L3's hound stand-in.", ExistingPath = "Assets/Kneel/Prefabs/L3/Enemies/L3_StandIn_Hound.prefab" });
            list.Add(new Item { Id = "N06b", Name = "L3_StandIn_Brute", Folder = "", Table = en, Size = new Vector3(2f, 2.6f, 2f), Collider = Col.None, Layer = hit, Status = "FOUND", Source = "L3 stand-in", Notes = "L3's brute stand-in.", ExistingPath = "Assets/Kneel/Prefabs/L3/Enemies/L3_StandIn_Brute.prefab" });
            Add("N07", "L4_Steward", "Enemies", en, 1f, 1f, 1.8f, Col.None, L3Build.Unlayered, "BUILT", "PolygonKnights Character_Soldier_01 baked bent over work",
                "The steward feeding the braziers on the Kneeling Court (vignette 7): harmless, ignores the player. (The gatekeeper and the pour-tender are inside H05b and H10.)",
                root =>
                {
                    var figure = L3Build.MeshChild(root.transform, "Figure", LabourFigure("Soldier_01"), Vector3.zero, L1Build.Mat("L1_Regiment_Vanguard"));
                    var marker = root.AddComponent<SpawnMarker>();
                    marker.enemyType = EnemyType.Steward;
                    marker.initialState = "Feeding the braziers";
                    figure.name = "Steward";
                });
        }

        // ---------------------------------------------------------------- Structures

        private static void Fade(GameObject go)
        {
            L4Build.BakeLooseMeshes(go, go.transform.root.name + "_" + go.name);
            L4Build.AddFade(go);
        }

        private static void WallRun(Transform parent, Vector3 start, Vector3 dir, float length, float height, float thickness, Material mat)
        {
            // Pack curtain pieces about 6 m long, closing with a shorter one, laid along 'dir' (x or z).
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
            float piece = length / n;
            bool alongZ = Mathf.Abs(dir.z) > 0.5f;
            for (int i = 0; i < n; i++)
            {
                var c = start + dir * (piece * (i + 0.5f));
                L3Build.Fit(parent, "K:SM_Bld_Castle_Wall_01", alongZ ? 90 : 0, alongZ ? new Vector3(thickness, height, piece) : new Vector3(piece, height, thickness), new Vector2(c.x, c.z), start.y, mat);
            }

            var col = L3Build.Child(parent, "WallCollider", start + dir * length * 0.5f);
            L3Build.StandingBox(col, alongZ ? new Vector3(thickness, height, length) : new Vector3(length, height, thickness));
        }

        private static void OuterGatehouse(GameObject root)
        {
            var t = root.transform;
            var arch = Kit(root, "W08b", new Vector3(-2f, 0f, 0f), 0f);
            var west = L3Build.Child(t, "WestWall").transform;
            WallRun(west, new Vector3(-9f, 0f, 1.2f), Vector3.forward, 25.8f, 7f, 2f, Stone);
            var east = L3Build.Child(t, "EastWall").transform;
            WallRun(east, new Vector3(9f, 0f, 7f), Vector3.forward, 20f, 7f, 2f, Stone);
            // (Refinement: the ram breach east of the arch is gone, so the front can stand symmetric about the
            // arch; the ram broke the portcullis instead.)
            var rng = new System.Random(401);

            // Flanking towers, 13 m either side of the arch's centre (x -2), raised from 9 m to 18 m (owner,
            // 2026-10-19: "it is not symmetrical. Make the side towers taller as well"): two more storeys, merlons
            // and a deck on top, where the fires burn. The upper storeys fade with the towers.
            foreach (float cx in new[] { -15f, 11f })
            {
                Kit(root, "W07", new Vector3(cx, 0f, 3f), 0f);
                var top = L3Build.Child(t, "TowerTop", new Vector3(cx, 0f, 3f)).transform;
                for (int side = 0; side < 4; side++)
                {
                    Vector2 dir = side == 0 ? Vector2.down : side == 1 ? Vector2.left : side == 2 ? Vector2.up : Vector2.right;
                    Vector2 at = dir * 3.9f;
                    bool ns = side % 2 == 0;
                    foreach (float y0 in new[] { 9f, 13.5f })
                    {
                        L3Build.Fit(top, "K:SM_Bld_Castle_Wall_01", ns ? 0 : 90, ns ? new Vector3(10f, 4.5f, 2.2f) : new Vector3(2.2f, 4.5f, 10f), at, y0, Stone);
                    }
                }

                L4Kit.MerlonRun(top, new Vector3(-5f, 18f, -5f), Vector3.right, Vector3.back, 5, 406, 0.3f, 0f, null, null, false);
                L4Kit.MerlonRun(top, new Vector3(5f, 18f, -5f), Vector3.forward, Vector3.right, 5, 407, 0.3f, 0f, null, null, false);
                L4Kit.MerlonRun(top, new Vector3(5f, 18f, 5f), Vector3.left, Vector3.forward, 5, 408, 0.3f, 0f, null, null, false);
                L4Kit.MerlonRun(top, new Vector3(-5f, 18f, 5f), Vector3.back, Vector3.left, 5, 409, 0.3f, 0f, null, null, false);
                L4Kit.Flags(L3Build.Child(top, "Deck", new Vector3(0f, 18f, 0f)).transform, new Vector2(7.8f, 7.8f), 0.35f, 410);
                L3Build.Part(top, "K:SM_Bld_Castle_Flagpole_01", new Vector3(cx < 0f ? -3.6f : 3.6f, 18f, 3.6f), Vector3.zero, Vector3.one * 1.3f, Soot);
                L3Build.Part(top, "K:SM_Bld_Castle_Flag_01", new Vector3(cx < 0f ? -3.6f : 3.6f, 18f + 2.55f * 1.3f, 3.6f), new Vector3(0f, cx < 0f ? 180f : 0f, 0f), Vector3.one * 1.3f, L4Citadel.BannerRed);
                L4Citadel.HangBanner(top, new Vector3(0f, 17.4f, -5.02f), 0f, 2.6f, cx < 0f ? 2 : 0);
                Fade(top.gameObject);
            }
            // The burnt roof: charred beams across the top of the passage, one fallen in.
            foreach (float z in new[] { 6f, 12f, 18f })
            {
                L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1.6f, 7.4f, 1.6f), new Vector2(0f, z), 6.6f, Charred);
            }

            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(18f, 30f, 72f), new Vector3(1.6f, 6f, 1.6f), new Vector2(-2f, 15f), 0f, Charred);
            // The inner end of the passage (plan y 84-86): a tall wall broken through by a 2 R gap, rubble at its
            // jambs. Tall, so the Husk's gap is the only way on (rule bend 10).
            Kit(root, "W01a", new Vector3(-5f, 0f, 28f), 0f);
            Kit(root, "W01a", new Vector3(5f, 0f, 28f), 0f);
            for (int i = 0; i < 6; i++)
            {
                L4Kit.Fallen(t, new Vector3((i % 2 == 0 ? -1f : 1f) * (1.5f + (float)rng.NextDouble() * 0.6f), 0f, 26.6f + (float)rng.NextDouble() * 2.8f), rng);
            }

            // The gate keep (owner, 2026-10-19: "I also want the initial gate to the stronghold to feel grand"): a
            // storey of wall over the arch up to 16 m, merlons along its top, three machicolations over the gate,
            // square bartizans with spires and the master's flags at its corners, two great banners down its piers;
            // braziers either side of the gate. It fades with the arch when it hides the player.
            var keep = L3Build.Child(t, "GateKeep").transform;
            WallRun(keep, new Vector3(-10.2f, 9f, 0f), Vector3.right, 16.4f, 7f, 2.6f, Stone);
            L4Kit.MerlonRun(keep, new Vector3(-10f, 16f, -1.3f), Vector3.right, Vector3.back, 8, 403, 0.25f, 0f, null, null, false);
            L4Kit.MerlonRun(keep, new Vector3(6f, 16f, 1.3f), Vector3.left, Vector3.forward, 8, 404, 0.25f, 0f, null, null, false);
            foreach (float x in new[] { -6f, -2f, 2f })
            {
                Kit(root, "W10", new Vector3(x, 15.2f, -1.3f), 0f).transform.SetParent(keep, true);
            }

            // The bartizans stand on the gate wall's top over the piers, inside its thickness (they hung off its
            // corners in the first try and read as floating).
            foreach (float x in new[] { -9f, 5f })
            {
                L3Build.Fit(keep, "K:SM_Bld_Castle_Wall_01", 90, new Vector3(2.6f, 4.6f, 2.6f), new Vector2(x, 0f), 16f, Stone);
                L4Kit.MerlonRun(keep, new Vector3(x - 1.3f, 20.6f, -1.3f), Vector3.right, Vector3.back, 1, 405 + (int)x, 0f, 0f, null, null, false);
                var spire = L3Build.Seat(keep, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(1f, 1.35f, 1f), new Vector2(x, 0f), 20.5f, Soot);
                float tip = L3Build.LocalBounds(spire, keep).max.y;
                L3Build.Part(keep, "K:SM_Bld_Castle_Flagpole_01", new Vector3(x, tip - 0.7f, 0f), Vector3.zero, Vector3.one * 1.1f, Soot);
                L3Build.Part(keep, "K:SM_Bld_Castle_Flag_01", new Vector3(x, tip - 0.7f + 2.55f * 1.1f, 0f), new Vector3(0f, x < 0f ? 180f : 0f, 0f), Vector3.one * 1.1f, L4Citadel.BannerRed);
            }

            foreach (float x in new[] { -9.1f, 5.1f })
            {
                L4Citadel.HangBanner(keep, new Vector3(x, 15.4f, -1.4f), 0f, 2.4f, x < 0f ? 0 : 1);
            }

            Fade(keep.gameObject);

            var braziers = L3Build.Child(t, "GateBraziers").transform;
            foreach (float x in new[] { -12.2f, 8.2f })
            {
                var bowl = L3Build.Seat(braziers, "K:SM_Prop_Brazier_01", Vector3.zero, 1.8f, new Vector2(x, -3.6f), 0f, Soot);
                var bb = L3Build.LocalBounds(bowl, braziers);
                L2Fire.Fire(braziers, "GateFire", new Vector3(bb.center.x, bb.max.y - 0.45f, bb.center.z), 1.2f, 3.5f, 11f, true);
                var col = L3Build.Child(braziers, "BrazierCollider", new Vector3(x, 0f, -3.6f));
                L3Build.StandingBox(col, new Vector3(1.3f, 1.8f, 1.3f));
                col.layer = L3Build.Layer(L3Build.Boundary);
            }

            Fade(arch);
        }

        // Shrine A's guardroom over the inner gatehouse, at T+1 (origin on its floor, at the room's centre). A stone
        // room: curtain walls east and west, each with a real doorway (east from the wall-walk, west to the west
        // wall-walk); the camera-side south wall cut low; on the north an arcade of round arches on square piers,
        // a rail across each bay, open over the courtyard; a flagged roof with merlons that fades when it hides the
        // player. Inside, what the guards left: their table and benches (one knocked over), barrels and a crate by
        // the east door, the weapon rack, and one of them, dead.
        private static void Guardroom(GameObject root)
        {
            var t = root.transform;
            const float h = 4.5f, w = 16f, d = 9f;
            var rng = new System.Random(411);
            for (int i = 0; i < 8; i++)
            {
                Kit(root, "W04", new Vector3(-7f + i * 2f, 0f, -4f), 0f);
            }

            var walls = L3Build.Child(t, "Walls").transform;
            foreach (var (x, door) in new[] { (-w * 0.5f, 0.6f), (w * 0.5f, -1.2f) })
            {
                // The pack gateway is 5.4 m across with a 2.2 m opening; the curtain either side of it.
                WallRun(walls, new Vector3(x, 0f, -d * 0.5f), Vector3.forward, door - 2.7f + d * 0.5f, h, 1f, Stone);
                L3Build.Fit(walls, "K:SM_Bld_Castle_Wall_Gate_01", 90, new Vector3(1f, h, 5.4f), new Vector2(x, door), 0f, Stone);
                WallRun(walls, new Vector3(x, 0f, door + 2.7f), Vector3.forward, d * 0.5f - door - 2.7f, h, 1f, Stone);
                foreach (float s in new[] { -1f, 1f })
                {
                    L3Build.StandingBox(L3Build.Child(walls, "JambCollider", new Vector3(x, 0f, door + s * 1.9f)), new Vector3(1f, h, 1.6f));
                }
            }

            // The north arcade: four round-arched bays between the end walls, a rail across each but the second,
            // where Shrine A's stair goes down into the courtyard (plan x 56-60).
            var arcade = L3Build.Child(t, "Arcade", new Vector3(0f, 0f, d * 0.5f - 0.5f)).transform;
            for (int i = 0; i < 4; i++)
            {
                float x = -5.625f + i * 3.75f;
                var bay = L3Build.Child(arcade, "Bay", new Vector3(x, 0f, 0f)).transform;
                L4Kit.GateArch(bay, 2.6f, 0.575f, 2.4f, 1.3f, h, 1f, 412 + i);
                if (i != 1)
                {
                    Kit(root, "W03a", new Vector3(x, 0f, d * 0.5f - 0.5f), 0f, new Vector3(1.3f, 1f, 1f));
                }

                // The piers and the wall over the arch; the opening stays clear.
                foreach (float side in new[] { -1f, 1f })
                {
                    L3Build.StandingBox(L3Build.Child(arcade, "PierCollider", new Vector3(x + side * 1.5875f, 0f, 0f)), new Vector3(0.575f, h, 1f));
                }

                L3Build.StandingBox(L3Build.Child(arcade, "SpandrelCollider", new Vector3(x, 2.4f, 0f)), new Vector3(2.6f, h - 2.4f, 1f));
            }

            // The roof: flags over the whole room, merlons round its edge.
            var roof = L3Build.Child(t, "Roof").transform;
            var deck = L3Build.Child(roof, "Deck", new Vector3(0f, h, 0f)).transform;
            L4Kit.Flags(deck, new Vector2(w + 1f, d + 0.4f), 0.35f, 416);
            L4Kit.MerlonRun(roof, new Vector3(-8f, h, -d * 0.5f - 0.2f), Vector3.right, Vector3.back, 8, 417, 0.35f, 0f, null, null, false);
            L4Kit.MerlonRun(roof, new Vector3(8f, h, d * 0.5f + 0.2f), Vector3.left, Vector3.forward, 8, 418, 0.2f, 0f, null, null, false);
            L4Kit.MerlonRun(roof, new Vector3(w * 0.5f + 0.5f, h, -4f), Vector3.forward, Vector3.right, 4, 419, 0.2f, 0f, null, null, false);
            L4Kit.MerlonRun(roof, new Vector3(-w * 0.5f - 0.5f, h, 4f), Vector3.back, Vector3.left, 4, 420, 0.2f, 0f, null, null, false);
            Fade(roof.gameObject);

            // The guards' table and benches.
            var furniture = L3Build.Child(t, "Furniture").transform;
            var table = L3Build.Child(furniture, "Table", new Vector3(-3.2f, 0f, 0.4f), new Vector3(0f, 6f, 0f)).transform;
            Board(table, new Vector3(2.4f, 0.07f, 0.9f), new Vector3(0f, 0.78f, 0f), Quaternion.identity, rng.Next(100000), false);
            foreach (var (lx, lz) in new[] { (-1.05f, -0.35f), (1.05f, -0.35f), (-1.05f, 0.35f), (1.05f, 0.35f) })
            {
                Board(table, new Vector3(0.09f, 0.74f, 0.09f), new Vector3(lx, 0.37f, lz), Quaternion.identity, rng.Next(100000), false);
            }

            L3Build.StandingBox(table.gameObject, new Vector3(2.4f, 0.82f, 0.9f));
            void Bench(Transform parent)
            {
                Board(parent, new Vector3(2.2f, 0.06f, 0.32f), new Vector3(0f, 0.44f, 0f), Quaternion.identity, rng.Next(100000), false);
                foreach (float lx in new[] { -0.85f, 0.85f })
                {
                    Board(parent, new Vector3(0.06f, 0.41f, 0.28f), new Vector3(lx, 0.205f, 0f), Quaternion.identity, rng.Next(100000), false);
                }
            }

            Bench(L3Build.Child(furniture, "Bench", new Vector3(-3.25f, 0f, -0.45f), new Vector3(0f, 4f, 0f)).transform);
            Bench(L3Build.Child(furniture, "BenchOver", new Vector3(-2.9f, 0.16f, 1.75f), new Vector3(-90f, 12f, 0f)).transform);
            L3Build.Seat(furniture, "A:SM_Prop_Barrel_01", new Vector3(0f, 20f, 0f), 0.95f, new Vector2(6.7f, -3.2f), 0f, AdvStone);
            L3Build.Seat(furniture, "A:SM_Prop_Barrel_01", new Vector3(0f, 75f, 0f), 0.95f, new Vector2(6.0f, -3.35f), 0f, AdvStone);
            L3Build.Seat(furniture, "A:SM_Prop_Barrel_01", new Vector3(90f, 0f, 35f), 0.9f, new Vector2(4.9f, -2.9f), 0f, AdvSoot);
            L3Build.Seat(furniture, "A:SM_Prop_Crate_01", new Vector3(0f, 15f, 0f), 0.85f, new Vector2(6.6f, 2.2f), 0f, AdvStone);
            Nest(root, Find("C21").Path, new Vector3(-6.95f, 0f, 3f), 90f);
            Nest(root, Find("F01b").Path, new Vector3(1.4f, 0f, -1.6f), 150f);

            var hider = Nest(root, Find("M11").Path, Vector3.zero, 0f);
            hider.transform.localScale = new Vector3(2f, 1f, 1.1f);
        }

        // How far the NE stair turret rises above the tower's roof (refinement).
        private const float Crown = 2.8f;

        private static void Tower(GameObject root, bool northEast)
        {
            var t = root.transform;
            var shell = L3Build.Child(t, "Shell").transform;
            // 5 m, so the pack gateway's arch clears the player (scaled to 4 m it left 1.75 m under the arch).
            const float h = 5f;
            // Faces (S, W, N, E): two 5 m pieces each, one of them a doorway (the pack gate wall) where the level
            // needs it; the number is the piece (0 or 1), -1 for none. NE: S onto the terrace, W onto the north walk,
            // N the broken doorway into the stair turret. NW: S the postern, E onto the north walk, N to the stair
            // down into the west lane.
            int[] door = northEast ? new[] { 0, 1, 1, -1 } : new[] { 0, -1, 1, 0 };
            for (int side = 0; side < 4; side++)
            {
                Vector3 n = side == 0 ? Vector3.back : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.right;
                Vector3 along = new Vector3(-n.z, 0f, n.x);
                bool ns = side % 2 == 0;
                int yaw = ns ? 0 : 90;
                var centre = n * 4.4f;
                for (int k = 0; k < 2; k++)
                {
                    var c = centre + along * (k == 0 ? -2.5f : 2.5f);
                    bool isDoor = door[side] == k;
                    string key = isDoor ? "K:SM_Bld_Castle_Wall_Gate_01" : "K:SM_Bld_Castle_Wall_01";
                    L3Build.Fit(shell, key, yaw, ns ? new Vector3(5f, h, 1.2f) : new Vector3(1.2f, h, 5f), new Vector2(c.x, c.z), 0f, Stone);

                    // Colliders follow the pieces: a doorway leaves its 2 m opening clear between two jambs.
                    foreach (float j in isDoor ? new[] { -1.75f, 1.75f } : new[] { 0f })
                    {
                        float len = isDoor ? 1.5f : 5f;
                        var jc = c + along * j;
                        var wall = L3Build.Child(t, "WallCollider", new Vector3(jc.x, 0f, jc.z));
                        L3Build.StandingBox(wall, ns ? new Vector3(len, h, 1.2f) : new Vector3(1.2f, h, len));
                    }
                }
            }

            L4Kit.MerlonRun(shell, new Vector3(-5f, h, -5f), Vector3.right, Vector3.back, 5, 421, 0.55f, 0f, null, null, false);
            L4Kit.MerlonRun(shell, new Vector3(5f, h, -5f), Vector3.forward, Vector3.right, 5, 422, 0.55f, 0f, null, null, false);
            // NE: the north merlons stop short of the stair turret, where the spiral comes out onto the roof.
            L4Kit.MerlonRun(shell, new Vector3(5f, h, 5f), Vector3.left, Vector3.forward, northEast ? 3 : 5, 423, 0.55f, 0f, null, null, false);
            L4Kit.MerlonRun(shell, new Vector3(-5f, h, 5f), Vector3.back, Vector3.left, 5, 424, 0.55f, 0f, null, null, false);
            var roofDeck = L3Build.Child(shell, "RoofDeck", new Vector3(0f, h, 0f)).transform;
            L4Kit.Flags(roofDeck, new Vector2(8.6f, 8.6f), 0.35f, northEast ? 425 : 426);
            // The roof is walkable (D2's lookout); the wall colliders carry it at the edges.
            L3Build.StandingBox(L3Build.Child(t, "RoofCollider", new Vector3(0f, h - 0.35f, 0f)), new Vector3(8.8f, 0.35f, 8.8f));
            if (northEast)
            {
                // The stair turret behind the north wall, through the broken doorway (rubble either side, the
                // handprint on the jamb): an octagon of coursed stone open to the tower on its south side and at the
                // top, a newel and a spiral of 20 treads, one full turn, up to the wall-walk and the roof.
                var turret = L3Build.Child(t, "StairTurret", new Vector3(-2.5f, 0f, 6.6f)).transform;
                var rng = new System.Random(427);
                const float r = 1.75f, wallDepth = 0.5f;
                float side = 2f * r * Mathf.Sin(22.5f * Mathf.Deg2Rad);
                for (int i = 1; i < 8; i++)
                {
                    float th = (-90f + i * 45f) * Mathf.Deg2Rad;
                    var o = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    var dir = new Vector3(o.z, 0f, -o.x);
                    float a0 = th + 22.5f * Mathf.Deg2Rad;
                    var start = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
                    L4Kit.Courses(turret, start, dir, o, side, h, rng, 0f, wallDepth);
                    // The turret runs a storey above the roof (refinement): the spiral comes out on the open south side.
                    L4Kit.Courses(turret, start + Vector3.up * h, dir, o, side, Crown, rng, 0f, wallDepth);
                    var col = L3Build.Child(turret, "WallCollider", o * (r * Mathf.Cos(22.5f * Mathf.Deg2Rad) - wallDepth * 0.5f));
                    col.transform.localRotation = Quaternion.LookRotation(o, Vector3.up);
                    L3Build.StandingBox(col, new Vector3(side + 0.1f, h + Crown, wallDepth));
                }

                L3Build.MeshChild(turret, "Newel", L2PolyKit.Tread(new Vector3(0.3f, h, 0.3f), rng.Next(100000)), new Vector3(0f, h * 0.5f, 0f), L4Kit.Stone);
                for (int i = 0; i < 20; i++)
                {
                    float a = (-72f + i * 18f) * Mathf.Deg2Rad;
                    var step = L3Build.MeshChild(turret, "Step", L2PolyKit.Tread(new Vector3(1f, 0.25f, 0.55f), rng.Next(100000)), new Vector3(Mathf.Cos(a) * 0.62f, 0.125f + i * 0.25f, Mathf.Sin(a) * 0.62f), L4Kit.Stone);
                    step.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    step.AddComponent<BoxCollider>().size = new Vector3(1f, 0.25f, 0.55f);
                }

                // The top landing over the doorway, level with the wall-walk, so the last turn steps straight out.
                var landing = L3Build.MeshChild(turret, "Landing", L2PolyKit.Tread(new Vector3(1.4f, 0.25f, 0.95f), rng.Next(100000)), new Vector3(0f, h - 0.125f, -1.2f), L4Kit.Stone);
                landing.AddComponent<BoxCollider>().size = new Vector3(1.4f, 0.25f, 0.95f);

                // Its spire (owner, 2026-10-19: "We need one for the tower (a spire)"), a corbelled ring under it and a
                // red pennant at the tip.
                L4Kit.Courses(turret, new Vector3(-r - 0.25f, h + Crown - 0.35f, -r - 0.25f), Vector3.right, Vector3.back, 2f * r + 0.5f, 0.35f, rng, 0f, 0.3f);
                var spire = L3Build.Seat(turret, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(1.4f, 1.45f, 1.4f), Vector2.zero, h + Crown - 0.05f, Soot);
                float tip = L3Build.LocalBounds(spire, turret).max.y;
                L3Build.Part(turret, "K:SM_Bld_Castle_Flagpole_01", new Vector3(0f, tip - 0.6f, 0f), Vector3.zero, Vector3.one * 0.8f, Soot);
                L3Build.Part(turret, "K:SM_Bld_Castle_Flag_01", new Vector3(0f, tip - 0.6f + 2.55f * 0.8f, 0f), new Vector3(0f, 200f, 0f), Vector3.one * 0.8f, L4Citadel.BannerRed);

                L4Kit.Fallen(t, new Vector3(-3.9f, 0f, 3.2f), rng);
                L4Kit.Fallen(t, new Vector3(-0.4f, 0.1f, 3f), rng);
                var tell = Nest(root, Find("M02").Path, new Vector3(-1.1f, 0f, 3.75f), 180f);
                tell.name = "SecretTell_D2";
            }
            else
            {
                var postern = Nest(root, Find("M06a").Path, new Vector3(-2.5f, 0f, -4.4f), 0f);
                postern.name = "Postern";
            }

            Fade(shell.gameObject);
        }

        // The garrison hall (origin: the centre of the hall floor). It was a chapel; the owner's review (2026-10-19:
        // "I do not like the Chapel. It does not feel like it belongs in the stronghold") made it the stronghold's
        // own hall, where the garrison ate, armed and slept, on the same plan so the E5 fight is unchanged:
        // castle-stone walls 7 m high with flat pilasters outside and arrow slits; the west door halfway up the west
        // aisle; two rows of square stone piers every 4 m under heavy timber beams (a 6 R hall between 3 R aisles);
        // north, the dais, a step up, with the high table under the master's banner; south (camera side), a full
        // gatehouse front with merlons round a castle gate arch, which fades when it hides the player. A square
        // castle tower at the north-east corner, smoke pouring from its top. Inside: trestle tables and benches
        // knocked about, weapon racks, stores. Open to the sky: its burnt roof read as floating and came off.
        private static void Chapel(GameObject root)
        {
            var t = root.transform;
            const float h = 7f, w = 24f, d = 28f, outer = w * 0.5f + 0.6f;
            var rng = new System.Random(431);
            var walls = L3Build.Child(t, "Walls").transform;
            WallRun(walls, new Vector3(w * 0.5f, 0f, -d * 0.5f), Vector3.forward, d, h, 1.2f, Stone);
            WallRun(walls, new Vector3(-w * 0.5f, 0f, -d * 0.5f), Vector3.forward, d * 0.5f + 1.3f, h, 1.2f, Stone);
            L3Build.Fit(walls, "K:SM_Bld_Castle_Wall_Gate_01", 90, new Vector3(1.2f, h, 5.4f), new Vector2(-w * 0.5f, 4f), 0f, Stone);
            WallRun(walls, new Vector3(-w * 0.5f, 0f, 6.7f), Vector3.forward, d * 0.5f - 6.7f, h, 1.2f, Stone);
            WallRun(walls, new Vector3(-w * 0.5f, 0f, d * 0.5f), Vector3.right, w, h + 1.5f, 1.2f, Stone);
            foreach (float s in new[] { -1f, 1f })
            {
                L3Build.StandingBox(L3Build.Child(walls, "JambCollider", new Vector3(-w * 0.5f, 0f, 4f + s * 1.9f)), new Vector3(1.2f, h, 1.6f));
            }

            // Flat pilasters up the long walls, full height, between the slits.
            var pilasters = L3Build.Child(t, "Pilasters").transform;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -12f, -6f, 0f, 6f, 12f })
                {
                    if (side < 0 && z == 6f)
                    {
                        continue;   // the west door
                    }

                    L3Build.Fit(pilasters, "K:SM_Bld_Castle_Wall_01", 90, new Vector3(1f, h, 1.4f), new Vector2(side * (outer + 0.45f), z), 0f, Stone);
                }
            }

            // Arrow slits through the long walls (both faces) and either side of the dais: a dark slot in a stone frame.
            Vector2 sideUV = L4Kit.Cell(L2PolyKit.Side), topUV = L4Kit.Cell(L2PolyKit.Top), darkUV = L4Kit.Cell(L2PolyKit.TopDark), jointUV = L4Kit.Cell(L2PolyKit.Joint);
            Shapes(walls, "ArrowSlits", L4Kit.Stone, m =>
            {
                void Slit(Vector3 at, Vector3 outward)
                {
                    var rot = Quaternion.LookRotation(outward);
                    L4Shapes.Box(m, 0, at, rot, new Vector3(0.18f, 1.7f, 0.06f), jointUV);
                    L4Shapes.Box(m, 0, at + Vector3.up * 0.95f, rot, new Vector3(0.62f, 0.2f, 0.1f), topUV);
                    L4Shapes.Box(m, 0, at - Vector3.up * 0.95f, rot, new Vector3(0.62f, 0.18f, 0.12f), darkUV);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        L4Shapes.Box(m, 0, at + rot * new Vector3(s * 0.22f, 0f, 0f), rot, new Vector3(0.22f, 1.7f, 0.08f), sideUV);
                    }
                }

                foreach (float z in new[] { -9f, -3f, 3f, 9f })
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (side < 0 && z == 3f)
                        {
                            continue;
                        }

                        Slit(new Vector3(side * (w * 0.5f - 0.6f - 0.02f), 3.4f, z), new Vector3(-side, 0f, 0f));
                        Slit(new Vector3(side * (w * 0.5f + 0.6f + 0.02f), 4.4f, z), new Vector3(side, 0f, 0f));
                    }
                }

                foreach (float x in new[] { -4.5f, 4.5f })
                {
                    Slit(new Vector3(x, 3.6f, d * 0.5f - 0.62f), Vector3.back);
                }
            });

            // The south front: a full wall either side of the gate arch, a lintel over it, merlons along the top; the
            // whole front fades while it hides the player (it was cut to 0.9 m, which read wrong).
            var front = L3Build.Child(t, "Front").transform;
            const float gate = 2.5f;
            WallRun(front, new Vector3(-w * 0.5f - 0.6f, 0f, -d * 0.5f), Vector3.right, w * 0.5f + 0.6f - gate, h, 1.2f, Stone);
            WallRun(front, new Vector3(gate, 0f, -d * 0.5f), Vector3.right, w * 0.5f + 0.6f - gate, h, 1.2f, Stone);
            var portal = L3Build.Child(front, "Portal", new Vector3(0f, 0f, -d * 0.5f)).transform;
            L4Kit.GateArch(portal, 3.4f, 0.8f, 3.8f, 1.7f, 6.4f, 1.2f, 432);
            // The arch's piers as solid boxes, so the NavMesh and the player agree on the opening (the fade probes
            // alone stop the player but are not baked).
            foreach (float s in new[] { -1f, 1f })
            {
                var pier = L3Build.Child(portal, "PierCollider", new Vector3(s * 2.1f, 0f, 0f));
                L3Build.StandingBox(pier, new Vector3(0.8f, 6.4f, 1.2f));
                pier.layer = L3Build.Layer(L3Build.Boundary);
            }

            L3Build.Fit(front, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(gate * 2f, h - 6.4f, 1.2f), new Vector2(0f, -d * 0.5f), 6.4f, Stone);
            L4Kit.MerlonRun(front, new Vector3(-12f, h, -d * 0.5f - 0.6f), Vector3.right, Vector3.back, 12, 433, 0.35f, 0f, null, null, false);
            L3Citadel_Banner(front, new Vector3(-6.5f, h - 0.4f, -d * 0.5f - 0.66f), 1.6f, 0);
            L3Citadel_Banner(front, new Vector3(6.5f, h - 0.4f, -d * 0.5f - 0.66f), 1.6f, 1);
            Fade(front.gameObject);

            // Square stone piers on plinths, under heavy timber beams that run the length of the hall.
            var columns = L3Build.Child(t, "Columns").transform;
            const float capital = 4.6f, abacus = 0.18f;
            Shapes(columns, "Piers", L4Kit.Stone, m =>
            {
                foreach (float x in new[] { -6f, 6f })
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var at = new Vector3(x, 0f, -10f + i * 4f);
                        L4Shapes.Box(m, 0, at + new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(1.4f, 0.3f, 1.4f), darkUV);
                        L4Shapes.Box(m, 0, at + new Vector3(0f, (capital - 0.3f) * 0.5f + 0.3f, 0f), Quaternion.identity, new Vector3(0.95f, capital - 0.3f, 0.95f), sideUV);
                        L4Shapes.Box(m, 0, at + new Vector3(0f, capital + abacus * 0.5f, 0f), Quaternion.identity, new Vector3(1.3f, abacus, 1.3f), topUV);
                    }
                }
            });
            const float beamLength = 2.49f;
            foreach (float x in new[] { -6f, 6f })
            {
                for (int i = 0; i < 6; i++)
                {
                    L3Build.StandingCylinder(L3Build.Child(columns, "Collider", new Vector3(x, 0f, -10f + i * 4f)), 0.9f, capital);
                }

                L3Build.Seat(columns, "K:SM_Prop_Beam_01", new Vector3(90f, 0f, 0f), new Vector3(2.4f, 22f / beamLength, 2.4f), new Vector2(x, 0f), capital + abacus, Charred);
                // Braces from each pier up to the beam.
                for (int i = 0; i < 6; i++)
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        if ((i == 0 && s < 0) || (i == 5 && s > 0))
                        {
                            continue;
                        }

                        L3Build.Seat(columns, "K:SM_Prop_Beam_01", new Vector3(s * 45f, 0f, 0f), new Vector3(1.2f, 1.4f / beamLength, 1.2f), new Vector2(x, -10f + i * 4f + s * 0.75f), capital - 0.75f, Charred);
                    }
                }
            }

            // The dais: a step up across the hall to the high table, the master's banner on the wall behind.
            var chancel = L3Build.Child(t, "Chancel", new Vector3(0f, 0.36f, 11.6f)).transform;
            L4Kit.Flags(chancel, new Vector2(11.2f, 3.6f), 0.36f, 445);
            for (int i = 0; i < 8; i++)
            {
                L3Build.MeshChild(chancel, "Step", L2PolyKit.Tread(new Vector3(1.4f, 0.18f, 0.45f), rng.Next(100000)), new Vector3(-4.9f + i * 1.4f, -0.27f, -2.02f), L4Kit.Stone);
            }

            L3Build.StandingBox(L3Build.Child(t, "ChancelCollider", new Vector3(0f, 0f, 11.6f)), new Vector3(11.2f, 0.36f, 3.6f));
            L3Build.StandingBox(L3Build.Child(t, "StepCollider", new Vector3(0f, 0f, 9.58f)), new Vector3(11.2f, 0.18f, 0.45f));
            HallTable(chancel, new Vector3(0f, 0f, 0.4f), 0f, 3.6f, rng);
            L3Citadel_Banner(walls, new Vector3(0f, h - 0.2f, d * 0.5f - 0.66f), 2f, 2);

            // The tower at the north-east corner: a square castle tower in three stages, merlons on top.
            var belfry = L3Build.Child(t, "Belfry", new Vector3(w * 0.5f + 1.5f, 0f, d * 0.5f + 1.5f)).transform;
            float top = 0f;
            foreach (var key in new[] { "K:SM_Bld_Castle_Tower_Base_01", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_Top_01" })
            {
                var piece = L3Build.Seat(belfry, key, Vector3.zero, 2.05f, Vector2.zero, top, Stone);
                top = L3Build.LocalBounds(piece, belfry).max.y;
            }

            L3Build.StandingBox(L3Build.Child(belfry, "Collider"), new Vector3(6.4f, 7.2f, 6.4f));

            // The roof is gone (owner, 2026-10-19, on the ruined roof: "floating elements, just remove the roof"):
            // open to the sky like the rest of the yards; what came down lies in the hall.
            // Two rafters that came down into the hall.
            const float beam = 2.49f;
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(12f, 64f, 80f), new Vector3(1.5f, 11f / beam, 1.5f), new Vector2(1.5f, 2f), 0f, Charred);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(-30f, 20f, 70f), new Vector3(1.3f, 6f / beam, 1.3f), new Vector2(3.6f, 0.2f), 0f, Charred);
            for (int i = 0; i < 6; i++)
            {
                L4Kit.Fallen(t, new Vector3(0.5f + (float)rng.NextDouble() * 3f, 0f, -0.5f + (float)rng.NextDouble() * 3.4f), rng);
            }

            // Trestle tables and benches where the pews stood (the same cover), some knocked about.
            var furniture = L3Build.Child(t, "Furniture").transform;
            var tables = new[] { (-2.3f, -9f, 90f), (2.3f, -9f, 90f), (-2.3f, -5.5f, 94f), (2.4f, -5.3f, 81f), (-2.1f, -2.6f, 113f), (-3.6f, 5.5f, 161f) };
            foreach (var (x, z, yaw) in tables)
            {
                HallTable(furniture, new Vector3(x, 0f, z), yaw, 3f, rng);
            }

            // Weapon racks and stores along the aisle walls.
            foreach (var (x, z, yaw) in new[] { (11.05f, -11f, -90f), (11.05f, 0f, -90f), (-11.05f, -11f, 90f), (-11.05f, -1f, 90f) })
            {
                Nest(root, Find("C21").Path, new Vector3(x, 0f, z), yaw);
            }

            L3Build.Seat(furniture, "A:SM_Prop_Barrel_01", new Vector3(0f, 20f, 0f), 0.95f, new Vector2(10.6f, 5.6f), 0f, AdvSoot);
            L3Build.Seat(furniture, "A:SM_Prop_Barrel_01", new Vector3(0f, 75f, 0f), 0.95f, new Vector2(10.5f, 4.6f), 0f, AdvSoot);
            L3Build.Seat(furniture, "A:SM_Prop_Crate_01", new Vector3(0f, 15f, 0f), 0.9f, new Vector2(-10.5f, -6.4f), 0f, AdvSoot);
            L3Build.Seat(furniture, "A:SM_Prop_Crate_02", new Vector3(0f, -8f, 0f), 0.9f, new Vector2(-10.4f, -7.6f), 0f, AdvSoot);

            // Banners on the aisle walls between the slits.
            foreach (float z in new[] { -6f, 6f })
            {
                L3Citadel_Banner(walls, new Vector3(w * 0.5f - 0.66f, h - 0.5f, z), 1.4f, z > 0f ? 1 : 0, -90f);
            }

            L3Citadel_Banner(walls, new Vector3(-w * 0.5f + 0.66f, h - 0.5f, -6f), 1.4f, 2, 90f);

            // The crypt grille at the north end of the east aisle, swung open against the aisle wall: the stair
            // down (S06) starts behind it.
            var grille = L3Build.Fit(t, "K:SM_Bld_Castle_Iron_Gate_01", 0, new Vector3(2.2f, 2.4f, 0.12f), new Vector2(9f, 8.4f), 0f, L4Build.Mat("L4_Iron"));
            grille.transform.localRotation = Quaternion.Euler(0f, 96f, 0f) * grille.transform.localRotation;
            var gb = L3Build.LocalBounds(grille, t);
            grille.transform.localPosition += new Vector3(7.95f - gb.min.x, 0f, 8.4f - gb.max.z);
        }

        // A hanging banner in the master's crimson, its top edge at 'top', facing 'yaw' (0: toward -Z).
        private static void L3Citadel_Banner(Transform parent, Vector3 top, float scale, int variant, float yaw = 0f)
        {
            L4Citadel.HangBanner(parent, top, yaw, scale, variant);
        }

        // A trestle table, length along local x, with a bench either side (one sometimes knocked over). Collider on
        // the table only (the benches are low).
        private static void HallTable(Transform parent, Vector3 at, float yaw, float length, System.Random rng)
        {
            var table = L3Build.Child(parent, "Table", at, new Vector3(0f, yaw, 0f)).transform;
            Board(table, new Vector3(length, 0.07f, 0.9f), new Vector3(0f, 0.78f, 0f), Quaternion.identity, rng.Next(100000), false);
            foreach (float lx in new[] { -length * 0.5f + 0.35f, length * 0.5f - 0.35f })
            {
                Board(table, new Vector3(0.09f, 0.74f, 0.7f), new Vector3(lx, 0.37f, 0f), Quaternion.identity, rng.Next(100000), false);
            }

            L3Build.StandingBox(table.gameObject, new Vector3(length, 0.82f, 0.9f));
            foreach (float s in new[] { -1f, 1f })
            {
                bool over = rng.NextDouble() < 0.3;
                var bench = L3Build.Child(table, "Bench", new Vector3(0f, over ? 0.16f : 0f, s * (over ? 1f : 0.75f)), new Vector3(over ? s * 90f : 0f, ((float)rng.NextDouble() - 0.5f) * 10f, 0f)).transform;
                Board(bench, new Vector3(length - 0.3f, 0.06f, 0.32f), new Vector3(0f, 0.44f, 0f), Quaternion.identity, rng.Next(100000), false);
                foreach (float lx in new[] { -length * 0.5f + 0.4f, length * 0.5f - 0.4f })
                {
                    Board(bench, new Vector3(0.06f, 0.41f, 0.28f), new Vector3(lx, 0.205f, 0f), Quaternion.identity, rng.Next(100000), false);
                }
            }
        }

        private static void Crypt(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(441);
            for (int i = 0; i < 12; i++)
            {
                L3Build.MeshChild(t, "Step", L2PolyKit.Tread(new Vector3(2f, 0.25f, 0.42f), rng.Next(100000)), new Vector3(0f, -0.125f - i * 0.25f, 0.21f + i * 0.42f), L4Kit.Stone);
            }

            foreach (float x in new[] { -1.25f, 1.25f })
            {
                var cheek = L3Build.Child(t, "Cheek", new Vector3(x, -3f, 0f)).transform;
                L4Kit.Courses(cheek, new Vector3(-0.25f, 0f, 0f), Vector3.forward, Vector3.left, 5f, 3.2f, rng, 0f, 0.5f);
            }

            // Half-bricked across the top of the stair.
            L4Kit.Courses(t, new Vector3(-1f, 0f, 0.1f), Vector3.right, Vector3.back, 1.1f, 1.35f, rng, 0.4f, 0.4f);
            var vault = L3Build.Child(t, "Vault", new Vector3(0f, -3f, 8f)).transform;
            foreach (var (x0, z0, dir, len) in new[] { (-4f, -3f, Vector3.right, 8f), (-4f, 3f, Vector3.right, 8f) })
            {
                L4Kit.Courses(vault, new Vector3(x0, 0f, z0), dir, Vector3.back, len, 2.8f, rng, 0f, 0.5f);
            }

            foreach (float x in new[] { -4f, 3.5f })
            {
                L4Kit.Courses(vault, new Vector3(x, 0f, -3f), Vector3.forward, Vector3.left, 6f, 2.8f, rng, 0f, 0.5f);
            }

            var floor = L3Build.Child(vault, "Floor").transform;
            L4Kit.Flags(floor, new Vector2(7.5f, 5.5f), 0.3f, 442);
            var ceiling = L3Build.Child(vault, "Ceiling", new Vector3(0f, 3.1f, 0f)).transform;
            L4Kit.Flags(ceiling, new Vector2(8f, 6.5f), 0.3f, 443);
            L2Fire.Candle(vault, "Candle", new Vector3(2.6f, 0.9f, 2.2f), 1.6f, 7f);
            Fade(ceiling.gameObject);
            var col = L3Build.Child(t, "FloorCollider", new Vector3(0f, -3.3f, 8f));
            L3Build.StandingBox(col, new Vector3(8f, 0.3f, 6f));
            col.layer = L3Build.Layer(L3Build.Walkable);
            var ramp = L3Build.Child(t, "Ramp", new Vector3(0f, -1.6f, 2.5f), new Vector3(Mathf.Atan2(3f, 5f) * Mathf.Rad2Deg, 0f, 0f));
            var rb = ramp.AddComponent<BoxCollider>();
            rb.size = new Vector3(2f, 0.3f, 5.9f);
            ramp.layer = L3Build.Layer(L3Build.Walkable);
        }

        private static void InnerGate(GameObject root)
        {
            var t = root.transform;
            Kit(root, "W08a", new Vector3(0f, 0f, -7f), 0f);
            Kit(root, "W08a", new Vector3(0f, 0f, 7f), 0f);
            var west = L3Build.Child(t, "WestWall").transform;
            WallRun(west, new Vector3(-5f, 0f, -5.8f), Vector3.forward, 11.6f, 7f, 2f, Stone);
            var east = L3Build.Child(t, "EastWall").transform;
            WallRun(east, new Vector3(5f, 0f, -5.8f), Vector3.forward, 6.8f, 7f, 2f, Stone);
            WallRun(east, new Vector3(5f, 0f, 5f), Vector3.forward, 0.8f, 7f, 2f, Stone);
            // The wrecked south portcullis, jammed up in its slot and bent.
            var wreck = L3Build.Child(t, "WreckedPortcullis", new Vector3(0f, 5.2f, -7f), new Vector3(8f, 0f, 3f)).transform;
            for (int c = 0; c < 3; c++)
            {
                L3Build.Fit(wreck, "K:SM_Bld_Castle_Iron_Gate_01", 0, new Vector3(2.66f, 1.6f, 0.12f), new Vector2(-2.66f + c * 2.66f, 0f), c == 2 ? -0.6f : 0f, L4Build.Mat("L4_Iron"));
            }

            var gate = Kit(root, "H04a", new Vector3(0f, 0f, 7f), 0f);
            var holeA = Kit(root, "H01", new Vector3(0f, 0f, -2f), 0f);
            var holeB = Kit(root, "H01", new Vector3(0f, 0f, 2f), 0f);
            foreach (var hole in new[] { holeA, holeB })
            {
                L3Build.Set(hole.GetComponent<MurderHole>(), "schedule", MurderHole.Schedule.External);
            }

            L3Build.SetArray(gate.GetComponent<PortcullisCycle>(), "downPhasePours", new Object[] { holeA.GetComponent<MurderHole>(), holeB.GetComponent<MurderHole>() });
            Kit(root, "H05b", new Vector3(7f, 0f, 3f), 90f);
            // The vault over the passage with its two slots.
            var vault = L3Build.Child(t, "Vault", new Vector3(0f, 7f, 0f)).transform;
            foreach (var (z0, len) in new[] { (-7f, 4.4f), (-0.6f, 1.2f), (3.4f, 3.6f) })
            {
                var part = L3Build.Child(vault, "Span", new Vector3(0f, 0f, z0 + len * 0.5f)).transform;
                L4Kit.Flags(part, new Vector2(8f, len), 0.5f, (int)(z0 * 10 + 451));
            }

            Fade(vault.gameObject);
        }

        private static void GrandStair(GameObject root)
        {
            var t = root.transform;
            Kit(root, "W09b", new Vector3(0f, 0f, 3.9f), 0f);
            var rng = new System.Random(461);
            foreach (float x in new[] { -8.5f, 8.5f })
            {
                var cheek = L3Build.Child(t, "Cheek", new Vector3(x, 0f, 0f)).transform;
                for (int i = 0; i < 4; i++)
                {
                    L4Kit.Courses(cheek, new Vector3(-0.5f, 0f, i * 1.95f), Vector3.forward, Vector3.left, 1.95f, 1.6f + i * 1.25f, rng, 0f, 1f);
                }
            }

            foreach (float x in new[] { -7f, 7f })
            {
                var bal = L3Build.Child(t, "Balustrade", new Vector3(x, 5f, 8.6f)).transform;
                for (int i = 0; i < 5; i++)
                {
                    L3Build.MeshChild(bal, "Baluster", L2PolyKit.Tread(new Vector3(0.25f, 0.8f, 0.25f), rng.Next(100000)), new Vector3(-0.9f + i * 0.45f, 0.4f, 0f), L4Kit.Stone);
                }

                L3Build.MeshChild(bal, "Rail", L2PolyKit.Tread(new Vector3(2.3f, 0.18f, 0.4f), rng.Next(100000)), new Vector3(0f, 0.9f, 0f), L4Kit.Stone);
            }
        }

        // The throne-hall doors (origin: the threshold, centre): 8.4 m doors shut in a portal of three recessed
        // round-arched orders, between two square towers that rise above it with banners down their fronts and
        // cressets on their tops. Two steps up to a dais before the doors, a great brazier burning on a stone plinth
        // either side (brazier gold at the end). Belongs under the L4L5_Threshold root.
        private static void ThroneDoors(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(471);
            const float dais = 0.4f, wall = 13f;
            var iron = L4Build.Mat("L4_Iron");

            // Threshold steps and the dais.
            var threshold = L3Build.Child(t, "Threshold").transform;
            for (int k = 0; k < 2; k++)
            {
                float top = (k + 1) * 0.2f;
                for (int i = 0; i < 8; i++)
                {
                    L3Build.MeshChild(threshold, "Step", L2PolyKit.Tread(new Vector3(1.65f, top, 0.5f), rng.Next(100000)), new Vector3(-5.775f + i * 1.65f, top * 0.5f, -3.6f + k * 0.5f), L4Kit.Stone);
                }

                L3Build.StandingBox(L3Build.Child(threshold, "StepCollider", new Vector3(0f, 0f, -3.6f + k * 0.5f)), new Vector3(13.2f, top, 0.5f));
            }

            var deck = L3Build.Child(threshold, "Dais", new Vector3(0f, dais, -1.43f)).transform;
            L4Kit.Flags(deck, new Vector2(13.2f, 2.85f), dais, 473);
            L3Build.StandingBox(L3Build.Child(threshold, "DaisCollider", new Vector3(0f, 0f, -1.43f)), new Vector3(13.2f, dais, 2.85f));

            // The portal: three orders, each a step wider and nearer.
            var portal = L3Build.Child(t, "Portal", new Vector3(0f, dais, 0f)).transform;
            for (int o = 0; o < 3; o++)
            {
                float span = 8.4f + o * 1.2f;
                var ring = L3Build.Child(portal, "Order", new Vector3(0f, 0f, o == 0 ? 0f : -0.3f - o * 0.6f)).transform;
                L4Kit.GateArch(ring, span, 0.6f, 6.6f, span * 0.5f, wall, o == 0 ? 1.2f : 0.6f, 474 + o);
            }

            var doors = L3Build.Child(portal, "Doors").transform;
            L3Build.Fit(doors, "K:SM_Bld_Castle_Door_01", 0, new Vector3(8.4f, 6.6f, 0.9f), new Vector2(0f, 0.6f), 0f, Soot);
            // The pack door is arched: a wall behind it closes the corners of the opening above its frame.
            L4Kit.Courses(doors, new Vector3(-4.8f, 0f, 1.05f), Vector3.right, Vector3.back, 9.6f, 7.2f, rng, 0f, 0.5f, 2f);
            var tympanum = L3Build.Child(doors, "Tympanum", new Vector3(0f, 6.6f, 0f)).transform;
            L4Kit.Courses(tympanum, new Vector3(-4.2f, 0f, 0.2f), Vector3.right, Vector3.back, 8.4f, 4.2f, rng, 0f, 0.6f, 2f);
            L4Kit.MerlonRun(portal, new Vector3(-6f, wall, -1.8f), Vector3.right, Vector3.back, 6, 478, 0.2f, 0f, null, null, false);
            L3Build.StandingBox(L3Build.Child(portal, "Collider", new Vector3(0f, 0f, 0f)), new Vector3(12f, wall, 3.6f));

            // Flanking towers: three pack tower stages and a crenellated top, banners down their fronts, cressets.
            const float scale = 1.6f, stage = 3.04f * scale;
            string[] stages = { "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_04" };
            foreach (float side in new[] { -1f, 1f })
            {
                var tower = L3Build.Child(t, side < 0 ? "TowerWest" : "TowerEast", new Vector3(side * 8.3f, 0f, 0f)).transform;
                for (int i = 0; i < 3; i++)
                {
                    L3Build.Part(tower, stages[i], new Vector3(0f, i * stage, 0f), new Vector3(0f, side < 0 ? 0f : 90f, 0f), Vector3.one * scale, Stone);
                }

                L3Build.Part(tower, "K:SM_Bld_Castle_Tower_Top_01", new Vector3(0f, 3f * stage, 0f), Vector3.zero, Vector3.one * scale, Stone);
                L3Build.Part(tower, "K:SM_Prop_Banner_01", new Vector3(0f, wall + 0.6f, -2.32f), new Vector3(0f, 0f, side * 1.5f), Vector3.one * 2f, L4Build.Flat("L4_RoyalCloth", "#4E1814", 0.08f));
                var cresset = L3Build.Child(tower, "Cresset", new Vector3(0f, 3f * stage, 0f)).transform;
                L3Build.Seat(cresset, "K:SM_Prop_Brazier_01", Vector3.zero, 0.9f, Vector2.zero, 0f, iron);
                L2Fire.Fire(cresset, "Flames", new Vector3(0f, 1.75f, 0f), 0.5f, 2.2f, 10f, true);
                L3Build.StandingBox(L3Build.Child(tower, "Collider"), new Vector3(2.78f * scale, 3f * stage, 2.78f * scale));
            }

            // The great braziers on their plinths.
            foreach (float x in new[] { -5.3f, 5.3f })
            {
                var brazier = L3Build.Child(t, "Brazier", new Vector3(x, dais, -2.2f)).transform;
                L4Kit.Courses(brazier, new Vector3(-0.6f, 0f, -0.6f), Vector3.right, Vector3.back, 1.2f, 0.9f, rng, 0f, 1.2f, 2f);
                L3Build.Seat(brazier, "K:SM_Prop_Brazier_01", Vector3.zero, 1.5f, Vector2.zero, 0.9f, iron);
                L2Fire.Fire(brazier, "Flames", new Vector3(0f, 0.9f + 1.89f * 1.5f - 0.1f, 0f), 0.85f, 3.6f, 13f, true);
                L3Build.StandingBox(L3Build.Child(brazier, "Collider"), new Vector3(1.2f, 3.6f, 1.2f));
            }
        }

        private static void PrisonCells(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(481);
            // The lane runs toward -X from the mouth: south wall, then three cells on the north side.
            L4Kit.Courses(t, new Vector3(-14f, 0f, -2f), Vector3.right, Vector3.back, 14f, 2.9f, rng, 0f, 0.6f);
            for (int c = 0; c < 3; c++)
            {
                float x0 = -13.5f + c * 4f;
                L4Kit.Courses(t, new Vector3(x0, 0f, 2f), Vector3.forward, Vector3.left, 3.4f, 2.9f, rng, 0f, 0.5f);
                L4Kit.Courses(t, new Vector3(x0 - 0.5f, 0f, 5.4f), Vector3.right, Vector3.forward, 4f, 2.9f, rng, 0f, 0.5f);
                var gate = L3Build.Part(t, "K:SM_Bld_Castle_Iron_Gate_01", Vector3.zero, new Vector3(0f, c == 1 ? -70f : 0f, 0f), new Vector3(1.4f, 0.95f, 1f), L4Build.Mat("L4_Iron"));
                var b = L3Build.LocalBounds(gate, t);
                gate.transform.localPosition += new Vector3(x0 + 1.8f - b.center.x, -b.min.y, 2.05f - b.center.z + (c == 1 ? 1.2f : 0f));
            }

            L4Kit.Courses(t, new Vector3(-1.5f, 0f, 2f), Vector3.forward, Vector3.left, 3.4f, 2.9f, rng, 0f, 0.5f);
            var warden = Nest(root, Find("F06").Path, new Vector3(-11.5f, 0f, 4.3f), 180f);
            warden.name = "Gatewarden";
            var scratch = new GameObject("Scratching");
            scratch.transform.SetParent(t, false);
            scratch.transform.localPosition = new Vector3(-11.6f, 1.5f, 5.12f);
            scratch.transform.localRotation = Quaternion.Euler(0f, 180f, -2f);
            var tmp = scratch.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "I did not open it.\nI watched my hands open it.";
            tmp.fontSize = 1.4f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.62f, 0.6f, 0.55f, 0.55f);
            tmp.rectTransform.sizeDelta = new Vector2(2.4f, 0.8f);
            var roof = L3Build.Child(t, "Roof").transform;
            for (int i = 0; i < 18; i++)
            {
                Plank(roof, new Vector3(14.4f, 0.07f, 0.42f), new Vector3(-7f, 3f, -2f + i * 0.42f), Quaternion.identity, rng.Next(100000), true);
            }

            Fade(roof.gameObject);
            L2Fire.Candle(t, "Candle", new Vector3(-12.6f, 0.4f, 3.2f), 0.7f, 4f);
            foreach (var (x, z, sx, sz) in new[] { (-7f, -2.3f, 14f, 0.6f), (-7f, 5.65f, 14f, 0.5f) })
            {
                var c = L3Build.Child(t, "WallCollider", new Vector3(x, 0f, z));
                L3Build.StandingBox(c, new Vector3(sx, 2.9f, sz));
            }
        }

        private static void Gallows(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(491);
            for (float x = -4.6f; x <= 4.7f; x += 2.3f)
            {
                foreach (float z in new[] { -3.6f, 0f, 3.6f })
                {
                    L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(1f, 0.78f, 1f), new Vector2(x, z), 0f, Charred);
                }
            }

            for (int i = 0; i < 20; i++)
            {
                Plank(t, new Vector3(10f, 0.07f, 0.4f), new Vector3(0f, 2f, -3.8f + i * 0.4f), Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 0.6f, 0f), rng.Next(100000), i % 6 == 0);
            }

            for (int i = 0; i < 8; i++)
            {
                L3Build.MeshChild(t, "Step", L2PolyKit.Plank(new Vector3(2f, 0.08f, 0.32f), rng.Next(100000)), new Vector3(3.6f, 0.22f + i * 0.25f, -4.3f - (7 - i) * 0.3f), Wood);
            }

            foreach (float x in new[] { -4.2f, 4.2f })
            {
                L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(1.3f, 1.6f, 1.3f), new Vector2(x, 1.5f), 2f, Wood);
            }

            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1.3f, 3.6f, 1.3f), new Vector2(0f, 1.5f), 5.7f, Wood);
            string[] hanged = { "F03a", "F03a", "F03a", "F03b" };
            for (int i = 0; i < 4; i++)
            {
                var h = Nest(root, Find(hanged[i]).Path, new Vector3(-3f + i * 2f, 5.7f, 1.5f), 160f + i * 25f);
                h.name = i == 3 ? "Captain" : "Hanged";
            }

            var deck = L3Build.Child(t, "Deck");
            L3Build.StandingBox(deck, new Vector3(10f, 2.05f, 8f));
            deck.layer = L3Build.Layer(L3Build.Walkable);
            var ramp = L3Build.Child(t, "Ramp", new Vector3(3.6f, 1f, -5.3f), new Vector3(-Mathf.Atan2(2f, 2.4f) * Mathf.Rad2Deg, 0f, 0f));
            var rb = ramp.AddComponent<BoxCollider>();
            rb.size = new Vector3(2f, 0.2f, 3.1f);
            ramp.layer = L3Build.Layer(L3Build.Walkable);
        }

        private static void CausewaySpan(GameObject root)
        {
            var t = root.transform;
            // The deck itself is the level's floor F01 (refinement: the same cobbles as the yards); this carries the
            // parapets, the arch under it and the deck's own collider.
            for (int i = 0; i < 5; i++)
            {
                Kit(root, "W03a", new Vector3(-4.25f, 0f, -4f + i * 2f), 90f);
                Kit(root, "W03a", new Vector3(4.25f, 0f, -4f + i * 2f), 90f);
            }

            var arch = L3Build.Child(t, "Arch", new Vector3(0f, -12.4f, 0f), new Vector3(0f, 90f, 0f)).transform;
            L4Kit.GateArch(arch, 7f, 1.5f, 7f, 3f, 12f, 8f, 502);
            var col = L3Build.Child(t, "DeckCollider", new Vector3(0f, -0.45f, 0f));
            L3Build.StandingBox(col, new Vector3(8f, 0.45f, 10f));
            col.layer = L3Build.Layer(L3Build.Walkable);
        }

        private static void KeepSpire(GameObject root)
        {
            var t = root.transform;
            string[] storeys = { "K:SM_Bld_Castle_Tower_Base_01", "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_03", "K:SM_Bld_Castle_Tower_04", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_01" };
            float y = 0f;
            var keep = L3Build.Child(t, "Keep", new Vector3(0f, 0f, 8f)).transform;
            foreach (var s in storeys)
            {
                var go = L3Build.Seat(keep, s, Vector3.zero, 3f, Vector2.zero, y, Stone);
                y = L3Build.LocalBounds(go, keep).max.y;
            }

            L3Build.Seat(keep, "K:SM_Bld_Castle_Tower_Top_01", Vector3.zero, 3f, Vector2.zero, y, Stone);
            L3Build.Seat(keep, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, new Vector3(2.9f, 1.6f, 2.9f), Vector2.zero, y + 2.4f, Soot);   // ~70 m to the tip
            for (int i = -1; i <= 1; i++)
            {
                L3Build.Seat(t, "K:SM_Bld_Church_Room_01", new Vector3(0f, 90f, 0f), 2.2f, new Vector2(i * 14f, -6f), 0f, Stone);
            }
        }

        private static void Cloister(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(511);
            for (int i = 0; i < 4; i++)
            {
                var bay = L3Build.Child(t, "Bay", new Vector3(-5.25f + i * 3.5f, 0f, 0f)).transform;
                L4Kit.GateArch(bay, 2.4f, 0.55f, 2.6f, 1.2f, 4.4f, 0.9f, 512 + i, i != 2);
            }

            for (int i = 0; i < 5; i++)
            {
                L4Kit.Fallen(t, new Vector3(1.75f + ((float)rng.NextDouble() - 0.5f) * 2.4f, 0f, -0.8f - (float)rng.NextDouble() * 1.4f), rng);
            }

            var col = L3Build.Child(t, "Collider");
            L3Build.StandingBox(col, new Vector3(14f, 4.4f, 0.9f));
        }

        // ---------------------------------------------------------------- Markers and gates

        private static void Pickup(GameObject root, PickupKind kind, string id, string packKey, Vector3 euler, float scale)
        {
            L3Build.Seat(root.transform, packKey, euler, scale, Vector2.zero, 0.02f, packKey.StartsWith("K") ? Stone : AdvStone);
            var marker = root.AddComponent<PickupMarker>();
            marker.kind = kind;
            marker.id = id;
            var glint = L3Build.Child(root.transform, "Glint", new Vector3(0f, 0.6f, 0f)).AddComponent<Light>();
            glint.type = LightType.Point;
            glint.color = L1Build.Hex("#FFD58A");
            glint.intensity = 0.8f;
            glint.range = 2.2f;
            glint.shadows = LightShadows.None;
        }

        private static void TribunalKey(GameObject root)
        {
            Shapes(root.transform, "Key", L4Shapes.Props, m =>
            {
                var bow = new Vector3(-0.1f, 0.03f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                    L4Shapes.Shaft(m, 0, bow + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.05f, bow + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.05f, 0.012f, 0.012f, 4, L4Kit.Cell(L4Shapes.Iron));
                }

                L4Shapes.Shaft(m, 0, bow + Vector3.right * 0.05f, new Vector3(0.14f, 0.03f, 0f), 0.012f, 0.012f, 5, L4Kit.Cell(L4Shapes.Iron));
                L4Shapes.Box(m, 0, new Vector3(0.12f, 0.03f, 0.025f), Quaternion.identity, new Vector3(0.04f, 0.015f, 0.05f), L4Kit.Cell(L4Shapes.Iron));
            });
            var marker = root.AddComponent<PickupMarker>();
            marker.kind = PickupKind.Key;
            marker.id = "TribunalKey";
        }

        private static void Gate(GameObject root, OneTimeGate.Lock kind)
        {
            var t = root.transform;
            var leaf = L3Build.Child(t, "Leaf", new Vector3(kind == OneTimeGate.Lock.Barred ? -1.1f : 0f, 0f, 0f)).transform;
            if (kind == OneTimeGate.Lock.Barred)
            {
                // A heavy door hinged at its -X edge; the bar across its +Z face.
                var door = L3Build.Fit(leaf, "K:SM_Bld_Castle_Door_01", 0, new Vector3(2.2f, 3f, 0.3f), new Vector2(1.1f, 0f), 0f, Soot);
                door.name = "Door";
                var bar = L3Build.Child(t, "Bar", new Vector3(0f, 1.3f, 0.3f)).transform;
                L3Build.Seat(bar, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1f, 1.05f, 1f), Vector2.zero, -0.15f, Charred);
            }
            else
            {
                L3Build.Fit(leaf, "K:SM_Bld_Castle_Iron_Gate_01", 0, new Vector3(3f, 2.8f, 0.12f), Vector2.zero, 0f, L4Build.Mat("L4_Iron"));
                Shapes(t, "Lock", L4Shapes.Props, m => L4Shapes.Box(m, 0, new Vector3(1.2f, 1.1f, -0.12f), Quaternion.identity, new Vector3(0.22f, 0.3f, 0.1f), L4Kit.Cell(L4Shapes.IronDark)));
            }

            var blocker = L3Build.Child(t, "Blocker");
            var box = L3Build.StandingBox(blocker, new Vector3(kind == OneTimeGate.Lock.Barred ? 2.4f : 3f, 3f, 0.4f));
            var obstacle = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(3f, 3f, 0.6f);
            obstacle.center = new Vector3(0f, 1.5f, 0f);
            obstacle.carving = true;
            obstacle.enabled = false;
            var prompt = Prompt(t, new Vector3(0f, 3.3f, kind == OneTimeGate.Lock.Barred ? 0.6f : -0.6f));
            var gate = root.AddComponent<OneTimeGate>();
            L3Build.Set(gate, "lockKind", kind);
            L3Build.Set(gate, "blocker", box);
            L3Build.Set(gate, "leaf", leaf);
            L3Build.Set(gate, "obstacle", obstacle);
            L3Build.Set(gate, "prompt", prompt.GetComponent<TMPro.TMP_Text>());
            if (kind == OneTimeGate.Lock.Barred)
            {
                L3Build.Set(gate, "bar", t.Find("Bar").gameObject);
                L3Build.Set(gate, "openEuler", new Vector3(0f, 100f, 0f));
            }
            else
            {
                L3Build.Set(gate, "openEuler", Vector3.zero);
                L3Build.Set(gate, "openOffset", new Vector3(0f, 2.6f, 0f));
            }
        }

        // ---------------------------------------------------------------- Enemy stand-ins

        private enum Pose
        {
            Stand,
            Aim,
        }

        private static void StandIn(GameObject root, string body, string material, EnemyType type, float scale, float radius, Pose pose, bool plates, bool brand, string weapon = null)
        {
            var t = root.transform;
            var mesh = StandFigure(body, pose);
            var figure = L3Build.MeshChild(t, "Figure", mesh, Vector3.zero, L1Build.Mat(material));
            figure.transform.localScale = Vector3.one * scale;
            float height = mesh.bounds.size.y * scale;
            if (plates)
            {
                Shapes(t, "Plates", L4Shapes.Props, m =>
                {
                    L4Shapes.Box(m, 0, new Vector3(0f, height * 0.68f, 0.17f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.5f, 0.42f, 0.12f), L4Kit.Cell(L4Shapes.Iron));
                    L4Shapes.Box(m, 0, new Vector3(-0.3f, height * 0.83f, 0f), Quaternion.Euler(0f, 0f, 24f), new Vector3(0.34f, 0.12f, 0.4f), L4Kit.Cell(L4Shapes.Iron));
                    L4Shapes.Box(m, 0, new Vector3(0.3f, height * 0.83f, 0f), Quaternion.Euler(0f, 0f, -24f), new Vector3(0.34f, 0.12f, 0.4f), L4Kit.Cell(L4Shapes.Iron));
                });
            }

            if (brand)
            {
                Shapes(t, "Brand", L4Build.Flat("L4_TwinBrand", "#2A0D04", 0.1f, "#FF5A14", 2.6f), m =>
                {
                    L4Shapes.Box(m, 0, new Vector3(0f, height * 0.7f, 0.16f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.12f, 0.12f, 0.02f), L4Kit.Cell(0));
                    L4Shapes.Box(m, 0, new Vector3(0f, height * 0.7f, 0.16f), Quaternion.identity, new Vector3(0.04f, 0.22f, 0.02f), L4Kit.Cell(0));
                });
            }

            if (weapon != null)
            {
                L3Build.Seat(t, weapon, new Vector3(0f, 0f, -20f), 1.5f, new Vector2(0.45f, 0.15f), 0.1f, Stone);
            }

            if (pose == Pose.Aim)
            {
                Shapes(t, "Crossbow", Wood, m =>
                {
                    var front = new Vector3(0f, height * 0.72f, 0.75f);
                    L4Shapes.Shaft(m, 0, new Vector3(0f, height * 0.72f, 0.05f), front, 0.04f, 0.035f, 5, L4Kit.Cell(L2PolyKit.Side));
                    L4Shapes.Shaft(m, 0, front + Vector3.left * 0.35f + Vector3.back * 0.1f, front + Vector3.right * 0.35f + Vector3.back * 0.1f, 0.025f, 0.025f, 5, L4Kit.Cell(L2PolyKit.Top));
                });
                var gate = root.AddComponent<OnScreenGate>();
                L3Build.Set(gate, "tuning", Tuning);
            }

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            var marker = root.AddComponent<SpawnMarker>();
            marker.enemyType = type;
        }

        private static Mesh StandFigure(string body, Pose pose)
        {
            string path = L4Build.MeshesPath + "/Figures/L4_" + pose + "_" + body + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            L1Bakers.PoseFn fn = pose == Pose.Aim ? AimPose : (L1Bakers.PoseFn)StandPose;
            var baked = L1Bakers.BakeStanding(KnightsCharacters + "Character_" + body + "_Blue.prefab", "Character_" + body, fn, null, out var rig);
            Object.DestroyImmediate(rig);
            L2Build.EnsureFolder(L4Build.MeshesPath + "/Figures");
            AssetDatabase.CreateAsset(baked, path);
            return baked;
        }

        private static void PoseAim(Transform r, string bone, string child, Vector3 dir)
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

            var b = F(bone);
            var c = F(child);
            b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
        }

        private static void StandPose(Transform r, float s)
        {
            var l = new Vector3(s, 0f, 0f);
            PoseAim(r, "Shoulder_L", "Elbow_L", l * 0.25f + new Vector3(0f, -1f, 0.05f));
            PoseAim(r, "Elbow_L", "Hand_L", l * 0.1f + new Vector3(0f, -1f, 0.2f));
            PoseAim(r, "Shoulder_R", "Elbow_R", -l * 0.25f + new Vector3(0f, -1f, 0.05f));
            PoseAim(r, "Elbow_R", "Hand_R", -l * 0.1f + new Vector3(0f, -1f, 0.2f));
            PoseAim(r, "UpperLeg_L", "LowerLeg_L", l * 0.1f + new Vector3(0f, -1f, 0.05f));
            PoseAim(r, "UpperLeg_R", "LowerLeg_R", -l * 0.1f + new Vector3(0f, -1f, -0.05f));
        }

        private static void AimPose(Transform r, float s)
        {
            var l = new Vector3(s, 0f, 0f);
            PoseAim(r, "Shoulder_L", "Elbow_L", l * 0.2f + new Vector3(0f, -0.3f, 1f));
            PoseAim(r, "Elbow_L", "Hand_L", new Vector3(-s * 0.1f, 0f, 1f));
            PoseAim(r, "Shoulder_R", "Elbow_R", -l * 0.35f + new Vector3(0f, -0.6f, 0.5f));
            PoseAim(r, "Elbow_R", "Hand_R", new Vector3(s * 0.2f, 0.1f, 1f));
            PoseAim(r, "UpperLeg_L", "LowerLeg_L", l * 0.12f + new Vector3(0f, -1f, 0.15f));
            PoseAim(r, "UpperLeg_R", "LowerLeg_R", -l * 0.12f + new Vector3(0f, -1f, -0.2f));
        }
    }
}
