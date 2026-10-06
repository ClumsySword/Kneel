using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Play-mode checks for the L3 level (section 15.8): the real player prefab is walked along the route by
    // writing its cached input, as CLAUDE.md describes, and the hazards and the gate are exercised where a
    // stage's checks ask for it. Start play mode, call Begin("A".."E"), poll Result.
    public static class L3LevelPlay
    {
        public static string Result = "";
        public static bool Running;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static readonly StringBuilder log = new StringBuilder();
        private static readonly List<string> errors = new List<string>();
        private static int failures;
        private static Component movement;
        private static CharacterController controller;
        private static FieldInfo moveField, sprintField, staminaField;
        private static float speedScale = 3f;

        public static string Begin(string test, float timeScale = 3f)
        {
            if (!Application.isPlaying)
            {
                return "Enter play mode first.";
            }

            Stop();
            log.Clear();
            errors.Clear();
            failures = 0;
            speedScale = timeScale;
            Result = "running";
            Running = true;
            Application.runInBackground = true;

            var player = GameObject.Find("Player");
            movement = player.GetComponent("PlayerMovement");
            controller = player.GetComponent<CharacterController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            moveField = movement.GetType().GetField("moveInput", flags);
            sprintField = movement.GetType().GetField("sprintHeld", flags);
            staminaField = movement.GetType().GetField("stamina", flags);
            foreach (var component in player.GetComponents<MonoBehaviour>())
            {
                var controls = component.GetType().GetField("controls", flags);
                if (controls != null && controls.GetValue(component) != null)
                {
                    controls.FieldType.GetMethod("Disable", Type.EmptyTypes).Invoke(controls.GetValue(component), null);
                }
            }

            Application.logMessageReceived += OnLog;
            Time.timeScale = speedScale;
            stack.Push(Script(test));
            EditorApplication.update += Tick;
            return "started " + test;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            stack.Clear();
            Running = false;
            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
                if (movement != null && moveField != null)
                {
                    moveField.SetValue(movement, Vector2.zero);
                    sprintField.SetValue(movement, false);
                }
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors.Add(Time.time.ToString("F1") + " s: " + condition);
            }
        }

        private static void Tick()
        {
            if (!Application.isPlaying)
            {
                Result = "play mode ended early\n" + log;
                Stop();
                return;
            }

            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }

                    if (top.Current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }

                    return;
                }
            }
            catch (Exception e)
            {
                Result = "EXCEPTION " + e + "\n" + log;
                Stop();
                return;
            }

            Result = (failures == 0 && errors.Count == 0 ? "PASS" : "FAIL") + ": " + failures + " failed checks, " + errors.Count + " console errors\n" + log
                + (errors.Count > 0 ? "errors:\n" + string.Join("\n", errors) : "");
            Stop();
        }

        private static void Check(bool ok, string text)
        {
            if (!ok)
            {
                failures++;
            }

            log.AppendLine((ok ? "ok    " : "FAIL  ") + text);
        }

        private static void Note(string text)
        {
            log.AppendLine("      " + text);
        }

        private static Vector3 Position => controller.transform.position;

        private static void Teleport(Vector3 p)
        {
            controller.enabled = false;
            controller.transform.position = p;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            moveField.SetValue(movement, Vector2.zero);
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static bool lastWalkArrived;

        // Runs the player toward a plan point. Arrives within 0.6 m, or gives up when it stops getting closer.
        private static IEnumerator WalkTo(float x, float z, float patience = 3f)
        {
            var target = new Vector2(x, z);
            float best = float.MaxValue, bestTime = Time.time;
            lastWalkArrived = false;
            while (true)
            {
                var here = new Vector2(Position.x, Position.z);
                float distance = (target - here).magnitude;
                if (distance < 0.6f)
                {
                    lastWalkArrived = true;
                    break;
                }

                if (distance < best - 0.3f)
                {
                    best = distance;
                    bestTime = Time.time;
                }
                else if (Time.time - bestTime > patience)
                {
                    break;
                }

                if (Position.y < -8f)
                {
                    break;
                }

                // The camera's yaw is 0, so input x and y are world x and z.
                moveField.SetValue(movement, (target - here).normalized);
                sprintField.SetValue(movement, true);
                if (staminaField != null)
                {
                    staminaField.SetValue(movement, 100f);
                }

                yield return null;
            }

            moveField.SetValue(movement, Vector2.zero);
            sprintField.SetValue(movement, false);
        }

        private static IEnumerator Route(string name, params float[] points)
        {
            float started = Time.time;
            Vector3 from = Position;
            float walked = 0f;
            for (int i = 0; i < points.Length; i += 2)
            {
                Vector3 before = Position;
                yield return WalkTo(points[i], points[i + 1]);
                walked += (Position - before).magnitude;
                if (!lastWalkArrived)
                {
                    Check(false, name + ": stopped at (" + Position.x.ToString("F1") + ", " + Position.y.ToString("F1") + ", " + Position.z.ToString("F1") + ") on the way to (" + points[i] + ", " + points[i + 1] + ")");
                    yield break;
                }
            }

            Check(true, name + ": walked " + walked.ToString("F0") + " m in " + (Time.time - started).ToString("F0") + " s, now at (" + Position.x.ToString("F1") + ", " + Position.y.ToString("F1") + ", " + Position.z.ToString("F1") + ")");
        }

        private static T Named<T>(string path) where T : Component
        {
            var t = L3Level.Root.transform.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        // ---------------------------------------------------------------- The scripts

        private static IEnumerator Script(string test)
        {
            yield return Wait(0.5f);
            // A few frames, not one: the first frame after a start or a reload is often a long one.
            float worstFrame = 0f, meanFrame = 0f;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                meanFrame += Time.unscaledDeltaTime / 30f;
            }

            Check(meanFrame < 0.1f, "editor frame time: mean " + (meanFrame * 1000f).ToString("F1") + " ms, worst " + (worstFrame * 1000f).ToString("F0") + " ms over 30 frames");
            foreach (char stage in test)
            {
                switch (stage)
                {
                    case 'A': yield return StageA(); break;
                    case 'B': yield return StageB(); break;
                    case 'C': yield return StageC(); break;
                    case 'D': yield return StageD(); break;
                    case 'E': yield return StageE(); break;
                    case 'J': yield return Jumps(); break;
                }
            }
        }

        private static IEnumerator StageA()
        {
            Note("Stage A");
            Teleport(new Vector3(64f, 0.4f, 2f));
            yield return Wait(0.3f);
            var trigger = Named<RowIgniteTrigger>("Hazards/E1a/IgniteTrigger_Descent");
            var row = Named<CropRow>("Hazards/E1a/CropRow_Descent");
            yield return Route("spawn to the crest", 64f, 20f, 64f, 36f);
            Check(Mathf.Abs(Position.y - 3f) < 0.15f, "on the crest at Y = " + Position.y.ToString("F2"));
            Check(trigger.Fired == false && row.IsBurning == false, "the demonstration row is dry before the trigger");
            yield return Route("crest to E1a", 64f, 47f, 64f, 60f, 64f, 78f);
            Check(trigger.Fired == true && (row.IsBurning || row.IsBurnedOut), "the demonstration row lit when the player crossed y 44-46");
            yield return Route("E1a to the middle of E1", 64f, 90f, 64f, 100f, 66f, 112f);
            // The secret: through the thin hedge into the orchard and back.
            yield return Route("E1 through the orchard gap and back", 52f, 117f, 52f, 124f, 52f, 134f, 52f, 124f, 52f, 117f, 66f, 112f);
            yield return Route("E1 to E2", 77f, 112.5f, 95f, 123.5f, 104f, 124f, 106f, 138f, 116f, 139f, 116f, 144f);
            yield return Route("E2 to the knoll", 128f, 163f, 139.5f, 183f, 140f, 187f, 140f, 205f, 140f, 208f);
            Check(Mathf.Abs(Position.y - 3f) < 0.15f, "at Shrine A on the knoll, Y = " + Position.y.ToString("F2"));
        }

        private static IEnumerator StageB()
        {
            Note("Stage B");
            Teleport(new Vector3(136f, 3.1f, 206f));
            yield return Wait(0.3f);
            yield return Route("off the knoll's west edge", 133f, 206f, 126f, 206f);
            Check(Mathf.Abs(Position.y) < 0.15f && Position.x < 130.5f, "landed on the hollow at (" + Position.x.ToString("F1") + ", " + Position.y.ToString("F2") + ", " + Position.z.ToString("F1") + ")");
            // Back toward the knoll: the bank must stop the player.
            yield return WalkTo(134f, 206f, 2f);
            Check(lastWalkArrived == false && Position.y < 0.5f && Position.x < 130.2f, "cannot climb back: stopped at x = " + Position.x.ToString("F2") + ", Y = " + Position.y.ToString("F2"));
            yield return WalkTo(134f, 201f, 2f);
            Check(lastWalkArrived == false && Position.y < 0.5f, "cannot climb the bank block either: stopped at (" + Position.x.ToString("F1") + ", " + Position.z.ToString("F1") + ")");

            yield return Route("hollow into E3 and up the middle lane", 124f, 200f, 117f, 200f, 106f, 203f, 104f, 210f, 104f, 216f, 104.6f, 228.5f);
            yield return Route("over the stone bridge", 105.5f, 232f, 106f, 235f, 106f, 239f);

            // The gate from the south: down the north ramp and along the footway.
            Teleport(new Vector3(140f, 3.1f, 216f));
            yield return Wait(0.3f);
            yield return Route("north ramp to the footway", 140f, 222f, 140f, 231f, 140f, 237f);
            var gate = Named<SluiceGate>("Markers/SG/SluiceGate");
            yield return WalkTo(140f, 242f, 2f);
            Check(lastWalkArrived == false && Position.z < 240f, "the shut gate stops the player at z = " + Position.z.ToString("F2"));
            Check(gate.TryOpenFrom(Position) == false && gate.IsOpen == false, "the gate refuses from the south");

            // Fire: both live rows, one after the other.
            foreach (string side in new[] { "West", "East" })
            {
                var stake = Named<BrandStake>("Hazards/E3/BrandStake_" + side);
                var row = Named<CropRow>("Hazards/E3/CropRow_" + side);
                var other = Named<CropRow>("Hazards/E3/CropRow_" + (side == "West" ? "East" : "West"));
                bool otherWasBurning = other.IsBurning || other.IsBurnedOut;
                float knocked = Time.time;
                stake.Knock();
                var lit = new float[row.Cells.Count];
                for (int i = 0; i < lit.Length; i++)
                {
                    lit[i] = -1f;
                }

                bool otherTouched = false;
                while (Time.time - knocked < 19f)
                {
                    for (int i = 0; i < lit.Length; i++)
                    {
                        if (lit[i] < 0f && row.Cells[i].State == FireCellState.Burning)
                        {
                            lit[i] = Time.time - knocked;
                        }
                    }

                    if (!otherWasBurning && other.IsBurning)
                    {
                        otherTouched = true;
                    }

                    yield return null;
                }

                bool ordered = lit[0] > 0f;
                for (int i = 1; i < lit.Length; i++)
                {
                    ordered &= lit[i] > lit[i - 1] && row.Cells[i].transform.position.z > row.Cells[i - 1].transform.position.z;
                }

                Check(ordered, side + " row burns south to north: first cell lit at " + lit[0].ToString("F1") + " s, last at " + lit[lit.Length - 1].ToString("F1") + " s");
                Check(!otherTouched, side + " row's fire did not reach the other row");
            }
        }

        private static IEnumerator StageC()
        {
            Note("Stage C");
            var health = GameObject.Find("Player").GetComponent<Health>();
            Teleport(new Vector3(124f, 0.1f, 244f));
            yield return Wait(0.3f);
            float before = HealthOf(health);
            yield return WalkTo(124f, 233f, 2f);
            yield return Wait(1f);
            Check(Position.y < -2f, "walking south off the dyke lane drops into the ditch: Y = " + Position.y.ToString("F2"));
            Check(HealthOf(health) < before, "the kill volume hit the player (health " + before.ToString("F0") + " to " + HealthOf(health).ToString("F0") + ")");

            Teleport(new Vector3(110f, 0.1f, 244f));
            yield return Wait(0.3f);
            yield return Route("dyke lane through the Wallow to the farm gate", 140f, 244f, 158f, 245f, 174f, 246f, 174f, 257f);
            yield return Route("yard to the house door and inside", 178f, 266f, 186f, 272f, 193f, 272f, 196f, 272f);
            yield return Route("back out and to the north gate", 186f, 272f, 174f, 276f, 174f, 283f);
        }

        private static float HealthOf(Component health)
        {
            if (health == null)
            {
                return -1f;
            }

            foreach (string name in new[] { "Current", "current", "CurrentHealth", "currentHealth", "Value" })
            {
                var p = health.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    return Convert.ToSingle(p.GetValue(health));
                }

                var f = health.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    return Convert.ToSingle(f.GetValue(health));
                }
            }

            return -1f;
        }

        private static IEnumerator StageD()
        {
            Note("Stage D");
            Teleport(new Vector3(174f, 0.1f, 286f));
            yield return Wait(0.3f);
            yield return Route("lane to the forecourt and the grave", 174f, 296f, 172f, 300f, 160f, 300f, 150f, 298f);
            yield return Route("back to the forecourt and into the mill yard", 160f, 300f, 172f, 300f, 174f, 304f, 174f, 316f);
            // Clear of the millstones on the way west, and of the grain cart and the trough on the way east.
            yield return Route("round the west of the yard and past the stair's foot to the exit", 168f, 319f, 160.5f, 323f, 160.5f, 338.8f, 168.5f, 338.8f, 168.5f, 333.3f, 179f, 333.3f, 179f, 338.8f, 186f, 339f, 186f, 345f);
            Teleport(new Vector3(174f, 0.1f, 318f));
            yield return Wait(0.3f);
            yield return Route("up the mill stair to the door", 177.5f, 322f, 177.5f, 333.3f, 174f, 333.6f, 174f, 337f, 174f, 341f);
            Check(Mathf.Abs(Position.y - 3.02f) < 0.15f, "on the landing at the mill door, Y = " + Position.y.ToString("F2"));
            yield return WalkTo(170f, 341f, 2f);
            Check(lastWalkArrived == false && Position.x > 172f, "the landing's west cheek stops the player at x = " + Position.x.ToString("F2"));
            yield return WalkTo(174f, 345f, 2f);
            Check(lastWalkArrived == false && Position.z < 342.2f, "the mill itself stops the player at z = " + Position.z.ToString("F2"));
            yield return Route("back down the stair", 174f, 337f, 174f, 333.3f);
            Check(Mathf.Abs(Position.y) < 0.15f, "back in the yard, Y = " + Position.y.ToString("F2"));
            Teleport(new Vector3(174f, 0.1f, 318f));
            yield return Wait(0.3f);
            yield return Route("round the east of the yard to the exit", 183f, 318.5f, 187.5f, 326f, 187.5f, 338f, 186f, 345f);
            yield return Route("exit lane and causeway", 186f, 362f, 186f, 398f);
        }

        // Runs at a low outer boundary and jumps just before it, the way a player would try to get out. The
        // player must end up on the floor it started on.
        private static IEnumerator JumpAt(string what, float x, float y, float z, float dx, float dz, float wallDistance, float jumpBefore)
        {
            Teleport(new Vector3(x, y + 0.05f, z));
            yield return Wait(0.4f);
            var jump = movement.GetType().GetMethod("SafeJump", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Vector3 start = Position;
            float until = Time.time + 3f, peak = Position.y;
            bool jumped = false;
            while (Time.time < until)
            {
                moveField.SetValue(movement, new Vector2(dx, dz));
                sprintField.SetValue(movement, true);
                staminaField.SetValue(movement, 100f);
                float gone = new Vector2(Position.x - start.x, Position.z - start.z).magnitude;
                if (!jumped && gone > wallDistance - jumpBefore)
                {
                    jump.Invoke(movement, null);
                    jumped = true;
                }

                peak = Mathf.Max(peak, Position.y);
                yield return null;
            }

            moveField.SetValue(movement, Vector2.zero);
            yield return Wait(0.6f);
            float travelled = new Vector2(Position.x - start.x, Position.z - start.z).magnitude;
            // A run may have wandered up to a metre outward, so the player may end that far past the floor's
            // edge; it must be standing on the ground, not on the wall.
            bool contained = travelled < wallDistance + 1f && Mathf.Abs(Position.y - y) < 0.2f;
            Check(jumped && contained, what + ": jumped " + jumpBefore.ToString("F1") + " m before it, peak " + (peak - y).ToString("F2") + " m, ended " + travelled.ToString("F2") + " m from the start (wall at " + wallDistance.ToString("F1") + " m), Y " + Position.y.ToString("F2"));
        }

        private static IEnumerator Jumps()
        {
            Note("Jumps at low walls");
            foreach (float before in new[] { 1.4f, 1.0f, 0.6f })
            {
                // North of the cart that lies on this verge.
                yield return JumpAt("lane fence, west (K06)", 64f, 0f, 68f, -1f, 0f, 4f, before);
                yield return JumpAt("lane fence, east (K06)", 64f, 0f, 64f, 1f, 0f, 4f, before);
                yield return JumpAt("dyke lane's north fence (K06)", 112f, 0f, 245f, 0f, 1f, 3f, before);
                yield return JumpAt("mill stair landing, west cheek", 174f, 3.02f, 341f, -1f, 0f, 1.8f, before);
                yield return JumpAt("mill stair landing, east cheek", 174f, 3.02f, 341f, 1f, 0f, 1.8f, before);
                yield return JumpAt("field's south hedge (K02)", 54f, 0f, 100f, 0f, -1f, 4f, before);
                yield return JumpAt("knoll's east kerb (K03 on the bank)", 143f, 3f, 209f, 1f, 0f, 5f, before);
                yield return JumpAt("yard's south wall (K03)", 164f, 0f, 262f, 0f, -1f, 4f, before);
            }
        }

        private static IEnumerator StageE()
        {
            Note("Stage E");
            // Carving only exists in play mode: the shut gate must cut the footway out of the NavMesh.
            var gate = Named<SluiceGate>("Markers/SG/SluiceGate");
            yield return Wait(0.5f);
            foreach (var agent in L3Level.Agents)
            {
                float shut = L3LevelChecks.PathLength(agent.name, new Vector3(140f, 0f, 236f), new Vector3(140f, 0f, 243f), out string why);
                Check(shut < 0f || shut > 30f, agent.name + ": no path across the footway while the gate is shut (" + (shut < 0f ? why : shut.ToString("F0") + " m, the long way round") + ")");
            }

            foreach (var agent in L3Level.Agents)
            {
                float back = L3LevelChecks.PathLength(agent.name, new Vector3(140f, 3f, 209.5f), new Vector3(126f, 0f, 204f), out string why);
                Check(back < 0f, agent.name + ": no path from the knoll (F14) to the landing hollow (F16) while the gate is shut (" + (back < 0f ? why : back.ToString("F0") + " m") + ")");
            }

            gate.SetOpen(true);
            yield return Wait(1f);
            foreach (var agent in L3Level.Agents)
            {
                float open = L3LevelChecks.PathLength(agent.name, new Vector3(140f, 0f, 236f), new Vector3(140f, 0f, 243f), out string why);
                Check(open > 0f && open < 9f, agent.name + ": path across the footway with the gate open, " + open.ToString("F1") + " m " + why);
            }

            float toYard = L3LevelChecks.PathLength("Thrall", new Vector3(140f, 3f, 209.5f), new Vector3(174f, 0f, 258f), out string detail);
            Check(toYard >= 65f && toYard <= 80f, "gate open: Shrine A to E4's south gate is " + toYard.ToString("F1") + " m (65-80) " + detail);
            gate.SetOpen(false);
            yield return Wait(0.5f);
        }
    }
}
