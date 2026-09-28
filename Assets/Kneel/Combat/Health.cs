using System;
using UnityEngine;

// Hit points. With regenerateToFull on, health refills after a quiet period instead of dying,
// which suits training targets (and the player until death is designed).
public class Health : MonoBehaviour
{
    [SerializeField]
    private float maxHealth = 100f;

    [SerializeField]
    private bool regenerateToFull = true;

    // Seconds without taking damage before health refills.
    [SerializeField]
    private float regenDelay = 3f;

    private float lastDamageTime = -999f;

    public float Current { get; private set; }

    public float Max => maxHealth;

    public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;

    // Amount of damage applied.
    public event Action<float> OnDamaged;

    public event Action OnDepleted;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void SetMax(float value, bool refill)
    {
        maxHealth = Mathf.Max(1f, value);
        Current = refill ? maxHealth : Mathf.Min(Current, maxHealth);
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Current = Mathf.Max(0f, Current - amount);
        lastDamageTime = Time.time;

        OnDamaged?.Invoke(amount);

        if (Current <= 0f)
        {
            OnDepleted?.Invoke();
        }
    }

    private void Update()
    {
        if (regenerateToFull == true && Current < maxHealth && Time.time - lastDamageTime > regenDelay)
        {
            Current = maxHealth;
        }
    }
}
