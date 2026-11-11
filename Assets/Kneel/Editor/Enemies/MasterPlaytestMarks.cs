using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The tells' look (owner, 2026-11-11): blows carry no light, spells charge his cracks, and where a spell lands is
    // marked on the floor (SpellMark), following the aim until a late lock. Groups: the move's own (decree, threads,
    // cleave, hammer, lash).
    public static partial class MasterPlaytest
    {
        private static int LitLights(Component body) => body.GetComponentsInChildren<Light>().Count(l => l.enabled && l.intensity > 0f);

        private static SpellMark FindMark() => UnityEngine.Object.FindObjectsByType<SpellMark>(FindObjectsSortMode.None).FirstOrDefault(m => m.isActiveAndEnabled);

        // --- Phase 1 ---

        private static IEnumerator BlowsUnlit()
        {
            Note("--- a blow carries no light, and the cracks stay at rest");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3f);
            float charge = 1f;
            int lit = 0;
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, master.bossTell.ChargeLevel);
                lit = Mathf.Max(lit, LitLights(master));
                return Move == null || Move.Landed;
            });
            Check("a Decree: no light on him, the cracks at rest", lit == 0 && charge < 1.01f, lit + " lights lit, charge x" + charge.ToString("F2"));
            Light palm = master.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "PalmLight");
            Check("the Command's palm light lights the hall, not him", palm != null && (palm.renderingLayerMask & (int)HallActors.BossLayer) == 0, palm != null ? "mask " + palm.renderingLayerMask : "no palm light");
            yield return WaitMoveEnds();
        }

        private static IEnumerator ThreadsMark()
        {
            Note("--- Threads: the lanes on the floor, the lock, the cracks' charge");
            yield return ResetFight(Centre);
            yield return FarCamera(true);
            yield return Start(MasterP1.MoveKind.Threads, 13f);
            int before = blows.Count;
            SpellMark mark = FindMark();
            Check("three lanes are marked from the start of the tell", mark != null && mark.transform.childCount == 3, mark != null ? mark.transform.childCount + " cracks" : "no mark");
            if (mark == null)
            {
                yield return WaitMoveEnds();
                yield break;
            }

            // A sidestep while he aims: the lanes follow.
            Vector3 aimed = mark.Forward;
            yield return 0.15f;
            PlaceAbsolute(playerRoot.position + master.transform.right * 2f, master.transform.position - playerRoot.position);
            yield return 0.15f;
            Check("before the lock the lanes follow the player", Vector3.Angle(aimed, mark.Forward) > 4f && mark.Locked == false, Vector3.Angle(aimed, mark.Forward).ToString("F1") + " deg, locked " + mark.Locked);

            float charge = 1f;
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, master.bossTell.ChargeLevel);
                return Move == null || Move.SinceStart >= Move.Tell - S.threadsLock + 0.05f;
            });
            Check("they lock " + S.threadsLock + " s before the release", mark != null && mark.Locked, mark != null ? "locked " + mark.Locked : "mark gone");
            Framed("Threads_mark", Vector3.Lerp(master.transform.position, playerRoot.position, 0.55f), 12f, 16f);

            // A step after the lock, into the gap between the centre lane and a side one: the threads keep to the lanes.
            Vector3 locked = mark.Forward;
            PlaceAbsolute(playerRoot.position + master.transform.right * 1.4f, master.transform.position - playerRoot.position);
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, master.bossTell.ChargeLevel);
                return Move == null || Move.Landed;
            });
            float peak = master.bossTell.chargePeak;
            Check("the cracks charge to x" + peak + " as it lands", charge >= peak * 0.9f, "x" + charge.ToString("F2"));
            yield return 0.4f;
            Framed("Threads_mark_running", Vector3.Lerp(master.transform.position, playerRoot.position, 0.5f), 12f, 18f);
            yield return 1.6f;
            Check("a step out of the locked lane: no hit", blows.Count == before, (blows.Count - before) + " hits");
            Check("the threads ran down the marked lanes", ThreadsAlong(locked), "locked aim " + locked.ToString("F2"));
            Check("the cracks settle", master.bossTell.ChargeLevel < 1.05f, "x" + master.bossTell.ChargeLevel.ToString("F2"));
            Check("the mark is gone", FindMark() == null, FindMark() == null ? "gone" : "still there");
            yield return WaitMoveEnds();
            yield return FarCamera(false);
        }

        // The centre thread's line is the locked aim (its fire runs along it).
        private static bool ThreadsAlong(Vector3 aim)
        {
            Vector3 probe = master.transform.position + aim * 6f;
            return FloorFire.IsBurning(probe);
        }

        // --- Phase 2 ---

        private static IEnumerator P2BlowsUnlit()
        {
            Note("--- a Colossus blow carries no light, and the cracks stay at rest");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Cleave, 4f);
            float charge = 1f;
            int lit = 0;
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, colossus.bossTell.ChargeLevel);
                lit = Mathf.Max(lit, LitLights(colossus));
                return P2Move == null || P2Move.Landed;
            });
            Check("a Cleave: no light on him, the cracks at rest", lit == 0 && charge < 1.01f, lit + " lights lit, charge x" + charge.ToString("F2"));
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator LashMark()
        {
            Note("--- Thread Lash: five lanes on the floor, the lock, the cracks' charge");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.ThreadLash, 15f);
            SpellMark mark = FindMark();
            Check(S2.lashCount + " lanes are marked from the start of the tell", mark != null && mark.transform.childCount == S2.lashCount, mark != null ? mark.transform.childCount + " cracks" : "no mark");
            float charge = 1f;
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, colossus.bossTell.ChargeLevel);
                return P2Move == null || P2Move.SinceStart >= P2Move.Tell - S2.lashLock + 0.05f;
            });
            Check("they lock " + S2.lashLock + " s before the release", mark != null && mark.Locked, mark != null ? "locked " + mark.Locked : "mark gone");
            Framed("P2_Lash_mark", Vector3.Lerp(colossus.transform.position, playerRoot.position, 0.55f), 14f, 20f);
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, colossus.bossTell.ChargeLevel);
                return P2Move == null || P2Move.Landed;
            });
            Check("the cracks charge as it lands", charge >= colossus.bossTell.chargePeak * 0.9f, "x" + charge.ToString("F2"));
            // Out of the lane, before the head arrives.
            PlaceAbsolute(playerRoot.position + colossus.transform.right * 1.6f, colossus.transform.position - playerRoot.position);
            yield return 2.5f;
            string[] otherMoves = Enum.GetNames(typeof(AshColossus.MoveKind)).Where(n => n != "ThreadLash").ToArray();
            int hits = p2Blows.Skip(p2Mark).Count(h => otherMoves.Contains(h.state) == false);
            Check("a step out of the locked lane: no hit", hits == 0, hits + " hits");
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator HammerMark()
        {
            Note("--- Hammerfall: the impact circle on the floor");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Hammerfall, 9f);
            SpellMark mark = FindMark();
            Check("the impact circle is marked from the start of the tell", mark != null && mark.transform.childCount == 2, mark != null ? mark.transform.childCount + " arcs" : "no mark");
            yield return (Func<bool>)(() => P2Move == null || P2Move.SinceStart >= P2Move.Tell - 0.2f);
            bool placed = mark != null && Mathf.Abs(HallActors.FlatDistance(mark.Origin, colossus.transform.position) - S2.hammerOut) < 0.3f && mark.Locked;
            Check("it sits " + S2.hammerOut + " m in front of him, locked for the last 0.25 s", placed, mark != null ? HallActors.FlatDistance(mark.Origin, colossus.transform.position).ToString("F2") + " m, locked " + mark.Locked : "mark gone");
            Framed("P2_Hammer_mark", mark != null ? mark.Origin : colossus.transform.position, 12f, 14f);
            float charge = 1f;
            yield return (Func<bool>)(() =>
            {
                charge = Mathf.Max(charge, colossus.bossTell.ChargeLevel);
                return P2Move == null || P2Move.Landed;
            });
            Check("a blow, not a spell: the cracks stay at rest", charge < 1.01f, "x" + charge.ToString("F2"));
            yield return 0.5f;
            Check("the mark is gone once it lands", FindMark() == null, FindMark() == null ? "gone" : "still there");
            yield return P2WaitMoveEnds();
        }
    }
}
