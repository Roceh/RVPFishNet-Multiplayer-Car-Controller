using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Hover/Hover Wheel", 1)]
    public class HoverWheel : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public float hoverDistance;

        [Tooltip("If the distance to the ground is less than this, extra hovering force will be applied based on the buffer float force")]
        public float bufferDistance;

        // Is the wheel turned on?
        public float floatForce = 1;

        [Tooltip("")]
        public float bufferFloatForce = 2;

        [Tooltip("Strength of the suspension depending on how compressed it is, x-axis = compression, y-axis = force")]
        public AnimationCurve floatForceCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("")]
        public float floatExponent = 1;

        [Tooltip("")]
        public float floatDampening;

        [Tooltip("")]
        public float brakeForce = 1;

        [Tooltip("")]
        public float ebrakeForce = 2;
        
        [Tooltip("How much the wheel steers")]
        public float steerFactor;

        [Tooltip("")]
        public float sideFriction;

        [Header("Visual Wheel")]
        public Transform visualWheel;

        [Tooltip("")]
        public float visualTiltRate = 10;

        [Tooltip("")]
        public float visualTiltAmount = 0.5f;

        [Header("Damage")]
        public float detachForce = Mathf.Infinity;

        [Tooltip("")]
        public float mass = 0.05f;

        [Tooltip("")]
        public Mesh wheelMeshLoose;

        // Mesh for detached wheel collider
        public PhysicMaterial detachedWheelMaterial;

        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [System.NonSerialized]
        public HoverContact contactPoint = new HoverContact();

        [System.NonSerialized]
        public bool doFloat;

        // Contact points of the wheels
        [System.NonSerialized]
        public bool getContact = true;

        // Should the wheel try to get contact info?
        [System.NonSerialized]
        public bool grounded;

        [System.NonSerialized]
        public float targetSpeed;

        [System.NonSerialized]
        public float targetForce;

        [System.NonSerialized]
        public float steerRate;               

        [System.NonSerialized]
        public bool connected = true;

        [System.NonSerialized]
        public bool canDetach;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private Vector3 _upDir; // Local up direction
        private float _compression; // How compressed the suspension is
        private float _flippedSideFactor; // Multiplier for inverting the forces on opposite sides
        private GameObject _detachedWheel;
        private MeshCollider _detachedCol;
        private Rigidbody _detachedBody;
        private MeshFilter _detachFilter;

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            contactPoint.GetFullState(writer);

            writer.WriteVector3(transform.localPosition);
            writer.WriteQuaternion(transform.localRotation, AutoPackType.PackedLess);
            writer.WriteBoolean(getContact);
            writer.WriteBoolean(grounded);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            contactPoint.SetFullState(reader);

            transform.localPosition = reader.ReadVector3();
            transform.localRotation = reader.ReadQuaternion(AutoPackType.PackedLess);
            getContact = reader.ReadBoolean();
            grounded = reader.ReadBoolean();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
            writer.WriteVector3(transform.localPosition);
            writer.WriteQuaternion(transform.localRotation);
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
            transform.localPosition = reader.ReadVector3();
            transform.localRotation = reader.ReadQuaternion();
        }

        void IVehicleComponent.Simulate()
        {
            _upDir = transform.up;

            // Get the contact point
            if (getContact)
            {
                GetWheelContact();
            }
            else if (grounded)
            {
                contactPoint.point += _rb.GetPointVelocity(transform.position) * _vm.tickDelta;
            }

            _compression = Mathf.Clamp01(contactPoint.distance / (hoverDistance));

            // Apply float and driving forces
            if (grounded && doFloat && connected)
            {
                ApplyFloat();
                ApplyFloatDrive();
            }
        }

        // Detach the wheel from the vehicle
        public void Detach()
        {
            if (connected && canDetach)
            {
                connected = false;
                _detachedWheel.SetActive(true);
                _detachedWheel.transform.position = visualWheel.position;
                _detachedWheel.transform.rotation = visualWheel.rotation;
                _detachedCol.sharedMaterial = detachedWheelMaterial;
                _detachedCol.sharedMesh = wheelMeshLoose ? wheelMeshLoose : _detachFilter.sharedMesh;

                _rb.mass -= mass;
                _detachedBody.velocity = _rb.GetPointVelocity(visualWheel.position);
                _detachedBody.angularVelocity = _rb.angularVelocity;

                visualWheel.gameObject.SetActive(false);
            }
        }

        // Reattach the wheel to the vehicle if detached
        public void Reattach()
        {
            if (!connected)
            {
                connected = true;
                _detachedWheel.SetActive(false);
                _rb.mass += mass;
                visualWheel.gameObject.SetActive(true);
            }
        }

        private void Awake()
        {
            _rb = transform.GetTopmostParentComponent<Rigidbody>();
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -20);
        }

        private void Start()
        {
            
            _flippedSideFactor = Vector3.Dot(transform.forward, _vp.transform.right) < 0 ? 1 : -1;
            canDetach = detachForce < Mathf.Infinity && Application.isPlaying;
            bufferDistance = Mathf.Min(hoverDistance, bufferDistance);

            if (canDetach)
            {
                _detachedWheel = new GameObject(_vp.transform.name + "'s Detached Wheel");
                _detachedWheel.layer = LayerMask.NameToLayer("Detachable Part");
                _detachFilter = _detachedWheel.AddComponent<MeshFilter>();
                _detachFilter.sharedMesh = visualWheel.GetComponent<MeshFilter>().sharedMesh;
                MeshRenderer detachRend = _detachedWheel.AddComponent<MeshRenderer>();
                detachRend.sharedMaterial = visualWheel.GetComponent<MeshRenderer>().sharedMaterial;
                _detachedCol = _detachedWheel.AddComponent<MeshCollider>();
                _detachedCol.convex = true;
                _detachedBody = _detachedWheel.AddComponent<Rigidbody>();
                _detachedBody.mass = mass;
                _detachedWheel.SetActive(false);
            }
        }

        private void Update()
        {
            // Tilt the visual wheel
            if (visualWheel && connected)
            {
                TiltWheel();
            }
        }

        // Get the contact point of the wheel
        private void GetWheelContact()
        {
            RaycastHit hit = new RaycastHit();
            Vector3 localVel = _rb.GetPointVelocity(transform.position);
            RaycastHit[] wheelHits = Physics.RaycastAll(transform.position, -_upDir, hoverDistance, GlobalControl.wheelCastMaskStatic);
            bool validHit = false;
            float hitDist = Mathf.Infinity;

            // Loop through contact points to get the closest one
            foreach (RaycastHit curHit in wheelHits)
            {
                if (!curHit.transform.IsChildOf(_vp.transform) && curHit.distance < hitDist)
                {
                    hit = curHit;
                    hitDist = curHit.distance;
                    validHit = true;
                }
            }

            // Set contact point variables
            if (validHit)
            {
                if (!hit.collider.transform.IsChildOf(_vp.transform))
                {
                    grounded = true;
                    contactPoint.distance = hit.distance;
                    contactPoint.point = hit.point + localVel * _vm.tickDelta;
                    contactPoint.grounded = true;
                    contactPoint.normal = hit.normal;
                    contactPoint.relativeVelocity = transform.InverseTransformDirection(localVel);
                    contactPoint.gameObject = hit.collider.gameObject;
                }
            }
            else
            {
                grounded = false;
                contactPoint.distance = hoverDistance;
                contactPoint.point = Vector3.zero;
                contactPoint.grounded = false;
                contactPoint.normal = _upDir;
                contactPoint.relativeVelocity = Vector3.zero;
                contactPoint.gameObject = null;
            }
        }

        // Make the vehicle hover
        private void ApplyFloat()
        {
            if (grounded)
            {
                // Get the vertical speed of the wheel
                float travelVel = _vp.norm.InverseTransformDirection(_rb.GetPointVelocity(transform.position)).z;

                _rb.AddForceAtPosition(_upDir * floatForce * (Mathf.Pow(floatForceCurve.Evaluate(1 - _compression), Mathf.Max(1, floatExponent)) - floatDampening * Mathf.Clamp(travelVel, -1, 1)),
                    transform.position,
                    _vp.suspensionForceMode);

                if (contactPoint.distance < bufferDistance)
                {
                    _rb.AddForceAtPosition(-_upDir * bufferFloatForce * floatForceCurve.Evaluate(contactPoint.distance / bufferDistance) * Mathf.Clamp(travelVel, -1, 0),
                        transform.position,
                        _vp.suspensionForceMode);
                }
            }
        }

        // Drive the vehicle
        private void ApplyFloatDrive()
        {
            // Get proper brake force
            float actualBrake = (_vp.localVelocity.z > 0 ? _vp.brakeInput : Mathf.Clamp01(_vp.accelInput)) * brakeForce + _vp.ebrakeInput * ebrakeForce;

            _rb.AddForceAtPosition(
                transform.TransformDirection(
                    (Mathf.Clamp(targetSpeed, -1, 1) * targetForce - actualBrake * Mathf.Max(5, Mathf.Abs(contactPoint.relativeVelocity.x)) * Mathf.Sign(contactPoint.relativeVelocity.x) * _flippedSideFactor) * _flippedSideFactor,
                    0,
                    -steerRate * steerFactor * _flippedSideFactor - contactPoint.relativeVelocity.z * sideFriction) * (1 - _compression),
                transform.position,
                _vp.wheelForceMode);
        }

        // Tilt the visual wheel
        private void TiltWheel()
        {
            float sideTilt = Mathf.Clamp(-steerRate * steerFactor * _flippedSideFactor - Mathf.Clamp(contactPoint.relativeVelocity.z * 0.1f, -1, 1) * sideFriction, -1, 1);
            float actualBrake = (_vp.localVelocity.z > 0 ? _vp.brakeInput : Mathf.Clamp01(_vp.accelInput)) * brakeForce + _vp.ebrakeInput * ebrakeForce;
            float forwardTilt = Mathf.Clamp((Mathf.Clamp(targetSpeed, -1, 1) * targetForce - actualBrake * Mathf.Clamp(contactPoint.relativeVelocity.x * 0.1f, -1, 1) * _flippedSideFactor) * _flippedSideFactor, -1, 1);

            visualWheel.localRotation = Quaternion.Lerp(visualWheel.localRotation,
                Quaternion.LookRotation(new Vector3(-forwardTilt * visualTiltAmount, -1 + Mathf.Abs(F.MaxAbs(sideTilt, forwardTilt)) * visualTiltAmount, -sideTilt * visualTiltAmount).normalized, Vector3.forward),
                visualTiltRate * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw a ray to show the distance of the "suspension"
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, -transform.up * hoverDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, -transform.up * bufferDistance);
        }

        // Destroy detached wheel
        private void OnDestroy()
        {
            if (_detachedWheel)
            {
                Destroy(_detachedWheel);
            }
        }
    }

    // Class for the contact point
    public class HoverContact
    {
        public GameObject gameObject; // Collider of the contact point
        public bool grounded; // Is it grounded?
        public Vector3 point; // Position of the contact point
        public Vector3 normal; // Normal of the contact point
        public Vector3 relativeVelocity; // Velocity of the wheel relative to the contact point
        public float distance; // Distance from the wheel to the contact point

        public void GetFullState(Writer writer)
        {
            writer.WriteGameObject(gameObject);
            writer.WriteBoolean(grounded);
            writer.WriteVector3(point);
            writer.WriteVector3(normal);
            writer.WriteVector3(relativeVelocity);
            writer.WriteSingle(distance);
        }

        public void SetFullState(Reader reader)
        {
            gameObject = reader.ReadGameObject();
            grounded = reader.ReadBoolean();
            point = reader.ReadVector3();
            normal = reader.ReadVector3();
            relativeVelocity = reader.ReadVector3();
            distance = reader.ReadSingle();
        }
    }
}