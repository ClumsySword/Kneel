using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Scripted Play-mode walk of L2 with the real player controller (see CLAUDE.md: input is written by
    // reflection from EditorApplication.update because simulated input is unreliable in an unfocused Editor).
    //  1. Walks the NavMesh path from PlayerStart to Exit_ToL3 at walk speed, facing the way it moves, and
    //     times it with unscaled time.
    //  2. Physical checks: the ledge can't be climbed back, each blocker and the closed gate hold, and the weapon
    //     alley is entered through the fire gap.
    // Results go to the Console with an [L2 Playtest] prefix and to Temp/L2_Playtest.txt.
    public static class L2Playtest
    {
        private const string ResultPath = "Temp/L2_Playtest.txt";

        private struct Probe
        {
            public string Name;
            public Vector2 From;
            public Vector2 To;
            public float Seconds;
            public bool ExpectReach;

            public Probe(string name, Vector2 from, Vector2 to, float seconds, bool expectReach)
            {
                Name = name;
                From = from;
                To = to;
                Seconds = seconds;
                ExpectReach = expectReach;
            }
        }

        private static readonly List<Vector3> Waypoints = new List<Vector3>();
        private static readonly Queue<Probe> Probes = new Queue<Probe>();
        private static readonly StringBuilder Log = new StringBuilder();
        private static int waypoint;
        private static float startTime, legStart, lastProgressTime, bestDistance;
        private static Probe probe;
        private static bool walking, probing, sprint;
        private static Component movement;
        private static int previousFrameRate = -1, previousVSync;
        private static float lastReplan;

        // Oscillation watch: positions every 0.5 s; barely any net travel over 20 s is stuck too (re-planning
        // keeps resetting the progress timer, so a knight swinging between two plans would never trip it).
        private static readonly List<(float time, Vector3 pos)> Trace = new List<(float, Vector3)>();
        private static float lastTrace;
        private const string TracePath = "Temp/L2_Playtest_Trace.txt";
        private static int lastProgressFrame;
        private static Vector3 exitPoint;
        private static FieldInfo moveField, aimField, sprintField;

        [MenuItem("Kneel/L2/Playtest/Walk Critical Path + Checks")]
        public static void RunMenu()
        {
            Run(false);
        }

        public static void Run(bool useSprint)
        {
            System.IO.File.WriteAllText(ResultPath, "running");
            SessionState.SetBool("L2Playtest.Pending", true);
            SessionState.SetBool("L2Playtest.Sprint", useSprint);
            Application.runInBackground = true;

            // If entering Play mode reloads the domain, Resume() runs again from InitializeOnLoadMethod.
            Resume();
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("L2Playtest.Pending", false))
            {
                return;
            }

            sprint = SessionState.GetBool("L2Playtest.Sprint", false);
            Log.Clear();
            walking = probing = false;
            movement = null;
            Application.runInBackground = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static string LastResult => System.IO.File.Exists(ResultPath) ? System.IO.File.ReadAllText(ResultPath) : "";

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                return;
            }

            if (movement == null)
            {
                // Setup, then start the walk next frame (the NavMesh update lifting the gate's carve has run by then).
                Setup();
                return;
            }

            if (!walking && !probing)
            {
                BeginWalk();
                return;
            }

            if (walking)
            {
                TickWalk();
            }
            else
            {
                TickProbe();
            }
        }

        private static bool Setup()
        {
            var player = GameObject.Find("Player");
            if (player == null || Time.unscaledTime < 0.5f)
            {
                return false;
            }

            // PlayerMovement can't get going above ~100 fps (its wall clamp floors speed at 0.1 m/s, which is under
            // CharacterController.minMoveDistance per frame), and an unfocused Editor runs at ~500 fps. Walk at 60.
            previousFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // Plan as a player who knows the levers are there: the shut gates' NavMesh carves are lifted for
            // planning; their colliders still stop the knight until he pulls each lever on the way past.
            foreach (var gateObstacle in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include))
            {
                if (gateObstacle.GetComponentInParent<Kneel.ShortcutGate>() != null)
                {
                    gateObstacle.enabled = false;
                }
            }

            movement = player.GetComponent("PlayerMovement");
            var type = movement.GetType();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            moveField = type.GetField("moveInput", flags);
            aimField = type.GetField("aimInput", flags);
            sprintField = type.GetField("sprintHeld", flags);
            foreach (var c in player.GetComponents<MonoBehaviour>())
            {
                var controls = c.GetType().GetField("controls", flags);
                if (controls != null && controls.GetValue(c) is PlayerControls pc)
                {
                    pc.Disable();
                }
            }

            return true;
        }

        private static void BeginWalk()
        {
            var gameplay = GameObject.Find("_Gameplay").transform;
            var start = gameplay.Find("PlayerStart").position;
            var exit = gameplay.Find("Exit_ToL3").position;
            NavMesh.SamplePosition(start, out var a, 2f, NavMesh.AllAreas);
            NavMesh.SamplePosition(exit, out var b, 2f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            Waypoints.Clear();
            Waypoints.AddRange(path.corners);
            float length = 0f;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                length += (Waypoints[i] - Waypoints[i - 1]).magnitude;
            }

            Log.AppendLine($"Walk: NavMesh path {path.status}, {Waypoints.Count} corners, {length:F0} m.");
            Trace.Clear();
            System.IO.File.WriteAllText(TracePath, "");
            exitPoint = b.position;
            Teleport(start);
            waypoint = 1;
            startTime = legStart = lastProgressTime = Time.unscaledTime;
            bestDistance = float.MaxValue;
            walking = true;
        }

        private static void TickWalk()
        {
            var pos = movement.transform.position;
            if (waypoint >= Waypoints.Count)
            {
                float seconds = Time.unscaledTime - startTime;
                Log.AppendLine($"Walk: reached Exit_ToL3 in {seconds:F0} s ({seconds / 60f:F1} min) of pure walking{(sprint ? " (sprinting)" : "")}.");
                walking = false;
                QueueProbes();
                probing = true;
                NextProbe();
                return;
            }

            // (Stuck needs 5 s *and* 240 frames without progress: the Editor sometimes stalls for seconds.)
        // Re-plan from where the knight actually is, so drift round corners never leaves it aiming into a wall.
            // (Not in mid-air: a plan from there would snap to whatever lies below.)
            var grounded = movement.GetComponent<CharacterController>().isGrounded;
            if (grounded && Time.unscaledTime - lastReplan > 1f && NavMesh.SamplePosition(pos, out var here, 1f, NavMesh.AllAreas))
            {
                lastReplan = Time.unscaledTime;
                var replan = new NavMeshPath();
                if (NavMesh.CalculatePath(here.position, exitPoint, NavMesh.AllAreas, replan) && replan.status == NavMeshPathStatus.PathComplete && replan.corners.Length > 1)
                {
                    if ((replan.corners[1] - Waypoints[Mathf.Min(waypoint, Waypoints.Count - 1)]).sqrMagnitude > 0.25f)
                    {
                        bestDistance = float.MaxValue;
                    }

                    Waypoints.Clear();
                    Waypoints.AddRange(replan.corners);
                    waypoint = 1;
                }
            }

            if (Time.unscaledTime - lastTrace > 0.5f)
            {
                lastTrace = Time.unscaledTime;
                Trace.Add((Time.unscaledTime, pos));
                var aim = Waypoints[Mathf.Min(waypoint, Waypoints.Count - 1)];
                System.IO.File.AppendAllText(TracePath, $"{Time.unscaledTime - startTime:F1} {pos.x:F2} {pos.y:F2} {pos.z:F2} wp{waypoint} -> {aim.x:F1},{aim.z:F1}\n");
                var old = Trace.Find(t => t.time > Time.unscaledTime - 20.5f);
                if (Time.unscaledTime - startTime > 25f && old.time < Time.unscaledTime - 19f && Flat(pos - old.pos).magnitude < 2.5f)
                {
                    Log.AppendLine($"Walk: STUCK (going back and forth) at ({pos.x:F1},{pos.y:F1},{pos.z:F1}) after {Time.unscaledTime - startTime:F0} s; trace in {TracePath}.");
                    walking = false;
                    QueueProbes();
                    probing = true;
                    NextProbe();
                    return;
                }
            }

            // The gates are on the route: pull each lever when passing it, as a player pressing E would.
            foreach (var routeLever in Object.FindObjectsByType<Kneel.GateLever>(FindObjectsInactive.Exclude))
            {
                if (!routeLever.Pulled && routeLever.InReach)
                {
                    routeLever.TryPull();
                    Log.AppendLine($"Walk: pulled the {routeLever.transform.parent.name} lever after {Time.unscaledTime - startTime:F0} s.");
                }
            }

            var target = Waypoints[waypoint];
            Drive(target);
            float d = Flat(target - pos).magnitude;
            if (d < bestDistance - 0.05f)
            {
                bestDistance = d;
                lastProgressTime = Time.unscaledTime;
                lastProgressFrame = Time.frameCount;
            }

            // Corners at a drop: arrive by plan distance, not height.
            if (d < 0.7f)
            {
                waypoint++;
                bestDistance = float.MaxValue;
                lastProgressTime = Time.unscaledTime;
                lastProgressFrame = Time.frameCount;
            }
            else if (Time.unscaledTime - lastProgressTime > 5f && Time.frameCount - lastProgressFrame > 240)
            {
                Log.AppendLine($"Walk: STUCK at ({pos.x:F1},{pos.y:F1},{pos.z:F1}) heading to corner {waypoint} ({target.x:F1},{target.y:F1},{target.z:F1}) after {Time.unscaledTime - startTime:F0} s.");
                walking = false;
                QueueProbes();
                probing = true;
                NextProbe();
            }
        }

        private static void QueueProbes()
        {
            Probes.Clear();
            Probes.Enqueue(new Probe("Church stair back up", L2Layout.StairFoot + new Vector2(0f, 1.5f), L2Layout.StairTop + new Vector2(-1.5f, -2f), 10f, true));
            foreach (var b in L2Layout.Blockers)
            {
                Probes.Enqueue(new Probe(b.Name, b.Near, b.Far, 5f, false));
            }

            foreach (var g in L2Layout.Gates)
            {
                Probes.Enqueue(new Probe("GATESHUT:" + g.Name + " (closed)", g.Center - g.Forward * 4f, g.Center + g.Forward * 4f, 5f, false));
            }

            Probes.Enqueue(new Probe("Weapon alley through the fire gap", new Vector2(25f, 71f), L2Layout.Markers["Loot_WeaponUpgrade"], 20f, true));

            // Gameplay: standing in fire hurts in ticks without stunning; the lever opens the gate from the crossroads.
            Probes.Enqueue(new Probe("FIRE:Stand in the E1 fire bed", L2Layout.FireZones[0].Center, L2Layout.FireZones[0].Center, 3.2f, true));
            foreach (var g in L2Layout.Gates)
            {
                Probes.Enqueue(new Probe("GATE:" + g.Name + " pull the lever, walk through", g.Lever - g.Forward * 0.6f - g.Along * 0.6f, g.Center + g.Forward * 4f, 14f, true));
            }
        }

        private static void NextProbe()
        {
            if (Probes.Count == 0)
            {
                Finish();
                return;
            }

            probe = Probes.Dequeue();
            Teleport(new Vector3(probe.From.x, L2Layout.Height(probe.From) + 0.1f, probe.From.y));
            legStart = Time.unscaledTime;
            healthAtStart = Health.Current;
            stunned = false;
            if (probe.Name.StartsWith("GATESHUT:"))
            {
                // The walk opened the gate on its way through: shut it again for this check.
                var shut = ProbeLever();
                if (shut != null)
                {
                    shut.ResetLever();
                }
            }

            if (probe.Name.StartsWith("GATE:"))
            {
                var lever = ProbeLever();
                if (lever != null)
                {
                    lever.TryPull();
                }

                gatePulled = lever != null && lever.Pulled;
            }
        }

        // The lever of the gate a GATE/GATESHUT probe names ("GATE:Gate_E4 ...").
        private static Kneel.GateLever ProbeLever()
        {
            string name = probe.Name.Substring(probe.Name.IndexOf(':') + 1).Split(' ')[0];
            var gate = GameObject.Find("Landmarks/Gates/" + name);
            return gate != null ? gate.GetComponentInChildren<Kneel.GateLever>() : null;
        }

        private static Health Health => movement.GetComponent<Health>();

        private static float healthAtStart;
        private static bool stunned, gatePulled;

        private static void TickProbe()
        {
            var pos = movement.transform.position;
            var target = new Vector3(probe.To.x, L2Layout.Height(probe.To), probe.To.y);
            var combat = movement.GetComponent("PlayerCombat");
            stunned |= combat != null && combat.GetType().GetProperty("State").GetValue(combat).ToString() == "HitStun";

            // Fire: stand still in it for the whole time, then report the burn.
            if (probe.Name.StartsWith("FIRE:"))
            {
                moveField.SetValue(movement, Vector2.zero);
                if (Time.unscaledTime - legStart > probe.Seconds)
                {
                    float burnt = healthAtStart - Health.Current;
                    bool pass = burnt >= 10f && !stunned;
                    Log.AppendLine($"Check '{probe.Name.Substring(5)}': took {burnt:F0} damage in {probe.Seconds:F1} s, hit-stun {(stunned ? "YES" : "no")} -> {(pass ? "PASS" : "FAIL")}.");
                    NextProbe();
                }

                return;
            }

            // Gate: give the lever and the swing a moment, then walk through.
            if (probe.Name.StartsWith("GATE:") && Time.unscaledTime - legStart < 2.2f)
            {
                moveField.SetValue(movement, Vector2.zero);
                return;
            }

            Drive(target);
            bool reached = Flat(target - pos).magnitude < 1.2f && Mathf.Abs(target.y - pos.y) < 1.2f;
            if (reached || Time.unscaledTime - legStart > probe.Seconds)
            {
                if (probe.Name.StartsWith("GATE:"))
                {
                    Log.Append(gatePulled ? "(lever pulled) " : "(LEVER NOT PULLED) ");
                }

                bool pass = reached == probe.ExpectReach;
                Log.AppendLine($"Check '{probe.Name}': {(reached ? "reached" : "did not reach")} target, ended at ({pos.x:F1},{pos.y:F1},{pos.z:F1}) -> {(pass ? "PASS" : "FAIL")}.");
                NextProbe();
            }
        }

        private static void Finish()
        {
            probing = false;
            SessionState.SetBool("L2Playtest.Pending", false);
            if (previousFrameRate != -1 || previousVSync != 0)
            {
                Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVSync;
            }

            EditorApplication.update -= Tick;
            movement = null;
            string result = Log.ToString();
            System.IO.File.WriteAllText(ResultPath, result);
            Debug.Log("[L2 Playtest]\n" + result);
            EditorApplication.isPlaying = false;
        }

        // Walks toward the target: camera-relative input (the camera looks along yaw 0, so input is world XZ),
        // aim on the target so the knight walks forward, not strafing.
        private static void Drive(Vector3 target)
        {
            var pos = movement.transform.position;
            var dir = Flat(target - pos).normalized;
            float yaw = Camera.main.transform.eulerAngles.y * Mathf.Deg2Rad;
            var local = new Vector2(dir.x * Mathf.Cos(yaw) - dir.z * Mathf.Sin(yaw), dir.x * Mathf.Sin(yaw) + dir.z * Mathf.Cos(yaw));
            moveField.SetValue(movement, local);
            Vector3 screen = Camera.main.WorldToScreenPoint(pos + dir * 3f);
            aimField.SetValue(movement, new Vector2(screen.x, screen.y));
            sprintField.SetValue(movement, sprint);
        }

        private static void Teleport(Vector3 p)
        {
            var cc = movement.GetComponent<CharacterController>();
            cc.enabled = false;
            movement.transform.position = p + Vector3.up * 0.05f;
            cc.enabled = true;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
