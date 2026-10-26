using System;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.Hazards
{
    // An iron portcullis in its slot. Every drop is told: the chain rattles and dust falls from the slot for 1.5 s.
    // The slot kills anything in it when the gate comes down (the brute included). Three modes:
    //  OneShot: the barbican demo; up, then on Trigger it rattles, drops and stays down for good.
    //  Cycle:   the gate passage; up 6 s, rattle 1.5, drop 0.3, down 4, rise 2.5, on its own clock. The murder
    //           holes in the passage fire during the down phase.
    //  Winch:   the west-lane gate; starts down, rises once when its winch is turned, stays up.
    public class PortcullisCycle : MonoBehaviour, ILevelResettable
    {
        public enum Mode
        {
            OneShot,
            Cycle,
            Winch,
        }

        public enum State
        {
            Up,
            Rattle,
            Drop,
            Down,
            Rise,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        [SerializeField]
        private Mode mode = Mode.Cycle;

        // The grid; it moves straight up and down between y 0 and raisedHeight.
        [SerializeField]
        private Transform grid;

        [SerializeField]
        private float raisedHeight = 6f;

        // The kill volume in the slot, and what blocks the way while the gate is down.
        [SerializeField]
        private BoxCollider slot;

        [SerializeField]
        private Collider blocker;

        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private GameObject rattleVisual;

        [SerializeField]
        private MurderHole[] downPhasePours;

        private readonly HazardSensor sensor = new HazardSensor();
        private float stateTime;
        private bool played;

        public State Current { get; private set; }

        public Mode GateMode => mode;

        public bool IsRaisedForGood { get; private set; }

        public float Height => grid != null ? grid.localPosition.y : 0f;

        public event Action<PortcullisCycle, State> StateChanged;

        private void Awake()
        {
            if (slot != null)
            {
                slot.isTrigger = true;
            }

            ResetState();
        }

        // OneShot: start the demo. Ignored once it has played (demos never replay, decision 3).
        public void Trigger()
        {
            if (mode == Mode.OneShot && played == false && Current == State.Up)
            {
                played = true;
                Enter(State.Rattle);
            }
        }

        // Winch: raise the gate for good.
        public void Raise()
        {
            if (mode == Mode.Winch && IsRaisedForGood == false)
            {
                IsRaisedForGood = true;
                Enter(State.Rise);
            }
        }

        public void ResetState()
        {
            switch (mode)
            {
                case Mode.OneShot:
                    Enter(played ? State.Down : State.Up);
                    SetHeight(played ? 0f : raisedHeight);
                    break;
                case Mode.Cycle:
                    Enter(State.Up);
                    SetHeight(raisedHeight);
                    break;
                case Mode.Winch:
                    Enter(IsRaisedForGood ? State.Up : State.Down);
                    SetHeight(IsRaisedForGood ? raisedHeight : 0f);
                    break;
            }
        }

        // For the save: put a one-time gate (demo, winch) in its finished state without playing it.
        public void SetFinished(bool finished)
        {
            if (mode == Mode.OneShot)
            {
                played = finished;
            }
            else if (mode == Mode.Winch)
            {
                IsRaisedForGood = finished;
            }

            ResetState();
        }

        private void Update()
        {
            if (tuning == null)
            {
                return;
            }

            stateTime += Time.deltaTime;
            switch (Current)
            {
                case State.Up when mode == Mode.Cycle && stateTime >= tuning.gateUp:
                    Enter(State.Rattle);
                    break;
                case State.Rattle when stateTime >= tuning.gateRattle:
                    Enter(State.Drop);
                    break;
                case State.Drop:
                    SetHeight(Mathf.Lerp(raisedHeight, 0f, Mathf.Clamp01(stateTime / tuning.gateDrop)));
                    PollSlot();
                    if (stateTime >= tuning.gateDrop)
                    {
                        SetHeight(0f);
                        Enter(State.Down);
                    }

                    break;
                case State.Down when mode == Mode.Cycle && stateTime >= tuning.gateDown:
                    Enter(State.Rise);
                    break;
                case State.Rise:
                    float duration = mode == Mode.Winch ? tuning.winchRaise : tuning.gateRise;
                    SetHeight(Mathf.Lerp(0f, raisedHeight, Mathf.Clamp01(stateTime / duration)));
                    if (stateTime >= duration)
                    {
                        SetHeight(raisedHeight);
                        Enter(State.Up);
                    }

                    break;
            }
        }

        // Anyone in the slot while the grid comes down is killed.
        private void PollSlot()
        {
            if (slot == null)
            {
                return;
            }

            sensor.Poll(slot, contact.actorMask);
            foreach (var actor in sensor.Entered)
            {
                HazardContact.Report(actor, HazardKind.GateSlot, contact, gameObject);
            }
        }

        private void SetHeight(float y)
        {
            if (grid != null)
            {
                var p = grid.localPosition;
                p.y = y;
                grid.localPosition = p;
            }

            // The way is shut while the grid is lower than a body can pass under.
            bool shut = y < (tuning != null ? tuning.gateSafeHeight : 2f);
            if (blocker != null)
            {
                blocker.enabled = shut;
            }

            if (obstacle != null)
            {
                obstacle.enabled = shut;
            }
        }

        private void Enter(State next)
        {
            Current = next;
            stateTime = 0f;
            if (rattleVisual != null)
            {
                rattleVisual.SetActive(next == State.Rattle);
            }

            if (next == State.Drop)
            {
                sensor.Clear();
            }

            if (next == State.Down && downPhasePours != null)
            {
                foreach (var pour in downPhasePours)
                {
                    if (pour != null)
                    {
                        pour.Fire();
                    }
                }
            }

            StateChanged?.Invoke(this, next);
        }
    }
}
