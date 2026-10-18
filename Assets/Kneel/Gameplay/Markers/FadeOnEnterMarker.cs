using UnityEngine;

namespace Kneel.Markers
{
    // Marks geometry that should fade out while the player is inside the building it belongs to
    // (the farmhouse's roof and camera-side wall). Data only.
    public class FadeOnEnterMarker : MonoBehaviour
    {
        public string buildingId;

        [TextArea]
        public string note;
    }
}
