using FishNet;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
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

        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        public float smoothingDuration = 0.05f;

        // -=-=-=-= LOCAL STATE =-=-=-=-

#if SYNC_DEBUG
        /// <summary>
        /// Used to determine current replay tick i.e. what equivilent server tick were are currenting on
        /// </summary>
        public static uint ReplayTick;
#endif

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
        /// Local rotation of visual object transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedLocalRotation;

        /// <summary>
        /// Used to read data from a stream
        /// </summary>
        private Writer _writer = new Writer();

        /// <summary>
        /// Holds the last received state if this is an non player controlled vehicle
        /// </summary>
        private CachedStateInfo? _cachedStateInfo;

        /// <summary>
        /// Used to cache last move on server and during replaying
        /// </summary>
        private BasicInput.MoveData _lastMove;

        /// <summary>
        /// Velocity for smoothing of position
        /// </summary>
        private Vector3 _smoothingPositionVelocity = Vector3.zero;

        /// <summary>
        /// Velocity for smoothing of rotation
        /// </summary>
        private float _smoothingRotationVelocity;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

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
#if SYNC_DEBUG
                base.TimeManager.OnPostReplicateReplay += TimeManager_OnPostReplicateReplay;
#endif
            }
            else
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
                base.TimeManager.OnPreReplicateReplay -= TimeManager_OnPreReplicateReplay;
#if SYNC_DEBUG
                base.TimeManager.OnPostReplicateReplay -= TimeManager_OnPostReplicateReplay;
#endif
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

#if SYNC_DEBUG
            ReplayTick = reader.ReadUInt32(AutoPackType.Unpacked);
#endif
      
            // sync debug - not compiled unless SYNC_DEBUG is set as global define
            StaticStateLogger.Log("************************************");
            StaticStateLogger.Log($"RECONCILE START");

            _vm.SetFullState(reader);
        }

        public static Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref float AngularVelocity, float smoothTime)
        {
            var delta = Quaternion.Angle(current, target);
            if (delta > 0.0f)
            {
                var t = Mathf.SmoothDampAngle(delta, 0.0f, ref AngularVelocity, smoothTime);
                t = 1.0f - t / delta;
                return Quaternion.Slerp(current, target, t);
            }

            return current;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            Transform t = vehicleVisualRootObject.transform;
            t.localPosition = Vector3.SmoothDamp(t.localPosition, _instantiatedLocalPosition, ref _smoothingPositionVelocity, smoothingDuration);
            t.localRotation = SmoothDampQuaternion(t.localRotation, _instantiatedLocalRotation, ref _smoothingRotationVelocity, smoothingDuration);
        }

        private void TimeManager_OnPreReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
#if SYNC_DEBUG
            // sync debug - not compiled unless SYNC_DEBUG is set as global define
            StaticStateLogger.Log("************************************");
            StaticStateLogger.Log($"TICK: {ReplayTick}");
#endif

            if (!base.IsOwner && !base.IsServer)
            {
                // for non server and non owner - we want to setup the forces before fishnet calls physics simulate
                SimulateWithMove(ref _lastMove);
            }
        }

#if DEBUG_SYNC
        private void TimeManager_OnPostReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
            if (base.IsOwner)
            {
                ReplayTick++;
            }
        }
#endif

        private void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            // this is so we can restore the visual state to what it was and lerp to new visual position/rotation after reconcile is done
            SetPreviousTransformProperties();

            // if this is a non player vehicle we want to simulate it using last received data (if available)
            if (!base.IsOwner && !base.IsServer)
            {
                // reset to no move
                _lastMove = default;

                // have we received info about the state of this vehicle on the server ?
                // if we haven't we just simulate from this point 
                if (_cachedStateInfo.HasValue)
                {
                    var reader = new Reader(_cachedStateInfo.Value.Data, base.NetworkManager);
                    _vm.SetFullState(reader);
                    _lastMove = _cachedStateInfo.Value.Move;
                }
            }
        }

        private void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            // Set transform back to where it was before reconcile so there's no visual disturbances.
            vehicleVisualRootObject.SetPositionAndRotation(_previousPosition, _previousRotation);
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
                if ((localTick % GlobalReconcilation.Instance.TicksBetweenReconcilation) == 0)
                {
                    // tell other clients the current state 
                    SendVehicleToObservers();

                    // create reconcilation data
                    _writer.Reset(this.NetworkManager);

#if SYNC_DEBUG
                    _writer.WriteUInt32(localTick + 1, AutoPackType.Unpacked);
#endif

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

            // reset visual object to what it was before physics step
            ResetToTransformPreviousProperties();
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

        private void SendVehicleToObservers()
        {
            _writer.Reset();
            _vm.GetFullState(_writer);

            // FIXME: REMOVE ALLOCATION - custom serialisation???
            var stateData = new byte[_writer.Length];
            Array.Copy(_writer.GetBuffer(), stateData, _writer.Length);
            
            // send the move and state data to the other clients
            ObserversSendVehicleState(_lastMove, stateData);
        }


        [ObserversRpc(IncludeOwner = false, BufferLast = true)]
        private void ObserversSendVehicleState(BasicInput.MoveData lastMove, byte[] stateData, Channel channel = Channel.Unreliable)
        {
            // ignore if we are controlling this vehicle (owner and server)
            if (!base.IsServer && !base.IsOwner)
            {
                // we just place in cache as this data is already old regards the client tick
                // so when the client reconcilates we will use this cache to fix up this vehicle position

                // store state in cache slot
                _cachedStateInfo = new CachedStateInfo { Move = lastMove, Data = stateData };
            }
        }

        public struct CachedStateInfo
        {
            public BasicInput.MoveData Move;
            public byte[] Data;
        }

        public struct ReconcilationData
        {
            public byte[] Data;
        }
    }
}