using FishNet.Serializing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleManager))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Parent", 0)]
    public class VehicleParent : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Accel axis is used for brake input")]
        public bool accelAxisIsBrake;

        [Tooltip("Brake input will act as reverse input")]
        public bool brakeIsReverse;

        [Tooltip("Automatically hold ebrake if it's pressed while parked")]
        public bool holdEbrakePark;

        [Tooltip("")]
        public float burnoutThreshold = 0.9f;

        [Tooltip("")]
        public float burnoutSpin = 5;

        [Tooltip("")]
        [Range(0, 0.9f)]
        public float burnoutSmoothness = 0.5f;

        [Tooltip("")]
        public Motor engine;

        [Tooltip("")]
        public Wheel[] wheels;

        [Tooltip("")]
        public HoverWheel[] hoverWheels;

        [Tooltip("")]
        public WheelCheckGroup[] wheelGroups;

        [Tooltip("")] 
        public bool hover;

        [Tooltip("Lower center of mass by suspension height")]
        public bool suspensionCenterOfMass;

        [Tooltip("")] 
        public Vector3 centerOfMassOffset;
        
        [Tooltip("")] 
        public ForceMode wheelForceMode = ForceMode.Acceleration;
        
        [Tooltip("")] 
        public ForceMode suspensionForceMode = ForceMode.Acceleration;

        [Tooltip("Tow vehicle to instantiate")]
        public GameObject towVehicle;

        [Header("Crashing")]
        [Tooltip("")]
        public bool canCrash = true;

        [Tooltip("")]
        public AudioSource crashSnd;

        [Tooltip("")]
        public AudioClip[] crashClips;

        [Tooltip("")]
        public ParticleSystem sparks;

        [Header("Camera")]
        public float cameraDistanceChange;

        [Tooltip("")]
        public float cameraHeightChange;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public Transform norm;

        [System.NonSerialized]
        public float accelInput;

        // Normal orientation object
        [System.NonSerialized]
        public float brakeInput;

        [System.NonSerialized]
        public float steerInput;

        [System.NonSerialized]
        public float ebrakeInput;

        [System.NonSerialized]
        public bool boostButton;

        [System.NonSerialized]
        public bool upshiftPressed;

        [System.NonSerialized]
        public bool downshiftPressed;

        [System.NonSerialized]
        public float upshiftHold;

        [System.NonSerialized]
        public float downshiftHold;

        [System.NonSerialized]
        public float pitchInput;

        [System.NonSerialized]
        public float yawInput;

        [System.NonSerialized]
        public float rollInput;       

        [System.NonSerialized]
        public float burnout;        

        [System.NonSerialized]
        public Vector3 localVelocity;

        // Local space velocity
        [System.NonSerialized]
        public Vector3 localAngularVel;

        // Local space angular velocity
        [System.NonSerialized]
        public Vector3 forwardDir;

        // Forward direction
        [System.NonSerialized]
        public Vector3 rightDir;

        // Right direction
        [System.NonSerialized]
        public Vector3 upDir;

        // Up direction
        [System.NonSerialized]
        public float forwardDot;

        // Dot product between forwardDir and GlobalControl.worldUpDir
        [System.NonSerialized]
        public float rightDot;

        // Dot product between rightDir and GlobalControl.worldUpDir
        [System.NonSerialized]
        public float upDot;

        // Dot product between upDir and GlobalControl.worldUpDir
        [System.NonSerialized]
        public float velMag;

        // Velocity magnitude
        [System.NonSerialized]
        public float sqrVelMag;

        [System.NonSerialized]
        public bool reversing;        

        [System.NonSerialized]
        public int groundedWheels;

        // Number of wheels grounded
        [System.NonSerialized]
        public Vector3 wheelNormalAverage;        

        [System.NonSerialized]
        public VehicleParent inputInherit;

        [System.NonSerialized]
        public bool crashing;        

        [System.NonSerialized]
        public bool playCrashSounds = true;

        [System.NonSerialized]
        public bool playCrashSparks = true;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private int _wheelCheckIndex = 0;
        private bool _stopUpshift;
        private bool _stopDownShift;
        private Vector3 _wheelContactsVelocity; // Average velocity of wheel contact points
        private GameObject _newTow;
        private Rigidbody _rb;
        private VehicleManager _vm;

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            writer.WriteVector3(_rb.transform.position);
            writer.WriteQuaternion(_rb.transform.rotation, AutoPackType.PackedLess);
            writer.WriteVector3(_rb.velocity);
            writer.WriteVector3(_rb.angularVelocity);
            writer.WriteSingle(burnout);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            _rb.transform.position = reader.ReadVector3();
            _rb.transform.rotation = reader.ReadQuaternion(AutoPackType.PackedLess);
            _rb.velocity = reader.ReadVector3();
            _rb.angularVelocity = reader.ReadVector3();
            burnout = reader.ReadSingle();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
            writer.WriteVector3(_rb.transform.position);
            writer.WriteQuaternion(_rb.transform.rotation);
            writer.WriteVector3(_rb.velocity);
            writer.WriteVector3(_rb.angularVelocity);
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
            _rb.transform.position = reader.ReadVector3();
            _rb.transform.rotation = reader.ReadQuaternion();
            _rb.velocity = reader.ReadVector3();
            _rb.angularVelocity = reader.ReadVector3();
        }

        void IVehicleComponent.Simulate()
        {
            if (inputInherit)
            {
                InheritInput();
            }

            // Shift single frame pressing logic
            if (_stopUpshift)
            {
                upshiftPressed = false;
                _stopUpshift = false;
            }

            if (_stopDownShift)
            {
                downshiftPressed = false;
                _stopDownShift = false;
            }

            if (upshiftPressed)
            {
                _stopUpshift = true;
            }

            if (downshiftPressed)
            {
                _stopDownShift = true;
            }

            if (inputInherit)
            {
                InheritInputOneShot();
            }

            StaticStateLogger.Log($"VehicleParent:transform.position={transform.position}");
            StaticStateLogger.Log($"VehicleParent:transform.rotation={transform.rotation}");
            StaticStateLogger.Log($"VehicleParent:_rb.velocity={_rb.velocity}");
            StaticStateLogger.Log($"VehicleParent:_rb.angularVelocity={_rb.angularVelocity}");

            if (_wheelCheckIndex >= 0 && _wheelCheckIndex < wheelGroups.Length)
            {
                wheelGroups[_wheelCheckIndex].Activate();
                wheelGroups[_wheelCheckIndex == 0 ? wheelGroups.Length - 1 : _wheelCheckIndex - 1].Deactivate();
                _wheelCheckIndex++;

                if (_wheelCheckIndex == wheelGroups.Length)
                    _wheelCheckIndex = 0;
            }

            GetGroundedWheels();

            if (groundedWheels > 0)
            {
                crashing = false;
            }

            localVelocity = transform.InverseTransformDirection(_rb.velocity - _wheelContactsVelocity);
            localAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);
            velMag = _rb.velocity.magnitude;
            sqrVelMag = _rb.velocity.sqrMagnitude;
            forwardDir = transform.forward;
            rightDir = transform.right;
            upDir = transform.up;
            forwardDot = Vector3.Dot(forwardDir, GlobalControl.worldUpDir);
            rightDot = Vector3.Dot(rightDir, GlobalControl.worldUpDir);
            upDot = Vector3.Dot(upDir, GlobalControl.worldUpDir);

            if (norm == null)
            {
                Debug.Log("Why?");
            }

            norm.transform.position = transform.position;
            norm.transform.rotation = Quaternion.LookRotation(groundedWheels == 0 ? upDir : wheelNormalAverage, forwardDir);

            // Check if performing a burnout
            if (groundedWheels > 0 && !hover && !accelAxisIsBrake && burnoutThreshold >= 0 && accelInput > burnoutThreshold && brakeInput > burnoutThreshold)
            {
                burnout = Mathf.Lerp(burnout, ((5 - Mathf.Min(5, Mathf.Abs(localVelocity.z))) / 5) * Mathf.Abs(accelInput), _vm.tickDelta * (1 - burnoutSmoothness) * 10);
            }
            else if (burnout > 0.01f)
            {
                burnout = Mathf.Lerp(burnout, 0, _vm.tickDelta * (1 - burnoutSmoothness) * 10);
            }
            else
            {
                burnout = 0;
            }

            if (engine)
            {
                burnout *= engine.health;
            }

            // Check if reversing
            if (brakeIsReverse && brakeInput > 0 && localVelocity.z < 1 && burnout == 0)
            {
                reversing = true;
            }
            else if (localVelocity.z >= 0 || burnout > 0)
            {
                reversing = false;
            }
        }

        // Set accel input
        public void SetAccel(float f)
        {
            f = Mathf.Clamp(f, -1, 1);
            accelInput = f;
        }

        // Set brake input
        public void SetBrake(float f)
        {
            brakeInput = accelAxisIsBrake ? -Mathf.Clamp(accelInput, -1, 0) : Mathf.Clamp(f, -1, 1);
        }

        // Set steer input
        public void SetSteer(float f)
        {
            steerInput = Mathf.Clamp(f, -1, 1);
        }

        // Set ebrake input
        public void SetEbrake(float f)
        {
            if ((f > 0 || ebrakeInput > 0) && holdEbrakePark && velMag < 1 && accelInput == 0 && (brakeInput == 0 || !brakeIsReverse))
            {
                ebrakeInput = 1;
            }
            else
            {
                ebrakeInput = Mathf.Clamp01(f);
            }
        }

        // Set boost input
        public void SetBoost(bool b)
        {
            boostButton = b;
        }

        // Set pitch rotate input
        public void SetPitch(float f)
        {
            pitchInput = Mathf.Clamp(f, -1, 1);
        }

        // Set yaw rotate input
        public void SetYaw(float f)
        {
            yawInput = Mathf.Clamp(f, -1, 1);
        }

        // Set roll rotate input
        public void SetRoll(float f)
        {
            rollInput = Mathf.Clamp(f, -1, 1);
        }

        // Do upshift input
        public void PressUpshift()
        {
            upshiftPressed = true;
        }

        // Do downshift input
        public void PressDownshift()
        {
            downshiftPressed = true;
        }

        // Set held upshift input
        public void SetUpshift(float f)
        {
            upshiftHold = f;
        }

        // Set held downshift input
        public void SetDownshift(float f)
        {
            downshiftHold = f;
        }

        private void OnDestroy()
        {
            if (norm)
            {
                Destroy(norm.gameObject);
            }

            if (sparks)
            {
                Destroy(sparks.gameObject);
            }
        }

        private void Awake()
        {
            // Create normal orientation object
            GameObject normTemp = new GameObject(transform.name + "'s Normal Orientation");
            norm = normTemp.transform;

            _rb = GetComponent<Rigidbody>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -140);
        }

        private void Start()
        {

            SetCenterOfMass();

            // Instantiate tow vehicle
            if (towVehicle)
            {
                _newTow = Instantiate(towVehicle, Vector3.zero, transform.rotation) as GameObject;
                _newTow.SetActive(false);
                _newTow.transform.position = transform.TransformPoint(_newTow.GetComponent<Joint>().connectedAnchor - _newTow.GetComponent<Joint>().anchor);
                _newTow.GetComponent<Joint>().connectedBody = _rb;
                _newTow.SetActive(true);
                _newTow.GetComponent<VehicleParent>().inputInherit = this;
            }

            if (sparks)
            {
                sparks.transform.parent = null;
            }
        }

        // Copy input from other vehicle
        private void InheritInput()
        {
            accelInput = inputInherit.accelInput;
            brakeInput = inputInherit.brakeInput;
            steerInput = inputInherit.steerInput;
            ebrakeInput = inputInherit.ebrakeInput;
            pitchInput = inputInherit.pitchInput;
            yawInput = inputInherit.yawInput;
            rollInput = inputInherit.rollInput;
        }

        // Copy single-frame input from other vehicle
        private void InheritInputOneShot()
        {
            upshiftPressed = inputInherit.upshiftPressed;
            downshiftPressed = inputInherit.downshiftPressed;
        }

        // Change the center of mass of the vehicle
        private void SetCenterOfMass()
        {
            float susAverage = 0;

            // Get average suspension height
            if (suspensionCenterOfMass)
            {
                if (hover)
                {
                    for (int i = 0; i < hoverWheels.Length; i++)
                    {
                        susAverage = i == 0 ? hoverWheels[i].hoverDistance : (susAverage + hoverWheels[i].hoverDistance) * 0.5f;
                    }
                }
                else
                {
                    for (int i = 0; i < wheels.Length; i++)
                    {
                        float newSusDist = wheels[i].transform.parent.GetComponent<Suspension>().suspensionDistance;
                        susAverage = i == 0 ? newSusDist : (susAverage + newSusDist) * 0.5f;
                    }
                }
            }

            _rb.centerOfMass = centerOfMassOffset + new Vector3(0, -susAverage, 0);
            _rb.inertiaTensor = _rb.inertiaTensor; // This is required due to decoupling of inertia tensor from center of mass in Unity 5.3
        }

        // Get the number of grounded wheels and the normals and velocities of surfaces they're sitting on
        private void GetGroundedWheels()
        {
            groundedWheels = 0;
            _wheelContactsVelocity = Vector3.zero;

            if (hover)
            {
                for (int i = 0; i < hoverWheels.Length; i++)
                {
                    if (hoverWheels[i].grounded)
                    {
                        wheelNormalAverage = i == 0 ? hoverWheels[i].contactPoint.normal : (wheelNormalAverage + hoverWheels[i].contactPoint.normal).normalized;
                    }

                    if (hoverWheels[i].grounded)
                    {
                        groundedWheels++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i].grounded)
                    {
                        _wheelContactsVelocity = i == 0 ? wheels[i].contactVelocity : (_wheelContactsVelocity + wheels[i].contactVelocity) * 0.5f;
                        wheelNormalAverage = i == 0 ? wheels[i].contactPoint.normal : (wheelNormalAverage + wheels[i].contactPoint.normal).normalized;
                    }

                    if (wheels[i].grounded)
                    {
                        groundedWheels++;
                    }
                }
            }
        }

        // Check for crashes and play collision sounds
        private void OnCollisionEnter(Collision col)
        {
            if (col.contacts.Length > 0 && groundedWheels == 0)
            {
                foreach (ContactPoint curCol in col.contacts)
                {
                    if (!curCol.thisCollider.CompareTag("Underside") && curCol.thisCollider.gameObject.layer != GlobalControl.ignoreWheelCastLayer)
                    {
                        if (Vector3.Dot(curCol.normal, col.relativeVelocity.normalized) > 0.2f && col.relativeVelocity.sqrMagnitude > 20)
                        {
                            bool checkTow = true;
                            if (_newTow)
                            {
                                checkTow = !curCol.otherCollider.transform.IsChildOf(_newTow.transform);
                            }

                            if (checkTow)
                            {
                                crashing = canCrash;

                                if (crashSnd && crashClips.Length > 0 && playCrashSounds)
                                {
                                    crashSnd.PlayOneShot(crashClips[Random.Range(0, crashClips.Length)], Mathf.Clamp01(col.relativeVelocity.magnitude * 0.1f));
                                }

                                if (sparks && playCrashSparks)
                                {
                                    sparks.transform.position = curCol.point;
                                    sparks.transform.rotation = Quaternion.LookRotation(col.relativeVelocity.normalized, curCol.normal);
                                    sparks.Play();
                                }
                            }
                        }
                    }
                }
            }
        }

        // Continuous collision checking
        private void OnCollisionStay(Collision col)
        {
            if (col.contacts.Length > 0 && groundedWheels == 0)
            {
                foreach (ContactPoint curCol in col.contacts)
                {
                    if (!curCol.thisCollider.CompareTag("Underside") && curCol.thisCollider.gameObject.layer != GlobalControl.ignoreWheelCastLayer)
                    {
                        if (col.relativeVelocity.sqrMagnitude < 5)
                        {
                            bool checkTow = true;

                            if (_newTow)
                            {
                                checkTow = !curCol.otherCollider.transform.IsChildOf(_newTow.transform);
                            }

                            if (checkTow)
                            {
                                crashing = canCrash;
                            }
                        }
                    }
                }
            }
        }
    }

    // Class for groups of wheels to check each FixedUpdate
    [System.Serializable]
    public class WheelCheckGroup
    {
        public Wheel[] wheels;
        public HoverWheel[] hoverWheels;

        public void Activate()
        {
            foreach (Wheel curWheel in wheels)
            {
                curWheel.getContact = true;
            }

            foreach (HoverWheel curHover in hoverWheels)
            {
                curHover.getContact = true;
            }
        }

        public void Deactivate()
        {
            foreach (Wheel curWheel in wheels)
            {
                curWheel.getContact = false;
            }

            foreach (HoverWheel curHover in hoverWheels)
            {
                curHover.getContact = false;
            }
        }
    }
}