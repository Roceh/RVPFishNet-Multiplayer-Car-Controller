using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Stunt/Stunt Detector", 1)]
    public class StuntDetect : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public bool detectDrift = true;

        [Tooltip("")]
        public bool detectJump = true;

        [Tooltip("")]
        public bool detectFlips = true;

        [Tooltip("")]
        public Motor engine;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float score;

        [System.NonSerialized]
        public float endDriftTime;

        [System.NonSerialized]
        public string stuntString;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private List<Stunt> _stunts = new List<Stunt>();
        private List<Stunt> _doneStunts = new List<Stunt>();
        private bool _drifting;
        private float _driftDist;
        private float _driftScore;
        private float _jumpDist;
        private float _jumpTime;
        private Vector3 _jumpStart;
        private string _driftString; // String indicating drift distance
        private string _jumpString; // String indicating jump distance
        private string _flipString; // String indicating flips

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
            writer.WriteSingle(score);
            writer.WriteSingle(endDriftTime);
            writer.WriteBoolean(_drifting);
            writer.WriteSingle(_driftDist);
            writer.WriteSingle(_driftScore);
            writer.WriteSingle(_jumpTime);
            writer.WriteSingle(_jumpDist);
            writer.WriteVector3(_jumpStart);
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
            score = reader.ReadSingle();
            endDriftTime = reader.ReadSingle();
            _drifting = reader.ReadBoolean();
            _driftDist = reader.ReadSingle();
            _driftScore = reader.ReadSingle();
            _jumpTime = reader.ReadSingle();
            _jumpDist = reader.ReadSingle();
            _jumpStart = reader.ReadVector3();
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            // Detect drifts
            if (detectDrift && !_vp.crashing)
            {
                DetectDrift();
            }
            else
            {
                _drifting = false;
                _driftDist = 0;
                _driftScore = 0;
                _driftString = "";
            }

            // Detect jumps
            if (detectJump && !_vp.crashing)
            {
                DetectJump();
            }
            else
            {
                _jumpTime = 0;
                _jumpDist = 0;
                _jumpString = "";
            }

            // Detect flips
            if (detectFlips && !_vp.crashing)
            {
                DetectFlips();
            }
            else
            {
                _stunts.Clear();
                _flipString = "";
            }

            // Combine strings into final stunt string
            stuntString = _vp.crashing ? "Crashed" : _driftString + _jumpString + (string.IsNullOrEmpty(_flipString) || string.IsNullOrEmpty(_jumpString) ? "" : " + ") + _flipString;
        }

        // String containing all stunts
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, 0);
        }

        // Logic for detecting and tracking drift
        private void DetectDrift()
        {
            endDriftTime = _vp.groundedWheels > 0 ? (Mathf.Abs(_vp.localVelocity.x) > 5 ? StuntManager.driftConnectDelayStatic : Mathf.Max(0, endDriftTime - Time.timeScale * TimeMaster.inverseFixedTimeFactor)) : 0;
            _drifting = endDriftTime > 0;

            if (_drifting)
            {
                _driftScore += (StuntManager.driftScoreRateStatic * Mathf.Abs(_vp.localVelocity.x)) * Time.timeScale * TimeMaster.inverseFixedTimeFactor;
                _driftDist += _vp.velMag * _vm.tickDelta;
                _driftString = "Drift: " + _driftDist.ToString("n0") + " m";

                if (engine)
                {
                    engine.boost += (StuntManager.driftBoostAddStatic * Mathf.Abs(_vp.localVelocity.x)) * Time.timeScale * 0.0002f * TimeMaster.inverseFixedTimeFactor;
                }
            }
            else
            {
                score += _driftScore;
                _driftDist = 0;
                _driftScore = 0;
                _driftString = "";
            }
        }

        // Logic for detecting and tracking jumps
        private void DetectJump()
        {
            if (_vp.groundedWheels == 0)
            {
                _jumpDist = Vector3.Distance(_jumpStart, transform.position);
                _jumpTime += _vm.tickDelta;
                _jumpString = "Jump: " + _jumpDist.ToString("n0") + " m";

                if (engine)
                {
                    engine.boost += StuntManager.jumpBoostAddStatic * Time.timeScale * 0.01f * TimeMaster.inverseFixedTimeFactor;
                }
            }
            else
            {
                score += (_jumpDist + _jumpTime) * StuntManager.jumpScoreRateStatic;

                if (engine)
                {
                    engine.boost += (_jumpDist + _jumpTime) * StuntManager.jumpBoostAddStatic * Time.timeScale * 0.01f * TimeMaster.inverseFixedTimeFactor;
                }

                _jumpStart = transform.position;
                _jumpDist = 0;
                _jumpTime = 0;
                _jumpString = "";
            }
        }

        // Logic for detecting and tracking flips
        private void DetectFlips()
        {
            if (_vp.groundedWheels == 0)
            {
                // Check to see if vehicle is performing a stunt and add it to the stunts list
                foreach (Stunt curStunt in StuntManager.stuntsStatic)
                {
                    if (Vector3.Dot(_vp.localAngularVel.normalized, curStunt.rotationAxis) >= curStunt.precision)
                    {
                        bool stuntExists = false;

                        foreach (Stunt checkStunt in _stunts)
                        {
                            if (curStunt.name == checkStunt.name)
                            {
                                stuntExists = true;
                                break;
                            }
                        }

                        if (!stuntExists)
                        {
                            _stunts.Add(new Stunt(curStunt));
                        }
                    }
                }

                // Check the progress of stunts and compile the flip string listing all stunts
                foreach (Stunt curStunt2 in _stunts)
                {
                    if (Vector3.Dot(_vp.localAngularVel.normalized, curStunt2.rotationAxis) >= curStunt2.precision)
                    {
                        curStunt2.progress += _rb.angularVelocity.magnitude * _vm.tickDelta;
                    }

                    if (curStunt2.progress * Mathf.Rad2Deg >= curStunt2.angleThreshold)
                    {
                        bool stuntDoneExists = false;

                        foreach (Stunt curDoneStunt in _doneStunts)
                        {
                            if (curDoneStunt == curStunt2)
                            {
                                stuntDoneExists = true;
                                break;
                            }
                        }

                        if (!stuntDoneExists)
                        {
                            _doneStunts.Add(curStunt2);
                        }
                    }
                }

                string stuntCount = "";
                _flipString = "";

                foreach (Stunt curDoneStunt2 in _doneStunts)
                {
                    stuntCount = curDoneStunt2.progress * Mathf.Rad2Deg >= curDoneStunt2.angleThreshold * 2 ? " x" + Mathf.FloorToInt((curDoneStunt2.progress * Mathf.Rad2Deg) / curDoneStunt2.angleThreshold).ToString() : "";
                    _flipString = string.IsNullOrEmpty(_flipString) ? curDoneStunt2.name + stuntCount : _flipString + " + " + curDoneStunt2.name + stuntCount;
                }
            }
            else
            {
                // Add stunt points to the score
                foreach (Stunt curStunt in _stunts)
                {
                    score += curStunt.progress * Mathf.Rad2Deg * curStunt.scoreRate * Mathf.FloorToInt((curStunt.progress * Mathf.Rad2Deg) / curStunt.angleThreshold) * curStunt.multiplier;

                    // Add boost to the engine
                    if (engine)
                    {
                        engine.boost += curStunt.progress * Mathf.Rad2Deg * curStunt.boostAdd * curStunt.multiplier * 0.01f;
                    }
                }

                _stunts.Clear();
                _doneStunts.Clear();
                _flipString = "";
            }
        }
    }
}