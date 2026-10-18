using Unity.AI.Navigation;
using UnityEngine;

namespace Kneel.Hazards
{
    // A patch of ground that changes how actors move (mud: half the roll, 85% speed). Applies to everyone
    // standing in it through IHazardTarget.SetSurface / ClearSurface.
    // The patch is resizable: Size drives the trigger, the ground plane, the NavMesh modifier and the damp
    // bands (the lighter strips of wet ground that warn of the mud, drawn just outside it).
    public class SurfaceVolume : MonoBehaviour
    {
        public const float DampBandWidth = 2f;

        [SerializeField]
        private SurfaceTuning tuning;

        [SerializeField]
        private HazardTuning contact;

        // Footprint in metres (x, z), centred on this object.
        [SerializeField]
        private Vector2 size = new Vector2(4f, 4f);

        [SerializeField]
        private bool dampWest = true;

        [SerializeField]
        private bool dampEast = true;

        [SerializeField]
        private bool dampSouth;

        [SerializeField]
        private bool dampNorth;

        [SerializeField]
        private BoxCollider area;

        // Unit quads lying flat (x, z), scaled to fit.
        [SerializeField]
        private Transform ground;

        [SerializeField]
        private Transform bandWest;

        [SerializeField]
        private Transform bandEast;

        [SerializeField]
        private Transform bandSouth;

        [SerializeField]
        private Transform bandNorth;

        [SerializeField]
        private NavMeshModifierVolume navVolume;

        private readonly HazardSensor sensor = new HazardSensor();

        public Vector2 Size => size;

        public int Occupants => sensor.Inside.Count;

        // Sets the footprint and which sides get a damp band.
        public void Configure(Vector2 footprint, bool west, bool east, bool south, bool north)
        {
            size = footprint;
            dampWest = west;
            dampEast = east;
            dampSouth = south;
            dampNorth = north;
            ApplyShape();
        }

        public void ApplyShape()
        {
            const float height = 2f;
            if (area != null)
            {
                area.size = new Vector3(size.x, height, size.y);
                area.center = new Vector3(0f, height * 0.5f, 0f);
            }

            if (ground != null)
            {
                ground.localScale = new Vector3(size.x, 1f, size.y);
            }

            if (navVolume != null)
            {
                navVolume.size = new Vector3(size.x, height, size.y);
                navVolume.center = new Vector3(0f, height * 0.5f - 0.5f, 0f);
            }

            float offsetX = (size.x + DampBandWidth) * 0.5f;
            float offsetZ = (size.y + DampBandWidth) * 0.5f;
            PlaceBand(bandWest, dampWest, new Vector3(-offsetX, 0f, 0f), new Vector3(DampBandWidth, 1f, size.y));
            PlaceBand(bandEast, dampEast, new Vector3(offsetX, 0f, 0f), new Vector3(DampBandWidth, 1f, size.y));
            PlaceBand(bandSouth, dampSouth, new Vector3(0f, 0f, -offsetZ), new Vector3(size.x, 1f, DampBandWidth));
            PlaceBand(bandNorth, dampNorth, new Vector3(0f, 0f, offsetZ), new Vector3(size.x, 1f, DampBandWidth));
        }

        private static void PlaceBand(Transform band, bool shown, Vector3 offset, Vector3 scale)
        {
            if (band == null)
            {
                return;
            }

            band.gameObject.SetActive(shown);
            band.localPosition = new Vector3(offset.x, band.localPosition.y, offset.z);
            band.localScale = scale;
        }

        private void FixedUpdate()
        {
            sensor.Poll(area, contact.actorMask);
            foreach (var actor in sensor.Entered)
            {
                if (actor is IHazardTarget target)
                {
                    target.SetSurface(tuning.rollDistanceMultiplier, tuning.moveSpeedMultiplier);
                }
            }

            foreach (var actor in sensor.Exited)
            {
                if (actor != null && actor is IHazardTarget target)
                {
                    target.ClearSurface();
                }
            }
        }

        private void OnDisable()
        {
            foreach (var actor in sensor.Inside)
            {
                if (actor != null && actor is IHazardTarget target)
                {
                    target.ClearSurface();
                }
            }

            sensor.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.2f, 0.1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up, new Vector3(size.x, 2f, size.y));
        }
    }
}
