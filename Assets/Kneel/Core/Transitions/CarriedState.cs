using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Core
{
    // Everything the player owns that carries from one level into the next: health, potions, unlocks, Ash (once it
    // exists) and anything added later. One snapshot, written by every component that owns a piece of it
    // (ICarriedState) and read back by the same components on arrival. It is saved in SaveData.entry as the state
    // the player carried INTO the save's level, so Continue and a death reload both start that level with it.
    [Serializable]
    public class CarriedState
    {
        // Bumped when a field changes meaning; old snapshots are still read (missing fields keep their defaults).
        public const int CurrentVersion = 1;

        // False for a save written before carried state existed (JsonUtility fills a missing object with defaults).
        public bool valid;
        public int version = CurrentVersion;

        // The level this snapshot was taken to enter. A snapshot only applies to its own level.
        public string scene;

        // Negative = not recorded (the component keeps its own start value).
        public float health = -1f;
        public float maxHealth = -1f;
        public int potions = -1;

        // The Ash meter (LORE §2: it persists between levels). No component owns it yet; the meter writes it here
        // through ICarriedState when it is built.
        public float ash;

        // Things earned once and kept: abilities, keys that open later doors, upgrades. Ids are free strings
        // ("HeavyAttack", "Ash.Ability1"). Nothing in the game unlocks anything yet; this is the hook.
        public List<string> unlocks = new List<string>();

        // Anything else a future component needs to carry, as key/value text (JsonUtility can't store dictionaries).
        public List<Entry> values = new List<Entry>();

        [Serializable]
        public struct Entry
        {
            public string key;
            public string value;
        }

        public bool HasUnlock(string id) => unlocks != null && unlocks.Contains(id);

        public void AddUnlock(string id)
        {
            unlocks ??= new List<string>();
            if (string.IsNullOrEmpty(id) == false && unlocks.Contains(id) == false)
            {
                unlocks.Add(id);
            }
        }

        public string Get(string key, string fallback = null)
        {
            if (values != null)
            {
                foreach (Entry e in values)
                {
                    if (e.key == key)
                    {
                        return e.value;
                    }
                }
            }

            return fallback;
        }

        public void Set(string key, string value)
        {
            values ??= new List<Entry>();
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].key == key)
                {
                    values[i] = new Entry { key = key, value = value };
                    return;
                }
            }

            values.Add(new Entry { key = key, value = value });
        }

        public CarriedState Clone() => JsonUtility.FromJson<CarriedState>(JsonUtility.ToJson(this));

        public override string ToString() =>
            valid ? $"[{scene}] hp {health:0.#}/{maxHealth:0.#} potions {potions} ash {ash:0.#} unlocks {(unlocks != null ? string.Join(",", unlocks) : "")} values {(values != null ? values.Count : 0)}" : "[none]";
    }

    // A component that owns part of the carried state. Components on the player are found automatically; anything
    // else (a meter living on a HUD) registers itself with CarriedStates.Register while it is enabled.
    public interface ICarriedState
    {
        void WriteTo(CarriedState state);

        // Called once the level has started (after every Start), so a value set here isn't overwritten by the
        // component's own start-up.
        void ReadFrom(CarriedState state);
    }

    public static class CarriedStates
    {
        private static readonly List<ICarriedState> registered = new List<ICarriedState>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();

        public static void Register(ICarriedState source)
        {
            if (source != null && registered.Contains(source) == false)
            {
                registered.Add(source);
            }
        }

        public static void Unregister(ICarriedState source) => registered.Remove(source);

        // A snapshot of the player as they are now, for entering `scene`.
        public static CarriedState Capture(GameObject player, string scene)
        {
            var state = new CarriedState { valid = true, scene = scene };
            foreach (ICarriedState source in Sources(player))
            {
                source.WriteTo(state);
            }

            return state;
        }

        public static void Apply(GameObject player, CarriedState state)
        {
            if (state == null || state.valid == false)
            {
                return;
            }

            foreach (ICarriedState source in Sources(player))
            {
                source.ReadFrom(state);
            }
        }

        private static IEnumerable<ICarriedState> Sources(GameObject player)
        {
            if (player != null)
            {
                foreach (ICarriedState s in player.GetComponentsInChildren<ICarriedState>(true))
                {
                    yield return s;
                }
            }

            for (int i = registered.Count - 1; i >= 0; i--)
            {
                if (registered[i] is UnityEngine.Object o && o == null)
                {
                    registered.RemoveAt(i);
                    continue;
                }

                yield return registered[i];
            }
        }
    }
}
