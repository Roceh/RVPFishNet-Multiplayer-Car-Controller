using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(AudioListener))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Camera/Camera Control", 0)]
    public class CameraControl : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")] 
        public float height;

        [Tooltip("")] 
        public float distance;

        [Tooltip("Should the camera stay flat? (Local y-axis always points up)")]
        public bool stayFlat;

        [Tooltip("Mask for which objects will be checked in between the camera and target vehicle")]
        public LayerMask castMask;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Transform _tr;
        private Camera _cam;
        private Rigidbody _rb;
        private float _xInput;
        private float _yInput;
        private Vector3 _lookDir;
        private float _smoothYRot;
        private Transform _lookObj;
        private Vector3 _forwardLook;
        private Vector3 _upLook;
        private Vector3 _targetForward;
        private Vector3 _targetUp;
        private VehicleParent _vp;
        private Transform _target;

        public void Initialize(Transform target, VehicleParent vp)
        {
            _target = target;
            _vp = vp;

            // lookObj is an object used to help position and rotate the camera
            if (!_lookObj)
            {
                GameObject lookTemp = new GameObject("Camera Looker");
                _lookObj = lookTemp.transform;
            }

            // Set variables based on target vehicle's properties
            if (target)
            {
                distance += vp.cameraDistanceChange;
                height += vp.cameraHeightChange;
                _forwardLook = target.forward;
                _upLook = target.up;
                _rb = vp.GetComponent<Rigidbody>();
            }

            // Set the audio listener update mode to fixed, because the camera moves in FixedUpdate
            // This is necessary for doppler effects to sound correct
            GetComponent<AudioListener>().velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
        }

        // function for setting the rotation input of the camera
        public void SetInput(float x, float y)
        {
            _xInput = x;
            _yInput = y;
        }

        private void Start()
        {
            _tr = transform;
            _cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (_vp != null)
            {
                UpdateCameraView();
            }
        }

        private void UpdateCameraView()
        {
            if (_vp && _vp.norm && _target && _rb && _target.gameObject.activeSelf)
            {
                if (_vp.groundedWheels > 0 || _targetForward == Vector3.zero)
                {
                    _targetForward = stayFlat ? new Vector3(_vp.norm.up.x, 0, _vp.norm.up.z) : _vp.norm.up;
                }

                float inverseDeltaTime = 1.0f - Time.deltaTime;

                _targetUp = stayFlat ? GlobalControl.worldUpDir : _vp.norm.forward;
                _lookDir = Vector3.Slerp(_lookDir, (_xInput == 0 && _yInput == 0 ? Vector3.forward : new Vector3(_xInput, 0, _yInput).normalized), 0.1f * inverseDeltaTime);
                _smoothYRot = Mathf.Lerp(_smoothYRot, _rb.angularVelocity.y, 0.02f * inverseDeltaTime);

                // Determine the upwards direction of the camera
                RaycastHit hit;
                if (Physics.Raycast(_target.position, -_targetUp, out hit, 1, castMask) && !stayFlat)
                {
                    _upLook = Vector3.Lerp(_upLook, (Vector3.Dot(hit.normal, _targetUp) > 0.5 ? hit.normal : _targetUp), 0.05f * inverseDeltaTime);
                }
                else
                {
                    _upLook = Vector3.Lerp(_upLook, _targetUp, 0.05f * inverseDeltaTime);
                }

                // Calculate rotation and position variables
                _forwardLook = Vector3.Lerp(_forwardLook, _targetForward, 0.05f * inverseDeltaTime);
                _lookObj.rotation = Quaternion.LookRotation(_forwardLook, _upLook);
                _lookObj.position = _target.position;
                Vector3 lookDirActual = (_lookDir - new Vector3(Mathf.Sin(_smoothYRot), 0, Mathf.Cos(_smoothYRot)) * Mathf.Abs(_smoothYRot) * 0.2f).normalized;
                Vector3 forwardDir = _lookObj.TransformDirection(lookDirActual);
                Vector3 localOffset = _lookObj.TransformPoint(-lookDirActual * distance - lookDirActual * Mathf.Min(_rb.velocity.magnitude * 0.05f, 2) + Vector3.up * height);

                // Check if there is an object between the camera and target vehicle and move the camera in front of it
                if (Physics.Linecast(_target.position, localOffset, out hit, castMask))
                {
                    _tr.position = hit.point + (_target.position - localOffset).normalized * (_cam.nearClipPlane + 0.1f);
                }
                else
                {
                    _tr.position = localOffset;
                }

                _tr.rotation = Quaternion.LookRotation(forwardDir, _lookObj.up);
            }
        }

        // Destroy lookObj
        private void OnDestroy()
        {
            if (_lookObj)
            {
                Destroy(_lookObj.gameObject);
            }
        }
    }
}