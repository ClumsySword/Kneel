using Kneel;
using UnityEngine;

// The healing potions the player carries: up to one per belt slot, shown as bottles hanging on the front of
// the belt. Those bottles are the only way to see how many are left. Interact (E) picks up the nearest potion
// in reach; PlayerCombat drinks them (F) and shows a bottle in the drinking hand while it does.
public class PlayerPotions : MonoBehaviour
{
    private PlayerControls controls;

    // The potion model. Copies are made at runtime for the belt and the hand, with their colliders removed.
    [SerializeField]
    private GameObject bottlePrefab;

    // One per slot, filled left to right.
    [SerializeField]
    private Transform[] beltSockets = new Transform[0];

    [SerializeField]
    private Transform handSocket;

    // World scale of the bottles (the model is a 20 x 28 cm bottle).
    [SerializeField]
    private float beltBottleScale = 0.45f;

    [SerializeField]
    private float handBottleScale = 0.65f;

    [SerializeField]
    private int startCount = 0;

    private GameObject[] beltBottles;
    private GameObject handBottle;
    private PlayerCombat combat;

    public int Count { get; private set; }

    public int Capacity => beltSockets.Length;

    public Transform HandSocket => handSocket;

    // The neck of the bottle in hand (the top of the model), in world space.
    public Vector3 HandBottleNeck => handBottle != null ? handBottle.transform.TransformPoint(bottleNeckLocal) : handSocket.position;

    private Vector3 bottleNeckLocal;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Character.Interact.performed += ctx => TryPickUpNearest();
        combat = GetComponent<PlayerCombat>();

        // The bottles render like the body, so the level's character lighting overrides apply to them.
        var body = GetComponentInChildren<SkinnedMeshRenderer>();
        uint renderingLayers = body != null ? body.renderingLayerMask : 1u;

        beltBottles = new GameObject[beltSockets.Length];
        for (int i = 0; i < beltSockets.Length; i++)
        {
            beltBottles[i] = MakeBottle(beltSockets[i], beltBottleScale, renderingLayers);
        }

        handBottle = handSocket != null ? MakeBottle(handSocket, handBottleScale, renderingLayers) : null;

        Count = Mathf.Clamp(startCount, 0, Capacity);
        Refresh();
    }

    private GameObject MakeBottle(Transform socket, float worldScale, uint renderingLayers)
    {
        GameObject bottle = Instantiate(bottlePrefab, socket, false);
        MeshFilter mesh = bottle.GetComponentInChildren<MeshFilter>();
        if (mesh != null)
        {
            Bounds b = mesh.sharedMesh.bounds;
            bottleNeckLocal = bottle.transform.InverseTransformPoint(mesh.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)));
        }

        bottle.name = "Bottle";
        bottle.transform.localPosition = Vector3.zero;
        bottle.transform.localRotation = Quaternion.identity;

        // The sockets sit under scaled bones; cancel that so the bottle ends up at worldScale.
        Vector3 parentScale = socket.lossyScale;
        bottle.transform.localScale = new Vector3(worldScale / parentScale.x, worldScale / parentScale.y, worldScale / parentScale.z);

        foreach (Collider c in bottle.GetComponentsInChildren<Collider>())
        {
            Destroy(c);
        }

        foreach (Renderer r in bottle.GetComponentsInChildren<Renderer>())
        {
            r.renderingLayerMask = renderingLayers;
        }

        bottle.SetActive(false);
        return bottle;
    }

    public bool TryAdd()
    {
        if (Count >= Capacity)
        {
            return false;
        }

        Count++;
        Refresh();
        return true;
    }

    // Takes the last-filled bottle off the belt.
    public bool TryTake()
    {
        if (Count <= 0)
        {
            return false;
        }

        Count--;
        Refresh();
        return true;
    }

    public void ShowInHand(bool show)
    {
        if (handBottle != null)
        {
            handBottle.SetActive(show);
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < beltBottles.Length; i++)
        {
            beltBottles[i].SetActive(i < Count);
        }
    }

    // Public so scripted tests can call it without input.
    public bool TryPickUpNearest()
    {
        if (combat != null && combat.IsDead == true)
        {
            return false;
        }

        HealthPotionPickup nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (HealthPotionPickup pickup in HealthPotionPickup.All)
        {
            float distance = pickup.DistanceTo(transform.position);
            if (distance <= pickup.Reach && distance < nearestDistance)
            {
                nearest = pickup;
                nearestDistance = distance;
            }
        }

        return nearest != null && nearest.TryPickUp(this);
    }

    private void OnEnable()
    {
        controls.Character.Interact.Enable();
    }

    private void OnDisable()
    {
        controls.Character.Interact.Disable();
    }
}
