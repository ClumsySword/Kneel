namespace Kneel.Hazards
{
    public enum HazardKind
    {
        FireEnter,
        FireTick,
        Smoulder,
        Ditch,
    }

    // What a hazard talks to. Hazards only report contact; the actor decides the consequence
    // (a burning row costs the player a hit and kills a hound).
    public interface IHazardTarget
    {
        void OnHazard(HazardKind kind);

        void SetSurface(float rollDistanceMultiplier, float moveSpeedMultiplier);

        void ClearSurface();
    }
}
