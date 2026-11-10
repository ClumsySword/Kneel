using System;
using System.Collections.Generic;
using UnityEngine;

// The throne hall's director (The Master.md §8, BossArenaDirector): it ties the hall to the fight and owns the
// pacing rule, so the player has one impact to read at a time.
// - Scripted hazards (panel F1 at P1's 50% beat now; the statues, the darkness and P2's panels in Stage 6) are held
//   while their impact would land within 0.5 s of one of his blows.
// - Levers the player sets off (winches, braziers) fire on time; instead he won't start a blow that would land
//   within 0.5 s of one (MasterP1.HazardImpactNear).
// - His Threads snap a chandelier's chain if they cross its winch; the statues bow at every Command.
public class BossArenaDirector : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public MasterBoss boss;
    public HallFloor floor;
    public FloorFire fire;
    public DarknessWave darkness;
    public AshStorm ashStorm;
    public float spacing = 0.5f;
    public float threadCutReach = 0.8f;

    private struct Pending
    {
        public float lead;
        public Action begin;
        public string name;
    }

    private readonly List<Pending> scripted = new List<Pending>();

    // How many times a scripted hazard was held back for a blow (the playtest reads it).
    public int Held { get; private set; }

    private void Start()
    {
        MasterP1 master = boss != null ? boss.master : null;
        MasterP1.HazardImpactNear = ImpactNear;
        if (master != null)
        {
            master.pommelState.OnPommelStrike += () => Schedule("panel F1", floor != null ? floor.tell : 2f, () => floor?.Crack(floor.f1));
            master.OnCommandTell += tell =>
            {
                foreach (HallStatue statue in HallStatue.All)
                    statue.Bow(tell);
            };
        }

        ThreadLine.OnSwept += OnThreadSwept;
    }

    private void OnDestroy()
    {
        ThreadLine.OnSwept -= OnThreadSwept;
        if (MasterP1.HazardImpactNear == (Func<float, bool>)ImpactNear)
            MasterP1.HazardImpactNear = null;
    }

    // A scripted hazard whose impact comes `lead` seconds after it begins: it begins once that impact is clear of
    // his next blow by `spacing`.
    public void Schedule(string name, float lead, Action begin)
    {
        scripted.Add(new Pending { name = name, lead = lead, begin = begin });
    }

    private void Update()
    {
        for (int i = scripted.Count - 1; i >= 0; i--)
        {
            Pending p = scripted[i];
            float impact = Time.time + p.lead;
            float blow = NextBlow();
            if (blow >= 0f && Mathf.Abs(blow - impact) < spacing)
            {
                Held++;
                continue;
            }

            scripted.RemoveAt(i);
            p.begin?.Invoke();
        }
    }

    // When his next blow lands (game time), or -1.
    public float NextBlow()
    {
        MasterP1 master = boss != null ? boss.master : null;
        if (master != null && master.stateMachine.currentState is MasterMoveState move)
            return move.LandsAt;
        return -1f;
    }

    // True if any hazard lands within `spacing` of time t (his picker asks before he starts a blow).
    public bool ImpactNear(float t)
    {
        foreach (float impact in Impacts())
        {
            if (impact >= 0f && Mathf.Abs(impact - t) < spacing)
                return true;
        }

        return false;
    }

    private IEnumerable<float> Impacts()
    {
        foreach (ChandelierDrop c in ChandelierDrop.All)
            yield return c.ImpactAt;
        foreach (BrazierTopple b in BrazierTopple.All)
            yield return b.ImpactAt;
        foreach (HallStatue s in HallStatue.All)
            yield return s.ImpactAt;
        if (floor != null)
        {
            for (int i = 0; i < floor.panels.Length; i++)
                yield return floor.ImpactAt(i);
        }
    }

    // His Threads: a line crossing a winch snaps its chain.
    private void OnThreadSwept(Vector3 from, Vector3 to, float width)
    {
        foreach (ChandelierDrop chandelier in ChandelierDrop.All)
        {
            if (chandelier.Current != ChandelierDrop.State.Hanging || chandelier.winch == null)
                continue;
            if (HallActors.SegmentDistance(chandelier.winch.transform.position, from, to) <= width * 0.5f + threadCutReach)
                chandelier.Trigger();
        }
    }

    public void ResetState()
    {
        scripted.Clear();
        Held = 0;
    }
}
