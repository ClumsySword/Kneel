using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.Hazards
{
    // Keys the player carries. The game has no inventory yet (the tribunal key is the first key item, note's open
    // questions), so this is the smallest thing that lets a gate ask.
    public static class KeyRing
    {
        private static readonly HashSet<string> keys = new HashSet<string>();

        public static event Action<string> Given;

        public static bool Has(string id) => keys.Contains(id);

        public static void Give(string id)
        {
            if (keys.Add(id))
            {
                Given?.Invoke(id);
            }
        }

        public static void Clear() => keys.Clear();
    }

    // A gate that opens once and stays open: never reset by deaths or rests (decision 3).
    //  Barred: the NW-tower postern; opens only from the bar side (+Z), "Barred from the other side" from -Z.
    //  Key:    the rampart-stair grille; opens with the tribunal key, from either side.
    // Interact (E). The leaf swings about its hinge (or lifts, for a grille).
    public class OneTimeGate : MonoBehaviour
    {
        public enum Lock
        {
            Barred,
            Key,
        }

        public const string LiftBar = "Lift the bar  [E]";
        public const string BarredOther = "Barred from the other side";
        public const string Unlock = "Unlock  [E]";
        public const string Locked = "Locked";

        [SerializeField]
        private Lock lockKind = Lock.Barred;

        [SerializeField]
        private string keyId = "TribunalKey";

        [SerializeField]
        private Collider blocker;

        [SerializeField]
        private Transform leaf;

        [SerializeField]
        private Vector3 openEuler = new Vector3(0f, 100f, 0f);

        [SerializeField]
        private Vector3 openOffset;

        [SerializeField]
        private GameObject bar;

        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private TMP_Text prompt;

        [SerializeField]
        private float reach = 2.5f;

        [SerializeField]
        private float openDuration = 1.5f;

        private PlayerControls controls;
        private Transform player;
        private Vector3 closedPosition;
        private Quaternion closedRotation;

        public bool IsOpen { get; private set; }

        public Lock LockKind => lockKind;

        public event Action<OneTimeGate> Opened;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += ctx => TryOpen();
            if (leaf != null)
            {
                closedPosition = leaf.localPosition;
                closedRotation = leaf.localRotation;
            }

            SetOpen(false);
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
            if (prompt == null)
            {
                return;
            }

            bool show = IsOpen == false && player != null && InReach(player.position);
            prompt.gameObject.SetActive(show);
            if (show)
            {
                prompt.text = PromptFor(player.position);
            }
        }

        public string PromptFor(Vector3 worldPoint)
        {
            if (lockKind == Lock.Barred)
            {
                return IsOnBarSide(worldPoint) ? LiftBar : BarredOther;
            }

            return KeyRing.Has(keyId) ? Unlock : Locked;
        }

        public bool IsOnBarSide(Vector3 worldPoint) => transform.InverseTransformPoint(worldPoint).z > 0f;

        public bool InReach(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            return d.magnitude <= reach;
        }

        public void TryOpen()
        {
            if (player != null)
            {
                TryOpenFrom(player.position);
            }
        }

        // Public so scripted tests and the inspector can try it from a given place. True if it opened.
        public bool TryOpenFrom(Vector3 worldPoint)
        {
            if (IsOpen || InReach(worldPoint) == false)
            {
                return false;
            }

            if (lockKind == Lock.Barred && IsOnBarSide(worldPoint) == false)
            {
                return false;
            }

            if (lockKind == Lock.Key && KeyRing.Has(keyId) == false)
            {
                return false;
            }

            IsOpen = true;
            Free();
            StartCoroutine(Swing());
            Opened?.Invoke(this);
            return true;
        }

        // For the save: a known state without the swing or the event.
        public void SetOpen(bool open)
        {
            StopAllCoroutines();
            IsOpen = open;
            if (blocker != null)
            {
                blocker.enabled = open == false;
            }

            if (obstacle != null)
            {
                obstacle.enabled = open == false;
            }

            if (bar != null)
            {
                bar.SetActive(open == false);
            }

            if (leaf != null)
            {
                leaf.localPosition = open ? closedPosition + openOffset : closedPosition;
                leaf.localRotation = open ? closedRotation * Quaternion.Euler(openEuler) : closedRotation;
            }
        }

        private void Free()
        {
            if (blocker != null)
            {
                blocker.enabled = false;
            }

            if (obstacle != null)
            {
                obstacle.enabled = false;
            }

            if (bar != null)
            {
                bar.SetActive(false);
            }
        }

        private IEnumerator Swing()
        {
            if (leaf == null)
            {
                yield break;
            }

            Quaternion to = closedRotation * Quaternion.Euler(openEuler);
            for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
            {
                float e = 1f - (1f - t) * (1f - t);
                leaf.localRotation = Quaternion.Slerp(closedRotation, to, e);
                leaf.localPosition = Vector3.Lerp(closedPosition, closedPosition + openOffset, e);
                yield return null;
            }

            leaf.localRotation = to;
            leaf.localPosition = closedPosition + openOffset;
        }
    }
}
