using UnityEngine;

namespace RVP
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Suspension/Suspension Part", 1)]
    public class SuspensionPart : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public Suspension suspension;

        [Tooltip("")]
        public bool isHub;

        [Header("Connections")]
        [Tooltip("Object to point at")]
        public Transform connectObj;

        [Tooltip("Local space point to point at in connectObj")]
        public Vector3 connectPoint;

        [Tooltip("Rotate to point at target?")]
        public bool rotate = true;

        [Tooltip("Scale along local z-axis to reach target?")]
        public bool stretch;

        [Header("Solid Axle")]
        [Tooltip("")]
        public bool solidAxle;

        [Tooltip("")]
        public bool invertRotation;

        [Tooltip("Does this part connect to a solid axle?")]
        public bool solidAxleConnector;

        [Tooltip("Wheel 1 for solid axle")]
        public Wheel wheel1;

        [Tooltip("Wheel 2 for solid axle")]
        public Wheel wheel2;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public Vector3 initialConnectPoint;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Transform _tr;
        private Wheel _wheel;
        private Vector3 _localConnectPoint; // Transformed connect point
        private float _initialDist;
        private Vector3 _initialScale;
        private Vector3 _wheelConnect1;
        private Vector3 _wheelConnect2;
        private Vector3 _parentUpDir; // Parent's up direction

        private void Start()
        {
            _tr = transform;
            initialConnectPoint = connectPoint;

            // Get the wheel
            if (suspension)
            {
                suspension.movingParts.Add(this);

                if (suspension.wheel)
                {
                    _wheel = suspension.wheel;
                }
            }

            // Get the initial distance from the target to use when stretching
            if (connectObj && !isHub && Application.isPlaying)
            {
                _initialDist = Mathf.Max(Vector3.Distance(_tr.position, connectObj.TransformPoint(connectPoint)), 0.01f);
                _initialScale = _tr.localScale;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                _tr = transform;

                // Get the wheel
                if (suspension)
                {
                    if (suspension.wheel)
                    {
                        _wheel = suspension.wheel;
                    }
                }
            }

            if (_tr)
            {
                if (!solidAxle && ((suspension && !solidAxleConnector) || solidAxleConnector))
                {
                    // Transformations for hubs
                    if (isHub && _wheel && !solidAxleConnector)
                    {
                        if (_wheel.visualRim)
                        {
                            _tr.position = _wheel.visualRim.position;
                            _tr.rotation = Quaternion.LookRotation(_wheel.visualRim.forward, suspension.upDir);
                            _tr.localEulerAngles = new Vector3(_tr.localEulerAngles.x, _tr.localEulerAngles.y, -suspension.casterAngle * suspension.flippedSideFactor);
                        }
                    }
                    else if (!isHub && connectObj)
                    {
                        _localConnectPoint = connectObj.TransformPoint(connectPoint);

                        // Rotate to look at connection point
                        if (rotate)
                        {
                            _tr.rotation = Quaternion.LookRotation((_localConnectPoint - _tr.position).normalized, (solidAxleConnector ? _tr.parent.forward : suspension.upDir));

                            // Don't set localEulerAngles if connected to a solid axle
                            if (!solidAxleConnector)
                            {
                                _tr.localEulerAngles = new Vector3(_tr.localEulerAngles.x, _tr.localEulerAngles.y, -suspension.casterAngle * suspension.flippedSideFactor);
                            }
                        }

                        // Stretch like a spring if stretch is true
                        if (stretch && Application.isPlaying)
                        {
                            _tr.localScale = new Vector3(_tr.localScale.x, _tr.localScale.y, _initialScale.z * (Vector3.Distance(_tr.position, _localConnectPoint) / _initialDist));
                        }
                    }
                }
                else if (solidAxle && wheel1 && wheel2)
                {
                    // Transformations for solid axles
                    if (wheel1.visualRim && wheel2.visualRim && wheel1.suspensionParent && wheel2.suspensionParent)
                    {
                        _parentUpDir = _tr.parent.up;
                        _wheelConnect1 = wheel1.visualRim.TransformPoint(0, 0, -wheel1.suspensionParent.pivotOffset);
                        _wheelConnect2 = wheel2.visualRim.TransformPoint(0, 0, -wheel2.suspensionParent.pivotOffset);
                        _tr.rotation = Quaternion.LookRotation((((_wheelConnect1 + _wheelConnect2) * 0.5f) - _tr.position).normalized, _parentUpDir);
                        _tr.localEulerAngles = new Vector3(
                            _tr.localEulerAngles.x,
                            _tr.localEulerAngles.y,
                            Vector3.Angle((_wheelConnect1 - _wheelConnect2).normalized, _tr.parent.right) * Mathf.Sign(Vector3.Dot((_wheelConnect1 - _wheelConnect2).normalized, _parentUpDir)) * Mathf.Sign(_tr.localPosition.z) * (invertRotation ? -1 : 1));
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_tr)
            {
                _tr = transform;
            }

            Gizmos.color = Color.green;

            // Visualize connections
            if (!isHub && connectObj && !solidAxle)
            {
                _localConnectPoint = connectObj.TransformPoint(connectPoint);
                Gizmos.DrawLine(_tr.position, _localConnectPoint);
                Gizmos.DrawWireSphere(_localConnectPoint, 0.01f);
            }
            else if (solidAxle && wheel1 && wheel2)
            {
                if (wheel1.visualRim && wheel2.visualRim && wheel1.suspensionParent && wheel2.suspensionParent)
                {
                    _wheelConnect1 = wheel1.visualRim.TransformPoint(0, 0, -wheel1.suspensionParent.pivotOffset);
                    _wheelConnect2 = wheel2.visualRim.TransformPoint(0, 0, -wheel2.suspensionParent.pivotOffset);
                    Gizmos.DrawLine(_wheelConnect1, _wheelConnect2);
                    Gizmos.DrawWireSphere(_wheelConnect1, 0.01f);
                    Gizmos.DrawWireSphere(_wheelConnect2, 0.01f);
                }
            }
        }
    }
}