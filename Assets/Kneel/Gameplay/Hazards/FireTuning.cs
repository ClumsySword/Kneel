using UnityEngine;

namespace Kneel.Hazards
{
    [CreateAssetMenu(menuName = "Kneel/Hazards/Fire Tuning")]
    public class FireTuning : ScriptableObject
    {
        // Metres per cell along the row.
        public float cellLength = 2f;

        // Seconds a cell smoulders before it lights.
        public float emberTell = 1.5f;

        // Seconds between one cell starting to smoulder and the next.
        public float cellDelay = 1.33f;

        // Seconds a cell burns before it is ash.
        public float burnTime = 20f;

        // Seconds between hits on an actor standing in a burning cell.
        public float damageTick = 1f;
    }
}
