using UnityEngine;

// Moves the sword and shield between their carry sockets (scabbard, back) and hand sockets.
// Sockets define the exact pose of the item, so items sit at identity inside them.
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

    private void Awake()
    {
        SwordHitbox = sword.GetComponent<WeaponHitbox>();
    }

    public void SetSwordInHand(bool inHand)
    {
        SwordInHand = inHand;
        Attach(sword, inHand ? swordHandSocket : swordSheathSocket);
    }

    public void SetShieldInHand(bool inHand)
    {
        ShieldInHand = inHand;
        Attach(shield, inHand ? shieldHandSocket : shieldBackSocket);
    }

    private static void Attach(Transform item, Transform socket)
    {
        item.SetParent(socket, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        item.localScale = Vector3.one;
    }
}
