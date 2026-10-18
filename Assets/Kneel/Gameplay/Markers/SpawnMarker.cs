using UnityEngine;

namespace Kneel.Markers
{
    // Where an enemy starts, and what it is doing when the player first sees it. Data only; the encounter
    // system reads it. The marker faces the way the enemy faces.
    public class SpawnMarker : MonoBehaviour
    {
        public EnemyType enemyType;
        public string encounterId;

        // Feeding, Asleep, Circling, Bedded, Standing, On the gibbet, At the well, Digging...
        public string initialState;

        // Metres; 0 means the encounter decides.
        public float aggroRadius;

        [TextArea]
        public string leashNote;

        private void OnDrawGizmos()
        {
            switch (enemyType)
            {
                case EnemyType.Hound: Gizmos.color = new Color(0.95f, 0.9f, 0.75f); break;
                case EnemyType.Brute: Gizmos.color = new Color(0.9f, 0.2f, 0.1f); break;
                default: Gizmos.color = new Color(0.9f, 0.6f, 0.1f); break;
            }

            Vector3 p = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawWireSphere(p, 0.4f);
            Gizmos.DrawLine(p, p + transform.forward * 1.2f);
            if (aggroRadius > 0f)
            {
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.35f);
                Gizmos.DrawWireSphere(p, aggroRadius);
            }
        }
    }
}
