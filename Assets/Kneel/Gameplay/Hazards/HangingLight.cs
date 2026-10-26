using System;
using System.Collections;
using UnityEngine;

namespace Kneel.Hazards
{
    // A light over a slick: a cresset on a jib, the chapel's corona on its chain, the loft's lamps, a brazier on
    // a tripod (decision 17). One blow on its post, chain or strut (the sword, an enemy's miss) or a body
    // charging into it sets it off: a 1.8 s swing with sparks and creaking, then it falls into its slick, which
    // lights. Its strike collider sits on the Enemy layer, where the sword looks.
    public class HangingLight : MonoBehaviour, IDamageable, ILevelResettable
    {
        public enum State
        {
            Hanging,
            Swinging,
            Fallen,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        // The slick it falls into.
        [SerializeField]
        private OilSlick slick;

        // What swings during the tell (its pivot at the hook or the tripod's foot).
        [SerializeField]
        private Transform swinging;

        // Where 'swinging' ends up once fallen (an empty child placed in the prefab).
        [SerializeField]
        private Transform landed;

        [SerializeField]
        private GameObject tellVisual;

        [SerializeField]
        private float swingDegrees = 14f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool restKnown;

        public State Current { get; private set; }

        public OilSlick Slick
        {
            get => slick;
            set => slick = value;
        }

        public event Action<HangingLight> Struck;

        public event Action<HangingLight> Landed;

        private void Awake()
        {
            CacheRest();
            ResetState();
        }

        public void TakeHit(DamageInfo info)
        {
            if (info.isEnvironmental == false)
            {
                Strike();
            }
        }

        // A body charging or knocked into the post, or any scripted cause.
        public void Strike()
        {
            if (Current != State.Hanging)
            {
                return;
            }

            Current = State.Swinging;
            Struck?.Invoke(this);
            StartCoroutine(SwingAndFall());
        }

        public void ResetState()
        {
            StopAllCoroutines();
            CacheRest();
            Current = State.Hanging;
            if (swinging != null)
            {
                swinging.localPosition = restPosition;
                swinging.localRotation = restRotation;
            }

            if (tellVisual != null)
            {
                tellVisual.SetActive(false);
            }
        }

        private void CacheRest()
        {
            if (restKnown == false && swinging != null)
            {
                restPosition = swinging.localPosition;
                restRotation = swinging.localRotation;
                restKnown = true;
            }
        }

        private IEnumerator SwingAndFall()
        {
            float swing = tuning != null ? tuning.lightSwing : 1.8f;
            float fall = tuning != null ? tuning.lightFall : 0.3f;
            if (tellVisual != null)
            {
                tellVisual.SetActive(true);
            }

            for (float t = 0f; t < swing; t += Time.deltaTime)
            {
                if (swinging != null)
                {
                    // Swinging harder as the fastening gives.
                    float amplitude = swingDegrees * (0.4f + 0.6f * t / swing);
                    swinging.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 9f) * amplitude, 0f, Mathf.Sin(t * 6.3f) * amplitude * 0.5f);
                }

                yield return null;
            }

            if (swinging != null && landed != null)
            {
                Vector3 fromPosition = swinging.position;
                Quaternion fromRotation = swinging.rotation;
                for (float t = 0f; t < 1f; t += Time.deltaTime / fall)
                {
                    swinging.SetPositionAndRotation(Vector3.Lerp(fromPosition, landed.position, t * t), Quaternion.Slerp(fromRotation, landed.rotation, t));
                    yield return null;
                }

                swinging.SetPositionAndRotation(landed.position, landed.rotation);
            }

            if (tellVisual != null)
            {
                tellVisual.SetActive(false);
            }

            Current = State.Fallen;
            Landed?.Invoke(this);
            if (slick != null)
            {
                slick.Ignite();
            }
        }
    }
}
