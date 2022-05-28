#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(TerrainSurface))]

    public class TerrainSurfaceEditor : Editor
    {
        private TerrainData _terDat;
        private TerrainSurface _targetScript;
        private string[] _surfaceNames;

        public override void OnInspectorGUI() {
            GroundSurfaceMaster surfaceMaster = FindObjectOfType<GroundSurfaceMaster>();
            _targetScript = (TerrainSurface)target;
            Undo.RecordObject(_targetScript, "Terrain Surface Change");

            if (_targetScript.GetComponent<Terrain>().terrainData) {
                _terDat = _targetScript.GetComponent<Terrain>().terrainData;
            }

            EditorGUILayout.LabelField("Textures and Surface Types:", EditorStyles.boldLabel);

            _surfaceNames = new string[surfaceMaster.surfaceTypes.Length];

            for (int i = 0; i < _surfaceNames.Length; i++) {
                _surfaceNames[i] = surfaceMaster.surfaceTypes[i].name;
            }

            if (_targetScript.surfaceTypes.Length > 0) {
                for (int j = 0; j < _targetScript.surfaceTypes.Length; j++) {
                    DrawTerrainInfo(_terDat, j);
                }
            }
            else {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("<No terrain textures found>");
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(_targetScript);
            }
        }

        void DrawTerrainInfo(TerrainData ter, int index) {
            EditorGUI.indentLevel = 1;
            _targetScript.surfaceTypes[index] = EditorGUILayout.Popup(_terDat.terrainLayers[index].diffuseTexture.name, _targetScript.surfaceTypes[index], _surfaceNames);
            EditorGUI.indentLevel++;
        }
    }
}
#endif