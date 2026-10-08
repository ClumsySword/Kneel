using UnityEngine;

namespace Kneel.Hazards
{
    [CreateAssetMenu(menuName = "Kneel/Hazards/Surface Tuning")]
    public class SurfaceTuning : ScriptableObject
    {
        public float rollDistanceMultiplier = 0.5f;
        public float moveSpeedMultiplier = 0.85f;
    }
}
