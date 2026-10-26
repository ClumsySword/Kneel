using System;
using UnityEngine;

namespace Kneel.Hazards
{
    // The lip of the rampart breach (E4). Enemy-applied knockback and the Iron Husk's charge stop at the lip, and
    // a charge that reaches it leaves the Husk teetering there (decision 18); only the Fool's lunge can go over.
    // It never touches movement from the player's input: a player can walk or roll off. The guard is a line along
    // its local x (its length), with outward +Z (over the drop).
    public class EdgeGuard : MonoBehaviour
    {
        [SerializeField]
        private float length = 12f;

        // How far short of the lip an enemy is stopped.
        [SerializeField]
        private float margin = 0.25f;

        public float Length => length;

        public event Action<EdgeGuard, GameObject> Teetered;

        // How far a point is from the lip, measured inward (negative: past it). Points beyond the guard's ends
        // are not its business (returns +infinity).
        public float InsetOf(Vector3 worldPoint)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            if (Mathf.Abs(local.x) > length * 0.5f)
            {
                return float.PositiveInfinity;
            }

            return -local.z;
        }

        // Clamps an enemy displacement so it ends 'margin' short of the lip. Returns true if it was clamped (the
        // enemy reached the lip). Call from enemy knockback and the Husk's charge, never from player input.
        public bool ClampEnemyMove(Vector3 from, ref Vector3 to)
        {
            if (float.IsInfinity(InsetOf(to)) || InsetOf(to) >= margin)
            {
                return false;
            }

            var localFrom = transform.InverseTransformPoint(from);
            var localTo = transform.InverseTransformPoint(to);
            // Stop 'margin' short of the lip, or where it already was if it started closer than that.
            localTo.z = Mathf.Min(localTo.z, Mathf.Max(localFrom.z, -margin));
            to = transform.TransformPoint(localTo);
            return true;
        }

        // The Husk's charge ended at the lip: listeners (its AI) start the 2.5 s teeter.
        public void ReportTeeter(GameObject enemy)
        {
            Teetered?.Invoke(this, enemy);
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            Gizmos.DrawLine(new Vector3(-length * 0.5f, 0.05f, 0f), new Vector3(length * 0.5f, 0.05f, 0f));
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.25f);
            Gizmos.DrawCube(new Vector3(0f, 0.05f, -margin * 0.5f), new Vector3(length, 0.02f, margin));
        }
    }
}
