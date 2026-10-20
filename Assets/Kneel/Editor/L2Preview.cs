using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Look-dev renders of single prefabs, away from the level (at x = 600): a dark ground, a moon-like key and
    // a camera at the gameplay pitch or any other angle. Particles are simulated forward, so fire shows in edit mode.
    public static class L2Preview
    {
        private static readonly Vector3 Stage = new Vector3(600f, 0f, 600f);

        public static string Render(string file, string[] keys, float spacing, float pitch, float yaw, float distance, int width = 1280, int height = 720, float simulate = 2.5f, bool night = true, Vector3 lookOffset = default, System.Action<Transform, Vector3> build = null)
        {
            var root = new GameObject("__Preview");
            try
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.SetParent(root.transform, false);
                ground.transform.position = Stage;
                ground.transform.localScale = Vector3.one * 6f;
                var groundMat = L2Build.Mat("L2_Ground");
                if (groundMat != null)
                {
                    ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                }

                float offset = -(keys.Length - 1) * spacing * 0.5f;
                for (int i = 0; i < keys.Length; i++)
                {
                    var go = L2Build.Spawn(keys[i], root.transform, Vector3.zero, Vector3.zero);
                    go.transform.position = Stage + Vector3.right * (offset + i * spacing);
                }

                build?.Invoke(root.transform, Stage);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                {
                    if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                    {
                        ps.Simulate(simulate, true, true);
                    }
                }

                if (!night)
                {
                    var key = new GameObject("Key").AddComponent<Light>();
                    key.transform.SetParent(root.transform, false);
                    key.type = LightType.Directional;
                    key.intensity = 1.6f;
                    key.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
                }

                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.AddComponent<Camera>();
                var main = Camera.main;
                if (main != null)
                {
                    cam.CopyFrom(main);
                }

                cam.fieldOfView = 40f;
                var rotation = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 look = Stage + (lookOffset == Vector3.zero ? Vector3.up * 0.6f : lookOffset);
                cam.transform.SetPositionAndRotation(look - rotation * Vector3.forward * distance, rotation);

                var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return file;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
