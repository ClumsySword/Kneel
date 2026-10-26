using System;
using TMPro;
using UnityEngine;

namespace Kneel.Hazards
{
    // A winch the player turns (Interact, E) to raise a gate for good: the west-lane gate beside the chapel's
    // west door (decision 16). Like every script here it reads its own PlayerControls.
    public class Winch : MonoBehaviour
    {
        public const string Prompt = "Turn the winch  [E]";

        [SerializeField]
        private PortcullisCycle gate;

        // The drum that turns while the gate rises.
        [SerializeField]
        private Transform drum;

        [SerializeField]
        private TMP_Text prompt;

        [SerializeField]
        private float reach = 2.5f;

        private PlayerControls controls;
        private Transform player;
        private float turning;

        public bool Turned { get; private set; }

        public PortcullisCycle Gate
        {
            get => gate;
            set => gate = value;
        }

        public event Action<Winch> TurnedOnce;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += ctx => TryTurn();
            if (prompt != null)
            {
                prompt.text = Prompt;
                prompt.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            var movement = FindAnyObjectByType<PlayerMovement>();
            player = movement != null ? movement.transform : null;
        }

        private void OnEnable()
        {
            controls.Character.Interact.Enable();
        }

        private void OnDisable()
        {
            controls.Character.Interact.Disable();
        }

        private void Update()
        {
            if (prompt != null)
            {
                prompt.gameObject.SetActive(Turned == false && player != null && InReach(player.position));
            }

            if (turning > 0f && drum != null)
            {
                turning -= Time.deltaTime;
                drum.Rotate(Vector3.right, 240f * Time.deltaTime, Space.Self);
            }
        }

        public bool InReach(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            return d.magnitude <= reach;
        }

        public void TryTurn()
        {
            if (player != null && InReach(player.position))
            {
                Turn();
            }
        }

        // Public so scripted tests and the inspector button can turn it without the player.
        public void Turn()
        {
            if (Turned)
            {
                return;
            }

            Turned = true;
            turning = 1.5f;
            if (gate != null)
            {
                gate.Raise();
            }

            TurnedOnce?.Invoke(this);
        }

        // For the save.
        public void SetTurned(bool turned)
        {
            Turned = turned;
            if (gate != null)
            {
                gate.SetFinished(turned);
            }
        }
    }
}
