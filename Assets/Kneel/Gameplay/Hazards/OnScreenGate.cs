using UnityEngine;

namespace Kneel.Hazards
{
    // On every ranged attacker (decision 9): its first wind-up may not start until its renderers have been inside
    // the camera frame, inset 10% on every side, for 1 s without a break. The AI asks CanStartWindUp before each
    // wind-up and calls WindUpStarted when it starts one; a wind-up started while not allowed is logged, so the
    // playtest check "no hit from off-screen" has a record to read.
    public class OnScreenGate : MonoBehaviour, ILevelResettable
    {
        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private Renderer[] renderers;

        private float seen;
        private bool everAllowed;

        public bool CanStartWindUp => everAllowed;

        public float SecondsInFrame => seen;

        public int ViolationCount { get; private set; }

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void Update()
        {
            if (IsInFrame(Camera.main))
            {
                seen += Time.deltaTime;
                if (seen >= (tuning != null ? tuning.onScreenTime : 1f))
                {
                    everAllowed = true;
                }
            }
            else
            {
                seen = 0f;
            }
        }

        public void WindUpStarted()
        {
            if (CanStartWindUp == false)
            {
                ViolationCount++;
                Debug.LogWarning("[OnScreenGate] " + name + " started a wind-up before it had been on screen for " + (tuning != null ? tuning.onScreenTime : 1f) + " s (seen " + seen.ToString("F2") + " s).", this);
            }
        }

        public void ResetState()
        {
            seen = 0f;
            everAllowed = false;
        }

        public bool IsInFrame(Camera cam)
        {
            if (cam == null)
            {
                return false;
            }

            float inset = tuning != null ? tuning.onScreenInset : 0.1f;
            foreach (var r in renderers)
            {
                if (r == null || r.enabled == false)
                {
                    continue;
                }

                var b = r.bounds;
                var v = cam.WorldToViewportPoint(b.center);
                if (v.z > 0f && v.x >= inset && v.x <= 1f - inset && v.y >= inset && v.y <= 1f - inset)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
