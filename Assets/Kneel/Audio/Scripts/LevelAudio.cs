using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.Audio
{
    // The level's music and ambience, from its LevelSound in the SoundBank: the level's score (intro once, then its
    // loop), or without one an exploration bed and combat drums that fade in while any enemy is hunting the player
    // (the ten-pager: "Combat music fades in on aggression"); an ambience bed, and a crackle at the level's fires. One object for the whole game, following the active scene, so a level
    // preloaded for a transition never doubles the music. The boss hall's MusicDirector takes over while it plays.
    public class LevelAudio : MonoBehaviour
    {
        // How far an enemy can be and still count as a fight.
        private const float FightRange = 40f;
        // Seconds the drums hold after the last enemy stops hunting.
        private const float CombatHold = 4f;
        private const float FadeIn = 1.5f;
        private const float FadeOut = 4f;
        private const float BedFade = 3f;
        // Seconds for the score to fade out (leaving the level, or giving way to the boss's score) and back in.
        private const float ScoreFadeOut = 2.5f;
        private const float ScoreFadeIn = 3f;
        private const int MaxFires = 40;
        private const float FireSpacing = 6f;

        public static LevelAudio Instance { get; private set; }

        // What the player's feet sound like in the active level.
        public static FootSurface Surface => Instance != null && Instance.level != null ? Instance.level.surface : FootSurface.Stone;

        public bool InCombat { get; private set; }

        private AudioSource explore;
        private AudioSource combat;
        private AudioSource ambience;
        private ScoreVoice score;
        private LevelSound level;
        private string sceneName;
        private float lastFight = -100f;
        private float nextScan;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private MusicDirector director;
        private bool findDirector;
        private PlayerCombat playerCombat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null || SoundBank.Current == null)
            {
                return;
            }

            var go = new GameObject("LevelAudio");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelAudio>();
        }

        private void Awake()
        {
            Instance = this;
            explore = MakeSource("Explore", SoundGroup.Music);
            combat = MakeSource("Combat", SoundGroup.Music);
            ambience = MakeSource("Ambience", SoundGroup.Ambience);
            score = new ScoreVoice(transform, "Score", Sfx.Group(SoundGroup.Music));
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                AddFires(SceneManager.GetSceneAt(i));
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private AudioSource MakeSource(string name, SoundGroup group)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = Sfx.Group(group);
            return source;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AddFires(scene);
            // A reload of the same level (a death) doesn't change the scene's name, but it does replace the boss
            // hall's MusicDirector: find the new one and give it its score.
            findDirector = true;
        }

        private void Update()
        {
            // Not while SceneLoader is loading: a load that discards a waiting preload lets that next level through (and
            // active) for a moment first, which would start its music, and restart the reloaded level's.
            Scene active = SceneManager.GetActiveScene();
            if (active.name != sceneName && Kneel.UI.SceneLoader.IsLoading == false)
            {
                sceneName = active.name;
                EnterLevel(SoundBank.Current != null ? SoundBank.Current.ForScene(sceneName) : null);
            }

            if (findDirector)
            {
                FindDirector();
            }

            UpdateFight();

            bool bossScore = director != null && director.Current != MusicDirector.Slot.None;
            bool hasScore = level != null && level.score.IsSet;
            float exploreTarget = level == null || bossScore || hasScore ? 0f : (InCombat ? level.exploreVolume * 0.4f : level.exploreVolume);
            float combatTarget = level == null || bossScore || hasScore || InCombat == false ? 0f : level.combatVolume;
            float ambienceTarget = level != null ? level.ambienceVolume : 0f;

            Fade(explore, exploreTarget, InCombat ? FadeIn : FadeOut);
            Fade(combat, combatTarget, InCombat ? FadeIn : FadeOut);
            Fade(ambience, ambienceTarget, BedFade);
            UpdateScore(hasScore && bossScore == false ? level.scoreVolume : 0f);
        }

        private void UpdateScore(float target)
        {
            score.Tick();
            if (score.Track == null)
            {
                return;
            }

            float full = Mathf.Max(0.01f, level != null ? level.scoreVolume : score.Volume);
            float seconds = target > score.Volume ? ScoreFadeIn : ScoreFadeOut;
            score.Volume = Mathf.MoveTowards(score.Volume, target, full * Time.unscaledDeltaTime / seconds);

            // Faded right out with nothing to come back for (the level has no score, or a different one): let it go,
            // so coming back starts the score from its beginning.
            bool wanted = level != null && level.score.IsSet && level.score.Same(score.Track);
            if (score.Volume <= 0f && wanted == false)
            {
                score.Stop();
            }
        }

        private void EnterLevel(LevelSound next)
        {
            level = next;
            FindDirector();
            InCombat = false;
            lastFight = -100f;
            enemies.Clear();
            nextScan = 0f;
            if (level == null)
            {
                return;
            }

            bool hasScore = level.score.IsSet;
            Switch(explore, hasScore ? null : Pick(level.explore));
            Switch(combat, hasScore ? null : Pick(level.combat));
            Switch(ambience, level.ambience);

            // A new score, or this one after it faded out, starts from its first note at full volume (the level's
            // arrival fades the Music group in); the same score still sounding carries on, so a death doesn't restart it.
            if (hasScore && (level.score.Same(score.Track) == false || score.IsPlaying == false || score.Volume <= 0f))
            {
                score.Play(level.score);
                score.Volume = level.scoreVolume;
            }
        }

        // The active level's MusicDirector (the boss hall's), with its score from the bank; any phase without one
        // borrows the combat drums.
        private void FindDirector()
        {
            findDirector = false;
            director = FindAnyObjectByType<MusicDirector>();
            if (director == null || level == null)
            {
                return;
            }

            if (level.bossPhase1.IsSet && director.phase1Score.IsSet == false)
            {
                director.phase1Score = level.bossPhase1;
            }

            if (level.bossPhase2.IsSet && director.phase2Score.IsSet == false)
            {
                director.phase2Score = level.bossPhase2;
            }

            if (level.combat.Length > 0)
            {
                if (director.phase1 == null && director.phase1Score.IsSet == false)
                {
                    director.phase1 = level.combat[0];
                }

                if (director.phase2 == null && director.phase2Score.IsSet == false)
                {
                    director.phase2 = level.combat[level.combat.Length - 1];
                }
            }
        }

        private static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        // A different clip starts from a random point, silent, and fades up; the same clip carries on.
        private static void Switch(AudioSource source, AudioClip clip)
        {
            if (source.clip == clip && (clip == null || source.isPlaying))
            {
                return;
            }

            source.Stop();
            source.clip = clip;
            source.volume = 0f;
            if (clip != null)
            {
                source.time = Random.Range(0f, clip.length * 0.8f);
                source.Play();
            }
        }

        private static void Fade(AudioSource source, float target, float seconds)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        }

        // A fight is any living enemy within FightRange that is past idling, patrolling or lying dormant.
        private void UpdateFight()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                enemies.Clear();
                enemies.AddRange(FindObjectsByType<Enemy>(FindObjectsInactive.Exclude));
                playerCombat = FindAnyObjectByType<PlayerCombat>();
            }

            Transform player = playerCombat != null ? playerCombat.transform : null;

            if (player != null)
            {
                foreach (Enemy enemy in enemies)
                {
                    if (enemy != null && enemy.isActiveAndEnabled && IsHunting(enemy) && (enemy.transform.position - player.position).sqrMagnitude < FightRange * FightRange)
                    {
                        lastFight = Time.unscaledTime;
                        break;
                    }
                }
            }

            InCombat = Time.unscaledTime - lastFight < CombatHold;
        }

        private static bool IsHunting(Enemy enemy)
        {
            if (enemy.health != null && enemy.health.Current <= 0f)
            {
                return false;
            }

            EnemyState state = enemy.stateMachine?.currentState;
            if (state == null)
            {
                return false;
            }

            string name = state.GetType().Name;
            return name.StartsWith("Idle") == false && name.StartsWith("Move") == false && name.StartsWith("Dormant") == false && name.StartsWith("Dead") == false;
        }

        // A looping crackle at each fire light in the scene, at least FireSpacing apart.
        private void AddFires(Scene scene)
        {
            LevelSound sound = SoundBank.Current != null ? SoundBank.Current.ForScene(scene.name) : null;
            if (sound == null || sound.fire == null || scene.isLoaded == false)
            {
                return;
            }

            var placed = new List<Vector3>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (placed.Count >= MaxFires || light.type != LightType.Point || IsFire(light.transform) == false || light.GetComponentInParent<PlayerCombat>() != null)
                    {
                        continue;
                    }

                    Vector3 at = light.transform.position;
                    bool crowded = false;
                    foreach (Vector3 other in placed)
                    {
                        if ((other - at).sqrMagnitude < FireSpacing * FireSpacing)
                        {
                            crowded = true;
                            break;
                        }
                    }

                    if (crowded)
                    {
                        continue;
                    }

                    placed.Add(at);
                    var emitter = new GameObject("FireSound");
                    emitter.transform.SetParent(light.transform, false);
                    var source = emitter.AddComponent<AudioSource>();
                    source.clip = sound.fire;
                    source.loop = true;
                    source.spatialBlend = 1f;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.minDistance = 1.5f;
                    source.maxDistance = 14f;
                    source.dopplerLevel = 0f;
                    source.volume = sound.fireVolume;
                    source.outputAudioMixerGroup = Sfx.Group(SoundGroup.Ambience);
                    // A fire that's switched off for now (one that lights later) starts when it's switched on.
                    source.playOnAwake = true;
                    if (emitter.activeInHierarchy)
                    {
                        source.time = Random.Range(0f, sound.fire.length * 0.9f);
                        source.Play();
                    }
                }
            }
        }

        private static bool IsFire(Transform t)
        {
            for (int i = 0; i < 3 && t != null; i++, t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("fire") || n.Contains("flame") || n.Contains("torch") || n.Contains("brazier") || n.Contains("burning") || n.Contains("ember"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
