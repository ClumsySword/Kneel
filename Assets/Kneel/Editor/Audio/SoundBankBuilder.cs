using System;
using System.Collections.Generic;
using System.Linq;
using Kneel.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Kneel.EditorTools
{
    // Kneel/Audio/Build Sound Bank: turns the folders under Assets/Kneel/Audio into the SoundBank the game plays from.
    // Each SFX/<SfxId> folder becomes Events/<SfxId>.asset (new events get the starting values below; an existing
    // event keeps its tuning and only has its clip list refreshed), and each level gets its music, ambience, fire and
    // footstep surface. To swap a sound, replace the files in its folder and run this again. See Audio/CREDITS.md.
    public static class SoundBankBuilder
    {
        private const string Root = "Assets/Kneel/Audio";
        private const string EventsFolder = Root + "/Events";
        private const string BankPath = Root + "/Resources/SoundBank.asset";
        private const string MixerPath = Root + "/Resources/KneelMixer.mixer";

        // volume, spatial (0 flat .. 1 3D), pitch spread, max distance, cooldown
        private static readonly Dictionary<SfxId, (float volume, float spatial, float pitchSpread, float maxDistance, float cooldown)> Defaults =
            new Dictionary<SfxId, (float, float, float, float, float)>
            {
                { SfxId.PlayerSwingLight, (0.55f, 0.8f, 0.08f, 25f, 0.03f) },
                { SfxId.PlayerSwingHeavy, (0.7f, 0.8f, 0.06f, 25f, 0.03f) },
                { SfxId.HitFlesh, (0.85f, 0.8f, 0.08f, 30f, 0.03f) },
                { SfxId.HitShield, (0.8f, 0.8f, 0.08f, 30f, 0.03f) },
                { SfxId.Parry, (1f, 0.6f, 0.04f, 35f, 0.05f) },
                { SfxId.Block, (0.8f, 0.7f, 0.06f, 30f, 0.03f) },
                { SfxId.GuardBreak, (1f, 0.6f, 0.05f, 35f, 0.1f) },
                { SfxId.Roll, (0.55f, 0.6f, 0.08f, 20f, 0.05f) },
                { SfxId.FootstepStone, (0.55f, 0.9f, 0.1f, 18f, 0f) },
                { SfxId.FootstepEarth, (0.6f, 0.9f, 0.1f, 18f, 0f) },
                { SfxId.Land, (0.7f, 0.7f, 0.06f, 20f, 0.1f) },
                { SfxId.SwordDraw, (0.55f, 0.6f, 0.05f, 20f, 0.1f) },
                { SfxId.SwordSheathe, (0.5f, 0.6f, 0.05f, 20f, 0.1f) },
                { SfxId.PlayerHurt, (0.6f, 0f, 0.06f, 20f, 0.3f) },
                { SfxId.PlayerDeath, (0.85f, 0f, 0.03f, 20f, 1f) },
                { SfxId.BodyFall, (0.75f, 0.8f, 0.06f, 25f, 0.1f) },
                { SfxId.Drink, (0.6f, 0f, 0.04f, 20f, 0.5f) },
                { SfxId.Pickup, (0.7f, 0f, 0.06f, 20f, 0.1f) },
                { SfxId.Lever, (0.9f, 0.8f, 0.04f, 30f, 0.2f) },
                { SfxId.Gate, (0.9f, 0.9f, 0.04f, 40f, 0.2f) },
                { SfxId.ShrineRest, (0.8f, 0.6f, 0.03f, 30f, 0.5f) },
                { SfxId.TellFootman, (0.75f, 1f, 0.06f, 35f, 0.15f) },
                { SfxId.TellBrute, (0.85f, 1f, 0.06f, 40f, 0.3f) },
                { SfxId.TellFool, (0.8f, 1f, 0.06f, 40f, 2.5f) },
                { SfxId.TellArcher, (0.8f, 1f, 0.06f, 40f, 0.2f) },
                { SfxId.BowRelease, (0.8f, 1f, 0.06f, 40f, 0.05f) },
                { SfxId.ArrowImpact, (0.7f, 1f, 0.08f, 30f, 0.05f) },
                { SfxId.EnemyHurt, (0.65f, 1f, 0.1f, 30f, 0.15f) },
                { SfxId.EnemyDeath, (0.8f, 1f, 0.06f, 35f, 0.2f) },
                { SfxId.BruteRoar, (1f, 1f, 0.04f, 50f, 1f) },
            };

        [MenuItem("Kneel/Audio/Build Sound Bank")]
        public static void BuildMenu() => Debug.Log("[Audio] " + Build());

        public static string Build()
        {
            var log = new List<string>();
            SetImportSettings(log);
            Folder(EventsFolder);
            Folder(Root + "/Resources");

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.sounds.Clear();
            int empty = 0;
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                string path = EventsFolder + "/" + id + ".asset";
                var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
                if (sound == null)
                {
                    sound = ScriptableObject.CreateInstance<SoundEvent>();
                    if (Defaults.TryGetValue(id, out var d))
                    {
                        sound.volume = d.volume;
                        sound.spatial = d.spatial;
                        sound.pitchSpread = d.pitchSpread;
                        sound.maxDistance = d.maxDistance;
                        sound.cooldown = d.cooldown;
                    }

                    AssetDatabase.CreateAsset(sound, path);
                }

                sound.clips = Clips(Root + "/SFX/" + id);
                empty += sound.clips.Length == 0 ? 1 : 0;
                EditorUtility.SetDirty(sound);
                bank.sounds.Add(new SoundBank.Entry { id = id, sound = sound });
            }

            AudioClip[] explore = Clips(Root + "/Music/Levels/Explore");
            AudioClip[] dark = Clips(Root + "/Music/Levels/ExploreDark");
            AudioClip[] combat = Clips(Root + "/Music/Levels/Combat");
            AudioClip wind = Clips(Root + "/Ambience/Wind").FirstOrDefault();
            AudioClip fire = Clips(Root + "/Ambience/Fire").FirstOrDefault();

            // The levels' music lines (vault, 02-Design/Levels): L1 a low drone, no percussion; L2 the drone deepens with
            // a distant crackle of fire; L4 oppressive and smoke-muffled; L5 has the composed score (MusicDirector).
            Level(bank, "L1_BrokenLine_Playable_L", explore, combat, wind, fire, FootSurface.Earth, 0.3f);
            Level(bank, "L2_HollowVillage", explore, combat, fire, fire, FootSurface.Stone, 0.2f);
            Level(bank, "L3_FallowFields", explore, combat, wind, fire, FootSurface.Earth, 0.35f);
            Level(bank, "L4_BurningGate", dark, combat, fire, fire, FootSurface.Stone, 0.3f);
            Level(bank, "L5_ThroneRoom", dark, combat, wind, fire, FootSurface.Stone, 0.15f);
            Level(bank, "Arena_S", explore, combat, wind, fire, FootSurface.Stone, 0.25f);

            EditorUtility.SetDirty(bank);
            log.Add(AddAmbienceGroup());
            AssetDatabase.SaveAssets();
            log.Insert(0, bank.sounds.Count + " sound events (" + empty + " without clips), " + bank.levels.Count + " levels");
            return string.Join("; ", log.Where(l => string.IsNullOrEmpty(l) == false));
        }

        // A level's entry: created with these values, or refreshed (clips only) if it's there already.
        private static void Level(SoundBank bank, string scene, AudioClip[] explore, AudioClip[] combat, AudioClip ambience, AudioClip fire, FootSurface surface, float ambienceVolume)
        {
            LevelSound level = bank.levels.FirstOrDefault(l => l.scene == scene);
            if (level == null)
            {
                level = new LevelSound { scene = scene, surface = surface, ambienceVolume = ambienceVolume };
                bank.levels.Add(level);
            }

            level.explore = explore;
            level.combat = combat;
            level.ambience = ambience;
            level.fire = fire;
        }

        private static AudioClip[] Clips(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
            {
                return new AudioClip[0];
            }

            return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();
        }

        // Effects: decompressed on load (short, played often, no hitch), mono (they're placed in the world).
        // Music and ambience: streamed (long; only a couple play at once).
        private static void SetImportSettings(List<string> log)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/SFX", Root + "/Music/Levels", Root + "/Ambience" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    continue;
                }

                bool effect = path.StartsWith(Root + "/SFX/");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                AudioClipLoadType load = effect ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
                float quality = effect ? 0.6f : 0.7f;
                if (importer.forceToMono == effect && settings.loadType == load && Mathf.Approximately(settings.quality, quality) && importer.loadInBackground == !effect)
                {
                    continue;
                }

                importer.forceToMono = effect;
                importer.loadInBackground = !effect;
                settings.loadType = load;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = quality;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                changed++;
            }

            if (changed > 0)
            {
                log.Add("import settings set on " + changed + " clips");
            }
        }

        // An Ambience group under Master, so the pause menu muffles the beds (PauseAudio) without pausing them and they
        // don't follow the music slider. Unity has no public API for a mixer's groups, hence the reflection; if it
        // fails the game routes ambience to Music instead (Sfx.Group).
        private static string AddAmbienceGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                return "no mixer at " + MixerPath;
            }

            if (mixer.FindMatchingGroups("Ambience").Length > 0)
            {
                return "";
            }

            try
            {
                Type controller = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
                object master = controller.GetProperty("masterGroup").GetValue(mixer);
                object group = controller.GetMethod("CreateNewGroup", new[] { typeof(string), typeof(bool) }).Invoke(mixer, new object[] { "Ambience", false });
                controller.GetMethod("AddChildToParent").Invoke(mixer, new[] { group, master });
                EditorUtility.SetDirty(mixer);
                return "added the Ambience mixer group";
            }
            catch (Exception e)
            {
                return "couldn't add an Ambience mixer group (" + e.GetType().Name + "); ambience plays on Music";
            }
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
