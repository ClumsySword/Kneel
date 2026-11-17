using UnityEngine;

// The enemies the F1 overlay can place (DebugTuningOverlay's Enemies section). It lives in Resources so development
// builds can load it as well as the editor. Add a new enemy type's prefab here to make it placeable.
[CreateAssetMenu(fileName = "EnemySpawnRoster", menuName = "Kneel/Enemy Spawn Roster")]
public class EnemySpawnRoster : ScriptableObject
{
    public const string ResourcePath = "EnemySpawnRoster";

    public Enemy[] prefabs = new Enemy[0];

    public static EnemySpawnRoster Load() => Resources.Load<EnemySpawnRoster>(ResourcePath);
}
