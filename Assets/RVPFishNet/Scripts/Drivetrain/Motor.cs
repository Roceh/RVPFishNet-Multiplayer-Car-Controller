using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    // Class for engines
    public abstract class Motor : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public bool ignition;

        [Tooltip("")]
        public float power = 1;

        [Tooltip("Throttle curve, x-axis = input, y-axis = output")]
        public AnimationCurve inputCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Engine Audio")]
        public float minPitch;

        [Tooltip("")]
        public float maxPitch;

        [Header("Nitrous Boost")]
        public bool canBoost = true;

        [Tooltip("")]
        public float boost = 1;

        [Tooltip("X-axis = local z-velocity, y-axis = power")]
        public AnimationCurve boostPowerCurve = AnimationCurve.EaseInOut(0, 0.1f, 50, 0.2f);

        [Tooltip("")]
        public float maxBoost = 1;

        [Tooltip("")]
        public float boostBurnRate = 0.01f;

        [Tooltip("")]
        public AudioSource boostLoopSnd;

        [Tooltip("")] 
        public AudioClip boostStart;

        [Tooltip("")] 
        public AudioClip boostEnd;

        [Tooltip("")] 
        public ParticleSystem[] boostParticles;

        [Header("Damage")]
        [Range(0, 1)]
        public float strength = 1;

        [Tooltip("")]
        public float damagePitchWiggle;

        [Tooltip("")] 
        public ParticleSystem smoke;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float targetPitch;

        [System.NonSerialized]
        public bool boosting;

        [System.NonSerialized]
        public float health = 1;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        protected VehicleParent _vp;
        protected VehicleManager _vm;
        protected float _actualInput; // Input after applying the input curve
        protected AudioSource _snd;
        protected float _pitchFactor;
        protected float _airPitch;
        private bool _boostReleased;
        private bool _boostPrev;
        private AudioSource _boostSnd; // AudioSource for boostStart and boostEnd
        private float _initialSmokeEmission;

        public virtual void Awake()
        {
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _vm = transform.GetTopmostParentComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -90);
        }

        public virtual void Start()
        {
            // Get engine sound
            _snd = GetComponent<AudioSource>();

            if (_snd)
            {
                _snd.pitch = minPitch;
            }

            // Get boost sound
            if (boostLoopSnd)
            {
                GameObject newBoost = Instantiate(boostLoopSnd.gameObject, boostLoopSnd.transform.position, boostLoopSnd.transform.rotation) as GameObject;
                _boostSnd = newBoost.GetComponent<AudioSource>();
                _boostSnd.transform.parent = boostLoopSnd.transform;
                _boostSnd.transform.localPosition = Vector3.zero;
                _boostSnd.transform.localRotation = Quaternion.identity;
                _boostSnd.loop = false;
            }

            if (smoke)
            {
                _initialSmokeEmission = smoke.emission.rateOverTime.constantMax;
            }
        }

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        public virtual void GetFullState(Writer writer)
        {
            writer.WriteBoolean(boosting);
            writer.WriteSingle(health);
        }

        public virtual void SetFullState(Reader reader)
        {
            boosting = reader.ReadBoolean();
            health = reader.ReadSingle();
        }

        public virtual void GetVisualState(Writer writer)
        {
            writer.WriteSingle(health);
            writer.WriteSingle(targetPitch);
        }

        public virtual void SetVisualState(Reader reader)
        {
            health = reader.ReadSingle();
            targetPitch = reader.ReadSingle();
        }

        public virtual void Simulate()
        {
            health = Mathf.Clamp01(health);

            // Boost logic
            boost = Mathf.Clamp(boosting ? boost - boostBurnRate * Time.timeScale * 0.05f * TimeMaster.inverseFixedTimeFactor : boost, 0, maxBoost);
            _boostPrev = boosting;

            if (canBoost && ignition && health > 0 && !_vp.crashing && boost > 0 && (_vp.hover ? _vp.accelInput != 0 || Mathf.Abs(_vp.localVelocity.z) > 1 : _vp.accelInput > 0 || _vp.localVelocity.z > 1))
            {
                if (((_boostReleased && !boosting) || boosting) && _vp.boostButton)
                {
                    boosting = true;
                    _boostReleased = false;
                }
                else
                {
                    boosting = false;
                }
            }
            else
            {
                boosting = false;
            }

            if (!_vp.boostButton)
            {
                _boostReleased = true;
            }

            if (boostLoopSnd && _boostSnd)
            {
                if (boosting && !boostLoopSnd.isPlaying)
                {
                    boostLoopSnd.Play();
                }
                else if (!boosting && boostLoopSnd.isPlaying)
                {
                    boostLoopSnd.Stop();
                }

                if (boosting && !_boostPrev)
                {
                    _boostSnd.clip = boostStart;
                    _boostSnd.Play();
                }
                else if (!boosting && _boostPrev)
                {
                    _boostSnd.clip = boostEnd;
                    _boostSnd.Play();
                }
            }
        }

        public virtual void Update()
        {
            // Set engine sound properties
            if (!ignition)
            {
                targetPitch = 0;
            }

            if (_snd)
            {
                if (ignition && health > 0)
                {
                    _snd.enabled = true;
                    _snd.pitch = Mathf.Lerp(_snd.pitch, Mathf.Lerp(minPitch, maxPitch, targetPitch), 20 * Time.deltaTime) + Mathf.Sin(Time.time * 200 * (1 - health)) * (1 - health) * 0.1f * damagePitchWiggle;
                    _snd.volume = Mathf.Lerp(_snd.volume, 0.3f + targetPitch * 0.7f, 20 * Time.deltaTime);
                }
                else
                {
                    _snd.enabled = false;
                }
            }

            // Play boost particles
            if (boostParticles.Length > 0)
            {
                foreach (ParticleSystem curBoost in boostParticles)
                {
                    if (boosting && curBoost.isStopped)
                    {
                        curBoost.Play();
                    }
                    else if (!boosting && curBoost.isPlaying)
                    {
                        curBoost.Stop();
                    }
                }
            }

            // Adjusting smoke particles based on damage
            if (smoke)
            {
                ParticleSystem.EmissionModule em = smoke.emission;
                em.rateOverTime = new ParticleSystem.MinMaxCurve(health < 0.7f ? _initialSmokeEmission * (1 - health) : 0);
            }
        }
    }
}