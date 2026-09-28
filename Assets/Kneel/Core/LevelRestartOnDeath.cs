using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reloads the current scene a moment after the player dies, so every enemy and pickup resets.
public class LevelRestartOnDeath : MonoBehaviour
{
    // Real seconds between the killing blow and the reload, long enough to see the death animation.
    [SerializeField]
    private float restartDelay = 2.5f;

    private PlayerCombat player;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerCombat>();
        if (player != null)
        {
            player.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnDied -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied()
    {
        StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        // Realtime: the killing blow usually triggers hit-stop, which slows scaled time.
        yield return new WaitForSecondsRealtime(restartDelay);

        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogWarning($"Can't restart '{scene.name}': add it to File > Build Profiles (scene list) first.", this);
            yield break;
        }

        SceneManager.LoadScene(scene.buildIndex);
    }
}
