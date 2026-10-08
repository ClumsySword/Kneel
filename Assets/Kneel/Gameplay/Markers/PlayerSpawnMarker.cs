using UnityEngine;

namespace Kneel.Markers
{
    // Where the player enters the level and respawns before reaching a shrine. Faces the way the player faces.
    public class PlayerSpawnMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 1f);
            Vector3 p = transform.position + Vector3.up * 0.1f;
            Vector3 tip = p + transform.forward * 2f;
            Gizmos.DrawWireSphere(p, 0.3f);
            Gizmos.DrawLine(p, tip);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.6f + transform.right * 0.35f);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.6f - transform.right * 0.35f);
        }
    }
}
