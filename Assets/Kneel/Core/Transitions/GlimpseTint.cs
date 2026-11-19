using UnityEngine;

namespace Kneel.Core
{
    // Darkens a group of copied pieces through a property block (their materials stay shared and untouched): the L5
    // nave seen through L4's throne-hall doors stands under L4's open noon ambient, and without this the dark hall
    // beyond the doors would read lit like the courtyard.
    [ExecuteAlways]
    public class GlimpseTint : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Range(0f, 1f)]
        public float brightness = 0.35f;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        public void Apply()
        {
            var block = new MaterialPropertyBlock();
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].HasProperty(BaseColor) == false)
                    {
                        continue;
                    }

                    Color c = mats[i].GetColor(BaseColor);
                    r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColor, new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a));
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
