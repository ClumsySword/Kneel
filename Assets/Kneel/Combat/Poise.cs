using System;
using UnityEngine;

// Balance. Hits wear it down and it regenerates after a quiet period; at zero the owner is staggered.
// It is a defensive resource only: nothing spends it to act. A plain class owned by each actor (player
// and enemies), which calls Tick from its Update.
[Serializable]
public class Poise
{
    public float maxPoise = 30f;

    // Seconds without taking poise damage before it starts regenerating.
    public float regenDelay = 2f;

    // Poise regained per second once regenerating.
    public float regenRate = 20f;

    private float regenTimer = 0f;

    public float Current { get; private set; }

    public float Fraction => maxPoise > 0f ? Current / maxPoise : 0f;

    public Poise() { }

    public Poise(float maxPoise, float regenDelay, float regenRate)
    {
        this.maxPoise = maxPoise;
        this.regenDelay = regenDelay;
        this.regenRate = regenRate;
    }

    public void Reset()
    {
        Current = maxPoise;
        regenTimer = 0f;
    }

    // Returns true when this breaks the poise. It then refills, so one stagger can't chain into the next.
    public bool ApplyDamage(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        Current -= amount;
        regenTimer = regenDelay;

        if (Current <= 0f)
        {
            Reset();
            return true;
        }

        return false;
    }

    public void Tick(float deltaTime)
    {
        // Clamped every frame so max edits made while playing apply immediately.
        Current = Mathf.Min(Current, maxPoise);

        if (regenTimer > 0f)
        {
            regenTimer -= deltaTime;
            return;
        }

        Current = Mathf.Min(Current + regenRate * deltaTime, maxPoise);
    }
}
