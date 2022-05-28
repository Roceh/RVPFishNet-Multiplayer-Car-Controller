using UnityEngine;
using UnityEngine.SceneManagement;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Scene Controllers/Global Control", 0)]
    public class GlobalControl : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Mask for what the wheels collide with")]
        public LayerMask wheelCastMask;

        [Tooltip("Mask for objects which vehicles check against if they are rolled over")]
        public LayerMask groundMask;

        [Tooltip("Mask for objects that cause damage to vehicles")]
        public LayerMask damageMask;

        [Tooltip("Frictionless physic material")]
        public PhysicMaterial frictionlessMat;

        [Tooltip("Maximum segments per tire mark")]
        public int tireMarkLength;

        // Global up direction, opposite of normalized gravity direction
        [Tooltip("Gap between tire mark segments")]
        public float tireMarkGap;

        [Tooltip("Tire mark height above ground")]
        public float tireMarkHeight;

        [Tooltip("Lifetime of tire marks")]
        public float tireFadeTime;

        // -=-=-=-= SHARED STATE =-=-=-=-

        public static LayerMask wheelCastMaskStatic;
        public static LayerMask groundMaskStatic;
        public static LayerMask damageMaskStatic;
        public static int ignoreWheelCastLayer;
        public static PhysicMaterial frictionlessMatStatic;
        public static Vector3 worldUpDir;
        public static int tireMarkLengthStatic;
        public static float tireMarkGapStatic;
        public static float tireMarkHeightStatic;
        public static float tireFadeTimeStatic;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private float _initialFixedTime;

        private void Start()
        {
            _initialFixedTime = Time.fixedDeltaTime;
            // Set static variables
            wheelCastMaskStatic = wheelCastMask;
            groundMaskStatic = groundMask;
            damageMaskStatic = damageMask;
            ignoreWheelCastLayer = LayerMask.NameToLayer("Ignore Wheel Cast");
            frictionlessMatStatic = frictionlessMat;
            tireMarkLengthStatic = Mathf.Max(tireMarkLength, 2);
            tireMarkGapStatic = tireMarkGap;
            tireMarkHeightStatic = tireMarkHeight;
            tireFadeTimeStatic = tireFadeTime;
        }

        private void FixedUpdate()
        {
            // Set global up direction
            worldUpDir = Physics.gravity.sqrMagnitude == 0 ? Vector3.up : -Physics.gravity.normalized;
        }
    }
}