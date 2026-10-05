using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.EditorTools
{
    // L2's fire VFX, in the low-poly style: flames are faceted 3D tongues (mesh particles, world-upright), so
    // they keep their shape from the angled top-down camera instead of reading as flat cut-outs. Each tongue is
    // born pale yellow, burns orange and dies deep red as it shrinks away; a few brighter, smaller tongues burn
    // at the heart. These fires have been burning for a while: flames come in patches, not sheets, over glowing
    // embers, with sparks lifting off and a column of smoke. Materials and meshes are assets (no runtime setup).
    public static class L2Fire
    {
        private const string FxPath = L2Build.MaterialsPath + "/FX";
        private const string MeshPath = L2Build.MeshesPath + "/FX";
        private const int Variants = 3;

        [MenuItem("Kneel/L2/Build/Fire Materials")]
        public static void RebuildMenu()
        {
            Debug.Log("[L2] " + RebuildMaterials());
        }

        public static string RebuildMaterials()
        {
            L2Build.EnsureFolder(FxPath);
            L2Build.EnsureFolder(MeshPath);
            for (int i = 0; i < Variants; i++)
            {
                FlameMesh(i);
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(FxPath + "/L2_FX_FlameMesh.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, FxPath + "/L2_FX_FlameMesh.mat");
            }

            // Opaque, unlit, HDR: the particle colour carries the flame's hue, the intensity feeds the bloom.
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_ZWrite", 1f);
            mat.SetFloat("_Cull", (float)CullMode.Back);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Geometry;
            mat.SetColor("_BaseColor", new Color(1.35f, 1.35f, 1.35f, 1f));
            mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return "fire: flame meshes + material";
        }

        public static Material FlameMaterial => AssetDatabase.LoadAssetAtPath<Material>(FxPath + "/L2_FX_FlameMesh.mat");

        private static Mesh[] Meshes()
        {
            var list = new List<Mesh>();
            for (int i = 0; i < Variants; i++)
            {
                list.Add(AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshPath}/L2_Flame_{i}.asset"));
            }

            return list.ToArray();
        }

        // ---------------------------------------------------------------- Flame tongue mesh

        // A faceted tongue 1 m tall: a narrow foot, a belly, then tapering rings that lean and twist into a tip.
        private static Mesh FlameMesh(int variant)
        {
            var rng = new System.Random(300 + variant);
            int sides = 5 + variant % 2;
            float[] ys = { 0f, 0.16f, 0.42f, 0.7f };
            float[] rs = { 0.2f, 0.34f, 0.25f, 0.12f };
            float lean = 0.1f + variant * 0.05f;
            float twist = 25f + variant * 15f;
            var rings = new Vector3[ys.Length][];
            for (int r = 0; r < ys.Length; r++)
            {
                rings[r] = new Vector3[sides];
                float bend = lean * ys[r] * ys[r];
                for (int i = 0; i < sides; i++)
                {
                    float a = (i / (float)sides * 360f + twist * ys[r]) * Mathf.Deg2Rad;
                    float wobble = 0.85f + (float)rng.NextDouble() * 0.3f;
                    rings[r][i] = new Vector3(Mathf.Cos(a) * rs[r] * wobble + bend, ys[r], Mathf.Sin(a) * rs[r] * wobble);
                }
            }

            var tip = new Vector3(lean * 1.2f, 1f, 0.02f * variant);
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int k = v.Count;
                v.Add(a);
                v.Add(b);
                v.Add(c);
                t.Add(k);
                t.Add(k + 1);
                t.Add(k + 2);
            }

            for (int r = 0; r < ys.Length - 1; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Tri(rings[r][i], rings[r + 1][i], rings[r + 1][j]);
                    Tri(rings[r][i], rings[r + 1][j], rings[r][j]);
                }
            }

            var top = rings[ys.Length - 1];
            var foot = rings[0];
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Tri(top[i], tip, top[j]);
                Tri(foot[j], Vector3.zero, foot[i]);
            }

            var mesh = new Mesh { name = "L2_Flame_" + variant };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{MeshPath}/L2_Flame_{variant}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Emitters

        public enum Shape { Point, Box, Line }

        // One burning spot. size scales the tongues (1 = a campfire, ~1.4 = a house fire); area spreads the fire
        // over a box (x, z) or, with Shape.Line, along a line of length area.x. density thins or thickens it.
        public static Transform Fire(Transform parent, string name, Vector3 position, float size, float lightIntensity, float lightRange,
            bool smoke, Vector3 area = default, Shape shape = Shape.Point, float density = 1f)
        {
            if (FlameMaterial == null || Meshes()[0] == null)
            {
                RebuildMaterials();
            }

            var head = new GameObject(name).transform;
            head.SetParent(parent, false);
            head.localPosition = position;
            if (area.sqrMagnitude > 0f && shape == Shape.Point)
            {
                shape = Shape.Box;
            }

            float spread = shape == Shape.Point ? 1f : Mathf.Max(1f, shape == Shape.Line ? area.x / 1.6f : area.x * area.z / 2.2f);
            float scale = Mathf.Sqrt(size);

            // The tongues: patchy (the fire has burnt down), each lives under a second.
            var tongues = Tongues("Flames", head, Mathf.CeilToInt(48 * spread * density), 6f * scale * spread * density, new Vector2(0.55f, 0.95f), new Vector2(0.65f, 1.3f) * size,
                new Ramp(("#FFD45C", 0f), ("#FF9A22", 0.3f), ("#E8481A", 0.7f), ("#8C1E0E", 1f)));
            Place(tongues, shape, area, size);

            // The heart: smaller, brighter tongues low in the fire.
            var heart = Tongues("Heart", head, Mathf.CeilToInt(24 * spread * density), 3.5f * scale * spread * density, new Vector2(0.35f, 0.6f), new Vector2(0.35f, 0.65f) * size,
                new Ramp(("#FFF0A8", 0f), ("#FFC23C", 0.5f), ("#FF8A1E", 1f)));
            Place(heart, shape, area * 0.7f, size * 0.7f);

            // Sparks lifting off on the heat, drifting with the wind.
            var sparks = L1Wayfinding.Particles("Embers", head, L1Build.FxMat("L1_FX_Ember"), Mathf.CeilToInt(25 * spread), 3f * scale * spread,
                new Vector2(2f, 3.6f), new Vector2(0.7f, 1.5f) * scale, new Vector2(0.035f, 0.07f), new Color(1f, 0.78f, 0.45f, 1f), new Color(1f, 0.35f, 0.1f, 0.8f), -0.05f);
            sparks.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ApplyShape(sparks.shape, shape, area, size * 0.8f, 20f);
            var sparkNoise = sparks.noise;
            sparkNoise.enabled = true;
            sparkNoise.strength = 0.6f;
            sparkNoise.frequency = 0.5f;
            sparkNoise.scrollSpeed = 0.4f;

            // A low warm glow round the base, so the fire sits in its own light.
            var glow = L1Wayfinding.Particles("Glow", head, L1Build.FxMat("L1_FX_Glow"), 3, 1.6f, new Vector2(1.6f, 1.8f), Vector2.zero,
                new Vector2(1.6f, 2.1f) * scale * Mathf.Max(1f, Mathf.Sqrt(spread) * 0.7f), new Color(1f, 0.5f, 0.2f, 0.18f), new Color(1f, 0.42f, 0.16f, 0.14f));
            glow.transform.localPosition = Vector3.up * 0.3f * size;
            ApplyShape(glow.shape, shape, area * 0.6f, 0.2f, 0f);

            if (smoke)
            {
                Smoke(head, size, shape, area, spread, scale);
            }

            foreach (var r in head.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                if (r.renderMode != ParticleSystemRenderMode.Mesh)
                {
                    // Billboards right in front of the camera must never balloon over the screen.
                    r.maxParticleSize = r.name == "Smoke" ? 1.2f : 0.45f;
                }
            }

            if (lightIntensity > 0f)
            {
                Light(head, size, lightIntensity, lightRange);
            }

            return head;
        }

        // A spot that has burnt down to glowing embers: the odd low flame and a thin smoke trail.
        public static Transform Smoulder(Transform parent, string name, Vector3 position, Vector2 area, float yaw = 0f, bool smoke = true)
        {
            var head = Fire(parent, name, position, 0.45f, 0f, 0f, false, new Vector3(area.x, 0f, area.y), Shape.Box, 0.25f);
            head.localRotation = Quaternion.Euler(0f, yaw, 0f);
            GroundEmbers(head, Vector3.zero, area);
            if (smoke)
            {
                Smoke(head, 0.6f, Shape.Box, new Vector3(area.x, 0f, area.y) * 0.5f, 1f, 0.8f);
            }

            return head;
        }

        // The flame inside a lantern: a couple of tiny tongues and the lantern's light, at the flame itself.
        public static Transform Candle(Transform parent, string name, Vector3 position, float intensity, float range)
        {
            if (FlameMaterial == null || Meshes()[0] == null)
            {
                RebuildMaterials();
            }

            var head = new GameObject(name).transform;
            head.SetParent(parent, false);
            head.localPosition = position;
            var flame = Tongues("Flame", head, 6, 5f, new Vector2(0.3f, 0.5f), new Vector2(0.07f, 0.11f), new Ramp(("#FFF3C4", 0f), ("#FFC45A", 0.6f), ("#F07A2A", 1f)));
            Place(flame, Shape.Point, Vector3.zero, 0.05f);
            var velocity = flame.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            var light = head.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = L1Build.Hex("#FFB25E");
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            var flicker = head.gameObject.AddComponent<Kneel.Lighting.LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = intensity;
            so.FindProperty("amplitude").floatValue = 0.1f;
            so.FindProperty("speed").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return head;
        }

        // Glowing coals under burning wreckage: short-lived orange flecks lying on the ground, pulsing.
        public static void GroundEmbers(Transform parent, Vector3 position, Vector2 area, float yaw = 0f)
        {
            var ps = L1Wayfinding.Particles("GroundEmbers", parent, L1Build.FxMat("L1_FX_Ember"), 160, area.x * area.y * 12f, new Vector2(1.2f, 2.6f), Vector2.zero,
                new Vector2(0.07f, 0.16f), new Color(1f, 0.52f, 0.16f, 1f), new Color(0.85f, 0.22f, 0.05f, 0.7f), 0f, ParticleSystemRenderMode.HorizontalBillboard);
            ps.transform.localPosition = position + Vector3.up * 0.04f;
            ps.transform.localRotation = Quaternion.Euler(-90f, yaw, 0f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, area.y, 0.02f);
        }

        // Mesh-particle tongues, standing upright in the world, each turned and tilted a little differently.
        private static ParticleSystem Tongues(string name, Transform parent, int max, float rate, Vector2 life, Vector2 size, Ramp colours)
        {
            var ps = L1Wayfinding.Particles(name, parent, FlameMaterial, max, rate, life, Vector2.zero, size, Color.white, Color.white);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            main.startColor = Color.white;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0.1f);

            // Lick up, flare, shrink to nothing: no fade needed (the flames are opaque).
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.22f, 1f), new Keyframe(0.6f, 0.75f), new Keyframe(1f, 0f)));

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = colours.ToGradient();

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.7f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(Meshes());
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = FlameMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Smoke(Transform head, float size, Shape shape, Vector3 area, float spread, float scale)
        {
            var plume = L1Wayfinding.Particles("Smoke", head, L1Build.FxMat("L1_FX_Smoke"), 60, 2.6f * Mathf.Max(1f, Mathf.Sqrt(spread)), new Vector2(10f, 14f), new Vector2(1f, 1.5f),
                new Vector2(1.3f, 2.2f) * scale, new Color(0.09f, 0.085f, 0.08f, 0.62f), new Color(0.2f, 0.19f, 0.19f, 0f));
            plume.transform.localPosition = Vector3.up * 1.2f * size;
            plume.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ApplyShape(plume.shape, shape, area * 0.6f, 0.8f * size, 8f);
            var plumeSize = plume.sizeOverLifetime;
            plumeSize.enabled = true;
            plumeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.4f));
            // Smoke leans north (up-screen, away from the camera and the fights).
            var velocity = plume.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            plume.GetComponent<ParticleSystemRenderer>().maxParticleSize = 1.2f;
        }

        private static void Light(Transform head, float size, float intensity, float range)
        {
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(head, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f * size, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = L1Build.Hex("#FF7A2E");
            light.intensity = intensity;
            light.range = range * 1.1f;
            light.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<Kneel.Lighting.LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = intensity;
            so.FindProperty("amplitude").floatValue = 0.2f;
            so.FindProperty("speed").floatValue = 1.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Place(ParticleSystem ps, Shape shape, Vector3 area, float size)
        {
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ApplyShape(ps.shape, shape, area, size, 6f);
        }

        private static void ApplyShape(ParticleSystem.ShapeModule s, Shape shape, Vector3 area, float size, float angle)
        {
            s.enabled = true;
            switch (shape)
            {
                case Shape.Box:
                    s.shapeType = ParticleSystemShapeType.Box;
                    s.scale = new Vector3(Mathf.Max(0.1f, area.x), Mathf.Max(0.1f, area.z), 0.05f);
                    break;
                case Shape.Line:
                    s.shapeType = ParticleSystemShapeType.Box;
                    s.scale = new Vector3(Mathf.Max(0.1f, area.x), 0.25f, 0.05f);
                    break;
                default:
                    s.shapeType = ParticleSystemShapeType.Cone;
                    s.angle = angle;
                    s.radius = Mathf.Max(0.05f, 0.32f * size);
                    break;
            }
        }

        // A colour ramp from hex keys (alpha stays 1: the tongues shrink away instead of fading).
        private readonly struct Ramp
        {
            private readonly (string hex, float time)[] keys;

            public Ramp(params (string hex, float time)[] keys)
            {
                this.keys = keys;
            }

            public Gradient ToGradient()
            {
                var colours = new GradientColorKey[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    colours[i] = new GradientColorKey(L1Build.Hex(keys[i].hex), keys[i].time);
                }

                var g = new Gradient();
                g.SetKeys(colours, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return g;
            }
        }
    }
}
