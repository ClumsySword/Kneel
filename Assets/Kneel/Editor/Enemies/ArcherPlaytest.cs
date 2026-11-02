using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // A scripted fight with one Hollow Archer. Start it once L1 is playing (Kneel/Enemies/Hollow Archer/Playtest): his own
    // levels have no open ground, so it spawns him at the Footman's post with the most room down-screen. Player input goes
    // through the real handlers with the input cached the way CLAUDE.md says, and he runs his own AI, so what it measures is
    // the fight itself: the shot's timing, the arrow, rolling through it, blocking it, trying to parry it, and closing the
    // distance. Level Check walks up to every archer in the open level and checks his post. Results go to
    // Temp/ArcherPlaytest.txt. Nothing in the scene is saved. Leave TakeShots off for timing: a screenshot stalls a frame.
    public static class ArcherPlaytest
    {
        public const string ResultPath = "Temp/ArcherPlaytest.txt";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Screenshots stall a frame, so timing runs leave them off.
        public static bool TakeShots;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static HollowArcher archer;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static Vector3 home;
        private static Quaternion homeFacing;
        private static float homeAttackRange;
        private static float homeChaseSpeed;
        private static float homeAggression;
        private static float homeLeash;
        private static float homeHealth;
        private static float lastPlayerDamage;
        private static float lastPlayerDamageTime;
        private static float worstFrame;
        private static int failures;
        private static Vector3 playerMove;
        private static Vector3? heldAway;
        private static Action<float> damageListener;

        private static float Now => Time.unscaledTime;

        [MenuItem("Kneel/Enemies/Hollow Archer/Playtest")]
        public static void Begin() => Start(Run());

        [MenuItem("Kneel/Enemies/Hollow Archer/Level Check")]
        public static void BeginLevelCheck() => Start(LevelCheck());

        private static void Start(IEnumerator routine)
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in a level with archers first.");
                return;
            }

            // Statics outlive Play mode in this project, and these may point at the last level's objects.
            archer = null;
            combat = null;
            heldAway = null;
            playerMove = Vector3.zero;

            Lines.Clear();
            Errors.Clear();
            failures = 0;
            worstFrame = 0f;
            Application.runInBackground = true;
            Application.targetFrameRate = 90;
            Application.logMessageReceived += OnLog;

            routines.Clear();
            routines.Push(routine);
            waitFor = null;
            waitUntil = 0f;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isPlaying == false)
            {
                Finish();
                return;
            }

            worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            DriveInput();
            if (heldAway.HasValue)
            {
                HoldAway();
            }

            if (Now < waitUntil)
            {
                return;
            }

            if (waitFor != null)
            {
                if (waitFor() == false && Now < waitForDeadline)
                {
                    return;
                }

                waitFor = null;
            }

            while (routines.Count > 0)
            {
                IEnumerator top = routines.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Note("EXCEPTION " + exception);
                    failures++;
                    routines.Clear();
                    break;
                }

                if (more == false)
                {
                    routines.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }

                if (top.Current is float seconds)
                {
                    waitUntil = Now + seconds;
                }
                else if (top.Current is Func<bool> condition)
                {
                    waitFor = condition;
                    waitForDeadline = Now + 14f;
                }

                return;
            }

            Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Application.targetFrameRate = -1;

            if (playerHealth != null && damageListener != null)
            {
                playerHealth.OnDamaged -= damageListener;
            }

            Note("worst frame " + worstFrame.ToString("F3") + " s");
            Note("console errors: " + Errors.Count);
            foreach (string error in Errors.Distinct().Take(10))
            {
                Note("  " + error);
            }

            string summary = failures == 0 && Errors.Count == 0 ? "PASS" : "FAIL (" + failures + " checks, " + Errors.Count + " errors)";
            Lines.Insert(0, summary);
            File.WriteAllLines(ResultPath, Lines);
        }

        private static IEnumerator Run()
        {
            Setup();
            yield return 0.5f;

            yield return Scenario(ShotTiming());
            yield return Scenario(ArrowHits(7f));
            yield return Scenario(ArrowHits(3f));
            yield return Scenario(FireRate());
            yield return Scenario(Block());
            yield return Scenario(TryToParry());
            yield return Scenario(RollThrough());
            yield return Scenario(SideStep());
            yield return Scenario(SprintWithoutRolling());
            yield return Scenario(CloseTheDistance());
            yield return Scenario(HitInTheWindup());
            yield return Scenario(HeavyStaggers());
            yield return Scenario(TwoLightsAndHeavy());
            yield return Scenario(Riposte());
            yield return Scenario(FootSlide(true));
            yield return Scenario(FootSlide(false));
        }

        private static IEnumerator Scenario(IEnumerator body)
        {
            yield return ResetFight();
            yield return body;
        }

        private static void FindPlayer()
        {
            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            playerHealth = combat.GetComponent<Health>();
            playerRoot = combat.transform;
            ((PlayerControls)typeof(PlayerCombat).GetField("controls", Any).GetValue(combat)).Disable();
            ((PlayerControls)typeof(PlayerMovement).GetField("controls", Any).GetValue(movement)).Disable();

            damageListener = amount =>
            {
                lastPlayerDamage = amount;
                lastPlayerDamageTime = Now;
            };
            playerHealth.OnDamaged += damageListener;
        }

        private static void Setup()
        {
            FindPlayer();

            // Down-screen of him, so he stays in the camera's frame and his bones keep updating.
            Vector3 down = -Camera.main.transform.forward;
            down.y = 0f;
            down.Normalize();

            AshenFootman[] footmen = UnityEngine.Object.FindObjectsByType<AshenFootman>();
            AshenFootman post = footmen.OrderByDescending(f => Room(f.transform.position, down, 13f)).First();
            home = post.transform.position;
            foreach (AshenFootman footman in footmen)
            {
                footman.gameObject.SetActive(false);
            }

            homeFacing = Quaternion.LookRotation(down);
            Note("open ground " + Room(home, down, 13f).ToString("F1") + " m at yaw " + homeFacing.eulerAngles.y.ToString("F0"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Enemies/HollowArcher/HollowArcher.prefab");
            archer = UnityEngine.Object.Instantiate(prefab, home, homeFacing).GetComponent<HollowArcher>();
            homeAttackRange = archer.attackRange;
            homeChaseSpeed = archer.chaseSpeed;
            homeAggression = archer.aggressionRange;
            homeLeash = archer.leashRadius;
            homeHealth = archer.health.Max;

            Note("fighting at " + home + " in " + archer.gameObject.scene.name);
        }

        private static float Room(Vector3 from, Vector3 direction, float length)
        {
            return NavMesh.Raycast(from, from + direction * length, out NavMeshHit hit, NavMesh.AllAreas) ? hit.distance : length;
        }

        private static IEnumerator ResetFight()
        {
            // Dead is final in the controller, so a body is put back by hand.
            if (archer.IsDead)
            {
                archer.GetComponent<Collider>().enabled = true;
                archer.agent.enabled = true;
                archer.anim.Play("Idle", 0, 0f);
            }

            archer.attackRange = homeAttackRange;
            archer.chaseSpeed = homeChaseSpeed;
            archer.aggressionRange = homeAggression;
            archer.leashRadius = homeLeash;
            archer.health.SetMax(homeHealth, true);
            archer.poise.Reset();
            archer.agent.Warp(home);
            archer.stateMachine.ChangeState(archer.idleState);
            archer.transform.rotation = homeFacing;
            typeof(HollowArcher).GetField("nextShotTime", Any).SetValue(archer, 0f);

            foreach (Arrow arrow in UnityEngine.Object.FindObjectsByType<Arrow>())
            {
                UnityEngine.Object.Destroy(arrow.gameObject);
            }

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            playerMove = Vector3.zero;
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
            typeof(PlayerMovement).GetField("sprintHeld", Any).SetValue(movement, false);

            // Out of his sight while the player gets ready.
            Place(40f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
            {
                Press("OnDrawPressed");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.1f;
        }

        // --- Scenarios ---

        private static IEnumerator ShotTiming()
        {
            Note("--- the shot: reach, arrow, nock, draw, release");
            Place(7f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            float start = Now;
            AttackState_HollowArcher shot = archer.attackState;

            Transform leftArm = archer.anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightArm = archer.anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 stance = Flatten(rightArm.position - leftArm.position);
            float nocked = float.MaxValue;
            float worstAim = 0f;
            float worstStep = 0f;
            float worstLeft = 0f;
            Vector3? lastShoulders = null;
            Func<bool> trackAim = () =>
            {
                Vector3 shoulders = Flatten(rightArm.position - leftArm.position);
                if (lastShoulders.HasValue)
                {
                    worstStep = Mathf.Max(worstStep, Vector3.Angle(lastShoulders.Value, shoulders));
                }

                lastShoulders = shoulders;
                worstLeft = Mathf.Max(worstLeft, Vector3.SignedAngle(shoulders, stance, Vector3.up));
                if (Now - start > nocked + 0.25f)
                {
                    float aim = Vector3.Angle(Flatten(archer.shootPoint.position - archer.stringHand.position), Flatten(playerRoot.position - archer.shootPoint.position));
                    worstAim = Mathf.Max(worstAim, aim);
                }

                return false;
            };

            yield return (Func<bool>)(() => trackAim() || archer.heldArrow.gameObject.activeSelf);
            float drawn = Now - start;
            Shot("1_Reach");
            float toQuiver = Vector3.Distance(archer.stringHand.position, archer.quiverMouth.position);

            yield return (Func<bool>)(() => trackAim() || typeof(BowString).GetField("puller", Any).GetValue(archer.bowString) != null);
            nocked = Now - start;
            float toRest = Vector3.Distance(archer.stringHand.position, archer.shootPoint.position);
            Shot("2_Nock");

            yield return (Func<bool>)(() => trackAim() || archer.anim.GetCurrentAnimatorStateInfo(0).IsName("Attack - Draw") || shot.released);
            float tellStart = Now - start;
            Shot("3_Draw");

            yield return (Func<bool>)(() => trackAim() || shot.released);
            float released = Now - start;
            yield return 0f;
            Arrow arrow = UnityEngine.Object.FindAnyObjectByType<Arrow>();
            Shot("4_Release");

            Check("the arrow shows at the quiver (hand " + toQuiver.ToString("F2") + " m from it)", Mathf.Abs(drawn - archer.reachTime) < 0.08f && toQuiver < 0.25f, drawn.ToString("F3") + " s");
            Check("the arrow goes on the string at the bow", Mathf.Abs(nocked - archer.reachTime - archer.nockSwing) < 0.08f, nocked.ToString("F3") + " s, hand " + toRest.ToString("F2") + " m from the rest");
            Check("the release beats a sprint from the edge of his range", released < 1.4f, released.ToString("F3") + " s");
            Check("the arrow stays on the player through the draw", worstAim < 8f, "worst " + worstAim.ToString("F1") + " deg from nock + 0.25 s to the release");
            Check("no snap in the body between the clips", worstStep < 6f, "worst " + worstStep.ToString("F1") + " deg in one frame");
            Check("he doesn't turn to his left in the wind-up", worstLeft < 8f, "furthest left of his stance " + worstLeft.ToString("F1") + " deg");
            Check("the final tell is 20 frames at 30 fps", Mathf.Abs(released - tellStart - 2f / 3f) < 0.08f, (released - tellStart).ToString("F3") + " s");
            Check("wind-up at least 300 ms (Combat Feel Spec)", released >= 0.3f, released.ToString("F3") + " s");
            Check("the nocked arrow hides at release", archer.heldArrow.gameObject.activeSelf == false, "");
            Check("the string lets go at release", typeof(BowString).GetField("puller", Any).GetValue(archer.bowString) == null, "");
            Check("an arrow left", arrow != null, "");
            Check("ShootPoint rides on the bow", archer.shootPoint.IsChildOf(archer.bowString.transform), archer.shootPoint.parent.name);

            if (arrow != null)
            {
                Vector3 toPlayer = (playerRoot.position + Vector3.up - arrow.transform.position).normalized;
                Check("the arrow points where it flies", Vector3.Angle(arrow.transform.forward, arrow.GetComponent<Rigidbody>().linearVelocity) < 1f, "");
                Check("it flies at the player", Vector3.Angle(arrow.GetComponent<Rigidbody>().linearVelocity, toPlayer) < 4f, Vector3.Angle(arrow.GetComponent<Rigidbody>().linearVelocity, toPlayer).ToString("F1") + " deg");
                yield return 0.25f;
                Shot("5_Flight");
            }
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        private static IEnumerator ArrowHits(float distance)
        {
            Note("--- standing still at " + distance + " m");
            Place(distance);
            float before = lastPlayerDamageTime;
            yield return (Func<bool>)(() => archer.IsAttacking);
            yield return (Func<bool>)(() => archer.attackState.released);
            float released = Now;
            yield return 0f;
            Arrow arrow = UnityEngine.Object.FindAnyObjectByType<Arrow>();
            Vector3 lastTip = Vector3.zero;
            yield return (Func<bool>)(() =>
            {
                if (arrow != null && arrow.gameObject.activeInHierarchy)
                {
                    lastTip = arrow.GetComponent<Collider>().bounds.center;
                }

                return arrow == null || arrow.gameObject.activeInHierarchy == false || lastPlayerDamageTime > before;
            });
            float flight = Now - released;
            float expected = Mathf.Max(0f, distance - 1.2f) / archer.arrowSpeed;
            yield return 0.1f;
            Check("this arrow lands on the player", lastPlayerDamageTime > before && Mathf.Abs(flight - expected) < 0.25f, flight.ToString("F2") + " s in the air (" + expected.ToString("F2") + " s expected), last at " + lastTip.ToString("F2") + ", player at " + playerRoot.position.ToString("F2") + ", he at " + archer.transform.position.ToString("F2"));
            Check("arrow damage", Mathf.Approximately(lastPlayerDamage, archer.attackDamage), lastPlayerDamage.ToString("F1"));
        }

        private static IEnumerator FireRate()
        {
            Note("--- fire rate, player standing in range");
            Place(7f);
            var releases = new List<float>();
            for (int i = 0; i < 3; i++)
            {
                yield return (Func<bool>)(() => archer.IsAttacking && archer.attackState.released == false);
                yield return (Func<bool>)(() => archer.attackState.released);
                releases.Add(Now);
                combat.Poise.Reset();
                playerHealth.SetMax(100000f, true);
                yield return (Func<bool>)(() => archer.IsAttacking == false);
            }

            float gap = (releases[2] - releases[0]) / 2f;
            Check("one arrow every 1 / fireRate seconds", Mathf.Abs(gap - 1f / archer.fireRate) < 0.2f, gap.ToString("F2") + " s between arrows");
        }

        private static IEnumerator WaitForArrowWithin(float distance)
        {
            yield return (Func<bool>)(() =>
            {
                Arrow arrow = UnityEngine.Object.FindAnyObjectByType<Arrow>();
                return arrow != null && Vector3.Distance(arrow.GetComponent<Collider>().bounds.center, playerRoot.position + Vector3.up) < distance;
            });
        }

        private static IEnumerator Block()
        {
            Note("--- blocking, held well before it arrives");
            Place(7f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            yield return 0.8f;
            Press("OnBlockPressed");
            float before = lastPlayerDamageTime;
            float parryBefore = combat.LastParryTime;
            float blockBefore = combat.LastBlockHitTime;
            float poiseBefore = combat.Poise.Current;
            yield return (Func<bool>)(() => combat.LastBlockHitTime > blockBefore);
            Shot("Block");
            Check("the shield takes the arrow", combat.LastBlockHitTime > blockBefore, "");
            Check("a block costs a fifth of the damage", Mathf.Abs(lastPlayerDamage - archer.attackDamage * 0.2f) < 0.01f && lastPlayerDamageTime > before, lastPlayerDamage.ToString("F1"));
            Check("a block costs half the poise", Mathf.Abs(poiseBefore - combat.Poise.Current - archer.attackPoiseDamage * 0.5f) < 1f, (poiseBefore - combat.Poise.Current).ToString("F1"));
            Check("no parry", Mathf.Approximately(combat.LastParryTime, parryBefore), "");
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
            yield return 0.3f;
            Check("the arrow is gone", UnityEngine.Object.FindAnyObjectByType<Arrow>() == null, "");
        }

        private static IEnumerator TryToParry()
        {
            Note("--- trying to parry it: block pressed just as it arrives, inside the parry window");
            Place(7f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            float parryBefore = combat.LastParryTime;
            float blockBefore = combat.LastBlockHitTime;
            yield return WaitForArrowWithin(archer.arrowSpeed * 0.1f + 0.3f);
            Press("OnBlockPressed");
            bool windowOpen = false;
            yield return (Func<bool>)(() =>
            {
                windowOpen |= combat.IsParryWindowOpen;
                return combat.LastBlockHitTime > blockBefore;
            });
            Check("the window was open as it landed", windowOpen, "");
            Check("no parry on an arrow", Mathf.Approximately(combat.LastParryTime, parryBefore), "last parry " + combat.LastParryTime.ToString("F2"));
            Check("it's a plain block", combat.LastBlockHitTime > blockBefore, "took " + lastPlayerDamage.ToString("F1"));
            Check("the archer isn't staggered", archer.IsStaggered == false, archer.stateMachine.currentState.GetType().Name);
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
        }

        private static IEnumerator RollThrough()
        {
            Note("--- rolling through the arrow, toward him");
            Place(7f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            float before = lastPlayerDamageTime;
            yield return WaitForArrowWithin(2f);
            Arrow arrow = UnityEngine.Object.FindAnyObjectByType<Arrow>();
            Roll((archer.transform.position - playerRoot.position).normalized);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Dodging);
            playerMove = Vector3.zero;
            yield return 0.15f;
            Shot("Roll");
            yield return 0.3f;

            Check("the arrow flies on past", arrow != null && arrow.gameObject.activeInHierarchy, "");
            if (arrow != null)
            {
                Check("it's behind the player", Vector3.Dot(arrow.GetComponent<Collider>().bounds.center - playerRoot.position, playerRoot.position - archer.transform.position) > 0f, "");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            Check("the roll takes no damage", lastPlayerDamageTime <= before, "");
        }

        private static IEnumerator SideStep()
        {
            Note("--- walking sideways as he looses");
            Place(9f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            yield return (Func<bool>)(() => archer.attackState.released);
            float before = lastPlayerDamageTime;
            playerMove = Vector3.Cross(Vector3.up, archer.transform.forward).normalized;
            yield return 1.5f;
            playerMove = Vector3.zero;
            Check("a walk sideways beats it at range", lastPlayerDamageTime <= before, "");
        }

        private static IEnumerator CloseTheDistance()
        {
            Note("--- closing from the edge of his range, rolling his arrow, then hitting him");
            Place(9.5f);

            // Sets off the moment he starts his reach: the best a player can do, and the arrow still has to be dealt with.
            yield return (Func<bool>)(() => archer.IsAttacking);
            typeof(PlayerMovement).GetField("sprintHeld", Any).SetValue(movement, true);
            float start = Now;
            float before = lastPlayerDamageTime;
            int rolls = 0;
            float health = archer.health.Current;

            while (Vector3.Distance(playerRoot.position, archer.transform.position) > 1.3f && Now - start < 8f)
            {
                playerMove = (archer.transform.position - playerRoot.position).normalized;
                Arrow arrow = UnityEngine.Object.FindAnyObjectByType<Arrow>();
                if (arrow != null && combat.State == PlayerCombat.CombatState.Armed
                    && Vector3.Dot(arrow.GetComponent<Rigidbody>().linearVelocity, playerRoot.position - arrow.transform.position) > 0f
                    && Vector3.Distance(arrow.GetComponent<Collider>().bounds.center, playerRoot.position + Vector3.up) < 3.2f)
                {
                    Roll(playerMove);
                    rolls++;
                    yield return 0.9f;
                }

                yield return 0f;
            }

            typeof(PlayerMovement).GetField("sprintHeld", Any).SetValue(movement, false);
            playerMove = Vector3.zero;
            float closed = Now - start;
            Note("closed in " + closed.ToString("F2") + " s with " + rolls + " roll(s)");
            Check("an arrow had to be rolled", rolls >= 1, rolls + " roll(s)");
            Check("reached him unhurt", lastPlayerDamageTime <= before, "");
            Check("in reach", Vector3.Distance(playerRoot.position, archer.transform.position) < 1.6f, Vector3.Distance(playerRoot.position, archer.transform.position).ToString("F2") + " m");

            yield return 0.05f;
            Press("OnStrikePressed");
            yield return 0.8f;
            Check("the strike lands", archer.health.Current < health, (health - archer.health.Current).ToString("F1"));
            Shot("Melee");
        }

        private static IEnumerator SprintWithoutRolling()
        {
            Note("--- sprinting straight in as he starts his reach, no roll");
            Place(9.5f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            float start = Now;
            float before = lastPlayerDamageTime;
            typeof(PlayerMovement).GetField("sprintHeld", Any).SetValue(movement, true);
            float atRelease = -1f;

            while (lastPlayerDamageTime <= before && Now - start < 3f)
            {
                playerMove = (archer.transform.position - playerRoot.position).normalized;
                if (atRelease < 0f && archer.attackState.released)
                {
                    atRelease = Vector3.Distance(playerRoot.position, archer.transform.position);
                }

                yield return 0f;
            }

            typeof(PlayerMovement).GetField("sprintHeld", Any).SetValue(movement, false);
            playerMove = Vector3.zero;
            Check("the arrow leaves before the player arrives", atRelease > 1.6f, atRelease.ToString("F2") + " m out at the release");
            Check("and it hits them", lastPlayerDamageTime > before, Vector3.Distance(playerRoot.position, archer.transform.position).ToString("F2") + " m out when it landed, " + (Now - start).ToString("F2") + " s in");
        }

        private static IEnumerator HitInTheWindup()
        {
            Note("--- a hit during the draw");
            Place(1.2f);
            yield return (Func<bool>)(() => archer.IsAttacking);
            yield return 0.45f;
            Check("the arrow is in his hand", archer.heldArrow.gameObject.activeSelf, "");
            float health = archer.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => archer.health.Current < health);
            yield return 0.05f;
            Check("a light in the draw breaks the shot", archer.IsStaggered, archer.stateMachine.currentState.GetType().Name + ", dealt " + (health - archer.health.Current).ToString("F1"));
            Check("the arrow goes with it", archer.heldArrow.gameObject.activeSelf == false, "");
            Check("the string lets go", typeof(BowString).GetField("puller", Any).GetValue(archer.bowString) == null, "");
        }

        private static IEnumerator HeavyStaggers()
        {
            Note("--- design note: low poise, staggers on any heavy hit");
            archer.attackRange = 0f;
            Place(1.5f);
            yield return 0.6f;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => archer.IsStaggered);
            Check("a heavy staggers him", archer.IsStaggered, archer.stateMachine.currentState.GetType().Name + ", dealt " + (archer.health.Max - archer.health.Current).ToString("F1"));
            Shot("Stagger");
            yield return archer.staggerTime + 0.3f;
        }

        private static IEnumerator TwoLightsAndHeavy()
        {
            Note("--- design note: 2 light hits + 1 heavy");
            archer.attackRange = 0f;
            archer.poise.maxPoise = 1000f;
            archer.poise.Reset();
            Place(1.2f);
            yield return 0.6f;
            Press("OnStrikePressed");
            yield return 1.6f;
            float first = archer.health.Current;
            PlaceBy(1.2f);
            yield return 0.1f;
            Press("OnStrikePressed");
            yield return 1.6f;
            float second = archer.health.Current;
            Check("still standing after two lights", archer.IsDead == false, first.ToString("F1") + " then " + second.ToString("F1"));
            PlaceBy(1.3f);
            yield return 0.1f;
            Press("OnHeavyPressed");
            yield return 2.2f;
            Check("the heavy finishes him", archer.IsDead, "health " + archer.health.Current.ToString("F1"));
            archer.poise.maxPoise = 30f;
            Shot("Dead");
            yield return 2.4f;
        }

        private static IEnumerator Riposte()
        {
            Note("--- design note: 1 riposte. He can't be parried, so the opening is a stagger from a broken poise");
            archer.attackRange = 0f;
            Place(1.5f);
            yield return 0.6f;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => archer.IsStaggered);
            float after = archer.health.Current;
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            PlaceBy(1.3f);
            yield return 0.05f;
            Press("OnHeavyPressed");
            yield return 1.6f;
            Check("a heavy on the stagger finishes him", archer.IsDead, "first left " + after.ToString("F1") + ", then " + archer.health.Current.ToString("F1"));
            yield return 2.4f;
        }

        // A planted foot has to travel backward along the body exactly as fast as the body goes forward, so this measures
        // the feet's backward speed mid-stance in the body's space, against the agent's speed.
        private static IEnumerator FootSlide(bool patrol)
        {
            Note("--- feet on " + (patrol ? "patrol" : "the chase"));
            archer.attackRange = 0f;
            archer.leashRadius = 40f;
            archer.aggressionRange = patrol ? 0f : 40f;
            archer.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            float target = patrol ? archer.moveSpeed : archer.chaseSpeed;

            if (patrol)
            {
                Place(45f);
                yield return (Func<bool>)(() => archer.stateMachine.currentState == archer.moveState && archer.agent.velocity.magnitude > target * 0.8f);
            }
            else
            {
                Place(13f);
                yield return (Func<bool>)(() => archer.stateMachine.currentState == archer.chaseState);
            }

            yield return 0.6f;

            Transform body = archer.transform;
            Transform leftFoot = archer.anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = archer.anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var left = new List<(float time, float along)>();
            var right = new List<(float time, float along)>();
            float until = Now + 2.2f;
            float agentSpeed = 0f;
            int frames = 0;

            while (Now < until)
            {
                yield return 0f;
                left.Add((Time.time, Vector3.Dot(leftFoot.position - body.position, body.forward)));
                right.Add((Time.time, Vector3.Dot(rightFoot.position - body.position, body.forward)));
                agentSpeed += archer.agent.velocity.magnitude;
                frames++;
            }

            agentSpeed /= Mathf.Max(1, frames);
            archer.anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            float stance = (StanceSpeed(left) + StanceSpeed(right)) * 0.5f;
            Check("agent reaches " + target.ToString("F2") + " m/s", Mathf.Abs(agentSpeed - target) < 0.2f, agentSpeed.ToString("F2"));
            Check("feet keep pace with the ground", Mathf.Abs(stance - agentSpeed) < target * 0.15f, "stance " + stance.ToString("F2") + " m/s against " + agentSpeed.ToString("F2") + " m/s of travel (" + (Mathf.Abs(stance - agentSpeed) / agentSpeed * 100f).ToString("F0") + "% slip)");
        }

        private static float StanceSpeed(List<(float time, float along)> track)
        {
            float front = track.Max(s => s.along);
            float back = track.Min(s => s.along);
            float middle = (front + back) * 0.5f;
            float half = (front - back) * 0.25f;

            var speeds = new List<float>();
            for (int i = 3; i < track.Count - 3; i++)
            {
                float dt = track[i + 3].time - track[i - 3].time;
                float velocity = dt > 0.0001f ? (track[i + 3].along - track[i - 3].along) / dt : 0f;
                if (velocity < 0f && Mathf.Abs(track[i].along - middle) < half)
                {
                    speeds.Add(-velocity);
                }
            }

            return speeds.Count > 0 ? speeds.Average() : 0f;
        }

        // --- Level check ---

        // Every archer in the level: on his NavMesh, walks his post without leaving it or sticking to its edge, stops at
        // the edge when the player is beyond it, obeys the gate where there is one, shoots, and goes home.
        private static IEnumerator LevelCheck()
        {
            FindPlayer();
            heldAway = null;
            playerHealth.SetMax(100000f, true);
            yield return 1f;

            HollowArcher[] all = UnityEngine.Object.FindObjectsByType<HollowArcher>(FindObjectsInactive.Include)
                .Where(a => a.gameObject.scene.IsValid()).ToArray();
            Note(all.Length + " archers in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            foreach (HollowArcher candidate in all)
            {
                yield return Post(candidate);
            }
        }

        private static IEnumerator Post(HollowArcher candidate)
        {
            archer = candidate;
            string label = candidate.transform.parent != null ? candidate.transform.parent.name : candidate.name;
            Vector3 post = candidate.Home;
            string nav = NavMesh.GetSettingsNameFromID(candidate.agent.agentTypeID);
            Note("--- " + label + " at " + post.ToString("F1") + " on " + nav);
            Check("is on his NavMesh", candidate.agent.enabled && candidate.agent.isOnNavMesh, nav);

            // Out of the way, so he walks his post undisturbed. A scripted walk this slow gets through more legs at speed.
            MoveAway();
            var gate = candidate.GetComponentInParent<OnScreenGate>();
            float until = Now + 9f;
            float furthest = 0f;
            float stuckFor = 0f;
            float worstStuck = 0f;
            int legs = 0;
            EnemyState last = candidate.stateMachine.currentState;
            Time.timeScale = 3f;
            while (Now < until)
            {
                yield return 0f;
                furthest = Mathf.Max(furthest, Flat(candidate.transform.position - post));
                bool walking = candidate.stateMachine.currentState == candidate.moveState;
                stuckFor = walking && candidate.agent.velocity.magnitude < 0.05f ? stuckFor + Time.unscaledDeltaTime * 3f : 0f;
                worstStuck = Mathf.Max(worstStuck, stuckFor);
                if (candidate.stateMachine.currentState != last)
                {
                    last = candidate.stateMachine.currentState;
                    if (last == candidate.idleState)
                    {
                        legs++;
                    }
                }
            }

            Time.timeScale = 1f;
            Check("walks his post (" + legs + " legs, furthest " + furthest.ToString("F1") + " m)", legs >= 1 && furthest > 0.5f, "");
            Check("stays within his leash", furthest <= candidate.leashRadius + 0.6f, furthest.ToString("F1") + " m");
            Check("never stands walking on the spot", worstStuck < 1.2f, worstStuck.ToString("F1") + " s");

            // Beyond his range, on open ground: he comes as far as his post allows and stands.
            Vector3 outward = BestDirection(post);
            float saved = candidate.attackRange;
            candidate.attackRange = 0f;
            PlaceOnGround(post + outward * Mathf.Min(candidate.aggressionRange - 1f, 11f));
            yield return (Func<bool>)(() => candidate.stateMachine.currentState == candidate.chaseState);
            yield return 8f;
            float settled = candidate.agent.velocity.magnitude;
            Vector3 stood = candidate.transform.position;
            yield return 1.5f;
            float crept = Flat(candidate.transform.position - stood);
            Check("stands at the end of his leash (state " + candidate.stateMachine.currentState.GetType().Name + ", " + Flat(stood - post).ToString("F1") + " m out)", Flat(stood - post) <= candidate.leashRadius + 0.6f, "");
            Check("isn't walking on the spot", settled < 0.1f && crept < 0.1f, "speed " + settled.ToString("F2") + ", crept " + crept.ToString("F2"));
            Check("isn't left off his NavMesh", candidate.agent.isOnNavMesh, "");
            Shot("Post_" + label);
            candidate.attackRange = saved;

            // Down-screen of him is where a player fights him: anywhere else he can be off the frame, and on a wall a
            // gated archer rightly holds his shot.
            Vector3 downScreen = -Camera.main.transform.forward;
            downScreen.y = 0f;
            downScreen.Normalize();

            if (gate != null)
            {
                yield return Gate(candidate, gate, post, downScreen);
            }

            // In range: he shoots.
            PlaceOnGround(stood + downScreen * 4f);
            float shotBy = Now + 8f;
            yield return (Func<bool>)(() => candidate.IsAttacking || Now > shotBy);
            Check("shoots a player in range", candidate.IsAttacking, candidate.stateMachine.currentState.GetType().Name);
            yield return (Func<bool>)(() => candidate.IsAttacking && candidate.attackState.released);
            yield return 0.4f;
            Shot("Shot_" + label);
            yield return (Func<bool>)(() => candidate.IsAttacking == false);

            // Gone: back to his post.
            MoveAway();
            float gaveUp = Now + candidate.giveUpTime + 2f;
            yield return (Func<bool>)(() => candidate.stateMachine.currentState != candidate.chaseState || Now > gaveUp);
            Time.timeScale = 3f;
            yield return 2.5f;
            Time.timeScale = 1f;
            Check("gives up and goes back to his post", Flat(candidate.transform.position - post) <= candidate.leashRadius + 0.6f && candidate.stateMachine.currentState != candidate.chaseState, candidate.stateMachine.currentState.GetType().Name + ", " + Flat(candidate.transform.position - post).ToString("F1") + " m from it");
            if (gate != null)
            {
                Check("no wind-up started off screen", gate.ViolationCount == 0, gate.ViolationCount + " violations");
            }
        }

        // L4 decision 9: a player he can reach but who hasn't had him on screen for a second doesn't get shot.
        private static IEnumerator Gate(HollowArcher candidate, OnScreenGate gate, Vector3 post, Vector3 downScreen)
        {
            gate.ResetState();
            typeof(HollowArcher).GetField("nextShotTime", Any).SetValue(candidate, 0f);
            Check("the gate shuts a fresh archer's first shot", candidate.CanStartShot == false, "");

            PlaceOnGround(post + downScreen * 3f);
            yield return (Func<bool>)(() => gate.IsInFrame(Camera.main));
            float inFrame = Now;
            Check("he doesn't start the moment he's in frame", candidate.IsAttacking == false, "");
            yield return (Func<bool>)(() => candidate.IsAttacking);
            float waited = Now - inFrame;
            Check("he waits his second in frame", waited >= 0.9f, waited.ToString("F2") + " s");
            Check("the shot is logged as allowed", gate.ViolationCount == 0, "");
            yield return (Func<bool>)(() => candidate.IsAttacking == false);
            MoveAway();
            yield return 0.5f;
        }

        private static float Flat(Vector3 offset)
        {
            offset.y = 0f;
            return offset.magnitude;
        }

        // The direction with the most walkable ground for the player around his post, from the ground below it.
        private static Vector3 BestDirection(Vector3 post)
        {
            float best = -1f;
            Vector3 direction = Vector3.forward;
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (Physics.Raycast(post + candidate * 9f + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f, GroundMask(), QueryTriggerInteraction.Ignore)
                    && Physics.Raycast(post + Vector3.up * 1.4f, candidate, out RaycastHit wall, 9f, GroundMask(), QueryTriggerInteraction.Ignore) == false)
                {
                    float score = 9f - Mathf.Abs(hit.point.y - post.y);
                    if (score > best)
                    {
                        best = score;
                        direction = candidate;
                    }
                }
            }

            return direction;
        }

        private static int GroundMask() => LayerMask.GetMask("Ground", "Obstacles");

        private static void PlaceOnGround(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 30f, GroundMask(), QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
            }

            heldAway = null;
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            playerRoot.position = point + Vector3.up * 0.1f;
            controller.enabled = true;
        }

        // Holds the player high above him until the next placement: a player just put up there falls back down in 3 s.
        private static void MoveAway()
        {
            heldAway = archer.transform.position + Vector3.up * 60f;
            HoldAway();
        }

        private static void HoldAway()
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            playerRoot.position = heldAway.Value;
            controller.enabled = true;
        }

        // --- Player driving ---

        private static void DriveInput()
        {
            if (archer == null || Camera.main == null || combat == null)
            {
                return;
            }

            // At his feet: the aim ray stops at the ground, so aiming at his chest lands past him and turns the player away.
            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(archer.transform.position));

            Quaternion yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * playerMove;
            typeof(PlayerMovement).GetField("moveInput", Any).SetValue(movement, new Vector2(local.x, local.z));
        }

        private static void Press(string handler)
        {
            typeof(PlayerCombat).GetMethod(handler, Any).Invoke(combat, null);
        }

        private static void Roll(Vector3 direction)
        {
            playerMove = direction;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
        }

        // `distance` metres out from his post along the open ground, facing him.
        private static void Place(float distance)
        {
            Vector3 position = home + homeFacing * Vector3.forward * distance;

            PlaceAt(position);
        }

        // `distance` metres from where he stands now, on the player's side of him.
        private static void PlaceBy(float distance)
        {
            Vector3 toPlayer = playerRoot.position - archer.transform.position;
            toPlayer.y = 0f;
            PlaceAt(archer.transform.position + toPlayer.normalized * distance);
        }

        private static void PlaceAt(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, GroundMask(), QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + 0.05f;
            }

            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            playerRoot.position = position;

            Vector3 toArcher = archer.transform.position - position;
            toArcher.y = 0f;
            if (toArcher.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toArcher);
            }

            controller.enabled = true;
        }

        // --- Reporting ---

        private static void Shot(string name)
        {
            if (TakeShots)
            {
                ScreenCapture.CaptureScreenshot("Temp/Archer_" + name + ".png");
            }
        }

        private static void Check(string what, bool passed, string detail)
        {
            Note((passed ? "ok   " : "FAIL ") + what + (detail.Length > 0 ? ": " + detail : ""));
            if (passed == false)
            {
                failures++;
            }
        }

        private static void Note(string line)
        {
            Lines.Add(line);
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(message);
            }
        }
    }
}
