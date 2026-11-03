using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Fool's table for EnemyRig. He hasn't had his refine yet, so this only holds his death clip for now.
    public static class TheFoolSetup
    {
        private const string Animations = "Assets/Kneel/Enemies/TheFool/Animations/";

        // death-1 is shared with the player and lands him 23 cm under the floor: his own copy brings him up to it as he goes
        // down.
        private const float DeathLift = 0.27f;
        private const float DeathFallStart = 1f;
        private const float DeathFallEnd = 2.2f;

        [MenuItem("Kneel/Enemies/The Fool/Build Death Clip")]
        public static void BuildDeathClip()
        {
            AnimationClip shared = EnemyRig.FindClip(EnemyRig.PlayerClips + "sword and shield death.fbx", "death-1");
            AnimationClip death = EnemyRig.CopyClip(shared, Animations + "Fool_Death.anim", 0f, shared.length);
            EnemyRig.LiftRoot(death, DeathLift, DeathFallStart, DeathFallEnd);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "TheFoolController.controller");
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == "Dead")
                {
                    child.state.motion = death;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(death);
            AssetDatabase.SaveAssetIfDirty(controller);
        }
    }
}
