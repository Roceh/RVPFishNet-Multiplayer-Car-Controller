using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Ground Surface/Ground Surface Master", 0)]
    public class GroundSurfaceMaster : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public GroundSurface[] surfaceTypes;

        // -=-=-=-= SHARED STATE =-=-=-=-

        public static GroundSurface[] surfaceTypesStatic;

        private void Start()
        {
            surfaceTypesStatic = surfaceTypes;
        }
    }

    // Class for individual surface types
    [System.Serializable]
    public class GroundSurface
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public string name = "Surface";

        [Tooltip("")]
        public bool useColliderFriction;

        [Tooltip("")] 
        public float friction;

        [Tooltip("Always leave tire marks")]
        public bool alwaysScrape;

        [Tooltip("Rims leave sparks on this surface")]
        public bool leaveSparks;

        [Tooltip("")]
        public AudioClip tireSnd;

        [Tooltip("")]
        public AudioClip rimSnd;
        
        [Tooltip("")] 
        public AudioClip tireRimSnd;
    }
}