using FishNet;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleManager))]
    [RequireComponent(typeof(VehicleParent))]
    public class PredictedVehicle : NetworkBehaviour
    {
        /// <summary>
        /// Root transform for visual car elements
        /// </summary>
        [Tooltip("Root transform holding all car visual game objects")]
        public Transform vehicleVisualRootObject;

        /// <summary>
        /// Root object for non visual car elements
        /// </summary>
        [Tooltip("Root object holding all car scripting game objects")]
        public GameObject vehicleScriptRootObject;

        /// <summary>
        /// Duration to smooth desynchronizations over.
        /// </summary>
        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        public float smoothingDuration = 0.125f;

        /// <summary>
        /// What interval to send Reconcilation back to client
        /// </summary>
        [Tooltip("How often (every n ticks) to send reconcilation to client.")]
        [Range(1, 50)]
        public uint reconcilationTickStep = 10;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        /// <summary>
        /// Number of previous server states to cache so we can restore them during reconcile when we are
        /// simulating an non player controlled vehicle on the client
        /// </summary>
        private const uint CacheSize = 10;

        /// <summary>
        /// Used to determine current replay tick i.e. what equivilent server tick were are currenting on
        /// </summary>
        public static uint ReplayTick;

        /// <summary>
        /// Bodge so other predicted items now when to send their state to the observers
        /// </summary>
        public static uint ReconcilationGlobalTickStep;

        /// <summary>
        /// True if we have subcribed to time manager tick events
        /// </summary>
        private bool _subscribed = false;

        /// <summary>
        /// User input handler
        /// </summary>
        private BasicInput _input;

        /// <summary>
        /// Car Controller core state
        /// </summary>
        private VehicleParent _vp;

        /// <summary>
        /// Car Controller manager
        /// </summary>
        private VehicleManager _vm;

        /// <summary>
        /// World position of visual object before transform was predicted or reset.
        /// </summary>
        private Vector3 _previousPosition;

        /// <summary>
        /// World rotation of visual object before transform was predicted or reset.
        /// </summary>
        private Quaternion _previousRotation;

        /// <summary>
        /// Local position of visual object of transform when instantiated.
        /// </summary>
        private Vector3 _instantiatedLocalPosition;

        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate;

        /// <summary>
        /// Local rotation of visual object transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedLocalRotation;

        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate;

        /// <summary>
        /// Used to read data from a stream
        /// </summary>
        private Writer _writer = new Writer();

        /// <summary>
        /// This is a not very efficient method of hold cache
        /// </summary>
        private CachedStateInfo[] _replayCache = new CachedStateInfo[CacheSize];

        /// <summary>
        /// Used to cache last move on server and during replaying
        /// </summary>
        private BasicInput.MoveData _lastMove;

        /// <summary>
        /// Last cache position we inserted in
        /// </summary>
        private int _replayCacheIndex = -1;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            ReconcilationGlobalTickStep = reconcilationTickStep;

            _instantiatedLocalPosition = vehicleVisualRootObject.localPosition;
            _instantiatedLocalRotation = vehicleVisualRootObject.localRotation;
        }

        private void ChangeSubscriptions(bool subscribe)
        {
            if (base.TimeManager == null)
                return;
            if (subscribe == _subscribed)
                return;

            _subscribed = subscribe;

            if (subscribe)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                base.TimeManager.OnPostTick += TimeManager_OnPostTick;
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
                base.TimeManager.OnPreReplicateReplay += TimeManager_OnPreReplicateReplay;
                base.TimeManager.OnPostReplicateReplay += TimeManager_OnPostReplicateReplay;
            }
            else
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
                base.TimeManager.OnPreReplicateReplay -= TimeManager_OnPreReplicateReplay;
                base.TimeManager.OnPostReplicateReplay -= TimeManager_OnPostReplicateReplay;
            }
        }

        private void Start()
        {
            // we setup the tick subscription here otherwise TimeManger.Tick could fire before the simulation scripts have initialised
            ChangeSubscriptions(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (base.IsOwner)
            {
                // client is controlling this - so setup camera
                var cfl = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraControl>();
                cfl.Initialize(vehicleVisualRootObject, _vp);
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (base.IsServer)
            {
                // sync debug - not compiled unless SYNC_DEBUG is set as global define
                StaticStateLogger.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.productName + "_LOG_SERVER.txt"));
            }
            else
            {
                // sync debug - not compiled unless SYNC_DEBUG is set as global define
                StaticStateLogger.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.productName + "_LOG_CLIENT.txt"));
            }

            ChangeSubscriptions(false);
        }

        private void Awake()
        {
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _input = GetComponent<BasicInput>();
            SetPreviousTransformProperties();
        }

        private void OnDestroy()
        {
            ChangeSubscriptions(false);
        }

        private void SimulateWithMove(ref BasicInput.MoveData md)
        {
            _vp.SetAccel(md.AccelInput);
            _vp.SetBrake(md.BrakeInput);
            _vp.SetSteer(md.SteerInput);
            _vp.SetEbrake(md.EbrakeInput);
            _vp.SetBoost(md.BoostButton);
            _vp.SetUpshift(md.UpshiftInput);
            _vp.SetDownshift(md.DownshiftInput);
            _vp.SetPitch(md.PitchInput);
            _vp.SetYaw(md.YawInput);
            _vp.SetRoll(md.RollInput);

            if (md.UpshiftButton)
                _vp.PressUpshift();
            if (md.DownshiftButton)
                _vp.PressDownshift();

            _vm.Simulate((float)InstanceFinder.TimeManager.TickDelta);
        }

        [Replicate]
        private void Move(BasicInput.MoveData md, bool asServer, bool replaying = false)
        {
            // this is so we can tell the other clients what our last move was
            if (base.IsServer)
                _lastMove = md;

            SimulateWithMove(ref md);
        }

        [Reconcile]
        private void Reconciliation(ReconcilationData rd, bool asServer)
        {
            var reader = new Reader(rd.Data, this.NetworkManager);

            ReplayTick = reader.ReadUInt32(AutoPackType.Unpacked);

            // sync debug - not compiled unless SYNC_DEBUG is set as global define
            StaticStateLogger.Log("************************************");
            StaticStateLogger.Log($"RECONCILE START");

            _vm.SetFullState(reader);
        }

        private bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
        {
            return (vehicleVisualRootObject.localPosition == position && vehicleVisualRootObject.localRotation == rotation);
        }

        private void MoveToTarget()
        {
            // Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -1f && _rotationMoveRate == -1f)
                return;

            Transform t = vehicleVisualRootObject;
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

        private void SetTransformMoveRates(float durationOverride = -1f)
        {
            float delta = (durationOverride == -1f) ? (float)base.TimeManager.TickDelta : durationOverride;
            float distance;

            distance = Vector3.Distance(_instantiatedLocalPosition, vehicleVisualRootObject.localPosition);
            _positionMoveRate = (distance / delta);
            distance = Quaternion.Angle(_instantiatedLocalRotation, vehicleVisualRootObject.localRotation);
            if (distance > 0f)
                _rotationMoveRate = (distance / delta);
        }

        private bool FindReplayCacheState(uint tick, out CachedStateInfo state)
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

        private void TimeManager_OnPreReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
            // sync debug - not compiled unless SYNC_DEBUG is set as global define
            StaticStateLogger.Log("************************************");
            StaticStateLogger.Log($"TICK: {ReplayTick}");

            if (!base.IsOwner && !base.IsServer)
            {
                // for non server and non owner vehicles we try and get the actual state from the server for this item
                // and use that from going forward
                if (FindReplayCacheState(ReplayTick, out CachedStateInfo cache))
                {
                    var reader = new Reader(cache.Data, base.NetworkManager);
                    _vm.SetFullState(reader);
                    _lastMove = cache.Move;
                }

                // for non server and non owner - we want to setup the forces before fishnet calls physics simulate
                SimulateWithMove(ref _lastMove);
            }
        }

        private void TimeManager_OnPostReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
            if (base.IsOwner)
            {
                ReplayTick++;
            }
        }

        private void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            // this is so we can restore the visual state to what it was and lerp to new visual position/rotation after reconcile is done
            SetPreviousTransformProperties();
            
            // reset to no move
            _lastMove = default;
        }

        private void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            // Set transform back to where it was before reconcile so there's no visual disturbances.
            vehicleVisualRootObject.SetPositionAndRotation(_previousPosition, _previousRotation);

            // determine rate to move visual object position/rotation to
            SetTransformMoveRates(smoothingDuration);
        }

        private void TimeManager_OnPreTick()
        {
            // used to smooth out physics simulation
            SetPreviousTransformProperties();
        }

        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                Reconciliation(default, false);
                _input.CheckInput(out BasicInput.MoveData md);
                Move(md, false);
            }

            if (base.IsServer)
            {
                // sync debug - not compiled unless SYNC_DEBUG is set as global define
                StaticStateLogger.Log("************************************");
                StaticStateLogger.Log($"TICK: {TimeManager.LocalTick}");

                Move(default, true);
            }

            if (!base.IsOwner && !base.IsServer)
            {
                SimulateWithMove(ref _lastMove);
            }
        }

        private void TimeManager_OnPostTick()
        {
            if (base.IsServer)
            {
                uint localTick = base.TimeManager.LocalTick;

                // we reconcilate at reduce tick step for bandwidth saving!
                if ((localTick % reconcilationTickStep) == 0)
                {
                    // tell other clients the current state 
                    SendVehicleToObservers(localTick + 1);

                    // create reconcilation data
                    _writer.Reset(this.NetworkManager);
                    _writer.WriteUInt32(localTick + 1, AutoPackType.Unpacked);

                    // sync debug - not compiled unless SYNC_DEBUG is set as global define
                    StaticStateLogger.Log("************************************");
                    StaticStateLogger.Log($"RECONCILE START");

                    // get the current state of the vehicle simulation
                    _vm.GetFullState(_writer);

                    // FIXME: REMOVE ALLOCATION - custom serialisation???
                    var rd = new ReconcilationData { Data = new byte[_writer.Length] };
                    Array.Copy(_writer.GetBuffer(), rd.Data, _writer.Length);

                    // tell owner the current servers simulation state
                    Reconciliation(rd, true);
                }
            }

            if (base.IsOwner)
            {
                // reset visual object to what it was before physics step
                ResetToTransformPreviousProperties();
                // determine rate of change to lerp to new visual position/rotation
                SetTransformMoveRates();
            }
        }

        private void SetPreviousTransformProperties()
        {
            _previousPosition = vehicleVisualRootObject.position;
            _previousRotation = vehicleVisualRootObject.rotation;
        }

        private void ResetToTransformPreviousProperties()
        {
            vehicleVisualRootObject.position = _previousPosition;
            vehicleVisualRootObject.rotation = _previousRotation;
        }

        private void Update()
        {
            MoveToTarget();
        }

        private void SendVehicleToObservers(uint tick)
        {
            _writer.Reset();
            _vm.GetFullState(_writer);

            // FIXME: REMOVE ALLOCATION - custom serialisation???
            var stateData = new byte[_writer.Length];
            Array.Copy(_writer.GetBuffer(), stateData, _writer.Length);
            
            // send the move and state data to the other clients
            ObserversSendVehicleState(tick, _lastMove, stateData);
        }

        private void NextReplayCacheSlot()
        {
            // get next cache slot
            _replayCacheIndex++;
            if (_replayCacheIndex >= _replayCache.Length)
                _replayCacheIndex = 0;
        }

        [ObserversRpc(IncludeOwner = false, BufferLast = true)]
        private void ObserversSendVehicleState(uint tick, BasicInput.MoveData lastMove, byte[] stateData, Channel channel = Channel.Unreliable)
        {
            // ignore if we are controlling this vehicle (owner and server)
            if (!base.IsServer && !base.IsOwner)
            {
                // we just place in cache as this data is already old regards the client tick
                // so when the client reconcilates we will use this cache to fix up this vehicle position

                // move to next cache slot
                NextReplayCacheSlot();

                // store state in cache slot
                _replayCache[_replayCacheIndex] = new CachedStateInfo { Tick = tick, Move = lastMove, Data = stateData };
            }
        }

        public struct CachedStateInfo
        {
            public uint Tick;
            public BasicInput.MoveData Move;
            public byte[] Data;
        }

        public struct ReconcilationData
        {
            public byte[] Data;
        }
    }
}