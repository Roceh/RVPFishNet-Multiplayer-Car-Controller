#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(SuspensionPart))]
    [CanEditMultipleObjects]

    public class SuspensionPartEditor : Editor
    {
        private static bool _showHandles = true;

        public override void OnInspectorGUI() {
            _showHandles = EditorGUILayout.Toggle("Show Handles", _showHandles);
            SceneView.RepaintAll();

            DrawDefaultInspector();
        }

        public void OnSceneGUI() {
            SuspensionPart targetScript = (SuspensionPart)target;
            Undo.RecordObject(targetScript, "Suspension Part Change");

            if (_showHandles && targetScript.gameObject.activeInHierarchy) {
                if (targetScript.connectObj && !targetScript.isHub && !targetScript.solidAxle && Tools.current == Tool.Move) {
                    targetScript.connectPoint = targetScript.connectObj.InverseTransformPoint(Handles.PositionHandle(targetScript.connectObj.TransformPoint(targetScript.connectPoint), Tools.pivotRotation == PivotRotation.Local ? targetScript.connectObj.rotation : Quaternion.identity));
                }
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }
    }
}
#endif