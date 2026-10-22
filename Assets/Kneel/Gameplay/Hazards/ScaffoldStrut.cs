using UnityEngine;

namespace Kneel.Hazards
{
    // The strut of a scaffold or loft. Takes one hit from anyone (on the Enemy layer, where the sword looks) and
    // brings its scaffold down. Each strut stands at least 1 R outside its own collapse zone.
    public class ScaffoldStrut : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private ScaffoldCollapse scaffold;

        public ScaffoldCollapse Scaffold
        {
            get => scaffold;
            set => scaffold = value;
        }

        public void TakeHit(DamageInfo info)
        {
            if (info.isEnvironmental == false && scaffold != null)
            {
                scaffold.Strike();
            }
        }
    }
}
