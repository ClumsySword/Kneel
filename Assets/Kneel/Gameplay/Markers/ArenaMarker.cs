using UnityEngine;

namespace Kneel.Markers
{
    // A fight space whose whole floor the camera should hold in frame. Data only.
    public class ArenaMarker : MonoBehaviour
    {
        public string arenaId;

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
