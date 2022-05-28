using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Hover/Hover Steer", 2)]
    public class HoverSteer : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public float steerRate = 1;

        [Tooltip("Curve for limiting steer range based on speed, x-axis = speed, y-axis = multiplier")]
        public AnimationCurve steerCurve = AnimationCurve.Linear(0, 1, 30, 0.1f);

        [Tooltip("Horizontal stretch of the steer curve")]
        public float steerCurveStretch = 1;

        [Tooltip("")]
        public HoverWheel[] steeredWheels;

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
            writer.WriteSingle(_steerAmount);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            _steerAmount = reader.ReadSingle();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            // Set steering of hover wheels
            float rbSpeed = _vp.localVelocity.z / steerCurveStretch;
            float steerLimit = steerCurve.Evaluate(Mathf.Abs(rbSpeed));
            _steerAmount = _vp.steerInput * steerLimit;

            foreach (HoverWheel curWheel in steeredWheels)
            {
                curWheel.steerRate = _steerAmount * steerRate;
            }
        }

        private void Awake()
        {
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -110);
        }

        private void Update()
        {
            // Set visual rotation
            if (rotate)
            {
                _steerRot = Mathf.Lerp(_steerRot, _steerAmount * maxDegreesRotation + rotationOffset, steerRate * 0.1f * Time.timeScale);
                transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, _steerRot);
            }
        }
    }
}