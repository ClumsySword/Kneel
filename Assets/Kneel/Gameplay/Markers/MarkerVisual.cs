using UnityEngine;

namespace Kneel.Markers
{
    // The magenta shape that shows a marker in the editor. Never rendered in play mode.
    public class MarkerVisual : MonoBehaviour
    {
        private void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }
        }
    }
}
