using UnityEngine;

namespace Kneel.Markers
{
    // While the player is inside, roof renderers listed here switch to shadows only (guardroom, towers).
    // Data only: the project's OcclusionFade already fades what hides the player.
    public class RoofHiderMarker : MonoBehaviour
    {
        public Renderer[] roofs;
    }
}
