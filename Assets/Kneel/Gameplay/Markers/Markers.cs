namespace Kneel.Markers
{
    public enum PickupKind
    {
        Consumable,
        Lore,
        HealCapacity,
        Tonal,

        // L4 (appended so L3's serialized values keep their meaning)
        HeavyWeapon,
        Key,
        Cordial,
    }

    public enum EnemyType
    {
        Footman,
        Hound,
        Brute,

        // L4 (appended so L3's serialized values keep their meaning)
        IronHusk,
        CursedTwin,
        Crossbowman,
        Fool,
        PourTender,
        Gatekeeper,
        Steward,
    }
}
