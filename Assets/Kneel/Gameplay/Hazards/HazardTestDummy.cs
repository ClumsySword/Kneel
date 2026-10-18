using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Hazards
{
    // A stand-in actor for checking hazards without the real player or enemies. It implements IHazardTarget,
    // logs every contact, and says what that contact would do to the kind of actor it is set to.
    public class HazardTestDummy : MonoBehaviour, IHazardTarget
    {
        public enum ActorKind
        {
            Player,
            Hound,
            Footman,
            Brute,
        }

        [SerializeField]
        private ActorKind actsAs = ActorKind.Player;

        [SerializeField]
        private bool logToConsole = true;

        private readonly Dictionary<HazardKind, int> counts = new Dictionary<HazardKind, int>();

        public ActorKind ActsAs
        {
            get => actsAs;
            set => actsAs = value;
        }

        // Oldest first; "time kind consequence".
        public readonly List<string> Log = new List<string>();

        public bool OnSurface { get; private set; }

        public float RollDistanceMultiplier { get; private set; } = 1f;

        public float MoveSpeedMultiplier { get; private set; } = 1f;

        public int Count(HazardKind kind)
        {
            return counts.TryGetValue(kind, out int n) ? n : 0;
        }

        public void ClearLog()
        {
            counts.Clear();
            Log.Clear();
        }

        public void OnHazard(HazardKind kind)
        {
            counts[kind] = Count(kind) + 1;
            Record(kind + ": " + Consequence(kind));
        }

        public void SetSurface(float rollDistanceMultiplier, float moveSpeedMultiplier)
        {
            OnSurface = true;
            RollDistanceMultiplier = rollDistanceMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            Record("SetSurface: roll x" + rollDistanceMultiplier + ", move x" + moveSpeedMultiplier);
        }

        public void ClearSurface()
        {
            OnSurface = false;
            RollDistanceMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            Record("ClearSurface");
        }

        // What the design says this contact does to this kind of actor.
        public string Consequence(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Smoulder:
                    return actsAs == ActorKind.Hound ? "bolts sideways into the nearest lane, staggered 1 s" : "no effect (the tell)";
                case HazardKind.FireEnter:
                case HazardKind.FireTick:
                    return actsAs == ActorKind.Hound ? "dies" : "takes one hit";
                default:
                    return "dies (fell in the ditch)";
            }
        }

        private void Record(string what)
        {
            string line = Time.time.ToString("F2") + "  " + name + " (" + actsAs + ")  " + what;
            Log.Add(line);
            if (logToConsole == true)
            {
                Debug.Log("[Hazard] " + line, this);
            }
        }
    }
}
