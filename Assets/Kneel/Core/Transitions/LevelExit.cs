using UnityEngine;

namespace Kneel.Core
{
    // The end of a level: walking into this trigger (the ExitMarker's box) hands Kael to the game. It walks him out
    // along walkPath (after the door beat, if there is a door), while the camera runs its outro keys, then the
    // LevelTransition flow fades to the loading screen. The next level is LevelCatalog's next: the exit's own
    // ExitMarker.destination is only checked against it.
    [RequireComponent(typeof(Collider))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("Where he walks once the game takes over, in order. The last point can lie past the fade.")]
        public Transform[] walkPath = new Transform[0];

        [Tooltip("A door beat (L4's throne-hall doors): he walks to doorStand, the door opens, then he walks the path through it.")]
        public LevelDoor door;
        public Transform doorStand;

        [Tooltip("The authored outro camera, from the moment the game takes over.")]
        public CameraKey[] cameraKeys = new CameraKey[0];

        [Tooltip("Seconds from the threshold to the fade to black (clamped by the settings' outroMaxTime).")]
        public float outroTime = 3.2f;

        [Tooltip("Unseen colliders that close the road beyond the exit in play; switched off when the game takes over, so the walk-off can go on down it.")]
        public Collider[] openWhenTaken = new Collider[0];

        public void Open()
        {
            foreach (Collider c in openWhenTaken)
            {
                if (c != null)
                {
                    c.enabled = false;
                }
            }
        }

        private Collider area;
        private PlayerMovement player;

        private void Awake()
        {
            area = GetComponent<Collider>();
            area.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null)
            {
                LevelTransition.Begin(this);
            }
        }

        // The trigger is the way in; this only catches a frame the physics step missed (a very fast arrival).
        private void Update()
        {
            if (LevelTransition.Active)
            {
                return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerMovement>();
                if (player == null)
                {
                    return;
                }
            }

            if (area.bounds.Contains(player.transform.position + Vector3.up * 0.5f))
            {
                LevelTransition.Begin(this);
            }
        }

        private void OnDrawGizmos()
        {
            TransitionCamera.DrawPath(walkPath, doorStand != null ? doorStand.position : transform.position, new Color(1f, 0.6f, 0.2f));
            TransitionCamera.DrawGizmos(cameraKeys, new Color(1f, 0.85f, 0.3f));
        }
    }
}
