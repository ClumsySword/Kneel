using UnityEngine;

namespace Kneel.Hazards
{
    // The bottom of the irrigation ditch: anything that falls in is told so once. A fall kills.
    [RequireComponent(typeof(BoxCollider))]
    public class DitchKill : MonoBehaviour
    {
        [SerializeField]
        private HazardTuning contact;

        private readonly HazardSensor sensor = new HazardSensor();
        private BoxCollider area;

        public int Falls { get; private set; }

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
        }

        private void FixedUpdate()
        {
            sensor.Poll(area, contact.actorMask);
            foreach (var actor in sensor.Entered)
            {
                Falls++;
                HazardContact.Report(actor, HazardKind.Ditch, contact, gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(0.1f, 0.2f, 0.7f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
