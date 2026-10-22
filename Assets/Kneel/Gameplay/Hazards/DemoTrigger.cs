using UnityEngine;

namespace Kneel.Hazards
{
    // A volume that plays a one-time demonstration when the player first walks into it: the barbican
    // portcullis slamming down (0:30), the rampart-stair scaffold collapsing (5:30). Demos never replay
    // (decision 3), so this does not reset.
    [RequireComponent(typeof(BoxCollider))]
    public class DemoTrigger : MonoBehaviour
    {
        [SerializeField]
        private LayerMask playerMask;

        [SerializeField]
        private PortcullisCycle portcullis;

        [SerializeField]
        private ScaffoldCollapse scaffold;

        private BoxCollider area;
        private readonly Collider[] buffer = new Collider[4];

        public bool Played { get; private set; }

        public void SetTargets(PortcullisCycle gate, ScaffoldCollapse collapse)
        {
            portcullis = gate;
            scaffold = collapse;
        }

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (Played)
            {
                return;
            }

            var t = area.transform;
            int n = Physics.OverlapBoxNonAlloc(t.TransformPoint(area.center), Vector3.Scale(area.size, t.lossyScale) * 0.5f, buffer, t.rotation, playerMask, QueryTriggerInteraction.Ignore);
            if (n > 0)
            {
                Play();
            }
        }

        public void Play()
        {
            if (Played)
            {
                return;
            }

            Played = true;
            if (portcullis != null)
            {
                portcullis.Trigger();
            }

            if (scaffold != null)
            {
                scaffold.Strike();
            }
        }
    }
}
