using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The transition, the Unmaking (The Master.md §5, 10 s, both invulnerable). MasterBoss runs it when Phase 1 ends.
//
//  0.0  he's down on one knee (MasterFallenState). The camera drops to a low, close frame on him.
//  0.5  he turns the sword on himself (MasterStabPose): an ember burst and a jolt at the contact.
//  1.5  the threads go taut: ~60 ember lines from the dead outside into his back (ThreadPull), on the summon clip.
//  2.5  the first darkness wave (the hall's DarknessWave), until 8.5.
//  3.0  every chandelier still up falls (no damage; a player under one is shoved 1 R clear), every brazier goes over,
//       the banners catch fire (visual only).
//  4.0  an ash vortex rises round him, opaque 4.5-5.5; the P1 body is gone at 5.0.
//  5.5  the Colossus rises in it (Mutant Flexing Muscles) at the nearest clear NavMesh point.
//  7.0  the camera pulls back to the fight's framing.
//  7.5  the mask splits (light pours out, a roar); the floor cracks in a ring and pushes a player inside it out to 3 R.
//  8.5  the hall relights from the doors; the bar refills with an ember sweep.
// 10.0  input returns; he opens with a Great Command.
// Kael can walk and heal throughout; nothing lands on him. After the first time each save, holding Interact for 0.5 s
// jumps to 7.5 s with the hazards' end state applied all the same.
public class Unmaking : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public Material smokeMaterial;
    public Material emberMaterial;
    public Material crackMaterial;

    public bool Running { get; private set; }

    // For MasterPlaytest: as if Interact were held (the skip), where the skip is allowed.
    public static bool ForceSkip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ForceSkip = false;

    // Seconds since it began (for the playtest), and whether it was skipped.
    public float Elapsed => Running ? Time.time - began : -1f;

    public bool Skipped { get; private set; }

    // Where the Colossus rose, and how far that was from where the Master knelt.
    public Vector3 RisePoint { get; private set; }

    private float began;
    private MasterStabPose stab;
    private Transform swordParent;
    private Vector3 swordLocalPosition;
    private Quaternion swordLocalRotation;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<(Renderer renderer, MaterialPropertyBlock block)> scorched = new List<(Renderer, MaterialPropertyBlock)>();
    private ThreadPull threads;
    private Material vortexMaterial;
    private Light maskLight;

    public IEnumerator Run(MasterBoss boss, bool skippable)
    {
        Running = true;
        Skipped = false;
        began = Time.time;
        MasterSettings s = boss.settings;
        MasterP1 master = boss.master;
        AshColossus colossus = boss.colossus;
        PlayerCombat player = FindAnyObjectByType<PlayerCombat>();
        PlayerCamera cam = FindAnyObjectByType<PlayerCamera>();
        DarknessWave darkness = FindAnyObjectByType<DarknessWave>();
        var controls = new PlayerControls();
        controls.Character.Interact.Enable();
        float held = 0f;

        // Kael walks and heals; nothing lands on him.
        player.Shield(s.unmakingTime + 0.5f);
        player.Restrict(s.unmakingTime, 1f, allowHeal: true);
        Vector3 knelt = master.transform.position;

        // The low, close frame on him (tilted up about 10 degrees), held through the stab and the threads.
        bool lowShot = true;
        StartCoroutine(LowShot(cam, master.transform, () => lowShot));

        bool stabbed = false, lodged = false, dark = false, fell = false, vortex = false, gone = false, risen = false, pulled = false, split = false, relit = false;
        ParticleSystem storm = null;
        while (Time.time - began < s.unmakingTime)
        {
            float t = Time.time - began;

            // The skip (not the first time): straight to the mask split, the hall as it would be by then.
            if (skippable && t < 7.5f)
            {
                held = controls.Character.Interact.IsPressed() || ForceSkip ? held + Time.deltaTime : 0f;
                if (held >= s.skipHoldTime)
                {
                    Skipped = true;
                    began = Time.time - 7.5f;
                    t = 7.5f;
                    if (dark == false) { dark = true; darkness?.Begin(1f); }
                    if (fell == false) { fell = true; DropTheHall(master.transform.position, s); }
                    if (gone == false) { gone = true; HideMaster(master); }
                    if (risen == false) { risen = true; RisePoint = Raise(colossus, knelt, s, 0.5f); }
                    vortex = true;
                    threads?.Release();
                    lowShot = false;
                    pulled = true;
                }
            }

            if (stabbed == false && t >= 0.5f)
            {
                stabbed = true;
                stab = master.GetComponent<MasterStabPose>() ?? master.gameObject.AddComponent<MasterStabPose>();
                Transform sword = master.swordTip.parent;
                swordParent = sword.parent;
                swordLocalPosition = sword.localPosition;
                swordLocalRotation = sword.localRotation;
                stab.Init(master.anim, master.swordTip);
                stab.Drive(0.35f);
                master.bossTell?.StopTells();
            }

            if (lodged == false && t >= 0.85f)
            {
                lodged = true;
                // The blade goes in: the cracks blaze, embers burst from his chest.
                Transform chest = master.anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? master.anim.GetBoneTransform(HumanBodyBones.Chest);
                Burst(chest.position, 0.6f, 70);
                if (master.bossTell != null) master.bossTell.EmberScale = 2.5f;
                cam?.AddShake(0.35f, 0.5f);
            }

            if (pulled == false && t >= 1.5f)
            {
                pulled = true;
                stab?.Lodge();
                master.anim.CrossFadeInFixedTime("Summon", 0.4f, 0, 0f);
                Transform back = master.anim.GetBoneTransform(HumanBodyBones.Chest) ?? master.transform;
                threads = ThreadPull.Begin(back, Vector3.zero, Mathf.Max(8, s.threadRibbons), 2.5f, 2.6f, 0.75f);
            }

            if (dark == false && t >= 2.5f)
            {
                dark = true;
                darkness?.Begin(6f);
            }

            if (fell == false && t >= 3f)
            {
                fell = true;
                DropTheHall(master.transform.position, s);
            }

            if (vortex == false && t >= 4f)
            {
                vortex = true;
                storm = Vortex(master.transform.position);
            }

            if (storm != null)
            {
                // Thickens to opaque by 4.5, holds to 5.5, thins out by 7.
                float density = t < 4.5f ? Mathf.InverseLerp(4f, 4.5f, t) : t < 5.5f ? 1f : 1f - Mathf.InverseLerp(5.5f, 7f, t);
                var emission = storm.emission;
                emission.rateOverTime = 380f * density;
            }

            if (gone == false && t >= 5f)
            {
                gone = true;
                threads?.Release(0.6f);
                HideMaster(master);
            }

            if (risen == false && t >= 5.5f)
            {
                risen = true;
                RisePoint = Raise(colossus, knelt, s, 2f);
            }

            if (lowShot && t >= 7f)
                lowShot = false;

            if (split == false && t >= 7.5f)
            {
                split = true;
                MaskSplit(colossus, player, s, cam);
            }

            if (relit == false && t >= 8.5f)
            {
                relit = true;
                boss.Bar.Show();
                boss.Bar.FillTo(0f, 1f, 1.5f);
            }

            yield return null;
        }

        controls.Character.Interact.Disable();
        controls.Dispose();
        player.ReleaseRestriction();
        if (storm != null)
            storm.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Running = false;
    }

    // The close frame (§5: lower and closer, tilted up about 10 degrees): from his side, toward the hall's middle, so the
    // threads from the grand doors cross the frame into his back and Kael isn't between the lens and him.
    private IEnumerator LowShot(PlayerCamera cam, Transform him, System.Func<bool> hold)
    {
        if (cam == null)
            yield break;
        Vector3 start = him.position;
        Vector3 side = start.x < 54f ? Vector3.right : Vector3.left;
        // A little toward the doors as well, so the threads come in from the frame's edge.
        Vector3 from = (side + Vector3.back * 0.45f).normalized;
        while (hold())
        {
            Vector3 focus = start + Vector3.up * 2.6f;
            Vector3 at = start + from * 12f + Vector3.up * 3.2f;
            cam.SetOverride(at, Quaternion.LookRotation(focus - at), 1f, 42f);
            yield return null;
        }

        cam.ClearOverride(1.5f);
    }

    // 3.0 s: the hall comes down around him.
    private void DropTheHall(Vector3 him, MasterSettings s)
    {
        foreach (ChandelierDrop chandelier in ChandelierDrop.All)
        {
            if (chandelier.Current == ChandelierDrop.State.Hanging)
                chandelier.Trigger(s.chandelierShove);
        }

        foreach (BrazierTopple brazier in BrazierTopple.All)
        {
            if (brazier.Current == BrazierTopple.State.Standing)
                brazier.Trigger(him);
        }

        BurnBanners();
    }

    // The banners catch: a short burst of flame on each, then they hang scorched through Phase 2 (visual only).
    private void BurnBanners()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name != "Banner" || t.position.x < 32f || t.position.x > 76f || t.position.z < 405f || t.position.z > 476f)
                continue;
            Renderer renderer = t.GetComponentInChildren<Renderer>();
            if (renderer == null)
                continue;
            Bounds b = renderer.bounds;
            var fire = new GameObject("BannerFire");
            fire.transform.position = b.center;
            spawned.Add(fire);
            Emit(fire.transform, emberMaterial, new Color(1f, 0.5f, 0.15f), new Vector3(b.size.x, b.size.y, b.size.z) * 0.5f, 18f, 4f, new Vector2(0.05f, 0.12f), new Vector2(0.8f, 1.6f), -0.25f);
            Emit(fire.transform, smokeMaterial, new Color(0.18f, 0.16f, 0.15f, 0.5f), new Vector3(b.size.x, b.size.y, b.size.z) * 0.4f, 5f, 4f, new Vector2(0.5f, 1.1f), new Vector2(1.5f, 2.5f), -0.05f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0.22f, 0.18f, 0.16f));
            renderer.SetPropertyBlock(block);
            scorched.Add((renderer, block));
        }
    }

    private void HideMaster(MasterP1 master)
    {
        Burst(master.transform.position + Vector3.up * 2f, 2f, 60);
        master.gameObject.SetActive(false);
    }

    // The Colossus at the nearest point of his NavMesh with room to stand (clearance from any edge), at most
    // colossusMaxOffset from where the Master knelt; crouched, rising over `seconds`.
    private Vector3 Raise(AshColossus colossus, Vector3 knelt, MasterSettings s, float seconds)
    {
        Vector3 at = ClearPoint(colossus, knelt, s.colossusClearance, s.colossusMaxOffset);
        PlayerCombat player = HallActors.Player;
        Vector3 look = player != null ? player.transform.position - at : Vector3.back;
        look.y = 0f;
        colossus.Show();
        colossus.RiseAt(at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.back), seconds);
        return at;
    }

    // Whether the last rise had to settle for the clearest point there was (nothing within reach clear enough).
    public static bool LastRiseFellBack { get; private set; }

    public static Vector3 ClearPoint(AshColossus colossus, Vector3 from, float clearance, float maxOffset)
    {
        LastRiseFellBack = false;
        var filter = new NavMeshQueryFilter { agentTypeID = colossus.agent.agentTypeID, areaMask = NavMesh.AllAreas };
        // His NavMesh already stops his radius short of anything, so the edge only has to be the rest of the way.
        clearance = Mathf.Max(0f, clearance - colossus.agent.radius);
        Vector3 best = from;
        float bestDistance = float.MaxValue;
        float bestClear = -1f;
        Vector3 bestFallback = from;
        for (float r = 0f; r <= maxOffset + 0.01f; r += 0.5f)
        {
            int steps = r <= 0f ? 1 : 16;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = from + Quaternion.Euler(0f, i * 360f / steps, 0f) * Vector3.forward * r;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, filter) == false)
                    continue;
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, filter) == false)
                    continue;
                float d = HallActors.FlatDistance(hit.position, from);
                if (edge.distance >= clearance && d < bestDistance)
                {
                    bestDistance = d;
                    best = hit.position;
                }

                if (edge.distance > bestClear)
                {
                    bestClear = edge.distance;
                    bestFallback = hit.position;
                }
            }

            if (bestDistance < float.MaxValue)
                return best;
        }

        LastRiseFellBack = true;
        return bestFallback;
    }

    // 7.5 s: the mask splits down the middle; ember light pours out, he roars, the floor rings out from him.
    private void MaskSplit(AshColossus colossus, PlayerCombat player, MasterSettings s, PlayerCamera cam)
    {
        colossus.riseState.Roar();
        if (colossus.mask != null)
        {
            if (maskLight == null)
            {
                maskLight = new GameObject("MaskLight").AddComponent<Light>();
                maskLight.type = LightType.Point;
                maskLight.color = new Color(1f, 0.45f, 0.15f);
                maskLight.shadows = LightShadows.None;
            }

            maskLight.transform.SetParent(colossus.mask, false);
            maskLight.transform.localPosition = Vector3.zero;
            StartCoroutine(MaskFlare(maskLight));
            Burst(colossus.mask.position, 0.5f, 80);
        }

        // The ring: cracks round him, dust, and a push out to 3 R for anyone inside it.
        Vector3 at = colossus.transform.position;
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "UnmakingRing";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetPositionAndRotation(new Vector3(at.x, at.y + 0.03f, at.z), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        ring.transform.localScale = Vector3.one * s.ringPush * 1.4f;
        var renderer = ring.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = crackMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spawned.Add(ring);
        ColossusFx.Ring(colossus, at, 2.5f, s.ringPush, 0.4f);
        cam?.AddShake(0.45f, 1.2f);

        if (player != null)
        {
            Vector3 away = player.transform.position - at;
            away.y = 0f;
            float d = away.magnitude;
            if (d < s.ringPush)
                player.Push(d > 0.1f ? away / d : Vector3.back, s.ringPush - d);
        }
    }

    private IEnumerator MaskFlare(Light light)
    {
        float start = Time.time;
        while (light != null && Time.time - start < 2.5f)
        {
            float t = Time.time - start;
            light.intensity = t < 0.3f ? Mathf.Lerp(0f, 45f, t / 0.3f) : Mathf.Lerp(45f, 8f, (t - 0.3f) / 2.2f);
            light.range = Mathf.Lerp(18f, 7f, t / 2.5f);
            yield return null;
        }
    }

    private ParticleSystem Vortex(Vector3 at)
    {
        var go = new GameObject("AshVortex");
        go.transform.position = at;
        spawned.Add(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.42f, 0.34f, 0.95f), new Color(0.75f, 0.58f, 0.45f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1200;
        main.gravityModifier = -0.08f;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 3.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = 3.2f;
        velocity.radial = -0.25f;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(2.6f, 4.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));
        // Ember-lit from below as it rises off him, cooling to grey ash: it has to read in the dark wave.
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.22f), 0f), new GradientColorKey(new Color(0.85f, 0.55f, 0.38f), 0.35f), new GradientColorKey(new Color(0.55f, 0.5f, 0.47f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        // The smoke sprite, but light enough to take the colours (its own base colour is a dark smoke).
        vortexMaterial = new Material(smokeMaterial);
        vortexMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
        renderer.sharedMaterial = vortexMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();

        // Embers caught in it.
        var embers = new GameObject("VortexEmbers");
        embers.transform.SetParent(go.transform, false);
        Emit(embers.transform, emberMaterial, new Color(1f, 0.55f, 0.2f), new Vector3(3.5f, 0.5f, 3.5f), 140f, 3.5f, new Vector2(0.05f, 0.14f), new Vector2(1f, 2.2f), -0.8f);
        // A low glow inside it, so the ash is lit from within as he burns.
        var glow = new GameObject("VortexGlow").AddComponent<Light>();
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = Vector3.up * 2.5f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.42f, 0.14f);
        glow.range = 9f;
        glow.intensity = 9f;
        glow.shadows = LightShadows.None;
        Destroy(glow.gameObject, 3.2f);
        return ps;
    }

    private void Burst(Vector3 at, float radius, int count)
    {
        var go = new GameObject("UnmakingBurst");
        go.transform.position = at;
        spawned.Add(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startColor = new Color(1f, 0.55f, 0.2f);
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 4;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = emberMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, 3f);
    }

    private static void Emit(Transform parent, Material material, Color color, Vector3 box, float rate, float seconds, Vector2 size, Vector2 life, float gravity)
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = seconds;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(rate * (life.y + 0.5f)) + 4;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    // Back to before it (a retry, the playtest): the Master whole with his sword in hand, no vortex, no scorch.
    public void ResetState()
    {
        StopAllCoroutines();
        Running = false;
        if (stab != null)
        {
            Transform sword = stab.GetComponent<MasterP1>()?.swordTip?.parent;
            if (sword != null && swordParent != null)
            {
                sword.SetParent(swordParent, false);
                sword.SetLocalPositionAndRotation(swordLocalPosition, swordLocalRotation);
            }

            Destroy(stab);
            stab = null;
        }

        foreach (GameObject go in spawned)
        {
            if (go != null)
                Destroy(go);
        }

        spawned.Clear();
        foreach (var (renderer, block) in scorched)
        {
            if (renderer != null)
                renderer.SetPropertyBlock(new MaterialPropertyBlock());
        }

        scorched.Clear();
        if (threads != null)
            Destroy(threads.gameObject);
        if (maskLight != null)
            Destroy(maskLight.gameObject);
        if (vortexMaterial != null)
            Destroy(vortexMaterial);
    }
}
