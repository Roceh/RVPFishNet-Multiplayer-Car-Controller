using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Terrain))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Ground Surface/Terrain Surface", 2)]
    public class TerrainSurface : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public int[] surfaceTypes = new int[0];

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float[] frictions;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Transform _tr;
        private TerrainData _terDat;
        private float[,,] _terrainAlphamap;

        // Updates the terrain alphamaps
        public void UpdateAlphamaps()
        {
            _terrainAlphamap = _terDat.GetAlphamaps(0, 0, _terDat.alphamapWidth, _terDat.alphamapHeight);
        }

        // Returns index of dominant surface type at point on terrain, relative to surface types array in GroundSurfaceMaster
        public int GetDominantSurfaceTypeAtPoint(Vector3 pos)
        {
            if (surfaceTypes.Length == 0) { return 0; }

            Vector2 coord = new Vector2(Mathf.Clamp01((pos.z - _tr.position.z) / _terDat.size.z), Mathf.Clamp01((pos.x - _tr.position.x) / _terDat.size.x));

            float maxVal = 0;
            int maxIndex = 0;
            float curVal = 0;

            for (int i = 0; i < _terrainAlphamap.GetLength(2); i++)
            {
                curVal = _terrainAlphamap[Mathf.FloorToInt(coord.x * (_terDat.alphamapWidth - 1)), Mathf.FloorToInt(coord.y * (_terDat.alphamapHeight - 1)), i];

                if (curVal > maxVal)
                {
                    maxVal = curVal;
                    maxIndex = i;
                }
            }

            return surfaceTypes[maxIndex];
        }

        // Gets the friction of the indicated surface type
        public float GetFriction(int sType)
        {
            float returnedFriction = 1;

            for (int i = 0; i < surfaceTypes.Length; i++)
            {
                if (sType == surfaceTypes[i])
                {
                    returnedFriction = frictions[i];
                    break;
                }
            }

            return returnedFriction;
        }

        private void Start()
        {
            _tr = transform;
            if (GetComponent<Terrain>().terrainData)
            {
                _terDat = GetComponent<Terrain>().terrainData;

                // Set frictions for each surface type
                if (Application.isPlaying)
                {
                    UpdateAlphamaps();
                    frictions = new float[surfaceTypes.Length];

                    for (int i = 0; i < frictions.Length; i++)
                    {
                        if (GroundSurfaceMaster.surfaceTypesStatic[surfaceTypes[i]].useColliderFriction)
                        {
                            PhysicMaterial sharedMat = GetComponent<Collider>().sharedMaterial;
                            frictions[i] = sharedMat != null ? sharedMat.dynamicFriction * 2 : 1.0f;
                        }
                        else
                        {
                            frictions[i] = GroundSurfaceMaster.surfaceTypesStatic[surfaceTypes[i]].friction;
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                if (_terDat)
                {
                    if (surfaceTypes.Length != _terDat.terrainLayers.Length)
                    {
                        ChangeSurfaceTypesLength();
                    }
                }
            }
        }

        // Calculate the number of surface types based on the terrain layers
        private void ChangeSurfaceTypesLength()
        {
            int[] tempVals = surfaceTypes;

            surfaceTypes = new int[_terDat.terrainLayers.Length];

            for (int i = 0; i < surfaceTypes.Length; i++)
            {
                if (i >= tempVals.Length)
                {
                    break;
                }
                else
                {
                    surfaceTypes[i] = tempVals[i];
                }
            }
        }
    }
}