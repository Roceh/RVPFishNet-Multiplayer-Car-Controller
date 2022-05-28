using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Effects/Tire Screech Audio", 1)]
    public class TireScreech : MonoBehaviour
    {
        // -=-=-=-= LOCAL STATE =-=-=-=-

        private AudioSource _snd;
        private VehicleParent _vp;
        private Wheel[] _wheels;
        private float _slipThreshold;
        private GroundSurface _surfaceType;

        private void Start()
        {
            _snd = GetComponent<AudioSource>();
            _vp = transform.GetTopmostParentComponent<VehicleParent>();
            _wheels = new Wheel[_vp.wheels.Length];

            // Get wheels and average slip threshold
            for (int i = 0; i < _vp.wheels.Length; i++)
            {
                _wheels[i] = _vp.wheels[i];
                if (_vp.wheels[i].GetComponent<TireMarkCreate>())
                {
                    float newThreshold = _vp.wheels[i].GetComponent<TireMarkCreate>().slipThreshold;
                    _slipThreshold = i == 0 ? newThreshold : (_slipThreshold + newThreshold) * 0.5f;
                }
            }
        }

        private void Update()
        {
            float screechAmount = 0;
            bool allPopped = true;
            bool nonePopped = true;
            float alwaysScrape = 0;

            for (int i = 0; i < _vp.wheels.Length; i++)
            {
                if (_wheels[i].connected)
                {
                    if (Mathf.Abs(F.MaxAbs(_wheels[i].sidewaysSlip, _wheels[i].forwardSlip, alwaysScrape)) - _slipThreshold > 0)
                    {
                        if (_wheels[i].popped)
                        {
                            nonePopped = false;
                        }
                        else
                        {
                            allPopped = false;
                        }
                    }

                    if (_wheels[i].grounded)
                    {
                        _surfaceType = GroundSurfaceMaster.surfaceTypesStatic[_wheels[i].contactPoint.surfaceType];

                        if (_surfaceType.alwaysScrape)
                        {
                            alwaysScrape = _slipThreshold + Mathf.Min(0.5f, Mathf.Abs(_wheels[i].rawRPM * 0.001f));
                        }
                    }

                    screechAmount = Mathf.Max(screechAmount, Mathf.Pow(Mathf.Clamp01(Mathf.Abs(F.MaxAbs(_wheels[i].sidewaysSlip, _wheels[i].forwardSlip, alwaysScrape)) - _slipThreshold), 2));
                }
            }

            // Set audio clip based on number of wheels popped
            if (_surfaceType != null)
            {
                _snd.clip = allPopped ? _surfaceType.rimSnd : (nonePopped ? _surfaceType.tireSnd : _surfaceType.tireRimSnd);
            }

            // Set sound volume and pitch
            if (screechAmount > 0)
            {
                if (!_snd.isPlaying)
                {
                    _snd.Play();
                    _snd.volume = 0;
                }
                else
                {
                    _snd.volume = Mathf.Lerp(_snd.volume, screechAmount * ((_vp.groundedWheels * 1.0f) / (_wheels.Length * 1.0f)), 2 * Time.deltaTime);
                    _snd.pitch = Mathf.Lerp(_snd.pitch, 0.5f + screechAmount * 0.9f, 2 * Time.deltaTime);
                }
            }
            else if (_snd.isPlaying)
            {
                _snd.volume = 0;
                _snd.Stop();
            }
        }
    }
}