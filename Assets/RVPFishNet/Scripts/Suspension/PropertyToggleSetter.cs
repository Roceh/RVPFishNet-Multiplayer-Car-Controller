using UnityEngine;

namespace RVP
{
    [AddComponentMenu("RVP/Suspension/Suspension Property Setter", 3)]
    public class PropertyToggleSetter : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Steering Controller")]
        public SteeringControl steerer;

        [Tooltip("")]
        public Transmission transmission;

        [Tooltip("Suspensions with properties to be toggled")]
        public SuspensionPropertyToggle[] suspensionProperties;

        [Tooltip("")]
        public PropertyTogglePreset[] presets;

        [Tooltip("")]
        public int currentPreset;

        [Tooltip("Input manager button which increments the preset")]
        public string changeButton;

        // Change the current preset
        public void ChangePreset(int preset)
        {
            currentPreset = preset % (presets.Length);

            if (steerer)
            {
                steerer.limitSteer = presets[currentPreset].limitSteer;
            }

            if (transmission)
            {
                transmission.skidSteerDrive = presets[currentPreset].skidSteerTransmission;
            }

            for (int i = 0; i < suspensionProperties.Length; i++)
            {
                for (int j = 0; j < suspensionProperties[i].properties.Length; j++)
                {
                    suspensionProperties[i].SetProperty(j, presets[currentPreset].wheels[i].preset[j]);
                }
            }
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(changeButton))
            {
                if (Input.GetButtonDown(changeButton))
                {
                    ChangePreset(currentPreset + 1);
                }
            }
        }
    }

    // Preset class
    [System.Serializable]
    public class PropertyTogglePreset
    {
        [Tooltip("Limit the steering range of wheels based on SteeringControl's curve?")]
        public bool limitSteer = true;

        [Tooltip("Transmission is adjusted for skid steering?")]
        public bool skidSteerTransmission;

        [Tooltip("Must be equal to the number of wheels")]
        public IndividualPreset[] wheels;
    }

    // Class for toggling the properties of SuspensionPropertyToggle instances
    [System.Serializable]
    public class IndividualPreset
    {
        [Tooltip("Must be equal to the SuspensionPropertyToggle properties array length")]
        public bool[] preset;
    }
}