using System;
using UnityEngine;
using UnityEngine.AI;

// The arena floor in panels (The Master.md §8, decided 2026-11-10): x 40.5-67.5, y 432-466.5, 5 columns (6/5/5/5/6 m)
// by 6 rows of 5.75 m. A panel that goes cracks first (ember cracks, dust, a rumble: 2.0 s), then drops into the
// burning undercroft 4 m below and leaves a hole with a broken-rubble lip that blocks walking and rolling (holes
// aren't lethal to walk past). Standing on it as it goes, the player drops with it into the lava and dies (owner,
// 2026-11-10: the 2 s of cracks are the warning); he takes 50 + a stagger and is thrown to the edge (P2: he Topples). The centre column never goes, so the arena can't be split, nor do the
// panels under the candelabras; at most 10 of the 30 ever fall. F1 (west band, row y 438) goes at P1's 50% beat;
// any other only to a Hammerfall or two Cleaves (Stage 6). Whole again at a level reset.
public class HallFloor : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public enum PanelState { Whole, Cracking, Fallen }

    [Serializable]
    public class Panel
    {
        public string name;
        public Rect rect;
        public bool neverFalls;
        public Transform slab;
        public Collider top;
        public GameObject crack;
        public GameObject hole;
        public NavMeshObstacle carve;
        public Light holeLight;

        [NonSerialized] public PanelState state;
        [NonSerialized] public float started;
        [NonSerialized] public Vector3 slabRest;
        [NonSerialized] public Quaternion slabRestRotation;
        [NonSerialized] public Vector3 spin;
    }

    public Panel[] panels = new Panel[0];
    public int f1 = -1;
    public int maxFallen = 10;
    public float tell = 2f;
    public float floorY = 9f;
    // The lava's surface in the holes (owner, 2026-11-10), and how many holes keep their light (§8: at most 8 extra).
    public float lavaY = 7.8f;
    public int maxHoleLights = 4;
    public float bossDamage = 50f;
    public float bossStagger = 2.5f;
    public ParticleSystem dust;

    [Header("The burst as a panel opens (owner, 2026-11-10: lava cracks, not oil)")]
    public Material emberMaterial;
    public Material smokeMaterial;
    public Mesh debrisMesh;
    public Material debrisMaterial;
    // The slab is thrown down this fast the instant it goes (m/s), then falls.
    public float burstDrop = 6f;

    public int FallenCount { get; private set; }

    public event Action<int> PanelFell;

    private float nextRumble;

    private void Awake()
    {
        foreach (Panel panel in panels)
        {
            panel.slabRest = panel.slab.position;
            panel.slabRestRotation = panel.slab.rotation;
        }

        ResetState();
    }

    // The panel under a point, or -1 (outside the arena: the aisles, the pillar lines, the dais are solid).
    public int IndexAt(Vector3 point)
    {
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].rect.Contains(new Vector2(point.x, point.z)))
                return i;
        }

        return -1;
    }

    public PanelState StateOf(int index) => panels[index].state;

    public bool CanFall(int index)
    {
        if (index < 0 || index >= panels.Length)
            return false;
        Panel panel = panels[index];
        int pending = 0;
        foreach (Panel p in panels)
        {
            if (p.state == PanelState.Cracking)
                pending++;
        }

        return panel.neverFalls == false && panel.state == PanelState.Whole && FallenCount + pending < maxFallen;
    }

    // When panel `index` drops (game time), while it cracks; -1 otherwise.
    public float ImpactAt(int index) => panels[index].state == PanelState.Cracking ? panels[index].started + tell : -1f;

    // Starts the panel's 2 s tell; false if it can't go.
    public bool Crack(int index)
    {
        if (CanFall(index) == false)
            return false;

        Panel panel = panels[index];
        panel.state = PanelState.Cracking;
        panel.started = Time.time;
        if (panel.crack != null)
        {
            panel.crack.SetActive(true);
            LavaCracks cracks = panel.crack.GetComponent<LavaCracks>();
            if (cracks != null)
            {
                cracks.Reveal = 0f;
                cracks.Intensity = 1.5f;
                cracks.Pulse = 0f;
            }
        }
        if (dust != null)
        {
            Vector2 c = panel.rect.center;
            dust.transform.position = new Vector3(c.x, floorY + 0.1f, c.y);
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.scale = new Vector3(panel.rect.width, panel.rect.height, 0.2f);
            dust.Play(true);
        }

        return true;
    }

    private void Update()
    {
        float now = Time.time;
        PlayerCamera cam = null;
        for (int i = 0; i < panels.Length; i++)
        {
            Panel panel = panels[i];
            if (panel.state == PanelState.Cracking)
            {
                float t = now - panel.started;

                // The rumble: small shakes, quickening toward the drop.
                if (now >= nextRumble)
                {
                    cam = cam != null ? cam : FindAnyObjectByType<PlayerCamera>();
                    cam?.AddShake(0.04f + 0.06f * t / tell, 0.25f);
                    nextRumble = now + Mathf.Lerp(0.5f, 0.2f, t / tell);
                }

                // The cracks run out over the panel in the first part of the tell, then glow hotter and hotter, flickering
                // faster toward the burst.
                LavaCracks cracks = panel.crack != null ? panel.crack.GetComponent<LavaCracks>() : null;
                if (cracks != null)
                {
                    float k = Mathf.Clamp01(t / tell);
                    cracks.Reveal = 1.05f * Mathf.Clamp01(t / (tell * 0.45f));
                    cracks.Intensity = Mathf.Lerp(1.5f, 12f, k * k);
                    cracks.Pulse = k;
                }

                if (t >= tell)
                    Drop(i);
            }
            else if (panel.state == PanelState.Fallen && panel.slab.gameObject.activeSelf)
            {
                // The slab drops into the lava, tilting, and sinks out of sight.
                float t = now - panel.started;
                float drop = burstDrop * t + 0.5f * 22f * t * t;
                float toLava = panel.slabRest.y - lavaY + 0.1f;
                float tLava = (-burstDrop + Mathf.Sqrt(burstDrop * burstDrop + 2f * 22f * toLava)) / 22f;
                if (drop > toLava)
                    drop = toLava + (t - tLava) * 0.45f;
                panel.slab.position = panel.slabRest + Vector3.down * drop;
                panel.slab.rotation = Quaternion.Euler(panel.spin * Mathf.Min(t, tLava + 0.3f) * 0.35f) * panel.slabRestRotation;
                if (t > tLava + 2.5f)
                    panel.slab.gameObject.SetActive(false);
            }
        }

        // Into the lava (with the panel under them, or past the lip some other way): no endless fall. Kael goes down
        // into it in a burst of flame and dies (owner, 2026-11-10).
        PlayerCombat player = HallActors.Player;
        if (player != null && sinking == false && player.IsDead == false && player.transform.position.y < floorY - 0.25f)
        {
            int at = IndexAt(player.transform.position);
            if (at >= 0 && panels[at].state == PanelState.Fallen)
                LavaDeath(player, at);
        }

        if (sinking && player != null)
        {
            // He sinks to the chest over a second and a half, then under.
            float t = now - sinkStart;
            player.transform.position = sinkFrom + Vector3.down * Mathf.Min(t * 1.1f, 2.4f);
        }
    }

    private void Drop(int index)
    {
        Panel panel = panels[index];
        panel.state = PanelState.Fallen;
        panel.started = Time.time;
        panel.spin = new Vector3(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-40f, 40f));
        FallenCount++;
        if (panel.top != null)
            panel.top.enabled = false;
        if (panel.crack != null)
            panel.crack.SetActive(false);
        if (panel.hole != null)
            panel.hole.SetActive(true);

        // The burst: the floor opens in that instant.
        Vector2 centre = panel.rect.center;
        LavaBurst.Spawn(new Vector3(centre.x, floorY, centre.y), panel.rect.size * 0.85f, emberMaterial, smokeMaterial, debrisMesh, debrisMaterial, 1.2f);
        if (panel.carve != null)
            panel.carve.enabled = true;
        LightHoles(index);

        FindAnyObjectByType<PlayerCamera>()?.AddShake(0.3f, 0.45f);

        // The player still on it drops with it (its top is gone) and the lava takes them: Update sees them go under.

        foreach (IHallBoss boss in HallActors.Bosses)
        {
            if (boss == null || (boss as UnityEngine.Object) == null || boss.IsDead)
                continue;
            Vector3 p = boss.transform.position;
            if (panel.rect.Contains(new Vector2(p.x, p.z)) == false)
                continue;
            boss.HazardHit(bossDamage, bossStagger);
            Vector3 edge = EdgeNear(p, index);
            var agent = boss.transform.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
                agent.Warp(edge);
            else
                boss.transform.position = edge;
        }

        PanelFell?.Invoke(index);
    }

    // The lava death: whether Kael is going under, and from where.
    private bool sinking;
    private float sinkStart;
    private Vector3 sinkFrom;

    public bool PlayerInLava => sinking;

    private void LavaDeath(PlayerCombat player, int index)
    {
        sinking = true;
        sinkStart = Time.time;
        sinkFrom = new Vector3(player.transform.position.x, lavaY - 0.2f, player.transform.position.z);

        // Off the controls and out of the physics: the lava has him.
        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;
        var controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        player.transform.position = sinkFrom;

        // The lava flares where he went in: flame, a fountain of sparks, a jolt.
        FloorFire.Circle(sinkFrom, 1.3f, 3f);
        Transform hole = panels[index].hole != null ? panels[index].hole.transform : null;
        hole?.Find("Embers")?.GetComponent<ParticleSystem>()?.Emit(90);
        FindAnyObjectByType<PlayerCamera>()?.AddShake(0.25f, 0.35f);

        Health health = player.GetComponent<Health>();
        if (health != null)
            health.ApplyDamage(health.Current + 1f);
    }

    private readonly System.Collections.Generic.List<int> fallenOrder = new System.Collections.Generic.List<int>();

    // The newest holes keep their light; older ones glow by their lava and embers alone.
    private void LightHoles(int newest)
    {
        fallenOrder.Remove(newest);
        fallenOrder.Add(newest);
        for (int i = 0; i < fallenOrder.Count; i++)
        {
            Light light = panels[fallenOrder[i]].holeLight;
            if (light != null)
                light.enabled = i >= fallenOrder.Count - maxHoleLights;
        }
    }

    // The nearest solid ground beside panel `index`: straight out past each of its edges (and its rubble lip),
    // taking the closest that isn't another hole.
    public Vector3 EdgeNear(Vector3 point, int index)
    {
        Rect r = panels[index].rect;
        const float clear = 1.2f;
        Vector3[] options =
        {
            new Vector3(r.xMin - clear, floorY, Mathf.Clamp(point.z, r.yMin + 0.5f, r.yMax - 0.5f)),
            new Vector3(r.xMax + clear, floorY, Mathf.Clamp(point.z, r.yMin + 0.5f, r.yMax - 0.5f)),
            new Vector3(Mathf.Clamp(point.x, r.xMin + 0.5f, r.xMax - 0.5f), floorY, r.yMin - clear),
            new Vector3(Mathf.Clamp(point.x, r.xMin + 0.5f, r.xMax - 0.5f), floorY, r.yMax + clear),
        };

        Vector3 best = options[0];
        float bestDistance = float.MaxValue;
        foreach (Vector3 option in options)
        {
            int under = IndexAt(option);
            if (under >= 0 && panels[under].state == PanelState.Fallen)
                continue;
            float d = HallActors.FlatDistance(point, option);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = option;
            }
        }

        return best;
    }

    public void ResetState()
    {
        if (sinking)
        {
            sinking = false;
            PlayerCombat player = HallActors.Player;
            if (player != null)
            {
                var movement = player.GetComponent<PlayerMovement>();
                if (movement != null)
                    movement.enabled = true;
                var controller = player.GetComponent<CharacterController>();
                if (controller != null)
                    controller.enabled = true;
            }
        }

        FallenCount = 0;
        fallenOrder.Clear();
        foreach (Panel panel in panels)
        {
            panel.state = PanelState.Whole;
            if (panel.slab != null)
            {
                panel.slab.gameObject.SetActive(true);
                if (panel.slabRest != Vector3.zero)
                    panel.slab.SetPositionAndRotation(panel.slabRest, panel.slabRestRotation);
            }

            if (panel.top != null)
                panel.top.enabled = true;
            if (panel.crack != null)
                panel.crack.SetActive(false);
            if (panel.hole != null)
                panel.hole.SetActive(false);
            if (panel.carve != null)
                panel.carve.enabled = false;
        }
    }
}
