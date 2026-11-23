using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Review captures of the monastery through the real Main Camera at the gameplay framing (post-processing on),
    // written to Temp/Monastery. The camera and the player are put back afterwards. 'kael' places the player for the
    // shot (null leaves him where he is); local Volumes blend on his position, as in play.
    public static class L1MonasteryShots
    {
        public const string Folder = "Temp/Monastery";
        private const int Width = 1600, Height = 900;

        [MenuItem("Kneel/L1/Monastery/Capture Review Shots")]
        public static void CaptureMenu()
        {
            Debug.Log("[L1 Monastery] " + CaptureReview());
        }

        public static string CaptureReview()
        {
            Capture("01_Altar", new Vector3(1.1f, 0f, -39.4f), L1Layout.CameraDistance, new Vector3(1.1f, 0f, -39.4f));
            Capture("02_Corridor", new Vector3(0f, 0f, -20f), L1Layout.CameraDistance, new Vector3(0f, 0f, -20f));
            Capture("03_OuterDoor", new Vector3(0f, 0f, -8.5f), L1Layout.CameraDistance, new Vector3(0f, 0f, -8.5f));
            return "Captured to " + Folder + ".";
        }

        public static string Capture(string name, Vector3 focus, float distance, Vector3? kael)
        {
            var cam = Camera.main;
            var camPose = new Pose(cam.transform.position, cam.transform.rotation);
            Transform player = null;
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Player")
                {
                    player = go.transform;
                }
            }

            var playerPose = player != null ? new Pose(player.position, player.rotation) : default;
            try
            {
                Vector3 ground = Ground(focus);
                if (player != null && kael.HasValue)
                {
                    player.SetPositionAndRotation(Ground(kael.Value), Quaternion.identity);
                }

                var pose = L1Layout.CameraPose(ground, distance);
                cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
                {
                    ps.Simulate(8f, true, true, true);
                }

                var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var previous = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previous;
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(Folder);
                string path = Folder + "/" + name + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return path;
            }
            finally
            {
                cam.transform.SetPositionAndRotation(camPose.position, camPose.rotation);
                if (player != null)
                {
                    player.SetPositionAndRotation(playerPose.position, playerPose.rotation);
                }
            }
        }

        private static Vector3 Ground(Vector3 p)
        {
            int mask = LayerMask.GetMask("Ground");
            return Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, mask) ? hit.point : p;
        }
    }
}
