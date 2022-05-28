using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(VehicleParent))]
    [RequireComponent(typeof(VehicleManager))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Damage/Vehicle Damage", 0)]
    public class VehicleDamage : MonoBehaviour, IVehicleComponent
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        [Range(0, 1)]
        public float strength;

        [Tooltip("")]
        public float damageFactor = 1;

        [Tooltip("")]
        public float maxCollisionMagnitude = 100;

        [Tooltip("Maximum collision points to use when deforming, has large effect on performance")]
        public int maxCollisionPoints = 2;

        [Tooltip("Collisions underneath this local y-position will be ignored")]
        public float collisionIgnoreHeight;

        [Tooltip("If true, grounded wheels will not be damaged, but can still be displaced")]
        public bool ignoreGroundedWheels;

        [Tooltip("Minimum time in seconds between collisions")]
        public float collisionTimeGap = 0.1f;

        [Tooltip("Whether the edges of adjacent deforming parts should match")]
        public bool seamlessDeform;

        [Tooltip("Add some perlin noise to deformation")]
        public bool usePerlinNoise = true;

        [Tooltip("Recalculate normals of deformed meshes")]
        public bool calculateNormals = true;

        [Tooltip("Parts that are damaged")]
        public Transform[] damageParts;

        [Tooltip("Meshes that are deformed")]
        public MeshFilter[] deformMeshes;

        [Tooltip("Mesh colliders that are deformed (Poor performance, must be convex)")]
        public MeshCollider[] deformColliders;

        [Tooltip("Parts that are displaced")]
        public Transform[] displaceParts;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Rigidbody _rb;
        private VehicleParent _vp;
        private VehicleManager _vm;
        private float _hitTime;
        private bool[] _damagedMeshes;
        private Mesh[] _tempMeshes;
        private meshVerts[] _meshVertices;
        private bool[] _damagedCols;
        private Mesh[] _tempCols;
        private meshVerts[] _colVertices;
        private Vector3[] _initialPartPositions;
        private ContactPoint _nullContact = new ContactPoint();
        
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
            // Decrease timer for collisionTimeGap
            _hitTime = Mathf.Max(0, _hitTime - _vm.tickDelta);
            // Make sure damageFactor is not negative
            damageFactor = Mathf.Max(0, damageFactor);
        }

        // Damage application from collision contact point
        public void ApplyDamage(ContactPoint colPoint, Vector3 colVel)
        {
            DamageApplication(colPoint.point, colVel, Mathf.Infinity, colPoint.normal, colPoint, true);
            FinalizeDamage();
        }

        // Same as above, but with extra float for clamping collision force
        public void ApplyDamage(ContactPoint colPoint, Vector3 colVel, float damageForceLimit)
        {
            DamageApplication(colPoint.point, colVel, damageForceLimit, colPoint.normal, colPoint, true);
            FinalizeDamage();
        }

        // Damage application from source other than collisions, e.g., an explosion
        public void ApplyDamage(Vector3 damagePoint, Vector3 damageForce)
        {
            DamageApplication(damagePoint, damageForce, Mathf.Infinity, damageForce.normalized, _nullContact, false);
            FinalizeDamage();
        }

        // Same as above, but with extra float for clamping damage force
        public void ApplyDamage(Vector3 damagePoint, Vector3 damageForce, float damageForceLimit)
        {
            DamageApplication(damagePoint, damageForce, damageForceLimit, damageForce.normalized, _nullContact, false);
            FinalizeDamage();
        }

        // Damage application from array of points
        public void ApplyDamage(Vector3[] damagePoints, Vector3 damageForce)
        {
            foreach (Vector3 curDamagePoint in damagePoints)
            {
                DamageApplication(curDamagePoint, damageForce, Mathf.Infinity, damageForce.normalized, _nullContact, false);
            }

            FinalizeDamage();
        }

        // Damage application from array of points, but with extra float for clamping damage force
        public void ApplyDamage(Vector3[] damagePoints, Vector3 damageForce, float damageForceLimit)
        {
            foreach (Vector3 curDamagePoint in damagePoints)
            {
                DamageApplication(curDamagePoint, damageForce, damageForceLimit, damageForce.normalized, _nullContact, false);
            }

            FinalizeDamage();
        }

        public void Repair()
        {
            // Fix damaged parts
            for (int i = 0; i < damageParts.Length; i++)
            {
                if (damageParts[i].GetComponent<Motor>())
                {
                    damageParts[i].GetComponent<Motor>().health = 1;
                }

                if (damageParts[i].GetComponent<Transmission>())
                {
                    damageParts[i].GetComponent<Transmission>().health = 1;
                }
            }

            // Restore deformed meshes
            for (int i = 0; i < deformMeshes.Length; i++)
            {
                for (int j = 0; j < _meshVertices[i].verts.Length; j++)
                {
                    _meshVertices[i].verts[j] = _meshVertices[i].initialVerts[j];
                }

                _tempMeshes[i].vertices = _meshVertices[i].verts;
                _tempMeshes[i].RecalculateNormals();
                _tempMeshes[i].RecalculateBounds();

                // Fix shattered parts
                ShatterPart fixedShatter = deformMeshes[i].GetComponent<ShatterPart>();
                if (fixedShatter)
                {
                    fixedShatter.shattered = false;

                    if (fixedShatter.brokenMaterial)
                    {
                        fixedShatter.rend.sharedMaterial = fixedShatter.initialMat;
                    }
                    else
                    {
                        fixedShatter.rend.enabled = true;
                    }
                }
            }

            // Restore deformed mesh colliders
            for (int i = 0; i < deformColliders.Length; i++)
            {
                for (int j = 0; j < _colVertices[i].verts.Length; j++)
                {
                    _colVertices[i].verts[j] = _colVertices[i].initialVerts[j];
                }

                _tempCols[i].vertices = _colVertices[i].verts;
                deformColliders[i].sharedMesh = null;
                deformColliders[i].sharedMesh = _tempCols[i];
            }

            // Fix displaced parts
            Suspension fixedSus;
            Transform curDisplacePart;
            for (int i = 0; i < displaceParts.Length; i++)
            {
                curDisplacePart = displaceParts[i];
                curDisplacePart.localPosition = _initialPartPositions[i];

                if (curDisplacePart.GetComponent<DetachablePart>())
                {
                    curDisplacePart.GetComponent<DetachablePart>().Reattach();
                }
                else if (curDisplacePart.parent.GetComponent<DetachablePart>())
                {
                    curDisplacePart.parent.GetComponent<DetachablePart>().Reattach();
                }

                fixedSus = curDisplacePart.GetComponent<Suspension>();
                if (fixedSus)
                {
                    curDisplacePart.localRotation = fixedSus.initialRotation;
                    fixedSus.jammed = false;

                    foreach (SuspensionPart curPart in fixedSus.movingParts)
                    {
                        if (curPart.connectObj && !curPart.isHub && !curPart.solidAxle)
                        {
                            if (!curPart.connectObj.GetComponent<SuspensionPart>())
                            {
                                curPart.connectPoint = curPart.initialConnectPoint;
                            }
                        }
                    }
                }
            }

            // Fix wheels
            foreach (Wheel curWheel in _vp.wheels)
            {
                curWheel.Reattach();
                curWheel.FixTire();
                curWheel.damage = 0;
            }

            // Fix hover wheels
            foreach (HoverWheel curHoverWheel in _vp.hoverWheels)
            {
                curHoverWheel.Reattach();
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vp = GetComponent<VehicleParent>();
            _vm = GetComponent<VehicleManager>();
            _vm.RegisterVehicleComponent(this, 50);
        }

        private void Start()
        {
            // Tell VehicleParent not to play crashing sounds because this script takes care of it
            _vp.playCrashSounds = false;
            _vp.playCrashSparks = false;

            // Set up mesh data
            _tempMeshes = new Mesh[deformMeshes.Length];
            _damagedMeshes = new bool[deformMeshes.Length];
            _meshVertices = new meshVerts[deformMeshes.Length];
            for (int i = 0; i < deformMeshes.Length; i++)
            {
                _tempMeshes[i] = deformMeshes[i].mesh;
                _meshVertices[i] = new meshVerts();
                _meshVertices[i].verts = deformMeshes[i].mesh.vertices;
                _meshVertices[i].initialVerts = deformMeshes[i].mesh.vertices;
                _damagedMeshes[i] = false;
            }

            // Set up mesh collider data
            _tempCols = new Mesh[deformColliders.Length];
            _damagedCols = new bool[deformColliders.Length];
            _colVertices = new meshVerts[deformColliders.Length];
            for (int i = 0; i < deformColliders.Length; i++)
            {
                _tempCols[i] = (Mesh)Instantiate(deformColliders[i].sharedMesh);
                _colVertices[i] = new meshVerts();
                _colVertices[i].verts = deformColliders[i].sharedMesh.vertices;
                _colVertices[i].initialVerts = deformColliders[i].sharedMesh.vertices;
                _damagedCols[i] = false;
            }

            // Set initial positions for displaced parts
            _initialPartPositions = new Vector3[displaceParts.Length];
            for (int i = 0; i < displaceParts.Length; i++)
            {
                _initialPartPositions[i] = displaceParts[i].localPosition;
            }
        }

        // Apply damage on collision
        private void OnCollisionEnter(Collision col)
        {
            if (_hitTime == 0 && col.relativeVelocity.sqrMagnitude * damageFactor > 1 && strength < 1)
            {
                Vector3 normalizedVel = col.relativeVelocity.normalized;
                int colsChecked = 0;
                bool soundPlayed = false;
                bool sparkPlayed = false;
                _hitTime = collisionTimeGap;

                foreach (ContactPoint curCol in col.contacts)
                {
                    if (transform.InverseTransformPoint(curCol.point).y > collisionIgnoreHeight && GlobalControl.damageMaskStatic == (GlobalControl.damageMaskStatic | (1 << curCol.otherCollider.gameObject.layer)))
                    {
                        colsChecked++;

                        // Play crash sound
                        if (_vp.crashSnd && _vp.crashClips.Length > 0 && !soundPlayed)
                        {
                            _vp.crashSnd.PlayOneShot(_vp.crashClips[Random.Range(0, _vp.crashClips.Length)], Mathf.Clamp01(col.relativeVelocity.magnitude * 0.1f));
                            soundPlayed = true;
                        }

                        // Play crash sparks
                        if (_vp.sparks && !sparkPlayed)
                        {
                            _vp.sparks.transform.position = curCol.point;
                            _vp.sparks.transform.rotation = Quaternion.LookRotation(normalizedVel, curCol.normal);
                            _vp.sparks.Play();
                            sparkPlayed = true;
                        }

                        DamageApplication(curCol.point, col.relativeVelocity, maxCollisionMagnitude, curCol.normal, curCol, true);
                    }

                    // Stop checking collision points when limit reached
                    if (colsChecked >= maxCollisionPoints)
                    {
                        break;
                    }
                }

                FinalizeDamage();
            }
        }

        // Where the damage is actually applied
        private void DamageApplication(Vector3 damagePoint, Vector3 damageForce, float damageForceLimit, Vector3 surfaceNormal, ContactPoint colPoint, bool useContactPoint)
        {
            float colMag = Mathf.Min(damageForce.magnitude, maxCollisionMagnitude) * (1 - strength) * damageFactor; // Magnitude of collision
            float clampedColMag = Mathf.Pow(Mathf.Sqrt(colMag) * 0.5f, 1.5f); // Clamped magnitude of collision
            Vector3 clampedVel = Vector3.ClampMagnitude(damageForce, damageForceLimit); // Clamped velocity of collision
            Vector3 normalizedVel = damageForce.normalized;
            float surfaceDot; // Dot production of collision velocity and surface normal
            float massFactor = 1; // Multiplier for damage based on mass of other rigidbody
            Transform curDamagePart;
            float damagePartFactor;
            MeshFilter curDamageMesh;
            Transform curDisplacePart;
            Transform seamKeeper = null; // Transform for maintaining seams on shattered parts
            Vector3 seamLocalPoint;
            Vector3 vertProjection;
            Vector3 translation;
            Vector3 clampedTranslation;
            Vector3 localPos;
            float vertDist;
            float distClamp;
            DetachablePart detachedPart;
            Suspension damagedSus;

            // Get mass factor for multiplying damage
            if (useContactPoint)
            {
                damagePoint = colPoint.point;
                surfaceNormal = colPoint.normal;

                if (colPoint.otherCollider.attachedRigidbody)
                {
                    massFactor = Mathf.Clamp01(colPoint.otherCollider.attachedRigidbody.mass / _rb.mass);
                }
            }

            surfaceDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal, normalizedVel)) * (Vector3.Dot((transform.position - damagePoint).normalized, normalizedVel) + 1) * 0.5f;

            // Damage damageable parts
            for (int i = 0; i < damageParts.Length; i++)
            {
                curDamagePart = damageParts[i];
                damagePartFactor = colMag * surfaceDot * massFactor * Mathf.Min(clampedColMag * 0.01f, (clampedColMag * 0.001f) / Mathf.Pow(Vector3.Distance(curDamagePart.position, damagePoint), clampedColMag));

                // Damage motors
                Motor damagedMotor = curDamagePart.GetComponent<Motor>();
                if (damagedMotor)
                {
                    damagedMotor.health -= damagePartFactor * (1 - damagedMotor.strength);
                }

                // Damage transmissions
                Transmission damagedTransmission = curDamagePart.GetComponent<Transmission>();
                if (damagedTransmission)
                {
                    damagedTransmission.health -= damagePartFactor * (1 - damagedTransmission.strength);
                }
            }

            // Deform meshes
            for (int i = 0; i < deformMeshes.Length; i++)
            {
                curDamageMesh = deformMeshes[i];
                localPos = curDamageMesh.transform.InverseTransformPoint(damagePoint);
                translation = curDamageMesh.transform.InverseTransformDirection(clampedVel);
                clampedTranslation = Vector3.ClampMagnitude(translation, clampedColMag);

                // Shatter parts that can shatter
                ShatterPart shattered = curDamageMesh.GetComponent<ShatterPart>();
                if (shattered)
                {
                    seamKeeper = shattered.seamKeeper;
                    if (Vector3.Distance(curDamageMesh.transform.position, damagePoint) < colMag * surfaceDot * 0.1f * massFactor && colMag * surfaceDot * massFactor > shattered.breakForce)
                    {
                        shattered.Shatter();
                    }
                }

                // Actual deformation
                if (translation.sqrMagnitude > 0 && strength < 1)
                {
                    for (int j = 0; j < _meshVertices[i].verts.Length; j++)
                    {
                        vertDist = (_meshVertices[i].verts[j] - localPos).sqrMagnitude;
                        distClamp = (clampedColMag * 0.001f) / Mathf.Pow(vertDist, clampedColMag * 0.5f);

                        if (distClamp > 0.001f)
                        {
                            _damagedMeshes[i] = true;
                            if (seamKeeper == null || seamlessDeform)
                            {
                                vertProjection = seamlessDeform ? Vector3.zero : Vector3.Project(normalizedVel, _meshVertices[i].verts[j]);
                                _meshVertices[i].verts[j] += (clampedTranslation - vertProjection * (usePerlinNoise ? 1 + Mathf.PerlinNoise(_meshVertices[i].verts[j].x * 100, _meshVertices[i].verts[j].y * 100) : 1)) * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor;
                            }
                            else
                            {
                                seamLocalPoint = seamKeeper.InverseTransformPoint(curDamageMesh.transform.TransformPoint(_meshVertices[i].verts[j]));
                                _meshVertices[i].verts[j] += (clampedTranslation - Vector3.Project(normalizedVel, seamLocalPoint) * (usePerlinNoise ? 1 + Mathf.PerlinNoise(seamLocalPoint.x * 100, seamLocalPoint.y * 100) : 1)) * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor;
                            }
                        }
                    }
                }
            }

            seamKeeper = null;

            // Deform mesh colliders
            for (int i = 0; i < deformColliders.Length; i++)
            {
                localPos = deformColliders[i].transform.InverseTransformPoint(damagePoint);
                translation = deformColliders[i].transform.InverseTransformDirection(clampedVel);
                clampedTranslation = Vector3.ClampMagnitude(translation, clampedColMag);

                if (translation.sqrMagnitude > 0 && strength < 1)
                {
                    for (int j = 0; j < _colVertices[i].verts.Length; j++)
                    {
                        vertDist = (_colVertices[i].verts[j] - localPos).sqrMagnitude;
                        distClamp = (clampedColMag * 0.001f) / Mathf.Pow(vertDist, clampedColMag * 0.5f);

                        if (distClamp > 0.001f)
                        {
                            _damagedCols[i] = true;
                            _colVertices[i].verts[j] += clampedTranslation * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor;
                        }
                    }
                }
            }

            // Displace parts
            for (int i = 0; i < displaceParts.Length; i++)
            {
                curDisplacePart = displaceParts[i];
                translation = clampedVel;
                clampedTranslation = Vector3.ClampMagnitude(translation, clampedColMag);

                if (translation.sqrMagnitude > 0 && strength < 1)
                {
                    vertDist = (curDisplacePart.position - damagePoint).sqrMagnitude;
                    distClamp = (clampedColMag * 0.001f) / Mathf.Pow(vertDist, clampedColMag * 0.5f);

                    if (distClamp > 0.001f)
                    {
                        curDisplacePart.position += clampedTranslation * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor;

                        // Detach detachable parts
                        if (curDisplacePart.GetComponent<DetachablePart>())
                        {
                            detachedPart = curDisplacePart.GetComponent<DetachablePart>();

                            if (colMag * surfaceDot * massFactor > detachedPart.looseForce && detachedPart.looseForce >= 0)
                            {
                                detachedPart.initialPos = curDisplacePart.localPosition;
                                detachedPart.Detach(true);
                            }
                            else if (colMag * surfaceDot * massFactor > detachedPart.breakForce)
                            {
                                detachedPart.Detach(false);
                            }
                        }
                        // Maybe the parent of this part is what actually detaches, useful for displacing compound colliders that represent single detachable objects
                        else if (curDisplacePart.parent.GetComponent<DetachablePart>())
                        {
                            detachedPart = curDisplacePart.parent.GetComponent<DetachablePart>();

                            if (!detachedPart.detached)
                            {
                                if (colMag * surfaceDot * massFactor > detachedPart.looseForce && detachedPart.looseForce >= 0)
                                {
                                    detachedPart.initialPos = curDisplacePart.parent.localPosition;
                                    detachedPart.Detach(true);
                                }
                                else if (colMag * surfaceDot * massFactor > detachedPart.breakForce)
                                {
                                    detachedPart.Detach(false);
                                }
                            }
                            else if (detachedPart.hinge)
                            {
                                detachedPart.displacedAnchor += curDisplacePart.parent.InverseTransformDirection(clampedTranslation * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor);
                            }
                        }

                        // Damage suspensions and wheels
                        damagedSus = curDisplacePart.GetComponent<Suspension>();
                        if (damagedSus)
                        {
                            if ((!damagedSus.wheel.grounded && ignoreGroundedWheels) || !ignoreGroundedWheels)
                            {
                                curDisplacePart.RotateAround(damagedSus.transform.TransformPoint(damagedSus.damagePivot), Vector3.ProjectOnPlane(damagePoint - curDisplacePart.position, -translation.normalized), clampedColMag * surfaceDot * distClamp * 20 * massFactor);

                                damagedSus.wheel.damage += clampedColMag * surfaceDot * distClamp * 10 * massFactor;

                                if (clampedColMag * surfaceDot * distClamp * 10 * massFactor > damagedSus.jamForce)
                                {
                                    damagedSus.jammed = true;
                                }

                                if (clampedColMag * surfaceDot * distClamp * 10 * massFactor > damagedSus.wheel.detachForce)
                                {
                                    damagedSus.wheel.Detach();
                                }

                                foreach (SuspensionPart curPart in damagedSus.movingParts)
                                {
                                    if (curPart.connectObj && !curPart.isHub && !curPart.solidAxle)
                                    {
                                        if (!curPart.connectObj.GetComponent<SuspensionPart>())
                                        {
                                            curPart.connectPoint += curPart.connectObj.InverseTransformDirection(clampedTranslation * surfaceDot * Mathf.Min(clampedColMag * 0.01f, distClamp) * massFactor);
                                        }
                                    }
                                }
                            }
                        }

                        // Damage hover wheels
                        HoverWheel damagedHoverWheel = curDisplacePart.GetComponent<HoverWheel>();
                        if (damagedHoverWheel)
                        {
                            if ((!damagedHoverWheel.grounded && ignoreGroundedWheels) || !ignoreGroundedWheels)
                            {
                                if (clampedColMag * surfaceDot * distClamp * 10 * massFactor > damagedHoverWheel.detachForce)
                                {
                                    damagedHoverWheel.Detach();
                                }
                            }
                        }
                    }
                }
            }
        }

        // Apply damage to meshes
        private void FinalizeDamage()
        {
            // Apply vertices to actual meshes
            for (int i = 0; i < deformMeshes.Length; i++)
            {
                if (_damagedMeshes[i])
                {
                    _tempMeshes[i].vertices = _meshVertices[i].verts;

                    if (calculateNormals)
                    {
                        _tempMeshes[i].RecalculateNormals();
                    }

                    _tempMeshes[i].RecalculateBounds();
                }

                _damagedMeshes[i] = false;
            }

            // Apply vertices to actual mesh colliders
            for (int i = 0; i < deformColliders.Length; i++)
            {
                if (_damagedCols[i])
                {
                    _tempCols[i].vertices = _colVertices[i].verts;
                    deformColliders[i].sharedMesh = null;
                    deformColliders[i].sharedMesh = _tempCols[i];
                }

                _damagedCols[i] = false;
            }
        }

        // Draw collisionIgnoreHeight gizmos
        private void OnDrawGizmosSelected()
        {
            Vector3 startPoint = transform.TransformPoint(Vector3.up * collisionIgnoreHeight);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(startPoint, transform.forward);
            Gizmos.DrawRay(startPoint, -transform.forward);
            Gizmos.DrawRay(startPoint, transform.right);
            Gizmos.DrawRay(startPoint, -transform.right);
        }

        // Destroy loose parts
        private void OnDestroy()
        {
            foreach (Transform curPart in displaceParts)
            {
                if (curPart)
                {
                    if (curPart.GetComponent<DetachablePart>() && curPart.parent == null)
                    {
                        Destroy(curPart.gameObject);
                    }
                    else if (curPart.parent.GetComponent<DetachablePart>() && curPart.parent.parent == null)
                    {
                        Destroy(curPart.parent.gameObject);
                    }
                }
            }
        }
    }

    // Class for easier mesh data manipulation
    internal class meshVerts
    {
        public Vector3[] verts; // Current mesh vertices
        public Vector3[] initialVerts; // Original mesh vertices
    }
}