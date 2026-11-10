using System.Collections.Generic;
using UnityEngine;

namespace Kneel
{
    // Keeps a hanging fixture out of the fight's way (L5's hall chandeliers; owner, 2026-11-10: "chandeliers should not
    // block the view of the fight"). While it hangs between the camera and the player, the boss, or the line between
    // them, its OcclusionFade fades it (the camera's OcclusionFader steps it). The fader itself only tests the player,
    // and only against FadeProbe colliders; a chandelier is a ring in mid-air, so a sphere round it is the test here.
    [RequireComponent(typeof(OcclusionFade))]
    public class FightClearance : MonoBehaviour
    {
        // The fixture's reach round its transform (the hoop's radius), and the clearance kept outside it.
        public float radius = 2.4f;
        public float margin = 1.4f;

        // Other subjects of the fight to keep clear (the boss registers itself; the Colossus will, Stage 6).
        public static readonly List<Transform> Subjects = new List<Transform>();

        private OcclusionFade fade;
        private Transform player;
        private float nextSearch;
        private Renderer[] renderers;
        private float lastBlocked = -10f;
        private float blockedSince = -1f;
        private bool hidden;

        private void Awake()
        {
            fade = GetComponent<OcclusionFade>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            if (player == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                var movement = FindAnyObjectByType<PlayerMovement>();
                if (movement != null)
                    player = movement.transform;
            }

            Vector3 eye = cam.transform.position;
            Vector3 centre = transform.position;
            float reach = radius + margin;
            Vector3 kael = player != null ? player.position + Vector3.up * 1f : Vector3.zero;
            bool blocks = player != null && Blocks(eye, kael, centre, reach);

            for (int i = Subjects.Count - 1; i >= 0 && blocks == false; i--)
            {
                Transform subject = Subjects[i];
                if (subject == null)
                {
                    Subjects.RemoveAt(i);
                    continue;
                }

                if (subject.gameObject.activeInHierarchy == false)
                    continue;

                Vector3 him = subject.position + Vector3.up * 2.5f;
                blocks = Blocks(eye, him, centre, reach);
                for (float t = 0.25f; t < 1f && blocks == false && player != null; t += 0.25f)
                    blocks = Blocks(eye, Vector3.Lerp(kael, him, t), centre, reach);
            }

            // It fades, and once faded it goes altogether: its candle flames are opaque particles that no alpha
            // touches, and even faded iron reads as a ghost over the fight. Back the moment the line is clear (it fades
            // in from there).
            float now = Time.unscaledTime;
            if (blocks)
            {
                fade.MarkBlocking();
                lastBlocked = now;
                if (blockedSince < 0f)
                    blockedSince = now;
            }
            else if (now - lastBlocked > 0.4f)
            {
                blockedSince = -1f;
            }

            bool hide = blockedSince >= 0f && now - blockedSince > 0.3f;
            if (hide != hidden)
            {
                hidden = hide;
                foreach (Renderer r in renderers)
                {
                    if (r != null)
                        r.enabled = hide == false;
                }
            }
        }

        // Switched off (a chandelier coming down): everything shows again.
        private void OnDisable()
        {
            blockedSince = -1f;
            if (hidden == false || renderers == null)
                return;
            hidden = false;
            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.enabled = true;
            }
        }

        // True when the sight line from the eye to the target passes within reach of the centre, short of the target.
        private static bool Blocks(Vector3 eye, Vector3 target, Vector3 centre, float reach)
        {
            Vector3 line = target - eye;
            float lengthSq = line.sqrMagnitude;
            if (lengthSq < 0.01f)
                return false;

            float t = Vector3.Dot(centre - eye, line) / lengthSq;
            if (t <= 0f || t >= 1f)
                return false;

            return (eye + line * t - centre).sqrMagnitude < reach * reach;
        }
    }
}
