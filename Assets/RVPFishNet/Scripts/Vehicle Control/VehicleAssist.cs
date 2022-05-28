using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Assist", 1)]
    public class VehicleAssist : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Header("Drift")]
        [Tooltip("Variables are multiplied based on the number of wheels grounded out of the total number of wheels")]
        public bool basedOnWheelsGrounded;

        [Tooltip("How much to assist with spinning while drifting")]
        public float driftSpinAssist;

        [Tooltip("")]
        public float driftSpinSpeed;

        [Tooltip("")]
        public float driftSpinExponent = 1;

        [Tooltip("Automatically adjust drift angle based on steer input magnitude")]
        public bool autoSteerDrift;

        [Tooltip("")]
        public float maxDriftAngle = 70;

        [Tooltip("Adjusts the force based on drift speed, x-axis = speed, y-axis = force")]
        public AnimationCurve driftSpinCurve = AnimationCurve.Linear(0, 0, 10, 1);

        [Tooltip("How much to push the vehicle forward while drifting")]
        public float driftPush;

        [Tooltip("Straighten out the vehicle when sliding slightly")]
        public bool straightenAssist;

        [Header("Downforce")]
        public float downforce = 1;

        [Tooltip("")]
        public bool invertDownforceInReverse;

        [Tooltip("")]
        public bool applyDownforceInAir;

        [Tooltip("X-axis = speed, y-axis = force")]
        public AnimationCurve downforceCurve = AnimationCurve.Linear(0, 0, 20, 1);

        [Header("Roll Over")]
        [Tooltip("Automatically roll over when rolled over")]
        public bool autoRollOver;

        [Tooltip("Roll over with steer input")]
        public bool steerRollOver;

        [Tooltip("Distance to check on sides to see if rolled over")]
        public float rollCheckDistance = 1;

        [Tooltip("")]
        public float rollOverForce = 1;

        [Tooltip("Maximum speed at which vehicle can be rolled over with assists")]
        public float rollSpeedThreshold;

        [Header("Air")]
        [Tooltip("Increase angular drag immediately after jumping")]
        public bool angularDragOnJump;

        [Tooltip("")]
        public float fallSpeedLimit = Mathf.Infinity;

        [Tooltip("")]
        public bool applyFallLimitUpwards;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public bool rolledOver;

        [System.NonSerialized]
        public float angDragTime = 0;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private float _groundedFactor;
        private float _targetDriftAngle;
        private float _initialAngularDrag;

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            writer.WriteSingle(_rb.angularDrag);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            _rb.angularDrag = reader.ReadSingle();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            if (_vp.groundedWheels > 0)
            {
                _groundedFactor = basedOnWheelsGrounded ? _vp.groundedWheels / (_vp.hover ? _vp.hoverWheels.Length : _vp.wheels.Length) : 1;

                angDragTime = 20;
                _rb.angularDrag = _initialAngularDrag;

                if (driftSpinAssist > 0)
                {
                    ApplySpinAssist();
                }

                if (driftPush > 0)
                {
                    ApplyDriftPush();
                }
            }
            else
            {
                if (angularDragOnJump)
                {
                    angDragTime = Mathf.Max(0, angDragTime - Time.timeScale * TimeMaster.inverseFixedTimeFactor);
                    _rb.angularDrag = angDragTime > 0 && _vp.upDot > 0.5 ? 10 : _initialAngularDrag;
                }
            }

            if (downforce > 0)
            {
                ApplyDownforce();
            }

            if (autoRollOver || steerRollOver)
            {
                RollOver();
            }

            if (Mathf.Abs(_vp.localVelocity.y) > fallSpeedLimit && (_vp.localVelocity.y < 0 || applyFallLimitUpwards))
            {
                _rb.AddRelativeForce(Vector3.down * _vp.localVelocity.y, ForceMode.Acceleration);
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -130);
        }

        private void Start()
        {
            _initialAngularDrag = _rb.angularDrag;
        }

        // Apply assist for steering and drifting
        private void ApplySpinAssist()
        {
            // Get desired rotation speed
            float targetTurnSpeed = 0;

            // Auto steer drift
            if (autoSteerDrift)
            {
                int steerSign = 0;
                if (_vp.steerInput != 0)
                {
                    steerSign = (int)Mathf.Sign(_vp.steerInput);
                }

                _targetDriftAngle = (steerSign != Mathf.Sign(_vp.localVelocity.x) ? _vp.steerInput : steerSign) * -maxDriftAngle;
                Vector3 velDir = new Vector3(_vp.localVelocity.x, 0, _vp.localVelocity.z).normalized;
                Vector3 targetDir = new Vector3(Mathf.Sin(_targetDriftAngle * Mathf.Deg2Rad), 0, Mathf.Cos(_targetDriftAngle * Mathf.Deg2Rad)).normalized;
                Vector3 driftTorqueTemp = velDir - targetDir;
                targetTurnSpeed = driftTorqueTemp.magnitude * Mathf.Sign(driftTorqueTemp.z) * steerSign * driftSpinSpeed - _vp.localAngularVel.y * Mathf.Clamp01(Vector3.Dot(velDir, targetDir)) * 2;
            }
            else
            {
                targetTurnSpeed = _vp.steerInput * driftSpinSpeed * (_vp.localVelocity.z < 0 ? (_vp.accelAxisIsBrake ? Mathf.Sign(_vp.accelInput) : Mathf.Sign(F.MaxAbs(_vp.accelInput, -_vp.brakeInput))) : 1);
            }

            _rb.AddRelativeTorque(
                new Vector3(0, (targetTurnSpeed - _vp.localAngularVel.y) * driftSpinAssist * driftSpinCurve.Evaluate(Mathf.Abs(Mathf.Pow(_vp.localVelocity.x, driftSpinExponent))) * _groundedFactor, 0),
                ForceMode.Acceleration);

            float rightVelDot = Vector3.Dot(transform.right, _rb.velocity.normalized);

            if (straightenAssist && _vp.steerInput == 0 && Mathf.Abs(rightVelDot) < 0.1f && _vp.sqrVelMag > 5)
            {
                _rb.AddRelativeTorque(
                    new Vector3(0, rightVelDot * 100 * Mathf.Sign(_vp.localVelocity.z) * driftSpinAssist, 0),
                    ForceMode.Acceleration);
            }
        }

        // Apply downforce
        private void ApplyDownforce()
        {
            if (_vp.groundedWheels > 0 || applyDownforceInAir)
            {
                _rb.AddRelativeForce(
                    new Vector3(0, downforceCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z)) * -downforce * (applyDownforceInAir ? 1 : _groundedFactor) * (invertDownforceInReverse ? Mathf.Sign(_vp.localVelocity.z) : 1), 0),
                    ForceMode.Acceleration);

                // Reverse downforce
                if (invertDownforceInReverse && _vp.localVelocity.z < 0)
                {
                    _rb.AddRelativeTorque(
                        new Vector3(downforceCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z)) * downforce * (applyDownforceInAir ? 1 : _groundedFactor), 0, 0),
                        ForceMode.Acceleration);
                }
            }
        }

        // Assist with rolling back over if upside down or on side
        private void RollOver()
        {
            RaycastHit rollHit;

            // Check if rolled over
            if (_vp.groundedWheels == 0 && _vp.velMag < rollSpeedThreshold && _vp.upDot < 0.8 && rollCheckDistance > 0)
            {
                if (Physics.Raycast(transform.position, _vp.upDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic)
                    || Physics.Raycast(transform.position, _vp.rightDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic)
                    || Physics.Raycast(transform.position, -_vp.rightDir, out rollHit, rollCheckDistance, GlobalControl.groundMaskStatic))
                {
                    rolledOver = true;
                }
                else
                {
                    rolledOver = false;
                }
            }
            else
            {
                rolledOver = false;
            }

            // Apply roll over force
            if (rolledOver)
            {
                if (steerRollOver && _vp.steerInput != 0)
                {
                    _rb.AddRelativeTorque(
                        new Vector3(0, 0, -_vp.steerInput * rollOverForce),
                        ForceMode.Acceleration);
                }
                else if (autoRollOver)
                {
                    _rb.AddRelativeTorque(
                        new Vector3(0, 0, -Mathf.Sign(_vp.rightDot) * rollOverForce),
                        ForceMode.Acceleration);
                }
            }
        }

        // Assist for accelerating while drifting
        private void ApplyDriftPush()
        {
            float pushFactor = (_vp.accelAxisIsBrake ? _vp.accelInput : _vp.accelInput - _vp.brakeInput) * Mathf.Abs(_vp.localVelocity.x) * driftPush * _groundedFactor * (1 - Mathf.Abs(Vector3.Dot(_vp.forwardDir, _rb.velocity.normalized)));

            var force = _vp.norm.TransformDirection(new Vector3(Mathf.Abs(pushFactor) * Mathf.Sign(_vp.localVelocity.x), Mathf.Abs(pushFactor) * Mathf.Sign(_vp.localVelocity.z), 0));

            _rb.AddForce(
                force,
                ForceMode.Acceleration);
        }
    }
}