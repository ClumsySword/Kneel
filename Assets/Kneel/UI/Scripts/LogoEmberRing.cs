using UnityEngine;

namespace Kneel.UI
{
    // Keeps the title logo's ember emitter on the logo's painted ring: the logo is fitted to the screen, so the
    // ring's radius in canvas units changes with the aspect ratio. The radius is a fraction of the logo's width.
    public class LogoEmberRing : MonoBehaviour
    {
        [SerializeField] private RectTransform logo;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private float radius = 0.2035f;

        private float appliedWidth = -1f;

        private void LateUpdate()
        {
            float width = logo.rect.width;
            if (width <= 0f || Mathf.Approximately(width, appliedWidth))
            {
                return;
            }

            ParticleSystem.ShapeModule shape = embers.shape;
            shape.radius = width * radius;
            bool first = appliedWidth < 0f;
            appliedWidth = width;

            // First fit: restart so the prewarmed embers sit on the right-sized ring.
            if (first)
            {
                embers.Clear(true);
                embers.Simulate(2.6f, true, true, false);
                embers.Play(true);
            }
        }
    }
}
