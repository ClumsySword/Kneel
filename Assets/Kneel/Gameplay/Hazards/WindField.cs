using UnityEngine;

namespace Kneel.Hazards
{
    // One level-wide wind. Fire spreads along it and smoke leans with it.
    [CreateAssetMenu(menuName = "Kneel/Hazards/Wind Field")]
    public class WindField : ScriptableObject
    {
        [SerializeField]
        private Vector3 direction = Vector3.forward;

        public Vector3 Direction => direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }
}
