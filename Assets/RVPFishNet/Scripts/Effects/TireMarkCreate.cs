using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Wheel))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Effects/Tire Mark Creator", 0)]
    public class TireMarkCreate : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("How much the tire must slip before marks are created")]
        public float slipThreshold;

        [Tooltip("")]
        public bool calculateTangents = true;

        [Tooltip("Materials in array correspond to indices in surface types in GroundSurfaceMaster")]
        public Material[] tireMarkMaterials;

        [Tooltip("Materials in array correspond to indices in surface types in GroundSurfaceMaster")]
        public Material[] rimMarkMaterials;

        [Tooltip("Particles in array correspond to indices in surface types in GroundSurfaceMaster")]
        public ParticleSystem[] debrisParticles;

        public ParticleSystem sparks;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private Transform _tr;
        private Wheel _wheel;
        private Mesh _mesh;
        private int[] _tris;
        private Vector3[] _verts;
        private Vector2[] _uvs;
        private Color[] _colors;
        private Vector3 _leftPoint;
        private Vector3 _rightPoint;
        private Vector3 _leftPointPrev;
        private Vector3 _rightPointPrev;
        private bool _creatingMark;
        private bool _continueMark; // Continue making mark after current one ends
        private GameObject _curMark; // Current mark
        private Transform _curMarkTr;
        private int _curEdge;
        private float _gapDelay; // Gap between segments
        private int _curSurface = -1; // Current surface type
        private int _prevSurface = -1; // Previous surface type
        private bool _popped = false;
        private bool _poppedPrev = false;
        private float _alwaysScrape;
        private float[] _initialEmissionRates;
        private ParticleSystem.MinMaxCurve _zeroEmission = new ParticleSystem.MinMaxCurve(0);

        private void Start()
        {
            _tr = transform;
            _wheel = GetComponent<Wheel>();

            _initialEmissionRates = new float[debrisParticles.Length + 1];
            for (int i = 0; i < debrisParticles.Length; i++)
            {
                _initialEmissionRates[i] = debrisParticles[i].emission.rateOverTime.constantMax;
            }

            if (sparks)
            {
                _initialEmissionRates[debrisParticles.Length] = sparks.emission.rateOverTime.constantMax;
            }
        }

        private void Update()
        {
            // Check for continuous marking
            if (_wheel.grounded)
            {
                _alwaysScrape = GroundSurfaceMaster.surfaceTypesStatic[_wheel.contactPoint.surfaceType].alwaysScrape ? slipThreshold + Mathf.Min(0.5f, Mathf.Abs(_wheel.rawRPM * 0.001f)) : 0;
            }
            else
            {
                _alwaysScrape = 0;
            }

            // Create mark
            if (_wheel.grounded && (Mathf.Abs(F.MaxAbs(_wheel.sidewaysSlip, _wheel.forwardSlip)) > slipThreshold || _alwaysScrape > 0) && _wheel.connected)
            {
                _prevSurface = _curSurface;
                _curSurface = _wheel.grounded ? _wheel.contactPoint.surfaceType : -1;

                _poppedPrev = _popped;
                _popped = _wheel.popped;

                if (!_creatingMark)
                {
                    _prevSurface = _curSurface;
                    StartMark();
                }
                else if (_curSurface != _prevSurface || _popped != _poppedPrev)
                {
                    EndMark();
                }

                // Calculate segment points
                if (_curMark)
                {
                    Vector3 pointDir = Quaternion.AngleAxis(90, _wheel.contactPoint.normal) * _tr.right * (_wheel.popped ? _wheel.rimWidth : _wheel.tireWidth);
                    _leftPoint = _curMarkTr.InverseTransformPoint(_wheel.contactPoint.point + pointDir * _wheel.suspensionParent.flippedSideFactor * Mathf.Sign(_wheel.rawRPM) + _wheel.contactPoint.normal * GlobalControl.tireMarkHeightStatic);
                    _rightPoint = _curMarkTr.InverseTransformPoint(_wheel.contactPoint.point - pointDir * _wheel.suspensionParent.flippedSideFactor * Mathf.Sign(_wheel.rawRPM) + _wheel.contactPoint.normal * GlobalControl.tireMarkHeightStatic);
                }
            }
            else if (_creatingMark)
            {
                EndMark();
            }

            // Update mark if it's short enough, otherwise end it
            if (_curEdge < GlobalControl.tireMarkLengthStatic && _creatingMark)
            {
                UpdateMark();
            }
            else if (_creatingMark)
            {
                EndMark();
            }

            // Set particle emission rates
            ParticleSystem.EmissionModule em;
            for (int i = 0; i < debrisParticles.Length; i++)
            {
                if (_wheel.connected)
                {
                    if (i == _wheel.contactPoint.surfaceType)
                    {
                        if (GroundSurfaceMaster.surfaceTypesStatic[_wheel.contactPoint.surfaceType].leaveSparks && _wheel.popped)
                        {
                            em = debrisParticles[i].emission;
                            em.rateOverTime = _zeroEmission;

                            if (sparks)
                            {
                                em = sparks.emission;
                                em.rateOverTime = new ParticleSystem.MinMaxCurve(_initialEmissionRates[debrisParticles.Length] * Mathf.Clamp01(Mathf.Abs(F.MaxAbs(_wheel.sidewaysSlip, _wheel.forwardSlip, _alwaysScrape)) - slipThreshold));
                            }
                        }
                        else
                        {
                            em = debrisParticles[i].emission;
                            em.rateOverTime = new ParticleSystem.MinMaxCurve(_initialEmissionRates[i] * Mathf.Clamp01(Mathf.Abs(F.MaxAbs(_wheel.sidewaysSlip, _wheel.forwardSlip, _alwaysScrape)) - slipThreshold));

                            if (sparks)
                            {
                                em = sparks.emission;
                                em.rateOverTime = _zeroEmission;
                            }
                        }
                    }
                    else
                    {
                        em = debrisParticles[i].emission;
                        em.rateOverTime = _zeroEmission;
                    }
                }
                else
                {
                    em = debrisParticles[i].emission;
                    em.rateOverTime = _zeroEmission;

                    if (sparks)
                    {
                        em = sparks.emission;
                        em.rateOverTime = _zeroEmission;
                    }
                }
            }
        }

        // Start creating a mark
        private void StartMark()
        {
            _creatingMark = true;
            _curMark = new GameObject("Tire Mark");
            _curMarkTr = _curMark.transform;
            _curMarkTr.parent = _wheel.contactPoint.gameObject.transform;
            _curMark.AddComponent<TireMark>();
            MeshRenderer tempRend = _curMark.AddComponent<MeshRenderer>();

            // Set material based on whether the tire is popped
            if (_wheel.popped)
            {
                tempRend.sharedMaterial = rimMarkMaterials[Mathf.Min(_wheel.contactPoint.surfaceType, rimMarkMaterials.Length - 1)];
            }
            else
            {
                tempRend.sharedMaterial = tireMarkMaterials[Mathf.Min(_wheel.contactPoint.surfaceType, tireMarkMaterials.Length - 1)];
            }

            tempRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mesh = _curMark.AddComponent<MeshFilter>().mesh;
            _verts = new Vector3[GlobalControl.tireMarkLengthStatic * 2];
            _tris = new int[GlobalControl.tireMarkLengthStatic * 3];

            if (_continueMark)
            {
                _verts[0] = _leftPointPrev;
                _verts[1] = _rightPointPrev;

                _tris[0] = 0;
                _tris[1] = 3;
                _tris[2] = 1;
                _tris[3] = 0;
                _tris[4] = 2;
                _tris[5] = 3;
            }

            _uvs = new Vector2[_verts.Length];
            _uvs[0] = new Vector2(0, 0);
            _uvs[1] = new Vector2(1, 0);
            _uvs[2] = new Vector2(0, 1);
            _uvs[3] = new Vector2(1, 1);

            _colors = new Color[_verts.Length];
            _colors[0].a = 0;
            _colors[1].a = 0;

            _curEdge = 2;
            _gapDelay = GlobalControl.tireMarkGapStatic;
        }

        // Update mark currently being generated
        private void UpdateMark()
        {
            if (_gapDelay == 0)
            {
                float alpha = (_curEdge < GlobalControl.tireMarkLengthStatic - 2 && _curEdge > 5 ? 1 : 0) *
                    Random.Range(
                        Mathf.Clamp01(Mathf.Abs(F.MaxAbs(_wheel.sidewaysSlip, _wheel.forwardSlip, _alwaysScrape)) - slipThreshold) * 0.9f,
                        Mathf.Clamp01(Mathf.Abs(F.MaxAbs(_wheel.sidewaysSlip, _wheel.forwardSlip, _alwaysScrape)) - slipThreshold));
                _gapDelay = GlobalControl.tireMarkGapStatic;
                _curEdge += 2;

                _verts[_curEdge] = _leftPoint;
                _verts[_curEdge + 1] = _rightPoint;

                for (int i = _curEdge + 2; i < _verts.Length; i++)
                {
                    _verts[i] = Mathf.Approximately(i * 0.5f, Mathf.Round(i * 0.5f)) ? _leftPoint : _rightPoint;
                    _colors[i].a = 0;
                }

                _tris[_curEdge * 3 - 3] = _curEdge;
                _tris[_curEdge * 3 - 2] = _curEdge + 3;
                _tris[_curEdge * 3 - 1] = _curEdge + 1;
                _tris[Mathf.Min(_curEdge * 3, _tris.Length - 1)] = _curEdge;
                _tris[Mathf.Min(_curEdge * 3 + 1, _tris.Length - 1)] = _curEdge + 2;
                _tris[Mathf.Min(_curEdge * 3 + 2, _tris.Length - 1)] = _curEdge + 3;

                _uvs[_curEdge] = new Vector2(0, _curEdge * 0.5f);
                _uvs[_curEdge + 1] = new Vector2(1, _curEdge * 0.5f);

                _colors[_curEdge] = new Color(1, 1, 1, alpha);
                _colors[_curEdge + 1] = _colors[_curEdge];

                _mesh.vertices = _verts;
                _mesh.triangles = _tris;
                _mesh.uv = _uvs;
                _mesh.colors = _colors;
                _mesh.RecalculateBounds();
                _mesh.RecalculateNormals();
            }
            else
            {
                _gapDelay = Mathf.Max(0, _gapDelay - Time.deltaTime);
                _verts[_curEdge] = _leftPoint;
                _verts[_curEdge + 1] = _rightPoint;

                for (int i = _curEdge + 2; i < _verts.Length; i++)
                {
                    _verts[i] = Mathf.Approximately(i * 0.5f, Mathf.Round(i * 0.5f)) ? _leftPoint : _rightPoint;
                    _colors[i].a = 0;
                }

                _mesh.vertices = _verts;
                _mesh.RecalculateBounds();
            }

            if (calculateTangents)
            {
                _mesh.RecalculateTangents();
            }
        }

        // Stop making mark
        private void EndMark()
        {
            _creatingMark = false;
            _leftPointPrev = _verts[Mathf.RoundToInt(_verts.Length * 0.5f)];
            _rightPointPrev = _verts[Mathf.RoundToInt(_verts.Length * 0.5f + 1)];
            _continueMark = _wheel.grounded;

            _curMark.GetComponent<TireMark>().fadeTime = GlobalControl.tireFadeTimeStatic;
            _curMark.GetComponent<TireMark>().mesh = _mesh;
            _curMark.GetComponent<TireMark>().colors = _colors;
            _curMark = null;
            _curMarkTr = null;
            _mesh = null;
        }

        // Clean up mark if destroyed while creating
        private void OnDestroy()
        {
            if (_creatingMark && _curMark)
            {
                EndMark();
            }
            else if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }

    // Class for tire mark instances
    public class TireMark : MonoBehaviour
    {
        [System.NonSerialized]
        public float fadeTime = -1;

        [System.NonSerialized]
        public Mesh mesh;

        [System.NonSerialized]
        public Color[] colors;

        private bool fading;
        private float alpha = 1;

        // Fade the tire mark and then destroy it
        private void Update()
        {
            if (fading)
            {
                if (alpha <= 0)
                {
                    Destroy(mesh);
                    Destroy(gameObject);
                }
                else
                {
                    alpha -= Time.deltaTime;

                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i].a -= Time.deltaTime;
                    }

                    mesh.colors = colors;
                }
            }
            else
            {
                if (fadeTime > 0)
                {
                    fadeTime = Mathf.Max(0, fadeTime - Time.deltaTime);
                }
                else if (fadeTime == 0)
                {
                    fading = true;
                }
            }
        }
    }
}