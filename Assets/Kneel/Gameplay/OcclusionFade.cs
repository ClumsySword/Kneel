using System.Collections.Generic;
using UnityEngine;

namespace Kneel
{
    // A tall piece of scenery that turns see-through while it actually hides the player (driven by
    // OcclusionFader on the camera). The test is exact: the fader raycasts mesh-collider probes on the
    // FadeProbe layer (children of the renderers, baked in the editor), not bounding boxes. The see-through
    // materials are assets made in the editor, so no shader variant is created or compiled mid-game.
    public class OcclusionFade : MonoBehaviour
    {
        public const string ProbeLayer = "FadeProbe";
        public static readonly List<OcclusionFade> All = new List<OcclusionFade>();

        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Material[] sources;
        [SerializeField] private Material[] faded;
        [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.3f;

        // Once faded, stay faded this long after the last blocked sight-line: no popping at the edges.
        private const float Hold = 0.4f;
        private const float Speed = 3.5f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Material[][] originals;
        private MaterialPropertyBlock block;
        private float alpha = 1f;
        private float lastBlocked = -10f;
        private bool swapped;

        public void Configure(Renderer[] targets, Material[] sourceMaterials, Material[] fadedMaterials)
        {
            renderers = targets;
            sources = sourceMaterials;
            faded = fadedMaterials;
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (swapped)
            {
                Restore();
            }
        }

        public void MarkBlocking()
        {
            lastBlocked = Time.unscaledTime;
        }

        public void Step(float dt)
        {
            float target = Time.unscaledTime - lastBlocked < Hold ? fadedAlpha : 1f;
            if (Mathf.Approximately(alpha, target) && !(target >= 1f && swapped))
            {
                return;
            }

            alpha = Mathf.MoveTowards(alpha, target, dt * Speed);
            if (alpha >= 1f)
            {
                if (swapped)
                {
                    Restore();
                }

                return;
            }

            if (!swapped)
            {
                Swap();
            }

            Apply();
        }

        private Material FadedFor(Material source)
        {
            if (sources == null || faded == null)
            {
                return null;
            }

            for (int i = 0; i < sources.Length && i < faded.Length; i++)
            {
                if (sources[i] == source)
                {
                    return faded[i];
                }
            }

            return null;
        }

        private void Swap()
        {
            originals = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                originals[i] = r.sharedMaterials;
                var mats = new Material[originals[i].Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    // A material without a see-through copy (none should exist) simply stays opaque.
                    var see = FadedFor(originals[i][m]);
                    mats[m] = see != null ? see : originals[i][m];
                }

                r.sharedMaterials = mats;
            }

            swapped = true;
        }

        private void Restore()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || originals == null || originals[i] == null)
                {
                    continue;
                }

                r.sharedMaterials = originals[i];
                for (int m = 0; m < originals[i].Length; m++)
                {
                    r.SetPropertyBlock(null, m);
                }
            }

            swapped = false;
            alpha = 1f;
        }

        private void Apply()
        {
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || originals[i] == null)
                {
                    continue;
                }

                for (int m = 0; m < originals[i].Length; m++)
                {
                    var source = originals[i][m];
                    if (source == null || !source.HasProperty(BaseColor) || FadedFor(source) == null)
                    {
                        continue;
                    }

                    Color c = source.GetColor(BaseColor);
                    c.a *= alpha;
                    block.Clear();
                    block.SetColor(BaseColor, c);
                    r.SetPropertyBlock(block, m);
                }
            }
        }
    }
}
