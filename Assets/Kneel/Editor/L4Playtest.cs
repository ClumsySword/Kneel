using System.Collections.Generic;
using System.Linq;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // A scripted play-mode run of the hazard test lane in L4_AssetReview. Start it once the scene is playing
    // (Kneel/L4/Check/Run Hazard Playtest). It drives the hazards through their public methods (and walks the
    // player into the scaffold's demo trigger), times everything in game time against StrongholdTuning and writes
    // what it measured to Result. Nothing in the scene is saved.
    public static class L4Playtest
    {
        public static bool Running { get; private set; }

        public static string Result { get; private set; } = "not run";

        private static readonly List<string> Failures = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly List<(float time, System.Action step)> Steps = new List<(float, System.Action)>();
        private static readonly Dictionary<string, List<float>> Events = new Dictionary<string, List<float>>();

        private static int nextStep;
        private static float start;
        private static StrongholdTuning tuning;
        private static Transform lane;
        private static MurderHole[] rings;
        private static MurderHole gateHole;
        private static OilSlick slickA, slickB;
        private static HangingLight cresset, brazier;
        private static PortcullisCycle gate, laneGate;
        private static Winch winch;
        private static ScaffoldCollapse scaffold;
        private static KneelerAmbush twinA, twinB, footKneeler;
        private static OneTimeGate postern, grille;
        private static PourTender tender;
        private static HazardTestDummy dPour, dOil, dHound, dSlot, dUnder;
        private static Transform player;
        private static Vector3 playerHome;
        private static int poursBeforeTenderDied;

        private static float Now => Time.time - start;

        [MenuItem("Kneel/L4/Check/Run Hazard Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Result = "Enter play mode in L4_AssetReview first.";
                Debug.LogWarning(Result);
                return;
            }

            Application.runInBackground = true;
            Failures.Clear();
            Notes.Clear();
            Errors.Clear();
            Steps.Clear();
            Events.Clear();
            nextStep = 0;
            KeyRing.Clear();

            tuning = L4Assets.Tuning;
            lane = GameObject.Find(L4Review.LaneName).transform;
            var holes = lane.GetComponentsInChildren<MurderHole>();
            rings = holes.Where(h => h.transform.localPosition.z > 16f && h.transform.localPosition.x < 0f).OrderBy(h => h.transform.localPosition.x).ToArray();
            gateHole = holes.First(h => h.transform.localPosition.x > 0f);
            var slicks = lane.GetComponentsInChildren<OilSlick>().OrderBy(s => s.transform.localPosition.x).ToArray();
            slickA = slicks[0];
            slickB = slicks[1];
            var lights = lane.GetComponentsInChildren<HangingLight>().OrderBy(l => l.transform.localPosition.x).ToArray();
            cresset = lights[0];
            brazier = lights[1];
            var gates = lane.GetComponentsInChildren<PortcullisCycle>().OrderBy(g => g.transform.localPosition.x).ToArray();
            gate = gates[0];
            laneGate = gates[1];
            winch = lane.GetComponentInChildren<Winch>();
            scaffold = lane.GetComponentInChildren<ScaffoldCollapse>();
            var kneelers = lane.GetComponentsInChildren<KneelerAmbush>().OrderBy(k => k.transform.localPosition.x).ToArray();
            twinA = kneelers[0];
            twinB = kneelers[1];
            footKneeler = kneelers[2];
            var oneTime = lane.GetComponentsInChildren<OneTimeGate>().OrderBy(g => g.transform.localPosition.x).ToArray();
            postern = oneTime[0];
            grille = oneTime[1];
            tender = lane.GetComponentInChildren<PourTender>();
            dPour = Dummy("Dummy_Pour");
            dOil = Dummy("Dummy_Oil");
            dHound = Dummy("Dummy_OilHound");
            dSlot = Dummy("Dummy_Slot");
            dUnder = Dummy("Dummy_Under");
            player = Object.FindAnyObjectByType<PlayerMovement>().transform;
            playerHome = player.position;

            for (int i = 0; i < rings.Length; i++)
            {
                int index = i;
                rings[i].TellStarted += h => Mark("ring" + index + ".tell");
                rings[i].Poured += h => Mark("ring" + index + ".pour");
            }

            gateHole.Poured += h => Mark("gateHole.pour");
            slickA.Lit += s => Mark("slickA.lit");
            slickB.Lit += s => Mark("slickB.lit");
            cresset.Landed += l => Mark("cresset.landed");
            gate.StateChanged += (g, s) => Mark("gate." + s);
            laneGate.StateChanged += (g, s) => Mark("laneGate." + s);
            scaffold.CreakStarted += s => Mark("scaffold.creak");
            scaffold.Collapsed += s => Mark("scaffold.collapsed");
            twinA.StruckEarly += k => Mark("twinA.struck");
            twinA.Woken += k => Mark("twinA.woken");
            twinB.Woken += k => Mark("twinB.woken");
            footKneeler.Woken += k => Mark("foot.woken");

            Application.logMessageReceived += OnLog;
            // Play mode ran for a while before the test began: put every hazard back to its start so the clocks
            // (rings, gate cycle) count from now.
            Object.FindAnyObjectByType<LevelReset>().ResetAll();
            foreach (var d in new[] { dPour, dOil, dHound, dSlot, dUnder })
            {
                d.ClearLog();
            }

            Time.timeScale = 2f;
            start = Time.time;
            Script();
            Running = true;
            Result = "running";
            EditorApplication.update += Update;
        }

        private static HazardTestDummy Dummy(string name)
        {
            var d = lane.Find(name).GetComponent<HazardTestDummy>();
            d.ClearLog();
            return d;
        }

        private static void Mark(string what)
        {
            if (!Events.TryGetValue(what, out var list))
            {
                Events[what] = list = new List<float>();
            }

            list.Add(Now);
        }

        private static float First(string what) => Events.TryGetValue(what, out var list) && list.Count > 0 ? list[0] : float.NaN;

        private static int CountOf(string what) => Events.TryGetValue(what, out var list) ? list.Count : 0;

        private static void Expect(bool ok, string what)
        {
            (ok ? Notes : Failures).Add((ok ? "ok    " : "FAIL  ") + what);
        }

        private static void Near(float measured, float expected, float tolerance, string what)
        {
            Expect(float.IsNaN(measured) == false && Mathf.Abs(measured - expected) <= tolerance, what + ": expected " + expected.ToString("F2") + ", measured " + measured.ToString("F2"));
        }

        private static void Teleport(Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            player.position = position;
            Physics.SyncTransforms();
            if (cc != null)
            {
                cc.enabled = true;
            }
        }

        private static void Hit(Component target)
        {
            ((IDamageable)target).TakeHit(new DamageInfo { amount = 10f, point = target.transform.position, direction = Vector3.forward, source = player.gameObject });
        }

        private static void Script()
        {
            float swing = tuning.lightSwing + tuning.lightFall;
            Steps.Add((0.5f, () =>
            {
                // The edge guard's clamp, as pure geometry: a knockback from 1 m inside to 1 m past the lip ends 0.25 m short.
                var guard = lane.GetComponentInChildren<EdgeGuard>();
                Vector3 from = guard.transform.TransformPoint(new Vector3(0f, 0f, -1f));
                Vector3 to = guard.transform.TransformPoint(new Vector3(0f, 0f, 1f));
                bool clamped = guard.ClampEnemyMove(from, ref to);
                Expect(clamped && Mathf.Abs(guard.transform.InverseTransformPoint(to).z + 0.25f) < 0.01f, "EdgeGuard stops an enemy 0.25 m short of the lip (" + guard.transform.InverseTransformPoint(to).z.ToString("F2") + ")");
            }));
            Steps.Add((1f, () =>
            {
                Mark("cresset.struck");
                cresset.Strike();
                Hit(brazier);
                Mark("brazier.struck");
            }));
            Steps.Add((2f, () =>
            {
                Mark("winch.turned");
                winch.Turn();
            }));
            Steps.Add((3f, () =>
            {
                // Walk into the scaffold's demo trigger.
                var trigger = scaffold.GetComponentInChildren<DemoTrigger>();
                Mark("player.inDemoTrigger");
                Teleport(trigger.transform.position + Vector3.up * 0.1f);
            }));
            Steps.Add((4.5f, () => Teleport(playerHome)));
            Steps.Add((6f, () =>
            {
                Mark("twinA.hit");
                Hit(twinA);
            }));
            Steps.Add((8f, () =>
            {
                Mark("player.nearFootKneeler");
                Teleport(footKneeler.transform.position + new Vector3(0f, 0.1f, -4f));
            }));
            Steps.Add((9.5f, () => Teleport(playerHome)));
            Steps.Add((10f, () =>
            {
                Vector3 south = postern.transform.TransformPoint(new Vector3(0f, 0f, -1.2f));
                Vector3 north = postern.transform.TransformPoint(new Vector3(0f, 0f, 1.2f));
                Expect(postern.TryOpenFrom(south) == false, "Postern stays barred from -Z");
                Expect(postern.TryOpenFrom(north), "Postern opens from its bar side (+Z)");
                Vector3 near = grille.transform.TransformPoint(new Vector3(0f, 0f, -1f));
                Expect(grille.TryOpenFrom(near) == false, "Grille stays locked without the key");
                KeyRing.Give("TribunalKey");
                Expect(grille.TryOpenFrom(near), "Grille opens with the tribunal key");
            }));
            Steps.Add((14f, () =>
            {
                poursBeforeTenderDied = rings.Sum(r => r.Pours);
                Hit(tender);
                Mark("tender.killed");
            }));
            Steps.Add((24f, () =>
            {
                Expect(rings.Sum(r => r.Pours) == poursBeforeTenderDied, "No pours while the pour-tender is dead (" + (rings.Sum(r => r.Pours) - poursBeforeTenderDied) + " after)");
                Expect(slickA.Current == OilSlick.State.Spent, "Slick A spent after its burn (" + slickA.Current + ")");
                Object.FindAnyObjectByType<LevelReset>().ResetAll();
                Mark("reset");
                Expect(slickA.Current == OilSlick.State.Unlit && cresset.Current == HangingLight.State.Hanging, "Reset: slick unlit, cresset hanging again");
                Expect(twinA.Current == KneelerAmbush.State.Kneeling && footKneeler.Current == KneelerAmbush.State.Kneeling, "Reset: kneelers kneel again");
                Expect(tender.Dead == false && rings.All(r => r.Supplied), "Reset: the pour-tender lives and the rings are fed");
                Expect(scaffold.Current == ScaffoldCollapse.State.Collapsed, "Reset: the demo scaffold stays down (" + scaffold.Current + ")");
                Expect(laneGate.IsRaisedForGood && laneGate.Height > 5.9f, "Reset: the winch gate stays up");
                Expect(postern.IsOpen && grille.IsOpen, "Reset: one-time gates stay open");
            }));
            Steps.Add((38f, Finish));
        }

        private static void Update()
        {
            if (Application.isPlaying == false)
            {
                Stop("play mode ended early");
                return;
            }

            while (nextStep < Steps.Count && Now >= Steps[nextStep].time)
            {
                try
                {
                    Steps[nextStep].step();
                }
                catch (System.Exception e)
                {
                    Failures.Add("FAIL  step at " + Steps[nextStep].time + " threw " + e.Message);
                }

                nextStep++;
            }
        }

        private static void Finish()
        {
            float swing = tuning.lightSwing + tuning.lightFall;

            // Murder holes: three rings 4 s apart on a 12 s loop, each told 1.5 s before it pours.
            Near(First("ring1.pour"), 4f, 0.2f, "Ring 1 first pour");
            Near(First("ring2.pour"), 8f, 0.2f, "Ring 2 first pour");
            Near(First("ring0.pour"), 12f, 0.2f, "Ring 0 first pour");
            Near(First("ring1.pour") - First("ring1.tell"), tuning.pourTell, 0.1f, "Ring tell before its pour");
            Expect(dPour.Count(HazardKind.Pour) == CountOf("ring0.pour"), "One Pour contact per pour on ring 0");

            // Lights and oil.
            Near(First("cresset.landed") - First("cresset.struck"), swing, 0.12f, "Cresset swing + fall");
            Near(First("slickA.lit") - First("cresset.struck"), swing, 0.12f, "Slick A lit when the cresset lands");
            Near(First("slickB.lit") - First("brazier.struck"), swing, 0.12f, "Slick B lit when the brazier topples (sword hit)");
            Expect(dOil.Count(HazardKind.OilBurnEnter) == 1, "Dummy in slick A burnt on contact once (" + dOil.Count(HazardKind.OilBurnEnter) + ")");
            int ticks = dOil.Count(HazardKind.OilBurnTick);
            Expect(ticks >= 4 && ticks <= 6, "Burning ticks in 6 s (" + ticks + ")");
            Expect(dHound.Count(HazardKind.OilBurnEnter) == 1, "Hound dummy in slick B burnt (" + dHound.Consequence(HazardKind.OilBurnEnter) + ")");

            // The gate passage.
            float rattle = First("gate.Rattle"), drop = First("gate.Drop"), down = First("gate.Down"), rise = First("gate.Rise"), up = Events.TryGetValue("gate.Up", out var ups) ? ups.FirstOrDefault(t => t > rise) : float.NaN;
            Near(rattle, tuning.gateUp, 0.15f, "Portcullis up phase");
            Near(drop - rattle, tuning.gateRattle, 0.1f, "Portcullis rattle");
            Near(down - drop, tuning.gateDrop, 0.1f, "Portcullis drop");
            Near(rise - down, tuning.gateDown, 0.1f, "Portcullis down phase");
            Near(up - rise, tuning.gateRise, 0.1f, "Portcullis rise");
            Expect(dSlot.Count(HazardKind.GateSlot) >= 1, "Dummy (brute) in the slot killed by the drop (" + dSlot.Count(HazardKind.GateSlot) + ")");
            Near(First("gateHole.pour") - down, tuning.pourTell, 0.12f, "Passage pour lands in the down phase");

            // Winch and lane gate.
            Near(First("laneGate.Up") - First("winch.turned"), tuning.winchRaise, 0.12f, "Lane gate raised by the winch");

            // Scaffold demo.
            Near(First("scaffold.creak") - First("player.inDemoTrigger"), 0f, 0.15f, "Scaffold demo starts when the player walks in");
            Near(First("scaffold.collapsed") - First("scaffold.creak"), tuning.scaffoldCreak + tuning.scaffoldFall, 0.12f, "Scaffold creak + fall");
            Expect(dUnder.Count(HazardKind.Crush) == 1, "Footman under the scaffold crushed once (" + dUnder.Count(HazardKind.Crush) + ")");
            Expect(CountOf("scaffold.creak") == 1, "Demo played once");

            // Kneelers.
            Expect(CountOf("twinA.struck") == 1, "Twin struck before rising");
            Near(First("twinA.woken") - First("twinA.hit"), tuning.kneelerRise, 0.12f, "Struck Twin rises after 1 s");
            Near(First("twinB.woken") - First("twinA.hit"), tuning.kneelerRise, 0.12f, "Its partner rises with it");
            Expect(First("foot.woken") - First("player.nearFootKneeler") < tuning.kneelerRise + 0.3f, "Footman kneeler wakes when the player comes within 3 R (" + (First("foot.woken") - First("player.nearFootKneeler")).ToString("F2") + " s)");

            // After the reset the rings pour again.
            Expect(Events.TryGetValue("ring1.pour", out var r1) && r1.Any(t => t > First("reset")), "Rings pour again after the reset");

            Expect(Errors.Count == 0, "No console errors (" + Errors.Count + ")");
            Stop(null);
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(message);
            }
        }

        private static void Stop(string reason)
        {
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            Running = false;
            var lines = new List<string>();
            lines.Add((Failures.Count == 0 && reason == null ? "PASS" : "FAIL") + ": " + Notes.Count + " ok, " + Failures.Count + " failed" + (reason != null ? " (" + reason + ")" : ""));
            lines.AddRange(Failures);
            lines.AddRange(Notes);
            lines.AddRange(Errors.Select(e => "error: " + e));
            Result = string.Join("\n", lines);
            Debug.Log("[L4 playtest]\n" + Result);
        }
    }
}
