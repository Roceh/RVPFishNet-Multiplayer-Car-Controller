#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(HoverWheel))]
    [CanEditMultipleObjects]

    public class HoverWheelEditor : Editor
    {
        private bool _isPrefab = false;
        private static bool _showButtons = true;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            HoverWheel targetScript = (HoverWheel)target;
            HoverWheel[] allTargets = new HoverWheel[targets.Length];
            _isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Hover Wheel Change");
                allTargets[i] = targets[i] as HoverWheel;
            }

            DrawDefaultInspector();

            if (!_isPrefab && targetScript.gameObject.activeInHierarchy) {
                _showButtons = EditorGUILayout.Foldout(_showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (_showButtons) {
                    if (GUILayout.Button("Get Visual Wheel")) {
                        foreach (HoverWheel curTarget in allTargets) {
                            if (curTarget.transform.childCount > 0) {
                                curTarget.visualWheel = curTarget.transform.GetChild(0);
                            }
                            else {
                                Debug.LogWarning("No visual wheel found.", this);
                            }
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