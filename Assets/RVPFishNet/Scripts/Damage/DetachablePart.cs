using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Damage/Detachable Part", 1)]
    public class DetachablePart : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public float mass = 0.1f;

        [Tooltip("")] 
        public float drag;

        [Tooltip("")] 
        public float angularDrag = 0.05f;

        [Tooltip("")] 
        public float looseForce = -1;

        [Tooltip("")] 
        public float breakForce = 25;

        [Tooltip("A hinge joint is randomly chosen from the list to use")]
        public PartJoint[] joints;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public HingeJoint hinge;

        [System.NonSerialized]
        public bool detached;

        [System.NonSerialized]
        public Vector3 initialPos;

        [System.NonSerialized]
        public Vector3 displacedAnchor;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Transform _tr;
        private Rigidbody _rb;
        private Rigidbody _parentBody;
        private Transform _initialParent;
        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private Vector3 _initialAnchor;

        public void Detach(bool makeJoint)
        {
            if (!detached)
            {
                detached = true;
                _tr.parent = null;
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.mass = mass;
                _rb.drag = drag;
                _rb.angularDrag = angularDrag;

                if (_parentBody)
                {
                    _parentBody.mass -= mass;
                    _rb.velocity = _parentBody.GetPointVelocity(_tr.position);
                    _rb.angularVelocity = _parentBody.angularVelocity;

                    // Pick a random hinge joint to use
                    if (makeJoint && joints.Length > 0)
                    {
                        PartJoint chosenJoint = joints[Random.Range(0, joints.Length)];
                        _initialAnchor = chosenJoint.hingeAnchor;
                        displacedAnchor = _initialAnchor;

                        hinge = gameObject.AddComponent<HingeJoint>();
                        hinge.autoConfigureConnectedAnchor = false;
                        hinge.connectedBody = _parentBody;
                        hinge.anchor = chosenJoint.hingeAnchor;
                        hinge.axis = chosenJoint.hingeAxis;
                        hinge.connectedAnchor = initialPos + chosenJoint.hingeAnchor;
                        hinge.enableCollision = false;
                        hinge.useLimits = chosenJoint.useLimits;

                        JointLimits limits = new JointLimits();
                        limits.min = chosenJoint.minLimit;
                        limits.max = chosenJoint.maxLimit;
                        limits.bounciness = chosenJoint.bounciness;
                        hinge.limits = limits;
                        hinge.useSpring = chosenJoint.useSpring;

                        JointSpring spring = new JointSpring();
                        spring.targetPosition = chosenJoint.springTargetPosition;
                        spring.spring = chosenJoint.springForce;
                        spring.damper = chosenJoint.springDamper;
                        hinge.spring = spring;
                        hinge.breakForce = breakForce;
                        hinge.breakTorque = breakForce;
                    }
                }
            }
        }

        public void Reattach()
        {
            if (detached)
            {
                detached = false;
                _tr.parent = _initialParent;
                _tr.localPosition = _initialLocalPos;
                _tr.localRotation = _initialLocalRot;

                if (_parentBody)
                {
                    _parentBody.mass += mass;
                }

                if (hinge)
                {
                    Destroy(hinge);
                }

                if (_rb)
                {
                    Destroy(_rb);
                }
            }
        }

        private void Start()
        {
            _tr = transform;

            if (_tr.parent)
            {
                _initialParent = _tr.parent;
                _initialLocalPos = _tr.localPosition;
                _initialLocalRot = _tr.localRotation;
            }

            _parentBody = _tr.GetTopmostParentComponent<Rigidbody>();
            initialPos = _tr.localPosition;
        }

        private void Update()
        {
            if (hinge)
            {
                // Destory hinge if displaced too far from original position
                if ((_initialAnchor - displacedAnchor).sqrMagnitude > 0.1f)
                {
                    Destroy(hinge);
                }
            }
        }

        // Draw joint gizmos
        private void OnDrawGizmosSelected()
        {
            if (!_tr)
            {
                _tr = transform;
            }

            if (looseForce >= 0 && joints.Length > 0)
            {
                Gizmos.color = Color.red;
                foreach (PartJoint curJoint in joints)
                {
                    Gizmos.DrawRay(_tr.TransformPoint(curJoint.hingeAnchor), _tr.TransformDirection(curJoint.hingeAxis).normalized * 0.2f);
                    Gizmos.DrawWireSphere(_tr.TransformPoint(curJoint.hingeAnchor), 0.02f);
                }
            }
        }
    }

    // Class for storing hinge joint information in the joints list
    [System.Serializable]
    public class PartJoint
    {
        public Vector3 hingeAnchor;
        public Vector3 hingeAxis = Vector3.right;
        public bool useLimits;
        public float minLimit;
        public float maxLimit;
        public float bounciness;
        public bool useSpring;
        public float springTargetPosition;
        public float springForce;
        public float springDamper;
    }
}