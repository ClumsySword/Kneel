using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel
{
    // A heavy gate that only opens from one side, because its lever (GateLever) stands on that side. Opening
    // swings the leaf on its hinge, drops the blocking collider and stops carving the NavMesh (the baked NavMesh
    // runs straight through; the obstacle closes it at runtime while the gate is shut).
    public class ShortcutGate : MonoBehaviour
    {
        [SerializeField]
        private Collider blocker;

        // The swinging leaf; its pivot is the hinge.
        [SerializeField]
        private Transform leaf;

        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private float openAngle = -105f;

        [SerializeField]
        private float openDuration = 1.4f;

        public bool IsOpen { get; private set; }

        private Quaternion closedRotation;
        private bool closedKnown;

        private void Awake()
        {
            if (leaf != null)
            {
                closedRotation = leaf.localRotation;
                closedKnown = true;
            }

            // The obstacle is saved disabled so it never carves the NavMesh in the editor; a shut gate carves at runtime.
            if (obstacle != null)
            {
                obstacle.enabled = !IsOpen;
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            StartCoroutine(Swing());
        }

        public void Close()
        {
            StopAllCoroutines();
            IsOpen = false;
            if (blocker != null)
            {
                blocker.enabled = true;
            }

            if (obstacle != null)
            {
                obstacle.enabled = true;
            }

            if (leaf != null && closedKnown)
            {
                leaf.localRotation = closedRotation;
            }
        }

        private IEnumerator Swing()
        {
            // The way through is free as soon as the gate starts to move; the leaf swings away from the lane.
            if (blocker != null)
            {
                blocker.enabled = false;
            }

            if (obstacle != null)
            {
                obstacle.enabled = false;
            }

            if (leaf == null)
            {
                yield break;
            }

            Quaternion closed = leaf.localRotation;
            Quaternion open = closed * Quaternion.Euler(0f, openAngle, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
            {
                // Heavy: slow to start, a little settle at the end.
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                leaf.localRotation = Quaternion.Slerp(closed, open, eased);
                yield return null;
            }

            leaf.localRotation = open;
        }
    }
}
