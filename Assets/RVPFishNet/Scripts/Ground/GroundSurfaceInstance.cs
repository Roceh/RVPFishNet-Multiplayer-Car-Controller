using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Ground Surface/Ground Surface Instance", 1)]
    public class GroundSurfaceInstance : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Which surface type to use from the GroundSurfaceMaster list of surface types")]
        public int surfaceType;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float friction;

        private void Start()
        {
            // Set friction
            if (GroundSurfaceMaster.surfaceTypesStatic[surfaceType].useColliderFriction)
            {
                PhysicMaterial sharedMat = GetComponent<Collider>().sharedMaterial;
                friction = sharedMat != null ? sharedMat.dynamicFriction * 2 : 1.0f;
            }
            else
            {
                friction = GroundSurfaceMaster.surfaceTypesStatic[surfaceType].friction;
            }
        }
    }
}