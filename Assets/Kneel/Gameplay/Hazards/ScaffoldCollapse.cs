using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.Hazards
{
    // A masons' scaffold or the chapel's choir loft. One blow on a strut (ScaffoldStrut) starts it: it creaks,
    // dust trickles and the planks sag for 1.5 s, then it comes down. Everything in the collapse zone (the
    // footprint plus 1 R, chalk-marked) takes two hits; anything standing on it is killed. A loft can carry lamps
    // that light the slick below it. The debris then blocks and carves until the level resets. A demo scaffold
    // is played once by its trigger and never replays.
    public class ScaffoldCollapse : MonoBehaviour, ILevelResettable
    {
        public enum State
        {
            Standing,
            Creaking,
            Collapsed,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        // Played once and never reset (the rampart-stair demo).
        [SerializeField]
        private bool demo;

        // The standing structure (deck, uprights) and the debris it leaves; the falling part tips about its pivot.
        [SerializeField]
        private Transform falling;

        [SerializeField]
        private Vector3 fallEuler = new Vector3(0f, 0f, 70f);

        [SerializeField]
        private GameObject creakVisual;

        [SerializeField]
        private GameObject debris;

        [SerializeField]
        private BoxCollider zone;

        // Standing on the deck when it goes is fatal.
        [SerializeField]
        private BoxCollider deck;

        [SerializeField]
        private Collider standingCollider;

        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private OilSlick igniteOnCollapse;

        private readonly HazardSensor zoneSensor = new HazardSensor();
        private readonly HazardSensor deckSensor = new HazardSensor();
        private Quaternion upright;
        private bool uprightKnown;
        private bool played;

        public State Current { get; private set; }

        public event Action<ScaffoldCollapse> CreakStarted;

        public event Action<ScaffoldCollapse> Collapsed;

        private void Awake()
        {
            if (zone != null)
            {
                zone.isTrigger = true;
            }

            if (deck != null)
            {
                deck.isTrigger = true;
            }

            CacheUpright();
            ResetState();
        }

        public void Strike()
        {
            if (Current != State.Standing || (demo && played))
            {
                return;
            }

            played = true;
            Current = State.Creaking;
            CreakStarted?.Invoke(this);
            StartCoroutine(Creak());
        }

        public void ResetState()
        {
            StopAllCoroutines();
            CacheUpright();
            bool down = demo && played;
            Current = down ? State.Collapsed : State.Standing;
            if (falling != null)
            {
                falling.localRotation = down ? upright * Quaternion.Euler(fallEuler) : upright;
                falling.gameObject.SetActive(true);
            }

            if (creakVisual != null)
            {
                creakVisual.SetActive(false);
            }

            if (debris != null)
            {
                debris.SetActive(down);
            }

            if (standingCollider != null)
            {
                standingCollider.enabled = down == false;
            }

            if (obstacle != null)
            {
                obstacle.enabled = down;
            }
        }

        private void CacheUpright()
        {
            if (uprightKnown == false && falling != null)
            {
                upright = falling.localRotation;
                uprightKnown = true;
            }
        }

        private IEnumerator Creak()
        {
            float creak = tuning != null ? tuning.scaffoldCreak : 1.5f;
            float fall = tuning != null ? tuning.scaffoldFall : 0.6f;
            if (creakVisual != null)
            {
                creakVisual.SetActive(true);
            }

            for (float t = 0f; t < creak; t += Time.deltaTime)
            {
                if (falling != null)
                {
                    // The sag: a slow lean with a shudder in it.
                    falling.localRotation = upright * Quaternion.Euler(fallEuler * (0.04f * t / creak) + Vector3.one * Mathf.Sin(t * 40f) * 0.25f);
                }

                yield return null;
            }

            if (creakVisual != null)
            {
                creakVisual.SetActive(false);
            }

            // Who is under it and who is on it, at the moment it goes.
            if (contact != null)
            {
                if (deck != null)
                {
                    deckSensor.Clear();
                    deckSensor.Poll(deck, contact.actorMask);
                    foreach (var actor in deckSensor.Inside)
                    {
                        HazardContact.Report(actor, HazardKind.Fall, contact, gameObject);
                    }
                }

                if (zone != null)
                {
                    zoneSensor.Clear();
                    zoneSensor.Poll(zone, contact.actorMask);
                    foreach (var actor in zoneSensor.Inside)
                    {
                        if (deckSensor.Inside.Contains(actor) == false)
                        {
                            HazardContact.Report(actor, HazardKind.Crush, contact, gameObject);
                        }
                    }
                }
            }

            if (standingCollider != null)
            {
                standingCollider.enabled = false;
            }

            Quaternion from = falling != null ? falling.localRotation : Quaternion.identity;
            Quaternion to = upright * Quaternion.Euler(fallEuler);
            for (float t = 0f; t < 1f; t += Time.deltaTime / fall)
            {
                if (falling != null)
                {
                    falling.localRotation = Quaternion.Slerp(from, to, t * t);
                }

                yield return null;
            }

            if (falling != null)
            {
                falling.localRotation = to;
            }

            if (debris != null)
            {
                debris.SetActive(true);
            }

            if (obstacle != null)
            {
                obstacle.enabled = true;
            }

            Current = State.Collapsed;
            Collapsed?.Invoke(this);
            if (igniteOnCollapse != null)
            {
                igniteOnCollapse.Ignite();
            }
        }
    }
}
