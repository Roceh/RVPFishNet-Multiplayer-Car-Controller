#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(VehicleParent))]
    [CanEditMultipleObjects]

    public class VehicleParentEditor : Editor
    {
        private bool _isPrefab = false;
        private static bool _showButtons = true;
        private bool _wheelMissing = false;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            VehicleParent targetScript = (VehicleParent)target;
            VehicleParent[] allTargets = new VehicleParent[targets.Length];
            _isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Vehicle Parent Change");
                allTargets[i] = targets[i] as VehicleParent;
            }

            _wheelMissing = false;
            if (targetScript.wheelGroups != null && targetScript.wheelGroups.Length > 0) {
                if (targetScript.hover) {
                    foreach (HoverWheel curWheel in targetScript.hoverWheels) {
                        bool wheelfound = false;
                        foreach (WheelCheckGroup curGroup in targetScript.wheelGroups) {
                            foreach (HoverWheel curWheelInstance in curGroup.hoverWheels) {
                                if (curWheel == curWheelInstance) {
                                    wheelfound = true;
                                }
                            }
                        }

                        if (!wheelfound) {
                            _wheelMissing = true;
                            break;
                        }
                    }
                }
                else {
                    foreach (Wheel curWheel in targetScript.wheels) {
                        bool wheelfound = false;
                        foreach (WheelCheckGroup curGroup in targetScript.wheelGroups) {
                            foreach (Wheel curWheelInstance in curGroup.wheels) {
                                if (curWheel == curWheelInstance) {
                                    wheelfound = true;
                                }
                            }
                        }

                        if (!wheelfound) {
                            _wheelMissing = true;
                            break;
                        }
                    }
                }
            }

            if (_wheelMissing) {
                EditorGUILayout.HelpBox("If there is at least one wheel group, all wheels must be part of a group.", MessageType.Error);
            }

            DrawDefaultInspector();

            if (!_isPrefab && targetScript.gameObject.activeInHierarchy) {
                _showButtons = EditorGUILayout.Foldout(_showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (_showButtons) {
                    if (GUILayout.Button("Get Engine")) {
                        foreach (VehicleParent curTarget in allTargets) {
                            curTarget.engine = curTarget.transform.GetComponentInChildren<Motor>();
                        }
                    }

                    if (GUILayout.Button("Get Wheels")) {
                        foreach (VehicleParent curTarget in allTargets) {
                            if (curTarget.hover) {
                                curTarget.hoverWheels = curTarget.transform.GetComponentsInChildren<HoverWheel>();
                            }
                            else {
                                curTarget.wheels = curTarget.transform.GetComponentsInChildren<Wheel>();
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