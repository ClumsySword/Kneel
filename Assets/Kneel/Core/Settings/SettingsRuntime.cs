using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.Core
{
    // The persistent object that keeps the audio mixer and brightness in line with SettingsService. Master
    // volume is AudioListener.volume, so it covers every source, routed through the mixer or not; Music and SFX
    // are the mixer's exposed MusicVolume / SFXVolume. Brightness is a global top-priority volume that only
    // overrides the gamma offset, which none of the levels' volumes touch.
    public class SettingsRuntime : MonoBehaviour
    {
        public const string MixerResource = "KneelMixer";
        public const string MusicParameter = "MusicVolume";
        public const string SfxParameter = "SFXVolume";

        // Gamma offset at brightness 0 and 1.
        private const float BrightnessRange = 0.35f;

        private AudioMixer mixer;
        private LiftGammaGain gamma;

        public static AudioMixer Mixer { get; private set; }

        public static void Create()
        {
            var go = new GameObject("[Settings]");
            DontDestroyOnLoad(go);
            go.AddComponent<SettingsRuntime>();
        }

        private void Awake()
        {
            mixer = Resources.Load<AudioMixer>(MixerResource);
            Mixer = mixer;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Brightness (runtime)";
            gamma = profile.Add<LiftGammaGain>();
            gamma.gamma.overrideState = true;

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = profile;

            SettingsService.Changed += Apply;
        }

        // Not Awake: AudioMixer.SetFloat is ignored until the mixer has started.
        private void Start()
        {
            Apply();
        }

        private void OnDestroy()
        {
            SettingsService.Changed -= Apply;
        }

        private void Apply()
        {
            GameSettings s = SettingsService.Current;
            AudioListener.volume = s.masterVolume;
            if (mixer != null)
            {
                mixer.SetFloat(MusicParameter, ToDecibels(s.musicVolume));
                mixer.SetFloat(SfxParameter, ToDecibels(s.sfxVolume));
            }

            float offset = (s.brightness - 0.5f) * 2f * BrightnessRange;
            gamma.gamma.value = new Vector4(1f, 1f, 1f, offset);
        }

        private static float ToDecibels(float linear)
        {
            return linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);
        }
    }
}
