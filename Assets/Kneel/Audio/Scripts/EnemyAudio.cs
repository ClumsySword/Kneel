using UnityEngine;

namespace Kneel.Audio
{
    // The enemies' sounds, by type and state. Every type's states enter through EnemyState.Enter, which calls
    // StateEntered, so a wind-up cue plays as each attack starts (the ten-pager: every enemy type has its own telegraph
    // cue, so an attack that starts off-screen can still be answered on sound alone). The Master and the Ash Colossus
    // have their own sounds (BossTell) and are left alone here.
    public static class EnemyAudio
    {
        // Feet are only heard this close to the camera, so a patrol across the level doesn't take the pool's voices.
        private const float StepRange = 25f;

        private enum Kind
        {
            None,
            Footman,
            Archer,
            Brute,
            Fool,
            Twin,
        }

        private static Kind KindOf(Enemy enemy)
        {
            if (enemy == null || enemy is MasterBody)
            {
                return Kind.None;
            }

            switch (enemy.GetType().Name)
            {
                case "AshenFootman": return Kind.Footman;
                case "HollowArcher": return Kind.Archer;
                case "BlightedBrute": return Kind.Brute;
                case "TheFool": return Kind.Fool;
                case "CursedTwins": return Kind.Twin;
                default: return Kind.Footman;
            }
        }

        public static void StateEntered(Enemy enemy, string state)
        {
            Kind kind = KindOf(enemy);
            if (kind == Kind.None)
            {
                return;
            }

            Vector3 at = enemy.transform.position + Vector3.up * 1.5f;
            switch (state)
            {
                case "Notice":
                    if (kind == Kind.Brute)
                        Sfx.Play(SfxId.BruteRoar, at);
                    else if (kind == Kind.Fool)
                        Sfx.Play(SfxId.TellFool, at);
                    break;

                case "Enrage":
                    Sfx.Play(SfxId.BruteRoar, at);
                    break;

                case "Attack":
                case "Sweep":
                case "Chop":
                case "Spin":
                case "Combo":
                    Sfx.Play(Tell(kind), at);
                    break;

                case "Dead":
                    Sfx.Play(SfxId.EnemyDeath, at);
                    Sfx.Play(SfxId.BodyFall, enemy.transform.position, 0.8f);
                    break;
            }
        }

        private static SfxId Tell(Kind kind)
        {
            switch (kind)
            {
                case Kind.Archer: return SfxId.TellArcher;
                case Kind.Brute: return SfxId.TellBrute;
                case Kind.Fool: return SfxId.TellFool;
                default: return SfxId.TellFootman;
            }
        }

        // The blade passing in front of him (each type's AttackHit, hit or miss).
        public static void Swing(Enemy enemy)
        {
            Kind kind = KindOf(enemy);
            if (kind == Kind.None || kind == Kind.Archer)
            {
                return;
            }

            bool heavy = kind == Kind.Brute || kind == Kind.Fool;
            Sfx.Play(heavy ? SfxId.PlayerSwingHeavy : SfxId.PlayerSwingLight, enemy.transform.position + Vector3.up * 1.2f, heavy ? 1f : 0.8f);
        }

        public static void Hurt(Enemy enemy)
        {
            if (KindOf(enemy) == Kind.None || enemy.health == null || enemy.health.Current <= 0f)
            {
                return;
            }

            Sfx.Play(SfxId.EnemyHurt, enemy.transform.position + Vector3.up * 1.5f, 0.8f);
        }

        // Called every frame with the distance he moved; plays a step every stride.
        public static void Step(Enemy enemy, ref float travelled, float moved)
        {
            Kind kind = KindOf(enemy);
            if (kind == Kind.None)
            {
                return;
            }

            travelled += moved;
            float stride = kind == Kind.Brute ? 1.5f : 0.85f;
            if (travelled < stride)
            {
                return;
            }

            travelled = 0f;
            Camera cam = Camera.main;
            if (cam == null || (cam.transform.position - enemy.transform.position).sqrMagnitude > StepRange * StepRange)
            {
                return;
            }

            SfxId step = LevelAudio.Surface == FootSurface.Earth ? SfxId.FootstepEarth : SfxId.FootstepStone;
            Sfx.Play(step, enemy.transform.position, kind == Kind.Brute ? 0.9f : 0.45f);
        }
    }
}
