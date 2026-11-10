using UnityEngine;

// The ash storm (The Master.md §6, §8; P2 below 50%): ash streams in from the side walls, and the outer band, 2 R
// (6 m) from each wall, does 10/s and slows the player 30%, pushing the fight toward the burning, broken centre.
// During a Gather the band widens to 3 R at 15/s, then shrinks back over 2 s. It hurts him too (10/s), but doesn't
// slow him.
public class AshStorm : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public float wallWest = 33f;
    public float wallEast = 75f;
    public float zMin = 432f;
    public float zMax = 472f;
    public float band = 6f;
    public float gatherBand = 9f;
    public float damagePerSecond = 10f;
    public float gatherDamagePerSecond = 15f;
    public float slow = 0.7f;
    public float tickInterval = 0.5f;
    public ParticleSystem west;
    public ParticleSystem east;

    public bool Running { get; private set; }

    // The band's current width (m) and damage per second.
    public float Width { get; private set; }
    public float Rate { get; private set; }

    private float gatherUntil = -1f;
    private float gatherEnd = -1f;
    private float nextPlayerTick;
    private float nextBossTick;
    private float baseRate = -1f;

    public void Begin()
    {
        Running = true;
        Width = 0f;
        Play(west, true);
        Play(east, true);
    }

    public void Stop()
    {
        Running = false;
        Play(west, false);
        Play(east, false);
    }

    // A Gather thickens it for `seconds`, then it shrinks back over 2 s.
    public void Thicken(float seconds)
    {
        gatherUntil = Time.time + seconds;
        gatherEnd = gatherUntil + 2f;
    }

    public bool InBand(Vector3 p)
    {
        if (Running == false || Width <= 0.01f || p.z < zMin || p.z > zMax)
            return false;
        return p.x < wallWest + Width || p.x > wallEast - Width;
    }

    private void Update()
    {
        if (Running == false)
            return;

        float now = Time.time;
        float target = band;
        float rate = damagePerSecond;
        if (now < gatherUntil)
        {
            target = gatherBand;
            rate = gatherDamagePerSecond;
        }
        else if (now < gatherEnd)
        {
            float k = (now - gatherUntil) / 2f;
            target = Mathf.Lerp(gatherBand, band, k);
            rate = Mathf.Lerp(gatherDamagePerSecond, damagePerSecond, k);
        }

        // It streams in rather than appearing.
        Width = Mathf.MoveTowards(Width, target, Time.deltaTime * (now < gatherUntil ? 6f : 3f));
        Rate = rate;
        Shape(west, wallWest + Width * 0.5f);
        Shape(east, wallEast - Width * 0.5f);

        PlayerCombat player = HallActors.Player;
        if (player != null && InBand(player.transform.position))
        {
            player.Slow(slow, 0.15f);
            if (now >= nextPlayerTick)
            {
                nextPlayerTick = now + tickInterval;
                HallActors.BurnPlayer(rate * tickInterval, gameObject);
            }
        }

        if (now >= nextBossTick)
        {
            nextBossTick = now + tickInterval;
            foreach (IHallBoss boss in HallActors.Bosses)
            {
                if (boss != null && (boss as Object) != null && boss.IsDead == false && InBand(boss.transform.position))
                    boss.HazardHit(rate * tickInterval, 0f);
            }
        }
    }

    private void Shape(ParticleSystem system, float x)
    {
        if (system == null)
            return;
        Vector3 p = system.transform.position;
        system.transform.position = new Vector3(x, p.y, (zMin + zMax) * 0.5f);
        ParticleSystem.ShapeModule shape = system.shape;
        shape.scale = new Vector3(Mathf.Max(Width, 0.1f), zMax - zMin, 4f);
        ParticleSystem.EmissionModule emission = system.emission;
        if (baseRate < 0f)
            baseRate = emission.rateOverTimeMultiplier;
        emission.rateOverTimeMultiplier = baseRate * Mathf.Clamp01(Width / band) * (Width > band ? Width / band : 1f);
    }

    private static void Play(ParticleSystem system, bool on)
    {
        if (system == null)
            return;
        if (on)
            system.Play(true);
        else
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void ResetState()
    {
        Stop();
        Width = 0f;
        gatherUntil = gatherEnd = -1f;
    }
}
