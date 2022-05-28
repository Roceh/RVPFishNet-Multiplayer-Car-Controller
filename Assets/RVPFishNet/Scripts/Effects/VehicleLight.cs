using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Effects/Vehicle Light", 3)]
    public class VehicleLight : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public bool on;

        [Tooltip("Example: a brake light would be half on when the night lights are on, and fully on when the brakes are pressed")]
        public bool halfOn;

        [Tooltip("")]
        public Light targetLight;

        [Tooltip("A light shared with another vehicle light, will turn off if one of the lights break, then the unbroken light will turn on its target light")]
        public Light sharedLight;

        [Tooltip("Vehicle light that the shared light is shared with")]
        public VehicleLight sharer;

        [Tooltip("")]
        public Material onMaterial;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public bool shattered;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Renderer _rend;
        private ShatterPart _shatter;
        private Material _offMaterial;

        private void Start()
        {
            _rend = GetComponent<Renderer>();
            if (_rend)
            {
                _offMaterial = _rend.sharedMaterial;
            }

            _shatter = GetComponent<ShatterPart>();
        }

        private void Update()
        {
            if (_shatter)
            {
                shattered = _shatter.shattered;
            }

            // Configure shared light
            if (sharedLight && sharer)
            {
                sharedLight.enabled = on && sharer.on && !shattered && !sharer.shattered;
            }

            // Configure target light
            if (targetLight)
            {
                if (sharedLight && sharer)
                {
                    targetLight.enabled = !shattered && on && !sharedLight.enabled;
                }
            }

            // Shatter logic
            if (_rend)
            {
                if (shattered)
                {
                    if (_shatter.brokenMaterial)
                    {
                        _rend.sharedMaterial = _shatter.brokenMaterial;
                    }
                    else
                    {
                        _rend.sharedMaterial = on || halfOn ? onMaterial : _offMaterial;
                    }
                }
                else
                {
                    _rend.sharedMaterial = on || halfOn ? onMaterial : _offMaterial;
                }
            }
        }
    }
}