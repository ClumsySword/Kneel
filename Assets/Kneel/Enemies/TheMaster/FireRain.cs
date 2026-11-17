using System.Collections.Generic;
using UnityEngine;

// The Gather's fire rain (owner, 2026-11-15: "fire flies down on the arena randomly for about 10 seconds; in this phase
// the player should only focus on dodging"). While he kneels, strikes of fire fall on the arena: each marked first by an
// ember ring (rainTell), the fire falling over its last 0.3 s, then the Command's bolt and cracks. They come quicker as
// it goes on; every rainAtPlayer-th lands where Kael stands as it's marked. Inside a ring: rainDamage (unblockable;
// a roll's i-frames get through). None land on him or off the arena.
public class FireRain : MonoBehaviour
{
    private class Drop
    {
        public EmberRing ring;
        public Vector3 at;
        public float lands;
        public bool falling;
    }

    // The arena (§8): x 40.5-67.5, z 432-466.5.
    private static readonly Rect Arena = new Rect(41.5f, 433f, 25f, 32.5f);
    private const float FallTime = 0.3f;

    private MasterBody owner;
    private MasterSettings S;
    private float started;
    private float until;
    private float next;
    private int count;
    private readonly List<Drop> drops = new List<Drop>();

    // How many strikes have landed, and how many hit Kael (the playtest reads them).
    public int Landed { get; private set; }
    public int Hits { get; private set; }

    public static FireRain Begin(MasterBody owner, float seconds)
    {
        var go = new GameObject("FireRain");
        var rain = go.AddComponent<FireRain>();
        rain.owner = owner;
        rain.S = owner.settings;
        rain.started = Time.time;
        rain.until = Time.time + seconds;
        rain.next = Time.time + 0.2f;
        return rain;
    }

    // Stops marking new strikes; the ones already marked still land.
    public void Stop() => until = Mathf.Min(until, Time.time);

    private void Update()
    {
        float now = Time.time;
        if (now < until && now >= next)
        {
            Mark();
            float t = Mathf.Clamp01((now - started) / Mathf.Max(until - started, 0.1f));
            next = now + Mathf.Lerp(S.rainIntervalStart, S.rainIntervalEnd, t) * Random.Range(0.8f, 1.2f);
        }

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Drop d = drops[i];
            if (d.falling == false && now >= d.lands - FallTime)
            {
                d.falling = true;
                d.ring.Lock();
                FireStrike.Begin(S, d.at, S.rainRadius, FallTime);
            }

            if (now >= d.lands)
            {
                Land(d);
                drops.RemoveAt(i);
            }
        }

        if (now >= until && drops.Count == 0)
            Destroy(gameObject);
    }

    private void Mark()
    {
        count++;
        Vector3 at;
        if (S.rainAtPlayer > 0 && count % S.rainAtPlayer == 0 && owner.player != null)
            at = owner.player.position;
        else
            at = RandomPoint();
        drops.Add(new Drop { at = at, lands = Time.time + S.rainTell, ring = EmberRing.Spawn(S.emberMaterial, at, owner.transform.position, S.rainRadius, S.rainTell) });
    }

    // Somewhere on the arena floor, clear of him and of the strikes already marked.
    private Vector3 RandomPoint()
    {
        Vector3 best = owner.transform.position + Vector3.forward * 6f;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var p = new Vector3(Random.Range(Arena.xMin, Arena.xMax), owner.transform.position.y, Random.Range(Arena.yMin, Arena.yMax));
            if (HallActors.FlatDistance(p, owner.transform.position) < 4.5f)
                continue;
            bool clear = true;
            foreach (Drop d in drops)
                if (HallActors.FlatDistance(p, d.at) < S.rainRadius * 2.2f)
                    clear = false;
            best = p;
            if (clear)
                break;
        }

        return best;
    }

    private void Land(Drop d)
    {
        Landed++;
        d.ring.Release();
        if (owner.player != null && HallActors.FlatDistance(owner.player.position, d.at) <= S.rainRadius + 0.3f)
        {
            Hits++;
            owner.Deal(S.rainDamage, 0f, true, true, 0f, 0f, owner.player.position - d.at);
        }
    }

    private void OnDestroy()
    {
        foreach (Drop d in drops)
            if (d.ring != null)
                d.ring.Release();
    }
}
