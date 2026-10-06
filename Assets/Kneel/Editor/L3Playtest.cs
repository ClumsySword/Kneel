using System.Collections.Generic;
using System.Text;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // A scripted play-mode run of the hazard test lane in L3_AssetReview. Start it once the scene is playing
    // (Kneel/L3/Check/Run Hazard Playtest); it drives the hazards through their public methods, times them in
    // game time and writes what it measured to Result. Nothing in the scene is saved.
    public static class L3Playtest
    {
        private const float Tick = 0.045f;

        public static bool Running { get; private set; }

        public static string Result { get; private set; } = "not run";

        private static readonly List<string> Failures = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly List<System.Action> Steps = new List<System.Action>();
        private static readonly List<float> StepTimes = new List<float>();

        private static float start;
        private static int nextStep;

        private static CropRow west, east;
        private static BrandStake westStake, eastStake;
        private static SluiceGate gate;
        private static GibbetAmbush gibbet;
        private static HazardTestDummy houndInRow, footmanInRow, playerInLane, playerInMud, footmanInDitch;
        private static Health playerHealth;
        private static Transform player;
        private static Vector3 playerHome;

        private static float[] smoulderAt, igniteAt, ashAt;
        private static float knockAt;
        private static int gateOpenedEvents, wokenEvents, struckEvents;
        private static bool eastTouched;

        private static float Now => Time.time - start;

        [MenuItem("Kneel/L3/Check/Run Hazard Playtest")]
        public static void Begin()
        {
            if (!Application.isPlaying)
            {
                Result = "Enter play mode in L3_AssetReview first.";
                Debug.LogWarning(Result);
                return;
            }

            Application.runInBackground = true;
            Failures.Clear();
            Notes.Clear();
            Errors.Clear();
            Steps.Clear();
            StepTimes.Clear();
            gateOpenedEvents = wokenEvents = struckEvents = 0;
            eastTouched = false;
            nextStep = 0;

            west = Find<CropRow>("CropRow_West");
            east = Find<CropRow>("CropRow_East");
            westStake = Find<BrandStake>("BrandStake_West");
            eastStake = Find<BrandStake>("BrandStake_East");
            gate = GameObject.Find("TestLane").GetComponentInChildren<SluiceGate>();
            gibbet = GameObject.Find("TestLane").GetComponentInChildren<GibbetAmbush>();
            houndInRow = Find<HazardTestDummy>("Dummy_Hound_InRow");
            footmanInRow = Find<HazardTestDummy>("Dummy_Footman_InRow");
            playerInLane = Find<HazardTestDummy>("Dummy_Player_InLane");
            playerInMud = Find<HazardTestDummy>("Dummy_Player_InMud");
            footmanInDitch = Find<HazardTestDummy>("Dummy_Footman_InDitch");
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            player = movement.transform;
            playerHome = player.position;
            playerHealth = player.GetComponent<Health>();

            int n = west.Cells.Count;
            smoulderAt = new float[n];
            igniteAt = new float[n];
            ashAt = new float[n];
            for (int i = 0; i < n; i++)
            {
                int index = i;
                west.Cells[i].OnSmoulder += c => smoulderAt[index] = Now;
                west.Cells[i].OnIgnite += c => igniteAt[index] = Now;
                west.Cells[i].OnBurnOut += c => ashAt[index] = Now;
            }

            gate.Opened += g => gateOpenedEvents++;
            gibbet.Woken += g => wokenEvents++;
            gibbet.StruckEarly += g => struckEvents++;

            Application.logMessageReceived += OnLog;
            start = Time.time;
            Script();
            Running = true;
            Result = "running";
            EditorApplication.update += Update;
        }

        private static T Find<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                throw new System.InvalidOperationException("Missing " + name + " in the scene");
            }

            return go.GetComponent<T>();
        }

        private static void At(float time, System.Action step)
        {
            StepTimes.Add(time);
            Steps.Add(step);
        }

        private static void Check(bool ok, string what)
        {
            if (!ok)
            {
                Failures.Add("[" + Now.ToString("F2") + "] " + what);
            }
        }

        private static bool Near(float value, float expected, float tolerance = Tick)
        {
            return Mathf.Abs(value - expected) <= tolerance;
        }

        private static void Script()
        {
            FireTuning fire = L3Look.Fire;

            // ---- Things that are true before anything is touched.
            At(0.3f, () =>
            {
                Check(playerInMud.OnSurface && Near(playerInMud.RollDistanceMultiplier, 0.5f, 0.001f) && Near(playerInMud.MoveSpeedMultiplier, 0.85f, 0.001f), "mud: dummy standing in it should have surface 0.5 / 0.85");
                Check(footmanInDitch.Count(HazardKind.Ditch) == 1, "ditch: dummy in the ditch should have been told once, got " + footmanInDitch.Count(HazardKind.Ditch));
                Check(gibbet.Current == GibbetAmbush.State.Dormant, "gibbet: should be dormant with the player far away");
                Check(west.Cells[0].State == FireCellState.Dry && !west.IsBurning, "fire: west row should start dry");

                // The baked NavMesh carries the two hazard areas.
                Check(AreaAt(playerInMud.transform.position) == "Mud", "navmesh: the mud is baked as " + AreaAt(playerInMud.transform.position) + ", expected Mud");
                Check(AreaAt(west.Cells[8].transform.position) == "CropRow", "navmesh: a dry crop row is baked as " + AreaAt(west.Cells[8].transform.position) + ", expected CropRow");
                Check(AreaAt(playerInLane.transform.position) == "Walkable", "navmesh: the lane between the rows is baked as " + AreaAt(playerInLane.transform.position) + ", expected Walkable");

                // The gate: barred from the south, opens from the north, and the NavMesh follows it.
                Vector3 south = gate.transform.position - gate.transform.forward * 1.5f, north = gate.transform.position + gate.transform.forward * 1.5f;
                Check(!PathAcross(south, north), "gate: a NavMesh path crosses the shut gate");
                Check(!gate.TryOpenFrom(south) && !gate.IsOpen, "gate: opened from the barred (-Z) side");
                Check(gate.TryOpenFrom(north) && gate.IsOpen, "gate: did not open from the bar (+Z) side");
                Check(!gate.TryOpenFrom(north) && gateOpenedEvents == 1, "gate: Opened should be raised exactly once, got " + gateOpenedEvents);

                // An environmental hit (fire) must not knock a stake.
                eastStake.TakeHit(new DamageInfo { amount = 10f, isEnvironmental = true });
                Check(!eastStake.IsKnocked, "stake: knocked by environmental damage");

                knockAt = Now;
                westStake.Knock();
            });

            At(2f, () =>
            {
                Vector3 south = gate.transform.position - gate.transform.forward * 1.5f, north = gate.transform.position + gate.transform.forward * 1.5f;
                Check(PathAcross(south, north), "gate: no NavMesh path through the open gate");
                var blocker = gate.transform.Find("Blocker").GetComponent<Collider>();
                Check(!blocker.enabled, "gate: blocker still enabled after opening");

                playerInMud.transform.position += Vector3.right * 9f;
                gibbet.TakeHit(new DamageInfo { amount = 10f });
                Check(struckEvents == 1 && gibbet.Current == GibbetAmbush.State.Waking, "gibbet: an early strike should raise StruckEarly and start the wake");
            });

            At(2.6f, () => Check(!playerInMud.OnSurface, "mud: surface not cleared after the dummy left"));

            At(4f, () =>
            {
                Check(wokenEvents == 1 && gibbet.Current == GibbetAmbush.State.Woken && gibbet.WasStruckEarly, "gibbet: should be woken 1.5 s after the strike (events " + wokenEvents + ", state " + gibbet.Current + ")");
                gibbet.ResetState();
                Check(gibbet.Current == GibbetAmbush.State.Dormant && !gibbet.WasStruckEarly, "gibbet: reset should return it to dormant");
                gibbet.Trigger();
            });

            At(6f, () => Check(wokenEvents == 2 && !gibbet.WasStruckEarly, "gibbet: proximity trigger should wake it 1.5 s later without StruckEarly"));

            // ---- The player has no IHazardTarget yet: fire reaches it through IDamageable.
            At(12f, () =>
            {
                Notes.Add("player health before standing in burning cell 6: " + playerHealth.Current);
                Teleport(west.Cells[6].transform.position);
            });

            At(15.3f, () =>
            {
                float lost = playerHealth.Max - playerHealth.Current;
                Notes.Add("player health after 3.3 s in a burning cell: " + playerHealth.Current + " (lost " + lost + " through IDamageable, environmental)");
                Check(lost >= 60f - 0.01f, "player: expected the IDamageable fallback to cost at least 3 hits in 3.3 s, lost " + lost);
                Teleport(playerHome);
            });

            // ---- West row has burned out (36.5 s after the knock); east must be untouched until now.
            At(38.5f, () =>
            {
                float fall = 0.35f;
                for (int i = 0; i < smoulderAt.Length; i++)
                {
                    float expected = knockAt + fall + i * fire.cellDelay;
                    Check(Near(smoulderAt[i], expected, 0.08f), "fire: cell " + i + " smouldered at " + smoulderAt[i].ToString("F2") + ", expected " + expected.ToString("F2"));
                    Check(Near(igniteAt[i] - smoulderAt[i], fire.emberTell), "fire: cell " + i + " smouldered for " + (igniteAt[i] - smoulderAt[i]).ToString("F2") + " s, expected " + fire.emberTell);
                    Check(Near(ashAt[i] - igniteAt[i], fire.burnTime), "fire: cell " + i + " burned for " + (ashAt[i] - igniteAt[i]).ToString("F2") + " s, expected " + fire.burnTime);
                    Check(west.Cells[i].State == FireCellState.Ash, "fire: cell " + i + " should be ash, is " + west.Cells[i].State);
                }

                int last = smoulderAt.Length - 1;
                Notes.Add("fire: knocked at " + knockAt.ToString("F2") + "; first cell smouldered +" + (smoulderAt[0] - knockAt).ToString("F2") + " s; last cell lit +" + (igniteAt[last] - knockAt).ToString("F2")
                    + " s (front took " + (smoulderAt[last] - smoulderAt[0]).ToString("F2") + " s to walk the row); first cell ash +" + (ashAt[0] - knockAt).ToString("F2") + " s; whole row ash +" + (ashAt[last] - knockAt).ToString("F2") + " s");
                Check(west.IsBurnedOut, "fire: west row should report burned out");
                Check(!eastTouched, "fire: the east row changed state while only the west row was lit");
                Check(footmanInRow.Log.Count == 0, "fire: dummy in the east row was touched (" + footmanInRow.Log.Count + " contacts)");
                Check(playerInLane.Log.Count == 0, "fire: dummy in the lane between the rows was touched");

                Check(houndInRow.Count(HazardKind.Smoulder) == 1 && houndInRow.Count(HazardKind.FireEnter) == 1, "fire: dummy in cell 4 should get one Smoulder and one FireEnter, got "
                    + houndInRow.Count(HazardKind.Smoulder) + " and " + houndInRow.Count(HazardKind.FireEnter));
                Check(houndInRow.Count(HazardKind.FireTick) >= 19 && houndInRow.Count(HazardKind.FireTick) <= 20, "fire: dummy in cell 4 should get a tick per second for 20 s, got " + houndInRow.Count(HazardKind.FireTick));
                Notes.Add("fire: dummy in cell 4 got Smoulder x" + houndInRow.Count(HazardKind.Smoulder) + ", FireEnter x" + houndInRow.Count(HazardKind.FireEnter) + ", FireTick x" + houndInRow.Count(HazardKind.FireTick));

                // Reset, then light the east row the way the sword would: a hit through IDamageable.
                west.ResetToDry();
                westStake.ResetState();
                Check(west.Cells[0].State == FireCellState.Dry && west.Cells[11].State == FireCellState.Dry && !westStake.IsKnocked, "fire: reset should leave the west row dry and its stake upright");
                eastStake.TakeHit(new DamageInfo { amount = 10f });
                Check(eastStake.IsKnocked, "stake: a hit through IDamageable should knock it");
            });

            At(42f, () =>
            {
                Check(east.Cells[0].State == FireCellState.Burning && east.Cells[1].State == FireCellState.Burning, "fire: east row should be alight after its stake was hit");
                Check(footmanInRow.Count(HazardKind.FireEnter) == 1, "fire: dummy in east cell 1 should have been caught when it lit");
                var carving = east.Cells[0].GetComponent<NavMeshObstacle>();
                Check(carving.enabled, "fire: a burning cell should carve the NavMesh");
                Check(!west.Cells[0].GetComponent<NavMeshObstacle>().enabled, "fire: a dry cell should not carve the NavMesh");
                Finish();
            });
        }

        private static bool PathAcross(Vector3 from, Vector3 to)
        {
            // The points are 3 m apart either side of the gate. With the gate shut the only way round is over
            // the stone bridge (about 34 m), so a short path means the way through the gate is open.
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length < 6f;
        }

        // The name of the NavMesh area under a point.
        private static string AreaAt(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 1f, NavMesh.AllAreas))
            {
                return "no NavMesh";
            }

            foreach (var name in NavMesh.GetAreaNames())
            {
                if (hit.mask == 1 << NavMesh.GetAreaFromName(name))
                {
                    return name;
                }
            }

            return "mask " + hit.mask;
        }

        private static void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position + Vector3.up * 0.05f;
            controller.enabled = true;
        }

        private static void Update()
        {
            if (!Application.isPlaying)
            {
                Running = false;
                EditorApplication.update -= Update;
                Application.logMessageReceived -= OnLog;
                if (Result == "running")
                {
                    Result = "play mode ended before the playtest finished";
                }

                return;
            }

            foreach (var cell in east.Cells)
            {
                eastTouched |= cell.State != FireCellState.Dry && !eastStake.IsKnocked;
            }

            while (nextStep < Steps.Count && Now >= StepTimes[nextStep])
            {
                try
                {
                    Steps[nextStep]();
                }
                catch (System.Exception e)
                {
                    Failures.Add("step at " + StepTimes[nextStep] + " threw " + e.Message);
                }

                nextStep++;
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add("[" + Now.ToString("F2") + " s] " + condition.Trim());
            }
        }

        private static void Finish()
        {
            Running = false;
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            var sb = new StringBuilder();
            sb.AppendLine("L3 hazard playtest: " + (Failures.Count == 0 && Errors.Count == 0 ? "PASS" : "FAIL") + " (" + Failures.Count + " failed checks, " + Errors.Count + " console errors, "
                + Now.ToString("F1") + " s of game time)");
            foreach (var f in Failures)
            {
                sb.AppendLine("  ! " + f);
            }

            foreach (var e in Errors)
            {
                sb.AppendLine("  error: " + e);
            }

            foreach (var n in Notes)
            {
                sb.AppendLine("  - " + n);
            }

            Result = sb.ToString();
            Debug.Log(Result);
        }
    }
}
