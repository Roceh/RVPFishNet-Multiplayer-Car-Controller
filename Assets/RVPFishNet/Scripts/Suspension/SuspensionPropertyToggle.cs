using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Suspension))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Suspension/Suspension Property", 2)]
    public class SuspensionPropertyToggle : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public SuspensionToggledProperty[] properties;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Suspension _sus;

        // Toggle a property in the properties array at index
        public void ToggleProperty(int index)
        {
            if (properties.Length - 1 >= index)
            {
                properties[index].toggled = !properties[index].toggled;

                if (_sus)
                {
                    _sus.UpdateProperties();
                }
            }
        }

        // Set a property in the properties array at index to the value
        public void SetProperty(int index, bool value)
        {
            if (properties.Length - 1 >= index)
            {
                properties[index].toggled = value;

                if (_sus)
                {
                    _sus.UpdateProperties();
                }
            }
        }

        private void Start()
        {
            _sus = GetComponent<Suspension>();
        }
    }

    // Class for a single property
    [System.Serializable]
    public class SuspensionToggledProperty
    {
        public Properties property;

        // The property
        public bool toggled;

        public enum Properties
        { steerEnable, steerInvert, driveEnable, driveInvert, ebrakeEnable, skidSteerBrake } // The type of property

        // steerEnable = enable steering
        // steerInvert = invert steering
        // driveEnable = enable driving
        // driveInvert = invert drive
        // ebrakeEnable = can ebrake
        // skidSteerBrake = brake is specially adjusted for skid steering

        // Is it enabled?
    }
}