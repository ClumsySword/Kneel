using System.Text;
using UnityEngine;

namespace Kneel.EditorTools.Transitions
{
    // An ASCII map of what a player could walk, for authoring the seams' walks: '#' a solid collider at body height,
    // '.' open ground, ' ' no ground, '~' ground well below or above `y`. North (+z) is up, x runs left to right.
    public static class SeamMap
    {
        // A flat-lit overhead capture for reading a layout (no fog, no grade, bright ambient), north up.
        public static string Top(string name, Vector3 center, float size, int pixels = 1280)
        {
            var mode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.9f, 0.9f, 0.9f);
            RenderSettings.fog = false;
            try
            {
                Camera cam = TransitionShots.MakeCamera();
                cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                cam.orthographic = true;
                cam.orthographicSize = size * 0.5f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.25f, 0f, 0.25f);
                cam.transform.SetPositionAndRotation(center + Vector3.up * 150f, Quaternion.Euler(90f, 0f, 0f));
                var rt = new RenderTexture(pixels, pixels, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(pixels, pixels, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                System.IO.Directory.CreateDirectory(TransitionShots.Folder);
                string path = System.IO.Path.Combine(TransitionShots.Folder, name + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(cam.gameObject);
                return path;
            }
            finally
            {
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.fog = fog;
            }
        }

        public static string Draw(float xMin, float xMax, float zMin, float zMax, float step, float y, params Vector3[] marks)
        {
            Physics.SyncTransforms();
            int ground = LayerMask.GetMask("Ground");
            var sb = new StringBuilder();
            sb.AppendLine($"x {xMin}..{xMax}, z {zMax} (top) .. {zMin}, step {step}");
            for (float z = zMax; z >= zMin - 0.001f; z -= step)
            {
                sb.Append($"{z,7:0.0} ");
                for (float x = xMin; x <= xMax + 0.001f; x += step)
                {
                    char c = ' ';
                    for (int i = 0; i < marks.Length; i++)
                    {
                        if (Mathf.Abs(marks[i].x - x) < step * 0.5f && Mathf.Abs(marks[i].z - z) < step * 0.5f)
                        {
                            c = (char)('A' + i);
                        }
                    }

                    if (c == ' ')
                    {
                        var from = new Vector3(x, y + 30f, z);
                        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, ground, QueryTriggerInteraction.Ignore))
                        {
                            float gy = hit.point.y;
                            c = Mathf.Abs(gy - y) > 1.2f ? '~' : '.';
                            bool solid = false;
                            foreach (Collider col in Physics.OverlapCapsule(new Vector3(x, gy + 0.6f, z), new Vector3(x, gy + 1.6f, z), 0.3f, ~ground, QueryTriggerInteraction.Ignore))
                            {
                                if (col.gameObject.layer != LayerMask.NameToLayer("Player"))
                                {
                                    solid = true;
                                    break;
                                }
                            }

                            if (solid)
                            {
                                c = '#';
                            }
                        }
                    }

                    sb.Append(c);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
