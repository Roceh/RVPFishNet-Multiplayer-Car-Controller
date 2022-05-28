﻿using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace RVP
{
    /// <summary>
    /// This is just until FGG implements a proper solution for caching previous state during preconcile. Its based upon his idea!
    /// This is a bit of a bodge as it gets the current Reconcile tick from the PredicatedVehicle static value which is not good.
    /// </summary>
    public partial class PredictedObjectCache : NetworkBehaviour
    {
        /// <summary>
        /// How often to synchronize values from server to clients when no changes have been detected.
        /// </summary>
        protected const float SEND_INTERVAL = 1f;

        /// <summary>
        /// How many server states we will save
        /// </summary>
        private const uint CacheSize = 10;

        /// <summary>
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        [Tooltip("Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.")]
        [SerializeField]
        private Transform _graphicalObject;

        /// <summary>
        /// Duration to smooth desynchronizations over.
        /// </summary>
        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _smoothingDuration = 0.125f;

        /// <summary>
        /// Rigidbody to predict.
        /// </summary>
        [Tooltip("Rigidbody to predict.")]
        [SerializeField]
        private Rigidbody _rigidbody;

        /// <summary>
        /// True if subscribed to events.
        /// </summary>
        private bool _subscribed;

        /// <summary>
        /// World position before transform was predicted or reset.
        /// </summary>
        private Vector3 _previousPosition;

        /// <summary>
        /// World rotation before transform was predicted or reset.
        /// </summary>
        private Quaternion _previousRotation;

        /// <summary>
        /// Local position of transform when instantiated.
        /// </summary>
        private Vector3 _instantiatedLocalPosition;

        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate;

        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedLocalRotation;

        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate;

        /// <summary>
        /// Index of last cache we stored
        /// </summary>
        private uint _replayCacheIndex;

        /// <summary>
        /// This is a not very efficient method caching state
        /// </summary>
        private CachedRigidbodyState[] _replayCache = new CachedRigidbodyState[CacheSize];

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
            _instantiatedLocalPosition = _graphicalObject.localPosition;
            _instantiatedLocalRotation = _graphicalObject.localRotation;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ChangeSubscriptions(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            ChangeSubscriptions(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (base.TimeManager != null)
            {
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        protected void TimeManager_OnPostTick()
        {
            if (base.IsClient)
            {
                ResetToTransformPreviousProperties();
                SetTransformMoveRates();
            }

            // another bodge - there are going to multiple vehicles - this assumes the reconcilation rate is the same on all
            if (base.IsServer && PredictedVehicle.ReconcilationGlobalTickStep != 0)
            {
                uint localTick = base.TimeManager.LocalTick;

                // ok this is a bit greed - its sending its position regrdless of its moving or not
                if ((localTick % PredictedVehicle.ReconcilationGlobalTickStep) == 0)
                {
                    SendRigidbodyState(localTick + 1);
                }
            }
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        protected virtual void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            SetPreviousTransformProperties();
        }

        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        protected bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
        {
            return (_graphicalObject.localPosition == position && _graphicalObject.localRotation == rotation);
        }

        private void Awake()
        {
            //Set in awake so they arent default.
            SetPreviousTransformProperties();

            if (Application.isPlaying)
                InitializeOnce();
        }

        private void Update()
        {
            MoveToTarget();
        }

        /// <summary>
        /// Sends current states of this object to client.
        /// </summary>
        private void SendRigidbodyState(uint tick)
        {
            RigidbodyState state = new RigidbodyState
            {
                Position = _rigidbody.transform.position,
                Rotation = _rigidbody.transform.rotation,
                Velocity = _rigidbody.velocity,
                AngularVelocity = _rigidbody.angularVelocity
            };

            ObserversSendRigidbodyState(tick, state);
        }

        private void NextReplayCacheSlot()
        {
            // get next cache slot
            _replayCacheIndex++;
            if (_replayCacheIndex >= _replayCache.Length)
                _replayCacheIndex = 0;
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        /// <param name="state"></param>
        [ObserversRpc(IncludeOwner = false, BufferLast = true)]
        private void ObserversSendRigidbodyState(uint tick, RigidbodyState state, Channel channel = Channel.Unreliable)
        {
            if (!base.IsOwner && !base.IsServer)
            {
                SetPreviousTransformProperties();
                _rigidbody.transform.position = state.Position;
                _rigidbody.transform.rotation = state.Rotation;
                _rigidbody.velocity = state.Velocity;
                _rigidbody.angularVelocity = state.AngularVelocity;
                ResetToTransformPreviousProperties();
                SetTransformMoveRates();

                // move to next cache slot
                NextReplayCacheSlot();

                // store state in cache slot
                _replayCache[_replayCacheIndex] = new CachedRigidbodyState { Tick = tick, State = state };
            }
        }

        private bool FindReplayCacheState(uint tick, out CachedRigidbodyState state)
        {
            state = default;

            for (int i = 0; i < CacheSize; i++)
            {
                if (_replayCache[i].Tick == tick)
                {
                    state = _replayCache[i];
                    return true;
                }
            }

            return false;
        }

        private void TimeManager_OnPreReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (!base.IsOwner && !base.IsServer)
            {
                if (FindReplayCacheState(PredictedVehicle.ReplayTick, out CachedRigidbodyState cache))
                {
                    _rigidbody.transform.position = cache.State.Position;
                    _rigidbody.transform.rotation = cache.State.Rotation;
                    _rigidbody.velocity = cache.State.Velocity;
                    _rigidbody.angularVelocity = cache.State.AngularVelocity;
                }
            }
        }

        private void TimeManager_OnPreTick()
        {
            SetPreviousTransformProperties();
        }

        /// <summary>
        /// Subscribes to events needed to function.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            if (base.TimeManager == null)
                return;
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPreReplicateReplay += TimeManager_OnPreReplicateReplay;
            }
            else
            {
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPreReplicateReplay -= TimeManager_OnPreReplicateReplay;
            }

            _subscribed = subscribe;
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            //No graphical object, cannot smooth.
            if (_graphicalObject == null)
            {
                if (NetworkManager.StaticCanLog(LoggingType.Error))
                    Debug.LogError($"GraphicalObject is not set on {gameObject.name}. Initialization will fail.");
                return;
            }
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            //Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -1f && _rotationMoveRate == -1f)
                return;

            Transform t = _graphicalObject;
            float delta = Time.deltaTime;
            if (_positionMoveRate > 0f)
                t.localPosition = Vector3.MoveTowards(t.localPosition, _instantiatedLocalPosition, _positionMoveRate * delta);
            if (_rotationMoveRate > 0f)
                t.localRotation = Quaternion.RotateTowards(t.localRotation, _instantiatedLocalRotation, _rotationMoveRate * delta);

            if (GraphicalObjectMatches(_instantiatedLocalPosition, _instantiatedLocalRotation))
            {
                _positionMoveRate = -1f;
                _rotationMoveRate = -1f;
            }
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        /// <param name="durationOverride">Smooth of this duration when not set to -1f. Otherwise TimeManager.TickDelta is used.</param>
        private void SetTransformMoveRates(float durationOverride = -1f)
        {
            float delta = (durationOverride == -1f) ? (float)base.TimeManager.TickDelta : durationOverride;
            float distance;

            distance = Vector3.Distance(_instantiatedLocalPosition, _graphicalObject.localPosition);
            _positionMoveRate = (distance / delta);
            distance = Quaternion.Angle(_instantiatedLocalRotation, _graphicalObject.localRotation);
            if (distance > 0f)
                _rotationMoveRate = (distance / delta);
        }

        /// <summary>
        /// Caches the transforms current position and rotation.
        /// </summary>
        private void SetPreviousTransformProperties()
        {
            _previousPosition = _graphicalObject.position;
            _previousRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Resets the transform to cached position and rotation of the transform.
        /// </summary>
        private void ResetToTransformPreviousProperties()
        {
            _graphicalObject.position = _previousPosition;
            _graphicalObject.rotation = _previousRotation;
        }

        public struct RigidbodyState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }

        public struct CachedRigidbodyState
        {
            public uint Tick;
            public RigidbodyState State;
        }
    }
}