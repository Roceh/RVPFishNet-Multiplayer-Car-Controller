using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Steering Control", 2)]
    public class SteeringControl : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public float steerRate = 0.1f;

        [Tooltip("Curve for limiting steer range based on speed, x-axis = speed, y-axis = multiplier")]
        public AnimationCurve steerCurve = AnimationCurve.Linear(0, 1, 30, 0.1f);

        [Tooltip("")]
        public bool limitSteer = true;

        [Tooltip("Horizontal stretch of the steer curve")]
        public float steerCurveStretch = 1;

        [Tooltip("Limit steering in reverse?")]
        public bool applyInReverse = true;

        [Tooltip("")]
        public Suspension[] steeredWheels;

        [Header("Visual")]
        public bool rotate;

        [Tooltip("")]
        public float maxDegreesRotation;

        [Tooltip("")]
        public float rotationOffset;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private VehicleParent _vp;
        private VehicleManager _vm;
        private float _steerAmount;
        private float _steerRot;

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
            float rbSpeed = _vp.localVelocity.z / steerCurveStretch;
            float steerLimit = limitSteer ? steerCurve.Evaluate(applyInReverse ? Mathf.Abs(rbSpeed) : rbSpeed) : 1;
            _steerAmount = _vp.steerInput * steerLimit;

            StaticStateLogger.Log($"SteeringControl:_vp.steerInput={_vp.steerInput}");
            StaticStateLogger.Log($"SteeringControl:rbSpeed={rbSpeed}");

            // Set steer angles in wheels
            foreach (Suspension curSus in steeredWheels)
            {
                StaticStateLogger.Log($"SteeringControl:curSus.steerAngle={curSus.steerAngle}");
                StaticStateLogger.Log($"SteeringControl:_steerAmount={_steerAmount}");
                StaticStateLogger.Log($"SteeringControl:curSus.steerFactor={curSus.steerFactor}");
                StaticStateLogger.Log($"SteeringControl:curSus.steerEnabled={curSus.steerEnabled}");
                StaticStateLogger.Log($"SteeringControl:curSus.steerInverted={curSus.steerInverted}");
                StaticStateLogger.Log($"SteeringControl:steerRate={steerRate}");

                curSus.steerAngle = Mathf.Lerp(curSus.steerAngle, _steerAmount * curSus.steerFactor * (curSus.steerEnabled ? 1 : 0) * (curSus.steerInverted ? -1 : 1), steerRate * TimeMaster.inverseFixedTimeFactor * Time.timeScale);
            }
        }

        private void Awake()
        {
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -120);
        }

        private void Start()
        {
            _steerRot = rotationOffset;
        }

        private void Update()
        {
            // Visual steering wheel rotation
            if (rotate)
            {
                _steerRot = Mathf.Lerp(_steerRot, _steerAmount * maxDegreesRotation + rotationOffset, steerRate * Time.timeScale);
                transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, _steerRot);
            }
        }
    }
}