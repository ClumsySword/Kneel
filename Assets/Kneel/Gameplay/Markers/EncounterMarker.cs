using UnityEngine;

namespace Kneel.Markers
{
    // An encounter's camera rules (design note L4, section 7): the arena framing it asks for and the lock that
    // freezes vistas and camera priorities while it is active, released a few seconds after the last kill.
    // Data only: the camera is final and nothing reads this yet.
    public class EncounterMarker : MonoBehaviour
    {
        public string encounterId;

        // "Arena" (24 x 15 R), "ArenaOffsetBreach" (shifted 3 R toward the breach), "Default".
        public string framing = "Arena";

        public float releaseDelay = 2f;

        [TextArea]
        public string note;
    }
}
