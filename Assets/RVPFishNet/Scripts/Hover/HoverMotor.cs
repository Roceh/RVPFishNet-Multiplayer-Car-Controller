using FishNet.Serializing;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Hover/Hover Motor", 0)]
    public class HoverMotor : Motor
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Header("Performance")]
        [Tooltip("Curve which calculates the driving force based on the speed of the vehicle, x-axis = speed, y-axis = force")]
        public AnimationCurve forceCurve = AnimationCurve.EaseInOut(0, 1, 50, 0);

        [Tooltip("")]
        public HoverWheel[] wheels;

        public override void Simulate()
        {
            base.Simulate();

            // Get proper input
            float actualAccel = _vp.brakeIsReverse ? _vp.accelInput - _vp.brakeInput : _vp.accelInput;
            _actualInput = inputCurve.Evaluate(Mathf.Abs(actualAccel)) * Mathf.Sign(actualAccel);

            // Set hover wheel speeds and forces
            foreach (HoverWheel curWheel in wheels)
            {
                if (ignition)
                {
                    float boostEval = boostPowerCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z));
                    curWheel.targetSpeed = _actualInput * forceCurve.keys[forceCurve.keys.Length - 1].time * (boosting ? 1 + boostEval : 1);
                    curWheel.targetForce = Mathf.Abs(_actualInput) * forceCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z) - (boosting ? boostEval : 0)) * power * (boosting ? 1 + boostEval : 1) * health;
                }
                else
                {
                    curWheel.targetSpeed = 0;
                    curWheel.targetForce = 0;
                }

                curWheel.doFloat = ignition && health > 0;
            }
        }

        public override void Update()
        {
            // Set engine pitch
            if (_snd && ignition)
            {
                targetPitch = Mathf.Max(Mathf.Abs(_actualInput), Mathf.Abs(_vp.steerInput) * 0.5f) * (1 - forceCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z)));
            }

            base.Update();
        }
    }
}