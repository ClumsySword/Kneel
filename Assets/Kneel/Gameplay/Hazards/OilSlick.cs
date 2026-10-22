using System;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.Hazards
{
    public interface IIgnitable
    {
        void Ignite();
    }

    // A pool of lamp oil on the floor (design note, decision 17). Unlit it does nothing: no slow, no slip.
    // Lit, it catches across its whole area within half a second and burns for six: one hit on contact, then one
    // a second, to anyone in it (enemies too). Then it is spent until the level resets. Only a light falling
    // into it lights it (HangingLight).
    [RequireComponent(typeof(BoxCollider))]
    public class OilSlick : MonoBehaviour, IIgnitable, ILevelResettable
    {
        public enum State
        {
            Unlit,
            Catching,
            Burning,
            Spent,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private GameObject unlitVisual;

        [SerializeField]
        private GameObject burningVisual;

        [SerializeField]
        private GameObject spentVisual;

        private readonly HazardSensor sensor = new HazardSensor();
        private BoxCollider area;
        private float stateTime;
        private float nextTick;

        public State Current { get; private set; }

        public event Action<OilSlick> Lit;

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
            ResetState();
        }

        public void Ignite()
        {
            if (Current != State.Unlit)
            {
                return;
            }

            Enter(State.Catching);
            Lit?.Invoke(this);
        }

        public void ResetState()
        {
            sensor.Clear();
            Enter(State.Unlit);
        }

        private void Update()
        {
            if (tuning == null || Current == State.Unlit || Current == State.Spent)
            {
                return;
            }

            stateTime += Time.deltaTime;
            if (Current == State.Catching && stateTime >= tuning.oilCatch)
            {
                Enter(State.Burning);
            }
            else if (Current == State.Burning && stateTime >= tuning.oilBurn)
            {
                Enter(State.Spent);
            }
        }

        private void FixedUpdate()
        {
            if (Current != State.Burning)
            {
                return;
            }

            sensor.Poll(area, contact.actorMask);
            foreach (var actor in sensor.Entered)
            {
                HazardContact.Report(actor, HazardKind.OilBurnEnter, contact, gameObject);
            }

            if (stateTime >= nextTick)
            {
                nextTick += tuning.oilTick;
                foreach (var actor in sensor.Inside)
                {
                    if (actor != null && sensor.Entered.Contains(actor) == false)
                    {
                        HazardContact.Report(actor, HazardKind.OilBurnTick, contact, gameObject);
                    }
                }
            }
        }

        private void Enter(State next)
        {
            Current = next;
            stateTime = 0f;
            nextTick = tuning != null ? tuning.oilTick : 1f;
            if (next == State.Burning)
            {
                sensor.Clear();
            }

            if (unlitVisual != null)
            {
                unlitVisual.SetActive(next == State.Unlit || next == State.Catching);
            }

            if (burningVisual != null)
            {
                burningVisual.SetActive(next == State.Catching || next == State.Burning);
            }

            if (spentVisual != null)
            {
                spentVisual.SetActive(next == State.Spent);
            }

            if (obstacle != null)
            {
                obstacle.enabled = next == State.Burning;
            }
        }
    }
}
