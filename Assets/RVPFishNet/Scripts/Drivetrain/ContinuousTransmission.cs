using FishNet.Serializing;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Drivetrain/Transmission/Continuous Transmission", 1)]
    public class ContinuousTransmission : Transmission
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Lerp value between min ratio and max ratio")]
        [Range(0, 1)]
        public float targetRatio;

        [Tooltip("")]
        public float minRatio;

        [Tooltip("")]
        public float maxRatio;

        [Tooltip("")]
        public bool canReverse;

        [Tooltip("How quickly the target ratio changes with manual shifting")]
        public float manualShiftRate = 0.5f;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float currentRatio;

        [System.NonSerialized]
        public bool reversing;

        public override void Simulate()
        {
            health = Mathf.Clamp01(health);

            // Set max RPM possible
            if (maxRPM == -1)
            {
                maxRPM = targetDrive.curve.keys[targetDrive.curve.length - 1].time * 1000;
            }

            if (health > 0)
            {
                if (automatic && _vp.groundedWheels > 0)
                {
                    // Automatically set the target ratio
                    targetRatio = (1 - _vp.burnout) * Mathf.Clamp01(Mathf.Abs(targetDrive.feedbackRPM) / Mathf.Max(0.01f, maxRPM * Mathf.Abs(currentRatio)));
                }
                else if (!automatic)
                {
                    // Manually set the target ratio
                    targetRatio = Mathf.Clamp01(targetRatio + (_vp.upshiftHold - _vp.downshiftHold) * manualShiftRate * Time.deltaTime);
                }
            }
            else
            {
                targetRatio = 0f;
            }

            reversing = canReverse && _vp.burnout == 0 && _vp.localVelocity.z < 1 && (_vp.accelInput < 0 || (_vp.brakeIsReverse && _vp.brakeInput > 0));
            currentRatio = Mathf.Lerp(minRatio, maxRatio, targetRatio) * (reversing ? -1 : 1);

            _newDrive.curve = targetDrive.curve;
            _newDrive.rpm = targetDrive.rpm / currentRatio;
            _newDrive.torque = Mathf.Abs(currentRatio) * targetDrive.torque;
            SetOutputDrives(currentRatio);
        }
    }
}