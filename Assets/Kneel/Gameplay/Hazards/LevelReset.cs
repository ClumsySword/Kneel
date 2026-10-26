using UnityEngine;

namespace Kneel.Hazards
{
    // On death or rest, every enemy, encounter and hazard in the loaded scenes goes back to its start
    // (decision 3). One-time things (opened gates, the winch, played demos, collected items) keep their state:
    // they either are not ILevelResettable or reset to their finished state. Nothing calls this yet: the
    // project has no death, respawn or rest (hook in the asset report).
    public class LevelReset : MonoBehaviour
    {
        public int LastCount { get; private set; }

        [ContextMenu("Reset Level")]
        public void ResetAll()
        {
            LastCount = 0;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is ILevelResettable resettable)
                {
                    resettable.ResetState();
                    LastCount++;
                }
            }
        }
    }
}
