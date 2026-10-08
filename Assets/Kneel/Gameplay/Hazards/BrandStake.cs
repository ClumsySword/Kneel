using System;
using System.Collections;
using UnityEngine;

namespace Kneel.Hazards
{
    // A burning brand on a post at the upwind end of a crop row. Any hit (the player's sword, a flail) knocks
    // it over into the crop, which lights the row. It takes hits through the project's IDamageable, so its hit
    // collider sits on the Enemy layer where the sword looks.
    public class BrandStake : MonoBehaviour, IDamageable, ILevelResettable
    {
        [SerializeField]
        private CropRow row;

        // The part that tips over; its pivot is at the foot of the post.
        [SerializeField]
        private Transform post;

        [SerializeField]
        private GameObject tipGlow;

        [SerializeField]
        private float fallAngle = 82f;

        [SerializeField]
        private float fallDuration = 0.35f;

        // A stake that fell before the player arrived (lies beside the row that is already ash).
        [SerializeField]
        private bool startFallen;

        private Quaternion upright;
        private bool uprightKnown;

        public bool IsKnocked { get; private set; }

        public CropRow Row
        {
            get => row;
            set => row = value;
        }

        public event Action<BrandStake> OnKnocked;

        private void Awake()
        {
            CacheUpright();
            ApplyStart();
        }

        public void TakeHit(DamageInfo info)
        {
            if (info.isEnvironmental == false)
            {
                Knock();
            }
        }

        // Public so a flail, a test or the inspector button can knock it without a hit.
        public void Knock()
        {
            if (IsKnocked == true)
            {
                return;
            }

            IsKnocked = true;
            OnKnocked?.Invoke(this);
            StartCoroutine(Fall());
        }

        public void ResetState()
        {
            StopAllCoroutines();
            ApplyStart();
        }

        // Edit time: lay the stake down (or stand it up) and remember that as its starting pose.
        public void SetStartFallen(bool fallen)
        {
            CacheUpright();
            startFallen = fallen;
            ApplyStart();
        }

        private void CacheUpright()
        {
            if (uprightKnown == false && post != null)
            {
                // The prefab is saved upright; a fallen start is applied on top of that.
                upright = startFallen ? post.localRotation * Quaternion.Inverse(Quaternion.Euler(fallAngle, 0f, 0f)) : post.localRotation;
                uprightKnown = true;
            }
        }

        private void ApplyStart()
        {
            IsKnocked = startFallen;
            if (post != null)
            {
                post.localRotation = startFallen ? upright * Quaternion.Euler(fallAngle, 0f, 0f) : upright;
            }

            if (tipGlow != null)
            {
                tipGlow.SetActive(startFallen == false);
            }
        }

        // Tips toward the stake's +Z, into the first cell of the row.
        private IEnumerator Fall()
        {
            Quaternion fallen = upright * Quaternion.Euler(fallAngle, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / fallDuration)
            {
                if (post != null)
                {
                    // Falls like a felled post: slow to start, fast at the end.
                    post.localRotation = Quaternion.Slerp(upright, fallen, t * t);
                }

                yield return null;
            }

            if (post != null)
            {
                post.localRotation = fallen;
            }

            if (tipGlow != null)
            {
                tipGlow.SetActive(false);
            }

            if (row != null)
            {
                row.Ignite();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (row != null && row.Cells.Count > 0 && row.Cells[0] != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f);
                Gizmos.DrawLine(transform.position + Vector3.up, row.Cells[0].transform.position + Vector3.up * 0.3f);
            }
        }
    }
}
