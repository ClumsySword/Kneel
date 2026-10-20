using System.Collections.Generic;
using UnityEngine;

namespace Kneel
{
    // On the camera: fades the tall scenery (OcclusionFade) that actually stands between the camera and the
    // player, the way Diablo-style cameras see through buildings. Sight-lines to the player (feet, chest and
    // head, each at the centre and either shoulder) and to the ground in a 2 m ring around him are raycast
    // against the pieces' exact mesh probes; a piece fades only when it blocks at least two of them, so a
    // corner grazing one line doesn't flicker it.
    [DefaultExecutionOrder(200)]
    public class OcclusionFader : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float shoulder = 0.35f;
        [SerializeField] private int minBlockedLines = 2;

        private static readonly float[] Heights = { 0.3f, 1.0f, 1.7f };
        private static readonly Vector3[] Ring =
        {
            new Vector3(2.2f, 0.4f, 0f), new Vector3(-2.2f, 0.4f, 0f), new Vector3(0f, 0.4f, -2.2f), new Vector3(0f, 0.4f, 2.2f),
            new Vector3(1.6f, 0.4f, -1.6f), new Vector3(-1.6f, 0.4f, -1.6f), new Vector3(1.6f, 0.4f, 1.6f), new Vector3(-1.6f, 0.4f, 1.6f),
        };
        private readonly Dictionary<OcclusionFade, int> counts = new Dictionary<OcclusionFade, int>();
        private readonly HashSet<OcclusionFade> thisLine = new HashSet<OcclusionFade>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private int probeMask;

        private void Awake()
        {
            int layer = LayerMask.NameToLayer(OcclusionFade.ProbeLayer);
            if (layer < 0)
            {
                enabled = false;
                return;
            }

            probeMask = 1 << layer;

            // The probes are only for sight-line tests: nothing ever collides with them.
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(layer, i, true);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<PlayerMovement>();
                if (player == null)
                {
                    return;
                }

                target = player.transform;
            }

            Vector3 eye = transform.position;
            Vector3 right = transform.right * shoulder;
            counts.Clear();
            foreach (float h in Heights)
            {
                Vector3 p = target.position + Vector3.up * h;
                Line(eye, p);
                Line(eye, p + right);
                Line(eye, p - right);
            }

            // The ground just around the player (where an enemy closing in or the next step would be): a house
            // that buries it fades too, not only one that covers the player himself.
            foreach (var offset in Ring)
            {
                Line(eye, target.position + offset);
            }

            foreach (var kv in counts)
            {
                if (kv.Value >= minBlockedLines)
                {
                    kv.Key.MarkBlocking();
                }
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = OcclusionFade.All.Count - 1; i >= 0; i--)
            {
                OcclusionFade.All[i].Step(dt);
            }
        }

        private void Line(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude - 0.2f;
            if (length <= 0f)
            {
                return;
            }

            int n = Physics.RaycastNonAlloc(from, d.normalized, hits, length, probeMask, QueryTriggerInteraction.Collide);
            thisLine.Clear();
            for (int i = 0; i < n; i++)
            {
                var piece = hits[i].collider.GetComponentInParent<OcclusionFade>();
                if (piece != null && thisLine.Add(piece))
                {
                    counts[piece] = counts.TryGetValue(piece, out int c) ? c + 1 : 1;
                }
            }
        }
    }
}
