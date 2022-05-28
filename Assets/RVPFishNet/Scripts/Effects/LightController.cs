using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Effects/Light Controller", 2)]
    public class LightController : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public bool headlightsOn;

        [Tooltip("")]
        public bool highBeams;

        [Tooltip("")]
        public bool brakelightsOn;

        [Tooltip("")]
        public bool rightBlinkersOn;

        [Tooltip("")]
        public bool leftBlinkersOn;

        [Tooltip("")]
        public float blinkerInterval = 0.3f;

        [Tooltip("")]
        public bool reverseLightsOn;

        [Tooltip("")]
        public Transmission transmission;

        [Tooltip("")]
        public VehicleLight[] headlights;

        [Tooltip("")]
        public VehicleLight[] brakeLights;

        [Tooltip("")]
        public VehicleLight[] RightBlinkers;

        [Tooltip("")]
        public VehicleLight[] LeftBlinkers;

        [Tooltip("")]
        public VehicleLight[] ReverseLights;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private VehicleParent _vp;
        private bool _blinkerIntervalOn;
        private float _blinkerSwitchTime;
        private GearboxTransmission _gearTrans;
        private ContinuousTransmission _conTrans;

        private void Start()
        {
            _vp = GetComponent<VehicleParent>();

            // Get transmission for using reverse lights
            if (transmission)
            {
                if (transmission is GearboxTransmission)
                {
                    _gearTrans = transmission as GearboxTransmission;
                }
                else if (transmission is ContinuousTransmission)
                {
                    _conTrans = transmission as ContinuousTransmission;
                }
            }
        }

        private void Update()
        {
            // Activate blinkers
            if (leftBlinkersOn || rightBlinkersOn)
            {
                if (_blinkerSwitchTime == 0)
                {
                    _blinkerIntervalOn = !_blinkerIntervalOn;
                    _blinkerSwitchTime = blinkerInterval;
                }
                else
                {
                    _blinkerSwitchTime = Mathf.Max(0, _blinkerSwitchTime - Time.deltaTime);
                }
            }
            else
            {
                _blinkerIntervalOn = false;
                _blinkerSwitchTime = 0;
            }

            // Activate reverse lights
            if (_gearTrans)
            {
                reverseLightsOn = _gearTrans.curGearRatio < 0;
            }
            else if (_conTrans)
            {
                reverseLightsOn = _conTrans.reversing;
            }

            // Activate brake lights
            if (_vp.accelAxisIsBrake)
            {
                brakelightsOn = _vp.accelInput != 0 && Mathf.Sign(_vp.accelInput) != Mathf.Sign(_vp.localVelocity.z) && Mathf.Abs(_vp.localVelocity.z) > 1;
            }
            else
            {
                if (!_vp.brakeIsReverse)
                {
                    brakelightsOn = (_vp.burnout > 0 && _vp.brakeInput > 0) || _vp.brakeInput > 0;
                }
                else
                {
                    brakelightsOn = (_vp.burnout > 0 && _vp.brakeInput > 0) || ((_vp.brakeInput > 0 && _vp.localVelocity.z > 1) || (_vp.accelInput > 0 && _vp.localVelocity.z < -1));
                }
            }

            SetLights(headlights, highBeams, headlightsOn);
            SetLights(brakeLights, headlightsOn || highBeams, brakelightsOn);
            SetLights(RightBlinkers, rightBlinkersOn && _blinkerIntervalOn);
            SetLights(LeftBlinkers, leftBlinkersOn && _blinkerIntervalOn);
            SetLights(ReverseLights, reverseLightsOn);
        }

        // Set if lights are on or off based on the condition
        private void SetLights(VehicleLight[] lights, bool condition)
        {
            foreach (VehicleLight curLight in lights)
            {
                curLight.on = condition;
            }
        }

        // Set if lights are on or off based on the first condition, and half on based on the second condition (see halfOn tooltip in VehicleLight)
        private void SetLights(VehicleLight[] lights, bool condition, bool halfCondition)
        {
            foreach (VehicleLight curLight in lights)
            {
                curLight.on = condition;
                curLight.halfOn = halfCondition;
            }
        }
    }
}