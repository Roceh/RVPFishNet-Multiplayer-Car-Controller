using FishNet.Serializing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [RequireComponent(typeof(VehicleManager))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/AI/Follow AI", 0)]
    public class FollowAI : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public Transform target;

        [Tooltip("")]
        public float followDistance;

        [Tooltip("Percentage of maximum speed to drive at")]
        [Range(0, 1)]
        public float speed = 1;

        [Tooltip("")]
        public float targetVelocity = -1;

        [Tooltip("Mask for which objects can block the view of the target")]
        public LayerMask viewBlockMask;

        [Tooltip("Time limit in seconds which the vehicle is stuck before attempting to reverse")]
        public float stopTimeReverse = 1;

        [Tooltip("Duration in seconds the vehicle will reverse after getting stuck")]
        public float reverseAttemptTime = 1;

        [Tooltip("How many times the vehicle will attempt reversing before resetting, -1 = no reset")]
        public int resetReverseCount = 1;

        [Tooltip("Seconds a vehicle will be rolled over before resetting, -1 = no reset")]
        public float rollResetTime = 3;

        private Rigidbody _rb;
        private VehicleManager _vm;
        private VehicleParent _vp;
        private VehicleAssist _va;
        private Transform _targetPrev;
        private Rigidbody _targetBody;
        private Vector3 _targetPoint;
        private bool _targetVisible;
        private bool _targetIsWaypoint;
        private VehicleWaypoint _targetWaypoint;
        private bool _close;
        private float _initialSpeed;
        private float _prevSpeed;
        private float _speedLimit = 1;
        private float _brakeTime;
        private Vector3 _dirToTarget; // Normalized direction to target
        private float _lookDot; // Dot product of forward direction and dirToTarget
        private float _steerDot; // Dot product of right direction and dirToTarget
        private float _stoppedTime;
        private float _reverseTime;
        private int _reverseAttempts;
        private float _rolledOverTime;

        void IVehicleComponent.SetActive(bool state)
        {
            enabled = state;
        }

        void IVehicleComponent.GetFullState(Writer writer)
        {
        }

        void IVehicleComponent.SetFullState(Reader reader)
        {
        }

        void IVehicleComponent.GetVisualState(Writer writer)
        {
        }

        void IVehicleComponent.SetVisualState(Reader reader)
        {
        }

        void IVehicleComponent.Simulate()
        {
            if (target)
            {
                if (target != _targetPrev)
                {
                    InitializeTarget();
                }

                _targetPrev = target;

                // Is the target a waypoint?
                _targetIsWaypoint = target.GetComponent<VehicleWaypoint>();
                // Can I see the target?
                _targetVisible = !Physics.Linecast(transform.position, target.position, viewBlockMask);

                if (_targetVisible || _targetIsWaypoint)
                {
                    _targetPoint = _targetBody ? target.position + _targetBody.velocity : target.position;
                }

                if (_targetIsWaypoint)
                {
                    // if vehicle is close enough to target waypoint, switch to the next one
                    if ((transform.position - target.position).sqrMagnitude <= _targetWaypoint.radius * _targetWaypoint.radius)
                    {
                        target = _targetWaypoint.nextPoint.transform;
                        _targetWaypoint = _targetWaypoint.nextPoint;
                        _prevSpeed = speed;
                        speed = Mathf.Clamp01(_targetWaypoint.speed * _initialSpeed);
                        _brakeTime = _prevSpeed / speed;

                        if (_brakeTime <= 1)
                        {
                            _brakeTime = 0;
                        }
                    }
                }

                _brakeTime = Mathf.Max(0, _brakeTime - Time.fixedDeltaTime);
                // Is the distance to the target less than the follow distance?
                _close = (transform.position - target.position).sqrMagnitude <= Mathf.Pow(followDistance, 2) && !_targetIsWaypoint;
                _dirToTarget = (_targetPoint - transform.position).normalized;
                _lookDot = Vector3.Dot(_vp.forwardDir, _dirToTarget);
                _steerDot = Vector3.Dot(_vp.rightDir, _dirToTarget);

                // Attempt to reverse if vehicle is stuck
                _stoppedTime = Mathf.Abs(_vp.localVelocity.z) < 1 && !_close && _vp.groundedWheels > 0 ? _stoppedTime + Time.fixedDeltaTime : 0;

                if (_stoppedTime > stopTimeReverse && _reverseTime == 0)
                {
                    _reverseTime = reverseAttemptTime;
                    _reverseAttempts++;
                }

                // Reset if reversed too many times
                if (_reverseAttempts > resetReverseCount && resetReverseCount >= 0)
                {
                    StartCoroutine(ReverseReset());
                }

                _reverseTime = Mathf.Max(0, _reverseTime - Time.fixedDeltaTime);

                if (targetVelocity > 0)
                {
                    _speedLimit = Mathf.Clamp01(targetVelocity - _vp.localVelocity.z);
                }
                else
                {
                    _speedLimit = 1;
                }

                // Set accel input
                if (!_close && (_lookDot > 0 || _vp.localVelocity.z < 5) && _vp.groundedWheels > 0 && _reverseTime == 0)
                {
                    _vp.SetAccel(speed * _speedLimit);
                }
                else
                {
                    _vp.SetAccel(0);
                }

                // Set brake input
                if (_reverseTime == 0 && _brakeTime == 0 && !(_close && _vp.localVelocity.z > 0.1f))
                {
                    if (_lookDot < 0.5f && _lookDot > 0 && _vp.localVelocity.z > 10)
                    {
                        _vp.SetBrake(0.5f - _lookDot);
                    }
                    else
                    {
                        _vp.SetBrake(0);
                    }
                }
                else
                {
                    if (_reverseTime > 0)
                    {
                        _vp.SetBrake(1);
                    }
                    else
                    {
                        if (_brakeTime > 0)
                        {
                            _vp.SetBrake(_brakeTime * 0.2f);
                        }
                        else
                        {
                            _vp.SetBrake(1 - Mathf.Clamp01(Vector3.Distance(transform.position, target.position) / Mathf.Max(0.01f, followDistance)));
                        }
                    }
                }

                // Set steer input
                if (_reverseTime == 0)
                {
                    _vp.SetSteer(Mathf.Abs(Mathf.Pow(_steerDot, (transform.position - target.position).sqrMagnitude > 20 ? 1 : 2)) * Mathf.Sign(_steerDot));
                }
                else
                {
                    _vp.SetSteer(-Mathf.Sign(_steerDot) * (_close ? 0 : 1));
                }

                // Set ebrake input
                if ((_close && _vp.localVelocity.z <= 0.1f) || (_lookDot <= 0 && _vp.velMag > 20))
                {
                    _vp.SetEbrake(1);
                }
                else
                {
                    _vp.SetEbrake(0);
                }
            }

            _rolledOverTime = _va.rolledOver ? _rolledOverTime + Time.fixedDeltaTime : 0;

            // Reset if stuck rolled over
            if (_rolledOverTime > rollResetTime && rollResetTime >= 0)
            {
                StartCoroutine(ResetRotation());
            }
        }

        public void InitializeTarget()
        {
            if (target)
            {
                // if target is a vehicle
                _targetBody = target.GetTopmostParentComponent<Rigidbody>();

                // if target is a waypoint
                _targetWaypoint = target.GetComponent<VehicleWaypoint>();
                if (_targetWaypoint)
                {
                    _prevSpeed = _targetWaypoint.speed;
                }
            }
        }

        private void Awake()
        {
            _va = GetComponent<VehicleAssist>();
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, -150);
        }

        private void Start()
        {
            _initialSpeed = speed;
            InitializeTarget();
        }

        private IEnumerator ReverseReset()
        {
            _reverseAttempts = 0;
            _reverseTime = 0;
            yield return new WaitForFixedUpdate();
            transform.position = _targetPoint;
            transform.rotation = Quaternion.LookRotation(_targetIsWaypoint ? (_targetWaypoint.nextPoint.transform.position - _targetPoint).normalized : Vector3.forward, GlobalControl.worldUpDir);
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        private IEnumerator ResetRotation()
        {
            yield return new WaitForFixedUpdate();
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            transform.Translate(Vector3.up, Space.World);
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}