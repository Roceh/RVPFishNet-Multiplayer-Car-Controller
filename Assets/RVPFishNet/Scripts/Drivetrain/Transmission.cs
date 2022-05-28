using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(DriveForce))]
    public abstract class Transmission : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        [Range(0, 1)]
        public float strength = 1;

        [Tooltip("")]
        public bool automatic;

        [Tooltip("Apply special drive to wheels for skid steering")]
        public bool skidSteerDrive;

        [Tooltip("")]
        public DriveForce[] outputDrives;

        [Tooltip("Exponent for torque output on each wheel")]
        public float driveDividePower = 3;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public DriveForce targetDrive;

        [System.NonSerialized]
        public float health = 1;

        [System.NonSerialized]
        public float maxRPM = -1;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        protected VehicleParent _vp;
        protected VehicleManager _vm;
        protected DriveForce _newDrive;

        public virtual void Awake()
        {
            targetDrive = GetComponent<DriveForce>();
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _newDrive = gameObject.AddComponent<DriveForce>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -70);
        }

        public void ResetMaxRPM()
        {
            maxRPM = -1; // Setting this to -1 triggers derived classes to recalculate things
        }

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        public virtual void GetFullState(Writer writer)
        {
            targetDrive.GetFullState(writer);
            writer.WriteSingle(health);
            writer.WriteSingle(maxRPM);
        }

        public virtual void SetFullState(Reader reader)
        {
            targetDrive.SetFullState(reader);
            health = reader.ReadSingle();
            maxRPM = reader.ReadSingle();
        }

        public virtual void GetVisualState(Writer writer)
        {
        }

        public virtual void SetVisualState(Reader reader)
        {
        }

        public abstract void Simulate();

        protected void SetOutputDrives(float ratio)
        {
            // Distribute drive to wheels
            if (outputDrives.Length > 0)
            {
                int enabledDrives = 0;

                // Check for which outputs are enabled
                foreach (DriveForce curOutput in outputDrives)
                {
                    if (curOutput.active)
                    {
                        enabledDrives++;
                    }
                }

                float torqueFactor = Mathf.Pow(1f / enabledDrives, driveDividePower);
                float tempRPM = 0;

                foreach (DriveForce curOutput in outputDrives)
                {
                    if (curOutput.active)
                    {
                        tempRPM += skidSteerDrive ? Mathf.Abs(curOutput.feedbackRPM) : curOutput.feedbackRPM;
                        curOutput.SetDrive(_newDrive, torqueFactor);
                    }
                }

                targetDrive.feedbackRPM = (tempRPM / enabledDrives) * ratio;
            }
        }
    }
}