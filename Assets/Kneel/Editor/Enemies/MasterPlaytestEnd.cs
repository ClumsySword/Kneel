using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Stage 7 of MasterPlaytest: the boss framing, Shrine C, and the whole fight from the aggro line to "The End"
    // (both phases, the Unmaking, the finisher). Groups: camera, shrine, fullfight. Runs after Phase 2, before the lava.
    public static partial class MasterPlaytest
    {
        private static readonly string[] EndGroups = { "camera", "shrine", "fullfight" };

        private static bool WantsAnyEnd => string.IsNullOrEmpty(Only) || Only.Split(',').Any(g => EndGroups.Contains(g));

        private static IEnumerator Ending()
        {
            if (WantsAnyEnd == false)
                yield break;
            colossus = boss.colossus;
            BackupSave();
            MasterFinisher.HoldMainMenu = true;
            if (Wants("shrine"))
                yield return ShrineC();
            if (Wants("camera") || Wants("fullfight"))
                yield return FullFight();
            MasterFinisher.HoldMainMenu = false;
            RestoreSave();
        }

        // --- Shrine C ---

        private static IEnumerator ShrineC()
        {
            Note("--- Shrine C, the Last Shrine");
            var rest = UnityEngine.Object.FindAnyObjectByType<ShrineRest>();
            Check("it's in the level", rest != null, "");
            if (rest == null)
                yield break;
            yield return ReviveP1();
            PlaceAbsolute(rest.transform.position + Vector3.forward * 1.8f, Vector3.back);
            yield return 0.3f;
            Check("within reach, Kael can rest", rest.InReach, HallActors.FlatDistance(playerRoot.position, rest.transform.position).ToString("F1") + " m");
            playerHealth.ApplyDamage(playerHealth.Current * 0.5f);
            var potions = combat.GetComponent<PlayerPotions>();
            while (potions != null && potions.Count > 0 && potions.TryTake()) { }
            rest.Rest();
            yield return 0.2f;
            Check("resting kneels him, heals him, refills his potions and saves the shrine", combat.IsKneeling && Mathf.Approximately(playerHealth.Current, playerHealth.Max) && (potions == null || potions.Count == potions.Capacity) && Kneel.Core.SaveFlags.Has(rest.flag),
                combat.State + ", health " + playerHealth.Fraction.ToString("P0") + ", potions " + (potions != null ? potions.Count + "/" + potions.Capacity : "-") + ", flag " + Kneel.Core.SaveFlags.Has(rest.flag));
            Shot("ShrineC_Rest");
            yield return (Func<bool>)(() => combat.IsKneeling == false);

            // After a death, the reload puts him here (ShrineRest.Start does this on load once the flag is saved).
            PlaceAbsolute(new Vector3(54f, 9f, 380f), Vector3.forward);
            rest.PlaceAtShrine(combat);
            yield return 0.1f;
            Check("a reload puts Kael at the shrine, facing the hall", HallActors.FlatDistance(playerRoot.position, rest.RespawnPoint) < 0.3f && Vector3.Angle(playerRoot.forward, Vector3.forward) < 5f, playerRoot.position.ToString("F1"));
            Note("  (the reload itself, the death screen's Restart, can't run inside one Play session: ShrineRest.Start places him)");
        }

        // --- Shrine C across a real reload: Kael dies, the death screen's Restart reloads L5, and he's at the shrine ---
        // Last of all: the reload replaces every object the other groups hold.
        private static IEnumerator ShrineReload()
        {
            Note("--- Shrine C across a reload (death, then the death screen's Restart)");
            BackupSave();
            var oldRest = UnityEngine.Object.FindAnyObjectByType<ShrineRest>();
            if (oldRest == null)
            {
                Check("Shrine C is in the level", false, "");
                yield break;
            }

            Kneel.Core.SaveFlags.Set(oldRest.flag);
            if (combat.IsDead == false)
            {
                playerHealth.SetMax(100f, true);
                playerHealth.ApplyDamage(1000f);
            }

            yield return 3f;
            Kneel.UI.PauseMenu menu = Kneel.UI.PauseMenu.Current;
            Check("Kael dies and the death screen opens", combat.IsDead && menu != null, combat.State.ToString());
            if (menu == null)
                yield break;
            typeof(Kneel.UI.PauseMenu).GetMethod("RestartLevel", Any).Invoke(menu, null);
            yield return (Func<bool>)(() =>
            {
                var now = UnityEngine.Object.FindAnyObjectByType<ShrineRest>();
                return now != null && now != oldRest;
            });
            yield return 1.5f;

            var rest = UnityEngine.Object.FindAnyObjectByType<ShrineRest>();
            var kael = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var newBoss = UnityEngine.Object.FindAnyObjectByType<MasterBoss>();
            bool reloaded = rest != null && rest != oldRest && kael != null;
            Check("the Restart reloads L5", reloaded, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (reloaded == false)
                yield break;
            // (He's placed facing the hall, but turns to the aim point the moment he has one, like everywhere else.)
            Check("Kael comes back at Shrine C", HallActors.FlatDistance(kael.transform.position, rest.RespawnPoint) < 0.4f,
                kael.transform.position.ToString("F1") + ", " + HallActors.FlatDistance(kael.transform.position, rest.RespawnPoint).ToString("F2") + " m from the spot");
            Health kaelHealth = kael.GetComponent<Health>();
            Check("whole, and free to move", kael.IsDead == false && kaelHealth.Current >= kaelHealth.Max && kael.IsRestricted == false, kael.State.ToString());
            Check("the fight is back at its start: the Master on his throne, the Colossus not in the hall",
                newBoss != null && newBoss.Current == MasterBoss.Beat.Seated && newBoss.throneSeat != null && Vector3.Distance(newBoss.master.transform.position, newBoss.throneSeat.position) < 0.5f
                && newBoss.colossus.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.enabled == false || r.gameObject.activeInHierarchy == false),
                newBoss != null ? newBoss.Current.ToString() : "no boss");
            Shot("ShrineC_Respawn");
            RestoreSave();
        }

        // --- The whole fight, from the aggro line to the end card ---

        private static IEnumerator FullFight()
        {
            Note("--- the whole fight, from the aggro line to the end card");
            var cam = UnityEngine.Object.FindAnyObjectByType<PlayerCamera>();
            yield return ReviveP1();
            boss.StopBeats();
            boss.Seat();
            playerHealth.SetMax(100000f, true);
            PlaceAbsolute(new Vector3(54f, 9f, 429f), Vector3.forward);
            yield return 0.5f;
            float crossed = Time.time;
            PlaceAbsolute(new Vector3(54f, 9f, 435f), Vector3.forward);
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Phase1);
            Check("the intro, then Phase 1", master.Engaged, (Time.time - crossed).ToString("F1") + " s from the line");
            if (combat.State == PlayerCombat.CombatState.Sheathed)
                Press("OnDrawPressed");

            // The framing: Phase 1, 10 m apart.
            Check("the camera frames him with Kael (Phase 1)", cam.FramingSubject == master.transform, "");
            master.circleState.Hold(9999f);
            PlaceFront(10f);
            yield return 1.5f;
            ReportFraming(cam, master.transform, 4.2f, boss.framingP1);
            Shot("Framing_P1");

            // Phase 1 down to half: the pommel beat.
            master.health.ApplyDamage(master.health.Current - master.health.Max * 0.48f);
            master.circleState.Hold(0f);
            yield return (Func<bool>)(() => State == master.pommelState);
            Check("the 50% beat", State == master.pommelState, "");
            yield return (Func<bool>)(() => State != master.pommelState);

            // Phase 1 ends: the Unmaking.
            boss.EndPhase1();
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Transition);
            float unmade = Time.time;
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Phase2);
            Check("the Unmaking, then Phase 2", colossus.Engaged, (Time.time - unmade).ToString("F1") + " s");

            // The framing: Phase 2, 14 m apart.
            Check("the camera frames the Colossus with Kael (Phase 2)", cam.FramingSubject == colossus.transform, "");
            yield return P2WaitMoveEnds();
            colossus.circleState.Hold(9999f);
            P2PlaceFront(14f);
            yield return 1.5f;
            ReportFraming(cam, colossus.transform, 6f, boss.framingP2);
            Shot("Framing_P2");

            // Phase 2 down to half: the roar.
            colossus.health.ApplyDamage(colossus.health.Current - colossus.health.Max * 0.48f);
            colossus.circleState.Hold(0f);
            yield return (Func<bool>)(() => P2State == colossus.roarState);
            Check("his 50% roar", P2State == colossus.roarState, "");
            yield return (Func<bool>)(() => P2State != colossus.roarState);
            yield return P2WaitMoveEnds();

            // Phase 2 ends at the player's blow.
            colossus.health.ApplyDamage(colossus.health.Current - 2f);
            for (int swing = 0; swing < 6 && colossus.IsDead == false; swing++)
            {
                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed || colossus.IsDead);
                if (colossus.IsDead)
                    break;
                colossus.circleState.Hold(9999f);
                P2PlaceFront(2.8f);
                Press("OnHeavyPressed");
                float swung = Time.time;
                yield return (Func<bool>)(() => colossus.IsDead || Time.time - swung > 1.6f);
            }

            Check("he drops to both knees", colossus.IsDead && boss.Current == MasterBoss.Beat.Phase2Down, P2State.GetType().Name);
            yield return Finisher();
        }

        // Kael and the boss both in the frame; Kael at least 60 px tall at 1080p; the camera no further than its cap.
        private static void ReportFraming(PlayerCamera cam, Transform subject, float height, float cap)
        {
            Camera c = Camera.main;
            Vector3 kFeet = c.WorldToViewportPoint(playerRoot.position), kHead = c.WorldToViewportPoint(playerRoot.position + Vector3.up * 1.8f);
            Vector3 bFeet = c.WorldToViewportPoint(subject.position), bHead = c.WorldToViewportPoint(subject.position + Vector3.up * height);
            bool In(Vector3 v) => v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            // His screen box (0.9 m across, 1.8 m tall), as the camera measures him.
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 v = c.WorldToViewportPoint(playerRoot.position + new Vector3((i & 1) == 0 ? -0.45f : 0.45f, (i & 2) == 0 ? 0f : 1.8f, (i & 4) == 0 ? -0.45f : 0.45f));
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            float kaelPixels = Mathf.Max((maxX - minX) * c.aspect, maxY - minY) * 1080f;
            // At the cap, Kael's floor comes first: the boss's head may graze the top edge (owner's trade-off to weigh).
            bool headNear = bHead.z > 0f && bHead.x >= 0f && bHead.x <= 1f && bHead.y <= 1.1f;
            Check("both in frame, " + HallActors.FlatDistance(playerRoot.position, subject.position).ToString("F0") + " m apart (his head may graze the top at the cap)", In(kFeet) && In(kHead) && In(bFeet) && headNear, "Kael " + kFeet.ToString("F2") + ", boss feet " + bFeet.ToString("F2") + " head " + bHead.ToString("F2"));
            Check("Kael at least 60 px on screen (1080p)", kaelPixels >= 58f, kaelPixels.ToString("F0") + " px");
            bool room = true;
            foreach (Vector3 o in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                room &= In(c.WorldToViewportPoint(playerRoot.position + o * cam.framingPlayerRoom));
            Check("the floor " + cam.framingPlayerRoom + " m round Kael is in the frame (room to aim)", room, "");
            Check("the camera within " + cap + " m", cam.FramedDistance <= cap + 0.3f, cam.FramedDistance.ToString("F1") + " m");
        }

        // --- The Last Command ---

        private static IEnumerator Finisher()
        {
            Note("--- the Last Command");
            MasterFinisher finisher = boss.finisher;
            yield return (Func<bool>)(() => finisher.Running);
            Shot("Finisher_1knees");
            {
                // What lights him (the finisher should read as a dying body, not a lamp).
                Vector3 chest = colossus.transform.position + Vector3.up * 3f;
                foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>().Where(l => l.enabled && l.type != LightType.Directional && Vector3.Distance(l.transform.position, chest) < l.range))
                    Note("  light " + l.name + " (" + (l.transform.parent != null ? l.transform.parent.name : "-") + ") i " + l.intensity.ToString("F1") + " r " + l.range.ToString("F1") + " d " + Vector3.Distance(l.transform.position, chest).ToString("F1"));
                var skinR = colossus.GetComponentInChildren<SkinnedMeshRenderer>();
                var pb = new MaterialPropertyBlock();
                skinR.GetPropertyBlock(pb);
                Note("  body emission " + pb.GetColor("_EmissionColor") + ", material " + skinR.sharedMaterial.GetColor("_EmissionColor"));
            }

            // Nothing hurts Kael now, not blows, not fire.
            float before = playerHealth.Current;
            HallActors.HitPlayer(40f, playerRoot.position + Vector3.forward, null, 1.5f);
            HallActors.BurnPlayer(25f, null);
            yield return 0.1f;
            Check("nothing hurts Kael now", playerHealth.Current >= before - 0.01f && FloorFire.Harmless, (before - playerHealth.Current).ToString("F1") + " damage");
            var storm = UnityEngine.Object.FindAnyObjectByType<AshStorm>();
            Check("the ash storm has gone", storm == null || storm.Running == false, "");

            // Kael keeps away: the palm completes, and he kneels (no damage), and it rises again.
            P2PlaceFront(9f);
            yield return (Func<bool>)(() => finisher.PalmRaised);
            float raised = Time.time;
            Shot("Finisher_2palm");
            yield return (Func<bool>)(() => combat.IsKneeling || finisher.PalmDown);
            Check("the palm completes (" + finisher.palmTime + " s) and Kael kneels, unhurt", combat.IsKneeling && finisher.PalmsCompleted == 1 && playerHealth.Current >= before - 0.01f, (Time.time - raised).ToString("F2") + " s, " + combat.State);
            yield return (Func<bool>)(() => combat.IsKneeling == false);
            yield return (Func<bool>)(() => finisher.PalmRaised);
            Check("and the palm rises again", finisher.PalmRaised, "");

            // Kael walks up: the palm comes down. "Go on."
            P2PlaceFront(3f);
            yield return (Func<bool>)(() => finisher.PalmDown);
            yield return 1f;
            Check("within 1.5 R the palm comes down: \"" + finisher.line + "\"", finisher.PalmDown && finisher.SubtitleShowing && colossus.FinisherOpen, "");
            Shot("Finisher_3goon");

            // The blow.
            int defeated = 0;
            boss.OnBossDefeated += () => defeated++;
            Transform sword = colossus.swordTip.parent;
            for (int swing = 0; swing < 6 && defeated == 0; swing++)
            {
                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed || defeated > 0);
                P2PlaceFront(2.8f);
                Press("OnStrikePressed");
                float swung = Time.time;
                yield return (Func<bool>)(() => defeated > 0 || Time.time - swung > 1.2f);
            }

            Check("an ordinary blow ends him", defeated == 1, "");
            yield return 1.2f;
            Shot("Finisher_4ash");
            Check("he's ash: the body gone, the King's sword on the floor", colossus.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.enabled == false) && sword.parent == null && sword.position.y < colossus.transform.position.y + 0.6f, "sword at " + sword.position.ToString("F1"));
            yield return (Func<bool>)(() => finisher.Mask != null && finisher.Mask.Landed);
            yield return 0.6f;
            MaskShatter mask = finisher.Mask;
            Check("the two halves of his mask fall and break on the floor", mask != null && mask.Landed && mask.ShardsOnFloor >= 4
                && mask.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).All(r => r.bounds.min.y < colossus.transform.position.y + 0.3f),
                mask == null ? "no mask" : mask.ShardsOnFloor + " shards");
            if (mask != null)
                CloseShot("Finisher_4mask", mask.transform.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.bounds.center).Aggregate(Vector3.zero, (x, y) => x + y) / Mathf.Max(1, mask.GetComponentsInChildren<MeshRenderer>(true).Length));
            yield return (Func<bool>)(() => Kneel.Core.SaveFlags.Has("GameCompleted") || finisher.Ended);
            yield return 1.6f;
            Shot("Finisher_5end");
            yield return (Func<bool>)(() => finisher.Ended);
            Check("stillness, the fade to black, the save records the end, \"The End\"", Kneel.Core.SaveFlags.Has("GameCompleted"), "");
        }
    
        // A close look the game camera can't give (a temporary camera rendered to a file; no post-processing).
        private static void CloseShot(string name, Vector3 at)
        {
            if (TakeShots == false)
                return;
            var go = new GameObject("CloseShot");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            // From above and a little to the side, with Kael out of the picture for the render.
            go.transform.position = at + new Vector3(1.2f, 3.2f, -1.2f);
            go.transform.LookAt(at);
            var hidden = combat.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            foreach (Renderer r in hidden)
                r.enabled = false;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            foreach (Renderer r in hidden)
                r.enabled = true;
            System.IO.File.WriteAllBytes("Temp/MasterShots/Master_" + name + ".png", tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(go);
        }
    }
}
