// One Ash ability in progress, driven by PlayerCombat's Casting state (PlayerCombat.StartCast). Times are real
// seconds since the cast started.
public abstract class AshCast
{
    // Walking speed allowed meanwhile, as a share of normal, and whether the body may turn.
    public float MoveMultiplier { get; protected set; }

    public bool RotationLocked { get; protected set; } = true;

    public abstract void Begin();

    // Returns false once the cast is over and control goes back.
    public abstract bool Tick(float time);

    // A poise break or death cut it short. The meter is already spent.
    public virtual void Interrupt() { }
}
