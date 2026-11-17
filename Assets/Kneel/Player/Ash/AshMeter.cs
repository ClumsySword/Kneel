using System;
using System.Collections.Generic;
using Kneel.Core;
using UnityEngine;

// The Ash meter: the Master's power, stolen down the channel. Kills release most of it, parries and rolls through
// attacks skim a little, blocking gives nothing. It holds a full meter at most, every ability spends all of it, and
// it empties on death. The value lives in AshProgress so it carries from level to level. Granted in Level 2.
[RequireComponent(typeof(PlayerCombat))]
public class AshMeter : MonoBehaviour
{
    public enum Source
    {
        Kill,
        Parry,
        Evade,
        Debug,
    }

    [SerializeField]
    private AshSettings settings;

    private PlayerCombat combat;
    private readonly Dictionary<GameObject, float> lastEvadeTimes = new Dictionary<GameObject, float>();

    // The F1 overlay's switch: the target dummy pays out like a kill when its health runs out.
    public static bool DummyGivesAsh { get; set; }

    public AshSettings Settings => settings;

    public float Ash => AshProgress.Ash;

    public float Max => settings.maxAsh;

    public float Fraction => settings.maxAsh > 0f ? Mathf.Clamp01(AshProgress.Ash / settings.maxAsh) : 0f;

    public bool IsFull => AshProgress.Ash >= settings.maxAsh;

    public bool Granted => AshProgress.MeterGranted;

    // Ash taken, from where in the world (the presenter flies motes from there to the hand).
    public event Action<Source, float, Vector3> Gained;

    public event Action BecameFull;

    // Spent on a cast, or lost on death.
    public event Action Spent;

    public event Action Emptied;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();

        if (settings == null)
        {
            Debug.LogWarning("AshMeter has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<AshSettings>();
        }
    }

    private void OnEnable()
    {
        combat.Parried += OnParried;
        combat.Evaded += OnEvaded;
        combat.OnDied += OnDied;
        Enemy.Killed += OnEnemyKilled;
        TargetDummy.Felled += OnDummyFelled;
    }

    private void OnDisable()
    {
        combat.Parried -= OnParried;
        combat.Evaded -= OnEvaded;
        combat.OnDied -= OnDied;
        Enemy.Killed -= OnEnemyKilled;
        TargetDummy.Felled -= OnDummyFelled;
    }

    public void Add(float amount, Source source, Vector3 origin)
    {
        if (amount <= 0f || Granted == false || combat.IsDead || IsFull)
        {
            return;
        }

        AshProgress.Ash = Mathf.Min(settings.maxAsh, AshProgress.Ash + amount);
        Gained?.Invoke(source, amount, origin);

        if (IsFull)
        {
            BecameFull?.Invoke();
        }
    }

    // Empties a full meter for a cast. Fails, changing nothing, if it isn't full.
    public bool TrySpendAll()
    {
        if (IsFull == false)
        {
            return false;
        }

        AshProgress.EmptyMeter();
        Spent?.Invoke();
        return true;
    }

    // The F1 overlay's slider.
    public void SetAsh(float value)
    {
        bool wasFull = IsFull;
        AshProgress.Ash = Mathf.Clamp(value, 0f, settings.maxAsh);

        if (wasFull == false && IsFull)
        {
            BecameFull?.Invoke();
        }
    }

    private void OnParried(GameObject attacker)
    {
        Vector3 origin = attacker != null ? attacker.transform.position + Vector3.up * 1.2f : transform.position;
        Add(settings.parryGain, Source.Parry, origin);
    }

    private void OnEvaded(GameObject source)
    {
        if (source == null)
        {
            return;
        }

        if (lastEvadeTimes.TryGetValue(source, out float last) && Time.time - last < settings.evadeDedupWindow)
        {
            return;
        }

        lastEvadeTimes[source] = Time.time;
        Add(settings.evadeGain, Source.Evade, source.transform.position);
    }

    // Any thrall that dies while engaged with the player pays: the binding collapses whatever cut it.
    private void OnEnemyKilled(Enemy enemy)
    {
        Vector3 toPlayer = transform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > enemy.aggressionRange + settings.engagedMargin)
        {
            return;
        }

        Add(KillYield(enemy), Source.Kill, enemy.transform.position + Vector3.up * 1.2f);
    }

    private float KillYield(Enemy enemy)
    {
        switch (enemy)
        {
            case HollowArcher _:
                return settings.archerKill;
            case BlightedBrute _:
                return settings.bruteKill;
            case TheFool _:
                return settings.foolKill;
            default:
                return settings.footmanKill;
        }
    }

    private void OnDummyFelled(TargetDummy dummy)
    {
        if (DummyGivesAsh)
        {
            Add(settings.dummyKill, Source.Kill, dummy.transform.position + Vector3.up * 1.2f);
        }
    }

    private void OnDied()
    {
        if (AshProgress.Ash <= 0f)
        {
            return;
        }

        AshProgress.EmptyMeter();
        Emptied?.Invoke();
    }
}
