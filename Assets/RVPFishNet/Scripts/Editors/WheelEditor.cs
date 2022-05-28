#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(Wheel))]
    [CanEditMultipleObjects]

    public class WheelEditor : Editor
    {
        private bool _isPrefab = false;
        private static bool _showButtons = true;
        private static float _radiusMargin = 0;
        private static float _widthMargin = 0;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            Wheel targetScript = (Wheel)target;
            Wheel[] allTargets = new Wheel[targets.Length];
            _isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Wheel Change");
                allTargets[i] = targets[i] as Wheel;
            }

            DrawDefaultInspector();

            if (!_isPrefab && targetScript.gameObject.activeInHierarchy) {
                _showButtons = EditorGUILayout.Foldout(_showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (_showButtons) {
                    if (GUILayout.Button("Get Wheel Dimensions")) {
                        foreach (Wheel curTarget in allTargets) {
                            curTarget.GetWheelDimensions(_radiusMargin, _widthMargin);
                        }
                    }

                    EditorGUI.indentLevel++;
                    _radiusMargin = EditorGUILayout.FloatField("Radius Margin", _radiusMargin);
                    _widthMargin = EditorGUILayout.FloatField("Width Margin", _widthMargin);
                    EditorGUI.indentLevel--;
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