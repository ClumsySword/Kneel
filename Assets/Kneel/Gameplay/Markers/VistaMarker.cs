using UnityEngine;

namespace Kneel.Markers
{
    // A place where the camera should open up for a long view while the player stands in this trigger.
    // Data only: the values are starting points for whoever connects it to a camera.
    public class VistaMarker : MonoBehaviour
    {
        public string id;

        // Degrees below the horizon.
        public float pitch;

        // Vertical field of view; 0 keeps the gameplay camera's.
        public float fieldOfView;

        // Metres further back than the gameplay camera.
        public float pullBack;

        // Metres sideways; negative is toward -X.
        public float sidewaysShift;

        // For a pan (the sluice gate): where to look, and for how long. Zero duration means a held vista.
        public Vector3 panTarget;
        public float panDuration;

        [TextArea]
        public string note;

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
