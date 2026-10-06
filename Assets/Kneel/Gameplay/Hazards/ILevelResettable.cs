namespace Kneel.Hazards
{
    // Anything that returns to its starting state when the player dies or rests
    // (crop rows, brand stakes, gibbets, encounters).
    public interface ILevelResettable
    {
        void ResetState();
    }
}
