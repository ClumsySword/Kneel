using Kneel.Hazards;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Test buttons on the hazard components, so each one can be tried from the inspector in play mode.
    public abstract class HazardInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                Buttons();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("The test buttons work in play mode.", MessageType.None);
            }
        }

        protected abstract void Buttons();
    }

    [CustomEditor(typeof(BrandStake))]
    public class BrandStakeInspector : HazardInspector
    {
        protected override void Buttons()
        {
            var stake = (BrandStake)target;
            if (GUILayout.Button("Knock"))
            {
                stake.Knock();
            }

            if (GUILayout.Button("Reset"))
            {
                stake.ResetState();
            }
        }
    }

    [CustomEditor(typeof(CropRow))]
    public class CropRowInspector : HazardInspector
    {
        protected override void Buttons()
        {
            var row = (CropRow)target;
            if (GUILayout.Button("Ignite"))
            {
                row.Ignite();
            }

            if (GUILayout.Button("Reset To Dry"))
            {
                row.ResetToDry();
            }
        }
    }

    [CustomEditor(typeof(GibbetAmbush))]
    public class GibbetAmbushInspector : HazardInspector
    {
        protected override void Buttons()
        {
            var gibbet = (GibbetAmbush)target;
            EditorGUILayout.LabelField("State", gibbet.Current + (gibbet.WasStruckEarly ? " (struck early)" : ""));
            if (GUILayout.Button("Trigger (player came close)"))
            {
                gibbet.Trigger();
            }

            if (GUILayout.Button("Strike Early"))
            {
                gibbet.TakeHit(new DamageInfo { amount = 1f, source = gibbet.gameObject });
            }

            if (GUILayout.Button("Reset"))
            {
                gibbet.ResetState();
            }
        }
    }

    [CustomEditor(typeof(SluiceGate))]
    public class SluiceGateInspector : HazardInspector
    {
        protected override void Buttons()
        {
            var gate = (SluiceGate)target;
            EditorGUILayout.LabelField("State", gate.IsOpen ? "Open" : "Barred");
            if (GUILayout.Button("Open From +Z (bar side)"))
            {
                gate.TryOpenFrom(gate.transform.position + gate.transform.forward);
            }

            if (GUILayout.Button("Try From -Z (barred side)"))
            {
                bool opened = gate.TryOpenFrom(gate.transform.position - gate.transform.forward);
                Debug.Log(opened ? "Opened from -Z (this should not happen)." : SluiceGate.BarredPrompt, gate);
            }

            if (GUILayout.Button("Close"))
            {
                gate.SetOpen(false);
            }
        }
    }
}
