using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // What mud is made of. The water in it is the ground's own: L3Ground paints pools into the layout's
    // wetness and the Kneel/Ground shader fills the earth's hollows there with the same dark, mirroring water
    // that stands anywhere else the ground is wet, so a pool has a real shoreline that follows the bumps of
    // the earth. This lays the solid part round those pools: faceted clods squeezed up by feet and hooves,
    // thickest along the water's edge. One baked mesh per mud area, no colliders: the gameplay is the mud
    // volume's trigger, which is untouched.
    public static class L3Mud
    {
        // Wet earth catches the low sun on its facets.
        public static Material ClodMaterial => L3Build.Flat("L3_Mud_Clod", "#33281F", 0.62f);

        // keepOut: plan rectangles (x0, x1, z0, z1) where nothing is laid (props, the trench).
        public static GameObject Build(Transform parent, L3Ground.Stain stain, params float[] keepOut)
        {
            var mesh = new L3Mesh();
            int seed = 0;
            float Rand()
            {
                return L3Level.Hash("mud" + stain.Name, seed++);
            }

            bool Free(Vector2 p, float margin)
            {
                for (int i = 0; i + 3 < keepOut.Length; i += 4)
                {
                    if (p.x > keepOut[i] - margin && p.x < keepOut[i + 1] + margin && p.y > keepOut[i + 2] - margin && p.y < keepOut[i + 3] + margin)
                    {
                        return false;
                    }
                }

                return true;
            }

            float area = (stain.X1 - stain.X0) * (stain.Z1 - stain.Z0);
            int tries = Mathf.RoundToInt(area * 1.6f), clods = 0;
            for (int i = 0; i < tries; i++)
            {
                var p = new Vector2(Mathf.Lerp(stain.X0 - 1.2f, stain.X1 + 1.2f, Rand()), Mathf.Lerp(stain.Z0 - 1.2f, stain.Z1 + 1.2f, Rand()));
                float pool = L3Ground.Pool(p, stain), weight = L3Ground.StainWeight(p, stain.X0, stain.X1, stain.Z0, stain.Z1);
                // Along the shore of a pool almost always; out in the open mud now and then; never in the water.
                bool shore = pool > 0.04f && pool < 0.55f;
                float chance = shore ? 0.8f : pool >= 0.55f ? 0f : 0.07f * weight;
                if (Rand() > chance || weight < 0.3f || !Free(p, 0.3f))
                {
                    continue;
                }

                float radius = Mathf.Lerp(0.14f, shore ? 0.42f : 0.32f, Rand() * Rand());
                var rotation = Quaternion.Euler((Rand() - 0.5f) * 14f, Rand() * 360f, (Rand() - 0.5f) * 14f);
                float height = Mathf.Lerp(0.025f, 0.07f, Rand());
                mesh.Ellipsoid(0, new Vector3(p.x, height * 0.15f, p.y), rotation, new Vector3(radius * Mathf.Lerp(1f, 1.7f, Rand()), height, radius), 7, 3, Vector2.zero);
                clods++;
            }

            var saved = L3Build.SaveMesh(mesh, "Mud", "L3_Mud_" + stain.Name);
            var go = L3Build.MeshChild(parent, "Mud " + stain.Name + " (" + clods + " clods; the water is in the ground paint)", saved, Vector3.zero, ClodMaterial);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
