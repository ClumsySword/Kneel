using System.Collections;
using UnityEngine;

namespace Kneel.Core
{
    // A pair of hinged leaves (or any number): the throne-hall doors at the end of L4, the same doors as L5's outer
    // doors. Each leaf turns about its own pivot by openAngle (degrees about world up). Blockers are the colliders
    // that close the doorway while shut.
    public class LevelDoor : MonoBehaviour
    {
        [System.Serializable]
        public struct Leaf
        {
            public Transform hinge;
            public float openAngle;
        }

        public Leaf[] leaves = new Leaf[0];
        public Collider[] blockers = new Collider[0];
        public float openTime = 2.2f;
        public float closeTime = 1.6f;

        private Quaternion[] shut;
        private float amount;

        public bool IsOpen => amount >= 1f;

        public float Amount => amount;

        private void Awake()
        {
            CacheShut();
        }

        private void CacheShut()
        {
            if (shut != null)
            {
                return;
            }

            shut = new Quaternion[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                shut[i] = leaves[i].hinge != null ? leaves[i].hinge.localRotation : Quaternion.identity;
            }
        }

        public void SetOpen(bool open)
        {
            Apply(open ? 1f : 0f);
        }

        public IEnumerator Open() => Swing(1f, openTime);

        public IEnumerator Close() => Swing(0f, closeTime);

        private IEnumerator Swing(float to, float duration)
        {
            float from = amount;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // Heavy: slow to start, slower to stop.
                float s = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, t / duration));
                Apply(Mathf.Lerp(from, to, s));
                yield return null;
            }

            Apply(to);
        }

        private void Apply(float value)
        {
            CacheShut();
            amount = value;
            for (int i = 0; i < leaves.Length; i++)
            {
                Transform hinge = leaves[i].hinge;
                if (hinge != null)
                {
                    Vector3 up = hinge.parent != null ? hinge.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
                    hinge.localRotation = Quaternion.AngleAxis(leaves[i].openAngle * value, up) * shut[i];
                }
            }

            foreach (Collider c in blockers)
            {
                if (c != null)
                {
                    c.enabled = value < 0.5f;
                }
            }
        }
    }
}
