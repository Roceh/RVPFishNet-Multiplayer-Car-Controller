using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(DriveForce))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Suspension/Suspension", 0)]
    public class Suspension : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public Wheel wheel;

        [Tooltip("Generate a capsule collider for hard compressions")]
        public bool generateHardCollider = true;

        [Tooltip("Multiplier for the radius of the hard collider")]
        public float hardColliderRadiusFactor = 1;

        [Header("Brakes and Steering")]
        [Tooltip("")]
        public float brakeForce;

        [Tooltip("")]
        public float ebrakeForce;

        [Tooltip("")]
        [Range(-180, 180)]
        public float steerRangeMin;

        [Tooltip("")]
        [Range(-180, 180)]
        public float steerRangeMax;

        [Tooltip("How much the wheel is steered")]
        public float steerFactor = 1;

        [Tooltip("Effect of Ackermann steering geometry")]
        public float ackermannFactor;

        [Tooltip("The camber of the wheel as it travels, x-axis = compression, y-axis = angle")]
        public AnimationCurve camberCurve = AnimationCurve.Linear(0, 0, 1, 0);

        [Tooltip("")]
        [Range(-89.999f, 89.999f)]
        public float camberOffset;

        [Tooltip("Adjust the camber as if it was connected to a solid axle, opposite wheel must be set")]
        public bool solidAxleCamber;

        [Tooltip("")]
        public Suspension oppositeWheel;

        [Tooltip("Angle at which the suspension points out to the side")]
        [Range(-89.999f, 89.999f)]
        public float sideAngle;

        [Tooltip("")]
        [Range(-89.999f, 89.999f)]
        public float casterAngle;

        [Tooltip("")]
        [Range(-89.999f, 89.999f)]
        public float toeAngle;

        [Tooltip("Wheel offset from its pivot point")]
        public float pivotOffset;

        [Header("Spring")]
        [Tooltip("")]
        public float suspensionDistance;

        [Tooltip("Should be left at 1 unless testing suspension travel")]
        [Range(0, 1)]
        public float targetCompression;

        [Tooltip("How deep the ground is interesecting with the wheel's tire")]
        public float springForce;

        [Tooltip("Force of the curve depending on it's compression, x-axis = compression, y-axis = force")]
        public AnimationCurve springForceCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Exponent for spring force based on compression")]
        public float springExponent = 1;

        [Tooltip("")]
        public float springDampening;

        [Tooltip("How quickly the suspension extends if it's not grounded")]
        public float extendSpeed = 20;

        [Tooltip("Apply forces to prevent the wheel from intersecting with the ground, not necessary if generating a hard collider")]
        public bool applyHardContactForce = true;

        [Tooltip("")]
        public float hardContactForce = 50;
        
        [Tooltip("")]
        public float hardContactSensitivity = 2;

        [Tooltip("Apply suspension forces at ground point")]
        public bool applyForceAtGroundContact = true;

        [Tooltip("Apply suspension forces along local up direction instead of ground normal")]
        public bool leaningForce;

        [Header("Damage")]
        [Tooltip("Point around which the suspension pivots when damaged")]
        public Vector3 damagePivot;

        [Tooltip("Compression amount to remain at when wheel is detached")]
        [Range(0, 1)]
        public float detachedCompression = 0.5f;

        [Tooltip("")]
        public float jamForce = Mathf.Infinity;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float steerAngle;

        // Variables for inverting certain values on opposite sides of the vehicle
        [System.NonSerialized]
        public bool flippedSide;

        [System.NonSerialized]
        public float flippedSideFactor;

        [System.NonSerialized]
        public Quaternion initialRotation;

        [System.NonSerialized]
        public List<SuspensionPart> movingParts = new List<SuspensionPart>();

        [System.NonSerialized]
        public Vector3 maxCompressPoint;

        [System.NonSerialized]
        public float compression;

        [System.NonSerialized]
        public float penetration;

        // Position of the wheel when the suspension is compressed all the way
        [System.NonSerialized]
        public Vector3 springDirection;

        [System.NonSerialized]
        public Vector3 upDir;

        // Local up direction
        [System.NonSerialized]
        public Vector3 forwardDir;

        [System.NonSerialized]
        public DriveForce targetDrive;

        [System.NonSerialized]
        public SuspensionPropertyToggle properties;

        // Property toggler
        [System.NonSerialized]
        public bool steerEnabled = true;

        // The drive being passed into the wheel
        [System.NonSerialized]
        public bool steerInverted;

        [System.NonSerialized]
        public float steerDegrees;

        // Local forward direction
        [System.NonSerialized]
        public bool driveEnabled = true;

        [System.NonSerialized]
        public bool driveInverted;

        [System.NonSerialized]
        public bool ebrakeEnabled = true;

        [System.NonSerialized]
        public bool skidSteerBrake;

        [System.NonSerialized]
        public float camberAngle;

        [System.NonSerialized]
        public bool jammed;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private CapsuleCollider _compressCol; // The hard collider
        private float _hardColliderRadiusFactorPrev;
        private float _setHardColliderRadiusFactor;
        private Transform _compressTr; // Transform component of the hard collider

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            targetDrive.GetFullState(writer);

            writer.WriteSingle(steerAngle);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            targetDrive.SetFullState(reader);

            steerAngle = reader.ReadSingle();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            upDir = transform.up;
            forwardDir = transform.forward;
            targetCompression = 1;

            GetCamber();

            GetSpringVectors();

            if (wheel.connected)
            {
                compression = Mathf.Min(targetCompression, suspensionDistance > 0 ? Mathf.Clamp01(wheel.contactPoint.distance / suspensionDistance) : 0);
                penetration = Mathf.Min(0, wheel.contactPoint.distance);
            }
            else
            {
                compression = detachedCompression;
                penetration = 0;
            }

            if (targetCompression > 0)
            {
                ApplySuspensionForce();
            }

            // Set hard collider size if it is changed during play mode
            if (generateHardCollider)
            {
                _setHardColliderRadiusFactor = hardColliderRadiusFactor;

                if (_hardColliderRadiusFactorPrev != _setHardColliderRadiusFactor || wheel.updatedSize || wheel.updatedPopped)
                {
                    if (wheel.rimWidth > wheel.actualRadius)
                    {
                        _compressCol.direction = 2;
                        _compressCol.radius = wheel.actualRadius * hardColliderRadiusFactor;
                        _compressCol.height = wheel.rimWidth * 2;
                    }
                    else
                    {
                        _compressCol.direction = 1;
                        _compressCol.radius = wheel.rimWidth * hardColliderRadiusFactor;
                        _compressCol.height = wheel.actualRadius * 2;
                    }
                }

                _hardColliderRadiusFactorPrev = _setHardColliderRadiusFactor;
            }

            // Set the drive of the wheel
            if (wheel.connected)
            {
                if (wheel.targetDrive)
                {
                    StaticStateLogger.Log($"Suspension:targetDrive.rpm={targetDrive.rpm}");
                    StaticStateLogger.Log($"Suspension:targetDrive.torque={targetDrive.torque}");

                    targetDrive.active = driveEnabled;
                    targetDrive.feedbackRPM = wheel.targetDrive.feedbackRPM;
                    wheel.targetDrive.SetDrive(targetDrive);
                }
            }
            else
            {
                targetDrive.feedbackRPM = targetDrive.rpm;
            }

            // Set steer angle for the wheel
            steerDegrees = Mathf.Abs(steerAngle) * (steerAngle > 0 ? steerRangeMax : steerRangeMin);
        }

        // Update the toggleable properties
        public void UpdateProperties()
        {
            if (properties)
            {
                foreach (SuspensionToggledProperty curProperty in properties.properties)
                {
                    switch ((int)curProperty.property)
                    {
                        case 0:
                            steerEnabled = curProperty.toggled;
                            break;

                        case 1:
                            steerInverted = curProperty.toggled;
                            break;

                        case 2:
                            driveEnabled = curProperty.toggled;
                            break;

                        case 3:
                            driveInverted = curProperty.toggled;
                            break;

                        case 4:
                            ebrakeEnabled = curProperty.toggled;
                            break;

                        case 5:
                            skidSteerBrake = curProperty.toggled;
                            break;
                    }
                }
            }
        }

        private void Awake()
        {
            targetDrive = GetComponent<DriveForce>();

            _rb = transform.GetTopmostParentComponent<Rigidbody>();
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -40);
        }

         private void Start()
         {
            flippedSide = Vector3.Dot(transform.forward, _vp.transform.right) < 0;
            flippedSideFactor = flippedSide ? -1 : 1;
            initialRotation = transform.localRotation;

            if (Application.isPlaying)
            {
                GetCamber();

                // Generate the hard collider
                if (generateHardCollider)
                {
                    GameObject cap = new GameObject("Compress Collider");
                    cap.layer = GlobalControl.ignoreWheelCastLayer;
                    _compressTr = cap.transform;
                    _compressTr.parent = transform;
                    _compressTr.localPosition = Vector3.zero;
                    _compressTr.localEulerAngles = new Vector3(camberAngle, 0, -casterAngle * flippedSideFactor);
                    _compressCol = cap.AddComponent<CapsuleCollider>();
                    _compressCol.direction = 1;
                    _setHardColliderRadiusFactor = hardColliderRadiusFactor;
                    _hardColliderRadiusFactorPrev = _setHardColliderRadiusFactor;
                    _compressCol.radius = wheel.rimWidth * hardColliderRadiusFactor;
                    _compressCol.height = (wheel.popped ? wheel.rimRadius : Mathf.Lerp(wheel.rimRadius, wheel.tireRadius, wheel.tirePressure)) * 2;
                    _compressCol.sharedMaterial = GlobalControl.frictionlessMatStatic;
                }

                steerRangeMax = Mathf.Max(steerRangeMin, steerRangeMax);

                properties = GetComponent<SuspensionPropertyToggle>();
                if (properties)
                {
                    UpdateProperties();
                }
            }
        }

        private void Update()
        {
            //GetCamber();

            if (!Application.isPlaying)
            {
                GetSpringVectors();
            }
        }

        // Apply suspension forces to support vehicles
        private void ApplySuspensionForce()
        {
            if (wheel.grounded && wheel.connected)
            {
                // Get the local vertical velocity
                float travelVel = _vp.norm.InverseTransformDirection(_rb.GetPointVelocity(transform.position)).z;

                // Apply the suspension force
                if (suspensionDistance > 0 && targetCompression > 0)
                {
                    Vector3 appliedSuspensionForce = (leaningForce ? Vector3.Lerp(upDir, _vp.norm.forward, Mathf.Abs(Mathf.Pow(Vector3.Dot(_vp.norm.forward, _vp.upDir), 5))) : _vp.norm.forward) *
                        springForce * (Mathf.Pow(springForceCurve.Evaluate(1 - compression), Mathf.Max(1, springExponent)) - (1 - targetCompression) - springDampening * Mathf.Clamp(travelVel, -1, 1));

                    _rb.AddForceAtPosition(
                        appliedSuspensionForce,
                        applyForceAtGroundContact ? wheel.contactPoint.point : wheel.transform.position,
                        _vp.suspensionForceMode);

                    // If wheel is resting on a rigidbody, apply opposing force to it
                    if (wheel.contactPoint.gameObject?.GetComponent<Rigidbody>())
                    {
                        wheel.contactPoint.gameObject.GetComponent<Rigidbody>().AddForceAtPosition(
                            -appliedSuspensionForce,
                            wheel.contactPoint.point,
                            _vp.suspensionForceMode);
                    }
                }

                // Apply hard contact force
                if (compression == 0 && !generateHardCollider && applyHardContactForce)
                {
                    var force = -_vp.norm.TransformDirection(0, 0, Mathf.Clamp(travelVel, -hardContactSensitivity * TimeMaster.fixedTimeFactor, 0) + penetration) * hardContactForce * Mathf.Clamp01(TimeMaster.fixedTimeFactor);
                    var position = applyForceAtGroundContact ? wheel.contactPoint.point : wheel.transform.position;

                    _rb.AddForceAtPosition(force,
                        position,
                        _vp.suspensionForceMode);
                }
            }
        }

        // Calculate the direction of the spring
        private void GetSpringVectors()
        {
            if (!Application.isPlaying)
            {
                flippedSide = Vector3.Dot(transform.forward, _vp.transform.right) < 0;
                flippedSideFactor = flippedSide ? -1 : 1;
            }

            maxCompressPoint = transform.position;

            float casterDir = -Mathf.Sin(casterAngle * Mathf.Deg2Rad) * flippedSideFactor;
            float sideDir = -Mathf.Sin(sideAngle * Mathf.Deg2Rad);

            springDirection = transform.TransformDirection(casterDir, Mathf.Max(Mathf.Abs(casterDir), Mathf.Abs(sideDir)) - 1, sideDir).normalized;
        }

        // Calculate the camber angle
        private void GetCamber()
        {
            if (solidAxleCamber && oppositeWheel && wheel.connected)
            {
                if (oppositeWheel.wheel.rim && wheel.rim)
                {
                    Vector3 axleDir = transform.InverseTransformDirection((oppositeWheel.wheel.rim.position - wheel.rim.position).normalized);
                    camberAngle = Mathf.Atan2(axleDir.z, axleDir.y) * Mathf.Rad2Deg + 90 + camberOffset;
                }
            }
            else
            {
                camberAngle = camberCurve.Evaluate((Application.isPlaying && wheel.connected ? wheel.travelDist : targetCompression)) + camberOffset;
            }
        }

        // Visualize steer range
        private void OnDrawGizmosSelected()
        {
            if (wheel)
            {
                if (wheel.rim)
                {
                    Vector3 wheelPoint = wheel.rim.position;

                    float camberSin = -Mathf.Sin(camberAngle * Mathf.Deg2Rad);
                    float steerSin = Mathf.Sin(Mathf.Lerp(steerRangeMin, steerRangeMax, (steerAngle + 1) * 0.5f) * Mathf.Deg2Rad);
                    float minSteerSin = Mathf.Sin(steerRangeMin * Mathf.Deg2Rad);
                    float maxSteerSin = Mathf.Sin(steerRangeMax * Mathf.Deg2Rad);

                    Gizmos.color = Color.magenta;

                    Gizmos.DrawWireSphere(wheelPoint, 0.05f);

                    Gizmos.DrawLine(wheelPoint, wheelPoint + transform.TransformDirection(minSteerSin,
                        camberSin * (1 - Mathf.Abs(minSteerSin)),
                        Mathf.Cos(steerRangeMin * Mathf.Deg2Rad) * (1 - Mathf.Abs(camberSin))
                        ).normalized);

                    Gizmos.DrawLine(wheelPoint, wheelPoint + transform.TransformDirection(maxSteerSin,
                        camberSin * (1 - Mathf.Abs(maxSteerSin)),
                        Mathf.Cos(steerRangeMax * Mathf.Deg2Rad) * (1 - Mathf.Abs(camberSin))
                        ).normalized);

                    Gizmos.DrawLine(wheelPoint + transform.TransformDirection(minSteerSin,
                        camberSin * (1 - Mathf.Abs(minSteerSin)),
                        Mathf.Cos(steerRangeMin * Mathf.Deg2Rad) * (1 - Mathf.Abs(camberSin))
                        ).normalized * 0.9f,
                    wheelPoint + transform.TransformDirection(maxSteerSin,
                        camberSin * (1 - Mathf.Abs(maxSteerSin)),
                        Mathf.Cos(steerRangeMax * Mathf.Deg2Rad) * (1 - Mathf.Abs(camberSin))
                        ).normalized * 0.9f);

                    Gizmos.DrawLine(wheelPoint, wheelPoint + transform.TransformDirection(steerSin,
                        camberSin * (1 - Mathf.Abs(steerSin)),
                        Mathf.Cos(steerRangeMin * Mathf.Deg2Rad) * (1 - Mathf.Abs(camberSin))
                        ).normalized);
                }
            }

            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(transform.TransformPoint(damagePivot), 0.05f);
        }
    }
}