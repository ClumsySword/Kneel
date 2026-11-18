using UnityEngine;

namespace Kneel.Core
{
    // The start of a level, as seen from the seam: where Kael is placed when he arrives from `fromScene`, the walk
    // that brings him to the level's spawn, the intro camera, and the door he comes through (L5's outer doors).
    public class LevelArrival : MonoBehaviour
    {
        [Tooltip("The level he comes from (LevelCatalog scene name).")]
        public string fromScene;

        [Tooltip("Where he stands when the intro fades in, facing along its forward.")]
        public Transform start;

        [Tooltip("The walk in; the last point is the level's spawn, where control returns.")]
        public Transform[] walkPath = new Transform[0];

        public CameraKey[] cameraKeys = new CameraKey[0];

        [Tooltip("A door he walks through on arrival: open when the intro starts, shut behind him at closeAt seconds (negative: left open).")]
        public LevelDoor door;
        public float closeAt = -1f;

        private void OnDrawGizmos()
        {
            TransitionCamera.DrawPath(walkPath, start != null ? start.position : transform.position, new Color(0.3f, 0.8f, 1f));
            TransitionCamera.DrawGizmos(cameraKeys, new Color(0.4f, 0.9f, 1f));
            if (start != null)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f);
                Gizmos.DrawWireSphere(start.position + Vector3.up * 0.1f, 0.35f);
                Gizmos.DrawLine(start.position + Vector3.up * 0.1f, start.position + Vector3.up * 0.1f + start.forward * 1.5f);
            }
        }
    }
}
