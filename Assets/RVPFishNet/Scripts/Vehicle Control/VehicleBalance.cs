using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Balance", 4)]
    public class VehicleBalance : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Lean strength along each axis")]
        public Vector3 leanFactor;

        [Tooltip("")]
        [Range(0, 0.99f)]
        public float leanSmoothness;

        [Tooltip("Adjusts the roll based on the speed, x-axis = speed, y-axis = roll amount")]
        public AnimationCurve leanRollCurve = AnimationCurve.Linear(0, 0, 10, 1);

        [Tooltip("Adjusts the pitch based on the speed, x-axis = speed, y-axis = pitch amount")]
        public AnimationCurve leanPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Adjusts the yaw based on the speed, x-axis = speed, y-axis = yaw amount")]
        public AnimationCurve leanYawCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Speed above which endos (forward wheelies) aren't allowed")]
        public float endoSpeedThreshold;

        [Tooltip("Exponent for pitch input")]
        public float pitchExponent;

        [Tooltip("How much to lean when sliding sideways")]
        public float slideLeanFactor = 1;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private float _actualPitchInput;
        private Vector3 _targetLean;
        private Vector3 _targetLeanActual;

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
            // Apply endo limit
            _actualPitchInput = _vp.wheels.Length == 1 ? 0 : Mathf.Clamp(_vp.pitchInput, -1, _vp.velMag > endoSpeedThreshold ? 0 : 1);

            if (_vp.groundedWheels > 0)
            {
                if (leanFactor != Vector3.zero)
                {
                    ApplyLean();
                }
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, 0);
        }

        // Apply corrective balance forces
        private void ApplyLean()
        {
            if (_vp.groundedWheels > 0)
            {
                Vector3 inverseWorldUp;
                inverseWorldUp = _vp.norm.InverseTransformDirection(Vector3.Dot(_vp.wheelNormalAverage, GlobalControl.worldUpDir) <= 0 ? _vp.wheelNormalAverage : Vector3.Lerp(GlobalControl.worldUpDir, _vp.wheelNormalAverage, Mathf.Abs(Vector3.Dot(_vp.norm.up, GlobalControl.worldUpDir)) * 2));
                Debug.DrawRay(transform.position, _vp.norm.TransformDirection(inverseWorldUp), Color.white);

                // Calculate target lean direction
                _targetLean = new Vector3(
                    Mathf.Lerp(
                        inverseWorldUp.x,
                        Mathf.Clamp(-_vp.rollInput * leanFactor.z * leanRollCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z)) + Mathf.Clamp(_vp.localVelocity.x * slideLeanFactor, -leanFactor.z * slideLeanFactor, leanFactor.z * slideLeanFactor), -leanFactor.z, leanFactor.z),
                        Mathf.Max(Mathf.Abs(F.MaxAbs(_vp.steerInput, _vp.rollInput)))),
                    Mathf.Pow(Mathf.Abs(_actualPitchInput), pitchExponent) * Mathf.Sign(_actualPitchInput) * leanFactor.x,
                    inverseWorldUp.z * (1 - Mathf.Abs(F.MaxAbs(_actualPitchInput * leanFactor.x, _vp.rollInput * leanFactor.z))));
            }
            else
            {
                _targetLean = _vp.upDir;
            }

            // Transform targetLean to world space
            _targetLeanActual = Vector3.Lerp(_targetLeanActual, _vp.norm.TransformDirection(_targetLean), (1 - leanSmoothness) * Time.timeScale * TimeMaster.inverseFixedTimeFactor).normalized;
            Debug.DrawRay(transform.position, _targetLeanActual, Color.black);

            // Apply pitch
            _rb.AddTorque(
                _vp.norm.right * -(Vector3.Dot(_vp.forwardDir, _targetLeanActual) * 20 - _vp.localAngularVel.x) * 100 * (_vp.wheels.Length == 1 ? 1 : leanPitchCurve.Evaluate(Mathf.Abs(_actualPitchInput))),
                ForceMode.Acceleration);

            // Apply yaw
            _rb.AddTorque(
                _vp.norm.forward * (_vp.groundedWheels == 1 ? _vp.steerInput * leanFactor.y - _vp.norm.InverseTransformDirection(_rb.angularVelocity).z : 0) * 100 * leanYawCurve.Evaluate(Mathf.Abs(_vp.steerInput)),
                ForceMode.Acceleration);

            // Apply roll
            _rb.AddTorque(
                _vp.norm.up * (-Vector3.Dot(_vp.rightDir, _targetLeanActual) * 20 - _vp.localAngularVel.z) * 100,
                ForceMode.Acceleration);

            // Turn vehicle during wheelies
            if (_vp.groundedWheels == 1 && leanFactor.y > 0)
            {
                _rb.AddTorque(_vp.norm.TransformDirection(
                    new Vector3(0, 0, _vp.steerInput * leanFactor.y - _vp.norm.InverseTransformDirection(_rb.angularVelocity).z)
                    ), ForceMode.Acceleration);
            }
        }
    }
}