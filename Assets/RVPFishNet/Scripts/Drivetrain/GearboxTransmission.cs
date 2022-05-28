using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Drivetrain/Transmission/Gearbox Transmission", 0)]
    public class GearboxTransmission : Transmission, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public Gear[] gears;

        [Tooltip("")]
        public int startGear;

        [Tooltip("")]
        public bool skipNeutral;

        // Ratio of the current gear
        [Tooltip("Calculate the RPM ranges of the gears in play mode.  This will overwrite the current values")]
        public bool autoCalculateRpmRanges = true;

        [Tooltip("Number of physics steps a shift should last")]
        public float shiftDelay;

        [Tooltip("Multiplier for comparisons in automatic shifting calculations, should be 2 in most cases")]
        public float shiftThreshold;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public int currentGear;

        [System.NonSerialized]
        public float curGearRatio;

        [System.NonSerialized]
        public float shiftTime;
        
        // -=-=-=-= LOCAL STATE =-=-=-=-

        private int _firstGear;
        private Gear _upperGear; // Next gear above current
        private Gear _lowerGear; // Next gear below current
        private float _upshiftDifference; // RPM difference between current gear and upper gear
        private float _downshiftDifference; // RPM difference between current gear and lower gear

        public void Start()
        {
            currentGear = Mathf.Clamp(startGear, 0, gears.Length - 1);

            // Get gear number 1 (first one above neutral)
            GetFirstGear();
        }

        public override void GetFullState(Writer writer)
        {
            base.GetFullState(writer);

            writer.WriteInt32(currentGear);
            writer.WriteSingle(shiftTime);
        }

        public override void SetFullState(Reader reader)
        {
            base.SetFullState(reader);

            currentGear = reader.ReadInt32();
            shiftTime = reader.ReadSingle();
        }

        public override void Simulate()
        {
            // Check for manual shift button presses
            if (!automatic)
            {
                if (_vp.upshiftPressed && currentGear < gears.Length - 1)
                {
                    Shift(1);
                }

                if (_vp.downshiftPressed && currentGear > 0)
                {
                    Shift(-1);
                }
            }

            health = Mathf.Clamp01(health);
            shiftTime = Mathf.Max(0, shiftTime - Time.timeScale * TimeMaster.inverseFixedTimeFactor);
            curGearRatio = gears[currentGear].ratio;

            // Calculate upperGear and lowerGear
            float actualFeedbackRPM = targetDrive.feedbackRPM / Mathf.Abs(curGearRatio);
            int upGearOffset = 1;
            int downGearOffset = 1;

            while ((skipNeutral || automatic) && gears[Mathf.Clamp(currentGear + upGearOffset, 0, gears.Length - 1)].ratio == 0 && currentGear + upGearOffset != 0 && currentGear + upGearOffset < gears.Length - 1)
            {
                upGearOffset++;
            }

            while ((skipNeutral || automatic) && gears[Mathf.Clamp(currentGear - downGearOffset, 0, gears.Length - 1)].ratio == 0 && currentGear - downGearOffset != 0 && currentGear - downGearOffset > 0)
            {
                downGearOffset++;
            }

            _upperGear = gears[Mathf.Min(gears.Length - 1, currentGear + upGearOffset)];
            _lowerGear = gears[Mathf.Max(0, currentGear - downGearOffset)];

            // Perform RPM calculations
            if (maxRPM == -1)
            {
                maxRPM = targetDrive.curve.keys[targetDrive.curve.length - 1].time;

                if (autoCalculateRpmRanges)
                {
                    CalculateRpmRanges();
                }
            }

            // Set RPMs and torque of output
            _newDrive.curve = targetDrive.curve;

            if (curGearRatio == 0 || shiftTime > 0)
            {
                _newDrive.rpm = 0;
                _newDrive.torque = 0;
            }
            else
            {
                _newDrive.rpm = (automatic && skidSteerDrive ? Mathf.Abs(targetDrive.rpm) * Mathf.Sign(_vp.accelInput - (_vp.brakeIsReverse ? _vp.brakeInput * (1 - _vp.burnout) : 0)) : targetDrive.rpm) / curGearRatio;
                _newDrive.torque = Mathf.Abs(curGearRatio) * targetDrive.torque;
            }

            // Perform automatic shifting
            _upshiftDifference = gears[currentGear].maxRPM - _upperGear.minRPM;
            _downshiftDifference = _lowerGear.maxRPM - gears[currentGear].minRPM;

            if (automatic && shiftTime == 0 && _vp.groundedWheels > 0)
            {
                if (!skidSteerDrive && _vp.burnout == 0)
                {
                    if (Mathf.Abs(_vp.localVelocity.z) > 1 || _vp.accelInput > 0 || (_vp.brakeInput > 0 && _vp.brakeIsReverse))
                    {
                        if (currentGear < gears.Length - 1
                            && (_upperGear.minRPM + _upshiftDifference * (curGearRatio < 0 ? Mathf.Min(1, shiftThreshold) : shiftThreshold) - actualFeedbackRPM <= 0 || (curGearRatio <= 0 && _upperGear.ratio > 0 && (!_vp.reversing || (_vp.accelInput > 0 && _vp.localVelocity.z > curGearRatio * 10))))
                            && !(_vp.brakeInput > 0 && _vp.brakeIsReverse && _upperGear.ratio >= 0)
                            && !(_vp.localVelocity.z < 0 && _vp.accelInput == 0))
                        {
                            Shift(1);
                        }
                        else if (currentGear > 0
                            && (actualFeedbackRPM - (_lowerGear.maxRPM - _downshiftDifference * shiftThreshold) <= 0 || (curGearRatio >= 0 && _lowerGear.ratio < 0 && (_vp.reversing || ((_vp.accelInput < 0 || (_vp.brakeInput > 0 && _vp.brakeIsReverse)) && _vp.localVelocity.z < curGearRatio * 10))))
                            && !(_vp.accelInput > 0 && _lowerGear.ratio <= 0)
                            && (_lowerGear.ratio > 0 || _vp.localVelocity.z < 1))
                        {
                            Shift(-1);
                        }
                    }
                }
                else if (currentGear != _firstGear)
                {
                    // Shift into first gear if skid steering or burning out
                    ShiftToGear(_firstGear);
                }
            }

            StaticStateLogger.Log($"GearboxTransmission:_newDrive.rpm={_newDrive.rpm}");
            StaticStateLogger.Log($"GearboxTransmission:_newDrive.torque={_newDrive.torque}");

            SetOutputDrives(curGearRatio);
        }

        // Shift gears by the number entered
        public void Shift(int dir)
        {
            if (health > 0)
            {
                shiftTime = shiftDelay;
                currentGear += dir;

                while ((skipNeutral || automatic) && gears[Mathf.Clamp(currentGear, 0, gears.Length - 1)].ratio == 0 && currentGear != 0 && currentGear != gears.Length - 1)
                {
                    currentGear += dir;
                }

                currentGear = Mathf.Clamp(currentGear, 0, gears.Length - 1);
            }
        }

        // Shift straight to the gear specified
        public void ShiftToGear(int gear)
        {
            if (health > 0)
            {
                shiftTime = shiftDelay;
                currentGear = Mathf.Clamp(gear, 0, gears.Length - 1);
            }
        }

        // Caculate ideal RPM ranges for each gear (works most of the time)
        public void CalculateRpmRanges()
        {
            bool cantCalc = false;
            if (!Application.isPlaying)
            {
                GasMotor engine = transform.GetTopmostParentComponent<VehicleParent>().GetComponentInChildren<GasMotor>();

                if (engine)
                {
                    maxRPM = engine.torqueCurve.keys[engine.torqueCurve.length - 1].time;
                }
                else
                {
                    Debug.LogError("There is no <GasMotor> in the vehicle to get RPM info from.", this);
                    cantCalc = true;
                }
            }

            if (!cantCalc)
            {
                float prevGearRatio;
                float nextGearRatio;
                float actualMaxRPM = maxRPM * 1000;

                for (int i = 0; i < gears.Length; i++)
                {
                    prevGearRatio = gears[Mathf.Max(i - 1, 0)].ratio;
                    nextGearRatio = gears[Mathf.Min(i + 1, gears.Length - 1)].ratio;

                    if (gears[i].ratio < 0)
                    {
                        gears[i].minRPM = actualMaxRPM / gears[i].ratio;

                        if (nextGearRatio == 0)
                        {
                            gears[i].maxRPM = 0;
                        }
                        else
                        {
                            gears[i].maxRPM = actualMaxRPM / nextGearRatio + (actualMaxRPM / nextGearRatio - gears[i].minRPM) * 0.5f;
                        }
                    }
                    else if (gears[i].ratio > 0)
                    {
                        gears[i].maxRPM = actualMaxRPM / gears[i].ratio;

                        if (prevGearRatio == 0)
                        {
                            gears[i].minRPM = 0;
                        }
                        else
                        {
                            gears[i].minRPM = actualMaxRPM / prevGearRatio - (gears[i].maxRPM - actualMaxRPM / prevGearRatio) * 0.5f;
                        }
                    }
                    else
                    {
                        gears[i].minRPM = 0;
                        gears[i].maxRPM = 0;
                    }

                    gears[i].minRPM *= 0.55f;
                    gears[i].maxRPM *= 0.55f;
                }
            }
        }

        // Returns the first gear (first gear above neutral)
        public void GetFirstGear()
        {
            for (int i = 0; i < gears.Length; i++)
            {
                if (gears[i].ratio == 0)
                {
                    _firstGear = i + 1;
                    break;
                }
            }
        }
    }

    // Gear class
    [System.Serializable]
    public class Gear
    {
        public float ratio;
        public float minRPM;
        public float maxRPM;
    }
}