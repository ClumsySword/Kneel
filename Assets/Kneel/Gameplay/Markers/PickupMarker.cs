using UnityEngine;

namespace Kneel.Markers
{
    // Marks something the player can pick up. Data only; the pickup system reads it.
    public class PickupMarker : MonoBehaviour
    {
        public string id;
        public PickupKind kind;
    }
}
