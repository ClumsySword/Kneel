using UnityEngine;

namespace Kneel.Hazards
{
    // Lights a crop row when the player walks into this box. Used once in L3, for the demonstration strip beside
    // the descent: the player sees a row catch and run north before fire can hurt them. Re-arms on reset.
    [RequireComponent(typeof(BoxCollider))]
    public class RowIgniteTrigger : MonoBehaviour, ILevelResettable
    {
        [SerializeField]
        private CropRow row;

        [SerializeField]
        private LayerMask playerMask;

        private readonly Collider[] buffer = new Collider[2];
        private BoxCollider box;

        public bool Fired { get; private set; }

        public CropRow Row => row;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
        }

        public void ResetState()
        {
            Fired = false;
        }

        private void FixedUpdate()
        {
            if (Fired == true || row == null)
            {
                return;
            }

            Vector3 centre = transform.TransformPoint(box.center);
            Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            if (Physics.OverlapBoxNonAlloc(centre, half, buffer, transform.rotation, playerMask, QueryTriggerInteraction.Ignore) > 0)
            {
                Fired = true;
                row.Ignite();
            }
        }
    }
}
