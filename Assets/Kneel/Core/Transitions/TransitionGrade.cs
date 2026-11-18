using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Kneel.Core
{
    // The grade lean across a seam: another level's own global grade blended over this one's at `amount` (a global
    // volume above the level's own and its zones, below the player's brightness), plus its fog and ambient colours
    // mixed into RenderSettings by the same amount. Lives in the active scene, so it goes with it.
    public class TransitionGrade
    {
        private const float Priority = 100f;

        private readonly Volume volume;
        private readonly LevelTransitionCatalog.LevelLook look;
        private readonly Color fog;
        private readonly Color sky;
        private readonly Color equator;
        private readonly Color ground;

        public float Amount { get; private set; }

        public TransitionGrade(LevelTransitionCatalog.LevelLook toward)
        {
            look = toward;
            fog = RenderSettings.fogColor;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            if (look != null && look.grade != null)
            {
                var go = new GameObject("[TransitionGrade]");
                SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = Priority;
                volume.sharedProfile = look.grade;
                volume.weight = 0f;
            }
        }

        public void Set(float amount)
        {
            Amount = Mathf.Clamp01(amount);
            if (volume != null)
            {
                volume.weight = Amount;
                volume.enabled = Amount > 0.0001f;
            }

            if (look == null)
            {
                return;
            }

            RenderSettings.fogColor = Color.Lerp(fog, look.fogColor, Amount);
            if (RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientSkyColor = Color.Lerp(sky, look.ambientSky, Amount);
                RenderSettings.ambientEquatorColor = Color.Lerp(equator, look.ambientEquator, Amount);
                RenderSettings.ambientGroundColor = Color.Lerp(ground, look.ambientGround, Amount);
            }
        }

        // Back to the level's own look, exactly, and gone.
        public void Remove()
        {
            Set(0f);
            if (volume != null)
            {
                Object.Destroy(volume.gameObject);
            }
        }
    }
}
