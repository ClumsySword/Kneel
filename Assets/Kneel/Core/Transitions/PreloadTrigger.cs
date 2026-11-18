using UnityEngine;

namespace Kneel.Core
{
    // Starts loading the next level in the background when Kael walks into it (past the last fight, timed from the
    // measured load so it is done well before the exit). Once only; the load waits at 90% until the exit.
    [RequireComponent(typeof(Collider))]
    public class PreloadTrigger : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null)
            {
                LevelTransition.RequestPreload(gameObject.scene.name);
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
