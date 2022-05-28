using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Stunt/Flip Control", 2)]
    public class FlipControl : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public bool disableDuringCrash;

        [Tooltip("")]
        public Vector3 flipPower;

        [Tooltip("Continue spinning if input is stopped")]
        public bool freeSpinFlip;

        [Tooltip("Stop spinning if input is stopped and vehicle is upright")]
        public bool stopFlip;

        [Tooltip("How quickly the vehicle will rotate upright in air")]
        public Vector3 rotationCorrection;

        [Tooltip("Distance to check for ground for reference normal for rotation correction")]
        public float groundCheckDistance = 100;

        [Tooltip("Minimum dot product between ground normal and global up direction for rotation correction")]
        public float groundSteepnessLimit = 0.5f;

        [Tooltip("How quickly the vehicle will dive in the direction it's soaring")]
        public float diveFactor;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private Quaternion _velDir;

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            if (_vp.groundedWheels == 0 && (!_vp.crashing || (_vp.crashing && !disableDuringCrash)))
            {
                _velDir = Quaternion.LookRotation(GlobalControl.worldUpDir, _rb.velocity);

                if (flipPower != Vector3.zero)
                {
                    ApplyFlip();
                }

                if (stopFlip)
                {
                    ApplyStopFlip();
                }

                if (rotationCorrection != Vector3.zero)
                {
                    ApplyRotationCorrection();
                }

                if (diveFactor > 0)
                {
                    Dive();
                }
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, 10);
        }

        // Apply flip forces
        private void ApplyFlip()
        {
            Vector3 flipTorque;

            if (freeSpinFlip)
            {
                flipTorque = new Vector3(
                    _vp.pitchInput * flipPower.x,
                    _vp.yawInput * flipPower.y,
                    _vp.rollInput * flipPower.z
                    );
            }
            else
            {
                flipTorque = new Vector3(
                    _vp.pitchInput != 0 && Mathf.Abs(_vp.localAngularVel.x) > 1 && System.Math.Sign(_vp.pitchInput * Mathf.Sign(flipPower.x)) != System.Math.Sign(_vp.localAngularVel.x) ? -_vp.localAngularVel.x * Mathf.Abs(flipPower.x) : _vp.pitchInput * flipPower.x - _vp.localAngularVel.x * (1 - Mathf.Abs(_vp.pitchInput)) * Mathf.Abs(flipPower.x),
                    _vp.yawInput != 0 && Mathf.Abs(_vp.localAngularVel.y) > 1 && System.Math.Sign(_vp.yawInput * Mathf.Sign(flipPower.y)) != System.Math.Sign(_vp.localAngularVel.y) ? -_vp.localAngularVel.y * Mathf.Abs(flipPower.y) : _vp.yawInput * flipPower.y - _vp.localAngularVel.y * (1 - Mathf.Abs(_vp.yawInput)) * Mathf.Abs(flipPower.y),
                    _vp.rollInput != 0 && Mathf.Abs(_vp.localAngularVel.z) > 1 && System.Math.Sign(_vp.rollInput * Mathf.Sign(flipPower.z)) != System.Math.Sign(_vp.localAngularVel.z) ? -_vp.localAngularVel.z * Mathf.Abs(flipPower.z) : _vp.rollInput * flipPower.z - _vp.localAngularVel.z * (1 - Mathf.Abs(_vp.rollInput)) * Mathf.Abs(flipPower.z)
                    );
            }

            _rb.AddRelativeTorque(flipTorque, ForceMode.Acceleration);
        }

        // Counteract flipping with forces
        private void ApplyStopFlip()
        {
            Vector3 stopFlipFactor = Vector3.zero;

            stopFlipFactor.x = _vp.pitchInput * flipPower.x == 0 ? Mathf.Pow(Mathf.Clamp01(_vp.upDot), Mathf.Clamp(10 - Mathf.Abs(_vp.localAngularVel.x), 2, 10)) * 10 : 0;
            stopFlipFactor.y = _vp.yawInput * flipPower.y == 0 && _vp.sqrVelMag > 5 ? Mathf.Pow(Mathf.Clamp01(Vector3.Dot(_vp.forwardDir, _velDir * Vector3.up)), Mathf.Clamp(10 - Mathf.Abs(_vp.localAngularVel.y), 2, 10)) * 10 : 0;
            stopFlipFactor.z = _vp.rollInput * flipPower.z == 0 ? Mathf.Pow(Mathf.Clamp01(_vp.upDot), Mathf.Clamp(10 - Mathf.Abs(_vp.localAngularVel.z), 2, 10)) * 10 : 0;

            _rb.AddRelativeTorque(new Vector3(-_vp.localAngularVel.x * stopFlipFactor.x, -_vp.localAngularVel.y * stopFlipFactor.y, -_vp.localAngularVel.z * stopFlipFactor.z), ForceMode.Acceleration);
        }

        // Apply forces to align vehicle with normal of ground surface that it will land on
        private void ApplyRotationCorrection()
        {
            float actualForwardDot = _vp.forwardDot;
            float actualRightDot = _vp.rightDot;
            float actualUpDot = _vp.upDot;

            if (groundCheckDistance > 0)
            {
                RaycastHit groundHit;

                if (Physics.Raycast(transform.position, (-GlobalControl.worldUpDir + _rb.velocity).normalized, out groundHit, groundCheckDistance, GlobalControl.groundMaskStatic))
                {
                    if (Vector3.Dot(groundHit.normal, GlobalControl.worldUpDir) >= groundSteepnessLimit)
                    {
                        actualForwardDot = Vector3.Dot(_vp.forwardDir, groundHit.normal);
                        actualRightDot = Vector3.Dot(_vp.rightDir, groundHit.normal);
                        actualUpDot = Vector3.Dot(_vp.upDir, groundHit.normal);
                    }
                }
            }

            _rb.AddRelativeTorque(new Vector3(
                _vp.pitchInput * flipPower.x == 0 ? actualForwardDot * (1 - Mathf.Abs(actualRightDot)) * rotationCorrection.x - _vp.localAngularVel.x * Mathf.Pow(actualUpDot, 2) * 10 : 0,
                _vp.yawInput * flipPower.y == 0 && _vp.sqrVelMag > 10 ? Vector3.Dot(_vp.forwardDir, _velDir * Vector3.right) * Mathf.Abs(actualUpDot) * rotationCorrection.y - _vp.localAngularVel.y * Mathf.Pow(actualUpDot, 2) * 10 : 0,
                _vp.rollInput * flipPower.z == 0 ? -actualRightDot * (1 - Mathf.Abs(actualForwardDot)) * rotationCorrection.z - _vp.localAngularVel.z * Mathf.Pow(actualUpDot, 2) * 10 : 0
                ), ForceMode.Acceleration);
        }

        // Apply diving force
        private void Dive()
        {
            _rb.AddTorque(_velDir * Vector3.left * Mathf.Clamp01(_vp.velMag * 0.01f) * Mathf.Clamp01(_vp.upDot) * diveFactor, ForceMode.Acceleration);
        }
    }
}