using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(DriveForce))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Drivetrain/Gas Motor", 0)]
    public class GasMotor : Motor
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Header("Performance")]
        [Tooltip("X-axis = RPM in thousands, y-axis = torque.  The rightmost key represents the maximum RPM")]
        public AnimationCurve torqueCurve = AnimationCurve.EaseInOut(0, 0, 8, 1);

        [Range(0, 0.99f)]
        [Tooltip("How quickly the engine adjusts its RPMs")]
        public float inertia;

        [Tooltip("Can the engine turn backwards?")]
        public bool canReverse;

        [Tooltip("")]
        public DriveForce[] outputDrives;

        [Tooltip("Exponent for torque output on each wheel")]
        public float driveDividePower = 3;

        [Header("Transmission")]
        public GearboxTransmission transmission;

        [Tooltip("Increase sound pitch between shifts")]
        public bool pitchIncreaseBetweenShift;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public DriveForce targetDrive;

        [System.NonSerialized]
        public float maxRPM;

        [System.NonSerialized]
        public bool shifting;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private float _actualAccel;

        public override void Awake()
        {
            base.Awake();

            targetDrive = GetComponent<DriveForce>();
        }

        public override void Start()
        {
            base.Start();

            // Get maximum possible RPM
            GetMaxRPM();
        }

        public override void GetFullState(Writer writer)
        {
            base.GetFullState(writer);

            targetDrive.GetFullState(writer);
            writer.WriteSingle(maxRPM);
        }

        public override void SetFullState(Reader reader)
        {
            base.SetFullState(reader);

            targetDrive.SetFullState(reader);
            maxRPM = reader.ReadSingle();
        }

        public override void Simulate()
        {
            base.Simulate();

            // Calculate proper input
            _actualAccel = Mathf.Lerp(_vp.brakeIsReverse && _vp.reversing && _vp.accelInput <= 0 ? _vp.brakeInput : _vp.accelInput, Mathf.Max(_vp.accelInput, _vp.burnout), _vp.burnout);
            float accelGet = canReverse ? _actualAccel : Mathf.Clamp01(_actualAccel);
            _actualInput = inputCurve.Evaluate(Mathf.Abs(accelGet)) * Mathf.Sign(accelGet);
            targetDrive.curve = torqueCurve;

            if (ignition)
            {
                float boostEval = boostPowerCurve.Evaluate(Mathf.Abs(_vp.localVelocity.z));

                // Set RPM
                targetDrive.rpm = Mathf.Lerp(targetDrive.rpm, _actualInput * maxRPM * 1000 * (boosting ? 1 + boostEval : 1), (1 - inertia) * Time.timeScale);

                // Set torque
                if (targetDrive.feedbackRPM > targetDrive.rpm)
                {
                    targetDrive.torque = 0;
                }
                else
                {
                    targetDrive.torque = torqueCurve.Evaluate(targetDrive.feedbackRPM * 0.001f - (boosting ? boostEval : 0)) * Mathf.Lerp(targetDrive.torque, power * Mathf.Abs(System.Math.Sign(_actualInput)), (1 - inertia) * Time.timeScale) * (boosting ? 1 + boostEval : 1) * health;
                }

                // Send RPM and torque through drivetrain
                if (outputDrives.Length > 0)
                {
                    float torqueFactor = Mathf.Pow(1f / outputDrives.Length, driveDividePower);
                    float tempRPM = 0;

                    foreach (DriveForce curOutput in outputDrives)
                    {
                        tempRPM += curOutput.feedbackRPM;
                        curOutput.SetDrive(targetDrive, torqueFactor);
                    }

                    targetDrive.feedbackRPM = tempRPM / outputDrives.Length;
                }

                if (transmission)
                {
                    shifting = transmission.shiftTime > 0;
                }
                else
                {
                    shifting = false;
                }
            }
            else
            {
                // If turned off, set RPM and torque to 0 and distribute it through drivetrain
                targetDrive.rpm = 0;
                targetDrive.torque = 0;
                targetDrive.feedbackRPM = 0;
                shifting = false;

                if (outputDrives.Length > 0)
                {
                    foreach (DriveForce curOutput in outputDrives)
                    {
                        curOutput.SetDrive(targetDrive);
                    }
                }
            }
        }

        public override void Update()
        {
            // Set audio pitch
            if (_snd && ignition)
            {
                _airPitch = _vp.groundedWheels > 0 || _actualAccel != 0 ? 1 : Mathf.Lerp(_airPitch, 0, 0.5f * Time.deltaTime);
                _pitchFactor = (_actualAccel != 0 || _vp.groundedWheels == 0 ? 1 : 0.5f) * (shifting ?
                    (pitchIncreaseBetweenShift ?
                        Mathf.Sin((transmission.shiftTime / transmission.shiftDelay) * Mathf.PI) :
                        Mathf.Min(transmission.shiftDelay, Mathf.Pow(transmission.shiftTime, 2)) / transmission.shiftDelay) :
                    1) * _airPitch;
                targetPitch = Mathf.Abs((targetDrive.feedbackRPM * 0.001f) / maxRPM) * _pitchFactor;
            }

            base.Update();
        }

        // Calculates the max RPM and propagates its effects
        public void GetMaxRPM()
        {
            maxRPM = torqueCurve.keys[torqueCurve.length - 1].time;

            if (outputDrives.Length > 0)
            {
                foreach (DriveForce curOutput in outputDrives)
                {
                    curOutput.curve = targetDrive.curve;

                    if (curOutput.GetComponent<Transmission>())
                    {
                        curOutput.GetComponent<Transmission>().ResetMaxRPM();
                    }
                }
            }
        }
    }
}