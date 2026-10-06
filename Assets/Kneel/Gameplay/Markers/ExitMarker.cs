using UnityEngine;

namespace Kneel.Markers
{
    // The trigger that ends the level. Data only; level loading reads it.
    public class ExitMarker : MonoBehaviour
    {
        public string destination;

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
