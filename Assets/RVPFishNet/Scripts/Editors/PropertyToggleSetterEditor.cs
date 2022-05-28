#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(PropertyToggleSetter))]
    [CanEditMultipleObjects]

    public class PropertyToggleSetterEditor : Editor
    {
        private bool _isPrefab = false;
        private static bool _showButtons = true;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            PropertyToggleSetter targetScript = (PropertyToggleSetter)target;
            PropertyToggleSetter[] allTargets = new PropertyToggleSetter[targets.Length];
            _isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Property Toggle Setter Change");
                allTargets[i] = targets[i] as PropertyToggleSetter;
            }

            DrawDefaultInspector();

            if (!_isPrefab && targetScript.gameObject.activeInHierarchy) {
                _showButtons = EditorGUILayout.Foldout(_showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (_showButtons) {
                    if (GUILayout.Button("Get Variables")) {
                        foreach (PropertyToggleSetter curTarget in allTargets) {
                            curTarget.steerer = curTarget.GetComponentInChildren<SteeringControl>();
                            curTarget.transmission = curTarget.GetComponentInChildren<Transmission>();
                            curTarget.suspensionProperties = curTarget.GetComponentsInChildren<SuspensionPropertyToggle>();
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }
    }
}
#endif