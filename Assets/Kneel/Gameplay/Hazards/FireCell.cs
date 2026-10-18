using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.Hazards
{
    public enum FireCellState
    {
        Dry,
        Smouldering,
        Burning,
        Ash,
    }

    // One cell of a crop row. Dry crop smoulders (the tell), burns, then is ash for good (until reset).
    // The cell runs its own clock once it starts to smoulder; its row decides when that is.
    // While smouldering or burning it carves the NavMesh, so nothing paths through fire.
    public class FireCell : MonoBehaviour, ILevelResettable
    {
        [SerializeField]
        private FireTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        [SerializeField]
        private BoxCollider area;

        // Saved disabled so it never carves in the editor.
        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private GameObject dryVisual;

        [SerializeField]
        private GameObject smoulderVisual;

        [SerializeField]
        private GameObject burningVisual;

        [SerializeField]
        private GameObject ashVisual;

        // A cell that burned before the player arrived (the worked example beside the fire field).
        [SerializeField]
        private bool startInAsh;

        private readonly HazardSensor sensor = new HazardSensor();
        private readonly Dictionary<Component, float> nextTick = new Dictionary<Component, float>();
        private float stateTime;

        public FireCellState State { get; private set; }

        public bool StartInAsh => startInAsh;

        public event Action<FireCell> OnSmoulder;
        public event Action<FireCell> OnIgnite;
        public event Action<FireCell> OnBurnOut;

        private void Awake()
        {
            Enter(startInAsh ? FireCellState.Ash : FireCellState.Dry, false);
        }

        // Dry crop catches: the ember tell starts.
        public void Smoulder()
        {
            if (State == FireCellState.Dry)
            {
                Enter(FireCellState.Smouldering, true);
            }
        }

        public void ResetState()
        {
            Enter(startInAsh ? FireCellState.Ash : FireCellState.Dry, false);
        }

        // Edit time: which state the cell starts in, with the matching visual showing.
        public void SetStartInAsh(bool ash)
        {
            startInAsh = ash;
            ShowVisual(ash ? FireCellState.Ash : FireCellState.Dry);
        }

        private void FixedUpdate()
        {
            if (State != FireCellState.Smouldering && State != FireCellState.Burning)
            {
                return;
            }

            stateTime += Time.fixedDeltaTime;
            sensor.Poll(area, contact.actorMask);

            if (State == FireCellState.Smouldering)
            {
                foreach (var actor in sensor.Entered)
                {
                    HazardContact.Report(actor, HazardKind.Smoulder, contact, gameObject);
                }

                if (stateTime >= tuning.emberTell)
                {
                    // Whoever is still standing here is caught when it lights.
                    Enter(FireCellState.Burning, true);
                }

                return;
            }

            foreach (var actor in sensor.Entered)
            {
                HazardContact.Report(actor, HazardKind.FireEnter, contact, gameObject);
                nextTick[actor] = stateTime + tuning.damageTick;
            }

            foreach (var actor in sensor.Exited)
            {
                nextTick.Remove(actor);
            }

            foreach (var actor in sensor.Inside)
            {
                if (nextTick.TryGetValue(actor, out float due) && stateTime >= due)
                {
                    HazardContact.Report(actor, HazardKind.FireTick, contact, gameObject);
                    nextTick[actor] = due + tuning.damageTick;
                }
            }

            if (stateTime >= tuning.burnTime)
            {
                Enter(FireCellState.Ash, true);
            }
        }

        private void Enter(FireCellState state, bool raise)
        {
            State = state;
            stateTime = 0f;
            sensor.Clear();
            nextTick.Clear();
            ShowVisual(state);

            if (obstacle != null)
            {
                obstacle.enabled = state == FireCellState.Smouldering || state == FireCellState.Burning;
            }

            if (raise == false)
            {
                return;
            }

            switch (state)
            {
                case FireCellState.Smouldering: OnSmoulder?.Invoke(this); break;
                case FireCellState.Burning: OnIgnite?.Invoke(this); break;
                case FireCellState.Ash: OnBurnOut?.Invoke(this); break;
            }
        }

        private void ShowVisual(FireCellState state)
        {
            SetActive(dryVisual, state == FireCellState.Dry || state == FireCellState.Smouldering);
            SetActive(smoulderVisual, state == FireCellState.Smouldering);
            SetActive(burningVisual, state == FireCellState.Burning);
            SetActive(ashVisual, state == FireCellState.Ash);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private void OnDrawGizmos()
        {
            if (area == null)
            {
                return;
            }

            FireCellState shown = Application.isPlaying ? State : (startInAsh ? FireCellState.Ash : FireCellState.Dry);
            switch (shown)
            {
                case FireCellState.Dry: Gizmos.color = new Color(0.85f, 0.75f, 0.4f, 0.35f); break;
                case FireCellState.Smouldering: Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.6f); break;
                case FireCellState.Burning: Gizmos.color = new Color(1f, 0.3f, 0.05f, 0.6f); break;
                default: Gizmos.color = new Color(0.05f, 0.05f, 0.05f, 0.6f); break;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(area.center, area.size);
            Gizmos.DrawCube(new Vector3(area.center.x, 0.05f, area.center.z), new Vector3(area.size.x, 0.1f, area.size.z));
        }
    }
}
