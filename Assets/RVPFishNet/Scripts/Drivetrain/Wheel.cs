using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(DriveForce))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Drivetrain/Wheel", 1)]
    public class Wheel : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Visual object for wheel rim")]
        public Transform visualRim;

        [Tooltip("Collision object for wheel rim")]
        public Transform rim;

        [Tooltip("Generate a sphere collider to represent the wheel for side collisions")]
        public bool generateHardCollider = true;

        [Header("Rotation")]
        [Tooltip("Bias for feedback RPM lerp between target RPM and raw RPM")]
        [Range(0, 1)]
        public float feedbackRpmBias;

        [Tooltip("Curve for setting final RPM of wheel based on driving torque/brake force, x-axis = torque/brake force, y-axis = lerp between raw RPM and target RPM")]
        public AnimationCurve rpmBiasCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("As the RPM of the wheel approaches this value, the RPM bias curve is interpolated with the default linear curve")]
        public float rpmBiasCurveLimit = Mathf.Infinity;

        [Tooltip("")]
        [Range(0, 10)]
        public float axleFriction;

        [Header("Friction")]
        [Tooltip("")]
        [Range(0, 1)]
        public float frictionSmoothness = 0.5f;

        [Tooltip("")]
        public float forwardFriction = 1;

        [Tooltip("")]
        public float sidewaysFriction = 1;

        [Tooltip("")]
        public float forwardRimFriction = 0.5f;

        [Tooltip("")]
        public float sidewaysRimFriction = 0.5f;

        [Tooltip("")]
        public float forwardCurveStretch = 1;
        
        [Tooltip("")]
        public float sidewaysCurveStretch = 1;

        [Tooltip("X-axis = slip, y-axis = friction")]
        public AnimationCurve forwardFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("X-axis = slip, y-axis = friction")]
        public AnimationCurve sidewaysFrictionCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("")]
        public SlipDependenceMode slipDependence = SlipDependenceMode.Sideways;

        [Range(0, 2)]
        [Tooltip("")]
        public float forwardSlipDependence = 2;

        [Range(0, 2)]
        [Tooltip("")]
        public float sidewaysSlipDependence = 2;

        [Tooltip("Adjusts how much friction the wheel has based on the normal of the ground surface. X-axis = normal dot product, y-axis = friction multiplier")]
        public AnimationCurve normalFrictionCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [Tooltip("How much the suspension compression affects the wheel friction")]
        [Range(0, 1)]
        public float compressionFrictionFactor = 0.5f;

        [Header("Size")]
        [Tooltip("")]
        public float tireRadius;

        [Tooltip("")]
        public float rimRadius;

        [Tooltip("")]
        public float tireWidth;

        [Tooltip("")]
        public float rimWidth;

        [Header("Tire")]
        [Tooltip("")]
        [Range(0, 1)]
        public float tirePressure = 1;

        [Tooltip("")]
        public bool popped;

        [Tooltip("")]
        public bool canPop;

        [Tooltip("Requires deform shader")]
        public float deformAmount;

        [Tooltip("")]
        [Range(0, 1)]
        public float rimGlow;

        [Tooltip("Apply friction forces at ground point")]
        public bool applyForceAtGroundContact;

        // Point at which friction forces are applied
        [Header("Audio")]
        [Tooltip("")]
        public AudioSource impactSnd;

        [Tooltip("")]
        public AudioClip[] tireHitClips;

        [Tooltip("")]
        public AudioClip rimHitClip;

        [Tooltip("")]
        public AudioClip tireAirClip;

        [Tooltip("")]
        public AudioClip tirePopClip;

        [Header("Damage")]
        [Tooltip("")]
        public float detachForce = Mathf.Infinity;

        [Tooltip("")]
        public float mass = 0.05f;

        [Tooltip("")] 
        public Mesh tireMeshLoose;

        // Tire mesh for detached wheel collider
        [Tooltip("")] 
        public Mesh rimMeshLoose;

        [Tooltip("")] 
        public PhysicMaterial detachedTireMaterial;
        
        [Tooltip("")] 
        public PhysicMaterial detachedRimMaterial;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public WheelContact contactPoint = new WheelContact();

        [System.NonSerialized]
        public DriveForce targetDrive;

        [System.NonSerialized]
        public Suspension suspensionParent;

        [System.NonSerialized]
        public float forwardSlip;

        [System.NonSerialized]
        public float sidewaysSlip;

        [System.NonSerialized]
        public float actualRadius;

        [System.NonSerialized]
        public bool updatedSize;

        [System.NonSerialized]
        public bool updatedPopped;

        [System.NonSerialized]
        public float rawRPM;

        [System.NonSerialized]
        public bool getContact = true;

        [System.NonSerialized]
        public bool grounded;

        [System.NonSerialized]
        public float travelDist;

        [System.NonSerialized]
        public Vector3 contactVelocity;

        [System.NonSerialized]
        public float damage;

        [System.NonSerialized]
        public bool connected = true;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private Transform _tire;
        private Vector3 _localVel;
        private SphereCollider _sphereCol; // Hard collider
        private Transform _sphereColTr; // Hard collider transform
        private float _initialTirePressure;
        private Material _rimMat;
        private Material _tireMat;
        private float _glowAmount;
        private Color _glowColor;
        private float _airTime;        
        private float _circumference; // Up direction       
        private float _actualEbrake;  // Velocity of contact point
        private float _actualTargetRPM;
        private float _actualTorque;        
        private GameObject _detachedWheel; // Rim mesh for detached wheel collider
        private GameObject _detachedTire;
        private MeshCollider _detachedCol;
        private Rigidbody _detachedBody;
        private MeshFilter _detachFilter;
        private MeshFilter _detachTireFilter;
        private Vector3 _frictionForce = Vector3.zero;
        private float _setTireWidth;
        private float _tireWidthPrev;
        private float _setTireRadius;
        private float _tireRadiusPrev;
        private float _setRimWidth;
        private float _rimWidthPrev;
        private float _setRimRadius;
        private float _rimRadiusPrev;
        private float _setTirePressure;
        private float _tirePressurePrev;
        private bool _setPopped;
        private bool _poppedPrev;
        private float _airLeakTime = -1;
        private float _currentRPM;
        private Vector3 _upDir;
        private Vector3 _forceApplicationPoint;
        private bool _canDetach;

        public enum SlipDependenceMode
        { 
            Dependent, 
            Forward, 
            Sideways, 
            Independent 
        };

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            contactPoint.GetFullState(writer);
            targetDrive.GetFullState(writer);

            writer.WriteVector3(transform.localPosition);
            writer.WriteQuaternion(transform.localRotation, AutoPackType.PackedLess); 
            writer.WriteVector3(rim.localPosition);
            writer.WriteQuaternion(rim.localRotation, AutoPackType.PackedLess);
            writer.WriteSingle(rawRPM);
            writer.WriteBoolean(getContact);
            writer.WriteBoolean(grounded);
            writer.WriteBoolean(popped);
            writer.WriteSingle(travelDist);
            writer.WriteVector3(contactVelocity);
            writer.WriteSingle(damage);
            writer.WriteSingle(actualRadius);
            writer.WriteSingle(tirePressure);

            writer.WriteVector3(_frictionForce);
            writer.WriteSingle(_airLeakTime);
            writer.WriteVector3(_forceApplicationPoint);
            writer.WriteSingle(_airTime);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            contactPoint.SetFullState(reader);
            targetDrive.SetFullState(reader);

            transform.localPosition = reader.ReadVector3();
            transform.localRotation = reader.ReadQuaternion(AutoPackType.PackedLess);
            rim.localPosition = reader.ReadVector3();
            rim.localRotation = reader.ReadQuaternion(AutoPackType.PackedLess);
            rawRPM = reader.ReadSingle();
            getContact = reader.ReadBoolean();
            grounded = reader.ReadBoolean();
            popped = reader.ReadBoolean();
            travelDist = reader.ReadSingle();
            contactVelocity = reader.ReadVector3();
            damage = reader.ReadSingle();
            actualRadius = reader.ReadSingle();
            tirePressure = reader.ReadSingle();

            _frictionForce = reader.ReadVector3();
            _airLeakTime = reader.ReadSingle();
            _forceApplicationPoint = reader.ReadVector3();
            _airTime = reader.ReadSingle();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
            writer.WriteVector3(transform.localPosition);
            writer.WriteQuaternion(transform.localRotation);
            writer.WriteVector3(rim.localPosition);
            writer.WriteQuaternion(rim.localRotation);
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
            transform.localPosition = reader.ReadVector3();
            transform.localRotation = reader.ReadQuaternion();
            rim.localPosition = reader.ReadVector3();
            rim.localRotation = reader.ReadQuaternion();
        }

        void IVehicleComponent.Simulate()
        {
            _upDir = transform.up;
            actualRadius = popped ? rimRadius : Mathf.Lerp(rimRadius, tireRadius, tirePressure);
            _circumference = Mathf.PI * actualRadius * 2;
            _localVel = _rb.GetPointVelocity(_forceApplicationPoint);

            // Get proper inputs
            _actualEbrake = suspensionParent.ebrakeEnabled ? suspensionParent.ebrakeForce : 0;
            _actualTargetRPM = targetDrive.rpm * (suspensionParent.driveInverted ? -1 : 1);
            _actualTorque = suspensionParent.driveEnabled ? Mathf.Lerp(targetDrive.torque, Mathf.Abs(_vp.accelInput), _vp.burnout) : 0;

            if (getContact)
            {
                GetWheelContact();
            }
            else if (grounded)
            {
                contactPoint.point += _localVel * _vm.tickDelta;
            }

            _airTime = grounded ? 0 : _airTime + _vm.tickDelta;
            _forceApplicationPoint = applyForceAtGroundContact ? contactPoint.point : transform.position;

            if (connected)
            {
                GetRawRPM();
                ApplyDrive();
            }
            else
            {
                rawRPM = 0;
                _currentRPM = 0;
                targetDrive.feedbackRPM = 0;
            }

            // Get travel distance
            travelDist = suspensionParent.compression < travelDist || grounded ? suspensionParent.compression : Mathf.Lerp(travelDist, suspensionParent.compression, suspensionParent.extendSpeed * _vm.tickDelta);

            PositionWheel();
            RotateWheel();

            if (connected)
            {
                // Update hard collider size upon changed radius or width
                if (generateHardCollider)
                {
                    _setRimWidth = rimWidth;
                    _setRimRadius = rimRadius;
                    _setTireWidth = tireWidth;
                    _setTireRadius = tireRadius;
                    _setTirePressure = tirePressure;

                    if (_rimWidthPrev != _setRimWidth || _rimRadiusPrev != _setRimRadius)
                    {
                        _sphereCol.radius = Mathf.Min(rimWidth * 0.5f, rimRadius * 0.5f);
                        updatedSize = true;
                    }
                    else if (_tireWidthPrev != _setTireWidth || _tireRadiusPrev != _setTireRadius || _tirePressurePrev != _setTirePressure)
                    {
                        updatedSize = true;
                    }
                    else
                    {
                        updatedSize = false;
                    }

                    _rimWidthPrev = _setRimWidth;
                    _rimRadiusPrev = _setRimRadius;
                    _tireWidthPrev = _setTireWidth;
                    _tireRadiusPrev = _setTireRadius;
                    _tirePressurePrev = _setTirePressure;
                }

                GetSlip();
                ApplyFriction();

                // Burnout spinning
                if (_vp.burnout > 0 && targetDrive.rpm != 0 && _actualEbrake * _vp.ebrakeInput == 0 && connected && grounded)
                {
                    var force = suspensionParent.forwardDir * -suspensionParent.flippedSideFactor * (_vp.steerInput * _vp.burnoutSpin * _currentRPM * Mathf.Min(0.1f, targetDrive.torque) * 0.001f) * _vp.burnout * (popped ? 0.5f : 1) * contactPoint.surfaceFriction;
                    var position = suspensionParent.transform.position;
                    var mode = _vp.wheelForceMode;

                    _rb.AddForceAtPosition(force, position, mode);
                }

                // Popping logic
                _setPopped = popped;

                if (_poppedPrev != _setPopped)
                {
                    if (_tire)
                    {
                        _tire.gameObject.SetActive(!popped);
                    }

                    updatedPopped = true;
                }
                else
                {
                    updatedPopped = false;
                }

                _poppedPrev = _setPopped;

                // Air leak logic
                if (_airLeakTime >= 0)
                {
                    tirePressure = Mathf.Clamp01(tirePressure - _vm.tickDelta * 0.5f);

                    if (grounded)
                    {
                        _airLeakTime += Mathf.Max(Mathf.Abs(_currentRPM) * 0.001f, _localVel.magnitude * 0.1f) * Time.timeScale * TimeMaster.inverseFixedTimeFactor;

                        if (_airLeakTime > 1000 && tirePressure == 0)
                        {
                            popped = true;
                            _airLeakTime = -1;

                            if (impactSnd && tirePopClip)
                            {
                                impactSnd.PlayOneShot(tirePopClip);
                                impactSnd.pitch = 1;
                            }
                        }
                    }
                }
            }

            StaticStateLogger.Log($"Wheel:transform.position={transform.position}");
            StaticStateLogger.Log($"Wheel:transform.rotation={transform.rotation}");
        }

        // Begin deflating the tire/leaking air
        public void Deflate()
        {
            _airLeakTime = 0;

            if (impactSnd && tireAirClip)
            {
                impactSnd.PlayOneShot(tireAirClip);
                impactSnd.pitch = 1;
            }
        }

        public void FixTire()
        {
            popped = false;
            tirePressure = _initialTirePressure;
            _airLeakTime = -1;
        }

        // Detach the wheel from the vehicle
        public void Detach()
        {
            if (connected && _canDetach)
            {
                connected = false;
                _detachedWheel.SetActive(true);
                _detachedWheel.transform.position = rim.position;
                _detachedWheel.transform.rotation = rim.rotation;
                _detachedCol.sharedMaterial = popped ? detachedRimMaterial : detachedTireMaterial;

                if (_tire)
                {
                    _detachedTire.SetActive(!popped);
                    _detachedCol.sharedMesh = _airLeakTime >= 0 || popped ? (rimMeshLoose ? rimMeshLoose : _detachFilter.sharedMesh) : (tireMeshLoose ? tireMeshLoose : _detachTireFilter.sharedMesh);
                }
                else
                {
                    _detachedCol.sharedMesh = rimMeshLoose ? rimMeshLoose : _detachFilter.sharedMesh;
                }

                _rb.mass -= mass;
                _detachedBody.velocity = _rb.GetPointVelocity(rim.position);
                _detachedBody.angularVelocity = _rb.angularVelocity;

                rim.gameObject.SetActive(false);

                if (_sphereColTr)
                {
                    _sphereColTr.gameObject.SetActive(false);
                }
            }
        }

        // Automatically sets wheel dimensions based on rim/tire meshes
        public void GetWheelDimensions(float radiusMargin, float widthMargin)
        {
            Mesh rimMesh = null;
            Mesh tireMesh = null;
            Mesh checker;
            Transform scaler = transform;

            if (transform.childCount > 0)
            {
                if (transform.GetChild(0).GetComponent<MeshFilter>())
                {
                    rimMesh = transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                    scaler = transform.GetChild(0);
                }

                if (transform.GetChild(0).childCount > 0)
                {
                    if (transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>())
                    {
                        tireMesh = transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                    }
                }

                checker = tireMesh ? tireMesh : rimMesh;

                if (checker)
                {
                    float maxWidth = 0;
                    float maxRadius = 0;

                    foreach (Vector3 curVert in checker.vertices)
                    {
                        if (new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude > maxRadius)
                        {
                            maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;
                        }

                        if (Mathf.Abs(curVert.z * scaler.localScale.z) > maxWidth)
                        {
                            maxWidth = Mathf.Abs(curVert.z * scaler.localScale.z);
                        }
                    }

                    tireRadius = maxRadius + radiusMargin;
                    tireWidth = maxWidth + widthMargin;

                    if (tireMesh && rimMesh)
                    {
                        maxWidth = 0;
                        maxRadius = 0;

                        foreach (Vector3 curVert in rimMesh.vertices)
                        {
                            if (new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude > maxRadius)
                            {
                                maxRadius = new Vector2(curVert.x * scaler.localScale.x, curVert.y * scaler.localScale.y).magnitude;
                            }

                            if (Mathf.Abs(curVert.z * scaler.localScale.z) > maxWidth)
                            {
                                maxWidth = Mathf.Abs(curVert.z * scaler.localScale.z);
                            }
                        }

                        rimRadius = maxRadius + radiusMargin;
                        rimWidth = maxWidth + widthMargin;
                    }
                    else
                    {
                        rimRadius = maxRadius * 0.5f + radiusMargin;
                        rimWidth = maxWidth * 0.5f + widthMargin;
                    }
                }
                else
                {
                    Debug.LogError("No rim or tire meshes found for getting wheel dimensions.", this);
                }
            }
        }

        // Attach the wheel back onto its vehicle if detached
        public void Reattach()
        {
            if (!connected)
            {
                connected = true;
                _detachedWheel.SetActive(false);
                _rb.mass += mass;
                rim.gameObject.SetActive(true);

                if (_sphereColTr)
                {
                    _sphereColTr.gameObject.SetActive(true);
                }
            }
        }

        private void Awake()
        {
            targetDrive = GetComponent<DriveForce>();
            suspensionParent = transform.parent.GetComponent<Suspension>();
            _rb = transform.GetTopmostParentComponent<Rigidbody>();
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -30);
        }

        private void Start()
        {            
            travelDist = suspensionParent.targetCompression;
            _canDetach = detachForce < Mathf.Infinity && Application.isPlaying;
            _initialTirePressure = tirePressure;

            if (transform.childCount > 0)
            {
                // Set up rim glow material
                if (rimGlow > 0 && Application.isPlaying)
                {
                    _rimMat = new Material(visualRim.GetComponent<MeshRenderer>().sharedMaterial);
                    _rimMat.EnableKeyword("_EMISSION");
                    visualRim.GetComponent<MeshRenderer>().sharedMaterial = _rimMat;
                }

                // Create detached wheel
                if (_canDetach)
                {
                    _detachedWheel = new GameObject(_vp.transform.name + "'s Detached Wheel");
                    _detachedWheel.layer = LayerMask.NameToLayer("Detachable Part");
                    _detachFilter = _detachedWheel.AddComponent<MeshFilter>();
                    _detachFilter.sharedMesh = visualRim.GetComponent<MeshFilter>().sharedMesh;
                    MeshRenderer detachRend = _detachedWheel.AddComponent<MeshRenderer>();
                    detachRend.sharedMaterial = visualRim.GetComponent<MeshRenderer>().sharedMaterial;
                    _detachedCol = _detachedWheel.AddComponent<MeshCollider>();
                    _detachedCol.convex = true;
                    _detachedBody = _detachedWheel.AddComponent<Rigidbody>();
                    _detachedBody.mass = mass;
                }

                // Get tire
                if (visualRim.childCount > 0)
                {
                    _tire = visualRim.GetChild(0);
                    if (deformAmount > 0 && Application.isPlaying)
                    {
                        _tireMat = new Material(_tire.GetComponent<MeshRenderer>().sharedMaterial);
                        _tire.GetComponent<MeshRenderer>().sharedMaterial = _tireMat;
                    }

                    // Create detached tire
                    if (_canDetach)
                    {
                        _detachedTire = new GameObject("Detached Tire");
                        _detachedTire.transform.parent = _detachedWheel.transform;
                        _detachedTire.transform.localPosition = Vector3.zero;
                        _detachedTire.transform.localRotation = Quaternion.identity;
                        _detachTireFilter = _detachedTire.AddComponent<MeshFilter>();
                        _detachTireFilter.sharedMesh = _tire.GetComponent<MeshFilter>().sharedMesh;
                        MeshRenderer detachTireRend = _detachedTire.AddComponent<MeshRenderer>();
                        detachTireRend.sharedMaterial = _tireMat ? _tireMat : _tire.GetComponent<MeshRenderer>().sharedMaterial;
                    }
                }

                if (Application.isPlaying)
                {
                    // Generate hard collider
                    if (generateHardCollider)
                    {
                        GameObject sphereColNew = new GameObject("Rim Collider");
                        sphereColNew.layer = GlobalControl.ignoreWheelCastLayer;
                        _sphereColTr = sphereColNew.transform;
                        _sphereCol = sphereColNew.AddComponent<SphereCollider>();
                        _sphereColTr.parent = transform;
                        _sphereColTr.localPosition = Vector3.zero;
                        _sphereColTr.localRotation = Quaternion.identity;
                        _sphereCol.radius = Mathf.Min(rimWidth * 0.5f, rimRadius * 0.5f);
                        _sphereCol.sharedMaterial = GlobalControl.frictionlessMatStatic;
                    }

                    if (_canDetach)
                    {
                        _detachedWheel.SetActive(false);
                    }
                }
            }

            _currentRPM = 0;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                PositionWheel();
            }
            else
            {
                // Update tire and rim materials
                if (deformAmount > 0 && _tireMat && connected)
                {
                    if (_tireMat.HasProperty("_DeformNormal"))
                    {
                        // Deform tire (requires deform shader)
                        Vector3 deformNormal = grounded ? contactPoint.normal * Mathf.Max(-suspensionParent.penetration * (1 - suspensionParent.compression) * 10, 1 - tirePressure) * deformAmount : Vector3.zero;
                        _tireMat.SetVector("_DeformNormal", new Vector4(deformNormal.x, deformNormal.y, deformNormal.z, 0));
                    }
                }

                if (_rimMat)
                {
                    if (_rimMat.HasProperty("_EmissionColor"))
                    {
                        // Make the rim glow
                        float targetGlow = connected && GroundSurfaceMaster.surfaceTypesStatic[contactPoint.surfaceType].leaveSparks ? Mathf.Abs(F.MaxAbs(forwardSlip, sidewaysSlip)) : 0;
                        _glowAmount = popped ? Mathf.Lerp(_glowAmount, targetGlow, (targetGlow > _glowAmount ? 2 : 0.2f) * Time.deltaTime) : 0;
                        _glowColor = new Color(_glowAmount, _glowAmount * 0.5f, 0);
                        _rimMat.SetColor("_EmissionColor", popped ? Color.Lerp(Color.black, _glowColor, _glowAmount * rimGlow) : Color.black);
                    }
                }

                visualRim.localPosition = rim.localPosition;
                visualRim.rotation = rim.rotation;
            }
        }

        // Use raycasting to find the current contact point for the wheel
        private void GetWheelContact()
        {
            float castDist = Mathf.Max(suspensionParent.suspensionDistance * Mathf.Max(0.001f, suspensionParent.targetCompression) + actualRadius, 0.001f);
            RaycastHit[] wheelHits = Physics.RaycastAll(suspensionParent.maxCompressPoint, suspensionParent.springDirection, castDist, GlobalControl.wheelCastMaskStatic);
            RaycastHit hit;
            int hitIndex = 0;
            bool validHit = false;
            float hitDist = Mathf.Infinity;

            if (connected)
            {
                // Loop through raycast hits to find closest one
                for (int i = 0; i < wheelHits.Length; i++)
                {
                    if (!wheelHits[i].transform.IsChildOf(_vp.transform) && wheelHits[i].distance < hitDist)
                    {
                        hitIndex = i;
                        hitDist = wheelHits[i].distance;
                        validHit = true;
                    }
                }
            }
            else
            {
                validHit = false;
            }

            // Set contact point variables
            if (validHit)
            {
                hit = wheelHits[hitIndex];

                if (!grounded && impactSnd && ((tireHitClips.Length > 0 && !popped) || (rimHitClip && popped)))
                {
                    impactSnd.PlayOneShot(popped ? rimHitClip : tireHitClips[Mathf.RoundToInt(Random.Range(0, tireHitClips.Length - 1))], Mathf.Clamp01(_airTime * _airTime));
                    impactSnd.pitch = Mathf.Clamp(_airTime * 0.2f + 0.8f, 0.8f, 1);
                }

                grounded = true;
                contactPoint.distance = hit.distance - actualRadius;
                contactPoint.point = hit.point + _localVel * _vm.tickDelta;
                contactPoint.grounded = true;
                contactPoint.normal = hit.normal;
                contactPoint.relativeVelocity = transform.InverseTransformDirection(_localVel);
                contactPoint.gameObject = hit.collider.gameObject;

                if (hit.collider.attachedRigidbody)
                {
                    contactVelocity = hit.collider.attachedRigidbody.GetPointVelocity(contactPoint.point);
                    contactPoint.relativeVelocity -= transform.InverseTransformDirection(contactVelocity);
                }
                else
                {
                    contactVelocity = Vector3.zero;
                }

                GroundSurfaceInstance curSurface = hit.collider.GetComponent<GroundSurfaceInstance>();
                TerrainSurface curTerrain = hit.collider.GetComponent<TerrainSurface>();

                if (curSurface)
                {
                    contactPoint.surfaceFriction = curSurface.friction;
                    contactPoint.surfaceType = curSurface.surfaceType;
                }
                else if (curTerrain)
                {
                    contactPoint.surfaceType = curTerrain.GetDominantSurfaceTypeAtPoint(contactPoint.point);
                    contactPoint.surfaceFriction = curTerrain.GetFriction(contactPoint.surfaceType);
                }
                else
                {
                    contactPoint.surfaceFriction = hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.dynamicFriction * 2 : 1.0f;
                    contactPoint.surfaceType = 0;
                }

                if (contactPoint.gameObject.CompareTag("Pop Tire") && canPop && _airLeakTime == -1 && !popped)
                {
                    Deflate();
                }
            }
            else
            {
                grounded = false;
                contactPoint.distance = suspensionParent.suspensionDistance;
                contactPoint.point = Vector3.zero;
                contactPoint.grounded = false;
                contactPoint.normal = _upDir;
                contactPoint.relativeVelocity = Vector3.zero;
                contactPoint.gameObject = null;
                contactVelocity = Vector3.zero;
                contactPoint.surfaceFriction = 0;
                contactPoint.surfaceType = 0;
            }
        }

        // Calculate what the RPM of the wheel would be based purely on its velocity
        private void GetRawRPM()
        {
            if (grounded)
            {
                rawRPM = (contactPoint.relativeVelocity.x / _circumference) * (Mathf.PI * 100) * -suspensionParent.flippedSideFactor;
            }
            else
            {
                rawRPM = Mathf.Lerp(rawRPM, _actualTargetRPM, (_actualTorque + suspensionParent.brakeForce * _vp.brakeInput + _actualEbrake * _vp.ebrakeInput) * Time.timeScale);
            }
        }

        // Calculate the current slip amount
        private void GetSlip()
        {
            if (grounded)
            {
                sidewaysSlip = (contactPoint.relativeVelocity.z * 0.1f) / sidewaysCurveStretch;
                forwardSlip = (0.01f * (rawRPM - _currentRPM)) / forwardCurveStretch;
            }
            else
            {
                sidewaysSlip = 0;
                forwardSlip = 0;
            }
        }

        // Apply actual forces to rigidbody based on wheel simulation
        private void ApplyFriction()
        {
            if (grounded)
            {
                float forwardSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 1 ? forwardSlip - sidewaysSlip : forwardSlip;
                float sidewaysSlipFactor = (int)slipDependence == 0 || (int)slipDependence == 2 ? sidewaysSlip - forwardSlip : sidewaysSlip;
                float forwardSlipDependenceFactor = Mathf.Clamp01(forwardSlipDependence - Mathf.Clamp01(Mathf.Abs(sidewaysSlip)));
                float sidewaysSlipDependenceFactor = Mathf.Clamp01(sidewaysSlipDependence - Mathf.Clamp01(Mathf.Abs(forwardSlip)));

                StaticStateLogger.Log($"Wheel:forwardSlipFactor={forwardSlipFactor:F7}");
                StaticStateLogger.Log($"Wheel:forwardSlip={forwardSlip:F7}");
                StaticStateLogger.Log($"Wheel:forwardFriction={forwardFriction:F7}");
                StaticStateLogger.Log($"Wheel:sidewaysSlipFactor={sidewaysSlipFactor:F7}");
                StaticStateLogger.Log($"Wheel:sidewaysSlip={sidewaysSlip:F7}");
                StaticStateLogger.Log($"Wheel:sidewaysFriction={sidewaysFriction:F7}");

                float targetForceX = forwardFrictionCurve.Evaluate(Mathf.Abs(forwardSlipFactor)) * -System.Math.Sign(forwardSlip) * (popped ? forwardRimFriction : forwardFriction) * forwardSlipDependenceFactor * -suspensionParent.flippedSideFactor;
                float targetForceZ = sidewaysFrictionCurve.Evaluate(Mathf.Abs(sidewaysSlipFactor)) * -System.Math.Sign(sidewaysSlip) * (popped ? sidewaysRimFriction : sidewaysFriction) * sidewaysSlipDependenceFactor *
                    normalFrictionCurve.Evaluate(Mathf.Clamp01(Vector3.Dot(contactPoint.normal, GlobalControl.worldUpDir))) *
                    (_vp.burnout > 0 && Mathf.Abs(targetDrive.rpm) != 0 && _actualEbrake * _vp.ebrakeInput == 0 && grounded ? (1 - _vp.burnout) * (1 - Mathf.Abs(_vp.accelInput)) : 1);

                Vector3 targetForce = transform.TransformDirection(targetForceX, 0, targetForceZ);
                float targetForceMultiplier = ((1 - compressionFrictionFactor) + (1 - suspensionParent.compression) * compressionFrictionFactor * Mathf.Clamp01(Mathf.Abs(suspensionParent.transform.InverseTransformDirection(_localVel).z) * 10)) * contactPoint.surfaceFriction;
                _frictionForce = Vector3.Lerp(_frictionForce, targetForce * targetForceMultiplier, 1 - frictionSmoothness);
                 _rb.AddForceAtPosition(_frictionForce, _forceApplicationPoint, _vp.wheelForceMode);

                // If resting on a rigidbody, apply opposing force to it
                if (contactPoint.gameObject?.GetComponent<Rigidbody>())
                {
                    contactPoint.gameObject.GetComponent<Rigidbody>().AddForceAtPosition(-_frictionForce, contactPoint.point, _vp.wheelForceMode);
                }
            }
        }

        // Do torque and RPM calculations/simulation
        private void ApplyDrive()
        {
            float brakeForce = 0;
            float brakeCheckValue = suspensionParent.skidSteerBrake ? _vp.localAngularVel.y : _vp.localVelocity.z;

            // Set brake force
            if (_vp.brakeIsReverse)
            {
                if (brakeCheckValue > 0)
                {
                    brakeForce = suspensionParent.brakeForce * _vp.brakeInput;
                }
                else if (brakeCheckValue <= 0)
                {
                    brakeForce = suspensionParent.brakeForce * Mathf.Clamp01(_vp.accelInput);
                }
            }
            else
            {
                brakeForce = suspensionParent.brakeForce * _vp.brakeInput;
            }

            brakeForce += axleFriction * 0.1f * (Mathf.Approximately(_actualTorque, 0) ? 1 : 0);

            if (targetDrive.rpm != 0)
            {
                brakeForce *= (1 - _vp.burnout);
            }

            // Set final RPM
            if (!suspensionParent.jammed && connected)
            {
                bool validTorque = (!(Mathf.Approximately(_actualTorque, 0) && Mathf.Abs(_actualTargetRPM) < 0.01f) && !Mathf.Approximately(_actualTargetRPM, 0)) || brakeForce + _actualEbrake * _vp.ebrakeInput > 0;

                StaticStateLogger.Log($"Wheel:rawRPM={rawRPM}");
                StaticStateLogger.Log($"Wheel:_actualTargetRPM={_actualTargetRPM}");
                StaticStateLogger.Log($"Wheel:_actualTorque={_actualTorque}");
                StaticStateLogger.Log($"Wheel:brakeForce={brakeForce}");
                StaticStateLogger.Log($"Wheel:_actualEbrake={_actualEbrake}");
                StaticStateLogger.Log($"Wheel:_vp.ebrakeInput={_vp.ebrakeInput}");
                StaticStateLogger.Log($"Wheel:validTorque={validTorque}");

                _currentRPM = Mathf.Lerp(rawRPM,
                    Mathf.Lerp(
                    Mathf.Lerp(rawRPM, _actualTargetRPM, validTorque ? EvaluateTorque(_actualTorque) : _actualTorque),
                    0, Mathf.Max(brakeForce, _actualEbrake * _vp.ebrakeInput)),
                validTorque ? EvaluateTorque(_actualTorque + brakeForce + _actualEbrake * _vp.ebrakeInput) : _actualTorque + brakeForce + _actualEbrake * _vp.ebrakeInput);

                targetDrive.feedbackRPM = Mathf.Lerp(_currentRPM, rawRPM, feedbackRpmBias);
            }
            else
            {
                _currentRPM = 0;
                targetDrive.feedbackRPM = 0;
            }
        }

        // Extra method for evaluating torque to make the ApplyDrive method more readable
        private float EvaluateTorque(float t)
        {
            float torque = Mathf.Lerp(rpmBiasCurve.Evaluate(t), t, rawRPM / (rpmBiasCurveLimit * Mathf.Sign(_actualTargetRPM)));
            return torque;
        }

        // Visual wheel positioning
        private void PositionWheel()
        {
            if (suspensionParent)
            {
                rim.position = suspensionParent.maxCompressPoint + suspensionParent.springDirection * suspensionParent.suspensionDistance * (Application.isPlaying ? travelDist : suspensionParent.targetCompression) +
                    suspensionParent.upDir * Mathf.Pow(Mathf.Max(Mathf.Abs(Mathf.Sin(suspensionParent.sideAngle * Mathf.Deg2Rad)), Mathf.Abs(Mathf.Sin(suspensionParent.casterAngle * Mathf.Deg2Rad))), 2) * actualRadius +
                    suspensionParent.pivotOffset * suspensionParent.transform.TransformDirection(Mathf.Sin(transform.localEulerAngles.y * Mathf.Deg2Rad), 0, Mathf.Cos(transform.localEulerAngles.y * Mathf.Deg2Rad))
                    - suspensionParent.pivotOffset * (Application.isPlaying ? suspensionParent.forwardDir : suspensionParent.transform.forward);
            }

            if (Application.isPlaying && generateHardCollider && connected)
            {
                _sphereColTr.position = rim.position;
            }
        }

        // Visual wheel rotation
        private void RotateWheel()
        {
            if (transform && suspensionParent)
            {
                float ackermannVal = Mathf.Sign(suspensionParent.steerAngle) == suspensionParent.flippedSideFactor ? 1 + suspensionParent.ackermannFactor : 1 - suspensionParent.ackermannFactor;

                StaticStateLogger.Log($"Wheel:suspensionParent.camberAngle={suspensionParent.camberAngle}");
                StaticStateLogger.Log($"Wheel:suspensionParent.steerAngle={suspensionParent.steerAngle}");
                StaticStateLogger.Log($"Wheel:suspensionParent.flippedSideFactor={suspensionParent.flippedSideFactor}");
                StaticStateLogger.Log($"Wheel:suspensionParent.toeAngle={suspensionParent.toeAngle}");
                StaticStateLogger.Log($"Wheel:suspensionParent.steerDegrees={suspensionParent.steerDegrees}");

                transform.localEulerAngles = new Vector3(
                    suspensionParent.camberAngle + suspensionParent.casterAngle * suspensionParent.steerAngle * suspensionParent.flippedSideFactor,
                    -suspensionParent.toeAngle * suspensionParent.flippedSideFactor + suspensionParent.steerDegrees * ackermannVal,
                    0);
            }

            if (Application.isPlaying)
            {
                rim.Rotate(Vector3.forward, _currentRPM * suspensionParent.flippedSideFactor * _vm.tickDelta);

                if (damage > 0)
                {
                    rim.localEulerAngles = new Vector3(
                        Mathf.Sin(-rim.localEulerAngles.z * Mathf.Deg2Rad) * Mathf.Clamp(damage, 0, 10),
                        Mathf.Cos(-rim.localEulerAngles.z * Mathf.Deg2Rad) * Mathf.Clamp(damage, 0, 10),
                        rim.localEulerAngles.z);
                }
                else if (rim.localEulerAngles.x != 0 || rim.localEulerAngles.y != 0)
                {
                    rim.localEulerAngles = new Vector3(0, 0, rim.localEulerAngles.z);
                }
            }
        }

        // visualize wheel
        private void OnDrawGizmosSelected()
        {
            if (transform.childCount > 0)
            {
                // Rim is the first child of this object
                rim = transform.GetChild(0);

                // Tire mesh should be first child of rim
                if (rim.childCount > 0)
                {
                    _tire = rim.GetChild(0);
                }
            }

            float tireActualRadius = Mathf.Lerp(rimRadius, tireRadius, tirePressure);

            if (tirePressure < 1 && tirePressure > 0)
            {
                Gizmos.color = new Color(1, 1, 0, popped ? 0.5f : 1);
                GizmosExtra.DrawWireCylinder(rim.position, rim.forward, tireActualRadius, tireWidth * 2);
            }

            Gizmos.color = Color.white;
            GizmosExtra.DrawWireCylinder(rim.position, rim.forward, tireRadius, tireWidth * 2);

            Gizmos.color = tirePressure == 0 || popped ? Color.green : Color.cyan;
            GizmosExtra.DrawWireCylinder(rim.position, rim.forward, rimRadius, rimWidth * 2);

            Gizmos.color = new Color(1, 1, 1, tirePressure < 1 ? 0.5f : 1);
            GizmosExtra.DrawWireCylinder(rim.position, rim.forward, tireRadius, tireWidth * 2);

            Gizmos.color = tirePressure == 0 || popped ? Color.green : Color.cyan;
            GizmosExtra.DrawWireCylinder(rim.position, rim.forward, rimRadius, rimWidth * 2);
        }

        // Destroy detached wheel
        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (_detachedWheel)
                {
                    Destroy(_detachedWheel);
                }
            }
        }
    }

    // Contact point class
    public class WheelContact
    {
        public GameObject gameObject;
        public bool grounded; // Is the contact point grounded?
        public Vector3 point; // The position of the contact point
        public Vector3 normal; // The normal of the contact point
        public Vector3 relativeVelocity; // Relative velocity between the wheel and the contact point object
        public float distance; // Distance from the suspension to the contact point minus the wheel radius
        public float surfaceFriction; // Friction of the contact surface
        public int surfaceType; // The surface type identified by the surface types array of GroundSurfaceMaster

        public void GetFullState(Writer writer)
        {
            writer.WriteGameObject(gameObject);
            writer.WriteBoolean(grounded);
            writer.WriteVector3(point);
            writer.WriteVector3(normal);
            writer.WriteVector3(relativeVelocity);
            writer.WriteSingle(distance);
            writer.WriteSingle(surfaceFriction);
            writer.WriteInt32(surfaceType);
        }

        public void SetFullState(Reader reader)
        {
            gameObject = reader.ReadGameObject();
            grounded = reader.ReadBoolean();
            point = reader.ReadVector3();
            normal = reader.ReadVector3();
            relativeVelocity = reader.ReadVector3();
            distance = reader.ReadSingle();
            surfaceFriction = reader.ReadSingle();
            surfaceType = reader.ReadInt32();
        }
    }
}