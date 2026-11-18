using UnityEngine;

// Moves the sword and shield between their carry sockets (scabbard, back) and hand sockets.
// Sockets define the exact pose of the item, so items sit at identity inside them. A move can settle: the item
// changes socket where it is and eases into the socket's pose over the given seconds (the draw and sheathe, with
// PlayerDrawPose taking the hands to the items, so nothing jumps between them).
public class PlayerEquipment : MonoBehaviour
{
    [Header("Items")]
    [SerializeField]
    private Transform sword;

    [SerializeField]
    private Transform shield;

    [Header("Sockets")]
    [SerializeField]
    private Transform swordHandSocket;

    [SerializeField]
    private Transform swordSheathSocket;

    [SerializeField]
    private Transform shieldHandSocket;

    [SerializeField]
    private Transform shieldBackSocket;

    public WeaponHitbox SwordHitbox { get; private set; }

    public bool SwordInHand { get; private set; }

    public bool ShieldInHand { get; private set; }

    public Transform Shield => shield;

    public Transform ShieldHandSocket => shieldHandSocket;

    public Renderer ShieldRenderer { get; private set; }

    public Transform SwordSheathSocket => swordSheathSocket;

    public Transform SwordHandSocket => swordHandSocket;

    public Transform ShieldBackSocket => shieldBackSocket;

    private struct Settle
    {
        public Vector3 fromPosition;
        public Quaternion fromRotation;
        public float duration;
        public float time;
    }

    private Settle swordSettle;
    private Settle shieldSettle;

    private void Awake()
    {
        SwordHitbox = sword.GetComponent<WeaponHitbox>();
        ShieldRenderer = shield.GetComponentInChildren<Renderer>();
    }

    public void SetSwordInHand(bool inHand, float settleTime = 0f)
    {
        SwordInHand = inHand;
        Attach(sword, inHand ? swordHandSocket : swordSheathSocket, settleTime, ref swordSettle);
    }

    public void SetShieldInHand(bool inHand, float settleTime = 0f)
    {
        ShieldInHand = inHand;
        Attach(shield, inHand ? shieldHandSocket : shieldBackSocket, settleTime, ref shieldSettle);
    }

    // Out of both sockets, for PlayerDrawPose to carry along its own path (over the shoulder and onto the back).
    public void HoldShieldFree(Transform parent)
    {
        ShieldInHand = false;
        shieldSettle.duration = 0f;
        shield.SetParent(parent, true);
    }

    private static void Attach(Transform item, Transform socket, float settleTime, ref Settle settle)
    {
        settle.duration = 0f;

        if (settleTime > 0f && item.parent != null)
        {
            // Keep the world pose for now; LateUpdate eases it into the socket's.
            item.SetParent(socket, true);
            item.localScale = Vector3.one;
            settle.fromPosition = item.localPosition;
            settle.fromRotation = item.localRotation;
            settle.duration = settleTime;
            settle.time = 0f;
            return;
        }

        item.SetParent(socket, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        item.localScale = Vector3.one;
    }

    // After the animation and IK have posed the hands, so the item rides the hand as it eases in.
    private void LateUpdate()
    {
        Step(sword, ref swordSettle);
        Step(shield, ref shieldSettle);
    }

    private static void Step(Transform item, ref Settle settle)
    {
        if (settle.duration <= 0f)
        {
            return;
        }

        settle.time += Time.deltaTime;
        float k = Mathf.SmoothStep(0f, 1f, settle.time / settle.duration);
        item.localPosition = Vector3.Lerp(settle.fromPosition, Vector3.zero, k);
        item.localRotation = Quaternion.Slerp(settle.fromRotation, Quaternion.identity, k);

        if (k >= 1f)
        {
            settle.duration = 0f;
        }
    }
}
