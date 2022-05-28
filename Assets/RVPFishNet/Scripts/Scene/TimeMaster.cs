using FishNet;
using UnityEngine;
using UnityEngine.Audio;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Scene Controllers/Time Master", 1)]
    public class TimeMaster : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("Master audio mixer")]
        public AudioMixer masterMixer;
        
        [Tooltip("")]
        public bool destroyOnLoad;

        // -=-=-=-= SHARED STATE =-=-=-=-

        public static float fixedTimeFactor;

        public static float inverseFixedTimeFactor;  // Multiplier for certain variables to change consistently over varying time steps

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private float initialFixedTime; // Intial Time.fixedDeltaTime

        private void Awake()
        {
            initialFixedTime = (float)InstanceFinder.TimeManager.TickDelta;

            if (!destroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            // Set the pitch of all audio to the time scale
            if (masterMixer)
            {
                masterMixer.SetFloat("MasterPitch", Time.timeScale);
            }
        }

        private void FixedUpdate()
        {
            // Set the fixed update rate based on time scale
            Time.fixedDeltaTime = Time.timeScale * initialFixedTime;
            fixedTimeFactor = 0.01f / initialFixedTime;
            inverseFixedTimeFactor = 1.0f / fixedTimeFactor;
        }
    }
}