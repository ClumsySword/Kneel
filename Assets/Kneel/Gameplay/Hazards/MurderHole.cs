using System;
using UnityEngine;

namespace Kneel.Hazards
{
    // A murder hole over a yard or a gate passage: boiling oil poured through a slot in the vault or a
    // machicolation. Its mark (old pitch) is always on the floor. Each pour: a tell (steam from the slot, a
    // ladle's clank, hissing drips), the pour, then the floor stays scalding. Anyone inside during the pour or
    // the scald is told once per pour. Rings run on their own clock (offset into a shared loop) or are fired by
    // something else (the gate passage's portcullis).
    public class MurderHole : MonoBehaviour, ILevelResettable
    {
        public enum State
        {
            Idle,
            Tell,
            Pour,
            Scald,
        }

        public enum Schedule
        {
            Sequential,
            External,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        [SerializeField]
        private Schedule schedule = Schedule.Sequential;

        // For a sequential ring: its place in the loop (0, 1, 2...) and how many rings share the loop.
        [SerializeField]
        private int order;

        [SerializeField]
        private int ringsInLoop = 3;

        [SerializeField]
        private GameObject tellVisual;

        [SerializeField]
        private GameObject pourVisual;

        [SerializeField]
        private GameObject scaldVisual;

        private readonly HazardSensor sensor = new HazardSensor();
        private readonly System.Collections.Generic.HashSet<Component> scaldedThisPour = new System.Collections.Generic.HashSet<Component>();
        private float clock;
        private float stateTime;

        public State Current { get; private set; }

        // False while nobody feeds the trough (the pour-tender is dead): the ring stays idle until the level resets.
        public bool Supplied { get; private set; } = true;

        public int Pours { get; private set; }

        public event Action<MurderHole> TellStarted;

        public event Action<MurderHole> Poured;

        private float Loop => Mathf.Max(1, ringsInLoop) * tuning.pourSpacing;

        private void Awake()
        {
            ResetState();
        }

        private void Update()
        {
            if (tuning == null)
            {
                return;
            }

            if (schedule == Schedule.Sequential && Supplied)
            {
                float before = clock;
                clock = (clock + Time.deltaTime) % Loop;
                float start = order * tuning.pourSpacing;
                // The tell starts so that the pour lands on this ring's slot in the loop.
                float tellAt = Mathf.Repeat(start - tuning.pourTell, Loop);
                bool crossed = before <= clock ? before < tellAt && clock >= tellAt : before < tellAt || clock >= tellAt;
                if (crossed && Current == State.Idle)
                {
                    Fire();
                }
            }

            if (Current == State.Idle)
            {
                return;
            }

            stateTime += Time.deltaTime;
            switch (Current)
            {
                case State.Tell when stateTime >= tuning.pourTell:
                    Enter(State.Pour);
                    break;
                case State.Pour when stateTime >= tuning.pourDuration:
                    Enter(State.Scald);
                    break;
                case State.Scald when stateTime >= tuning.scaldDuration:
                    Enter(State.Idle);
                    break;
            }

            if (Current == State.Pour || Current == State.Scald)
            {
                sensor.PollSphere(transform.position + Vector3.up, tuning.pourRadius, contact.actorMask);
                foreach (var actor in sensor.Inside)
                {
                    if (actor != null && scaldedThisPour.Add(actor))
                    {
                        HazardContact.Report(actor, HazardKind.Pour, contact, gameObject);
                    }
                }
            }
        }

        // Starts one pour from its tell. Ignored while one is already under way or nobody feeds the trough.
        public void Fire()
        {
            if (Current != State.Idle || Supplied == false)
            {
                return;
            }

            Enter(State.Tell);
        }

        public void SetSupplied(bool supplied)
        {
            Supplied = supplied;
            if (supplied == false && Current == State.Tell)
            {
                Enter(State.Idle);
            }
        }

        public void ResetState()
        {
            Supplied = true;
            clock = 0f;
            Enter(State.Idle);
        }

        private void Enter(State next)
        {
            Current = next;
            stateTime = 0f;
            if (tellVisual != null)
            {
                tellVisual.SetActive(next == State.Tell);
            }

            if (pourVisual != null)
            {
                pourVisual.SetActive(next == State.Pour);
            }

            if (scaldVisual != null)
            {
                scaldVisual.SetActive(next == State.Pour || next == State.Scald);
            }

            if (next == State.Tell)
            {
                TellStarted?.Invoke(this);
            }
            else if (next == State.Pour)
            {
                Pours++;
                scaldedThisPour.Clear();
                sensor.Clear();
                Poured?.Invoke(this);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.4f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, tuning != null ? tuning.pourRadius : 3f);
        }
    }
}
