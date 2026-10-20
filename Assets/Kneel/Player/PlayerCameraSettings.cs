using UnityEngine;

// Tuning values for PlayerCamera. Kept in an asset so edits made during Play mode are saved.
[CreateAssetMenu(fileName = "PlayerCameraSettings", menuName = "Kneel/Player Camera Settings")]
public class PlayerCameraSettings : ScriptableObject
{
    [Header("Target Info")]
    // Point on the player the camera looks at, relative to their feet.
    public float targetHeight = 1f;

    public float followSmoothTime = 0.1f;

    [Header("Angle Info")]
    [Range(30f, 90f)]
    public float pitch = 65f;

    public float yaw = 0f;

    [Header("Zoom Info")]
    public float minDistance = 8f;

    public float maxDistance = 25f;

    public float startDistance = 15f;

    // Distance changed per scroll notch.
    public float zoomStep = 2f;

    public float zoomSmoothTime = 0.1f;

    private void OnValidate()
    {
        minDistance = Mathf.Max(1f, minDistance);
        maxDistance = Mathf.Max(maxDistance, minDistance);
        startDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
    }
}
